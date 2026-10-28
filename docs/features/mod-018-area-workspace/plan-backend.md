# Backend plan: the organization's area workspace, typed single tree (mod-018)

Stage 04, L, reworked 2026-10-28 for the human's typed tree. Read: `spec.md` (BR-AW-0..15, OQ-3..OQ-13 as resolved), `design.md`,
ADR-0031 (supersedes ADR-0028 rules 2, 3, 7, 8 and ADR-0029 § 6 in part), ADR-0028 rules 4-6, feat-028 `plan-backend.md` (§ 7 locks).
Contract: `api-contract.md`. One developer, no lanes. The first build (untyped tree, area-project links, item area retired) was
unmerged; its migration is replaced, not stacked.

## 1. Slices

| Module | Slice | Change |
|---|---|---|
| Organizations | `GetAreaTree`, `GetArea`, `GetAreaDeletion`, `GetAreaTeams` | organization tree with `type`, `projectId`, `projectActive`; page with parent and children |
| Organizations | `CreateArea` (department or sub-area), `RenameArea`, `SetAreaDescription`, `MoveArea`, `DeleteArea` | type rules, `outside_project`, `root_is_fixed`, deactivated-project 404 |
| Organizations | `LinkTeamsToArea`, `UnlinkTeamFromArea`, `LinkAreasToTeam`, `UnlinkAreaFromTeam`, `GetTeamAreas`, `GetProjectAreas` | team links to departments only; the project subtree read |
| Organizations | `CreateProject`, `RenameProject`, `AddTeam`, `DeleteTeam` | project creates its node (`parentAreaId`); rename renames the node; teams take `areaIds`; team deletion ends its links |
| Organizations | removed | `LinkAreasToProject`, `UnlinkAreaFromProject`, `LinkProjectsToArea`, `UnlinkProjectFromArea`, `GetAreaProjects` |
| WorkItems | `CreateWorkItem`, `SaveWorkItem`, read models | the item's «Область» returns, bounded by its project (`outside_project`) |

## 2. Domain

- `Area`: `OrganizationId`, `ParentId`, `Type` (`Department`, `Project`, `Area`), nullable `ProjectId`, `Name`/`NormalizedName`,
  `Description`, `InheritsAccess`. Factories `CreateDepartment`, `CreateProjectNode`, `CreateSubArea`; `Rename` (refused on a
  project node: `root_is_fixed`), `Describe`, `MoveUnder` (type, project, cycle, depth), `EnsureDeletable`.
- `AreaTree`: `PathOf`, `LevelOf`, `HeightOf`, `IsSelfOrDescendant`, `EnsureCanHold` (`MaxDepth = 10`), `PreOrder`.
- `AreaTeam`: the department–team link. `AreaProject` is gone. `Project.Create` returns `NewProject(Project, Root)`.
- `PermissionCatalog`: `organizations.area.create`, `organizations.area.link` (area-team only).

## 3. Database and migration `OrganizationAreaWorkspace`

One regenerated migration (the first build's was unmerged): `areas` gains `type`, nullable `project_id`, `created_by/at`; check
`ck_areas_type_shape`; partial unique `ix_areas_project_id_node` (type = 2); sibling unique (organization, parent, normalized
name, NULLS NOT DISTINCT); parent key (organization, parent); `area_teams` (cascade from the area); `area_tree_locks`;
`work_items.area_id` kept with the key (project, area) → node/sub-area, added by raw SQL because EF cannot put an alternate key on
a nullable property (plain unique index `ak_areas_project_id_id`). Up: fails naming the project when a project tree is deeper
than 10; turns each root into the project node, descendants into sub-areas, fills the organization, creator and time from the
project. Down: drops the key, sets project nodes' parent null, deletes departments and team links.

## 4. Locks

Tree lock, then area rows by id (moved `FOR UPDATE`, destination or parent `FOR SHARE`), then the project row, the project's node,
teams, links (ADR-0031 rule 6). A locked row's presence is the lock statement's answer. A write beneath a project reads
`IsReachable` after its own lock. The item save takes the project hierarchy lock, the item, then its area `FOR SHARE`.

## 5. Tests

Domain (`AreaTests`, `AreaTreeTests`, `AreaLinkTests`, `ProjectTests`); Application with fakes for every handler's order of
answers and lock calls; Api.IntegrationTests: `AreaWorkspaceEndpointTests`, `AreaLinkEndpointTests`, `AreaRacesEndpointTests`,
`AreaVolumeEndpointTests` (200 projects, 200 teams, about 1 000 nodes, reads under 2 s), the inventory and containment tests;
Infrastructure.Tests: the migration test over a populated database (upgrade, depth refusal, rollback, empty), schema checks, read
statement counts (tree 1, page 1, project areas 3, team areas 3, deletion 2); Contracts (error registry, regenerated reference).

## 6. Decisions and deviations

Raw-SQL work item key and a unique index in place of an alternate key (EF limitation); `CreateProject` takes 9 statements (was 6)
and team deletion 14 (was 13): the node, its parent lookup and the team-link removal.
