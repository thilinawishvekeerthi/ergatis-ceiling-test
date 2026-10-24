package com.talent.shared.security;

/** Securable objects in the permission matrix (ADR-07). */
public enum ObjectType {
    CONFIG(false),
    MASTER_DATA(false),
    EMPLOYEE(true),
    CAREER_ASPIRATION(true),
    CYCLE(false),
    ASSESSMENT(false),
    NINE_BOX(false),
    BOARD(false),
    TALENT_POOL(false),
    CRITICAL_POSITION(false),
    SUCCESSION_PLAN(false),
    IDP(false),
    REPORT(false),
    AUDIT(false),
    SYNC(false);

    private final boolean selfService;

    ObjectType(boolean selfService) {
        this.selfService = selfService;
    }

    /** True when people may edit their own record (e.g. their career aspirations). */
    public boolean selfService() {
        return selfService;
    }
}
