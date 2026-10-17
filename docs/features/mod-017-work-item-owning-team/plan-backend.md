# Plan — backend (mod-017-work-item-owning-team)

Stage 4a, deep pass (L). No code yet. It covers the backend half of **US-1..US-7**
and fixes one interface, [api-contract.md](api-contract.md). The screens, the
picker, the filter control and the dialogs belong to stage 6
(`plan-frontend.md`, not yet written).

Inputs: [spec.md](spec.md) (BR-OT-1..-16, § Existing rules, § NFR), the stories
[US-1](stories/US-1-owning-team-on-the-item.md)..[US-7](stories/US-7-first-team-and-team-deletion.md),
[design.md](design.md) § 12, [review.md](review.md), doc-012 (BR-MX-9..-12, -17, -18,
-25, -26, § Existing data) and [ADR-0028](../../adr/0028-matrix-organization-model.md)
(rules 2, 5, 7, 8, 9, 12), ADR-0004..0006, 0010, 0011, 0026, 0027,
`docs/conventions/backend.md`, `src/backend/AGENTS.md`. The code this plan builds on
was read, not assumed (branch tip `47fffb4a`): `WorkItem`, `Team`, `Area`, `AreaTree`,
`Project`, `TeamDefaultArea`; the work-item, backlog and board read models,
repositories and memberships; `OrganizationStructure`; `CreateWorkItemHandler`,
`SaveWorkItemHandler`, `DeleteTeamHandler`, `AddTeamHandler`, `CreateProjectHandler`,
`DeleteAreaHandler`, the two reorder handlers, `GetTeamsHandler`; the three endpoint
files; `UnitOfWork`; the architecture rules; the commit-gate and statement-counter
test helpers; the merged migrations. **A 144-name check** of the spec's § Existing
rules found every backticked test name present in the code; a scan for the retiring
symbols found **36 more test files** the table does not name (§ 9.1b, § 9.2).

- **ADR:** none new. One **clarification of ADR-0028 rule 8** is needed (D-3, team
  deletion locks `FOR NO KEY UPDATE`); the human reads it in the morning.
- **Lanes: yes** (§ 10.2): eight lanes after a sequential phase 1.
- **No code, comment, test name, log line or commit names the reference tracker.**
- E2E: the standing answer is NO (paused 2026-10-12); not asked, not run.

## 1. Decisions

Decisions that depart from a spec or ADR reading, or that only the human could
settle, carry the night-delegation mark and stay open for the morning.

| # | Decision | Why |
|---|---|---|
| D-1 | **Two additive migrations**, not one: `AddWorkItemOwningTeam` (columns, keys, the mapping; drops nothing) then `RetireAreaOwnership` (the stale places, the place key, the three markers). decided: two (recommended) under the night delegation — for the human. | The spec says "additive migrations" (BR-OT-14, plural) and ADR-0028 rule 12 "migrations". One migration forces one commit that breaks every consumer of the area owner at once; two let the code move onto the owner in green commits (expand, then contract, § 10.1). The data drop is entirely the second. |
| D-2 | **The team is sent as `team: { teamId }`**, not as the flat `teamId` that design § 12 sketches. decided: the wrapper (recommended) under the night delegation — for the human. | mod-003 § 2.4: a field "absent, or `null`, is not changed", so a flat `null` («Без команды») is ambiguous on a save. The clearable fields already use a wrapper (`assignee`, `parent`, feat-022's `{ value }`). One shape on create and save; mod-006's flat `teamId` is retired (api-contract § 0.2). A custom converter would be a second convention for one field. |
| D-3 | **Team deletion locks the team `FOR NO KEY UPDATE`**, not `FOR UPDATE`; it locks every active team of the project that way, in id order. decided: no-key-update (recommended) under the night delegation — for the human; ADR-0028 rule 8 "Team deletion: the team `FOR UPDATE`" should read so. | `FOR UPDATE` deadlocks with a child created beneath an item of the team: the child's insert takes `FOR KEY SHARE` on the team row through the owner key, waits for the deletion, while the deletion waits for the parent the creation holds. `FOR NO KEY UPDATE` conflicts with every binder's `FOR SHARE` and with another deletion, and not with a key share. The status write is not a key change (the only unique index on `status` is partial), so the deletion still serializes with every binder (§ 7.4). |
| D-4 | The last-team guard (BR-OT-11, BR-OT-13 (4)) is serialized by that same lock of **all the project's active teams** in one statement ordered by id. | Two deletions that would empty a project each lock all teams in the same order, so the second waits, re-reads and is refused. A project-row lock would deadlock with a default naming (team, then project row). |
| D-5 | The four retired codes are **marked `"deprecated": true`** in `contracts/errors/organizations.json` and their C# constants removed; the catalog entries stay. | ADR-0005 rule 3 and `contracts/README.md`: "the code is retired, but the entry stays". The spec's "removes" reads so. decided: deprecate (recommended) under the night delegation — for the human. |
| D-6 | The default-team setting is **`projects.default_team_id`**, a composite key `(id, default_team_id)` onto `teams (project_id, id)`. | ADR-0028 § 1: "the project default team is a setting that references one of the project's links". A flag on the team would reintroduce the markers this line drops, and take two rows to move. |
| D-7 | `PUT …/default-team` answers **204**; `{ "teamId": null }`, `{}` and no body all clear the setting. | feat-001 § 3.2 has the same "a missing body is `{}`: clear". The panel re-reads the teams for the marks. |
| D-8 | The deletion's count is **`GET …/teams/{teamId}/owned-items/count`** → `{ "count" }`. | A read of work-item data under a team's address, owned by the Organizations slice that answers the deletion dialog, through a work-items contract (§ 6). |
| D-9 | The filter is the repeated query parameter **`teamId`**, a value a team id or `none`; a value that names no team of the project selects nothing and is never an error. | BR-OT-8, BR-OT-16: identical answers for an unknown, foreign, deleted and other-project id. Repeated singular names are what `feat-003` and `feat-013` will extend. |
| D-10 | A team backlog reorder takes the team `FOR SHARE`, its **member items `FOR KEY SHARE`** (ordered by id), then the order row `FOR UPDATE`, then reads the members **again in a locking statement**. | BR-OT-13 (3), NFR Concurrency: items (rule 8 step 5) before order rows (step 6). A save locks its item `FOR UPDATE`, so it conflicts with a key share; the place write's own foreign-key check is also a key share and does not. |
| D-11 | The place key `(work_item_id, team_id) → work_items (id, owning_team_id)` is created **in SQL** in the second migration; the unique index `(id, owning_team_id)` it needs is declared in the model. | Verified with EF 10: an alternate key over `owning_team_id` makes the column `NOT NULL` (EF refuses a nullable key property), which would end «Без команды». A foreign key cannot be declared without a principal key, so it is SQL. The model snapshot stays consistent; a test pins the constraint. |
| D-12 | **Lanes: yes**, reference use case **`SaveWorkItem`** (§ 10.2). | Eight lanes with disjoint zones; the binder pattern (lock the team, then decide, then write) is what four of them copy. |
| D-13 | `Project.Create` returns `NewProject(Project, Team, Area Root)` (the property was `FirstTeam`); `Team.Delete(int activeTeamsOfProject)` carries the guard; `Team.Create` replaces `CreateFirst` and `CreateWithArea`. | "No team is the first" (BR-OT-11) reaches the names. |
| D-14 | A child's creation **locks the project default team `FOR SHARE` before the number lock**, whether or not the parent has a team; it binds the parent's team by the owner key alone. | The fallback to the default is known only after the parent is locked; a lock not taken then would let the child land on a team a concurrent deletion has just committed (§ 7.3). The parent's team needs no lock: the deletion waits for the parent. |
| D-15 | A `23503` on the owner keys is mapped to `organizations.team.not_found` in `WorkItemRepository.SaveAsync` and on the default key in `ProjectRepository`. | ADR-0028 § 4: a key violation is a not-found refusal, never a 500. It is the backstop of the locks; no race of §7 should reach it. |
| D-16 | A rewritten test whose **name states a retired rule** is renamed, the commit message listing old → new; one whose name survives keeps it. | The spec says a rewritten test "keeps its name and intent"; `…FirstTeamFirst…` cannot after BR-OT-11. P-1 traceability is the message. |

## 2. Affected slices

Module → use case → change. "New" and "Gone" are folders.

| Module | Use case | Change | Lane |
|---|---|---|---|
| WorkItems | `SaveWorkItem` | `team` in the command; binder; place drop; reference | phase 1 |
| WorkItems | `CreateWorkItem` | `team`, defaults, the child rule, the lock order; `teamId` and the team's default area retire | L1 |
| WorkItems | `GetWorkItems` | the filter; `team` on rows | L2 |
| WorkItems | `GetWorkItem` | `team` on the item | L2 |
| WorkItems | `SetWorkItemParent`, `GetParentCandidates`, `GetAssigneeCandidates` | none (re-run for regression) | - |
| Boards | `GetBacklog`, `GetBoard` | membership by owning team; `BoardTeam` without a default area; `team` on rows and cards; no area tree read | L5 |
| Boards | `ReorderBacklog` | the lock sequence of D-10; the stale rule | L4 |
| Boards | `GetProjectBacklog`, `GetProjectBoard` | the filter; `team` | L6 |
| Boards | `ReorderProjectBacklog` | none (the project order does not meet the team) | - |
| Organizations | `GetTeams`, `GetTeam`, `RenameTeam`, `AddTeam` | `Team` shape (`defaultMark`); order by name; add creates no area | L7 |
| Organizations | `CreateProject` | the team is ordinary, the root unowned | L7 |
| Organizations | `DeleteTeam` | reassign, drop places, clear the setting, last-team guard | L3 |
| Organizations | **`SetDefaultTeam`** (new) | `PUT …/default-team` | phase 1 |
| Organizations | **`GetTeamOwnedItemCount`** (new) | `GET …/teams/{teamId}/owned-items/count` | phase 1 |
| Organizations | `CreateArea`, `MoveArea`, `RenameArea`, `DeleteArea`, `GetAreaTree` | no owner, no default, no team lock | L8 |
| Organizations | `SetAreaOwner`, `SetTeamDefaultArea` | **gone** (BR-OT-10, BR-OT-4) | L8 |
| Organizations | `RestoreProject`, `DeactivateProject`, `RenameProject` | the setting survives; the root rename stays | tests only |
| Shared | `IOrganizationStructure`, `IWorkItemRepository`, `IBacklogRepository`, `IBacklogReadModel`, `IWorkItemReadModel`, `ITeamReadModel`, `ITeamRepository`, `IProjectRepository`, `IAreaTreeReadModel`; new `IWorkItemTeamOwnership`, `ITeamBacklogPlaces` | § 6 | phase 1 |

## 3. Domain (`Mustask.Domain`)

**Organizations**

- `Team`: `IsFirstTeam`, `CreateFirst`, `CreateWithArea`, `NewTeam` go. `Team.Create(projectId, name)`;
  `Rename` unchanged; **`Delete(int activeTeamsOfProject)`** throws
  `ConflictException(TeamLastTeamCannotBeDeleted)` when the count is 1, then sets `Deleted` (D-13).
- `Area`: `OwnerTeamId`, `DefaultOfTeamId`, `SetOwner`, `MarkDefaultOf`, `ClearDefault`, the default checks go.
  `CreateRoot(projectId, name)`, `CreateChild(parent, name)`, `EnsureDeletable(hasChildren, holdsWorkItems)`
  (the root, then children or items; **no default refusal**). `TeamDefaultArea` is deleted.
- `AreaTree`: `AreaNode(Id, ParentId, Name)`; `ResolveOwner`, `ResolvedOwner` go; paths, depth, pre-order stay.
- `Project`: `DefaultTeamId`; **`NameDefaultTeam(Guid? teamId)`** (returns whether it changed);
  `Create` returns `NewProject(Project, Team, Root)` (D-13). `Rename` stays (it renames the root).
- **`ProjectDefaultTeam`** (new, static, pure): `Resolve(Guid? named, IReadOnlyCollection<Guid> activeTeamIds)` →
  the named team when it is active, else the only active team, else `null`; `MarkOf(bool named, int activeTeams)` →
  `DefaultTeamMark { None, Named, OnlyTeam }`. The one definition of BR-OT-4, used by the read models, the structure
  contract and the binder.
- **`ProjectTeams`** (new, static): `EnsureBindable(TeamFacts? team, Guid organizationId, Guid projectId)` with
  `TeamFacts(Id, OrganizationId, ProjectId, Status, IsReachable)`; the table of api-contract § 0.2 —
  `NotFoundException(TeamNotFound)` for no team, another organization, a deleted team, a team whose project is
  not reachable; `ConflictException(TeamNotInProject)` for an active team of another active project. One rule for
  the save, the create and the default naming.
- `OrganizationErrorCodes`: remove `TeamDefaultAreaNotOwned`, `TeamFirstTeamDefaultFixed`,
  `TeamFirstTeamCannotBeDeleted`, `AreaIsTeamDefault`; add **`TeamNotInProject`**
  (`organizations.team.not_in_project`), **`TeamLastTeamCannotBeDeleted`**
  (`organizations.team.last_team_cannot_be_deleted`).

**WorkItems**

- `WorkItem.OwningTeamId (Guid?)`; `Create(…, Guid? owningTeamId, …)`; `WorkItemEdit` gains
  **`TeamEdit? Team`** (`TeamEdit(Guid? TeamId)`; `null` = not sent). `Save` sets the value when it differs and then,
  and only then, moves `ChangedAt` (BR-OT-1). The handler detects a changed team by comparing before and after
  (`Save` keeps its `bool` result).
- `WorkItemHierarchy.CreateChild(…, Guid? owningTeamId, …)`: the child's team is decided by the handler under the
  parent's lock (`ChildTeam.Of(parentTeamId, projectDefaultTeamId)` in `ProjectTeams`, one line, unit-tested).
- **`WorkItemFilter`** (new): `TeamsSet`, `TeamIds`, `NoTeam`; `WorkItemFilter.None`;
  `WorkItemFilter.OfTeams(IEnumerable<string?>? raw)` — a canonical id → a team id, `none` → `NoTeam`, anything else is
  ignored but the filter is **set** (BR-OT-8); `null` or an empty sequence → `None`. The one definition of the
  filter's meaning, used by the list and by both project views.

## 4. Database

### 4.1 Entity configurations (Infrastructure)

- `WorkItemConfiguration`: property `OwningTeamId`; **foreign key** `(ProjectId, OwningTeamId)` →
  `Team (ProjectId, Id)`, `Restrict`; **unique index** `(Id, OwningTeamId)`; index `(ProjectId, OwningTeamId, TypeId)`
  named `ix_work_items_project_id_owning_team_id_type_id` (it also serves the key, so EF adds no second index).
  `ix_work_items_project_id_type_id_area_id` stays (the project views read its `(project, type)` prefix; the area
  guard reads `(project, area)`).
- `ProjectConfiguration`: property `DefaultTeamId`; foreign key `(Id, DefaultTeamId)` → `Team (ProjectId, Id)`,
  `Restrict`. A composite key whose first column is the primary key and whose second is nullable builds in EF 10
  (probed: optional, `MATCH SIMPLE`); a schema test (`Project_NameAndClearDefaultTeam_RoundTripsThroughEf`) proves the
  runtime path, with the SQL-only fallback of D-11 if it ever refuses.
- `AreaConfiguration`, `TeamConfiguration`: lose the owner, the default and the first-team properties, keys, indexes,
  checks (second migration). `OrganizationsConstraints` loses `FirstTeam` and `AreaDefault`; `WorkItemsConstraints`
  gains the new names.
- `BacklogPlaceConfiguration`: unchanged in the model (the place key is SQL, D-11).

### 4.2 Migration 1 — `AddWorkItemOwningTeam` (additive)

Generated with `dotnet ef migrations add AddWorkItemOwningTeam --project src/backend/Mustask.Infrastructure`, then the
data step is added by hand between the column and the keys:

1. add `work_items.owning_team_id uuid NULL`, `projects.default_team_id uuid NULL`;
2. **map**, in one `DO` block:
   `WITH RECURSIVE resolved(id, owner) AS (root areas with their owner UNION ALL children with COALESCE(own, parent's))
   UPDATE work_items SET owning_team_id = resolved.owner FROM resolved JOIN teams ON teams.id = resolved.owner AND teams.status = 'Active' WHERE work_items.area_id = resolved.id`.
   `changed_at` is not written; no project gets a default. The block `RAISE NOTICE`s the items mapped and the items
   left without a team (an owner that is deleted: expected 0; the first team owns the root and is never deleted);
3. create the unique index, the team-view index and the two foreign keys.

`Down`: drop the keys, the indexes and the two columns.

### 4.3 Migration 2 — `RetireAreaOwnership` (the data drop)

1. `DELETE FROM backlog_places p USING work_items w WHERE w.id = p.work_item_id AND p.team_id IS DISTINCT FROM w.owning_team_id`;
   `RAISE NOTICE` the count. A place that names the item's mapped owner stays; the others named a team an area edit took
   the item from and were already unread (BR-OT-14);
2. `ALTER TABLE backlog_places ADD CONSTRAINT fk_backlog_places_work_items_owner FOREIGN KEY (work_item_id, team_id)
   REFERENCES work_items (id, owning_team_id)` (D-11); a place of an item with no owner, or of another team's, is refused
   with `23503`;
3. count (`RAISE NOTICE`) and drop: the foreign keys `(project_id, owner_team_id)` and `(project_id, default_of_team_id)`
   of `areas`, `ix_areas_default_of_team_id`, `ck_areas_default_owned`, `ck_areas_root_owned`, the columns
   `owner_team_id` and `default_of_team_id`; `teams.is_first_team` with `ix_teams_project_id_first_team` and
   `ck_teams_first_team_active`.

`Down` restores the **schema and what the old checks require**: the columns and indexes; `is_first_team` on the earliest
active team of each project (UUIDv7 order); the root of each project owned by that team and its default; then the
checks. It does not recreate explicit owners or the other teams' default areas — the data OQ-4 A drops. The previous
build needs a stand reset after a `Down`; the migration test rolls both migrations back and applies them again.

### 4.4 Data dropped (named in the MR)

The **stale team places** (`backlog_places` rows whose team is not the item's mapped owner) and the **three markers**
(`areas.owner_team_id`, `areas.default_of_team_id`, `teams.is_first_team`). No item, area, team, project, project place,
team membership or other row is dropped. No item's «Изменён» moves; no project names a default, so in a project of several
teams a new item from the list starts «Без команды», where it landed on the first team before. Counts are logged by the
`NOTICE`s of § 4.2–§ 4.3.

## 5. Endpoints

The route table, the shapes and the error codes are [api-contract.md](api-contract.md) §§ 1–5. For the files:

- `OrganizationEndpoints.cs`: the two routes go (`…/default-area`, `…/areas/{areaId}/owner`), the two come
  (`PUT …/projects/{projectId}/default-team`, `GET …/teams/{teamId}/owned-items/count`); `.ProducesProblem(409)` on the
  default naming and the team delete; the area group loses its owner request records.
- `WorkItemEndpoints.cs`: the create and save request records carry `TeamRequest? Team` (`record TeamRequest(string? TeamId)`);
  a sent wrapper is a sent field (as `AssigneeRequest`); `[FromQuery(Name = "teamId")] string[]? teamId` on the list.
  mod-006's flat `TeamId` is removed from the create record.
- `BoardEndpoints.cs`: the same `teamId` binding on the two project reads; the team reads and the reorders take none.
- All other files of `Mustask.Api` are unchanged. **Phase 1 writes the three endpoint files in full** (4c).
- OpenAPI tests (`OrganizationsOpenApiTests`, `WorkItemsOpenApiTests`) and the route inventories follow (§ 9.4).

## 6. In-process contracts that change

(api-contract § 7 has the signatures.)

| Contract | Change |
|---|---|
| `IOrganizationStructure` | `ProjectInfo`: `FirstTeamId` → `DefaultTeamId` (resolved by `ProjectDefaultTeam` in the same statement as the project); `TeamInfo`: `IsFirstTeam`, `DefaultAreaId` go; `FindAreaOwnerAsync`, `AreaOwnerInfo` go; `ListActiveTeamsAsync` orders by name. Expand first: `DefaultTeamId` is added in phase 1, the removals are phase 3. |
| `IWorkItemRepository` | `LockTeamDefaultAreaAsync` goes. New: `LockTeamAsync(organizationId, teamId)` — `SELECT 1 FROM teams JOIN projects … WHERE team.id AND project.organization_id FOR SHARE OF team` (lock only, no decision; a foreign team locks nothing — ADR-0028 § 4, no lock spans organizations); `LockDefaultTeamAsync(projectId)` — read the named team and the active teams, `ProjectDefaultTeam.Resolve`, lock the result `FOR SHARE`, read its status again; a team found deleted means a deletion cleared the setting, so read again (three attempts, then none — unreachable short of three namings in a row). A default that changed during the attempt is taken as it was: the creation is ordered before the naming. `SaveAsync` maps `23503` on the owner key (D-15). |
| `IBacklogRepository` | `LockAsync(projectId, teamId, levelId)` becomes `LockAsync(scope)`: team `FOR SHARE` and active check → the members `FOR KEY SHARE` → the order row `FOR UPDATE`; `ReadEntriesAsync(scope)` is the locking read of D-10 (raw, parameterized, `ORDER BY id FOR KEY SHARE OF item`); `SaveOrderAsync` drops the places of items that left (by state) and upserts the members. |
| `IBacklogReadModel` | `ReadAsync`, `ReadTreeAsync` return `IReadOnlyList<BacklogItemRow>` (no `DefaultArea`); the project scopes carry `WorkItemFilter`; `BacklogItemRow` gains `Team (WorkItemTeamView?)`. |
| `IWorkItemReadModel` | `GetPageAsync(projectId, page, pageSize, filter, ct)`; `WorkItemView` and `WorkItemListItemView` gain `Team (WorkItemTeamView?)`; new `WorkItemTeamView(Guid Id, string Name)`. |
| `ITeamReadModel` | `TeamRow(Id, ProjectId, Name, DefaultTeamMark Mark, bool IsMember)`; one statement computes the mark (`ProjectDefaultTeam.MarkOf` over the project's named team and its active count); the list orders by name, then id. |
| `ITeamRepository` | `GetFirstTeamAsync`, `ReassignExplicitAreasAsync` go; `Add(Team)`; new `LockProjectTeamsAsync(projectId)` (the deletion's `FOR NO KEY UPDATE` of D-3/D-4, ids ordered). |
| `IProjectRepository` | `Add(NewProject)`; new `ClearDefaultTeamAsync(projectId, teamId)` (`UPDATE projects SET default_team_id = NULL WHERE id AND default_team_id = team`). |
| `IAreaTreeReadModel` | `AreaTreeRow` loses the owner and default names (the joins go). |
| **`IWorkItemTeamOwnership`** (new, `Features/WorkItems` root) | `CountOwnedAsync`, `LockOwnedAsync` (`FOR UPDATE`, by id), `ReleaseOwnedAsync(…, now)` — ADR-0028 rule 7's work-items write contract; implemented in `Infrastructure/WorkItems`, joining the caller's transaction. |
| **`ITeamBacklogPlaces`** (new, `Features/Boards` root) | `DropForItemAsync`, `DropForTeamAsync` — the boards module's tables are written by the boards module. Implemented in `Infrastructure/Boards`. |

`VerticalSliceRules` checks `using Mustask.Application.Features.<module>.<slice>` only; a module-root namespace passes (as
`IOrganizationStructure` already does).

## 7. Locks and concurrency (ADR-0011 echelon 3, ADR-0028 rule 8)

### 7.1 One order

Before the feat-028 links exist, the "link" of rule 8 is the team row. The order every write follows, taking only what it needs
and re-reading what it checks after locking:

| Step | Lock | Taken by |
|---|---|---|
| 2 | `teams` row(s): `FOR SHARE` by a **binder**; `FOR NO KEY UPDATE` by a deletion (all the project's active teams, by id) and a rename | a save or create that names a team, `SetDefaultTeam`, a team backlog reorder, the default lock of a create; `DeleteTeam`; `RenameTeam` |
| 5a | the project's number row, the hierarchy row | create, a parent write (mod-003's place, kept beside the items) |
| 5b | `work_items` rows `FOR UPDATE` (a save, a deletion's owned set); `FOR NO KEY UPDATE` (a parent as a link end); `FOR KEY SHARE` (a team reorder's members) | save; `DeleteTeam`; create beneath a parent; reorder |
| 6 | the order row `FOR UPDATE` | a team reorder |
| after | the `projects` row (the default setting's write: an `UPDATE`, `FOR NO KEY UPDATE`) | `SetDefaultTeam`; `DeleteTeam` clearing it |

The project row is **never taken before a team row**, so a default naming (team, then project) and a deletion (all teams, then
project) cannot cross. A binder of the *new* team takes nothing of the *old* one.

### 7.2 Sequences

| Use case | In this order |
|---|---|
| `SaveWorkItem` (reference) | (second code) → address → **team `FOR SHARE`** if a non-null team is sent (lock only) → hierarchy row if a parent is sent → **item `FOR UPDATE`** → values → area `FOR SHARE` → **team decision** (`FindTeamAsync`, a fresh statement, `ProjectTeams.EnsureBindable`) → parent → `Save` → if the owner changed, **`ITeamBacklogPlaces.DropForItemAsync`** → flush. The place is deleted before the item's `UPDATE` because the place key is checked at the end of the update statement. |
| `CreateWorkItem` | (second code) → address → values → type → **team lock** (an explicit non-null `team` with no parent: `LockTeamAsync`; no `team` field, or a parent: `LockDefaultTeamAsync`; `{ teamId: null }`: nothing) → number row → **replay (200)** → team decision (explicit only) → hierarchy row and parent `FOR NO KEY UPDATE` → team = parent's stored team, else the locked default → insert (the owner key takes a key share). |
| `SetDefaultTeam` | address → **team `FOR SHARE`** (tenant-scoped) → decision → project row `FOR NO KEY UPDATE` (re-check the status) → `NameDefaultTeam` → flush. |
| `DeleteTeam` | address → **`LockProjectTeamsAsync`** (active teams, `FOR NO KEY UPDATE`, by id) → the addressed team must be among them (`team_not_found`) → `Team.Delete(count)` (`last_team_cannot_be_deleted`) → `LockOwnedAsync` → `DropForTeamAsync` → `ReleaseOwnedAsync(now)` → `ClearDefaultTeamAsync` → memberships end → flush the status. |
| Team reorder | address → team `FOR SHARE` + active → members `FOR KEY SHARE` → order row `FOR UPDATE` → the locking read → stale check → drop and upsert. |
| Project reorder, reads | unchanged. |

### 7.3 The races (BR-OT-13), and why each ends in one order

1. **A binder meets the deletion.** The binder holds `FOR SHARE` on the team; the deletion's `FOR NO KEY UPDATE` waits. The
   binder commits (its item or setting is then taken with the rest: `LockOwnedAsync` and `ReleaseOwnedAsync` run on fresh
   statements), or the deletion committed first and the binder, re-reading after its lock, finds `Deleted` and is refused
   `team_not_found`.
2. **A child beneath an item of the team.** The child holds its parent `FOR NO KEY UPDATE` and inserts with the owner key
   (a key share, which `FOR NO KEY UPDATE` does not wait on). Child first: the deletion's `LockOwnedAsync` waits for the parent,
   then its release (a new statement) sees the committed child and reassigns it. Deletion first: the child's parent lock waits for
   the deletion's `UPDATE`, re-reads the parent without a team, and takes the **default locked before the number row**
   (D-14), which a deletion that named it has already cleared. The child is never refused for a team.
3. **A reorder meets a team change of a member.** The reorder's member lock (`KEY SHARE`) conflicts with the save's
   `FOR UPDATE` on the item. Reorder first: the save waits, then drops the place the reorder wrote. Save first: the
   reorder's locking read, evaluated on the updated row, no longer sees the item as a member; the handler finds the moved or
   followed item missing (`409 reorder_stale`) or completes on the members that remain and writes no place for the one
   that left. The place write's foreign-key check never fails: every member it writes is locked.
4. **Two deletions that would empty a project.** Both lock all active teams in id order; the second waits, re-reads, finds the
   other team gone, and is refused. A deletion also waits for a reorder of any team of the project, a bounded cost
   (the lock lasts the deletion, within 5 s for 5 000 items).

### 7.4 Deadlock reading

Edges that could form a cycle and why they do not:

- binder ↔ deletion: both take a team row first; nothing holds an item before it;
- child creation ↔ deletion: with `FOR UPDATE` it **would** cycle (child: parent → team key share; deletion: team → parent), which is
  D-3; with `FOR NO KEY UPDATE` the child's key share does not wait;
- reorder ↔ save: the reorder holds the order row and key shares, the save holds an item and no order row; the save deletes a
  place row only after it holds the item, and the reorder writes place rows only after it holds the members;
- default naming ↔ deletion: both take a team row, then the project row.

`23503` on the owner keys is the backstop (D-15) and is covered by a repository test that forces it; no race above reaches it.

## 8. Use-case notes

- **SaveWorkItem.** The `team` wrapper is parsed in the endpoint; the command carries `TeamChange? Team(string? TeamId)`. A team of
  the project already owning the item is a no-op and moves nothing. The answer is read after the flush (`GetAsync`), so `team`
  shows the stored value.
- **CreateWorkItem.** `CreateWorkItemCommand` loses `TeamId`'s old meaning: `TeamChange? Team`. The area is
  `project.RootAreaId` for a new item and the parent's area for a child. `ChildTeam.Of`. The replay is read before the team decision
  (api-contract § 0.4).
- **GetWorkItems / GetWorkItem.** `WorkItemReadModel.Rows` left-joins `teams` (the name now); `GetPageAsync` applies
  `WorkItemFilterPredicate.Apply(IQueryable<WorkItem>, WorkItemFilter)` to the **count and the page** (the one predicate of
  `Infrastructure/WorkItems`, shared by the boards' memberships: Boards infrastructure already uses WorkItems infrastructure's
  helpers). No statement is added.
- **Team backlog and board.** `BacklogMembership.Members` / `TreeMembers`: `item.OwningTeamId == scope.TeamId` replaces the area
  set; `AreaTreeAsync` and `TeamAreas` go; `BacklogReadModel.ReadAsync` drops its area-tree statement (one statement fewer),
  the default-area helper goes; `ItemsAsync` joins the team's name. `BoardTeamView(Id, Name)`.
- **Project backlog and board.** `ProjectBacklogMembership` applies the filter on the same statement; `ReadProjectAsync` /
  `ReadProjectTreeAsync` carry the filter in the scope; `FindLiveParentsAsync` and `ReadChecklistsAsync` are not filtered
  (BR-OT-8). `BacklogRowView.Tree` numbers the visible top-level rows (it already numbers what it is given).
- **Teams.** `TeamReadModel` and `OrganizationStructure.Stored` lose the default-area subquery and the first-team order; the mark
  is a subquery over `projects.default_team_id` and the active count. `AddTeam` loses the area repository and the root lock;
  `RenameTeam` and `GetTeam` lose the area. `GetTeams` orders by name only.
- **Areas.** `CreateArea` loses `OwnerTeamId` and the team lock; `DeleteArea` loses the default check and the team repository;
  `MoveArea`/`RenameArea` answer without an owner; `GetAreaTree` loses the owner joins.
- **Projects.** `CreateProject` creates `Team.Create` and `Area.CreateRoot` (no owner); the creator still joins the team.

## 9. Tests

Tests are written with the code (04-backend.md § 4b). A test that covers a story scenario carries a comment `// US-n · <scenario>`.

### 9.1 Retire or rewrite, by name, with the rule (P-1)

**The spec's § Existing rules table is the list** — every name in it was checked against the code (144 names, none missing).
This section fixes *what happens to each group*, and § 9.1b adds what the table does not name. Frontend tests of the table
(`TeamsPage.test.tsx`, `AreasPage.test.tsx`, `AreaDialogs.test.tsx`, `DefaultAreaDialog.test.tsx`, `areaTree.test.ts`,
`testApi.test.ts`, the backlog, board, list and project page tests) belong to `plan-frontend.md`.

| Group (rule) | Backend tests | Fate |
|---|---|---|
| mod-002 BR-M1-9 → BR-OT-11 | `AddTeamEndpointTests`: `Add_CreatesTeam_WithRootChildOwnedAndDefault`, `Add_RootHasChildOfThatName_Returns409AreaNameTaken_CreatesNothing`, `Add_NameOfDeletedTeam_WhileRootChildRemains_Returns409AreaNameTaken`, `Add_NameOfDeletedTeam_AfterAreaRenamed_Succeeds`, `Add_ConcurrentWithARootChildAreaOfTheSameName_OneRefused_NoTeamWithoutItsArea`; `AddTeamHandlerTests`: `Handle_AddsTheTeamWithRootChildOwnedByItAndItsDefault`, `Handle_RootHasChildOfThatName_IsAreaNameTaken_CreatesNothing`, `Handle_AreaOfThatNameBelowAChild_DoesNotBlock`, `Handle_NameOfDeletedTeam_WhileItsAreaRemains_IsAreaNameTaken_AfterAreaRenamed_Succeeds`, `Handle_LocksTheRootShare_ThenSavesOnce`; `TeamTests`: `CreateWithArea_AddsARootChildOwnedAndDefault`, `CreateWithArea_UnderAChildOrAnotherProjectsRoot_IsADefect`; `OrganizationsSchemaTests` «BR-M1-9: a second first team» | **retire** |
| | `AddTeamEndpointTests.Add_TwoConcurrentSameName_OneTeamAndOneArea`; `ProjectTests.Create_ComesWithFirstTeamAndRoot_OwnedAndDefault`; `CreateProjectEndpointTests.Create_Returns201_WithFirstTeam_RootOwnedByItAndItsDefault`; `CreateProjectHandlerTests.Handle_ValidNameAndKey_CreatesProjectWithFirstTeamAndRoot_RecordsCreator` | **rewrite** (D-16): one team, an ordinary one; the root owned by nobody |
| mod-002 BR-M1-11 → BR-OT-5, -10 | `AreaTreeTests`: `ResolveOwner_InheritsFromTheNearestOwnedAncestor`, `ResolveOwner_NodeWithNoOwnedAncestorBelowTheRoot_FallsBackToTheRootsOwner`; `GetAreaTreeEndpointTests`: `Get_InheritedOwnersMarked_RootFallsBackToFirstTeam`, `Get_AfterTeamDeleted_ItsAreasBelongToTheFirstTeam_NoDefaultOfIt`, `Seam_FindAreaOwner_EqualsTheTreesResolvedOwner_ForEveryNode`, `Seam_FindAreaOwner_UnderDeactivatedProject_IsUnreachable_WhereTheTreeAnswers404`; `GetAreaTreeHandlerTests`: `Handle_ResolvedOwners_NearestExplicitAncestor_InheritedMarksWhereNoExplicitOwner`, `Handle_Root_IsOwnedExplicitlyByTheFirstTeam_NeverInherited`, `Handle_DefaultOfTeam_NamesTheTeamOnItsDefaultAreaOnly`, `Handle_TeamRenamed_TreeNamesTheTeamNow_AreaNamesKept`, `Handle_EveryNode_EqualsWhatAreaTreeResolves`; `OrganizationStructureTests.FindAreaOwner_ResolvesThroughTheTree`; `ReadModelsTests.AreaTree_GetNodes_CarriesTheNamesOfTheTeamsEachNodeRefersTo`; `OrganizationsSchemaTests` — the cases «a root with no owner» (`ck_areas_root_owned`), «a team that owns areas», «an area owned by another organization's team», «an area re-owned to another organization's team», «an area owned by another project's team in the organization», «an area owned by its project's team», «an area re-owned to its project's team» | **retire** |
| | `OrganizationsSchemaTests` «BR-M1-11: a second root»; `RenameAreaHandlerTests.Handle_ChildWithChildren_RenamesIt_KeepsChildrenAndOwner`; `RenameAreaEndpointTests.Rename_KeepsChildrenAndOwner`; `RestoreProjectHandlerTests.Handle_DeactivatedProject_KeepsTeamStatusesOwnersAndDefaults`; `GetAreaTreeEndpointTests`: `Get_ReturnsPreOrderNodes_WithPathsDepthsAndResolvedOwners`, `Get_NodeShape_IsExactlyTheContracts`, `Get_FiveHundredNodesTenLevels_ReturnsWithinTwoSeconds_EveryNodeResolved`; `DeleteAreaEndpointTests.Delete_Root_Returns409RootIsFixed_RootStaysOwnedByTheFirstTeam`; `OrganizationStructureTests.FindProject_ReturnsFirstTeamRootAndReachability` | **rewrite** without owner fields (the restore test keeps the statuses and the default-team setting) |
| mod-002 US-6 → BR-OT-10 | all of `SetAreaOwnerEndpointTests`, `SetAreaOwnerHandlerTests`; `AreaTests`: `CreateChild_WithOrWithoutOwner_IsAChildOfItsParent`, `CreateChild_OwnerOfAnotherProjectOrDeleted_IsADefect`, `SetOwner_SetsClearsAndIsANoOpForTheSameOwner`, `SetOwner_DefaultArea_IsRefusedNamingTheTeam`, `MarkDefaultOf_RequiresExplicitOwnership`; `CreateAreaEndpointTests`: `Create_WithOwnerOrInheriting_StoresTheExplicitOwnerOrNone`, `Create_OwnerNotAnActiveTeamOfTheProject_Returns404TeamNotFound_CreatesNothing`, `Create_WithAnOwnerTeamBeingDeleted_NoAreaOwnedByADeletedTeam_BothOrders`; `CreateAreaHandlerTests`: `Handle_WithOwnerOrInheriting_StoresTheExplicitOwnerOrNone`, `Handle_OwnerNotAnActiveTeamOfTheProject_IsTeamNotFound_BeforeTheParent`, `Handle_LocksTheOwnerThenTheParent_ThenSavesOnce`; `MoveAreaEndpointTests.Move_ReResolvesInheritedOwners_KeepsExplicitOnes`; `MoveAreaHandlerTests.Handle_ReResolvesInheritedOwners_KeepsExplicitOnes` | **retire** |
| mod-002 BR-M1-14, mod-003 BR-M3-20 (team), mod-006 BR-M4-21, feat-027 BR-T-10 (team) → BR-OT-3, -4 | all of `SetTeamDefaultAreaEndpointTests`, `SetTeamDefaultAreaHandlerTests`, `TeamDefaultAreaTests`; `OrganizationsSchemaTests` «BR-M1-14: a second default area of one team» (`ix_areas_default_of_team_id`), «a default area owned by another team» (`ck_areas_default_owned`); `CreateFromTeamEndpointTests`: `Create_FromATeam_TakesItsDefaultArea_LandsLast`, `Create_FromTheFirstTeam_TakesTheRoot`, `Create_DefaultChangedSinceThePageWasRead_TakesTheCurrentDefault`, `Create_FromTheFirstTeamsBoard_RootAndLastCard` | **retire** |
| | `CreateChildEndpointTests`: `Create_WithATeamAndAParent_TakesTheParentsArea`, `Create_WhileTheParentsAreaIsBeingChanged_WaitsThenTakesTheNewArea`; `CreateFromTeamEndpointTests.Create_TeamNotFound_Returns404_NothingCreated` (adds the other project's team) | **rewrite** onto the team |
| mod-002 BR-M1-18, mod-006 BR-M4-20 → BR-OT-11, -12 | `DeleteTeamEndpointTests`: `Delete_ReownsExplicitAreasToFirstTeam_InheritedFollow_NamesAndPlacesKept`, `Delete_FreesTheTeamName_TheAreaKeepsItsOwn`, `Delete_FirstTeam_Returns409FirstTeamCannotBeDeleted_ChangesNothing`, `Delete_ConcurrentWithReownToIt_DeletionFirst_ReownRefused_NoAreaOwnedByDeletedTeam`, `Delete_ConcurrentWithReownToIt_ReownFirst_BothSucceed_AreaEndsWithFirstTeam`; `DeleteTeamHandlerTests`: `Handle_PassesExplicitAreasToTheFirstTeam_InheritedFollow_NamesAndPlacesKept`, `Handle_DefaultAreaLosesTheMark_FirstTeamsDefaultStaysTheRoot_NothingIsRemoved`, `Handle_FirstTeam_IsFirstTeamCannotBeDeleted_ChangesNothing`, `Handle_FirstTeamRefused_EndsNoMembership`; `TeamTests.Delete_FirstTeam_IsRefused`; `OrganizationsSchemaTests` «a deleted first team» (`ck_teams_first_team_active`); `GetBacklogEndpointTests.Get_DeletedTeam_ItsItemsGoToTheFirstTeamByNumber` | **retire** |
| | `DeleteTeamHandlerTests.Handle_LocksTheTeamForUpdate_ThenReassignsSetBased_ThenEndsTheMemberships_ThenSavesOnce`; `DeleteTeamEndpointTests.Delete_ThenEveryTeamOperationAndOwnerBinding_Returns404TeamNotFound` | **rewrite** (D-16: the first renamed `…LocksAllTheProjectsTeams_ThenReleasesTheOwnedItems_…`) |
| mod-002 BR-M1-13, mod-003 BR-M3-23/-24 (default refusal) | `DeleteAreaEndpointTests`: `Delete_DefaultArea_Returns409IsTeamDefault_NamingTheTeam`, `Delete_FormerDefaultArea_AfterTheDefaultMoved_Returns204`; `DeleteAreaHandlerTests`: `Handle_DefaultLeaf_IsAreaIsTeamDefault_NamingTheTeam_NothingRemoved`, `Handle_DefaultAreaWithAChild_IsAreaNotEmpty_BeforeTheDefault`, `Handle_FormerDefault_AfterTheDefaultMoved_IsDeleted` | **retire** |
| | `AreaTests.EnsureDeletable_RefusesChildrenThenDefault_AllowsAnEmptyLeaf`; `SaveWorkItemEndpointTests.Save_AreaRenamedMovedAndItsTeamDeleted_ItemFollowsTheArea` | **rewrite** (children, then items; the team is unaffected) |
| mod-006 BR-M4-5, -6 → BR-OT-5, -6 | `GetBacklogEndpointTests`: `Get_AreaWithoutOwner_CountsForTheNearestOwner`, `Get_AreaSetOnTheForm_MovesTheItemToTheOtherTeam`, `Get_AreaReOwned_TakesItsItemsAlong`, `Get_AreaMoved_TakesItsItemsAlong`, `Get_ItemBackWithNoReorderMeanwhile_ReturnsToItsPlace` | **retire** |
| | `GetBacklogEndpointTests`: `Get_HoldsTheTeamsItemsOfTheLevel_AndNothingElse`, `Get_ItemPlacedElsewhereMeanwhile_ComesBackLast`, `Get_ItemCreatedFromTheList_IsTheFirstTeamsLastRow`; `GetProjectBacklogEndpointTests.Get_AreasWithAndWithoutAnExplicitOwner_AllOfTheProjectCount`; `BacklogTreeEndpointTests.Get_AnotherTeamsChildren_StandBeneathNoRow` | **rewrite** by owning team |
| mod-002 §§ 3.1–3.3, mod-016 BR-M1-27, mod-006 `defaultArea` → BR-OT-11 | `GetTeamsEndpointTests`: `Get_ActiveTeams_FirstTeamFirstThenDisplayOrder_WithDefaultAreaPaths`, `Get_NewProject_ListsTheFirstTeamAlone_DefaultAreaIsTheRoot`, `Get_ThirtyTwoTeams_ReturnsWithinTwoSeconds_FirstTeamFirst`; `GetTeamEndpointTests`: `Get_FirstTeam_Returns200_DefaultAreaIsTheRoot`, `Get_SecondTeam_Returns200_DefaultAreaIsItsOwnAreaUnderTheRoot`, `Get_DefaultAreaBelowTheRoot_ReturnsItsPathBelowTheRoot`; `GetTeamsHandlerTests`: `Handle_ActiveTeams_FirstTeamFirstThenDisplayOrder_WithDefaultAreaPaths`, `Handle_NewProject_ListsTheFirstTeamAlone_DefaultAreaIsTheRoot`; `GetTeamHandlerTests`: `Handle_FirstTeam_ReturnsItWithTheRootAsItsDefaultArea`, `Handle_SecondTeam_ReturnsItWithItsOwnAreaAsItsDefaultArea`, `Handle_DefaultAreaBelowTheRoot_ReturnsThePathBelowTheRoot`, `Handle_RenamedTeam_ReturnsTheNewNameAndTheSameDefaultArea`; `ReadModelsTests.Teams_ListActive_FirstTeamFirstThenRussianOrder_WithDefaultAreas`; `RenameTeamEndpointTests`: `Rename_FreeName_Returns200_TeamWithItsDefaultArea_NewNameStored`, `Rename_KeepsAreaNamesOwnershipAndDefault`, `Rename_FirstTeam_Returns200_DefaultStaysTheRoot_RootKeepsItsName`; `RenameTeamHandlerTests`: `Handle_FreeName_RenamesTheTeam_AnswersTheTeamWithItsDefaultArea`, `Handle_KeepsAreaNamesOwnershipAndDefault`, `Handle_FirstTeam_IsRenamed_DefaultStaysTheRoot_RootKeepsItsName` | **rewrite** over the new `Team` (D-16: old → new in the commit message) |
| feat-039 BR-PB-6 | `GetProjectBacklogEndpointTests`/`GetProjectBoardEndpointTests` — nothing asserts the absence of a create command on the backend | no backend test |
| kept (mod-001 BR-M2-\*, mod-002 BR-M1-1..3, -20..22, mod-003, mod-006, feat-027, feat-032, mod-016) | every other test | **stays green** |

### 9.1b Found by the scan, not named in the spec (same rules)

| Test | Rule | Fate |
|---|---|---|
| `RepositoriesTests.ReassignExplicitAreas_MovesEveryExplicitAreaAndClearsTheDefault` | mod-002 BR-M1-18 | **retire** |
| `RepositoriesTests.Locks_FindTheRowOnlyInItsOwnContainer`, `.GetNodes_ReturnsEveryAreaOfTheProject_SiblingsInDisplayOrder` | BR-M1-22, BR-M1-12 | **rewrite** (they use `GetFirstTeamAsync` and the owner fields) |
| `OrganizationsSaveChangesTests.SaveAsync_UnmappedIndex_IsRethrownAsIs` | ADR-0011 | **rewrite**: its "unmapped" example is `ix_areas_default_of_team_id`; use another unmapped constraint |
| `OrganizationErrorCatalogTests` (the rows of the four retired codes) | ADR-0005 | **rewrite**: the four become `deprecated`, the two new codes get their rows |
| `OrganizationContainmentTests.AnAreaOrTeamIdOfAnotherOrganizationInABody_IsRefusedAsNotFound_NothingChanges` | BR-M1-22, BR-OT-16 | **rewrite**: the owner and default-area requests go; B's team in a save, in a create and in `PUT …/default-team` joins |
| `OrganizationAddressedRoutes.All` (the inventory) | mod-016 § 4 | two rows removed, two added (§ 9.4) |
| `DeactivateProjectEndpointTests` (the snapshot line with `first=` and `owner=`) | ADR-0026 rules 2–3 | **rewrite** the snapshot: teams, areas, the setting |
| `AddProjectBacklogOrderMigrationTests` | feat-039 | **rewrite the arrangement**: the seeded place needs the item's owning team set; the roll-back passes through both new migrations |

### 9.2 Arrangement-only files and fixtures (no assertion on a retired rule)

They change because a helper, a record or a column they use is renamed or gone; the assertions stay.

`Api.IntegrationTests`: `BoardsTestData` (the area-owner helpers become `SetOwningTeamAsync`; `FirstTeamId` → `TeamId`),
`ProjectBoardsTestData`, `OrganizationsTestData`, `WorkItemsTestData` (`CreateItemAsync`/`CreateAsync` gain the owning
team), `GetProjectBoardEndpointTests`, `ReorderProjectBacklogEndpointTests`, `BacklogTreeEndpointTests`,
`AddTeamMemberEndpointTests`, `GetTeamMembersEndpointTests`, `RemoveMemberEndpointTests`, `RemoveTeamMemberEndpointTests`,
`SearchTeamMemberCandidatesEndpointTests`, `DeactivateOrganizationEndpointTests`, `RestoreOrganizationEndpointTests`,
`RenameProjectEndpointTests`, `RestoreProjectEndpointTests`. `Application.Tests`: `InMemoryOrganizations`,
`FakeOrganizationsReadModels`, `FakeOrganizationsRepositories` (+ new `FakeWorkItemTeamOwnership`,
`FakeTeamBacklogPlaces`), `AddTeamMemberHandlerTests`, `GetTeamMembersHandlerTests`, `RemoveMemberHandlerTests`,
`RemoveTeamMemberHandlerTests`, `DeactivateOrganizationHandlerTests`, `DeactivateProjectHandlerTests`,
`RestoreOrganizationHandlerTests`, `RenameOrganizationHandlerTests`, `RenameProjectHandlerTests`. `Domain.Tests`:
`OrganizationsDomainTestData`, `ProjectTests`, `AreaTests`, `AreaTreeTests`. `Infrastructure.Tests`:
`OrganizationsDatabaseFixture` (`CreateStructureAsync`), `OrganizationStructureTests`, `ReachabilityTests`,
`OrganizationMembershipsSchemaTests`, `MembershipRepositoriesTests`, `MemberReadModelTests`,
`AddOrganizationAccessMigrationTests`. Their old helpers stay until phase 3 (expand and contract, § 10.1), and a lane's new
tests use only the new helpers phase 1 adds.

### 9.3 Story coverage (backend half; every scenario has a test)

FE = the screen is `plan-frontend.md`'s. Classes: `SaveOwningTeamEndpointTests` (S), `CreateOwningTeamEndpointTests` (C),
`SetDefaultTeamEndpointTests` (D), `OwnedViewsEndpointTests` (V), `ReorderVersusTeamChangeEndpointTests` (R),
`TeamFilterEndpointTests` (F), `DeleteTeamEndpointTests` (X), `AreasOwnNothingEndpointTests` (A),
`OwningTeamMigrationTests` (M, Infrastructure.Tests).

**US-1**

| Scenario | Tests |
|---|---|
| shown wherever the item is | S `Get_Create_Save_AnswerTheTeamIdAndName_OrNone`; FE the band |
| picker offers none, then teams by name | `GetTeamsEndpointTests.Get_ActiveTeams_ByNameIgnoringCase_NoDeletedNoOtherProjectNoOtherOrganization`; FE the order of «Без команды» |
| a reader cannot change it | S `Save_ReaderSetsTeam_Returns403_ChangesNothing` |
| changing the team changes nothing else | S `Save_SetTeam_ChangesTeamAndChangedAtOnly_KeepsProjectOrderAndPlaces` |
| not sent keeps; none is a choice; none again is a no-op | S `Save_TitleOnlyWhileTeamChangedMeanwhile_KeepsTheNewTeam`, `Save_NoTeam_ThenNoTeamAgain_ChangedAtDoesNotMove`, `Save_EmptyTeamWrapper_IsNone` |
| a team the project does not have (4 rows) | S `Save_TeamRefusals_StoreNothing` (theory: other project 409; deleted, deactivated project, unknown, malformed 404; the title is not stored) |
| another organization's team answers as unknown | S `Save_ForeignTeam_AnswersExactlyAsUnknown` (status and bytes equal) |
| the address is answered before the team | S `Save_AnswerOrder_ItemNotFound_Then403_Then409` |
| a child may have another team | S `Save_ChildAndParentTeams_AreIndependent` |
| the assignee need not belong to the team | S `Save_AssigneeOutsideTheTeam_IsAccepted`, `Save_TeamChange_KeepsTheAssignee` |
| a person moving moves no item | `AddTeamMemberEndpointTests.Membership_Moves_MoveNoItem`; `RemoveTeamMemberEndpointTests` alongside |
| saving the area changes no team | S `Save_AreaChange_KeepsTheTeam` |
| the team survives every other write | S `Save_StateByFormAndCardMoveAndChecklistTick_KeepTheTeams` |

**US-2**

| Scenario | Tests |
|---|---|
| outline: default per project | C `Create_NoTeamField_TakesTheProjectDefault` (theory: named; only team; two, none named; named then deleted) |
| the dialog says where / offers teams | FE; C `Create_NewItem_TakesTheRootArea_WhateverTheTeam` |
| the switcher's team is not a team surface | FE (the server reads no switcher) |
| the dialog waits for the teams | FE |
| the dialog creates with the team it shows | C `Create_DefaultNamedWhileTheDialogIsOpen_KeepsTheSentTeam`, `Create_FieldOmitted_TakesTheDefaultAsStoredAtSave` |
| from a team's surface | C `Create_FromTeamSurface_OwnedByTheTeam_LastRowOfBothBacklogs_NoPlaceWritten`, `…_OtherTeamOrNone` |
| a project view starts with the default | C as the outline; FE the command |
| an empty project view | FE |
| outline: a direct creation naming a team | C `Create_TeamRefusals_NothingCreated_NoNumberConsumed` |
| a team gone while the dialog was open | C `Create_TeamDeletedBeforeSubmit_Refused_ThenOtherTeamCreates_ReplayAnswers200OwnedByNone` |
| a child starts with its parent's team | C `Create_Child_TakesParentTeam_ElseProjectDefault_OnAnySurface` |
| the parent's team is read when the child is saved | C `Create_ChildWhileParentTeamChanges_TakesTheTeamAsStored_BothOrders` (gate) |
| a child is never refused for a team | C `Create_ChildVersusParentTeamDeletion_BothOrders_NeverRefused`, `Create_ChildWithATeamInTheBody_TeamIsNotRead` (gate) |
| who creates | C `Create_Reader_Returns403_CreatesNothing` |

**US-3**

| Scenario | Tests |
|---|---|
| the administrator chooses the default | D `Name_Team_MarksItInTheTeamsRead_OthersSeeTheMarks`, `Name_Member_Returns403`; FE the dialog |
| outline: refusals and roles | D `Name_Refusals_SettingUnchanged` (theory: 409, 404, 403, deactivated project 404) |
| two teams and none named; one team; named wins | `GetTeamsEndpointTests.Get_DefaultMark_NamedOnlyTeamNone`; `ProjectDefaultTeamTests.MarkOf_*` (domain) |
| adding a second team ends the inference | `AddTeamEndpointTests.Add_SecondTeam_EndsTheOnlyTeamMark` |
| outline: losing the named team | X `Delete_NamedTeam_ClearsTheSetting`; D `Name_NoTeam_ClearsTheSetting` |
| the default affects the creation only | D `Name_MovesNoItem_ChangesNoViewOrFilter` |
| survives a deactivated project | D `Setting_SurvivesDeactivateAndRestore` |
| a write racing the deletion | D `Name_VersusDeleteTeam_BothOrders_NeverNamesADeletedTeam` (gate) |

**US-4**

| Scenario | Tests |
|---|---|
| each team holds its own; the project all | V `Views_TeamsAndProject_HoldTheirItems_EachOnce_NoTeamItemOnNoTeamView` |
| membership no longer follows areas | V `Views_AreaRenameMoveAndItemAreaSave_MoveNoItem` |
| shown on every row and card | V `Rows_AndCards_CarryTheTeam_OrNone` |
| every state but Removed | V `Views_HoldEveryStateButRemoved_PerLevel` |
| children follow the view | V `Backlog_Children_AreOnlyTheViewsOwn_ParentOfAnotherTeamLeavesATopLevelRow` |
| changing the team moves the item | S `Save_TeamChange_LeavesTheTeamViewAndLandsLastUnplaced_ProjectPlaceKept`; R `Reorder_AfterTheItemLeft_IsStale` |
| outline: a reorder against a team change | R `Reorder_VersusTeamChange_FourOrders` (gate: reorder first/save first × moved item/another member) |
| comes back unplaced | S `Save_TeamBack_ComesBackUnplaced_NoFormerPlace` |
| the two orders are independent | R `TeamAndProjectOrders_AreIndependent` |
| a card move writes only the state | V `CardMove_WritesOnlyTheState_BothPlacesKept_ShownOnceOnEachView` |
| a created item is unplaced on both | C `Create_IsUnplacedOnBothOrders` |
| large views | `OwnedViewsVolumeTests` (5 000, 2 s); `OwningTeamStatementCountTests` |

**US-5**

| Scenario | Tests |
|---|---|
| the list shows the team | F `List_RowsCarryTheTeam`; FE the column |
| filtered by one, several, none | F `List_TeamFilter_OneSeveralAndNone_MatchAnyValue_AllTypesAndStates` |
| pages from the first page | F `List_FilterPaging_CountAndLastPage` |
| matches nothing | F `List_FilterWithNoMatch_IsEmpty_CountZero`; FE the frame |
| project backlog filtered | F `ProjectBacklog_Filter_TreeOverMatchingOnly_OrderNumbersVisibleRows` |
| board filtered; the checklist not narrowed | F `ProjectBoard_Filter_ColumnsFollow_ChecklistNotNarrowed` |
| the three surfaces agree | F `ThreeSurfaces_AgreeForEveryCombination` (theory over MARS, VENUS, none) |
| a filtered backlog reorders against the full list | F `ProjectReorder_WhileFiltered_HiddenItemsKeepTheirRelativeOrder` |
| a value that is not a team of the project | F `Filter_NonTeamValues_FourIdenticalAnswers_TotalZero` |
| the choice lives with the visit | FE; F `Filter_ChangesNothingStored` |
| everyone who reads may filter | F `Filter_Reader_SameAsMember` |
| a filter keeps its time budget | `ListFilterVolumeTests`, `ProjectViewsFilterVolumeTests`, `OwningTeamStatementCountTests` |

**US-6**

| Scenario | Tests |
|---|---|
| the tree shows names, nothing owned | A `Tree_NodeShape_HasNoOwnerFields` (`GetAreaTreeEndpointTests` rewritten); FE |
| a new area has a name and a place | A `CreateArea_BodyWithOwnerTeamId_IsIgnored_NoOwnerStored` |
| the owner and default-area writes are gone | A `RetiredRoutes_Answer404_NothingChanges` (both) |
| moving or renaming an area moves no item | A `MoveAndRename_MoveNoItem_KeepTeamAndChangedAt` |
| deleting an area: children and items alone | `DeleteAreaEndpointTests.Delete_FormerDefault_Returns204` (rewrites the retired pair), `.Delete_Root_Returns409RootIsFixed`; A `Delete_NoAnswerNamesATeamDefault` |
| the item's area is a classification path | A `NewItem_TakesRoot_ChildTakesParentsArea`, `Save_ChildArea_ChangesNoTeam` |
| a team has no area | `GetTeamsEndpointTests`/`GetTeamEndpointTests` shape; FE |
| adding or renaming a team touches no area | `AddTeamEndpointTests.Add_NameOfAnExistingAreaUnderTheRoot_Succeeds_NoAreaCreated`, `RenameTeamEndpointTests.Rename_TouchesNoArea` |
| a project is still created with a team and a root | `CreateProjectEndpointTests.Create_TeamOrdinary_CreatorOnlyMember_RootUnowned`; FE the helper |
| the upgrade keeps every item on its area's team | M `Upgrade_MapsEveryItemToTheTeamThatOwnedItsArea` (root owner, inherited, nothing owned up the tree; `changed_at` unmoved; no default named; places that name the owner kept; areas keep name, parent, items) |
| a stale place is dropped | M `Upgrade_DeletesStaleTeamPlaces_ItemListsUnplaced_ThenSetBackIsNotSecond`, `Upgrade_LogsTheCounts` (Notice handler on the connection) |
| a deactivated project and a deleted team are upgraded | M `Upgrade_DeactivatedProjectAndDeletedTeam_NoItemOwnedByADeletedTeam` |

**US-7**

| Scenario | Tests |
|---|---|
| listed by name, none first | `GetTeamsEndpointTests.Get_ByNameIgnoringCase_NoFirst`; FE |
| any team renamed or deleted | X `Delete_TheTeamCreatedWithTheProject_IsAllowedWhenAnotherRemains`, `RenameTeamEndpointTests.Rename_AnyTeam` |
| the switcher's fallback | FE |
| the confirmation counts | `GetTeamOwnedItemCountEndpointTests.Count_EveryState_AtTheMomentOfTheRead`, `Count_NonAdministrator_Returns403`; FE the text |
| confirming sends the items to none | X `Delete_ReassignsEverythingTheTeamOwns_ClearsSetting_EndsMemberships_FreesName_AddressAnswers404` |
| the last team cannot be deleted | X `Delete_LastTeam_Returns409_ChangesNothing`; `TeamTests.Delete_LastTeam_IsRefused` |
| two deletions that would empty a project | X `Delete_TwoLastTeamsAtOnce_OneDoneOneRefused` (gate) |
| outline: a write racing the deletion (4 writes) | X `Delete_VersusSaveCreateNameReorder_BothOrders_NoItemOwnedByADeletedTeam_No5xx` |
| a child beneath a story of the team | X (and C) `Delete_VersusChildCreation_BothOrders` |
| a deletion in a deactivated project | X `Delete_InDeactivatedProject_Returns404_ThenRestoreKeepsEverything` |
| a big deletion, all or nothing | `DeleteTeamVolumeTests.Delete_5000OwnedItems_Within5Seconds`, `…_FailureAtEachStep_RollsEverythingBack` (a test decorator throws at each of the six steps) |

### 9.4 Cross-cutting tests

- **Statement counts** (`Infrastructure.Tests`, the `ProjectBacklogStatementCountTests` counter; each pinned, small = large):
  new `OwningTeamStatementCountTests`: the list (unfiltered, two teams, `none`) for 50 items and 2 teams against 5 000 items
  and 50 teams with a four-level tree; one item; a team's backlog tree (stories, epics) and board for 50 and 2 000 per
  level; a team reorder; the teams list and one team (2 and 50 teams); the owned count; the deletion (50 and 5 000 owned items,
  2 and 50 teams); a save that changes the team; a child creation. `ProjectBacklogStatementCountTests` is re-pinned and gains
  the filtered backlog and board (equal to the unfiltered counts: **a filter adds no statement**). The pins are the first green
  run's, as that test says.
- **Containment** (`OrganizationContainmentTests`): the route table is driven by `OrganizationAddressedRoutes.All`, so the new
  routes join the path-id theory by themselves; add the body cases: B's team in A's save, in A's create, in A's
  `PUT …/default-team`, and B's team id as A's list filter (equal to an unknown id, bytes included); the "refused requests
  change nothing of the other organization" state string gains the owning teams and the setting.
- **Route inventory**: `OrganizationAddressedRoutes.All` (two rows out, two in; 47 stays 47), `OrganizationAccessInventoryTests`,
  `EndpointCategoryInventoryTests`, `ForbiddenErrorCodeTests` (a reader's and a member's writes).
- **Contracts**: `OrganizationErrorCatalogTests`, `ErrorCatalogCompletenessTests` (the deprecated entries are not orphans),
  `ErrorCodesReferenceTests` (`MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests`, then commit
  `docs/error-codes.md`).
- **Architecture**: `VerticalSliceRules` and `OnionDependencyRules` must stay green with the two new root contracts; no new rule.
- **Migration** (`Infrastructure.Tests`, its own database): `OwningTeamMigrationTests` (M) migrates to the migration before the
  first, seeds the legacy shape with SQL (the area owners, the markers, the places, a deactivated project, a deleted team), runs
  both migrations and asserts § 4.2–§ 4.4 and US-6's two scenarios; rolls both back (with places and owners present) and applies
  them again; `Model_HasNoPendingChanges_AfterTheMigrations`; `OwningTeamSchemaTests`: the item key refuses another project's
  team (`23503`), the place key refuses a place of another team and of an ownerless item and an update of the owner under a
  place, the default key refuses another project's team, `Project_NameAndClearDefaultTeam_RoundTripsThroughEf`, the unique
  index and the team-view index exist.
- **Volume**: `BacklogVolumeTests`, `BacklogTreeVolumeTests`, `ProjectBacklogVolumeTests` re-seed by owning team; new
  `OwnedViewsVolumeTests` (5 000 for the team and the project), `ListFilterVolumeTests` (a page of 50 on a project of 5 000,
  filtered or not, within 2 s), `DeleteTeamVolumeTests` (5 000 within 5 s).
- **Domain**: `ProjectTeamsTests` (every row of api-contract § 0.2), `ProjectDefaultTeamTests`, `WorkItemOwningTeamTests`
  (set, clear, equal is a no-op and keeps `ChangedAt`, create with a team), `WorkItemFilterTests` (every raw form),
  `TeamTests` (create; delete with the guard), `ProjectTests` (ordinary team, unowned root, `NameDefaultTeam`), the rewritten
  `AreaTests`, `AreaTreeTests`, `WorkItemHierarchyTests` (a child's team).

### 9.5 Concurrency catalogue

All use the existing `CommitGate` (a request holds its locks at the gate) and `PgLocks.WaitUntilBlockedAsync`; each asserts both
orders, **no 5xx**, no item owned by a deleted team, no setting naming a deleted team, no place naming a team that does not own
its item. Rows: save × delete, create × delete, default naming × delete, reorder × delete, **child create × delete** (the
D-3 deadlock if the mode is wrong), reorder × team change (four combinations), two deletions, child create × parent's team save,
save × save of one item (BR-M3-25, unchanged).

## 10. Order of work

### 10.1 Expand and contract (green at every commit)

Each commit builds and its tests pass (`scripts/verify.sh`). Old members stay until nothing uses them.

| # | Commit subject (`mod(workitems): … (mod-017)`) | Content |
|---|---|---|
| 1 | `domain rules for the owning team` | `ProjectTeams`, `ProjectDefaultTeam`, `WorkItemFilter`, `TeamNotInProject` and `TeamLastTeamCannotBeDeleted` with their catalog rows, the four retired codes `deprecated`, `docs/error-codes.md` regenerated, `OrganizationErrorCatalogTests`; domain tests |
| 2 | `owning team migration and model` | migration 1, `WorkItem.OwningTeamId`, `Project.DefaultTeamId`, `WorkItem.Create`/`Save` with the team, configurations; `OwningTeamMigrationTests` (the mapping half), `OwningTeamSchemaTests`, the model test |
| 3 | `owner-based infrastructure` | `IOrganizationStructure.DefaultTeamId`, `IWorkItemRepository` locks, `IWorkItemTeamOwnership`, `ITeamBacklogPlaces`, `WorkItemFilterPredicate`, the read models' `team` and filter, `TeamReadModel` mark (expand: the old fields stay), `LockProjectTeamsAsync`, `ClearDefaultTeamAsync`, the new test helpers |
| 4 | `endpoints and contracts` | the three endpoint files in full, the request records, DI, the inventory rows |
| 5 | `save a work item's owning team` (reference) | `SaveWorkItem` end to end: handler, command, binder, place drop, `SaveOwningTeamEndpointTests`, concurrency rows |
| 6 | `name the default team and count a team's items` | `SetDefaultTeam`, `GetTeamOwnedItemCount`, handler, endpoint and Infrastructure tests |
| 7..14 | one commit per lane (§ 10.2), made by the orchestrator after a wave's single verification run | |
| 15 | `retire area ownership and the first team` | migration 2 and its tests; the removals of the old members, records and helpers (§ 9.2 sweep); the retired tests of § 9.1; `OrganizationAddressedRoutes` rows |
| 16 | `statement counts, volumes, containment` | § 9.4 |
| 17 | `owners' notes` | the P-10 notes (§ 11) |

Migrations apply on a clean database and again (04-backend.md DoD): `OwningTeamMigrationTests` rolls back and re-applies.

### 10.2 Lanes (04-backend.md § 4c)

**Phase 1 (sequential)** is commits 1–6: domain, both migrations' first, the shared infrastructure, the three endpoint files, DI,
the error codes, and **three use cases end to end** — the reference `SaveWorkItem` (the binder, the place drop, the gate tests
the others copy) plus the two small new routes, so every route in the contract exists. Phase 1 may make minimal compile
edits inside a lane's folder where a shared record changed; the lane then replaces its zone wholesale.

**Phase 2** — a lane writes exactly: `src/backend/Mustask.Application/Features/<Module>/<UseCase>/`,
`src/backend/tests/Mustask.Application.Tests/Features/<Module>/<UseCase>/`, and the named `<UseCase>EndpointTests.cs` (plus
the extra test files listed). A lane runs no suite; the orchestrator runs one at the end of a wave and commits.

| Lane | Use cases | Endpoint-test files in its zone | Wave |
|---|---|---|---|
| L3 | `DeleteTeam` | `DeleteTeamEndpointTests`, `DeleteTeamVolumeTests` | 1 |
| L4 | `ReorderBacklog` | `ReorderBacklogEndpointTests`, `ReorderVersusTeamChangeEndpointTests` | 1 |
| L1 | `CreateWorkItem` | `CreateWorkItemEndpointTests`, `CreateOwningTeamEndpointTests`, `CreateChildEndpointTests`, `CreateFromTeamEndpointTests` | 1 |
| L2 | `GetWorkItems`, `GetWorkItem` | `GetWorkItemsEndpointTests`, `GetWorkItemEndpointTests`, the list half of `TeamFilterEndpointTests` | 2 |
| L5 | `GetBacklog`, `GetBoard` | `GetBacklogEndpointTests`, `GetBoardEndpointTests`, `BacklogTreeEndpointTests`, `OwnedViewsEndpointTests` | 2 |
| L6 | `GetProjectBacklog`, `GetProjectBoard` | `GetProjectBacklogEndpointTests`, `GetProjectBoardEndpointTests`, the views half of `TeamFilterEndpointTests` | 2 |
| L7 | `GetTeams`, `GetTeam`, `AddTeam`, `RenameTeam`, `CreateProject` | their endpoint tests, `CreateProjectEndpointTests` | 2 |
| L8 | `CreateArea`, `MoveArea`, `RenameArea`, `DeleteArea`, `GetAreaTree`; delete `SetAreaOwner`, `SetTeamDefaultArea` | their endpoint tests, `AreasOwnNothingEndpointTests` | 2 |

Wave 1 first because it holds the concurrency-heavy slices; the new test helpers from phase 1 are their only shared input.
Lanes that need something shared escalate, they do not negotiate.

**Phase 3** (orchestrator): commits 15–17 — the contraction, the cross-cutting tests, the notes.

## 11. Risks, MR notes, and what is owed

- **Risks.** (1) D-3 is a departure from ADR-0028 rule 8's text; the child-creation test is the proof and the morning's question.
  (2) The composite key `(id, default_team_id)` over EF: the schema test settles it, and D-11's SQL is the fallback.
  (3) A large wave of renamed test names (D-16): the commit messages carry old → new. (4) The locking read of D-10 is raw SQL
  by reasons of row locking (backend.md "Raw SQL"), parameterized.
- **The MR names:** the data dropped (§ 4.4); that no project gets a named default and a new item from the list in a
  multi-team project starts «Без команды»; the retired and rewritten tests (§ 9.1); the broken E2E specs —
  `21-organizations.spec.ts` (area owner and default-area copy) and any spec that places an item on a team through an area; the
  deprecated codes; the clarification of ADR-0028 rule 8 (D-3).
- **Owed to the orchestrator (P-10 notes in each owner's `status.md`, not written here):** mod-002 api-contract §§ 2.3, 3.1–3.3,
  3.5, 3.6, 4.1, 4.2, 4.4–4.6, 5, 6, 7; mod-003 §§ 1.1, 1.2, 2.1, 2.2, 2.4, 4, 6; mod-006 §§ 1, 2.1, 2.3, 3, 7; mod-016 §§ 3.1,
  3.3, 4.1 (rows 19, 25) and the route inventory; feat-027 (a child's team, BR-T-1, -4, -5); feat-039 (BR-PB-6); mod-015 and
  mod-016 US-7 (the switcher's fallback). `feat-028`'s status: the last-team guard and the two codes; `feat-043`'s: the default-team
  command and the team filter move with their pages; `feat-015` already carries "records owning-team changes".
- **For the frontend plan:** the contract is fixed; `RefusalField` gains `'team'` and `placeRefusal` maps
  `organizations.team.not_found` / `.not_in_project` on an item write to it (design § 12); the wire is the wrapper (D-2); the
  teams list carries `defaultMark`; the deletion's count is the new route; the filter is `teamId` repeated with `none`.
- **Security pass (later, ≤ 2 delta reads):** every new route declares an organization code; team ids and filter values are parsed
  as GUIDs and bound as parameters; the team lock is tenant-scoped; a foreign id answers as an unknown one; the new count route
  reveals only the number to a holder of `organizations.team.write`.
- **Open questions, decided under the night delegation** (for the human): D-1 two migrations, D-2 the wrapper, D-3 the
  lock mode and the ADR clarification, D-5 deprecate rather than delete the four codes.

## 12. As built — deviations from this plan (backend pass)

What differs from the text above, and why. Each is a decision of the implementation; none changes the API contract.

1. **One more repository member.** `ITeamRepository.GetMarkAsync(Team)` (one statement: the named setting and the count of active
   teams, through `ProjectDefaultTeam.MarkOf`) — `RenameTeam` answers the team with its mark (api-contract § 1.3) and holds only
   that team's lock. § 6 did not list it.
2. **Members removed with their only callers** (beyond § 6's list): `ITeamRepository.LockForUpdateAsync`,
   `IAreaRepository.LockManyForUpdateAsync` and `LockDefaultOfAsync`, `IOrganizationStructure.FindAreaOwnerAsync` with
   `AreaOwnerInfo`, `TeamInfo.IsFirstTeam` and `DefaultAreaId`, `Team.CreateWithArea` / `NewTeam`, `Area.SetOwner` /
   `MarkDefaultOf` / `ClearDefault`, `TeamDefaultArea`, `AreaTree.ResolveOwner` / `ResolvedOwner`, and the four
   `OrganizationErrorCodes` constants (the catalog entries stay, `deprecated`).
3. **`AreaTreeRow` keeps `ToNode()`** and shrinks to `(Id, ParentId, Name)`; `AreaNode` is `(Id, ParentId, Name)` too.
4. **The place key has no `ON DELETE` clause** (plain `NO ACTION`, as § 4.3 wrote it): a work item's removal still takes its
   place through the older cascade key on `work_item_id`, and a change of the owner under a place is refused with `23503` — the
   reason `DropForItemAsync` / `DropForTeamAsync` run before the write.
5. **Migration 2's log lines** are two `RAISE NOTICE`s: `mod-017: N stale team places deleted; M team places kept` and
   `mod-017: A area owners, B default areas and C first-team markers dropped`. `Down` restores the markers and the checks and
   drops the place key; it recreates no explicit owner and no other team's default area (OQ-4 A).
6. **The failure test is stronger than § 9.3 said.** `DeleteTeamAtomicityTests.Delete_FailureAtEachStatement_RollsEverythingBack`
   makes every statement of the deletion fail in turn (an interceptor, not a decorator at six steps), so the six steps and the
   statements between them are all covered; the assertion is the same: nothing changed.
7. **Statement counts pinned on the first green run** (`OwningTeamStatementCountTests`; small = 50 items per level and 2 teams,
   large = 1 250 per level and 50 teams): list 7, team backlog 11, team board 13, team reorder 15, teams list 1, owned count 3,
   deletion 9; the filtered project backlog and board take the statements of the unfiltered. A statement of the assignees' reads
   is skipped when a result has no assignee, so the seed gives every slice one and the numbers do not depend on the data.
8. **Test helpers.** `OwningTeamTestData.SetOwningTeamAsync` drops the item's team place when the team changes (as a save
   does), because the place key refuses the change under a place; `OrganizationsTestData` loses `SetTeamDefaultAreaAsync` and
   the owner parameter of `AddAreaAsync`, and gains `SetDefaultTeamAsync`; `OrganizationStructure.FirstTeamId` became
   `ProjectTeamId` (the helpers' parameter name `creatorJoinsFirstTeam` is kept: a name, not a rule).
9. **Renames (D-16) are listed in the commit messages** of the contraction (old → new), and so are the retired tests with their
   rule.
