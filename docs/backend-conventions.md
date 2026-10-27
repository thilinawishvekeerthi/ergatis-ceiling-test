# Backend conventions

How the Talent Management backend is organised. Read this before adding code.

## Stack

- Java 21, Spring Boot 3.5, Spring Modulith 1.4 (ADR-01, ADR-12)
- PostgreSQL 16+ with Flyway migrations. Embedded PostgreSQL for dev and demos (`dev` profile).
- Hibernate `ddl-auto: validate`: every JPA entity must match a Flyway table exactly.

## Modules

Each direct sub-package of `com.talent` is a module. The top-level package is its **public API**. Sub-packages
(`internal`) are private. `ModularityTest` fails the build on cycles or on use of another module's internals.

| Module | Owns | Flyway versions |
|---|---|---|
| `shared` (open) | errors, auth, audit log, masking, JSON | V1 |
| `config` | versioned config, grid engine, rule engine | V2 |
| `integration` | canonical model, adapters, sync, ID map | V3 |
| `access` | permission matrix, scopes, roles, audit viewer, dev sign-in | V4 |
| `workflow` | state machine, tasks, delegation, SLA timers | V5 |
| `career` | job families, career paths, requirements, aspirations, % match | V10–V19 |
| `assessment` | cycles, candidates, assessments, boards, talent pool, snapshots | V20–V29 |
| `succession` | critical positions, successors, bench strength, readiness snapshots | V30–V39 |
| `idp` | development plans, actions, progress | V40–V49 |
| `notification` | templates, in-app inbox, email | V50–V59 |
| `reporting` | read model, reports, exports | V60–V69 |

Dependency direction: `shared` ← `config`, `integration` ← `access` ← `workflow` ← business modules ← `notification`, `reporting`.
Business modules talk to each other only through API interfaces and events (e.g. `career.ProfileMatchService`,
`assessment.TalentPoolQuery`, `succession.SuccessionQuery`).

## Rules for every feature

1. **Authorise in the service layer** (ADR-07): call `AccessGuard.require(action, objectType, subjectEmployeeId)`
   before reading or changing talent data. For lists, filter with `AccessGuard.rowFilter(...)`.
2. **Mark confidential fields** in response records with `@Confidential(ObjectType.X)` and implement `HasSubject`.
   Masking happens at JSON serialisation, so it cannot be forgotten.
3. **Audit** every confidential write, approval and export with `AuditService.record(...)` (inside the transaction).
4. **Workflows** run through `WorkflowService` with a definition from config (`config-defaults/03-workflows.json`).
   Mirror the returned state onto your own record. Never hard-code approvers.
5. **Configuration over code**: scales, grids, rules, weights, workflows, permissions and templates are config
   versions. A cycle or record pins the version IDs it used and calls `ConfigService.lock(versionId)`.
6. **Events** (ADR-10): publish domain events with `ApplicationEventPublisher` inside the transaction.
   Listen in other modules with `@ApplicationModuleListener` (async, after commit, stored in the outbox).
7. **Time** comes from the injected `java.time.Clock`, never `LocalDate.now()` without a clock.
8. **Errors**: throw `NotFoundException`, `BusinessRuleException(code, message)` (HTTP 422) or `ForbiddenException`.
9. **Master data** (employees, org, jobs, competencies, appraisals, resume, LMS) is read-only, through
   `EmployeeDirectory` and `TalentDataQuery`. Talent never writes it.

## Running locally

```bash
cd backend
mvn spring-boot:run -Dspring-boot.run.profiles=dev
# then sign in as a seeded user:
curl -X POST localhost:8080/api/auth/dev/login -H 'Content-Type: application/json' -d '{"employeeNo":"E0002"}'
```

Seeded people (mock Core HR): `E0001` CEO, `E0002` Head of HR (HR_ADMIN), `E0003` HR Manager (HR_APPROVER),
`E0007` Head of Finance, `E0013` Head of IT, `E0014` IT Manager, `E0030` Sales Executive (plain employee).
