package com.talent.integration.internal;

import java.util.List;
import java.util.Map;

import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.talent.integration.CompetencyView;
import com.talent.integration.CourseView;
import com.talent.integration.EmployeeDirectory;
import com.talent.integration.EmployeeView;
import com.talent.integration.EnrolmentView;
import com.talent.integration.JobView;
import com.talent.integration.OrgUnitView;
import com.talent.integration.PositionView;
import com.talent.integration.ResumeView;
import com.talent.integration.TalentDataQuery;
import com.talent.shared.audit.AuditService;
import com.talent.shared.security.AccessGuard;
import com.talent.shared.security.Action;
import com.talent.shared.security.CurrentUser;
import com.talent.shared.security.ObjectType;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

/**
 * Directory look-ups (INTERNAL data, any signed-in user), resume data (scoped), and the admin sync dashboard.
 */
@RestController
@RequestMapping("/api")
class IntegrationController {

    private final EmployeeDirectory directory;
    private final TalentDataQuery data;
    private final SyncService sync;
    private final AccessGuard access;
    private final AuditService audit;
    private final NamedParameterJdbcTemplate jdbc;

    IntegrationController(EmployeeDirectory directory, TalentDataQuery data, SyncService sync, AccessGuard access,
                          AuditService audit, NamedParameterJdbcTemplate jdbc) {
        this.directory = directory;
        this.data = data;
        this.sync = sync;
        this.access = access;
        this.audit = audit;
        this.jdbc = jdbc;
    }

    // ---- directory ------------------------------------------------------------------------

    @GetMapping("/me")
    EmployeeView me() {
        return directory.get(CurrentUser.get().employeeId());
    }

    @GetMapping("/employees")
    List<EmployeeView> search(@RequestParam(required = false) String q, @RequestParam(required = false) Long orgUnitId,
                              @RequestParam(defaultValue = "50") int limit) {
        return directory.search(q, orgUnitId, limit);
    }

    @GetMapping("/employees/{id}")
    EmployeeView employee(@PathVariable Long id) {
        return directory.get(id);
    }

    @GetMapping("/employees/{id}/resume")
    ResumeView resume(@PathVariable Long id) {
        access.require(Action.VIEW, ObjectType.EMPLOYEE, id);
        return data.resume(id);
    }

    @GetMapping("/employees/{id}/enrolments")
    List<EnrolmentView> enrolments(@PathVariable Long id) {
        access.require(Action.VIEW, ObjectType.EMPLOYEE, id);
        return data.enrolments(id);
    }

    @GetMapping("/org-units")
    List<OrgUnitView> orgUnits() {
        return directory.orgUnits();
    }

    @GetMapping("/positions")
    List<PositionView> positions(@RequestParam(required = false) String type) {
        return directory.positions(type);
    }

    @GetMapping("/jobs")
    List<JobView> jobs() {
        return directory.jobs();
    }

    @GetMapping("/competencies")
    List<CompetencyView> competencies() {
        return directory.competencies();
    }

    @GetMapping("/courses")
    List<CourseView> courses(@RequestParam(required = false) String mappingType, @RequestParam(required = false) String code) {
        return mappingType != null && code != null ? data.coursesFor(mappingType, code) : data.courses();
    }

    // ---- sync dashboard (ADR-08) ----------------------------------------------------------

    @GetMapping("/integration/runs")
    List<Map<String, Object>> runs() {
        access.require(Action.VIEW, ObjectType.SYNC, null);
        return jdbc.queryForList("select * from sync_run order by id desc limit 100", Map.of());
    }

    @GetMapping("/integration/dead-letters")
    List<Map<String, Object>> deadLetters(@RequestParam(defaultValue = "OPEN") String status) {
        access.require(Action.VIEW, ObjectType.SYNC, null);
        return jdbc.queryForList("""
                select id, system, entity, external_id, cast(payload as text) as payload, error, attempts, status,
                       first_failed_at, last_failed_at, resolved_at
                from sync_dead_letter where status = :s order by last_failed_at desc limit 500""", Map.of("s", status));
    }

    @PostMapping("/integration/sync")
    List<SyncService.RunResult> syncAll() {
        access.require(Action.EDIT, ObjectType.SYNC, null);
        return sync.syncAll("MANUAL");
    }

    @PostMapping("/integration/sync/{system}")
    SyncService.RunResult syncOne(@PathVariable String system) {
        access.require(Action.EDIT, ObjectType.SYNC, null);
        return sync.sync(system.toUpperCase(), "MANUAL");
    }

    @PostMapping("/integration/dead-letters/{id}/retry")
    Map<String, Object> retry(@PathVariable Long id) {
        access.require(Action.EDIT, ObjectType.SYNC, null);
        return Map.of("loaded", sync.retry(id));
    }

    /** On-demand refresh of one employee from all source systems. */
    @PostMapping("/employees/{id}/refresh")
    Map<String, Object> refresh(@PathVariable Long id) {
        access.require(Action.VIEW, ObjectType.EMPLOYEE, id);
        return Map.of("records", sync.refreshEmployee(directory.get(id).employeeNo()));
    }

    @GetMapping("/integration/id-map")
    List<Map<String, Object>> idMap(@RequestParam(required = false) String system, @RequestParam(required = false) String entity) {
        access.require(Action.VIEW, ObjectType.SYNC, null);
        return jdbc.queryForList("""
                select * from external_id_map where (cast(:s as text) is null or system = :s) and (cast(:e as text) is null or entity = :e)
                order by system, entity, external_id limit 1000""", new org.springframework.jdbc.core.namedparam.MapSqlParameterSource()
                .addValue("s", system).addValue("e", entity));
    }

    record IdMapping(@NotBlank String system, @NotBlank String entity, @NotBlank String externalId, @NotNull Long internalId) {
    }

    /** Manual key mapping when systems use different keys (G-L3). Then retry the dead letters. */
    @PostMapping("/integration/id-map")
    @Transactional
    IdMapping addMapping(@Valid @RequestBody IdMapping m) {
        access.require(Action.EDIT, ObjectType.SYNC, null);
        EntityKind.valueOf(m.entity());
        jdbc.update("""
                insert into external_id_map (system, entity, external_id, internal_id) values (:s, :e, :x, :i)
                on conflict (system, entity, external_id) do update set internal_id = excluded.internal_id""",
                Map.of("s", m.system(), "e", m.entity(), "x", m.externalId(), "i", m.internalId()));
        audit.record("ID_MAPPING_SET", "SYNC", m.system() + "/" + m.entity() + "/" + m.externalId(), null, null, m, null);
        return m;
    }
}
