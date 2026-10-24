package com.talent.integration;

/** Holder class; events are the nested records. */
public final class IntegrationEvents {

    private IntegrationEvents() {
    }

    /** Status changed, e.g. ACTIVE → RESIGNED. Workflow reassigns open tasks (architecture §3.1 rule 5). */
    public record EmployeeStatusChanged(Long employeeId, EmployeeStatus from, EmployeeStatus to) {
    }

    /** Org unit or supervisor changed. */
    public record EmployeeTransferred(Long employeeId, Long fromOrgUnitId, Long toOrgUnitId, Long fromSupervisorId, Long toSupervisorId) {
    }

    public record CourseEnrolmentChanged(Long employeeId, Long courseId, EnrolmentView.EnrolmentStatus status, Integer progressPercent) {
    }

    public record SyncCompleted(String system, long read, long upserted, long failed) {
    }
}
