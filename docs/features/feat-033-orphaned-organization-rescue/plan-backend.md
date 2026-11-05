# Backend plan — feat-033-orphaned-organization-rescue

Stage 4a (2026-11-03). Contract: `api-contract.md`. Decisions above the plan
level are in ADR-0035 (proposed; accepted only by the human at gate-3) and
ADR-0029 § 9; this plan departs from no convention.

**Baseline.** Written against `origin/main` 41d784ee plus tech-025
(`origin/tech/025-user-projection-without-provider`, PR #55, ADR-0034). The
coding run starts only after main, with #55 merged, is merged into this
branch. What the plan relies on from tech-025: `IUserDirectory.SearchAsync` /
`LookupAsync` unchanged, `DirectoryUser.IsActive` still true for every
sign-in-written row and false only for pre-tech-025 rows marked by the push
feed; `DirectorySearchText.CodePointCount` and `IsNoncharacter`
(`Mustask.Domain.UserDirectory`); the push routes `/api/user-directory/feed/*`
gone (so `EndpointCategoryInventoryTests.ExpectedPublicRoutes` is already
shorter there — nothing for this task). tech-025 adds no migration.

## 1. Slices

Module `Organizations`. One use case = one folder; slices do not reference
each other.

| # | Use case (folder under `Mustask.Application/Features/Organizations/`) | Route | Story |
|---|---|---|---|
| U1 | `GetRescueOrganizations` | `GET /api/organizations/rescue` | US-1 |
| U2 | `SearchAppointeeCandidates` | `GET /api/organizations/rescue/appointee-candidates` | US-2 |
| U3 | `AppointAdministrator` (**reference use case**) | `POST /api/organizations/rescue/{organizationId}/appointments` | US-2, US-3 |
| U4 | `GetRescueAppointments` | `GET /api/organizations/rescue/{organizationId}/appointments` | US-3 |
| U5 | `GetAppointments` | `GET /api/organizations/{organizationId}/appointments` | US-3 |

**No lanes.** Five use cases, but U4 and U5 share one read model and U1 reads
the record table U3 writes; the zones are not disjoint enough to pay for
parallel agents, and economy mode (one agent per stage) applies. One agent,
in the order of § 6.

## 2. Identity: the instance code (BR-RS-1, ADR-0035 § 2)

- `PermissionCatalog`: `OrganizationsOrganizationRescue =
  "organizations.organization.rescue"`, `PermissionScope.Instance`,
  description «Назначение администратора в любую организацию».
- `InstanceRoles`: no row changes — `system-admin` is built from
  `PermissionCatalog.InstanceCodes`, so it gains the code; `user`,
  `organization-creator` and `dictionaries-admin` list their codes explicitly
  and do not. The remark "Later tasks use the role for cross-organization
  administration" is reworded to name ADR-0035 as the one such use (ADR-0035,
  amendment of ADR-0033 § 1).
- Realm import: nothing (it carries role names, not codes; ADR-0033).
- **Panel mirror in the same commit** (ADR-0033 § 2: "change both together"):
  `src/frontend/src/shared/api/tokenClaims.ts` gains the code under
  `system-admin`, and `tokenClaims.test.ts` line 20's expected set gains it.
  Two lines outside `src/backend/`, taken by this stage so the mirror never
  drifts between commits; the rest of the panel is stage 6.

## 3. Domain (`Mustask.Domain/Organizations`)

- **`AppointmentReason`** (value object): `TryCreate(string? raw, out
  AppointmentReason reason, out FieldError? error)`. Trims (`string.Trim()`,
  the same whitespace set as JavaScript's `trim()` for practical input);
  empty → `reason_required`; `DirectorySearchText.CodePointCount(trimmed) >
  500` → `reason_too_long`; U+0000 or a noncharacter → `reason_invalid`.
  `OrganizationLimits.AppointmentReasonMaxLength = 500` (BR-RS-5 — a value
  owned by the spec, not data).
- **`OrganizationAppointment`** (entity, insert-only): `Id` (UUID v7),
  `OrganizationId`, `AppointeeId`, `AppointedBy`, `AppointedAt`, `Reason`.
  One factory `Record(...)`; no mutators, so nothing can edit it (BR-RS-10).
- **`OrganizationAppointmentRule`** (static, pure): given the caller's subject,
  the user id, whether the user is appointable (projection known and active)
  and the user's current membership (`null` or its role), returns the first
  refusal of BR-RS-6a rows 4b–6 or "add" / "raise". The one place the order
  lives; unit-tested exhaustively.
- `OrganizationMembership.Add` / `ChangeRole` are reused as they are.
- `OrganizationErrorCodes`: the eight codes of api-contract § 3.

## 4. Infrastructure

### 4.1 Migration `AddOrganizationAppointments` (additive, reversible)

Table `organization_appointments`:

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` PK | |
| `organization_id` | `uuid` not null | FK → `organizations(id)` `ON DELETE RESTRICT` (organizations are never purged, ADR-0026) |
| `appointee_id` | `varchar(255)` not null | Keycloak `sub`; no FK to the projection (as memberships, BR-M1-17) |
| `appointed_by` | `varchar(255)` not null | |
| `appointed_at` | `timestamptz` not null | `IClock.UtcNow` |
| `reason` | `varchar(500)` not null | PostgreSQL counts `varchar(n)` in characters = code points under UTF-8, the same unit as BR-RS-5 — a second guard |

- Check `ck_organization_appointments_reason_not_blank`: `btrim(reason) <> ''`.
- Index `ix_organization_appointments_organization_id_appointed_at` on
  `(organization_id, appointed_at DESC, id DESC)`: serves U4/U5's order and
  U1's newest-record lookup.
- `Down` drops the table. No data, no backfill. `xmin` comes from the
  convention (ADR-0010); nothing per entity.
- Rebase note: feat-048 runs in parallel; if main brings a migration first,
  this one is deleted and regenerated on the fresh snapshot
  (`docs/conventions/backend.md` § Database).

### 4.2 Writes

- `IAppointmentRecordRepository` (`Application/Abstractions/Organizations`):
  `Add(OrganizationAppointment)` only — no update, no delete. Implemented over
  the request's `MustaskDbContext`, so `IMembershipRepository.SaveAsync`
  flushes the membership and the record in **one `SaveChanges`** inside the
  handler's transaction: both or neither (BR-RS-10, US-3 "No appointment
  without its record").
- Locks: `IOrganizationRepository.LockAsync` (any status, `FOR NO KEY UPDATE`),
  then `IMembershipRepository.LockMemberAsync` (`FOR UPDATE`) — mod-016's
  order (its plan-backend § 3.8), so ChangeMemberRole, RemoveMember and another
  appointment queue behind one another (BR-RS-11).

### 4.3 Reads (`IAppointmentReadModel`, AsNoTracking, projections)

| Method | Statements | Notes |
|---|---|---|
| `GetOrganizationsPageAsync(nameFilter, page, pageSize)` | **2**: `COUNT(*)`, then the page | every organization (no status filter). Filter: `EF.Functions.Collate(o.Name, "ru-x-icu").ToLower().Replace("ё","е").Contains(<same on text>)` — the rule `WorkItemFilterPredicate` already uses. Order `Collate(Name, ru-x-icu)`, then `Id`. `LastAppointedAt` as a correlated `Max(appointed_at)` subquery served by the index. `Skip/Take` in SQL. |
| `ListAsync(organizationId)` | **2** (1 + `ceil(ids/500)` lookups) | records newest first, then one `IUserDirectory.LookupAsync` for the distinct `appointed_by` ∪ `appointee_id`; `UserRefView.Of` names them (`inactive`/`unknown` only for old rows) |
| `FindAsync(recordId)` | 2 | U3's 201 body, read after commit |
| `OrganizationExistsAsync(organizationId)` | 1 | U4: any status |

`AppointmentView(Id, AppointedAt, UserRefView AppointedBy, UserRefView
Appointee, Reason)` lives in `Application/Abstractions/Organizations` beside
`MemberView` (the precedent for a view two slices return). The statement
counts are pinned by `SqlLogInterceptor` tests (§ 7.3), as
`MemberReadModelTests` pins its three.

## 5. Slices in detail

### U3 `AppointAdministrator` — the order is the contract's § 1.3

`unitOfWork.ExecuteInTransactionAsync`:

1. `AddressedId.TryParse` → else 404 `organization.not_found`.
2. `organizations.LockAsync(id)` → `null` → 404. Status is **not** checked
   (deactivated accepted, BR-RS-4).
3. `OrganizationMembership.IsValidUserId(userId)` → else 400
   `membership.user_invalid` (field `userId`).
4. `directory.LookupAsync([userId])` — a database read, no external I/O.
5. `memberships.LockMemberAsync(id, userId)` → the current membership or
   `null`.
6. `OrganizationAppointmentRule.Decide(...)` → 409 `rescue.user_unavailable` /
   `rescue.self_appointment` / `rescue.already_admin`, in that order.
7. `AppointmentReason.TryCreate(reason)` → 400 `validation_failed`, field
   `reason`.
8. Raise (`target.ChangeRole(Admin, actor, now)`) or add
   (`memberships.Add(OrganizationMembership.Add(id, userId, Admin, actor,
   now))`); `records.Add(OrganizationAppointment.Record(...))`;
   `memberships.SaveAsync` — a PK violation from a lockless mod-016 add that
   committed between steps 5 and 8 is mapped by the repository to
   `membership.already_member` (contract row 8) and the transaction rolls
   back.
9. After commit: `readModel.FindAsync(record.Id)` → 201.

The handler receives no organization code and asks `IOrganizationAccess`
nothing: the caller is not a member (ADR-0029 § 9).

### U1, U2, U4, U5

- **U1** validates `page` (≥ 1, else 400 `rescue.invalid_page` under
  `validation_failed`), trims `name`, calls the read model with
  `OrganizationLimits.RescueListPageSize = 25`. Response
  `RescueOrganizationsResponse(int Count, IReadOnlyList<RescueOrganizationRow>
  Data)`, `Status` mapped by hand to `"active"`/`"deactivated"`.
- **U2** = `SearchMemberCandidatesHandler` without the address:
  `SearchAsync(new UserSearch(text ?? "", IncludeInactive: false))`; its own
  response copy `{ users, moreMatched }`.
- **U4** parses the id (404), `OrganizationExistsAsync` (404), `ListAsync`.
- **U5** parses the id (the scoped check has answered), `ListAsync`.

## 6. Api (`Mustask.Api/Features/Organizations`)

- New `RescueEndpoints.cs`, `MapRescueEndpoints()`, group
  `/api/organizations/rescue`, tag `Organizations`. Each of U1–U4:
  `.RequiresPermission(PermissionCatalog.OrganizationsOrganizationRescue)`,
  `.WithForbiddenErrorCode(OrganizationErrorCodes.RescueForbidden)`,
  `.Produces…`/`.ProducesProblem(401, 403, 404 where an id is carried, 400,
  409)`. Mapped in `Program.cs` beside the organization endpoints; the literal
  `rescue` outranks `{organizationId}` (the `/deactivated` precedent).
- U5 in `MembershipEndpoints.cs`:
  `.RequiresOrganizationPermission(PermissionCatalog.OrganizationsMembershipWrite)`.
- DI: the two new abstractions in `Mustask.Infrastructure/DependencyInjection.cs`,
  the five handlers in `Mustask.Application/DependencyInjection.cs`.
- Errors: the eight codes in `contracts/errors/organizations.json` (ru as the
  contract, kk with `kkNeedsReview: true`); `docs/error-codes.md` regenerated.

**Order of work** (`stages/04-backend.md` § 4b): code + catalog + mirror →
domain + its unit tests → migration + configuration + schema tests → U3 end
to end → U1 → U2 → U4/U5 → inventories → `scripts/verify.sh`. A commit per
step, green after each.

## 7. Tests (written with the code)

### 7.1 Route-enumerating tests that must learn the new routes

| Test | Change |
|---|---|
| `Api.IntegrationTests/Features/Authorization/OrganizationAccessInventoryTests` | **New `InstanceOperationRoutes` dictionary** (key → ADR-0029 § 9 reason) holding `GET` and `POST /api/organizations/rescue/{organizationId}/appointments`; `AddressedEndpoints()` excludes exactly those. New facts: (a) the exception routes exist exactly as listed; (b) each declares category `Permission` with `organizations.organization.rescue`, the instance policy and no other, and the forbidden code `organizations.rescue.forbidden`; (c) no other route with `{organizationId}` lacks an organization code (the existing fact, now over the rest); (d) the exception list is not vacuous. `TheScopedRoutes_AreExactlyTheListedOnes` count **73 → 74** (U5). `OpenApiDocument_ListsA403_OnEveryOrganizationScopedOperation`: expected operations = `OrganizationAddressedRoutes.All.Count + InstanceOperationRoutes.Count`, every one with a 403. `EveryRoute_ForEveryRole` and `EveryRoute_ForANonMember` pick U5 up from the list. |
| `Api.IntegrationTests/Features/Organizations/OrganizationAddressedRoutes` | add `new("GET", Org + "/appointments", OrganizationsMembershipWrite)` **before** the mod-016 block — `Memberships` is "the last ten rows" (`All.Skip(All.Count - 10)`), so appending would shift it; summary comment updated. |
| `Api.IntegrationTests/Features/WorkItems/DeletedItemSurfaceMatrixTests` | `ReadRoutes` gains `["GET " + Org + "/appointments"] = Reads.NoWorkItem` (its key set must equal the GETs of `OrganizationAddressedRoutes`). |
| `Api.IntegrationTests/Features/Authorization/WorkItemWriteInventoryTests` | it scans **every** write route with `{organizationId}`, so `POST /api/organizations/rescue/{organizationId}/appointments` needs a row: `Kind.NamesNoWorkItem`, `ReasonMembership`. |
| `Api.IntegrationTests/Features/Organizations/ScopedAccessTests` | no edit: U5 is a GET, outside `WriteRoutes()`; `AllRoutes()` and the deactivated theory pick it up. |
| `Api.IntegrationTests/Features/Authorization/EndpointCategoryInventoryTests`, `PermissionClaimsTests`, `DenyAllRuntimeTests`, `ApiExplorerTests` | no edit: they derive from the endpoint data source; `PermissionClaimsTests.RealmRolesAndForeignClientRoles…` will call the four rescue routes and must see 403 (it does — the policy runs before the body). |
| `Domain.Tests/Identity/PermissionCatalogTests` | `InstanceCodes_AreExactlyTheThree…` → the four, renamed; the organization-code count (25) is unchanged. |
| `Domain.Tests/Identity/InstanceRolesTests` | new fact: `organizations.organization.rescue` is held by `system-admin` and by no other role; `SystemAdmin_HoldsEveryInstanceCode` stays. |
| `Api.IntegrationTests/Features/Authorization/KeycloakPermissionClaimsTests`, `Contracts.Tests/KeycloakStandDriftCheckTests`, `OrganizationPermissionRealmTests` | no edit: computed from `InstanceCodes` / `InstanceRoles`. |
| `src/frontend/src/shared/api/tokenClaims.test.ts` | the `system-admin` expectation gains the code (§ 2). |

Token helper: `KeycloakTestRealm.RolesFor` refuses a code set no role covers
exactly, and `system-admin` covers all instance codes — so tests build the
rescuer with `options.Roles = ["user", "system-admin"]`, through a new
`OrganizationsTokens.SystemAdmin(subject?)`.

### 7.2 Unit (`Domain.Tests`, `Application.Tests`)

- `AppointmentReasonTests`: empty, spaces only, `null` → required; 500 ASCII,
  500 emoji (1 000 UTF-16 units) → valid; 501 / 501 emoji → too long; leading
  and trailing spaces trimmed; U+0000, U+FFFE → invalid. (US-2 "The reason is
  required" table.)
- `OrganizationAppointmentRuleTests`: every row of BR-RS-6a and its pairwise
  precedences (unknown + self, self + already admin, inactive + already admin);
  member → raise, viewer → raise, non-member → add.
- `AppointAdministratorHandlerTests` (fakes): the refusal order incl. the
  contract's "Several refusals at once" table; no write on any refusal;
  `already_member` from the save rolls back and records nothing.
- `GetRescueOrganizationsHandlerTests`: page < 1 → 400; name trimmed; page
  size 25.

### 7.3 Integration (`Api.IntegrationTests`, `Infrastructure.Tests`; Testcontainers)

`Features/Organizations/`:

- `GetRescueOrganizationsEndpointTests` — US-1: three rows in name order with
  status and last appointment; the row carries only `id/name/status/
  lastAppointedAt`; the filter table (гам, ГАМ, `  бет`, а, spaces, омега),
  ё = е («Ёлка» by «елк»), `%`/`_` literal; 120 organizations → 25 + count 120,
  a filter across pages → 3 on one page; page past the last → `[]` + count;
  403 for `organization-creator` + `dictionaries-admin`; a revoked role (next
  token) → 403; 401.
- `SearchAppointeeCandidatesEndpointTests` — US-2: a signed-in user found by
  name and by e-mail; an unknown id not found; a pre-tech-025 inactive row
  hidden (planted through `UserDirectoryTestData`); answer fields exactly
  `id/displayName/email`, same for a member and a non-member; 1 character →
  empty; 101 characters → mod-014's 400; 403; 401.
- `AppointAdministratorEndpointTests` — US-2 and US-3: non-member → `admin`,
  `addedBy` = the rescuer, no team, others unchanged (snapshot of every
  membership before/after); member (`viewer` in a team) → raised, `addedBy`
  kept, `roleChangedBy` = rescuer, still in the team; an organization with
  administrators keeps them; deactivated organization stays deactivated and
  the appointee sees it in `GET /api/organizations/deactivated` and can
  restore it; the appointee's **next request** passes the scoped check with
  no new token; the rescuer still gets 404 on `GET /api/organizations/{id}`;
  repeatable (a second appointee leaves the first unchanged); refusals —
  already admin, never signed in, pre-tech-025 inactive, self (and another
  rescuer may appoint them), unknown organization, malformed id — each with
  nothing changed and no record; the reason table over HTTP (trimmed value
  stored); the five-row "Several refusals at once" table; 403 for an
  organization administrator without `system-admin`; 401; the 201 body.
- `AppointAdministratorConcurrencyTests` — BR-RS-11 with two real
  connections: same user twice → one 201 + one `already_admin`, one record;
  different users → two 201, two records; against `DELETE …/members/{userId}`
  and against `PUT …/role` (admin) in both commit orders, asserting the
  committed state each time.
- `GetAppointmentsEndpointTests` (U4 + U5) — newest first; same rows from both
  routes, reason included; `[]` when none; a refused appointment leaves none;
  the record survives the appointee's later role change and removal; U5: an
  admin reads, a member and a reader get 403, a non-member gets 404 for another
  organization; U4: unknown id 404, deactivated organization readable, 403.
- `AppointmentRecordAtomicityTests` — "No appointment without its record": a
  failing record write (a test-only `IAppointmentRecordRepository` decorator
  that throws on save, or a planted check violation) → 500, memberships
  unchanged, no record.

`Mustask.Infrastructure.Tests`:

- `MigrationsTests` gains `Migrator_AddOrganizationAppointments_CreatesTheTable_AndRollbackRemovesOnlyIt`;
  migrate twice is already covered generically.
- `OrganizationsSchemaTests`: the reason check and `varchar(500)` refuse a
  blank and a 501-code-point reason; the FK restricts deleting an organization
  with records.
- `AppointmentReadModelTests`: the list page in **2 statements** for 1 000
  organizations, within the US-1 2 s NFR on the container; the record of 100
  rows in **2 statements**; the ё/case filter at the SQL level.

### 7.4 Coverage map

Every criterion of US-1..US-3 maps to a test above; the frontend-only ones
(«Доступ ограничен», no menu entry, the dialog warning, «Отмена», focus) are
stage 6's. The coding run adds the `// US-n "<scenario>"` comment on each.

## 8. Risks

| # | Risk | Mitigation |
|---|---|---|
| R-1 | tech-025 (#55) changes before merging, e.g. `IsActive` semantics or `CodePointCount` moves | re-read the merged files before step 1; the plan touches only `IUserDirectory` and two helpers |
| R-2 | feat-048 (parallel) adds organization routes or a migration: the inventory counts (74) and the snapshot collide | rebase late; recompute the count; regenerate the migration |
| R-3 | the lockless mod-016 add race (contract row 8) answers «Пользователь уже состоит в организации» in the appointment dialog, a text the design does not list | rare (two people acting on the same user in the same instant); the design's generic error notice shows the server text; a repeat succeeds. See decision D-1 |
| R-4 | `ToLower()` under `ru-x-icu` + `Replace("ё","е")` defeats an index | 1 000 rows, a sequential scan is far inside 2 s; pinned by the volume test |
| R-5 | the inventory exception becomes a hole a later route slips through | the exception list is exact and asserted both ways, and each entry must carry the rescue code |

## 9. Decisions asked of the orchestrator

- **D-1 (contract row 8).** Accept the existing `already_member` 409 for the
  rare race with a lockless mod-016 add, or make AddMember take the organization
  lock too (touches mod-016's slice, adds contention to every add). Plan:
  accept.
- **D-2 (`reason_invalid`).** One new user-facing text, «Причина содержит
  недопустимые символы», for U+0000/noncharacters — an API-only case the
  panel cannot produce. Alternative: let U+0000 fail as a 500. Plan: the code.
- **D-3 (the panel mirror).** This stage edits `tokenClaims.ts` and its test
  (two lines) in the catalog's commit, per ADR-0033 § 2, although they sit in
  `src/frontend/`. Plan: yes.
- **D-4 (no organization id in the candidate search).** The design's
  `appointee` scope carries the organization id "for the order of refusals";
  the search has no refusal that needs it, so the route takes none. Stage 6
  drops it from the URL.

## 10. Deviations found while coding (stage 4)

None changes the contract.

- `page` is bound as a string and parsed by the handler (`int.TryParse`, digits only), so `?page=abc`, `?page=` and an out-of-range number answer the contract's 400 on `page` instead of the framework's binding error.
- `RescueOrganizationRowView` carries `IsActive` (a flag); the handler spells it as `"active"` / `"deactivated"`.
- The 120-organization volume case of § 7.3 is a unique-tag filter over 30 organizations at the API level (25 + 5 across two pages, the true count, an empty page past the end), and the 1000-organization case (two statements, within two seconds) lives in `AppointmentReadModelTests`.
- Test helper `OrganizationsTokens.AllInstanceRolesButSystemAdmin` (every other instance role, no `system-admin`) and `RescueTestData` were added.
- The foreign key from `organization_appointments` to `organizations` is `Restrict`, which PostgreSQL reports as `23001` (restrict_violation), not `23503`.
