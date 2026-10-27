package com.talent.config;

import java.util.List;

/**
 * Data an eligibility rule is evaluated against, usually a cycle snapshot of one employee (ADR-04).
 */
public interface EligibilityFacts {

    /** Value of a plain field such as {@code grade} or {@code service_years}. Null if unknown. */
    Object field(String name);

    /**
     * Value of a function property such as {@code last_n_appraisals(2).min_score}.
     * Return null when the data does not exist (the condition is then false).
     */
    Object function(String name, List<Object> args, String property);
}
