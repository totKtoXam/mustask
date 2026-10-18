# API contract — feat-040-team-surfaces-across-projects

Fixed at stage 4a (2026-10-18), before the code. From now on it changes only with a note in [status.md](status.md) and
a Log line; once stage 6 (frontend) has started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan is
[plan-backend.md](plan-backend.md). Spec: [spec.md](spec.md) (BR-TS-n), stories US-1..US-7.

This line **moves** the team's backlog, reorder and board from the project's address to the team's, **retires** the three
project-nested routes with no alias, adds a repeated query parameter `projectId` to the two reads, and adds `projects`
(the response) and `project` (every row and card) to the team views. Two **additive** fields on existing responses
complete it (§ 4). It registers **no permission code and no error code** (BR-TS-13) and creates **no table**; one index
(§ 9). Merged contracts it restates, not edits: mod-006 §§ 2-3 and 5, mod-017 § 3 (the filter's meaning), feat-027 §§ 1-2,
feat-032 § 2, feat-039 §§ 1-3, feat-028 §§ 1.2, 2.1.

## 0. Rules shared by every endpoint

Exactly mod-006's (its api-contract § 0) with the **project level taken out** of the team routes.

### 0.1 Access

- **Authentication.** `Authorization: Bearer <access token>` from realm `mustask`; none, or an invalid or expired one:
  401 `http.unauthorized` before any other check.
- **Organization codes** (ADR-0027, mod-016 § 0.2). The two reads declare `workitems.workitem.read`, the reorder
  `boards.backlog.reorder` (`mod-019-scoped-access` rescopes them). The scoped check runs before the body is read: 401,
  then 404 `organizations.organization.not_found` for a person outside the organization (an instance administrator with
  no membership included), then 403 `organizations.access.forbidden` for a role without the code.

| Role | Reads | Reorder |
|---|---|---|
| «Администратор», «Участник» | yes | yes |
| «Читатель» | yes | 403, before any not-found of the team |

- **Tenant isolation** (tech-011 m3). A team, project or item id of another organization answers as an id that names
  nothing, on every route here (§ 8.2).

### 0.2 Identifiers and the level

As mod-006 § 0.2: every id is a UUID written lowercase in responses; an id in a path, a query or a body is an untrusted
string, accepted only in the canonical 8-4-4-4-12 form (either letter case, not all zeros), and anything else answers
like an id that names nothing. `{level}` is `epics`, `features` or `stories`, mapped to the organization's process by
kind and rank, never by name; `tasks` and every other value answer `boards.level.not_found`.

### 0.3 Not found, from the outside in

| Level | When | `errorCode` | `detail` |
|---|---|---|---|
| organization | malformed, unknown or deactivated (the scoped check) | `organizations.organization.not_found` | «Организация не найдена.» |
| team | malformed, unknown, **deleted**, of another organization | `organizations.team.not_found` | «Команда не найдена.» |
| level | not one of § 0.2's three, or the process has no such level | `boards.level.not_found` | «Уровень бэклога не найден.» |

There is **no project level** at the team's address: no route here takes a project, and `organizations.project.not_found`
cannot occur on it. A team linked to no project, or to deactivated projects only, is a team: it answers 200 with no item
(§ 1) and, for a reorder, 409 (§ 3.2).

### 0.4 The order of answers (mod-016 § 0.1)

Reads: 401, organization 404, 403, team 404, level 404.
Reorder: 401, organization 404, 403, team 404, level 404, then 400 `boards.validation_failed` (the position), then, under
the locks, the team's not-found again if it was deleted meanwhile, then 409 `boards.backlog.reorder_stale`.

### 0.5 Error body

`application/problem+json` as everywhere (mod-002 § 0.5); `errors` is present only with `boards.validation_failed`:
`{ "position": [ { "code", "detail" } ] }`.

## 1. What a team's backlog and board hold (BR-TS-2)

For team T (active, in an active organization) and level L, exactly the work items that, **at the moment of the read**:

1. are **owned by T** (`owning_team_id`); a team owns items only in the projects it is linked to (feat-028 BR-OL-6, held
   by the database), so no link is read;
2. belong to an **active project of the caller's organization** (a deactivated project's items leave the view and come
   back with their places when it is restored, ADR-0028 rule 9);
3. have a type of L;
4. are in a state outside the Removed category (the one definition of "deleted", ADR-0030 rule 2).

Nothing else narrows it but the filter (§ 3.1): not the item's project, area or sprint. An item is on one team's
views only and on exactly one level; the board holds the same items as the backlog.

**The tree** is feat-027 § 1's over T: the *tree set* is T's items of L **and of every level ranked below L**, the
«Задачи» level included, by conditions 1-4. Beneath a row stand **T's own children only** (mod-017 BR-OT-5); a child of
another team, or in «Удалена», is no entry. A parent and its child are always in one project, so a tree never crosses
projects. `BacklogTree.Nest` is the same code as the project's.

**The order** (BR-TS-6) is the team's, one per team and level across its projects, kept apart from every project's own
order:

1. the items **placed** on this team backlog, by their `position` (placed first);
2. every other item, and every placed item of an **equal position** (feat-028's per-project reorders left them), by
   creation time (`createdAt`), then **project key by character code** (ordinal), then identifier **number as a number**.
   The rule is the same for the «Задачи» level on a team's view: the tasks beneath a row stand by it too (a team's tasks
   of two projects would otherwise tie on number).

Every item is unplaced on the team backlog until a reorder places it, so a created item is last (`createdAt` is highest).
A reorder writes a dense `1..n` over the whole backlog, so equal positions end with the first reorder. The project
orders (`project_backlog_places`) are never read or written here.

## 2. Shapes

### 2.1 `WorkItemProject` (new)

```json
{ "id": "0199b0a2-…", "name": "Beta" }
```

The item's project **as it stands now** (a rename shows at once). Every item has one.

### 2.2 `BoardTeam` and `projects`

`BoardTeam` is `{ "id", "name" }` (mod-017 § 1.4), the team as it stands now. `projects` is new on both team reads:
**T's linked, active projects** (`{ "id", "name" }`, `BoardProject` of feat-039 § 2.1), **by name** in Russian collation
(letter case not deciding), then id; `[]` for a team linked to no active project. It feeds the filter's options, the
empty state (a) and the create command (the panel offers it only when `projects` is non-empty). Same statement set as
the team's own `GET …/teams/{teamId}/projects` (feat-028 § 2.2), which stays.

### 2.3 Rows, children, cards

`BacklogRow` and `BacklogChild` (feat-027 § 2.1-§ 2.2), and a board card (mod-006 § 3.3, feat-032 § 2) gain one field:

```json
{ "id": "…", "identifier": "BET-US-3", …, "team": { "id": "…", "name": "MARS" } | null,
  "project": { "id": "…", "name": "Beta" }, … }
```

`project` stands right after `team`, on **every** row, child and card of **every** backlog and board — team and project
views alike (BR-TS-3). A card's `checklist` entries (feat-032 § 2.2) and a `parent` cell (`WorkItemRef`) are unchanged:
they are always the card's own project. Every other field and meaning is unchanged; `order` on a backlog row is the 1-based
position among the **top-level** rows, dense; on a board card it is the item's position in the whole order of the level.

## 3. Routes

The base is `/api/organizations/{organizationId}/teams/{teamId}` (`…` below). The OpenAPI tag is `Boards`.

### 3.1 `GET …/backlogs/{level}` — a team's backlog (US-1, US-2)

Query: **`projectId`**, repeated, optional — the project filter (BR-TS-8).

- **200**
  ```json
  { "team": BoardTeam, "projects": [ BoardProject, … ], "level": BoardLevel, "items": [ BacklogRow, … ] }
  ```
  `items` holds the top-level rows of § 1 in the team's order, each with the whole tree beneath it in one answer, no
  paging, `[]` for an empty backlog. Written with the backlog's deeper JSON limit (feat-027 § 1), as before.
- **The filter's meaning** (the shared specification, mod-017 § 3, second dimension): an item matches when its project is
  any of the values. Absent or empty is no filter. Each value is a canonical project id or is **ignored**; a filter that
  is set but names no project the team's items are in (a malformed value, an unknown id, a deactivated or unlinked
  project's, another organization's) **selects nothing**, and none is an error. Beside a valid value an invalid one adds
  nothing. It narrows the tree set in the same statement (the tree is built over matching items; a parent outside the
  set is read as any parent outside it, unfiltered), changes no item, team or place, and a reorder ignores it (§ 3.2).
  `projects` is **not** narrowed by it.
- **404** team, level (§ 0.3); 401/403 by the scoped check.

### 3.2 `POST …/backlogs/{level}/reorder` — move one item (US-3)

Body and positions exactly as mod-006 § 3.2: `{ "itemId", "position": "top" | "bottom" | "after", "afterItemId" }`. No
query. In one transaction the server:

1. locks the team `FOR SHARE` (organization-checked) and re-checks it is active (a deletion holds it `FOR NO KEY
   UPDATE`, so the two serialize); then the backlog's **members** `FOR KEY SHARE` in id order, read by § 1 in a **locking
   statement** that states the project status itself (a save, a Delete or an unlink's reassignment of a member takes
   the row `FOR UPDATE` and waits for the reorder, or is read as a non-member by it); then the order row of (team, level)
   `FOR UPDATE`, creating it on the first reorder — so the reorders of one backlog run one at a time in arrival order
   (ADR-0028 rule 8: team, items, order row; no link and no project row is locked);
2. reads the team's order of § 1 over those members;
3. refuses with 409 `boards.backlog.reorder_stale` if `itemId`, or with `"after"` the `afterItemId`, is not on this backlog
   now — missing, malformed, unknown, **another team's, a deactivated project's, in the Removed category, another
   organization's** (all alike in status and body: nothing is learned about the other object); nothing changes;
4. moves the one item, gives **every** item of the backlog — of every active project — the place it now stands at (1-based,
   dense), and **drops the places this backlog holds for items that have left it** (they are no member by owner, type or
   state) **in the organization's active projects**. The places of a **deactivated project's** items are left exactly as
   they are (ADR-0028 rule 9; BR-TS-7). The project orders are untouched.

A reorder that meets a deactivation or a restore of one of T's projects works on the members its locking statement reads:
that project's items keep a place on T's backlog either way (the one they had, or the one the reorder wrote), and a
reorder whose moved or named item has just left is 409. An unlink, a team deletion and a save that changes an item's team
end in one order with no server error (§ 8.1).

- **204** — no body; the panel reads § 3.1 again.
- **400** `boards.validation_failed` — `errors.position`: `boards.backlog.position_invalid`. A panel defect.
- **404** organization, team, level (§ 0.3); the team again when it was deleted while the reorder waited for its lock.
- **409** `boards.backlog.reorder_stale` — step 3 (also the answer for a team linked to no active project: nothing to move).

### 3.3 `GET …/boards/{level}` — a team's board (US-1, US-2, US-4)

Query: **`projectId`**, as § 3.1.

- **200**
  ```json
  { "team": BoardTeam, "projects": [ BoardProject, … ], "level": BoardLevel, "columns": [ BoardColumn, … ] }
  ```
  `columns` are derived from the organization's process on every read (one process per organization, ADR-0028 § 11),
  each card in the column its type's current state maps to, in the team's order of § 1; each card carries its
  `checklist` (feat-032) of live task, bug and test-case children of its own project, **whatever their team and whatever
  the filter** (a checklist is not narrowed). A column's `targets` are unchanged: a column maps a state per type and
  `stateId: null` is no drop target.
- **404** team, level (§ 0.3).

### 3.4 Retired: the project-nested team views

`GET …/projects/{projectId}/teams/{teamId}/backlogs/{level}`, `POST …/backlogs/{level}/reorder` and `GET
…/boards/{level}` are **not served and have no alias** (BR-TS-1; ADR-0028 § 7; feat-028 BR-OL-11 replaced). Each answers as
any path that does not exist: **404, the platform's `http.not_found`, no code of ours**, for every method and whoever
asks (a non-member included), whether or not the project and the team exist; nothing changes. The project keeps `…/teams`
(the linked teams), `…/teams/{teamId}/unlink` and `…/unlink-count`, which are not views.

## 4. Extended responses (additive)

### 4.1 `GET /api/organizations/{organizationId}/teams` — `isMember` (feat-028 § 1.2, § 2.1)

Each `TeamListItem` gains **`isMember`**: whether the **caller** is in the team (mod-016 § 3.3 semantics, as the project's
`ProjectTeam.isMember`). The list is unchanged otherwise: the organization's active teams **by name**, the server's order;
the panel groups the user's own first, each group by name (BR-TS-10). Read with the list in the same statement.

### 4.2 `GET …/projects/{projectId}/workitems/{identifier}` and the writes' answers — `project` (mod-003 § 1.1)

`WorkItem` gains **`project`: `WorkItemProject`** (§ 2.1) beside `projectId`, which stays. The card dialog's «Проект» reads
it (BR-TS-3). It rides every answer that returns a `WorkItem` (create, save, parent write, restore).

## 5. Error codes

**None new.** Used unchanged: `organizations.organization.not_found`, `organizations.team.not_found`,
`organizations.access.forbidden`, `boards.level.not_found`, `boards.validation_failed`, `boards.backlog.position_invalid`,
`boards.backlog.reorder_stale`; and, on the writes of § 7, `organizations.project.not_found`,
`organizations.team.not_in_project`, `workitems.workitem.not_found`, `workitems.link.parent_not_found`,
`workitems.workitem.state_not_allowed`. `docs/error-codes.md` and `contracts/errors/*.json` do not change.

## 6. Summary of routes

| Method | Route | Code declared | Story |
|---|---|---|---|
| GET | `/api/organizations/{organizationId}/teams/{teamId}/backlogs/{level}` | `workitems.workitem.read` | US-1, US-2 |
| POST | `/api/organizations/{organizationId}/teams/{teamId}/backlogs/{level}/reorder` | `boards.backlog.reorder` | US-3 |
| GET | `/api/organizations/{organizationId}/teams/{teamId}/boards/{level}` | `workitems.workitem.read` | US-1, US-2, US-4 |

These replace rows 35-37 of mod-016's inventory: `OrganizationAddressedRoutes` swaps its three `Team + …` rows for
`OrgTeam + …` rows, the count stays **59**, and the three project-nested ones are not listed.

## 7. Writes and reads the panel reuses (unchanged, in the item's own project)

The row and the card carry `project.id`; every call below goes to **that** project, never to the page's.

| Need | Route |
|---|---|
| A card over a team view (`?workitem=BET-US-3`) | mod-003 `GET /projects/{project.id}/workitems/{identifier}`. By address: the panel resolves the identifier's project key through `GET /api/organizations/{o}/projects` (the organization's **active** projects, each with `key`, mod-002 § 2.1); an unknown key sends no request and shows the not-found frame. The server's own refusals — `workitems.workitem.not_found` (unknown, malformed, a key/type mismatch) and `organizations.project.not_found` (a deactivated project) — show the same frame. |
| A card move (US-4) | mod-003 `PATCH /projects/{project.id}/workitems/{identifier}` with `{ "stateId" }`; touches no place of either order; 409 `workitems.workitem.state_not_allowed` for a state of another type |
| A row's «+», a card's «Добавить задачу» | feat-027 `POST /projects/{project.id}/workitems` with `parentId`; the item takes the parent's project, number and team |
| A drop onto a row, «Изменить родителя…» | feat-001 `PUT /projects/{project.id}/workitems/{identifier}/parent`; a parent of another project answers 404 `workitems.link.parent_not_found`; `GET …/parent-candidates` offers the item's own project's items |
| «Новый рабочий элемент», «Новый элемент» (US-5) | mod-003 `POST /projects/{chosen project}/workitems` with `team: { teamId }` (a team of that project, or `{}` for «Без команды»); the project is chosen in the dialog from `projects` (§ 2.2). 404 `organizations.project.not_found` (deactivated), 409 `organizations.team.not_in_project` (unlinked meanwhile), 404 `organizations.team.not_found` (deleted); nothing is created. The item is unplaced on both backlogs |
| The dialog's «Команда» options | feat-028 `GET /projects/{chosen project}/teams` (with `isMember`, `defaultMark`) |
| The dialog's «Проект» options re-read | `GET …/teams/{teamId}/projects` (feat-028 § 2.2) or § 3.1/§ 3.3 again |
| The switcher's and the head's team list | `GET /api/organizations/{o}/teams` with `isMember` (§ 4.1) |

## 8. Concurrency, containment, speed

### 8.1 Races (BR-TS-7, ADR-0011; each ends in one order and no server error)

| Race | How it ends |
|---|---|
| reorder vs an unlink of one of T's projects | the member locks order them: the reorder commits first and the unlink then drops the places of that project's items, or the unlink commits first and the reorder is 409 (moved item left) or completes on the remaining members |
| reorder vs T's deletion | the reorder commits first and the deletion removes every place, or the reorder is 404 `organizations.team.not_found` |
| reorder vs a save changing a member's team | the reorder first: the save waits and drops the place the reorder wrote; the save first: the reorder's locking read no longer finds the item (409 if it was moved or named) |
| reorder vs a deactivation or restore of one of T's projects | no lock on the project; the reorder works on the members its locking statement reads; that project's places are kept or written, never dropped; a moved or named item that has just left is 409 |
| a read vs T's deletion | the view as it stood, or 404 `organizations.team.not_found`; never a server error (tech-011 m2, § 8.3) |
| a creation vs an unlink, a team deletion | mod-017 / feat-028 locks, unchanged |

### 8.2 Containment (tech-011 m3)

Every statement of a team view carries the caller's organization: the team is found by (organization, id), the members join
`projects` on the caller's organization, the live parents and the checklists are read under it. A foreign team id is
404 as an unknown one; a foreign project id in the filter selects nothing; a foreign item id in a reorder is 409; and in
none of them does any answer say whether the other object exists.

### 8.3 Reads that meet a deletion

The team is read first (404 before the level), the reads follow, and the team is **read again after them**: if it was
deleted meanwhile the answer is 404 `organizations.team.not_found`, otherwise the view read before the deletion.

### 8.4 Query shape and speed (spec § NFR)

A team read takes a fixed number of statements whatever the rows, projects, filter values or depth: the team, the
process, the team's linked projects, **one** statement for the members or the tree set (project, type, level, state,
team and place joined), the assignees' lookups in batches of the directory's limit with one membership statement, one
for the parents outside the set, and the team read again; the board adds one for all the cards' checklists. A reorder
takes a fixed number too (the five lock statements of § 3.2, one read, one drop, one upsert). Statement-count tests pin each. A team of 50 projects and
5 000 items answers a read within 2 s, filtered or not, and a reorder within 1 s.

## 9. Storage and migration

One additive migration, **`AddTeamLedWorkItemIndex`**: `CREATE INDEX ix_work_items_owning_team_id_type_id ON work_items
(owning_team_id, type_id)` (ADR-0028 rule 13: "items by (owning team, type) serve team views across all of a team's
projects"); the existing `ix_work_items_project_id_owning_team_id_type_id` leads with the project and stays for the
project views and the owner key. **No table, column or row changes; nothing is dropped or remapped** (BR-TS-14): the team
places and orders feat-028 left are read as they are. `Down` drops the index.

## 10. Breaks to merged contracts (P-10)

The owners get a note in their `status.md` at the code stage's close (plan-backend § 12): mod-006 (§§ 2-3, 5 and 7: the
team routes, rows 35-37), mod-016 (inventory rows 35-37), feat-027 and feat-032 (row and card shapes gain `project`),
feat-039 (BR-PB-4's last sentence: team views no longer keep the number tie-break), feat-028 (BR-OL-11, the team views at
a project's address), mod-017 (§ 3's team views; BR-OT-7 «Проект» arrives), mod-003 (§ 1.1 `project`). Additive, not
breaking: feat-028 § 1.2 `isMember`.
