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
```

## Layout

- `src/api` hand-written typed client (`client.ts`), types, TanStack hooks. All HTTP goes through the client; the config calls use the real `/api/v1/config` routes (list by type, get/save/publish by version id). Types stay hand-written until the OpenAPI document exists.
- `src/session` fake session provider. Swap for one that reads the BFF user; nothing else changes. Hiding screens is not security.
- `src/components` `NineBox` (generic N x M grid, driven by a grid definition, keyboard operable) and `ScoreBreakdown`.
- `src/features/config` scales and grids admin screens.
- `src/portals` employee, manager and HR admin shells. Employee and manager screens are placeholders.
- `src/labels.ts` all user-facing text.
