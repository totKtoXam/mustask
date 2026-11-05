# mod-019-scoped-access

- type: module
- size: L
- branch: mod/019-scoped-access
- stage: 02-requirements-review
- epic: core/org-access
- deps: mod-018,feat-043
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

Route L, the full pipeline: business analysis → requirements review → gate-1
→ UX design → gate-2 → backend (plan, code, QA) → frontend (plan, code, QA) →
code review (one Opus pass) → gate-3 → squash PR to main with Auto-fix.
Economy mode: one agent per stage, no build fan-out. E2E: no (paused).
Plans: `plan-backend.md`, `plan-frontend.md`.

## Log

- 2026-11-06 [orchestrator] task started in the app-made worktree
  .claude/worktrees/workflow-debts-c5f842, its branch renamed to
  mod/019-scoped-access (handoff.md: task not started yet, the session creates
  the branch itself); base origin/main 72e630a1
- 2026-11-06 [orchestrator] route confirmed by the human in chat: L, the full
  route, all three gates the human's, no E2E. Checklist: block 1 fired on the
  authorization-rules threshold and on the breaking change of the access
  semantics of mod-016 api-contract §§ 0.2, 1.1; migrations are additive in
  the module's own tables (3.2 not asked: L already fired). Size matches the
  queue line's size:L
- 2026-11-06 [orchestrator] queue line taken: scripts/queue.sh take mod-019,
  sync PR https://github.com/totKtoXam/mustask/pull/88 (auto-merge squash)
- 2026-11-06 [orchestrator] session bound: host DESKTOP-2S17CA7, toplevel
  .claude/worktrees/workflow-debts-c5f842, branch mod/019-scoped-access
- 2026-11-06 [business-analyst] spec.md + US-1..US-6 written; stage → 02-requirements-review. Open for gate-1: OQ-1..OQ-6 (department-only grants, the switch per type, project managers and sub-areas, department managers and project creation, one L or cut lines, confirmation on switch off)
- 2026-11-06 [requirements-reviewer] round 1 in review.md: verdict needs-human-decision. 3 blocker (R-1 «Участник» fixture voids the 403 outcomes, R-2 switch order in US-2, R-3 child areas under a department impossible), 8 major (R-4..R-11), 10 minor (R-12..R-21); back to the business analyst for R-1..R-11. Opinions on OQ-1..OQ-6; new OQ-7 (instance role table in the bundle contract). gate-1 untouched

## Notes

Summary (from the queue line): Area, project and team grants on top of organization membership; inheritance through the area tree, a project's grants reaching it through its node's ancestors, there being no area–project links; «Наследовать доступ»'s control on the node's page, its meaning per node type, its rules of who switches it and doc-012 US-1's switch clauses (mod-018 stores the switch, on, with no control: its OQ-2 B, OQ-9); the effective-permissions policy on every route, served to the panel per object, which shows an object page's sections by it; boards.projectbacklog.reorder; the inventory test per scope and the bundle contract. The grants are sections of the objects' own pages: «Участники и доступ» of a project and of a team (beside the team's members), «Участники» of an area (doc-012 BR-MX-20, -22, -23, -26; US-5, US-10; ADR-0029) · epic:core/org-access · size:L · deps:mod-018,feat-043 · note:doc-012 decomposition line 7; after feat-043 by doc-013, 2026-10-18; changes the authorization rules (mod-016 api-contract §§ 0.2, 1.1; mod-006 BR-M4-27); answers doc-011's scoped-permission-overrides; takes over tech-026 OP-7; gains the inheritance switch's control and rules from mod-018 (its OQ-2 B, the human at gate-1, 2026-10-18); takes over mod-018's m-2, m-3 / DEF-4 and TC-30; reworded after mod-018 (typed tree, ADR-0031), 2026-10-30
