# Audit 7-plan-impact — Plan impact: approved decisions, queue lines, in-flight tasks

Read-only audit, 2026-10-16. Sources: doc-001/004/006/010 specs and statuses, mod-002/003/006/016 specs, ADR-0024/0026/0027,
`docs/backlog.md` (worktree copy, identical to main), branches `doc/011-release-2-scope` and `feat/038-first-organization-onboarding`
(read with `git show`), the backend code, against input-brief §§ 1-3, 5-10, 12-14 and prior-art P-1..P-11. Facts and questions only.

## As-is (what exists, with repository-relative file paths and line numbers where it matters)

- **Delivered** (`stage: complete`, `docs/features/*/status.md`): doc-001..007, doc-010; mod-001/002/003/006/014/015/016; feat-001/022/026/027/029/032/035/036/037.
  Not delivered: mod-004/005/007/008..013, feat-002/003/005/006/008..013/015..017/020/021/023..025/028/030/033/034 (queue only).
- **Hierarchy in code is Organization → Project → Team|Area|WorkItem.** `Team.ProjectId` mandatory (`src/backend/Mustask.Domain/Organizations/Team.cs:30`),
  `Team.IsFirstTeam` (`:39`); `Area.ProjectId/ParentId/OwnerTeamId/DefaultOfTeamId` (`Area.cs:36-49`); `WorkItem.ProjectId` (`WorkItem.cs:53`) and
  `WorkItem.AreaId` (`:65`) — no team column, no sprint column. DB: `ak_teams_project_id_id` (`Migrations/20261005092819_AddOrganizationStructure.cs:83`),
  composite FKs `(project_id, owner_team_id)`/`(project_id, default_of_team_id)` to teams (`:127-137`), team name unique per project (`ix_teams_project_id_normalized_name`).
- **A team's backlog/board is defined by area ownership**: `src/backend/Mustask.Infrastructure/Boards/BacklogMembership.cs:17-26` ("the one definition of what a
  team's backlog and board hold": item in the project, type in the level, **area resolves to the team**, state not Removed) — used by backlog read, board read and reorder.
- **No iteration/sprint exists** in `src/backend` (only a naming test) nor in `src/frontend` (one card slot, `features/workitems/components/card/cardSlots.ts`). mod-007 is unbuilt.
- **Access**: 20 codes in `src/backend/Mustask.Domain/Identity/PermissionCatalog.cs:25-87`; three org roles admin/member/viewer as fixed bundles
  (`OrganizationRoleBundles.cs:23-40`); the only scope is the organization; team membership grants no code (ADR-0027 rule 2). No project visibility concept exists
  (grep of `docs/`: "restricted" only as a plain word).
- **Routes**: mod-016 spec § Retrofit inventory rows 1-37: rows 15-26 team/area routes under `{o}/projects/{p}`, rows 35-37 `…/projects/{p}/teams/{t}/backlogs|boards/{level}`,
  rows 28-34 work items under the project (area in body). Eight `api-contract.md` mention team/area routes or `areaId`: mod-002 (25 hits), mod-016 (15), mod-003 (6), mod-006 (5),
  feat-001, feat-022, feat-027, feat-032 (2 each). The shell address rule `…/projects/:projectId/teams/:teamId/…` is ADR-0024 § 2.
- **Queue** (`docs/backlog.md`): feat-028-org-level-teams `:23` (L, deps mod-002, queued e962cd1; no queue line lists it in deps); mod-007-iterations `:40`
  (Later, L); doc-012 itself still on `:35` while in flight. 13 lines depend on mod-007 directly or transitively: mod-008 `:42`, feat-008 `:47`, feat-009 `:48`, mod-013 `:54`;
  then feat-010, mod-011, feat-011, mod-012, feat-012 (via feat-009/mod-011), mod-009, feat-005, mod-010, feat-006 (via mod-008). mod-011 `:50` still cites withdrawn feat-004/feat-014.
- **In flight** (`scripts/backlog.sh`): doc/011 (L, gate-1 pending, 21 scope rows, OQ-1..OQ-11 open, no queue line entered); feat/038 (M, stage 01, spec + 3 stories, OQ-1..OQ-6 open,
  frontend only); feat/018-work-item-rich-text (L, 05-qa-backend ∥ 06-frontend, adds `Migrations/20261013104500_AddWorkItemDescriptionFormat` and a `work_items` snapshot change);
  feat/031-test-case-type (M, stage 01, no code); tech/031 (gate-3, stand). mod-016 names feat-028 as the planned follow-up in 9 spec lines (D-2; BR-M1-25 merge-order clause; BR-M1-27), plan-backend § 2.3, plan-frontend, api-contract and ADR-0027 "Spawned"; feat-029 spec `:206` / design `:761` ask feat-028's owner to re-check the card dialog's project.

## Responsibilities the current model carries (one duty per bullet; ⚠ = one entity, several duties)

- ⚠ **Area (per project tree)**: (1) classifies a work item — `Область` required, an area of the item's project (mod-003 BR-M3-23, doc-002 § Fields); (2) decides which team's backlog/board holds
  the item — unique owner by inheritance (doc-001 BR-3, doc-006 BR-M1-11, mod-006 BR-M4-5); (3) supplies the creation default — team default area / root (BR-M1-14, BR-M4-21, BR-M3-20);
  (4) is a filter scope "area with sub-areas" (doc-001 BR-15, BR-M4-22, feat-003); (5) resolves the team that must select an iteration before a pointer is set (doc-004 BR-M5-7); (6) blocks its own deletion
  (BR-M1-13, BR-M3-24) and carries the team-deletion re-own (BR-M1-18); (7) planned: burndown "over the team's area ownership" (BR-M5-16). It carries **no** access duty (doc-001 BR-5).
- ⚠ **Team (child of a project)**: (1) membership group, no role, no access (BR-M1-10, doc-006 § Roles); (2) owner of areas → scope of its board/backlog, one board per level (BR-M4-1);
  (3) selector of iterations and default (doc-004 BR-M5-5/6, planned); (4) namespace of dashboards and `@CurrentIteration` binding (doc-005 D-15/16, BR-M6-6, planned);
  (5) the **first team** is special: created with the project, owns the root area, never deleted, receives re-owned areas (BR-M1-9/11/18); (6) context level in shell and URL (ADR-0024, mod-002 BR-M1-24);
  (7) per-user default team = first team the user belongs to (mod-016 BR-M1-27).
- ⚠ **Project**: container of teams, areas, work items, tags (BR-8), future iterations/queries; unit of deactivation reachability (ADR-0026 rule 3); identifier key + sequence (BR-M3-5);
  its rename renames the root area (BR-M1-11); context level.
- **ORG_MEMBERSHIP.role**: the only source of codes; read per request (ADR-0027 rule 3). **Process**: one per organization (BR-M1-3). **Assignee**: any active member of the organization (doc-001 BR-13).

## Rules and contracts the matrix model touches

Decisions overturned or reopened (brief → what collides):

| id | collision |
|---|---|
| doc-001 D-2 | "Organization → Project → Team", team a child of a project; brief §§ 1, 5, 21.2-.3: Team independent, M:N to Project |
| doc-001 D-8, BR-3, doc-006 BR-M1-11 | Area tree = team-ownership mechanism, one owner per area; brief §§ 3.2, 21.4: Area is a universal workspace, ownership moves to `OwningTeamId` (§ 6) |
| doc-001 D-11, doc-004 BR-M5-1/2/5/7, E-5, E-7, E-10, E-11 | iterations belong to a project, teams select them, pointer valid if selected by the area's owner, sprints numbered per project; brief § 8: Sprint belongs to Team, spans projects, releases not mixed with sprints |
| doc-001 D-9, BR-5; doc-006 § Roles (cons. 1-8); ADR-0027 rules 2-4 | nothing finer than the organization, team membership grants no access; brief § 10: Area/Project/Team scopes, team roles (§ 5.6), Restricted visibility, inheritance |
| doc-001 D-1, BR-5, MVP table M1 | D-1 says "scope = membership in an organization/project", then BR-5 fixes the organization as the finest MVP scope; per-project and per-area overrides are release 2 (doc-011 OQ-9) |
| doc-006 BR-M1-7; ADR-0026 rules 3, 5; ADR-0027 rule 3 | resolution/reachability chain Area|Team → Project → Organization and the not-found chain org → project → team; brief § 3.1: Team, Area → Organization |
| doc-006 BR-M1-9, -14, -18; mod-002 BR-M1-21/22/24; mod-016 BR-M1-25/27/36 | first team auto-created, team names unique per project, later team gets a child area, deletion re-owns areas to the first team, creator joins the first team, default team = first of the user's teams in project order; brief § 5 + human decision "project default team set in settings or inferred from one linked team" |
| doc-003 BR-M4-1/5/6/11/13/17/20/21 | one board and backlog per (team, level) inside a project; an entry exists at exactly one (team, level) via the area; team deletion re-scopes items; create-from-team uses the default area; brief §§ 7, 6 rule 2 (item may have no team) |
| doc-002 BR-M3-2/23/24, § Fields | `Область` required, from the project's tree, deleting an area refused while it holds an item; brief § 6 rule 8 and § 13 leave the item↔area relation implicit |
| doc-005 BR-M6-1/3/4/6/10, D-15/16 | queries one project, results org-wide readable, `@CurrentIteration` binds a team of the query's own project, one default team dashboard seeded per team; brief §§ 7.3, 10.3 |
| doc-004 BR-M5-12/13/15/16/17 | "owned by the team" via area, pane over the team's selected iterations, capacity per team/iteration/member, burndown by area ownership; BR-M5-17 (delivery plan across projects) is already cross-project |
| doc-010 PR-1, PR-3, PR-8, PR-9, PR-11 | one founder per module (M1 mod-002, M5 mod-007); minimal deps; a line states its full scope; later lines backfill data that predates them (waived by the human); a reworded line keeps its id |
| doc-010 § Permission codes, § ADR debt, MS-1/MS-3/MS-4 demo texts | `organizations.team.write` covers create/rename/membership/default area/delete; `iterations.team-settings.write`; MS texts name "teams, default areas and areas", "boards and backlogs for every team", "Итерации и планирование" |
| doc-010 D-6, D-7, D-23 (a)(b)(c), D-25 | team deletion cascade to the first team, per-team default area, later team's own area, mod-002/mod-016 split — the structure the brief replaces |

Kept or compatible (fact): doc-001 BR-1 (item in one project), BR-8 (tags per project), BR-13 (assignee not tied to a team — matches brief § 6), BR-14, D-9 (role in the DB membership;
consistent with tech-024: client roles only in Keycloak), doc-006 BR-M1-1/-5/-8/-10 (organization boundary, one membership per org, non-member → not found, team ⊂ org members).

ADRs amended (by text): **ADR-0027** (rule 2 "team membership grants no code", rule 3 chain, rule 4; its "Revisit when: a user needs a different role per project or team inside one organization" is the brief's § 10;
"locked in": role names, code strings, order 401/404/403); **ADR-0026** (rule 1 Area/Team status table, rule 2 team deletion re-owns areas, rule 3 containers, rule 5, rule 7, rule 8 team-row `FOR SHARE` binders);
**ADR-0024** § 2 (team-level address `…/projects/:p/teams/:t`, switcher levels). Also `docs/conventions/backend.md` "Soft removal" (points to ADR-0026). ADR-0018 is already amended by ADR-0027; doc-011's
`instance-rights` row owes a further ADR-0018 amendment. Owed ADRs that touch the model: mod-005 (references ADR-0026 rule 5), mod-012 (history-derived metrics by team), feat-015, feat-021 (queued).

Queue lines (text overlap with the brief; "full" = same scope stated, "premise" = text names a model element the brief changes, "debt" = review debts against code the model replaces):

- **full**: feat-028 (teams to the organization, "what a team's backlog and kanban hold across projects, its areas" — brief §§ 5, 7.3; says "creating a project no longer creates a team", the human now says default team by settings/single link);
  mod-007 (iteration tree per project, "team selection and default", pointer, "a deleted team's settings dropped" — brief § 8).
- **premise**: feat-008 (current iteration on team surfaces), feat-009 (pane: iterations of the team), feat-010 (items owned by the team), mod-011/feat-011/mod-012 (sprint views, capacity, burndown per team iteration),
  mod-008 (Under on Область and Итерация, queries bound to a team), mod-009 (default team dashboard "for new and existing teams"), feat-003 and feat-013 (area filter, team's own items), feat-023/024/034 (team backlog/board features), mod-013 (already organization-owned, cross-project).
- **debt**: tech-009 (F2 teams + area tree read, F3 project rename and team reads, F4 switcher ids, F5 team-deletion text), tech-011 (m2 team deleted mid-read, m3 foreign project on board routes, m4 context source), tech-010 m4 (raw-SQL lock on the organizations module's areas row), tech-026 (unrun E2E 27-30, role-bundle drift).
- **no textual overlap**: tech-005/006/008/012/013/014/016..020/024/025, feat-002/005/006/012/015..017/020/021/025/030/033, mod-004/005/010; tech-024/025/feat-033 constrain only where scoped assignments live (the MUSTASK database, not Keycloak).

In-flight collisions:

- **doc/011** (gate-1 pending): `scoped-permission-overrides` (OQ-9) is the brief's § 10 capability scheduled for release 2; `team-area-sets` (OQ-10) rests on BR-3 unique ownership; `multiple-team-boards`, `board-columns`, `team-backlog-level-visibility`,
  `backlog-parent-grouping` assume "per team and level" boards in a project; BR-R2-7 "team membership grants no extra rights"; `work-item-move/copy` project migration vs owning team; `work-item-move` and `team-area-sets` list mod-007 as dep;
  its text treats mod-016 as undelivered (merged 2026-10-13) and cites feat-015/016/021, which are still queued lines.
- **feat/038**: BR-5 opens the new organization's project list (organization-level route), BR-1 reads `organizationsKeys.organizations('active')`; it creates no project, team or area — no collision found in its spec; OQ-1..OQ-6 do not touch the model.
- **feat/018**: its migration changes `work_items`; `docs/conventions/backend.md:179` — whoever merges second deletes and regenerates its migrations on the fresh snapshot, and a landed migration is never edited.
- **feat/031**: new work item type; stage 01, no code; touches the item model only if the matrix adds columns.

## Regression surface (existing behaviour that must keep working, with the test or spec that proves it today)

- Tenant boundary, 401 → 404 → 403 order, route inventory: `src/backend/tests/Mustask.Api.IntegrationTests/Features/Organizations/{OrganizationContainmentTests,ScopedAccessTests,OrganizationAddressedRoutes}.cs`,
  `Features/Authorization/OrganizationAccessInventoryTests.cs` (mod-016 BR-M1-33: a route with `{organizationId}` must declare a code); served access: `GetOrganizationAccessEndpointTests.cs`.
- Role bundles and realm contract: `Mustask.Domain.Tests/Identity/{OrganizationRoleBundlesTests,PermissionCatalogTests}.cs`, `Mustask.Contracts.Tests/{KeycloakRealmRolesTests,OrganizationPermissionRealmTests,KeycloakLiveRealmMigrationTests}.cs`
  (realm roles = instance codes only); frontend `src/test/organizationRoles.test.ts`, `shared/lib/access.test.tsx`.
- Area tree and team rules: `Mustask.Domain.Tests/Organizations/{AreaTests,AreaTreeTests,TeamTests,TeamDefaultAreaTests}.cs`; integration `{Create,Move,Delete}Area`, `SetAreaOwner`, `SetTeamDefaultArea`, `AddTeam`, `DeleteTeam`, `Get{Teams,Team,AreaTree}EndpointTests.cs`;
  `WorkItems/DeleteAreaWithWorkItemsEndpointTests.cs` (BR-M3-24); `Mustask.Infrastructure.Tests/Organizations/ReachabilityTests.cs` and `Deactivate*EndpointTests.cs` (ADR-0026).
- Team membership: `AddTeamMember/RemoveTeamMember/RemoveMember` endpoint tests, `OrganizationMembershipsSchemaTests.cs`, `AddOrganizationAccessMigrationTests.cs` (composite FK team → organization membership, BR-M1-10/36).
- Backlog/board contents, rank, create-from-team, hierarchy: `Features/Boards/{GetBacklog,GetBoard,ReorderBacklog,CreateFromTeam,CreateChild,BacklogTree}EndpointTests.cs`, `BacklogVolumeTests.cs` (1 s reorder budget, tech-011 t4), `Mustask.Domain.Tests/Boards`.
- Work items: `Features/WorkItems/{Create,Save,Get}…EndpointTests.cs`, `AssigneeMembershipEndpointTests.cs` (BR-M1-26), `HierarchyDepthEndpointTests.cs`; identifier unique per project inside the organization (BR-M3-5).
- Frontend: `app/shell/ContextSwitcher*.test.tsx`, `features/organizations/context/{resolveSelection,useOrganizationsContextSource*,storage}.test.*` (defaultTeam alone: 47 hits), `app/navigation/{switchTarget,visibility}.test.ts`,
  `app/routes.{boards,organizations,access}.test.tsx`, `features/boards/pages/{Backlog,Board}Page*.test.tsx`, `features/organizations/pages/{Teams,Team,Areas}Page*.test.tsx`.
- Volume: about 106 backend and 96 frontend test files mention team/area ids (word grep). E2E specs 21, 23-26, 27, 28-team-members, 29 mention teams but **E2E is paused** (mod-016 D-22; tech-026): specs 27-30 never ran, so they prove nothing today.
- Green means `bash scripts/verify.sh` (prior-art P-11); AGENTS.md: an `api-contract.md` changes only with a note in its feature's `status.md` (P-10).

## Risks and open questions for the analyst

1. Is `WorkItem.AreaId` / the `Область` field kept, and if kept, is it the project-internal path of today or the new workspace Area? Both carry the UI name «Область»; "Under" (BR-15, doc-010 D-16), feat-003/013 filters, mod-003 US-4/US-6 and the area-delete rule depend on it (brief § 13 says "area paths used for classification").
2. Today an item always has exactly one backlog entry (team derived from the area, BR-M4-5). With optional `OwningTeamId`, where do items with no team rank, and what is the project-level backlog's order (brief § 11.3)? Rank is scoped per (team, level) (doc-001 BR-12).
3. Three "default" notions meet: the team's default area (BR-M1-14), mod-016's per-user default team (BR-M1-27), the human's per-project default team. What does the project default team govern (new items, board context, switcher)?
4. Do the "first team" rules (BR-M1-9/11/18, mod-016 BR-M1-25/27) retire, and how is each retired by name (prior-art P-1)? What replaces team deletion's re-own (SC-09, P-7) given ADR-0026 rule 2 and ADR-0013's outbox ("revisit when" in ADR-0026)?
5. Sprint model: where do releases (doc-004 kinds, BR-M5-2) and per-project sprint numbers go; is "current iteration" (BR-9, UTC rule) still derived from a team's selection; how are the 13 mod-007 dependents and doc-011's `work-item-move`/`team-area-sets` re-pointed? Does one founder per module (PR-1) still hold for M5?
6. Scoped assignments: do the brief's roles reuse the admin/member/viewer bundles or add bundles (ADR-0027 "locked in": role names, code strings)? Brief permission names (`workitem.read`, `sprint.manage`) differ from the catalog (`workitems.workitem.read`, `iterations.*`); `workitem.delete` belongs to queued mod-005. ADR-0027's "Revisit when" is triggered — amend or supersede?
7. Restricted projects vs 404-for-non-members (ADR-0027 rule 4, ADR-0026 rule 5): is "project not found" for an org member a new answer? mod-008 BR-M6-3/4 (org-wide readable queries and results) and mod-009 dashboards are unbuilt and must be restated; doc-011 BR-R2-6 states the same non-disclosure rule for its surfaces.
8. Contracts: eight `api-contract.md` files and the 37-row inventory change; which delivery line owns each break, and where is the status.md note recorded (P-10)? Is `feat-028`'s number kept (doc-010 PR-11: a reworded line keeps its id) or withdrawn (`scripts/queue.sh withdraw`; numbers never reused)?
9. Waiver scope: "existing data may be dropped" vs mod-016 D-6 / ADR-0027 rule 8 (the human's stand database `mustask` migrated without reset) and `backend.md:179` (landed migrations are never edited). Does the waiver permit replacing migrations, or only dropping rows?
10. Order against doc-011: its gate-1 is open (OQ-9, OQ-10 overlap the brief). Does doc-012 answer, supersede or wait for them, and who amends doc-011's text (PR-7: a Log line in the amended task)?
11. Amending merged specs: doc-001, doc-003, doc-004, doc-005, doc-006 and the milestone texts MS-1/MS-3/MS-4 need an amendment pass under PR-7; which delivery line carries it?
12. Tenant boundary as DB fact: today's composite keys are `(project_id, id)` (`…AddOrganizationStructure.cs:83,127-137`); tech-005 (queued) is the only organization-level composite guard. Organization-level M:N links need an organization-keyed pattern (P-3).
