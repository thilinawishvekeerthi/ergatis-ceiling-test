package com.talent.access;

import java.util.Set;

import com.talent.shared.security.ObjectType;

/**
 * Implemented by modules that assign people to work on other people's records (e.g. assessment: reviewers,
 * approvers, board members). Drives derived roles and the CYCLE_ASSIGNED data scope.
 */
public interface CycleRoleProvider {

    /** Cycle roles this employee holds in any open cycle, e.g. {@code REVIEWER}. */
    Set<String> rolesOf(Long employeeId);

    /** Employees whose records of {@code object} this actor may access in {@code role}. */
    Set<Long> assignedSubjects(Long actorEmployeeId, String role, ObjectType object);
}
