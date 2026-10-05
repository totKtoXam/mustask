# API contract — mod-002-organizations-projects-teams

Fixed at stage 4a (2026-10-02). From now on it changes only with a note in
[status.md](status.md) and a Log line. Once stage 6 has started, a change
goes to the orchestrator (04-backend.md § 4a.4). The plan behind it is
[plan-backend.md](plan-backend.md).

It has two consumers. The panel uses § 1–§ 6, the HTTP surface behind
[design.md](design.md) § 15. Later modules use § 7, the in-process reads.
Section 8 lists what this task registers, and § 9 what changes for
mod-001's process read.

## 0. Rules shared by every endpoint

### 0.1 Access

- **Authentication.** `Authorization: Bearer <access token>` from realm
  `mustask`. No token, or an invalid or expired one, gets 401
  `http.unauthorized`, before any other check. While the realm is not
  ready, the platform answers 503 `http.service_unavailable`.
- **Guard (interim, BR-M1-19).** Every endpoint is open to any signed-in
  user, except `POST /api/organizations`, which also needs the code
  `organizations.organization.create` in
  `resource_access.mustask-api.roles`. Nothing narrows by organization
  until mod-016. When it does, a non-member gets the same not-found answer
  as an unknown organization, never a 403.

### 0.2 Identifiers

- Every id is a UUID. Responses write it in lowercase canonical form,
  `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`.
- An id in a path or in a body is an untrusted **string**. The server
  accepts only that canonical form, in either letter case and not all
  zeros. Any other value gets the same answer as an id that names nothing,
  which is the not-found code of its level (BR-M1-20). The client sends
  ids as received and does not validate them.
- A body field that holds an id is a JSON string or `null`. A JSON number or
  object there fails binding with the platform's 400 `http.bad_request`.

### 0.3 Not found, from the outside in

A request is answered with the not-found code of the **outermost** level
that is not found. A level is not found when its id is malformed, names
nothing, names an object outside the container the path carries
(BR-M1-22), or names a deactivated or deleted object or one beneath such
an object (ADR-0026 rule 5):

| Level | `errorCode` | `detail` |
|---|---|---|
| organization | `organizations.organization.not_found` | «Организация не найдена.» |
| project | `organizations.project.not_found` | «Проект не найден.» |
| team | `organizations.team.not_found` | «Команда не найдена.» |
| area | `organizations.area.not_found` | «Область не найдена.» |

A deactivated organization or project is never revealed as such through an
addressed request. The only exception is `…/restore` (§ 1.7, § 2.7), which
addresses a deactivated object directly. A team or an area named in a
**body** is answered the same way (§ 0.4 step 4): a deleted team, or a team
or area of another project, is `team.not_found` or `area.not_found`.
`area.not_found` has no page frame: the panel shows it in the dialog's
`Alert` (design § 4.4, "anything else").

### 0.4 The order of answers

A request with several faults gets the first of:

1. the not-found of § 0.3 for the path;
2. a rule about the addressed object alone (the root area, the first team);
3. 400 `organizations.validation_failed`, with **every** field reason at
   once;
4. the not-found of a body reference (the owner team before the area);
5. a 409.

The error tables below group the codes by status. This list, not the row
order of a table, decides between them.

### 0.5 Error body

`application/problem+json`, as everywhere in the API:

| Field | Meaning |
|---|---|
| `status`, `title`, `instance`, `method`, `traceId` | the platform's |
| `errorCode` | the code. The client branches on it, never on the text or the status alone. |
| `detail` | the Russian text from the catalog (§ 6) |
| `params` | present only for a code with parameters: `{ "<name>": { "value": …, "type": "string" } }` |
| `errors` | only with `organizations.validation_failed`: `{ "<field>": [ { "code", "detail" } ] }`. The fields are `name` and `key`. |

### 0.6 Lists, order, versions

- **No list here is a registry.** NFR Volumes bounds them to tens of
  organizations per instance, tens of projects per organization and tens
  of teams per project, and design § 5.2 and § 7.2 show no paging and no
  filter. So each returns a plain JSON array of everything it holds, with
  no `page`, `pageSize`, `filter` or `orderBy` parameter. ADR-0015's
  `Paging<T>` (`{ count, data }`) applies to the first list that becomes a
  registry (mod-016's members).
- **The server orders every list, and the client renders it in the order
  received.** "Display order" below is by name in Russian collation,
  letters before letter case, «Ё» with «Е» (design § 4.6), then by id.
- **No `If-Match`, no versions.** Concurrent writes are serialized by the
  server (plan § 8). A rename is last-writer-wins, and a business conflict
  answers its 409 code. The loser of a concurrent pair gets exactly the
  codes listed for its endpoint.
- **Names** are trimmed by the server and stored trimmed. Comparisons ignore
  letter case and surrounding spaces, and a name never conflicts with the
  object's own current name (BR-M1-1).

## 1. Organizations

### 1.1 `GET /api/organizations` — the active organizations

Response 200: `OrganizationListItem[]` in display order.

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | |
| `name` | string | |

Errors: 401.

### 1.2 `GET /api/organizations/deactivated` — the deactivated organizations

Response 200: `OrganizationListItem[]` (§ 1.1), the deactivated ones only,
in display order. Errors: 401.

### 1.3 `POST /api/organizations` — create (US-1)

Needs `organizations.organization.create` (§ 0.1).

Request:

| Field | Type | Rule |
|---|---|---|
| `name` | string | 1–100 characters after trimming; unique instance-wide among all organizations, deactivated ones included |

Response 201, `Location: /api/organizations/{id}`, body `Organization`:

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | |
| `name` | string | as stored, trimmed |

The organization is active and has its process. mod-001's seed ran in the
same transaction, and the creator and the time are recorded (BR-M1-3,
BR-M1-21). The creator record is not served over HTTP (§ 7).

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed`, field `name`: `organizations.name_length` | the name is null, empty, spaces only or over 100 characters after trimming |
| 401 | `http.unauthorized` | § 0.1 |
| 403 | `organizations.organization.create_forbidden` | the token lacks the create right. Never `http.forbidden`. |
| 409 | `organizations.organization.name_taken`, `params.name` | an organization of any status has the name. `name` is that organization's stored name («пример организации » → «Название «Пример организации» уже занято»). The answer is the same for an active and a deactivated holder. When a concurrent creation took the name first, `name` is the submitted name, trimmed. |
| 500 | `organizations.organization.create_failed` | the process seed failed. Nothing is created, and the name stays free. |

### 1.4 `GET /api/organizations/{organizationId}` — one organization

Response 200: `Organization` (§ 1.3). Errors: 401, 404 organization.

### 1.5 `PUT /api/organizations/{organizationId}/name` — rename (US-2)

Request `{ "name": string }`, the rule of § 1.3. Response 200:
`Organization`. Renaming to the current name in another letter case is
allowed.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed` (`name`: `organizations.name_length`) | |
| 401, 404 | as § 0 | 404: organization |
| 409 | `organizations.organization.name_taken`, `params.name` | as § 1.3 |

### 1.6 `POST /api/organizations/{organizationId}/deactivate` — deactivate (US-2)

No body. Response 204. The organization leaves § 1.1 and joins § 1.2. From
now on every request under it answers `organization.not_found`, except
§ 1.7. Nothing beneath it changes (BR-M1-20). Errors: 401, 404
organization (already deactivated included).

### 1.7 `POST /api/organizations/{organizationId}/restore` — restore (US-2)

No body. Response 200: `Organization`. It is the one request a deactivated
organization accepts. Everything beneath comes back as it was, and a
project deactivated on its own stays deactivated (BR-M1-20). Restoring an
active organization changes nothing and answers 200. Errors: 401, 404
organization (malformed or unknown id).

## 2. Projects

Every route here starts with `/api/organizations/{organizationId}` and
answers 404 `organization.not_found` per § 0.3 before anything else.

### 2.1 `GET …/projects` — the organization's active projects

Response 200: `ProjectListItem[]` in display order.

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | |
| `name` | string | |
| `key` | string | 2–10 Latin capital letters and digits |

Errors: 401, 404 organization.

### 2.2 `GET …/projects/deactivated` — its deactivated projects

Response 200: `ProjectListItem[]`, the deactivated ones only, in display
order. Errors: 401, 404 organization.

### 2.3 `POST …/projects` — create (US-3)

Request:

| Field | Type | Rule |
|---|---|---|
| `name` | string | 1–100 after trimming; unique in the organization among all projects, deactivated ones included |
| `key` | string | required; `^[A-Z][A-Z0-9]{1,9}$` as sent (not trimmed, not upper-cased); unique in the organization among all projects; never changes |

Response 201, `Location: …/projects/{id}`, body `Project`:

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | |
| `organizationId` | uuid | |
| `name` | string | |
| `key` | string | |

The project comes with its first team, named after it (§ 3), and its root
area, also named after it, owned by that team for good and set as its
default area (§ 4). It uses the organization's process. Its creator and
the time are recorded.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed`; `name`: `organizations.name_length`; `key`: `organizations.project.key_required` or `organizations.project.key_invalid` | `key_required` for a null or empty key; `key_invalid` otherwise (`prm`, `1PRM`, `P`, `PROJECTKEY01`, `РМК`) |
| 401, 404 | | 404: organization |
| 409 | `organizations.project.name_taken` | an active project has the name, or a concurrent creation took it first |
| 409 | `organizations.project.name_taken_by_deactivated` | a deactivated project has the name |
| 409 | `organizations.project.key_taken`, `params.key` | a project of any status has the key |

When both the name and the key are taken, the name's code is answered.

### 2.4 `GET …/projects/{projectId}` — one project

Response 200: `Project` (§ 2.3). Errors: 401, 404 organization or project.

### 2.5 `PUT …/projects/{projectId}/name` — rename (US-3)

Request `{ "name": string }`. Response 200: `Project`. The root area takes
the same name (BR-M1-11). The key, the teams and the rest of the tree do
not change. No request changes a key.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed` (`name`) | |
| 401, 404 | | 404: organization, project |
| 409 | `organizations.project.name_taken`, `organizations.project.name_taken_by_deactivated` | as § 2.3 |

### 2.6 `POST …/projects/{projectId}/deactivate` — deactivate (US-3)

No body. Response 204. The project leaves § 2.1 and joins § 2.2.
Everything under it answers `project.not_found` until it is restored.
Errors: 401, 404 organization or project (already deactivated included).

### 2.7 `POST …/projects/{projectId}/restore` — restore (US-3)

No body. Response 200: `Project`. It accepts a deactivated project of an
active organization. Under a deactivated organization the answer is
`organization.not_found` (ADR-0026 rule 5). Restoring an active project
changes nothing and answers 200. Errors: 401, 404 organization or project.

## 3. Teams

Every route here starts with
`/api/organizations/{organizationId}/projects/{projectId}`. The project must
be reachable (§ 0.3).

`Team`, the one shape every team response uses:

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | |
| `projectId` | uuid | |
| `name` | string | |
| `isFirstTeam` | boolean | the team that owns the root. It cannot be deleted, and its default area is the root for good. |
| `defaultArea` | object | `{ "id": uuid, "path": string }`. `path` is the names below the root joined by « / », and the root's own name for the root (DC-9). Always present. |

### 3.1 `GET …/teams` — the project's active teams

Response 200: `Team[]`. The first team comes first, then the rest in
display order. Deleted teams are left out. Errors: 401, 404 organization
or project.

### 3.2 `GET …/teams/{teamId}` — one team

Response 200: `Team`. Errors: 401, 404 organization, project or team
(deleted included).

### 3.3 `POST …/teams` — add a team (US-4)

Request `{ "name": string }`: 1–100 after trimming, unique among the
project's **active** teams. Response 201, `Location: …/teams/{id}`, body
`Team`. The root gains a child area with the same name, owned explicitly by
the new team and set as its default (BR-M1-9).

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed` (`name`) | |
| 401, 404 | | 404: organization, project |
| 409 | `organizations.team.name_taken` | an active team of the project has the name |
| 409 | `organizations.area.name_taken` | the root already has a child area of that name, a deleted team's included. No team and no area is created. |

### 3.4 `PUT …/teams/{teamId}/name` — rename (US-4)

Request `{ "name": string }`. Response 200: `Team`. The team's areas keep
their names.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed` (`name`) | |
| 401, 404 | | 404: organization, project, team |
| 409 | `organizations.team.name_taken` | |

### 3.5 `PUT …/teams/{teamId}/default-area` — set the default area (US-4)

Request:

| Field | Type | Rule |
|---|---|---|
| `areaId` | string (uuid) | an area of the same project that the team owns **explicitly** |

Response 200: `Team`. Setting the current default again answers 200 and
changes nothing.

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization, project, team; then `area.not_found` for an `areaId` that is null, malformed, unknown or of another project |
| 409 | `organizations.team.first_team_default_fixed` | the team is the project's first team |
| 409 | `organizations.team.default_area_not_owned` | the area's owner is inherited or another team |

### 3.6 `DELETE …/teams/{teamId}` — delete a team (US-7)

No body. Response 204. In one operation, every area the team owns
explicitly passes to the first team, and its default area with them. The
team leaves § 3.1 and every picker, and its name is free in the project.
From now on, every request that addresses or names the team answers
`team.not_found`. There is no restore.

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization, project, team (already deleted included) |
| 409 | `organizations.team.first_team_cannot_be_deleted` | |

## 4. Areas

Every route here starts with
`/api/organizations/{organizationId}/projects/{projectId}`. Areas have no
status: a deleted area is gone.

### 4.1 `GET …/areas` — the project's area tree (US-5, US-6)

Response 200: `AreaNode[]`, the whole tree in **pre-order** (each node
before its children, siblings in display order), the root first.

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | |
| `parentId` | uuid or `null` | `null` for the root only |
| `name` | string | |
| `path` | string | DC-9, as in `Team.defaultArea.path` |
| `depth` | integer | 0 for the root |
| `ownerTeamId` | uuid or `null` | the **explicit** owner. `null` means the owner is inherited. The root's is never `null`. |
| `resolvedOwner` | object | `{ "id": uuid, "name": string }`: the nearest explicit owner up the tree, at the latest the root's (the first team). Every node has exactly one. |
| `ownerInherited` | boolean | `true` when `ownerTeamId` is `null` |
| `defaultOfTeam` | object or `null` | `{ "id": uuid, "name": string }`: the team whose default area this is |

The default-area picker offers the nodes whose `ownerTeamId` is the team's
id (US-4). The move picker leaves out the node, its subtree and its parent
(design § 11.5). Errors: 401, 404 organization or project.

`Area`, the shape every area write returns:

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | |
| `projectId` | uuid | |
| `parentId` | uuid | |
| `name` | string | |
| `ownerTeamId` | uuid or `null` | the explicit owner |

The panel refetches § 4.1 after a write, because the change can move the
resolved owners of a whole subtree.

### 4.2 `POST …/areas` — create a child area (US-5, US-6)

Request:

| Field | Type | Rule |
|---|---|---|
| `parentId` | string (uuid) | required; an area of this project. «Новая область» sends the root's id. |
| `name` | string | 1–100 after trimming; unique among the parent's children |
| `ownerTeamId` | string (uuid) or `null` | an active team of this project; `null` or absent: inherit from the parent |

Response 201, `Location: …/areas/{id}`, body `Area`.

| Status | `errorCode` | When |
|---|---|---|
| 400 | `organizations.validation_failed` (`name`) | |
| 401, 404 | | 404: organization, project; then `team.not_found` for `ownerTeamId` (deleted, unknown, malformed, another project's); then `area.not_found` for `parentId` (null, malformed, unknown, another project's) |
| 409 | `organizations.area.name_taken` | a sibling has the name |

### 4.3 `PUT …/areas/{areaId}/name` — rename (US-5)

Request `{ "name": string }`. Response 200: `Area`. Children and owner do
not change.

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization, project, area |
| 409 | `organizations.area.root_is_fixed` | the area is the root (it is renamed only with its project, § 2.5) |
| 400 | `organizations.validation_failed` (`name`) | |
| 409 | `organizations.area.name_taken` | |

### 4.4 `PUT …/areas/{areaId}/parent` — move (US-5, US-6)

Request:

| Field | Type | Rule |
|---|---|---|
| `parentId` | string (uuid) | an area of this project, outside the moved node's subtree |

Response 200: `Area`. The node moves with its whole subtree. Explicit owners
stay, and inherited ones re-resolve. Moving under the current parent
answers 200 and changes nothing.

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization, project, area; then `area.not_found` for `parentId` |
| 409 | `organizations.area.root_is_fixed` | the moved area is the root |
| 409 | `organizations.area.move_into_own_subtree` | `parentId` is the node itself or one of its descendants |
| 409 | `organizations.area.name_taken` | the new parent already has a child of that name |

### 4.5 `PUT …/areas/{areaId}/owner` — set or clear the owner (US-6)

Request:

| Field | Type | Rule |
|---|---|---|
| `ownerTeamId` | string (uuid) or `null` | an active team of this project; `null` or absent clears the explicit owner, so the area inherits |

Response 200: `Area`. Setting the current owner again answers 200 and
changes nothing.

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization, project, area; then `team.not_found` for `ownerTeamId` |
| 409 | `organizations.area.root_is_fixed` | the area is the root |
| 409 | `organizations.area.is_team_default`, `params.team` | the area is its owner's default area, and the request gives it another owner or none. `team` is that team's name. |

### 4.6 `DELETE …/areas/{areaId}` — delete a leaf (US-5, US-6)

No body. Response 204. There is no restore.

| Status | `errorCode` | When |
|---|---|---|
| 401, 404 | | 404: organization, project, area |
| 409 | `organizations.area.root_is_fixed` | the area is the root |
| 409 | `organizations.area.not_empty` | the area has children. From mod-003, also work items in it, «Удалена» included (BR-M1-13); the text already says so. |
| 409 | `organizations.area.is_team_default`, `params.team` | the area is a team's default area |

## 5. Summary of routes

All of them are under `/api/organizations`, tagged `Organizations` in
`/openapi/v1.json`. No other operation of this module exists. In
particular, none deletes or purges an organization or a project, restores a
team or an area, changes a key, or writes rights, roles or groups.

| Method | Route | § | Category | Organization filter |
|---|---|---|---|---|
| GET | `/api/organizations` | 1.1 | authenticated | — |
| GET | `/api/organizations/deactivated` | 1.2 | authenticated | — |
| POST | `/api/organizations` | 1.3 | `organizations.organization.create` | — |
| GET | `/api/organizations/{organizationId}` | 1.4 | authenticated | active |
| PUT | `/api/organizations/{organizationId}/name` | 1.5 | authenticated | active |
| POST | `/api/organizations/{organizationId}/deactivate` | 1.6 | authenticated | active |
| POST | `/api/organizations/{organizationId}/restore` | 1.7 | authenticated | active or deactivated |
| GET | `…/projects` | 2.1 | authenticated | active |
| GET | `…/projects/deactivated` | 2.2 | authenticated | active |
| POST | `…/projects` | 2.3 | authenticated | active |
| GET | `…/projects/{projectId}` | 2.4 | authenticated | active |
| PUT | `…/projects/{projectId}/name` | 2.5 | authenticated | active |
| POST | `…/projects/{projectId}/deactivate` | 2.6 | authenticated | active |
| POST | `…/projects/{projectId}/restore` | 2.7 | authenticated | active |
| GET | `…/projects/{projectId}/teams` | 3.1 | authenticated | active |
| GET | `…/projects/{projectId}/teams/{teamId}` | 3.2 | authenticated | active |
| POST | `…/projects/{projectId}/teams` | 3.3 | authenticated | active |
| PUT | `…/projects/{projectId}/teams/{teamId}/name` | 3.4 | authenticated | active |
| PUT | `…/projects/{projectId}/teams/{teamId}/default-area` | 3.5 | authenticated | active |
| DELETE | `…/projects/{projectId}/teams/{teamId}` | 3.6 | authenticated | active |
| GET | `…/projects/{projectId}/areas` | 4.1 | authenticated | active |
| POST | `…/projects/{projectId}/areas` | 4.2 | authenticated | active |
| PUT | `…/projects/{projectId}/areas/{areaId}/name` | 4.3 | authenticated | active |
| PUT | `…/projects/{projectId}/areas/{areaId}/parent` | 4.4 | authenticated | active |
| PUT | `…/projects/{projectId}/areas/{areaId}/owner` | 4.5 | authenticated | active |
| DELETE | `…/projects/{projectId}/areas/{areaId}` | 4.6 | authenticated | active |

`…` is `/api/organizations/{organizationId}`. The filter also covers
mod-001's `GET /api/organizations/{organizationId}/process` (§ 9).

## 6. Error codes

All of them are in `contracts/errors/organizations.json` (module
`organizations`, origin `slice`), a file this module owns from now on (D-k),
and in `Mustask.Domain/Organizations/OrganizationErrorCodes.cs`. The
Russian texts are final and are copied from the stories and design § 4.4
where those quote them. The four marked † are not quoted there (plan P-2).
Every Kazakh text carries `kkNeedsReview: true`. The panel keeps the codes
as constants in `shared/api`, one to one with this table.

| Code | Status | Params | ru | kk |
|---|---|---|---|---|
| `organizations.organization.not_found` | 404 | — | «Организация не найдена.» (mod-001's, unchanged) | «Ұйым табылмады.» |
| `organizations.project.not_found` | 404 | — | «Проект не найден.» | «Жоба табылмады.» |
| `organizations.team.not_found` | 404 | — | «Команда не найдена.» | «Команда табылмады.» |
| `organizations.area.not_found` | 404 | — | «Область не найдена.» † | «Аймақ табылмады.» |
| `organizations.validation_failed` | 400 | — | «Проверьте правильность заполнения полей.» † | «Өрістердің дұрыс толтырылғанын тексеріңіз.» |
| `organizations.name_length` | 400 (field reason) | — | «Название должно содержать от 1 до 100 символов» | «Атауы 1-ден 100 таңбаға дейін болуы керек» |
| `organizations.organization.create_forbidden` | 403 | — | «Недостаточно прав» | «Құқықтар жеткіліксіз» |
| `organizations.organization.create_failed` | 500 | — | «Не удалось создать организацию. Попробуйте ещё раз» | «Ұйымды құру мүмкін болмады. Қайталап көріңіз» |
| `organizations.organization.name_taken` | 409 | `name`: string | «Название «{name}» уже занято» | ««{name}» атауы бос емес» |
| `organizations.project.name_taken` | 409 | — | «Проект с таким названием уже есть в организации» | «Ұйымда мұндай атауы бар жоба бар» |
| `organizations.project.name_taken_by_deactivated` | 409 | — | «Проект с таким названием уже есть в организации: он деактивирован» | «Ұйымда мұндай атауы бар жоба бар: ол сөндірілген» |
| `organizations.project.key_required` | 400 (field reason) | — | «Укажите ключ проекта» | «Жоба кілтін көрсетіңіз» |
| `organizations.project.key_invalid` | 400 (field reason) | — | «Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы» | «Жоба кілті: әріптен басталатын 2-ден 10-ға дейін латын бас әріптері мен цифрлар» |
| `organizations.project.key_taken` | 409 | `key`: string | «Ключ «{key}» уже используется в организации» | ««{key}» кілті ұйымда қолданылып жүр» |
| `organizations.team.name_taken` | 409 | — | «Команда с таким названием уже есть в проекте» | «Жобада мұндай атауы бар команда бар» |
| `organizations.team.default_area_not_owned` | 409 | — | «Область по умолчанию должна принадлежать самой команде» | «Әдепкі аймақ команданың өзіне тиесілі болуы керек» |
| `organizations.team.first_team_default_fixed` | 409 | — | «У первой команды область по умолчанию не меняется» † | «Бірінші команданың әдепкі аймағы өзгермейді» |
| `organizations.team.first_team_cannot_be_deleted` | 409 | — | «Нельзя удалить команду, которой принадлежит корневая область проекта» | «Жобаның түбірлік аймағы тиесілі команданы жоюға болмайды» |
| `organizations.area.name_taken` | 409 | — | «Область с таким названием уже есть на этом уровне» | «Бұл деңгейде мұндай атауы бар аймақ бар» |
| `organizations.area.move_into_own_subtree` | 409 | — | «Нельзя переместить область внутрь неё самой или её вложенной области» | «Аймақты өзінің ішіне немесе оның ішкі аймағына жылжытуға болмайды» |
| `organizations.area.is_team_default` | 409 | `team`: string | «Область используется как область по умолчанию команды «{team}»» | «Аймақ «{team}» командасының әдепкі аймағы ретінде пайдаланылады» |
| `organizations.area.not_empty` | 409 | — | «Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области» | «Жұмыс элементтері (жойылғандарын қоса) немесе ішкі аймақтары бар аймақты жоюға болмайды» |
| `organizations.area.root_is_fixed` | 409 | — | «Корневая область меняется только вместе с проектом» † | «Түбірлік аймақ тек жобамен бірге өзгереді» |

Where the panel shows each code, field or dialog `Alert`, is design § 4.4.
The three not-found codes of the organization, project and team levels
pick the page's not-found frame (design § 12).

## 7. In-process reads for other modules

Not HTTP. Namespace `Mustask.Application.Features.Organizations`, at the
module root, so a slice of another module depends on it without
referencing a slice of this one. It is registered scoped: resolve it in
the request's scope, because it uses the request's database context. It
only reads, and it publishes no event (ADR-0026 rule 7). A module that
must act on a removal reads the status in its own write path.

```csharp
namespace Mustask.Application.Features.Organizations;

public interface IOrganizationStructure
{
    Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<ProjectInfo?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<TeamInfo?> FindTeamAsync(Guid teamId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TeamInfo>> ListActiveTeamsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<AreaOwnerInfo?> FindAreaOwnerAsync(Guid areaId, CancellationToken cancellationToken);
}

public sealed record OrganizationInfo(
    Guid Id, string Name, OrganizationStatus Status, ProcessId ProcessId,
    string CreatedBy, DateTimeOffset CreatedAt);

public sealed record ProjectInfo(
    Guid Id, Guid OrganizationId, string Name, string Key, ProjectStatus Status,
    bool IsReachable, Guid FirstTeamId, Guid RootAreaId,
    string CreatedBy, DateTimeOffset CreatedAt);

public sealed record TeamInfo(
    Guid Id, Guid ProjectId, Guid OrganizationId, string Name, TeamStatus Status,
    bool IsFirstTeam, Guid? DefaultAreaId, bool IsReachable);

public sealed record AreaOwnerInfo(
    Guid AreaId, Guid ProjectId, Guid OwningTeamId, bool IsInherited, bool IsReachable);
```

The statuses are `Mustask.Domain.Organizations` enums, and `ProcessId` is
mod-001's `Mustask.Domain.Process.ProcessId`.

- **Every by-id read returns the object with its status, reachable or
  not** (ADR-0026 rule 4). `null` means only that no such object ever
  existed. `IsReachable` is `true` when the object and every container
  above it are active.
- **A deleted team** answers `Status = Deleted` and `DefaultAreaId = null`,
  never `null` and never `Active` (US-7, doc-010 PR-9). `ListActiveTeamsAsync`
  leaves it out and orders as § 3.1.
- **The area with no team context** is `ProjectInfo.RootAreaId`. A team's
  default area is `TeamInfo.DefaultAreaId` (BR-M1-14).
- **`FindAreaOwnerAsync`** resolves exactly as § 4.1's `resolvedOwner` and
  `ownerInherited` do, through the same domain code (US-6). `null` means
  the area does not exist: a deleted area is gone.
- **`CreatedBy`** is the creator's Keycloak subject (`sub`), the id
  mod-014's directory uses. mod-016 reads it to make the creator the first
  administrator (BR-M1-21).
- **None of these checks access.** The caller applies its own.

## 8. Registrations

| What | Value | Where |
|---|---|---|
| Permission code | `organizations.organization.create`, instance-level | `Mustask.Domain/Identity/PermissionCatalog.cs` |
| Keycloak client role | `organizations.organization.create` on `mustask-api` | `deploy/keycloak/realms/mustask-realm.json` |
| Keycloak realm role | `organization-creator`, a composite of that one code | same file |
| Keycloak group | «Создатели организаций», mapping `organization-creator` | same file |
| Realm role `admin` | gains the code | same file |
| Error codes | § 6 | `contracts/errors/organizations.json`, `OrganizationErrorCodes.cs`, `docs/error-codes.md` (generated) |

A grant or a revocation is made in the Keycloak console, by adding a user
to the group or removing them. It takes effect when the user's token is
renewed (BR-M1-2). A stand needs a reset to pick up the import change (D-j,
plan § 9). The panel offers the create action when `can('organizations.organization.create')`
holds. No organization-scoped code is registered (C-2).

## 9. What changes for mod-001

- **`GET /api/organizations/{organizationId}/process`** now runs behind
  the organization filter. Its `organizations.organization.not_found`
  answer (mod-001 contract § 1.3) now also covers an organization that
  does not exist and a deactivated one, and an organization that has a
  process but no organization row (the old demonstration id). Its shape
  and its other answers do not change. After restore it answers again.
- **The Development demonstration seed is gone** (D-h): no fixed
  organization id, and no `DemonstrationSeed*` log events. On a stand,
  the administrator creates an organization in the panel.
- **The seed seam is called as mod-001's contract § 2 requires:** inside
  `ExecuteInTransactionAsync`, before this module tracks a change of its
  own, with the new organization's id, and its exception fails the
  creation as a whole (§ 1.3, 500).
