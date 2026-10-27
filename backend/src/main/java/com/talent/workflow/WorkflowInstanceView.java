package com.talent.workflow;

import java.time.Instant;
import java.util.List;

import com.talent.shared.security.ObjectType;

public record WorkflowInstanceView(Long id, String definitionCode, Long definitionVersionId, ObjectType objectType, Long objectId,
                                   Long subjectEmployeeId, Long contextId, String state, String stateLabel, boolean terminal,
                                   Instant startedAt, Instant updatedAt, List<TaskView> openTasks) {
}
