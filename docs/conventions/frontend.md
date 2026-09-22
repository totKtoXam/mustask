# Frontend conventions: React + MUI, Vertical Slice Design

## Structure

```
src/
  app/               # shell: router, MUI theme, providers, layout, guards
  features/          # vertical slices — the primary unit of code
    <feature>/       # e.g. order-list, order-form
      api/           # requests to the backend (per the feature's api-contract.md)
      components/    # slice components
      hooks/         # slice logic
      pages/         # pages wired into the router
      index.ts       # the slice's ONLY public entry point
  shared/            # reusable code WITHOUT business logic:
    ui/              # wrappers over MUI (PageHeader, ConfirmDialog…)
    api/             # http client, error handling, auth interceptor
    lib/             # utilities, formatters (dates, IIN/BIN etc.)
    config/          # constants, env
```

## Slice rules

- Slices do not import each other's internals — only `features/x`
  (index.ts). Anything shared between slices → `shared/` (if free of
  business logic) or is deliberately duplicated.
- `shared/` does not import from `features/`. `app/` assembles
  everything.
- One slice ≈ one feature from `docs/features/<id>/` — names are kept in
  sync.

## MUI

The choice of UI stack and the canon of screen states are fixed in
[ADR-0007](../adr/0007-frontend-ui-stack-and-screen-state-canon.md) — it
also covers the reasons and the boundary of the free edition of
`@mui/x-data-grid` (narrowed by
[ADR-0019](../adr/0019-ui-stack-narrowed-to-actual.md)). Ready-made
wrappers live in `src/frontend/src/shared/ui`, and the dictionaries feature
(`src/frontend/src/features/dictionaries`) is the reference registry, form
and card; a new feature takes them rather than designing from scratch.

- The theme is the only place for customizing appearance: palette,
  typography, overrides in `app/theme`. No hardcoded colors/spacing in
  components; `sx` — only for small layout details.
- Use standard MUI components (DataGrid, Autocomplete, Dialog,
  Snackbar…) before writing your own. Custom wrappers — in `shared/ui`,
  if needed by ≥ 2 slices.
- Forms: react-hook-form + a zod schema via `@hookform/resolvers/zod`;
  error texts — from design.md, not made up in code.
- Registries: `DataGrid` from `@mui/x-data-grid`, **MIT edition only**.
  Row grouping and Excel export are paid Pro features and must not be
  used (ADR-0007).
- Dates: `@mui/x-date-pickers` with the `dayjs` adapter and the Russian
  locale.

## Data and state

- Server state — TanStack Query: keys `[feature, entity, params]`,
  invalidation after mutations. No duplicating server data in local
  state.
- Client state — useState/useReducer in the slice; a global store — only
  for what is genuinely global (the current user, permissions).
- The slice's API layer is written strictly to `api-contract.md`; DTO
  types — in the slice's `api/` (or generated from OpenAPI, once it
  exists).

## Mandatory states for every screen

loading (skeleton), empty, error (with retry), and permission behavior —
as described in the feature's design.md. A screen without them does not
pass QA.

## Tests

- Logic (hooks, utilities) — unit (Vitest).
- Key screen scenarios — Testing Library + MSW (mocked per api-contract).
- E2E happy path of the feature — Playwright (`src/frontend/e2e/`,
  `npm run e2e`), at the QA stage and only if the human answered yes to the
  E2E question there (`docs/ai-workflow/stages/05-frontend.md`). Not part of
  `npm test` or `scripts/verify.sh` — it needs a live, seeded stand.

## Miscellaneous

- TypeScript strict; ESLint + Prettier — green before commit.
- Interface texts — Russian, from design.md; no made-up text in code.

## Named values

- The rules are shared with the backend (`backend.md`, "Named values"): a
  literal with domain meaning gets a name; values that are coincidentally
  equal are not merged.
- Backend error codes (`errorCode` from ProblemDetails) — string
  constants in `shared/api`, one-to-one with the slice's
  `api-contract.md`; branch only on them, never on the message text
  (texts are localized, СТ-1).
- Closed sets — union types or `as const` objects, not scattered string
  literals.
