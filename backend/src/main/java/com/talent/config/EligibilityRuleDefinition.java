package com.talent.config;

/**
 * Leadership candidate rule (ADR-04), e.g.
 * {@code last_n_appraisals(2).min_score >= 4 AND grade IN (M1, M2, M3) AND service_years >= 2}.
 */
public record EligibilityRuleDefinition(String description, String expression) {
}
