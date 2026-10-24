package com.talent.succession;

public final class SuccessionEvents {

    private SuccessionEvents() {
    }

    /** Sent to HR only (G-N3 "successor nominated"). */
    public record SuccessorNominated(Long criticalPositionId, Long positionId, Long employeeId, String readinessCode) {
    }

    public record SuccessorRemoved(Long criticalPositionId, Long employeeId) {
    }

    public record SuccessionPlanApproved(Long criticalPositionId) {
    }

    public record ReadinessSnapshotTaken(String period, int rows) {
    }
}
