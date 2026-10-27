package com.talent.workflow;

import java.util.Set;

/**
 * Resolves an actor expression such as {@code CYCLE_ASSIGNMENT(REVIEWER)} to employee IDs.
 * Built in: DIRECT_SUPERVISOR, HOD, ROLE(x), SUBJECT. Modules add their own as Spring beans.
 */
public interface ActorResolver {

    /** The name before the brackets, e.g. {@code CYCLE_ASSIGNMENT}. */
    String name();

    Set<Long> resolve(WorkflowSubject subject, String argument);
}
