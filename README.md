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
| `tests/` | Unit, host and architecture (module boundary) tests |

## Run it locally

```bash
./scripts/pg-start.sh                                        # PostgreSQL 16 on 127.0.0.1:5433
dotnet run --project src/Talent.Host --launch-profile http   # http://localhost:5000, migrates, syncs mock HR data, seeds dev users
cd web && npm install && npm run dev                         # http://localhost:5173
```

Sign in on the landing page as `dev.admin`, `dev.manager` or `dev.employee`. `npm run e2e` in `web/` runs the whole flow in a browser.

## Status

Done: Config (versioned scales and grids, N x M grid engine, admin API), Security (BFF cookie login, permission matrix, masking, audit),
Integration (mock Core HR adapter, delta sync), Workflow (approval engine, SLA, delegation, HTTP API; managers from synced HR data, role members from Security),
React app (config admin, development plan approval, approvals inbox).
Not yet: Assessment, Succession, IDP content, Career, Notification modules; real OIDC provider and Core HR adapters.
