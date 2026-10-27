# Backend plan: the organization's area workspace (mod-018)

Stage 04, L. Read: `spec.md`, `stories/US-1..6`, `design.md` § 13, ADR-0028 rules 3-5, 8, feat-028 `plan-backend.md`
(§ 7 locks, D-3), mod-005 `plan-backend.md` § 14.3. Contract: `api-contract.md`. One developer, no lanes (the use cases share
the new tables, the lock helper and the structure contract, and the retirement of the item's area crosses every test fixture).

## 1. Affected slices

| Module | Slice | Change |
|---|---|---|
| Organizations | `GetAreaTree`, `CreateArea`, `RenameArea`, `MoveArea`, `DeleteArea` | rewritten over the organization (no project in the address) |
| Organizations | `GetArea`, `SetAreaDescription`, `GetAreaDeletion` | new |
| Organizations | `LinkProjectsToArea`, `UnlinkProjectFromArea`, `LinkTeamsToArea`, `UnlinkTeamFromArea`, `GetAreaProjects`, `GetAreaTeams` | new (area's end) |
| Organizations | `GetProjectAreas`, `LinkAreasToProject`, `UnlinkAreaFromProject`, `GetTeamAreas`, `LinkAreasToTeam`, `UnlinkAreaFromTeam` | new (the other ends) |
| Organizations | `CreateProject`, `AddTeam` | take `areaIds`, no root area; `RenameProject` renames no area; `DeleteTeam` ends the team's area links |
| WorkItems | `CreateWorkItem`, `SaveWorkItem`, read models, `WorkItemHierarchy` | the item's area retires (US-4) |
| Boards | backlog read models | no area (none read today; comments only) |

## 2. Domain

- `Area`: `OrganizationId`, optional `ParentId`, `Name`/`NormalizedName`, `Description`, `InheritsAccess` (set true, no accessor
  beyond the entity). `Area.Create`, `Rename`, `Describe`, `MoveUnder(parent, tree)` / `MoveToTopLevel(tree)`; no root rule.
- `AreaTree`: a forest. `PathOf`, `LevelOf` (1-based), `IsSelfOrDescendant`, `HeightOf` (levels below a node),
  `EnsureCanHold(parentId, subtreeHeight)` (`MaxDepth = 10`, throws `area.depth_exceeded`), `PreOrder`.
- `AreaProject`, `AreaTeam`: the links (organization, both ends, `LinkedBy`, `LinkedAt`), like `ProjectTeam`.
- `Project.Create` returns the `Project` (no root), `Project.Rename` takes no root; `NewProject` and `AreaRootIsFixed` go.
- `PermissionCatalog`: `organizations.area.create` (O), `organizations.area.link` (A, registered as Organization scope until mod-019);
  `area.read`, `area.write` re-described; the admin bundle holds all, member/viewer read only.

## 3. Database and migration `OrganizationAreaWorkspace`

New migration on top of the merged ones, editing none (BR-AW-14, ADR-0028 rule 12):

1. Log (`RAISE NOTICE`) and drop: `UPDATE work_items SET area_id = NULL` is impossible (NOT NULL), so the order is: drop the item's
   area key and column and its indexes, then `DELETE FROM areas` (children first through the `parent_id` key: one statement
   deletes all, the key is checked at statement end). The count of areas dropped and of items whose area was cleared is logged
   per kind. `work_items.changed_at` is not written.
2. `work_items`: drop `fk_work_items_areas_project_id_area_id`, indexes `ix_work_items_project_id_area_id`,
   `ix_work_items_project_id_type_id_area_id`, column `area_id`; **create `ix_work_items_project_id_type_id`** (ADR-0028 rule 13)
   so the project backlog keeps its (project, type) prefix.
3. `areas` is dropped and recreated (it is empty): `organization_id`, `parent_id`, `name`, `normalized_name`, `description`,
   `inherits_access` (default true), `created_by`, `created_at`; PK `id`; alternate key `(organization_id, id)`; parent key
   `(organization_id, parent_id)` → `(organization_id, id)` Restrict; **unique `(organization_id, parent_id, normalized_name)`
   NULLS NOT DISTINCT** (`ix_areas_organization_id_parent_id_normalized_name`); checks on name and description length.
4. `area_projects`, `area_teams` (PK `(area_id, project_id | team_id)`, organization copy, `linked_by`, `linked_at`, keys
   `(organization_id, area_id)` → areas **Cascade**, `(organization_id, project_id | team_id)` → the object Restrict, a reverse
   index per end). `area_tree_locks(organization_id)` (PK, key to the organization): the tree lock row, inserted on demand
   (as `work_item_hierarchy_locks`).
5. Down restores the shape (an empty `areas` of the old shape with the old `area_id` column re-added nullable-then-not-null is
   impossible with items, so Down empties items' area through a root per project: each project is given a root named after it and
   items point at it), the previous build needing a stand reset as feat-028's Down.

## 4. Locks (ADR-0028 rule 8, with the amendment)

One order: (1) the organization's **area-tree lock** `FOR UPDATE`, then the area rows by id; (2) team rows; (3) project-team links by
(project, team); (4) sprints; (5) work items with the project's hierarchy lock; (6) order rows. Per write:

| Write | Takes |
|---|---|
| Create area | tree lock; parent `FOR SHARE`; then the tree read **after** the locks (depth), the sibling pre-check, insert; named teams `FOR SHARE`, projects read |
| Move | tree lock; the area `FOR UPDATE`, the destination `FOR SHARE`, by id; the tree re-read after the locks (cycle, depth, name) |
| Delete | tree lock; the area `FOR UPDATE`; children check after the lock; the area's links cascade |
| Rename, describe | the area `FOR UPDATE` only (no shape change, ADR-0028 rule 5) |
| Link (any end) | area `FOR SHARE` (a binder), team `FOR SHARE` (rule 8), then one `INSERT … ON CONFLICT DO NOTHING`; the project is only read |
| Unlink (any end) | the area `FOR SHARE`, one `DELETE`; area links have no binder, so no link-row lock |
| Team deletion | as feat-028, plus `DELETE FROM area_teams WHERE team_id` after the project links, before the status write |

A locked row's **presence is the lock statement's own answer** (A-9's corollary): the area locks return the id they locked
(`SELECT id … FOR …` through `SqlQuery`) and the handler loads the tracked row only after that answer says it exists.

## 5. Application and API

- `IAreaRepository` (organization-keyed), `IAreaLinkRepository`, `IAreaTreeReadModel` (organization), `IAreaLinkReadModel`.
- `IOrganizationStructure` adds the area reads of `api-contract.md` § 7 and loses `RootAreaId`.
- Endpoints: `OrganizationEndpoints` re-maps the area group at `/areas`, adds the project's and team's ends; the inventory test and the
  route table of `OrganizationAddressedRoutes` list each with its code.
- Work items: `WorkItem.Create` takes no area; `WorkItem.AreaId`, `WorkItemEdit.AreaId`, `IWorkItemRepository.LockAreaAsync`,
  `WorkItemAreaUsage`, `IAreaUsage`, `WorkItemAreaView`, the read model's area-path read and `SaveWorkItemCommand.AreaId` go; the
  read model's statement count drops by one.
- Error codes: `depth_exceeded` new; `not_empty` text; `root_is_fixed` deprecated (constant removed, entry stays); kk
  `kkNeedsReview`.

## 6. ADR-0028 amendment (in place, "mod-018, 2026-10-27", `docs/adr/README.md` processes 2-3)

Rule 8 text: (a) links locked `FOR NO KEY UPDATE` where it read `FOR UPDATE` (feat-028 D-3), every binder `FOR SHARE`;
(b) the corollary: a locked row's presence is judged by the lock statement's own answer, a fresh read decides only a locked row's
other fields; (c) the one lock order with the project row and the project hierarchy tier in it (mod-005 § 14.3, feat-028 § 7.1:
unlink and team deletion take the project hierarchy lock before the owned rows, mod-005 QD-1). No decision changes.

## 7. Tests (each acceptance criterion has a test with a story comment)

- Domain: `AreaTests`, `AreaTreeTests` rewritten (forest, path, level, height, depth, cycle), `AreaLinkTests`, `ProjectTests`.
- Application (fakes): `CreateArea`, `Rename`, `Move`, `Delete`, `GetAreaTree` and the link handlers' order of answers.
- Integration (Testcontainers): one `*EndpointTests` per use case, `OrganizationContainmentTests`, `AreasOwnNothingEndpointTests`,
  the concurrency pairs of US-1/2/3 (name, cycle, depth, delete vs create, link vs deletion), `OrganizationAccessInventoryTests`,
  volume test (1 000 areas, 2 s) and statement counts.
- Infrastructure: `OrganizationAreaWorkspaceMigrationTests` over a populated database (US-5), `OrganizationsSchemaTests`.
- Retired by name: `DeleteAreaWithWorkItemsEndpointTests`, `FakeAreaUsage`; rewritten without an area: every fixture that
  created items through `RootAreaId` (spec § Existing rules).
