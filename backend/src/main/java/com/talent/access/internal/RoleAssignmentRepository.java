package com.talent.access.internal;

import java.time.LocalDate;
import java.util.List;
import java.util.Map;
import java.util.Set;

import org.springframework.jdbc.core.namedparam.MapSqlParameterSource;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.stereotype.Repository;

/** Global role assignments maintained in Talent (§3.1 rule 7: role source). IdP roles are added from the token. */
@Repository
class RoleAssignmentRepository {

    private final NamedParameterJdbcTemplate jdbc;

    RoleAssignmentRepository(NamedParameterJdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    Set<String> rolesOf(Long employeeId, LocalDate today) {
        return Set.copyOf(jdbc.queryForList("""
                select role from role_assignment where employee_id = :e
                and (valid_from is null or valid_from <= :d) and (valid_to is null or valid_to >= :d)""",
                Map.of("e", employeeId, "d", today), String.class));
    }

    Set<Long> holdersOf(String role, LocalDate today) {
        return Set.copyOf(jdbc.queryForList("""
                select a.employee_id from role_assignment a join employee e on e.id = a.employee_id
                where a.role = :r and e.status in ('ACTIVE', 'ON_LEAVE')
                and (a.valid_from is null or a.valid_from <= :d) and (a.valid_to is null or a.valid_to >= :d)""",
                Map.of("r", role, "d", today), Long.class));
    }

    List<Map<String, Object>> all() {
        return jdbc.queryForList("""
                select a.id, a.employee_id, e.employee_no, e.display_name, a.role, a.source, a.valid_from, a.valid_to
                from role_assignment a join employee e on e.id = a.employee_id order by a.role, e.display_name""", Map.of());
    }

    void add(Long employeeId, String role, String source, LocalDate from, LocalDate to) {
        jdbc.update("""
                insert into role_assignment (employee_id, role, source, valid_from, valid_to) values (:e, :r, :s, :f, :t)
                on conflict (employee_id, role) do update set valid_from = excluded.valid_from, valid_to = excluded.valid_to""",
                new MapSqlParameterSource().addValue("e", employeeId).addValue("r", role).addValue("s", source)
                        .addValue("f", from).addValue("t", to));
    }

    int remove(Long id) {
        return jdbc.update("delete from role_assignment where id = :id", Map.of("id", id));
    }

    Map<String, Object> find(Long id) {
        return jdbc.queryForList("select * from role_assignment where id = :id", Map.of("id", id)).stream().findFirst().orElse(null);
    }
}
