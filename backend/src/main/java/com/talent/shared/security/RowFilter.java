package com.talent.shared.security;

import java.util.Collection;
import java.util.Set;

/**
 * Row-level filter for list queries and reports (ADR-07, ADR-11): either every employee, or a fixed set.
 */
public record RowFilter(boolean all, Set<Long> employeeIds) {

    public static final RowFilter NONE = new RowFilter(false, Set.of());
    public static final RowFilter ALL = new RowFilter(true, Set.of());

    public boolean allows(Long employeeId) {
        return all || (employeeId != null && employeeIds.contains(employeeId));
    }

    public boolean isEmpty() {
        return !all && employeeIds.isEmpty();
    }

    /** Ids to use in an SQL {@code IN} clause. Only valid when {@link #all()} is false. */
    public Collection<Long> ids() {
        return employeeIds.isEmpty() ? Set.of(-1L) : employeeIds;
    }
}
