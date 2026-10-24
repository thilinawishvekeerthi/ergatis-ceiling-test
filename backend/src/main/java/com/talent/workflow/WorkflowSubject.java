package com.talent.workflow;

import com.talent.shared.security.ObjectType;

/** What a workflow instance is about. {@code contextId} is usually the talent review cycle. */
public record WorkflowSubject(ObjectType objectType, Long objectId, Long subjectEmployeeId, Long contextId) {
}
