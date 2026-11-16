# API contract: feat-041-project-visibility

Fixed before the code (stage 4a). Base: mod-016 (§ 0.1, § 3.4), mod-019, mod-021 contracts; this file lists only what changes.
Routes are under `/api/organizations/{organizationId}`.

## 1. Answer order (amends mod-016 § 0.1 rows 2-4 and ADR-0029 § 8; see the note in mod-016's `status.md`)

For a non-administrator, after 401 and 404 organization (rows 1-2), the first that applies:

1. **404 `organizations.project.not_found`** «Проект не найден.»: the path's `projectId` is malformed, unknown, of another
   organization, or a project the caller does not see (BR-TD-12). Before any 403: a viewer's write to a missing or hidden project
   answers 404, not «Недостаточно прав».
2. **404 `organizations.team.not_found`** «Команда не найдена.» / **404 `organizations.area.not_found`** «Область не найдена.»: the
   path's `teamId` / `areaId` is malformed, unknown, or hidden (BR-TD-13, -14; a project node and its sub-areas are hidden exactly
   with their project). This includes the team's backlog reorder and sprint routes.
3. The same three answers for the query ids of a read: a `teamId`, `departmentId` or `projectId` that is not a visible object of the
   organization (unknown, of another organization, malformed or hidden: indistinguishable) answers the 404 of its kind. The word
   `none` and an empty value are not ids and pass. This includes `GET /workitems/assignee-candidates?projectId=` (a malformed id is a 404).
3a. **404 `iterations.sprint.not_found`** / **404 `workitems.workitem.not_found`** (code review R-2; ADR-0029 § 8 item 4, mod-019 BR-SA-9 "until feat-041"): a path `sprintId` that is malformed or names no sprint of the team, and a path `identifier` that is malformed, of another project key or names no item of the project (an item in any state, a deleted one too), answer their not-found after the project, team and area checks and before any 403. The container is visible by then, so nothing hidden is disclosed. An identifier whose type key is wrong for an existing number is still judged by the handler (403 first for a role lacking the code).
4. 403 `organizations.access.forbidden`; then 400, then the **body ids**: on `POST /teams/{t}/projects` and `POST /projects/{p}/teams` a body project or team that the caller does not see answers 404 `organizations.project.not_found` / `team.not_found`, as one that names nothing, before the second code (R-5). Not done here, left for feat-062 with the team views: the item ids of a team backlog reorder (an item of a project the caller does not see is still ordered), and the other body ids.
5. The operation's own answers (unchanged).

The administrator sees everything: answers 1-3 never apply to them (a missing object answers as before).
Sight changes (a grant, a team membership, a link, the flag) apply on the caller's next request.

The visibility write logs an information line with the project, the value and the actor (as grant writes do).

## 2. New route

### 2.1 `PUT /projects/{projectId}/visibility` : «Видим всей организации» (BR-TD-12 (c))

- Declared code: `organizations.visibility.write`, scope Project (new catalog code; held by the project «Менеджер», the department
  «Менеджер» on the projects its grant reaches, and the administrator; `contracts/access/role-bundles.json` regenerated).
- Body: `{ "visibleToOrganization": true | false }`, a required boolean.
- 200: `{ "id", "organizationId", "visibleToOrganization" }`. Setting the value it already holds is a 200 that changes nothing.
- Order: 401, 404 organization, 404 project (§ 1), 403, 404 `organizations.project.not_found` (a deactivated project), 400
  `organizations.validation_failed` field `visibleToOrganization` (absent or not a boolean), the write.
- The write changes sight only; it adds no code (BR-TD-16).

## 3. Changed routes

- `GET /projects/{projectId}`: gains `"visibleToOrganization": bool` (false for every project at the upgrade and at creation).
  `codes` lists `organizations.visibility.write` for a holder: the panel shows the control by that code.
- `GET /workitems/assignee-candidates?projectId=…` (mod-016 § 3.4 reworded): offers the active members **who see the project**
  (administrators, grantees that reach it, members of a linked team, every member when the flag is on). Without `projectId` it
  offers active members as before. A `projectId` the caller does not see answers 404 (§ 1).
- `PATCH …/workitems/{identifier}` (mod-016 § 3.4): a newly set assignee who does not see the item's project answers 409
  `workitems.workitem.assignee_unavailable`, as a non-member does. An assignee already set who has since lost sight stays and
  never refuses a save of other fields (the rule applies to a newly set assignee only).

## 4. Upgrade (OQ-5 A)

Migration `20261116104040_ProjectVisibleToOrganization`: `projects.visible_to_organization` default false; table
`project_sight_upgrade_log (organization_id, project_id, project_key, user_id)` lists, per project, the members who saw it before
and do not after; a notice per project names the count.

## 5. Not changed here

Lists, selectors, «Структура», «Обзор», pickers, multi-project views, search, counts (feat-061, feat-062): they still read the
organization's projects through their own read models and are not narrowed by this line.
