package com.talent.workflow;

import java.util.List;
import java.util.Optional;

import com.talent.shared.security.ObjectType;

/**
 * Generic state machine. Business modules call it inside their own transaction and mirror the returned state
 * onto their record. It does not check the permission matrix: the module does that; the engine checks that the
 * caller holds (or is the delegate for) an open task on the instance.
 */
public interface WorkflowService {

    /** Starts an instance on the definition's effective version, which is pinned and locked (ADR-02). */
    WorkflowInstanceView start(String definitionCode, WorkflowSubject subject);

    /** Fires a transition as the signed-in user. */
    WorkflowInstanceView transition(Long instanceId, String action, String comment);

    /** Fires a transition with no human actor (system transitions, cycle close). */
    WorkflowInstanceView transitionAsSystem(Long instanceId, String action, String comment);

    WorkflowInstanceView instance(Long instanceId);

    Optional<WorkflowInstanceView> instanceFor(ObjectType objectType, Long objectId);

    /** Actions the signed-in user may fire now. */
    List<String> availableActions(Long instanceId);

    List<HistoryView> history(Long instanceId);

    /** Open tasks of the signed-in user, including tasks delegated to them. */
    List<TaskView> myTasks();

    /** Re-resolves the actor of every open task held by this employee (left, transferred, long leave). */
    int reassignOpenTasksOf(Long employeeId, String reason);

    /** Cancels open tasks and ends the instance in its current state (e.g. candidate removed). */
    void cancel(Long instanceId, String reason);
}
