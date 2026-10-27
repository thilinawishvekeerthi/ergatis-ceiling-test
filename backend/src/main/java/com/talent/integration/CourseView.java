package com.talent.integration;

import java.util.List;

/** LMS course (API 1, I-06) with the roles / competencies it is mapped to. */
public record CourseView(Long id, String externalId, String title, String url, String provider, Double durationHours,
                         List<Mapping> mappings) {

    /** {@code type} is ROLE (a job code) or COMPETENCY (a competency code). */
    public record Mapping(String type, String code) {
    }
}
