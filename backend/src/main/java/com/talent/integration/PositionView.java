package com.talent.integration;

public record PositionView(Long id, String code, String title, String positionType, Long orgUnitId, String orgUnitName,
                           Long jobId, String jobCode, Long incumbentEmployeeId, String incumbentName,
                           Long supervisorEmployeeId, String supervisorName,
                           String costCentre, String location, String country, String workStation) {
}
