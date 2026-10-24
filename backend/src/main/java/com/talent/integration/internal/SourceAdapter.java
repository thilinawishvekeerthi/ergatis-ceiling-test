package com.talent.integration.internal;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * One adapter per source system (ADR-08). It returns records already shaped like the canonical model;
 * references to other records use the source system's own keys ({@code ...Ref} fields).
 * The record format is documented in {@code docs/integration-contract.md}.
 */
interface SourceAdapter {

    /** e.g. CORE_HR, SSHR, PERFORMANCE, LMS. Also the namespace for External ID Map keys. */
    String system();

    Set<EntityKind> kinds();

    /** Records changed since {@code since} (null = full load). Each map must contain {@code id}. */
    List<Map<String, Object>> fetch(EntityKind kind, Instant since);

    /** Records of one employee, for the on-demand refresh. {@code employeeRef} is the source key. */
    List<Map<String, Object>> fetchForEmployee(EntityKind kind, String employeeRef);
}
