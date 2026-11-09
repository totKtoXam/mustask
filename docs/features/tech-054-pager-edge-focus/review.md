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
