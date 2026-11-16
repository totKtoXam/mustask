# feat-041-project-visibility

- type: feature
- size: L
- branch: feat/041-project-visibility
- stage: 4-backend
- epic: core/org-access
- deps: mod-019,mod-021
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-12..-16,
OQ-5 A; story US-6, its core; `design.md`; `docs/adr/0040-*.md`). Their gates
are not repeated here.

Scope (US-6 core): the sight rule of BR-TD-12..-16 in the evaluator's three
forms — a reaching grant, membership of a linked team, or the project's
«Видим всей организации» — with the team and department gates (BR-TD-13,
-14), the flag set in the project's «Настройки» under
`organizations.visibility.write`, its upgrade value off for every existing
project with the per-project log of who lost sight (OQ-5 A), the answer order
(«не найден» before 403; mod-016 api-contract § 0.1 rows 2-4, § 3.4;
BR-M1-26; RetrofitAccessTests restated), and the assignee candidates (those who
see the project). This rewords mod-008's premise that results are readable
across the organization.

Out of scope: applying the rule on lists, selectors, «Структура», «Обзор»,
pickers (feat-061) and on the multi-project views, search, counts and the
sprint clauses owed to mod-007, feat-057, feat-058 (feat-062).

Stages: backend (plan-backend.md, code, tests, api-contract.md) → backend QA
→ frontend («Видим всей организации» in the project's «Настройки», the
assignee picker, «не найден» answers) → frontend QA → code review (one Opus
pass with the security pass) → gate-3 → PR on GitHub, merge commit, auto-merge
asked for (tech-072), branch kept.

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-16 [orchestrator] task started on branch feat/041-project-visibility in worktree .claude/worktrees/workflow-debts-c5f842 (host DESKTOP-2S17CA7, git rev-parse --show-toplevel and git branch --show-current confirmed), cut from origin/main 66727b0e. The worktree's stale core.hooksPath override (doc-012 worktree) unset.
- 2026-11-16 [orchestrator] route confirmed by the human in chat: L from stage 4 (the queue line said M; the L threshold "authorization rules change" fires); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only. Migration additive (a project column, off by default).
- 2026-11-16 [orchestrator] queue line taken by scripts/queue.sh take feat-041, PR #198 with squash auto-merge.
