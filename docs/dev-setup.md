# Developer setup (no Docker)

```bash
# .NET 10 SDK in ~/.dotnet (no root)
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir $HOME/.dotnet
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.dotnet/tools:$PATH
dotnet tool install --global dotnet-ef

# PostgreSQL 16 binaries in ~/.pg16 (see scripts/pg-start.sh), then:
./scripts/pg-start.sh          # 127.0.0.1:5433, user postgres
```

## Tests

- Run each test project with `dotnet test --project tests/<Project>`.
- Database tests create their own throwaway database on the local server (`PostgresFixture`). Override with `TALENT_TEST_PG`.
- `PostgresFixture.ResetAsync` clears every schema except migration history and the audit log.

## Conventions every module follows

- Projects: `src/Modules/<M>/Talent.<M>.Contracts` (public) and `Talent.<M>` (internal types, one `<M>Module` static class with `Add<M>Module`, `Map<M>Endpoints`, `Initialize<M>Async`).
- One `DbContext` per module, schema `<m>` in lower case, `__ef_migrations` history table in that schema, `MapSharedTables(ownedByThisContext: false)` then `UseSnakeCaseNames()`.
- Migrations: `dotnet ef migrations add <Name> -p <module project> -s <module project> -o Persistence/Migrations` (design-time factory reads `TALENT_DB`).
- Tests: `tests/Talent.<M>.Tests` (unit) and `tests/Talent.<M>.IntegrationTests` (real Postgres). Copy the shape of the Config test projects.
