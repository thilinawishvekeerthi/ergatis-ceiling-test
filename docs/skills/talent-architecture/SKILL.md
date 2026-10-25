---
name: talent-architecture
description: Overall architecture of the Talent Management module (modular monolith, 8 modules, 6 principles, plug points, ADR-01 to ADR-12). Use before starting any feature, when deciding which module code belongs in, when adding a module or a dependency, or when a design choice might conflict with an ADR.
---

# Talent Management architecture

Source: `docs/talent-solution-architecture.md` and `docs/talent-architecture-diagrams.md` (PR #34).
Read the relevant ADR before changing its area. If a change conflicts with an ADR, stop and raise it.

## Stack (decided)

| Layer | Choice |
|---|---|
| Backend | **C# / .NET 10 (ASP.NET Core)**. Team skill and the HRIS product's stack (ADR-12 Rule 1). |
| Database | **PostgreSQL 16+** |
| Frontend | **React + TypeScript** |
| Jobs | **Hangfire** (PostgreSQL storage) |
| Reports | Free, open-source only: Razor HTML + Playwright PDF + ClosedXML. See `reporting`. |
| Runtime | **Docker** containers |

- .NET 10 is LTS (supported to Nov 2028). .NET 8 support ends Nov 2026. Do not target .NET 8.
- Development is **test-driven**. See `tdd-and-testing`.
- Do not add paid-licence libraries without approval.

## The 6 principles

1. **Configuration over customisation.** Client behaviour lives in data. A second client needs no code change.
2. **Versioned, immutable configuration.** Used config is never edited. See `versioned-configuration`.
3. **Talent does not own master data.** Read it via adapters. See `integration-adapters`.
4. **Cycle-scoped snapshots.** A cycle freezes its inputs. See `postgres-data-model`.
5. **Secure by default.** Deny by default, check, audit. See `talent-security`.
6. **Integration failure must not stop users.** Read local synced copies.

## Modules (ADR-01: modular monolith)

| Module | Owns |
|---|---|
| `Config` | Versioned scales, grids, rules, weights, workflows, permission matrices |
| `Career` | Career Centre: aspirations, preferred jobs, mobility, % match |
| `Assessment` | Cycles, candidates, assessments, board calibration, talent pool |
| `Succession` | Critical positions, successors, readiness, bench strength |
| `Idp` | Development plans, actions, approvals, progress |
| `Integration` | Canonical model, adapters, ID map, sync, dead-letter |
| `Notification` | Event subscribers, versioned templates, email + in-app |
| `Reporting` | Reporting read model, report templates |

Shared: workflow engine, scoring strategies, outbox, snapshot jobs, audit log.

## Layers

```
React UI (Employee / Manager / Board / HR Admin portals)
  → Host: BFF login (OIDC) → authZ (ADR-07) → validation → audit
    → Business modules → Shared services
      → Integration layer → Core HR, SSHR, Performance, T&D, LMS, SMTP
```

## Plug points

Changeable parts sit behind interfaces and are chosen by configuration. Full list in `dotnet-modular-backend`.
Login system, role source, data scopes, source adapters, workflow engine, actor resolvers, scoring strategies, notification channels, report renderers, clock.

## Hard rules

- **No cross-module table access.** Use the other module's `.Contracts`, or its events.
- **Side effects go through domain events**, not direct calls into Notification or Reporting.
- **Never call a source system** inside a user request. Read local tables.
- **Never hard-code** scales, grids, rules, weights, workflow steps, role names or alert text.
- **Reports never read transactional tables.**
- **Not allowed without an ADR change:** microservices, MongoDB, Kafka, Redis, a second database.

## Related skills

`dotnet-modular-backend`, `csharp-coding-standards`, `tdd-and-testing`, `versioned-configuration`, `workflow-and-events`, `talent-security`, `integration-adapters`, `postgres-data-model`, `reporting`, `react-frontend`.
