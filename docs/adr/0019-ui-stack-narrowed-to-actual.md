# ADR-0019: Narrow the UI stack to what actually exists — MUI core instead of x-data-grid and controlled forms instead of react-hook-form

- Status: accepted (by the human in chat, 2026-09-08)
- Date: 2026-09-07
- Context: the architecture stage of the series that moved the system onto
  Keycloak; the divergence was uncovered during that review. Narrows [ADR-0007](0007-frontend-ui-stack-and-screen-state-canon.md)

## Problem

ADR-0007 fixed the UI stack on the UI scaffolding task: registries on
`@mui/x-data-grid` (MIT), forms on `react-hook-form` + `zod`, dates on
`@mui/x-date-pickers`, the screen-state canon as a single component
`shared/ui/DataState` with five states, including a separate "empty after
filter."

Since then the scaffold has stopped existing: `src/frontend` was replaced
by the panel, and the remaining scaffold pages were removed. The panel's
actual code uses none of the four
libraries — they are not in `src/frontend/package.json`; lists are built on
`Table` + `TablePagination`, forms are controlled, dates are formatted by
`dayjs` in `shared/lib/format.ts`, and `shared/ui` holds three components
(`ConfirmDialog`, `ErrorBanner`, `LoadingBox`) — `DataState` is not among
them, and "empty" is one and the same for every page's cases.

While this was one feature's debt, the divergence could be tolerated. Once
the code becomes the foundation every later screen copies, the choice must
be made explicit: either rewrite a dozen working pages to match the
document, or bring the document in line with reality.

## Options considered

### Option 1: rewrite the code to match ADR-0007

- Pros: the canon is honored; DataGrid gives out of the box what is
  currently hand-written (sorting, column selection, virtualization).
- Cons: a separate size-L task ahead of the Keycloak work — a dozen working,
  test-covered pages get rewritten for the sake of matching the document; regression
  risk with no new user-facing outcome.

### Option 2: narrow ADR-0007 to the actual stack (chosen)

- Pros: the existing pages carry over without a rewrite; the document
  stops diverging from the code; the decision on DataGrid is made when
  there is a real reason for it.
- Cons: uniformity with what was originally intended for MUSTASK registries is
  lost; when the first large registry arrives, part of the work will have
  to be redone — but this time knowingly.

### Option 3: leave the divergence as is

- Cons: the ADR stops being a source of truth, and the next agent, reading
  ADR-0007, will add DataGrid to one slice and end up with two different
  kinds of tables. Rejected.

## Decision

The frontend stack is what is in the code: MUI core (`Table`,
`TablePagination`), controlled forms, `dayjs` for dates, React Query for
loading, `react-router-dom` for routes. The ADR-0007 requirements for
`@mui/x-data-grid`, `react-hook-form` + `zod` and `@mui/x-date-pickers` are
lifted.

**The screen-state canon stays mandatory** — loading, empty, empty after
filter, error with retry, denied by permissions — but is implemented by
each page's own means rather than by a single `DataState` component. This
is recorded as debt at the same time: "empty after filter" currently does
not differ from "empty," and this needs to be finished in the shared layer
(a follow-up task).

`@mui/x-data-grid` comes back once a registry appears with server-side
pagination, sorting and filtering per ADR-0015/ADR-0016 — as a separate
decision with a real consumer, not in advance.

The `docs/conventions/frontend.md` convention is brought in line together
with the acceptance of this ADR.

## Consequences

- **Simpler:** the code carries over as-is; ADR-0007 no longer
  promises what is not in the repository.
- **Harder:** tables stay manual — sorting and column selection, if needed,
  are hand-written or DataGrid is brought back by a separate decision.
- **Spawned task:** finish "empty after filter" in the shared layer
  (a follow-up task), or the canon stays a declaration here too.
- **When to revisit:** the first large MUSTASK registry (the work item
  list) — that is
  where ADR-0015 server-side pagination and, likely, DataGrid apply.
