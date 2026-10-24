package com.talent.config;

import java.util.Map;

/** Weights for {@code ProfileMatchStrategy} (ADR-06), per job family code with a default. */
public record ProfileMatchWeights(Weights defaults, Map<String, Weights> perJobFamily) {

    public record Weights(double qualification, double experience, double competency) {
        public double total() {
            return qualification + experience + competency;
        }
    }

    public Weights forFamily(String jobFamilyCode) {
        if (perJobFamily != null && jobFamilyCode != null && perJobFamily.containsKey(jobFamilyCode)) {
            return perJobFamily.get(jobFamilyCode);
        }
        return defaults;
    }
}
