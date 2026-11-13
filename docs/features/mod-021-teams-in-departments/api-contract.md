# API contract: mod-021-teams-in-departments

Fixed before the code (stage 4a). Base: feat-028 and mod-018 contracts; this file lists only what changes. Routes are under
`/api/organizations/{organizationId}`. Authorization is unchanged (OQ-6 / department managers is mod-023). Colour key values,
in palette order: `blue`, `orange`, `green`, `purple`, `red`, `teal`, `amber`, `pink`, `indigo`, `lime`, `magenta`, `cyan`
(the panel owns the hex). `color` is assigned at creation, never edited, kept on a move.

## 1. Retired routes (all answer 404 from now on)

| Route | Replaced by |
|---|---|
| `POST /areas/{areaId}/teams` | the team's place (`departmentId` at creation, `PUT /teams/{teamId}/department`) |
| `DELETE /areas/{areaId}/teams/{teamId}` | none |
| `GET /teams/{teamId}/areas` | `departmentId` / `departmentName` on the team |
| `POST /teams/{teamId}/areas` | as the first row |
| `DELETE /teams/{teamId}/areas/{areaId}` | none |

Error codes `organizations.area.link_type_not_allowed` stays in the registry (never reused) and is no longer raised.
`organizations.area.link` stays in the permission catalog and is declared by no route.

## 2. New route

### `PUT /teams/{teamId}/department` : move a team (BR-TD-4, team part)

- Declared code: `organizations.team.create` (the administrator's; mod-023 widens it). Scope: organization.
- Body: `{ "departmentId": "<guid>" }`. Required.
- 200: `TeamPlace` = `{ "id", "name", "description", "departmentId", "departmentName", "color" }`.
- Order of answers: 401, 404 organization (non-member), 403; 404 `organizations.team.not_found` (unknown, foreign or deleted
  team); 400 `organizations.validation_failed` with `departmentId` reason `organizations.team.department_required` (absent or
  null); 404 `organizations.area.not_found` (malformed, unknown, foreign or deleted department); 409
  `organizations.area.type_not_allowed` (a project node or a sub-area); the destination equal to the current department is a
  200 that changes nothing, whatever the links; 409 `organizations.team.has_links` while the team is linked to any project of any
  status.
- Concurrency: a move against the destination's deletion ends in one order (200 and the deletion refused with
  `has_teams`, or 404 `area.not_found`); a move against a link ends in one order; never a 500.

## 3. Changed routes

### `POST /teams`

Body gains `departmentId` (required); `areaIds` is removed (ignored if sent). Validation, in one 400
`organizations.validation_failed` with every field reason: `name`, `description`, and `departmentId` with reason
`organizations.team.department_required` («Выберите департамент») when absent. Then, under the tree lock and the department
`FOR SHARE`: 404 `organizations.area.not_found` (unknown, foreign or deleted department), 409
`organizations.area.type_not_allowed` (project node or sub-area). Creation permission stays `organizations.team.create`.
201: `TeamPlace` as above (`departmentName` filled).

### `GET /teams`, `GET /teams/{teamId}`

Each item gains `departmentId` (Guid), `departmentName` (string), `color` (string). `GET /teams/{teamId}` keeps `codes`.
`PUT /teams/{teamId}/name` and `/description` answer `{ id, name, description, departmentId, color }`.

### `GET /projects/{projectId}/teams` (a project's linked teams)

Each item gains `color`.

### `GET /areas/{areaId}/teams` : a department's teams by place

200: `[ { "id", "name", "color" } ]` by name, active teams standing in the department. The `canUnlink` field is removed. Any other
node type answers an empty list.

### `GET /areas/{areaId}/deletion`

`teams` now lists the active teams standing in the department, which block its deletion; shape `{ id, name }` unchanged.

### `DELETE /areas/{areaId}`

New 409 `organizations.area.has_teams` «Нельзя удалить департамент, в котором есть команды» while an active team stands in the
department. It is decided before `organizations.area.not_empty`, so a department holding a team and a project answers with the
team text. A deleted team never blocks it.

### `POST /areas`

`teamIds` is removed (ignored if sent). The response gains `color` (a department's colour, `null` for a sub-area).
`PUT /areas/{areaId}/name`, `/description`, `/parent` answers gain `color` as well.

### `GET /areas`, `GET /areas/{areaId}`

Each node gains `color`: a department's own, a project node's project colour, `null` for a sub-area. `GET /areas/{areaId}` children
gain `color` the same way.

### `POST /projects`, `GET /projects`, `GET /projects/deactivated`, `GET /projects/{projectId}` and the project writes

Each project object gains `color`.

### `GET /selector-offers`

- `departments[]`: gains `color`.
- `projects[]`: gains `color`.
- `teams[]`: `departmentIds` (array) is replaced by `departmentId` (Guid, the team's place); gains `color`.

## 4. Error codes (new; texts in `contracts/errors/organizations.json` and `docs/error-codes.md`)

| Code | Status | Text |
|---|---|---|
| `organizations.team.has_links` | 409 | Команда связана с проектами. Отключите её от них перед перемещением. |
| `organizations.area.has_teams` | 409 | Нельзя удалить департамент, в котором есть команды |
| `organizations.team.department_required` | 400 field reason | Выберите департамент |

## 5. Upgrade (not an endpoint)

The migration places every active team (BR-TD-19 step 1), creates «Команды без департамента» per organization where needed
(reused when present), drops the department–team links and colours every department, team and project (BR-TD-20). It writes
`team_place_upgrade_log` (columns: `organization_id`, `kind` in `team_placed|department_created|link_dropped`, `team_id`,
`team_name`, `department_id`, `department_name`, `rule`) so the MR can name each. Projects standing at the top level are
coloured as one scope of their own until mod-022 places them.
