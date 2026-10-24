package com.talent.workflow.internal;

import java.sql.Date;
import java.time.Clock;
import java.time.LocalDate;
import java.util.List;
import java.util.Map;

import org.springframework.jdbc.core.namedparam.MapSqlParameterSource;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.talent.integration.EmployeeDirectory;
import com.talent.shared.audit.AuditService;
import com.talent.shared.security.AccessGuard;
import com.talent.shared.security.Action;
import com.talent.shared.security.CurrentUser;
import com.talent.shared.security.ObjectType;
import com.talent.shared.web.BusinessRuleException;
import com.talent.shared.web.NotFoundException;
import com.talent.workflow.HistoryView;
import com.talent.workflow.TaskView;
import com.talent.workflow.WorkflowInstanceView;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

@RestController
@RequestMapping("/api/workflow")
class WorkflowController {

    private final WorkflowEngine engine;
    private final AccessGuard access;
    private final NamedParameterJdbcTemplate jdbc;
    private final EmployeeDirectory directory;
    private final AuditService audit;
    private final Clock clock;

    WorkflowController(WorkflowEngine engine, AccessGuard access, NamedParameterJdbcTemplate jdbc, EmployeeDirectory directory,
                       AuditService audit, Clock clock) {
        this.engine = engine;
        this.access = access;
        this.jdbc = jdbc;
        this.directory = directory;
        this.audit = audit;
        this.clock = clock;
    }

    /** My inbox: open tasks, including tasks delegated to me. */
    @GetMapping("/tasks/mine")
    List<TaskView> myTasks() {
        return engine.myTasks();
    }

    /** HR view of every open task, optionally for one cycle. */
    @GetMapping("/tasks")
    List<TaskView> openTasks(@RequestParam(required = false) Long contextId) {
        access.require(Action.VIEW, ObjectType.CYCLE, null);
        return engine.openTasks(contextId);
    }

    record Reassign(@NotNull Long assigneeEmployeeId, @NotBlank String reason) {
    }

    @PostMapping("/tasks/{id}/reassign")
    TaskView reassign(@PathVariable Long id, @Valid @RequestBody Reassign r) {
        access.require(Action.EDIT, ObjectType.CYCLE, null);
        return engine.reassignTask(id, r.assigneeEmployeeId(), r.reason());
    }

    @GetMapping("/instances/{id}")
    WorkflowInstanceView instance(@PathVariable Long id) {
        WorkflowInstanceView v = engine.instance(id);
        requireViewer(v);
        return v;
    }

    @GetMapping("/instances/{id}/history")
    List<HistoryView> history(@PathVariable Long id) {
        requireViewer(engine.instance(id));
        return engine.history(id);
    }

    @GetMapping("/instances/{id}/actions")
    List<String> actions(@PathVariable Long id) {
        return engine.availableActions(id);
    }

    private void requireViewer(WorkflowInstanceView v) {
        boolean assignee = v.openTasks().stream().anyMatch(t -> t.assigneeEmployeeId().equals(CurrentUser.get().employeeId()));
        if (!assignee) {
            access.require(Action.VIEW, v.objectType(), v.subjectEmployeeId());
        }
    }

    // ---- delegation ------------------------------------------------------------------------

    @GetMapping("/delegations")
    List<Map<String, Object>> delegations() {
        Long me = CurrentUser.get().employeeId();
        return jdbc.queryForList("""
                select d.*, a.display_name as delegator_name, b.display_name as delegate_name
                from delegation d join employee a on a.id = d.delegator_id join employee b on b.id = d.delegate_id
                where d.delegator_id = :me or d.delegate_id = :me order by d.valid_from desc""", Map.of("me", me));
    }

    record DelegationRequest(@NotNull Long delegateEmployeeId, @NotNull LocalDate validFrom, @NotNull LocalDate validTo, String reason) {
    }

    /** Hand my tasks to a deputy for a date range (§3.1 rule 6). */
    @PostMapping("/delegations")
    @Transactional
    Map<String, Object> delegate(@Valid @RequestBody DelegationRequest r) {
        Long me = CurrentUser.get().employeeId();
        if (r.delegateEmployeeId().equals(me)) {
            throw new BusinessRuleException("DELEGATE_SELF", "You cannot delegate to yourself");
        }
        if (r.validTo().isBefore(r.validFrom()) || r.validTo().isBefore(LocalDate.now(clock))) {
            throw new BusinessRuleException("DELEGATE_DATES", "The end date must be today or later, and after the start date");
        }
        if (!directory.get(r.delegateEmployeeId()).status().isActive()) {
            throw new BusinessRuleException("DELEGATE_INACTIVE", "The deputy is not an active employee");
        }
        jdbc.update("""
                insert into delegation (delegator_id, delegate_id, valid_from, valid_to, reason, created_at)
                values (:a, :b, :f, :t, :r, now())""", new MapSqlParameterSource().addValue("a", me).addValue("b", r.delegateEmployeeId())
                .addValue("f", Date.valueOf(r.validFrom())).addValue("t", Date.valueOf(r.validTo())).addValue("r", r.reason()));
        audit.record("DELEGATION_CREATED", "DELEGATION", me, null, null, r, r.reason());
        return Map.of("ok", true);
    }

    @DeleteMapping("/delegations/{id}")
    @Transactional
    void endDelegation(@PathVariable Long id) {
        Long me = CurrentUser.get().employeeId();
        int n = jdbc.update("update delegation set valid_to = :y where id = :id and delegator_id = :me",
                Map.of("y", Date.valueOf(LocalDate.now(clock).minusDays(1)), "id", id, "me", me));
        if (n == 0) {
            throw new NotFoundException("Delegation", id);
        }
        audit.record("DELEGATION_ENDED", "DELEGATION", id, null, null, null, null);
    }
}
