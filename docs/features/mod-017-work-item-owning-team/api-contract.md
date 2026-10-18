# API contract — mod-017-work-item-owning-team

Fixed at stage 4a (2026-10-17), before the code. From now on it changes only
with a note in [status.md](status.md) and a Log line; once stage 6 (frontend)
has started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan
is [plan-backend.md](plan-backend.md); the rules are [spec.md](spec.md)
(BR-OT-n; BR-MX-n are doc-012's) and the decisions marked D-n are the plan's § 1.

This task is **breaking** (doc-012 § Decomposition). It adds two routes, removes
two, and changes the shape or the meaning of the routes listed in § 2. It
registers **no permission code** (BR-OT-15), adds **two error codes** and
deprecates four (§ 4), and adds two migrations (§ 6). Earlier contracts that it
supersedes are named in § 9; everything not named there stands.

## 0. Rules shared by every endpoint

Exactly the earlier contracts' ([mod-002](../mod-002-organizations-projects-teams/api-contract.md) § 0,
[mod-006](../mod-006-boards-backlogs/api-contract.md) § 0,
[mod-003](../mod-003-work-item-core/api-contract.md) § 0,
[mod-016](../mod-016-organization-access/api-contract.md) § 0), plus the rules below.

### 0.1 Access (BR-OT-15)

No code is new. The scoped check (ADR-0027) runs before the body is read: 401,
then 404 `organizations.organization.not_found` for a person who is not a member
(an instance administrator included), then 403 `organizations.access.forbidden`
for a role without the route's code. «Администратор» and «Участник» hold the
work-item and board codes; **only «Администратор» holds `organizations.team.write`**.

| Route group | Code declared | «Читатель» |
|---|---|---|
| work-item reads, backlog and board reads, the filter | `workitems.workitem.read` | allowed |
| `POST` / `PATCH` work item (the team field included) | `workitems.workitem.write` | 403 |
| team reads (the list, one team, the default mark, the picker's and the filter's options) | `organizations.team.read` | allowed |
| `PUT …/default-team`, add, rename, delete a team, the deletion's count | `organizations.team.write` | 403 |
| team backlog reorder | `boards.backlog.reorder` | 403 |

Every route here is under `{organizationId}`, so `OrganizationAccessInventoryTests`
lists it (§ 5). A team, a filter value or a default named in a request, of
**another organization**, answers as an id that names nothing and changes
nothing (BR-OT-16).

### 0.2 Identifiers and the team in a body (BR-OT-2)

Ids are UUIDs, written lowercase in responses; in a path, a body or a query an id
is an untrusted string, accepted only in the canonical 8-4-4-4-12 form (either
letter case, not all zeros); anything else answers as an id that names nothing
(mod-002 § 0.2). A body field that holds an id is a JSON string or `null`; a
number or an object there fails binding with 400 `http.bad_request`.

**The owning team is sent in a wrapper, not as a flat `teamId` (D-2).** A flat
`null` would read as «not changed» under mod-003 § 2.4 ("absent, or `null`, is not
changed"), so the clearable fields use a wrapper object, as `assignee` and
`parent` already do:

```json
"team": { "teamId": "0199b0a2-…" }      a team of the item's project
"team": { "teamId": null }              «Без команды»
(absent)                                the field is left out
```

`"team": {}` is `{ "teamId": null }` (the key left out inside the wrapper means
none, as mod-003's `assignee` does). The flat `teamId` of mod-006 § 7 on a create
**is retired**: a body that still carries it has it ignored (unknown properties
are ignored, as everywhere).

**How a team named in a write is answered** (create with no parent, save,
`PUT …/default-team`; BR-OT-2, BR-OT-4). The first row that applies:

| The named team | Answer |
|---|---|
| a string that is not an id; no such team; a team of **another organization**; a **deleted** team; a team of a **deactivated** project | 404 `organizations.team.not_found` «Команда не найдена.» — identical status and body for each (ADR-0026 rule 5: a deactivation is never revealed) |
| an active team of **another active project** of the organization | 409 `organizations.team.not_in_project` «Команда не участвует в проекте.» |
| an active team of this project | accepted |

The refusal stores nothing of the request and consumes no number. The address's
not-founds and the 403 come before it (§ 0.4).

### 0.3 Not found, from the outside in

As mod-002 § 0.3, with the levels this task uses:

| Level | `errorCode` | `detail` |
|---|---|---|
| organization | `organizations.organization.not_found` | «Организация не найдена.» |
| project | `organizations.project.not_found` | «Проект не найден.» |
| team (the address) | `organizations.team.not_found` | «Команда не найдена.» |
| work item | `workitems.workitem.not_found` | «Рабочий элемент не найден.» |
| level | `boards.level.not_found` | «Уровень бэклога не найден.» |

A deleted team's address answers `team.not_found` for every route under it, its
members', its backlog's, its board's and its count (BR-OT-12).

### 0.4 The order of answers

Reads: 401, organization 404, 403, project 404, then the rest of the address
(team, item, level). A **filter value never answers an error** (§ 3).

`POST …/workitems`: 401, organization 404, 403 (and, with `parentId`, the second
code `workitems.link.write` first of all), project 404, 400 values (every field
reason at once), type 404, **the replay (200)**, the team (§ 0.2; **not read
when `parentId` is present**, BR-OT-3), the parent (404, then 409), then the
creation. The replay precedes the team: a repeated submission answers 200 with
its item as it is now, owned by no team if its team has been deleted since.

`PATCH …/workitems/{identifier}`: 401, organization 404, 403 (the second code
first when `parent` is sent), project 404, item 404, 400 values, area 404, **the
team**, new parent 404, then 409 (state, assignee, the parent's 409s). A
refusal throws before anything is flushed: no field of the request is stored.

`PUT …/default-team`: 401, organization 404, 403, project 404, then the team
(§ 0.2).

`DELETE …/teams/{teamId}`: 401, organization 404, 403, project 404, team 404,
then 409 `last_team_cannot_be_deleted`.

### 0.5 Error body

`application/problem+json` as everywhere (mod-002 § 0.5). `errors` appears only
with `workitems.validation_failed` or `organizations.validation_failed`.

## 1. Shapes

### 1.1 `WorkItemTeam`

```json
{ "id": "0199b0a2-…", "name": "MARS" }
```

The owning team **as it stands now** (a rename shows at once). `null` where the
item has none («Без команды»). A deleted team never appears: its items have no
owning team (BR-OT-12).

### 1.2 `WorkItem` and `WorkItemListItem` (mod-003 § 1.1, § 1.2)

Both gain **`team`: `WorkItemTeam | null`**, beside `area`. `area` is unchanged:
a classification path until `mod-018-area-workspace` (BR-OT-10). The create, the
read and the save answer it.

### 1.3 `Team` (replaces mod-002 § 3 `Team`)

```json
{ "id": "…", "projectId": "…", "name": "MARS", "defaultMark": "named", "isMember": true }
```

| Field | Meaning |
|---|---|
| `defaultMark` | `"named"`: the project's settings name this team; `"onlyTeam"`: no team is named and this is the project's only active team; `null`: neither (BR-OT-4). At most one team of a project carries a mark. |
| `isMember` | the **caller** is in the team (mod-016 § 3.3); **the list only** — the single read, the add and the rename answer `false` |

**Removed:** `isFirstTeam` and `defaultArea`. There is no first team and a team
has no area (BR-OT-10, BR-OT-11). The list is ordered **by name in Russian
collation (letter case does not decide), then by id** — no team comes first, and
the panel renders the order as received.

### 1.4 `BoardTeam` (replaces mod-006 § 2.1)

```json
{ "id": "…", "name": "MARS" }
```

`defaultArea` is removed.

### 1.5 `BacklogItem`, `BacklogRow`, `BacklogChild`, board card

Every row of a backlog (top level and nested) and every board card (mod-006
§ 2.3, feat-027 § 2.1–§ 2.2, feat-032 § 2.1) gains **`team`: `WorkItemTeam | null`**.
A checklist entry (`ChecklistChild`, feat-032 § 2.2) does not.

### 1.6 `AreaNode` and `Area` (replace mod-002 § 4.1)

`AreaNode`: `id`, `parentId`, `name`, `path`, `depth`. **Removed:** `ownerTeamId`,
`resolvedOwner`, `ownerInherited`, `defaultOfTeam`. `Area` (the answer of a
create, a rename and a move): `id`, `projectId`, `parentId`, `name`; `ownerTeamId`
is removed.

## 2. Routes

The base is `/api/organizations/{organizationId}/projects/{projectId}` (`…`
below).

### 2.1 `GET …/workitems?page=1&teamId=…` — the project's list (US-5; BR-OT-8, BR-OT-9)

mod-003 § 2.1 plus the filter of § 3: `teamId` repeated, a value is a team's id
or `none`. `count` in `{ "count", "data" }` is the number of items that match.
The rows are `WorkItemListItem` with `team`. Order, paging and the 50 rows a
page are unchanged; a page past the last answers an empty `data` with the right
`count`. A filter change in the panel returns to page 1.

- **200**, **400** `workitems.validation_failed` (`page`), **404** organization,
  project. A filter value never answers an error.

### 2.2 `POST …/workitems` — create (US-2; BR-OT-2, BR-OT-3)

mod-003 § 2.2 with the body field **`team`** (§ 0.2) and without mod-006's
`teamId`:

| Case | The item's owning team |
|---|---|
| **`parentId` present** (any surface) | the **parent's owning team as stored when the child is saved**; if the parent has none, the **project default team** as it stands then, else none. A `team` in the same body is **not read**: a child's creation is never refused for a team. |
| no parent, `team` present | that value (a team of the project, or none), refused as § 0.2 |
| no parent, `team` absent | the **project default team** as stored when the item is saved (BR-OT-4), else none |

The project default team is the team the project's settings name, else the
project's only active team, else none. The panel's dialog sends the team it
shows (the team of the backlog or board it was opened on, else the default, or
the user's change) and a row's «+» and a card's «Добавить задачу» send `parentId`
and no `team`. The item's **area** is the project's root area for a new item and
the parent's area as stored for a child (BR-OT-10, OQ-4); it is no longer a team
rule.

Everything else is mod-003 § 2.2 and feat-027 § 3, unchanged: the type, the
replay (200), the parent's 404 and 409s, the number from the project's one
sequence. A refusal consumes no number.

- **201** `WorkItem` (with `team`), **200** a replay, **400**, **403**, **404**
  (organization, project, type, parent, **`organizations.team.not_found`**),
  **409** (**`organizations.team.not_in_project`**; the parent's 409s).

### 2.3 `GET …/workitems/{identifier}` — one item

mod-003 § 2.3; the answer is `WorkItem` with `team`.

### 2.4 `PATCH …/workitems/{identifier}` — save the form (US-1; BR-OT-1, BR-OT-2)

mod-003 § 2.4 with the body field **`team`** (§ 0.2). A save that leaves it out
keeps the stored value, whoever changed it meanwhile (BR-M3-25). A value equal to
the stored one changes nothing and does not move `changedAt`. A changed team:

- changes the team and `changedAt` and **nothing else** of the item: not its
  project, identifier, state, assignee, estimates, «Область», parent or children;
  no child follows its parent's team or the reverse (BR-OT-1);
- **drops the item's team place in the same write**; the item lands unplaced
  below every placed item on the new team's backlog, with no memory of a former
  place; the project place is untouched (BR-OT-6);
- is refused, with nothing of the request stored, as § 0.2 says.

- **200** `WorkItem`, **400**, **403**, **404** (… `organizations.team.not_found`),
  **409** (`organizations.team.not_in_project`; mod-003's and feat-001's).

### 2.5 Team views (US-4; BR-OT-5, BR-OT-6)

Routes, answers and error codes are mod-006's (§ 3.1–§ 3.3, feat-027, feat-032);
what changes:

**What a team's backlog and board hold (replaces mod-006 § 1 point 3).** For
team T of project P (both active) and level L, exactly the work items that, at
the moment of the read, (1) belong to P; (2) have a type of L; (3) have **T as
their owning team**; (4) are in a state outside the Removed category. An item's
area, its owner and its moves decide nothing. The tree (feat-027 § 1) is built
over T's items only: a child that another team owns is not beneath a row of T's;
a T child of a parent that T does not own is a top-level row. The board holds
the same items as the backlog; a card's checklist is **not** narrowed by team
(feat-032 BR-C-1).

- `GET …/teams/{teamId}/backlogs/{level}` and `…/boards/{level}`: `team` is
  `BoardTeam` (§ 1.4); rows and cards carry `team`. No `teamId` filter is read
  on a team's routes (the team is the filter).
- **Order.** A team place exists only while the team owns the item (§ 2.4,
  § 6). Unplaced items follow the placed ones, by number (until
  `feat-040-team-surfaces-across-projects`).
- `POST …/teams/{teamId}/backlogs/{level}/reorder` — body, positions, 204/400/404
  as mod-006 § 3.2. What the server does, in one transaction, in this lock order
  (plan § 7): the team `FOR SHARE` and re-checks it is active (404 if it was
  deleted while the reorder waited); the backlog's **member items `FOR KEY
  SHARE`**; the backlog's order row `FOR UPDATE`; the members read again under
  those locks; **409 `boards.backlog.reorder_stale`** if `itemId`, or with
  `"after"` the `afterItemId`, is not a member of this backlog now — an item
  that has changed its owning team before or while the reorder ran included
  (BR-OT-13 (3)); then every remaining member gets its place and the places this
  backlog held for items that left it (by state) are dropped. A save that moves a
  member to another team and a reorder end in one order: reorder first, the save
  drops the place the reorder wrote; save first, the reorder is refused if it
  moves or follows that item, else it completes on the members that remain and
  writes no place for the one that left. Neither ends in a server error.

### 2.6 Project views and the team filter (US-5; BR-OT-5, BR-OT-8)

`GET …/backlogs/{level}`, `GET …/boards/{level}` (feat-039 § 3.1, § 3.3) hold
**every item of the project of the level, whatever its team and none included**
(feat-039 § 1, unchanged); rows and cards carry `team`. They gain the filter of
§ 3: `…/backlogs/stories?teamId=<id>&teamId=none`.

- The tree is built over the **matching items only**: a child whose parent does
  not match is a top-level row; `order` numbers the visible top-level rows 1..n
  in the project's order; the board's columns and their cards follow the filter;
  **a card's checklist is not narrowed**.
- `POST …/backlogs/{level}/reorder` (project; feat-039 § 3.2) takes **no filter**:
  it is made against the full list, so the items a filter hid keep their relative
  order (BR-OT-8). A team's order and the project's never affect each other.
- **200**, **404** project, level as feat-039 § 0.3. A filter value never answers
  an error.

### 2.7 Teams (US-3, US-6, US-7; BR-OT-4, BR-OT-11, BR-OT-12)

Routes under `…/teams`, answers `Team` (§ 1.3) unless stated.

| Route | Change |
|---|---|
| `GET …/teams` | the shape of § 1.3; ordered by name (§ 1.3); `isMember` kept |
| `GET …/teams/{teamId}` | the shape of § 1.3 |
| `POST …/teams` | body `{ "name" }` as before. **Creates no area** (BR-OT-11): the team is created alone. Errors: 400 `name`; 409 `organizations.team.name_taken` only — **`organizations.area.name_taken` can no longer come from adding a team** |
| `PUT …/teams/{teamId}/name` | `Team`; no area is touched |
| `DELETE …/teams/{teamId}` | below |
| `PUT …/teams/{teamId}/default-area` | **removed** (answers as a route that does not exist: 404) |

**`DELETE …/teams/{teamId}` — delete a team (BR-OT-11, BR-OT-12).** No body,
**204**. After the address and the team (404), and **409 `organizations.team.last_team_cannot_be_deleted`
«Нельзя удалить единственную команду проекта.»** when it is the project's last
active team (the interim guard, lifted by `feat-028-org-level-teams`), in one
operation over what the team owns when the operation runs:

1. every item it owns, in any state, gets **no owning team** and shows the
   operation's time as `changedAt`; its project, identifier, assignee,
   estimates, state, «Область» and project-backlog place are untouched;
2. the team's backlog places are deleted;
3. the project's default-team setting is cleared if it named this team;
4. the team's memberships end (mod-016 BR-M1-36) — the people stay in the
   organization;
5. the team is deleted: it leaves every list, picker and filter, its name is
   free, and every request that addresses it answers `team.not_found`. No restore.

Areas, projects and other teams' items are untouched. The operation is all or
nothing; 5 000 owned items complete within 5 s. Two deletions that would leave a
project without a team end with one done and one refused 409; a write that names
the team (§ 0.2, a reorder) either commits first and is taken with the rest, or
comes after and is refused `team.not_found`; a **child created beneath one of
its items** is never refused for the team (§ 2.2) and ends reassigned or on the
project default (BR-OT-13).

### 2.8 `PUT …/default-team` — name the project's default team (US-3; BR-OT-4)

New. Requires `organizations.team.write`.

```json
{ "teamId": "0199b0a2-…" }       name this team
{ "teamId": null }  or  {}       «Не выбрана»: the setting is cleared (D-7)
```

The team is judged as § 0.2 (a team of the project, else 404 / 409). Naming the
current default again changes nothing. It decides the starting value of a
creation: no item moves, and the views, the filters and the orders do not read
it (BR-OT-4). (Amended by mod-020, 2026-10-18, OQ-8 B: the switcher reads it too,
as the team it opens on when no team is chosen, BR-OT-11; the endpoint is
unchanged, and the panel reads the mark from `GET …/teams`.) The setting survives a
deactivated project (the project answers 404 meanwhile) and is cleared by the
deletion of the team it names.

- **204**, no body. The panel reads `GET …/teams` again for the marks.
- **401**, **403**, **404** organization, project, `organizations.team.not_found`,
  **409** `organizations.team.not_in_project`.

### 2.9 `GET …/teams/{teamId}/owned-items/count` — what a deletion would reassign (US-7; BR-OT-12)

New. Requires `organizations.team.write`. Read **when the confirmation opens**.

```json
{ "count": 5 }
```

The number of items the team owns at this moment, in **any state** (Removed
included). An item given to the team afterwards is not in a count already read,
and is reassigned all the same.

- **200**, **401**, **403**, **404** organization, project, team (a deleted team
  included).

### 2.10 Areas (US-6; BR-OT-10)

| Route | Change |
|---|---|
| `GET …/areas` | `AreaNode` of § 1.6; pre-order, paths and depths unchanged |
| `POST …/areas` | body `{ "parentId", "name" }`; **`ownerTeamId` is retired** (ignored if sent). 201 `Area`. Errors: 400 `name`; 404 `area.not_found` (the parent); 409 `area.name_taken` |
| `PUT …/areas/{areaId}/name`, `PUT …/areas/{areaId}/parent` | `Area` of § 1.6; no team is touched |
| `PUT …/areas/{areaId}/owner` | **removed** (404, as a route that does not exist) |
| `DELETE …/areas/{areaId}` | refused only for children or items: 409 `organizations.area.not_empty` «Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области»; the root: 409 `organizations.area.root_is_fixed`. **`organizations.area.is_team_default` can no longer be answered.** |

Moving, renaming, deleting or re-saving an area never changes an item's team.

### 2.11 Projects

`POST …/projects` (mod-002 § 2.3) is unchanged on the wire. The project still
comes with **one team named after it, its creator its only member, and the root
area** (BR-OT-11, until `feat-028-org-level-teams` and `mod-018-area-workspace`);
the team is an ordinary one: not first, deletable once the project has another,
and the root area is **owned by nobody**.

## 3. The team filter — the first dimension of the shared filter specification (BR-OT-8)

One meaning on the three surfaces that take it: the project's list (§ 2.1) and the
project's backlog and board (§ 2.6).

| Item | Rule |
|---|---|
| Parameter | `teamId`, **repeated**: `?teamId=<id>&teamId=<id>&teamId=none` |
| A value | a team's id, or the literal `none` («Без команды»); an item matches when it is owned by **any** chosen team, or owned by none when `none` is chosen |
| Absent | no filter; the surface is as before |
| Present but nothing in it names a team of the project | **selects nothing**: a malformed value, an unknown id, a **deleted** team's, a team of **another project** and a team of **another organization** answer alike — status and body identical, no item, total 0 (BR-MX-1, BR-OT-16). Nothing is validated and nothing is disclosed. |
| Options | the panel's, from `GET …/teams` (by name) and `none` last; no counts |
| What it narrows | the view only; no item, team or place changes; the tree is built over matching items; totals follow it; a card's checklist and a reorder are not narrowed |
| Life | for the visit, and in the page's address as this parameter (mod-020, OQ-9 B: a link carries it); the server stores nothing (`feat-013-board-backlog-filter` remembers) |

The request line bounds the number of values (Kestrel's 8 KB line); no cap is
added. `feat-003-work-item-search` and `feat-013-board-backlog-filter` add other
dimensions and leave this one's meaning.

## 4. Error codes

From `contracts/errors/organizations.json` (Russian and Kazakh mandatory; the
new Kazakh texts carry `kkNeedsReview`). `docs/error-codes.md` is regenerated.

| Code | Status | Text | Use here |
|---|---|---|---|
| **new** `organizations.team.not_in_project` | 409 | «Команда не участвует в проекте.» | an active team of another active project in a create, a save or `PUT …/default-team` (§ 0.2). `feat-028` reuses it for an unlink. |
| **new** `organizations.team.last_team_cannot_be_deleted` | 409 | «Нельзя удалить единственную команду проекта.» | `DELETE …/teams/{teamId}` on the project's last active team. `feat-028` retires it. |
| `organizations.team.not_found` | 404 | «Команда не найдена.» | the address of a deleted team; a team named in a write or `default-team` (§ 0.2) |
| `boards.backlog.reorder_stale` | 409 | «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.» | an item that changed team before the reorder or while it ran (§ 2.5) |
| `organizations.access.forbidden` | 403 | «Недостаточно прав» | a reader's write; a non-administrator's team or default-team write or count |
| `organizations.area.name_taken` | 409 | as registered | kept for areas; **no longer from adding a team** |
| **deprecated** `organizations.team.default_area_not_owned`, `…team.first_team_default_fixed`, `…team.first_team_cannot_be_deleted`, `organizations.area.is_team_default` | - | - | retired with BR-M1-14 and BR-M1-18. Per ADR-0005 rule 3 and `contracts/README.md` a retired code is **marked `"deprecated": true` in the catalog and stays taken**; its C# constant is removed (the spec's "removes" reads so). |

Unchanged and reused: `organizations.organization.not_found`,
`organizations.project.not_found`, `workitems.workitem.not_found`,
`workitems.validation_failed`, `workitems.link.*`, `boards.level.not_found`,
`boards.validation_failed`, `boards.backlog.position_invalid`,
`organizations.area.not_empty`, `organizations.area.root_is_fixed`,
`organizations.team.name_taken`.

## 5. Summary of routes

Organization-addressed routes: **47 → 47** (two removed, two added).

| Method | Route | Code declared | Story | Change |
|---|---|---|---|---|
| GET | `…/workitems` | `workitems.workitem.read` | US-5 | filter, `team` |
| POST | `…/workitems` | `workitems.workitem.write` | US-2 | `team`, defaults |
| GET | `…/workitems/{identifier}` | `workitems.workitem.read` | US-1 | `team` |
| PATCH | `…/workitems/{identifier}` | `workitems.workitem.write` | US-1 | `team` |
| GET | `…/teams/{teamId}/backlogs/{level}` | `workitems.workitem.read` | US-4 | membership, `team` |
| POST | `…/teams/{teamId}/backlogs/{level}/reorder` | `boards.backlog.reorder` | US-4 | locks, stale |
| GET | `…/teams/{teamId}/boards/{level}` | `workitems.workitem.read` | US-4 | membership, `team` |
| GET | `…/backlogs/{level}` | `workitems.workitem.read` | US-5 | filter, `team` |
| GET | `…/boards/{level}` | `workitems.workitem.read` | US-5 | filter, `team` |
| GET | `…/teams`, `…/teams/{teamId}` | `organizations.team.read` | US-3, US-7 | shape |
| POST | `…/teams` | `organizations.team.write` | US-6 | no area |
| PUT | `…/teams/{teamId}/name` | `organizations.team.write` | US-6 | shape |
| DELETE | `…/teams/{teamId}` | `organizations.team.write` | US-7 | reassign, guard |
| **PUT** | **`…/default-team`** | `organizations.team.write` | US-3 | **new** |
| **GET** | **`…/teams/{teamId}/owned-items/count`** | `organizations.team.write` | US-7 | **new** |
| GET, POST, PUT, DELETE | `…/areas…` | as before | US-6 | shapes |
| ~~PUT~~ | ~~`…/teams/{teamId}/default-area`~~ | | | **removed** |
| ~~PUT~~ | ~~`…/areas/{areaId}/owner`~~ | | | **removed** |

OpenAPI tags are unchanged (`Organizations`, `WorkItems`, `Boards`). In
`OrganizationAddressedRoutes.All` the two removed rows go and the two new rows
join, each with its code, so the inventory test and the containment test follow.

## 6. Storage and migrations (BR-OT-14; ADR-0028 rules 5, 9, 12)

Two additive migrations on top of the merged ones; none is edited. The human
waived preservation (doc-012 § Existing data, OQ-4 A).

**`AddWorkItemOwningTeam` — additive, drops nothing.**

| Change | Meaning |
|---|---|
| `work_items.owning_team_id` (uuid, null) | the owning team |
| FK `(project_id, owning_team_id)` → `teams (project_id, id)`, `Restrict` | the team is a team of the item's project; `NULL` = «Без команды» (MATCH SIMPLE) |
| unique `(id, owning_team_id)` | the target of the place key below |
| index `(project_id, owning_team_id, type_id)` | a team's views |
| `projects.default_team_id` (uuid, null) + FK `(id, default_team_id)` → `teams (project_id, id)`, `Restrict` | the default-team setting; a team of the project |
| data | **every item takes the team that owned its area, resolved through the tree** (the root's owner when no area up the tree is owned); «Изменён» does not move; no project names a default |

**`RetireAreaOwnership` — the data drop.**

| Change | Meaning |
|---|---|
| delete the **stale team places** | `backlog_places` rows whose team is not the item's owning team: places the area edits left behind, already unread (a place is read only on the backlog of the item's owner). Kept: every place that names the item's mapped owner. |
| FK `(work_item_id, team_id)` → `work_items (id, owning_team_id)` | a team place names the item's current owning team (ADR-0028 rule 5); added in SQL, because EF cannot declare an alternate key over a nullable column |
| drop `areas.owner_team_id`, `areas.default_of_team_id` (with their FKs, `ix_areas_default_of_team_id`, `ck_areas_default_owned`, `ck_areas_root_owned`) and `teams.is_first_team` (with `ix_teams_project_id_first_team`, `ck_teams_first_team_active`) | the three markers |

**Data dropped, named in the MR:** the stale team places, and the three markers.
No item, area, team, project, project place or other row is dropped. Each
migration logs its counts (`RAISE NOTICE`): items mapped, items left without a
team, stale team places deleted, markers dropped. Both are reversible (`Down`
restores the schema and what the old checks require, not the dropped data).

## 7. In-process contracts that change (ADR-0028 rules 7, 8)

Not HTTP. All at their module's root namespace, scoped, no access check, no
event.

**`IOrganizationStructure`** (`Mustask.Application.Features.Organizations`):

```csharp
public sealed record ProjectInfo(
    Guid Id, Guid OrganizationId, string Name, string Key, ProjectStatus Status,
    bool IsReachable, Guid? DefaultTeamId, Guid RootAreaId,   // FirstTeamId removed; the resolved default added
    string CreatedBy, DateTimeOffset CreatedAt);

public sealed record TeamInfo(
    Guid Id, Guid ProjectId, Guid OrganizationId, string Name, TeamStatus Status,
    bool IsReachable);                                         // IsFirstTeam and DefaultAreaId removed
```

`FindAreaOwnerAsync` and `AreaOwnerInfo` are **removed**. `ListActiveTeamsAsync`
stays and orders by name (letter case does not decide), then id. `RootAreaId`,
`TeamInfo.ProjectId` and `ListActiveTeamsAsync` stay until `mod-018` and `feat-028`.
`DefaultTeamId` is **resolved in one place**: the project's named team, else its
only active team, else `null` (BR-OT-4).

**`IWorkItemTeamOwnership`** (new, `Mustask.Application.Features.WorkItems`),
called by team deletion and the count in the caller's transaction, each call
locking its own rows in rule 8's order:

```csharp
Task<int>  CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken ct);            // any state
Task       LockOwnedAsync(Guid projectId, Guid teamId, CancellationToken ct);             // the owned items FOR UPDATE, by id
Task<int>  ReleaseOwnedAsync(Guid projectId, Guid teamId, DateTimeOffset now, CancellationToken ct); // owning team := none, changed_at := now
```

**`ITeamBacklogPlaces`** (new, `Mustask.Application.Features.Boards`): the team
places are the boards module's tables, so the other modules ask it to drop them.

```csharp
Task DropForItemAsync(Guid workItemId, CancellationToken ct);   // a save that changes the owning team
Task DropForTeamAsync(Guid teamId, CancellationToken ct);       // a team's deletion
```

## 8. Query shape and speed (spec § NFR)

Each read takes a fixed number of statements, whatever the rows, teams or depth:
the list is the count, the page (the team joined in it), the area nodes and the
assignees; a team's backlog or board no longer reads the area tree (one
statement fewer) and a project's is unchanged; a filter adds **none**; the teams
list is the teams and the project's named default; the count is one statement;
the deletion is the lock of the project's teams, the team, the owned items'
lock, the places' delete, the release, the default's clear, the memberships' end
and the team's status. A statement-count test pins each (plan § 9.4). A level of
5 000 items is served within 2 s, the list's page of 50 within 2 s on a project of
5 000 items filtered or not, and a deletion of a team that owns 5 000 items
within 5 s.

## 9. What this contract supersedes in earlier ones (P-10)

| Contract | Sections |
|---|---|
| [mod-002](../mod-002-organizations-projects-teams/api-contract.md) | § 2.3 (the first team and the owned root), § 3 `Team` and §§ 3.1–3.6 (no first team, no default area, ordered by name, add without an area, delete reassigns), § 4.1 `AreaNode`, § 4.2 (no owner), §§ 4.4–4.6 (move and rename shapes, no owner route, the delete guard), § 5, § 6 (four codes deprecated, two added), § 7 `IOrganizationStructure` |
| [mod-003](../mod-003-work-item-core/api-contract.md) | §§ 1.1, 1.2 (`team`), 2.1 (the filter), 2.2 (`team`, the defaults; `teamId` retired), 2.4 (`team`), § 4 (the area and the team), § 6 |
| [mod-006](../mod-006-boards-backlogs/api-contract.md) | § 1 (membership by owning team), § 2.1 (`BoardTeam`), § 2.3 (`team`), § 3.2 (the locks and the stale rule), § 7 (the flat `teamId` retired) |
| [mod-016](../mod-016-organization-access/api-contract.md) | §§ 3.1, 3.3 (`Team` without `isFirstTeam`; the list's order), § 4.1 rows 19 and 25 and the route inventory |
| [feat-027](../feat-027-backlog-tree-editing/api-contract.md) | a child's team (BR-T-10: the area stays the parent's, the team is the parent's); BR-T-1, BR-T-4, BR-T-5 over owned items |
| [feat-039](../feat-039-project-board/api-contract.md) | § 3.1, § 3.3 (the filter, `team`); BR-PB-6 lifted (the create command on a project view) |
