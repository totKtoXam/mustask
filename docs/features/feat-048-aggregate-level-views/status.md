# feat-048-aggregate-level-views

- type: feature
- size: L
- branch: feat/048-aggregate-level-views
- stage: 02-requirements-review
- epic: platform/ui
- deps: feat-043
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- L: see plan-backend.md / plan-frontend.md -->

## Decisions

## Log

- 2026-11-03 [orchestrator] task started in the worktree .claude/worktrees/mod-018-build-9e3722 (app-made; branch feat/048-aggregate-level-views cut from origin/main 41d784ee; new-task.sh not used because the session is already bound here; the app's sync tool refused, origin not pinned). Route confirmed by the human: feature, L, not the queue's M (threshold: an ADR is owed for the group reads' cursor, which deviates from ADR-0014/0015's page envelope, and for the server-side read limiter of feat-043 design § 12). Full route, gates 1/2/3 the human's; the spec is reused: spec.md indexes feat-043's US-6, US-10, BR-NV-12, BR-NV-20, approved at feat-043's gate-1 and gate-2, and gate-1 covers only what this line adds. Block 3.3: only the queue line. Economy mode; E2E no. Queue: `queue.sh take` PR #52 (auto-merge on). Usage at start: weekly 17%, 5-hour 5%.

- 2026-11-03 [orchestrator] Stage 1 done (business-analyst): spec.md as an index into feat-043's folder plus a Delta of OQ-1..OQ-5 (429 handling, limiter scope, per-tab cap against the per-user limit, failed cursor reads, the project list's «Команда» control left by feat-043 against BR-NV-12); no new story. Deps checked merged on origin/main by the orchestrator: feat-003 #32, mod-006, mod-017 #11, mod-020, feat-040 #31, feat-043 #49. verify.sh green.

## Notes

Summary (from the queue line): «Рабочие элементы», «Канбан» and «Бэклог» at the organization and department level and «Рабочие элементы» at the team level: the organization-wide work-item list with feat-003's filters and keyword rules, and the aggregate backlogs and boards of every active project (department: the projects placed in it) as one collapsible group (swimlane on the board) per project in the project's own rank order, with a move inside a group and rows loaded per project as a group opens (reads capped at 4 in flight per tab, paged by a cursor), with the additive reads and the budgets of BR-NV-12 and BR-NV-20. Spec and stories US-6, US-10 live in feat-043's folder (its OQ-9); takes feat-042's board and backlog part, BR-NE-8's cross-project list and mod-020 CR-2's test.
