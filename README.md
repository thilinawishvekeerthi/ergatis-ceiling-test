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

## Java backend

`backend/` holds a second backend: Java 21 / Spring Boot 3.5, built with Maven.
`Dockerfile` builds it together with `frontend/` into one image.

```bash
mvn -f backend/pom.xml package
docker compose up --build   # PostgreSQL 16 + the app on http://localhost:8080
```

`docker-compose.yml` runs with mock source systems and dev sign-in. For a client environment, see the comments in that file.

## Status

Done: Config module domain (version lifecycle, grid placement and override), module boundary tests, host skeleton.
Not yet: PostgreSQL / EF Core and Testcontainers tests (Docker was not available), other 7 modules, login, workflow engine, React app.
