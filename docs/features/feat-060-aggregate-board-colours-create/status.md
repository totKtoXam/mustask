# feat-060-aggregate-board-colours-create

- type: feature
- size: S
- branch: feat/060-aggregate-board-colours-create
- stage: complete
- epic: core/backlogs-boards
- deps: feat-048,feat-037
- gates: gate-3

## Gates

gate-3: approved · 2026-11-13 (fix m1, m2, m4, m5 before the PR; m3 debt)

## Plan

1. Colours: the aggregate board (`AggregateBoardView.tsx`, feat-048) gets the team board's status-category top stripe and header dot per column and the type left stripe per card (feat-037, `BoardColumns.tsx`) — the stripe styles extracted into one shared piece both views use, not copied.
2. Create: a «Создать» command in the aggregate board's header (department and organization levels) opening the work-item create dialog with a required «Проект» picker among the active projects in the view's scope, as the team views' create (feat-040 BR-TS-9, `BacklogPage.createProject.test.tsx`); preselected when the view's project filter names exactly one. Only for a user with `workitems.workitem.write`; the created item shows up on the board.
3. Frontend only, on existing APIs (the create endpoint is the project's own). If the scope's project list needs a new API read, stop and re-route as M (route confirmation 2026-11-12).
4. Check: Vitest page tests — column stripe and dot by category, card stripe by type, create command present/absent by permission, project required, created item appears; the team board unchanged.

## Decisions

- 2026-11-12 D-1 (human, «⭐ Одна задача на оба»): the aggregate board gets a create command. This reverses, for the board only, feat-048 BR-AV-8's "no create command" and its spec § Operation completeness "no create command on the aggregate views"; the aggregate backlog and «Рабочие элементы» keep feat-048's rule. A note is added to feat-048's status.md. S has no spec, so this line is the record.
- 2026-11-12 E2E: no (standing answer, E2E paused since 2026-10-12).

## Log

- 2026-11-12 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-060-aggregate-board-colours-create
- 2026-11-12 [orchestrator] route confirmed by the human: feature, S (frontend only, one layer, existing APIs; ~5-8 files with tests), frontend-developer → qa-engineer (light) → code-reviewer → gate-3 → PR. Gates: gate-3 only. Queue line taken (scripts/queue.sh take feat-060). Economy mode: one agent per stage, one Opus review, verification by CI.
- 2026-11-12 [frontend] aggregate board: shared stripes (boardStripes.ts, team board output unchanged), column dot, create command (header label NEW_WORK_ITEM_LABEL as on the team views) + CreatePlace 'scope'; project list from the shell's selector-offers (department board: its projects) ∩ projects with workitems.workitem.write; no new API; commit a6b36501. Vitest: AggregateViews* 42 passed; team/project create and board suites 675 passed, 1 skipped; tsc + eslint clean. verify.sh not run (economy mode, CI).
- 2026-11-13 [qa] light QA at 27544300: plan items 1-4 covered except the project-filter preselect (unreachable at org/department level — dead branch, minor) and no assertion that aggregate «Рабочие элементы» has no create command (minor); boards suite 70 files / 869 passed, CreateWorkItemDialog 23 passed; no tests added; 0 blocker, 0 major, 2 minor. Stage → 06-code-review.
- 2026-11-13 [code-review] round 1 (Opus) at c2bdc4e9: approve, 0 blocker, 0 major, 5 minor (m1 dead preselect + doc claim, m2 contextGone ignored, m3 board remount on create, m4 test precision, m5 feat-048 spec.md:95 stale) — review.md, commit ce964c29. Gate-3 requested.
- 2026-11-13 [orchestrator] gate-3 answer from the human: «⭐ Починить m1,m2,m4,m5 → PR» — fix m1, m2, m4, m5 before the PR without another review round (the human sees the diff in the PR); m3 (board remount on create) accepted as debt. Header label: «⭐ «Новый рабочий элемент»» — stays NEW_WORK_ITEM_LABEL, as on the team views.

- 2026-11-13 [frontend] review fixes m1, m2, m4, m5, commit 4a3da935: m1 preselect removed (no project is preselected at organization/department level — no project filter exists there), dead filterProjectIds dropped from the scope place; m2 contextGone re-reads the headers; m3 comment states the remount (accepted debt); m4 exact type colour, write-on-one-project case, «Рабочие элементы» no-create case; m5 feat-048 spec.md:95 clause. Vitest 6 files / 69 passed; tsc + eslint clean.
- 2026-11-13 [orchestrator] stage complete; push and PR next.

## Notes

Summary (from the queue line or an argument): Aggregate board (department and organization levels, feat-048): the columns get the status-category stripe and dot of the team board (feat-037) and the cards the type stripe; and a «Создать» command with a required project picker, as on the backlog (feat-040) — reverses feat-048's 'no create command on the aggregate views' for the board
