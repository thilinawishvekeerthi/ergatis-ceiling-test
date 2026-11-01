# Talent Management

Talent Management module: C# / .NET 10 modular monolith, PostgreSQL 16+, React + TypeScript.
Architecture and rules are in `docs/` and the project skills in `docs/skills/`.

## Build and test

Needs the .NET 10 SDK (see `global.json`). Tests use xUnit v3 on Microsoft.Testing.Platform.

```bash
dotnet build Talent.sln
dotnet test --solution Talent.sln
dotnet format Talent.sln --verify-no-changes
```

## Layout

| Path | Holds |
|---|---|
| `src/Talent.Host` | ASP.NET Core app: health checks, ProblemDetails, wires modules |
| `src/Talent.Shared` | Shared kernel (kept small) |
| `src/Modules/Config` | Versioned config (ADR-02) and the N x M grid engine (ADR-03) |
| `src/Modules/Assessment` | Review cycles, ratings, nine-box placement and calibration (`/api/v1/assessment`) |
| `src/Modules/Career` | Job catalogue, career paths, requirement gaps and % match (`/api/v1/career`) |
| `src/Modules/Security`, `Integration`, `Workflow` | Login and permissions, synced HR data, workflow engine |
| `web/` | React + TypeScript app (config admin, manager assessments, cycle and grid screens, employee Career Centre) |
| `tests/` | Unit, host and architecture (module boundary) tests |

## Status

Done: Config, Security, Integration, Workflow, Assessment and Career modules with PostgreSQL / EF Core and integration tests, module boundary tests, host, and the web screens for config and assessment.
Not yet: the remaining modules (for example Notification), HTTP endpoints for Integration and Workflow, and real (non-mock) HR source adapters.

Existing installs keep their current permission matrix. To give roles access to Assessment, publish a new matrix version with the `assessment:*` and `career:*` grants from `DefaultPermissionMatrix` (new installs get them automatically).
