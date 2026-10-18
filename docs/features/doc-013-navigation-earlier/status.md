# doc-013-navigation-earlier

- type: docs
- size: M
- branch: doc/013-navigation-earlier
- stage: 02-requirements-review
- epic: product/analysis
- deps: -
- gates: gate-1, gate-3

## Gates

gate-1: pending
gate-2: n/a (docs route: stages 1-2 only, no screens, no design)
gate-3: pending

## Plan

Route M docs on the light route: business analysis (a shortened M spec plus the
edits to doc-012, its stories and the ADR consequences) -> one requirements-review
round -> one fix round -> gate-1 (the human approves the new order) -> MR
(gate-3 = the human's merge). No design, no development. After gate-1 the
orchestrator rewords the queue lines in a separate queue commit to main.

## Decisions

- Route M docs, light route, confirmed by the human in chat on 2026-10-18
  («⭐ M, после mod-018»).
- The human, 2026-10-18: the current organization/project/team switching is the
  worst pain point («Текущее переключение организация/проект/команда -
  максимально неудобное»), and first kept the old order, then reversed it:
  «Давай всё таки 043 раньше». The doc-012 chain becomes feat-040 -> mod-018 ->
  feat-043 -> mod-019 -> feat-041 -> feat-042 -> mod-007 -> feat-013; the
  parallel lane mod-005 -> feat-003 is untouched.
- E2E: n/a (docs).

## Log

- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-013-navigation-earlier
- 2026-10-18 [orchestrator] route M docs (queue size), light route, confirmed by the human in chat («⭐ M, после mod-018»); queue line taken in its own sync PR (Sync main: take doc-013).
- 2026-10-18 [business-analyst] bound to the checkout C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-028-org-level-teams on branch doc/013-navigation-earlier (git branch --show-current, git rev-parse --show-toplevel), not the worktree named above.
- 2026-10-18 [business-analyst] spec.md + the doc-012 / stories / ADR edits (5535923c); OQ-1..OQ-6 left for the human at gate-1, each going on with the recommendation.
- 2026-10-18 [requirements-reviewer] the one round of the light route: changes-requested, 3 major and 7 minor, no critical (83d024f4).

## Notes

Summary (from the queue line): Amend doc-012's decomposition so feat-043-area-project-team-navigation runs right after mod-018-area-workspace (deps feat-040, mod-018) instead of after feat-013: the order paragraph and feat-043's Task cell; the interim entries of the lines after it (mod-019, feat-041, feat-042, mod-007, feat-013, feat-045), which put their sections on the objects' own pages instead; the stories' owed-by clauses; the ADR-0028/0029 consequences that name the order; then the queue lines are reworded by an orchestrator queue commit. Note: the human, 2026-10-18: the current organization/project/team switching is the worst pain point; route «⭐ M, после mod-018».
