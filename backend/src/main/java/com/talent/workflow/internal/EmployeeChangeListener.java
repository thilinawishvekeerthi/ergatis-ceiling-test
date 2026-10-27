package com.talent.workflow.internal;

import org.springframework.modulith.events.ApplicationModuleListener;
import org.springframework.stereotype.Component;

import com.talent.integration.IntegrationEvents;
import com.talent.workflow.WorkflowService;

/** Mid-cycle changes (§3.1 rule 5): when an actor leaves, their open tasks move to whoever now resolves. */
@Component
class EmployeeChangeListener {

    private final WorkflowService workflow;

    EmployeeChangeListener(WorkflowService workflow) {
        this.workflow = workflow;
    }

    @ApplicationModuleListener
    void on(IntegrationEvents.EmployeeStatusChanged e) {
        if (!e.to().isActive()) {
            workflow.reassignOpenTasksOf(e.employeeId(), "Employee status changed to " + e.to());
        }
    }
}
