package com.talent.integration;

import java.time.LocalDate;
import java.util.Map;

/** One appraisal result from the Performance module (I-04). Competency scores keyed by competency code. */
public record AppraisalView(Long id, Long employeeId, String cycleCode, LocalDate periodEnd, Double finalScore,
                            String ratingBand, boolean finalised, Map<String, Double> competencyScores) {
}
