package com.talent.succession;

import java.time.LocalDate;
import java.util.List;

/** Internal API for IDP (target positions, G-D7) and reporting (ADR-11). No access checks here. */
public interface SuccessionQuery {

    /** Critical positions this employee is a nominated successor for. */
    List<TargetPosition> targetsFor(Long employeeId);

    /** One row per successor (or one row with null successor for a position that has none). */
    List<SuccessorRow> successorRows();

    /** Monthly readiness snapshots (ADR-09), for trend reports. */
    List<SnapshotRow> snapshots(LocalDate fromPeriodOrNull);

    record SuccessorRow(Long criticalPositionId, Long positionId, String positionTitle, Long orgUnitId, Long jobId, Long incumbentId,
                        String riskOfLossCode, String impactOfLossCode, String planState, Long successorEmployeeId,
                        String readinessCode, String readinessBand, Integer rank, Double benchStrength, String benchStrengthLabel) {
    }

    record SnapshotRow(LocalDate period, Long criticalPositionId, String positionTitle, Long orgUnitId, Long successorEmployeeId,
                       String readinessCode, String readinessBand) {
    }

    record TargetPosition(Long criticalPositionId, Long positionId, String positionTitle, Long jobId, String readinessCode, Integer rank) {
    }
}
