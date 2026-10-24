package com.talent.access.internal;

import java.util.List;
import java.util.Map;

import org.springframework.beans.factory.ObjectProvider;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.talent.access.RoleDirectory;
import com.talent.integration.EmployeeDirectory;
import com.talent.integration.EmployeeView;
import com.talent.shared.security.DevTokenIssuer;
import com.talent.shared.security.SecurityProperties;
import com.talent.shared.web.ForbiddenException;
import com.talent.shared.web.NotFoundException;

/**
 * Dev / demo sign-in: pick a seeded employee and get a signed token. Disabled unless
 * {@code talent.security.mode=dev}; production uses the client's identity provider (OIDC).
 */
@RestController
@RequestMapping("/api/auth")
class DevAuthController {

    private final SecurityProperties props;
    private final ObjectProvider<DevTokenIssuer> issuer;
    private final EmployeeDirectory directory;
    private final RoleDirectory roles;
    private final RoleAssignmentRepository assignments;

    DevAuthController(SecurityProperties props, ObjectProvider<DevTokenIssuer> issuer, EmployeeDirectory directory,
                      RoleDirectory roles, RoleAssignmentRepository assignments) {
        this.props = props;
        this.issuer = issuer;
        this.directory = directory;
        this.roles = roles;
        this.assignments = assignments;
    }

    record AuthMode(String mode) {
    }

    @GetMapping("/mode")
    AuthMode mode() {
        return new AuthMode(props.mode());
    }

    record DevUser(Long employeeId, String employeeNo, String displayName, String jobTitle, String orgUnitName, List<String> roles) {
    }

    @GetMapping("/dev/users")
    List<DevUser> users() {
        requireDev();
        return directory.activeEmployees().stream()
                .map(e -> new DevUser(e.id(), e.employeeNo(), e.displayName(), e.jobTitle(), e.orgUnitName(),
                        List.copyOf(roles.effectiveRoles(e.id()))))
                .toList();
    }

    record LoginRequest(String employeeNo) {
    }

    @PostMapping("/dev/login")
    Map<String, Object> login(@RequestBody LoginRequest r) {
        requireDev();
        EmployeeView e = directory.findByEmployeeNo(r.employeeNo()).filter(x -> x.status().isActive())
                .orElseThrow(() -> new NotFoundException("Employee", r.employeeNo()));
        List<String> global = List.copyOf(assignments.rolesOf(e.id(), java.time.LocalDate.now()));
        String token = issuer.getObject().issue(e.id(), e.email() != null ? e.email() : e.employeeNo(), e.displayName(), global);
        return Map.of("token", token, "employeeId", e.id(), "displayName", e.displayName());
    }

    private void requireDev() {
        if (!props.devMode()) {
            throw new ForbiddenException("Dev sign-in is disabled");
        }
    }
}
