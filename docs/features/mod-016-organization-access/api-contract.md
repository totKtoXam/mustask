# API contract — mod-016-organization-access

Fixed at stage 4a (2026-10-11). From now on it changes only with a note in
[status.md](status.md) and a Log line. Once stage 6 (frontend) has started, a
change goes to the orchestrator (04-backend.md § 4a.4). The plan behind it is
[plan-backend.md](plan-backend.md); the decision it implements is
[ADR-0027](../../adr/0027-organization-access-from-membership.md) (proposed).

It has one consumer, the panel. It covers four things: the ten new
operations (§ 1–§ 2), the behaviour added to existing operations (§ 3), what
the scoped check requires on every endpoint that exists (§ 4), and the new
error codes (§ 5). Nothing here changes mod-002's, mod-003's or mod-006's
request and response shapes except the two additive fields listed in § 3.3.

## 0. Rules shared by every endpoint of this task

### 0.1 Access: who may call, in which order the answers come

- **Authentication.** `Authorization: Bearer <access token>` of realm
  `mustask`. No token, or an invalid or expired one, gets 401
  `http.unauthorized` before anything else. While the realm is not ready the
  platform answers 503 `http.service_unavailable`.
- **Two kinds of code** (ADR-0027, doc-006 constraint 1). An **instance code**
  (`dictionaries.languages.*`, `organizations.organization.create`) comes from
  the token (`resource_access.mustask-api.roles`) and is checked as before. An
  **organization code** (every other code of § 0.2) is never in the token: it
  is derived on every request from the caller's membership in the
  organization of the address. A role change, an addition or a removal
  therefore applies on the caller's next request, with no re-login
  (doc-006 BR-M1-6).
- **The scoped check** runs on every route that has `{organizationId}` in its
  address (mod-015 BR-18), after authentication and before the request body is
  read. It answers, in this order, the first that applies:

  | # | Condition | Answer |
  |---|---|---|
  | 1 | no valid token | 401 `http.unauthorized` |
  | 2 | `organizationId` is malformed, names no organization, names one that is not active (the restore of § 4.1 row 7 accepts a deactivated one), or the caller holds no membership in it | **404** `organizations.organization.not_found` «Организация не найдена.» — the same answer, byte for byte, for every cause |
  | 3 | the caller's role bundle does not hold the route's organization code | **403** `organizations.access.forbidden` «Недостаточно прав» |
  | 4 | a nested id (project, team, area, work item) is malformed, unknown, or outside the container the path carries | 404 of the outermost level not found (mod-002 § 0.3) |
  | 5 | the request body or query is invalid | 400 |
  | 6 | the operation's own refusals | 409 and the rest |

  So «Недостаточно прав» only ever reaches a member whose role lacks the
  code, and a non-member learns nothing about the organization. A viewer who
  writes to a project that does not exist gets 403, not «Проект не найден»; a
  member who reads it gets «Проект не найден».
- **Instance administrators** (realm role `instance-admin`) hold the instance
  codes only. With no membership they get 404 on everything organization-scoped.
- **Two routes outside the check** keep their guard: `GET /api/organizations`
  and `GET /api/organizations/deactivated` are open to any signed-in user and
  narrow their answers by membership (§ 4.1 rows 1–2).
- **Dictionaries.** `dictionaries.languages.read` reaches every signed-in user
  (the realm's default roles carry it, ADR-0027); `…write` is unchanged.

### 0.2 The organization codes and what each role holds

One catalog; every code carries its scope. `A` admin, `M` member, `V` viewer.

| Code | A | M | V |
|---|---|---|---|
| `organizations.organization.read` | yes | yes | yes |
| `organizations.organization.write` (rename) | yes | no | no |
| `organizations.organization.deactivate` (deactivate, restore, the deactivated list) | yes | no | no |
| `organizations.membership.read` | yes | yes | yes |
| `organizations.membership.write` (add, change role, remove, candidate search) | yes | no | no |
| `organizations.project.read` | yes | yes | yes |
| `organizations.project.write` (create, rename) | yes | no | no |
| `organizations.project.deactivate` (deactivate, restore, the deactivated list) | yes | no | no |
| `organizations.team.read` (teams, team members) | yes | yes | yes |
| `organizations.team.write` (create, rename, default area, delete, team members, team candidate search) | yes | no | no |
| `organizations.area.read` | yes | yes | yes |
| `organizations.area.write` | yes | no | no |
| `process.definition.read` | yes | yes | yes |
| `workitems.workitem.read` | yes | yes | yes |
| `workitems.workitem.write` | yes | yes | no |
| `workitems.link.write` | yes | yes | no |
| `boards.backlog.reorder` | yes | yes | no |

Team membership grants no code. The instance codes are not in this table.

### 0.3 Identifiers and users

- Organization, project, team, area and work-item ids keep mod-002 § 0.2: a
  path or body id is an untrusted string, and anything but a canonical UUID
  (not all zeros) answers like an id that names nothing.
- A **user id** is the Keycloak `sub`: an opaque string of 1 to 255
  characters, **not** a UUID by contract. In a path it is matched as given; a
  value that names no membership, or is longer than 255 characters, answers
  the operation's "not found" code. In a body a missing, blank or over-long id
  is a 400 (`organizations.membership.user_invalid`).

### 0.4 The user reference

`UserRef`, the one shape that names a person in every list below. It is built
from the user projection (mod-014) and never calls Keycloak.

| Field | Type | Meaning |
|---|---|---|
| `id` | string | the Keycloak user id |
| `displayName` | string \| null | `null` only when `state` is `unknown` |
| `email` | string \| null | `""` for a known user with none; `null` only when `unknown` |
| `state` | string | `active`, `inactive` (disabled or deleted in Keycloak, mod-014 BR-UP-11) or `unknown` (the projection does not hold the id yet, BR-UP-17) |

The panel renders «неактивен» after the name for `inactive`, and
«Неизвестный пользователь» with an empty «Почта» for `unknown`.

### 0.5 Lists, order, volumes

- **No list here is paged.** A membership list is up to ~500 rows and is read
  whole (US-1 "Past 500 ids…": all 501 are listed). It is not a registry in
  ADR-0015's sense: no page, `pageSize`, `filter` or `orderBy` parameter, and
  the answer is a plain JSON array. (mod-002 § 0.6 expected the members to be
  the first registry; the stories do not ask for paging, sorting or filtering,
  so the plan declines it — plan-backend § 3.7.)
- **Order.** Member lists are ordered by display name under the `ru` collation
  at primary strength, then by user id in code-point order — mod-014 BR-UP-15's
  order (`DirectoryUserOrder`) — with `unknown` users last, by id.
- **Candidate searches** answer at most 20 users in mod-014's order, and
  `moreMatched` when more matched. A text under 2 characters (after mod-014's
  normalization) answers nobody; over 100 characters is mod-014's 400
  `user_directory.validation_failed` (`errors.text`:
  `user_directory.value_too_long`), passed through unchanged.
- **Large organizations.** Nothing caps memberships. Every read that looks
  ids up in the projection, and every search restricted to members, splits
  above 500 ids into batches and merges (mod-014 BR-UP-17); it never refuses.
- **No `If-Match`, no versions.** Concurrent writes are serialized by the
  server (plan-backend § 3.8); the loser of a pair gets exactly the codes
  listed for its endpoint.
- Times are ISO 8601 with offset, in UTC (`2026-10-11T09:30:00+00:00`).

### 0.6 Error body

`application/problem+json`, as everywhere (mod-002 § 0.5): `status`, `title`,
`instance`, `method`, `traceId`, `errorCode`, `detail` (Russian, from the
catalog), `params` only for a code with parameters, and `errors`
(`{ "<field>": [ { "code", "detail" } ] }`) only with a validation code. The
client branches on `errorCode`.

## 1. Access and members of an organization

Every route here starts with `/api/organizations/{organizationId}` and passes
the scoped check of § 0.1 with the code named in its heading.

### 1.1 `GET …/access` — the caller's role and codes · `organizations.organization.read`

The panel's one read of what the caller may do in this organization (BR-M1-31,
ADR-0027). It is derived from the membership at the moment of the request.

Response 200, `Cache-Control: no-store`:

```json
{
  "role": "member",
  "permissions": [
    "boards.backlog.reorder",
    "organizations.area.read",
    "…"
  ]
}
```

| Field | Type | Meaning |
|---|---|---|
| `role` | string | `admin`, `member` or `viewer` |
| `permissions` | string[] | exactly the role's bundle of § 0.2, organization codes only, in ordinal order. No instance code: those stay in the token. |

Errors: 401; 404 organization (a non-member included — the panel reads this as
"no longer a member" and re-reads the organization list).

### 1.2 `GET …/members` — the members · `organizations.membership.read`

Response 200: `Member[]`, in the order of § 0.5.

`Member`:

| Field | Type | Meaning |
|---|---|---|
| `user` | `UserRef` | the member |
| `role` | string | `admin`, `member` or `viewer` |
| `addedBy` | `UserRef` | who added the membership (the creator, for the founding one) |
| `addedAt` | time | |
| `roleChangedBy` | `UserRef` \| null | `null` while the role never changed |
| `roleChangedAt` | time \| null | |

A member whose account is disabled or deleted stays listed (`state`
`inactive`); one the projection does not know is listed `unknown`
(doc-006 BR-M1-17). Errors: 401, 404 organization.

### 1.3 `GET …/member-candidates?text=смирн` — users to add · `organizations.membership.write`

The «Добавление участника» search. It reads the projection only and works with
Keycloak stopped. Candidates are **active** projected users, whoever they
already are: an existing member is offered and refused on add (§ 1.4), as
doc-006 US-3 has it. Nothing says which other organizations a candidate is in.

Query `text`: as § 0.5; absent is `""`.

Response 200: `{ "users": [ { "id", "displayName", "email" } ], "moreMatched": false }`
— the shape of the assignee picker (mod-003 § 2.5).

Errors: 400 `user_directory.validation_failed` (a text over 100 characters);
401; 403 `organizations.access.forbidden` (member, viewer); 404 organization.

### 1.4 `POST …/members` — add a member · `organizations.membership.write`

Request:

| Field | Type | Rule |
|---|---|---|
| `userId` | string | required, 1 to 255 characters, not blank |
| `role` | string | required: `admin`, `member` or `viewer` |

Response 201, `Location: …/members`, body `Member`. The membership records the
caller as `addedBy` and the time; `roleChangedBy` and `roleChangedAt` are
`null`. It applies to the user's next request.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed`; `userId`: `organizations.membership.user_invalid`; `role`: `organizations.membership.role_invalid` | every field reason at once |
| 401, 404 | | 404: organization |
| 403 | `organizations.access.forbidden` | member, viewer |
| 409 | `organizations.membership.user_unavailable` | the projection does not hold the id (`unknown`) or the account is `inactive` (BR-M1-32). Nothing is created. |
| 409 | `organizations.membership.already_member` | the user already has a membership in this organization, or a concurrent add took it first |

`user_unavailable` is answered before `already_member`.

### 1.5 `PUT …/members/{userId}/role` — change a role · `organizations.membership.write`

Request `{ "role": string }`, the rule of § 1.4. Response 200: `Member` with
`roleChangedBy` the caller and `roleChangedAt` now. Setting the role the
member already has answers 200 and changes nothing — not even the two
"changed" fields. The caller may change their own role.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed` (`role`: `organizations.membership.role_invalid`) | |
| 401, 404 | | 404: organization |
| 403 | `organizations.access.forbidden` | member, viewer; also an administrator whose demotion committed while this request waited for the organization lock (§ 1.6, last paragraph) |
| 404 | `organizations.membership.not_found` | no membership of that user here — also one another administrator just removed |
| 409 | `organizations.membership.last_admin` | the change would leave no **known, active** administrator (BR-M1-4, BR-M1-32): only an `admin` membership whose user the projection holds as `active` counts |

### 1.6 `DELETE …/members/{userId}` — remove a member · `organizations.membership.write`

No body. Response 204. In the same transaction the user leaves every team of
the organization (BR-M1-36); work items assigned to them keep their assignee
(BR-M1-26). The caller may remove themselves; their next request answers 404
«Организация не найдена».

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization |
| 403 | `organizations.access.forbidden` | member, viewer; also an administrator demoted while this request waited, as § 1.5 |
| 404 | `organizations.membership.not_found` | as § 1.5 |
| 409 | `organizations.membership.last_admin` | as § 1.5. An **inactive** administrator is removed only while a known active one remains. |

When two administrators remove or downgrade each other at the same moment
exactly one change is saved; the other answers `last_admin` (or, if the
refused caller's own membership is already gone, as a non-member: 404
organization).

**The caller is judged again under the lock** (§ 1.5 and this section). The
scoped check of § 0.1 admits a request on the role the caller has when it
begins, outside the write's transaction; these two writes then wait for the
organization lock, and a demotion that commits meanwhile must not leave the
demoted caller able to act as an administrator (undo their own demotion,
promote, or remove). So, under the lock and in this order: the caller's
membership gone → 404 organization; the target's membership gone → 404
`organizations.membership.not_found`; the rule of BR-M1-4 for a target that is
an administrator being demoted or removed → 409 `last_admin`; the caller's
**current** role no longer holds `organizations.membership.write` → 403
`organizations.access.forbidden`, and nothing is written. The `last_admin`
answer comes first so that two administrators who demote each other keep the
answer above; a demoted caller acting on anyone else, or on a target whose
change leaves an administrator behind, hears 403.

## 2. Team members

Every route here starts with
`/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}`. The
team must be reachable (mod-002 § 0.3): a deleted team, and one of another
project or organization, answers `organizations.team.not_found`.

### 2.1 `GET …/members` — the team's members · `organizations.team.read`

Response 200: `UserRef[]` in the order of § 0.5. A team may have none. An
`inactive` or `unknown` member stays listed. Errors: 401, 404 organization,
project or team.

### 2.2 `GET …/member-candidates?text=смирн` — users to add · `organizations.team.write`

Only **active members of this organization** are offered, found by name or
e-mail in the projection (a user of another organization, a projected user in
none, and a member whose account is disabled are not). A user already in the
team is offered and refused on add. Query and response as § 1.3. Errors: 400
`user_directory.validation_failed`; 401; 403; 404 organization, project or team.

### 2.3 `POST …/members` — add to the team · `organizations.team.write`

Request `{ "userId": string }`, the rule of § 1.4. Response 201,
`Location: …/members`, body `UserRef`. The user's role is unchanged.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed` (`userId`: `organizations.membership.user_invalid`) | |
| 401, 404 | | 404: organization, project, team |
| 403 | `organizations.access.forbidden` | member, viewer |
| 409 | `organizations.team.member_not_in_organization` | the user is not a member of this organization, or is `inactive` or `unknown`, or their membership ended while the add ran |
| 409 | `organizations.team.member_exists` | already in the team, or a concurrent add took it first |

A team deleted while the add ran answers `organizations.team.not_found` (404);
no membership of a deleted team remains.

### 2.4 `DELETE …/members/{userId}` — remove from the team · `organizations.team.write`

No body. Response 204. The user stays in the organization with their role and
still reads every team.

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization, project, team |
| 403 | `organizations.access.forbidden` | member, viewer |
| 404 | `organizations.team.member_not_found` | the user is not in this team — also one another administrator just removed |

## 3. Behaviour added to existing operations

These are not new routes. Their shapes change only where § 3.3 says.

### 3.1 Creating an organization and a project

- `POST /api/organizations` (mod-002 § 1.3): the creator becomes the
  organization's `admin`, added by themselves at the creation time, in the same
  transaction as the organization and its process (BR-M1-25). The creator need
  not be in the projection yet. A failed process seed leaves no membership.
- `POST …/projects` (mod-002 § 2.3): the creating administrator becomes the only
  member of the project's first team, whatever the projection says of them
  (BR-M1-25). The rule lapses with the first team when feat-028 removes it.

### 3.2 Deleting a team

`DELETE …/teams/{teamId}` (mod-002 § 3.6): the team's memberships end in the
same transaction (BR-M1-36); the team's page and member list then answer
`organizations.team.not_found`. A member who is also in another team stays there.

### 3.3 Additive response fields

| Where | Field | Meaning |
|---|---|---|
| `Team` of `GET …/projects/{projectId}/teams` (mod-002 § 3.1) | `isMember: boolean` | the **caller** is in this team. The panel opens the project on the first team, in the list's order, with `isMember` true, else on the first team (BR-M1-27, D-5); a project with a default team opens on it first (mod-020, OQ-8 B). The single-team read § 3.2 and the write responses do not carry it. |
| `assignee` of a work item in `GET …/workitems` and `GET …/workitems/{identifier}` (mod-003 § 1), in the team's backlog and board (mod-006 § 2.3) and in the save and create answers | `isMember: boolean` | the assignee is currently a member of the item's organization (any role, BR-M1-26). The flag reads the membership alone (BR-M1-32): `false` for an ex-member and for an id that never was a member; `true` for a member the projection does not know yet (`displayName` and `email` are then `null`, § 0.4). The «неактивен» and «не в организации» marks are feat-017's (D-4). `assignee` is still `null` for «Не назначен». |

### 3.4 The assignee rule

`PATCH …/workitems/{identifier}` (mod-003 § 2.4): setting `assignee` to a user
who is not an **active member of the item's organization** — a non-member, an
`inactive` user, one the projection does not know — answers 409
`workitems.workitem.assignee_unavailable` «Этого пользователя нельзя назначить
исполнителем.» and changes nothing. An assignee who was already set and has
since left, or been disabled, stays, and saving other fields is never refused
for it (the rule applies to a **newly set** assignee only). `assignee: null`
is always allowed. `GET …/workitems/assignee-candidates` offers only active
members of the organization (§ 4.2 row 34).

## 4. The retrofit: what each existing route now requires

"Code" is the organization code of § 0.2 the scoped check (§ 0.1) holds the
caller's role to. Every row also answers 401, 404 organization and — except
rows 1–3 — 403 `organizations.access.forbidden` as § 0.1 describes; the
columns list what is new beyond that. "Org" is `/api/organizations`.

### 4.1 Organization, project, team and area routes

| # | Route | Code | Also |
|---|---|---|---|
| 1 | `GET Org` | none (signed in) | lists only the organizations the caller is a member of, in any role (the active ones) |
| 2 | `GET Org/deactivated` | none (signed in) | lists only deactivated organizations where the caller's role holds `organizations.organization.deactivate` — administrators (BR-M1-35) |
| 3 | `POST Org` | instance code `organizations.organization.create` (token) | unchanged guard and 403 `organizations.organization.create_forbidden`; the creator becomes admin (§ 3.1) |
| 4 | `GET Org/{o}` | `organizations.organization.read` | |
| 5 | `PUT Org/{o}/name` | `organizations.organization.write` | |
| 6 | `POST Org/{o}/deactivate` | `organizations.organization.deactivate` | |
| 7 | `POST Org/{o}/restore` | `organizations.organization.deactivate` | the one route that accepts a deactivated organization; a non-member still gets 404 |
| 8 | `GET Org/{o}/projects` | `organizations.project.read` | |
| 9 | `GET Org/{o}/projects/deactivated` | `organizations.project.deactivate` | |
| 10 | `POST Org/{o}/projects` | `organizations.project.write` | the creator joins the first team (§ 3.1) |
| 11 | `GET …/projects/{p}` | `organizations.project.read` | |
| 12 | `PUT …/projects/{p}/name` | `organizations.project.write` | |
| 13 | `POST …/projects/{p}/deactivate` | `organizations.project.deactivate` | |
| 14 | `POST …/projects/{p}/restore` | `organizations.project.deactivate` | |
| 15 | `GET …/projects/{p}/teams` | `organizations.team.read` | each `Team` carries `isMember` (§ 3.3) |
| 16 | `GET …/teams/{t}` | `organizations.team.read` | |
| 17 | `POST …/projects/{p}/teams` | `organizations.team.write` | |
| 18 | `PUT …/teams/{t}/name` | `organizations.team.write` | |
| 19 | `PUT …/teams/{t}/default-area` | `organizations.team.write` | an area of another project answers `organizations.area.not_found` (mod-002 BR-M1-22) |
| 20 | `DELETE …/teams/{t}` | `organizations.team.write` | ends the team's memberships (§ 3.2) |
| 21 | `GET …/projects/{p}/areas` | `organizations.area.read` | |
| 22 | `POST …/projects/{p}/areas` | `organizations.area.write` | |
| 23 | `PUT …/areas/{a}/name` | `organizations.area.write` | |
| 24 | `PUT …/areas/{a}/parent` | `organizations.area.write` | |
| 25 | `PUT …/areas/{a}/owner` | `organizations.area.write` | a team of another project answers `organizations.team.not_found` |
| 26 | `DELETE …/areas/{a}` | `organizations.area.write` | |

### 4.2 Process, work items, backlogs and boards

| # | Route | Code | Also |
|---|---|---|---|
| 27 | `GET Org/{o}/process` | `process.definition.read` | |
| 28 | `GET …/projects/{p}/workitems` | `workitems.workitem.read` | `assignee.isMember` (§ 3.3) |
| 29 | `POST …/projects/{p}/workitems` | `workitems.workitem.write` | with feat-027's `parentId`, also `workitems.link.write` (below) |
| 30 | `GET …/workitems/{identifier}` | `workitems.workitem.read` | `assignee.isMember` |
| 31 | `PATCH …/workitems/{identifier}` | `workitems.workitem.write` | a `parent` in the save also needs `workitems.link.write`; assignee per § 3.4; `assignee.isMember` |
| 32 | `PUT …/workitems/{identifier}/parent` | `workitems.link.write` | |
| 33 | `GET …/workitems/{identifier}/parent-candidates` | `workitems.workitem.read` | |
| 34 | `GET Org/{o}/workitems/assignee-candidates` | `workitems.workitem.write` | only active members of the organization (a viewer gets 403; the picker is a write control) |
| 35 | `GET …/teams/{t}/backlogs/{level}` | `workitems.workitem.read` | `assignee.isMember` on every row, in the flat read and in feat-027's tree read (the answer keeps feat-027's own JSON writer; only the guard changed) |
| 36 | `POST …/teams/{t}/backlogs/{level}/reorder` | `boards.backlog.reorder` | |
| 37 | `GET …/teams/{t}/boards/{level}` | `workitems.workitem.read` | `assignee.isMember` |

**The second code of rows 29 and 31.** A create or a save whose body carries a
parent needs `workitems.link.write` as well. The route declares
`workitems.workitem.write` — its one code — and the operation checks
`workitems.link.write` against the same role bundle **first**, before any
other answer about the parent or the item (so before `…not_found` of a nested
level and before any 400): 403 `organizations.access.forbidden`, nothing
changes (feat-001 BR-H-18, its § 0.1). Under the bundles of § 0.2 the two
codes never differ, so no answer changes today.

### 4.3 The inventory as built (pass B)

The spec's inventory (rows 1–37) was re-checked against every `*Endpoints.cs` of `Mustask.Api` at the rebase onto main `bd3bb22`
(feat-027 merged): the code holds exactly rows 1–37, and no route is missing from the table or from the code.

- **Organization-addressed routes** (a `{organizationId}` in the path): rows 4–37 — 34 routes — plus the ten of § 1–§ 2, **44** in
  all. Each declares its organization code with `.RequiresOrganizationPermission(code)` (the restore of row 7 with
  `acceptsDeactivated: true`, the only one); `.AuthenticatedOnly(…).RequiresOrganizationContext()` and mod-002's organization-context
  filter no longer exist. The published API description lists `403` on every one of them.
- **Not organization-addressed:** rows 1 and 2 (`GET /api/organizations`, `…/deactivated`, authenticated-only, narrowed by membership
  inside the read), row 3 (`POST /api/organizations`, the instance code `organizations.organization.create`), the dictionaries, the
  session, the user-directory feed, health and the OpenAPI document (§ 4.4).
- **feat-027 added no route.** It changed two: row 29 (`POST …/workitems` accepts `parentId` — the handler holds the second code
  `workitems.link.write` first, as for row 31) and row 35 (the backlog answer is a tree). Rows 36 and 37 are unchanged.
- **A line that merges after this task** (feat-028, feat-029, tech-015 or any later one) and adds a route under
  `/api/organizations/{organizationId}` declares its code or turns `scripts/verify.sh` red (BR-M1-33): the inventory test
  (`OrganizationAccessInventoryTests`) compares the real routes with `OrganizationAddressedRoutes`, code by code, and calls every
  route with every role.

### 4.4 Routes outside the retrofit, unchanged

`/api/dictionaries/languages` (GET `…read`; POST, PUT default, PUT {code}
`…write`), the four `/api/session/*` routes, the four
`/api/user-directory/feed/*` routes, health and the OpenAPI document keep
their guards. The one visible change is that `…languages.read` now reaches every
signed-in user.

## 5. Error codes

New, in `contracts/errors/organizations.json`, module `organizations`. All
without parameters. Kazakh texts are drafts (`kkNeedsReview`).

| Code | Status | ru | kk |
|---|---|---|---|
| `organizations.access.forbidden` | 403 | Недостаточно прав | Құқықтар жеткіліксіз |
| `organizations.membership.already_member` | 409 | Пользователь уже состоит в организации | Пайдаланушы ұйымның құрамында бұрыннан бар |
| `organizations.membership.last_admin` | 409 | В организации должен остаться хотя бы один администратор | Ұйымда кемінде бір әкімші қалуы керек |
| `organizations.membership.user_unavailable` | 409 | Этого пользователя нельзя добавить в организацию | Бұл пайдаланушыны ұйымға қосуға болмайды |
| `organizations.membership.not_found` | 404 | Участник не найден. | Мүше табылмады. |
| `organizations.membership.role_invalid` | 400 (field reason) | Выберите роль: администратор, участник или читатель | Рөлді таңдаңыз: әкімші, қатысушы немесе оқырман |
| `organizations.membership.user_invalid` | 400 (field reason) | Укажите пользователя | Пайдаланушыны көрсетіңіз |
| `organizations.team.member_exists` | 409 | Пользователь уже состоит в команде | Пайдаланушы командада бұрыннан бар |
| `organizations.team.member_not_in_organization` | 409 | Добавить в команду можно только активного участника организации | Командаға ұйымның тек белсенді мүшесін ғана қосуға болады |
| `organizations.team.member_not_found` | 404 | Участник команды не найден. | Команда мүшесі табылмады. |

`role_invalid` and `user_invalid` are the field reasons under
`organizations.validation_failed` that the spec's list left implicit: a
validation answer names each failed field with a registry code of its own
(backend.md, "ValidationException.Errors").

Reused, unchanged: `organizations.organization.not_found` (every non-member
answer), `organizations.project.not_found`, `organizations.team.not_found`,
`organizations.area.not_found`, `organizations.validation_failed`,
`organizations.organization.create_forbidden` (row 3 only),
`workitems.workitem.assignee_unavailable`, `user_directory.validation_failed`
and its field reasons. `organizations.access.forbidden` is the one 403 of the
scoped check, whatever module the route belongs to; `http.forbidden` stays the
platform's answer for an instance code the token lacks.

## 6. What the panel does with it (hand-off to stage 6)

- Read § 1.1 on an organization switch, on window refocus, after the caller's
  own role change succeeds, and after a 403 or 404 from any other
  organization-scoped call; a 404 «Организация не найдена» also re-reads
  `GET Org` (BR-M1-31). The navigation and the controls hold the same check
  (mod-015 BR-8, BR-9).
- The default team of a project: the first `Team` with `isMember`, else the
  first (§ 3.3, BR-M1-27); the project's own default team, where it has one,
  comes before both (mod-020, OQ-8 B). feat-028 carries the rule to
  organization-level teams.
- A refused write shows «Недостаточно прав» where the dialog shows its refusals,
  keeping what was typed (mod-002 design § 4.4).
