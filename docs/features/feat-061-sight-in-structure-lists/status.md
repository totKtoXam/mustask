# feat-061-sight-in-structure-lists

- type: feature
- size: L
- branch: feat/061-sight-in-structure-lists
- stage: 4-backend
- epic: core/org-access
- deps: feat-041
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-12..-17,
BR-TD-21; story US-6, surfaces part 1; `design.md`; `docs/adr/0040-*.md`).
Their gates are not repeated here.

Scope (US-6 surfaces, part 1): BR-TD-17 — exactly the objects of
BR-TD-12..-15, before paging, so no count or «ещё» reveals a hidden one — on
the selector offers' read, «Структура» (list and tree), «Проекты»,
«Команды», «Обзор» and the pickers; the «Структура» filter field «Найти
департамент, команду или проект» (BR-TD-21: the same sight-scoped match,
hides what does not match in the open view, a department staying while a
child matches, not remembered, never navigates). One rule: feat-041's set
form of the sight rule (the evaluator / `AccessSet`), no second
implementation. Closes feat-041's debt R-4 (header selectors offering hidden
objects; the aggregate views' generic error).

Out of scope: the multi-project views, search, counts and the sprint clauses
(feat-062); the context path button and its panel search (feat-063).

Stages: backend (plan-backend.md, code, tests, api-contract.md) → backend QA
→ frontend → frontend QA → code review (one Opus pass with the security pass)
→ gate-3 → PR on GitHub, merge commit, auto-merge asked for, branch kept.
The PR waits for feat-041 (PR #203) to reach main; then origin/main is merged
in (merge, not rebase).

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-17 [orchestrator] task started on branch feat/061-sight-in-structure-lists in worktree .claude/worktrees/feat-061-sight-lists-c61481, cut from feat/041-project-visibility 9e0c9262 (PR #203 not merged yet); git rev-parse --show-toplevel and git branch --show-current confirmed. The worktree's stale core.hooksPath override (doc-012 worktree) unset.
- 2026-11-17 [orchestrator] route confirmed by the human in chat: L from stage 4 (the queue line said M; the L threshold "authorization rules change" fires: what each list and count discloses changes, a mistake is a leak); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only.
- 2026-11-17 [orchestrator] queue line taken by scripts/queue.sh take feat-061, PR #212 with squash auto-merge.
