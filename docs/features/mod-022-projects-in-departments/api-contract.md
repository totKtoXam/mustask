# API contract: mod-022-projects-in-departments

Fixed before the code (stage 4a). Base: mod-018, feat-028 and mod-021 contracts; this file lists only what changes. Routes are under
`/api/organizations/{organizationId}`. Authorization is unchanged. No route is added or removed; no response shape changes (a project
node's `parentId` is now always a department id).

## 1. Changed and refused behaviours

### `POST /projects` (BR-TD-6, BR-TD-3)

- The body's `parentAreaId` (the department, «Расположение») is now **required**. Absent or `null`: 400
  `organizations.validation_failed` with field `parentAreaId`, reason `organizations.project.department_required`
  («Выберите департамент»), listed with the other field reasons in the same answer; nothing is created. No place is judged for such a
  request (the 403 for a role without `organizations.area.write` on the department applies when a department is named).
- A named department that is unknown, foreign, deleted: 404 `organizations.area.not_found`; a project node or sub-area: 409
  `organizations.area.type_not_allowed`; a role without `organizations.area.write` on it: 403, as before.
- `teamIds` (optional): every team must stand in the named department, else 409 `organizations.team.other_department` and nothing is
  created. Order: values (400), department (404, 409), teams (404 `team.not_found`, then 409 `team.other_department`), name and key
  (409).

### `PUT /areas/{areaId}/parent` on a project node (BR-TD-6, BR-TD-4)

- Destination `null` (the top level): 409 `organizations.area.project_needs_department` («Проект должен находиться в департаменте.»),
  nothing changes. (The second-code check `organizations.area.create` at the top level still answers 403 first for a role without it.)
- Destination another department while any team of any status is linked to the project: 409 `organizations.area.project_has_teams`
  («Проект связан с командами. Отключите их перед перемещением.»), nothing changes. Moving to the department it already stands in is a
  200 that changes nothing, links or not. Order: type rules (`type_not_allowed`, `area.move_into_own_subtree`) and the top-level refusal,
  then `project_has_teams`, then the sibling name and depth.
- A move against a link ends in one order: the move refused with `project_has_teams`, or the link refused with `team.other_department`
  (never a 500).

### `POST /projects/{projectId}/teams`, `POST /teams/{teamId}/projects`, `POST /teams` with `projectIds` (BR-TD-3)

A team and a project are linked only when they stand in the same department; otherwise 409 `organizations.team.other_department`
(«Команду можно связать только с проектом своего департамента.»), all or nothing, nothing changes. Linking an already linked pair is a
success that changes nothing. It is decided after the 404s and the 403s of each route (so ids that name nothing and missing rights
still answer as before). `POST /projects` with `teamIds` as above.

## 2. Error codes

| Code | Status | Text |
|---|---|---|
| `organizations.team.other_department` | 409 | Команду можно связать только с проектом своего департамента. |
| `organizations.area.project_has_teams` | 409 | Проект связан с командами. Отключите их перед перемещением. |
| `organizations.area.project_needs_department` | 409 | Проект должен находиться в департаменте. |
| `organizations.project.department_required` | 400 field reason | Выберите департамент |

`organizations.area.type_not_allowed`'s text now reads «… проект — в департаменте …» (no top level). Registered in
`contracts/errors/organizations.json` and `docs/error-codes.md`.

## 3. Upgrade (not an endpoint)

The migration places every top-level project (BR-TD-19 step 2; fallback department «Проекты без департамента»), cuts every link across
two departments like an unlink to «Без команды» (step 3) and logs each in `project_place_upgrade_log` (columns: `organization_id`,
`kind` in `department_created|project_placed|link_cut`, `project_id`, `project_name`, `team_id`, `team_name`, `department_id`,
`department_name`, `rule` in `most_teams|tie_by_name|fallback`, `item_count`). After it, `areas` refuses a project node with no
parent (constraint `ck_areas_type_shape`).
