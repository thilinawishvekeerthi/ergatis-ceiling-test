---
name: versioned-configuration
description: Rules for configurable, versioned objects - ConfigDefinition/ConfigVersion lifecycle, the N x M grid engine (nine-box), rule-based eligibility and pluggable scoring strategies (ADR-02, 03, 04, 06). Use when building or changing scales, grids, eligibility rules, scoring weights, workflow definitions, permission matrices, or anything a client might configure.
---

# Versioned configuration engines

Source: `docs/talent-solution-architecture.md` ADR-02, 03, 04, 06.
These engines turn client blockers B1 to B5 into **configuration data before UAT**. Never hard-code the values.

## ADR-02: config versions

```
ConfigDefinition (id, type, code, name)
  └─< ConfigVersion (id, definition_id, version_no, status, effective_from,
                     payload_json, checksum, published_by, published_at)
TalentReviewCycle (…, pinned_versions_json)
```

Lifecycle: `Draft → Published → Locked → Retired`

| Rule | Meaning |
|---|---|
| **Lock on first use** | Locked when a cycle or record first references it. Then immutable. |
| **Change = new version** | Copy the previous version, then edit the copy. |
| **Cycles pin versions** | At launch, a cycle stores all its config version IDs. |
| **In-flight forms keep their version** | They finish on the workflow version they started with. |
| **Reporting bands** | Each scale level maps to a stable band, so trends work across versions. |

Applies to: scales, nine-box grids, eligibility rules, scoring weights, workflows, permission matrices.

Storage: **typed tables** for scales (reports query them). **JSONB** only for whole definitions (workflows, rules).

## ADR-03: grid engine

- The nine-box is a configured **N × M grid**. Each axis binds to a scale.
- Score ranges map to rows and columns. Each cell has a label, a colour and a reporting band.
- Placement modes: `Manual`, `Calculated`, `CalculatedWithOverride`.
- An override **needs a reason**. Store `ProposedCell`, `FinalCell`, `OverriddenBy` and `Reason`.
- Limits: grids from **2 × 2 to 5 × 5**, scales from **2 to 10 levels**. Validate these on publish.

## ADR-04: eligibility rules

- Evaluate rules against the **cycle snapshot**, not live data.
- Grammar: AND/OR of `field operator value`. **No scripting.** Never compile or evaluate user text as code.
- Parse rules into a typed expression tree. Allow only fields and operators from a fixed list.
- Example: `last_n_appraisals(2).min_score >= 4 AND grade IN (M1,M2,M3) AND service_years >= 2`
- Flow: system suggests → admin adds or removes people with a reason → list frozen at launch.

## ADR-06: scoring strategies

```csharp
public interface IProfileMatchStrategy
{
    ScoreResult Score(EmployeeProfile employee, JobRequirements job, MatchWeights weights);
}

public sealed record ScoreResult(decimal Score, IReadOnlyList<ScoreComponent> Breakdown);
```

| Strategy | Options |
|---|---|
| `IProfileMatchStrategy` | Weighted qualification + experience + competency gap. Weights per job family. 0 to 100%. |
| `IBenchStrengthStrategy` | Count-based, or weighted by readiness band |
| `IIdpProgressStrategy` | Equal-weight actions, weighted actions, or LMS-driven |

- Register each implementation as a **keyed service**. The config version stores the strategy code. See `dotnet-modular-backend`.
- Always return and show the **breakdown**. HR will challenge these numbers.
- Strategies are **pure**: no database calls, no clock. That makes them easy to test first.

## Checklist for a new configurable object

- [ ] Stored as `ConfigDefinition` + `ConfigVersion`
- [ ] Publish validates limits and sets the `checksum`
- [ ] Locked versions reject edits (test first)
- [ ] Referenced by version ID, never by code
- [ ] Ships with a default version
