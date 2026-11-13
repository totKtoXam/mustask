# API contract: team sprints (mod-007)

Stage 4 artifact, fixed with `plan-backend.md`. Stories US-1..US-5, US-9, US-10; rules BR-1..BR-16 (`spec.md`); texts
`design.md` § 10. From here the contract changes only with a note in `status.md` and a line in its Log.

## 0. Conventions

- Base: `/api/organizations/{organizationId}/teams/{teamId}/sprints`. Every id in an address or a body is the untrusted
  string it arrived as: a malformed id answers its level's not-found code, never a 400 (as every module).
- JSON is camelCase; dates are `YYYY-MM-DD`; times are ISO 8601 UTC; a state is the string `Planned`, `Active` or `Closed`.
- Errors are ProblemDetails with `errorCode` (`docs/error-codes.md`); a field refusal is a 400 with `errors[field]`.
- **The order of answers** of every route: the scoped check (401, 404 organization, 403 code) → the team (404
  `organizations.team.not_found`: unknown, foreign and deleted alike) → the sprint (404 `iterations.sprint.not_found`) →
  the body (400, every field reason at once) → rules of the state (409) → the name (409). A refusal writes nothing.
- Rights: reads need `organizations.team.read` (every organization role holds it); writes need the new code
  `iterations.sprint.manage`, scope Team (`ResourceScope.Team`): held by the organization «Администратор», by the team
  roles «Участник» and «Менеджер» of that team, by no other role. Setting an item's sprint is an item change
  (`workitems.workitem.write`), not a sprint-management act (BR-13).
- Nothing here names a project, an item or a count the caller may not see (BR-9): a deactivated project's items are
  counted nowhere. (Visibility clauses are feat-041's.)

## 1. Shapes

`Sprint`:

```json
{ "id": "…", "teamId": "…", "number": 12, "name": "Спринт 12", "state": "Active",
  "startDate": "2026-11-02", "endDate": "2026-11-13", "createdAt": "…", "createdBy": "<sub>",
  "closedAt": null, "closedBy": null }
```

`Figure` (display-ready: the exact values never leave the server; BR-15): `{ "min": n|null, "average": n|null, "max": n|null }`.
A count's `average` has one decimal (half up); a percent figure is a whole number (half up); `null` is «—».

## 2. Routes

### 2.1 `GET …/sprints?before={number}&open={bool}` — the list (US-3)

Highest number first, 200 a page. `before` is the cursor (rows with a lower number); `open=true` keeps Planned and Active.
`200`: `{ "items": [SprintRow], "nextBefore": 14 | null }`.
`SprintRow`: `id, number, name, state, startDate, endDate, itemCount, storyPoints, closedItems, closedStoryPoints`.
Planned and Active: items outside «Удалена» of reachable projects, at every level, `closedItems` and `closedStoryPoints`
`null`. Closed: the snapshot at the level «Истории» (D-8): `itemCount`/`storyPoints` held, `closedItems`/`closedStoryPoints` done.
Codes: 404 team. Statements: fixed (sprints page, live counts, snapshot counts).

### 2.2 `POST …/sprints` — create (US-1) · `iterations.sprint.manage`

Body `{ "name": string|null, "startDate": date|null, "endDate": date|null }`. `201 Sprint` (Planned, next number; no name or
an empty one: «Спринт <n>»). 400 `iterations.validation_failed` with `errors.name` = `iterations.sprint.name_invalid`,
`errors.endDate` = `iterations.sprint.dates_unpaired` | `iterations.sprint.dates_reversed`; 409 `iterations.sprint.name_taken`
(no number is issued); 403; 404 team.

### 2.3 `PUT …/sprints/{sprintId}` — rename and date (US-1) · manage

Body `{ "name": string, "startDate": date|null, "endDate": date|null }`, replacing all three. `200 Sprint`.
Planned: any valid dates (both or neither). Active: both required (409 `iterations.sprint.active_dates_required`). Closed: the
dates must equal the stored ones (409 `iterations.sprint.closed_dates_locked`); only the name changes. 400 as 2.2; 409
`name_taken` (ignoring case and surrounding spaces, never against itself); 404 sprint.

### 2.4 `POST …/sprints/{sprintId}/start` — start (US-2) · manage

No body. `200 Sprint` (Active). 409 `iterations.sprint.not_planned`, `iterations.sprint.start_dates_required`,
`iterations.sprint.active_exists`; 404 sprint.

### 2.5 `DELETE …/sprints/{sprintId}` — remove (US-2) · manage

`204`. Only a Planned sprint holding no item outside «Удалена» (deactivated projects' items count as held); its «Удалена» items
lose their pointer; the number is never reused. 409 `iterations.sprint.not_removable`; 404 sprint.

### 2.6 `GET …/sprints/{sprintId}/close-preview` — the close dialog (US-5)

Active sprints only. `200`:
`{ "moving": {"items": n, "storyPoints": n}, "staying": {"items": n, "storyPoints": n}, "destinations": [{"id","name","startDate","endDate"}] }`.
`moving`: items in a Proposed, In Progress or Resolved category; `staying`: Completed; «Удалена» and unreachable projects
excluded. `destinations`: the team's Planned sprints in number order. 409 `iterations.sprint.not_active`; 404 sprint.

### 2.7 `POST …/sprints/{sprintId}/close` — close (US-5) · manage

Body `{ "destinationSprintId": string|null }` (absent or `null` = «Без спринта»). One operation: the unfinished items move
(their «Изменён» becomes the close's time), the snapshot is written, the sprint becomes Closed. `200 Sprint`.
409 `iterations.sprint.not_active`; 409 `iterations.sprint.close_destination_invalid` (the destination exists but is not a
Planned sprint of this team); 404 `iterations.sprint.not_found` (the destination, or the sprint, names nothing: malformed,
unknown, removed meanwhile, of another organization); 404 team.

### 2.8 `GET …/sprints/{sprintId}` — a Closed sprint (US-5)

`200`: `{ "sprint": Sprint, "closedByName": string|null, "itemCount": n, "storyPoints": n }` — the done part of the snapshot
(BR-6), all levels. Not Closed: 404 `iterations.sprint.not_found`.

### 2.9 Page reads of a Closed sprint (US-9, US-10)

Common query: `level=epics|features|stories` (default `stories`; another value: 404 `boards.level.not_found`),
`from=YYYY-MM-DD&to=YYYY-MM-DD` (both or neither, `to ≥ from`; otherwise the default: the 90 days before this sprint's
start through the day before it). The range selects the team's **other** Closed sprints whose end date lies in `[from, to]`;
at most 20 are read, the latest by end date. Figures come from the close snapshot of the sprints, at the level, after BR-9.

`GET …/sprints/{sprintId}/dynamics` → `200`:

```json
{ "level": "stories", "from": "2026-08-04", "to": "2026-11-01", "rangeSprints": 3, "shownSprints": 3, "truncated": false,
  "current": Row, "sprints": [Row], "summary": { "closedItems": Figure, "closedItemsPercent": Figure,
  "closedStoryPoints": Figure, "closedStoryPointsPercent": Figure } }
```

`Row`: `{ "id","number","name","endDate","items","storyPoints","closedItems","closedItemsPercent","closedStoryPoints","closedStoryPointsPercent" }`;
a percent is `null` when held is 0. `sprints`: latest end first. `summary` is over `sprints` only; a held-0 sprint counts as 0
in the count figures and in no percent; with no sprint every figure is `null`.

`GET …/sprints/{sprintId}/participants?offset=0` → `200`:
`{ "level","from","to","rangeSprints","rows": [Participant], "total": Participant, "totalRows": n }` — 200 rows a page, by current
full name, then «Без исполнителя» last; `total` (the «Итого» row) is the same on every page. `Participant`:
`{ "userId": string|null, "displayName": string|null, "isMember": bool, "items","storyPoints","closedStoryPoints","closedStoryPointsPercent","closedItems","closedItemsPercent", "comparedSprints": k, "dynamics": { "closedItems": Figure, "closedItemsPercent": Figure, "closedStoryPoints": Figure, "closedStoryPointsPercent": Figure } }`
(«k из N» is `comparedSprints` of `rangeSprints`).
`userId` is `null` on «Без исполнителя» and on the total. A person's figures run over the range sprints in which they held
items at the level («k из N»); SP percent leaves a sprint whose held SP is 0 out.

`GET …/sprints/{sprintId}/items?offset=0` → `200`: `{ "items": [ { "identifier","title","project": {"id","name"}, "storyPoints", "purged": bool } ], "totalItems": n, "totalStoryPoints": n }`
(the done items, whole, 200 a page, by project key then number; a purged item: `title` `null`, `purged` `true`).

404 for a sprint that is not Closed; 404 team (a deleted team has no page).

## 3. The item's sprint (US-4) — a change of mod-003's routes

`PATCH /api/organizations/{o}/projects/{p}/workitems/{identifier}` gains `sprint: { "sprintId": string|null }` (absent = keep;
the same wrapper as `team`). Every item view (card: create, save and read answers) gains
`sprint: { "id", "name", "state" } | null`.

Rules (BR-2, BR-5): setting a sprint checks only when the pointer changes. The effective team is the sent one, else the stored one.
409 `iterations.sprint.team_required` (no team), `iterations.sprint.other_team`, `iterations.sprint.closed_target`; 404
`iterations.sprint.not_found` (unknown, foreign organization); an item in «Удалена» answers `workitems.workitem.deleted` first.
A save that changes the owning team and sends no sprint clears the pointer; sending a Planned or Active sprint of the new team keeps it.
The unlink of a team and its deletion clear the pointers of the items they reassign, a Closed sprint's included.

## 4. Errors added

`iterations.validation_failed` (400), and the codes of `design.md` § 10 under their names: `iterations.sprint.name_taken`,
`name_invalid`, `dates_unpaired`, `dates_reversed`, `active_dates_required`, `closed_dates_locked`, `start_dates_required`,
`active_exists`, `not_planned`, `not_active`, `not_removable`, `not_found`, `close_destination_invalid`, `other_team`,
`team_required`, `closed_target`. Texts: `contracts/errors/iterations.json`, projection `docs/error-codes.md`.
