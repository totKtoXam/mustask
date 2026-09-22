# ADR-0007: Fix the frontend UI stack and the screen-state canon

- Status: accepted (2026-08-24, a human decision in chat at gate-3 of
  the UI scaffolding task), partially narrowed by
  [ADR-0019](0019-ui-stack-narrowed-to-actual.md) (2026-09-07): the
  requirements about `@mui/x-data-grid`, `react-hook-form` + `zod`, and
  `@mui/x-date-pickers` are lifted, the screen-state canon remains in
  force
- Date: 2026-08-24
- Context: the UI kit and application shell scaffolding task, stage 3
  (design) and stages 6–8 (implementation, QA, review)

## Problem

The UI scaffolding task sets up the foundation that every screen in the system
will sit on: the MUI theme, the application shell, a set of `shared/ui`
wrappers, and three screen patterns (registry, form, card). Along the
way, several decisions were made about libraries and about how the
mandatory screen states are structured.

So far these decisions are recorded only in the `status.md` and
`design.md` of one completed tech task. Neither is a place the author of
the next feature looks into: a closed task's `status.md` is not read at
all, and `design.md` describes specific screens. At the same time the
decisions fall under two criteria from `docs/adr/README.md` at once — "a
library choice with long-term consequences" and "a pattern that every
slice will repeat".

A separate force: `@mui/x-data-grid` has a paid edition, and the boundary
between free and paid runs inside the API of a single component. Without
an explicitly fixed boundary it will be crossed by accident — not at the
moment the library is chosen, but six months later, in someone else's
slice, with a single prop.

## Options considered

### Option 1: leave the decisions in the task's `status.md` and `design.md`

- Pros: nothing needs to be done; the task's artifacts are already
  written.
- Cons: the next feature reads `docs/conventions/frontend.md` and the
  `design.md` of neighboring features, not the `status.md` of a closed
  tech task. The DataGrid license boundary is nowhere visible at the
  moment it is crossed. The scaffolding task's review pointed this out
  directly (finding R-5).

### Option 2: rewrite the decisions into `docs/conventions/frontend.md`

- Pros: every developer reads the conventions; one place.
- Cons: the convention answers "how to write", not "why this specific
  choice". Rationale and rejected alternatives don't belong in it, and
  without them the decision looks arbitrary and gets reopened in every
  argument.

### Option 3: an ADR + a short link from the convention

- Pros: the rationale and alternatives live in the decision log, as
  `docs/adr/README.md` requires; the convention stays an instruction; the
  link leads to the reasons. Changing the decision will require a new
  ADR, i.e. a deliberate step.
- Cons: one more document.

## Decision

Option 3.

**Libraries:**

- Registries — `@mui/x-data-grid`, **MIT edition only**. Row grouping and
  Excel export belong to the paid Pro edition and must not be used; if a
  registry needs that, it is a separate decision, not a slice edit.
- Forms — `react-hook-form` + `zod` via `@hookform/resolvers/zod`. This is
  not a new decision but the execution of `docs/conventions/frontend.md`,
  where both libraries have been named the standard from the start.
- Dates — `@mui/x-date-pickers` with the `dayjs` adapter and the Russian
  locale.
- All three Russian locales (core, DataGrid, date-pickers) are registered
  in a single `createTheme` call, after the options object.

**The screen-state canon.** Every screen must show: loading (skeleton),
empty (no data), empty after a filter, error with retry, access denied.
"Empty" and "empty after a filter" are different states with different
texts: a user who narrowed the filter themselves and a user who has no
data at all need different hints. The canon is implemented in a single
`shared/ui/DataState` component, the texts live in it, not with the
caller.

**The theme is the only place for customizing appearance.** The semantic
color palette is deliberately darker than MUI's defaults: the default
`warning` and `info` fail AA contrast for white text on filled `Chip` and
`Alert`, and the system is meant for a full workday of use.

The `docs/conventions/frontend.md` convention is extended with a
reference to this ADR.

## Consequences

It gets simpler: the author of a new feature does not choose the stack or
design states from scratch — they take `DataState` and the three screen
patterns. Review gets a checkable criterion instead of a matter of taste.

It gets harder: swapping any of the libraries is a new ADR and edits to
every slice at once. This is a deliberate price for consistency.

The DataGrid license boundary is fixed for the long term. The decision is
worth revisiting if registries broadly need Pro features — then the
question is not technical but budgetary, and it's for the human to
decide.

Creates the task `tech-NNN-frontend-bundle-splitting`: `DataGrid` and
date-pickers pushed the prod bundle up to 552 kB in a single chunk.
