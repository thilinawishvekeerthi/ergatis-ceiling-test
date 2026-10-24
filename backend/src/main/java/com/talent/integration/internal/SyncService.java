package com.talent.integration.internal;

import java.sql.Timestamp;
import java.time.Clock;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.jdbc.core.namedparam.MapSqlParameterSource;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.jdbc.support.GeneratedKeyHolder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.TransactionDefinition;
import org.springframework.transaction.support.TransactionTemplate;

import com.talent.integration.IntegrationEvents;
import com.talent.shared.json.Json;
import com.talent.shared.web.BusinessRuleException;
import com.talent.shared.web.NotFoundException;

/**
 * Delta sync into the local read tables (ADR-08). Each record loads in its own transaction, so one bad record
 * goes to the dead-letter table and never stops the run (principle 6: integration failure must not stop users).
 */
@Service
class SyncService {

    private static final Logger log = LoggerFactory.getLogger(SyncService.class);

    private final List<SourceAdapter> adapters;
    private final RecordLoader loader;
    private final NamedParameterJdbcTemplate jdbc;
    private final TransactionTemplate perRecord;
    private final ApplicationEventPublisher events;
    private final Clock clock;

    SyncService(List<SourceAdapter> adapters, RecordLoader loader, NamedParameterJdbcTemplate jdbc,
                PlatformTransactionManager tm, ApplicationEventPublisher events, Clock clock) {
        this.adapters = adapters;
        this.loader = loader;
        this.jdbc = jdbc;
        this.perRecord = new TransactionTemplate(tm);
        this.perRecord.setPropagationBehavior(TransactionDefinition.PROPAGATION_REQUIRES_NEW);
        this.events = events;
        this.clock = clock;
    }

    record RunResult(Long runId, String system, long read, long upserted, long failed, String status, String message) {
    }

    List<RunResult> syncAll(String trigger) {
        return adapters.stream().map(a -> sync(a, trigger)).toList();
    }

    RunResult sync(String system, String trigger) {
        return sync(adapter(system), trigger);
    }

    private RunResult sync(SourceAdapter adapter, String trigger) {
        Instant since = lastSuccessfulStart(adapter.system());
        Instant started = clock.instant();
        Long runId = startRun(adapter.system(), trigger, started);
        long read = 0;
        long ok = 0;
        long failed = 0;
        String status = "SUCCESS";
        String message = null;
        try {
            List<Object[]> loaded = new ArrayList<>();
            for (EntityKind kind : EntityKind.values()) {
                if (!adapter.kinds().contains(kind)) {
                    continue;
                }
                for (Map<String, Object> r : adapter.fetch(kind, since)) {
                    read++;
                    if (loadOne(adapter.system(), kind, r)) {
                        ok++;
                        loaded.add(new Object[]{kind, r});
                    } else {
                        failed++;
                    }
                }
            }
            for (Object[] l : loaded) {
                linkOne(adapter.system(), (EntityKind) l[0], cast(l[1]));
            }
            if (failed > 0) {
                status = "PARTIAL";
            }
        } catch (RuntimeException e) {
            log.error("Sync of {} failed", adapter.system(), e);
            status = "FAILED";
            message = e.getMessage();
        }
        finishRun(runId, status, read, ok, failed, message);
        events.publishEvent(new IntegrationEvents.SyncCompleted(adapter.system(), read, ok, failed));
        return new RunResult(runId, adapter.system(), read, ok, failed, status, message);
    }

    /** On-demand refresh of one employee from every system (ADR-08 "optional on-demand refresh"). */
    int refreshEmployee(String employeeNo) {
        int count = 0;
        for (SourceAdapter a : adapters) {
            for (EntityKind kind : EntityKind.values()) {
                if (!a.kinds().contains(kind)) {
                    continue;
                }
                String ref = kind == EntityKind.EMPLOYEE ? externalIdOfEmployee(a.system(), employeeNo) : employeeNo;
                for (Map<String, Object> r : a.fetchForEmployee(kind, ref)) {
                    if (loadOne(a.system(), kind, r)) {
                        linkOne(a.system(), kind, r);
                        count++;
                    }
                }
            }
        }
        return count;
    }

    /** Re-processes one dead-lettered record, e.g. after an admin added an ID mapping. */
    boolean retry(Long deadLetterId) {
        Map<String, Object> dl = jdbc.queryForList("select * from sync_dead_letter where id = :id", Map.of("id", deadLetterId))
                .stream().findFirst().orElseThrow(() -> new NotFoundException("Dead letter", deadLetterId));
        if (!"OPEN".equals(dl.get("status"))) {
            throw new BusinessRuleException("NOT_OPEN", "This record is already resolved");
        }
        Map<String, Object> record = cast(Json.read((String) dl.get("payload").toString(), Map.class));
        EntityKind kind = EntityKind.valueOf((String) dl.get("entity"));
        String system = (String) dl.get("system");
        if (loadOne(system, kind, record)) {
            linkOne(system, kind, record);
            return true;
        }
        return false;
    }

    private boolean loadOne(String system, EntityKind kind, Map<String, Object> r) {
        try {
            perRecord.executeWithoutResult(s -> loader.load(system, kind, r));
            resolveDeadLetter(system, kind, r);
            return true;
        } catch (RuntimeException e) {
            deadLetter(system, kind, r, e);
            return false;
        }
    }

    private void linkOne(String system, EntityKind kind, Map<String, Object> r) {
        try {
            perRecord.executeWithoutResult(s -> loader.link(system, kind, r));
        } catch (RuntimeException e) {
            deadLetter(system, kind, r, e);
        }
    }

    private void deadLetter(String system, EntityKind kind, Map<String, Object> r, RuntimeException e) {
        String error = e instanceof RecordLoader.RecordException ? e.getMessage() : e.getClass().getSimpleName() + ": " + e.getMessage();
        String extId = String.valueOf(r.get("id"));
        perRecord.executeWithoutResult(s -> {
            int n = jdbc.update("""
                    update sync_dead_letter set attempts = attempts + 1, error = :err, payload = cast(:p as jsonb), last_failed_at = :now
                    where system = :s and entity = :e and external_id = :x and status = 'OPEN'""", params(system, kind, extId)
                    .addValue("err", truncate(error)).addValue("p", Json.write(r)).addValue("now", Timestamp.from(clock.instant())));
            if (n == 0) {
                jdbc.update("""
                        insert into sync_dead_letter (system, entity, external_id, payload, error, attempts, status, first_failed_at, last_failed_at)
                        values (:s, :e, :x, cast(:p as jsonb), :err, 1, 'OPEN', :now, :now)""", params(system, kind, extId)
                        .addValue("err", truncate(error)).addValue("p", Json.write(r)).addValue("now", Timestamp.from(clock.instant())));
            }
        });
    }

    private void resolveDeadLetter(String system, EntityKind kind, Map<String, Object> r) {
        jdbc.update("update sync_dead_letter set status = 'RESOLVED', resolved_at = :now where system = :s and entity = :e and external_id = :x and status = 'OPEN'",
                params(system, kind, String.valueOf(r.get("id"))).addValue("now", Timestamp.from(clock.instant())));
    }

    private Instant lastSuccessfulStart(String system) {
        List<Timestamp> t = jdbc.queryForList("select max(started_at) from sync_run where system = :s and status = 'SUCCESS'",
                Map.of("s", system), Timestamp.class);
        return t.isEmpty() || t.get(0) == null ? null : t.get(0).toInstant();
    }

    private Long startRun(String system, String trigger, Instant started) {
        GeneratedKeyHolder keys = new GeneratedKeyHolder();
        jdbc.update("insert into sync_run (system, trigger, started_at, status) values (:s, :t, :at, 'RUNNING')",
                new MapSqlParameterSource().addValue("s", system).addValue("t", trigger).addValue("at", Timestamp.from(started)), keys, new String[]{"id"});
        return keys.getKey().longValue();
    }

    private void finishRun(Long id, String status, long read, long ok, long failed, String message) {
        jdbc.update("""
                update sync_run set finished_at = :at, status = :st, read_count = :r, upserted_count = :o, failed_count = :f, message = :m
                where id = :id""", new MapSqlParameterSource().addValue("at", Timestamp.from(clock.instant())).addValue("st", status)
                .addValue("r", read).addValue("o", ok).addValue("f", failed).addValue("m", truncate(message)).addValue("id", id));
    }

    private String externalIdOfEmployee(String system, String employeeNo) {
        List<String> ids = jdbc.queryForList("""
                select m.external_id from external_id_map m join employee e on e.id = m.internal_id
                where m.system = :s and m.entity = 'EMPLOYEE' and e.employee_no = :no""", Map.of("s", system, "no", employeeNo), String.class);
        return ids.isEmpty() ? employeeNo : ids.get(0);
    }

    private SourceAdapter adapter(String system) {
        return adapters.stream().filter(a -> a.system().equalsIgnoreCase(system)).findFirst()
                .orElseThrow(() -> new NotFoundException("Source system", system));
    }

    private static MapSqlParameterSource params(String system, EntityKind kind, String extId) {
        return new MapSqlParameterSource().addValue("s", system).addValue("e", kind.name()).addValue("x", extId);
    }

    private static String truncate(String s) {
        return s == null || s.length() <= 2000 ? s : s.substring(0, 2000);
    }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> cast(Object o) {
        return (Map<String, Object>) o;
    }
}
