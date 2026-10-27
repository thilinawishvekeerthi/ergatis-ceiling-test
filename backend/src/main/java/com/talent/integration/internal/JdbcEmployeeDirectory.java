package com.talent.integration.internal;

import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.Collection;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

import org.springframework.jdbc.core.RowMapper;
import org.springframework.jdbc.core.namedparam.MapSqlParameterSource;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.stereotype.Service;

import com.talent.integration.CompetencyView;
import com.talent.integration.EmployeeDirectory;
import com.talent.integration.EmployeeStatus;
import com.talent.integration.EmployeeView;
import com.talent.integration.JobView;
import com.talent.integration.OrgUnitView;
import com.talent.integration.PositionView;
import com.talent.shared.security.EmployeeIdentityResolver;
import com.talent.shared.web.NotFoundException;

@Service
class JdbcEmployeeDirectory implements EmployeeDirectory, EmployeeIdentityResolver {

    private static final String EMPLOYEE_SELECT = """
            select e.*, j.code as job_code, j.title as job_title, p.title as position_title, o.name as org_unit_name,
                   s.display_name as supervisor_name
            from employee e
            left join job j on j.id = e.job_id
            left join position p on p.id = e.position_id
            left join org_unit o on o.id = e.org_unit_id
            left join employee s on s.id = e.supervisor_id
            """;

    private static final String POSITION_SELECT = """
            select p.*, o.name as org_unit_name, j.code as job_code, i.display_name as incumbent_name,
                   i.supervisor_id as supervisor_employee_id, s.display_name as supervisor_name
            from position p
            left join org_unit o on o.id = p.org_unit_id
            left join job j on j.id = p.job_id
            left join employee i on i.id = p.incumbent_employee_id
            left join employee s on s.id = i.supervisor_id
            """;

    private final NamedParameterJdbcTemplate jdbc;

    JdbcEmployeeDirectory(NamedParameterJdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    @Override
    public Optional<EmployeeView> find(Long employeeId) {
        if (employeeId == null) {
            return Optional.empty();
        }
        return jdbc.query(EMPLOYEE_SELECT + " where e.id = :id", Map.of("id", employeeId), EMPLOYEE).stream().findFirst();
    }

    @Override
    public EmployeeView get(Long employeeId) {
        return find(employeeId).orElseThrow(() -> new NotFoundException("Employee", employeeId));
    }

    @Override
    public Map<Long, EmployeeView> findAll(Collection<Long> employeeIds) {
        Map<Long, EmployeeView> out = new LinkedHashMap<>();
        if (employeeIds == null || employeeIds.isEmpty()) {
            return out;
        }
        jdbc.query(EMPLOYEE_SELECT + " where e.id in (:ids)", Map.of("ids", new HashSet<>(employeeIds)), EMPLOYEE)
                .forEach(e -> out.put(e.id(), e));
        return out;
    }

    @Override
    public Optional<EmployeeView> findByEmployeeNo(String employeeNo) {
        return jdbc.query(EMPLOYEE_SELECT + " where e.employee_no = :no", Map.of("no", employeeNo), EMPLOYEE).stream().findFirst();
    }

    @Override
    public List<EmployeeView> search(String text, Long orgUnitId, int limit) {
        StringBuilder sql = new StringBuilder(EMPLOYEE_SELECT).append(" where 1 = 1");
        MapSqlParameterSource p = new MapSqlParameterSource().addValue("limit", Math.max(1, Math.min(limit, 500)));
        if (text != null && !text.isBlank()) {
            sql.append(" and (lower(e.display_name) like :q or lower(e.employee_no) like :q)");
            p.addValue("q", "%" + text.toLowerCase().trim() + "%");
        }
        if (orgUnitId != null) {
            sql.append(" and e.org_unit_id in (:units)");
            p.addValue("units", orgSubtree(orgUnitId));
        }
        sql.append(" order by e.display_name limit :limit");
        return jdbc.query(sql.toString(), p, EMPLOYEE);
    }

    @Override
    public List<EmployeeView> activeEmployees() {
        return jdbc.query(EMPLOYEE_SELECT + " where e.status in ('ACTIVE', 'ON_LEAVE') order by e.display_name", Map.of(), EMPLOYEE);
    }

    @Override
    public Set<Long> directReports(Long supervisorId) {
        return new HashSet<>(jdbc.queryForList("select id from employee where supervisor_id = :s", Map.of("s", supervisorId), Long.class));
    }

    @Override
    public Set<Long> orgSubtree(Long orgUnitId) {
        return new HashSet<>(jdbc.queryForList("""
                with recursive t(id) as (select id from org_unit where id = :id
                                         union select o.id from org_unit o join t on o.parent_id = t.id)
                select id from t""", Map.of("id", orgUnitId), Long.class));
    }

    @Override
    public Set<Long> employeesInOrgUnits(Collection<Long> orgUnitIds) {
        if (orgUnitIds == null || orgUnitIds.isEmpty()) {
            return Set.of();
        }
        return new HashSet<>(jdbc.queryForList("select id from employee where org_unit_id in (:u)", Map.of("u", orgUnitIds), Long.class));
    }

    @Override
    public Set<Long> unitsHeadedBy(Long employeeId) {
        return new HashSet<>(jdbc.queryForList("select id from org_unit where head_employee_id = :e", Map.of("e", employeeId), Long.class));
    }

    @Override
    public Optional<Long> headOfDepartmentFor(Long employeeId) {
        List<Long> heads = jdbc.queryForList("""
                with recursive up(id, parent_id, head, depth) as (
                    select o.id, o.parent_id, o.head_employee_id, 0 from org_unit o join employee e on e.org_unit_id = o.id where e.id = :e
                    union all
                    select o.id, o.parent_id, o.head_employee_id, up.depth + 1 from org_unit o join up on o.id = up.parent_id)
                select head from up where head is not null and head <> :e order by depth limit 1""", Map.of("e", employeeId), Long.class);
        return heads.stream().findFirst();
    }

    @Override
    public Optional<OrgUnitView> orgUnit(Long orgUnitId) {
        return jdbc.query("select * from org_unit where id = :id", Map.of("id", orgUnitId), ORG_UNIT).stream().findFirst();
    }

    @Override
    public List<OrgUnitView> orgUnits() {
        return jdbc.query("select * from org_unit order by name", ORG_UNIT);
    }

    @Override
    public Optional<PositionView> position(Long positionId) {
        return jdbc.query(POSITION_SELECT + " where p.id = :id", Map.of("id", positionId), POSITION).stream().findFirst();
    }

    @Override
    public List<PositionView> positions(String positionTypeOrNull) {
        if (positionTypeOrNull == null || positionTypeOrNull.isBlank()) {
            return jdbc.query(POSITION_SELECT + " order by p.title", POSITION);
        }
        return jdbc.query(POSITION_SELECT + " where p.position_type = :t order by p.title", Map.of("t", positionTypeOrNull), POSITION);
    }

    @Override
    public Optional<JobView> job(Long jobId) {
        return jdbc.query("select * from job where id = :id", Map.of("id", jobId), JOB).stream().findFirst();
    }

    @Override
    public Optional<JobView> jobByCode(String code) {
        return jdbc.query("select * from job where code = :c", Map.of("c", code), JOB).stream().findFirst();
    }

    @Override
    public List<JobView> jobs() {
        return jdbc.query("select * from job order by title", JOB);
    }

    @Override
    public List<CompetencyView> competencies() {
        return jdbc.query("select * from competency order by category, name", COMPETENCY);
    }

    @Override
    public Optional<CompetencyView> competency(String code) {
        return jdbc.query("select * from competency where code = :c", Map.of("c", code), COMPETENCY).stream().findFirst();
    }

    // ---- EmployeeIdentityResolver: SSO login → employee ---------------------------------------

    @Override
    public Optional<ResolvedIdentity> resolve(String login) {
        if (login == null) {
            return Optional.empty();
        }
        return jdbc.query(EMPLOYEE_SELECT + " where lower(e.email) = lower(:l) or e.employee_no = :l", Map.of("l", login), EMPLOYEE)
                .stream().filter(e -> e.status().isActive()).findFirst()
                .map(e -> new ResolvedIdentity(e.id(), e.email() != null ? e.email() : e.employeeNo(), e.displayName()));
    }

    @Override
    public Optional<ResolvedIdentity> byEmployeeId(Long employeeId) {
        return find(employeeId).filter(e -> e.status().isActive())
                .map(e -> new ResolvedIdentity(e.id(), e.email() != null ? e.email() : e.employeeNo(), e.displayName()));
    }

    // ---- row mappers ------------------------------------------------------------------------

    private static Long longOrNull(ResultSet rs, String col) throws SQLException {
        long v = rs.getLong(col);
        return rs.wasNull() ? null : v;
    }

    static final RowMapper<EmployeeView> EMPLOYEE = (rs, i) -> new EmployeeView(
            rs.getLong("id"), rs.getString("employee_no"), rs.getString("display_name"), rs.getString("first_name"),
            rs.getString("last_name"), rs.getString("email"), rs.getString("picture_url"), rs.getString("grade_code"),
            longOrNull(rs, "job_id"), rs.getString("job_code"), rs.getString("job_title"),
            longOrNull(rs, "position_id"), rs.getString("position_title"), longOrNull(rs, "org_unit_id"), rs.getString("org_unit_name"),
            longOrNull(rs, "supervisor_id"), rs.getString("supervisor_name"), EmployeeStatus.valueOf(rs.getString("status")),
            rs.getDate("join_date") == null ? null : rs.getDate("join_date").toLocalDate(),
            rs.getString("employment_category"), rs.getString("location"), rs.getString("work_station"),
            rs.getString("country"), rs.getString("cost_centre"));

    static final RowMapper<OrgUnitView> ORG_UNIT = (rs, i) -> new OrgUnitView(rs.getLong("id"), rs.getString("code"),
            rs.getString("name"), rs.getString("type"), longOrNull(rs, "parent_id"), longOrNull(rs, "head_employee_id"),
            rs.getString("cost_centre"), rs.getString("location"), rs.getString("country"));

    static final RowMapper<PositionView> POSITION = (rs, i) -> new PositionView(rs.getLong("id"), rs.getString("code"),
            rs.getString("title"), rs.getString("position_type"), longOrNull(rs, "org_unit_id"), rs.getString("org_unit_name"),
            longOrNull(rs, "job_id"), rs.getString("job_code"), longOrNull(rs, "incumbent_employee_id"), rs.getString("incumbent_name"),
            longOrNull(rs, "supervisor_employee_id"), rs.getString("supervisor_name"),
            rs.getString("cost_centre"), rs.getString("location"), rs.getString("country"), rs.getString("work_station"));

    static final RowMapper<JobView> JOB = (rs, i) -> new JobView(rs.getLong("id"), rs.getString("code"), rs.getString("title"),
            rs.getString("grade_code"));

    static final RowMapper<CompetencyView> COMPETENCY = (rs, i) -> new CompetencyView(rs.getLong("id"), rs.getString("code"),
            rs.getString("name"), rs.getString("category"), rs.getString("description"));
}
