---
name: postgres-data-model
description: Database conventions on PostgreSQL 16+ with EF Core - schema per module, typed tables vs JSONB, EF Core migrations, optimistic concurrency with xmin, query rules, row-level security, snapshots and the append-only audit log (ADR-02, 07, 09, 11). Use when designing tables, writing EF Core mappings, migrations or queries, or adding snapshots.
---

# Data: PostgreSQL 16+ with EF Core

Source: `docs/talent-solution-architecture.md` ADR-02, 07, 09, 11; entities in gap analysis §5.3.

## Ownership

- **1 PostgreSQL schema per module** (`assessment`, `succession`, …). Set it with `HasDefaultSchema` in each module's `DbContext`.
- A module never maps another module's tables.
- Use **snake_case** names (`EFCore.NamingConventions`).

## Migrations

- Use **EF Core migrations**, 1 migrations set per module `DbContext`.
- **Review the generated SQL** (`dotnet ef migrations script`) before merging.
- Run migrations in CI against Testcontainers PostgreSQL.
- Never change a database by hand.

## Typed tables vs JSONB

| Typed tables | JSONB |
|---|---|
| Anything reports or filters query | Whole definitions read at once (workflow, rules) |

If you query inside JSONB, move that data to a typed table.

## Concurrency

- Use **optimistic concurrency** on every record that users edit or approve. Map PostgreSQL's `xmin` as the row version:

```csharp
builder.Property<uint>("Version").IsRowVersion();
```

- A `DbUpdateConcurrencyException` becomes **409** (see `csharp-coding-standards`).

## Snapshots (ADR-09)

- **At cycle launch:** snapshot each candidate's grade, job, department, appraisal scores and config version IDs.
- **Monthly:** `ReadinessSnapshot` for each critical position and successor.
- Snapshots are **insert-only**.

## Audit log

- Append-only. Do not grant `UPDATE` or `DELETE` to the app role.

## Queries

- Apply the data scope filter **in SQL**.
- **No lazy loading.** Load what you need with `Select` projections, or `Include` when you need the entity.
- Avoid **N+1 queries** (1 extra query per row). Check the SQL log in tests for list endpoints.
- Use `AsNoTracking()` for read-only queries.
- Every list query is **paged** and has a stable `OrderBy`.
- Target: a nine-box with **2,000 people in under 3 seconds**. Add indexes for the filters you use.
