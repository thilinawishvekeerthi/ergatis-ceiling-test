package com.talent.integration;

public record OrgUnitView(Long id, String code, String name, String type, Long parentId, Long headEmployeeId,
                          String costCentre, String location, String country) {
}
