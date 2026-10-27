package com.talent.shared.security;

import java.util.Set;

import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;

import com.talent.shared.web.ForbiddenException;

/**
 * The signed-in user. {@code roles} are the global roles from the identity provider (e.g. HR_ADMIN).
 * Cycle roles (reviewer, board member) are resolved by the access module, not stored here.
 */
public record CurrentUser(Long employeeId, String username, String displayName, Set<String> roles) {

    public boolean hasRole(String role) {
        return roles.contains(role);
    }

    public static CurrentUser get() {
        Authentication auth = SecurityContextHolder.getContext().getAuthentication();
        if (auth instanceof TalentAuthentication ta) {
            return ta.getPrincipal();
        }
        throw new ForbiddenException("No signed-in user");
    }

    /** For background jobs: returns the user when one is signed in, otherwise {@code null}. */
    public static CurrentUser orNull() {
        Authentication auth = SecurityContextHolder.getContext().getAuthentication();
        return auth instanceof TalentAuthentication ta ? ta.getPrincipal() : null;
    }

    public static Long idOrNull() {
        CurrentUser u = orNull();
        return u == null ? null : u.employeeId();
    }
}
