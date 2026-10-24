package com.talent.integration;

import java.time.LocalDate;

public record EmployeeView(Long id, String employeeNo, String displayName, String firstName, String lastName, String email,
                           String pictureUrl, String gradeCode, Long jobId, String jobCode, String jobTitle,
                           Long positionId, String positionTitle, Long orgUnitId, String orgUnitName,
                           Long supervisorId, String supervisorName, EmployeeStatus status, LocalDate joinDate,
                           String employmentCategory, String location, String workStation, String country, String costCentre) {

    public double serviceYears(LocalDate today) {
        return joinDate == null ? 0 : java.time.temporal.ChronoUnit.DAYS.between(joinDate, today) / 365.25;
    }
}
