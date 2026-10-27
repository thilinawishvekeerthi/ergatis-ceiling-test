package com.talent.access;

import java.util.List;
import java.util.Set;

import com.talent.shared.security.Action;
import com.talent.shared.security.DataScope;
import com.talent.shared.security.ObjectType;

/** Payload of config type PERMISSION_MATRIX (ADR-07). Deny by default: anything not granted is forbidden. */
public record PermissionMatrix(String description, List<Grant> grants) {

    /** @param confidential true if this grant also shows TALENT_CONFIDENTIAL fields (only meaningful with VIEW) */
    public record Grant(String role, ObjectType object, Set<Action> actions, DataScope scope, boolean confidential) {
    }
}
