---
name: dotnet-modular-backend
description: Backend conventions for the Talent module on C# / .NET 10 and ASP.NET Core - solution layout, module boundaries, Minimal API endpoints, plug points with keyed services, outbox events and Hangfire jobs (ADR-01, ADR-10, ADR-12). Use when writing or reviewing backend C#, creating a module, adding an endpoint, a service, a plug-in implementation, a background job or a cross-module call.
---

# Backend: C# / .NET 10 modular monolith

Source: `docs/talent-solution-architecture.md` ADR-01, ADR-05, ADR-06, ADR-08, ADR-10, ADR-12.
Write the test first (`tdd-and-testing`). Follow `csharp-coding-standards`.

## Stack

| Need | Use |
|---|---|
| Web/API | **ASP.NET Core** on **.NET 10**, Minimal APIs, built-in OpenAPI (`Microsoft.AspNetCore.OpenApi`) |
| Data | **EF Core** + **Npgsql**, 1 `DbContext` per module |
| Jobs | **Hangfire** with PostgreSQL storage (`Hangfire.PostgreSql`) |
| Events | In-process events + **outbox table** (no broker) |
| Auth | BFF login with OpenID Connect + cookie. See `talent-security`. |

## Solution layout

```
src/
  Talent.Host/                      ← ASP.NET Core app: BFF login, wires modules together
  Talent.Shared/                    ← outbox, audit, workflow engine, plug-point interfaces (keep small)
  Modules/
    Assessment/
      Talent.Assessment.Contracts/  ← public: DTOs, interfaces, events
      Talent.Assessment/            ← internal: domain, EF Core, endpoints
    Career/ Config/ Succession/ Idp/ Integration/ Notification/ Reporting/
tests/
  Talent.Assessment.Tests/  …  Talent.ArchitectureTests/
```

- A module may reference **only other modules' `.Contracts` projects** and `Talent.Shared`.
- Make implementation types **`internal`**. Use `InternalsVisibleTo` for the module's own test project.
- Each module has 1 entry point: `services.AddAssessmentModule(configuration)` and `app.MapAssessmentEndpoints()`.
- `services.AddTalentModules(configuration)` registers all modules. The same call can load Talent inside the HRIS host later (B7).

## Boundary test (runs on every build)

```csharp
[Fact]
public void Modules_reference_only_contracts_of_other_modules()
{
    foreach (var module in ModuleCatalog.Assemblies)
    {
        var forbidden = module.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Talent.")
                        && !n.EndsWith(".Contracts")
                        && n != "Talent.Shared"
                        && n != module.GetName().Name);

        forbidden.ShouldBeEmpty($"{module.GetName().Name} breaks a module boundary");
    }
}
```

Use **NetArchTest** for layer rules. Example: domain types must not depend on `Microsoft.EntityFrameworkCore`.

## Endpoints

- **Minimal APIs**, 1 route group per module: `/api/v1/assessments`, `/api/v1/succession`, …
- Every list endpoint is **paged**. Never return an unbounded list.
- Errors are **ProblemDetails**. See `csharp-coding-standards`.
- The generated OpenAPI document is **checked into the repo**. CI fails when it changes without the file being updated, so breaking changes are visible.

## Plug points

Business code depends only on the interface. It never names a concrete plug.

| Plug point | Interface | Implementations | Chosen by |
|---|---|---|---|
| Login system | OIDC settings | Entra ID, AD FS, Keycloak | `Authentication:Oidc:Authority`, `ClientId` |
| Current user | `ICurrentUser` | 1, built per request | — |
| Role source | `IRoleSource` | `TalentManaged`, `IdpGroups` | Setting (open question §3.1 #7) |
| Data scope | `IDataScopeResolver` | 1 per scope (`Self`, `DirectReports`, `DeptTree`, `CycleAssigned`, `All`) | Keyed by scope |
| Source data | `ISourceAdapter<T>` | `Mock`, `DbView`, `Rest` | `Integration:<System>:Mode` |
| Workflow engine | `IWorkflowEngine` | HRIS engine adapter, in-house engine | Setting (week-1 spike) |
| Actors | `IActorResolver` | `DirectSupervisor`, `HodOf`, `Role`, `CycleAssignment`, `BoardHead` | Keyed by resolver code in the workflow definition |
| Scoring | `IProfileMatchStrategy`, `IBenchStrengthStrategy`, `IIdpProgressStrategy` | Weighted, count-based, LMS-driven | Keyed by strategy code in the config version |
| Notifications | `INotificationChannel` | `Email`, `InApp` | Template config |
| Reports | `IReportRenderer` | `Pdf`, `Excel` | Keyed by format |
| Clock | `TimeProvider` | System, fake in tests | — |

Use **keyed services** when config data picks the implementation:

```csharp
services.AddKeyedScoped<IProfileMatchStrategy, WeightedProfileMatch>("WEIGHTED");

// at runtime: the pinned config version names the strategy
var strategy = provider.GetRequiredKeyedService<IProfileMatchStrategy>(version.StrategyCode);
```

- A missing key must **fail at startup** (validate all codes in published config), not mid-request.
- Every plug has a **fake or mock** implementation for tests.

## Rules

- **Authorisation in the service layer**, not only in endpoints or the UI (ADR-07).
- **Audit** every confidential write: before and after values (ADR-07).
- **Read config by the cycle's pinned version IDs**, never "latest" (ADR-02).
- **Never call another module's internals.** Use its Contracts interface or its events.

## Outbox (ADR-10)

1. The service changes state and raises a domain event.
2. An EF Core `SaveChanges` interceptor writes the event to `outbox_messages` in the **same transaction**.
3. A Hangfire job delivers the events to in-process handlers **after commit**.
4. Handlers are **idempotent**. They record processed event IDs, so a second delivery does nothing.

## Hangfire jobs

- Jobs: nightly delta sync (done by 06:00), cycle-launch snapshot, monthly readiness snapshot, SLA timers, outbox delivery, reporting refresh, report rendering.
- Every job is **safe to rerun**. Set the **client time zone** on recurring jobs.
- Job classes stay thin. They call a module service that has its own tests.
- Jobs run in the **background worker** container. Web nodes do not process jobs.
