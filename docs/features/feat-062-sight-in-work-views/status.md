# feat-062-sight-in-work-views

- type: feature
- size: L
- branch: feat/062-sight-in-work-views
- stage: 8-review
- epic: core/org-access
- deps: feat-041
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-17, the
Non-disclosure NFR; story US-6, part 2; `design.md`; ADR-0040). Their gates are
not repeated here.

Scope (US-6 part 2):

- BR-TD-17 on feat-048's multi-project work item views (team, department,
  organization level) and their counts: exactly the visible projects' items,
  filtered before paging, so no total, page or «ещё» reveals a hidden one.
- The sprint clauses owed by feat-041 to mod-007 (spec OQ-7 B; stories US-2,
  US-3, US-5, US-9, US-10 marked "owed by feat-041"): the removal refusal,
  the close and its counts, the sprint list counts, a Closed sprint's outcome,
  participants and dynamics.
- feat-041's debt R-5 remainder (feat-041 review.md, api-contract § 1 item 4
  and § 5): the item ids of a team backlog reorder from hidden projects, and
  the body ids of other routes, answering as ids that name nothing.
- One set form of the sight rule: feat-041's evaluator / `AccessSet`; no
  second implementation.

Out of scope: the structure lists, selectors and pickers (feat-061); the
global search reads (feat-071, feat-072 apply sight in their own reads); the
team sprint board and a project's «Спринты» archive (feat-057, feat-058 are
not merged and run their own clauses, mod-007 spec § Stories).

Stages: backend (plan-backend.md, code, tests, api-contract.md) → backend QA
→ frontend only if a screen changes → code review (one Opus pass with the
security pass) → gate-3 → PR on GitHub after feat-041 (PR #203) is in main,
merge commit, auto-merge asked for, branch kept.

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-17 [orchestrator] task started on branch feat/062-sight-in-work-views in worktree .claude/worktrees/feat-062-sight-views-cce79e (host DESKTOP-2S17CA7, git rev-parse --show-toplevel and git branch --show-current confirmed), cut from feat/041-project-visibility 9e0c9262 (PR #203 not merged yet). The worktree's stale core.hooksPath override (doc-012 worktree) unset.
- 2026-11-17 [orchestrator] route confirmed by the human in chat: L from stage 4 (the queue line said M; the L threshold "authorization rules change" fires: read narrowing on many surfaces, a missed filter is a leak); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only; security pass in the code review.
- 2026-11-17 [orchestrator] queue line taken by scripts/queue.sh take feat-062, PR #211 with squash auto-merge.
- 2026-11-17 [orchestrator] held at the human's word («feat-062 пусть ждёт») after the queue take; the backend agent was stopped before writing anything.
- 2026-11-18 [orchestrator] hold lifted by the human in chat after feat-041 (PR #203) merged; fresh origin/main merged into the branch (851aee9f). Stage 4 restarted.
- 2026-11-18 [backend] stage 4 done: BR-TD-17 on feat-048 aggregates, the flat list, assignee options and the team backlog/board (incl. linked-projects list); mod-007 sprint reads (list, card, close preview, Closed outcome) narrowed by one VisibleProjects value taken from the feat-041 AccessSet (SeenProjects), applied as a predicate inside each statement, so no statement is added and the pins are unchanged (dynamics 8, participants 8, items 4, list 6, close 17); R-5 remainder: reorder item/anchor of a hidden project = 409 boards.backlog.reorder_stale like an unknown id; body ids closed: team in create/save (404, not 409 not_in_project), sprint in save and close destination of a hidden team (404 iterations.sprint.not_found). Audited with no gap: see api-contract section 6. Deviations: Overview and selector offers left to feat-061; AccessSet.SeesAsync still has no Area scope; no frontend change (no route or shape change); feat-041 and mod-007 statuses not edited (their owed clauses are met here, contract in api-contract.md). Tests: Api.IntegrationTests Boards+Iterations 457 and WorkItems+Organizations 2411 green, Infrastructure statement-count subset 34 green, Application.Tests 905 green; verify.sh not run (economy mode).
- 2026-11-18 [orchestrator] the human decided feat-057 delivers the sight clauses on its own team sprint board (relayed by the feat-057 session); already out of this scope. Cross-reference notes added to feat-041 and mod-007 status.md.
- 2026-11-18 [backend QA] pass: 12 sight tests green (3 added: sprint range, selector ids, grant/linked-team sight); 0 blocker/major, 1 minor (D-1 fail-open defaults) carried to gate-3; no frontend change needed; stage 6-frontend (skip). See qa.md.
- 2026-11-18 [orchestrator] stages 6-7 skipped: no route, shape, status or copy changed (backend QA confirmed); stage: 8-review.
