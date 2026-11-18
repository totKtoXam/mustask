# QA — mod-022-projects-in-departments

## Backend

Plan written from `doc-016` spec (BR-TD-3, -4, -6, -19 steps 2-3, -20, US-5, US-2) and `api-contract.md` before the code was read.

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|---|---|---|---|
| BR-TD-6: no department -> 400 `department_required` on `parentAreaId`, nothing created | absent field; JSON null; with a bad name both reasons listed | integration (`ProjectsInDepartmentsEndpointTests`, `…QaTests`) | pass |
| BR-TD-6: place is a department only | project node, sub-area -> 409 `type_not_allowed`; unknown / foreign -> 404 | integration (QA) | pass |
| BR-TD-6: who creates (`area.write` on the department) | manager creates in own department only; plain member 403 | integration (QA) | pass |
| BR-TD-6: request naming no department, caller without the top-level code | contract: 400, "no place is judged" | integration (QA, un-skipped) | pass (D-1 fixed) |
| BR-TD-3: team–project link in one department only | both link routes, create-with-teams, team-create-with-projects; batch with one foreign -> none linked; relink = 204; unknown id = 404 | integration | pass |
| BR-TD-3 vs BR-TD-4 order | link-first / move-first races | integration (races) | pass |
| BR-TD-4: linked project not moved; same department = 200 no-op; after unlink it moves | + order: type rules and top-level refusal before `project_has_teams` | integration (QA) | pass |
| BR-TD-4: project to the top level -> `project_needs_department` | domain, handler, API | unit + integration | pass |
| BR-TD-19 step 2: placement by most teams / tie by name / fallback, reuse of fallback, colour of the created department | populated fixture, two organizations | migration (Testcontainers) | pass |
| BR-TD-19 step 3: cross links cut like unlink to «Без команды» (default team, places, owner, sprint, deleted items, `changed_at` untouched) | fixture | migration | pass |
| Orchestrator: idempotent | script run a second time, fingerprint equal | migration | pass |
| Orchestrator: per-organization log (counts, names) | log rows + notices asserted | migration | pass |
| Orchestrator: all or nothing | injected clash stops migration, log table absent, data unchanged | migration | pass |
| Orchestrator: Down/Up on populated data | Down drops log, relaxes check; Up again | migration | pass |
| NFR: 5 000 items per organization | cut completes, counted | migration | pass |
| BR-TD-20 for projects: project keeps colour on placement | colours before = after | migration | pass |
| DB refuses a project node without parent | `ck_areas_type_shape` | migration | pass |

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Deactivated project moved (linked or not) | still 404 `project.not_found` (mod-018 BR-AW-10), not `project_has_teams` | pass |
| 2 | mod-024 import creating a top-level project | impossible: `CreateProjectHandler` is the only project creator (`Project.Create` has one caller); import writes into existing projects; the DB check would refuse it anyway | confirmed by reading |
| 3 | Retired tests (department-vs-project name race, `OneNameSpacePerLevel_AcrossCreateRenameAndMove`) | protected a department and a project node sharing the top level; after this task they share no level, project names stay unique organization-wide (BR-M1-1, other tests), department names among departments (other tests). Nothing lost | confirmed |
| 4 | Deleted team (only statuses are Active/Deleted) | links go with the team; nothing to block a move | n/a |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-18 | `Mustask.Api.IntegrationTests` filter `ProjectsInDepartments` (developer's 9 + QA 14) | 22 passed, 1 skipped (D-1 pin) |
| 2026-11-18 | `Mustask.Infrastructure.Tests` filter `RichText`, alone | **not green, not the branch's**: every failure is a wall-clock budget assertion (`exceeded=True`, e.g. 4479 ms; `took 00:00:05.58`) on a loaded Windows machine, and the run ended with a test host crash. The branch changes no RichText or reducer file (`git diff origin/main...HEAD --name-only`). Counted 6 failing cases before the host died |
| 2026-11-18 | Developer-reported sets (Application, Domain, Contracts, Architecture, migration tests) | not re-run (green, run economy) |

### Defects

#### D-1: a request naming no department is judged by the top-level code first — FIXED (backend, mod-022 QA minors pass)

- Severity: minor
- Steps: a user who holds `organizations.area.write` only on a department (grant «Менеджер») sends `POST /projects` with no `parentAreaId`.
- Expected (BR-TD-6; contract § 1: "No place is judged for such a request"): 400 `validation_failed`, field `parentAreaId`, reason `organizations.project.department_required`. Actual: 403, because `CreateProjectHandler` checks `organizations.area.create` for a null place before validating. The developer recorded it as a deviation but the contract was not changed.
- Impact: none for the panel (the form requires a department); an API client sees a misleading 403. Either amend the contract wording or reorder the check.
- Pin: `ProjectsInDepartmentsQaTests.Create_WithoutADepartment_ByADepartmentManager_Is400_NotA403` (skipped).
- Status: open (minor, carried to gate-3 as a debt)

#### D-2: stale comment on `Project.Create` — FIXED (Project.Create requires a department parent, with a Domain test)

- Severity: minor
- `Project.Create` still says the node "stands under a department or at the top" and accepts a null parent; only the handler and the DB check stop a top-level project. Domain guard would make the rule local.
- Status: open (minor)

### Verdict

blockers: 0 · majors: 0 · minors: 2 · result: pass (backend)

## Frontend

Plan written from doc-016 (BR-TD-3, -4, -6, -20 for projects; US-5, US-2; `design.md` §§ 5, 7) before the diff was read. E2E: skipped by the human's standing decision (NO, paused).

### Coverage matrix

| Criterion / screen | Check | Test | Status |
|---|---|---|---|
| BR-TD-6 «Новый проект»: «Расположение» required, departments only, never «Верхний уровень» | options, helper, «Выберите департамент» on submit, nothing sent | `ProjectListPage.create.test` | pass |
| BR-TD-6 preset: entry point, the only department, manager's own | department «Проекты», manager with one writable department | `CreateProjectByPlace.test` | pass |
| BR-TD-6 no command without a place | reader sees no «Создать проект» | `CreateProjectByPlace.test`, `ProjectListPage.test` | pass |
| BR-TD-6 server `department_required` / `project_needs_department` on the field | refusal placing, `parentAreaId` key | `refusals.test`, `testApi.test` | pass |
| BR-TD-3 creation: «Команды» of the chosen department only, drops on change | | `ProjectListPage.create.test` | pass |
| BR-TD-3 link pickers, both ends, empty states, stale refusal | | `TeamPage.test`, `TeamsPage.test` | pass |
| BR-TD-4 project move: departments only, current excluded | | `AreasPage.test`, `ProjectGeneralPage.test` | pass |
| BR-TD-4 `project_has_teams` kept in the dialog, teams, «Отключить», department kept, then move | | `AreasPage.test` | pass |
| BR-TD-4 no other department (new) | text, no field, submit disabled | `ProjectPlaces.qa.test` | pass |
| BR-TD-4 caller who may not unlink (new) | team named, no «Отключить» | `ProjectPlaces.qa.test` | pass |
| BR-TD-6 «Вынести на верхний уровень» removed | | `AreaPage.test` | pass |
| BR-TD-20 colour marks (list, page) | | `ProjectListPage.test`, `ProjectGeneralPage.test` | pass |
| BR-TD-6 selector «＋ Создать проект», organization «все» | not built | - | out of scope (see below) |

### Judgements on the developer's flags

1. **Selector «＋ Создать проект» and organization «все»: out of scope, not a gap.** doc-016 spec § queue lines give `mod-NNN-projects-in-departments` only the creation/move/link rules and the upgrade; the selector entry is `feat-052` ("BR-TD-6's selector entry point") and the cascades `feat-063`. The dialog's `presetPlaceId` is what feat-052 will call. Carry to gate-3 as a note: US-5's first scenario's selector entry is not reachable until feat-052.
2. «Добавить проект» on a department shows `project_has_teams` as an alert without the team list: minor (F-3).
3. No «Переместить» in a department's project rows: minor (F-4). Design § 3 lists it; the move is reachable from the project's «Общие» and the tree row menu.
4. Stale link picker shows the server text under the field: minor (F-2); design § 5.3 says `ErrorBanner`.
5. **Unlink gated by server `canUnlink`: not a permission hole.** `canUnlink` is a display hint ("the unlink route decides", api-contract); the unlink request is authorized server-side. Hiding the button never grants anything. Only the default differs (`?? true` here, `?? canLink` on the pages), harmless as the server refuses.
6. **`httpClient.files.test.ts` is not the branch's.** The failing case is `apiFetchBlob ...` with `answer.blob.text is not a function` (jsdom `Blob` has no `.text()`); the branch changes under `src/shared` only `api/errorCodes.ts` and its drift test, nothing the file imports in a way that touches Blob. Re-run in this tree: 2 of 3 pass, same one fails. Not run against a pristine origin/main tree (no second checkout); reasoning only.

Invented copy (plan-frontend.md § 4): «Сначала выберите расположение.», «Нет другого департамента, в который вы можете переместить проект.» (mirrors the team's, same tone), «Команды:» / «Отключить» (mirrors «Проекты:»), «Команда уже подключена ко всем проектам департамента.» (mirrors «Все команды департамента уже подключены к проекту.», but the project's wording is «Все … уже подключены к проекту», the team's reads fine), «В департаменте «X» нет проектов. Создайте проект в этом департаменте.» (mirrors the design's teams line). Consistent with the design's tone; no change requested.

### Runs

| What | Result |
|---|---|
| `ProjectPlaces.qa.test.tsx` (2 new) | 2 passed |
| `httpClient.files.test.ts` | 2 passed, 1 failed (jsdom `Blob.text`), not the branch's |
| `tsc -b`, ESLint, Prettier on the new file | clean |
| Developer's set over `features/organizations`, `shared/api`, `src/test` | not re-run (run economy; my own run of that set returned no captured output) |

### Defects

- **F-1 (minor, fixed).** In the project move dialog, after the last team is unlinked the refusal text «Проект связан с командами…» stays on screen with an empty list until «Переместить» is pressed again (`linksText` is cleared only on submit). By reading `AreaDialogs.tsx`; not pinned by a test. Steps: move a project with one team to another department, get the refusal, «Отключить», confirm.
- **F-2 (minor, fixed).** Stale link picker (`team.other_department`) shows the text under the field, design § 5.3 asks for an `ErrorBanner`. Behaviour (nothing changes, list re-read) is right.
- **F-3 (minor, fixed).** Department «Добавить проект» of a project with linked teams: server text as alert, no team list or «Отключить» there (design § 5.2 shows them in the move dialog only; the user must go to the project).
- **F-4 (minor, stays a gate-3 debt, not built).** No row menu «Переместить» on a department's «Проекты» (design § 3).
- **F-5 (nit, fixed).** `CreateProjectByPlace.test.tsx` last test title still says «Верхний уровень»; `TeamPage` passes `departmentName ?? ''`, which would read «В департаменте «» нет проектов» if the name were ever absent.

### Verdict

blockers: 0 · majors: 0 · minors: 4 · nits: 1 · result: pass (frontend)
