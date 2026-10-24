package com.talent.workflow;

import java.time.Instant;

public record HistoryView(Long id, String fromState, String action, String toState, Long actorEmployeeId, String actorName,
                          String comment, Instant at) {
}
