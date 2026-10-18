# Plan — backend (feat-028-org-level-teams)

Stage 4a, deep pass (L). No production code yet. It covers the backend half of
**US-1..US-8** and fixes one interface, [api-contract.md](api-contract.md). The screens, the
searchable multiselect, the dialogs and the new pages belong to stage 6 (`plan-frontend.md`,
not yet written).

Inputs: [spec.md](spec.md) (BR-OL-1..-14, § Existing rules, § NFR), the stories
[US-1](stories/US-1-organization-teams.md)..[US-8](stories/US-8-tenant-containment-and-access.md),
[design.md](design.md) § 13, [review.md](review.md), the status.md gate answers (OQ-1 C, OQ-2..-5 A,
D-1..D-5 A); doc-012 (BR-MX-4..-6, -12, § Existing data, § Decomposition row feat-028 and its Breaks
cell); [ADR-0028](../../adr/0028-matrix-organization-model.md) (rules 2, 4-9, 12) and
[ADR-0029](../../adr/0029-scoped-effective-permissions.md) (§§ 4, 6, 8, "What the delivery lines
implement"); mod-017 `plan-backend.md` §§ 7, 11, 12 and its `review-code.md` CR-7; ADR-0004..0006,
0010, 0011, 0026, 0027; `docs/conventions/backend.md`; `src/backend/AGENTS.md`. The code this plan builds
on was read, not assumed (branch tip `555ef937`, based on `main` at `92942e14`, mod-017 merged): `Team`, `TeamMembership`,
`Project`, `ProjectTeams`, `ProjectDefaultTeam`; `TeamConfiguration`, `ProjectConfiguration`,
`WorkItemConfiguration`, `TeamMembershipConfiguration`, the two backlog configurations; the migrations
`AddWorkItemOwningTeam` and `RetireAreaOwnership`; `OrganizationEndpoints`, `MembershipEndpoints`,
`BoardEndpoints`; the handlers `AddTeam`, `RenameTeam`, `DeleteTeam`, `SetDefaultTeam`, `AddTeamMember`,
`GetTeamOwnedItemCount`, `CreateProject`, `ReorderBacklog`, `GetBacklog`, `CreateWorkItem`, `SaveWorkItem`;
`OrganizationStructure`, `TeamRepository`, `TeamReadModel`, `WorkItemRepository`, `WorkItemTeamOwnership`,
`BacklogRepository`, `TeamBacklogPlaces`, `OrganizationsSaveChanges`, `PermissionCatalog`,
`OrganizationRoleBundles`; the route inventory and the migration tests. **A 68-name check** of the spec's
§ Existing rules found every named test method present in the code; a scan for the symbols this line
removes found the files of § 9.1b and § 9.2 that the table does not name.

- **ADR:** none new. **One clarification of ADR-0028 rule 8 is needed** (D-3, the lock modes of the
  unlink; the same family as mod-017 D-3): the ADR's text stays as accepted, the MR names it, and
  `mod-018-area-workspace`'s amendment can carry it (mod-017 CR-6). Left for the human.
- **bug-005 is not left standing** (D-4): the rewrite adopts CR-7's fix as its base.
- **Lanes: yes** (§ 10.2): nine lanes after a sequential phase 1.
- **No code, comment, test name, log line or commit names the reference tracker.**
- E2E: the standing answer is NO (paused 2026-10-12); not asked, not run; the specs the line breaks are
  listed in § 11 and not edited.

## 1. Decisions

Decisions that depart from a spec or ADR reading, or that only the human could settle, say "for the human"
and carry the option this plan goes on with.

| # | Decision | Why |
|---|---|---|
| D-1 | **One migration, `OrganizationLevelTeams`**: the data drop (OQ-1 C) and the reshaping together, atomic. Its `Down` restores the shape and **empties the teams again**. | Two migrations (a pure drop, then a reshape) would let the first commit and the second fail, leaving a database with its teams gone and the old shape. Under no expand/contract (D-2) there is no code that needs the shape in two steps. A `Down` cannot return a team to "its project": an organization team may have none or many, so the old shape can only be restored empty. Recommended: one. |
| D-2 | **No expand/contract.** Phase 1 is one *model commit* that changes schema, domain, configurations, the structure contract and every consumer at once, and **removes the project-nested team slices with their tests** (retired by name, § 9.1). The new routes arrive after it. | mod-017 staged an additive migration because its old and new models could live side by side. Here the break is total (a team loses its project; every FK onto `teams (project_id, id)` moves) and the data is dropped, so a dual-write phase would be code written to be deleted. The branch's build stays green; the product has no way to create a team between the model commit and the lane that restores it, which no merge sees. |
| D-3 | **The link locks.** The unlink locks the pair's links, the unlinked team's and the destination's, in **one statement ordered by team id, `FOR NO KEY UPDATE`**; the deletion locks the team `FOR NO KEY UPDATE` (as mod-017 D-3) and then **all of the team's links in project-id order, `FOR NO KEY UPDATE`**; every binder takes the link `FOR SHARE`. ADR-0028 rule 8 reads `FOR UPDATE` for the unlink. For the human. | `NO KEY UPDATE` conflicts with every binder's `FOR SHARE` and with another unlink or deletion, and not with a bare `FOR KEY SHARE`, which `FOR UPDATE` would queue behind: the cycle mod-017 D-3 found (a key share held while waiting for an item the unlink locks) cannot form even if a writer ever skips the `SHARE` that § 7.4 requires of it, so this is defence in depth and the same mode as the deletion's team lock. The row's `DELETE` at the end takes the stronger lock itself, when nothing else holds the row (§ 7.4). The destination is locked `NO KEY UPDATE` too, one mode for both rows: it queues a binder of the destination and a second unlink to it, which is all the ADR's `FOR SHARE` on it buys. |
| D-4 | **A creation that may default** (no `team` sent, or a child, whatever team its parent has) **locks every link of the project `FOR SHARE` in team-id order, then resolves the default once** under that lock. The retry loop and the single-row lock of mod-017 go. | This is the fix `review-code.md` CR-7 asked for as "a simplification": every link lock a creation takes is then in the unlink's and the deletion's order, so the three-request cycle (a deletion of the named default, a second deletion, a creation) cannot form. **That is bug-005's whole content, so this line's rewrite leaves it not standing**: the orchestrator may withdraw bug-005 from the queue, or keep it as the regression test `Create_VersusTheDeletionOfTheNamedDefault_AndAnUnlink_NoDeadlock` (§ 9.4). If bug-005 lands first, this line rebases onto its change of `WorkItemRepository.LockDefaultTeamAsync` and `CreateWorkItemHandler`; the conflict is textual. For the human (R-14). |
| D-5 | **The unlink takes no team row.** It judges the unlinked team's and the destination's *status* on a fresh read **after** it holds the links. | ADR-0028 rule 8 puts team rows (step 2) before links (step 3), but the unlink binds nothing to a team: a deletion needs the team row first and then the link, the unlink needs the link alone, and the link `NO KEY UPDATE` already queues it behind the deletion's. Re-reading the status after the lock gives the right answer in both orders (404 for a team deleted meanwhile, § 7.3 C-7). |
| D-6 | **Routes.** The unlink is `POST …/projects/{p}/teams/{t}/unlink` with a body, not `DELETE`; its count is `GET …/unlink-count`; the deletion's counts are `GET …/teams/{t}/deletion-counts`; a link has two doors, `POST …/projects/{p}/teams` and `POST …/teams/{t}/projects`. | A `DELETE` with a body is dropped by some proxies, and the project's other state changes are `POST` verbs (`deactivate`, `restore`). The unlink's count must not be `…/owned-items/count`: US-1 lists "an owned-items request" at the project-nested address among the paths that no longer exist, and US-8 keeps "the unlink's count" at a project's address; one name cannot be both, so the count is renamed (a spec inconsistency this plan settles, § 11). |
| D-7 | **Second codes** (spec § Roles): the route declares the code of the end whose address it is, and the handler asks the other from `IOrganizationAccess` **first of all**, before any id is looked at (as `SaveWorkItem` asks `workitems.link.write`). A creation with a non-empty `memberIds` also needs `team.write`. For the human; recommended: yes. | A role without the second code learns nothing about any id and nothing changes. The spec names the link codes for a creation that links; staffing a team at creation is the same argument (R-3), and `mod-019-scoped-access` would otherwise inherit an unspecified path. Under today's bundles only «Администратор» holds all, so nothing differs. |
| D-8 | **The unlink's `destination` wrapper requires its `teamId` key.** `destination` absent, `null` or `{}` is 400 `destination_required`; `{ "teamId": null }` is «Без команды». | `{}` meaning «Без команды» would let a client defect move every item to no team silently (mod-017 D-2's `team: {}` is a different case: nothing moves on a save). The endpoint binds the body as a `JsonElement` and reads the key. |
| D-9 | **Descriptions.** `teams.description` and `projects.description` are `text NULL`, stored exactly as sent; the empty string is stored as `NULL`; at most 2 000 UTF-16 code units; a check `description IS NULL OR char_length(description) BETWEEN 1 AND 2000` is the backstop. A new value type `EntityDescription` (`TryCreate(string?, out EntityDescription?)`) is the one rule. | BR-OL-3 and R-13: "kept as typed; an empty one is no description". Counting UTF-16 units is `WorkItemText`'s precedent. |
| D-10 | **The `23503` backstop keeps mod-017 D-15's answer** (404 `team.not_found`) on the renamed owner key `fk_work_items_project_teams_project_id_owning_team_id` and default key `fk_projects_project_teams_id_default_team_id`. | The locks make it unreachable; whichever answer, the write stores nothing. It is no longer certain whether the cause is an unlink (409) or a deletion (404); the not-found is the existing, tested mapping, and the constants and `OwningTeamBackstopTests` follow the new names. |
| D-11 | **`IOrganizationStructure`** takes the organization in every by-id read (api-contract § 6): `FindProjectAsync(org, project)`, `FindTeamAsync(org, team)`, the new `FindProjectTeamAsync(org, project, team)` (the team and whether it is linked, in one statement), `ListLinkedTeamsAsync`, `ListLinkedProjectsAsync`; `ListActiveTeamsAsync(project)` has no caller and goes. The callers drop their `project.OrganizationId != organizationId` comparison. | mod-017 S-2 and BR-OL-1: the contract answers null for a foreign object, so the check cannot be forgotten. |
| D-12 | **Slice names are kept** where the test names the spec rewrites depend on them: `AddTeam` (now the organization-level create), `RenameTeam`, `DeleteTeam`, `GetTeam`, `GetTeams` (the **project's** linked teams, at the project's address), `GetTeamOwnedItemCount` (the unlink's count). New slices: `GetOrganizationTeams`, `SetTeamDescription`, `SetProjectDescription`, `GetTeamProjects`, `GetTeamDeletionCounts`, `LinkTeamsToProject`, `LinkProjectsToTeam`, `UnlinkTeam`. | P-1: "rewritten keeps the name and the intent" (spec § Existing rules). |
| D-13 | **A team reorder's "departed" drop is per project** (`BacklogRepository.SaveOrderAsync`): the places held on (team, level) are read joined to the items' project, so a reorder in project P never deletes a place of the team's items in project Q. | OQ-3 A. Today `held` is every place of (team, level), and `held.Except(ids)` would delete another project's places as "departed". Found by reading the code; a test pins it (`Reorder_InOneProject_KeepsTheTeamsPlacesInTheOther`). |
| D-14 | **A link request with an absent, `null` or empty list is a no-op 204**; duplicates collapse; an old `{ "name" }` body to `POST …/projects/{p}/teams` therefore creates nothing. | There is nothing to link and nothing to refuse; adding a validation code for a defect only the panel could cause is not worth a registry entry. |
| D-15 | **Lanes: yes**, reference use case **`UnlinkTeam`** (§ 10.2). | It carries every pattern the other lanes copy: the second code, the locks in one order, a fresh read after them, the set-based reassignment through the module contracts, and the concurrency gate tests. |
| D-16 | A rewritten test whose **name states a retired rule** is renamed, the commit message listing old → new; one whose name survives keeps it (mod-017 D-16). | P-1 traceability is the message. |

## 2. Affected slices

Module → use case → change. "New" and "Gone" are folders. Lanes are § 10.2's.

| Module | Use case | Change | Lane |
|---|---|---|---|
| Organizations | `UnlinkTeam` | **new**, `POST …/projects/{p}/teams/{t}/unlink` | phase 1 |
| Organizations | `AddTeam` | organization-level create: `description`, `memberIds`, `projectIds`; no project in the address | L1 |
| Organizations | `DeleteTeam`, `GetTeamDeletionCounts` | delete over every linked project, no guard; **new** counts | L2 |
| Organizations | `LinkTeamsToProject`, `LinkProjectsToTeam` | **new** | L3 |
| Organizations | `GetOrganizationTeams`, `GetTeam`, `GetTeams`, `GetTeamProjects` | **new**; moved to the team's address; linked teams with the mark; **new** | L4 |
| Organizations | `RenameTeam`, `SetTeamDescription`; `GetTeamMembers`, `AddTeamMember`, `RemoveTeamMember`, `SearchTeamMemberCandidates` | moved off the project; **new** description | L5 |
| Organizations | `SetDefaultTeam`, `GetTeamOwnedItemCount` | link-based; `project.write`; the unlink's count | L6 |
| Organizations | `CreateProject`, `SetProjectDescription`; `GetProject`, `RenameProject`, `RestoreProject` | no team, `description`, `teamIds`; **new**; `description` in the answer | L7 |
| Organizations | `GetProjects`, `GetDeactivatedProjects`, `DeactivateProject`, the area slices | none (re-run for regression) | - |
| Boards | `GetBacklog`, `GetBoard`, `ReorderBacklog` | the team must be linked; the reorder's lock sequence and the per-project drop (D-13) | L8 |
| Boards | `GetProjectBacklog`, `GetProjectBoard`, `ReorderProjectBacklog` | the structure signature only | phase 1 |
| WorkItems | `SaveWorkItem`, `CreateWorkItem` | the binder locks the **link**; the default lock of D-4 | L9 |
| WorkItems | `GetWorkItem`, `GetWorkItems`, `SetWorkItemParent`, `GetParentCandidates` | the structure signature only | phase 1 |
| Shared | `IOrganizationStructure`, `IWorkItemTeamOwnership`, `ITeamBacklogPlaces`, `ITeamRepository`, `IProjectRepository`, `ITeamReadModel`, `IMemberReadModel`; new `IProjectTeamRepository` | § 6 | phase 1 |

## 3. Domain (`Mustask.Domain`)

**Organizations**

- `Team`: `ProjectId` goes; `OrganizationId` and `Description (string?)` come. `Create(organizationId, name,
  description?)`, `Rename`, **`Describe(EntityDescription?)`**, **`Delete()`** — the status is terminal and
  there is **no guard** (`Delete(int)` and `TeamLastTeamCannotBeDeleted` go).
- **`ProjectTeam`** (new, the link; entity): `OrganizationId`, `ProjectId`, `TeamId`, `LinkedBy`, `LinkedAt`;
  `Link(organizationId, projectId, teamId, linkedBy, linkedAt)`. No status: a row exists or it does not.
- `Project`: `Description`; **`Describe(EntityDescription?)`**; `Create(organizationId, name, key, description?,
  createdAt, createdBy)` returns **`NewProject(Project, Area Root)`** — no team (BR-OL-2). `NameDefaultTeam`
  is unchanged.
- **`EntityDescription`** (new, value type): `TryCreate(string? raw, out EntityDescription? value)`; false when over
  `OrganizationLimits.DescriptionMaxLength` (2 000); a `null` or empty `raw` is "no description" and returns true with
  `null`. `LengthReason` is the field reason `organizations.description_length` (D-9).
- `ProjectDefaultTeam`: the same two functions; the inputs are now the project's **linked active teams**.
- **`ProjectTeams`** (rewritten, pure): `TeamFacts(Id, OrganizationId, Status, IsLinked)`; `EnsureBindable(TeamFacts?,
  organizationId)` — the table of api-contract § 0.3 (no team, another organization, deleted → `NotFoundException(TeamNotFound)`;
  active and not linked → `ConflictException(TeamNotInProject)`); `EnsureLinkable(TeamFacts?, organizationId)` (not an
  active team of the organization → `TeamNotFound`); **`EnsureDestination(TeamFacts? destination, Guid unlinkedTeamId)`**
  (unknown, foreign or deleted → `TeamNotFound`; the unlinked team itself or not linked → `TeamNotInProject`); `ChildTeam`
  unchanged. One rule for the save, the create, the default naming and the unlink.
- `OrganizationErrorCodes`: remove `TeamLastTeamCannotBeDeleted` (the catalog entry is `deprecated`); add
  **`DescriptionLength`** (`organizations.description_length`) and **`TeamDestinationRequired`**
  (`organizations.team.destination_required`).
- `OrganizationLimits`: `DescriptionMaxLength = 2000` and `LinkedByMaxLength = 255` — its own constant, equal to
  `CreatedByMaxLength` by coincidence of the identifier it holds (`backend.md`, "Named values", 5).

**Identity.** `PermissionCatalog` gains `OrganizationsTeamCreate`, `OrganizationsTeamDelete`, `OrganizationsTeamLink`,
`OrganizationsProjectLink` (`PermissionScope.Organization`, Russian descriptions «Создание команд», «Удаление
команд», «Связи команды с проектами», «Связи проекта с командами»); `OrganizationsTeamWrite`'s description
narrows to «Название, описание и участники команды»; `OrganizationsProjectWrite`'s to «Создание, переименование и описание
проектов, команда по умолчанию». `OrganizationRoleBundles.Admin` takes every organization code, so the four join
«Администратор» and no other role (ADR-0029 § 4; the matrix test, § 9.4).

## 4. Database

### 4.1 Entity configurations (Infrastructure)

- `TeamConfiguration`: `OrganizationId` required with a `Restrict` foreign key to `organizations`; **alternate key
  `(organization_id, id)`**; `Description` (`text`, check `ck_teams_description_length`); the unique index
  `OrganizationsConstraints.TeamName` = **`ix_teams_organization_id_normalized_name`** on `(organization_id,
  normalized_name)` `WHERE status = 'Active'`. `ProjectId`, its alternate key `(project_id, id)`, its foreign key,
  `ix_teams_project_id` and the per-project name index are removed.
- **`ProjectTeamConfiguration`** (new, table `project_teams`): primary key `(project_id, team_id)`
  (`OrganizationsConstraints.ProjectTeamPrimaryKey = "pk_project_teams"`); properties `OrganizationId`, `LinkedBy` (255), `LinkedAt`;
  foreign keys `(organization_id, project_id)` → `projects (organization_id, id)` and `(organization_id, team_id)` →
  `teams (organization_id, id)`, both `Restrict`; index `(team_id, project_id)` named `ix_project_teams_team_id_project_id`. EF adds the
  indexes of the two composite foreign keys by convention; they stay.
- `ProjectConfiguration`: `Description` (`ck_projects_description_length`); **alternate key `(organization_id, id)`**; the
  default-team key is retargeted: `(id, default_team_id)` → `project_teams (project_id, team_id)` (the link's primary key),
  `Restrict`, named `fk_projects_project_teams_id_default_team_id` (`OrganizationsConstraints.ProjectDefaultTeamKey`).
- `WorkItemConfiguration`: the owner key is retargeted to the **link**: `(project_id, owning_team_id)` → `project_teams
  (project_id, team_id)`, `Restrict`, named `fk_work_items_project_teams_project_id_owning_team_id`
  (`WorkItemsConstraints.OwningTeamKey`). The index `ix_work_items_project_id_owning_team_id_type_id` keeps serving it,
  the unlink's reassignment and the link's delete check; the unique `(id, owning_team_id)` and the place key
  `fk_backlog_places_work_items_owner` are unchanged.
- `TeamMembershipConfiguration`: the single-column key on `team_id` is replaced by the composite **`(organization_id, team_id)` →
  `teams (organization_id, id)`**, `Restrict`, named `fk_team_memberships_teams_organization_id_team_id` (ADR-0028 rule 4:
  the organization copy becomes a real key). The key onto the organization membership and its cascade are unchanged.
- `BacklogOrderConfiguration`, `BacklogPlaceConfiguration`: unchanged in the model.
- The generated constraint names are confirmed by the migration; `OrganizationsConstraints`, `WorkItemsConstraints` and
  `OrganizationsSaveChanges` follow them, and `OrganizationLevelTeamsSchemaTests` pins every name the code maps (§ 9.4).

### 4.2 The migration — `OrganizationLevelTeams` (the data drop, OQ-1 C)

Generated with `dotnet ef migrations add OrganizationLevelTeams --project src/backend/Mustask.Infrastructure`,
then edited by hand: the data step is added **first** in `Up`, and the generated `defaultValue` of the new
`teams.organization_id` is **removed** (the table is empty at that step; a default would let a row in with no
organization). One migration, atomic (D-1). If `main` gains migrations first, it is regenerated on the fresh snapshot
(`backend.md`, "Parallel branches").

`Up`, in this order:

1. **The drop and its log**, one `DO` block: count, delete and `RAISE NOTICE` — `DELETE FROM backlog_places` (every row is a
   team place; the project places are another table), `DELETE FROM backlog_orders`, `UPDATE work_items SET owning_team_id = NULL
   WHERE owning_team_id IS NOT NULL` (**`changed_at` is not written**, BR-OL-13), `UPDATE projects SET default_team_id = NULL
   WHERE default_team_id IS NOT NULL`, `DELETE FROM team_memberships`, `DELETE FROM teams` (a deleted team's row too). The order
   is the order of the keys: the places before the orders (their key) and before the items' owner (the place key is checked at
   the end of the `UPDATE`), the orders and the memberships before the teams. The notice reads
   `feat-028: N teams, M team memberships, P team places, Q team order rows and R default-team settings dropped; S work items made without a team`.
2. Drop `fk_work_items_teams_project_id_owning_team_id`, `fk_projects_teams_id_default_team_id`, the single-column key of
   `team_memberships` onto `teams`, `fk_teams_projects_project_id`, `ix_teams_project_id_normalized_name`, `ix_teams_project_id`,
   the alternate key `(project_id, id)` and the column `teams.project_id` (the names as the model snapshot holds them).
3. `teams`: add `organization_id uuid NOT NULL`, `description text NULL`, `ck_teams_description_length`, the alternate key
   `(organization_id, id)`, the key to `organizations`, the unique partial name index. `projects`: add `description text NULL`,
   `ck_projects_description_length`, the alternate key `(organization_id, id)`.
4. Create `project_teams` with its keys and index; add the three retargeted keys (items' owner, projects' default,
   team memberships' composite).

`Down` restores the **shape** the previous build needs, and first empties what the old shape cannot hold: it repeats step 1's
deletes (without the notice), drops the three retargeted keys and `project_teams`, drops the new columns, keys and index, re-adds
`teams.project_id uuid NOT NULL` with its foreign key, alternate key and the two old indexes, and re-adds the two old keys onto
`teams (project_id, id)` and the single-column membership key. The previous build needs a stand reset after a `Down`; the
migration test rolls back past it and applies it again.

### 4.3 Data dropped (named in the MR; BR-OL-13)

- **Dropped:** every team (any status, any organization, any project — a deleted team's row included); their team memberships;
  every row of `backlog_places` and `backlog_orders` (the team places and the team order rows); every `projects.default_team_id`;
  and the owning team of every item that had one (the item stays, «Без команды»).
- **Remapped: none.** No team is renamed, merged or linked; names are never compared.
- **Not dropped:** organizations, organization memberships and roles, projects (deactivated ones too), areas, work items (their
  project, number, identifier, assignee, state, estimates, parent and children, «Область», `changed_at`), `project_backlog_places`
  and `project_backlog_orders`. No sprint, dashboard, area link or grant exists yet, so none is dropped with the teams.
- **Logged:** the notice of § 4.2 step 1; `OrganizationLevelTeamsMigrationTests` reads it from the connection.

### 4.4 Volumes and indexes (ADR-0028 rule 13, as far as this line reaches)

Teams lead with the organization (`ix_teams_organization_id_normalized_name`, the alternate key); links lead with the project
(the primary key) and with the team (`ix_project_teams_team_id_project_id`), both directions. The deletion's statements run
`WHERE project_id = ANY(@projects) AND owning_team_id = @team` on `ix_work_items_project_id_owning_team_id_type_id`. The
team views across projects (an index led by the owning team alone) are `feat-040`'s.

## 5. Endpoints

The route table, the shapes and the error codes are [api-contract.md](api-contract.md) §§ 1–4. For the files:

- `OrganizationEndpoints.cs`: the `projects/{projectId}/teams` group keeps `GET` and `POST …/teams`, `…/teams/{teamId}/unlink`
  and `…/unlink-count`; `PUT …/default-team` declares `OrganizationsProjectWrite`; `PUT …/description` is new; a new group
  `organizations.MapGroup("/{organizationId}/teams")` maps the nine team-address routes (list, create, read, name, description,
  delete, deletion-counts, projects read, projects link). Each declares the code of api-contract § 4 and carries
  `.ProducesProblem` for its statuses. Request records: `AddTeamRequest(Name, Description, MemberIds, ProjectIds)`,
  `DescriptionRequest(Description)`, `LinkTeamsRequest(TeamIds)`, `LinkProjectsRequest(ProjectIds)`, **`UnlinkRequest(JsonElement?
  Destination)`** (D-8), and `CreateProjectRequest` gains `Description` and `TeamIds`.
- `MembershipEndpoints.cs`: `TeamPath` becomes `OrganizationPath + "/teams/{teamId}"`; the four member routes lose the project; the
  `Location` header of an add is rebuilt.
- `BoardEndpoints.cs`: unchanged (the team views stay at the project's address, BR-OL-11).
- `PermissionCatalog`, `OrganizationRoleBundles` (§ 3). `OrganizationAddressedRoutes.All` (§ 9.4): 8 rows out, 16 in, 2 recoded;
  its `Memberships` property (`Skip(All.Count - 10)`) is rebuilt over the member routes' new place, and `Path` learns no new token.
- **Phase 1 writes `OrganizationEndpoints.cs`, `MembershipEndpoints.cs` and the request records in full** (04-backend.md § 4c); a
  route whose handler is not yet written maps to the lane's `Command` and a handler class with its final signature, so the file
  compiles once and no lane touches it.
- `OrganizationsOpenApiTests` and the route inventories follow (§ 9.4).

## 6. In-process contracts that change

(api-contract § 6 has the signatures of the two that other modules see.)

| Contract | Change |
|---|---|
| `IOrganizationStructure` | the five reads of api-contract § 6; `ProjectInfo.DefaultTeamId` is resolved in the statement that reads the project, over the project's **links** (`NamedActive` = the setting when its link exists and the team is active; `ActiveTeams` = linked active teams; `AnActive` = any one of them); `TeamInfo.ProjectId` goes; `ListActiveTeamsAsync` goes. The by-id reads filter by `(organization_id, id)`; a project-owned read filters by `(project_id, id)` with the project resolved under the organization in the same call (ADR-0028 rule 4). |
| `IWorkItemTeamOwnership` | api-contract § 6: `CountOwnedByProjectAsync` (one `GROUP BY project_id`), `LockOwnedAsync` and `ReassignOwnedAsync` over a **set of projects** and an optional destination — an unlink passes one project, a deletion every linked project, so the statements do not grow with the number of projects. `ReleaseOwnedAsync` is replaced by `ReassignOwnedAsync(…, null, now)`. **`mod-007-iterations`** adds its open-sprint step before `LockOwned` (rule 8 step 4). |
| `ITeamBacklogPlaces` | adds `DropForTeamInProjectAsync(teamId, projectId)`: `DELETE FROM backlog_places p USING work_items i WHERE i.id = p.work_item_id AND i.project_id = {projectId} AND p.team_id = {teamId}` — the unlink's step, **before** the reassignment (the place key). |
| `ITeamRepository` | `LockForNoKeyUpdateAsync(org, team)`, `LockForShareAsync(org, team)`, `LockManyForShareAsync(org, ids)` (ordered by id), `IsActiveNameTakenAsync(org, normalized, exceptId)`, `FindNameAsync`, `Add`, `SaveAsync`. **Gone:** `LockProjectTeamsAsync`, `GetMarkAsync` (nothing answers a mark but the project's list). |
| **`IProjectTeamRepository`** (new) | `LockLinksAsync(org, project, teamIds)` (`NO KEY UPDATE`, ordered by team id; **returns the teams whose links the statement locked**, A-9), `LockLinkForShareAsync(org, project, team)` (one statement, **answers whether it found the link**, A-9), `LockTeamLinksAsync(org, team)` (`NO KEY UPDATE`, ordered by project id; returns the project ids), `FindLinkedAsync(project, teamIds)`, `LinkAsync(org, pairs, linkedBy, now)` (one `INSERT … SELECT unnest … ON CONFLICT (project_id, team_id) DO NOTHING`), `RemoveLinkAsync(project, team)`, `RemoveTeamLinksAsync(team)`, `Add(ProjectTeam)` (for the creates, whose team or project is new). Every statement carries the organization. |
| `IProjectRepository` | `Add(NewProject)` adds the project and its root only; `ClearDefaultTeamAsync(project, team)` is kept; **`ClearDefaultTeamAsync(org, team)`** clears every setting in the organization that names the team (one statement, led by the organization). |
| `ITeamReadModel` | `ListOrganizationTeamsAsync(org)` (teams, then their active projects: two statements), `FindAsync(org, team)`, `ListLinkedAsync(org, project, userId)` (the `ProjectTeam` rows: name, mark, `isMember`, one statement), `ListProjectsOfTeamAsync(org, team)`, `ListLinksWithProjectsAsync(org, team)` (every status, for the counts). `ReachableTeams` of `Reachability` is replaced by `ActiveTeamsOf(org)`, `LinkedTeams(org, project)` and `LinkedProjects(org, team)`. |
| `IMemberReadModel` | adds `FilterMembersAsync(org, userIds)`: which of the ids hold a membership, one statement (the create's member check). |
| `IWorkItemRepository` | `LockTeamAsync(org, team)` becomes **`LockTeamLinkAsync(org, project, team)`** (`SELECT team_id FROM project_teams WHERE organization_id = … AND project_id = … AND team_id = … FOR SHARE`, **answers whether the statement returned the row**, A-9); `LockDefaultTeamAsync(project)` becomes **`LockProjectLinksAsync(org, project)`** (all the project's links `FOR SHARE` ordered by team id and returned, then `DefaultTeams.ResolveAsync` once **over the links that statement held**; D-4, A-9). `SaveAsync` maps `23503` on the renamed owner key (D-10). |
| `IBacklogRepository` | `LockAsync(scope)`: the team `FOR SHARE` and active, then **the link `(project, team)` `FOR SHARE` and present** (else `false` → 404), then the members `FOR KEY SHARE`, the order row, as before. `SaveOrderAsync` drops departed places **of this project only** (D-13). |

`VerticalSliceRules` checks `using Mustask.Application.Features.<module>.<slice>` only; the module-root contracts pass.

## 7. Locks and concurrency (ADR-0011 echelon 3, ADR-0028 rule 8)

### 7.1 One order

The order every write follows, taking only what it needs and re-reading what it checks after locking. Rows of one step are
locked in **one statement ordered by id** (the `ORDER BY` is applied before the lock):

| Step | Lock | Taken by |
|---|---|---|
| 2 | `teams` row: `FOR SHARE` by a **team binder**; `FOR NO KEY UPDATE` by a deletion, a rename, a description write | link creation (a team end and a project end), a project creation naming teams, a member add, a team reorder; `DeleteTeam`, `RenameTeam`, `SetTeamDescription` |
| 3 | `project_teams` rows, in **(project id, team id)** order: `FOR SHARE` by an **item binder**; `FOR NO KEY UPDATE` by the unlink (the pair, by team id) and the deletion (the team's links, by project id) | a save or create naming a team, `SetDefaultTeam`, a team reorder, a creation that may default (**every link of the project**, D-4); `UnlinkTeam`, `DeleteTeam` |
| 5a | `work_item_counters`, `work_item_hierarchy_locks` of the project | create, a parent write (mod-003's place) |
| 5b | `work_items` rows: `FOR UPDATE` (a save; the unlink's and the deletion's owned set); `FOR NO KEY UPDATE` (a parent as a link end); `FOR KEY SHARE` (a team reorder's members) | save; `UnlinkTeam`, `DeleteTeam`; create beneath a parent; reorder |
| 6 | the order row `FOR UPDATE` | a team reorder |
| after | the `projects` row `FOR NO KEY UPDATE` | `SetDefaultTeam`; the clearing of a setting by the unlink and the deletion; deactivate, restore, rename, describe |

Step 4 (sprints) is `mod-007-iterations`'s and sits between 3 and 5. The project row is **never taken before a team or a link
row**: a default naming holds a link and then the project row, an unlink and a deletion hold links and items and then the project
row (to clear a setting), a deactivation holds the project row alone.

### 7.2 Sequences

| Use case | In this order |
|---|---|
| **`UnlinkTeam`** (reference) | the second code (`team.link`) → address: project reachable, team an active team of the organization (404s) → `destination` present (400) → destination, if a team, an active team of the organization (404) → **links `(P, {T, D})` `NO KEY UPDATE`, one statement by team id, returning the links it locked** → fresh reads: **T's status** (deleted → 404), **D's status** (deleted → 404); *linked* is **what that statement returned**, never a later read (A-9) → **(P, T) not held → 204, nothing else** → D is T or not held → 409 → `LockOwnedAsync([P], T)` → `DropForTeamInProjectAsync(T, P)` → `ReassignOwnedAsync([P], T, D, now)` → *(mod-007's step)* → `ClearDefaultTeamAsync(P, T)` → `RemoveLinkAsync(P, T)` → flush. |
| `DeleteTeam` | address (404) → team `NO KEY UPDATE`, re-check Active (404) → `LockTeamLinksAsync(T)` → the project ids of the links (fresh) → `LockOwnedAsync(projects, T)` → `DropForTeamAsync(T)` → `ReassignOwnedAsync(projects, T, null, now)` → `ClearDefaultTeamAsync(T)` → `RemoveTeamLinksAsync(T)` → `EndAllAsync(T)` (memberships) → `team.Delete()` → flush. Constant statements. |
| `LinkTeamsToProject` | second code → project reachable (404) → `LockManyForShareAsync(org, teamIds)` → fresh: every id an active team of the organization (else 404, nothing linked) → `LinkAsync` (one statement). |
| `LinkProjectsToTeam` | second code → team `FOR SHARE`, Active (404) → every project id an active project of the organization (one statement; else 404) → `LinkAsync`. |
| `AddTeam` | second codes (§ 0.1) → values (400, every reason) → projects (one statement; 404) → members (`FilterMembersAsync` + directory; 409) → name pre-check (409) → `Team.Create`, links and memberships added → **one flush** (the name index and the membership keys are the races' backstops). No lock: the team is new. |
| `RenameTeam`, `SetTeamDescription` | address → team `NO KEY UPDATE`, Active → values → (name pre-check) → flush. |
| `AddTeamMember` | address → team `FOR SHARE`, Active → body → organization member (409) → duplicate (409) → flush (as today, keyed by the organization). |
| `SetDefaultTeam` | address (project reachable) → team id parsed → **link `(P, T)` `FOR SHARE`, one statement that returns the row** → `FindProjectTeamAsync` fresh (the team's status) → `EnsureBindable` with *linked* = what the lock returned (A-9) → project row `NO KEY UPDATE`, status re-read → `NameDefaultTeam` → flush. Clearing names no team and takes the project row alone. |
| `CreateProject` | second codes → organization active → values (400) → `teamIds`: `LockManyForShareAsync`, every one an active team (404) → name, then key pre-checks (409) → project, root and links added → flush. |
| `SaveWorkItem`, `CreateWorkItem` | as mod-017 § 7.2 with the team lock replaced: an explicit non-null team takes **`LockTeamLinkAsync(org, P, T)`** before the item and the number; a creation that may default takes **`LockProjectLinksAsync`** (D-4) before the number; «Без команды» takes nothing. The decision is a fresh `FindProjectTeamAsync` (the team's status and organization) and `EnsureBindable`, with *linked* = **what the lock statement returned** (A-9): a link committed after the lock is held by nobody and is 409 `not_in_project`, and for a creation that defaults it is never the default. |
| Team reorder | address (the team must be **linked** and reachable: `FindProjectTeamAsync`, else 404) → team `FOR SHARE`, Active → **link `(P, T)` `FOR SHARE`, present** → members `FOR KEY SHARE` → order row `FOR UPDATE` → the locking read → stale check → drop the departed places of this project and upsert. |
| Reads, project reorder | unchanged. |

### 7.3 The races, and why each ends in one order

Every row asserts **both orders, no 5xx**, and the invariants *no item owned by a team not linked to its project* (the key), *no setting
names an unlinked or deleted team*, *no place names a team that does not own its item*, *no link names a deleted team*.

| # | Race | Decided by | First wins → | Second |
|---|---|---|---|---|
| C-1 | **Unlink** × a save that sets T | the save's link `SHARE` against the unlink's `NO KEY UPDATE` | the save commits; the unlink re-reads and **reassigns it with the rest** (D gets 8 in the spec's example) | the save finds no link: 409 `not_in_project`, stores nothing |
| C-2 | Unlink × a **create** naming T | the same | taken with the rest | 409, creates nothing, consumes no number |
| C-3 | Unlink × a **child created beneath** an item T owns | the child holds every link of the project `SHARE` first (D-4), then the parent `NO KEY UPDATE`; the unlink holds link T `NO KEY UPDATE` | the child commits first: the unlink's owned set is read after it (`LockOwned` is a new statement), so the child is reassigned with the rest | the child waits at its link lock, then reads its parent's *new* team (D or none): never refused for a team |
| C-4 | Unlink × a **default naming** of T | the naming's link `SHARE`, then the project row | the naming commits; the unlink's `ClearDefaultTeamAsync` clears it | 409 `not_in_project`; the setting stays where it was |
| C-5 | Unlink × a **reorder** of T's backlog at P | the reorder's link `SHARE` and member `KEY SHARE` against the unlink's link lock and item `FOR UPDATE` | the reorder commits (places written); the unlink's `DropForTeamInProject` deletes them and the items arrive unplaced | 404 `team.not_found` (the link is gone), writing no place |
| C-6 | **Two unlinks** that name each other (T→D, D→T) | both lock the pair in team-id order in one statement | the first applies | the second finds its destination unlinked: 409 `not_in_project` |
| C-7 | Unlink × the **deletion of the unlinked team** | the deletion's team lock, then its link locks, against the unlink's link lock | unlink first: the deletion works on what is left (T is no longer linked to P) | deletion first: the unlink re-reads T **deleted** → 404 `team.not_found` |
| C-8 | Unlink × the **deletion of the destination** D | the deletion's link locks against the unlink's lock on `(P, D)` | unlink first: D receives the items, then the deletion releases them to none | deletion first: D deleted → 404 `team.not_found`, nothing changes |
| C-9 | Unlink × a **link** of the same pair | `INSERT … ON CONFLICT` waits on the unlink's row | unlink commits, the link is inserted anew (nothing moves back) | link first: the link is a no-op and the unlink then removes it |
| C-10 | **Link** × the deletion of T (either end) | the link holds the team row `SHARE`, the deletion `NO KEY UPDATE` | link first: the deletion re-reads the team's links and removes the new one | deletion first: the link re-reads the team **deleted** → 404; no link to a deleted team remains |
| C-11 | **Two links** of one unlinked pair | the primary key, `ON CONFLICT DO NOTHING` | exactly one row, recording the first | a no-op 204; no 5xx |
| C-12 | **Deletion** × a save, a create, a default naming, a team add, a reorder, a rename, a link, an unlink of T | the team row (`NO KEY UPDATE` against a team binder's `SHARE`: an add, a rename, a link, a reorder); the link rows (`NO KEY UPDATE` against an item binder's `SHARE`: a save, a create, a default naming; and against the unlink's lock) | the write commits; the deletion takes what it gave T with the rest | 404 `team.not_found` (the item binders re-read the team **deleted**, not merely unlinked) |
| C-13 | Deletion × a child created beneath T's item, named default or none | D-4 | as C-3 | as C-3 |
| C-14 | **Two deletions** of T; two deletions that empty a project | the team row serializes; the guard is gone | the first deletes | the second 404 (same team) / both done (two teams of one project) |
| C-15 | Create "SATURN" × create "SATURN"; create × rename to it | the unique index, mapped to `name_taken` | one team | 409 `name_taken` |
| C-16 | Project create naming T × the deletion of T | the creation's team `SHARE` against `NO KEY UPDATE` | the project is created linked, then the deletion removes the link | 404 `team.not_found`, no project |
| C-17 | Project **deactivation** × a link to it (either end) | the link only reads the project's status (ADR-0026 rule 8); the foreign key takes a key share the deactivation's `NO KEY UPDATE` does not wait on | both succeed in either order ("a link just before the deactivation") | - |
| C-18 | Reorders of one team in **two projects** | the order row `FOR UPDATE` serializes them; each writes its own project's places | both complete; neither moves the other's places (D-13) | - |

### 7.4 Deadlock reading

- **One order for links.** Every request that holds several link rows takes them in one statement in **(project id, team id)**
  order: the unlink the pair of one project by team id, the deletion one team's links by project id, a creation every link of one
  project by team id. A request that holds one link and waits for another is therefore waiting for a row *above* it in the order,
  so the waits-for graph over links has no cycle; a rename, a binder or a deactivation holds one row of one kind.
- **Items.** The unlink and the deletion lock items only after their links, so a holder of an item they wait for is a save that
  took its own link `SHARE` before the item (it waits for no link while holding the item), a save that names no team (it waits for
  no link), or a creation of a child (which holds every link of the project `SHARE` before the parent, so the unlink could not hold
  a link `NO KEY UPDATE` of that project at the same time — the two are never in flight together). **mod-017 D-3's cycle** (a child's
  key share on the owner's row while the deletion waits for the child's parent) needs a request that holds a parent and waits for a
  link the unlink holds; D-4 and `NO KEY UPDATE` rule it out twice.
- **CR-7's three requests** (a deletion of the named default, a second deletion, a creation): the creation takes every link of the
  project in one ordered statement, so it never waits for a link while holding another out of order (a test reproduces it, § 9.4).
- **Teams.** A deletion holds *one* team row and then links; a link creation holds several team rows `SHARE` in id order and no link; a
  binder holds a link and no team row. No request holds a link and waits for a team row (the unlink takes none, D-5; the reorder takes
  its team row *before* its link), so team rows and link rows never cross.
- **Project rows.** Taken last, one at a time by every writer (the clearing statements update at most the one row naming the team);
  a deactivation takes only that row.
- **The `DELETE` of a link** needs no other holder of its row. The deleter holds it `NO KEY UPDATE` and every taker of `FOR SHARE` is
  queued behind it. The only transactions that take a `KEY SHARE` on a link row are those that write an item's owner or a project's
  setting through the foreign keys, and each holds the row `SHARE` (a binder, a creation) or `NO KEY UPDATE` (an unlink writing its
  destination) — **and holds it because the statement that locked it returned it** (A-9). That is the whole claim. A lock statement
  followed by a second read of the same link judges two moments (READ COMMITTED gives every statement a snapshot of its own): a link
  committed between the two reads as linked and is held by nobody, an item bound to it takes only the key share, which the deleter's
  `NO KEY UPDATE` admits, and the `DELETE` waits for it and fails with `23001` (RESTRICT) when the binder commits — the 500 that failed
  PR #17's CI (review-security S-1, review-code CR-4). So every binder (a save, a create, a default naming, a creation that may default)
  and the unlink (for the unlinked team and for its destination) judge "linked" by the rows their own lock statement returned, and a
  link committed after it is "not linked" for that request: 409 for a binder, a no-op for the unlinked team, 409 for a destination, no
  default for a creation. The reference-integrity check of the `DELETE` therefore finds no referencing row: the items were reassigned
  and the setting cleared first.
- **A child's owner** is its parent's team (a creation that names no team), read under the parent's lock; the child holds no link of
  its own. An unlink or a deletion of that team locks the parent (`LockOwned`, `FOR UPDATE`) and so waits for the creation, then
  reassigns the child with the rest, so the child's key share on the link is never alone.
- **The deletion** needs no judgement by rows: every link creation of its team holds the team row `FOR SHARE`, and the deletion holds
  it `NO KEY UPDATE` before it reads the links, so the set it reads after locking them cannot grow meanwhile.
- **The reorder** (step 3 of its lock) still locks the link and reads it again. It writes no key onto the link (a place keys the item's
  owner and the team's order row), so a link it did not hold cannot make a `DELETE` fail: the unlink waits for the reorder's member
  items, then drops the places the reorder wrote. (CR-5's missing organization on that statement stays a carried minor.)

`23503` on the owner and default keys is the backstop of the *insert* side (D-10), covered by `OwningTeamBackstopTests`. The final
`DELETE`'s `23001` has no mapping (one would need an error code of its own, a contract change) and after A-9 no race above reaches it
(`LinkJudgementWindowTests` reach the window on purpose; `TeamQaEndpointTests` races it).

## 8. Use-case notes

- **`GetOrganizationTeams`.** Two statements, whatever the number of teams or projects: the organization's active teams ordered by
  `EF.Functions.Collate(name, OrganizationsCollation.Russian)` then id; then the links joined to the reachable projects of those teams
  (the organization active, the project `Active`), ordered by project name then id, grouped in memory by team.
- **`GetTeams` (the project's end).** One statement with the mark (the project's setting equals the team; the count of linked active
  teams, as `ProjectDefaultTeam.MarkOf`) and `isMember` as one `EXISTS` per row, as today; `projectId` leaves the row.
- **`GetTeamDeletionCounts`.** Three statements: the team (Active, else 404), its links joined to projects of **any** status with their
  names, and `CountOwnedByProjectAsync`; merged by project id so a project with no item reads 0. The same for 2 and 50 projects.
- **`AddTeam`.** A member id must be known to the directory and active there (as `AddTeamMember`) *and* hold a membership of this
  organization: `directory.LookupAsync(ids)` is one batch and `FilterMembersAsync` one statement. The memberships are
  `TeamMembership.Add(team.Id, organizationId, userId)`; a membership removed between the check and the flush fails the composite key,
  which `OrganizationsSaveChanges` already answers `member_not_in_organization`. Creating a team writes no membership of the caller (BR-OL-2).
- **`CreateProject`.** `teamMemberships` and the creator's join leave the handler (BR-M1-25 lapses); the "creator is no longer a member"
  rollback to the organization's not-found goes with it, since no insert depends on the creator's membership any more.
- **`DeleteTeam` and the destination of nothing.** The deletion passes `null` as the destination to `ReassignOwnedAsync`. Items of
  deactivated projects are reassigned too (the statements name project ids, not reachability); their `changed_at` moves like any other.
- **`UnlinkTeam` with no items.** `LockOwned`, `DropForTeamInProject` and `ReassignOwned` are still run (three cheap statements); a
  request that named a destination while the team owns nothing reassigns nothing, and an item given to the team after the dialog counted
  is reassigned to the destination named (`«Без команды»` when the dialog offered none).
- **The structure contract.** `OrganizationStructure.FindProjectAsync(org, project)` filters `WHERE id = project AND organization_id = org`;
  `FindTeamAsync(org, team)` the same; `FindProjectTeamAsync` joins the link by `(project_id, team_id)` with the organization in both
  predicates. `Reachability` keeps one place per module (ADR-0026).
- **Reorder.** `BacklogRepository.LockAsync` adds the link lock after the team's; `SaveOrderAsync` reads the departed places with
  `FROM backlog_places p JOIN work_items i ON i.id = p.work_item_id WHERE p.team_id = … AND p.backlog_level_id = … AND i.project_id = …`.
- **Raw SQL** is used only for locks and set statements (`backend.md`, "Raw SQL"), parameterized, and **every statement carries the
  organization** or an id resolved under it, so a foreign id locks and changes nothing.

## 9. Tests

Tests are written with the code (04-backend.md § 4b). A test that covers a story scenario carries a comment `// US-n · <scenario>`.
Names are retired or rewritten only with their rule (P-1).

### 9.1 Retire or rewrite, by name, with the rule (P-1)

**The spec's § Existing rules table is the list** — all 68 test-method names in it were checked against the code (none missing). This
section fixes what happens to each group; § 9.1b adds what the table does not name.

| Group (rule) | Fate |
|---|---|
| mod-002 BR-M1-1 → BR-OL-3 (the four per-project name tests) | **retire** as the spec lists; the rewritten org-level names (`Add_NameTakenAnyCase_Returns409TeamNameTaken_CreatesNothing`, `Add_NameOfDeletedTeam_IsFree`, `Add_TwoConcurrentSameName_OneTeam`, `Rename_ToTakenName_…`, `Rename_ToNameOfDeletedTeam_Returns200`, the two handler twins, `OrganizationsSchemaTests.Insert_NameOfADeletedTeam_IsAllowed`) **rewrite** over the organization |
| mod-002 BR-M1-9, mod-016 BR-M1-25 → BR-OL-2 | **retire** the six project-creates-a-team tests the spec lists; **rewrite** the five, renamed where the name states the retired rule (D-16): `CreateProjectEndpointTests.Create_Returns201_WithOneOrdinaryTeamAndTheRoot` → `Create_Returns201_WithTheRootAndNoTeam`, `CreateProjectHandlerTests.Handle_ValidNameAndKey_CreatesProjectWithOneOrdinaryTeamAndRoot_RecordsCreator` → `…_CreatesProjectWithTheRoot_RecordsCreator`, `Handle_Creates_OneProjectOneTeamOneArea_OneSave_NoLock` → `Handle_Creates_OneProjectOneArea_OneSave_NoLock`, `ProjectTests.Create_ComesWithOneOrdinaryTeamAndTheRoot_OwnedByNobody` → `Create_ComesWithTheRoot_OwnedByNobody_AndNoTeam`; `Create_ByANonMember_Is404_AMemberOrViewerIs403_NothingIsCreated_NoTeamIsJoined` keeps its name (the clause is still true) |
| mod-002 BR-M1-22, -23; mod-016 inventory rows 15-18, 20 → BR-OL-1, -12 | **retire** the methods the spec lists in `AddTeamEndpointTests`, `RenameTeamEndpointTests`, `GetTeamEndpointTests`, `DeleteTeamEndpointTests`, `GetTeamOwnedItemCountEndpointTests` and the handler twins — the "project not reachable" and "not a team of the project" rows, which name a project in a team write; the classes stay and are **rewritten** at the team's address; the member routes' tests are **rewritten** at the team's address too; `GetTeamsEndpointTests`/`GetTeamsHandlerTests` **stay** at the project's address over links. **One departure from the spec's list:** `GetTeamOwnedItemCountEndpointTests.Count_NotATeamOfTheProject_Returns404TeamNotFound` is **rewritten, not retired** — the unlink's count stays at the project's address and a team not linked there answers `team.not_found` (api-contract § 2.4); it keeps its name's intent and is renamed `Count_NotLinkedToTheProject_Returns404TeamNotFound` (D-16) |
| mod-002 BR-M1-18 as replaced by mod-017 BR-OT-12; mod-017 BR-OT-11's guard → BR-OL-8, -9, -10 | **retire** the guard tests the spec lists (`TeamTests.Delete_LastTeam_IsRefused`, the `DeleteTeamHandlerTests` and `DeleteOwnedItemsEndpointTests` rows); **rewrite** per project/linked: `Delete_ReassignsEverythingTheTeamOwns_ClearsSetting_EndsMemberships_FreesName_AddressAnswers404`, `Delete_NamedTeam_ClearsTheSetting_AnotherTeamKeepsIt`, `Delete_VersusSaveCreateNameReorder_BothOrders_NoItemOwnedByADeletedTeam_No5xx`, `DeleteTeamVolumeTests`, `DeleteTeamAtomicityTests`; the catalog row of the retired code becomes `deprecated` (`OrganizationErrorCatalogTests`) |
| mod-017 BR-OT-2, -4 ("a team of the project") → BR-OL-6 | **rewrite** to "not linked" the tests the spec lists (`ProjectTeamsTests.EnsureBindable_ActiveTeamOfAnotherActiveProject_IsNotInProject` → `…_ActiveTeamNotLinkedToTheProject_IsNotInProject`, D-16; `SetDefaultTeamHandlerTests`, `SetDefaultTeamEndpointTests`, `SaveOwningTeamEndpointTests`, `CreateOwningTeamEndpointTests`, `CreateFromTeamEndpointTests`, `OwningTeamSchemaTests` rows, `OrganizationsSchemaTests.Write_RowReferringToAnotherProject_IsRefusedByItsCompositeKey` — its default rows) |
| mod-002 api-contract § 7 → BR-OL-1 | **rewrite** `OrganizationStructureTests` (the five the spec lists) over the organization-scoped reads, plus the new containment cases (§ 9.4); `DeleteTeamEndpointTests.Delete_Seam_FindTeamSaysDeleted_ListActiveTeamsOmitsIt` becomes `…_FindTeamSaysDeleted_ListLinkedTeamsOmitsIt`; the double `FakeOrganizationStructure` follows |
| mod-016 BR-M1-30, -33 (catalog, inventory) | **rewrite** the rows of `OrganizationAddressedRoutes`, `RetrofitAccessTests`, `ScopedAccessTests` and the bundle matrix `OrganizationRoleBundlesTests` (17 codes → 21; the four new rows are admin-only; `team.write` stays admin-only) |
| mod-002 BR-M1-20, -24; mod-016 BR-M1-10, -27, -36; mod-017 BR-OT-5, -6, -13; feat-039 BR-PB-1..-8 | **kept**; the arrangement changes (§ 9.2) and the races extend (§ 9.4) |
| mod-017 BR-OT-14 (the earlier upgrade) | **kept as a merged migration, never edited**; `OwningTeamMigrationTests` and `RetireAreaOwnershipMigrationTests` roll back from head and migrate **to their own migration**, so they stay — but see § 9.1b for the two that cross this line's migration |
| tech-009 F2, F5; mod-009 "seeded when a team is created" | obsolete; no test |

### 9.1b Found by the scan, not named in the spec (same rules)

| Test | Rule | Fate |
|---|---|---|
| `AddOrganizationAccessMigrationTests.Migrator_AddOrganizationAccess_SeedsEachCreator_EmptyDatabaseIsFine_RevertsAndReapplies` | mod-016 BR-M1-25, BR-M1-37; BR-OL-13 | **rewrite the arrangement**: it seeds the legacy shape and rolls **forward to head**, then reads `team_memberships` joined to `teams.project_id`; this line's migration would drop exactly those rows. It migrates forward **to its own migration** (`MigrateToAsync(MigrationSuffix)`, as `OwningTeamMigrationTests` do) and to head only at the end. Name kept. |
| `AddProjectBacklogOrderMigrationTests.Migrator_AddProjectBacklogOrder_IsAdditive_RollsBackToItsTwoTables_AndAppliesAgain` | feat-039; BR-OL-13 | **rewrite the arrangement**: it seeds at head and asserts a team place survives a roll-back that crosses this line's `Down` (which empties teams and places). It rolls back to `AddProjectBacklogOrder`'s own migration first, seeds the legacy shape with SQL (the owner, the place) and then rolls back **one** step and forward again, never crossing this line's migration. Name kept; the spec names its `Model_HasNoPendingChanges_AfterTheMigrations` twin only. |
| `RepositoriesTests.LockProjectTeams_ReturnsTheActiveTeamsOfTheProjectOnly_InAscendingId` | mod-017 BR-OT-13 (4); BR-OL-10 | **retire** (the last-team serialization is gone); replaced by `LockTeamLinks_ReturnsTheTeamsLinksOnly_InAscendingProjectId` and `LockLinks_OfAPair_InAscendingTeamId` |
| `RepositoriesTests.GetMark_NamedOnlyTeamOrNone` | mod-017 BR-OT-4 | **retire** with `ITeamRepository.GetMarkAsync`; the mark is covered by `ReadModelsTests` over `ListLinkedAsync` |
| `ReadModelsTests.Teams_ListActive_ByName_WithTheDefaultMark_OneStatement`, `Teams_TheOnlyActiveTeam_IsMarkedOnlyTeam_UnlessTheSettingsNameAnother`, `Teams_DeletedTeam_IsLeftOut` | BR-OT-4; BR-OL-7 | **rewrite** over the linked list (`Teams_ListLinked_…`, D-16 old → new in the commit) |
| `ReachabilityTests.DeactivatedOrganization_HidesItsProjectsTeamsAndAreas_RestoreBringsThemBack`, `DeletedTeam_AndProjectOfAnotherOrganization_AreNotReachable` | ADR-0026 rule 3 | **rewrite**: a team is hidden by its own status and its organization, not by a project; a deactivated project hides its links from the team's list |
| `OrganizationsSaveChangesTests.SaveAsync_TeamNameRace_IsTeamNameTaken` | BR-OL-3 | **rewrite** over `ix_teams_organization_id_normalized_name`; an unmapped-index example stays another constraint |
| `OrganizationsSchemaTests.Update_AnyTeamDeleted_IsAllowed_TheProjectsOwnTeamIncluded` | mod-017 BR-OT-11 | **rename** `Update_AnyTeamDeleted_IsAllowed` (the clause states a retired rule) |
| `MembershipRepositoriesTests.TeamList_IsMember_FollowsTheCallersTeamMemberships_InOneStatement`, `TeamFind_NeverCarriesTheFlag` | mod-016 BR-M1-27 | **rewrite the arrangement** over links; assertions kept |
| `OrganizationMembershipsSchemaTests` team-key rows (`Insert_ATeamMemberWhoIsNotAnOrganizationMember_IsRefusedByTheCompositeKey`, `Delete_ATeamThatHasMembers_IsRestricted`, …) | mod-016 BR-M1-10 | **kept**, arrangement only (a team of the organization) |
| `OrganizationContainmentTests.AnAreaOrTeamIdOfAnotherOrganizationInABody_IsRefusedAsNotFound_NothingChanges` | BR-M1-22, BR-OT-16 | **rewrite**: the team rows move to the new routes (§ 9.4) |
| `DeactivateProjectEndpointTests` (the snapshot line), `RestoreProjectHandlerTests.Handle_DeactivatedProject_KeepsTeamStatusesOwnersAndDefaults` | ADR-0026 rules 2-3 | **rewrite** the snapshot: the project's **links** and the setting |
| `OwningTeamSchemaTests` (the keys), `OwningTeamBackstopTests`, `OwningTeamStatementCountTests` | mod-017 | **rewrite** onto the link keys and the renamed constants (D-10); the pinned counts are re-pinned on the first green run |

### 9.2 Arrangement-only files and fixtures (no assertion on a retired rule)

They change because a helper, a record or a column they use is renamed or gone; the assertions stay. The first compile after the
model commit lists every remaining caller; the known ones:

`Api.IntegrationTests`: `OrganizationsTestData` (`CreateProjectAsync` returns `NewProject` with no team; `AddTeamAsync(factory, organizationId,
name)` and a new `LinkAsync(factory, projectId, teamId)` / `AddLinkedTeamAsync`; `DeleteTeamAsync` loses the project), `OwningTeamTestData`
(`CreateWorldAsync`, `AddTeamAsync`), `BoardsTestData` (`FirstTeamId` → a linked team), `ProjectBoardsTestData`, `WorkItemsTestData`,
`MembershipTestData`, and the endpoint tests that create a team through a project: `AddTeamMemberEndpointTests`, `GetTeamMembersEndpointTests`,
`RemoveTeamMemberEndpointTests`, `SearchTeamMemberCandidatesEndpointTests`, `GetTeamsEndpointTests`, `GetTeamEndpointTests`,
`CreateOwningTeamEndpointTests`, `OwningTeamQaEndpointTests`, `OwnedViewsEndpointTests`, `ReorderVersusTeamChangeEndpointTests`,
`RemoveMemberEndpointTests`, `DeactivateOrganizationEndpointTests`, `RestoreOrganizationEndpointTests`, `RenameProjectEndpointTests`,
`RestoreProjectEndpointTests`. `Application.Tests`: `InMemoryOrganizations`, `FakeOrganizationsReadModels`, `FakeOrganizationsRepositories`
(+ new `FakeProjectTeamRepository`, `FakeWorkItemTeamOwnership` gains the destination, `FakeTeamBacklogPlaces` the per-project drop),
`FakeOrganizationStructure`, and the handler tests of the member slices, `GetTeam`, `GetTeams`, `RenameTeam`, `SetDefaultTeam`, `CreateProject`,
`RenameProject`, `RemoveMember`, `DeactivateOrganization`, `RestoreOrganization`, `DeactivateProject`. `Domain.Tests`: `OrganizationsDomainTestData`,
`ProjectTests`, `TeamTests`, `ProjectTeamsTests`. `Infrastructure.Tests`: `OrganizationsDatabaseFixture` (`CreateStructureAsync` seeds an
organization, a project, **two organization teams and one link**; `AddProjectAsync` returns a project with its root only), `OrganizationStructureTests`,
`ReachabilityTests`, `RepositoriesTests`, `ReadModelsTests`, `MemberReadModelTests`, `MembershipRepositoriesTests`, `OrganizationMembershipsSchemaTests`,
`ProjectBacklogStatementCountTests`, `OwningTeamStatementCountTests`. The legacy-shape helpers (`CreateLegacyStructureAsync`, `AddLegacyProjectAsync`) are for
tests that roll back past this line and stay as they are.

### 9.3 Story coverage (backend half; every scenario has a test)

FE = the screen is `plan-frontend.md`'s. Classes (all under `Api.IntegrationTests/Features/Organizations` unless marked):
A `AddTeamEndpointTests`, L `GetOrganizationTeamsEndpointTests`, G `GetTeamEndpointTests`, RN `RenameTeamEndpointTests`, TD
`SetTeamDescriptionEndpointTests`, PD `SetProjectDescriptionEndpointTests`, TM `TeamMembersAtTheTeamEndpointTests` (the four member
routes), DX `DeleteTeamEndpointTests`, DO `DeleteOwnedItemsEndpointTests`, DC `GetTeamDeletionCountsEndpointTests`, LP
`LinkTeamsToProjectEndpointTests`, LT `LinkProjectsToTeamEndpointTests`, UL `UnlinkTeamEndpointTests`, UR `UnlinkRacesEndpointTests`, CN
`GetTeamOwnedItemCountEndpointTests`, DT `SetDefaultTeamEndpointTests`, CP `CreateProjectEndpointTests`, TX `TeamAcrossProjectsEndpointTests`,
CT `OrganizationContainmentTests`, RT `RetiredTeamRoutesEndpointTests`, PM `TeamPermissionMatrixEndpointTests`; `Infrastructure.Tests`: MG
`OrganizationLevelTeamsMigrationTests`, ST `OrganizationStructureTests`.

**US-1**

| Scenario | Tests |
|---|---|
| the organization's teams are listed; roles | L `Get_ListsTheOrganizationsActiveTeams_ByNameAnyCase_NoDeletedNoOtherOrganization`, `Get_MemberViewerAndAdmin_AllRead`, `Get_NonMemberOrInstanceAdministrator_Returns404Organization`; FE the commands by code |
| created with a name only | A `Add_NameOnly_CreatesTeamAloneWithNoProjectLinkOrMembership` (the caller is no member), `Add_MemberAndViewer_Return403_CreateNothing` |
| created with its optional parts in one operation | A `Add_DescriptionMembersAndProjects_AreOneOperation_LinksRecordTheCallerAndTime`, `Add_OnlyLinkedTeam_IsTheProjectsDefaultByInference`, `Add_MemberOfTheTeam_StillOnlyReadsTheProject` |
| a choice that cannot be made (4 rows) | A `Add_Refusals_CreateNothing_NameStaysFree` (theory: Oleg and the other-organization user 409; Zeta and a deactivated project 404; 2 001 characters 400) |
| names are unique per organization (5 rows) | A `Add_NameTakenAnyCase_…`, `Add_NameOfDeletedTeam_IsFree`, `Add_NameInAnotherOrganization_IsFree`, `Add_BlankOrTooLongName_Returns400` |
| two creations of one name | A `Add_TwoConcurrentSameName_OneTeam` |
| rename and description | RN `Rename_…` (own name any case; taken name 409; deleted name free), TD `Describe_KeptAsTyped_ClearedByEmptyNullAndAbsent`, `Describe_2001Characters_Returns400_Unchanged`; the "shows wherever MARS is listed" clause is TX `Rename_ShowsInEveryProjectsList` |
| members belong to the team, one list serves every project | TM `Add_AddsOnceForEveryProject`, `Add_Duplicate_Returns409`, `Add_NotAnActiveMember_Returns409`, `Remove_LeavesTheOtherTeamsAndTheOrganization`, `RemoveFromOrganization_EmptiesThatOrganizationsTeamsOnly`, `Roles_MemberAndViewer_Return403` |
| a team page does not depend on a project | G `Get_AfterItsOnlyProjectIsDeactivated_StillReads`, L `ProjectsOf_DeactivatedProjectsAreOmitted`; FE |
| the project-nested routes are gone | RT `ProjectNestedTeamRoutes_AnswerAsPathsThatDoNotExist` (§ 2.6, theory over the eight), `ProjectsTeamsList_StillAnswers` |

**US-2**

| Scenario | Tests |
|---|---|
| a project with a name and a key only | CP `Create_Returns201_WithTheRootAndNoTeam_NoMembership`, `Create_ByMemberOrViewer_Returns403`; `Domain.Tests` `ProjectTests.Create_HasNoTeam_RootOwnedByNobody` |
| names and keys keep BR-M1-1 | CP `Create_NameOrKeyTaken_Returns409_CreatesNothing_NoLink` |
| description and teams in one operation | CP `Create_WithDescriptionAndTeams_LinksExactlyThose_NoDefaultNamed` |
| a choice that cannot be made | CP `Create_Refusals_CreateNothing` (Orion 404, deleted team 404, 2 001 characters 400) |
| a project without a team works | `CreateOwningTeamEndpointTests.Create_NoTeamProject_StartsWithoutATeam`, `GetProjectBacklogEndpointTests.Get_TeamlessProject_HoldsItsItems`, `ReorderProjectBacklogEndpointTests.Reorder_TeamlessProject_Works`, `GetTeamsEndpointTests.Get_ProjectWithNoTeam_ReturnsEmpty`; FE the shell |
| the shell on a project with no team | FE; the backend half is `GetTeams_ProjectWithNoTeam_ReturnsEmptyArray` |
| edit a project's description | PD `Describe_SavesClearsAndKeepsAsTyped`, `Describe_Over2000_Returns400`, `Describe_MemberViewer403`, `Describe_SurvivesDeactivateAndRestore` |
| rename keeps description and links | `RenameProjectEndpointTests.Rename_KeepsDescriptionAndLinks` |

**US-3**

| Scenario | Tests |
|---|---|
| a link from either page | LP `Link_FromTheProjectsPage_RecordsTheCallerAndTheTime_BothListsShowIt`, LT `Link_FromTheTeamsPage_…` |
| several ends in one confirmation | LT `Link_ThreeProjects_AllOrNothing_DeactivatedOneMeansNone404` |
| the multiselect offers only what may be linked | the options are the lists: L `Get_…`, `ListsExcludeDeletedAndOtherOrganization`; `GetTeamProjects` rows; FE the exclusion of already linked |
| one object, no copies | LT `Link_ToSeveralProjects_CreatesNoTeamProjectOrItem_SameItems` |
| linking a linked pair, unlinking an unlinked one | LP `Link_AlreadyLinked_ChangesNothing_KeepsWhoAndWhen`, UL `Unlink_NotLinked_Returns204_NamingAnyDestination`, LP/LT `Link_SamePairConcurrently_OneLinkNo5xx` |
| only active ends are linked | LP `Link_DeletedTeam_Returns404Team`, LT `Link_DeactivatedProject_Returns404Project`, `Link_ToMars_AndItsDeletion_NoLinkToTheDeletedTeamRemains` (C-10) |
| a deactivated project keeps its links | `DeactivateProjectEndpointTests`/`RestoreProjectEndpointTests` snapshot rows; G `Projects_OfATeam_LeavesTheDeactivatedProject_LinkSurvives` |
| only a manager of both ends links (4 rows) | PM `Link_ByRole_AdminDone_MemberViewer403_NonMember404` (both doors), `Link_SecondCodeMissing_IsRefusedBeforeAnyId` |
| readers read and are offered no command | PM `Reads_AllRoles`, `Viewer_LinkToAMissingProject_Is403Not404` |
| a link grants nothing | PM `Link_ChangesNoOneAccess_AndNoItem` |
| an owning team must be linked (4 rows) | `SaveOwningTeamEndpointTests.Save_TeamRefusals_StoreNothing` (not linked 409; unlinked after the card 409; deleted 404; foreign 404), `CreateOwningTeamEndpointTests.Create_TeamRefusals_NothingCreated_NoNumberConsumed` |
| only linked teams are offered | `GetTeamsEndpointTests.Get_OnlyLinkedActiveTeams_ByName`; FE the picker |
| the default must be linked | DT `Name_NotLinked_Returns409_ThenLinkedSucceeds`, `Name_Refusals_SettingUnchanged` |
| the mark follows the links | `GetTeamsEndpointTests.Get_DefaultMark_FollowsTheLinks_NamedOnlyTeamNone` |

**US-4**

| Scenario | Tests |
|---|---|
| the dialog states the items and one destination | CN `Count_IsThisProjectsItemsInAnyState_NotAnotherProjects`, `Count_NotLinkedToTheProject_Returns404TeamNotFound`; FE the field |
| confirming with another team | UL `Unlink_ToATeam_ReassignsOnce_UnplacesBelowTheDestinationsPlaced_KeepsEverythingElse`, `…_TouchesNoOtherProject`, `…_ChildAndParentOfOtherTeamsKeepTheirs`, `…_ClearsTheNamedDefault`, `…_ChangedAtIsTheOperationsTime`, `…_RemovesTheLink_ViewsAnswer404`, `LinkAgain_MovesNothingBack` |
| confirming with «Без команды» | UL `Unlink_ToNone_OnNoTeamBacklog_StaysOnTheProjectBacklog` |
| the only team | UL `Unlink_TheOnlyTeam_ToNone_LeavesTheProjectTeamless` |
| a team that owns nothing | UL `Unlink_OwningNothing_RemovesLink_ClearsDefault`, `…_ItemGivenAfterTheCount_GoesToTheNamedDestination` |
| a write while the team is being unlinked | UR C-1, C-2, C-3 (`Unlink_VersusSave/Create/ChildCreate_BothOrders_…`) |
| a reorder or a default naming | UR C-4, C-5 |
| a bad destination (6 rows) | UL `Unlink_DestinationRefusals_ChangeNothing` (self 409, unlinked 409, JUPITER 409, deleted 404, Orion 404, absent 400) |
| two unlinks that name each other | UR C-6 |
| linking again moves nothing back | UL `LinkAgain_MovesNothingBack` |
| only a manager of both ends | PM `Unlink_ByRole_…` |
| a deactivated project | UL `Unlink_FromADeactivatedProject_Returns404Project_ChangesNothing` |
| 5 000 items | `UnlinkTeamVolumeTests.Unlink_5000Items_Within5Seconds_AllOrNothing` (+ the failure-at-each-statement test `UnlinkTeamAtomicityTests`) |

**US-5**

| Scenario | Tests |
|---|---|
| the confirmation counts per project | DC `Counts_PerLinkedProject_DeactivatedAndZerosIncluded_AnyState`, `Counts_TeamLinkedToNone_IsEmpty`, `Counts_Member403`; ST none |
| confirming deletes in one operation | DX/DO `Delete_ReassignsEverythingTheTeamOwns_ClearsSetting_EndsMemberships_FreesName_AddressAnswers404` (the 18 items of the example), `Delete_NamedTeam_ClearsTheSetting_AnotherTeamKeepsIt` |
| the deletion reaches a deactivated project | DO `Delete_DeactivatedProjectsItemsAndSetting_AreReassigned_RestoreFindsThemClean` |
| a project's last team can be deleted | DO `Delete_TheProjectsLastTeam_IsAllowed_ProjectWorksOn`, DX `Delete_NoGuardCode_IsAnswered` |
| two deletions emptying a project | DO C-14 |
| a write while the team is deleted (7 rows) | DO C-12 (`Delete_VersusSaveCreateNameReorderLinkUnlink_BothOrders_…`) |
| a child beneath a story | DO C-13 |
| only an administrator deletes | PM `Delete_ByRole_…` |
| a big deletion is all or nothing | `DeleteTeamVolumeTests` (5 000 over three projects within 5 s), `DeleteTeamAtomicityTests` |
| the dialog's counts are a fixed number of statements | `TeamStatementCountTests` (§ 9.4) |

**US-6**

| Scenario | Tests |
|---|---|
| one team, the same name and people | TX `Rename_ShowsInEveryProjectsList`, `Members_AreMarkedInEveryProject` |
| views hold the team's items of that project | TX `TeamViews_AtEachProjectsAddress_HoldThatProjectsItemsOnly` |
| reordering in one project moves nothing in the other | TX `Reorder_InOneProject_LeavesTheOthersPlacesAndProjectOrdersAlone` and the repository test `Reorder_InOneProject_KeepsTheTeamsPlacesInTheOther` (D-13) |
| the default is each project's own | TX `Default_IsPerProject`, `Unlink_FromOneProject_LeavesTheOthersSettingAndItems` |
| an address under a project it is not linked to | TX `TeamViews_UnderAnUnlinkedProject_Return404Team`, `Reorder_…_MemberIs404_ViewerIs403` |
| creating from a team's surface | `CreateFromTeamEndpointTests.Create_FromATeamAtAnotherProjectsAddress_OwnedByTheTeam_LastOnItsBacklog` |
| the switcher falls back | FE (the server reads no switcher); TX `Unlink_TeamBecomes404AtTheProjectsAddress` |
| owning team chosen among linked | `GetTeamsEndpointTests`, `SaveOwningTeamEndpointTests` rows above |
| the «Команда» filter offers linked teams | `TeamFilterEndpointTests.Filter_UnlinkedTeam_SelectsNothing_IsNoError`; FE the options |

**US-7**

| Scenario | Tests |
|---|---|
| every team is dropped with what hangs on it | MG `Upgrade_DropsEveryTeam_Memberships_Places_Orders_AndEveryDefaultSetting` |
| every owned item becomes «Без команды», nothing else moves | MG `Upgrade_ItemsLoseTheirOwner_KeepEverythingElse_ChangedAtDoesNotMove`, `…_RestoredDeactivatedProjectsItemIsAlsoWithoutATeam` |
| nothing but teams is dropped, and the counts are logged | MG `Upgrade_DropsNothingElse_CountsOfTheOtherTablesAreAsBefore`, `Upgrade_LogsTheCounts_PerKind` (7, 2, places, orders, 1, 22 on the story's data) |
| what a user sees: no team anywhere | `GetOrganizationTeamsEndpointTests.Get_AfterTheUpgrade_IsEmpty`, `GetTeamsEndpointTests.Get_ProjectsAfterTheUpgrade_AreEmpty`, `GetProjectBacklogEndpointTests.Get_AfterTheUpgrade_HoldsAllItemsInTheProjectOrder`, `GetTeamEndpointTests.Get_ADroppedTeam_Returns404Team`, `CreateOwningTeamEndpointTests.Create_AfterTheUpgrade_StartsWithoutATeam`; FE the empty states |
| an administrator rebuilds the structure | A `Add_AfterTheUpgrade_NameIsFree_LinkedToTheChosenProjects_OwnsNothing`, `SaveOwningTeamEndpointTests.Save_FirstTeamSet_StandsUnplaced_NoFormerPlace` |
| little to drop | MG `Upgrade_OrganizationWithNoProject_LogsZeros`, `Upgrade_EmptyDatabase_ChangesNoRow` |

**US-8**

| Scenario | Tests |
|---|---|
| the structure contract reads inside the organization | ST `FindProject_OfAnotherOrganization_IsNull`, `FindTeam_OfAnotherOrganization_IsNull`, `ListLinkedTeams_And_ListLinkedProjects_OfAnotherOrganization_AreEmpty`, `EveryReadAskedInItsOwnOrganization_AnswersWithItsStatus`; the contract test fails if a by-id read returns a foreign object |
| a foreign id answers as one that names nothing (10 rows) | CT `AForeignIdInARequest_AnswersExactlyAsAnUnknownOne_NothingChanges` (theory over the ten rows of the story, bytes compared) |
| names and lists are the organization's own | A/L `Add_SameNameAsAnotherOrganizations_IsFree`, L `Get_NeverListsAnotherOrganizationsTeam`, RN `Rename_ToAnotherOrganizationsName_IsFree` |
| every step answers by the caller's role | PM `EveryStep_ByRole` (theory over the nine operations × Ann, Max, Vera, Olga) |
| a refusal changes nothing, the order of today's answers | PM `Order_401_Then404Org_Then403_Then404Address_Then400_Then409`, `Viewer_TeamWriteNamingAMissingTeam_Is403Not404`, CT `RefusedRequests_ChangeNothingOfEitherOrganization` (the state string gains links) |
| every route declares its code, the old routes are gone | `OrganizationAccessInventoryTests` (55 rows), `OrganizationAddressedRoutes.All`, `ForbiddenErrorCodeTests`, RT, `OrganizationRoleBundlesTests`, `GetOrganizationAccessEndpointTests.Get_ListsTheFourNewCodesForAdminOnly` |

### 9.4 Cross-cutting tests

- **Containment** (`OrganizationContainmentTests`): the path-id theory is driven by `OrganizationAddressedRoutes.All`, so the 16 new routes join it
  by themselves; add the body cases of US-8's table (Orion as a destination, as a project's link, as an item's owner, as a default; Zeta in a
  team's `projectIds`, in a project's link; a user of "Другая" in `memberIds`); the "state string" of a refused request carries the links, the
  owning teams and the settings of both organizations. A raw-SQL test inserts a link across organizations and expects `23503`.
- **Statement counts** (`Infrastructure.Tests`, the existing counter; each pinned, small = large, on the first green run): new `TeamStatementCountTests`:
  the organization's list (2 teams / 2 projects against 200 / 50), the project's linked list (2 and 50 teams), a team's projects, **the deletion's counts**
  (2 and 50 projects), a link of 1 and of 50 pairs, a create with 50 projects and 200 members, a project create with 50 teams, **the unlink** (50 and 5 000
  items), **the deletion** (50 and 5 000 items; 2 and 50 projects). `ProjectBacklogStatementCountTests` and `OwningTeamStatementCountTests` are re-pinned.
- **Concurrency** (`UnlinkRacesEndpointTests`, `DeleteOwnedItemsEndpointTests`, `LinkRacesEndpointTests`; the existing `CommitGate` and `PgLocks.WaitUntilBlockedAsync`):
  one test per row of § 7.3, both orders, asserting no 5xx and the four invariants; plus `Create_VersusTheDeletionOfTheNamedDefault_AndAnUnlink_NoDeadlock`
  (CR-7's three requests with links) and `LockOrder_UnlinkAndLink_AreAscendingByTeamId` (the order of the lock statements, read from the SQL log).
- **Route inventory**: `OrganizationAddressedRoutes.All` (8 out, 16 in, 2 recoded; 47 → 55), `OrganizationAccessInventoryTests`, `EndpointCategoryInventoryTests`,
  `ForbiddenErrorCodeTests`, `OrganizationsOpenApiTests`.
- **Contracts**: `OrganizationErrorCatalogTests` (the two new codes, `name_taken`'s new texts, the deprecated entry), `ErrorCatalogCompletenessTests`,
  `ErrorCodesReferenceTests` (`MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests`, then commit `docs/error-codes.md`).
- **Architecture**: `VerticalSliceRules` and `OnionDependencyRules` stay green; no new rule.
- **Migration** (`Infrastructure.Tests`, its own database): `OrganizationLevelTeamsMigrationTests` (MG) migrates to the migration before it, seeds the **story's
  legacy data with SQL** (US-7: the teams Platform, Reports, Old, "platform", Core, Ops and "Другая"'s Platform; their members, places, an order, a default
  setting; items owned by teams and by none; a deactivated project), runs it and asserts §§ 4.2–4.3 (the counts it logs are 7, 2, the places, the orders,
  1 and 22), that no other table's count moved and that no `changed_at` moved; a database with an organization and no project, and an empty one;
  **rolls back past it with a post-upgrade team, link, member and place present** (the `Down` empties them and restores the old shape) and applies it
  again; `Model_HasNoPendingChanges_AfterTheMigrations`. `OrganizationLevelTeamsSchemaTests`: the item key refuses a team not linked to the item's
  project (`23503`), the default key the same, a link across organizations is refused, a team membership across organizations is refused, the link
  primary key refuses a second row, the place key still refuses a place of another team, the name index is per organization, every constraint name the code
  maps exists, `Project_NameAndClearDefaultTeam_RoundTripsThroughEf`.
- **Volume**: `UnlinkTeamVolumeTests` (5 000 within 5 s), `DeleteTeamVolumeTests` (5 000 over three projects within 5 s), `GetOrganizationTeamsVolumeTests` (200 teams
  within 2 s), and the view volume tests re-seed their teams as linked organization teams.
- **Domain**: `EntityDescriptionTests`, `TeamTests` (create, describe, delete without a guard), `ProjectTeamTests`, `ProjectTeamsTests` (every row of
  api-contract § 0.3 and `EnsureLinkable`, `EnsureDestination`), `ProjectTests` (no team, `Describe`), `ProjectDefaultTeamTests` (kept),
  `OrganizationRoleBundlesTests` (the four rows).

## 10. Order of work

### 10.1 Commits (green at every commit; `scripts/verify.sh`)

| # | Commit subject (`feat(organizations): … (feat-028)`) | Content |
|---|---|---|
| 1 | `domain rules, codes and descriptions` | `EntityDescription`, the rewritten `ProjectTeams` rules and `ProjectTeam` entity (not yet mapped), the permission codes and bundles, the error codes (`description_length`, `destination_required`, `name_taken`'s text, the deprecated entry), `docs/error-codes.md`, their domain and contract tests. **Additive; green.** |
| 2 | `organization-level teams: model and migration` | the migration, the configurations, `Team`/`Project`/`NewProject` reshaped, `IOrganizationStructure` and its consumers' signatures, the repositories, read models and locks over links (§ 6), the **project-nested team slices and their tests removed** (§ 9.1), every arrangement helper (§ 9.2), the inventory rows, the migration and schema tests. The one large commit (D-2). |
| 3 | `team and link routes, in full` | `OrganizationEndpoints.cs`, `MembershipEndpoints.cs`, the request records, the handler shells with their final signatures, DI, `OrganizationAddressedRoutes` |
| 4 | `unlink a team from a project` (reference) | `UnlinkTeam` end to end: handler, the three contract calls, `UnlinkTeamEndpointTests`, `UnlinkRacesEndpointTests`, `UnlinkTeamVolumeTests`, `UnlinkTeamAtomicityTests` |
| 5..13 | one commit per lane (§ 10.2), made by the orchestrator after a wave's single verification run | |
| 14 | `statement counts, containment, volumes` | § 9.4 |
| 15 | `owners' notes` | the P-10 notes (§ 11), by the orchestrator |

Migrations apply on a clean database and again (04-backend.md DoD): `OrganizationLevelTeamsMigrationTests` rolls back and re-applies.

### 10.2 Lanes (04-backend.md § 4c)

**Phase 1 (sequential)** is commits 1–4: domain, the migration and model, the endpoints in full, DI, the codes, and the reference use case.
Phase 1 makes the minimal compile edits inside a lane's folder where a shared record changed; the lane then replaces its zone wholesale.

**Phase 2** — a lane writes exactly `src/backend/Mustask.Application/Features/<Module>/<UseCase>/`,
`src/backend/tests/Mustask.Application.Tests/Features/<Module>/<UseCase>/` and the named endpoint-test files. A lane runs no suite; the
orchestrator runs one at the end of a wave and commits.

| Lane | Use cases | Endpoint-test files in its zone | Wave |
|---|---|---|---|
| L2 | `DeleteTeam`, `GetTeamDeletionCounts` | `DeleteTeamEndpointTests`, `DeleteOwnedItemsEndpointTests`, `DeleteTeamVolumeTests`, `DeleteTeamAtomicityTests`, `GetTeamDeletionCountsEndpointTests` | 1 |
| L3 | `LinkTeamsToProject`, `LinkProjectsToTeam` | `LinkTeamsToProjectEndpointTests`, `LinkProjectsToTeamEndpointTests`, `LinkRacesEndpointTests` | 1 |
| L9 | `SaveWorkItem`, `CreateWorkItem` | `SaveOwningTeamEndpointTests`, `CreateOwningTeamEndpointTests`, `CreateFromTeamEndpointTests`, `CreateChildEndpointTests` | 1 |
| L1 | `AddTeam` | `AddTeamEndpointTests` | 2 |
| L8 | `GetBacklog`, `GetBoard`, `ReorderBacklog` | `GetBacklogEndpointTests`, `GetBoardEndpointTests`, `ReorderBacklogEndpointTests`, `OwnedViewsEndpointTests`, `ReorderVersusTeamChangeEndpointTests`, `TeamAcrossProjectsEndpointTests` (the views and reorder rows) | 2 |
| L6 | `SetDefaultTeam`, `GetTeamOwnedItemCount` | `SetDefaultTeamEndpointTests`, `GetTeamOwnedItemCountEndpointTests` | 2 |
| L5 | `RenameTeam`, `SetTeamDescription`, the four member slices | `RenameTeamEndpointTests`, `SetTeamDescriptionEndpointTests`, `TeamMembersAtTheTeamEndpointTests`, `AddTeamMember/GetTeamMembers/RemoveTeamMember/SearchTeamMemberCandidates` endpoint tests | 3 |
| L4 | `GetOrganizationTeams`, `GetTeam`, `GetTeams`, `GetTeamProjects` | `GetOrganizationTeamsEndpointTests`, `GetTeamEndpointTests`, `GetTeamsEndpointTests`, `GetOrganizationTeamsVolumeTests` | 3 |
| L7 | `CreateProject`, `SetProjectDescription`, `GetProject`, `RenameProject`, `RestoreProject` | `CreateProjectEndpointTests`, `SetProjectDescriptionEndpointTests`, `GetProjectEndpointTests`, `RenameProjectEndpointTests`, `RestoreProjectEndpointTests` | 3 |

Wave 1 first because it holds the concurrency-heavy slices; the new test helpers of phase 1 are their only shared input. A lane that needs
something shared escalates, it does not negotiate. Cross-cutting rows that no lane owns (`TeamPermissionMatrixEndpointTests`, `RetiredTeamRoutesEndpointTests`,
`OrganizationContainmentTests`, the inventory) are written in commit 14 after wave 3.

## 11. Risks, MR notes, and what is owed

- **Risks.** (1) The model commit (D-2) is large; the first compile after it lists every caller, and the spec's name lists are checked. (2) D-3 departs from
  ADR-0028 rule 8's text for the unlink; the concurrency rows C-1..C-9 are the proof and the morning's question. (3) The composite keys onto a link's primary
  key under EF 10: `OrganizationLevelTeamsSchemaTests.Project_NameAndClearDefaultTeam_RoundTripsThroughEf` settles it, and a SQL-only key (mod-017 D-11's
  technique) is the fallback. (4) A renamed-test wave (D-16): the commit messages carry old → new. (5) The migration's `UPDATE work_items` rewrites every
  owned row; on the stand's data this is seconds, and it runs once.
- **The MR names:** the data dropped (§ 4.3); that every project has no team after the upgrade and the organization has none until an administrator creates one;
  the retired and rewritten tests (§ 9.1, § 9.1b); the broken E2E specs below; the deprecated code; the clarification of ADR-0028 rule 8 (D-3); that bug-005 is
  superseded (D-4).
- **E2E specs this line breaks** (paused, so none is edited). The suite already reads the retired model of mod-017 (`isFirstTeam`, `defaultArea`:
  `e2e/support/organizationsApi.ts:37-38`); beyond it, this line makes **a project come with no team**, moves the team routes and pages, and retires `addTeam`'s
  meaning: `support/organizationsApi.ts` (`teams`, `addTeam`, `deleteTeam`, `createOrganizationWithProject` — the team list, the add and the delete all change; `:144-171`,
  `:183-207`), `support/membersApi.ts:58` (the team-member routes), `support/membersUi.ts:25,40,46` (the project's team settings page); specs
  `19-shell-in-a-browser.spec.ts` (`:92` «Команда: Пример проекта 1», `:872`, `:908`, `:930` — a team in the switcher after a project is created),
  `21-organizations.spec.ts` (`:160-166` «Команды» add dialog, `:213-218`, `:405`), `23-backlogs-boards.spec.ts` (`:4`, `:72` first team, `:285-292` deleted team),
  `24-work-item-hierarchy.spec.ts:59`, `25-work-item-fields.spec.ts:56`, `26-backlog-tree-editing.spec.ts:138`, `27-organization-members.spec.ts:401` (the project's only
  team), `27-work-item-card.spec.ts:135`, `28-team-members.spec.ts:101` (and the team page's address), `29-role-views.spec.ts:105,234` (the «Команды» table). Line numbers
  are at the branch tip.
- **Owed to the orchestrator (P-10 notes in each owner's `status.md`, not written here):** mod-002 api-contract §§ 0.3–0.4, 3, 6, 7; mod-016 § 2, § 3.3, § 4.1 rows
  15–18 and 20 and the route inventory; mod-017 BR-OT-2, -4, -11, -12, -15, api-contract §§ 0.1, 0.2, 0.4, 1.3, 2.7–2.9, 7 and its `plan-backend.md` §§ 7, 11; mod-006 §§ 3.1–3.3
  (the team address check is "linked"); feat-039 BR-PB-9; **feat-043**: the project's «Команды» (linked teams, the unlink, the default team) and the organization's team pages move onto
  the objects' own pages; **mod-007**: the sprint step of the unlink and the deletion (rule 8 step 4) joins `IWorkItemTeamOwnership`'s callers; **mod-018, mod-019, feat-041**:
  the clauses of spec § Owed by. `feat-028`'s own status: `stage: 05-frontend` after the backend pass, the two decisions of "for the human", and the E2E list above.
- **For the frontend plan.** The contract is fixed. `RefusalField` gains `description`, `destination`, `teams`, `projects` and `members`; `placeRefusal` maps `description_length`,
  `destination_required`, `project.not_found` and `team.not_found` of a create or a link to them (design § 13); the team's wire is `{ id, name, description }`, the list's rows carry
  `projects`, the project's list is `ProjectTeam`; the unlink is `POST` with the `destination` wrapper (`teamId: null` for «Без команды»); the dialogs read `unlink-count` and
  `deletion-counts` when they open; the codes the panel gates on are the four new ones, and `src/frontend/src/test/organizationRoles.ts` (the hand copy) gains them.
- **Security pass (later, ≤ 2 delta reads):** every new route declares an organization code and the second code is asked first of all; every id in a path, a body or a list
  is parsed as a GUID and bound as a parameter; every raw statement carries the organization (a foreign id locks and changes nothing); the by-id structure reads are
  organization-scoped (S-2); the count routes reveal a number (and, for a deletion, the names of the team's projects, deactivated ones included) to the holder of `team.delete` or both link
  codes, who is «Администратор» until `feat-041-project-visibility` filters it.
- **Open questions, decided in this plan** (the option this plan goes on with, **for the human**): D-1 one migration; D-3 the lock mode and the ADR clarification; D-4 the
  default lock, and bug-005 superseded; D-6 the unlink as a `POST`, the count renamed `unlink-count` (settling US-1 against US-8); D-7 the second codes, including `team.write` for
  members at creation; D-8 `destination` requires its key. The spec's retirement of `GetTeamOwnedItemCountEndpointTests.Count_NotATeamOfTheProject_Returns404TeamNotFound` is
  reversed to a rewrite (§ 9.1) — an inconsistency of the spec table with US-8, found here.

## 12. As built

Written at the end of stage 4b, against the code of the branch. **`api-contract.md` was unchanged** by every deviation A-1..A-7; it changed once afterwards,
at gate-3, by the human's choice (A-8: § 2.3a, one route added). Where the plan said "for the human", the option it went on with is what was built.

### 12.1 Deviations from the plan

| # | Plan | As built | Why |
|---|---|---|---|
| A-1 | § 4.2: `Down` restores the shape and leaves the teams empty. | `Down` also **inserts one active team per project, named after it**, as its last statement (the only rows it writes). | The previous migration's `Down` (`RetireAreaOwnership`) re-creates the first-team marker and needs every project to have an active team, as mod-002 guaranteed; with the new world's teamless projects it failed. A rollback past this migration is a stand reset anyway (§ 4.2); the migration test rolls back past it with a post-upgrade team, link, member and place present and applies it again. |
| A-2 | § 3: `Project.Create(organizationId, name, key, description?, createdAt, createdBy)`. | `Project.Create(organizationId, name, key, createdAt, createdBy, description = null)`; `Team.Create(organizationId, name, description = null)`. | The optional value goes last, so the callers that predate the description keep their argument order (one overload, no call site reordered). |
| A-3 | § 6: `IProjectTeamRepository.LockLinksAsync` returns the locked rows; `FindLinkedAsync(project, teamIds)`. | `LockLinksAsync` returns `Task` (the handler judges on the fresh `FindProjectTeamAsync` reads that follow, § 7.2), and every method takes the organization first, `FindLinkedAsync` included. | A lock statement that returns rows would invite judging on them; the organization in every statement is ADR-0028 rule 4. **Reversed by A-9:** judging on them is exactly what is required. |
| A-4 | § 9.1: `GetTeamOwnedItemCountEndpointTests.Count_NotATeamOfTheProject_Returns404TeamNotFound` is rewritten as `Count_NotLinkedToTheProject_Returns404TeamNotFound` in the same class. | The endpoint class is renamed **`GetUnlinkCountEndpointTests`** (the route is the unlink's `unlink-count` now), the method **`Count_NotALinkedActiveTeamOfTheProject_Returns404TeamNotFound`** (it also covers a team linked to no project and one of a deactivated project); the handler-test class `GetTeamOwnedItemCountHandlerTests` keeps its name (D-12 keeps the slice). | D-16: the name states the rule it tests. |
| A-5 | § 9.3: one `TeamMembersAtTheTeamEndpointTests` class for the four member routes. | The four existing classes (`AddTeamMemberEndpointTests`, `GetTeamMembersEndpointTests`, `RemoveTeamMemberEndpointTests`, `SearchTeamMemberCandidatesEndpointTests`) are rewritten at the team's address; "one list serves every project" is `TeamAcrossProjectsEndpointTests.Members_AreMarkedInEveryProject`. | A new class would have copied four existing ones; the rewrite keeps their history and their names (P-1). |
| A-6 | § 9.4: `LockOrder_UnlinkAndLink_AreAscendingByTeamId` reads the lock statements of the unlink and the link from the SQL log. | `TeamStatementCountTests.Unlink_LocksThePairOfLinksInOneOrderedStatement_AndNoTeamRow` (the unlink: one `FOR NO KEY UPDATE … ORDER BY` statement on `project_teams`, no team row, D-5); the creation's ordered lock is proved by `UnlinkRacesEndpointTests.Create_VersusTheDeletionOfTheNamedDefault_AndASecondDeletionOrAnUnlink_NoDeadlock` (four cases, both orders of the team ids). | The behaviour is what matters: the three requests of CR-7 finish. `SqlLogInterceptor` is the helper that pins a statement's shape. |
| A-7 | § 9.3: scenario test names. | Several names differ from § 9.3's (the intent and the `// US-n · scenario` comments are kept). | Names were chosen against the test files as written; every scenario of § 9.3 has a test, and the commit messages carry old → new for the renamed ones. |
| A-8 | § 5 and plan-frontend § 6: the creation form has no server search of the organization's active members; the panel filters `GET …/members` locally (`useMemberSearch`). | **`GET …/teams/member-candidates?text=`** (api-contract § 2.3a; 55 → 56 routes, OpenAPI 44 → 45): the team's own search over the organization, declared `organizations.team.create`, the handler asks `organizations.team.write` first of all (D-7). It is `SearchNewTeamMemberCandidatesHandler` in the `SearchTeamMemberCandidates` slice, both handlers running one `TeamMemberCandidateSearch` (a slice may not use another slice, so the second route lives beside the first and shares its search, its response record and its tests' world). The query parameter is `text`, as on the two sibling searches. | The human's gate-3 choice «⛔ Серверный поиск сейчас» (2026-10-18), over the recommended local filter. Routing: a literal `member-candidates` outranks `{teamId}`, and the binding is proved in `SearchNewTeamMemberCandidatesEndpointTests.Route_MemberCandidates_NeverBindsAsATeamId_…` (the team read by id, in either letter case, still answers; the sub-routes and a `DELETE` read it as an unknown id). The second code is `team.write` as D-7 has it — a creation with `memberIds` needs the same pair, so the people offered are the people it accepts; no better fit found. Statement count pinned at 2 (`TeamStatementCountTests.NewTeamMemberCandidates_…`, 2 members and 200). **Frontend:** `useMemberSearch` can give way to `useUserSearch` with a new scope; plan-frontend § 6's first bullet is the frontend stage's to rewrite. |
| A-9 | § 6, A-3, § 7.4: a lock statement locks, and the handler judges "linked" on a fresh read that follows (`FindProjectTeamAsync.IsLinked`); a creation that may default resolves the default in a second statement over all the project's links. | **"Linked" is the answer of the lock statement.** `LockTeamLinkAsync` and `LockLinkForShareAsync` answer `bool` from one statement that returns the row (`SqlQuery<Guid>` … `FOR SHARE`); `LockLinksAsync` answers the set of teams it locked; `LockProjectLinksAsync` locks every link of the project, gets the held teams back, and `DefaultTeams.ResolveAsync` runs over those teams only. The handlers (save, create, default naming, unlink) pass the answer to `EnsureBindable`/`EnsureDestination` as `IsLinked`, and `FindProjectTeamAsync` is still read for the team's status and organization. `api-contract.md` is unchanged: a link seen too late is the "not linked" the contract already answers (409 `team.not_in_project`, the 204 of an unlinked pair). | PR #17's CI failed on `TeamQaEndpointTests.TheFirstTeamOfATeamlessProject_…` (review-code CR-4 and review-security S-1, filed as minors, were this race). Under READ COMMITTED a link committed between a lock statement and the next read was judged linked and held by nobody; an item bound to it took a key share the unlink's `NO KEY UPDATE` admits, and the unlink's final `DELETE` failed with `23001` (RESTRICT) — a 500 (§ 7.4). The unlink was the path the test hit (its own judgement of the pair, not only the binders'): fixing the binders alone left four of four 150-round runs red. CR-4's own fix ("lock the resolved team's link, re-resolve if gone") was not built: it takes a link after others, out of the (project id, team id) order, and with an unlink that holds it a deadlock could form; resolving over the held links needs no second lock. |

### 12.2 Pinned numbers

Statement counts (`TeamStatementCountTests`, small = large, pinned on the first green run): the organization's list **2** (2 teams over 2 projects and 200 over 50);
the project's linked list **2**; a team's projects **2**; the deletion's counts **3** (2 and 50 projects); a link **4** (1 pair and 50, from the project's end and from the
team's); the creation of a team **5** (1 project and 1 member, 50 and 200); the creation of a project **6** (1 team and 50); the creation form's member search **2** (2 members and 200; A-8); the unlink **11** (50 items and 5 000);
the deletion **11** (2 projects and 50 items, 50 projects and 5 000). `OwningTeamStatementCountTests` and `ProjectBacklogStatementCountTests` are re-pinned.

**A-9 (the link judgement) moved no pinned number.** Each lock is still one statement, now returning its rows instead of nothing: the unlink's pair lock (the
unlink stays **11**), a save's and a create's binder lock, and the creation's lock of every link, which is still followed by one resolution of the default
(`ChildCreationStatements` stays **21**; a project with no links skips the resolution, one statement fewer, and no count pins that case). The one real
change is the default naming's link lock, which was two statements (the lock and a read of the link) and is one; it is pinned by
`RepositoriesTests.LinkLocks_AnswerWhatTheirOwnStatementLocked_InOneStatementEach` (three lock statements for three calls), because no count pins the naming.

The migration's notice reads `feat-028: N teams, M team memberships, P team places, Q team order rows and R default-team settings dropped; S work items made without a team`;
on the test data of US-7 (seven teams, two memberships, three places, two order rows, one setting, five owned items) it is
`feat-028: 7 teams, 2 team memberships, 3 team places, 2 team order rows and 1 default-team settings dropped; 5 work items made without a team`.

Volumes: `UnlinkTeamVolumeTests` (5 000 items within 5 s, the team's 200 items in another project untouched), `DeleteTeamVolumeTests` (5 000 over three projects within 5 s),
`GetOrganizationTeamsVolumeTests` (200 teams over 50 projects within 2 s).

### 12.3 For the human (the plan's recommendation was built)

- **D-3** — the unlink locks the links `FOR NO KEY UPDATE`, where ADR-0028 rule 8 reads `FOR UPDATE`; the ADR's text stays as accepted, and `mod-018-area-workspace`'s amendment can carry the clarification.
- **D-4** — a creation that may default locks every link of the project `FOR SHARE` in one statement ordered by team id; **bug-005 is superseded** (its cycle cannot form) and is kept as
  the regression test `Create_VersusTheDeletionOfTheNamedDefault_AndASecondDeletionOrAnUnlink_NoDeadlock`; the orchestrator may withdraw it from the queue.
- **D-7 / D-8** — the second codes are asked first of all (a creation with `memberIds` also needs `team.write`); `destination` requires its `teamId` key.
- **A-1** — the `Down` writes one team per project.
- **A-8** — the human chose the server-side search of the creation form's «Участники» at gate-3 (the plan had recommended the local filter); built, api-contract § 2.3a.

### 12.4 Owed to the other stages

- **Frontend (stage 6):** `src/frontend/src/test/organizationRoles.ts` gains the four codes; "For the frontend plan" in § 11 stands. **A-8:** the creation form's
  «Участники» reads `GET …/teams/member-candidates?text=` (api-contract § 2.3a) in place of the local filter of `GET …/members`.
- **E2E:** the standing answer is NO; the specs § 11 lists are broken and not edited.
- **P-10 notes** for mod-002, mod-016, mod-017, mod-006 and feat-039 (§ 11) are one Log line each in their `status.md`; the amendments of their contracts are the orchestrator's.
