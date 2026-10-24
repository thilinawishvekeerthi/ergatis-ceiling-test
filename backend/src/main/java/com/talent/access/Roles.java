package com.talent.access;

/** Role names used in the permission matrix and workflow actor resolvers. */
public final class Roles {

    private Roles() {
    }

    /** Everyone with an employee record. */
    public static final String EMPLOYEE = "EMPLOYEE";
    /** Derived: has at least one direct report. */
    public static final String SUPERVISOR = "SUPERVISOR";
    /** Derived: head of at least one org unit. */
    public static final String HOD = "HOD";
    /** Cycle roles, assigned per talent review cycle by HR Admin (gap analysis §3.1). */
    public static final String REVIEWER = "REVIEWER";
    public static final String APPROVER = "APPROVER";
    public static final String BOARD_HEAD = "BOARD_HEAD";
    public static final String BOARD_MEMBER = "BOARD_MEMBER";
    /** Global roles, from the identity provider or the role assignment table. */
    public static final String HR_ADMIN = "HR_ADMIN";
    public static final String HR_APPROVER = "HR_APPROVER";
}
