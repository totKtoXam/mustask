# QA — mod-021-teams-in-departments

## Backend

Plan written from doc-016 (BR-TD-1, -4 team part, -5, -19 step 1, -20; US-1, US-2) and `api-contract.md` before the
implementation or the developer's tests were read. E2E: not asked, not run (the human's standing answer is NO, paused).

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|---|---|---|---|
| BR-TD-1 create in a department, required, 400 on `departmentId` with other reasons | `TeamPlacesEndpointTests`, `AddTeamHandlerTests` | integration, unit | covered |
| BR-TD-1 unknown / foreign / deleted department 404, node or sub-area 409 | same + `MoveTeam_Refusals…` (QA) | integration | covered |
| BR-TD-1 deleting a team clears its department | `DeleteTeam_ClearsItsDepartment` (QA); `ck_teams_department_place` | integration | added |
| BR-TD-4 move: unlinked moves, members and colour stay | `Move_UnlinkedTeam…`, `Move_KeepsMembers…` (QA) | integration | covered |
| BR-TD-4 linked 409 `has_links`; same department 200 no-op | `Move_LinkedTeam…` | integration | covered |
| BR-TD-4 refusal codes: deleted/foreign team, foreign/deleted department, sub-area, null body, stranger | `Move_Refusals…`, `Move_KeepsMembers…` (QA) | integration | added |
| BR-TD-4 races: move vs destination deletion, vs link, vs second move, vs team deletion | `AreaRacesEndpointTests`, `MoveTeam_VersusTheDeletionOfTheTeam…` (QA) | integration | covered / added |
| BR-TD-5 `has_teams` before `not_empty`; deleted team never blocks | `DeleteDepartment_WithATeamAndAProject…` (QA), `TeamPlacesEndpointTests` | integration | added |
| BR-TD-5 creation vs department deletion, both orders | `AreaRacesEndpointTests` | integration | covered |
| Retired routes (6) answer 404/405; retired body fields ignored | `RetiredLinkRoutes…`, `RetiredUnlinkRoute…` (QA) | integration | added (the 6th route) |
| BR-TD-19 step 1: one link, most projects (deactivated counts, top-level does not), ties, first link, fallback, reuse, deleted teams | `TeamsInDepartmentsMigrationTests` | integration (populated cast) | covered |
| BR-TD-19 ties by NAME not creation order; `first_link` rule | `Upgrade_TieAndFirstLink_GoByDepartmentName…` (QA) | integration | added (cast ties coincided with creation order; `first_link` was unreached) |
| BR-TD-19 log: placements with rule, created departments, dropped links | `Upgrade_LogsEveryPlacement…` | integration | covered |
| BR-TD-19 all-or-nothing, Down/Up, empty database, idempotent re-run | dev tests | integration | covered |
| BR-TD-20 palette order, none-in-use, max distance, tie, all-used | `ColorPaletteTests` | unit | covered |
| BR-TD-20 scopes: department's own colour counted for teams and projects; kinds apart | `Colour_OfProjectsAndTeams…` (QA) | integration | added |
| BR-TD-20 deleted frees; moved keeps and frees in old scope; all 12 used (14 teams) | `Colour_OfADeletedTeam…`, `…AMovedTeam…`, `Colour_WhenEveryEntryIsInUse…` (QA) | integration | added |
| BR-TD-20 determinism under concurrent creation | `CreateTeam_TwoAtOnce…`, `CreateDepartment_TwoAtOnce…` (QA) | integration (race) | added |
| BR-TD-20 upgrade: SQL rule equals the domain rule, incl. the all-used case; ties by name at one instant; deactivated project not counted | `Upgrade_ColoursAtScale_EqualTheDomainRule…` (QA) | integration | added |
| Upgrade: org with only deleted teams creates nothing | `Upgrade_OfAnOrganizationWithOnlyDeletedTeams…` (QA) | integration | added |

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Two team creations at once in one department | distinct colours (tree lock orders them) | pass |
| 2 | Move against the team's own deletion, both orders | no 5xx; deleted team has no department | pass |
| 3 | Retired body fields (`areaIds` on a team, `teamIds` on an area) | ignored, not honoured | pass |
| 4 | Fallback name held by a top-level non-department node | whole migration fails with a `mod-021` message, schema unchanged | pass (dev test) |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-13 | `dotnet test src/backend/tests/Mustask.Infrastructure.Tests` (full) | 2927 pass, 4 fail: RichText timing tests (`RichTextInputGuardTests.Reduce_EveryElementName_FloodedFlat…("table")`, `ReducerRulesTests` x3), 2 s budgets under load; the branch touches nothing under RichText |
| 2026-11-13 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests` (full) | 3463 pass, 3 skipped, 1 fail: `AggregateViewVolumeTests.EveryGrouping_OfAMillionItemsInTwoHundredProjects_IsReadWithin2s` (see D-1 note) |
| 2026-11-13 | QA additions: `TeamsInDepartmentsMigrationTests` (14), `TeamPlacesQaTests` (13) | all pass on the first run |
| 2026-11-13 | `AggregateViewVolumeTests` alone, on `origin/main` (temp worktree, removed) | fails: `flat list, keyword` 2331 ms over the 2 s budget |
| 2026-11-13 | `AggregateViewVolumeTests` alone, on the branch | passes (1 m 51 s) |
| 2026-11-13 | `Mustask.Infrastructure.Tests --filter RichText` | 9 of 2450 fail, all 2 s timing tests (the filter matches the namespace and runs ~2450 CPU-bound tests in parallel); no file of the branch touches RichText |

### Defects

No blocker and no major found. Two minors, both risks rather than failures:

#### D-1: AggregateViewVolumeTests budget (not caused by this task)

- Severity: minor (environment)
- Steps: run the test alone on `origin/main` on this machine.
- Expected / Actual: within 2 s / `flat list, keyword` 2331 ms on main, in isolation. On the branch it failed only inside the full parallel
  run (every query slower, including ones that touch no department) and passes alone: load, not the branch. The RichText timing failures
  (4 in the full run, 9 in the filtered run) are the same kind of load-bound 2 s budgets in code the branch does not touch.
- Status: not this branch; left to CI.

#### D-2: the migration needs the `ru-x-icu` collation

- Severity: minor (deployment risk)
- Steps: `TeamsInDepartments` orders ties by `name COLLATE "ru-x-icu"`; run it on a PostgreSQL built without ICU or an image without that locale.
- Expected / Actual: the placement by name needs the collation; where it is missing the migration fails as a whole (all or nothing, nothing half-done),
  loud but blocking the deploy. The Testcontainers image and the stand's image have it.
- Status: open, to carry to gate-3 (confirm the production server has ICU collations before the deploy; the MR should say so).

Noted, not defects: `POST /teams` still accepts `projectIds` and can link a new team to a project of another department (the link rule
`organizations.team.other_department` is mod-022, BR-TD-3); the colours of the deleted teams and of projects with no node are `blue` by
the migration's own fill rule (deviation already named in `status.md`).

### Verdict

blockers: 0 · result: pass

## Frontend

Plan written from doc-016 (BR-TD-1, -4 team part, -5, -20; US-1, US-2; `design.md` §§ 4, 7, 8; the decomposition row of this line)
before the frontend code was read. E2E: not asked, not run (the human's standing answer is NO, paused). Scope: the team form's and
page's «Департамент», a department's «Команды», the move, the deletion guard, the diagram, the selectors' fit, the colour mark.
Not this line: «Список»/«Дерево» of «Структура», the search, the cascading panel and its path button (feat-061..065), projects in
departments (mod-022), department managers (mod-023).

### Coverage matrix

| Criterion / screen | Check | Test type | Status |
|---|---|---|---|
| US-1 «Новая команда»: «Департамент» required, «Выберите департамент», helper, nothing sent | `OrganizationTeamsPage.test` | component | covered |
| US-1 picker offers departments only (no project node, sub-area, «Верхний уровень») | `TeamPlaces.qa.test` | component | added |
| US-1 pre-set from a department's «Команды» | `AreaPage.test` | component | covered |
| US-1 server 404 `area.not_found` on create lands under the field, nothing created | `TeamPlaces.qa.test` | component | added |
| US-1 team page: «Департамент: …» link with colour mark; no «Области»/link picker | `TeamPage.department.test` | component | covered |
| BR-TD-4 move: other departments only, one `PUT`, notice, new place | `TeamPage.department.test` | component | covered |
| BR-TD-4 move: required field, no other department, `has_links` refusal with projects + «Отключить», retry after unlink | `TeamPage.department.test` | component | covered |
| BR-TD-4 move: department gone (404) under the field; 403 as the dialog alert; «Отмена» of the unlink returns with refusal and choice kept | `TeamPlaces.qa.test` | component | added |
| BR-TD-4 move: team linked only to a deactivated project | `TeamPlaces.qa.test` (`it.fails`) | component | **defect F-1** |
| BR-TD-4 «Переместить» only for the holder of `team.create` | `TeamPage.department.test` | component | covered |
| BR-TD-2 department «Команды» = teams standing in it, no link picker, no «Отключить» | `AreaPage.test` | component | covered |
| BR-TD-5 deleting a department with a team: `has_teams` text, names, no confirmation | `AreasPage.test` | component | covered |
| feat-049 diagram: a team once, in its department; missing place = outside | `diagramModel.test`, `diagramLayout.test` | unit | covered |
| Selector fit: a team fits a department by its place only | `selectorFit.test` | unit | covered |
| BR-TD-20 colour mark: 12 hexes equal the spec (checked by hand), unknown key neutral, name always beside it | `ColorLabel.test` | unit | covered |
| BR-TD-20 where it shows: department «Команды» rows, «Команды» list name, team page department line | tests above | component | partly: F-2, F-3 |
| Retired UI and routes gone (`areaIds`, `departmentIds`, `AreaLinkDialog`, department-team link hooks) | grep of `src` and `e2e` | static | none left |
| Error codes `has_links`, `has_teams`, `department_required` pinned to the catalog | `errorCodes.organizations.test` | unit | covered |

### Decisions on the open points

1. **Environment.** `npm ci` in `src/frontend` installed the locked tree; `@tiptap/*` is present. `tsc -b`: clean, exit 0. The 328
   failures and the tsc errors the developer saw were the missing install, not the branch.
2. **Move refusal for a deactivated project: a defect, F-1 (major).** BR-TD-4 sends the user to unlink first (feat-028 BR-OL-8),
   and the design's refusal block lists the projects with «Отключить». The server counts links to projects of any status
   (api-contract § 2), a deactivated project keeps its links, but the link and unlink routes treat a deactivated project as
   not found (feat-028 api-contract: "deactivated — alike"), and the dialog's list is the team's active projects. The
   `oldTeam` fixture («Старый проект», deactivated) reproduces it: the refusal text with «Проекты:» and nothing under it. No
   path in the panel completes the move, and the way out (restore the project, or delete the team) is named nowhere.
3. **Colour marks.** The line's own surfaces are the team form and page, a department's «Команды», the «Команды» list and the
   diagram. Design § 7.1 puts the team dot in "the page title of the team" and the department dot in the «Департамент» column
   of «Команды». Both are missing: F-2, F-3 (minor). The path button, search rows, «Структура» chips and tree stripes belong to
   feat-061..065 and the structure-list lines; the «Проекты» list's columns to mod-022. Not defects here.
4. **Light/dark.** Confirmed: `theme.ts` fixes `palette.mode: 'light'`, no `colorSchemes` exist, and `richText.ts` says "no dark
   theme yet". One hex per key serves the only theme; the spec's "read on both themes" has no surface to check. Not a defect.

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Move dialog, 404 department | text under «Новый департамент», dialog stays | pass |
| 2 | Move dialog, 403 | `Недостаточно прав` alert | pass |
| 3 | Cancel the unlink dialog from a move refusal | back to the move dialog, refusal and department kept, no unlink sent | pass |
| 4 | Create team, 404 department | «Область не найдена.» under «Департамент», dialog open | pass |
| 5 | Move a team linked only to a deactivated project | a way to unlink, or a different message | **fails (F-1)** |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-13 | `npm ci` in `src/frontend` | ok |
| 2026-11-13 | `npx tsc -b` | clean |
| 2026-11-13 | `npx vitest run` (whole suite, once) | 6486 pass, 6 skipped, 12 fail in 9 files, none in this line's files: work item card and form pages, `useGroupRead` "four in flight", `OrganizationTeamsPage.members` search line; failures were timeouts of 24-83 s (limit 30 s) in a 27-minute run on a loaded machine |
| 2026-11-13 | the 9 failed files re-run alone | 4 files (76 tests: `useGroupRead`, `OrganizationTeamsPage.members`, work item `sections`, `planning`) pass; the other 5 work item files: 3 pass, 2 fail on their first test (`WorkItemFormPage.acceptanceCriteria` "renders existing …", `WorkItemFormPage` "reads «Баг MSK-BUG-4» …") at 25-26 s, the per-test limit, spent loading the rich-text editor cold; a lone re-run of the first file reproduced it, the rest of that file passes (12 of 13); the branch touches nothing under `workitems` or `boards`: the machine, not the branch (D-3) |
| 2026-11-13 | `TeamPlaces.qa.test.tsx` (new, 6 tests) | pass (F-1 as `it.fails`) |
| 2026-11-13 | ESLint and Prettier on the new file | clean |

### Defects
#### D-3: cold first-test timeouts in two work item form tests (not this task)- Severity: minor (environment). The branch does not touch the slice; CI decides. Not a debt of this task.

#### F-1: a team linked only to a deactivated project cannot be moved and the dialog offers nothing to do

- Severity: major (a dead end in the flow the spec makes the only way: BR-TD-4 "unlink first")
- Steps: a team whose only link is to a deactivated project; open its page, «Переместить», choose another department, «Переместить».
- Expected: the refusal names what to remove, or the refusal does not fire for a deactivated link (BR-TD-4, design § 4.2).
- Actual: 409 `organizations.team.has_links`; the dialog shows the text and «Проекты:» with an empty list. No «Отключить»; the
  unlink routes do not take a deactivated project. Test: `TeamPlaces.qa.test.tsx`, "a team linked only to a deactivated
  project …" (`it.fails`: drop `.fails` with the fix).
- Fix options (the orchestrator decides, the second touches the contract): the dialog lists the deactivated linked projects too,
  tagged «деактивирован», with a line telling to restore the project first; or the move counts active links only (backend and
  contract change). At minimum the empty-list case must say what to do.

#### F-2: no colour mark in the team page's title

- Severity: minor. Design § 7.1: "in the page title of the team or project". Actual: the title of the team's «Обзор» is plain
  text; the mark stands on the department line only. Carry to gate-3 as a debt.

#### F-3: no department mark in the «Департамент» column of the «Команды» list

- Severity: minor. Design § 7.1: "in the «Департамент» column of «Проекты» and «Команды»". Actual: a plain link; the team's own
  dot is in the name column. The list item carries the department's name, not its colour. Carry to gate-3 as a debt.

Noted, not defects: the move uses the `team.create` code, so a department «Менеджер» does not see «Переместить» (mod-023, as the
plan says); the look of the dots was not checked in a browser (no stand, no E2E).

### Verdict

blockers: 0 · majors: 1 (F-1, returned to the frontend developer) · minors: 2 (F-2, F-3) · result: not yet pass
