# API contract — mod-003-work-item-core

Fixed at stage 4a (2026-10-05), before the code. From now on it changes only
with a note in [status.md](status.md) and a Log line; once stage 6 has
started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan
is the `## Plan` section of [status.md](status.md).

The panel uses § 1–§ 5. § 6 is what changes in mod-002's area deletion, and
§ 7 the in-process port behind it.

## 0. Rules shared by every endpoint

### 0.1 Access

- **Authentication.** `Authorization: Bearer <access token>` from realm
  `mustask`. No token, or an invalid or expired one, gets 401
  `http.unauthorized` before any other check.
- **Guard (interim, D-3, BR-M3-27).** Every endpoint is open to any
  signed-in user, exactly as mod-002's (its api-contract § 0.1). Nothing
  narrows by membership until mod-016.
- **Permission codes.** The reads declare `workitems.workitem.read`, the
  writes `workitems.workitem.write`. Both are organization-scoped codes
  (`OrganizationPermissionCatalog`): no token carries them, the realm import
  does not mention them, and nothing checks them until mod-016, as mod-001's
  `process.definition.read`.
- **Organization context.** Every route has an `organizationId` and runs
  behind mod-002's organization-context filter (its D-g): a malformed,
  unknown or deactivated organization answers 404
  `organizations.organization.not_found` before anything else is read.

### 0.2 Identifiers

- Every id is a UUID, as in mod-002 api-contract § 0.2: responses write it
  in lowercase canonical form; an id in a path or a body is an untrusted
  string, accepted only in the canonical 8-4-4-4-12 form in either letter
  case and not all zeros, and anything else answers like an id that names
  nothing.
- A user id (`assignee.id`, `assignee.userId`, `createdBy`) is the Keycloak
  user id, the `sub` of that user's tokens: opaque text, never parsed.
- **The work item identifier** `<project key>-<type key>-<number>`, e.g.
  `MSK-BUG-42` (spec § The identifier and its number). It is well-formed only
  in exactly this form: a project key (`^[A-Z][A-Z0-9]{1,9}$`), a type key
  (Latin capitals, `EPIC`, `FEAT`, `US`, `BUG` or `TASK` in the seeded
  process), and a number of 1 or more in decimal without leading zeros that
  fits a 32-bit signed integer. `msk-bug-42`, `MSK-BUG-042`, `MSK-BUG-0` are
  malformed. In a path it is used as is; it needs no escaping.

### 0.3 Not found, from the outside in

A request is answered with the not-found code of the outermost level that is
not found:

| Level | When | `errorCode` | `detail` |
|---|---|---|---|
| organization | the id is malformed, names nothing, or names a deactivated organization | `organizations.organization.not_found` | «Организация не найдена.» |
| project | the id is malformed, names nothing, names a project of another organization, or a deactivated project | `organizations.project.not_found` | «Проект не найден.» |
| work item | the identifier is malformed, its project key is not the addressed project's key, the project has no item with that number, or that item's type key differs from the identifier's (D-5) | `workitems.workitem.not_found` | «Рабочий элемент не найден.» |

### 0.4 The order of answers

A request with several faults gets the first of:

1. the not-found of § 0.3 for the path;
2. 400 `workitems.validation_failed`, with **every** field reason at once;
3. the not-found of a body reference: the type on create
   (`workitems.workitem.type_not_found`), the area on save
   (`organizations.area.not_found`);
4. a 409: the state (`workitems.workitem.state_not_allowed`), then the
   assignee (`workitems.workitem.assignee_unavailable`).

### 0.5 Error body

`application/problem+json`, as everywhere in the API (mod-002 api-contract
§ 0.5): `errorCode` is the code the client branches on, `detail` the Russian
text from the catalog (§ 4), and `errors` is present only with
`workitems.validation_failed`: `{ "<field>": [ { "code", "detail" } ] }`.
The fields are `page`, `requestId`, `title` and `description`, and a field carries
one reason.

**Where the panel shows a refusal** (spec § Error codes): a not-found of the
path replaces the page with its frame, except in the open create dialog,
where it is the dialog's `Alert`. `workitems.workitem.type_not_found` (404)
is the dialog's `Alert`, `organizations.area.not_found` (404) on a save is
the error under «Область», and the two 409s are the errors under «Статус»
and «Исполнитель»: these three 404/409 answers to a body never mean the page
is gone.

### 0.6 Values and time

- **Заголовок** (`title`): trimmed by the server (every Unicode white space
  at either end), then checked: empty → `workitems.workitem.title_required`;
  more than 255 UTF-16 code units → `workitems.workitem.title_too_long`; a
  line break anywhere in it (`\n`, `\r`, `\v`, `\f`, U+0085, U+2028, U+2029) →
  `workitems.workitem.title_multiline`. One reason per field, in this order.
  Stored trimmed (BR-M3-3).
- **Описание** (`description`): plain text, stored exactly as sent, line
  breaks and markup characters kept; more than 100 000 UTF-16 code units →
  `workitems.workitem.description_too_long` (BR-M3-19). An empty description
  is `""`, never `null`.
- **Time** is UTC, ISO 8601 with its offset (`2026-10-05T14:30:00.123456+00:00`;
  the fraction has up to six digits and may be shorter or absent). The panel
  formats it as «05.10.2026 14:30» in the user's time zone.

## 1. Shapes

### 1.1 `WorkItem` — the form's item (create, read, save)

```json
{
  "id": "0199b0a2-…",
  "identifier": "MSK-BUG-42",
  "number": 42,
  "projectId": "0199b09f-…",
  "type": { "id": "…", "key": "BUG", "name": "Баг", "appearance": "bug" },
  "title": "Падение при входе",
  "description": "",
  "state": {
    "id": "…",
    "name": "Новая",
    "category": { "code": "Proposed", "label": "Новые" }
  },
  "assignee": null,
  "area": { "id": "…", "path": "Платформа / API" },
  "createdBy": "5d1e8f3a-…",
  "createdAt": "2026-10-05T14:30:00.123456+00:00",
  "changedAt": "2026-10-05T14:30:00.123456+00:00"
}
```

| Field | Meaning |
|---|---|
| `type` | the item's type, never changed (BR-M3-1): its id, key, Russian name and appearance token (`epic`, `feature`, `story`, `task`, `bug`), as mod-001's process read gives them |
| `state` | the current state: its id, Russian name and category, as mod-001's process read gives them (`category.code` is never shown; `category.label` is) |
| `assignee` | `null` for «Не назначен»; otherwise `{ "id", "displayName", "email" }` — `displayName` and `email` are both `null` when the user directory does not know the id, which the panel reads «Неизвестный пользователь»; `email` is `""` for a known user without one. No mark for an inactive assignee (D-2) |
| `area` | the item's area with its current path (mod-002 DC-9: «Платформа / API», the root by its own name) |
| `createdBy`, `createdAt` | the creator and the creation time (BR-M3-28); not shown in this task |
| `changedAt` | the time of the latest save that changed a value; the creation time until then (BR-M3-28) |

### 1.2 `WorkItemListItem` — a row of the list

`WorkItem` without `projectId`, `description`, `createdBy` and `createdAt`:
`id`, `identifier`, `number`, `type`, `title`, `assignee`, `state`, `area`,
`changedAt`, with the same meanings.

## 2. Routes

The base is `/api/organizations/{organizationId}/projects/{projectId}/workitems`
(`…` below) for everything but § 2.5.

### 2.1 `GET …/workitems?page=1` — the project's list (US-2)

Every item of the project in every state (BR-M3-29), most recently changed
first (`changedAt` descending), then by `number` descending; 50 rows a page.

| Query | Meaning |
|---|---|
| `page` | optional, 1-based, default 1. A page past the last answers an empty `data` with the right `count`. |

There is no `pageSize`, `filter` or `orderBy`; an unknown query parameter is
ignored.

- **200** `{ "count": 120, "data": [ WorkItemListItem, … ] }` — ADR-0015's
  `Paging<T>`: `count` is the number of items in the project, `data` the
  page.
- **400** `workitems.validation_failed` — `errors.page`:
  `workitems.invalid_page` (`page` is less than 1). A `page` that is
  not an integer fails binding with the platform's 400 `http.bad_request`.
- **404** organization, project (§ 0.3).

### 2.2 `POST …/workitems` — create (US-1)

```json
{ "requestId": "0199b0a3-…", "typeId": "…", "title": "Настроить сборку" }
```

| Field | Meaning |
|---|---|
| `requestId` | **required.** A UUID the panel generates once per submission of the dialog and sends again unchanged on every retry of that submission (a double activation of «Создать», a resend after a lost answer). It is the idempotency key of BR-M3-26, unique per project. |
| `typeId` | the id of one of the five types of the organization's process (mod-001's process read) |
| `title` | Заголовок (§ 0.6) |
| `teamId` | optional, added by mod-006 (its api-contract § 7): the team whose backlog or board the item is created from. The item then takes that team's default area as it stands at creation instead of the root (mod-006 BR-M4-21). Absent or `null`: the root, exactly as before. |

The item takes its type's initial state («Новая»), no assignee, an empty
description and the project's root area (BR-M3-20) — or, with `teamId`, that
team's default area; its number is the next of the project's one sequence
(BR-M3-5).

- **201** `WorkItem`, `Location: /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}`.
- **200** `WorkItem` — **a replay**: the project already has the item this
  `requestId` created. Nothing is created, no number is consumed, and the
  answer is that item as it is now. The body of a replay is not compared
  with the first submission's. Two submissions with one `requestId` that
  arrive at the same moment leave one item: one answers 201, the other 200.
- **400** `workitems.validation_failed` — `errors.requestId`:
  `workitems.workitem.request_id_invalid` (missing, malformed or all zeros);
  `errors.title`: `title_required`, `title_too_long` or `title_multiline`.
- **404** organization, project (§ 0.3); `workitems.workitem.type_not_found`
  — `typeId` is missing, malformed, unknown or a type of another
  organization's process; `organizations.team.not_found` (mod-006) —
  `teamId` is present and malformed, unknown, a team of another project or
  a deleted team. The team is answered after the type and after the replay
  check: a replay answers 200 with its item whatever its team has become.

### 2.3 `GET …/workitems/{identifier}` — one item (US-3)

- **200** `WorkItem`.
- **404** organization, project, work item (§ 0.3).

### 2.4 `PATCH …/workitems/{identifier}` — save the form (US-3, US-4, US-5)

Changes exactly the fields sent, all of them or none (BR-M3-25). A field
that is absent, or `null`, is not changed and keeps the value stored at the
moment of the save, whoever changed it meanwhile; two saves of the same
field leave the later one; no conflict is ever reported.

```json
{
  "title": "Падение при входе через SSO",
  "description": "Шаги:\n1. …",
  "stateId": "…",
  "assignee": { "userId": "5d1e8f3a-…" },
  "areaId": "…"
}
```

| Field | Meaning |
|---|---|
| `title` | Заголовок (§ 0.6) |
| `description` | Описание (§ 0.6); `""` clears it |
| `stateId` | a state of the item's own type other than its «Удалена» (category `Removed`), any of them from any other, forwards or backwards (BR-M3-21) |
| `assignee` | `{ "userId": "<id>" }` assigns an active user of the directory; `{ "userId": null }` (or `{}`) clears to «Не назначен» (BR-M3-22). `"assignee": null` or no `assignee` leaves it unchanged. |
| `areaId` | an area of the item's own project, the root included (BR-M3-23) |

A value equal to the stored one changes nothing: re-sending the current
state, or the current assignee even when that user has become inactive since,
is accepted and changes nothing. `changedAt` moves only when at least one
value changes; `{}` answers 200 and changes nothing.

- **200** `WorkItem` — the item as stored after the save.
- **400** `workitems.validation_failed` — `errors.title`: `title_required`,
  `title_too_long`, `title_multiline`; `errors.description`:
  `description_too_long`.
- **404** organization, project, work item (§ 0.3);
  `organizations.area.not_found` — `areaId` is malformed, unknown, an area of
  another project, or an area deleted at the same moment (BR-M3-24).
- **409** `workitems.workitem.state_not_allowed` — `stateId` is malformed,
  unknown, a state of another type or organization, or the type's «Удалена»;
  `workitems.workitem.assignee_unavailable` — `assignee.userId` is unknown to
  the directory or names an inactive user, and is not the current assignee.

### 2.5 `GET /api/organizations/{organizationId}/workitems/assignee-candidates?text=анна` — the assignee picker (US-4)

Until mod-016 the candidates are every **active** user of mod-014's
directory (D-5, OQ-3), found by display name or e-mail, containing the text,
ignoring letter case (mod-014 BR-UP-14).

| Query | Meaning |
|---|---|
| `text` | the typed text. Under 2 characters (after mod-014's normalization) answers nobody; absent is `""`. |

- **200** `{ "users": [ { "id", "displayName", "email" } ], "moreMatched": false }`
  — at most 20 users in mod-014's order (display name, then id);
  `moreMatched` is `true` when more than 20 matched, which the panel reads
  «Показаны первые 20. Уточните запрос.». `email` is `""` when the user has
  none.
- **400** `user_directory.validation_failed` — `errors.text`:
  `user_directory.value_too_long` (more than 100 characters, `params.limit`).
  mod-014's own code, passed through unchanged.
- **404** organization (§ 0.3).

## 3. Reads the panel reuses, unchanged

| Need | Read |
|---|---|
| The type picker, the type icons, the «Статус» control and the dialog's initial-state text | mod-001's `GET /api/organizations/{organizationId}/process`: types in display order (EPIC, FEAT, US, BUG, TASK), each with its states by `order`. The «Статус» control offers the item's type's states whose `category.code` is not `Removed`. |
| The «Область» picker and the dialog's root path | mod-002's `GET …/projects/{projectId}/areas`: every area in pre-order, the root first, each with its `path` (its api-contract § 4.1). |

## 4. Error codes

New, module `workitems`, `contracts/errors/workitems.json`:

| Code | Status | Params | ru |
|---|---|---|---|
| `workitems.workitem.not_found` | 404 | — | «Рабочий элемент не найден.» |
| `workitems.validation_failed` | 400 | — | «Проверьте правильность заполнения полей.» |
| `workitems.workitem.title_required` | 400 (field reason) | — | «Введите заголовок.» |
| `workitems.workitem.title_too_long` | 400 (field reason) | — | «Не более 255 символов.» |
| `workitems.workitem.title_multiline` | 400 (field reason) | — | «Заголовок должен быть в одну строку.» |
| `workitems.workitem.description_too_long` | 400 (field reason) | — | «Не более 100 000 символов.» |
| `workitems.workitem.request_id_invalid` | 400 (field reason) | — | «Не удалось отправить форму. Обновите страницу и попробуйте ещё раз.» |
| `workitems.invalid_page` | 400 (field reason) | — | «Некорректный номер страницы.» |
| `workitems.workitem.type_not_found` | 404 | — | «Тип рабочего элемента не найден.» |
| `workitems.workitem.state_not_allowed` | 409 | — | «Этот статус недоступен для рабочего элемента этого типа.» |
| `workitems.workitem.assignee_unavailable` | 409 | — | «Этого пользователя нельзя назначить исполнителем.» |

Two of these, `request_id_invalid` and `invalid_page`, are not in the
spec's table: they answer a panel defect, never a user's input,
and the panel shows them as any other refusal (the dialog's `Alert`, the
list's `ErrorBanner`).

Reused, unchanged: `organizations.organization.not_found`,
`organizations.project.not_found`, `organizations.area.not_found`,
`organizations.area.not_empty` (§ 6), and mod-014's
`user_directory.validation_failed` / `user_directory.value_too_long` (§ 2.5).

## 5. Summary of routes

| Method | Route | Code declared | Story |
|---|---|---|---|
| GET | `/api/organizations/{organizationId}/projects/{projectId}/workitems` | `workitems.workitem.read` | US-2 |
| POST | `/api/organizations/{organizationId}/projects/{projectId}/workitems` | `workitems.workitem.write` | US-1 |
| GET | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}` | `workitems.workitem.read` | US-3 |
| PATCH | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}` | `workitems.workitem.write` | US-3, US-4, US-5 |
| GET | `/api/organizations/{organizationId}/workitems/assignee-candidates` | `workitems.workitem.write` | US-4 |

All five are organization-addressed and carry mod-002's filter; its
inventory test lists them.

## 6. What changes in mod-002's area deletion (US-6)

`DELETE /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}`
(mod-002 api-contract § 4.6) also refuses an area that is the `area` of any
work item, in any state, with its existing 409 `organizations.area.not_empty`
and text (BR-M3-24). The order of its refusals: the root, then children or
work items, then a team's default area. Nothing else in mod-002's contract
changes.

**The race** (BR-M3-24): a save that moves an item into an area locks that
area `FOR SHARE`; the deletion locks it `FOR UPDATE` and only then looks for
children and work items. Whoever comes second waits: a deletion that went
first leaves no area, and the save answers `organizations.area.not_found`
with the item unchanged; a save that went first leaves an item in the area,
and the deletion answers `organizations.area.not_empty`.

## 7. In-process port

`Mustask.Application.Abstractions.Organizations.IAreaUsage` — declared by M1,
implemented by this module:

```csharp
Task<bool> IsInUseAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken);
```

`true` when a work item of that project, in any state, has that area. M1's
deletion calls it after its lock on the area; M1 never reads work items
itself.
