# Audit 4-boards-backlogs — Boards and backlogs

As-is audit for doc-012, read-only on the code. Sources: mod-006 (spec, api-contract, stories), doc-003,
`src/backend/*/Boards`, `BoardEndpoints`, and feat-026/027/032/037 only where they constrain the board.
Paths are repository-relative; `Infra` = `src/backend/Mustask.Infrastructure`.

## As-is (what exists, with repository-relative file paths and line numbers where it matters)

**What defines "the items of a team's backlog/board" today.** Four conditions, evaluated at every read
(mod-006 spec § Model, BR-M4-5; api-contract § 1), in one place, `Infra/Boards/BacklogMembership.cs`:

1. the item belongs to the **address's project** and the team is an active team of that project:
   `item.ProjectId == scope.ProjectId` (l. 66); handlers refuse `team.ProjectId != projectId`
   (`GetBacklogHandler.cs` l. 46-51, `GetBoardHandler.cs` l. 42-47, `ReorderBacklogHandler.cs` l. 56-61);
2. its type is one of the level's types (l. 67);
3. its **Область resolves to the team**: `TeamAreas` (l. 45-46) keeps every node of the project's area tree with
   `AreaTree.ResolveOwner(node.Id).TeamId == teamId` (`src/backend/Mustask.Domain/Organizations/AreaTree.cs` l. 145-161:
   nearest explicit `OwnerTeamId` upward, the root's being the first team's), then `areaIds.Contains(item.AreaId)` (l. 68);
4. its state is outside the Removed category (l. 69).

- **The item stores no team.** `WorkItem` has `ProjectId` (l. 53) and `AreaId` (l. 65), no team field
  (`src/backend/Mustask.Domain/WorkItems/WorkItem.cs`); the owning team is derived item -> area -> owner. An item is on
  exactly one team's backlog of its level (doc-001 BR-3).
- **Teams and areas are children of a project** (`Team.ProjectId` `Team.cs` l. 30; `Area.ProjectId` `Area.cs` l. 36), and
  the DB enforces it: `areas` composite FKs `(project_id, owner_team_id|default_of_team_id|parent_id)`
  (`Infra/Organizations/Configurations/AreaConfiguration.cs` l. 36-47), `work_items (project_id, area_id)` -> `areas`
  (`Infra/WorkItems/Configurations/WorkItemConfiguration.cs` l. 83-87), checks `ck_areas_default_owned`/`ck_areas_root_owned`
  (l. 64-65) and a unique index on `default_of_team_id` (l. 57): a team has at most one default area.
- **Membership is read, never stored.** `BacklogReadModel.ReadAsync/ReadTreeAsync` (`Infra/Boards/BacklogReadModel.cs`
  l. 27-41) load the project's area nodes, compute the team's area set, then run one `work_items` statement left-joined to
  the item's place; `ix_work_items_project_id_type_id_area_id` serves it. **No paging**: the whole backlog/board is read
  (l. 15-24); NFR 5 000 items in 2 s; a tree set up to 20 000 rows (l. 195-196); the panel draws at most 2 000 (feat-027 BR-T-7).
- **What changes membership (writes elsewhere, mod-006 US-7):** Область set on the form (`WorkItem.cs` l. 303); an area
  re-owned or moved (`SetAreaOwner`, `MoveArea`); a team deleted (`DeleteTeamHandler` re-owns its explicit areas to the first
  team, BR-M1-18); creation, which takes the team's default area (`CreateWorkItemHandler.cs` l. 125-136,
  `Infra/WorkItems/WorkItemRepository.cs` l. 41-53) or the parent's (feat-027 BR-T-10).
- **Order (stack rank)** is not an item field: `backlog_places(work_item_id PK, team_id, backlog_level_id, position)`, so one
  place and one backlog per item (`Infra/Boards/BacklogPlace.cs`, `Configurations/BacklogPlaceConfiguration.cs` l. 18-30);
  `backlog_orders(team_id, backlog_level_id)` is the reorder's lock row, FK to `teams` Restrict. Order = placed by position,
  then unplaced by `Number` (`src/backend/Mustask.Domain/Boards/StackRank.cs` l. 71-73); `Number` is one sequence **per
  project** (`work_item_counters`, `WorkItemRepository.cs` l. 21-25). A reorder places every item and drops places of departed
  items (`BacklogRepository.cs` l. 57-92). Locks: `teams WHERE id AND project_id FOR SHARE`, then the order row `FOR UPDATE`
  (l. 20-46; ADR-0011); creation from a team takes the same team lock.
- **Columns per (type, state)** come from the **organization's process** (`GetBoardHandler.cs` l. 50, 59-63;
  `Organization.ProcessId`, `Organization.cs` l. 38; `Project` has none, mod-002 BR-M1-3): every project of an organization
  has the same states; columns are never stored. A card move is mod-003's PATCH `projects/{projectId}/workitems/{identifier}`
  `{stateId}` (api-contract § 6); no board route writes a state.
- **Routes** (`src/backend/Mustask.Api/Features/Boards/BoardEndpoints.cs` l. 30-56), all under
  `/api/organizations/{o}/projects/{p}/teams/{t}/`: `GET backlogs/{level}`, `POST backlogs/{level}/reorder`, `GET boards/{level}`;
  codes `workitems.workitem.read` and `boards.backlog.reorder`, organization-scoped from the membership role (ADR-0027;
  `PermissionScope` has only Instance and Organization). No project-, area- or team-wide board exists.
- **Payload.** `BoardTeamView(Id, Name, DefaultArea)` (`Application/Abstractions/Boards/BoardViews.cs` l. 9); rows and cards
  carry `identifier` but **no project id/name, area or team**. The frontend builds every card link and every write from the
  **page's** `projectId` (`src/frontend/src/features/boards/components/BacklogTable.tsx` l. 263-266, 1117; `BoardColumns.tsx`
  l. 114, 366; keys `boards/api/queryKeys.ts`).
- **Checklists (feat-032)** read direct children whatever their Область (`BacklogReadModel.cs` l. 49-107); the feat-027 tree
  shows only children that are members of the team (BR-T-4, `src/backend/Mustask.Domain/Boards/BacklogTree.cs`).

## Responsibilities the current model carries (one duty per bullet; mark where one entity carries several duties)

- **Area carries four duties in one entity (several-in-one):** classification path (`AreaTree.PathOf`, DC-9); **item -> team
  ownership** (`OwnerTeamId`, inheritance down the tree), the only item-team link there is; the team's default for new items
  (`DefaultOfTeamId`); a deletion guard (BR-M1-13). Boards consume the second, creation the third.
- **Team (several-in-one):** member group (mod-016); owner of areas; the unit that has a backlog, a board and an order; the
  project's "first team" (root owner, receiver of a deleted team's areas, default selection); a navigation level under a project.
- **Project (several-in-one):** tenant child, work-item numbering, area-tree root, team set, and the address prefix of every
  board route; the board query's first filter.
- **`backlog_places` (several-in-one):** order position, "which backlog the item is on" (`team_id`, level), and the
  one-backlog-per-item invariant (PK = item).
- **Organization's process:** the column set of every board of every project. **Page address:** scoping and the write
  target of every card action. **Role bundle:** all board access; team membership grants nothing (doc-003 § Roles).

## Rules and contracts the matrix model touches (by id: BR-M1-n, D-n, E-n, US-n, ADR-NNNN, api-contract §) — for each: what in the brief collides with it

- **doc-001 BR-3 / BR-M4-5 / api-contract § 1** (an item is on a team's backlog only when its area resolves to the team;
  exactly one team): brief §§ 5-6 give the item an optional `OwningTeamId` and a team a view across projects; teams own no
  areas. Conditions 1 and 3 are replaced; "exactly one team" has no counterpart for an unowned item.
- **doc-001 BR-12 / BR-M4-24 / BR-M4-13** (order per team and level, unplaced by project-local `Number`): brief § 7 has three
  board levels over one collection; `Number` ties across projects; a project or area board has no (team, level) key.
- **BR-M4-1** (one backlog and board per active team per level, whenever created): the brief adds project and area boards.
- **BR-M4-20 / doc-010 D-6 / BR-M1-18** (deleted team's items go to the first team, by read-time derivation): needs an explicit
  rule for a stored owner (brief SC-09, rule 9; prior-art P-7).
- **BR-M4-21 / doc-010 D-7 / BR-M1-14 / api-contract § 7** (create from a team takes its default area; `defaultArea` in
  `BoardTeam`; dialog names the area): the brief gives a project a default team, a team spanning projects has no single area.
- **BR-M4-26 / `boards.backlog.reorder_stale`**: "stale" means left this backlog; its meaning follows membership.
- **BR-M4-27, ADR-0027, mod-016 BR-M1-33**: board routes carry organization codes only; brief § 10 wants Area/Project/Team
  scopes, `Restricted` projects and no leaks through counters (P-4, P-5). Board reads filter nothing by visibility.
- **BR-M4-2/-3/-18** (columns from the process, card move writes the state): brief SC-10 assumes per-project status sets;
  the process is organization-wide (BR-M1-3), so "validated by the source project's workflow" has no distinct input.
- **BR-M4-14 / feat-027 BR-T-1 / feat-032 BR-C-6** (fields): brief § 11.6 wants project and responsible team on tasks.
- **BR-M4-28** and keys `['boards', kind, org, project, team, level]`: routes, keys and refresh are team-in-project shaped.
- **feat-027 BR-T-4/-5/-10/-12/-15, `workitems.link.*`**: parent links are same-project only (`parent_not_found`); BR-T-4's
  "children on the team's surfaces" loses its definition; BR-T-10 lands the child on the parent's team via its area.
- **feat-032 BR-C-1 (D-4)**: checklists count children of any team by design. **feat-037**: no collision.
- **feat-026 BR-H-15/16, feat-027 BR-T-16** (`TeamHead.tsx`: "Сменить команду" from the project's team list), **mod-015 BR-18,
  mod-002 BR-M1-24, mod-016 BR-M1-27**: team pages carry the project in the address; default team = the user's first team
  in the project; brief § 11.1 makes Area, Project, Team separate entries.
- **doc-004 BR-M5-1/-5/-9/-13** (mod-007, feat-008, feat-009 queued; no code): iterations belong to a project, teams select
  them, the planning pane counts "the team's own items"; brief § 8 makes the sprint the team's.
- **ADR-0026 rules 3, 7, 8**: reachability by containers (org, project); `FOR SHARE` lock keyed (team, project). **ADR-0011**
  echelon 3. **ADR-0014**: paging envelope is for registries; boards are unpaged (brief § 7 asks paging or virtualization).
- Queue: **feat-028-org-level-teams** already names what a team's backlog and kanban hold across projects; **tech-011** m1/m2
  (place race; 500 when a team is deleted between check and read) are open debts on the same code.

## Regression surface (existing behaviour that must keep working, with the test or spec that proves it today)

Backend integration tests live in `src/backend/tests/Mustask.Api.IntegrationTests/Features/Boards/`.
- Membership by area owner, inheritance, re-own, move, deleted team, area set on the form: `GetBacklogEndpointTests`
  (`Get_HoldsTheTeamsItems…`, `Get_AreaWithoutOwner_…`, `Get_AreaReOwned_…`, `Get_AreaMoved_…`, `Get_DeletedTeam_…`); mod-006 US-1, US-7.
- Order: placed then unplaced by number, an item coming back: `GetBacklogEndpointTests` (`Get_ItemBackWithNoReorderMeanwhile_…`),
  `ReorderBacklogEndpointTests` (16 tests: races, stale 409, 400, deleted team), `StackRankTests`, `BacklogVolumeTests` (5 000 items).
- Board: columns, targets, cards in backlog order, a move writes only the state: `GetBoardEndpointTests`, `BoardLayoutTests`,
  `BoardLevelTests` (bug-004 D-3 columns).
- Creation from team surfaces (default area, replay, deleted team, parent area): `CreateFromTeamEndpointTests`, `CreateChildEndpointTests`.
- Tree and parent writes: `BacklogTreeEndpointTests`, `BacklogTreeTests`, `BacklogTreeVolumeTests`. Checklists:
  `BoardChecklistsEndpointTests`, `CardChecklistTests`, `BoardChecklistsVolumeTests`, `BoardChecklistsQaTests`.
- Access by role (viewer reads, member reorders): `WorkItemsAccessEndpointTests`, `OrganizationAddressedRoutes` (route inventory),
  `OrganizationRoleBundlesTests`; frontend `BoardPage.roles.test.tsx`, `BacklogPage.gating.test.tsx`.
- Frontend: 38 test files under `src/frontend/src/features/boards`, `src/frontend/src/app/routes.boards.test.tsx`,
  `features/organizations/context/useOrganizationsContextSource.teamLevel.test.tsx` (switcher); E2E `e2e/specs/23-backlogs-boards.spec.ts`
  and `26-backlog-tree-editing.spec.ts` (E2E is paused by the human). Contracts: mod-006 api-contract §§ 3.1-3.3, § 7, `BoardErrorCatalogTests`.

## Risks and open questions for the analyst

1. **Order across projects (P-2).** One stack rank per item (today's PK) or one per view (team, project, area)? A project
   board and a team board over one item either share an order or need a second storage. What breaks ties among unplaced
   items of several projects, as `Number` is per project?
2. **Items with no team.** Today every item is on exactly one team's backlog. With a nullable owner, which backlog holds an
   unowned item, and does "exactly one team" survive? Is a team's backlog its `OwningTeamId` items over all linked projects, or only what the viewer can see?
3. **Creation from a team board.** A team spanning projects has no single default area: which project, area and owning team
   does the new item get? What replaces `BoardTeam.defaultArea` and the dialog text?
4. **SC-09 / P-7 / BR-M4-20.** What happens to owned items, their places and `backlog_orders` rows when a team is deleted or
   unlinked from a project? Today a soft delete rewrites areas to the first team; there is no mechanism for an owner field.
5. **Payload and addressing.** Rows and cards lack project, team and area; every frontend write uses the page's `projectId`.
   Which breaking changes of mod-006 api-contract §§ 3, 7 does each delivery line own (P-10)?
6. **Visibility (P-4).** The three reads, column counts, `FindLiveParentsAsync` and checklists filter by project only. One
   rule for Restricted projects, and which code or scope each surface declares (P-5)?
7. **Workflows (SC-10).** The process is per organization, so projects cannot differ. Keep SC-10 out, or require a per-project process?
8. **Hierarchy (feat-027).** Parent links are same-project only, so a multi-project team board is a forest per project.
9. **Scale and tenant (P-3).** Boards are unpaged; `AssigneesAsync` resolves membership by one project id (`BacklogReadModel.cs`
   l. 212); the lock `(team, project)` and the `(project, type, area)` index have no equivalent for owning-team or area-link filters.
10. **Sprint column (doc-004).** mod-007 is unbuilt and ties iterations to a project; brief § 8 makes the sprint the team's.
    Which line owns the board's sprint column and the planning pane?
