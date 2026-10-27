package com.talent.workflow;

import java.time.Instant;

import com.talent.shared.security.ObjectType;

/** Domain events of the workflow engine (ADR-10). Notifications subscribe to these. */
public final class WorkflowEvents {

    private WorkflowEvents() {
    }

    public record TaskAssigned(Long taskId, Long instanceId, String definitionCode, ObjectType objectType, Long objectId,
                               Long subjectEmployeeId, Long contextId, String state, Long assigneeEmployeeId, Instant dueAt) {
    }

    public record Transitioned(Long instanceId, String definitionCode, ObjectType objectType, Long objectId, Long subjectEmployeeId,
                               Long contextId, String fromState, String action, String toState, Long actorEmployeeId, String comment) {
    }

    /** Due soon (reminder window). Sent once per task. */
    public record TaskReminderDue(Long taskId, String definitionCode, ObjectType objectType, Long objectId, Long subjectEmployeeId,
                                  String state, Long assigneeEmployeeId, Instant dueAt) {
    }

    /** Overdue. Sent once per task. {@code escalateToEmployeeId} is the assignee's head of department, if any. */
    public record SlaBreached(Long taskId, String definitionCode, ObjectType objectType, Long objectId, Long subjectEmployeeId,
                              String state, Long assigneeEmployeeId, Long escalateToEmployeeId, Instant dueAt) {
    }
}
