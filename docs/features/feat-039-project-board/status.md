# feat-039-project-board

- type: feature
- size: M
- branch: feat/039-project-board
- stage: complete
- epic: core/backlogs-boards
- deps: mod-006
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

Route M with UI: business analysis → requirements review → UX design → gate-1 +
gate-2 as one package → backend → frontend → one QA pass over both layers →
one code-review round → gate-3 → PR.

- Backend (8f227204, 75a8dd42; contract 13b58afc): three routes under
  `/api/organizations/{o}/projects/{p}` — `GET backlogs/{level}`,
  `GET boards/{level}` (`workitems.workitem.read`), `POST
  backlogs/{level}/reorder` (`boards.backlog.reorder`); no new code or error
  code. Slices GetProjectBacklog, GetProjectBoard, ReorderProjectBacklog;
  every item of the project at the level, no team or area condition. The
  project's own order in `project_backlog_orders` / `project_backlog_places`
  (additive migration), the reorder serialised by a row lock per (project,
  level). StackRank's creation-time tie-break for unplaced items (team order
  unchanged); tasks under a story by number. Inventory 44 → 47 routes.
- Frontend (0775efbc): `…/projects/:p/backlogs|boards/:level`;
  ProjectBacklogPage / ProjectBoardPage share BacklogTable / ScopedBoard with
  the team pages (`scope.ts`: team | project); «Весь проект» last in the page
  head's team drop-down; the shell per BR-PB-9.
- Tests with the code: endpoint suites, volume (2 s / 1 s), statement counts,
  the migration, QA's ProjectBoardsQaTests; ~100 Vitest tests.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Decided under the human's night delegation (the recommended option), for the
  human at the gate-1 + gate-2 package: spec OQ-1..OQ-6 (A each; OQ-6 = the
  shell on a project page: a team choice moves nothing, a project or
  organization switch keeps the view and level); design D-1..D-5 (A each).
- QA D-2: a team board's failed re-read keeps its columns (feat-032 design
  § 7.1, now in BR-PB-8). Review m-2: feat-027's prose on the old drop-down
  left as merged history. QA D-4 / review m-3: the MessagePack 2.5.303
  `VersionOverride` in `Mustask.AppHost.csproj` (NU1902, GHSA-qhrr-8q5h-9q3h,
  fails main too since 2026-10-15) rides on this branch, named in the PR; the
  fix on main is a queue line.

## Log

- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-039-project-board
- 2026-10-17 [orchestrator] route M (queue size; doc-012 § Decomposition: an additive view in the boards slice) taken under the human's night standing approval (no confirmation needed; gates stay pending for the human, work proceeds and is listed for the morning). E2E: no (paused 2026-10-12).
- 2026-10-17 [orchestrator] the branch stacks on doc/012-matrix-organization-model (a653917f, PR #6 unmerged) so doc-012's spec, stories and ADR-0028/0029 are in the tree; the PR targets that branch until #6 merges. Stages run as agents through workflows («используй агентов и воркфлоу»).
- 2026-10-17 [business-analyst] spec.md + US-1..US-3 (short form) from doc-012 US-7 project part; OQ-1..OQ-5 decided A for the human.
- 2026-10-17 [requirements-reviewer] round 1: changes-requested, R-1 major (the shell on a project page), R-2..R-10 fixed in place; analyst resolved R-1 as BR-PB-9 + OQ-6 (A).
- 2026-10-17 [ux-designer] design.md (c0586a36), D-1..D-5 decided A for the human.
- 2026-10-17 [backend-developer] 13b58afc contract, 75a8dd42 tie-break, 8f227204 project backlog, board, reorder with storage.
- 2026-10-17 [frontend-developer] 0775efbc project pages, the «Весь проект» entry, the shell (BR-PB-9).
- 2026-10-17 [qa-engineer] one pass over both layers (f396ad39) + re-check (c34ecc10): 0 feature blocker/major; D-1 NU1902 (environment) fixed by f8ea04ed; D-2..D-4 minor.
- 2026-10-17 [code-reviewer] round 1 (cebe31b3): CR-1 major (spec not committed), m-1..m-3; fixes 27d3843a, 2261f41b; delta read (37bb3008): approve, m-2..m-4 minor left (m-4 = this status.md, now done).
- 2026-10-17 [orchestrator] `bash scripts/verify.sh` green (fixer's run, exit 0). stage: complete; gates left pending for the human; branch pushed, PR to doc/012-matrix-organization-model.

## Notes

Summary (from the queue line or an argument): A project backlog and board per level over every item of the project, whatever its team or with none, in the project's own order; reached from the page head's team drop-down until the navigation line (doc-012 BR-MX-17, BR-MX-18; US-7, project part)
