package com.talent.assessment;

import java.time.LocalDateTime;
import java.util.List;

/** Domain events of the assessment module (ADR-10). */
public final class AssessmentEvents {

    private AssessmentEvents() {
    }

    public record CycleLaunched(Long cycleId, String cycleName, List<Long> candidateEmployeeIds) {
    }

    public record AssessmentSubmitted(Long assessmentId, Long cycleId, Long employeeId, Long reviewerId) {
    }

    public record AssessmentApproved(Long assessmentId, Long cycleId, Long employeeId) {
    }

    public record AssessmentReturned(Long assessmentId, Long cycleId, Long employeeId, Long reviewerId, String comment) {
    }

    /** Alert #2: a review meeting was scheduled for a board. */
    public record ReviewMeetingScheduled(Long meetingId, Long boardId, Long cycleId, LocalDateTime startsAt, String location,
                                         List<Long> boardMemberIds) {
    }

    /** Board decisions published; the talent pool now holds these employees' final results. */
    public record BoardDecisionPublished(Long cycleId, List<Long> employeeIds) {
    }

    /**
     * A board recommendation for one employee. {@code type}: PROMOTION, IDP, ROTATION, RETENTION, SUCCESSION, OTHER.
     * The IDP module turns IDP recommendations into draft plans (C9).
     */
    public record RecommendationRaised(Long recommendationId, Long cycleId, Long employeeId, String type, String text) {
    }

    public record CycleClosed(Long cycleId) {
    }
}
