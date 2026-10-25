# Project skills

Claude Code skills for building the Talent Management module.
They come from the architecture plan in PR #34 (`docs/talent-solution-architecture.md`, `docs/talent-architecture-diagrams.md`, `docs/talent-management-gap-analysis.md`), plus team decisions.

## Install

Claude Code loads project skills from `.claude/skills/`. These files are staged here because the agent could not write to `.claude/`. Move them once:

```bash
mkdir -p .claude && git mv docs/skills .claude/skills
```

## Decisions

C# / .NET 10 backend, PostgreSQL 16+, React + TypeScript, test-driven development, free and open-source report tools, BFF login with OpenID Connect.

## Skills

| Skill | Use when |
|---|---|
| `talent-architecture` | Starting any feature. Deciding where code belongs. |
| `dotnet-modular-backend` | Backend C#, modules, endpoints, plug points, outbox, Hangfire jobs |
| `csharp-coding-standards` | Writing or reviewing any C# code |
| `tdd-and-testing` | Before any production code. Tests and CI. |
| `versioned-configuration` | Scales, grids, eligibility rules, scoring weights |
| `workflow-and-events` | Approval flows, SLA timers, events, alerts |
| `talent-security` | Login, roles, and any endpoint, query, report or export on talent data |
| `integration-adapters` | Core HR, SSHR, Performance or LMS data |
| `postgres-data-model` | Tables, EF Core, migrations, concurrency, snapshots |
| `reporting` | Reports, exports, BI views |
| `react-frontend` | UI screens, forms, charts, the nine-box |

## Still open

- Component library: MUI or Ant Design.
- Workflow engine: HRIS engine or in-house (week-1 spike, ADR-05).
- Role source: Talent-managed or login-system groups (§3.1).
- Hosting: own host or inside the HRIS host (B7).
