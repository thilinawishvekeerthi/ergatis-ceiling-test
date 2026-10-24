package com.talent.shared.security;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * @param mode       {@code dev}: the app signs its own tokens for seeded users (no IdP needed).
 *                   {@code oidc}: tokens come from the client's identity provider (ADR-12).
 * @param devSecret  HMAC secret used in dev mode (at least 32 bytes).
 * @param rolesClaim claim that carries global roles / groups.
 * @param loginClaim claim used to find the employee (email or UPN) in oidc mode.
 */
@ConfigurationProperties("talent.security")
public record SecurityProperties(String mode, String devSecret, String rolesClaim, String loginClaim) {

    public SecurityProperties {
        mode = mode == null ? "oidc" : mode;
        rolesClaim = rolesClaim == null ? "roles" : rolesClaim;
        loginClaim = loginClaim == null ? "preferred_username" : loginClaim;
    }

    public boolean devMode() {
        return "dev".equals(mode);
    }
}
