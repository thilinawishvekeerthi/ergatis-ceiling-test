package com.talent.access.internal;

import java.time.LocalDate;
import java.util.List;
import java.util.Map;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.talent.access.AccessInfo;
import com.talent.access.Roles;
import com.talent.integration.EmployeeDirectory;
import com.talent.shared.audit.AuditEntry;
import com.talent.shared.audit.AuditService;
import com.talent.shared.security.AccessGuard;
import com.talent.shared.security.Action;
import com.talent.shared.security.ObjectType;
import com.talent.shared.web.BusinessRuleException;
import com.talent.shared.web.NotFoundException;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

@RestController
@RequestMapping("/api/access")
class AccessController {

    /** Global roles that can be assigned here. Cycle roles are assigned inside each cycle. */
    static final List<String> ASSIGNABLE = List.of(Roles.HR_ADMIN, Roles.HR_APPROVER);

    private final AccessGuardImpl guard;
    private final AccessGuard access;
    private final RoleAssignmentRepository assignments;
    private final EmployeeDirectory directory;
    private final AuditService audit;

    AccessController(AccessGuardImpl guard, AccessGuard access, RoleAssignmentRepository assignments,
                     EmployeeDirectory directory, AuditService audit) {
        this.guard = guard;
        this.access = access;
        this.assignments = assignments;
        this.directory = directory;
        this.audit = audit;
    }

    @GetMapping("/me")
    AccessInfo me() {
        return guard.info();
    }

    @GetMapping("/role-assignments")
    List<Map<String, Object>> roleAssignments() {
        access.require(Action.VIEW, ObjectType.CONFIG, null);
        return assignments.all();
    }

    record AssignRole(@NotNull Long employeeId, @NotBlank String role, LocalDate validFrom, LocalDate validTo) {
    }

    @PostMapping("/role-assignments")
    @Transactional
    void assign(@Valid @RequestBody AssignRole r) {
        access.require(Action.EDIT, ObjectType.CONFIG, null);
        if (!ASSIGNABLE.contains(r.role())) {
            throw new BusinessRuleException("ROLE_NOT_ASSIGNABLE", "Only " + ASSIGNABLE + " can be assigned here");
        }
        directory.get(r.employeeId());
        assignments.add(r.employeeId(), r.role(), "ADMIN", r.validFrom(), r.validTo());
        audit.record("ROLE_ASSIGNED", "ROLE", r.role(), r.employeeId(), null, r, null);
    }

    @DeleteMapping("/role-assignments/{id}")
    @Transactional
    void remove(@PathVariable Long id) {
        access.require(Action.EDIT, ObjectType.CONFIG, null);
        Map<String, Object> before = assignments.find(id);
        if (before == null) {
            throw new NotFoundException("Role assignment", id);
        }
        assignments.remove(id);
        audit.record("ROLE_REMOVED", "ROLE", before.get("role"), ((Number) before.get("employee_id")).longValue(), before, null, null);
    }

    @GetMapping("/audit")
    Page<AuditEntry> audit(@RequestParam(required = false) String objectType, @RequestParam(required = false) String objectId,
                           @RequestParam(required = false) Long subjectEmployeeId,
                           @RequestParam(defaultValue = "0") int page, @RequestParam(defaultValue = "50") int size) {
        access.require(Action.VIEW, ObjectType.AUDIT, null);
        return audit.search(objectType, objectId, subjectEmployeeId, PageRequest.of(page, Math.min(size, 200)));
    }
}
