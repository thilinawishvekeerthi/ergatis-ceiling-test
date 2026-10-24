package com.talent.shared.security;

/** Actions in the permission matrix (ADR-07). */
public enum Action {
    VIEW, EDIT, COMMENT, ACKNOWLEDGE, APPROVE, CALIBRATE, EXPORT;

    /** Actions that judge a person. Nobody may perform them on their own record (conflict of interest). */
    public boolean judgesSubject() {
        return this == EDIT || this == COMMENT || this == APPROVE || this == CALIBRATE;
    }
}
