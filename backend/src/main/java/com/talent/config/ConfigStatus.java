package com.talent.config;

/** Draft → Published → Locked → Retired (ADR-02). Locked versions are immutable. */
public enum ConfigStatus {
    DRAFT, PUBLISHED, LOCKED, RETIRED
}
