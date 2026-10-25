---
name: csharp-coding-standards
description: C# coding standards for the Talent module on .NET 10 - nullable types, analyzers, code style, error handling with ProblemDetails, validation, Options pattern, dependency injection, async, time and number types, structured logging and OpenTelemetry. Use whenever writing, reviewing or refactoring C# code.
---

# C# coding standards

These are team standards, not ADRs. They support the plan's security and NFR targets (§5).

## Compiler and tooling

Set these once in `Directory.Build.props`:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

- Package versions live in **`Directory.Packages.props`** (central package management).
- Shared **`.editorconfig`**. CI runs `dotnet format --verify-no-changes`.
- Do not suppress a warning without a comment saying why.

## Style

- **File-scoped namespaces.** Classes are **`sealed`** by default.
- **Records** for DTOs, events and value objects. Prefer immutable types.
- Implementation types are **`internal`**. Only Contracts are `public`.
- **`decimal`** for scores, weights and percentages. Never `double`.
- **`DateTimeOffset`** stored in UTC. Convert to the client time zone only for display.
- Get the time from **`TimeProvider`**. Never call `DateTime.Now` or `DateTimeOffset.UtcNow` directly.
- Use names from the domain: `TalentReviewCycle`, `Successor`, `ReadinessBand`. Avoid `Manager`, `Helper` or `Util` classes.

## Async

- Async all the way. Pass a **`CancellationToken`** to every async call.
- No `async void`, `.Result`, `.Wait()` or `Task.Run` to fake async.
- Suffix async methods with `Async`.

## Errors

- **Expected failures** (version locked, out of scope, invalid transition) are **typed domain errors**. Return them as results or throw specific domain exceptions. Never return `null` to mean an error.
- API errors use **ProblemDetails** (RFC 9457), through `AddProblemDetails()` and 1 global `IExceptionHandler`.
- Map errors to codes: validation → 400, not authenticated → 401, no permission or out of scope → 403, not found → 404, concurrency conflict → 409.
- Never put stack traces or confidential data in an error response.

## Validation

- Validate input **at the edge** (endpoints) with **FluentValidation**.
- Domain objects protect their own rules (invariants) in their methods as well.
- Reject unknown enum values and over-long strings.

## Configuration and dependency injection

- **Options pattern** with validation and `ValidateOnStart()`:

```csharp
services.AddOptions<SyncOptions>()
    .Bind(configuration.GetSection("Integration:Sync"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

- **No secrets in `appsettings`.** Use user secrets locally and environment variables or a secret store elsewhere.
- **Constructor injection** only. No service locator, except keyed-service lookups at a plug point.
- Never inject a scoped service into a singleton.

## Logging and monitoring

- **Structured logs** with message templates:

```csharp
logger.LogInformation("Cycle {CycleId} launched with {CandidateCount} candidates", cycleId, count);
```

- Never use `$"..."` in log messages.
- **Never log confidential fields or personal data**: nine-box, potential, successor status, risk of loss, names or emails. Log IDs only.
- **OpenTelemetry** for traces and metrics, including EF Core, HttpClient and Hangfire.
- **Health checks**: `/health/live` and `/health/ready` (database, Hangfire) for the load balancer.

## Security in code

- No raw SQL built from strings. Use LINQ or `FromSql` with interpolated parameters.
- No `[AllowAnonymous]` without a review.
- Never write confidential data to files, temp folders or caches without encryption.
