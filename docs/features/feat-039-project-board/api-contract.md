# API contract — feat-039-project-board

Fixed at stage 4a (2026-10-17), before the code. From now on it changes only
with a note in [status.md](status.md) and a Log line; once stage 6 has
started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan
is the `## Plan` section of [status.md](status.md).

This task adds **three routes** to the boards slice, the project's own
backlog, reorder and board (BR-PB-1, BR-PB-4), and the storage of the
project's order (§ 6). It changes no team route, answer, order or code
(BR-PB-8), registers **no permission code and no error code** (BR-PB-5,
ADR-0029 delivery lines), and changes no merged contract: § 7 lists what the
panel reuses, unchanged.

## 0. Rules shared by every endpoint

Exactly mod-006's (its api-contract § 0), with the team level taken out.

### 0.1 Access

- **Authentication.** `Authorization: Bearer <access token>` from realm
  `mustask`; no token, or an invalid or expired one, gets 401
  `http.unauthorized` before any other check.
- **Organization codes** (ADR-0027, mod-016 api-contract § 0.2). The two reads
  declare `workitems.workitem.read`, the reorder `boards.backlog.reorder`
  («Администратор» and «Участник» hold it, «Читатель» does not). The scoped
  check runs before the body is read: 401, then 404
  `organizations.organization.not_found` for a person outside the
  organization (an instance administrator with no membership included), then
  403 `organizations.access.forbidden` for a role without the code.
  `mod-019-scoped-access` replaces the reorder's code with
  `boards.projectbacklog.reorder` (BR-PB-5); until then a project's backlog is
  ordered by the same bundle as a team's.
- **Tenant isolation.** Each route has `{organizationId}` and declares its
  code, so the inventory test (`OrganizationAccessInventoryTests`) lists all
  three (§ 5); a project of another organization answers like one that names
  nothing (BR-MX-1).

### 0.2 Identifiers and the level

As mod-006 § 0.2: every id is a UUID written lowercase in responses; an id in a
path or a body is an untrusted string, accepted only in the canonical
8-4-4-4-12 form (either letter case, not all zeros), and anything else answers
like an id that names nothing. `{level}` is `epics`, `features` or `stories`,
mapped to the organization's process by kind and rank and never by name; `tasks`
and every other value answer `boards.level.not_found` (BR-PB-1).

### 0.3 Not found, from the outside in

| Level | When | `errorCode` | `detail` |
|---|---|---|---|
| organization | malformed, unknown or deactivated (the scoped check) | `organizations.organization.not_found` | «Организация не найдена.» |
| project | malformed, unknown, of another organization, or deactivated | `organizations.project.not_found` | «Проект не найден.» |
| level | not one of § 0.2's three, or the process has no such level | `boards.level.not_found` | «Уровень бэклога не найден.» |

There is **no team level**: no route here takes a team, and
`organizations.team.not_found` cannot occur on a project page.
`feat-041-project-visibility` (BR-MX-24) later puts the project's not-found
before the 403; today the order is the one below.

### 0.4 The order of answers

Reads: 401, organization 404, 403, project 404, level 404.
Reorder: 401, organization 404, 403, project 404, level 404, then 400
`boards.validation_failed` (the position), then, under the locks, the project's
not-found again if it was deactivated meanwhile, then 409
`boards.backlog.reorder_stale`.

### 0.5 Error body

`application/problem+json` as everywhere (mod-002 api-contract § 0.5);
`errors` is present only with `boards.validation_failed`:
`{ "position": [ { "code", "detail" } ] }`.

## 1. What a project's backlog and board hold (BR-PB-2)

For project P (active, in an active organization) and level L, exactly the work
items that, **at the moment of the read**, (1) belong to P; (2) have a type of
L; (3) are in a state outside the Removed category. Nothing else narrows it: not
the item's area, the area's owning team, a sprint or a filter. An item is on one
project's views only, and on exactly one level; the board holds the same items
as the backlog.

**The tree** (BR-PB-3) is feat-027 § 1's, over the project instead of a team:
the *tree set* is P's items of L **and of every level ranked below L**, the
«Задачи» level included, by conditions (1) and (3); nesting, the top-level rows,
the 64-deep bound and the depth-first order are feat-027's, because
`BacklogTree.Nest` is the same code. An item stands beneath its stored parent
exactly when that parent is in the set and its level ranks the same or higher;
a parent of a higher level than the page's, or in «Удалена», leaves the child a
top-level row of its own level. Every child of the project stands, whatever its
area or team.

**The order** (BR-PB-4) is the project's, one per project and level, kept apart
from every team's:

1. the items **placed** on this project backlog, by their places;
2. then the **unplaced** ones by creation time (`createdAt`), then by `number`
   (one project, one key; BR-MX-18). The «Задачи» level has no order of its own
   before `feat-023-tasks-backlog`: the tasks beneath a row stand by `number`,
   as feat-027 BR-T-6 has them.

Every item is unplaced on its project backlog until a reorder of that backlog
places it, so a created item is last (`createdAt` is highest) and an item that
changes team keeps its place: a team place and a project place are different
rows (§ 6). The order is not a field of the work item; a reorder changes no
item and not its `changedAt`. Team backlogs and boards keep their number
tie-break until `feat-040-team-surfaces-across-projects`.

## 2. Shapes

### 2.1 `BoardProject`

```json
{ "id": "0199b0a2-…", "name": "Альфа" }
```

The project as it stands now. The panel's `h1` reads `name` once the answer is
there (design § 3.3).

### 2.2 The rest

`BoardLevel` (mod-006 § 2.2), `BacklogRow` and `BacklogChild` (feat-027
§ 2.1–§ 2.2: the planning values, `parent`, `children`), and a board `column` and
its cards (mod-006 § 3.3, bug-004 D-3, feat-032 § 2: `checklist` on the card)
are **unchanged**: same fields, same meanings. `order` on a backlog row is the
row's 1-based position among the **top-level** rows, dense; a nested row has no
`order`. On a board card it is the item's position in the whole project order
of the level (gaps inside a column).

## 3. Routes

The base is `/api/organizations/{organizationId}/projects/{projectId}` (`…`
below).

### 3.1 `GET …/backlogs/{level}` — the project's backlog (US-1)

- **200**
  ```json
  { "project": BoardProject, "level": BoardLevel, "items": [ BacklogRow, … ] }
  ```
  `items` holds the top-level rows of § 1 in the project's order, each with the
  whole tree beneath it in one answer, no paging, `[]` for an empty backlog.
  Written with the backlog's deeper JSON limit (feat-027 § 1, D-9), as the
  team's is.
- **404** project, level (§ 0.3); 401/403 by the scoped check.

### 3.2 `POST …/backlogs/{level}/reorder` — move one item (US-2)

Body and positions exactly as mod-006 § 3.2: `{ "itemId", "position":
"top" | "bottom" | "after", "afterItemId" }`. In one transaction the server:

1. locks the project `FOR SHARE` and re-checks it is still active (a
   deactivation holds it `FOR NO KEY UPDATE`, so the two serialize), then locks
   the order row of (project, level) `FOR UPDATE`, creating it on the first
   reorder, so reorders of one project backlog and level run one at a time in
   arrival order (BR-PB-4, mod-006 BR-M4-25);
2. reads the project's order of § 1 as the previous reorder committed it;
3. refuses with 409 `boards.backlog.reorder_stale` if `itemId`, or with
   `"after"` the `afterItemId`, is not an item of this project backlog now —
   missing, malformed, unknown, of another project, another level, or in the
   Removed category (a **foreign id answers like an id that names nothing**, so
   nothing is learned about another project); nothing changes;
4. moves the one item, gives **every** item of the backlog (nested same-level
   items included) the place it now stands at and drops the places this
   backlog held for items that left it.

It writes the **project's** places only: no team place is read, written or
dropped, and a reorder of a team's backlog at the same moment fails neither
(different tables, different lock rows).

- **204** — no body; the panel reads § 3.1 again.
- **400** `boards.validation_failed` — `errors.position`:
  `boards.backlog.position_invalid`. A panel defect.
- **404** project, level (§ 0.3), and the project again when it was deactivated
  while the reorder waited for its lock.
- **409** `boards.backlog.reorder_stale` — step 3.

### 3.3 `GET …/boards/{level}` — the project's board (US-3)

- **200**
  ```json
  { "project": BoardProject, "level": BoardLevel, "columns": [ BoardColumn, … ] }
  ```
  `columns` are derived from the organization's process on every read
  (BR-MX-19, mod-006 § 3.3 as changed by bug-004 D-3), each card in the
  column its type's current state maps to, in the project's order of § 1; each
  card carries its checklist (feat-032) of live task, bug and test-case
  children whatever their team.
- **404** project, level (§ 0.3).

## 4. Error codes

**None new.** Reused unchanged: `organizations.organization.not_found`,
`organizations.project.not_found`, `organizations.access.forbidden`,
`boards.level.not_found`, `boards.validation_failed`,
`boards.backlog.position_invalid`, `boards.backlog.reorder_stale`
(`contracts/errors/boards.json`, `organizations.json`).

## 5. Summary of routes

| Method | Route | Code declared | Story |
|---|---|---|---|
| GET | `/api/organizations/{organizationId}/projects/{projectId}/backlogs/{level}` | `workitems.workitem.read` | US-1 |
| POST | `/api/organizations/{organizationId}/projects/{projectId}/backlogs/{level}/reorder` | `boards.backlog.reorder` | US-2 |
| GET | `/api/organizations/{organizationId}/projects/{projectId}/boards/{level}` | `workitems.workitem.read` | US-1 (head), US-3 |

OpenAPI tag `Boards`. The three join `OrganizationAddressedRoutes` (44 → 47
organization-addressed routes).

## 6. Storage and migration (ADR-0028 rule 9)

One additive migration, `AddProjectBacklogOrder`, two new tables and nothing
dropped or rewritten:

| Table | Key | Meaning |
|---|---|---|
| `project_backlog_orders` | (`project_id`, `backlog_level_id`) | the lock row of one project backlog; created on its first reorder; both keys `Restrict` (nothing beneath is purged, ADR-0026) |
| `project_backlog_places` | `work_item_id` | an item's place (≥ 1) on its project backlog; at most one per item; parent (`project_id`, `backlog_level_id`) `Restrict`; goes with its item (`Cascade`) |

`backlog_orders` and `backlog_places` (the team's) are not touched. Every
item is unplaced on its project backlog the moment the migration applies, which
is the order of § 1 point 2 — no data to move. `Down` drops the two tables.

## 7. Writes the panel reuses (unchanged)

| Need | Route |
|---|---|
| A card move, a checklist tick (US-3) | mod-003 `PATCH …/workitems/{identifier}` with `{ "stateId" }`; touches no place of either order, changes neither the item's project nor its team (BR-MX-9); 409 `workitems.workitem.state_not_allowed` for a state of another type |
| A row's «+» and inline entry, a card's «Добавить задачу» | feat-027 § 3 `POST …/workitems` with `parentId` and **no `teamId`**; the item takes its parent's area, so its parent's team; it writes no place on any order |
| A drop onto a row, «Изменить родителя…», «Убрать родителя» | feat-001 `PUT …/workitems/{identifier}/parent` |
| The item's card over the page (feat-029) | mod-003 `GET …/workitems/{identifier}` |
| «Новый рабочий элемент» | **none** on a project view (BR-PB-6) |

## 8. Query shape and speed (spec § NFR)

Reading a view takes a fixed number of statements, whatever the items,
parents, teams or depth: the project and the process (the same two as the
team's), **one** statement for the tree set or the level's members (no area
tree is read — a project view has no area condition), the lookups of the
assignees in batches of the directory's limit, and one statement for the
parents outside the set; the board adds one statement for all the cards'
checklists. A statement-count test pins the backlog and the board. A level of
5 000 items is served within 2 s (the «Эпики» and «Истории» trees of four
linked levels of 5 000 each, within 2 s), a reorder within 1 s.
