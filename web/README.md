# Talent web

React 19 + TypeScript (strict) + Vite. Server data with TanStack Query, forms with React Hook Form + Zod, charts with Apache ECharts.

## Component library: MUI

The architecture left the choice open (MUI or Ant Design). We picked **MUI** (`@mui/material`) for its accessibility defaults and first-class React Hook Form support. Never mix it with Ant Design.

## Commands

```bash
npm install        # .npmrc sets legacy-peer-deps (npm 10 crashes resolving vite peers otherwise)
npm run dev        # Vite; proxies /api and /bff to http://localhost:5000
npm test           # Vitest + React Testing Library + MSW
npm run typecheck  # tsc --noEmit
npm run lint
npm run build
npm run e2e        # Playwright, real backend + PostgreSQL (see below)
```

## End-to-end run

1. `../scripts/pg-start.sh` (PostgreSQL on 127.0.0.1:5433).
2. `npm run e2e`. It starts the backend (`dotnet run`, Development: migrates, syncs mock Core HR data, seeds dev users) and Vite if they are not running.
   With an installed Chromium: `PLAYWRIGHT_CHROMIUM_PATH=/usr/bin/chromium npm run e2e`. Screenshots go to `e2e-artifacts/`.

Dev users (`src/Talent.Host/appsettings.Development.json`): `dev.employee` (E00002) reports to `dev.manager` (E00001); `dev.admin` is HR.

## Layout

- `src/api` hand-written typed client (`client.ts`), types, TanStack hooks. All HTTP goes through the client: `/bff/*` (session), `/api/v1/config/*` and `/api/v1/workflows/*`.
- `src/session` reads the signed-in user from `GET /bff/user`. In development the landing page signs in through `/bff/fake-login`; a production build links to `/bff/login`. Hiding screens is not security.
- `src/components` `NineBox` (generic N x M grid, driven by a grid definition, keyboard operable) and `ScoreBreakdown`.
- `src/features/config` scales and grids admin screens: edit a draft, publish it, start a new version from a published one.
- `src/features/workflow` the employee's development plans (IDP approval) and the approvals inbox for managers and HR.
- `src/portals` employee, manager and HR admin shells. Career Centre, assessments and team plans are placeholders until their modules exist.
- `src/labels.ts` all user-facing text.
