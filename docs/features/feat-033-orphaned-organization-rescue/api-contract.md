# API contract — feat-033-orphaned-organization-rescue

Fixed at stage 4 (2026-11-03). The interface for stage 6; a change after this
point needs a note in `status.md` and a Log line, and once stage 6 has started
it goes to the orchestrator (`stages/04-backend.md` § 4a item 4).

Inputs: `spec.md` BR-RS-1..12, US-1..US-3, `design.md`, ADR-0029 § 8–§ 9,
ADR-0033, ADR-0034 (the projection after tech-025), ADR-0035 (proposed).
Plan: `plan-backend.md`.

## 0. Rules shared by every endpoint

### 0.1 Two kinds of route

| Kind | Routes | Guard |
|---|---|---|
| **Rescue (instance)** | §§ 1.1–1.4, under `/api/organizations/rescue` | the instance code `organizations.organization.rescue` from the token (`.RequiresPermission`), reached through `InstanceRoles` by `system-admin` only (ADR-0035 § 2). No membership is read for the guard. |
| **Organization** | § 2.1 | the scoped check with the organization code `organizations.membership.write` (`.RequiresOrganizationPermission`, ADR-0027) |

`rescue` is a literal segment: the router ranks it above `{organizationId}`,
as it does `/deactivated`. The two rescue routes that carry
`{organizationId}` are **inventory exceptions** with ADR-0029 § 9's reason
(mod-016 BR-M1-33): they declare the instance code, not an organization code,
and they read no organization content.

### 0.2 Answer order

**Rescue routes** — the first that applies (BR-RS-6a, ADR-0029 § 8's
not-found-first habit after the guard):

| # | Condition | Answer |
|---|---|---|
| 1 | no valid token | 401 `http.unauthorized` |
| 2 | the token lacks `organizations.organization.rescue` | **403** `organizations.rescue.forbidden` «Недостаточно прав» — before the body is read; nothing changes |
| 3 | `{organizationId}` (where present) is malformed or names no organization. A **deactivated** organization is found. | 404 `organizations.organization.not_found` «Организация не найдена.» |
| 4 | the operation's own answers, in the order its section gives | 400 / 409 |

**§ 2.1** — the scoped check, unchanged (mod-016 api-contract § 0.1 as
amended by ADR-0029 § 8): 401 → 404 `organizations.organization.not_found`
(malformed, unknown, deactivated, or the caller holds no membership) → 403
`organizations.access.forbidden` «Недостаточно прав» (a member whose role lacks
`organizations.membership.write`) → 200.

### 0.3 Shapes reused

- **`UserRef`** — mod-016 api-contract § 0.4, unchanged: `{ id, displayName,
  email, state }`, `state` ∈ `active` | `inactive` | `unknown`. `inactive` and
  `unknown` can only appear for rows from before tech-025 (BR-RS-10).
- **User id** — mod-016 § 0.3: the Keycloak `sub`, an opaque string of 1–255
  characters, taken as given (never trimmed).
- **Organization id** — mod-002 § 0.2: an untrusted string; anything but a
  canonical, non-zero UUID answers like an id that names nothing.
- **Times** — ISO 8601 in UTC with offset. The panel shows them in the viewer's
  zone (BR-RS-12).
- **Error body** — `application/problem+json` as everywhere (mod-016 § 0.6);
  the client branches on `errorCode`.
- **No `If-Match`.** Concurrent writes are serialized by the server (§ 1.3,
  "Concurrency").

## 1. Rescue routes (system administrator)

### 1.1 `GET /api/organizations/rescue?page=1&name=гам` — every organization · `organizations.organization.rescue`

US-1, BR-RS-2, BR-RS-3 (a).

| Query | Type | Rule |
|---|---|---|
| `page` | integer, optional, default 1 | `< 1` or not an integer → 400 `organizations.validation_failed`, `errors.page`: `organizations.rescue.invalid_page`. A page past the last answers `data: []` with the true `count`. |
| `name` | string, optional | trimmed; empty after trimming = no filter. Matches any **substring** of the organization's name, letter case ignored, `ё` = `е` (both sides). `%`, `_`, `\` are literal. Never an error: a text that matches nothing (over 100 characters included) answers `count: 0`. |

The page size is fixed at **25** (BR-RS-2); there is no `pageSize` parameter.
Every organization of the instance, **active and deactivated**, ordered by name
(`ru-x-icu` collation: letters before case, `Ё` with `Е`), then by id.

**200** (ADR-0015's shape, as the work item list):

```json
{
  "count": 120,
  "data": [
    { "id": "0192…", "name": "Альфа", "status": "active", "lastAppointedAt": null },
    { "id": "0193…", "name": "Бета", "status": "active", "lastAppointedAt": "2026-11-03T11:05:00+00:00" },
    { "id": "0194…", "name": "Гамма", "status": "deactivated", "lastAppointedAt": null }
  ]
}
```

| Field | Meaning |
|---|---|
| `count` | organizations matching the filter, all pages |
| `status` | `active` → «Активна», `deactivated` → «Деактивирована» |
| `lastAppointedAt` | the moment of the newest appointment record of the organization (§ 1.3); `null` → «—» |

Nothing else is returned: no member count, no administrator, no project
(BR-RS-2, BR-RS-3). **400** `page`. **401**. **403** `organizations.rescue.forbidden`.

### 1.2 `GET /api/organizations/rescue/appointee-candidates?text=вера` — users to appoint · `organizations.organization.rescue`

US-2 "Only users who have signed in are found", "A row marked inactive before
tech-025", "The search does not reveal membership"; BR-RS-6, BR-RS-3 (b).

The «Пользователь» search of the appointment dialog: the **active** users of
the projection (mod-014 BR-UP-14/15 through `IUserDirectory.SearchAsync`,
`IncludeInactive: false`): by display name or e-mail, at least 2 characters
after mod-014's normalization (fewer answers nobody), at most 20 in mod-014's
order, `moreMatched` when more matched. A user who never signed in is not in
the projection; a pre-tech-025 row marked inactive is hidden. **No organization
in the address**: the answer says nothing about any membership, so it needs
none (the design's `{ kind: 'appointee', organizationId }` scope sends no id).

**200** — the shape of mod-016 § 1.3:

```json
{ "users": [ { "id": "c0ffee…", "displayName": "Вера Иванова", "email": "vera@example.org" } ], "moreMatched": false }
```

`email` is `""` for a user with none. **400** — mod-014's own, passed through:
`user_directory.validation_failed`, `errors.text`: `user_directory.value_too_long`
(over 100 characters). **401**. **403** `organizations.rescue.forbidden`.

### 1.3 `POST /api/organizations/rescue/{organizationId}/appointments` — appoint an administrator · `organizations.organization.rescue`

US-2, US-3 "An appointment leaves a record", "No appointment without its
record"; BR-RS-4..-11.

Body:

```json
{ "userId": "c0ffee…", "reason": "  Борис уволился  " }
```

**Answer order** (after § 0.2 rows 1–3; BR-RS-6a). The first that applies:

| # | Condition | Answer |
|---|---|---|
| 3 | organization malformed or unknown (deactivated is accepted) | 404 `organizations.organization.not_found` |
| 4a | `userId` missing, blank or over 255 characters | 400 `organizations.validation_failed`, `errors.userId`: `organizations.membership.user_invalid` «Укажите пользователя» (the panel never sends it: «Назначить» is disabled until a user is chosen) |
| 4b | the user is not in the projection, or their row is inactive (pre-tech-025) | 409 `organizations.rescue.user_unavailable` «Этого пользователя нельзя назначить администратором» |
| 5 | `userId` is the caller's own `sub` | 409 `organizations.rescue.self_appointment` «Нельзя назначить администратором себя» |
| 6 | the user already holds `admin` in the organization (judged on committed state, under the lock) | 409 `organizations.rescue.already_admin` «Пользователь уже администратор этой организации» |
| 7 | `reason` after trimming: missing, empty, or only whitespace | 400 `organizations.validation_failed`, `errors.reason`: `organizations.rescue.reason_required` «Укажите причину» |
| 7 | `reason` after trimming is over **500 Unicode code points** (counted as the panel counts, `Array.from(text.trim()).length`; an emoji is one) | 400 … `errors.reason`: `organizations.rescue.reason_too_long` «Не более 500 символов» |
| 7 | `reason` holds U+0000 or a Unicode noncharacter (an API caller only; PostgreSQL cannot store U+0000) | 400 … `errors.reason`: `organizations.rescue.reason_invalid` «Причина содержит недопустимые символы» |
| 8 | a concurrent mod-016 «Добавить участника» of the same user committed between this request's lock and its insert (see "Concurrency") | 409 `organizations.membership.already_member` «Пользователь уже состоит в организации»; nothing changes, and a repeat raises the new member |

A refusal writes nothing — no membership change, no record.

**Effect** (one transaction, BR-RS-8, BR-RS-10):

- the user is a member (`member` or `viewer`): the role becomes `admin`;
  `addedBy`/`addedAt` and team memberships are kept; `roleChangedBy` = the
  caller, `roleChangedAt` = now;
- the user is not a member: a new `admin` membership with `addedBy` = the
  caller, `addedAt` = now, `roleChangedBy`/`roleChangedAt` `null`, in no team;
- one appointment record: the organization, the appointee, the caller, now, and
  the **trimmed** reason;
- nothing else: no other membership, the organization's status (a deactivated
  one stays deactivated, BR-RS-9) and no Keycloak call.

**201** — the record just written, the row shape of § 1.4:

```json
{
  "id": "0195…",
  "appointedAt": "2026-11-03T11:05:00+00:00",
  "appointedBy": { "id": "sa…", "displayName": "Системный Администратор", "email": "admin@example.org", "state": "active" },
  "appointee":   { "id": "c0ffee…", "displayName": "Вера Иванова", "email": "vera@example.org", "state": "active" },
  "reason": "Борис уволился"
}
```

`Location` is not set (a record has no read of its own). The panel shows
«Администратор назначен: <appointee.displayName>».

**Concurrency** (BR-RS-11). The appointment locks the organization row
(`FOR NO KEY UPDATE`), then the user's membership row — the lock order of
mod-016's role change and removal, so it is serialized against them and
against another appointment to the same organization:

- the same user appointed twice at once: one succeeds, the other gets
  `already_admin`;
- different users at once: both succeed, each with its own record;
- against a removal: the later one is judged on the committed state (removed
  first → a new `admin` membership; appointed first → the removal applies to
  the `admin` membership under mod-016's rules, BR-M1-4, and the record stays);
- against a raise to `admin`: the later appointment gets `already_admin`;
- against a mod-016 add, which takes no lock: if the add commits in between,
  row 8 answers and nothing changes.

**400**, **401**, **403** `organizations.rescue.forbidden`, **404**, **409**
as above.

### 1.4 `GET /api/organizations/rescue/{organizationId}/appointments` — an organization's record · `organizations.organization.rescue`

US-3, BR-RS-10. Any organization, active or deactivated.

**200** — every record of the organization, **newest first** (`appointedAt`
descending, then `id` descending); `[]` when there is none («Назначений не
было»). Not paged (up to ~100 rows, NFR):

```json
[
  { "id": "0196…", "appointedAt": "2026-11-20T08:00:00+00:00", "appointedBy": { "…": "UserRef" }, "appointee": { "…": "UserRef" }, "reason": "…" },
  { "id": "0195…", "appointedAt": "2026-11-03T11:05:00+00:00", "appointedBy": { "…": "UserRef" }, "appointee": { "…": "UserRef" }, "reason": "Борис уволился" }
]
```

| Field | Meaning |
|---|---|
| `appointedAt` | the moment of the appointment («Когда», «ДД.ММ.ГГГГ ЧЧ:мм» in the viewer's zone) |
| `appointedBy` | «Кто назначил», a `UserRef` |
| `appointee` | «Кого назначили», a `UserRef` |
| `reason` | the full trimmed reason, 1–500 code points |

Records are never edited or deleted; no route writes one except § 1.3.
**401**, **403** `organizations.rescue.forbidden`, **404**
`organizations.organization.not_found` (malformed or unknown id only).

## 2. Organization route (the organization's administrators)

### 2.1 `GET /api/organizations/{organizationId}/appointments` — the organization's own record · `organizations.membership.write`

US-3 "The organization's administrators read their own record", "Other
members do not", "An organization's administrator reads only their own
organization", "Without the right". A GET with a write code — legitimate (as
`…/member-candidates`), not an exception of BR-M1-33.

**200** — the same rows as § 1.4, in the same order, the same shape.
**401**; **404** `organizations.organization.not_found` for a non-member, an
unknown or a deactivated organization; **403** `organizations.access.forbidden`
«Недостаточно прав» for a member or a reader.

## 3. Error codes

New codes, module `organizations` (`OrganizationErrorCodes`, `[ErrorCodes]`;
`contracts/errors/organizations.json` with `ru` and `kk`, `kkNeedsReview: true`;
`docs/error-codes.md` regenerated):

| Code | Status | ru |
|---|---|---|
| `organizations.rescue.forbidden` | 403 | Недостаточно прав |
| `organizations.rescue.user_unavailable` | 409 | Этого пользователя нельзя назначить администратором |
| `organizations.rescue.self_appointment` | 409 | Нельзя назначить администратором себя |
| `organizations.rescue.already_admin` | 409 | Пользователь уже администратор этой организации |
| `organizations.rescue.reason_required` | 400 (field reason) | Укажите причину |
| `organizations.rescue.reason_too_long` | 400 (field reason) | Не более 500 символов |
| `organizations.rescue.reason_invalid` | 400 (field reason) | Причина содержит недопустимые символы |
| `organizations.rescue.invalid_page` | 400 (field page) | Некорректный номер страницы. |

Reused, unchanged: `organizations.organization.not_found`,
`organizations.access.forbidden`, `organizations.validation_failed`,
`organizations.membership.user_invalid`, `organizations.membership.already_member`,
`user_directory.validation_failed` / `user_directory.value_too_long`,
`http.unauthorized`. The rescue routes answer their own 403 code (as
`organizations.organization.create_forbidden`), never the platform's
`http.forbidden` «Доступ запрещён.», because the spec's text is «Недостаточно
прав».

## 4. Instance code

| Code | Scope | Description (catalog) | Held by |
|---|---|---|---|
| `organizations.organization.rescue` | Instance | «Назначение администратора в любую организацию» | `system-admin` only (it holds every instance code; no other role lists it) |

The panel's mirror `src/frontend/src/shared/api/tokenClaims.ts`
(`INSTANCE_ROLE_CODES['system-admin']`) gains the code in the same change
(ADR-0033 § 2, ADR-0035 § 2). The panel shows «Все организации» by this code
and treats any 403 from §§ 1.1–1.4 as «Доступ ограничен» (design DC-8); the
«Участники» command «История назначений» is drawn from
`organizations.membership.write` in the organization's access answer (mod-016
§ 1.1), which carries no new field.
