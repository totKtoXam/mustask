# mod-018 — Frontend QA (typed single area tree)

Branch `mod/018-area-workspace`, reviewed at `d892e7d2`. Replaces the report of the first (untyped) build.
QA did not write the code. E2E: not run (standing answer NO).

## Test plan (written from spec.md, US-1..US-6, design.md, before the code)

| # | Scenario | Source |
|---|---|---|
| P1 | Tree: type marks, per-type row menus, no organization row, department create (name only) | US-1, US-2, D-8, D-9 |
| P2 | Sub-area create only under project nodes / sub-areas; parent picker = the project's subtree | US-2 |
| P3 | Move per type (project: organization or department; sub-area: its project's subtree) | US-2 |
| P4 | Node pages per type; deactivated project's node is read-only (no commands, no team link/unlink, no «Открыть проект») | US-2, US-3, design § 4 |
| P5 | «Расположение» on «Общие», «Новый проект», «Новая команда»; department-only team links | US-3, D-10 |
| P6 | Department deletion lists the team links it ends | US-2 |
| P7 | Refusals by code (name_taken, type_not_allowed, depth_exceeded, outside_project, link_type_not_allowed, not_empty, root_is_fixed, project/team/area not_found, 403) | design § 10 |
| P8 | Work item «Область»: offer = project subtree, project-relative path; creation dialog line | US-4, design § 8 |
| P9 | Regression: TreeRegistry kit change, boards/backlog create dialogs, other slices | — |
| P10 | Contract: routes, bodies, types against api-contract.md and the backend | api-contract |

## Lenses run

Four read-only lenses (spec, contract, states, tests), then verified by the QA engineer against the code.

## Defects

| Id | Sev | Finding | Status |
|---|---|---|---|
| DEF-1 | major | Deactivated project's node still drew «Добавить команду» and the unlink menu (`ProjectNodeTeams`) and «Открыть проект» | fixed: `active` prop, link hidden; test red without the fix, green with it (`AreaPage.test.tsx`) |
| DEF-2 | major | Boards MSW fake had no `type` on the area tree: 8 boards tests red (BacklogPage.create x4, createProject x3, BoardPage.create x1) | fixed in `boards/testApi.ts` |
| DEF-3 | major | Department page «Проекты»: a deactivated project had no «деактивирован» tag | fixed (tree flag, test added). The «Ключ» column stays open, see DEF-9 |
| DEF-4 | major | Item «Область» picker and the creation line used the organization path («Логистика / Alpha / Платформа»); design § 8 / BR-AW-13 want the project-relative path | fixed: `workitems/lib/areaPaths.ts` + unit test, used in `AreaField` and `CreateWorkItemDialog` |
| DEF-5 | minor | Project «Области» page showed a «Тип» column (design § 7: none) | fixed |
| DEF-6 | minor | prettier failed on `WorkItemFormPage.readOnly.test.tsx` | fixed |
| DEF-7 | minor | eslint warning react-refresh in `AreaTypeMark.tsx:36` (non-component export beside the component) | open, warning only |
| DEF-8 | minor | Coverage gaps: refusal chains in the UI (move dialog name_taken / depth_exceeded / type_not_allowed, rename name_taken, project create name_taken, project.not_found on a write, 403 on area dialogs, team.not_found on link), typing into move/parent pickers, `QA-FE-T5` item-area-of-another-project test only covers «area vanished» | open, debt; the pure mapping (`refusals.test.ts`) is covered |
| DEF-9 | minor | Design § 4 facts/columns not met: department page «Проекты» has no «Ключ» column (AreaPage children carry no key — contract); project node «Расположение» and «Общие» «Расположение» are text, not a Link to the department; no «Организация» location fact on a department; «Название меняется вместе с проектом.» never shown; focus after a tree row delete goes to the parent/none, not the next row; loading state has no section skeletons; invented ◆ copy for the team department picker not recorded in design | partly fixed in the code-review round (2026-10-29): the «Ключ» column on a department's «Проекты» (`AreaPage.children[].projectKey`) and the tag from `projectActive`, tested. Still open: the key in the «Проект» fact («Alpha (ALP)»; the tree node carries no key), «Расположение» as a Link, no «Организация» fact on a department, «Название меняется вместе с проектом.», focus after a row delete, section skeletons, the ◆ picker copy |
| DEF-10 | minor | Area writes do not invalidate work-item caches (an open card may show an old path until refetch) | open, not verified, low |
| DEF-11 | major | `WorkItemsPage.keyword.test.tsx` "a word of a title finds the items" failed alone and in the full run; it passes on the base `f106932a`. Cause: the branch added the «Область» column, and the first filter round in jsdom now outruns the default 1 s `waitFor` | fixed: 8 s timeout on that helper (the test, not the code). `WorkItemFormPage.readOnly` failed once under full-run load and passes alone and in the slice run: load flake, not reproduced |
| DEF-13 | major | Review TC-4: a deactivated project's node read `GET /projects/{id}/teams` (404): «Команды» showed «Проект не найден.» with «Повторить»; a sub-area's «Проект» fact stayed «…» (it read the project) | fixed: `ProjectNodeTeams` does not read and does not draw the section when the project is inactive (design delta for status.md: design § 4 says the lists stay readable, nothing to read); the fact takes its name from the tree node. Tests in `AreaPage.test.tsx` (404 served on the teams read; sub-area fact) |
| DEF-14 | minor | Review TC-5: department-end unlink also asked `teamLink` | fixed: gated on `areaLink` alone (api-contract § 5 `DELETE /areas/{a}/teams/{t}`); comments fixed in `AreaPage` and `TeamDepartmentsSection`. No dedicated test: no fixture role holds `area.link` without `team.link` |
| DEF-15 | minor | Review TC-23: deleting a node refetched its own page («Область не найдена» flash) | fixed: `useDeleteArea` removes the node's `area` and `areaTeams` queries before invalidating |
| DEF-12 | minor | The older `qa-frontend.md` was stale | fixed by this rewrite |

Dropped: none of the lens findings was refuted. `api-contract` § 6 says 409 `outside_project` for a work item's area; the backend answers 404 `area.not_found` and the frontend follows the code (not a frontend defect; backend QA D-1).

## Backend / contract items for the orchestrator

- **BLOCKER (backend)** `WorkItemReadModel.AreaPathsAsync` (`Mustask.Infrastructure/WorkItems/WorkItemReadModel.cs:350`) builds `AreaTree` from `Areas.Where(ProjectId == projectId)`. A project node under a department has `ParentId` = the department, which is not in the set, so `AreaTree`'s constructor throws "parent ... is not in the tree": the work-item list (:134) and card (:292) fail for every project placed under a department. Confirmed by reading; no integration test places a project under a department. Fix: root the tree at the project's node (treat the node as a root) and add a WorkItems integration test.
- api-contract § 6 / § 4: 409 `outside_project` vs the code's 404 `area.not_found`; the subtree read's `path` is the full organization path (the frontend now strips the part above the project).
- Contract: department «Проекты» rows need the project key (and `projectActive`) for design § 4's «Ключ» and tag; the tag is taken from the tree meanwhile.

## Results

- `tsc -b` clean; eslint: 0 errors, 1 warning (DEF-7); prettier clean on every changed file (20 other files under `src` already fail `prettier --check`, untouched by this branch).
- Vitest: full `vitest run src` after the fixes: 5121 passed, 6 skipped, 2 failed (DEF-11, both fixed or flake as above); the re-run of the keyword and readOnly files with `boards` and `organizations`: 109 files, 1555 tests, all passed. Added 5 tests (AreaPage x2, areaPaths x3).
- E2E: skipped, standing answer NO.
