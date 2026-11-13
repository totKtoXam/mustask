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
