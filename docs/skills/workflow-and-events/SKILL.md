---
name: workflow-and-events
description: Workflow definitions, approval and return flows, actor resolvers, SLA timers, domain events, the outbox and notifications (ADR-05, ADR-10). Use when building submit/approve/return/calibrate flows, adding a workflow state, a reminder or escalation, publishing or handling a domain event, or adding an alert template or channel.
---

# Workflow, events and notifications

Source: `docs/talent-solution-architecture.md` ADR-05, ADR-10; states from `docs/talent-management-gap-analysis.md` §5.5.

## Engine choice (plug point `IWorkflowEngine`)

1. Use the **HRIS platform engine** if the week-1 spike shows it supports versioned definitions, returns and delegation.
2. Otherwise, use the **small in-house engine** in `Talent.Shared`.
   - It reads **JSON workflow definitions** from versioned config.
   - Code-defined state machine libraries don't fit, because clients must change flows without a code change.

Business modules call `IWorkflowEngine` only. They never know which engine is behind it.

## Each definition has

- States and transitions
- Actor resolvers (plug point `IActorResolver`, keyed): `DirectSupervisor`, `HodOf(dept)`, `Role(HR_APPROVER)`, `CycleAssignment(reviewer)`, `BoardHead`
- SLA timers that drive reminders and escalations (Hangfire)

## Every flow must support

- **Return with comments.** The blueprint leaves these paths out. We add them.
- **Delegation** by date range.
- **Reassignment** when an actor leaves or transfers.
- **Conflict-of-interest guard:** an actor never acts on their own record.

## Default states

| Object | States |
|---|---|
| Review Cycle | Draft → Launched → Assessment closed → Board review → Published → Closed |
| Assessment | Not started → Draft → Submitted → Approved / Returned → Calibrated → Final |
| Succession Plan | Draft → (Submitted → Approved) → Active → Archived |
| IDP | Draft → Submitted → Approved / Returned → In progress → Review due → Completed |

These are defaults. Client rules (B3) arrive as configuration.

## Concurrency

- 2 actors may act on the same record at once. Use **optimistic concurrency** (`xmin`, see `postgres-data-model`).
- A lost race returns **409** with a ProblemDetails body. The UI asks the user to reload.

## Domain events

- Name events in the **past tense**: `AssessmentSubmitted`, `BoardDecisionPublished`, `IdpApproved`, `SlaBreached`.
- Events are `sealed record`s in the module's **`.Contracts`** project. Carry IDs, not whole entities.
- Write events to the **outbox in the same transaction**. They are delivered after commit.
- Handlers are **idempotent**.

```
Submit → auth check → pinned config → scoring → workflow transition
       → audit → outbox: AssessmentSubmitted ──after commit──→ notifications
```

## Notifications

- Subscribe to events. Render **versioned, admin-editable templates**.
- Channels are a plug point (`INotificationChannel`): **email and in-app** now. Teams or SMS can be added later.
- Never hard-code alert text. A new alert = a new template + subscription.
- Never put confidential talent data in email text. Link to the app instead.
