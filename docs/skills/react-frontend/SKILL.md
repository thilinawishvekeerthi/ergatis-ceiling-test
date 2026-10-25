---
name: react-frontend
description: Frontend conventions - React + TypeScript (strict), BFF cookie login, role-based portals, the configurable nine-box grid component, score breakdowns, Apache ECharts, TanStack Query and React Hook Form + Zod (ADR-03, 06, 07, 12). Use when building or reviewing UI screens, forms, data grids, charts or the nine-box.
---

# Frontend: React + TypeScript

Source: `docs/talent-solution-architecture.md` ADR-03, 06, 07, 12.

## Stack

| Need | Use |
|---|---|
| Language | **TypeScript** with `"strict": true`. No `any`. |
| Components | 1 library: **MUI or Ant Design** (not yet chosen; never mix them) |
| Server data | **TanStack Query** |
| Forms | **React Hook Form** + **Zod** |
| Charts | **Apache ECharts** (same as the PDF reports) |
| API types | **Generated from the OpenAPI document** |
| Lint / format | **ESLint** + **Prettier** in CI |
| Tests | **Vitest + React Testing Library + MSW**. Write the test first. See `tdd-and-testing`. |

## Login (BFF)

- The app never handles tokens. Login and logout are redirects to the host's `/bff/login` and `/bff/logout` endpoints.
- API calls go to the **same origin** with the session cookie. Send the **anti-forgery** token on state-changing calls.
- On **401**, redirect to login. On **403**, show a "no access" message.

## Portals

| Portal | Screens |
|---|---|
| Employee | Career Centre, own IDP (view / acknowledge) |
| Manager | Assessments, team IDPs |
| Board | Calibration, nine-box, recommendations |
| HR Admin | Config versions, cycles, succession, sync dashboard, reports |

## Nine-box (`TalentGrid`)

- 1 generic component. Rows, columns, labels and colours come from the pinned grid config.
- Supports 2 × 2 to 5 × 5. **Never assume 3 × 3.**
- With overrides, show the proposed and the final cell. Ask for a reason.
- 2,000 people in under 3 seconds: filter, page or virtualise.

## Rules

- Show the **score breakdown** for % match, bench strength and IDP progress.
- The **server masks** confidential data. Hiding a button is never the security.
- Never put confidential data in URLs, local storage or logs.
- Workflow buttons come from the transitions the API allows for this user.
- On **409** (concurrency conflict), tell the user someone else changed the record and offer a reload.
- No hard-coded labels, scales or states. English only for now, with labels kept in 1 place.
- Target: 95% of screens under **2 seconds**.
