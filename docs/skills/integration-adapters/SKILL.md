---
name: integration-adapters
description: Integration layer - canonical data model, pluggable source adapters (mock, database view, REST), scheduled delta sync, idempotent upserts, ExternalIdMap, dead-letter table (ADR-08). Use when reading employee, org, appraisal, competency, qualification or LMS data, or when building an adapter for Core HR, SSHR, Performance, Training & Development or the LMS.
---

# Integration: canonical model + adapters

Source: `docs/talent-solution-architecture.md` ADR-08; diagram 3; interfaces in gap analysis §4.12.

## Canonical entities

Business modules read **only** these:
`Employee`, `OrgUnit`, `Position`, `Job`, `Competency`, `AppraisalResult`, `Qualification`, `Experience`, `Course`, `CourseEnrolment`.

## Adapters (plug point)

- **1 adapter per source system.** Core HR, SSHR and Performance can be database views or internal APIs.
- The LMS adapter calls the client's 2 APIs. It is **read-only**.
- Call HTTP sources through **`IHttpClientFactory`** with the standard resilience handler (`Microsoft.Extensions.Http.Resilience`): retries, timeouts, circuit breaker.

```csharp
public interface ISourceAdapter<T>
{
    string System { get; }   // "CORE_HR", "LMS", …
    IAsyncEnumerable<SourceRecord<T>> FetchChangesAsync(DateTimeOffset since, CancellationToken ct);
    Task<SourceRecord<T>?> FetchOneAsync(string externalId, CancellationToken ct);
}
```

- Choose the implementation **by setting**: `Integration:<System>:Mode = Mock | DbView | Rest`.
- Business modules never change when an adapter is swapped.

## Sync

```
Hangfire nightly delta ─┐
On-demand refresh (1)  ─┴→ Adapter → map to canonical ⇄ ExternalIdMap
                                       ├─ idempotent upsert → local read tables
                                       └─ failure → dead-letter → admin dashboard
```

- Nightly sync done by **06:00**.
- **Idempotent upserts keyed on source IDs** (PostgreSQL `INSERT … ON CONFLICT`).
- One bad record goes to the **dead-letter table**. The batch continues.
- Store the last successful sync time per system and entity. The next delta starts from there.

## ID mapping

`ExternalIdMap(system, entity, external_id, internal_id)`. Use it for every cross-system key, including the login system (`SSO`).

## Mocks first

- Build a **mock adapter** for each source before the real one. Screens and tests run against the mocks.
- Mock data comes from **Bogus** builders, shaped like the real source.

## Do not

- Call a source system inside a user request (except the refresh action).
- Store source IDs as foreign keys in business tables.
- Reference an adapter type from a business module.
- Log personal data from source payloads.
