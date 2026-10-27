package com.talent.integration.internal;

import java.sql.Date;
import java.time.LocalDate;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;

import org.springframework.context.ApplicationEventPublisher;
import org.springframework.jdbc.core.namedparam.MapSqlParameterSource;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.jdbc.support.GeneratedKeyHolder;
import org.springframework.stereotype.Component;

import com.talent.integration.EmployeeStatus;
import com.talent.integration.EnrolmentView;
import com.talent.integration.IntegrationEvents;
import com.talent.shared.json.Json;

/**
 * Maps one canonical record into the local read tables: idempotent upsert keyed on the source ID through the
 * External ID Map (ADR-08, G-L3). Throws {@link RecordException} for bad records; the caller dead-letters them.
 */
@Component
class RecordLoader {

    private final NamedParameterJdbcTemplate jdbc;
    private final ApplicationEventPublisher events;

    RecordLoader(NamedParameterJdbcTemplate jdbc, ApplicationEventPublisher events) {
        this.jdbc = jdbc;
        this.events = events;
    }

    static class RecordException extends RuntimeException {
        RecordException(String message) {
            super(message);
        }
    }

    /** First pass: the record itself. References to employees are linked in {@link #link}. */
    void load(String system, EntityKind kind, Map<String, Object> r) {
        String extId = required(r, "id");
        switch (kind) {
            case ORG_UNIT -> upsert(system, kind, extId, "org_unit", row(
                    "code", required(r, "code"), "name", required(r, "name"), "type", str(r, "type"),
                    "cost_centre", str(r, "costCentre"), "location", str(r, "location"), "country", str(r, "country")));
            case JOB -> upsert(system, kind, extId, "job", row(
                    "code", required(r, "code"), "title", required(r, "title"), "grade_code", str(r, "gradeCode")));
            case POSITION -> upsert(system, kind, extId, "position", row(
                    "code", required(r, "code"), "title", required(r, "title"), "position_type", str(r, "positionType"),
                    "org_unit_id", ref(system, EntityKind.ORG_UNIT, r, "orgUnitRef", true),
                    "job_id", ref(system, EntityKind.JOB, r, "jobRef", false),
                    "cost_centre", str(r, "costCentre"), "location", str(r, "location"), "country", str(r, "country"),
                    "work_station", str(r, "workStation")));
            case EMPLOYEE -> loadEmployee(system, extId, r);
            case COMPETENCY -> upsert(system, kind, extId, "competency", row(
                    "code", required(r, "code"), "name", required(r, "name"), "category", str(r, "category"),
                    "description", str(r, "description")));
            case APPRAISAL -> upsert(system, kind, extId, "appraisal_result", row(
                    "employee_id", ref(system, EntityKind.EMPLOYEE, r, "employeeRef", true),
                    "cycle_code", required(r, "cycleCode"), "period_end", date(r, "periodEnd"),
                    "final_score", num(r, "finalScore"), "rating_band", str(r, "ratingBand"),
                    "finalised", Boolean.TRUE.equals(r.get("finalised")),
                    "competency_scores", Json.write(r.getOrDefault("competencyScores", Map.of()))));
            case QUALIFICATION -> upsert(system, kind, extId, "qualification", row(
                    "employee_id", ref(system, EntityKind.EMPLOYEE, r, "employeeRef", true),
                    "type", str(r, "type"), "title", required(r, "title"), "institution", str(r, "institution"),
                    "level", integer(r, "level"), "year_completed", integer(r, "yearCompleted")));
            case EXPERIENCE -> upsert(system, kind, extId, "experience", row(
                    "employee_id", ref(system, EntityKind.EMPLOYEE, r, "employeeRef", true),
                    "employer", str(r, "employer"), "title", required(r, "title"), "job_code", str(r, "jobCode"),
                    "start_date", date(r, "startDate"), "end_date", date(r, "endDate"),
                    "internal", Boolean.TRUE.equals(r.get("internal"))));
            case EXPERTISE -> upsert(system, kind, extId, "expertise", row(
                    "employee_id", ref(system, EntityKind.EMPLOYEE, r, "employeeRef", true),
                    "area", required(r, "area"), "competency_code", str(r, "competencyCode"),
                    "proficiency", integer(r, "proficiency")));
            case COURSE -> loadCourse(system, extId, r);
            case ENROLMENT -> loadEnrolment(system, extId, r);
        }
    }

    /** Second pass: links that point at employees, which may load later in the same run. */
    void link(String system, EntityKind kind, Map<String, Object> r) {
        Long id = internalId(system, kind, required(r, "id"));
        if (id == null) {
            return;
        }
        switch (kind) {
            case ORG_UNIT -> jdbc.update("update org_unit set parent_id = :p, head_employee_id = :h where id = :id", new MapSqlParameterSource()
                    .addValue("p", ref(system, EntityKind.ORG_UNIT, r, "parentRef", false))
                    .addValue("h", ref(system, EntityKind.EMPLOYEE, r, "headRef", false)).addValue("id", id));
            case POSITION -> jdbc.update("update position set incumbent_employee_id = :i where id = :id", new MapSqlParameterSource()
                    .addValue("i", ref(system, EntityKind.EMPLOYEE, r, "incumbentRef", false)).addValue("id", id));
            case EMPLOYEE -> {
                Long newSup = ref(system, EntityKind.EMPLOYEE, r, "supervisorRef", false);
                Long oldSup = jdbc.queryForObject("select supervisor_id from employee where id = :id", Map.of("id", id), Long.class);
                if (!Objects.equals(oldSup, newSup)) {
                    jdbc.update("update employee set supervisor_id = :s where id = :id", new MapSqlParameterSource().addValue("s", newSup).addValue("id", id));
                    if (oldSup != null) {
                        events.publishEvent(new IntegrationEvents.EmployeeTransferred(id, null, null, oldSup, newSup));
                    }
                }
            }
            default -> {
            }
        }
    }

    private void loadEmployee(String system, String extId, Map<String, Object> r) {
        String first = str(r, "firstName");
        String last = str(r, "lastName");
        EmployeeStatus status = EmployeeStatus.valueOf(Objects.requireNonNullElse(str(r, "status"), "ACTIVE"));
        Long orgUnit = ref(system, EntityKind.ORG_UNIT, r, "orgUnitRef", false);
        Map<String, Object> cols = new HashMap<>();
        cols.put("employee_no", required(r, "employeeNo"));
        cols.put("first_name", first);
        cols.put("last_name", last);
        cols.put("display_name", Objects.requireNonNullElse(str(r, "displayName"), (Objects.toString(first, "") + " " + Objects.toString(last, "")).trim()));
        cols.put("email", str(r, "email"));
        cols.put("picture_url", str(r, "pictureUrl"));
        cols.put("grade_code", str(r, "gradeCode"));
        cols.put("job_id", ref(system, EntityKind.JOB, r, "jobRef", false));
        cols.put("position_id", ref(system, EntityKind.POSITION, r, "positionRef", false));
        cols.put("org_unit_id", orgUnit);
        cols.put("status", status.name());
        cols.put("join_date", date(r, "joinDate"));
        cols.put("employment_category", str(r, "employmentCategory"));
        cols.put("location", str(r, "location"));
        cols.put("work_station", str(r, "workStation"));
        cols.put("country", str(r, "country"));
        cols.put("cost_centre", str(r, "costCentre"));

        Long existing = internalId(system, EntityKind.EMPLOYEE, extId);
        Map<String, Object> before = existing == null ? null
                : jdbc.queryForMap("select status, org_unit_id from employee where id = :id", Map.of("id", existing));
        Long id = upsert(system, EntityKind.EMPLOYEE, extId, "employee", cols);
        if (before != null) {
            EmployeeStatus oldStatus = EmployeeStatus.valueOf((String) before.get("status"));
            if (oldStatus != status) {
                events.publishEvent(new IntegrationEvents.EmployeeStatusChanged(id, oldStatus, status));
            }
            Long oldOrg = before.get("org_unit_id") == null ? null : ((Number) before.get("org_unit_id")).longValue();
            if (!Objects.equals(oldOrg, orgUnit)) {
                events.publishEvent(new IntegrationEvents.EmployeeTransferred(id, oldOrg, orgUnit, null, null));
            }
        }
    }

    @SuppressWarnings("unchecked")
    private void loadCourse(String system, String extId, Map<String, Object> r) {
        Long id = upsert(system, EntityKind.COURSE, extId, "course", row(
                "external_id", extId, "title", required(r, "title"), "url", str(r, "url"), "provider", str(r, "provider"),
                "duration_hours", num(r, "durationHours")));
        jdbc.update("delete from course_mapping where course_id = :c", Map.of("c", id));
        for (Map<String, Object> m : (List<Map<String, Object>>) r.getOrDefault("mappings", List.of())) {
            jdbc.update("insert into course_mapping (course_id, mapping_type, code) values (:c, :t, :code)",
                    Map.of("c", id, "t", required(m, "type"), "code", required(m, "code")));
        }
    }

    private void loadEnrolment(String system, String extId, Map<String, Object> r) {
        Long employee = ref(system, EntityKind.EMPLOYEE, r, "employeeRef", true);
        Long course = ref(system, EntityKind.COURSE, r, "courseRef", true);
        EnrolmentView.EnrolmentStatus status = EnrolmentView.EnrolmentStatus.valueOf(required(r, "status"));
        Integer progress = integer(r, "progressPercent");
        Long existing = internalId(system, EntityKind.ENROLMENT, extId);
        String oldStatus = existing == null ? null
                : jdbc.queryForObject("select status from course_enrolment where id = :id", Map.of("id", existing), String.class);
        Map<String, Object> cols = new HashMap<>();
        cols.put("employee_id", employee);
        cols.put("course_id", course);
        cols.put("status", status.name());
        cols.put("assigned_on", date(r, "assignedOn"));
        cols.put("completed_on", date(r, "completedOn"));
        cols.put("progress_percent", progress);
        cols.put("score", num(r, "score"));
        upsert(system, EntityKind.ENROLMENT, extId, "course_enrolment", cols);
        if (!status.name().equals(oldStatus)) {
            events.publishEvent(new IntegrationEvents.CourseEnrolmentChanged(employee, course, status, progress));
        }
    }

    // ---- helpers ----------------------------------------------------------------------------

    Long internalId(String system, EntityKind kind, String externalId) {
        List<Long> ids = jdbc.queryForList("select internal_id from external_id_map where system = :s and entity = :e and external_id = :x",
                Map.of("s", system, "e", kind.name(), "x", externalId), Long.class);
        return ids.isEmpty() ? null : ids.get(0);
    }

    private Long upsert(String system, EntityKind kind, String extId, String table, Map<String, Object> cols) {
        MapSqlParameterSource params = new MapSqlParameterSource(cols);
        Long id = internalId(system, kind, extId);
        if (id != null) {
            String set = String.join(", ", cols.keySet().stream().map(c -> c + " = " + bind(c)).toList());
            int n = jdbc.update("update " + table + " set " + set + ", synced_at = now() where id = :__id", params.addValue("__id", id));
            if (n == 1) {
                return id;
            }
        }
        String names = String.join(", ", cols.keySet());
        String values = String.join(", ", cols.keySet().stream().map(this::bind).toList());
        GeneratedKeyHolder keys = new GeneratedKeyHolder();
        jdbc.update("insert into " + table + " (" + names + ", synced_at) values (" + values + ", now())", params, keys, new String[]{"id"});
        Long newId = keys.getKey().longValue();
        jdbc.update("""
                insert into external_id_map (system, entity, external_id, internal_id) values (:s, :e, :x, :i)
                on conflict (system, entity, external_id) do update set internal_id = excluded.internal_id""",
                Map.of("s", system, "e", kind.name(), "x", extId, "i", newId));
        return newId;
    }

    /** Like {@code Map.of} but allows null values (most source fields are optional). */
    private static Map<String, Object> row(Object... keyValues) {
        Map<String, Object> m = new java.util.LinkedHashMap<>();
        for (int i = 0; i < keyValues.length; i += 2) {
            m.put((String) keyValues[i], keyValues[i + 1]);
        }
        return m;
    }

    private String bind(String column) {
        return column.equals("competency_scores") ? "cast(:" + column + " as jsonb)" : ":" + column;
    }

    /**
     * Resolves a reference to another record. Looks in the External ID Map for this system first, then falls back
     * to a natural key (employee number, code) and remembers the match, so keys that differ between systems still
     * line up (G-L3). An admin can also add mappings by hand.
     */
    private Long ref(String system, EntityKind kind, Map<String, Object> r, String field, boolean mandatory) {
        String value = str(r, field);
        if (value == null) {
            if (mandatory) {
                throw new RecordException(field + " is required");
            }
            return null;
        }
        Long id = internalId(system, kind, value);
        if (id == null) {
            id = byNaturalKey(kind, value);
            if (id != null) {
                jdbc.update("insert into external_id_map (system, entity, external_id, internal_id) values (:s, :e, :x, :i) on conflict do nothing",
                        Map.of("s", system, "e", kind.name(), "x", value, "i", id));
            }
        }
        if (id == null) {
            throw new RecordException("Unknown " + kind + " reference " + field + "=" + value);
        }
        return id;
    }

    private Long byNaturalKey(EntityKind kind, String value) {
        String sql = switch (kind) {
            case EMPLOYEE -> "select id from employee where employee_no = :v";
            case ORG_UNIT -> "select id from org_unit where code = :v";
            case JOB -> "select id from job where code = :v";
            case POSITION -> "select id from position where code = :v";
            case COMPETENCY -> "select id from competency where code = :v";
            case COURSE -> "select id from course where external_id = :v";
            default -> null;
        };
        if (sql == null) {
            return null;
        }
        List<Long> ids = jdbc.queryForList(sql, Map.of("v", value), Long.class);
        return ids.size() == 1 ? ids.get(0) : null;
    }

    private static String required(Map<String, Object> r, String field) {
        String v = str(r, field);
        if (v == null || v.isBlank()) {
            throw new RecordException(field + " is required");
        }
        return v;
    }

    private static String str(Map<String, Object> r, String field) {
        Object v = r.get(field);
        return v == null ? null : v.toString();
    }

    private static Double num(Map<String, Object> r, String field) {
        Object v = r.get(field);
        try {
            return v == null ? null : Double.valueOf(v.toString());
        } catch (NumberFormatException e) {
            throw new RecordException(field + " is not a number: " + v);
        }
    }

    private static Integer integer(Map<String, Object> r, String field) {
        Double d = num(r, field);
        return d == null ? null : d.intValue();
    }

    private static Date date(Map<String, Object> r, String field) {
        String v = str(r, field);
        try {
            return v == null ? null : Date.valueOf(LocalDate.parse(v));
        } catch (Exception e) {
            throw new RecordException(field + " is not a date (yyyy-MM-dd): " + v);
        }
    }
}
