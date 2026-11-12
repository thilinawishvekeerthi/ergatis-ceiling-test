# BRD to Deployable Software — Plain-English Summary

Summary of *Implementation Approach: From BRD to Deployable Software* (draft, 9 Nov 2026).

## TL;DR

- A **plan** for turning a **BRD** (Business Requirements Document) into working, tested software using Claude Code.
- Start with a **2-week pilot** using a delivery kit. Then follow a roadmap to a full automated platform by about week 19.
- Humans approve at fixed gates. Agents cannot weaken tests to pass.

## The problem it solves

- The original "Master Implementation Prompt" was **too big** to build in one pass.
- One pass would give a wide but shallow, stubbed codebase.
- This approach splits the work into small, checked steps.

## Part 1: The 2-week delivery kit

- A **repo template** for Azure DevOps.
- Engineers run **one slash command per stage**:

```
/analyze → /architect → /plan → /acceptance → /build → /verify → /fix → /secure → /document → /release
```

- Extra stages: `/ifs-contracts` (pin IFS API specs) and `/evals` (AI evals and safety tests).
- **Humans approve** by merging pull requests:
  - Gate 1: requirements (Project Owner).
  - Gate 2: architecture (Technical Approver).
  - Release decision (Release Approver).

### Anti-cheating controls

- Approved tests are **locked** with a checksum. Any change fails CI.
- Test changes and app changes can never share a PR.
- Reviewer agents are **read-only**.
- No agent can close a bug without evidence, approve a gate, or deploy to production.
- The real enforcement lives in **branch policies and CI**, which agents cannot change.

## Part 2: Roadmap

| Stage | Weeks | Goal |
|---|---|---|
| 0 Pilot | 1–2 | 1 real app with the kit |
| 1 Kit v2 | 3–6 | Automate the gaps |
| 2 Walking skeleton | 7–11 | Orchestrator, web UI, approvals |
| 3 Verification and security | 12–16 | Enforced QA and scans |
| 4 Release | 17–19 | Full platform acceptance test |
| 5 Hardening | 20+ | Driven by usage |

- **Decision point A** (week 6): build the platform only if the pilot shows the need.
- **Decision point B** (week 19): roll out only if tests pass and cost is acceptable.

## Other topics

- **Cost:** tools about $500–1,600 for the pilot. About 90% of total cost is engineer time.
- **Devin comparison:** Devin is task-level coding. The kit governs the whole BRD-to-release process. They can be combined.
- **IFS Cloud:** solutions call IFS APIs from outside. Changes inside IFS are out of scope.
- **Agentic layer:** every solution gets an AI assistant for internal and external users.
  - It reaches IFS only through its own **MCP server** (a controlled tool gateway).
  - Writes need human approval.
  - Safety tests must pass 100%.
- **Not yet verified:** Azure DevOps commands and the IFS export script have not run against live systems.

See also: [delivery goals](delivery-goals.md) and the [pilot BRD](brd/BRD.md).
