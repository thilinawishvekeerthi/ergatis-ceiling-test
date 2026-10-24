package com.talent.workflow;

import java.time.Instant;

import com.talent.shared.security.ObjectType;

public record TaskView(Long id, Long instanceId, String definitionCode, ObjectType objectType, Long objectId, Long subjectEmployeeId,
                       String subjectName, Long contextId, String state, String stateLabel, Long assigneeEmployeeId,
                       String assigneeName, Long originalAssigneeId, Instant createdAt, Instant dueAt, boolean overdue,
                       String status, boolean delegated) {
}
