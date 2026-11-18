# QA — mod-022-projects-in-departments

## Backend

Plan written from `doc-016` spec (BR-TD-3, -4, -6, -19 steps 2-3, -20, US-5, US-2) and `api-contract.md` before the code was read.

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|---|---|---|---|
| BR-TD-6: no department -> 400 `department_required` on `parentAreaId`, nothing created | absent field; JSON null; with a bad name both reasons listed | integration (`ProjectsInDepartmentsEndpointTests`, `…QaTests`) | pass |
| BR-TD-6: place is a department only | project node, sub-area -> 409 `type_not_allowed`; unknown / foreign -> 404 | integration (QA) | pass |
| BR-TD-6: who creates (`area.write` on the department) | manager creates in own department only; plain member 403 | integration (QA) | pass |
| BR-TD-6: request naming no department, caller without the top-level code | contract: 400, "no place is judged" | integration (QA, skipped) | **D-1** |
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

#### D-1: a request naming no department is judged by the top-level code first

- Severity: minor
- Steps: a user who holds `organizations.area.write` only on a department (grant «Менеджер») sends `POST /projects` with no `parentAreaId`.
- Expected (BR-TD-6; contract § 1: "No place is judged for such a request"): 400 `validation_failed`, field `parentAreaId`, reason `organizations.project.department_required`. Actual: 403, because `CreateProjectHandler` checks `organizations.area.create` for a null place before validating. The developer recorded it as a deviation but the contract was not changed.
- Impact: none for the panel (the form requires a department); an API client sees a misleading 403. Either amend the contract wording or reorder the check.
- Pin: `ProjectsInDepartmentsQaTests.Create_WithoutADepartment_ByADepartmentManager_Is400_NotA403` (skipped).
- Status: open (minor, carried to gate-3 as a debt)

#### D-2: stale comment on `Project.Create`

- Severity: minor
- `Project.Create` still says the node "stands under a department or at the top" and accepts a null parent; only the handler and the DB check stop a top-level project. Domain guard would make the rule local.
- Status: open (minor)

### Verdict

blockers: 0 · majors: 0 · minors: 2 · result: pass (backend)
