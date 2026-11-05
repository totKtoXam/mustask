# API contract — mod-006-boards-backlogs

Fixed at stage 4a (2026-10-05), before the code. From now on it changes only
with a note in [status.md](status.md) and a Log line; once stage 6 has
started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan
is the `## Plan` section of [status.md](status.md).

The panel uses § 1–§ 6. § 7 is the one change to mod-003's contract (the
create request gains `teamId`), recorded there too.

## 0. Rules shared by every endpoint

### 0.1 Access

- **Authentication.** `Authorization: Bearer <access token>` from realm
  `mustask`. No token, or an invalid or expired one, gets 401
  `http.unauthorized` before any other check.
- **Guard (interim, D-3, BR-M4-27).** Every endpoint is open to any
  signed-in user, exactly as mod-002's and mod-003's. Nothing narrows by
  membership until mod-016.
- **Permission codes** (BR-M4-27). The two reads declare
  `workitems.workitem.read`, the reorder `boards.backlog.reorder`; the card
  move and the creation are mod-003's routes and declare
  `workitems.workitem.write`. `boards.backlog.reorder` is a new
  organization-scoped code (`OrganizationPermissionCatalog`): no token
  carries it, the realm import does not mention it, and nothing checks it
  until mod-016.
- **Organization context.** Every route has an `organizationId` and runs
  behind mod-002's organization-context filter (its D-g); its inventory test
  lists the three routes of § 5.

### 0.2 Identifiers

As mod-002 api-contract § 0.2: every id is a UUID, written lowercase in
responses; an id in a path or a body is an untrusted string, accepted only in
the canonical 8-4-4-4-12 form in either letter case and not all zeros, and
anything else answers like an id that names nothing.

**The level** is the path segment `{level}`, one of three Latin words
(spec § Model):

| `{level}` | Level of the process | Types, in display order (seeded process) |
|---|---|---|
| `epics` | the portfolio level directly above `features` | «Эпик» |
| `features` | the lowest portfolio level | «Фича» |
| `stories` | the requirement level | «Пользовательская история», «Баг» |

Exact spelling, lower case. `tasks` and every other value answer
`boards.level.not_found` (§ 4): the «Задачи» level has no backlog or board in
this cut (OQ-3, BR-M4-1). The mapping goes by the level's kind and rank in
the organization's process (mod-001), never by its name; a process with
fewer portfolio levels has no `epics` (then no `features`) and answers
`boards.level.not_found` for it.

### 0.3 Not found, from the outside in

A request is answered with the not-found code of the outermost level that is
not found:

| Level | When | `errorCode` | `detail` |
|---|---|---|---|
| organization | malformed, unknown or deactivated (the filter) | `organizations.organization.not_found` | «Организация не найдена.» |
| project | malformed, unknown, of another organization, or deactivated | `organizations.project.not_found` | «Проект не найден.» |
| team | malformed, unknown, of another project, or deleted (BR-M4-20) | `organizations.team.not_found` | «Команда не найдена.» |
| level | not one of § 0.2's three, or the process has no such level | `boards.level.not_found` | «Уровень бэклога не найден.» |

### 0.4 The order of answers

1. the not-found of § 0.3 for the path;
2. 400 `boards.validation_failed` (the reorder only);
3. after the backlog's lock: the team's not-found again, if it was deleted
   meanwhile, then 409 `boards.backlog.reorder_stale`.

### 0.5 Error body

`application/problem+json`, as everywhere in the API (mod-002 api-contract
§ 0.5): `errorCode` is the code the client branches on, `detail` the Russian
text from the catalog (§ 4). `errors` is present only with
`boards.validation_failed`: `{ "position": [ { "code", "detail" } ] }`.

## 1. What a team's backlog and board hold

For team T of project P and level L, exactly the work items that, **at the
moment of the read**, (1) belong to P, T being an active team of P; (2) have
a type of L; (3) have an area whose resolved owner is T — its explicit owner,
else its nearest owned ancestor's, the root's being the first team (mod-002
`resolvedOwner`, BR-M1-11); (4) are in a state outside the Removed category
(so in this cut every state, «Закрыта» included, OQ-2). An item is on
exactly one team's backlog at its level. The board holds the same items as
the backlog, in the same order.

**The order** (BR-M4-24): the items **placed** on this backlog by their
places, then the **unplaced** ones by `number` ascending. An item is
unplaced on a backlog until a reorder of that backlog places it; every item
is unplaced when this task ships, a created item is unplaced (so it is last:
it has the highest number), and an item that comes onto the backlog — its
area changed, its area re-owned or moved, its team deleted — is unplaced
there unless it still holds a place on this very backlog (it left, nothing
reordered this backlog meanwhile, and it was placed nowhere else). The order
is not a field of the work item: a reorder changes no item and not its
`changedAt`.

## 2. Shapes

### 2.1 `BoardTeam`

```json
{ "id": "…", "name": "Платформа", "defaultArea": { "id": "…", "path": "Платформа" } }
```

`defaultArea.path` as mod-002 DC-9 («Платформа / API», the root by its own
name). It is what the creation dialog quotes (spec § Dialog), read with the
page; the creation itself takes the default as it stands when it runs (§ 7).

### 2.2 `BoardLevel`

```json
{
  "code": "stories",
  "name": "Истории",
  "types": [
    {
      "id": "…", "key": "US", "name": "Пользовательская история", "appearance": "story",
      "initialState": { "id": "…", "name": "Новая", "category": { "code": "Proposed", "label": "Новые" } }
    },
    {
      "id": "…", "key": "BUG", "name": "Баг", "appearance": "bug",
      "initialState": { "id": "…", "name": "Новая", "category": { "code": "Proposed", "label": "Новые" } }
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `code` | the `{level}` segment it was read under |
| `name` | the process level's Russian name («Эпики», «Фичи», «Истории») |
| `types` | the level's types in the process's display order — the «Новый рабочий элемент» picker (one type: the dialog opens preset) — each with mod-003's type fields and its initial state, which the dialog's text names |

### 2.3 `BacklogItem` — a backlog row, a board card

> Superseded in part by feat-032 (2026-10-12): the board card gains `checklist`, the item's task, bug and test-case children (see feat-032's api-contract § 2 and spec BR-C-1).

```json
{
  "order": 1,
  "id": "0199b0a2-…",
  "identifier": "MSK-BUG-4",
  "number": 4,
  "type": { "id": "…", "key": "BUG", "name": "Баг", "appearance": "bug" },
  "title": "Падение при входе",
  "assignee": null,
  "state": { "id": "…", "name": "Активна", "category": { "code": "InProgress", "label": "В работе" } }
}
```

| Field | Meaning |
|---|---|
| `order` | «Порядок»: the item's 1-based position in § 1's order, dense over the whole backlog; on a card, its backlog position |
| `id` | the work item's id: what a reorder names |
| `identifier`, `number`, `type`, `title`, `state` | exactly as mod-003's `WorkItem` (its api-contract § 1.1); `identifier` is what the form's address uses |
| `assignee` | as mod-003: `null` for «Не назначен»; `{ "id", "displayName", "email" }`, both names `null` for «Неизвестный пользователь» |
| `priority` | added by feat-022 (its api-contract § 5): the item's «Приоритет», an integer 1..4 — on every backlog row, nested rows included, and every board card |
| `effort`, `businessValue`, `valueArea` | added by feat-027 (its api-contract § 2.1) on **backlog rows only**, nested rows included: «Трудоёмкость», «Бизнес-ценность», «Вид ценности» as feat-022 answers them on the item, `null` where the type does not have the field. A board card does not carry them. |

On the backlog read, feat-027 (its api-contract § 2.1) narrows `order` to the
top-level rows, dense over them; a card's `order` is unchanged.

## 3. Routes

The base is
`/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}`
(`…` below).

### 3.1 `GET …/backlogs/{level}` — a team's backlog (US-1)

- **200**
  ```json
  { "team": BoardTeam, "level": BoardLevel, "items": [ BacklogItem, … ] }
  ```
  `items` is the whole backlog in § 1's order, `order` 1..n; no paging
  (spec § Screens: «No paging»), `[]` for an empty backlog. Added by
  feat-001 (its api-contract § 4): every element of `items` also carries
  `parent` (the stored parent, `null` when none or in «Удалена») and
  `children` (the same team's backlog one level below nested by the links,
  each nested row without `order`); the board's cards (§ 3.3) do not.
  Changed by feat-027 (its api-contract § 1, § 2): `children` nest the tree
  at full depth — every level below this one, the «Задачи» level included,
  each child of this team and not in «Удалена», by level then its backlog's
  order — and an item whose parent is of this level and this team stands
  beneath that parent only, not among the top-level rows; `order` numbers the
  top-level rows 1..n; every row gains `effort`, `businessValue` and
  `valueArea` (§ 2.3).
- **404** organization, project, team, level (§ 0.3).

### 3.2 `POST …/backlogs/{level}/reorder` — move one item (US-2)

```json
{ "itemId": "…", "position": "after", "afterItemId": "…" }
```

| Field | Meaning |
|---|---|
| `itemId` | the id of the moved item |
| `position` | `"top"`, `"bottom"` or `"after"`, exact spelling (BR-M4-25) |
| `afterItemId` | with `"after"` only: the id of the item the moved one goes directly after. Ignored with `"top"` and `"bottom"`. |

How the panel's gestures map (spec § Page «Бэклоги»): a drop above the first
row and «В начало» → `top`; below the last row and «В конец» → `bottom`; a
drop elsewhere → `after` the row above the line; «Выше» → `after` the row two
above, or `top` on the second row; «Ниже» → `after` the row below.

What the server does (BR-M4-24, -25), in one transaction:

1. locks the team and then this backlog, so reorders of one backlog run one
   at a time in arrival order;
2. reads the backlog's current order (§ 1) as it stands after the previous
   reorder committed;
3. refuses with 409 `boards.backlog.reorder_stale` if `itemId`, or with
   `"after"` the `afterItemId`, is not an item of this backlog now —
   missing, malformed, unknown, of another backlog, or gone from this one
   since the client read it (BR-M4-26); nothing changes;
4. moves the one item, then gives **every** item of the backlog the place it
   now stands at, and drops the places this backlog held for items that
   have left it. An item placed here loses any place it held on another
   backlog. `afterItemId` equal to `itemId` leaves the item where it is.

Two reorders of different items both take effect; of two reorders of the
same item, the later decides its place. No reorder is refused for an old
view, and nobody is told (D-2) — the panel reads the backlog again.

- **204** — no body; the panel reads § 3.1 again.
- **400** `boards.validation_failed` — `errors.position`:
  `boards.backlog.position_invalid` (`position` is missing or not one of the
  three words). A panel defect, never a user's input.
- **404** organization, project, team, level (§ 0.3); the team also when it
  was deleted while the reorder waited for its lock.
- **409** `boards.backlog.reorder_stale` — step 3.

### 3.3 `GET …/boards/{level}` — a team's board (US-4)

- **200**
  ```json
  {
    "team": BoardTeam,
    "level": BoardLevel,
    "columns": [
      {
        "key": "InProgress-2",
        "header": "Разрешена",
        "category": { "code": "InProgress", "label": "В работе" },
        "targets": [
          { "typeId": "<US>", "stateId": "<US «Разрешена»>" },
          { "typeId": "<BUG>", "stateId": null }
        ],
        "items": [ BacklogItem, … ]
      }
    ]
  }
  ```
- **404** organization, project, team, level (§ 0.3).

| Field | Meaning |
|---|---|
| `columns` | BR-M4-2, derived from the process on every read: categories in the order Proposed, InProgress, Resolved, Completed (Removed has none); inside a category as many columns as the most states any of the level's types has there, the k-th mapping each type to its k-th state of that category in the process's order, or to none. A category no type of the level has gets no column. |
| `key` | `<category code>-<k>`, stable for the process: `Proposed-1`, `InProgress-1`, `InProgress-2`, `Resolved-1`, `Completed-1` |
| `header` | the column's title: the category's label for k = 1, otherwise the name of the state it maps for the first type, in display order, that maps one |
| `category` | the category the column belongs to (`code` never shown) |
| `targets` | one entry per type of the level, in display order: the state a drop of a card of that type in this column writes, or `null` — then the column is no drop target for that type and is not offered in its card menu (BR-M4-18) |
| `items` | the cards: the items whose current state is a `targets` state of their type here, in the backlog's order (§ 1; `order` is their backlog position, so it has gaps inside a column). An empty column has `[]`. Every item of the backlog is in exactly one column. *Superseded in part by feat-032 (2026-10-12): the board card gains `checklist`, the item's task, bug and test-case children (see feat-032's api-contract § 2 and spec BR-C-1).* |

For the seeded process: `epics` and `features` have «Новые», «В работе»,
«Разрешена», «Завершённые»; `stories` has «Новые», «В работе», «Разрешена»
(US «Разрешена», BUG `null`), «Решённые» (US `null`, BUG «Разрешена»),
«Завершённые» (spec § Model).

> **Changed by bug-004 D-3 (2026-10-12), one name, one meaning** — a note in
> that task's `status.md` records it; the shape of the answer (the fields
> above) does not change, only these values:
>
> - `header`: **the name of the state the column maps** for the first type, in
>   display order, that maps one — for every column, not only for k > 1. The
>   category's label is no header; the column's `category` still carries it.
> - `key`: unchanged in form; the seeded keys are `Proposed-1`,
>   `InProgress-1`, `Resolved-1`, `Completed-1` — `InProgress-2` is gone,
>   because «Разрешена» is in the Resolved category on every type.
> - the seeded process: `epics`, `features` and `stories` all have «Новая»,
>   «Активна», «Разрешена», «Закрыта», each column mapping every type of its
>   level (the story's and the bug's «Разрешена» are two states under one
>   column). The example above reads `"key": "Resolved-1"`,
>   `"category": { "code": "Resolved", "label": "Решённые" }`, with both
>   `targets` non-null.
> - a `null` target remains possible for a process shaped differently
>   (BR-M4-18); the seeded one has none.
>
> The table and the example above are left as written.

## 4. Error codes

New, module `boards`, `contracts/errors/boards.json` (Kazakh texts with
`kkNeedsReview`):

| Code | Status | Params | ru |
|---|---|---|---|
| `boards.backlog.reorder_stale` | 409 | — | «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.» |
| `boards.level.not_found` | 404 | — | «Уровень бэклога не найден.» |
| `boards.validation_failed` | 400 | — | «Проверьте правильность запроса.» |
| `boards.backlog.position_invalid` | 400 (field reason) | — | «Некорректная позиция перемещения.» |

Only the first is the spec's; the other three answer a panel defect (an
unknown level never leaves the panel's router, a malformed position never
leaves its code), as spec § Error codes leaves to this stage, and the panel
shows them as any other refusal — `boards.level.not_found` in place of the
table or the columns as `ErrorBanner`, the two 400s as the reorder's
notice with the server's `detail`.

Reused, unchanged: mod-002's `organizations.organization.not_found`,
`organizations.project.not_found`, `organizations.team.not_found` (page
frames; the creation dialog's `Alert`); mod-003's
`workitems.workitem.state_not_allowed` and its other PATCH and create codes.

## 5. Summary of routes

| Method | Route | Code declared | Story |
|---|---|---|---|
| GET | `/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}` | `workitems.workitem.read` | US-1, US-7 |
| POST | `/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}/reorder` | `boards.backlog.reorder` | US-2 |
| GET | `/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/boards/{level}` | `workitems.workitem.read` | US-4, US-7 |

All three are organization-addressed and carry mod-002's filter; its
inventory test lists them. OpenAPI tag `Boards`.

## 6. Writes the panel reuses

| Need | Route |
|---|---|
| A card move (US-5, BR-M4-3) | mod-003's `PATCH …/projects/{projectId}/workitems/{identifier}` with `{ "stateId": "<the column's target for the card's type>" }`, unchanged: it writes only the state and `changedAt`, last write wins, touches no backlog place, and refuses a state of another type with 409 `workitems.workitem.state_not_allowed` (BR-M4-18). Its 200 body is the item; the panel reads the board again. Every rule of spec § Model's card move is met by it as it is, so the board has no move route of its own. |
| A creation from a backlog or a board (US-3, US-6, BR-M4-21) | mod-003's `POST …/projects/{projectId}/workitems` with `teamId` (§ 7) |
| The dialog's team name and default-area path | § 2.1 (also mod-002's `GET …/teams/{teamId}`) |

## 7. The change to mod-003's create (additive)

`POST /api/organizations/{organizationId}/projects/{projectId}/workitems`
(mod-003 api-contract § 2.2) gains one optional body field:

| Field | Meaning |
|---|---|
| `teamId` | optional. The team whose backlog or board the item is created from: the item takes that team's default area **as it stands when the item is created** (the first team's is the root), instead of the project's root. Absent or `null`: the root, as before. *Changed by feat-027: when the same request also names a `parentId` (mod-003 § 2.2), the item takes the parent's area as stored instead — the team is still checked (404 below), but its default area is not taken.* |

- **404** `organizations.team.not_found` — `teamId` is malformed, unknown, a
  team of another project, or a deleted team (US-3 "The team was deleted
  while the dialog was open"); nothing is created and no number is consumed.
- Order: after the type's not-found, the number's lock and the replay check
  — a replay answers 200 with its item whatever its team has become since.
- Concurrency: the team is locked `FOR SHARE` and its default read under
  that lock. A default change (`FOR NO KEY UPDATE`) or a deletion
  (`FOR UPDATE`) of that team waits for the creation, or the creation waits
  for it and then sees its outcome: a new default is taken, a deleted team
  answers not found.

Every other field, answer and rule of mod-003 § 2.2 is unchanged; a request
without `teamId` behaves exactly as before. *Changed by feat-027: mod-003
§ 2.2 gained the optional `parentId`, with its 404 and 409s and the parent's
area (feat-027 api-contract § 3); a request with `parentId` follows that
section, not this one, for its area and its `parent`.*
