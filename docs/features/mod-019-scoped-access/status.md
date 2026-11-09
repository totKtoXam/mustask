# mod-019-scoped-access

- type: module
- size: L
- branch: mod/019-scoped-access
- stage: 05-frontend
- epic: core/org-access
- deps: mod-018,feat-043
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-11-06, human in chat, one question each: OQ-1 A, OQ-2 reviewer variant, OQ-3 B, OQ-4 B, OQ-5 A, OQ-6 reviewer A, OQ-7 A, OQ-8 A, OQ-9 B, R-27 shown in the switch-off dialog; after review round 3, approved-by-ai, requested by the human)
gate-2: approved (2026-11-09, human in chat, after D-1..D-13 A, OQ-D1 A, OQ-10 role rename and the prototype screenshots)
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
- 2026-11-06 [orchestrator] gate-1 open questions answered by the human in
  chat, one question each: OQ-1 A (department-held area grants only); OQ-2
  the reviewer's variant (control on project nodes and sub-areas, a
  department's value stored but not shown, no migration); OQ-3 B (a project
  «Управление» holder manages the project's sub-areas now, amending the
  ADR-0029 matrix); OQ-4 B (a department manager creates projects in the
  department with area.write); OQ-5 A (one L); OQ-6 the reviewer's A (count of
  grant holders who stop being reached, plus a self-lockout warning); OQ-7 A
  (the bundle contract carries the instance table and replaces the
  tokenClaims.ts mirror). gate-1 itself waits for the fix round on R-1..R-21
- 2026-11-06 [business-analyst] fix round for review round 1: R-1..R-21 all fixed, none rejected (Author's response in review.md). OQ-1..OQ-7 marked answered in spec.md; stories US-7 (links under grants) and US-8 (managing the tree) added; new OQ-8 for the human (what a department manager holds on a project it creates); OQ-3 B / OQ-4 B need a new ADR amending ADR-0029 § 4 (BR-SA-16, -17)
- 2026-11-06 [business-analyst] OQ-8 answered A by the human (department «Управление» holds project.write/.link/.grant on the projects it reaches): spec BR-SA-16, Roles, § Breaks, OQ-8 updated; scenarios added in US-2, US-3, US-5, US-8. The new ADR amending ADR-0029 § 4 must record it (a widening of existing grants)
- 2026-11-06 [requirements-reviewer] round 2 (delta) in review.md: verdict needs-human-decision. Closed: R-1..R-9, R-11..R-18, R-20, R-21. Reopened: R-10 (major, the matrix omits project roles on area routes under OQ-3 B) and R-19 (minor). New: R-22 major (`POST /projects` has no valid route code or answer order under BR-SA-17), R-23 major (the deferred ADR also touches §§ 6, 7, 12; BR-SA-5 lacks the project-grant reach; write it before code), R-24 major (self-grant escalation under OQ-8 A, so OQ-9 goes to the human), R-25 and R-26 minor. No blocker. Second and last iteration; gate-1 untouched
- 2026-11-06 [requirements-reviewer] round 3 (past the two-iteration limit, by the human's request in chat) in review.md: verdict approved-by-ai, ready for gate-1. R-10, R-19, R-22..R-26 closed. New minors: R-27 accepted-risk, needs the human's acknowledgement at gate-1 (two managers granting each other project «Управление» survive a switch-off); R-28 (409 root_is_fixed, not 403, in US-8) and R-29 (the admin's type_not_allowed for a non-department place) fixed by small reviewer edits in spec.md BR-SA-17/-19, US-2, US-5 and US-8. gate-1 untouched
- 2026-11-06 [business-analyst] round-2 fix: R-10, R-19 (reopened) and R-22..R-26 fixed, none rejected (Author's response, round 2, in review.md). OQ-9 B recorded as BR-SA-19 with a US-1 scenario. POST /projects keeps its path (inventory exception, place in the body, step 0). The ADR amending ADR-0029 §§ 4, 6, 7, 12 and the delivery bullet is `proposed` in plan-backend before code. Ready for gate-1
- 2026-11-06 [business-analyst] R-27 closed by the human's decision (make it visible): the switch-off confirmation also lists the project grants on the project given by the users who stop being reached, list only; BR-SA-19, US-2, review.md updated. Gates untouched
- 2026-11-06 [orchestrator] gate-1 approved by the human in chat after a
  third review round the human asked for (past the two-round limit by the
  human's decision); R-27 closed by listing the reached users' project grants in
  the switch-off confirmation; stage → 03-ux-design
- 2026-11-06 [ux-designer] design.md (about 28 KB) and prototype/index.html written; D-1..D-13 for gate-2, new open questions OQ-D1 (switch-off list on a sub-area) and OQ-D2 (per-row link flag, no matrix). Gates untouched
- 2026-11-06 [orchestrator] gate-2 decisions answered by the human in chat,
  one question each: D-1..D-13 all A; OQ-D1 A (the sub-area switch-off dialog
  lists the owning project's grants given by those who stop being reached);
  OQ-D2 (per-row canUnlink, filtered link pickers) goes to plan-backend's
  contract. The prototype was clicked through by the orchestrator (create
  project, row menu, change role, revoke; no JS errors). gate-2 itself waits
  for the human's own look at the prototype
- 2026-11-06 [task-executor] scoped roles renamed in spec.md (new OQ-10, Roles, Breaks), stories, design.md and the prototype by the human's decision in chat: department and project «Чтение»/«Изменение»/«Управление» → «Читатель»/«Участник»/«Менеджер»; team «Планирование»/«Управление» → «Участник»/«Менеджер» plus a new lowest team «Читатель» (lists the person only). Names only, rights unchanged; review.md left as the history of the rounds.
- 2026-11-09 [orchestrator] gate-2 approved by the human in chat; stage → 04-backend.
  Held before stage 4 by the doc-012 orchestrator until the v0.2.0 deploy is
  done; plan-backend owes the proposed ADR amending ADR-0029, OQ-D2's contract
  and the error codes
- 2026-11-09 [backend-developer] stage 4 first pass: plan-backend.md, api-contract.md (grants, candidates, role-bundle contract), ADR-0038 (proposed, for gate-3) written; code done for the grant tables, the scoped check (resource scope on the requirement), the five grant slices on three scopes, four error codes and contracts/access/role-bundles.json. Deferred to the next backend pass: the switch and its impact read, scoped policy on the remaining routes, links under grants, create-by-place, served codes, tree ids. Gates untouched
- 2026-11-09 [backend-developer] stage 4 second pass: the inherits-access switch and impact read, scoped policy on the remaining routes (boards.projectbacklog.reorder on the project backlog), canUnlink and linkableFor, create-by-place, served codes and writable-ids; ADR renumbered 0038. Tests: Domain 922, Application 857, Architecture 82, Contracts 232, Shared 53, Infrastructure 466, Api.IntegrationTests 3338 (3 skipped), 0 failed. Deferred: per-card flag on a shared board; stage → 05-frontend. Gates untouched
- 2026-11-09 [qa-engineer] backend QA pass 1 in qa.md: 0 blockers, 1 major (D-1: project and team routes ignore grants, 403 for project/department/team managers on rename, link, default team; inventory silent), 4 minor (D-2 team delete keeps grants, D-3 assignee picker org-only, D-4 no per-card flag, D-5 unexercised concurrency); 24 tests added (17 pass, 7 skipped with defect ids). Deferrals (a) and (b) judged acceptable for the server. E2E: standing answer NO. Gates and stage untouched

## Notes

Summary (from the queue line): Area, project and team grants on top of organization membership; inheritance through the area tree, a project's grants reaching it through its node's ancestors, there being no area–project links; «Наследовать доступ»'s control on the node's page, its meaning per node type, its rules of who switches it and doc-012 US-1's switch clauses (mod-018 stores the switch, on, with no control: its OQ-2 B, OQ-9); the effective-permissions policy on every route, served to the panel per object, which shows an object page's sections by it; boards.projectbacklog.reorder; the inventory test per scope and the bundle contract. The grants are sections of the objects' own pages: «Участники и доступ» of a project and of a team (beside the team's members), «Участники» of an area (doc-012 BR-MX-20, -22, -23, -26; US-5, US-10; ADR-0029) · epic:core/org-access · size:L · deps:mod-018,feat-043 · note:doc-012 decomposition line 7; after feat-043 by doc-013, 2026-10-18; changes the authorization rules (mod-016 api-contract §§ 0.2, 1.1; mod-006 BR-M4-27); answers doc-011's scoped-permission-overrides; takes over tech-026 OP-7; gains the inheritance switch's control and rules from mod-018 (its OQ-2 B, the human at gate-1, 2026-10-18); takes over mod-018's m-2, m-3 / DEF-4 and TC-30; reworded after mod-018 (typed tree, ADR-0031), 2026-10-30
