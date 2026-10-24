package com.talent.config;

/** {@code IdpProgressStrategy} settings (ADR-06). */
public record IdpProgressDefinition(Method method) {

    /** EQUAL: completed / total. WEIGHTED: by action weight. LMS: LMS completion % for linked courses. */
    public enum Method { EQUAL, WEIGHTED, LMS }
}
