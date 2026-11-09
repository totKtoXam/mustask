# QA — mod-019 scoped access

## Backend

Pass 1, 2026-11-09, HEAD 95c51cd1. The plan below was written from spec.md,
stories and api-contract.md before any implementation file or test was read.
E2E: the standing answer is NO (paused); not asked, not run.

### Coverage matrix (plan)

| Criterion | Check | Type |
|---|---|---|
| US-1 give/change/remove on dept, project, team; roles offered | 201/200/204, one row per member+object, giver and time recorded | integration |
| US-1 no grant code | 403 and nothing stored (all three scopes) | integration |
| US-1 grantee not active / other org / removed / unknown | 404 membership.not_found, nothing stored | integration |
| US-1 wrong scope (grant on project node or sub-area) | 409 scope_not_allowed on GET/POST/PUT/DELETE/candidates | integration |
| US-1 invalid role, blank userId | 400 role_invalid, membership.user_invalid | integration |
| US-1 self grant (POST, PUT, admin too); self removal allowed | 400 self_grant; 204 | integration |
| US-1 POST on held / PUT, DELETE on missing | 409 grant.changed; same-role PUT keeps giver and time | integration |
| US-1 membership removal ends grants and team membership; re-add restores nothing | cascade | integration |
| US-1 race: grant vs membership removal | no orphan grant | integration (concurrency) |
| US-1 delete department/team ends grants; deactivate+restore project keeps | lifecycle | integration |
| US-1 grant to admin kept after demotion | stored | integration |
| US-1 other org id | object not-found | integration |
| US-1 team Reader only lists, does not reorder/sprints | 403 on reorder | integration |
| US-1 candidates: excludes caller and holders, active only, 20 + moreMatched | read | integration |
| US-2 reach dept -> project node -> sub-area; switch off cuts, held grant stays; on restores next request | reach | integration + domain unit |
| US-2 sub-area switch decides area-scope codes only | project grant unaffected | integration |
| US-2 who may switch (node + parent code; top-level uses area.create); department -> 409 | 403/409 | integration |
| US-2 impact: counts exclude admins and project-grant holders, callerIncluded, givenGrants (OQ-D1), department 409, already-off 0 | read | integration |
| US-2 move changes reach; no link/team carries access | reach | integration |
| US-2 switch on the locked row, concurrent move (m-2) | no stale pass | integration (concurrency) |
| US-2 create form applies switch last | create | integration |
| US-3 role/grant x object table (11 rows) | 403 vs kept | integration |
| US-3 codes only added; order independence; parity single vs set; property test | union | domain unit |
| US-3 backlog reorder split (BR-SA-10) all five users | matrix | integration |
| US-3 answer order 401, org-not-found, 403, 404; RetrofitAccessTests unchanged | order | integration |
| US-3 grant removal vs in-flight save | no half-apply | integration |
| US-3 refusal log has route, code, scope, user, no names | log | integration |
| US-3 instance admin without membership | org not found | integration |
| US-3 statement count equal at 10 / 10 000 grants, depth 1 / 10; 500 ms | perf | integration |
| US-3 shared board per-card flag | deferred (judge) | - |
| US-4 codes on area/project/team read, once per project row, org answer org-scope only | contract | integration |
| US-4 writable-ids from the set form, count pinned | read | integration |
| US-6 inventory test: four faults each fail and name route; exception list both ways; generated matrix; contract vs domain; realm client roles | architecture / contracts | unit |
| US-7 link create both ends, remove project-team both, dept-team either; order of 403 before 404; no power over other end; canUnlink; linkableFor (400 on unknown) | matrix | integration |
| US-8 POST /projects by place (step 0, non-admin 403 for any bad place, admin 404/409); dept manager project.write/link/grant; project manager area codes on subtree irrespective of switches; cannot move/switch own node; root_is_fixed; move needs three codes; name clash after codes | matrix | integration |
| Migrations | clean DB and repeated; additive; no merged migration edited | integration / git diff |
| Error codes | registry, JSON catalog, kk flagged | contracts tests |

### Additional scenarios

| # | Scenario | Expectation |
|---|---|---|
| A-1 | Grant PUT/DELETE with a malformed userId or userId of another org | 404 membership.not_found, not 500 |
| A-2 | Grant routes with a deleted/other-org area/project/team id | not-found for admin, 403 for non-admin |
| A-3 | Deactivated project: grants still readable/changeable? | consistent with 'keeps them' |
| A-4 | Dept grant reaches the project only when node switch on; project grant read lists inherited rows only then | GrantList inherited rows |
| A-5 | Inactive org (deactivated) with a grant | org not found |
| A-6 | Grant on a top-level project (no department): inheritance null | no 500 |
| A-7 | Candidates with 1-char text / empty / 200 chars / SQL-like text | no 500, no 400 |
| A-8 | linkableFor = unknown, empty, mixed case | 400 validation_failed |
| A-9 | Department manager sets project visibility / deactivates | 403 |
| A-10 | Role string case ("Manager", "MANAGER", null, numeric) | 400 role_invalid |

### Coverage found (after reading the tests)

Developer tests cover: the grant routes on all three scopes (give/change/revoke, self-grant, role_invalid, scope_not_allowed, membership.not_found, grant.changed, cascade on membership removal, candidates, answer order); the switch (off by a department manager, 409 on a department, 403 without the parent's code, idempotent, 400 without value); impact; served codes; `linkableFor`; `POST /projects` by place; writable ids; the inventory (100 routes, exception list both ways); the statement count at 10 and 10 000 grants; Domain and Contracts tests for the bundles and `role-bundles.json`; migration `AddScopedGrants` (additive, three tables, Down drops them; no merged migration edited; it runs on a clean Testcontainers database in every fixture).

Not covered before this pass: what a project, department or team grant lets its holder do on the project and team routes (renames, links), sub-area writes by project and department managers and the switch interplay, a move needing both parents, the reorder split for all roles, work item change by role, grant edge cases (self-removal, same-role PUT, mutual grants, grant to a demoted admin, lifecycle, bad input), impact exclusions.

Tests added (2 files, 24 tests): `Features/Organizations/ScopedRoutesQaTests.cs` (14), `GrantsQaTests.cs` (10). 17 pass; 7 are skipped with the defect id in the reason (D-1 x5, D-2, D-3) and are to be un-skipped by the fix.

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-09 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter ScopedRoutesQaTests` (first run, no Skip) | 5 failed, 5 passed; all five failures are D-1 (403 `access.forbidden` for a grant holder) |
| 2026-11-09 | `--filter GrantsQaTests` | 3 failed: a grantee absent from the user projection (test fixed), a sub-millisecond `givenAt` compare (test fixed), D-2 (confirmed) |
| 2026-11-09 | `--filter "GrantsQaTests\|ScopedRoutesQaTests"` after fixes and Skips (before the D-3 test was added) | 17 passed, 0 failed, 6 skipped (23) |
| 2026-11-09 | `--filter AProjectMember_OpensTheAssigneePicker` (added later, then Skipped for D-3) | 1 failed as expected: 403 |
| - | Developer totals in status.md (Domain 922, Application 857, Architecture 82, Contracts 232, Shared 53, Infrastructure 466, Api.IntegrationTests 3338) | not re-run: green is not re-checked |

### Judging the deferrals of plan-backend section 6

- Teams named at project creation by a department manager: acceptable. A team link needs `team.link` on the team end (BR-SA-15), which a department grant never gives, so an administrator-only step follows from the spec. `ADepartmentManagerNamingTeamsAtCreation_IsRefusedCleanly` pins 403 or 201, never a 500. The frontend must not offer team naming to a non-admin.
- Per-card flag on a shared board: acceptable for the server, not for the panel. A direct move of a card of B by a holder of «Участник» on A is a work item save checked on its project (`AWorkItemChanges_...` passes), so the server decides correctly. The board read has no per-card flag, so US-3's "the board offers moves on the cards of A only" is not met; the frontend plan must carry the contract addition (D-4).
- Frontend switch-over: expected at stage 6; it becomes load-bearing once D-1 is fixed.

### Not run (stated gaps)

Concurrency scenarios of US-1/US-2/US-3 (a membership removal racing a grant, a move racing a switch on the locked row, a grant removal during a save), the refusal log line, the 500 ms timing seed, and the generated per-scope matrix of US-6 (the spec's decomposition lists it as the later `tech-NNN-scoped-inventory-matrix` line). Carried as D-5.

### Defects

#### D-1: Project and team routes ignore every grant (major)

- Severity: major (the feature's main effect is absent on the routes it exists for)
- Steps: give a user (organization «Читатель») project «Менеджер» on project A, or department «Менеджер» on A's department, or team «Менеджер» on team T. Then call `PUT /api/organizations/{org}/projects/{A}/name`, `.../description`, `.../default-team`, `POST .../projects/{A}/teams` (link), `POST .../teams/{t}/unlink`, `GET .../unlink-count`, `PUT .../teams/{T}/name`, `.../description`, `POST .../teams/{T}/projects`.
- Expected (US-1 "Kirill renames A, sets its default team, renames MARS"; US-3 table; US-7; US-8 OQ-8 A): 200/204.
- Actual: 403 `organizations.access.forbidden`. These routes use the `RequiresOrganizationPermission` overload without a resource scope, so `OrganizationPermissionHandler.HoldsAsync` checks the organization role's bundle alone. Reproduced by the five skipped tests in `ScopedRoutesQaTests` (they failed on the first run).
- Why the inventory is silent: `OrganizationAccessInventoryTests` compares a declared scope with the code's, but a project- or team-scope code declared with no scope passes. Fix both: declare `ResourceScope.Project` or `Team` on these routes, and make the inventory fail on a code whose resource scope is not the organization when the route declares none (US-6 fault list).
- Status: fixed (pass 2, 1c442797): ten routes declare the scope; the five tests are un-skipped and green; the inventory now fails on the fault (see pass 2)

#### D-2: Deleting a team does not end its grants (minor)

- Steps: seed a team grant, `DELETE /teams/{id}` (204), read the grant through `IGrantRepository.FindAsync`.
- Expected (BR-SA-3, US-1, api-contract section 2 Lifecycle): gone in the same transaction. Actual: the row stays; the comment in `GrantConfigurations.cs` says the handler ends them, `DeleteTeamHandler` does not. No API read shows it (the team answers not found). Skipped test `DeletingATeamEndsItsGrants_...`.
- Status: fixed (pass 2, 76f2a690): DeleteTeamHandler ends the grants; the test is un-skipped and green

#### D-3: Organization-level pickers check the organization role only (minor, needs a decision)

- Steps: a project «Участник» (organization «Читатель») opens `GET /workitems/assignee-candidates`. Actual: 403, though the same user may change items of that project, so the item form's picker is unreachable. The spec is silent on org-level pickers. Skipped test `AProjectMember_OpensTheAssigneePicker`.
- Status: fixed (pass 2, decision OQ-11, 2cefbafc): the picker is judged on the optional `projectId`; the test is un-skipped and green

#### D-4: No per-card flag on shared boards (minor, deferral b)

- The server is correct; the panel cannot hide the moves. Add to the contract before the frontend plan.
- Status: open, carried to the frontend stage

#### D-5: Concurrency, log and timing scenarios not exercised (minor, QA gap)

- See "Not run". Not a found fault.
- Status: open

### Observations (no defect)

- `givenAt` in the POST answer differs from the stored value below one millisecond (in-memory ticks versus microseconds).
- `grant-candidates?text=` of 300 characters answers 400 while the contract says no 400 of its own; same rule as the directory search.

### Pass 2

2026-11-09, HEAD e7f404f3: the re-check of fix round 1 (1c442797, 76f2a690, 17dae47d, 2cefbafc, 4517a519, e7f404f3) and what it could have broken. E2E: standing answer NO; not asked, not run.

- **D-1 fixed.** `OrganizationAccessInventoryTests` now fails a narrower code (area, project, team scope) declared without a scope on a route that names `{areaId}`, `{projectId}` or `{teamId}`, and fails a code on no list when the route names none. `OrganizationWideUseOfNarrowCodes` holds 11 routes (a project list, two team-addressed reads, eight feat-048 aggregate reads), each with a reason, asserted both ways. The reasons are sound: each is a list or aggregate whose rows the handler narrows by the access set. The five un-skipped D-1 tests pass.
- **D-2 fixed.** `DeleteTeamHandler` calls `IGrantRepository.RemoveAllAsync` in the same unit of work; the test passes, plus a handler unit test.
- **D-3 fixed, per OQ-11.** `projectId` is optional; with it the code is judged on that project through the role or a reaching grant; without it, or with an id naming no project of the organization, by the role alone. The route is on the decided-in-handler list. Probed: a reader of the project, a member grant on another project, no `projectId` (all 403 for a viewer); a project of another organization, a malformed id and an unknown id: 403 for a grant holder (the grant lookup is organization-bound, so nothing leaks), 200 for an organization «Участник» (role alone). One note, not a defect: the fallback is by contract; the frontend must pass the real project id.
- **Order (BR-SA-9, US-7).** The six handlers judge not-found before the second code. Probed beyond the developer's tests: unlink and unlink-count with an unknown team are 404 for a project manager; a known team without a team grant is 403; a viewer is 403 first; `POST /teams/{id}/projects` and `/areas` with an unknown other end are 404 for a team manager. An empty `teamIds` list on project-team link still judges the role alone (kept, D-14).
- **Regression.** Nothing in what pass 1 covered failed; no handler outside the listed ones changed.

Tests added in pass 2 (`ScopedRoutesQaTests`, +2): `TheAssigneePicker_ANamedProjectOfAnotherOrganizationOrAMalformedId_FallsBackToTheRole`, `TheLinkRoutes_AnswerNotFoundBeforeTheSecondCode`; the class is 17 tests, all green, none skipped (with `GrantsQaTests`: 25 + 2 = 27 across the two classes).

### Timing (the developer's two failures)

| Run | Result |
|---|---|
| HEAD, `ProjectBacklogVolumeTests` alone | passed, 12 s total |
| HEAD, `AggregateViewVolumeTests` alone, first run | failed: board groups by project 12.8 s, backlog rows (no team) 2.1 s (budget 2 s) |
| 72e630a1 (merge base of origin/main; temporary worktree, removed), same test alone | passed, 4 m 41 s with the build |
| HEAD, same test alone, second run (hypothesis: noise) | passed, 1 m 48 s; groups 324-387 ms, «no team» 744 ms, keyword 1137 ms |
| HEAD, full integration run | passed, including both volume tests |

Per-grouping times at HEAD are the same as or below the merge base's (e.g. «backlog rows, no team» 744 ms against 1019 ms; the flat list 222 ms against 339 ms). The one failure was a single 12.8 s outlier on a loaded machine, not a slowdown by mod-019. No timing defect.

### Runs (pass 2)

| What | Result |
|---|---|
| `Mustask.Api.IntegrationTests`, whole | 3365 passed, 3 skipped, 0 failed (6 m 40 s) |
| Domain / Application / Architecture / Contracts / Shared / Infrastructure | 922 / 858 / 82 / 232 / 53 / 466 passed, 0 failed |

Remaining skips: two `LiveRealmImportTests` (need `MUSTASK_KEYCLOAK_LIVE_TESTS=1`, the verify keycloak bucket) and `WorkItemFilterQaTests.Search_ADecomposedYo_IsStillYo` (an older feature's open minor, D-B1).

### Defects after pass 2

D-1, D-2, D-3: fixed. D-4: open, carried to the frontend plan. D-5: open as is (concurrency, log line and the 500 ms seed unexercised; a minor, a debt for gate-3).

### Verdict

blockers: 0 · majors: 0 · minors open: 2 (D-4, D-5) · result: **pass** for the backend. The cycle ends: no blocker or major is open, and this was the first re-check. E2E: standing answer NO (paused).
