# Delivery Goals

From *Implementation Approach: From BRD to Deployable Software* (draft, 9 Nov 2026).

## TL;DR

- **Short term (weeks 1–2):** deliver **1 real app** from 1 BRD using the kit, with real metrics.
- **Long term (about week 19):** a **repeatable BRD-to-release platform** that passes its own acceptance test.
- Either a **readiness report** or an **honest blocked-release report** counts as success. A falsely green report does not.

## Pilot goals (weeks 1–2)

- Build a **small real app**: about 10–25 requirements and 2–3 user roles.
- **Trace every acceptance criterion** to a story, a passing locked test and Azure DevOps items.
- **Find real bugs**, then fix and close them with evidence.
- Produce **security scan results** and **documentation**.
- Include an **AI assistant** that reads IFS test data, for internal and external users.
- Include **1 approve-then-write action** (a person confirms before the agent changes data).
- **Record metrics** in `METRICS.md`, such as cost and where effort went.

## Roadmap goals and exit criteria

| Stage | Weeks | Goal | Done when |
|---|---|---|---|
| 0 Pilot | 1–2 | 1 app with the kit | Pilot criteria met; metrics recorded |
| 1 Kit v2 | 3–6 | Automate review, checks, bug filing | 3 apps delivered; human hours per BRD falling |
| 2 Walking skeleton | 7–11 | Orchestrator, web UI, approvals | 3 clean end-to-end runs; restart and cost-limit recovery shown |
| 3 Verification and security | 12–16 | Enforced QA, defect loop, scans | Real defects closed with evidence; test-weakening and vulnerable dependencies caught |
| 4 Release | 17–19 | Packaging, deploy checks, production gate | Full platform acceptance test passes |
| 5 Hardening | 20+ | Recovery, performance, more stacks | Driven by usage |

## Release readiness goals (each delivered app)

- **Eval thresholds** met (AI answers reach the approved pass rate).
- **Safety tests at 100%**.
- **Cost per conversation** within budget.
- A **tested kill switch** (a way to turn the AI off fast).
- For external users: **penetration test** and **data-protection review** done.
- A **clean install** works from the release package.
