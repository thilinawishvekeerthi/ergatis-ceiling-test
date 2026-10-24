package com.talent.assessment;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

/**
 * The talent pool: published (final) nine-box results of the latest cycle that each employee was assessed in.
 * Internal API for succession and reporting. Callers must apply TALENT_POOL access rules themselves.
 */
public interface TalentPoolQuery {

    /** Latest published result per employee, optionally only one reporting band (e.g. TOP_TALENT). */
    List<PoolMember> latestPool(String reportingBandOrNull);

    Optional<PoolMember> latestFor(Long employeeId);

    record PoolMember(Long employeeId, Long cycleId, String cycleName, String cellCode, String cellLabel, String reportingBand,
                      Double performanceScore, Integer potential, Instant publishedAt) {
    }
}
