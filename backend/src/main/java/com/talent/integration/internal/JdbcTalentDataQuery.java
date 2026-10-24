package com.talent.integration.internal;

import java.sql.Date;
import java.time.LocalDate;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;

import org.springframework.jdbc.core.RowMapper;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.stereotype.Service;

import com.fasterxml.jackson.core.type.TypeReference;
import com.talent.integration.AppraisalView;
import com.talent.integration.CourseView;
import com.talent.integration.EnrolmentView;
import com.talent.integration.ResumeView;
import com.talent.integration.TalentDataQuery;
import com.talent.shared.json.Json;

@Service
class JdbcTalentDataQuery implements TalentDataQuery {

    private final NamedParameterJdbcTemplate jdbc;

    JdbcTalentDataQuery(NamedParameterJdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    @Override
    public ResumeView resume(Long employeeId) {
        Map<String, Object> p = Map.of("e", employeeId);
        var quals = jdbc.query("select * from qualification where employee_id = :e order by year_completed desc nulls last", p,
                (rs, i) -> new ResumeView.Qualification(rs.getLong("id"), rs.getString("type"), rs.getString("title"),
                        rs.getString("institution"), (Integer) rs.getObject("level"), (Integer) rs.getObject("year_completed")));
        var exp = jdbc.query("select * from experience where employee_id = :e order by start_date desc nulls last", p,
                (rs, i) -> new ResumeView.Experience(rs.getLong("id"), rs.getString("employer"), rs.getString("title"),
                        rs.getString("job_code"), date(rs.getDate("start_date")), date(rs.getDate("end_date")), rs.getBoolean("internal")));
        var skills = jdbc.query("select * from expertise where employee_id = :e order by area", p,
                (rs, i) -> new ResumeView.Expertise(rs.getLong("id"), rs.getString("area"), rs.getString("competency_code"),
                        (Integer) rs.getObject("proficiency")));
        return new ResumeView(employeeId, quals, exp, skills);
    }

    @Override
    public List<AppraisalView> appraisals(Long employeeId) {
        return jdbc.query("select * from appraisal_result where employee_id = :e order by period_end desc", Map.of("e", employeeId), APPRAISAL);
    }

    @Override
    public Optional<AppraisalView> latestFinalisedAppraisal(Long employeeId) {
        return appraisals(employeeId).stream().filter(AppraisalView::finalised).findFirst();
    }

    @Override
    public List<CourseView> courses() {
        return withMappings(jdbc.query("select * from course order by title", COURSE));
    }

    @Override
    public Optional<CourseView> course(Long courseId) {
        return withMappings(jdbc.query("select * from course where id = :id", Map.of("id", courseId), COURSE)).stream().findFirst();
    }

    @Override
    public List<CourseView> coursesFor(String mappingType, String code) {
        return withMappings(jdbc.query("""
                select c.* from course c where exists (select 1 from course_mapping m
                where m.course_id = c.id and m.mapping_type = :t and m.code = :c) order by c.title""",
                Map.of("t", mappingType, "c", code), COURSE));
    }

    @Override
    public List<EnrolmentView> enrolments(Long employeeId) {
        return jdbc.query("""
                select ce.*, c.title as course_title from course_enrolment ce join course c on c.id = ce.course_id
                where ce.employee_id = :e order by ce.assigned_on desc nulls last""", Map.of("e", employeeId),
                (rs, i) -> new EnrolmentView(rs.getLong("id"), rs.getLong("employee_id"), rs.getLong("course_id"),
                        rs.getString("course_title"), EnrolmentView.EnrolmentStatus.valueOf(rs.getString("status")),
                        date(rs.getDate("assigned_on")), date(rs.getDate("completed_on")), (Integer) rs.getObject("progress_percent"),
                        (Double) rs.getObject("score")));
    }

    private List<CourseView> withMappings(List<CourseView> courses) {
        if (courses.isEmpty()) {
            return courses;
        }
        Map<Long, List<CourseView.Mapping>> mappings = new LinkedHashMap<>();
        jdbc.query("select * from course_mapping where course_id in (:ids)", Map.of("ids", courses.stream().map(CourseView::id).toList()),
                rs -> {
                    mappings.computeIfAbsent(rs.getLong("course_id"), k -> new java.util.ArrayList<>())
                            .add(new CourseView.Mapping(rs.getString("mapping_type"), rs.getString("code")));
                });
        return courses.stream().map(c -> new CourseView(c.id(), c.externalId(), c.title(), c.url(), c.provider(), c.durationHours(),
                mappings.getOrDefault(c.id(), List.of()))).toList();
    }

    private static LocalDate date(Date d) {
        return d == null ? null : d.toLocalDate();
    }

    private static final RowMapper<CourseView> COURSE = (rs, i) -> new CourseView(rs.getLong("id"), rs.getString("external_id"),
            rs.getString("title"), rs.getString("url"), rs.getString("provider"), (Double) rs.getObject("duration_hours"), List.of());

    private static final RowMapper<AppraisalView> APPRAISAL = (rs, i) -> new AppraisalView(rs.getLong("id"), rs.getLong("employee_id"),
            rs.getString("cycle_code"), date(rs.getDate("period_end")), (Double) rs.getObject("final_score"), rs.getString("rating_band"),
            rs.getBoolean("finalised"), scores(rs.getString("competency_scores")));

    private static Map<String, Double> scores(String json) {
        if (json == null) {
            return Map.of();
        }
        try {
            return Json.mapper().readValue(json, new TypeReference<Map<String, Double>>() {
            });
        } catch (Exception e) {
            return Map.of();
        }
    }
}
