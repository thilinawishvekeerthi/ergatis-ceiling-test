/**
 * Access control (ADR-07): versioned permission matrix (role × action × object), data scopes, conflict-of-interest
 * guard, TALENT_CONFIDENTIAL field grants, role assignments, the audit log viewer and dev sign-in.
 */
@org.springframework.modulith.ApplicationModule(displayName = "Access control")
package com.talent.access;
