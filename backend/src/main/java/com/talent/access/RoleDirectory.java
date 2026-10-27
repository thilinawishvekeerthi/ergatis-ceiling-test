package com.talent.access;

import java.util.Set;

/** Who holds a global role (HR_ADMIN, HR_APPROVER). Used by workflow {@code ROLE(x)} actor resolution. */
public interface RoleDirectory {

    Set<Long> holdersOf(String role);

    /** Effective roles of any employee: global + derived (supervisor, HOD) + cycle roles. */
    Set<String> effectiveRoles(Long employeeId);
}
