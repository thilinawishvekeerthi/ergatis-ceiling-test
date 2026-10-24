package com.talent.access.internal;

import java.time.Clock;
import java.time.LocalDate;
import java.util.List;
import java.util.Map;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Component;

import com.talent.integration.EmployeeDirectory;
import com.talent.integration.IntegrationEvents;

/**
 * Seeds global role assignments by employee number after the first Core HR sync, e.g.
 * {@code talent.access.seed-roles.E0003=HR_ADMIN}. Existing assignments are kept.
 */
@Component
class DevRoleSeeder {

    @ConfigurationProperties("talent.access")
    record SeedProperties(Map<String, List<String>> seedRoles) {
        SeedProperties {
            seedRoles = seedRoles == null ? Map.of() : seedRoles;
        }
    }

    private final SeedProperties props;
    private final EmployeeDirectory directory;
    private final RoleAssignmentRepository assignments;
    private final Clock clock;

    DevRoleSeeder(SeedProperties props, EmployeeDirectory directory, RoleAssignmentRepository assignments, Clock clock) {
        this.props = props;
        this.directory = directory;
        this.assignments = assignments;
        this.clock = clock;
    }

    @EventListener
    void onSync(IntegrationEvents.SyncCompleted e) {
        if (!"CORE_HR".equals(e.system())) {
            return;
        }
        props.seedRoles().forEach((employeeNo, roles) -> directory.findByEmployeeNo(employeeNo.toUpperCase()).ifPresent(emp -> roles.forEach(role -> {
            if (!assignments.rolesOf(emp.id(), LocalDate.now(clock)).contains(role)) {
                assignments.add(emp.id(), role, "SEED", null, null);
            }
        })));
    }
}
