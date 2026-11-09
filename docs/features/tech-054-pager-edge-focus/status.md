# tech-054-pager-edge-focus

- type: tech
- size: S
- branch: tech/054-pager-edge-focus
- stage: complete
- epic: platform/ui
- deps: -
- gates: gate-3

## Gates

gate-3: approved 2026-11-09 (CR-1/CR-2 fixed first) · PR https://github.com/totKtoXam/mustask/pull/110

## Plan

- What: on the first/last page MUI renders the prev/next arrow with native `disabled`, so a
  focused arrow leaves the Tab order and focus falls to `<body>` after paging to an edge.
- Where: `src/frontend/src/shared/ui/table/TablePager.tsx` (+ `TablePager.test.tsx`).
- How: an edge arrow stays focusable and is marked `aria-disabled` with the inert look — the
  same treatment the `disabled` prop already gives both arrows — and a click on it is a no-op.
- Check: a test pages to the last page and asserts focus is still on the arrow,
  `aria-disabled="true"`, and a further click does not call `onPageChange`; the same on the
  first page; `scripts/verify.sh` green.

## Log

- 2026-11-09 [orchestrator] route confirmed by the human: tech, S, frontend-developer →
  qa-engineer → code-reviewer → gate-3 → PR. Grounds: focus behaviour changes (not F), no L
  thresholds, ~1 h, 2 files, one layer (frontend shared/ui). Economy mode: one agent per
  stage, one Opus review.
- 2026-11-09 [orchestrator] queue line taken: PR #101 (auto-merge on green CI).
- 2026-11-09 [frontend-developer] b4e03d9d: edge arrows stay focusable, `aria-disabled` +
  inert look, out-of-range `onPageChange` ignored; TablePager tests pin focus on both edges.
  ec9bb698: `AllOrganizationsPage.qa.test.tsx` asserted native `disabled` on the first-page
  arrow — now `aria-disabled` (write zone widened by the orchestrator).
  `bash scripts/verify.sh` → exit 1 twice, each time one different workitems test
  (`WorkItemsPage.keyword`, then `WorkItemsPage.assignee` — timing-measured), both green
  alone, unrelated to the pager; left to QA's run to settle.
- 2026-11-09 [orchestrator] first QA run stopped by the human (a stray full Vitest run was
  eating memory); restarted at the human's word.
- 2026-11-09 [qa-engineer] 10b5f3de: frontend QA done, light form. Defects: none
  blocker/major, 1 minor (count=-1 reads as one page, no consumer does it). E2E: no
  (standing answer).
- 2026-11-09 [qa-engineer] `VERIFY_FULL=1 bash scripts/verify.sh` → exit 1: 6 timing failures
  in 5 files (BacklogPage.qa, WorkItemFormPage.planning/.readOnly, WorkItemsPage.assignee/
  .keyword, 11–160 s under load), none touching the pager; the 5 files rerun alone 79/79
  green, judged flaky, not this diff. CI on the PR settles it.
- 2026-11-09 [code-reviewer] b95ea33c: code review round 1 (review.md) — approve; 0 blocker,
  0 major, 2 minor (CR-1 range guard swallows a back click when page > lastPage, unreachable
  today; CR-2 focus assertions cannot fail in jsdom, add toBeEnabled). Targeted Vitest (table
  + AllOrganizationsPage) 191/191 green.
- 2026-11-09 [orchestrator] gate-3 requested.
- 2026-11-09 [human] gate-3: approved, on condition that CR-1 and CR-2 are fixed before the PR.
- 2026-11-09 [task-executor] 6a25ac82: CR-1 direction guard + test (page past lastPage still
  pages back), CR-2 `toBeEnabled()` in both focus tests. `npx vitest run src/shared/ui/table
  src/features/organizations/pages/AllOrganizationsPage` → exit 0 (192/192); `npm run lint` →
  exit 0. Full suite left to CI on the PR.
- 2026-11-09 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-054-pager-edge-focus

## Notes

Summary (from the queue line or an argument): TablePager: MUI natively disables the first/last arrows on the edge pages, so keyboard focus drops to the page start when paging reaches the first or last page; keep focus on the pager (aria-disabled or a focus hand-off) — found in feat-033 QA
