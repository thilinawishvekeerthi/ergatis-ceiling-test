package com.talent.workflow.internal;

import java.sql.Timestamp;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.Map;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import com.talent.integration.EmployeeDirectory;
import com.talent.shared.security.ObjectType;
import com.talent.workflow.WorkflowEvents;

/** SLA timers (ADR-05): one reminder before the due date, one escalation after it. */
@Component
class SlaTimer {

    private final NamedParameterJdbcTemplate jdbc;
    private final ApplicationEventPublisher events;
    private final EmployeeDirectory directory;
    private final Clock clock;
    private final Duration reminderWindow;

    SlaTimer(NamedParameterJdbcTemplate jdbc, ApplicationEventPublisher events, EmployeeDirectory directory, Clock clock,
             @Value("${talent.workflow.reminder-days-before-due:2}") int reminderDays) {
        this.jdbc = jdbc;
        this.events = events;
        this.directory = directory;
        this.clock = clock;
        this.reminderWindow = Duration.ofDays(reminderDays);
    }

    @Scheduled(cron = "${talent.workflow.sla-cron:0 0 * * * *}")
    @Transactional
    public int run() {
        Instant now = clock.instant();
        int sent = 0;
        for (Map<String, Object> t : due("t.reminder_sent = false and t.due_at > :now and t.due_at <= :soon", now)) {
            jdbc.update("update workflow_task set reminder_sent = true where id = :id", Map.of("id", t.get("id")));
            events.publishEvent(new WorkflowEvents.TaskReminderDue(id(t, "id"), (String) t.get("definition_code"),
                    ObjectType.valueOf((String) t.get("object_type")), id(t, "object_id"), id(t, "subject_employee_id"),
                    (String) t.get("state"), id(t, "assignee_employee_id"), ((Timestamp) t.get("due_at")).toInstant()));
            sent++;
        }
        for (Map<String, Object> t : due("t.escalated = false and t.due_at <= :now", now)) {
            jdbc.update("update workflow_task set escalated = true where id = :id", Map.of("id", t.get("id")));
            Long assignee = id(t, "assignee_employee_id");
            Long hod = directory.headOfDepartmentFor(assignee).orElse(null);
            events.publishEvent(new WorkflowEvents.SlaBreached(id(t, "id"), (String) t.get("definition_code"),
                    ObjectType.valueOf((String) t.get("object_type")), id(t, "object_id"), id(t, "subject_employee_id"),
                    (String) t.get("state"), assignee, hod, ((Timestamp) t.get("due_at")).toInstant()));
            sent++;
        }
        return sent;
    }

    private List<Map<String, Object>> due(String condition, Instant now) {
        return jdbc.queryForList("""
                select t.*, i.definition_code, i.object_type, i.object_id, i.subject_employee_id
                from workflow_task t join workflow_instance i on i.id = t.instance_id
                where t.status = 'OPEN' and t.due_at is not null and """ + condition,
                Map.of("now", Timestamp.from(now), "soon", Timestamp.from(now.plus(reminderWindow))));
    }

    private static Long id(Map<String, Object> row, String col) {
        Object v = row.get(col);
        return v == null ? null : ((Number) v).longValue();
    }
}
