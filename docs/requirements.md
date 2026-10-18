# TalentHub — Functional Requirements

Derived from the client's *Talent Management Blueprint* (V1.0, 06/10/2026). Pages 1–7 were available in extractable form; sections 3.1–3.5 detail (pages 8–17) were not machine-readable, so behaviour there is reconstructed from the operation table and module notes and is flagged `[assumed]`.

## 1. System context

- Web application used by employees of different work categories: **Employee, Reviewer, Supervisor/HOD, Talent Review Board member (and Board Head), HR Admin**.
- Automates the repeating 4-phase lifecycle:
  1. Capture career aspirations
  2. Review talents to identify top performers / future leaders
  3. Update the succession plan
  4. Update individual development plans
- Integrates with the client **LMS** (two APIs: courses per role/competency; employee course status) and depends on the **Performance module** for appraisal information and the competency catalogue.
- Links to the Training & Development module.

## 2. Operation map (blueprint steps → system capabilities)

| # | Operation | Actor | Capability |
|---|-----------|-------|-----------|
| 1 | Definitions: scales, job families, jobs→family, alternative career paths | HR Admin | Admin CRUD |
| 2 | Career centre: view resume, view career path, profile match-up, enter career preferences | Employee | Career Centre |
| 3 | Define talent review cycle | HR Admin | Admin CRUD |
| 4 | Select leadership candidates | HR Admin | Admin CRUD |
| 5 | Configure reviewers + talent assessment competencies | HR Admin | Admin CRUD |
| 6 | Create talent review boards | HR Admin | Admin CRUD |
| 7 | Talent assessment (talent profile + nine-box) and submission | Supervisor/HOD | Assessment |
| 9 | Schedule review meetings | HR Admin | Admin CRUD |
| 10 | Review nomination: initial-assessment-completed employees listed; board head places final nine-box | Assigned Head of Talent Review Board | Assessment |
| 11 | Critical position information | HR Admin | Admin CRUD |
| 12 | Assign successors for critical positions | HR Admin | Succession |
| 13 | Development plan information (frequency, period length, rating method) | HR Admin | Admin CRUD |
| 14 | Review development plan | Direct Supervisor | Dev Plans |
| 15 | Development plan approval | HR Admin | Dev Plans |

## 3. Module requirements

### 3.1 Career Centre (employee)
- View own resume and career path progression derived from job-family definitions.
- Enter/edit career preferences: mobility (willing to relocate + details), preferred jobs, long-term career goals, preferred workstation.
- Profile match-up: compare own profile against preferred jobs and alternative career paths. `[assumed: match = family adjacency via alternative career paths]`

### 3.2 Talent Assessment
- HR defines review cycles with competencies and a rating scale; competencies must exist before cycle configuration (Performance module is source `[assumed: admin-defined cycle competencies]`).
- HR selects leadership candidates (informed by performance appraisal data `[assumed: manually entered scores]`).
- HR configures reviewers and creates review boards (members + designated head), schedules review meetings.
- Supervisor/HOD performs assessments: competency scores, performance & potential ratings; submits.
- Submitted assessments appear in the **nomination review** list for the board head, who places the final nine-box (performance × potential, 3×3).
- Nine-box labels `[assumed]`: standard 9-box naming (e.g. Star, High Potential, Core Player, …).

### 3.3 Succession Planning
- HR records critical positions (head-of-department roles and select others such as HRO Payroll) with incumbent and notes.
- HR assigns successors per critical position with suitability and **readiness** (Not ready / Now / Ready in 1 year / Ready in 2 years).
- Reports: monthly and quarterly readiness reports with trend analysis; role-wise readiness analysis. **Client will supply templates** → ship CSV/JSON exports with placeholder layouts.

### 3.4 Individual Development Plans
- HR configures plan settings: review frequency, review period length, overall rating method.
- Plans contain items: competency, action, support required, measure of success, target date.
- Workflow: employee/HR creates plan → **supervisor reviews** → **HR approves or rejects** (Draft → Submitted → Approved/Rejected).

### 3.5 Alerts
- In-app notifications per employee: assessment due, nomination pending, plan submitted/needs approval `[assumed triggers until page detail available]`; mark-as-read supported.

### 3.6 LMS integration
- `ILmsClient.GetCoursesAsync()` — courses and links per role or competency (client API to be provided; mock until then).
- `ILmsClient.GetEmployeeCoursesAsync(userId)` — employee course details/status incl. assigned and ongoing (client API to be provided; mock until then).
- Surfaces in Succession (grooming successors) and Training & Development.

## 4. Non-functional requirements `[assumed]`

- Auth: username/password with JWT; role-based authorization on all endpoints.
- Auditability: timestamps on state changes; history-preserving deletes for people-linked records.
- Portability: SQLite default for dev/demo; SQL Server connection string ready for client deployment.
- Testability: xUnit integration tests against the API host.

## 5. Explicitly deferred (pending client input)

1. Real LMS API contracts + credentials (mock adapter in place).
2. Report templates for readiness/trend/role-wise reports.
3. Performance-module integration for competency catalogue and appraisal data.
4. Detail-level process flows from blueprint pages 8–17.
