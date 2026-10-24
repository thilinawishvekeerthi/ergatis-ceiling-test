package com.talent.integration;

import java.time.LocalDate;

/** Employee course status from LMS API 2 (I-07). */
public record EnrolmentView(Long id, Long employeeId, Long courseId, String courseTitle, EnrolmentStatus status,
                            LocalDate assignedOn, LocalDate completedOn, Integer progressPercent, Double score) {

    public enum EnrolmentStatus { ASSIGNED, ONGOING, COMPLETED, CANCELLED }
}
