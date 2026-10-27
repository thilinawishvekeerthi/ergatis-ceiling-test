package com.talent.assessment;

import java.time.Instant;
import java.time.LocalDate;
import java.util.List;

/** Flat rows for the reporting read model (ADR-11). No access checks: reporting applies row-level security. */
public interface AssessmentReportQuery {

    List<CycleRow> cycles();

    List<AssessmentRow> assessments(Long cycleIdOrNull);

    record CycleRow(Long cycleId, String name, String status, LocalDate startDate, LocalDate reviewerCutoffDate, LocalDate endDate,
                    int candidates, int submitted, int approved, int finalised) {
    }

    record AssessmentRow(Long assessmentId, Long cycleId, String cycleName, Long employeeId, String state, Long reviewerId,
                         Double performanceScore, Integer potential, String proposedCellCode, String finalCellCode,
                         String finalCellLabel, String reportingBand, Instant submittedAt, Instant finalisedAt) {
    }
}
