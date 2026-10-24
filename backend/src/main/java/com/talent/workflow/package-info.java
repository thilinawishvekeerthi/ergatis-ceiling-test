/**
 * Workflow engine (ADR-05): small in-house state machine with versioned definitions (config type WORKFLOW),
 * actor resolvers, returns with comments, delegation, reassignment, SLA timers and a conflict-of-interest guard.
 */
@org.springframework.modulith.ApplicationModule(displayName = "Workflow engine")
package com.talent.workflow;
