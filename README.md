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

## Run the nine-box demo locally

```bash
./scripts/pg-start.sh                                   # PostgreSQL 16 on 127.0.0.1:5433
cd src/Talent.Host && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5000 dotnet run --no-launch-profile
cd web && npm install && npm run dev                    # http://localhost:5173
```

Open http://localhost:5173, choose **Continue as hr-admin**, then **Nine-box**.
On first boot the host migrates the database, syncs 12 mock employees and publishes the BRD nine-box config.
Screenshots of a run are in `docs/demo/`. The data model and its trade-offs are in `docs/nine-box-data-model.md`.

## Layout

| Path | Holds |
|---|---|
| `src/Talent.Host` | ASP.NET Core app: health checks, ProblemDetails, wires modules |
| `src/Talent.Shared` | Shared kernel (kept small) |
| `src/Modules/Config` | Versioned config (ADR-02) and the N x M grid engine (ADR-03) |
| `src/Modules/Assessment` | Nine-box talent evaluation (BRD demo slice): pure placement rules, history, latest placement |
| `docs/api/nine-box.openapi.yaml` | API contract for the nine-box endpoints (checked against the running host by a test) |
| `tests/` | Unit, host and architecture (module boundary) tests |

## Status

Done: Config module domain (version lifecycle, grid placement and override), module boundary tests, host skeleton.
Not yet: PostgreSQL / EF Core and Testcontainers tests (Docker was not available), other 7 modules, login, workflow engine, React app.
