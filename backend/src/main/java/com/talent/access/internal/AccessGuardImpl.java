package com.talent.access.internal;

import java.time.Clock;
import java.time.LocalDate;
import java.util.EnumMap;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.TreeSet;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Supplier;

import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Service;
import org.springframework.web.context.request.RequestAttributes;
import org.springframework.web.context.request.RequestContextHolder;

import com.talent.access.AccessInfo;
import com.talent.access.CycleRoleProvider;
import com.talent.access.PermissionMatrix;
import com.talent.access.RoleDirectory;
import com.talent.access.Roles;
import com.talent.config.ActiveConfig;
import com.talent.config.ConfigService;
import com.talent.config.ConfigType;
import com.talent.config.ConfigVersionPublished;
import com.talent.integration.EmployeeDirectory;
import com.talent.shared.security.AccessGuard;
import com.talent.shared.security.Action;
import com.talent.shared.security.CurrentUser;
import com.talent.shared.security.DataScope;
import com.talent.shared.security.ObjectType;
import com.talent.shared.security.RowFilter;
import com.talent.shared.web.ForbiddenException;

/**
 * ADR-07 enforcement: role has permission → record inside the grant's data scope → no conflict of interest.
 * Confidential fields need a grant with {@code confidential = true}, and are never shown to the employee they are about.
 */
@Service
class AccessGuardImpl implements AccessGuard, RoleDirectory {

    static final String MATRIX_CODE = "DEFAULT";

    private final ConfigService config;
    private final EmployeeDirectory directory;
    private final RoleAssignmentRepository assignments;
    private final List<CycleRoleProvider> cycleRoles;
    private final Clock clock;
    private final AtomicReference<ActiveConfig<PermissionMatrix>> matrix = new AtomicReference<>();

    AccessGuardImpl(ConfigService config, EmployeeDirectory directory, RoleAssignmentRepository assignments,
                    List<CycleRoleProvider> cycleRoles, Clock clock) {
        this.config = config;
        this.directory = directory;
        this.assignments = assignments;
        this.cycleRoles = cycleRoles;
        this.clock = clock;
    }

    @Override
    public void require(Action action, ObjectType object, Long subject) {
        if (!can(action, object, subject)) {
            throw new ForbiddenException("You are not allowed to " + action.name().toLowerCase() + " " + object.name().toLowerCase().replace('_', ' ')
                    + (subject == null ? "" : " for this employee"));
        }
    }

    @Override
    public boolean can(Action action, ObjectType object, Long subject) {
        CurrentUser user = CurrentUser.orNull();
        if (user == null) {
            return false;
        }
        if (subject != null && action.judgesSubject() && subject.equals(user.employeeId()) && !object.selfService()) {
            return false; // conflict of interest: nobody judges their own record
        }
        for (PermissionMatrix.Grant g : grants(user, object)) {
            if (g.actions().contains(action) && (subject == null || covers(user, g, subject))) {
                return true;
            }
        }
        return false;
    }

    @Override
    public boolean canViewConfidential(ObjectType object, Long subject) {
        CurrentUser user = CurrentUser.orNull();
        if (user == null || subject == null || subject.equals(user.employeeId())) {
            return false; // employees never see TALENT_CONFIDENTIAL fields about themselves
        }
        for (PermissionMatrix.Grant g : grants(user, object)) {
            if (g.confidential() && g.actions().contains(Action.VIEW) && covers(user, g, subject)) {
                return true;
            }
        }
        return false;
    }

    @Override
    public RowFilter rowFilter(Action action, ObjectType object) {
        CurrentUser user = CurrentUser.orNull();
        if (user == null) {
            return RowFilter.NONE;
        }
        Set<Long> ids = new HashSet<>();
        for (PermissionMatrix.Grant g : grants(user, object)) {
            if (!g.actions().contains(action)) {
                continue;
            }
            if (g.scope() == DataScope.ALL) {
                return RowFilter.ALL;
            }
            ids.addAll(scopeMembers(user, g));
        }
        if (action.judgesSubject() && !object.selfService()) {
            ids.remove(user.employeeId());
        }
        return ids.isEmpty() ? RowFilter.NONE : new RowFilter(false, Set.copyOf(ids));
    }

    AccessInfo info() {
        CurrentUser user = CurrentUser.get();
        Map<String, Set<String>> perms = new TreeMap<>();
        for (ObjectType o : ObjectType.values()) {
            Set<String> actions = new TreeSet<>();
            grants(user, o).forEach(g -> g.actions().forEach(a -> actions.add(a.name())));
            if (!actions.isEmpty()) {
                perms.put(o.name(), actions);
            }
        }
        return new AccessInfo(user.employeeId(), user.displayName(), new TreeSet<>(effectiveRoles(user)), perms);
    }

    // ---- RoleDirectory ----------------------------------------------------------------------

    @Override
    public Set<Long> holdersOf(String role) {
        return assignments.holdersOf(role, LocalDate.now(clock));
    }

    @Override
    public Set<String> effectiveRoles(Long employeeId) {
        Set<String> roles = new LinkedHashSet<>();
        roles.add(Roles.EMPLOYEE);
        roles.addAll(assignments.rolesOf(employeeId, LocalDate.now(clock)));
        if (!directory.directReports(employeeId).isEmpty()) {
            roles.add(Roles.SUPERVISOR);
        }
        if (!directory.unitsHeadedBy(employeeId).isEmpty()) {
            roles.add(Roles.HOD);
        }
        cycleRoles.forEach(p -> roles.addAll(p.rolesOf(employeeId)));
        return roles;
    }

    // ---- internals --------------------------------------------------------------------------

    private Set<String> effectiveRoles(CurrentUser user) {
        return perRequest("talent.roles", () -> {
            Set<String> roles = new LinkedHashSet<>(effectiveRoles(user.employeeId()));
            roles.addAll(user.roles());
            return roles;
        });
    }

    private List<PermissionMatrix.Grant> grants(CurrentUser user, ObjectType object) {
        Map<ObjectType, List<PermissionMatrix.Grant>> byObject = perRequest("talent.grants", () -> {
            Set<String> roles = effectiveRoles(user);
            Map<ObjectType, List<PermissionMatrix.Grant>> m = new EnumMap<>(ObjectType.class);
            for (PermissionMatrix.Grant g : matrix().payload().grants()) {
                if (roles.contains(g.role())) {
                    m.computeIfAbsent(g.object(), k -> new java.util.ArrayList<>()).add(g);
                }
            }
            return m;
        });
        return byObject.getOrDefault(object, List.of());
    }

    private boolean covers(CurrentUser user, PermissionMatrix.Grant g, Long subject) {
        return g.scope() == DataScope.ALL || scopeMembers(user, g).contains(subject);
    }

    private Set<Long> scopeMembers(CurrentUser user, PermissionMatrix.Grant g) {
        Long me = user.employeeId();
        return switch (g.scope()) {
            case SELF -> Set.of(me);
            case DIRECT_REPORTS -> perRequest("talent.reports", () -> directory.directReports(me));
            case DEPT_TREE -> perRequest("talent.dept", () -> {
                Set<Long> units = new HashSet<>();
                directory.unitsHeadedBy(me).forEach(u -> units.addAll(directory.orgSubtree(u)));
                return directory.employeesInOrgUnits(units);
            });
            case CYCLE_ASSIGNED -> perRequest("talent.cycle." + g.role() + "." + g.object(), () -> {
                Set<Long> ids = new HashSet<>();
                cycleRoles.forEach(p -> ids.addAll(p.assignedSubjects(me, g.role(), g.object())));
                return ids;
            });
            case ALL -> Set.of();
        };
    }

    private ActiveConfig<PermissionMatrix> matrix() {
        ActiveConfig<PermissionMatrix> m = matrix.get();
        if (m == null) {
            m = config.activePayload(ConfigType.PERMISSION_MATRIX, MATRIX_CODE, PermissionMatrix.class);
            matrix.set(m);
        }
        return m;
    }

    @EventListener
    void onPublished(ConfigVersionPublished e) {
        if (e.type() == ConfigType.PERMISSION_MATRIX) {
            matrix.set(null);
        }
    }

    /** Memoises within one HTTP request; recomputes every time outside a request. */
    @SuppressWarnings("unchecked")
    private static <T> T perRequest(String key, Supplier<T> supplier) {
        RequestAttributes attrs = RequestContextHolder.getRequestAttributes();
        if (attrs == null) {
            return supplier.get();
        }
        Object v = attrs.getAttribute(key, RequestAttributes.SCOPE_REQUEST);
        if (v == null) {
            v = supplier.get();
            attrs.setAttribute(key, v, RequestAttributes.SCOPE_REQUEST);
        }
        return (T) v;
    }
}
