package com.talent.shared.security;

/**
 * Service-layer authorisation (ADR-07): permission matrix × data scope × conflict-of-interest guard.
 * Implemented by the access module. Every module calls this before reading or changing talent data.
 */
public interface AccessGuard {

    /**
     * @param subjectEmployeeId the employee the record is about; null for records not about a person
     * @throws com.talent.shared.web.ForbiddenException when not allowed
     */
    void require(Action action, ObjectType object, Long subjectEmployeeId);

    boolean can(Action action, ObjectType object, Long subjectEmployeeId);

    /** True if the current user may see TALENT_CONFIDENTIAL fields of this object about this employee. */
    boolean canViewConfidential(ObjectType object, Long subjectEmployeeId);

    /** Employees whose records of this type the current user may access with this action. */
    RowFilter rowFilter(Action action, ObjectType object);
}
