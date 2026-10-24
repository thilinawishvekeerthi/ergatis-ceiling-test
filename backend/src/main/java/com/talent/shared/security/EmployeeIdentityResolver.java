package com.talent.shared.security;

import java.util.Optional;

/**
 * Maps an identity-provider login (email / UPN) to the internal employee ID. Implemented by the
 * integration module, which owns the employee master data (ADR-08).
 */
public interface EmployeeIdentityResolver {

    Optional<ResolvedIdentity> resolve(String login);

    Optional<ResolvedIdentity> byEmployeeId(Long employeeId);

    record ResolvedIdentity(Long employeeId, String username, String displayName) {
    }
}
