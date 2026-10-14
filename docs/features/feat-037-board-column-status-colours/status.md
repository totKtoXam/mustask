# feat-037-board-column-status-colours

- type: feature
- size: S
- branch: feat/037-board-column-status-colours
- stage: complete
- epic: core/backlogs-boards
- deps: -
- gates: gate-3

## Gates

gate-3: approved · orchestrator under the human's standing approval of 2026-10-05 (D-1); review round 1 approved, F1 fixed; E2E not run, paused by the human

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

1. `features/boards/components/BoardColumns.tsx`: every column's box gets a 4 px top border and its header a `CategoryMarker` dot, both in `theme.workTracking.categoryColour(column.category.code)` — the colours the backlog's and the card's category dots already use. No new token, no API change (`BoardColumn.category` is already in the response).
2. One test in `BoardPage.test.tsx`: the four seeded columns' top border and dot match their category's colour.
3. Not touched: the card's own look, the drop-area highlight (a dashed outline over the same box), the backend.
4. Check: `npm test` for `features/boards` and `kitGuard`, `tsc -b`, ESLint, Prettier, `scripts/verify.sh`. E2E skipped (standing answer, memory `mustask-e2e-paused-2026-10-12`).

## Decisions

- D-1 (human 2026-10-13, in chat and through AskUserQuestion): «В канбан доске добавь цвета для колонок по статусу» → «Цветная полоса сверху + точка в заголовке». The colour is the column's status category (Proposed, InProgress, Resolved, Completed, Removed), not a per-state colour: states of one name already share one category (bug-004). Short route under the human's standing approval of 2026-10-05; the orchestrator decides gate-3 and the merge.

## Log

- 2026-10-13 [orchestrator] gate-3: approved under the human's standing approval of 2026-10-05 (D-1): review round 1 approved with F1 fixed; E2E not run (paused by the human); stage complete, local squash via scripts/finish-task.sh.
- 2026-10-13 [review] round 1: approve, 0 blocker/major, 1 minor (F1: unguarded `null` in the new test) fixed in the same pass; `review.md`.
- 2026-10-13 [dev] BoardColumns draws the stripe and the dot; one test added; `BoardPage*`, `kitGuard`, `tsc -b`, ESLint and Prettier green on the touched files. Review, `verify.sh` and the merge follow.
- 2026-10-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-037-board-column-status-colours

## Notes

Summary (from the queue line or an argument): Board columns coloured by their status category: a colour stripe on top and the category dot in the header, the colours of the backlog's category dots (human 2026-10-13)
