# Plan — backend (mod-016-organization-access)

Stage 4a, deep pass, step 0 (plan, contract, ADR draft — no code). It covers
the backend half of **US-1..US-8** in the two passes the spec's
§ Decomposition defines: **pass A** (the catalog, the memberships, the access
check and its first routes, the member operations, the assignee rule, the
default-team flag, the seeding migration) and **pass B** (the retrofit of
rows 1–37, the inventory test, the realm import and the restated contract
tests, `scripts/keycloak-seed.sh`, the live-realm command, `src/backend/AGENTS.md`).
The screens, the served-code navigation and the default-team choice belong to
stage 6. This plan fixes one interface, [api-contract.md](api-contract.md).

Inputs: [spec.md](spec.md), stories
[US-1](stories/US-1-manage-organization-membership.md)..[US-8](stories/US-8-instance-administrator-realm.md),
[review.md](review.md), [status.md](status.md) D-1..D-15,
[ADR-0027](../../adr/0027-organization-access-from-membership.md) (proposed —
this plan implements it), ADR-0004, 0005, 0006, 0010, 0011, 0018, 0022, 0025,
0026, `docs/conventions/backend.md`, `src/backend/AGENTS.md`. The code this
plan builds on was read, not assumed (main at `e962cd1`): `EndpointCategories`,
`PermissionPolicies`, `ForbiddenErrorCodeResultHandler`,
`OrganizationContextFilter` and its metadata, the four endpoint files
(`OrganizationEndpoints`, `WorkItemEndpoints`, `BoardEndpoints`,
`ProcessEndpoints`), `PermissionCatalog`, `OrganizationPermissionCatalog`,
`HttpContextCurrentUser`, the mod-002 repositories, read models and
`Reachability`, `CreateOrganizationHandler`, `CreateProjectHandler`,
`DeleteTeamHandler`, `SaveWorkItemHandler`, `GetAssigneeCandidatesHandler`,
`IUserDirectory` with `UserDirectoryReadModel`, the work-item and backlog read
models, the realm import, `KeycloakStandDriftCheck` and `KeycloakRealmImport`,
`scripts/keycloak-seed.sh`, the realm contract tests, the drift-check tests,
the inventory tests, `OrganizationsTestData`/`OrganizationsTokens` and
`UserDirectoryBoundaryRules`. Reference slices: `Features/Organizations`
(mod-002, the closest) and `Features/Dictionaries`.

- **ADR:** [ADR-0027](../../adr/0027-organization-access-from-membership.md),
  `proposed`, amends ADR-0018. The human accepts it with this plan (D-1,
  D-14). Code may start before that, but nothing merges before it (gate-3).
- **Lanes: yes** (§ 3.13, § 4.10). Pass A holds ten new use cases and six
  changed ones with disjoint zones; pass B's test retrofit splits by folder.
- **No code, comment, test name, log line, realm string or commit names the
  reference tracker** (spec, Localization).

## 1. Values and decisions this plan fixes

| What | Value | Where it is defined |
|---|---|---|
| Module folder and namespaces | `Organizations` (`Mustask.*.Organizations`, `Features/Organizations`); membership routes in a new file `Mustask.Api/Features/Organizations/MembershipEndpoints.cs` | the spec's codes are `organizations.*`; mod-002's module |
| Route prefix | `/api/organizations/{organizationId}/…`, as mod-015 BR-18 | api-contract § 1–§ 2 |
| Migration | **`AddOrganizationAccess`** — two tables and the D-3 seed, one migration | § 3.3 |
| Tables | `organization_memberships` (PK `(organization_id, user_id)`), `team_memberships` (PK `(team_id, user_id)`, composite FK to the first with `ON DELETE CASCADE`) | § 3.3 |
| Stored role | text `admin` \| `member` \| `viewer` (the API's spelling), check-constrained | § 3.2 |
| Catalog | one `PermissionCatalog`, every code with a `PermissionScope`; `OrganizationPermissionCatalog` is deleted | D-8; § 3.1 |
| User id | the Keycloak `sub`, `varchar(255)`, opaque, never a UUID by contract | api-contract § 0.3 |
| New ADR | `docs/adr/0027-organization-access-from-membership.md`, `proposed` | ADR README |
| Live-realm tool | a stand-only command of `Mustask.AppHost` (`--migrate-live-realm`), wrapped by `scripts/migrate-live-realm.sh` / `.ps1`; reads `KEYCLOAK_ADMIN_PASSWORD` from the environment | § 4.7 |

**Decisions this plan takes** (each is open to the orchestrator; the ones that
depart from a spec reading are repeated in § 6):

| # | Decision | Why |
|---|---|---|
| P-1 | The scoped check is an **authorization requirement with its own handler** (`orgperm:<code>` policies), not an endpoint filter; `OrganizationContextFilter` is retired in pass B | A filter runs after parameter binding; the spec's order puts the 404 and 403 before the body is read (§ 3.5). The requirement also makes the category and the enforcement one thing. |
| P-2 | `team_memberships` carries a denormalized `organization_id` and a composite FK `(organization_id, user_id) → organization_memberships ON DELETE CASCADE` | BR-M1-10 and BR-M1-36's first half become a database fact, and the add-vs-remove race (NFR Concurrency) is closed by the FK lock instead of by code (§ 3.8). A team never changes organization, so the copy cannot go stale; feat-028 keeps it valid. |
| P-3 | The D-3 seed is in the **migration**, not at startup (§ 3.3) | atomic with the tables, exactly once, no window with tables and no administrator, testable by `MigrateAsync(target)` |
| P-4 | Writes that can threaten BR-M1-4 take the **organization row `FOR NO KEY UPDATE`** first, then the target's membership row `FOR UPDATE` (§ 3.8) | one existing primitive (`IOrganizationRepository.LockAsync`); it does not conflict with project creation's `FOR KEY SHARE` |
| P-5 | Member and team-member lists are **plain arrays, not paged** | the stories need all 501 listed; no story asks for paging, sorting or filtering (§ 3.7) |
| P-6 | A person is a `UserRef` with `state` = `active` \| `inactive` \| `unknown` | one closed value instead of two booleans that can contradict |
| P-7 | **Pass A leaves the interim guard on the existing routes**; pass B flips them together with the test fixtures | flipping a route without its tests' memberships turns hundreds of tests red (§ 4.8); the pass-A routes use the new category from day one |
| P-8 | The realm's default roles are declared as a `roles.realm` entry `default-roles-mustask` with Keycloak's built-in composites, plus a `defaultRole` stub — **after a spike** that proves Keycloak 26.4 imports it (§ 4.3) | BR-M1-37 needs one declaration that the contract tests and the drift check both read |
| P-9 | Two field-reason codes beyond the spec's list: `organizations.membership.role_invalid`, `organizations.membership.user_invalid` | a validation answer names every failed field with a registry code (backend.md) |

## 2. Preconditions, parallel work, rebase notes

- **Base.** The branch is `mod/016-organization-access` at `3c35c29`, which
  descends from main `e962cd1`. mod-002, mod-014, feat-001, feat-022 and
  feat-026 are in main.
- **feat-027** (backlog tree editing) and **feat-028** (teams move to the
  organization) run beside this task (D-2, D-11, D-15). Whichever merges
  second rebases. The rebase of this branch onto a main that has either one:
  1. **Migration.** Delete `AddOrganizationAccess` and regenerate it on the
     fresh snapshot (backend.md, "Parallel branches with migrations"); the
     hand-written seed SQL is kept. If feat-028 is in main, the
     `team_memberships` seed statement and the first-team membership in
     `CreateProjectHandler` are **dropped** (BR-M1-25's merge-order clause);
     the tables stay.
  2. **Endpoints.** Every route the other task added gets its code by § 4.1's
     rule: `.AuthenticatedOnly(…).RequiresOrganizationContext()` does not
     compile any more (the extension is removed) — it becomes
     `.RequiresOrganizationPermission(PermissionCatalog.X)`. feat-027's
     create-with-`parentId` also gets the second-code check of § 4.1.
     `OrganizationPermissionCatalog.X` becomes `PermissionCatalog.X`.
     `OrganizationAddressedRoutes.All` gets the new routes.
  3. **Teams.** Under feat-028 the default-team rule (BR-M1-27) follows its
     team order, and `team_memberships.team_id` keeps pointing at the same
     team ids (D-2). The settings pages' conflict is stage 6's.
  4. Tests of the other task get the creator-as-admin fixtures (§ 4.8).
- **Pass order.** Pass B starts when pass A is merged into the branch. The
  ADR need not be accepted for pass A; its realm half (§ 4.3, § 4.7) is the
  part a changed ADR would move.
- **The frontend** may start when this contract and pass A's endpoints exist
  (spec § Decomposition: frontend deps backend A). A contract change after that
  goes to the orchestrator.

## 3. Pass A

### 3.1 The scoped catalog (D-8, BR-M1-30)

`Mustask.Domain/Identity/`:

| File | Content |
|---|---|
| `PermissionScope.cs` (new) | `enum PermissionScope { Instance, Organization }` |
| `PermissionCatalog.cs` (rewritten) | the 20 codes as `const string`s (3 instance: `DictionariesLanguagesRead/Write`, `OrganizationsOrganizationCreate`; 17 organization: the spec's matrix rows, `ProcessDefinitionRead`, `WorkItemsWorkItemRead/Write`, `WorkItemsLinkWrite`, `BoardsBacklogReorder` included); `record PermissionDefinition(string Code, PermissionScope Scope, string Description)`; `All` (ordered), `AllCodes`, `InstanceCodes`, `OrganizationCodes`, `ScopeOf(code)` (throws on an unknown code) |
| `OrganizationRole.cs` (new) | `enum OrganizationRole { Admin, Member, Viewer }`; static `OrganizationRoles` with `ToCode` / `TryParse` (`admin` \| `member` \| `viewer`, ordinal, case-sensitive) |
| `OrganizationRoleBundles.cs` (new) | `CodesOf(role)` → `IReadOnlySet<string>`, `Holds(role, code)`, `RolesHolding(code)`. Explicit sets, not derived from a suffix: admin = every organization code; member = the seven read codes + `workitems.workitem.write`, `workitems.link.write`, `boards.backlog.reorder`; viewer = the seven read codes |
| `OrganizationPermissionCatalog.cs` | **deleted**. Its constants move into `PermissionCatalog`; every reference is renamed (`WorkItemEndpoints`, `BoardEndpoints`, `ProcessEndpoints`, the tests). `PermissionCatalog.AllCodes` is no longer "the realm's codes" — the realm tests use `InstanceCodes` (§ 4.4). |

Tests (Domain.Tests, `Identity/`): `PermissionCatalogTests` (every code well
formed by `PermissionCode.IsWellFormed`, unique, has a description and a scope;
instance codes are exactly the three; no code is in both sets; `ScopeOf`
refuses an unknown code), `OrganizationRoleBundlesTests` (the spec's matrix as
**data**: for each of 17 organization codes × 3 roles the expected yes/no;
admin holds all; viewer holds only `*.read`; member = viewer + the three
writes; no bundle holds an instance code), `OrganizationPermissionCatalogTests`
is deleted with its class. A new code with no bundle fails the matrix test.

### 3.2 Domain — `Mustask.Domain/Organizations/`

| File | Content |
|---|---|
| `OrganizationMembership.cs` | `OrganizationId`, `UserId`, `Role`, `AddedBy`, `AddedAt`, `RoleChangedBy?`, `RoleChangedAt?`. `Add(organizationId, userId, role, addedBy, now)`; `Founder(organizationId, creatorId, createdAt)` (added by itself); `ChangeRole(role, by, now)` returns whether anything changed — the same role changes nothing, **not even the "changed" fields** (api-contract § 1.5). `UserId` and `AddedBy` are 1–255 characters, not blank. |
| `TeamMembership.cs` | `TeamId`, `OrganizationId`, `UserId`; no role, no who/when (spec Entities) |
| `OrganizationAdminRule.cs` | `EnsureAnAdministratorRemains(int knownActiveAdministratorsLeft)` → `ConflictException(MembershipLastAdmin)` when 0. The one definition of BR-M1-4; handlers count, the domain decides. |
| `OrganizationErrorCodes.cs` | the ten new constants of api-contract § 5, each under `[ErrorCodes]` |
| `OrganizationLimits.cs` | `UserIdMaxLength = 255` (its own constant; it is not `CreatedByMaxLength`, which is a different concept that happens to be equal) |

Domain tests: `OrganizationMembershipTests` (founder is added by self, no
change record; `ChangeRole` same role is a no-op; blank/over-long ids refused),
`TeamMembershipTests`, `OrganizationAdminRuleTests` (0 refuses, 1 passes).

### 3.3 Data model and the migration `AddOrganizationAccess`

Two tables, one migration, `dotnet ef migrations add AddOrganizationAccess
--project src/backend/Mustask.Infrastructure` (runbook
`docs/runbooks/local-migrations.md`), snake_case by convention, `xmin` by the
convention, soft removal untouched.

**`organization_memberships`**

| Column | Type | Notes |
|---|---|---|
| `organization_id` | `uuid` | PK part; FK → `organizations(id)` `RESTRICT` (organizations are never purged) |
| `user_id` | `varchar(255)` | PK part; the Keycloak `sub`. **No FK to `directory_users`**: the membership outlives the account (BR-M1-5, BR-M1-17), and a creator the projection does not know yet must be storable (BR-M1-25). |
| `role` | `varchar(16)` | `ck_organization_memberships_role`: `role IN ('admin','member','viewer')`; value converter ↔ `OrganizationRole` |
| `added_by` | `varchar(255)` | the adder's `sub` |
| `added_at` | `timestamptz` | |
| `role_changed_by` | `varchar(255)` null | |
| `role_changed_at` | `timestamptz` null | `ck_organization_memberships_role_change`: both null or both set |

Indexes: the PK `pk_organization_memberships`; `ix_organization_memberships_user_id`
(`user_id`) for "my organizations" and the access read.

**`team_memberships`**

| Column | Type | Notes |
|---|---|---|
| `team_id` | `uuid` | PK part; FK → `teams(id)` `RESTRICT` (teams are soft-deleted; ending the memberships is the handler's, § 3.9) |
| `organization_id` | `uuid` | the team's organization, copied once; part of the composite FK |
| `user_id` | `varchar(255)` | PK part |

PK `pk_team_memberships (team_id, user_id)`: a user is in a team at most once.
Composite FK `fk_team_memberships_organization_memberships (organization_id,
user_id) → organization_memberships (organization_id, user_id) ON DELETE
CASCADE`, with its index `ix_team_memberships_organization_id_user_id` (it also
serves "the teams I am in"). The constraint names are constants in
`OrganizationsConstraints`. The copy is set only from the address chain
(`team ∈ project ∈ organization`) in the add handler, and a repository test
pins that it equals the team's real organization.

**The D-3 seed, in `Up`, after the two tables** (raw SQL, no C# types — the
migration must not depend on the domain's evolution):

```sql
INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at)
SELECT id, created_by, 'admin', created_by, created_at FROM organizations;

INSERT INTO team_memberships (team_id, organization_id, user_id)
SELECT t.id, p.organization_id, p.created_by
FROM projects p
JOIN teams t ON t.project_id = p.id AND t.is_first_team
JOIN organization_memberships m ON m.organization_id = p.organization_id AND m.user_id = p.created_by;
```

This is BR-M1-25's seed exactly: one `admin` per organization (active and
deactivated), the recorded creator, whatever the projection says, "added by"
the creator at the organization's creation time, no role change; a project's
creator joins **its first team** only when they are then a member of its
organization; no other team gains a member. `Down` drops `team_memberships`,
then `organization_memberships` (the membership data is gone; organizations,
projects, teams and areas are untouched).

**Why the migration and not a startup step.** (1) It is atomic with the tables:
there is no instant at which the tables exist and no one is an administrator,
which at startup would answer «Организация не найдена» to everybody.
(2) Exactly once, with no "already seeded?" bookkeeping, and reversible by the
same mechanism as the schema (ADR-0002: migrations are an explicit step).
(3) It needs only columns already stored (`created_by`, `created_at`) — no
projection (D-10), no Keycloak. (4) A startup seed would run on every start in
every environment and would have to tell an organization created by the new
code from one created by the old. (5) It is testable with `MigrateAsync(target)`
on a database holding data.

Tests (Infrastructure.Tests, Testcontainers, `Organizations/`):
`AddOrganizationAccessMigrationTests` — **US-6**: migrate to the previous
migration, insert organizations (one deactivated) with creators, projects (one
whose creator is not the organization's creator) with their first teams, plus
an extra team; migrate; assert the memberships and the team membership; assert
"no other team gains a member"; run on an empty database (no rows, no error);
revert to the previous migration (tables gone, the rest intact); migrate again
(idempotent). A model test asserts the unique/PK/FK/cascade shape.

### 3.4 Application surface — `Mustask.Application/Abstractions/`

`Organizations/`:

| Abstraction | Members | Notes |
|---|---|---|
| `IOrganizationAccessReadModel` | `FindAsync(organizationId, userId)` → `OrganizationAccessRow?(Status, OrganizationRole?)` | **one** query: the organization left-joined to the caller's membership, `AsNoTracking`; `null` = no such organization. The access check's only read. |
| `IOrganizationAccess` | `Role`, `Holds(code)` | scoped; set by the access check for the request; throws if the route had no scoped check. Used by handlers that need a second code (§ 4.1) and by `GetOrganizationAccess`. Implemented in Api (`HttpContextOrganizationAccess`). |
| `IMembershipRepository` | `LockMemberAsync(orgId, userId)` (`SELECT … FOR UPDATE`, then tracked load); `FindAsync`; `ListOtherAdminIdsAsync(orgId, exceptUserId)`; `Add`; `DeleteAsync(orgId, userId)` (raw `DELETE`, cascades to team memberships); `SaveAsync` (maps `pk_organization_memberships` → `already_member`) | the organization row is locked through the existing `IOrganizationRepository.LockAsync` |
| `ITeamMembershipRepository` | `Add`; `DeleteAsync(teamId, userId)` → rows deleted; `EndAllAsync(teamId)`; `SaveAsync` (maps `pk_team_memberships` → `member_exists`, the composite FK → `member_not_in_organization`) | the team row is locked through the existing `ITeamRepository.LockForShareAsync` / `LockForUpdateAsync` |
| `IMemberReadModel` | `ListAsync(orgId)`; `FindAsync(orgId, userId)`; `ListTeamMembersAsync(teamId)`; `ListMemberIdsAsync(orgId)`; `FindAmongAsync(orgId, ids)` → the ids that are members; `IsMemberAsync` | returns `MemberView` / `UserRefView` (shared records in this file, like `WorkItemView`: slices share Abstractions, never each other). Builds the views with the projection lookup of § 3.7. |
| `IOrganizationReadModel` (changed, **pass B**) | `ListAsync(status, userId, onlyRolesHolding?)` | rows 1–2 |
| `ITeamReadModel` (changed) | `ListActiveAsync(orgId, projectId, userId)`; `TeamRow` gains `IsMember` | BR-M1-27; one extra `EXISTS` per row, one query |
| `IWorkItemReadModel`, `IBacklogReadModel` (changed) | `WorkItemAssigneeView` gains `bool IsMember` | § 3.10 |

`UserDirectory/UserDirectoryBatches.cs` (new, static): `LookupAllAsync(directory,
ids)` and `SearchAmongAsync(directory, text, ids)` split above
`UserDirectoryLimits.IdsPerRead` (500) into batches and merge —
`UserSearchResult.Merge` and `DirectoryUserOrder` already exist for this
(mod-014 BR-UP-17). One definition, in Abstractions, so no slice calls another.

Infrastructure implements each, registers them in `DependencyInjection.cs`, and
adds the EF configurations and the two `DbSet`s. None of them may mention
`DirectoryUsers`/`ProjectedUser` (`UserDirectoryBoundaryRules`): the read
models use `IUserDirectory` only. The access check's files must not mention
`DirectoryUser` either (§ 3.5).

### 3.5 The access check — the successor of the organization-context filter

**Where it runs, and why.** ASP.NET Core runs authorization before endpoint
filters, and parameter binding before filters. A filter-based check could not
give a non-member with a malformed body the 404 the spec orders first, and an
`AuthenticatedOnly` route with a forgotten filter would be open. So the check
is an `IAuthorizationRequirement` with a handler, evaluated by the
authorization middleware — the place where every access decision already
lives — and its failure reasons are rendered by the result handler that
already renders a policy failure as a business code (mod-002 D-p).

`Mustask.Api/Authorization/` (new unless marked):

| File | Content |
|---|---|
| `OrganizationPermissionRequirement.cs` | `record(string Code, bool AcceptsDeactivated)` |
| `OrganizationPermissionHandler.cs` | `AuthorizationHandler<OrganizationPermissionRequirement>`, **scoped**. Steps below. |
| `OrganizationAccessFailure.cs` | two `AuthorizationFailureReason`s: `OrganizationNotFound`, `RoleLacksCode` |
| `HttpContextOrganizationAccess.cs` | `IOrganizationAccess` over `HttpContext.Items` (the handler stores `(OrganizationId, Role)` there) |
| `PermissionPolicies.cs` (changed) | two more prefixes: `org:<code>` and `org+restore:<code>` → `RequireAuthenticatedUser()` + the requirement; `OrganizationPolicyName(code, acceptsDeactivated)` |
| `EndpointCategories.cs` (changed) | `RequiresOrganizationPermission(code, acceptsDeactivated = false)`; `RequiresPermission` now refuses (at mapping time, `InvalidOperationException`) a code whose scope is not `Instance`, and the new method refuses one whose scope is not `Organization` — a code in the wrong place would be a permanent silent 403 |
| `EndpointCategoryMetadata.cs` (changed) | `EndpointCategoryKind.OrganizationPermission`; the metadata carries the code and `AcceptsDeactivated`. **Exactly one category per endpoint** is kept: the new method attaches one `EndpointCategoryMetadata`, and `RequireAuthorization(OrganizationPolicyName(…))` is its enforcement. |
| `ForbiddenErrorCodeResultHandler.cs` (changed) | before its D-p branch: a `Forbid` whose failure carries `OrganizationNotFound` throws `NotFoundException(OrganizationNotFound)`; one carrying `RoleLacksCode` throws `ForbiddenException(AccessForbidden)`; both are logged once at Information (`reason`, `organizationId`, `code` — never a name) |
| `JwtBearerSetup.cs` (changed) | `services.AddScoped<IAuthorizationHandler, OrganizationPermissionHandler>()` |

The handler, per request:

1. an unauthenticated user: no verdict (the middleware challenges → 401 first);
2. `organizationId` from `RouteValues` (`MapGroup` keeps the name), parsed with
   `AddressedId.TryParse`; malformed → fail `OrganizationNotFound`;
3. `IOrganizationAccessReadModel.FindAsync(id, ICurrentUser.SubjectId)` — **one
   read, no cache beyond the request, no call to Keycloak, no read of the
   projection** (`UserDirectoryBoundaryRules` forbids both in this folder);
4. no such organization, an organization that is not active (unless
   `AcceptsDeactivated`), or no membership → fail `OrganizationNotFound` — the
   *same* reason for every cause, so the answer is the same bytes;
5. `OrganizationRoleBundles.Holds(role, code)` false → fail `RoleLacksCode`;
6. store `(organizationId, role)` for `IOrganizationAccess`; succeed.

**The order of the answers** is therefore: 401 → 404 organization → 403 → (the
handler) nested not-found → 400 → 409 (api-contract § 0.1). A test fixes it
with a malformed body (§ 3.12).

**Resolution chains, route shape by route shape.** Every organization-scoped
route carries its organization (mod-015 BR-18), so the chain always starts at
the requirement; there is no route that resolves the organization from a nested
id. The nested links are the handlers', each inside the organization of the
address — the audit of the code in main:

| Route shape (spec chain) | Link after the requirement | Where it is enforced today |
|---|---|---|
| `Org/{o}` and org-level children (name, deactivate, restore, process, access, members, member candidates, assignee candidates, projects list) | none | the requirement |
| `…/projects/{p}[/…]` | p ∈ o | `IProjectRepository.IsReachableAsync(o, p)`, `Reachability`, and `project.OrganizationId != organizationId` in every work-item and board handler |
| `…/teams/{t}[/…]` incl. backlogs, boards, team members | t ∈ p ∈ o | `ReachableTeams(o, p)`, the team locks keyed `(project, team)` |
| `…/areas/{a}` | a ∈ p ∈ o | `AreasOfReachableProject(o, p)` |
| `…/workitems/{identifier}[/…]` | item ∈ p ∈ o | the project check, then the identifier's key against `project.Key`, then the lock `(project, number)` |
| ids in a body (parent, area, team, assignee, new member) | inside the same project or organization | `LockParentWriteAsync`, `LockAreaAsync`, `LockTeamDefaultAreaAsync`, the area-owner and default-area checks; **new:** the assignee (§ 3.10) and the new member / team member (§ 3.6) |

Pass B adds one consolidated test over this table (§ 4.2, "containment"): for
every row of `OrganizationAddressedRoutes.All` with a nested id, an
administrator of A and of B addresses A with B's id and gets B's id's level
`…not_found`. A handler that does not hold its link fails it.

`OrganizationContextFilter`, `OrganizationContextEndpoints`,
`OrganizationContextMetadata` and `RequiresOrganizationContext` stay in pass A
(the existing routes still use them) and are **deleted in pass B** when the
last route moves. `docs/conventions/backend.md` § "Soft removal" ("at the
boundary the organization-context endpoint filter") is updated in pass B to
point at ADR-0027's requirement handler.

### 3.6 The use cases of pass A — `Mustask.Application/Features/Organizations/<UseCase>/`

Ten new, six changed. Each folder has the command or query, the validator
inside it, the handler, the response (backend.md). Handlers register scoped in
`DependencyInjection.cs`. "Answers" lists the order of api-contract § 0.1 for
the handler's part.

| # | Use case | Route (api-contract) | Code | Handler (what it does, in order) |
|---|---|---|---|---|
| 1 | `GetOrganizationAccess` | `GET …/access` § 1.1 | `organizations.organization.read` | `IOrganizationAccess.Role` and its bundle, sorted ordinally. No query of its own. The endpoint adds `Cache-Control: no-store`. |
| 2 | `GetMembers` | `GET …/members` § 1.2 | `organizations.membership.read` | `IMemberReadModel.ListAsync`: members, then **one** batched lookup of every member id **and** every `addedBy` / `roleChangedBy` id (they may no longer be members); order of § 3.7. |
| 3 | `SearchMemberCandidates` | `GET …/member-candidates` § 1.3 | `organizations.membership.write` | `IUserDirectory.SearchAsync(text, IncludeInactive: false)` over the whole projection; mod-014's own refusals pass through. |
| 4 | **`AddMember`** (reference) | `POST …/members` § 1.4 | `organizations.membership.write` | values (`userId`, `role`: both reasons at once) → `LookupAsync` known and active, else `user_unavailable` → pre-check `already_member` → `Add` → `SaveAsync` (a lost race maps to the same `already_member`) → `MemberView`. No lock: adding cannot threaten BR-M1-4, and the PK closes BR-M1-5. |
| 5 | `ChangeMemberRole` | `PUT …/members/{userId}/role` § 1.5 | `organizations.membership.write` | path: membership exists (else `membership.not_found`) → role value → in one transaction: lock the organization row (`FOR NO KEY UPDATE`; not active → org not found), **re-read the actor's membership under the lock** (gone → org not found; the actor's role is judged after the rule below), lock the target `FOR UPDATE` (gone → `membership.not_found`); target is an admin being demoted → count of *other* admins that are **known and active** (`ListOtherAdminIdsAsync` + `LookupAllAsync`) → `OrganizationAdminRule` (`last_admin` answers before the role check, so two administrators who demote each other keep § 1.6's answer); **then the re-read actor's role must still hold the code, else `access.forbidden`** (review round 1a BE-SEC-1, built as `IMembershipRepository.FindRoleAsync`); same role → return unchanged → `ChangeRole` → save. |
| 6 | `RemoveMember` | `DELETE …/members/{userId}` § 1.6 | `organizations.membership.write` | as 5 up to the rule (removing an admin counts the others the same way), then `DeleteAsync` — the cascade ends the user's team memberships in the same statement (BR-M1-36) — 204. Work items keep their assignee (nothing touches them). |
| 7 | `GetTeamMembers` | `GET …/teams/{t}/members` § 2.1 | `organizations.team.read` | team reachable `(o, p, t)` → `ListTeamMembersAsync` → views, order of § 3.7. |
| 8 | `SearchTeamMemberCandidates` | `GET …/teams/{t}/member-candidates` § 2.2 | `organizations.team.write` | team reachable → `ListMemberIdsAsync` → `SearchAmongAsync(text, ids)` (active only). |
| 9 | `AddTeamMember` | `POST …/teams/{t}/members` § 2.3 | `organizations.team.write` | project reachable → **team `FOR SHARE`, re-checked `Active`** (ADR-0026 rule 8; else `team.not_found`) → value `userId` → `LookupAsync` known and active and `IsMemberAsync`, else `member_not_in_organization` → pre-check `member_exists` → `Add` → `SaveAsync` (23505 → `member_exists`; the composite-FK violation → `member_not_in_organization`). |
| 10 | `RemoveTeamMember` | `DELETE …/teams/{t}/members/{userId}` § 2.4 | `organizations.team.write` | team reachable → `DeleteAsync` → 0 rows → `team.member_not_found`. |
| 11 | `CreateOrganization` (changed) | `POST /api/organizations` § 3.1 | instance code | in the existing unit of work, after the organization is added: `OrganizationMembership.Founder(organization.Id, currentUser.SubjectId, now)`. A failed seed rolls both back (US-6, doc-006 US-2 sc. 5). |
| 12 | `CreateProject` (changed) | `POST …/projects` § 3.1 | `organizations.project.write` (pass B) | after `projects.Add(created)`: `TeamMembership(created.FirstTeam.Id, organizationId, currentUser.SubjectId)`; the composite-FK violation (the creator's membership ended mid-request) maps to `organization.not_found` and rolls the project back. Lapses with the first team under feat-028. |
| 13 | `DeleteTeam` (changed) | § 3.2 | `organizations.team.write` (pass B) | after the cascade of areas, inside the existing `FOR UPDATE` on the team: `ITeamMembershipRepository.EndAllAsync(team.Id)` (BR-M1-18, BR-M1-36). |
| 14 | `GetTeams` (changed) | § 3.3 | `organizations.team.read` (pass B) | `TeamResponse.IsMember` from `TeamRow` (§ 3.10). |
| 15 | `GetAssigneeCandidates` (changed) | § 4.2 row 34 | `workitems.workitem.write` (pass B) | `ListMemberIdsAsync` → `SearchAmongAsync` — active members only. |
| 16 | `SaveWorkItem` (changed) | § 3.4 | `workitems.workitem.write` (pass B) | `AssigneeOfAsync` also asks `IMemberReadModel.IsMemberAsync(organizationId, userId)`; `IsAvailable = known ∧ active ∧ member`. The domain (`WorkItem.Save`, `AssigneeEdit`) is unchanged: it already refuses only a **newly set** unavailable assignee and always accepts `null`. |

Rows 11–16 are *additive* in pass A and keep their interim guards; the codes in
the "Code" column that say pass B arrive with the retrofit (§ 4.1). The
read-side flag of the work-item reads is Infrastructure (§ 3.10), phase 1.

### 3.7 Member lists and searches over `IUserDirectory` (spec NFR Volumes)

- `GetMembers` runs a fixed number of queries: the memberships of the
  organization (one), then the projection lookups of `ceil(ids / 500)`
  statements for the union of member ids and actor ids. Past 500 ids nothing is
  refused (US-1 "Past 500 ids…"). `GetTeamMembers` the same.
- **Order** (api-contract § 0.5): known users by `DirectoryUserOrder.Comparer`
  (display name under `ru` at primary strength, then id by code point), unknown
  users last by id. It is sorted in memory over the ids the page already holds;
  the ban in backend.md is on reading *extra* rows.
- `SearchTeamMemberCandidates` and `GetAssigneeCandidates` restrict to members
  with `AmongIds` = the member ids, batched by 500 and merged by
  `UserSearchResult.Merge` — at most 20 in order, `moreMatched` when any batch
  or the merge says so.
- All three work with Keycloak stopped: they read `directory_users` only
  (mod-014 BR-UP-9; the architecture rule already forbids an `HttpClient` there).
- **No paging** (P-5). mod-002's contract expected the members to be the first
  registry (ADR-0015 `Paging<T>`). The stories ask for all 501 listed, with no
  page, sort or filter control, and 500 rows of a few fields render well inside
  the 2 s budget; a paged envelope would be a decision nobody asked for. If a
  later story wants a filter on «Участники», the list becomes a registry then.

### 3.8 Concurrency (NFR Concurrency, ADR-0011, ADR-0026 rule 8)

| Race | Echelon | How it is closed |
|---|---|---|
| Two administrators remove or downgrade each other (BR-M1-4) | 3 — row lock | every `ChangeMemberRole`/`RemoveMember` takes the **organization row `FOR NO KEY UPDATE`** (P-4), then re-evaluates the rule on what is committed. They queue; the second sees the first's result: it counts one administrator fewer and is refused (`last_admin`), or — if its own caller's membership is already gone — is answered as a non-member (the actor re-read). |
| The same user added to an organization twice (BR-M1-5) | 1 — constraint | PK `(organization_id, user_id)`; 23505 → `already_member` |
| The same user added to one team twice | 1 — constraint | PK `(team_id, user_id)`; 23505 → `member_exists` |
| A team member added while the organization membership is removed (BR-M1-10) | 1 — constraint | composite FK. The insert's referential check takes `FOR KEY SHARE` on the parent membership; the removal's `DELETE` needs `FOR UPDATE` and so waits; its cascade then deletes the newly committed child. If the removal committed first, the insert fails the FK (23503) → `member_not_in_organization`. Afterwards the user is in no team of the organization either way. |
| A team member added while the team is deleted (BR-M1-36) | 3 — row lock | the add takes the team `FOR SHARE` and re-checks `Active` (ADR-0026 rule 8, the mechanism binders already use); `DeleteTeam` holds it `FOR UPDATE` and ends the memberships under it. Deletion first → the add answers `team.not_found`; add first → the deletion's `EndAllAsync` removes it. |
| A role change and a removal of the same member | 3 | both take the organization row first; the second finds the changed state (`membership.not_found` for a change of a removed member) |
| Creating a project while the creator's membership is removed | 1 | the FK; § 3.6 row 12 |
| Assigning a user who is removed at the same moment (BR-M1-26) | none | no lock: the rule is checked at save time, and an assignee who is no longer a member is a state the rule already allows (the item keeps them) |

Lock order, to rule out deadlocks: organization row → member row → (team row is
taken *before* the membership insert in `AddTeamMember`, and the removal path
never takes a team row). `ChangeMemberRole`/`RemoveMember` never wait on a team
lock; `AddTeamMember` never takes the organization row. The tests make "at the
same moment" deterministic with the existing `CommitGate` and `PgLocks`
(mod-002 plan § 8.3): hold one transaction before its commit, run the other,
release.

### 3.9 Creation memberships, the end of a team's memberships, the deactivated lists

- **Creator as administrator** (§ 3.6 row 11) is the same transaction as the
  organization and its process seed; it stores the creator whatever the
  projection says (BR-M1-25, mod-014 BR-UP-17). The first reads show them as
  `unknown` until the projection receives them; they administer at once,
  because access is read from the membership alone (BR-M1-32).
- **Project creator as first-team member** (row 12), the same.
- **A deleted team's memberships end** (row 13); **removing an organization
  membership ends the user's team memberships** (the cascade, row 6).
- **D-13, the deactivated lists** (pass B): `GET Org/deactivated` joins the
  caller's memberships and keeps the organizations where the role is one of
  `OrganizationRoleBundles.RolesHolding("organizations.organization.deactivate")`
  (today `admin`; derived, not hard-coded). `GET …/projects/deactivated` and
  both restores are routed by the `*.deactivate` code (§ 4.1).

### 3.10 The assignee rule, the `isMember` flags, the default team

- **The write rule (BR-M1-26).** One write path sets an assignee today: the
  `assignee` of `PATCH …/workitems/{identifier}` (create takes no assignee; a
  card move is a state change on the same PATCH). The rule lives in one place,
  the handler's `AssigneeOfAsync` (§ 3.6 row 16). A repository-wide search for
  `AssigneeId =` / `AssigneeEdit(` is a checklist step of the lane: a later
  path that sets an assignee must go through the same function (feat-027's
  create adds `parentId`, not an assignee).
- **The read flag.** `WorkItemAssigneeView(Id, DisplayName, Email)` becomes
  `(Id, DisplayName, Email, IsMember)`. `WorkItemReadModel` and
  `BacklogReadModel` already look the assignees of their page up in the
  projection; each adds **one** statement:
  `SELECT m.user_id FROM organization_memberships m JOIN projects p ON
  p.organization_id = m.organization_id WHERE p.id = @projectId AND m.user_id =
  ANY(@ids)` and sets `IsMember` from it. The list, the item, the backlog and
  the board all go through these two read models; the save and create answers
  go through `IWorkItemReadModel.GetAsync`. No mark is rendered here (D-4).
- **The default team (D-5, BR-M1-27).** The server's part is the flag:
  `GetTeams` reads the caller's team memberships *with the team list* (§ 3.4
  `ITeamReadModel`), so a change counts from the next read of that list. The
  choice (the first team, in the list's order, with `isMember`, else the first)
  is the panel's, in stage 6.

### 3.11 Endpoints, error codes, DI

- `MembershipEndpoints.cs` (`MapMembershipEndpoints`) is written **in full in
  phase 1**: the six organization-level routes and the four team-member routes,
  each `.RequiresOrganizationPermission(code)`, `.Produces…`/`.ProducesProblem…`
  per api-contract, tags `Organizations`. `Program.cs` maps it. The team-member
  routes sit in the same file (they share the `organizationId` prefix).
- Error codes: the ten constants in `OrganizationErrorCodes`, the ten entries
  in `contracts/errors/organizations.json` with ru and kk (`kkNeedsReview`),
  and the regenerated `docs/error-codes.md` (`MUSTASK_UPDATE_ERROR_CODES=1`)
  land in **one commit** — constants and JSON alone each turn
  `ErrorCatalogCompletenessTests` red.
- DI: `Application/DependencyInjection.cs` (the ten handlers),
  `Infrastructure/DependencyInjection.cs` (repositories, read models, access
  read model), `Api/Program.cs` (`MapMembershipEndpoints`, the scoped
  `IOrganizationAccess`).
- `MembershipSaveChanges` (or the existing `OrganizationsSaveChanges`, extended)
  maps the three constraints of § 3.3; `PostgresConflictDetection` gains
  `IsForeignKeyViolationOnConstraint` (23503).

### 3.12 Tests of pass A, per story

Named `Method_Scenario_Expected`, each carrying a comment with the story and
the scenario. App = Application.Tests (handlers on fakes, the order of answers
pinned), Api = Api.IntegrationTests (Testcontainers), Inf = Infrastructure.Tests,
Dom = Domain.Tests, Con = Contracts.Tests. Frontend-only scenarios are listed so
none is lost; the backend's share of them is named.

**US-1 — membership** (doc-006 US-3 scenarios listed in the story, then the new ones)

| Scenario | Tests |
|---|---|
| adds a member; effective on the next request | Api `AddMemberEndpointTests` (201 + `Member` shape; the new member's `GET Org`-style read after pass B, and in pass A `GET …/access` answers their role); App order |
| search needs two characters | Api `SearchMemberCandidatesEndpointTests` (empty answer under 2; "no request is sent" is the panel's) |
| more than twenty matches | same (`moreMatched`, exactly 20, in order) |
| inactive or unknown neither offered nor added | same (not offered); Api `AddMember` 409 `user_unavailable`, no row |
| an unknown administrator does not count | App `ChangeMemberRoleHandlerTests`, `RemoveMemberHandlerTests` (admins A active + B unknown → A's step-down refused); Api one end-to-end |
| role change from the row menu | Api `ChangeMemberRoleEndpointTests` (200, `roleChangedBy/At`); same role = 200 with the fields untouched |
| a role outside the three | Api `AddMember`, `ChangeMemberRole` 400 `role_invalid` under `validation_failed`, role unchanged |
| removes a member | Api `RemoveMemberEndpointTests` (204; gone from the list and from the team's list; the item keeps its assignee; the next request answers 404 — pass B for the org routes, `GET …/access` in pass A) |
| cancelling the removal | frontend |
| removes themselves while another admin remains | Api `RemoveMember` self (204; the next `GET …/access` is 404); App rule |
| membership already gone | Api change/remove → 404 `membership.not_found` |
| inactive or unknown member shown as such | Api `GetMembersEndpointTests` (`state`, order, unknown last, actor `UserRef`s) |
| past 500 ids | Inf `MemberReadModelTests` (501 members, some `addedBy` no longer members: all listed with names; 3 lookup statements); Api `GetMembers` 501 rows; `SearchTeamMemberCandidates` finds a member among 501 |
| doc-006 sc. 5 (Keycloak down) | the endpoint tests run without Keycloak; `UserDirectoryBoundaryRules` already forbids an `HttpClient` in the projection's reach |
| sc. 7 / 14 / 15 / 16 | `GetMembers` (who/when); a projection update flips `state` back to `active`; member/viewer read, admin writes (403 for the others — see US-2); a removed member's next request is 404 |
| sc. 11 (mutual removal) | Api race test (`CommitGate`/`PgLocks`): two admins, each removes the other → exactly one 204, one 409 `last_admin` (or 404 org), one admin left |

**US-2 — scoped access** (pass A: the mechanism on the new routes; pass B: on every route, § 4.9)

| Scenario | Tests |
|---|---|
| organization list holds only the user's | pass B (row 1) |
| empty state | pass B + frontend |
| non-member gets 404 at every depth | pass A: Api `ScopedAccessTests` on the new routes (byte-equal to a never-existing organization, a malformed id, and a deactivated one); pass B: every row of `OrganizationAddressedRoutes.All` |
| member whose role lacks the code is refused | pass A: Api `ScopedAccessTests` (member → `PUT role`, `POST members`, `GET member-candidates`: 403, nothing changes) |
| order of the answers | Api `ScopedAccessOrderTests`: no token 401 before anything; **a non-member with a malformed JSON body gets 404 (not 400)**; a viewer writing to a project that does not exist gets 403 (pass B); a member reading it gets `project.not_found` (pass B) |
| deactivated lists and restore | pass B |
| body ids outside the address | pass B (the containment test) |
| a parent write needs the link code | pass B (rows 29, 31) |
| the inventory | pass B (§ 4.2) |

**US-3 — team members**

| Scenario | Tests |
|---|---|
| adds an organization member | Api `AddTeamMemberEndpointTests` (201 `UserRef`, role unchanged) |
| only active members offered and accepted | Api `SearchTeamMemberCandidates` (another organization's user, a projected non-member, a disabled member: none; "Пользователи не найдены" = empty); `AddTeamMember` 409 `member_not_in_organization` for each |
| at most once; concurrent duplicate | Api `AddTeamMember` 409 `member_exists`; race test, two adds → one 201, one 409, listed once |
| removes a team member | Api `RemoveTeamMemberEndpointTests` (204; still reads other teams — pass B for the read) |
| removing one already gone | Api 404 `team.member_not_found` |
| a team with no members | Api `GetTeamMembers` empty array; the copy is the panel's |
| inactive or unknown team member stays | Api `GetTeamMembers` (`state`, kept) |
| the project's creator is the first team's first member | Api `CreateProjectEndpointTests` (lane 12) |
| a creator the projection does not know | Api `CreateOrganizationEndpointTests`/`CreateProject` with a token whose `sub` is not in the projection: both succeed, creator is admin and the only first-team member, shown `unknown` |
| a deleted team's memberships end | Api `DeleteTeamEndpointTests` (two members, one also in another team; the other team keeps hers; the deleted team's list and page answer 404 `team.not_found`) |
| add while the organization membership is removed | Api race test (FK path both orders) |
| add while the team is deleted | Api race test (`FOR SHARE` vs `FOR UPDATE`, both orders) |

**US-4 — served codes.** Backend: Api `GetOrganizationAccessEndpointTests` (the role and **exactly** the bundle for each of the three roles, sorted; no instance code; `Cache-Control: no-store`; a non-member 404; the answer changes on the next call after a role change). "Instance codes still come from the token": Api — a token with `organizations.organization.create` and no membership still creates an organization, and `…access` of any organization is 404 for them. Everything else in US-4 (controls, skeleton, retry, refocus, the empty hints) is stage 6.

**US-5 — assignee.** Api `GetAssigneeCandidatesEndpointTests` (offers only active members of the item's organization; neither a disabled member nor a member of B; "Никого не найдено" = empty), `SaveWorkItemEndpointTests` (a non-member, a disabled, an unknown id: 409 `assignee_unavailable`, assignee unchanged; an ex-member stays and other fields save; `null` always allowed; a member re-added as viewer is accepted), the read-flag tests `GetWorkItems`/`GetWorkItem`/`GetBacklog`/`GetBoard` (`assignee.isMember` false for the ex-member, true for a member), App `SaveWorkItemHandlerTests` (availability = known ∧ active ∧ member), Inf `WorkItemReadModelTests`/`BacklogReadModelTests` (the one membership statement).

**US-6 — existing data.** Inf `AddOrganizationAccessMigrationTests` (§ 3.3: each existing creator is the admin; B deactivated too; nobody else; creator disabled/unknown still gets it; project creators join the first team only when members; empty database; revert); "everyone else is a non-member until added" is pass B's `ScopedAccess` read of an organization created before. "A creator the projection does not know yet administers as usual" — Api: such a creator adds a member, changes roles, and is **refused** `last_admin` on stepping down while no known active admin remains, then permitted once the projection receives them.

**US-7 — default team.** Api `GetTeamsEndpointTests` (`isMember` per team for the caller; changes after add/remove; none → all false). The selection scenarios are stage 6.

**US-8** is pass B (§ 4.9). Contract tests for the ten new catalog entries: Con `OrganizationErrorCatalogTests` (extended): ru and kk present, statuses as in api-contract § 5, `docs/error-codes.md` regenerated.

### 3.13 Order of work and lanes of pass A

**Phase 1 — sequential, the backend developer's.** Each step is a commit that
builds and passes the suite.

1. **Catalog and roles** (§ 3.1) with the constant rename everywhere and the
   Dom tests.
2. **Domain and contracts** (§ 3.2, § 3.11 first bullet pair): entities, rule,
   the ten codes, `organizations.json`, regenerated `docs/error-codes.md`.
3. **Infrastructure** (§ 3.3, § 3.4): configurations, `DbSet`s, constants, the
   `AddOrganizationAccess` migration with the seed, `SaveChanges` mapping, the
   repositories, the read models (the new ones **and** the changed
   `TeamReadModel`, `WorkItemReadModel`, `BacklogReadModel`), DI; Inf tests.
4. **Application surface:** the abstractions; `UserDirectoryBatches`; for every
   use case of § 3.6 its command or query, result, response records and a
   handler whose `Handle` throws `NotImplementedException` (except the
   reference); registrations; in-memory fakes under
   `Mustask.Application.Tests/TestDoubles/Organizations/` (memberships, team
   memberships, member read model, access) and `FakeOrganizationAccess`.
5. **Api:** the access check (§ 3.5) with its tests; `MembershipEndpoints.cs`
   in full; `Program.cs`; the test helpers every lane uses —
   `MembershipTestData` (adds an organization membership / a team membership
   through the repositories, no endpoint; creates a "member of role X" with a
   token), `OrganizationsTokens` gains a `For(subject)` that carries the
   projection-known subject; the projection fixture (a known active/inactive
   user) reused from mod-014's tests.
6. **The reference use case, `AddMember`, end to end** (below).
7. **Close phase 1:** `bash scripts/verify.sh` green, a `[dev]` Log line,
   hand-over to the orchestrator for the lanes.

**The reference use case: `AddMember`.** It is the richest single pattern for
the lanes to copy: the new category and the 404/403 order; a value validation
with two field reasons; a reference resolved through the projection with its
own refusal; a pre-check plus the PK race mapped to one code; `201 Created`
with a `Location`; the shared `MemberView`; App tests on fakes that pin the
order of answers; Api tests (the not-found/403/401 theories, the projection
variants, the duplicate, the race).
`GetMembers` is the second read example (the batched lookups); a lane may copy
either and stops there (04-backend.md § 4a.1).

**Phase 2 — the lanes.** A lane's write zone is
`Features/Organizations/<UseCase>/`, `Mustask.Application.Tests/Features/Organizations/<UseCase>/`
and `Mustask.Api.IntegrationTests/Features/Organizations/<UseCase>EndpointTests.cs`
(for L-14..L-16 the zones named in the table); nothing else. The handler body
is the lane's; the command/result/response records of phase 1 are the contract.
A missing repository method, read-model method, fake or helper is escalated,
never added outside the zone. No lane runs tests; the orchestrator runs one
verification.

| Lane | Use case | Beyond the common tests | Race test |
|---|---|---|---|
| L-1 | `GetOrganizationAccess` | each role's exact bundle; `no-store`; non-member 404 | — |
| L-2 | `GetMembers` | order with «Ё», case, unknown last; 501 members; actor refs | — |
| L-3 | `SearchMemberCandidates` | 2-character floor, 20 + `moreMatched`, 101 chars → 400 | — |
| L-4 | `ChangeMemberRole` | step-down with/without another admin; unknown admin; same role no-op | two admins demote each other |
| L-5 | `RemoveMember` | self-removal; team memberships cascade; assignee kept; inactive admin | two admins remove each other; vs a team add |
| L-6 | `GetTeamMembers` | order, unknown/inactive, empty | — |
| L-7 | `SearchTeamMemberCandidates` | members only, 501 members | — |
| L-8 | `AddTeamMember` | active-member rule, duplicate | duplicate add; vs org removal (both orders); vs team delete (both orders) |
| L-9 | `RemoveTeamMember` | already gone | — |
| L-10 | `CreateOrganization` | founder membership, rollback on seed failure, unknown creator | — |
| L-11 | `CreateProject` | first-team member, unknown creator | creator removed mid-request |
| L-12 | `DeleteTeam` | memberships end, the other team keeps hers | vs `AddTeamMember` (both orders; shared with L-8) |
| L-13 | `GetTeams` | `isMember` per caller | — |
| L-14 | `GetAssigneeCandidates` | members only; zone adds `Features/WorkItems/AssigneeMembershipEndpointTests.cs` | — |
| L-15 | `SaveWorkItem` | the assignee rule (§ 3.12 US-5) | — |
| L-16 | `GetWorkItem`, `GetWorkItems`, `GetBacklog`, `GetBoard` | `assignee.isMember` only | — |

Waves of five (a wave of deep-tier agents has hit a rate limit): wave 1
L-1..L-5, wave 2 L-6..L-10, wave 3 L-11..L-16 (the race lanes L-4, L-5, L-8,
L-12 are the ones to give the deep tier). The orchestrator may reorder: no lane
depends on another (`MembershipTestData` builds state through the
repositories).

**Phase 3 — the orchestrator's:** one `bash scripts/verify.sh`, the commits by
explicit path, the contract re-checked against the code, a `[dev]` Log line.

## 4. Pass B

### 4.1 The retrofit: rows 1–37 → code

Every route below gets, in its endpoint file, exactly one of
`.AuthenticatedOnly(reason)` (rows 1–2 only), `.RequiresPermission(instance
code)` (row 3, 38–41) or `.RequiresOrganizationPermission(code[, acceptsDeactivated: true])`.
`AuthenticatedOnly(…).RequiresOrganizationContext()` is removed everywhere, and
`InterimAccess`, `ReadAccess`, `WriteAccess`, `SaveAccess`, `LinkAccess` and
the process read's prose go with it. `.ProducesProblem(403)` is added to every
organization-scoped route; the OpenAPI test is extended (§ 4.9).

| Rows | File | Declaration |
|---|---|---|
| 1, 2 | `OrganizationEndpoints` | `AuthenticatedOnly("Lists only the organizations the caller belongs to; the deactivated list only those where the role holds organizations.organization.deactivate")`; read models narrowed by membership (§ 3.4, § 3.9) |
| 3 | `OrganizationEndpoints` | unchanged (`RequiresPermission(OrganizationsOrganizationCreate)` + `WithForbiddenErrorCode(create_forbidden)`) |
| 4, 5, 6, 7 | `OrganizationEndpoints` | `OrganizationRead`, `OrganizationWrite`, `OrganizationDeactivate`, `OrganizationDeactivate` with `acceptsDeactivated: true` |
| 8, 9, 10 | | `ProjectRead`, `ProjectDeactivate`, `ProjectWrite` |
| 11, 12, 13, 14 | | `ProjectRead`, `ProjectWrite`, `ProjectDeactivate`, `ProjectDeactivate` |
| 15, 16 | | `TeamRead` |
| 17, 18, 19, 20 | | `TeamWrite` |
| 21 | | `AreaRead` |
| 22–26 | | `AreaWrite` |
| 27 | `ProcessEndpoints` | `ProcessDefinitionRead` |
| 28, 30, 33, 35, 37 | `WorkItemEndpoints`, `BoardEndpoints` | `WorkItemsWorkItemRead` |
| 29, 31 | `WorkItemEndpoints` | `WorkItemsWorkItemWrite`; the handlers (`CreateWorkItemHandler`, `SaveWorkItemHandler`) take `IOrganizationAccess` and, when the command carries a parent, check `Holds(WorkItemsLinkWrite)` **first** (before the project check), throwing `ForbiddenException(AccessForbidden)` — the second code of rows 29/31. `CreateWorkItemCommand` has no `parentId` until feat-027: the check ships in `SaveWorkItemHandler` now and in the create handler at feat-027's rebase. |
| 32 | | `WorkItemsLinkWrite` |
| 34 | | `WorkItemsWorkItemWrite` (a GET with a write code is legitimate) |
| 36 | `BoardEndpoints` | `BoardsBacklogReorder` |
| 38–49, health, OpenAPI | | unchanged |
| new routes (pass A) | `MembershipEndpoints` | already declared |

A row's handler needs no change beyond what § 3.6 lists: the requirement has
answered the organization level before it runs, and every handler already
re-checks its nested links (§ 3.5 audit). Handlers that re-parse
`OrganizationId` keep doing so (the filter-is-the-boundary-not-the-only-check
rule of mod-002 § 7.3 stands).

### 4.2 The inventory test (D-11, BR-M1-33)

`Features/Authorization/OrganizationAccessInventoryTests.cs` replaces
`OrganizationContextInventoryTests` and extends `EndpointCategoryInventoryTests`
(whose "exactly one category per endpoint" and public-route list stay):

1. every real endpoint whose route has `{organizationId}` has `Kind ==
   OrganizationPermission`, its code is in `PermissionCatalog.OrganizationCodes`,
   and its `IAuthorizeData` policy name **equals**
   `PermissionPolicies.OrganizationPolicyName(code, acceptsDeactivated)` — the
   code it declares is the code it enforces;
2. no endpoint without `{organizationId}` has that kind or declares an
   organization code;
3. no POST, PUT, PATCH or DELETE declares a code ending in `.read`, unless
   listed in an `AllowedWriteWithReadCode` dictionary of (route, reason) —
   **empty today**; a GET with a write code (row 34) is fine;
4. only the organization's restore accepts a deactivated organization;
5. the set of such routes equals `OrganizationAddressedRoutes.All` (extended
   with the ten new routes), no more, no less;
6. **behavioural proof** (so a route that declares the right code but enforces
   a wrong one fails): for every route of `OrganizationAddressedRoutes.All` and
   each of the three roles, a request with fake nested ids and a well-formed
   body is 403 `access.forbidden` iff the role's bundle lacks the route's code,
   and otherwise anything but that 403 and anything but the organization 404; a
   non-member always gets the byte-identical organization 404;
7. **containment**: § 3.5's cross-organization theory for every nested row;
8. the OpenAPI document lists 403 on every organization-scoped operation.

Adding a route under `/api/organizations/{organizationId}` without a code, or a
write route with a read code, turns `scripts/verify.sh` red (BR-M1-33). Lines
that merge after this task enforce their own codes or fail.

### 4.3 The realm import (D-7, BR-M1-29, BR-M1-37)

`deploy/keycloak/realms/mustask-realm.json`:

- `roles.client.mustask-api` — unchanged: the three instance codes
  (`PermissionCatalog.InstanceCodes`).
- `roles.realm`: **`instance-admin`** (composite of the three codes),
  `dictionaries-admin` and `organization-creator` unchanged, and
  **`default-roles-mustask`** declared as a composite of realm `offline_access`,
  `uma_authorization`, client `account` `view-profile`, `manage-account` (Keycloak's
  built-in composites, listed so the drift check's exact comparison and the
  contract test both see them) **plus** `mustask-api/dictionaries.languages.read`.
  `admin`, `member`, `viewer` are removed.
- top-level `defaultRole`: `{ "name": "default-roles-mustask" }`, so Keycloak
  takes the declared role as the realm's default instead of creating its own.
- `groups`: `Администраторы` → `instance-admin`; `Администраторы справочников`
  → `dictionaries-admin`; `Создатели организаций` → `organization-creator`.
  `Сотрудники` and its subgroup are removed.

**Spike first (risk R-1).** Before anything else in pass B: bring up
`quay.io/keycloak/keycloak:26.4` with `start-dev --import-realm` on the
candidate file and read the realm back through the admin API — the default role
must be `default-roles-mustask` with exactly the declared composites, a fresh
user in no group must get `resource_access.mustask-api.roles =
[dictionaries.languages.read]` in their token, and no duplicate-role error may
appear. If Keycloak rejects the `roles.realm` entry beside `defaultRole`, the
fallbacks, in order: the composites only on top-level `defaultRole`
(`KeycloakRealmImport.Parse` then reads `defaultRole` into a `DeclaredRole`);
the legacy `defaultRoles` list. Whichever form wins, the contract tests and the
drift check read the **same** declaration, and the spike's finding is a Log line.

The spike does not stay a one-off: it becomes an opt-in live test,
`LiveRealmImportTests` (`Features/UserDirectory/Live/`, tagged
`KeycloakProvider`, switched by the same `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1`),
that starts the stand's Keycloak with the **repository's real realm file** (the
stand's placeholders filled in) and asserts the default role's composites, the
token of a user in no group (`dictionaries.languages.read` and no other code),
the three groups' mappings, and that the real drift check reports nothing. The
existing live fixture imports a throwaway realm of its own, so this is new
coverage. `scripts/verify.sh` classifies `deploy/keycloak/realms/*` as `dotnet`
only; whether a realm edit should also trigger the `provider` bucket (Docker) is
the orchestrator's call (OP-10).

### 4.4 The contract tests restated (constraint 3, BR-M1-29)

`Mustask.Contracts.Tests` (no Docker, reads the import file):

| Test | Change |
|---|---|
| `KeycloakRealmRolesTests.MustaskRealm_DeclaresClientRoleForEveryPermissionCode` / `…HasNoClientRolesOutsideThePermissionCatalog` | compare to `PermissionCatalog.InstanceCodes` |
| `…RealmRolesAreBusinessRoles_NotPermissionCodes` | against `AllCodes` (instance and organization) |
| `…Realm_EveryRoleIsCompositeOfDeclaredPermissionsOnly` | unchanged for every role **except** the default role, which has its own test below; no loosening |
| `…Realm_HasAdminRoleWithEveryPermission_MappedToSomeGroup` | restated: `instance-admin` holds **every instance code and no organization code**, and ≥ 1 group maps it |
| new `RealmInventory_IsExactlyTheNamedOne` | realm roles = `instance-admin`, `dictionaries-admin`, `organization-creator`, the default role; groups = the three; no role named `admin`, `member`, `viewer`; no group `/Сотрудники…` |
| new `DefaultRole_HoldsReadAndOnlyKnownBuiltIns` | holds `dictionaries.languages.read` and no other `mustask-api` code; its other composites are exactly the Keycloak built-ins listed in § 4.3 |
| `OrganizationPermissionRealmTests` | `OrganizationPermissionCatalog.AllCodes` → `PermissionCatalog.OrganizationCodes`; the "scan reaches the codes" test compares with `InstanceCodes` |
| `DictionaryPermissionsRealmTests` | "every panel role holds read" → the **default role** holds read (so every user reads); `instance-admin` and `dictionaries-admin` hold read and write; write is held only by those two; `organization-creator` holds no dictionary code |
| `OrganizationCreateRightRealmTests` | `admin` → `instance-admin`; the create right is held only by `instance-admin` and `organization-creator`; granted only through «Администраторы» and «Создатели организаций» |
| `SessionCookieContractTests` (Api) | fake group claims stop naming removed groups |

### 4.5 The drift check (BR-M1-37)

`KeycloakStandDriftCheck` is **not changed in logic**: it compares the realm
roles the import declares, and the default role is now among them, so its
composites are compared exactly (the existing `name != defaultRoles` clause only
spares an *undeclared* default role). Its remedy text stays (D-15 (1)). The
tests (`KeycloakStandDriftCheckTests`) are restated against the new import:
`FakeKeycloak.FromImport` stops hard-coding `default-roles-mustask`; group
cases use `/Администраторы справочников`; role cases use `dictionaries-admin`;
`TheImport_IsReadWithTheStandsPlaceholders` asserts the new groups; new cases —
"the default role without the read code fails naming `default-roles-mustask`",
"the old realm (the pre-mod-016 import kept as test data) fails with exactly the
findings the AGENTS.md list names (the roles `admin`, `member`, `viewer`, the
missing `instance-admin`, the default role's composites, the old groups'
mappings)", and **"the old realm after the live-realm command has run (§ 4.7)
has no finding"**.

### 4.6 `keycloak-seed.sh`, the E2E fixture, `src/backend/AGENTS.md`, conventions

- **`scripts/keycloak-seed.sh`.** The administrator goes to `/Администраторы`
  (now `instance-admin`); the optional `member` user goes to **no group**
  (`seed_user … ""` skips the group step) and reads the dictionaries through
  the default roles; the header and the `MEMBER_PASSWORD` description say so,
  and that `member` sees no organization until an administrator adds them
  (US-8 "The stand seed follows the inventory"). **The Keycloak admin token
  leaves the command line** (SEC-4): `api()` stops passing `-H "Authorization:
  Bearer $token"`; the token is written once to a private temp file
  (`umask 077`, removed by the existing `trap`) as a curl config line
  `header = "Authorization: Bearer …"` and read with `curl -K`. `printf` is a
  builtin, so the token passes through no process argument; the password
  already travels over stdin. A check in `Mustask.Contracts.Tests` greps the
  script for `Bearer $token` on a `curl` line.
- **E2E fixture's group.** `KeycloakStandProvisioningOptions.GroupPath` stays
  `/Администраторы`: the fixture is the instance administrator, creates
  organizations and becomes their administrator; its doc comments say so.
  Handed to stage 6: `src/frontend/e2e/support/keycloakAdmin.ts` puts its
  throwaway reader in `/Сотрудники` (gone) — the reader is now a user in no
  group; and the specs the spec names (16, 19.9, 20.4, 21.6) need members added
  first (D-3).
- **`src/backend/AGENTS.md`** gains: the model in three lines; the organization
  on the stand (the creator is its administrator; others are added on
  «Участники»; the seeded `member` sees nothing until added); **the human's
  order after the merge** — back up if wanted, apply the migration per the
  runbook (it seeds the creators), start the AppHost (the `keycloak` resource
  stays unhealthy and the stand not ready: the drift check reports the old
  realm roles `admin`, `member`, `viewer`, the new `instance-admin`, the
  composites of `default-roles-mustask` and the mappings of `/Администраторы`),
  run `bash scripts/migrate-live-realm.sh` (password from the environment),
  the next probe passes and the stand is ready — **never `stand-reset.sh`, which
  the check's text names for these findings**; and that these findings are
  cleared by the command, not the reset (BR-M1-37).
- **`docs/conventions/backend.md`** § "Soft removal" and the one place that
  names the filter: point at the requirement handler (ADR-0027).

### 4.7 The live-realm migration (D-6, BR-M1-28)

**Language and place.** C#, in `Mustask.AppHost` — the stand-only project that
already holds the admin-REST code (`KeycloakStandDriftCheck`,
`KeycloakStandProvisioner`) and is never deployed (ADR-0023, ADR-0025). A shell
script cannot parse the nested JSON this needs safely (the seed script's `sed`
approach is already a known fragility), and a new project would touch
`Mustask.sln`, which the orchestrator owns. Entry: `dotnet run --project
src/backend/Mustask.AppHost -- --migrate-live-realm`, handled by an early branch
of `AppHost.cs` before the distributed-application builder (the way
`--stand-reset` is parsed first), wrapped by `scripts/migrate-live-realm.sh` and
`.ps1` (like `stand-reset`). Code in `Mustask.AppHost/Provisioning/`:
`KeycloakLiveRealmMigration.cs` (the steps, takes an `HttpClient`, options and a
`TextWriter`, returns an exit code — the unit-testable core) and its options.

**Authentication without printing secrets.** The administrator password comes
from the environment (`KEYCLOAK_ADMIN_PASSWORD`; `KEYCLOAK_URI`, default
`http://localhost:5010`; user `admin` of the `master` realm, as the seed script
and the drift check). It is sent once in a form body to the token endpoint — never
as a command-line argument, never in a URL. The access token lives in memory and
in one `HttpClient` default header; no logging handler is attached; exceptions
are reported as the HTTP status and the route template, never headers or bodies.
The output holds only fixed phrases, role and group names, and users as
**username or id**. A test feeds a fake whose token and password are
recognizable strings and asserts neither is in the captured output.

**The steps** (realm `mustask` only; every grant before any removal; each step
idempotent — it reads, acts only on the difference, and says "nothing to change"
when there is none):

| Step | Action | Notes |
|---|---|---|
| S0 | Preflight, read-only: sign in; the realm exists; inventory roles, groups with subgroups, the direct holders of `admin` (paged through `first`/`max`), the groups mapping each of the three roles, the composites that contain them | **Stops with exit code 2 and no write at all** (stricter than "before any removal") when `/Сотрудники` holds a subgroup other than `/Участники`, when it maps a role other than `viewer`, when `/Сотрудники/Участники` maps one other than `member`, or when a realm role other than the default role has `admin` as a composite (open point OP-2). Names what it found. |
| S1 | Create realm role `instance-admin`, composite of `mustask-api` `dictionaries.languages.read`, `…write`, `organizations.organization.create`; if it exists, add the missing composites | |
| S2 | Map `instance-admin` onto `/Администраторы` and onto every other group, at any depth, that maps `admin` | undeclared groups keep their members and other mappings |
| S3 | Map `instance-admin` onto every user who holds `admin` directly | |
| S4 | Add `mustask-api/dictionaries.languages.read` to the composites of `default-roles-mustask` | |
| — | "Grants complete" | |
| S5 | Delete realm roles `admin`, `member`, `viewer` (absent ones skipped) | ends every mapping of them, on users, groups and roles |
| S6 | Delete groups `/Сотрудники/Участники`, then `/Сотрудники` (absent skipped) | its users stay, in no group, and read dictionaries through S4 |

It touches nothing else: no user is created or deleted; ids, credentials,
attributes, every other group membership and role mapping, clients and their
secrets, and Keycloak's built-in roles stay (the default role gains the one
code). A run stopped at any point is completed by running it again: S1–S4 are
safe to repeat, S5–S6 skip what is gone, and since every grant precedes every
removal, a user who held `admin` never has neither role. Exit codes: 0 done or
nothing to change, 2 stopped by S0, 1 a request failed (the message says to run
again).

**Tests** (`Mustask.Contracts.Tests/KeycloakLiveRealmMigrationTests.cs`, no
Docker; a write-capable in-memory fake of the admin surface, `FakeKeycloakAdmin`,
built from the **pre-mod-016 import kept as test data**, with users, direct role
holders, an undeclared group mapping `admin` and `dictionaries-admin`, a user
with a credential and attributes): in-place migration (both admin holders hold
`instance-admin`, the old users read dictionaries, roles/groups gone); changes
nothing else (a before/after diff of the whole fake state equals exactly
BR-M1-28's list, item by item); idempotent (second run: "nothing to change" and
**zero mutating requests**); interrupted at every mutating request k = 1..N
(fault injection), then re-run → the state equals the uninterrupted run, and at
the first `DELETE` of every run the fake asserts every `admin` holder already
holds `instance-admin` (grant before removal); the unexpected structure stops
with no write and names the subgroup; secrets absent from the output; the drift
check against the new import has **no finding** after the command (§ 4.5).
A second, opt-in layer proves the same against a **real** Keycloak 26.4: the
live test `LiveRealmMigrationTests` (same switch and fixture pattern as
§ 4.3) imports the pre-mod-016 realm, creates the users, runs
`KeycloakLiveRealmMigration` against the container, and asserts through the admin
API, through real tokens and through `KeycloakStandDriftCheck` (no finding).
Then the **task-stand trial** (BR-M1-28, US-8): a stand started from the old
code, a user holding `admin` directly, one in `/Сотрудники`, one in
`/Сотрудники/Участники`; the command; a real sign-in of each; a realm export
before and after; the stand ready with no reset. It is the orchestrator's or QA's
run — it cannot be proven offline (risk R-7).

### 4.8 Retrofitting the existing tests

Hundreds of existing tests call organization-scoped routes with a random
`SignedIn()` subject. Once a route is guarded they need a membership.
The approach keeps the diff in the helpers:

- `OrganizationsTestData.CreateOrganizationAsync` also adds the founding
  `admin` membership for `OrganizationsTestData.Creator` (as the handler now
  does) and, for projects, the first-team membership; `CreateProjectAsync`
  likewise.
- `OrganizationsTokens.SignedIn()` with no argument uses `Creator` as its
  subject, so a test that built its data through `OrganizationsTestData` is the
  administrator of it. A test that needs a member, a viewer or an outsider
  calls `MembershipTestData.TokenFor(factory, organizationId, role)` /
  `.Outsider()`.
- Tests that create the organization **through the API** with
  `WithCreateRight()` get a random subject (the creator is its admin); their
  follow-up calls must reuse that token — the edit is per file.
- The E2E-style flows in `Mustask.Api.IntegrationTests` that use several
  tokens are edited per file.

Counts, for the lane plan: 46 test files call `SignedIn()` or
`WithCreateRight()` (124 call sites): 29 in `Features/Organizations`, 11 in
`Features/WorkItems`, 5 in `Features/Boards`, 1 in `Features/Process`. The
dictionaries and session tests use other tokens and are not organization-scoped.

### 4.9 Tests of pass B, per story

**US-2.** Api `OrganizationAccessInventoryTests` (§ 4.2); `ScopedAccessTests`
over all rows: non-member 404 byte-equal for every address in the story's
outline (organization, project, team, area, work item, backlog, board, process,
assignee candidates, access, the restore of a deactivated organization);
member refused on each admin write of the outline and viewer on each member
write (403, nothing changes); a member writes work items / a viewer reads them
and is refused (`WorkItemsAccessEndpointTests`); the order test with a
nonexistent project (viewer 403, member `project.not_found`); the deactivated
lists (admin of A and member of B, C and D deactivated: only C listed; B's
deactivated projects and restore 403, A's open); the containment theory; the
link-code case with a test `IOrganizationAccess` that holds `workitem.write`
but not `link.write` (create-with-parent is feat-027's — the save, now); the
organization list and empty state (row 1).
**US-4** — "Instance codes still come from the token": the dictionaries read by
a user in no group, in `/Создатели организаций` only, and in `/Администраторы
справочников` (Api with test tokens carrying the default-role read) and the write
refused to the first two. **US-6** — "Everyone else is a non-member until
added" on an organization created before the migration. **US-8** — Con tests
(§ 4.4, § 4.5, § 4.7), the seed-script test, the secrets-off-the-command-line
grep, and the task-stand trial; the realm import's token for a user in no group
holds the read code (spike, § 4.3).

### 4.10 Order of work and lanes of pass B

**Phase 1 — sequential.** (1) The Keycloak spike (§ 4.3). (2) The retrofit of
the four endpoint files, `IOrganizationReadModel`, `GetOrganizations`,
`GetDeactivatedOrganizations`, the second-code check, deletion of
`OrganizationContextFilter` and its siblings, the policy-name pieces; (3) the
test helpers of § 4.8; (4) the inventory test; (5) the first folder of tests
retrofitted as the example. Build green after each.

**Phase 2 — lanes by test folder** (write zone: that folder's test files only):
B-1 Organizations organization/project tests (rows 1–14), B-2 team tests
(15–20), B-3 area tests (21–26), B-4 work-item tests (28–34, 11 files), B-5
board and process tests (27, 35–37, 6 files), B-6 the new `ScopedAccess` and
containment suites. Waves of three.

**Phase 3 — sequential, the developer's** (no lanes: few files, cross-cutting):
the realm import and the restated contract tests (§ 4.3–§ 4.5), the live-realm
command and its tests (§ 4.7), `keycloak-seed.sh`, `src/backend/AGENTS.md`, the
conventions edit; then the orchestrator's `bash scripts/verify.sh`, and the
task-stand trial before the human's stand (D-6, D-15 (1)).

## 5. Files

New: `Mustask.Domain/Identity/{PermissionScope,OrganizationRole,OrganizationRoleBundles}.cs`;
`Mustask.Domain/Organizations/{OrganizationMembership,TeamMembership,OrganizationAdminRule}.cs`;
`Mustask.Infrastructure/Organizations/{Configurations/OrganizationMembershipConfiguration,Configurations/TeamMembershipConfiguration,MembershipRepository,TeamMembershipRepository,MemberReadModel,OrganizationAccessReadModel}.cs`;
`Mustask.Infrastructure/Migrations/*_AddOrganizationAccess{,.Designer}.cs`;
`Mustask.Application/Abstractions/Organizations/{IOrganizationAccessReadModel,IOrganizationAccess,IMembershipRepository,ITeamMembershipRepository,IMemberReadModel}.cs`;
`Mustask.Application/Abstractions/UserDirectory/UserDirectoryBatches.cs`;
ten folders under `Mustask.Application/Features/Organizations/`;
`Mustask.Api/Authorization/{OrganizationPermissionRequirement,OrganizationPermissionHandler,OrganizationAccessFailure,HttpContextOrganizationAccess}.cs`;
`Mustask.Api/Features/Organizations/MembershipEndpoints.cs`;
`Mustask.AppHost/Provisioning/KeycloakLiveRealmMigration.cs`;
`scripts/migrate-live-realm.{sh,ps1}`;
the test files of § 3.12, § 4.2, § 4.7, `MembershipTestData`, the old realm import as test data,
`FakeKeycloakAdmin`, `LiveRealmImportTests`, `LiveRealmMigrationTests`.
Changed: `PermissionCatalog`, `OrganizationErrorCodes`, `OrganizationLimits`,
`MustaskDbContext`, `OrganizationsConstraints`, `OrganizationsSaveChanges`,
`PostgresConflictDetection`, `TeamReadModel`, `OrganizationReadModel`,
`WorkItemReadModel`, `BacklogReadModel`, `WorkItemAssigneeView`, `TeamRow`,
the six changed handlers, the four endpoint files, `EndpointCategories`,
`EndpointCategoryMetadata`, `PermissionPolicies`, `ForbiddenErrorCodeResultHandler`,
`JwtBearerSetup`, `Program.cs`, the three DI files, `AppHost.cs`,
`contracts/errors/organizations.json`, `docs/error-codes.md`,
`deploy/keycloak/realms/mustask-realm.json`, `scripts/keycloak-seed.sh`,
`KeycloakStandProvisioningOptions` (comment), `src/backend/AGENTS.md`,
`docs/conventions/backend.md`, the Contracts and Api test files named above.
Deleted: `OrganizationPermissionCatalog.cs` and its test,
`OrganizationContextFilter.cs`, `OrganizationContextEndpoints.cs`,
`OrganizationContextMetadata.cs` and `OrganizationContextFilterTests` /
`OrganizationContextInventoryTests` (pass B).

## 6. Risks and open points

| # | Risk or open point | Handling |
|---|---|---|
| R-1 | Whether Keycloak 26.4 imports `default-roles-mustask` declared in `roles.realm` beside `defaultRole` with account and `mustask-api` composites | the spike of § 4.3 is the first step of pass B; three forms in order; the drift check and the contract tests follow the winner |
| R-2 | The 404/403 are rendered from `AuthorizationFailure.FailureReasons` in the result handler | the order tests (§ 3.12) fail if a framework change drops them; fallback: the handler writes a marker into `HttpContext.Items` and the result handler reads it |
| R-3 | Authorization-middleware code that reads the database is new for this codebase | one indexed query; scoped handler; covered by the same factory as every endpoint test; the architecture rule keeps the projection and Keycloak out of the folder |
| R-4 | Test retrofit volume (46 files, 124 call sites) | the diff is kept in the helpers (§ 4.8); lanes by folder |
| R-5 | Rebase collisions with feat-027 and feat-028 (endpoint files, the migration, `OrganizationPermissionCatalog` rename, team tables) | § 2 lists the steps; the extension removal makes a missed route a compile error, the inventory a red build |
| R-6 | A new organization code added without a bundle | the matrix test fails |
| R-7 | The live-realm command cannot be fully proven offline: the fake mirrors the admin API the code expects, not Keycloak's real behaviour | three layers: the fake-admin tests (logic, order, idempotence, interruption), the opt-in live tests against a real Keycloak 26.4, and the mandatory task-stand trial before the human's stand (BR-M1-28, D-15) |
| R-8 | `UserDirectoryBoundaryRules` forbids `DirectoryUsers`/`ProjectedUser` outside the sub-slice and `DirectoryUser` in `Mustask.Api/Authorization/` | the plan uses `IUserDirectory` in Application/Infrastructure only and keeps the access check on memberships |
| R-9 | Hand-written seed SQL must track the schema | the migration test builds real rows and asserts them; on rebase the seed is re-read (§ 2) |
| OP-1 | ADR-0027 is `proposed`; only the human accepts it, and the ADR-0018 index line gets "amended by ADR-0027" then | orchestrator, with this plan |
| OP-2 | BR-M1-28 does not say what to do with a realm role that has `admin` as a composite | the plan stops and names it in S0 (an extra stop condition); the alternative is to grant `instance-admin` to such a role too — orchestrator to choose |
| OP-3 | Member lists are not paged (P-5), against mod-002 § 0.6's expectation | confirm |
| OP-4 | The check is a requirement handler, not mod-002's filter (P-1); ADR-0026 names the filter | ADR-0027 records it; confirm |
| OP-5 | Two field-reason codes beyond the spec's list (P-9) | confirm; they are additive |
| OP-6 | The candidate search offers existing members (refused on add) as doc-006 US-3 has it; the panel may want a marker | an additive `isMember` on candidates can be added later without breaking the contract |
| OP-7 | The live-realm tool is an AppHost command, not a shell script | confirm; a shell script was rejected for JSON safety and `Mustask.sln` ownership |
| OP-8 | Pass A leaves the interim guard on existing routes (P-7), so it is not shippable alone | by design: all stories are `shippable: no`; pass B closes it |
| OP-9 | `src/frontend/e2e/support/keycloakAdmin.ts` (reader in `/Сотрудники`) and the specs 16, 19.9, 20.4, 21.6 | stage 6, listed in § 4.6 |
| OP-10 | `scripts/verify.sh` runs the live Keycloak tests only in its `provider` bucket, which a realm edit does not trigger | orchestrator: add `deploy/keycloak/realms/*` to that bucket, or run the live tests explicitly at QA |
