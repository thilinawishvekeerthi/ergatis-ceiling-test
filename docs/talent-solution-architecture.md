# Talent Management Module: Solution Architecture and Key Decisions

| | |
|---|---|
| **Status** | Draft v0.1 for internal architecture review |
| **Date** | 24/10/2026 |
| **Inputs** | Talent Management Blueprint V1.0; [gap analysis](talent-management-gap-analysis.md) |
| **Approach** | Decide now wherever an architectural choice removes a client dependency. Leave open only what truly needs the client. |

---

## 1. Architectural position

The gap analysis treated B1–B5 as items the client must answer before design. **That is the wrong posture for a product team.** Architecture can remove most of them:

| Blocker | Architectural answer | Client input moves to |
|---|---|---|
| B1 Scales, nine-box | Versioned scale and grid engine (ADR-02, ADR-03) | Configuration data, before UAT |
| B2 Candidate criteria | Rule-based eligibility engine (ADR-04) | Configuration data, before UAT |
| B3 Workflow rules | Configurable workflow definitions (ADR-05) | Configuration data plus 1 workshop |
| B4 Formulas | Pluggable scoring strategies with weights (ADR-06) | Weights, before UAT |
| B5 Visibility | Permission matrix plus data scopes (ADR-07) | Matrix values, before UAT |
| B6 Integrations | Canonical model plus adapters (ADR-08) | Specs before integration build, **not before design** |
| B7–B12 | Not architectural. Still client-dependent. | No change |

**Result:** functional and technical design can start now. The critical path moves from "client answers" to "integration access and environments".

---

## 2. Architecture principles

1. **Configuration over customisation.** Client-specific behaviour lives in data, not code. A second client should need no code change.
2. **Versioned, immutable configuration.** Configuration that has been used is never edited in place.
3. **Talent does not own master data.** Employee, org and competency data are read from the systems of record through adapters.
4. **Cycle-scoped snapshots.** A talent cycle freezes the inputs it used (config versions, employee attributes, appraisal scores), so results can always be explained later.
5. **Secure by default.** Talent data is deny-by-default. Every read of sensitive fields is permission-checked, and every change is audited.
6. **Integration failure must not stop users.** Users read from local, synced copies. A live call is only an optional refresh.

---

## 3. Architecture Decision Records (ADRs)

### ADR-01: Modular monolith, not microservices

- **Context:** 1 module, 1 client, a small team, inside an existing HRIS product.
- **Decision:** build Talent as a **modular monolith**: 1 deployable unit with strict internal modules (Config, Career, Assessment, Succession, IDP, Integration, Notification, Reporting).
- **Why:** lower operational cost. Simple transactions across sub-modules (e.g. board decision → talent pool → IDP).
- **Consequence:** module boundaries are enforced in code (no cross-module table access). Integration or Reporting can be split out later if needed.

### ADR-02: Versioned configuration

- **Decision:** every configurable object has `ConfigDefinition` → `ConfigVersion` (Draft → Published → Locked → Retired).
  - Applies to scales, nine-box grids, eligibility rules, scoring weights, workflows and permission matrices.
- **Rules:**
  - A version becomes **Locked** when the first cycle or record references it. After that it is immutable.
  - Changes always create a new version, copied from the previous one.
  - A cycle pins **all** of its config version IDs when it is launched.
  - Forms already in progress finish on the workflow version they started with.
- **Cross-version reporting:** each scale level maps to a stable **reporting band**, so trend reports work across versions.

```
ConfigDefinition (id, type, code, name)
  └─< ConfigVersion (id, definition_id, version_no, status, effective_from,
                     payload_json, checksum, published_by, published_at)
TalentReviewCycle (…, pinned_versions_json)
```

- **Payload storage:** typed tables for scales (heavily queried by reports). JSON payload for workflow and rule definitions (read as a whole, rarely queried).

### ADR-03: Generic grid engine (nine-box is a configuration)

- **Decision:** the "nine-box" is a configured **N × M grid**.
  - X and Y axes each bind to a scale.
  - Score ranges map to rows and columns.
  - Each cell has a label, a colour and a reporting band.
- **Supported placement modes:** `MANUAL`, `CALCULATED`, `CALCULATED_WITH_OVERRIDE`. Overrides need a reason, and both values are stored (`proposed_cell`, `final_cell`, `overridden_by`, `reason`).
- **Limits (to control test effort):** grid sizes from 2 × 2 to 5 × 5. Scales with 2 to 10 levels.

### ADR-04: Rule-based eligibility for leadership candidates

- **Decision:** candidate selection is a configurable rule set, evaluated against a **cycle snapshot** of employee and appraisal data.
- **Rule grammar:** a small, safe expression set (AND/OR of field-operator-value conditions). No free-form scripting.
  - Example: `last_n_appraisals(2).min_score >= 4 AND grade IN (M1,M2,M3) AND service_years >= 2`
- **Flow:** system suggests → admin adds or removes people with a reason → list is frozen at launch.

### ADR-05: Configurable workflow definitions

- **Decision:** use the HRIS platform's workflow engine if it supports versioned definitions, returns and delegation. If it does not, build a small state-machine service inside Talent.
- **Each definition has:** states, transitions, actor resolvers and SLA timers.
  - **Actor resolvers:** `DIRECT_SUPERVISOR`, `HOD_OF(dept)`, `ROLE(HR_APPROVER)`, `CYCLE_ASSIGNMENT(reviewer)`, `BOARD_HEAD`.
  - **SLA timers** drive reminders and escalations.
- **Built-in capabilities:** return with comments, delegation by date range, reassignment when an actor leaves, and a conflict-of-interest guard (an actor can never act on their own record).
- **Defaults shipped:** the flows in blueprint pp.10–15, plus the reject paths the blueprint leaves out.

### ADR-06: Pluggable scoring strategies

- **Decision:** % match, bench strength and IDP progress are each a **strategy interface** with configurable weights.
  - `ProfileMatchStrategy`: weighted qualification + experience + competency gap. Weights per job family. Returns a score **and a breakdown**, so HR can see why.
  - `BenchStrengthStrategy`: count-based or weighted by readiness band.
  - `IdpProgressStrategy`: equal-weight actions, weighted actions, or LMS-driven.
- **Why:** HR will challenge these numbers in UAT. A visible breakdown plus tunable weights turns arguments into configuration changes.

### ADR-07: Security model with a permission matrix, data scopes and field classes

- **Permissions:** role × action (`VIEW`, `EDIT`, `APPROVE`, `CALIBRATE`, `EXPORT`) × object, held in a versioned matrix.
- **Data scope** for each grant: `SELF`, `DIRECT_REPORTS`, `DEPT_TREE`, `CYCLE_ASSIGNED`, `ALL`.
- **Field classification:** `PUBLIC`, `INTERNAL`, `TALENT_CONFIDENTIAL` (nine-box, potential, successor status, risk of loss). Confidential fields are masked unless the grant is explicit.
- **Enforcement:** in the service layer and in report queries (row-level filters), not only in the UI.
- **Default:** employees never see `TALENT_CONFIDENTIAL` fields about themselves.
- **Audit:** append-only log with before/after values for every confidential write and every export.

### ADR-08: Canonical data model and integration adapters

- **Decision:** Talent reads only **canonical entities**: `Employee`, `OrgUnit`, `Position`, `Job`, `Competency`, `AppraisalResult`, `Qualification`, `Experience`, `Course`, `CourseEnrolment`.
- **1 adapter per source system** maps that source into the canonical model.
  - If Talent runs inside the same HRIS, the Core HR, SSHR and Performance adapters are simple database views or internal APIs.
  - The LMS adapter calls the client's 2 APIs.
- **Sync pattern:**
  - Scheduled delta sync into local read tables.
  - Optional on-demand refresh for each employee.
  - Idempotent upserts keyed on source IDs.
  - A dead-letter table and an admin dashboard for failed records.
- **ID mapping:** an `ExternalIdMap(system, entity, external_id, internal_id)` table solves the "keys do not match between systems" problem (G-L3).
- **Why:** design can start with mock adapters. The real specs only block adapter build. It also enables "plug into any HRIS" later.

### ADR-09: Snapshots for history and trends

- **Decision:**
  - At cycle launch, snapshot each candidate's grade, job, department, appraisal scores and config versions.
  - A monthly scheduled job snapshots succession readiness for each critical position and successor.
- **Why:** trend reports (blueprint p.6) cannot be rebuilt after the fact. Snapshots also make each decision explainable.

### ADR-10: Event-driven notifications

- **Decision:** sub-modules publish domain events (`AssessmentSubmitted`, `BoardDecisionPublished`, `IdpApproved`, `SlaBreached`, …) to an in-process event bus with an outbox table.
- **Notification module:** subscribes to events and renders templates. Templates are versioned and editable by admin, so alert text (G-N1) is not a blocker. It sends by email and in-app.
- **Why:** new alerts become configuration, not code. The outbox guarantees delivery after a commit.

### ADR-11: Reporting on a read model

- **Decision:** reports read from a denormalised **reporting schema**, refreshed by events and nightly jobs. They never read the transactional tables directly.
- **Access:** row-level security from ADR-07 applies inside the reporting schema.
- **Templates:** client templates are built on the platform's standard report tool. If the client has a BI tool (Power BI or similar), we expose the reporting schema to it as governed views.

---

## 4. Logical component view

```
+--------------------------------------------------------------------------+
|  UI: Employee / Manager / Board / HR Admin portals (responsive web)      |
+--------------------------------------------------------------------------+
|  API layer: authN (SSO), authZ (ADR-07), validation, audit interceptor   |
+-----------+------------+-------------+-----------+----------+------------+
|  Config   |  Career    | Assessment  | Succession|   IDP    | Reporting  |
|  engine   |  Centre    | & Board     |           |          | read model |
| (ADR-02/3)|            | (ADR-03/04) | (ADR-06)  | (ADR-06) | (ADR-11)   |
+-----------+------------+-------------+-----------+----------+------------+
|  Shared: Workflow engine (05) | Scoring (06) | Event bus + outbox (10)   |
|          Snapshot jobs (09)   | Audit log    | Notification (10)         |
+--------------------------------------------------------------------------+
|  Integration layer (ADR-08): canonical model, adapters, ID map, sync jobs|
+------+-----------+-------------+-------------+---------+---------+-------+
       |           |             |             |         |         |
    Core HR      SSHR      Performance     T&D module   LMS      SSO/SMTP
```

---

## 5. Non-functional targets (proposed, to confirm with B10)

| Area | Target |
|---|---|
| Screen response | 95% of screens under 2 seconds. Nine-box with 2,000 people under 3 seconds. |
| Concurrency | Sized for 10% of employees at once during the cycle peak (cut-off week) |
| Availability | 99.5% during business hours. Planned maintenance outside the review window. |
| Recovery | RPO 24 hours, RTO 8 hours (to confirm with client IT) |
| Security | TLS 1.2 or higher. Encryption at rest for confidential fields. OWASP Top 10 tested. Pen test before go-live. |
| Audit retention | 7 years, or the client policy if different |
| Integration | Nightly sync done by 06:00. Failed records visible to admin in the dashboard. |

---

## 6. What still needs the client (true residual dependencies)

| # | Item | Needed by |
|---|---|---|
| 1 | Integration specs, test access, network access (B6) | Start of adapter build |
| 2 | Hosting, platform version, environments (B7) | Start of build |
| 3 | Scope of T&D and SSHR (B8) | Effort estimate |
| 4 | Report templates (B9) | Effort estimate |
| 5 | Volumes (B10) | Infrastructure sizing |
| 6 | Legacy data (B11) | Migration plan |
| 7 | Decision maker (B12) | Now |
| 8 | Configuration values for B1–B5 | Before UAT |

---

## 7. Risks created by this architecture

| Risk | Mitigation |
|---|---|
| Configurability raises build and test effort | Hard limits (ADR-03). Ship strong defaults. Test the configuration combinations the client actually uses. |
| Client cannot fill in the configuration | Deliver a configuration workbook and 1 facilitated workshop. Pre-load the defaults. |
| The platform workflow engine lacks versioning or returns | Spike it in week 1. Fall back to the internal state machine (ADR-05). |
| JSON payloads become hard to query | Use JSON only for whole-object definitions. Use typed tables for anything that reports read. |

---

## 8. Immediate next actions

1. **Architecture review** of these ADRs with the tech team. Accept or amend each one.
2. **Week-1 spikes:** platform workflow engine capability (ADR-05), and platform report tool vs a read-model approach (ADR-11).
3. **Define the canonical model** field by field. Build mock adapters so screens can be built without client systems.
4. **Update the commercial position:** B1–B5 become "configuration data before UAT". Remove them from the sign-off blockers.
