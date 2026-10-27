package com.talent.shared.security;

/** Which records a grant covers (ADR-07). */
public enum DataScope {
    SELF, DIRECT_REPORTS, DEPT_TREE, CYCLE_ASSIGNED, ALL
}
