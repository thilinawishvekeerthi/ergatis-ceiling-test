package com.talent.integration;

public enum EmployeeStatus {
    ACTIVE, ON_LEAVE, RESIGNED, TERMINATED;

    public boolean isActive() {
        return this == ACTIVE || this == ON_LEAVE;
    }
}
