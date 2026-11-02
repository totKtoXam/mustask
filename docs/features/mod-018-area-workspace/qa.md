# QA — mod-018-area-workspace

## Backend

QA pass of 2026-10-29 (stage `05-backend-qa`, clean context) over the typed single tree at `6fe29b48`. It replaces the pass of
2026-10-27, which described the first build. The plan below was drawn from `spec.md`, the six stories and the api-contract before
the implementation was read. E2E: the standing answer is NO; not asked, not run.

### Runs

Built once (`dotnet build Mustask.sln`, 0 errors), then each project run once with `--no-build`:

| Project | Result |
|---|---|
| Domain.Tests | 842 passed |
| Application.Tests | 767 passed |
| Contracts.Tests | 252 passed |
| Architecture.Tests | 86 passed |
| Shared.Tests | 53 passed |
| Infrastructure.Tests (Docker) | 447 passed, 6 m 48 s |
| Api.IntegrationTests (Docker), the whole project | 3 111 passed, 18 skipped, 0 failed, 23 m 4 s |
| Api.IntegrationTests, the 9 tests added in this pass | 9 passed (3 120 passed, 18 skipped in all) |

The 18 skips are the live-Keycloak and the diacritics cases that were skipped before this line. `ProjectBacklogVolumeTests` (flaky on
timing earlier) did not fail in this run; no recurrence. The migration was run on clean databases by
`OrganizationAreaWorkspaceMigrationTests` (see P15).

### Test plan and coverage matrix

"dev" = the developer's test, "QA" = added in this pass (`AreaTypedTreeQaEndpointTests`).

| # | Criterion (story) | Check | Where | Status |
|---|---|---|---|---|
| P1 | US-1 department at the top, sub-area under a project node or sub-area; type, project, path in every answer | `Create_DepartmentAndSubArea_…`, `Create_UnderADepartment_IsTypeNotAllowed_…` | API AreaWorkspace (dev) | pass |
| P2 | US-1 name 1-100 trimmed, description 2 000 UTF-16 units, every 400 reason at once | `Create_InvalidValues…`, `Description_IsCountedInUtf16Units` | API (dev) | pass |
| P3 | OQ-12 one name space per level, case/space-insensitive, across types | `Create_SiblingName_…AcrossTypes` (dev); `OneNameSpacePerLevel_AcrossCreateRenameAndMove`: project create/rename into a department's name, a node moved to the top onto a department's name, a department onto a project's name (QA) | API | pass |
| P4 | BR-AW-3 depth 10 at creation and after a move | `Create_TheEleventhLevel…`, `Move_ASubtreeThatWouldPassTenLevels…` (dev); `MoveProjectNode_UnderADepartment_TheNinthLevelFits_TheTenthDoesNot` (QA, the boundary of a node plus its subtree under a department) | API | pass |
| P5 | Move rules per type: project node between departments and top, sub-area inside its project only, department never nested, cycle | `Move_SubAreaAndProjectNode_AndTheTypeRules` (dev); `MoveDepartment_ToTheTop_IsANoOp_…` (QA) | API | pass (see D-2) |
| P6 | OQ-5/OQ-10 project node only with its project: rename and delete are `root_is_fixed`; the project's rename renames the node | `Rename_…`, `RenameProject_RenamesItsNode`, `Delete_TheRulesOfEachType` (dev) | API | pass |
| P7 | OQ-6 deactivated project: node marked, read-only (`project.not_found`), still a child and a taken name | `DeactivatedProject_NodeIsMarked_AndReadOnly` (dev) | API | pass |
| P8 | US-3 project placement: `parentAreaId` on create; not a department is `type_not_allowed`; missing is 404 | `CreateProject_WithAParentArea_PutsItsNodeThere` (dev) | API | pass |
| P9 | OQ-8/OQ-11 team links join departments only; `link_type_not_allowed` from either end and from a creation; all or none | `Link_ToAProjectNodeOrASubArea_…`, `Create_DepartmentWithTeams_…` (dev); `CreateTeam_NamingANonDepartment_Returns409_CreatesNoTeam` (QA, the team form) | API | pass |
| P10 | Links survive a rename and a project moving under the department; team deletion ends links | `DepartmentLinks_SurviveARenameAndAProjectMovingUnderIt` (QA); `DeletingATeam_EndsEveryAreaLinkOfIt_…` (dev) | API | pass |
| P11 | US-4 item «Область» is the project's node or a sub-area; a department, another project's node or sub-area is «Область не найдена.»; an item blocks deletion in any state | `ItemArea_ADepartmentOrAnotherProjectsNode_IsRefused_…` (QA), `Save_AreaOfAnotherProject…` , `DeleteAreaWithWorkItemsEndpointTests` (dev) | API | pass (see D-1) |
| P12 | US-5 upgrade: every tree kept as a project node, types set, items keep areas, depth >10 fails naming the project, Down, empty database | `OrganizationAreaWorkspaceMigrationTests` (6 tests, dev) | INF | pass |
| P13 | US-6 tenant containment, 401, non-member 404, member/viewer 403, order of answers | `EveryAreaRoute_…` (4), `Writes_ForAMemberAndAViewer…`, `AMissingArea_IsA403ForAViewer…`, `OrganizationAccessInventoryTests` (dev) | API | pass |
| P14 | NFR volumes: 1 000 nodes, 200 projects, 200 teams; reads under 2 s | `Reads_1000Nodes_200Projects_200Teams_Within2SecondsEach` (dev) | API | pass |
| P15 | NFR query shape (tree 1, page 1, project areas 3, team areas 3, deletion 2), structure contract | `AreaReadsTests`, `OrganizationStructureTests` (dev) | INF | pass |
| P16 | Concurrency: same name, create vs rename, cycle, depth, delete vs write under the leaf, project-node move vs department deletion, rename vs move, link vs deletion of either end, same pair from both ends | `AreaRacesEndpointTests` (dev, 12 cases) | API | pass |
| P17 | Concurrency, spec § NFR items the dev set lacks: a project's creation under a department vs that department's deletion (both orders); a deactivation vs a move of its node | `CreateProjectUnderADepartment_VersusItsDeletion_…` (2), `DeactivateProject_VersusTheMoveOfItsNode_…` (QA) | API | pass |
| P18 | Domain-spec refactor (`ReachabilityRules`, `WorkItemSetRules`, `OwnedItemsRule`): decisions testable without a database; behaviour and statement counts unchanged | `SpecificationTests` (dev, Domain); `DeletedStateParityTests`, `WorkItemSetsTests`, `OwningTeamStatementCountTests`, `UnlinkTeamAtomicityTests`, `DeleteTeamAtomicityTests`, WorkItemAccessRules test, all in the full runs | DOM, INF | pass |
| P19 | Error catalog and reference (`depth_exceeded`, `type_not_allowed`, `outside_project`, `link_type_not_allowed`, `root_is_fixed` back, `not_empty` text) | `OrganizationErrorCatalogTests` (Contracts) | CON | pass |

### Added tests

`src/backend/tests/Mustask.Api.IntegrationTests/Features/Organizations/AreaTypedTreeQaEndpointTests.cs`, 9 tests (P3, P4, P5, P9,
P10, P11, P17). While writing them four of my own expectations were wrong and were fixed in the test, none was a product defect: a
project name is unique in the organization before the node's sibling check (`project.name_taken` answers first), the item type key is
`BUG`, and the deactivation/move pair cannot be gated because a move reads the project's status without locking it.

### Read of the implementation (after the plan)

- Domain-spec refactor: `DefaultTeams` now filters the joined teams by `ReachabilityRules.ActiveTeam` in the join instead of a
  `where` clause. The predicate is the same and EF still produces one statement, which the statement-count tests pin; the exact SQL
  text was not diffed (the pins hold). `OwnedItemsRule` feeds the count, the lock and the `UPDATE` from one value; the raw SQL is
  character-for-character the old text.
- A write beneath a project reads its status, as ADR-0026 rule 8 says, and does not lock it: a move that started before a
  deactivation may commit after it. By design; recorded so nobody reads it as a gap.
- The migration's depth check counts every root as level 1 and the exception names the project and key.

### Defects

| id | severity | status | Description and reproduction |
|---|---|---|---|
| D-1 | minor | closed 2026-10-29 (contract wording fixed; see status.md) | api-contract § 6 says an item's «Область» outside its project is 409 `outside_project`. The code, US-4 and the design answer 404 `area.not_found` (`SaveWorkItemHandler.LockAreaAsync`), and `outside_project` exists only for a sub-area's move. Repro: `PUT …/workitems/{id}` with `areaId` of another project's node. The behaviour is the intended one; the contract text is wrong (a wording change needs a `status.md` note by the api-contract rule). |
| D-2 | minor | closed 2026-10-29 (contract wording fixed; see status.md) | api-contract § 3 says a department's move is `type_not_allowed`. A department moved "to the top" is 200 and changes nothing (the parent it already has, BR-AW-3); only a move under another department is `type_not_allowed`. Pinned by `MoveDepartment_ToTheTop_IsANoOp_…`. Fix the contract wording or the behaviour; the spec is silent. |

No blocker and no major. Nothing is sent back to the backend developer. The old debts of status.md (m-2 fixed by the developer, m-3
frontend, m-5 narrowed into ADR-0031 rule 6) are not re-checked here.

Blockers: 0.
