package com.talent.idp;

import java.time.LocalDate;

public final class IdpEvents {

    private IdpEvents() {
    }

    /** Alert #3: supervisor submitted a (revised) plan; goes to HR Approver after employee acknowledgement. */
    public record IdpSubmitted(Long idpId, Long employeeId, Long supervisorId) {
    }

    /** Alert #4: HR approved; notify the direct supervisor. */
    public record IdpApproved(Long idpId, Long employeeId, Long supervisorId) {
    }

    public record IdpReturned(Long idpId, Long employeeId, Long supervisorId, String comment) {
    }

    public record IdpActionOverdue(Long idpId, Long actionId, Long employeeId, Long supervisorId, LocalDate dueDate) {
    }

    public record IdpReviewDue(Long idpId, Long employeeId, Long supervisorId, LocalDate reviewDate) {
    }
}
