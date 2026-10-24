package com.talent.integration;

import java.time.LocalDate;
import java.util.List;

/** Resume data from SSHR (I-03): Career Centre "View your Resume Details" (blueprint p.8). */
public record ResumeView(Long employeeId, List<Qualification> qualifications, List<Experience> experience,
                         List<Expertise> expertise) {

    /** {@code level} is an ordinal: 1 certificate, 2 diploma, 3 bachelor, 4 master, 5 doctorate. */
    public record Qualification(Long id, String type, String title, String institution, Integer level, Integer yearCompleted) {
    }

    public record Experience(Long id, String employer, String title, String jobCode, LocalDate startDate, LocalDate endDate,
                             boolean internal) {
        public double years(LocalDate today) {
            LocalDate end = endDate == null ? today : endDate;
            return startDate == null ? 0 : java.time.temporal.ChronoUnit.DAYS.between(startDate, end) / 365.25;
        }
    }

    public record Expertise(Long id, String area, String competencyCode, Integer proficiency) {
    }

    public double totalExperienceYears(LocalDate today) {
        return experience.stream().mapToDouble(e -> e.years(today)).sum();
    }
}
