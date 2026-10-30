# mod-018-area-workspace

- type: module
- size: L
- branch: mod/018-area-workspace
- stage: 07-mr-complete
- epic: core/org-access
- deps: feat-028
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-10-28, human in chat, one question each: OQ-3..OQ-8, OQ-10..OQ-13 all A; OQ-9 dropped, default A; the typed tree is a new ADR in this PR). History: approved 2026-10-18 for the old spec (OQ-1 A, OQ-2 B), reset 2026-10-28 by the typed-tree rework
gate-2: approved (2026-10-28, human in chat, one question each: D-8 A no organization row, D-9 A «Новый департамент», D-10 A «Департаменты», the D-1..D-7 re-check accepted as written: D-1/D-3/D-7 kept, D-2/D-5 amended, D-4/D-6 retired; history: approved 2026-10-18, D-1..D-7 A)
gate-3: approved (2026-10-29, human in chat, four questions: ADR-0031 accepted, TC-28 read-only teams list, name_taken/404 decisions accepted, minors to Debt). History: approved 2026-10-27 for the OLD spec, reset 2026-10-28 by the typed-tree rework

## Plan

Route L with UI on the light route (the human's choice of 2026-10-17 for the
lines after mod-017): business analysis → requirements review (one round) →
UX design (there are new screens) → gate-1 + gate-2 put to the human as one
package, one question per open question and per design decision → backend
(plan, then code) → frontend → one QA pass over both layers alongside the code
review and the security pass → one fix round → at most one delta read →
gate-3 → PR to main. Plans: `plan-backend.md`, `plan-frontend.md`.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Light route: the human's choice of 2026-10-17, see Plan.
- In scope by the human's answer at feat-028 gate-3 (2026-10-18): the
  ADR-0028 amendment carrying rule 8's clarification (links locked
  `FOR NO KEY UPDATE`, feat-028 plan-backend D-3) and A-9's corollary (a
  locked row's presence is judged by the lock statement's own answer,
  feat-028 review-code DR3-4). Its text is written at the backend stage, not
  in stage 1 (spec § ADR-0028 amendment).
- Interim surfaces for one line (doc-013): the area pages in the organization
  settings and the project's area links in the project's settings;
  `feat-043-area-project-team-navigation` moves them.
- Design (2026-10-18): D-1..D-7 of `design.md` § 14 went to the package, each with a
  recommended option A; the prototype follows the stage file's rule for an L.
- Gate-1 + gate-2 (the human in chat, 2026-10-18, one question each): OQ-1 A,
  OQ-2 B, D-1..D-7 A; spec and design stand as written (both already went on
  with these). OQ-2 B: `mod-019-scoped-access`'s queue line gains «Наследовать
  доступ»'s control, its rules and doc-012 US-1's switch clauses (spec
  § Decomposition), amended by the orchestrator's queue PR.
- Also in the ADR-0028 amendment, by the human's answer at mod-005 gate-3
  (2026-10-18, «⭐ A: В поправку mod-018»): rule 8 states the one lock order
  with the project row and the hierarchy tier in it (mod-005 review-code
  DR2-2 (c)), written today only in mod-005 `plan-backend.md` § 14.3 and
  feat-028 `plan-backend.md` § 7.1: unlink and team deletion take the project
  hierarchy lock before the owned rows (mod-005 QD-1).
- Development waits for feat-040 to merge: both re-lay the card's band
  (feat-040 D-2 puts «Проект» in block A row 2; this design's D-6 moves
  «Команда» up in block B), and both touch the item's read models.

- **Typed single tree (2026-10-27, the human in chat via the orchestrator, all four answers the recommended ⭐):** «Организация (корневая область) → Департамент (область) → Проект (область)», all areas of different types, in one tree per organization. (1) A project has exactly ONE parent (a department or the organization root) and can be moved; NOT many-to-many area–project links. (2) Below a project its sub-areas stay, and the work item's «Область» STAYS, pointing at the project's node or one of its sub-areas; the upgrade keeps existing data (each project's tree hangs under the organization root as a project-type node). (3) Teams stay organization-level (feat-028) and link many-to-many to any node (department or project), as mod-018 does now. (4) Reworked before merge, on this branch.
  - **Overrides in doc-012:** OQ-1 A (retire the item's «Область») and BR-MX-5's area–project many-to-many link (and BR-MX-6, -7, -8, doc-012 US-3, US-10 where they speak of area–project links or of an area owning no project). This spec's own OQ-1 A (drop every per-project area) is superseded; OQ-2 B stands (spec § BR-AW-5, OQ-9).
  - **Later queue lines it touches** (rewording comes later through a sync PR, not now): `feat-043-area-project-team-navigation` (pages of a typed tree, project placement); `mod-019-scoped-access` (grants reach projects through the tree, not area–project links; «Наследовать доступ» per type); `feat-042-area-board-and-overview` (area views read through the subtree, department → project nodes); `feat-044-area-analytics`. Possibly feat-003, mod-008, feat-013 (the item's area stays, so their «no area filter» wording is to be re-checked).
  - Open for the human, spec OQ-3..OQ-13 (OQ-9 dropped: nothing reads «Наследовать доступ» under OQ-2 B; mod-019 decides): department nesting (3); stored organization root (4); project node created and deleted only with the project (5); deactivation of a project's node and the writes on it (6); sub-areas under a department (7); team links to sub-areas and the root (8); the node's name following the project (10); a team's link to a project node versus feat-028's project–team link (11); one name space per level (12); a legacy tree past the depth limit in the upgrade (13).
  - **Gate-1 answers (2026-10-28, the human in chat, AskUserQuestion, one question each):** OQ-3 A (no nesting of departments), OQ-4 A (the organization is not a stored node), OQ-5 A (a project node is created and deleted only with its project), OQ-6 A (deactivation changes nothing; the node is read-only and marked «деактивирован»), OQ-7 A (sub-areas only below a project node), OQ-8 A (teams link to departments, and a project node's teams are the project–team links), OQ-10 A (the node is renamed with its project), OQ-11 A (one link: a project node's «Команды» are feat-028's project–team links), OQ-12 A (one name space per level), OQ-13 A (the upgrade fails naming a tree past the depth limit); OQ-9 dropped (nothing reads «Наследовать доступ» under OQ-2 B; default A).
  - **ADR (2026-10-28, «⭐ Новый ADR в этом PR»):** the typed tree is a NEW ADR in this PR, superseding the affected parts of ADR-0028 rules 2, 3, 7 and 8 and ADR-0029 § 6; written at the backend stage, accepted by the human at gate-3 (spec § ADR-0028 amendment). The two editorial amendments of that section stay editorial.
  - Rework scope for the developer later: the first build implemented the old spec (no per-project tree, item area retired, area–project links, upgrade dropping data); see the spec's § Existing rules and the BA hand-off notes. Plans, api-contract, design and prototype need rework after gates 1+2 are re-approved.
- **Gate-3 answers (2026-10-29, the human in chat, AskUserQuestion, ask-format, one question each, all the recommended ⭐):** (1) ADR-0031 accepted (Status, ADR-0028/0029 markers and the ADR index updated). (2) TC-28: a deactivated project's node shows its «Команды» **read-only**, as design § 4 / OQ-6 A say, not hidden; the section reads the new `GET /projects/{id}/team-links` (api-contract § 4), because `…/projects/{id}/teams` stays 404 for a deactivated project (feat-028). The frontend must call `team-links` for a deactivated project and render the list without link or unlink; the hide commit 62bf7a35 is to be undone for the read. design § 4 is unchanged: behaviour now matches it (no design delta). (3) Both accepted: renaming a project into a name taken on its level is 409 `area.name_taken`; `GET /projects/{id}/areas` for a deactivated project is 404. (4) Gate-3 approved, the open minors go to Debt.

## Log

- 2026-10-30 [orchestrator] Log entries of 2026-10-18..2026-10-29 (task start to the backend QA of the typed tree) rolled into history.md (size budget).

Superseded by the rework (2026-10-28): the gate-3 of 2026-10-27 and the list below belong to the old spec; the list is kept and will be re-checked when the reworked code is reviewed.

- m-2 (→ mod-019): MoveAreaHandler decides `area.create` for the current parent from a read taken before the lock; re-check `area.ParentId is null` after the locks and narrow api-contract § intro's "before any id is looked at". Invisible until mod-019 splits the codes.
- m-3 / DEF-4 (→ mod-019): AreasPage.tsx and AreaPage.tsx gate the create dialog on `areaWrite`; gate it on `areaCreate || areaWrite` when the codes split.
- m-5 (→ mod-007): ADR-0028 *Item save* bullet does not place the hierarchy tier; mod-007 must state the hierarchy lock stands before the sprint (rule 8 steps 5 → 6).

## Notes

Summary (reworked 2026-10-27 to the human's typed single tree): one tree per organization, «Организация → Департамент → Проект», all areas of different types; a project has one node with one parent (a department or the organization) and can be moved; the project's sub-areas and the work item's «Область» stay, pointing at the project's node or a sub-area; teams (organization-level, feat-028) link to nodes many-to-many; description and «Наследовать доступ» (stored, shown from mod-019) on every node; the upgrade keeps each project's tree as a project node; area pages in the organization settings until the navigation line (doc-012 BR-MX-3..-8 as amended; US-1..US-6)
- 2026-10-29 [frontend-developer] frontend rework to the typed single tree done (stage unchanged). plan-frontend.md rewritten. Mismatches: api-contract § 6 says 409 outside_project for an item area, the code answers 404 area.not_found (followed). Tests, tsc, eslint, prettier green.
- 2026-10-29 [qa-engineer] frontend QA of the typed tree done (qa-frontend.md): four lenses verified, 5 frontend defects fixed (team section and «Открыть проект» on a deactivated node, boards fake without `type`, department tag, project-relative area path, «Тип» column), keyword-test timing fixed; full vitest src green after the re-runs, tsc/eslint/prettier clean on changed files. Open for gate-3: minor DEF-7..DEF-10. BLOCKER for the backend developer: `WorkItemReadModel.AreaPathsAsync` throws for a project under a department (work-item list and card 500). E2E: no (standing). stage -> 08-code-review.
- 2026-10-29 [backend-developer] pre-review backend fix. BLOCKER from frontend QA confirmed (500 on the card, the list, create and save of any item of a project whose node sits under a department): `WorkItemReadModel.AreaPathsAsync` built the tree from the project's areas only, and `AreaTree` rejects a node whose parent is absent. Fixed: the project's node is read as the top, so the item's `area.path` is project-relative (design § 8: `Alpha / Платформа`). Test `WorkItemAreaUnderDepartmentEndpointTests` failed 500 first, then green. No other read walks ancestors on a project-only set (boards/backlog read no area tree, `GET /projects/{id}/areas` and the tree use the whole organization tree). Contract changes (api-contract note): (1) § 6 states the item path is project-relative and an item area outside its project is 404 `area.not_found`; `area.outside_project` is only the move of a sub-area (§ 3) and is never returned for an item - the code stays (QA D-1 closed); (2) § 3 a department moved to the top is a 200 no-op, `type_not_allowed` only under another department (QA D-2 closed); (3) § 2 `AreaPage.children[]` gains `projectKey` and `projectActive` (null except on a project node) for the department's «Проекты» «Ключ» column - additive, one join already in the statement; test `DepartmentPage_ProjectChildren_CarryTheKeyAndTheActiveFlag`. The frontend (stage already started) needs to read the two fields; nothing it uses changed or was removed.
- 2026-10-29 [code-reviewer] code + security review of the typed tree (panel of 6 + skeptics, spot-checked): request-changes; open 0 blocker, 13 major (TC-1..TC-13: 4 runtime/test, 9 prose/contract/ADR), 14 minor debts (TC-14..TC-27); old m-3 closed, m-2/m-5 folded into TC-15/TC-10. Findings in review.md; review.md is past its 32 KB budget. Stage stays 08-code-review.
- 2026-10-29 [backend-developer] code-review fix round (backend + docs). TC-1 `Area.MoveUnder` runs the type rules before the cycle rule (a project node under a sub-area, even its own, is 409 `type_not_allowed`); TC-2 `CreateProject` checks `area.write` when `parentAreaId` is present and `area.create` when absent, as step 0 (403, nothing stored); TC-3 the migration logs `mod-018: typed N project nodes, kept M sub-areas` (0 and 0 on an empty database), asserted in the populated and empty tests; TC-5 contract § 5 team-end unlink is `team.link` alone (code unchanged); TC-6..TC-12 and TC-26 prose: ADR-0031 (scope line, rule 1 on what the domain holds, rule 3, rule 4, rule 5 page stays, rule 6 title and one lock order with the conditional hierarchy tier, rule 8, consequences), ADR-0028 (markers «proposed», rule 8 and (c) point to ADR-0031 rule 6, rule 7 settings/areas page stays, delivery row), ADR-0029 (marker, delivery bullet), `adr/README.md`, plan-backend, plan-frontend, api-contract, the mod-002/mod-003 P-10 notes, and the eight owners' superseded 2026-10-27 P-10 notes dropped; TC-13 the cross-organization `areaId` save case is back (404 `area.not_found`, nothing changed); TC-15 contract § 1 order of answers narrowed; TC-16 the permission label; TC-17 the two link POSTs declare 409; TC-20 area locks are `FOR NO KEY UPDATE` except the deletion. Contract changes (api-contract note): (1) § 5 the team-end unlink needs `team.link` alone, as the code and BR-MX-6 say; (2) § 4 and § 1 `POST /projects` second code: `area.write` with a `parentAreaId`, `area.create` without (spec § Roles, US-6; latent until mod-019, only «Администратор» holds `project.write`); (3) § 3 a project node moved under a sub-area is `type_not_allowed` (was `move_into_own_subtree` when the sub-area was its own); (4) § 6 `areaId` is on save only; (5) § 8 `RootAreaId` stays, no `ListAreasOfProjectAsync`; (6) § 1 order of answers. No wire shape changed. Frontend must gate: department-end unlink on `areaLink` alone, team-end unlink on `teamLink` alone, a project's placement field (create form) on `areaWrite` with a department and `areaCreate` at the top.

### Debt after the typed-tree code review (2026-10-29)

- TC-4 and TC-28: closed at gate-3 (2026-10-29): not hidden, read-only list from the new `GET …/projects/{id}/team-links` (Decisions). TC-15, 16, 17, 20, 31 are fixed. Open minors, all accepted as Debt at gate-3: TC-14, TC-18 (second half), TC-19, TC-21, TC-22, TC-23..TC-27 (frontend/test items below), TC-29 (rename lock comment), TC-30 (`CreateProjectDialog` empty place, with mod-019), DEF-7 (eslint react-refresh warning), DEF-8 (UI refusal-chain coverage gaps), DEF-9 (department page «Ключ» column, node «Расположение» as a Link), DEF-10 (area writes do not invalidate work-item caches).
- TC-14: `OrganizationStructure` decides the default team and reachability inline, and `ReachabilityRules.IsReachable` has no production caller; use the rules or drop it (code predates the branch).
- TC-18 (second half): the migration test does not exercise a tree exactly 10 deep kept and its move into a department refused.
- TC-19: EF cannot see the raw-SQL FK on `ak_areas_project_id_id`; recorded in ADR-0031 rule 1 (do not let a generated migration drop the index), no schema test yet.
- TC-21: no negative case for ActiveOrganization, no test of `ProjectOf` or `RemovedState`; `WorkItemSetRules.IsRemoved` is unused.
- TC-22: `OwnedItemsRule` doc says count, lock and write «cannot name different sets» while the SQL restates the predicate; no test that the count equals the rows affected.
- TC-23, TC-24, TC-25 (DEF-9), TC-26 frontend items (`paths.ts` comment, `ProjectAreasPage` stale branch, «Тип» skeleton column): frontend.
- TC-27: BR-AW-10 is pinned in part (describe, moving the deactivated node, the holding department's `not_empty`, the node's name staying taken are not); the Status lines of ADR-0028/0029 get «partially superseded by ADR-0031» at acceptance.
- 2026-10-29 [code-reviewer] delta read of the typed-tree fix round (review.md § Round 2): TC-1..TC-13 closed, approve; open 0 major, 4 minor (TC-28..TC-31) as debt; TC-28 needs the orchestrator to ratify the TC-4 design delta. stage -> gate-3.
- 2026-10-29 [orchestrator] Usage at gate-3: weekly 16%, 5-hour 25%.
- 2026-10-29 [backend-developer] gate-3 recorded (four answers, Decisions) and TC-28 backend: new read `GET /api/organizations/{organizationId}/projects/{projectId}/team-links` (permission `organizations.team.read`) → 200 `[{ id, name }]`, the active teams linked to the project by name, for a project of any status; 404 `organizations.project.not_found` for an unknown or foreign project. Contract note: api-contract § 4 gains the row and § 2/3 the pointer; added after the contract was fixed, additive, the frontend (not yet reading it) must switch `ProjectNodeTeams` to it for `active === false`. Test `ProjectTeamLinks_ListsTheLinkedTeams_ForADeactivatedProjectToo`; Api AreaWorkspace + GetTeams, Application 768, Architecture 86 green.
- 2026-10-29 [orchestrator] the human (relayed 2026-10-29): no local full verify.sh — CI's required full verify.sh on the PR is the gate; push runs only the quick pre-push verify (tech-041).
- 2026-10-30 [orchestrator] TC-28 closed (backend 90722c3c read-only team-links, frontend bf9cee03); main merged; stage → 07-mr-complete; push with the quick pre-push verify, PR with Auto-fix; CI's full verify.sh is the gate. Usage at the end: weekly 0% (the weekly window reset 2026-10-30; 16% of the previous week at gate-3), 5-hour 4%.
