# tech-054-pager-edge-focus

- type: tech
- size: S
- branch: tech/054-pager-edge-focus
- stage: 05-frontend
- epic: platform/ui
- deps: -
- gates: gate-3

## Gates

gate-3: pending

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
- 2026-11-09 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-054-pager-edge-focus

## Notes

Summary (from the queue line or an argument): TablePager: MUI natively disables the first/last arrows on the edge pages, so keyboard focus drops to the page start when paging reaches the first or last page; keep focus on the pager (aria-disabled or a focus hand-off) — found in feat-033 QA
