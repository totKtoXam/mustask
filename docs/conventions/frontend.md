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

The UI stack is MUI core and `@mui/icons-material`, as
[ADR-0019](../adr/0019-ui-stack-narrowed-to-actual.md) narrowed
[ADR-0007](../adr/0007-frontend-ui-stack-and-screen-state-canon.md) to what
the panel uses. [ADR-0024](../adr/0024-app-shell-and-visual-language.md)
fixes the shell, the visual language, the shared patterns, the work context
in the address and the type appearance tokens. Its catalogue is mod-015's
[design.md](../features/mod-015-app-shell-design-system/design.md) with the
clickable prototype beside it: a module's `design.md` fills those patterns
with its own fields and words instead of drawing a layout. The dictionaries
feature (`src/frontend/src/features/dictionaries`) is the reference
registry, form and dialog in code.

- **Shell.** Every signed-in page renders inside `app/AppLayout`: the top
  bar (product mark, context switcher, page trail, user menu), the main
  navigation and, on settings pages, the settings navigation. A page never
  draws its own frame. The line that builds a page registers its entry in
  `app/navigation/model.ts` (label, scope, permission code, landing address,
  and an icon for a main navigation entry, which the type requires); an
  entry exists only once its page does, and is never shown disabled.
- **Theme.** `app/theme` is the only place for appearance: colour roles,
  the semantic palette, the type scale, the `layout.*` and `density.*`
  tokens, breakpoints and component defaults. No colour literal outside it,
  test files (`*.test.*`) and `src/test/` excepted:
  `src/test/colourLiterals.test.ts` fails `npm test` on one. Every other
  dimension is a theme token or the spacing unit, checked in review. Slices
  read the custom keys through the theme object (`useTheme()`, `sx`
  callbacks), never by importing `app/`; `sx` is for layout details.
- **One accent.** A page header or dialog has at most one filled primary
  action; secondary commands are text buttons with a leading icon and a
  neutral label; selection controls and progress stay neutral.
- **Shared patterns** in `shared/ui`, taken rather than re-drawn:
  `PageHeader` (the title as the page's only `h1` and the browser tab, one
  primary action, commands with «Ещё действия» overflow, views as tabs; the
  shell derives the trail's part the navigation model knows, «Настройки /
  <section>» and the link to the page's entry, so a page passes as
  `ancestors` only the pages between its entry's page and itself),
  `StateFrame` with `AccessDenied` and `NotFound` by level, `FilterBar` and
  `SortableHeaderCell` for registries, `RegistrySkeleton` and
  `FormSkeleton`, `ErrorBanner` with an optional title, `ConfirmDialog`
  with a destructive flag, `FormDialog` and `FormPanel`, and `TreeRegistry`
  for a hierarchy (a WAI-ARIA treegrid with its keyboard model, which names
  nothing of what its nodes are; mod-002). A new wrapper goes
  to `shared/ui` when it is a pattern of that catalogue or two slices need
  it.
- **Work context in the address.** Organization-, project- and team-level
  pages live under `/organizations/:organizationId/…`,
  `…/projects/:projectId/…` and `…/teams/:teamId/…`. Ids are untyped
  strings the page validates; a context that is not found answers
  `NotFound` for its level. The current context comes from
  `useWorkContext()` (`shared/lib/workContext`).
- **Permissions.** Pages and navigation decide through one check —
  `useAuth().can(code)` for instance codes, `useAccessCheck()`
  (`shared/lib/access`) for a scoped code — and keep no copy of its
  answers.
- **Work item type look.** A type's icon and colour come from its stored
  appearance token through `theme.workTracking.typeAppearance(token)`, a
  state's colour from its category code through `categoryColour(code)`;
  colour is never the only carrier.
- **Components.** Standard MUI core components (`Table` with
  `TablePagination`, `Dialog`, `Menu`, `Autocomplete`, `Snackbar`…) before
  your own. `@mui/x-data-grid` is not in the stack; its revisit at the work
  item list is mod-003's (ADR-0019).
- **Forms:** controlled inputs; error texts from design.md, not made up in
  code; a field's error on the field (`error`, `helperText`), any other in
  the form's banner.
- **Dates:** formatted with `dayjs` in `shared/lib/format.ts`.
- **Widths:** supported down to 640 CSS px, with the overlay navigation
  below 1024 px (ADR-0024).

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

loading (skeleton), empty, empty after a filter, error (with retry), not
found, and permission behavior — as described in the feature's design.md,
built from the shared frame and banner (ADR-0024). A screen without them
does not pass QA.

## Tests

- Logic (hooks, utilities) — unit (Vitest).
- Key screen scenarios — Testing Library + MSW (mocked per api-contract).
  Under Vitest, `@testing-library/react` resolves to
  `src/test/testingLibrary.tsx`, so every `render` and `renderHook` runs
  under the app theme; `src/test/viewport.ts` sets the window width and
  reduced motion that `useMediaQuery` reads (1440 px by default).
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
