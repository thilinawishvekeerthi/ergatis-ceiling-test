package com.talent.career;

public final class CareerEvents {

    private CareerEvents() {
    }

    /** Employee submitted career preferences for review (alert #1). */
    public record CareerAspirationSubmitted(Long aspirationId, Long employeeId) {
    }
}
