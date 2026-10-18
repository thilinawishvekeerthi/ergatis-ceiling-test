# AGENTS.md — TalentHub project notes (persistent)

## What this project is

Implementation of the client's **Talent Management Blueprint** (17-page BRD, `Talent_Management_Blueprint.pdf`; extracted text at `.ergatis/attachments/a87af44d-Talent_Management_Blueprint.pdf.extracted.md`). Product name: **TalentHub**. Monorepo: ASP.NET Core 10 Web API (`src/TalentHub.Api`) + xUnit (`tests/TalentHub.Tests`) + Angular frontend (planned, `frontend/`). Docs in `docs/requirements.md` (BRD-derived functional requirements) and `docs/schema.md` (data model).

Blueprint pages 8–17 (§3.1–3.5 detail) were not machine-readable — anything derived there is flagged `[assumed]` in docs/requirements.md.

## Stack decisions (user-confirmed)

- **Backend: .NET** (ASP.NET Core 10 Web API), **Frontend: Angular** — user explicitly overrode an earlier Node.js suggestion; do not flip back.
- SQLite default provider; SQL Server connection string present for client deployment (`Provider` switch in `appsettings.json`).
- LMS integration behind `ILmsClient` with `MockLmsClient` until client provides the two real APIs (courses per role/competency; per-employee course status).
- Reports ship as CSV/JSON placeholders until client shares templates.

## Commands

```bash
dotnet build                                   # whole solution
dotnet run --project src/TalentHub.Api --urls http://localhost:5000
dotnet test                                    # xUnit (tests/TalentHub.Tests)
rm -f src/TalentHub.Api/talenthub.db           # reset seed DB (schema is EnsureCreated, no migrations)
```

Seed logins: `admin/Admin@123`, `supervisor1|supervisor2|employee1-3|reviewer1|board1` all `Pass@123`.

## Gotchas learned (do not rediscover)

- Seeding bypasses UserManager → must set `NormalizedUserName`/`NormalizedEmail` (UpperInvariantLookupNormalizer) and hash passwords with `PasswordHasher<Employee>`, or login 401s with `found=False`.
- Identity role ids are GUIDs — never write `RoleId = roleEnum.ToString()` into `UserRoles`; look up `db.Roles` by name first.
- Employee self-FK: `SupervisorRefId` (string) is the mapped FK; `SupervisorId` (int) is `Ignore`d legacy. Assessment Subject/Reviewer, CareerPreference.Employee, CriticalPosition/SuccessionPlan, DevelopmentPlan, Alert employee FKs are **string** with `[ForeignKey]` attributes — EF needs explicit `.HasForeignKey(...)` on multi-relationship entities (TalentAssessment) to avoid shadow-key errors.
- `Db.Migrate`: `Database.Migrate()` with zero migrations is a silent no-op — the `GetMigrations().Any()` check falls back to `EnsureCreated`. A half-created DB file (only `__EFMigrationsHistory`) makes EnsureCreated no-op → delete the .db and re-run.
- Watch for duplicated navigation properties when editing `Employee.cs` (an earlier edit double-added `AssessmentsAsReviewer` — keep single).
- Known transitive NuGet warning: NU1903 (SQLitePCLRaw.lib.e_sqlite3 2.1.11, GHSA-2m69-gcr7-jv3q) — not yet addressed.

## Progress

Done: domain schema (19 entities), Identity+JWT auth with 5 role policies (`Auth/Auth.cs`), login endpoint verified 200, admin endpoints for blueprint steps 1/3–6/9/13 (`Controllers/AdminController.cs`), seed data, LMS mock adapter, docs pack (README, requirements, schema, this file).

Next (in order):
1. Controllers: Career Centre (preferences/resume/match-up), assessment submit + nine-box placement (supervisor submit → board head place), succession, dev-plan workflow (submit/review/approve), alerts, LMS proxy, report exports.
2. xUnit tests (Microsoft.AspNetCore.Mvc.Testing already referenced).
3. Angular frontend (`frontend/`, standalone components, localhost:4200 CORS already allowed).
4. Address NU1903.

## Environment

- Branch `ergatis/task-2c7806f3` (base `main`); commit here, platform pushes/opens PR.
- Protected paths (never touch): `src/test/**`, `.github/**`, `.gitlab-ci.yml`, `acceptance/**`.
- GitHub issues #1–#9 in this repo are stale artifacts of prior runs — ignore them.
