package com.talent.config;

import java.util.List;
import java.util.Map;

/**
 * {@code BenchStrengthStrategy} settings (ADR-06).
 * COUNT: number of successors whose readiness is in {@code countedReadiness}.
 * WEIGHTED: sum of readiness weights. Bands turn the number into a label (e.g. Weak / Adequate / Strong).
 */
public record BenchStrengthDefinition(Method method, List<String> countedReadiness, Map<String, Double> readinessWeights,
                                      List<Band> bands) {

    public enum Method { COUNT, WEIGHTED }

    /** Score {@code >= min} gets this label. Bands are checked from highest {@code min} down. */
    public record Band(double min, String label, String colour) {
    }
}
