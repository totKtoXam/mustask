# tech-054 QA review (frontend, light form)

Verdict: no blocker, no major. E2E: no (standing answer).

## Defects

- minor: `TablePager` treats `count = -1` (MUI "unknown total") as one page, so the forward
  arrow would be inert. No consumer passes -1 (grep: none); only matters if one starts to.
  No action.

## Checked

- Plan covered: TablePager.test pins, on both edges, `aria-disabled="true"`, not natively
  disabled, focus kept on the arrow after paging there (Tab + Enter), a further Enter and click
  do not call `onPageChange` (exactly once). Mid-range arrows carry no `aria-disabled`.
- Existing `disabled` prop test kept (both arrows aria-disabled, clicks ignored).
- Consumers: DataTable and TableCard pass props through, AllOrganizationsPage test updated;
  uikit DialogsDemo uses the same component; no other test asserts native `disabled` on a
  pager arrow. Out-of-range `onPageChange` guard is correct (0..lastPage).
- Semantics: aria-disabled button stays in the Tab order, keeps its accessible name; inert look
  matches the `disabled` look.

## Verify

`VERIFY_FULL=1 bash scripts/verify.sh` -> exit 1: 6 failures in 5 files (BacklogPage.qa,
WorkItemFormPage.planning, WorkItemFormPage.readOnly x2, WorkItemsPage.assignee,
WorkItemsPage.keyword), each with 11-160 s durations (timeouts under machine load). None
of those pages use TablePager/DataTable/TableCard. The five files rerun alone: 79/79 green.
Judged flaky (load), not caused by this diff.

## Round 1

Code review over `origin/main...8dc96c6a` (3 code files, 2 docs). Verdict: **approve**.
0 blockers, 0 majors, 2 minors (debts, no round owed).

### Findings

- **CR-1 minor** `src/frontend/src/shared/ui/table/TablePager.tsx:55` — the range guard
  `next <= lastPage` also swallows a *back* click when `page > lastPage` (count shrank under
  the page): the back arrow looks active and does nothing, where MUI used to page back. Not
  reachable today (AllOrganizationsPage clamps the page, line 117; DataTable shows no pager
  with zero rows). Fix: guard by direction, not range:
  `next < page ? page > 0 : page < lastPage`.
- **CR-2 minor** `src/frontend/src/shared/ui/table/TablePager.test.tsx:101,133` — in the two
  tech-054 tests `toHaveFocus()` cannot fail on the old code: jsdom does no focus fixup
  (feat-033 qa.md D-11), so focus is not what pins the fix. The pin is "`aria-disabled` and
  `toBeEnabled()`" (lines 62-73). Fix: add `toBeEnabled()` on the arrow after paging in both
  tests, plus a one-line comment that jsdom cannot show the drop, so "not `disabled`" is the
  proxy.

### Checked

- Plan: edge arrows stay focusable, `aria-disabled`, inert look, click a no-op. Done.
  `disabled: false` in the slot props beats MUI's edge flag because `TablePaginationActions`
  spreads `slotProps.previousButton/nextButton` after its own `disabled`
  (@mui/material 9.4.0, `TablePaginationActions.js:104,116`). The guard turns the now-firing
  `onClick` into a no-op on both edges and under `disabled`.
- Correctness: `count = 0` gives lastPage 0, so both arrows are inert; `count = -1` is QA's
  minor. MUI swaps the slot props under RTL; the app is `ruRU`, LTR only, so this is not a
  finding.
- Security / architecture: nothing applies; the change stays in `shared/ui/table`.
- Tests: `npx vitest run src/shared/ui/table src/features/organizations/pages/AllOrganizationsPage`
  → 12 files, 191 passed. QA's 6 full-run failures are in workitems/backlog files that do not
  render TablePager/DataTable/TableCard, so load flakes are plausible; this diff did not cause them.

### Behavioural claims

- `TablePager.tsx:32-34` "stays in the Tab order … `disabled: false` overrides MUI's edge
  flag" → `TablePager.tsx:38-39` + `TablePaginationActions.js:104-120` (spread order).
- `TablePager.tsx:13-15` (`disabled` docstring) → `TablePager.tsx:55,61-62`; test line 141.
- status.md Plan "a click on it is a no-op" → `TablePager.tsx:55`; `TablePager.test.tsx:102-105,134-137`.
- Direction 2: the changed behaviour is that edge arrows are no longer natively `disabled`.
  Grepped `TablePager|TablePagination` × `disabl|arrow|focus|inert` in docs/, .claude/ and
  AGENTS.md. Hits: `docs/conventions/ui-kit.md:247` (describes `disabled` only; still true,
  says nothing about edges, and no wrong action follows); feat-033 qa/review/status (history,
  the defect this task closes); tech-015 design § 5.11 (no edge claim). None stale.
