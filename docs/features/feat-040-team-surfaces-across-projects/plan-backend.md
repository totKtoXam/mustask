# Plan — backend (feat-040-team-surfaces-across-projects)

Stage 4a (2026-10-18), size L, deep pass, written before any code. Inputs: `spec.md` and US-1..US-7, `design.md` § 14, `review.md`,
ADR-0024 § 2, ADR-0026, ADR-0027, ADR-0028 (rules 3, 5, 8, 9, 13, § 7), ADR-0029 § 7, ADR-0030 (rules 2, 6), doc-013 BR-NE-5/-8/-9,
mod-006, mod-017, feat-028, feat-039 contracts and the code they left (`Features/Boards`, `Abstractions/Boards`, `Infrastructure/Boards`,
`Infrastructure/WorkItems/WorkItemSets`, `Infrastructure/Organizations`, the Boards, WorkItems and Organizations tests). The contract this plan
fixes is [api-contract.md](api-contract.md). `origin/main` is merged in (mod-005 and feat-028 are in this tree; no conflict).

**Lanes: none** (04-backend.md § 4c). The three team slices are one change to the same membership, repository and ordering; the rest is
a handful of small additive edits, below the "four use cases with disjoint write zones" bar. **ADR: none** — every decision here is inside
ADR-0028/0029/0030 and `docs/conventions/backend.md`; no departure from a convention. **Nothing for the human** — see § 13.

## 1. What the backend does for this line (summary)

1. **Move** the team's backlog, reorder and board to `/api/organizations/{o}/teams/{t}/…`; **retire** the three project-nested routes (404
   `http.not_found`, no alias). Slices keep their names (`GetBacklog`, `GetBoard`, `ReorderBacklog`) and lose their `ProjectId`.
2. **Membership across projects**: the items T owns, in the caller's organization's active projects, by level and state — one statement.
3. **Project filter**: repeated `projectId`, the shared filter's second dimension (`WorkItemFilter`), applied in the same statement.
4. **`projects`** on both reads (T's linked active projects); **`project`** on every row, child and card of **every** backlog and board
   (team and project views) and on the `WorkItem` detail.
5. **One team order** (BR-TS-6): creation time, project key, number; placed first by position.
6. **Reorder over the whole list**: team → members (joined to project status) → order row; the departed-place drop spans the
   active projects and keeps a deactivated project's places (BR-TS-7).
7. **The caller as a required argument** of the team read model (BR-TS-13, ADR-0029 § 7) — and the organization as its tenant predicate.
8. **tech-011 m2** (a read that meets T's deletion): the team is read again after the reads. **m3**: a containment test on every route.
9. **Switcher data**: `isMember` on `GET /teams` (additive). **Card**: `project` on `WorkItem` (additive); no new route.
10. **One index** (`owning_team_id, type_id`); no table, column or row changes (BR-TS-14).

Unchanged and reused: creation (`POST /projects/{p}/workitems` + `team`), card move (`PATCH`), parent write (`PUT …/parent`), the project
views' routes and answers, every error code and permission code, the board layout, the reorder body.

## 2. Facts found in the code (what the plan stands on)

| Fact | Where | Consequence |
|---|---|---|
| The team views read at `/projects/{p}/teams/{t}`; the membership is `item.ProjectId == p && OwningTeamId == t && type && state` | `BoardEndpoints.cs`, `BacklogMembership.cs` | membership becomes team-led; the project join carries status and tenant |
| `StackRank.Order` = place, `CreatedAt`, number; a team's entries carry no `CreatedAt` | `Domain/Boards/StackRank.cs`, `BacklogReadModel.ItemsAsync` (`ordersByCreation`) | add the project key; a team read always supplies `CreatedAt` and the key |
| The reorder locks team (project-resolved org), ONE link, members, order row; drops departed places of THIS project | `BacklogRepository.cs` | no link lock (the member locks order it with an unlink); the team by organization; the drop spans active projects |
| `owning_team_id` is a key column (unique `(id, owning_team_id)`), so a save locks the row `FOR UPDATE`, conflicting with the reorder's `FOR KEY SHARE` | `WorkItemConfiguration`, `BacklogRepository` comment | member locks stay the serializer against ownership changes |
| The place's keys: `fk_backlog_places_work_items_work_item_id`, `fk_backlog_places_work_items_owner` `(work_item_id, team_id)` → `(id, owning_team_id)` | migrations `RetireAreaOwnership` | a place can never name a team that does not own its item; a violation maps to a stale reorder |
| `IOrganizationStructure.FindTeamAsync(org, team)` (null for a foreign team, `IsReachable`) and `ListLinkedProjectsAsync(org, team)` (reachable linked projects, name order) exist | `OrganizationStructure.cs` | no new structure read |
| `GET /projects` returns `{id, name, key}` of the active projects; `GET /teams/{t}/projects` returns `{id, name, key}` | `GetProjects`, `GetTeamProjects` | a card by address needs no new route (BR-TS-4) |
| `GET /teams` returns `{id, name, description, projects}`; no membership flag | `GetOrganizationTeams`, `TeamReadModel` | the interim switcher needs `isMember` |
| `CreateWorkItemHandler` already judges the project (404 deactivated), the explicit team (404 deleted, 409 unlinked) and the link locks | `CreateWorkItemHandler.cs` | US-5 needs tests, not code |
| `ReadChecklistsAsync(projectId, …)`, `FindLiveParentsAsync(projectId, …)` and the assignee-membership read take ONE project | `BacklogReadModel.cs`, `ProjectMembers.cs` | take the organization instead (a project's parents/children are in its organization) |
| Unmatched routes answer 404 `http.not_found` through `UseUnmatchedRouteAsNotFound` | `UnmatchedRouteMiddleware.cs`, `RetiredTeamRoutesEndpointTests` | retiring a route is deleting its `Map…`; the answer is the platform's |
| Tests build every team URL through `BoardsTestData.Team(world, teamId)` | `BoardsTestData.cs` | one helper edit moves most tests; the rest are named in § 10 |

## 3. Plan decisions (PD-n)

| # | Decision | Why |
|---|---|---|
| PD-1 | Slice folders keep their names; `GetBacklogQuery(OrganizationId, TeamId, Level, ProjectIds?)`, `GetBoardQuery` likewise, `ReorderBacklogCommand(OrganizationId, TeamId, Level, ItemId, Position, AfterItemId)`. Responses: `BacklogResponse(Team, Projects, Level, Items)`, `BoardResponse(Team, Projects, Level, Columns)`. | the module's original names; the project views are `GetProject…` already; fewer renames, same spec/test names |
| PD-2 | **Membership by ownership, tenant and project status in one statement**: `work_items ⋈ projects` with `owning_team_id = T ∧ project.organization_id = caller.org ∧ project.status = 'Active' ∧ type ∈ L ∧ state ∈ shown [∧ project ∈ filter]`. No project-id list from the handler, no link join. | one snapshot for membership and status (the reorder race of BR-TS-7 needs it); the owner key already guarantees "linked" (BR-OL-6) |
| PD-3 | `CallerAccess(OrganizationId, UserId, Role)` (`Application/Abstractions/Organizations`), built by the handler from `IOrganizationAccess.Role` + `ICurrentUser.SubjectId`, is a **required** parameter of `ReadTeamAsync` / `ReadTeamTreeAsync`. Its organization is the tenant predicate of the statement; mod-019 changes what it carries, feat-041 adds the set form — no new signature (BR-TS-13). The scopes carry no organization. | ADR-0029 § 7; makes m3 a property of the query, not only of the handler |
| PD-4 | `StackRankEntry` and `BacklogTreeEntry` gain an optional `ProjectKey`; `StackRank.Order` = `Place is null`, `Place`, `CreatedAt`, `ProjectKey` (ordinal), `Number`. A **team** read supplies `CreatedAt` and the key on **every** row, the «Задачи» level included (a deliberate departure from the project read, which leaves task rows by number: a team's tasks of two projects would tie on number). | BR-TS-6, fate row "feat-027 BR-T-6 … replaced"; project rows have one key, so nothing changes for them |
| PD-5 | The project dimension is two `init` members of `WorkItemFilter` (`ProjectsSet`, `ProjectIds`) with `WorkItemFilter.OfProjects(raw)`; `WorkItemFilterPredicate.Apply` narrows by it. The list and the project views never set it. | "the shared specification's second dimension"; no change to the positional constructor; mod-020 is frontend only (its queue line), so the API form (repeated query value, invalid ignored, set-but-empty selects nothing) is founded here and is mod-017's pattern |
| PD-6 | `project` on a row is `WorkItemProjectView(Id, Name)` (in `IWorkItemReadModel.cs` next to `WorkItemTeamView`); `BacklogItemRow` gains `Project` and an internal `ProjectKey`. `WorkItemView` gains `Project`. Checklist entries and `WorkItemRef` do not. | design § 14; a rename shows at once; the key is the tie-break, never on the wire |
| PD-7 | `projects` on a team read = `IOrganizationStructure.ListLinkedProjectsAsync(org, team)` (reachable, by name), mapped to `BoardProjectView`; not narrowed by the filter. | one existing read; the filter's options, the empty state and the create command read the same list |
| PD-8 | **Reorder locks** (ADR-0028 rule 8, steps 2, 5, 6): team `FOR SHARE` (by organization) → active check → members `FOR KEY SHARE OF item` ordered by item id, in the membership SQL with the project status → order row. **No project row and no link is locked.** | BR-TS-7: a deactivation/restore is not serialized with the reorder. A link is the binder of a write that *sets* a team, which a reorder does not. An unlink (links `FOR NO KEY UPDATE`, then the owned items `FOR UPDATE` in id order, then it drops the places and reassigns), a save that changes a member's team and a Delete all lock the item rows, so the member key shares order them with the reorder (reorder first: they wait, then drop what it wrote; they first: its locking read no longer finds the item); a team deletion waits on the team row. Every party takes item locks in id order, so there is no cycle; also locking up to 200 links would only widen the window |
| PD-9 | **The departed drop is by predicate, in SQL**: delete T's places on level L whose item is in an *active project of the organization* and is **not a member by owner, type and state** (§ 6). No `held − ids` in memory. | the in-memory `held − ids` would drop the kept place of a restored project's items if the project is restored between the locking read (inactive, excluded) and the `held` read (active); a predicate made of locked, immutable-under-lock facts cannot |
| PD-10 | tech-011 m2: handler order = org → team (404) → level (404) → reads → **team read again** (404 if deleted). | the empty view that a deletion leaves would otherwise be a third answer; the second read is one cheap statement |
| PD-11 | `SaveOrderAsync` maps a 23503 on either place key (`…_work_item_id`, `…_owner`) to `ConflictException(ReorderStale)`, as `ProjectBacklogRepository` does. | ADR-0028 rule 4: a key violation is the refusal, never a 500; belt to the locks' braces |
| PD-12 | `ReadChecklistsAsync(organizationId, parentIds)`, `FindLiveParentsAsync(organizationId, ids)` and the assignee-membership lookup (`ProjectMembers` gains an organization overload) take the **organization**. The project views pass the project's organization. | a team view has no single project; each read still filters in SQL by the organization |
| PD-13 | `TeamListItem.isMember` is computed in the list's own statement (one `EXISTS` per team, as `ListLinkedAsync`); the list stays two statements. The panel groups own teams first. | doc-013 OQ-3 A interim level; no per-kind memory |
| PD-14 | Index `ix_work_items_owning_team_id_type_id (owning_team_id, type_id)`, plain (not partial). | ADR-0028 rule 13; the existing project-led index cannot serve a team-led read without a skip scan |
| PD-15 | No new route for the card by address: the panel maps the identifier's key to a project through `GET /projects` (active projects, with `key`) and reads `GET /projects/{id}/workitems/{identifier}`. | BR-TS-4; an unknown key sends no request, a deactivated project's key is absent from the list and the server also answers 404 |
| PD-16 | Old team URLs in tests move through `BoardsTestData.Team`; the retired routes get a theory beside `RetiredTeamRoutesEndpointTests`. | § 10 |

## 4. Affected slices and files

| Layer | File / folder | Change |
|---|---|---|
| Domain | `Boards/StackRank.cs` | `StackRankEntry.ProjectKey`; `Order` as PD-4 |
| Domain | `Boards/BacklogTree.cs` | `BacklogTreeEntry.ProjectKey`; `Nest` passes it |
| Domain | `WorkItems/WorkItemFilter.cs` | project dimension (PD-5) |
| Application | `Abstractions/Organizations/CallerAccess.cs` (new) | PD-3 |
| Application | `Abstractions/Boards/IBacklogReadModel.cs` | `ReadTeamAsync`, `ReadTeamTreeAsync` (scope + `CallerAccess`) replace `ReadAsync`/`ReadTreeAsync`; `TeamBacklogScope(TeamId, LevelId, TypeIds, ShownStateIds, Filter)` and `TeamBacklogTreeScope(TeamId, TypeIds, ShownStateIds, Filter)` replace `BacklogScope`/`BacklogTreeScope` (parallel to `ProjectBacklogScope`/`ProjectBacklogTreeScope`); `ReadChecklistsAsync`, `FindLiveParentsAsync` take the organization; `BacklogItemRow` + `Project`, `ProjectKey` |
| Application | `Abstractions/Boards/BoardViews.cs` | `project` on `BacklogItemView`, `BoardCardView`, `BacklogRowView`, `BacklogChildView`; `InOrder`/`Tree` pass `ProjectKey` |
| Application | `Abstractions/Boards/IBacklogRepository.cs` | `LockAsync(organizationId, TeamBacklogScope)`, `ReadEntriesAsync`, `SaveOrderAsync(TeamBacklogScope, …)`; no project in the scope |
| Application | `Abstractions/WorkItems/IWorkItemReadModel.cs` | `WorkItemProjectView`; `WorkItemView.Project` |
| Application | `Features/Boards/GetBacklog`, `GetBoard`, `ReorderBacklog` | the handlers of § 5-§ 7; queries/responses of PD-1 |
| Application | `Features/Boards/GetProjectBacklog`, `GetProjectBoard` | rows carry `project` (no handler logic change beyond the organization argument) |
| Application | `Features/Organizations/GetOrganizationTeams` | `IsMember` in `TeamListItemResponse`; the handler asks `ICurrentUser` |
| Application | `Abstractions/Organizations/ITeamReadModel.cs` | `ListOrganizationTeamsAsync(organizationId, userId, …)`, `OrganizationTeamRow.IsMember` |
| Infrastructure | `Boards/BacklogMembership.cs` | PD-2 (stays the allow-listed place in `WorkItemAccessRules`) |
| Infrastructure | `Boards/BacklogReadModel.cs` | project/rows (PD-6), PD-12, `CreatedAt`/`ProjectKey` always on a team read |
| Infrastructure | `Boards/BacklogRepository.cs` | PD-8, PD-9, PD-11 |
| Infrastructure | `Organizations/TeamReadModel.cs`, `ProjectMembers.cs` | PD-13, PD-12 |
| Infrastructure | `WorkItems/WorkItemFilterPredicate.cs`, `WorkItemReadModel.cs` | project dimension; `Project` on the detail |
| Infrastructure | `WorkItems/Configurations/WorkItemConfiguration.cs`, `WorkItemsConstraints.cs`, `Migrations/` | PD-14 |
| Api | `Features/Boards/BoardEndpoints.cs` | the three team routes on `/api/organizations/{organizationId}/teams/{teamId}`; `[FromQuery(Name = "projectId")] string[]?` on the reads; the old group deleted |
| Api | `Features/Organizations/OrganizationEndpoints.cs` | none (the response type carries `isMember`) |
| DI | `Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs` | no change (handlers/repositories keep their registrations) |

No new error code, permission code, contract file under `contracts/errors/`, or `docs/error-codes.md` line. `Mustask.sln` and `Directory.*.props` are not touched.

## 5. Reads: membership, filter, rows (BR-TS-2, -3, -6, -8)

**Handler (both reads)**: parse the organization (404) → `FindTeamAsync(org, team)` `is { IsReachable: true }` else 404 `organizations.team.not_found`
(malformed, unknown, deleted, foreign alike) → level and process (404 `boards.level.not_found`) → `ListLinkedProjectsAsync` (`projects`) →
`ReadTeamTreeAsync` / `ReadTeamAsync` with `TeamBacklogTreeScope.Of(team, treeTypes, filter)` and `new CallerAccess(org, user, role)` → live parents
outside the set → (board: checklists) → **`FindTeamAsync` again** (PD-10) → map. The filter is `WorkItemFilter.OfProjects(query.ProjectIds)`.

**Membership SQL shape** (LINQ over `IQueryable`, filtering before materialization):

```sql
FROM work_items item
JOIN projects project ON project.id = item.project_id
LEFT JOIN backlog_places place ON place.work_item_id = item.id          -- joined by its key alone, kept when (team, level) match
WHERE item.owning_team_id = @team
  AND project.organization_id = @caller_organization AND project.status = 'Active'
  AND item.type_id = ANY(@types) AND item.state_id = ANY(@shown_states) -- Removed category excluded: the scope's shown-state ids
  [AND item.project_id = ANY(@filter_projects)]                          -- set filter; an empty array selects nothing
```

The tree set is the same over every level of the page (`TreeMembers`), the place joined by the item's own type's level. The item, project, type, level,
state, owning team and place go in **one** statement; the assignees' lookups (batches of the directory's limit, one membership statement) and the one read
of the parents outside the set follow as today.

`ordersByCreation` becomes a property of the read: **team reads always supply `CreatedAt` and `ProjectKey`**; project reads keep their rule (no
`CreatedAt` on the «Задачи» level). Rows carry `project {id, name}` from the join the read already makes. The filter changes nothing but the
`WHERE`; `projects` is read apart and is never narrowed.

**Why no link join and no `IN (project ids)`**: `owning_team_id = T` implies the project is T's linked project (the owner key), and a deactivated
project is filtered by status in the same snapshot as the items. A project unlinked meanwhile has had its items reassigned in the unlink's
transaction.

**Deleted items** (ADR-0030 rule 2): `BacklogMembership.cs` stays the allow-listed file that names the context's `WorkItems` set; the live parents and
the checklists go through `WorkItemSets.Live`. Nothing new names the set elsewhere.

## 6. Reorder (BR-TS-6, -7; US-3)

Handler: org → team (404) → level (404) → position (400) → `ExecuteInTransactionAsync`: `LockAsync(org, scope)` (false → 404 team) → `ReadEntriesAsync` →
`StackRank.Order` → item parse (malformed → 409) → `StackRank.Move` (not on the backlog → 409 `ReorderStale`) → `SaveOrderAsync`. The scope carries **no**
filter (a filtered scope handed to `LockAsync` is refused as a programming error, as `ProjectBacklogRepository` does).

**`LockAsync(organizationId, TeamBacklogScope)`**, in ADR-0028 rule 8's order, each a separate statement:

1. `SELECT 1 FROM teams WHERE id = @t AND organization_id = @org FOR SHARE`; then `Teams.AnyAsync(Active)` — false → return false (404 team).
2. `SELECT 1 FROM work_items item JOIN projects project … WHERE <membership, no filter> ORDER BY item.id FOR KEY SHARE OF item`.
3. `INSERT INTO backlog_orders … ON CONFLICT DO NOTHING`, then `SELECT … FOR UPDATE` of the (team, level) row.

A team linked to no active project has no member: the reorder ends 409 (nothing to move), not 404.

**`ReadEntriesAsync`** is statement 2 again returning `item.id, item.number, item.created_at, project.key` and the place on this backlog — the locking
read the order is computed from; `DeletedStateParityTests` pins it equal to the membership.

**`SaveOrderAsync`**: (a) one `DELETE FROM backlog_places AS place USING work_items AS item, projects AS project WHERE … place.team_id = @t AND
place.backlog_level_id = @l AND project.organization_id = @org AND project.status = 'Active' AND NOT (item.owning_team_id = @t AND item.type_id = ANY(@types)
AND item.state_id = ANY(@shown))` — the places of items that have left (state, type) in the active projects; a deactivated project's places are left
untouched (ADR-0028 rule 9), and an item that is a member by the predicate is never "departed" whatever the project's status at the statement's
snapshot (PD-9); (b) the existing upsert of `unnest(ids) WITH ORDINALITY` (`ON CONFLICT (work_item_id) DO UPDATE … IS DISTINCT FROM`); a 23503 on a
place key → `ReorderStale` (PD-11). Positions are dense `1..n` over the members, so equal positions end with the first reorder (US-3 scenario 2).

**Races** (api-contract § 8.1), each with a test that uses the existing `CommitGate` harness:

| Race | Serialized by | Result |
|---|---|---|
| reorder vs unlink of Beta from T | members `FOR KEY SHARE` vs the unlink's owned items `FOR UPDATE` (both in id order) | either order; the unlink's `DropForTeamInProject` removes what a first reorder wrote; a reorder second finds Beta's items no longer T's; no deadlock |
| reorder vs T's deletion | team `FOR SHARE` vs `FOR NO KEY UPDATE` | the deletion removes every place, or the reorder is 404 |
| reorder vs a save changing a member's team | member `FOR KEY SHARE` vs `FOR UPDATE` | existing `ReorderVersusTeamChangeEndpointTests`, address only |
| reorder vs deactivation of Gamma (gated, uncommitted at the read) | none | Gamma's items are members; the reorder writes their places; the deactivation then keeps them |
| reorder vs deactivation committed first | none | Gamma excluded, its places untouched and kept (departed drop skips an inactive project) |
| reorder vs restore | none | members as the locking statement saw them; kept places never dropped (PD-9); a moved/named item that left → 409 |
| two reorders of one backlog | the order row `FOR UPDATE` | arrival order, each reads the other's result (existing tests, new address) |

## 7. Containment (m3), reads against a deletion (m2), roles

- **m3**: `FindTeamAsync(org, team)` is null for a foreign team; the membership joins `projects.organization_id = caller.org`; a foreign project id in the
  filter matches nothing; a foreign item id in the reorder is not a member (409); the parents and checklists are read under the organization;
  `LockAsync` filters the team by (organization, id). `TeamViewContainmentTests` calls **each of the three routes** with Orion's id as the team,
  Zeta's id in the filter and as the moved/anchor item, a non-member (and an instance administrator who is no member), and asserts status and body
  identical to an unknown id (nothing says whether Orion, Zeta or the item exist); the project views keep their test (`project-of-another-organization`
  on `GetProjectBacklogEndpointTests`).
- **m2**: `TeamViewDeletionRaceTests` (Infrastructure.Tests, own service provider like `ProjectBacklogStatementCountTests`) wraps `IBacklogReadModel` in a
  decorator that deletes the team (a second context, committed) *between* the team check and the read, and *after* the read: both end in 404 through the second team
  read of PD-10 (never an empty 200, never a 500) — for the backlog and the board; the endpoint test covers the two plain orders (read before / after the committed deletion).
- **Roles**: no new code; `RequiresOrganizationPermission` on each route as § 6 of the contract; «Читатель»: reads 200, reorder 403 before any not-found.
  Moves/creations by «Читатель» are the existing work item routes' 403 (tests from the team-view side, US-4/US-5).

## 8. Switcher, card, creation — what the backend adds and what it only tests

- **Added**: `isMember` (PD-13) and `WorkItem.project` (PD-6). Both additive; `GetOrganizationTeamsEndpointTests.Get_ItemShape_…` is rewritten for the new shape.
- **Tested from the team-view side, no code**: US-4 (a card of Beta read/written in Beta while ALP-US-3 is untouched; a move writes the state in the item's project;
  another type's state refused; a child lands in the parent's project with its next number and team; a parent of another project is 404
  `workitems.link.parent_not_found`; «Читатель» 403) and US-5 (created in the chosen project owned by T or none; unplaced on **both** orders; refusals
  404/409/404 create nothing and consume no number; the viewer's 403; the create-vs-unlink race). Existing classes cover the single-project forms
  (`CreateOwningTeamEndpointTests`, `CreateFromTeamEndpointTests`); the new class adds the cross-project and team-view forms.

## 9. Query shape, speed, migration

- **Statements** per read, fixed: team, process, linked projects, items/tree (1), assignees (batches + 1 membership), parents outside, team again; board +1
  (checklists) and its assignees. A reorder: handler reads + the 5 lock statements + entries + drop + upsert. The first run pins the numbers
  (`TeamViewStatementCountTests`), small = 1 project / 10 items, large = 50 projects / 5 000 items / a parent chain 5 deep / 10 filter values, and the
  pair must be equal; the three team tests of `OwningTeamStatementCountTests` move there.
- **Speed**: `TeamBacklogVolumeTests` (Api.IntegrationTests, `MustaskApiCollection`, after one warm-up as `BacklogVolumeTests`): MARS linked to 50 projects
  owning 5 000 stories, places for 4 000 (SQL seed), backlog and board unfiltered and filtered to 10 projects each < 2 s; a move to the top < 1 s. The
  existing `BacklogVolumeTests` (one project) stays on the new address through the helper.
- **Migration** `AddTeamLedWorkItemIndex` (generated with `dotnet ef migrations add` on the post-merge snapshot; rebase and regenerate if main brings
  migrations before the PR): `CreateIndex ix_work_items_owning_team_id_type_id`; `Down` drops it. `AddTeamLedWorkItemIndexMigrationTests` (own database,
  like `AddProjectBacklogOrderMigrationTests`): applies, the index exists, rolls back, applies again, rows untouched; the model and the migrations agree.

## 10. Tests

### 10.1 New (each criterion of US-1..US-7 has one; a comment links the story)

| Class (project) | Covers |
|---|---|
| `TeamBacklogAcrossProjectsEndpointTests` (Api) | US-1: 22 items over three projects, no VENUS/unowned; the levels; every state but Removed; children are T's own; rows/cards carry `project` and `team` on the four views; project views unchanged; Gamma deactivated → 15, restored → 22 with its places; foreign/deleted/malformed/unknown team alike; JUPITER and all-deactivated alike (empty, `projects: []`); `projects` by name; a failed re-read keeps its frame is the panel's |
| `RetiredTeamViewRoutesEndpointTests` (Api, beside `RetiredTeamRoutesEndpointTests`) | the three project-nested routes × methods × (member, non-member, existing/unknown project and team) → 404 `http.not_found`, nothing changed |
| `TeamProjectFilterEndpointTests` (Api) | US-2: one, two, none values; options = `projects`; changes nothing; tree built over matching items, checklist whole; filtered reorder against the full list; unknown/malformed/Zeta/Delta/unlinked/deactivated → none, total 0; Beta + unknown → Beta; the deactivated project's value selects nothing and again after the restore; board = backlog |
| `TeamOrderAcrossProjectsEndpointTests` (Api) | US-3: tie-break (placed first; creation time, key, number — 10:00…10:15 example; the «Задачи» rows); equal positions from per-project reorders; BR-TS-14 (existing legacy rows read as they are); a cross-project reorder; the five stale cases alike; Gamma's places kept; roles; the races of § 6 (gated); m2 plain orders |
| `TeamCardAndMovesEndpointTests` (Api) | US-4 (§ 8) |
| `CreateFromTeamAcrossProjectsEndpointTests` (Api) | US-5 (§ 8) |
| `TeamViewContainmentTests` (Api) | US-7: non-member/instance administrator; the answer order; foreign ids on each route (m3); project views' boundary; roles (the route inventory is `OrganizationAccessInventoryTests` over the swapped rows of § 10.2) |
| `TeamBacklogVolumeTests` (Api) | US-7 volume |
| `TeamViewStatementCountTests` (Infra) | US-7 query shape, backlog (stories, epics), board, reorder |
| `TeamBacklogMemberLockTests` (Infra) | the lock holds the team and the members (`FOR SHARE`/`FOR KEY SHARE` conflict probes as `ProjectBacklogMemberLockTests`: a `FOR UPDATE` of a member and a `FOR NO KEY UPDATE` of the team wait; an unlink-shaped sequence — owned items `FOR UPDATE` in id order — and a reorder cannot deadlock), spares non-members and a deactivated project's items, a filtered scope is refused, `ReadEntriesAsync` = the membership, `SaveOrderAsync` keeps an inactive project's places and a restored-race member's, an item that left is a stale reorder |
| `TeamViewDeletionRaceTests` (Infra) | m2, § 7 |
| `AddTeamLedWorkItemIndexMigrationTests` (Infra) | § 9 |
| `StackRankTests`, `BacklogTreeTests`, `WorkItemFilterTests` (Domain) | the tie-break (time, key by character code — «ALP»<«BET»<«alp» —, number as a number), equal positions, the task level; the tree across keys; `OfProjects` (absent, empty, malformed, mixed, set-but-empty) |
| `BacklogRowViewTests` (Application) | `project` on top-level rows and nested children |
| `GetOrganizationTeamsEndpointTests` (Api) | `isMember` for member/non-member/admin of no team |

### 10.2 Rewritten (name kept, new intent) and re-pinned

`BoardsTestData.Team/Backlog/Board/ReorderAsync` (the team address); `GetBacklogEndpointTests`, `GetBoardEndpointTests`, `ReorderBacklogEndpointTests` (their
`…_ContextNotFound_AnswersItsLevelsCode`: the `project-deactivated` case is dropped, `team-of-another-project` becomes `team-of-another-organization`
with a team of «Другая»); `GetBacklogEndpointTests.Get_NeverReordered_ListsByNumber` → creation time, key, number (BR-TS-6); `Reorder_TeamDeletedWhileWaiting_Returns404TeamNotFound`
kept; `OwnedViewsEndpointTests`, `BacklogTreeEndpointTests`, `BoardChecklistsEndpointTests`, `ReorderVersusTeamChangeEndpointTests`, `CreateFromTeamEndpointTests`
through the helper; `DeleteTeamEndpointTests` ("backlog"/"board" rows), `UnlinkTeamEndpointTests` (`TeamBacklog`, line ~237), `TeamQaEndpointTests` (lines ~333): their
direct URLs go to the team address; `TeamAcrossProjectsEndpointTests.Unlink_TeamBecomes404AtTheProjectsAddress_ButIsStillReadAtItsOwn` (after an unlink
the team's views hold the other project's items only; its own page still reads) and `…Unlink_FromOneProject_LeavesTheOthersSettingAndItems` (the last line via the helper);
`RetiredTeamRoutesEndpointTests.ProjectsTeamsList_StillAnswers_AndTheTeamViewsAndTheUnlinkPairToo` (the list and the unlink pair answer; the views are 404, BR-TS-1);
`OrganizationAddressedRoutes` (three rows → `OrgTeam`, count 59, doc text), `WorkItemWriteInventoryTests` (the reorder row's key), `DeletedItemSurfaceMatrixTests`
(`ReadRoutes`: the two views move to `OrgTeam`; Row 3 gains a team view across two projects with a deleted item in the second), `DeletedStateParityTests`
(`Memberships_TeamAndProject_…`, `LockingReads_OfTheTeamAndProjectReorders_…`, `LiveParents_AndChecklists_…`: the new scope, the organization argument, a deactivated project, a
second project), `ProjectBacklogMemberLockTests`/`ProjectBacklogStatementCountTests` (the checklist/parents signature only), `GetOrganizationTeamsEndpointTests.Get_ItemShape_IsIdNameDescriptionProjects`
(+ `isMember`). `BacklogVolumeTests` and `BacklogTreeVolumeTests` keep their content.

### 10.3 Retired, by name, with the rule (P-1)

| Test | Rule that retires it | Replaced by |
|---|---|---|
| `TeamAcrossProjectsEndpointTests.Reorder_InOneProject_LeavesTheOthersPlacesAndProjectOrdersAlone` (the spec's) | feat-028 BR-OL-11, D-13 → BR-TS-7 | `TeamOrderAcrossProjectsEndpointTests.Reorder_AcrossProjects_RanksTheirItemsInOneOrder_LeavesEveryProjectOrderAlone` |
| `TeamAcrossProjectsEndpointTests.TeamViews_AtEachProjectsAddress_HoldThatProjectsItemsOnly` | feat-028 BR-OL-11 → BR-TS-1, -2 | `TeamBacklogAcrossProjectsEndpointTests` |
| `TeamAcrossProjectsEndpointTests.Reorder_DropsTheDepartedPlacesOfThisProjectOnly` | feat-028 D-13 → BR-TS-7 | `Reorder_DropsTheDepartedPlacesOfTheActiveProjects_KeepsADeactivatedProjectsPlaces` |
| `TeamAcrossProjectsEndpointTests.TeamViews_UnderAnUnlinkedProject_Return404Team_ReorderByViewerIs403First` | feat-028 BR-OL-11 (404 at a project the team is not linked to) → BR-TS-1 (no project in the address); its 403-first half → BR-TS-13 | `TeamViewContainmentTests` (answer order), `RetiredTeamViewRoutesEndpointTests` |
| `OwningTeamStatementCountTests.TeamBacklog_StoriesAndEpics_…`, `.TeamBoard_…`, `.TeamReorder_…` and their three constants | mod-017's one-project counts → BR-TS-13 (query shape across projects) | `TeamViewStatementCountTests` |
| the `project-deactivated` case of the three `…_ContextNotFound_…` theories | BR-TS-1: the team's address has no project level | the project views' own theories (feat-039, kept) |

### 10.4 E2E (standing NO — listed, not edited, not run)

The change breaks, at least: `support/membersUi.ts` (`backlogAddress`, `boardAddress` still build `…/projects/:p/teams/:t/…`); `specs/23-backlogs-boards.spec.ts`,
`24-work-item-hierarchy`, `25-work-item-fields`, `26-backlog-tree-editing` (all four build `teamPrefix` under a project; 26 also opens the menu «Команды проекта»
and «Весь проект»), `27-work-item-card`, `28-team-members`, `29-role-views` (the addresses and the head menu), and `19-shell-in-a-browser`, `21-organizations`
(the switcher's team level and the entries «Бэклоги»/«Канбан» now land on the organization's first team). Found by `grep -rn "teams/\|Бэклоги\|Канбан\|Команды проекта" src/frontend/e2e`; the MR names them.

## 11. Order of work (small commits; build and tests green after each)

1. **Domain**: `StackRankEntry`/`BacklogTreeEntry` `ProjectKey` + `Order`; `WorkItemFilter` project dimension; Domain tests.
2. **Migration** `AddTeamLedWorkItemIndex` + its test.
3. **Additive wire fields**: `project` on rows/cards/children (`BacklogItemRow` + views, team and project reads), `WorkItem.project`, `isMember`; the tests that pin shapes
   (`BacklogRowViewTests`, `GetOrganizationTeamsEndpointTests`, the work item read tests). The old routes still serve; nothing is removed yet.
4. **Team infrastructure beside the old** (new types and overloads, so the old routes keep serving): `CallerAccess`; `TeamBacklogScope`/`TeamBacklogTreeScope`, the cross-project
   `BacklogMembership` members, `ReadTeamAsync`/`ReadTeamTreeAsync`, org-scoped checklists/parents/assignees (PD-12, the project reads move to them in this commit), the
   `BacklogRepository` overloads (PD-8, -9, -11); the Infrastructure tests of § 10.1/10.2 (parity, locks, deletion race, statement count) on them.
5. **Cut-over** (one commit, because a route cannot be half moved): the three handlers and queries (PD-1), `BoardEndpoints`, the old scopes/queries/repository code deleted,
   `BoardsTestData`, the rewritten and retired tests of § 10.2/10.3, `OrganizationAddressedRoutes` and the two inventory/matrix tests.
6. **New endpoint tests**: `TeamBacklogAcrossProjectsEndpointTests`, filter, order (with races), card/moves, creation, containment, retired routes, volume.
7. **Close-out**: the P-10 notes (§ 12), `status.md` Log; `scripts/verify.sh`; `dotnet test` of the backend projects (Docker up). The stage line moves to `05-frontend` when the code
   stage closes (04-backend.md, Definition of Done); QA is the single pass over both layers after the frontend (status.md § Plan).

## 12. Close-out items

- **P-10 notes** (api-contract § 10): one Log line in each owner's `status.md` — mod-006, mod-016, mod-017, feat-027, feat-028, feat-032, feat-039, mod-003 — added in the last
  commit; their `api-contract.md` files are **not** edited (this contract restates).
- **For the orchestrator**: (a) mod-020 gets the note of spec § Decomposition (the fallback team on this line's team level, the filter's address form; the API form is founded
  here — repeated `projectId`, like `teamId`); (b) the frontend stage reads api-contract §§ 2-4, 7: `project` on rows, `projects` for the filter/empty state/create command,
  `isMember` for the switcher, `GET /projects` `key` for the card by address, `organizations.project.not_found` on a card read = the not-found frame.
- **Definition of done** (04-backend.md): contract fixed; every criterion has a test; migrations apply on a clean database and again; the retired and rewritten tests exist by name;
  `scripts/verify.sh` green; no process left running (Testcontainers are torn down by the fixtures).

## 13. Risks and questions

- **Risk — reorder at 5 000 across 50 projects** shares the budget with tech-011 t1/t4 (a 1 s write after a 356–949 ms warm-up). The locking statement now joins `projects`;
  the volume test prints the timings and the first run decides whether the plan's statement shape needs a tweak (not the contract).
- **Risk — the planner on the team-led read**: with 50 projects the join order is items-first on the new index; the volume test is the proof, `EXPLAIN` is only for diagnosis.
- **Risk — render cost of a 5 000-item team view** is the frontend's accepted one (spec NFR Client load, OQ-3 A).
- **Question "for the human": none.** Decisions that could look like one are settled by the spec: the task level's order on a team view (BR-TS-6's fate row), `project` on the detail
  and `isMember` (additive, design § 14), no redirects, E2E NO. If the orchestrator would rather not edit other features' `status.md` for the P-10 notes, the alternative
  (one list in this feature's `status.md`) changes no code.

## 14. As built (2026-10-18)

The code stage matches §§ 1-9 and `api-contract.md`, which is **unchanged**. Deviations and what the first runs fixed:

- **Order of work.** §§ 11.4 and 11.5 became one commit (the cut-over): the team read model, repository and scopes were replaced, not kept beside the old, since the old
  ones had no other caller. The Infrastructure tests of § 10 moved with it.
- **`IBacklogRepository`**: `ReadEntriesAsync(organizationId, scope)` and `SaveOrderAsync(organizationId, scope, order)` take the organization too (the plan named it
  for `LockAsync` only): the membership SQL and the departed drop both state the tenant predicate. `LockAsync` refuses a scope carrying a **team** filter as well as a project one.
- **`BacklogScope.ShownStates`** (the shown-state ids of a level's types) became `BacklogStates.ShownOf`, an internal helper in `IBacklogReadModel.cs`, which the four scopes
  share. `BacklogItemRow.ProjectKey` is a public property of the (public) row, never on the wire; `WorkItemView.Project` is a `required` init member.
- **PD-12**: the organization overload is `ProjectMembers.MemberIdsOfOrganizationAsync`; the read model picks it by a private `Tenant` value. A project's own read still asks by
  project; the project handlers pass `project.OrganizationId` to `ReadChecklistsAsync` and `FindLiveParentsAsync` only.
- **PD-9**: the departed drop's predicate reads `NOT (COALESCE(item.owning_team_id = @team, false) AND type ∈ L AND state ∈ shown)`; an item that left by owner never reaches it
  (the place's owner key drops the place first), so the `COALESCE` is the null-safe form, not a case.
- **Statement counts pinned** (`TeamViewStatementCountTests`; equal for 1 project / 10 items per level and 50 projects / 5 000 items / 10 filter values): backlog «Истории» 10,
  «Эпики» 9 (no parent outside the set), board 12, reorder 12. The process read is three statements, so §9's "fixed number" is these four.
- **Timings, first run** (`TeamBacklogVolumeTests`, 50 projects, 5 000 stories, 4 000 placed): backlog 241 ms, board 367 ms, filtered to 10 projects 77 / 110 ms, move to the top 379 ms,
  move after 473 ms. No tweak to the statement shape was needed (§ 13 risks closed).
- **Tests, as planned in § 10.1** (by class): `TeamBacklogAcrossProjectsEndpointTests`, `RetiredTeamViewRoutesEndpointTests`, `TeamProjectFilterEndpointTests`,
  `TeamOrderAcrossProjectsEndpointTests` (with the races of § 6, gated), `TeamCardAndMovesEndpointTests`, `CreateFromTeamAcrossProjectsEndpointTests`, `TeamViewContainmentTests`,
  `TeamBacklogVolumeTests` (Api); `TeamViewStatementCountTests`, `TeamBacklogMemberLockTests`, `TeamViewDeletionRaceTests`, `AddTeamLedWorkItemIndexMigrationTests`, and a team-across-projects
  case in `DeletedStateParityTests` (Infrastructure); `StackRankTests`, `BacklogTreeTests`, `WorkItemFilterTests`, `BacklogRowViewTests`, `GetOrganizationTeamsHandlerTests`; the surface
  matrix gained a team view across two projects. Shared arrangements: `TeamAcrossProjectsTestData` (Api) and `TeamViewWorld` (Infrastructure).
- **Retired, as § 10.3**: the four `TeamAcrossProjectsEndpointTests` tests named there, the three team tests of `OwningTeamStatementCountTests` and their constants, and the
  `project-deactivated` case of the three `…_ContextNotFound_…` theories. Rewritten by name: the three theories (`team-of-another-project` → `team-of-another-organization`),
  `Unlink_TeamBecomes404AtTheProjectsAddress_ButIsStillReadAtItsOwn`, `Unlink_RemovesTheLink_ViewsCountAndNamingAnswerAsNotLinked`, the retired-routes list test, the route
  inventory rows (count 59), the write inventory's reorder key and the surface matrix's read routes.
- **Two facts the tests met**: a child created beneath a parent takes the parent's team whatever `team` the body names (mod-017 BR-OT-3), so a test gives a child to another team by a
  change of owner; a team can own an item only in a project it is linked to (the owner key), so "another team's item" in a project needs a team linked to that project.
- **E2E (standing NO, not edited, not run)** breaks: `support/membersUi.ts` (`backlogAddress`, `boardAddress`), and the specs `19-shell-in-a-browser`, `23-backlogs-boards` (also its
  direct API read of `/api…/teams/…/boards/stories`), `24-work-item-hierarchy`, `25-work-item-fields`, `26-backlog-tree-editing`, `27-work-item-card`, `28-team-members`,
  `29-role-views` (the project-nested team addresses, the head menu and the switcher's team level); `21-organizations` is touched by the switcher alone (frontend stage).
- **P-10 notes** are added to the eight owners' `status.md` (all `stage: complete`, so the size budget skips them): mod-003, mod-006, mod-016, mod-017, feat-027, feat-028, feat-032, feat-039.
