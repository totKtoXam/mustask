# doc-004-iterations-planning

- type: docs
- size: L
- branch: doc/004-iterations-planning
- stage: complete
- epic: product/analysis
- deps: doc-001
- gates: gate-1, gate-3

## Gates

gate-1: approved (confirmed by the human in chat, 2026-09-24)
gate-3: approved (confirmed by the human in chat, 2026-09-24, "Принять, squash")

## Plan

1. Input (read-only, from the merged doc-001): `docs/features/doc-001-domain-map/spec.md`
   (module M5 boundary, MVP and release tables, ER model, BR-7/BR-9,
   decomposition row (a) for doc-004), `status.md` § Decisions D-1..D-12
   (binding; D-11 defines the current iteration), `stories/US-9-plan-by-iteration.md`,
   `research/ado-boards.md` (§ Team settings, § Backlogs & sprints, § Other product
   capabilities) and `screenshots/` 03, 05, 10, 11, 12, 17, 24.
2. Stage 1 (business-analyst, deep tier — L): `spec.md` = context, roles, BPMN of
   iteration planning, the M5 logical data model in depth (ITERATION, team
   selection, team default, the work item pointer), business rules, operation
   completeness, MVP cut vs release 2/3 ground (sprint taskboard, capacity,
   burndown/velocity, forecast, delivery plans); stories for the MVP behaviour;
   decomposition table refining `mod-iterations` (already queued as mod-007) and
   naming the release-2/3 feature slices without numbers.
3. Stage 2 (requirements-reviewer, clean context) → gate-1.
4. Stages 3–8 skipped (docs task: no screens, no code); gate-3 → local squash
   via `scripts/finish-task.sh` (no remote configured).

## Decisions

- Scope is exactly the queue line and doc-001's decomposition row (a): the MVP part
  is what module M5 owns (BR-7, BR-9, D-11); sprint taskboard, capacity,
  burndown/velocity, forecast and delivery plans are analysed as release-2/3 ground,
  not as MVP scope (D-3).
- D-1..D-12 of doc-001 are binding and are not reopened here; a finding that needs
  one of them changed is an escalation to the human, not a spec edit.
- This task accepts no ADR (an analysis task writes none, doc-001 spec § Decomposition);
  decisions that outlive the task are named for `mod-007-iterations` to take.

- Human decisions of 2026-09-24 on the round-1 review questions (review.md § Questions), binding for the rework:
  - E-1 (R-8) "Today" for the current iteration is the server's date in UTC.
  - E-2 (R-1) «Текущая итерация» exists only on team screens (board, backlog); the project-wide work item list filters by a specific iteration, no "current" macro.
  - E-3 (R-2) No reactivation of a removed iteration in MVP; soft removal is a storage property (doc-001 Cancel/reverse = n/a, D-5).
  - E-4 (R-4) The iteration filter on the M3 list and M4 board/backlog is a new task `feat-NNN-iteration-filter` (M, deps mod-007, mod-006, feat-003); mod-007 stays the pure module founder.
  - E-5 (R-10) Several teams per project already in MVP — amends doc-001's "one default team per project" MVP cut (M1, mod-002-organizations-projects-teams).
  - E-6 (Q2) The backlog planning pane (drag a work item onto an iteration) is in MVP — adds an M4 dependency to the MVP planning scope.
  - E-7 (Q3) Iterations are a tree already in MVP (e.g. Release > Sprint) — amends doc-001 US-9 "flat list, no tree/hierarchy".
  - E-8 (Q5) Estimation fields (Story Points, Remaining Work) are owned by doc-002 (M3 fields); doc-004 only records the dependency.
  - E-9 (Q6) MVP has a manual "move the unfinished rest to another iteration" action.
  - E-10 (R-3) Human answer verbatim: "автонумерация спринта в одном проекте" — sprints are auto-numbered within a project; orchestrator's reading: numbers are issued per project and never reused, which removes the name collision under soft removal; whether the name stays editable is left for the BA to surface as an open question if it matters.
  - E-11 (R-17, 2026-09-24) A separate counter for sprints: sprint nodes are auto-numbered within the project (never reused), release nodes are not in that sequence — this introduces a node kind (release / sprint) on the iteration; the human chose it knowingly over the recommended "number only unnamed nodes".
  - E-12 (2026-09-24) An iteration's name is editable (empty name → «Спринт N» for a sprint); the number stays fixed.
  - E-13 (2026-09-24, after gate-1) Conflict with the since-merged doc-006 BR-M1-9 (one team per project in MVP): the human keeps E-5; doc-009-teams-per-project-carry-back carries it into doc-006, and mod-002 waits on it.
- E-5, E-6, E-7 and E-9 widen doc-001's MVP boundary by the human's decision; the queue lines they touch (mod-002, mod-006, mod-007) are amended by the orchestrator after gate-1, not in this branch.

## Log

- 2026-09-24 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh
- 2026-09-24 [orchestrator] stages 3–8 skipped (docs task); gate-3 requested
- 2026-09-24 [orchestrator] queue in main: added doc-009-teams-per-project-carry-back, feat-008-iteration-filter, feat-009-iteration-planning-pane, feat-010-move-unfinished-work, mod-011-sprint-views, feat-011-sprint-capacity, mod-012-iteration-metrics, feat-012-backlog-forecast, mod-013-delivery-plans (all Later); amended mod-002 (several teams, +dep doc-009), mod-006 (every team), mod-007 (tree, numbered sprints, UTC) in 486295a; gap: doc-002 merged without an estimation-fields slice (E-8), noted on mod-011/mod-012
- 2026-09-24 [orchestrator] conflict found: E-5 vs merged doc-006 BR-M1-9; the human kept E-5 (E-13)
- 2026-09-24 [orchestrator] gate-1: approved by the human in chat (question form, accepting the immutable-kind cost)
- 2026-09-24 [orchestrator] gate-1 requested from the human
- 2026-09-24 [requirements-reviewer] delta check E-11/E-12 in review.md: approved-by-ai — 2 minors fixed in place (R-26 BR-1 kind scope, R-27 blank rename of a release), derived rules sound; accepted cost to surface at gate-1: a wrong kind is fixed only by remove+recreate; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] narrow reviewer check of the E-11/E-12 delta delegated before gate-1
- 2026-09-24 [business-analyst] E-11/E-12 applied: iteration kind «Релиз»/«Спринт» set at creation and immutable, only sprints numbered per project, releases require a name, no placement constraint, sprint name editable (empty → «Спринт N»); spec.md 250, US-1 149, US-4 101, US-14 83 lines; R-17 resolved; no open questions; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] human answered the 2 round-2 questions: E-11 separate sprint counter (node kind release/sprint), E-12 editable name; targeted BA rework delegated, then a narrow reviewer check before gate-1
- 2026-09-24 [orchestrator] round limit reached; 2 human-only questions (R-17 which nodes take a number, the editable name) put to the human before gate-1
- 2026-09-24 [requirements-reviewer] round 2 in review.md: needs-human-decision — R-1..R-10 verified resolved, R-11..R-16 survived, E-1..E-10 applied verbatim, the BA's 7 unasked decisions judged sound derivations; 0 blockers, 1 major (R-17 numbering of release nodes, human), 8 minors fixed in place (R-18..R-25); mod-007 still one L at the upper edge; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] round 2 review delegated to requirements-reviewer (deep tier, clean context): delta over round 1 + E-1..E-10 + the BA's own unasked decisions
- 2026-09-24 [business-analyst] rework: spec.md 247 lines, US-1..US-10 revised, US-11 planning pane, US-12 move unfinished work, US-13 filter by iteration, US-14 remove iteration added; R-1..R-10 resolved in review.md; § Amendments lists superseded doc-001 statements and proposed queue-line wording for mod-002/mod-006/mod-007; 3 new MVP tasks (iteration-filter, iteration-planning-pane, move-unfinished-work); mod-007 stays one L; 1 open question (is the iteration name editable); verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] stage 1 rework delegated to business-analyst (deep tier) with E-1..E-10
- 2026-09-24 [orchestrator] human answered all 10 round-1 questions one by one (question forms): E-1..E-10 recorded in § Decisions; 1–4, 8 as recommended; 5, 6, 7, 9 widen the MVP; 10 answered in free text
- 2026-09-22 [orchestrator] decisions requested from the human before round 2 (clock for "today", team context of «Текущая итерация» on the project list, reactivation of removed iterations, home of the iteration filter, teams per project in MVP, planning pane, flat iterations, estimation fields owner, end-of-iteration move, name uniqueness)
- 2026-09-22 [requirements-reviewer] round 1 in review.md: needs-human-decision — 3 blockers (R-1 team context of the current-iteration filter on the project list, R-2 reactivation of a removed iteration, R-3 name uniqueness under soft removal), 7 majors (R-4..R-10), 6 minors fixed in place (R-11..R-16); 10 human-only questions collected for gate-1
- 2026-09-22 [orchestrator] stage 2 delegated to requirements-reviewer (deep tier, clean context)
- 2026-09-22 [business-analyst] spec.md (237 lines: context, roles, BPMN, M5 model in depth, BR-1..9, operation completeness, MVP vs release-2/3 cut, decomposition refining mod-007 and naming release-2/3 slices) + stories US-1..US-4 (MVP) and US-5..US-10 (release 2/3), all `shippable: no`; 6 open questions for the human; artifacts written in the session worktree (subagent pinned there) and moved to the task branch by the orchestrator; `bash scripts/verify.sh` → RESULT: green
- 2026-09-22 [orchestrator] route confirmed by the human (in chat, question form): docs, size L — raised from the queue's M by the human's choice (checklist: block 0 not F, block 1 clean, 2.1 fails → M; doubt resolved upward); stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable (no design stage); block 3: 3.1 no regulatory basis, 3.2 no migration, 3.3 "the queue line is the scope"
- 2026-09-22 [orchestrator] queue line removed from main (commit 77f6485 "Take doc-004-iterations-planning"); deps doc-001 verified merged (b5fde80)
- 2026-09-22 [new-task] size mismatch: queue has size:M, human confirmed L
- 2026-09-22 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-004-iterations-planning

## Notes

Summary (from the queue line): Iterations and planning module analysis: sprints, capacity, taskboard, burndown and velocity, forecast, delivery plans
