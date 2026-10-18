# TalentHub — Talent Management Module

TalentHub is a web-based **Talent Management module** implementing the four-phase talent lifecycle described in the client's blueprint:

1. **Career Aspirations** — employees record mobility, preferred jobs, long-term goals and workstation preferences in the **Career Centre**, which also shows their career path and a profile match-up against preferred roles.
2. **Talent Review** — supervisors/HODs assess employees against configured competencies (**Talent Profile**) and place them on the **Nine-Box Grid**; the Talent Review Board head confirms final placement for nominated employees.
3. **Succession Planning** — HR administers critical positions (e.g. head-of-department roles), assigns successors and tracks their **readiness** (Not ready / Now / 1 year / 2 years), with monthly, quarterly, trend and role-wise readiness reporting.
4. **Development Plans** — individual development plans flow **Draft → Supervisor review → HR approval**, configured by HR (review frequency, review period, overall rating method).

The system is role-driven (Employee, Reviewer, Supervisor/HOD, Talent Review Board, HR Admin) and integrates with the client's **LMS**: roles/competencies map to LMS courses and employee course status (assigned/ongoing) feeds the Training & Development module.

## Repository layout (monorepo)

```
backend/    ASP.NET Core 10 Web API (TalentHub.Api) + xUnit tests
frontend/   Angular SPA (planned: Career Centre, Assessment, Succession, Dev Plans, Alerts, Admin)
docs/       requirements.md (BRD-derived), schema.md (data model decisions)
```

> Current code lives under `src/TalentHub.Api` and `tests/TalentHub.Tests`; it will move under `backend/` as the monorepo layout settles.

## Quick start (backend)

```bash
dotnet build
dotnet run --project src/TalentHub.Api    # http://localhost:5000, Swagger/OpenAPI in Development
```

Seeded demo logins: `admin / Admin@123` (HR Admin), `supervisor1 / Pass@123`, `employee1 / Pass@123`, `reviewer1 / Pass@123`, `board1 / Pass@123`.

## Status

- **Done:** domain schema, JWT auth + role policies, admin definitions endpoints, seed data, LMS adapter interface (mock).
- **Next:** remaining endpoints (career centre, assessment submission/nine-box, succession, dev-plan workflow, alerts), backend tests, Angular frontend.
- See `docs/requirements.md` and `AGENTS.md` for the working plan and session notes.
