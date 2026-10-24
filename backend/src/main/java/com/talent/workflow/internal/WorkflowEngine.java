package com.talent.workflow.internal;

import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Timestamp;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.function.Function;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

import org.springframework.context.ApplicationEventPublisher;
import org.springframework.jdbc.core.RowMapper;
import org.springframework.jdbc.core.namedparam.MapSqlParameterSource;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.jdbc.support.GeneratedKeyHolder;
import org.springframework.orm.ObjectOptimisticLockingFailureException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.talent.access.RoleDirectory;
import com.talent.access.Roles;
import com.talent.config.ActiveConfig;
import com.talent.config.ConfigService;
import com.talent.config.ConfigType;
import com.talent.integration.EmployeeDirectory;
import com.talent.integration.EmployeeView;
import com.talent.shared.audit.AuditService;
import com.talent.shared.security.CurrentUser;
import com.talent.shared.security.ObjectType;
import com.talent.shared.web.BusinessRuleException;
import com.talent.shared.web.ForbiddenException;
import com.talent.shared.web.NotFoundException;
import com.talent.workflow.ActorResolver;
import com.talent.workflow.HistoryView;
import com.talent.workflow.TaskView;
import com.talent.workflow.WorkflowDefinition;
import com.talent.workflow.WorkflowEvents;
import com.talent.workflow.WorkflowInstanceView;
import com.talent.workflow.WorkflowService;
import com.talent.workflow.WorkflowSubject;

@Service
@Transactional
class WorkflowEngine implements WorkflowService {

    static final String SUBJECT = "SUBJECT";
    static final String NONE = "NONE";
    private static final Pattern ACTOR = Pattern.compile("([A-Z_]+)(?:\\(([A-Z_]+)\\))?");

    private final NamedParameterJdbcTemplate jdbc;
    private final ConfigService config;
    private final Map<String, ActorResolver> resolvers;
    private final EmployeeDirectory directory;
    private final RoleDirectory roles;
    private final AuditService audit;
    private final ApplicationEventPublisher events;
    private final Clock clock;

    WorkflowEngine(NamedParameterJdbcTemplate jdbc, ConfigService config, List<ActorResolver> resolvers, EmployeeDirectory directory,
                   RoleDirectory roles, AuditService audit, ApplicationEventPublisher events, Clock clock) {
        this.jdbc = jdbc;
        this.config = config;
        this.resolvers = resolvers.stream().collect(Collectors.toMap(ActorResolver::name, Function.identity()));
        this.directory = directory;
        this.roles = roles;
        this.audit = audit;
        this.events = events;
        this.clock = clock;
    }

    record Instance(Long id, String definitionCode, Long versionId, ObjectType objectType, Long objectId, Long subjectId, Long contextId,
                    String state, String status, Instant startedAt, Instant updatedAt, long rowVersion) {
        WorkflowSubject subject() {
            return new WorkflowSubject(objectType, objectId, subjectId, contextId);
        }
    }

    // ---- WorkflowService --------------------------------------------------------------------

    @Override
    public WorkflowInstanceView start(String definitionCode, WorkflowSubject subject) {
        if (instanceRow(subject.objectType(), subject.objectId()).isPresent()) {
            throw new BusinessRuleException("WORKFLOW_EXISTS", "A workflow is already running for this record");
        }
        ActiveConfig<WorkflowDefinition> def = config.activePayload(ConfigType.WORKFLOW, definitionCode, WorkflowDefinition.class);
        config.lock(def.versionId());
        Instant now = clock.instant();
        GeneratedKeyHolder keys = new GeneratedKeyHolder();
        jdbc.update("""
                insert into workflow_instance (definition_code, definition_version_id, object_type, object_id, subject_employee_id,
                    context_id, state, status, started_at, updated_at, row_version)
                values (:code, :v, :ot, :oid, :sub, :ctx, :state, 'ACTIVE', :now, :now, 0)""", new MapSqlParameterSource()
                .addValue("code", definitionCode).addValue("v", def.versionId()).addValue("ot", subject.objectType().name())
                .addValue("oid", subject.objectId()).addValue("sub", subject.subjectEmployeeId()).addValue("ctx", subject.contextId())
                .addValue("state", def.payload().initialState()).addValue("now", Timestamp.from(now)), keys, new String[]{"id"});
        Instance inst = load(keys.getKey().longValue());
        createTasks(inst, def.payload());
        return view(inst);
    }

    @Override
    public WorkflowInstanceView transition(Long instanceId, String action, String comment) {
        CurrentUser user = CurrentUser.get();
        Instance inst = load(instanceId);
        WorkflowDefinition def = definition(inst);
        WorkflowDefinition.Transition t = transitionFor(def, inst, action);
        if (t.system()) {
            throw new ForbiddenException("Action " + action + " is done by the system");
        }
        Long taskId = actingTask(inst, user.employeeId())
                .orElseThrow(() -> new ForbiddenException("You have no open task on this record"));
        return fire(inst, def, t, user.employeeId(), taskId, comment);
    }

    @Override
    public WorkflowInstanceView transitionAsSystem(Long instanceId, String action, String comment) {
        Instance inst = load(instanceId);
        WorkflowDefinition def = definition(inst);
        return fire(inst, def, transitionFor(def, inst, action), null, null, comment);
    }

    @Override
    @Transactional(readOnly = true)
    public WorkflowInstanceView instance(Long instanceId) {
        return view(load(instanceId));
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<WorkflowInstanceView> instanceFor(ObjectType objectType, Long objectId) {
        return instanceRow(objectType, objectId).map(this::view);
    }

    @Override
    @Transactional(readOnly = true)
    public List<String> availableActions(Long instanceId) {
        CurrentUser user = CurrentUser.orNull();
        Instance inst = load(instanceId);
        if (user == null || !"ACTIVE".equals(inst.status()) || actingTask(inst, user.employeeId()).isEmpty()) {
            return List.of();
        }
        return definition(inst).from(inst.state()).stream().filter(t -> !t.system()).map(WorkflowDefinition.Transition::action).toList();
    }

    @Override
    @Transactional(readOnly = true)
    public List<HistoryView> history(Long instanceId) {
        return jdbc.query("""
                select h.*, e.display_name as actor_name from workflow_history h left join employee e on e.id = h.actor_employee_id
                where h.instance_id = :i order by h.id""", Map.of("i", instanceId), (rs, i) -> new HistoryView(rs.getLong("id"),
                rs.getString("from_state"), rs.getString("action"), rs.getString("to_state"), longOrNull(rs, "actor_employee_id"),
                rs.getString("actor_name"), rs.getString("comment"), rs.getTimestamp("at").toInstant()));
    }

    @Override
    @Transactional(readOnly = true)
    public List<TaskView> myTasks() {
        Long me = CurrentUser.get().employeeId();
        Set<Long> owners = new LinkedHashSet<>();
        owners.add(me);
        owners.addAll(delegatorsOf(me));
        return tasks("t.status = 'OPEN' and t.assignee_employee_id in (:owners) and coalesce(i.subject_employee_id, -1) <> :me",
                new MapSqlParameterSource().addValue("owners", owners).addValue("me", me), me);
    }

    @Override
    public int reassignOpenTasksOf(Long employeeId, String reason) {
        List<Map<String, Object>> open = jdbc.queryForList("select id, instance_id from workflow_task where status = 'OPEN' and assignee_employee_id = :e",
                Map.of("e", employeeId));
        for (Map<String, Object> t : open) {
            Instance inst = load(((Number) t.get("instance_id")).longValue());
            WorkflowDefinition def = definition(inst);
            Set<Long> actors = resolveActors(inst, def.state(inst.state()).orElseThrow().actor());
            actors.remove(employeeId);
            if (actors.isEmpty()) {
                actors = fallbackActors(inst);
            }
            Long taskId = ((Number) t.get("id")).longValue();
            jdbc.update("update workflow_task set status = 'CANCELLED', completed_at = :now where id = :id",
                    Map.of("now", Timestamp.from(clock.instant()), "id", taskId));
            for (Long a : actors) {
                insertTask(inst, def, a, employeeId);
            }
            audit.record("TASK_REASSIGNED", "WORKFLOW_TASK", taskId, inst.subjectId(), Map.of("assignee", employeeId),
                    Map.of("assignees", actors), reason);
        }
        return open.size();
    }

    @Override
    public void cancel(Long instanceId, String reason) {
        Instance inst = load(instanceId);
        Instant now = clock.instant();
        jdbc.update("update workflow_task set status = 'CANCELLED', completed_at = :now where instance_id = :i and status = 'OPEN'",
                Map.of("now", Timestamp.from(now), "i", instanceId));
        jdbc.update("update workflow_instance set status = 'CANCELLED', updated_at = :now, row_version = row_version + 1 where id = :i",
                Map.of("now", Timestamp.from(now), "i", instanceId));
        history(inst.id(), inst.state(), "CANCEL", inst.state(), CurrentUser.idOrNull(), reason);
    }

    // ---- admin (used by the controller) ------------------------------------------------------

    TaskView reassignTask(Long taskId, Long newAssignee, String reason) {
        Map<String, Object> t = jdbc.queryForList("select * from workflow_task where id = :id", Map.of("id", taskId)).stream().findFirst()
                .orElseThrow(() -> new NotFoundException("Task", taskId));
        if (!"OPEN".equals(t.get("status"))) {
            throw new BusinessRuleException("TASK_CLOSED", "Only open tasks can be reassigned");
        }
        Instance inst = load(((Number) t.get("instance_id")).longValue());
        if (newAssignee.equals(inst.subjectId()) && !SUBJECT.equals(actorName(definition(inst).state(inst.state()).orElseThrow().actor()))) {
            throw new BusinessRuleException("CONFLICT_OF_INTEREST", "A person cannot act on their own record");
        }
        directory.get(newAssignee);
        jdbc.update("update workflow_task set assignee_employee_id = :a, original_assignee_id = coalesce(original_assignee_id, :old) where id = :id",
                Map.of("a", newAssignee, "old", t.get("assignee_employee_id"), "id", taskId));
        audit.record("TASK_REASSIGNED", "WORKFLOW_TASK", taskId, inst.subjectId(), Map.of("assignee", t.get("assignee_employee_id")),
                Map.of("assignee", newAssignee), reason);
        publishAssigned(taskId, inst, newAssignee);
        return task(taskId);
    }

    List<TaskView> openTasks(Long contextIdOrNull) {
        MapSqlParameterSource p = new MapSqlParameterSource().addValue("ctx", contextIdOrNull);
        return tasks("t.status = 'OPEN' and (cast(:ctx as bigint) is null or i.context_id = :ctx)", p, null);
    }

    TaskView task(Long taskId) {
        return tasks("t.id = :id", new MapSqlParameterSource("id", taskId), null).stream().findFirst()
                .orElseThrow(() -> new NotFoundException("Task", taskId));
    }

    // ---- internals --------------------------------------------------------------------------

    private WorkflowInstanceView fire(Instance inst, WorkflowDefinition def, WorkflowDefinition.Transition t, Long actor, Long taskId,
                                     String comment) {
        if (t.commentRequired() && (comment == null || comment.isBlank())) {
            throw new BusinessRuleException("COMMENT_REQUIRED", "A comment is required to " + t.action().toLowerCase().replace('_', ' '));
        }
        Instant now = clock.instant();
        WorkflowDefinition.State to = def.state(t.to()).orElseThrow();
        int n = jdbc.update("""
                update workflow_instance set state = :s, status = :st, updated_at = :now, row_version = row_version + 1
                where id = :id and row_version = :v""", new MapSqlParameterSource().addValue("s", to.code())
                .addValue("st", to.terminal() ? "ENDED" : "ACTIVE").addValue("now", Timestamp.from(now))
                .addValue("id", inst.id()).addValue("v", inst.rowVersion()));
        if (n == 0) {
            throw new ObjectOptimisticLockingFailureException("workflow_instance", inst.id());
        }
        jdbc.update("""
                update workflow_task set status = case when id = :task then 'DONE' else 'CANCELLED' end,
                    completed_at = :now, completed_by = case when id = :task then cast(:actor as bigint) end, action = case when id = :task then :action end
                where instance_id = :i and status = 'OPEN'""", new MapSqlParameterSource().addValue("task", taskId == null ? -1L : taskId)
                .addValue("now", Timestamp.from(now)).addValue("actor", actor).addValue("action", t.action()).addValue("i", inst.id()));
        history(inst.id(), inst.state(), t.action(), to.code(), actor, comment);
        Instance updated = load(inst.id());
        if (!to.terminal()) {
            createTasks(updated, def);
        }
        events.publishEvent(new WorkflowEvents.Transitioned(inst.id(), inst.definitionCode(), inst.objectType(), inst.objectId(),
                inst.subjectId(), inst.contextId(), inst.state(), t.action(), to.code(), actor, comment));
        return view(updated);
    }

    private void createTasks(Instance inst, WorkflowDefinition def) {
        WorkflowDefinition.State state = def.state(inst.state()).orElseThrow();
        if (state.terminal() || NONE.equals(state.actor())) {
            return;
        }
        Set<Long> actors = resolveActors(inst, state.actor());
        if (actors.isEmpty()) {
            actors = fallbackActors(inst);
        }
        for (Long a : actors) {
            insertTask(inst, def, a, null);
        }
    }

    private void insertTask(Instance inst, WorkflowDefinition def, Long assignee, Long originalAssignee) {
        WorkflowDefinition.State state = def.state(inst.state()).orElseThrow();
        Instant now = clock.instant();
        Instant due = state.slaDays() == null ? null : now.plus(Duration.ofDays(state.slaDays()));
        GeneratedKeyHolder keys = new GeneratedKeyHolder();
        jdbc.update("""
                insert into workflow_task (instance_id, state, assignee_employee_id, original_assignee_id, status, created_at, due_at,
                    reminder_sent, escalated)
                values (:i, :s, :a, :o, 'OPEN', :now, :due, false, false)""", new MapSqlParameterSource().addValue("i", inst.id())
                .addValue("s", inst.state()).addValue("a", assignee).addValue("o", originalAssignee)
                .addValue("now", Timestamp.from(now)).addValue("due", due == null ? null : Timestamp.from(due)), keys, new String[]{"id"});
        publishAssigned(keys.getKey().longValue(), inst, assignee);
    }

    private void publishAssigned(Long taskId, Instance inst, Long assignee) {
        Timestamp due = jdbc.queryForObject("select due_at from workflow_task where id = :id", Map.of("id", taskId), Timestamp.class);
        events.publishEvent(new WorkflowEvents.TaskAssigned(taskId, inst.id(), inst.definitionCode(), inst.objectType(), inst.objectId(),
                inst.subjectId(), inst.contextId(), inst.state(), assignee, due == null ? null : due.toInstant()));
    }

    /** Resolves the actor expression and applies the conflict-of-interest guard and inactive-employee filter. */
    private Set<Long> resolveActors(Instance inst, String expression) {
        String name = actorName(expression);
        Matcher m = ACTOR.matcher(expression);
        if (!m.matches()) {
            throw new IllegalStateException("Bad actor expression " + expression);
        }
        ActorResolver resolver = resolvers.get(name);
        if (resolver == null) {
            throw new IllegalStateException("No actor resolver named " + name);
        }
        Set<Long> ids = new LinkedHashSet<>(resolver.resolve(inst.subject(), m.group(2)));
        if (!SUBJECT.equals(name) && inst.subjectId() != null) {
            ids.remove(inst.subjectId());
        }
        Map<Long, EmployeeView> people = directory.findAll(ids);
        ids.removeIf(id -> !people.containsKey(id) || !people.get(id).status().isActive());
        return ids;
    }

    /** Nobody resolved (e.g. no supervisor): HR admins get the task so the process never stalls. */
    private Set<Long> fallbackActors(Instance inst) {
        Set<Long> admins = new LinkedHashSet<>(roles.holdersOf(Roles.HR_ADMIN));
        admins.remove(inst.subjectId());
        if (admins.isEmpty()) {
            throw new BusinessRuleException("NO_ACTOR", "Nobody can take the next step on this record. Ask HR to assign one.");
        }
        return admins;
    }

    private static String actorName(String expression) {
        int i = expression.indexOf('(');
        return i < 0 ? expression : expression.substring(0, i);
    }

    private Optional<Long> actingTask(Instance inst, Long me) {
        if (!SUBJECT.equals(actorName(definition(inst).state(inst.state()).map(WorkflowDefinition.State::actor).orElse(NONE)))
                && me.equals(inst.subjectId())) {
            return Optional.empty(); // conflict of interest, even through delegation
        }
        Set<Long> owners = new LinkedHashSet<>();
        owners.add(me);
        owners.addAll(delegatorsOf(me));
        return jdbc.queryForList("select id from workflow_task where instance_id = :i and status = 'OPEN' and assignee_employee_id in (:o) order by id",
                new MapSqlParameterSource().addValue("i", inst.id()).addValue("o", owners), Long.class).stream().findFirst();
    }

    Set<Long> delegatorsOf(Long delegate) {
        LocalDate today = LocalDate.now(clock);
        return new LinkedHashSet<>(jdbc.queryForList("""
                select delegator_id from delegation where delegate_id = :d and valid_from <= :t and valid_to >= :t""",
                Map.of("d", delegate, "t", today), Long.class));
    }

    private WorkflowDefinition.Transition transitionFor(WorkflowDefinition def, Instance inst, String action) {
        if (!"ACTIVE".equals(inst.status())) {
            throw new BusinessRuleException("WORKFLOW_ENDED", "This workflow has ended");
        }
        return def.from(inst.state()).stream().filter(t -> t.action().equals(action)).findFirst()
                .orElseThrow(() -> new BusinessRuleException("INVALID_ACTION", "Action " + action + " is not allowed in state " + inst.state()));
    }

    private WorkflowDefinition definition(Instance inst) {
        return config.payload(inst.versionId(), WorkflowDefinition.class);
    }

    private void history(Long instanceId, String from, String action, String to, Long actor, String comment) {
        jdbc.update("""
                insert into workflow_history (instance_id, from_state, action, to_state, actor_employee_id, comment, at)
                values (:i, :f, :a, :t, :actor, :c, :now)""", new MapSqlParameterSource().addValue("i", instanceId).addValue("f", from)
                .addValue("a", action).addValue("t", to).addValue("actor", actor).addValue("c", comment)
                .addValue("now", Timestamp.from(clock.instant())));
    }

    private Instance load(Long id) {
        return jdbc.query("select * from workflow_instance where id = :id", Map.of("id", id), INSTANCE).stream().findFirst()
                .orElseThrow(() -> new NotFoundException("Workflow", id));
    }

    private Optional<Instance> instanceRow(ObjectType type, Long objectId) {
        return jdbc.query("select * from workflow_instance where object_type = :t and object_id = :o and status <> 'CANCELLED'",
                Map.of("t", type.name(), "o", objectId), INSTANCE).stream().findFirst();
    }

    private WorkflowInstanceView view(Instance inst) {
        WorkflowDefinition.State s = definition(inst).state(inst.state()).orElseThrow();
        List<TaskView> open = tasks("t.instance_id = :i and t.status = 'OPEN'", new MapSqlParameterSource("i", inst.id()), null);
        return new WorkflowInstanceView(inst.id(), inst.definitionCode(), inst.versionId(), inst.objectType(), inst.objectId(),
                inst.subjectId(), inst.contextId(), inst.state(), s.label() == null ? s.code() : s.label(), s.terminal(),
                inst.startedAt(), inst.updatedAt(), open);
    }

    private List<TaskView> tasks(String where, MapSqlParameterSource params, Long viewer) {
        Instant now = clock.instant();
        Map<Long, WorkflowDefinition> defs = new java.util.HashMap<>();
        List<TaskView> out = new ArrayList<>();
        jdbc.query("""
                select t.*, i.definition_code, i.definition_version_id, i.object_type, i.object_id, i.subject_employee_id, i.context_id,
                       s.display_name as subject_name, a.display_name as assignee_name
                from workflow_task t join workflow_instance i on i.id = t.instance_id
                left join employee s on s.id = i.subject_employee_id
                left join employee a on a.id = t.assignee_employee_id
                where """ + where + " order by t.due_at nulls last, t.id", params, rs -> {
            Long versionId = rs.getLong("definition_version_id");
            WorkflowDefinition def = defs.computeIfAbsent(versionId, v -> config.payload(v, WorkflowDefinition.class));
            String state = rs.getString("state");
            String label = def.state(state).map(WorkflowDefinition.State::label).orElse(state);
            Timestamp due = rs.getTimestamp("due_at");
            Long assignee = rs.getLong("assignee_employee_id");
            out.add(new TaskView(rs.getLong("id"), rs.getLong("instance_id"), rs.getString("definition_code"),
                    ObjectType.valueOf(rs.getString("object_type")), rs.getLong("object_id"), longOrNull(rs, "subject_employee_id"),
                    rs.getString("subject_name"), longOrNull(rs, "context_id"), state, label == null ? state : label, assignee,
                    rs.getString("assignee_name"), longOrNull(rs, "original_assignee_id"), rs.getTimestamp("created_at").toInstant(),
                    due == null ? null : due.toInstant(), "OPEN".equals(rs.getString("status")) && due != null && due.toInstant().isBefore(now),
                    rs.getString("status"), viewer != null && !viewer.equals(assignee)));
        });
        return out;
    }

    private static Long longOrNull(ResultSet rs, String col) throws SQLException {
        long v = rs.getLong(col);
        return rs.wasNull() ? null : v;
    }

    private static final RowMapper<Instance> INSTANCE = (rs, i) -> new Instance(rs.getLong("id"), rs.getString("definition_code"),
            rs.getLong("definition_version_id"), ObjectType.valueOf(rs.getString("object_type")), rs.getLong("object_id"),
            longOrNull(rs, "subject_employee_id"), longOrNull(rs, "context_id"), rs.getString("state"), rs.getString("status"),
            rs.getTimestamp("started_at").toInstant(), rs.getTimestamp("updated_at").toInstant(), rs.getLong("row_version"));
}
