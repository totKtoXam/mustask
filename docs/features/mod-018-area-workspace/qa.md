# QA — mod-018-area-workspace

## Backend

QA pass of 2026-10-27 (stage `05-backend-qa`, clean context, deep tier). The test plan below was written from `spec.md`, the
six stories and `api-contract.md` before any implementation file was read. E2E: standing answer NO, not asked, not run.

### Test plan and coverage matrix

Test names are in `Mustask.Api.IntegrationTests/Features/Organizations` (API) or `Mustask.Infrastructure.Tests/Organizations` (INF) unless
a project is named. "dev" = written by the developer, "QA" = added in this pass.

| # | Criterion (story) | Check | Where | Status |
|---|---|---|---|---|
| P1 | US-1 example tree, order, each once, `inheritsAccess` stored and in no response | Create_TopLevelAndChild…, Create_AnswersNoInheritsAccess… | API AreaWorkspaceEndpointTests (dev) | pass |
| P2 | US-1/US-2 creation and edits change no item, team, project | AreaEdits_ChangeNoItemNoTeamNoProject (dev) | API | pass |
| P3 | US-1 name 1-100 trimmed, description 2 000 UTF-16, 400 with every reason | Create_InvalidValues… (dev, order fixed by QA, D-2), Edges_OfADescriptionInUnits… (QA) | API | pass |
| P4 | US-1/2 sibling uniqueness, case, top level are siblings | Create_SiblingName…, Rename_…, Move_… (dev); schema unique index (dev, fixed by QA) | API, INF OrganizationsSchemaTests | pass |
| P5 | US-1/2 depth 10 at creation and after a move | Create_TheEleventhLevel…, Move_ASubtree… (dev) | API | pass |
| P6 | US-1/6 foreign, missing or deleted parent: identical 404 | Create_ParentNotOfTheOrganization…, EveryAreaRoute_AnAreaOfAnotherOrganization… (dev) | API | pass |
| P7 | US-1 1 000 areas over 10 levels: whole tree within 2 s | AreaVolumeEndpointTests (QA) | API | pass (1 s for all reads) |
| P8 | US-2 rename, description, clear; own name in another case | Rename_…, SetDescription_… (dev) | API | pass |
| P9 | US-2 move: subtree, top level, same parent, name clash, own subtree, depth | Move_UnderAnotherAreaAndToTheTop… (dev) | API | pass |
| P10 | US-2/US-4 delete a leaf: 409 not_empty text, preview, links end, nothing else changes, 404 after | Delete_WithChildren… (dev), DeletingATeam… (QA) | API | pass |
| P11 | US-3 link/unlink from each end, idempotent, authors kept, all-or-none, active ends, foreign ends | AreaLinkEndpointTests (dev); ARelink_KeepsTheFirstAuthorAndTime, LinkSeveralProjects_OneMissing_LinksNone (QA) | API | pass |
| P12 | US-3 creation with links; a refused one leaves name and key free | Create_WithLinks… (dev); ARefusedCreationWithAreas_LeavesTheNameAndTheKeyFree (QA) | API | pass |
| P13 | US-3 deactivated project inert and restored; team deletion ends links | DeactivatedProject_…, Link_ADeactivatedProject… (dev); DeletingATeam_EndsEveryAreaLinkOfIt (QA) | API | pass |
| P14 | US-4 no area on items, no project-nested tree routes | WorkItem_CarriesNoArea…, ProjectNestedAreaRoutes_AreGone (dev) | API | pass |
| P15 | US-5 upgrade on populated, clean and empty database; Down; applies again | OrganizationAreaWorkspaceMigrationTests (dev, 1 assertion fixed by QA, D-2) | INF | pass |
| P16 | US-6 foreign id = nonexistent id on every row of the table | EveryAreaRoute_AnAreaOfAnotherOrganization…, Link_ProjectOrTeamOfAnotherOrganization… (dev); creation rows (QA) | API | pass |
| P17 | US-6 roles per route; order 401 > 404 org > 403 > 404 area > 400 > 409 | Writes_ForAMemberAndAViewer…, EveryAreaRoute_WithNoToken_Returns401, AMissingArea_IsA403ForAViewer… , CreatingAProjectOrATeamWithAreas_ForAMember… (QA) | API | pass |
| P18 | US-6 inventory: each route declares its code; `/access` lists the codes | OrganizationAccessInventoryTests, OpenApi, TeamViewContainment (stale counts fixed by QA, D-2) | API | pass |
| P19 | US-6 structure contract: foreign id reads nothing; each project once; deactivated left out | AreaReadsTests.StructureContract_…, ListLinkedProjectsOfArea_… (QA) | INF | pass |
| P20 | NFR query shape: tree, page, area's projects and teams, deletion, project's and team's areas do not grow with size | AreaReadsTests.NewReads_TheSameNumberOfStatements_For5AreasAs400 (QA); item reads re-pinned (D-2) | INF | pass |
| P21 | NFR concurrency: name, rename vs create, rename vs move, cycle, depth, delete vs create/move/rename, link vs deletion of either end, team deletion vs link, two links of a pair | AreaRacesEndpointTests, 24 cases (QA) | API | pass, no 5xx, invariants read from the database |
| P22 | NFR 200 projects + 200 teams on one area; project's and team's areas | AreaVolumeEndpointTests (QA) | API | pass |
| P23 | Extras: 401 on all 20 routes, malformed ids, a move with no body, a description of 1 000 emoji | AreaQaEndpointTests (QA) | API | pass |
| P24 | ADR-0028 amendment text, error catalog (`depth_exceeded` new, `root_is_fixed` deprecated, `not_empty` text) | read in the diff; Contracts.Tests green (dev run) | docs | pass |

Not covered, by decision: per-handler unit tests are the developer's (Application.Tests 762, Domain.Tests 827, green in the
developer's run and untouched here); the item reads' statement count with no area is pinned by the re-pinned counts of D-2.

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| A1 | Rename of one area into a name that a move brings to the same level at once, both orders | one applied, the other `name_taken`, no 5xx (they meet on the sibling index, not on the tree lock) | pass |
| A2 | Rename against deletion of the area, both orders | delete first: rename 404; rename first: both done | pass |
| A3 | Link of an area to a team against the team's deletion, from the area's end and the team's, both orders | link first: both 204, no link remains; deletion first: 404 team_not_found | pass |
| A4 | A creation naming a foreign area along with an own one | refused, nothing linked, name and key free | pass |
| A5 | Re-link from the other end keeps the first author and time; unlink twice is 204 twice | as stated | pass |
| A6 | A move with no body | the top level (200) | pass |

### Runs

| Date | What was run | Result |
|------|--------------|--------|
| 2026-10-27 | `dotnet build Mustask.sln` | 0 errors |
| 2026-10-27 | Infrastructure.Tests, full (first run on Docker, the developer could not run it) | 419 passed, 27 failed of 446: all stale fixtures or pins, D-2 |
| 2026-10-27 | Api.IntegrationTests, full (first run) | 3 106 passed, 16 failed, 18 skipped of 3 140 (26 min): 13 stale expectations (D-2), 3 timing failures |
| 2026-10-27 | Infrastructure.Tests, the 9 affected classes after the fixes + `AreaReadsTests` (new) | 107 passed |
| 2026-10-27 | Api.IntegrationTests, the 11 affected classes + new classes | 397 passed, 0 failed |
| 2026-10-27 | the 3 timing failures alone (`BacklogVolumeTests`, `ProjectBacklogVolumeTests`, `WorkItemFilterVolumeTests`) + `AreaVolumeEndpointTests` | 5 passed. Hypothesis tested, not a ritual: they had run while a second test project used the same Docker host, and failed on a 1.04 s reorder (1 s budget) and a 30 s stream timeout |
| 2026-10-27 | `AreaQaEndpointTests`, `AreaRacesEndpointTests` (24) | 8 and 24 passed |

Totals after the pass: Api.IntegrationTests 3 155 green (3 106 + 16 fixed or re-run + 33 new) (0 failing, 18 skipped as before);
Infrastructure.Tests 449 (446 + 3 new) green; Application 762, Domain 827, Contracts 248, Architecture 86 green by the developer's run
and not re-run (no production code changed). Migrations: applied to a clean database, populated one, empty one, rolled back and applied
again by `OrganizationAreaWorkspaceMigrationTests`; no manual stand was needed.

### Defects

#### D-1: api-contract § 2 names the tree read as "2 statements"; it takes 1, a page 1

- Severity: minor (documentation)
- Steps: `AreaReadsTests.NewReads_TheSameNumberOfStatements_For5AreasAs400`, output.
- Expected / Actual: contract says 2; the handler sends 1 statement for the tree and 1 for the page, independent of size. Pinned at the
  real numbers (tree 1, page 1, projects 2, teams 2, deletion 3, project's areas 4, team's areas 3).
- Status: open, carried to gate-3 as a debt (edit the contract's parenthesis when the frontend stage next touches it).

#### D-2: 40 developer tests were stale or built blind (fixtures, pins, expectations)

- Severity: major as a gate (Api and Infrastructure projects were red, 16 + 27 failures), no production defect behind any of them
- Steps: first full runs, above. Causes: seeds that still joined `areas.project_id` or bound an `@area` (`TeamViewStatementCountTests`,
  `DeletedStateParityTests`); statement pins not moved by the retired area read and the new `area_teams` delete (`OwningTeam…`,
  `Filter…`, `Deletion…`, `TeamStatementCountTests`: 7 to 6, 21 to 20, 16 to 15, 13 to 12, 11 to 10, deletion 13 to 14); schema tests
  naming the old index and a "second root" case (`OrganizationsSchemaTests`); a sort-order expectation (`RepositoriesTests`); the
  migration test's project count (4, not 3); route counts 60 to 75 and 45 to 60 and `area.create` (checked by handlers, so declared by no
  route) in the inventory tests; the read-route matrix missing the six new reads; three save-order tests and one lock race that still used
  the retired area step (the race now holds the child item row, same lock cycle); `Create_InvalidValues…` expected 400 before the
  parent's 404 against BR-AW-15 (the handler is right).
- Expected / Actual: each corrected to the spec's behaviour; no assertion weakened.
- Status: fixed, re-run green.

### Verdict

blockers: 0 · majors open: 0 · minors open: 1 (D-1) · result: pass

The first pass found no production defect: the races, the 1 000-area and 200 + 200 volumes, the containment table and the answer order
all held. That is a statement about these probes, not readiness; the frontend QA pass and the code review are still to come.
