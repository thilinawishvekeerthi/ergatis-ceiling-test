package com.talent.career;

import java.util.List;

/**
 * % match of an employee against a job's requirements (ADR-06 ProfileMatchStrategy): weighted qualification +
 * experience + competency, weights per job family from config PROFILE_MATCH_WEIGHTS. Always returns a breakdown,
 * so HR can see why.
 */
public interface ProfileMatchService {

    MatchResult match(Long employeeId, Long jobId);

    /** Matches against every job in the employee's job family (Career Centre "suitability with other jobs"). */
    List<MatchResult> matchFamily(Long employeeId);

    record MatchResult(Long employeeId, Long jobId, String jobCode, String jobTitle, double score, Part qualification,
                       Part experience, Part competency, List<Gap> gaps, Long weightsVersionId) {
    }

    /** {@code score} 0–100 for this part; {@code weight} as configured; {@code detail} human-readable reason. */
    record Part(double score, double weight, String detail) {
    }

    /** A requirement the employee does not yet meet, e.g. a competency below the required proficiency. */
    record Gap(String type, String code, String name, Double required, Double actual) {
    }
}
