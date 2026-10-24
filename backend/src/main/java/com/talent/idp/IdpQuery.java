package com.talent.idp;

import java.time.Instant;
import java.time.LocalDate;
import java.util.List;

/** Flat rows for the reporting read model (ADR-11). No access checks here. */
public interface IdpQuery {

    List<IdpRow> rows();

    record IdpRow(Long idpId, Long employeeId, Long supervisorId, String state, Double progressPercent, Integer overallRating,
                  int actionsTotal, int actionsCompleted, int actionsOverdue, Instant lastRevisedAt, LocalDate nextReviewDate) {
    }
}
