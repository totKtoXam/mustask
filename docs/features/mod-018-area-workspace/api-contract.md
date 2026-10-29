# API contract: the organization's area workspace, typed single tree (mod-018)

Fixed at stage 04 (backend plan), reworked 2026-10-28 for the human's typed tree (spec BR-AW-0..15, OQ-3..OQ-13 as
resolved, ADR-0031). It replaces the first build's contract (area-project links, free-form areas, no project node). This file is
the interface the frontend reads, in parallel with the backend. A change after this point is a commit of its own and a line in
`status.md` naming what changed. JSON is camelCase; ids are GUID strings; errors are ProblemDetails with `errorCode`
(registry `docs/error-codes.md`, texts there and in `contracts/errors/organizations.json`).

## 0. The model in five lines

- One tree per organization. The organization is **not** a node. A node has a `type`: `department` (top level only, never nested),
  `project` (one per project) or `area` (a sub-area, only below a project node or another sub-area).
- A project node is created, renamed and deleted only together with its project; it has one parent (a department or the top
  level) and moves. While the project is deactivated its node and sub-areas are read-only and `projectActive` is `false`.
- A sub-area stays inside its own project's subtree (`outside_project` otherwise).
- Team links join **departments only**. A project node's teams are the project's own team links (feat-028).
- One name space per level (siblings of any type); at most 10 levels; the work item's «Область» is one of its project's
  nodes (the project node or a sub-area), see § 6.

## 1. Conventions

- **Address.** Every route is under `/api/organizations/{organizationId}`. Areas live under `/areas`; the project's subtree and the
  team's links under their own objects (§ 4, § 5).
- **Order of answers.** 401; 404 `organizations.organization.not_found` for a non-member; 403 `organizations.access.forbidden` for a
  role lacking the route's declared code; then the **second codes** (listed per route), before the organization's own id is looked
  at; then the not-found of the address outside in; then, on the routes that have one, the **rules about the addressed node**
  (409 `type_not_allowed`, `link_type_not_allowed`, `root_is_fixed`): `POST /areas`, `POST /projects`, `POST /areas/{id}/teams` and
  the rename answer them before the ids a body names and before the 400 on values; 400 (values, every reason at once); the ids a
  body names, each its own 404; then the deactivated-project 404 (`project.not_found`); then the remaining 409 (type rules before
  the cycle rule, then `not_empty`, cycle, depth, name). A malformed, unknown, foreign or deleted id is the same 404 of its level.
  A malformed, unknown, foreign or deleted id is the same 404 of its level.
- **Declared codes** (what the role bundle is checked for): `organizations.area.read` on reads; `organizations.area.write` on
  create, rename, describe, move, delete and the deletion preview; `organizations.area.link` on an area's team routes;
  `organizations.team.read` / `organizations.team.link` as listed. **Second codes** (checked by the handler, 403 the same way):
  `organizations.area.create` for creating a top-level node (a department), for moving a node **to or from** the top level, and for
  creating a project at the top level; `organizations.area.write` for creating a project under a department (placing a project, US-6);
  `organizations.team.link` with `organizations.area.link` when a request names teams or areas to link. Admin holds all;
  member and viewer hold reads only. Until `mod-019-scoped-access` a code is checked against the organization role alone.
- **Name.** Trimmed, 1-100 characters, unique among the siblings (same parent; top-level nodes are siblings), ignoring case and
  surrounding spaces, across types (a department and a project node at the top level share the space).
  **Description.** Optional, as typed, at most 2 000 UTF-16 units; `null`, absent and `""` are none.
- **Path.** Node names from the top level joined by `" / "` (a sub-area: `Department / Project / Reports`). `depth` is the level
  (1 for a top-level node).
- **Links.** All-or-none per request; linking an existing pair or unlinking an absent one is a success that changes nothing.
  An empty, absent or `null` id list is a 204 no-op. Only active teams of the organization link.
- `inheritsAccess` is stored (always `true`) and is in no request and no response (BR-AW-5).

## 2. Shapes

```
AreaNode     { id, parentId: Guid|null, type: "department"|"project"|"area", projectId: Guid|null,
               projectActive: bool|null, name, description: string|null, path, depth: int }
               // projectId/projectActive: null for a department; projectActive false = the project is deactivated
Area         { id, parentId: Guid|null, type, projectId: Guid|null, name, description: string|null, path }
               // returned by create, rename, describe, move
AreaPage     { id, parentId, type, projectId, projectActive, name, description, path,
               parent: { id, name, path } | null,
               children: [{ id, type, name, description, projectKey: string|null, projectActive: bool|null }] }
               // direct children, by name; projectKey/projectActive only on a project node (the department page's «Ключ» and «деактивирован»)
AreaTeam     { id, name }                                          // GET /areas/{id}/teams
TeamArea     { id, name, path }                                    // GET /teams/{id}/areas (departments)
AreaDeletion { teams: [{ id, name }] }                             // what deleting the area would end
```

## 3. The organization's areas

| Route | Declared code (+ second codes) | Body → Answer |
|---|---|---|
| `GET /areas` | `area.read` | → 200 `AreaNode[]`, the whole tree, pre-order, siblings by name (one statement) |
| `POST /areas` | `area.write` (+ `area.create` when `parentId` is absent; + `team.link` and `area.link` when `teamIds` is not empty) | `{ name, parentId?, description?, teamIds? }` → 201 `Area`, `Location: …/areas/{id}`. No `parentId`: creates a **department** (teams may be linked in the same step). With `parentId`: creates a **sub-area** under a project node or a sub-area |
| `GET /areas/{areaId}` | `area.read` | → 200 `AreaPage` |
| `PUT /areas/{areaId}/name` | `area.write` | `{ name }` → 200 `Area`; the same name changes nothing |
| `PUT /areas/{areaId}/description` | `area.write` | `{ description }` → 200 `Area` (`null`/`""` clears) |
| `PUT /areas/{areaId}/parent` | `area.write` (+ `area.create` when the destination or the current parent is the top level) | `{ parentId: Guid\|null }` → 200 `Area` with the new `path`; the parent it already has: 200, nothing changes. `null`/absent = the top level (project nodes only). A department moved to the top is the parent it already has: 200, nothing changes; a department moved under another department is `type_not_allowed` |
| `DELETE /areas/{areaId}` | `area.write` | → 204; the department's team links end with it |
| `GET /areas/{areaId}/deletion` | `area.write` | → 200 `AreaDeletion` |
| `GET /areas/{areaId}/teams` | `area.read` | → 200 `AreaTeam[]` (active teams linked to this department, by name; empty for other types) |
| `POST /areas/{areaId}/teams` | `area.link` (+ `team.link`) | `{ teamIds }` → 204 |
| `DELETE /areas/{areaId}/teams/{teamId}` | `area.link` | → 204 |

### Which operation is allowed on which type

| | department | project node | sub-area |
|---|---|---|---|
| create | yes (no parent) | **no** (with its project: `POST /projects`) | yes (parent = project node or sub-area) |
| rename | yes | **no**, `root_is_fixed` (rename the project) | yes |
| describe | yes | yes | yes |
| move | **no**, `type_not_allowed` | yes: under a department or to the top level | yes: under a node of its own project only |
| delete | yes if it has no child | **no**, `root_is_fixed` (delete the project) | yes if no child and no work item |
| team links | yes | no, `link_type_not_allowed` | no, `link_type_not_allowed` |

A project node's description is the project's own description page field in the tree; its "Команды" are `GET/POST/DELETE …/projects/{id}/teams`
(feat-028), never `/areas/{id}/teams`.

### Answers per route (beyond 401, 403 and the organization 404)

| Route | Errors |
|---|---|
| `POST /areas` | 404 `area.not_found` (parent), 404 `team.not_found` (a team named, foreign or unknown), 404 `project.not_found` (the parent belongs to a deactivated project); 400 `validation_failed` (fields `name`: `name_length`; `description`: `description_length`); 409 `area.type_not_allowed` (parent is a department), 409 `area.link_type_not_allowed` (`teamIds` with a `parentId`), 409 `area.depth_exceeded`, 409 `area.name_taken` |
| `GET /areas/{id}` | 404 `area.not_found` |
| `PUT …/name` | 404 `area.not_found`; 404 `project.not_found` (deactivated project); 400 (`name_length`); 409 `area.root_is_fixed` (project node), 409 `area.name_taken` |
| `PUT …/description` | 404 `area.not_found`; 404 `project.not_found` (deactivated project); 400 (`description_length`) |
| `PUT …/parent` | 404 `area.not_found` (the area, then the destination); 404 `project.not_found` (deactivated project); 409 `area.type_not_allowed` (a department moved under another department - to the top it is a 200 no-op; a sub-area to the top level or under a department; a project node under a sub-area, even its own, is `type_not_allowed`: the type rules answer before the cycle rule), 409 `area.outside_project` (a sub-area under another project's node or sub-area), 409 `area.move_into_own_subtree`, 409 `area.depth_exceeded`, 409 `area.name_taken` |
| `DELETE /areas/{id}` | 404 `area.not_found`; 404 `project.not_found` (deactivated project); 409 `area.root_is_fixed` (project node), 409 `area.not_empty` (a child, or a work item in the sub-area in any state) |
| `GET …/deletion` | 404 `area.not_found` |
| `POST …/teams` | 404 `area.not_found`, 404 `team.not_found`; 409 `area.link_type_not_allowed` (area is not a department) |
| `DELETE …/teams/{teamId}` | 404 `area.not_found`, 404 `team.not_found` |

## 4. The project's subtree and project creation

| Route | Code | Body → Answer |
|---|---|---|
| `GET /projects/{projectId}/areas` | `area.read` | → 200 `AreaNode[]`: the project's node and the sub-areas below it, pre-order, with the **same** `path` and `depth` as the organization tree. 404 `project.not_found` for an unknown or deactivated project |
| `POST /projects` | `organizations.project.write` (+ `area.write` when `parentAreaId` is present, `area.create` when absent; + `team.link` and `project.link` when `teamIds`) | gains **`parentAreaId?`**: the department to place the project's node under; absent = the top level. Creates the project and its node together. 404 `area.not_found`, 409 `area.type_not_allowed` (the id is not a department), 409 `area.name_taken` (a sibling at that level holds the project's name), (depth never applies: a project node is at most level 2) |

`PUT /projects/{id}/name` renames the project **and** its node (409 `area.name_taken` if the node's siblings hold the new name).
Project deactivation and restore keep the node in the tree; `projectActive` shows it. Deleting the project (deactivation is the
only removal today) leaves the node.

Removed from the first build and from mod-002: `GET|POST|DELETE /areas/{id}/projects*`, `POST|DELETE /projects/{id}/areas*`,
`POST /projects/{id}/areas`, `PUT /projects/{id}/areas/{areaId}/name|parent`. `GET /projects/{id}/areas` stays with the meaning above.

## 5. The team's end

| Route | Code (+ second) | Body → Answer |
|---|---|---|
| `GET /teams/{teamId}/areas` | `area.read` | → 200 `TeamArea[]` (the departments the team is linked to, by path) |
| `POST /teams/{teamId}/areas` | `team.link` (+ `area.link`, a second code) | `{ areaIds }` → 204; 404 `team.not_found`, 404 `area.not_found`; 409 `area.link_type_not_allowed` if any id is not a department (nothing is linked) |
| `DELETE /teams/{teamId}/areas/{areaId}` | `team.link` (no second code: BR-MX-6, the unlink has one code per end) | → 204; 404 `team.not_found`, 404 `area.not_found` |
| `POST /teams` | gains **`areaIds?`** (departments; needs `team.link` and `area.link`) | 404 `area.not_found`, 409 `area.link_type_not_allowed` |

Deleting a team ends its department links.

## 6. Work items

The work item's **«Область»** is back (mod-003 BR-M3-23/24 as before the first build's retirement): `area` on `WorkItem`, `WorkItemRow`
(`{id, path}`), `areaId` on save only (`CreateWorkItemRequest` has no area: a new item takes the project's node, or its parent's area). The area must be the item's own project's node or one of its
sub-areas: 404 `area.not_found` otherwise (a department, another project's node or sub-area, or an id of another organization); `area.outside_project` is never returned by a work item request, it belongs to the move of a sub-area (§ 3). The path in `area` is **project-relative**: the project's node is its top, whatever stands above it (`Alpha / Платформа`, not `Логистика / Alpha / Платформа`), as the picker shows it (design § 8); `GET /projects/{projectId}/areas` (§ 4) answers the full path and the panel strips the part above the project. The
offer for the field is `GET /projects/{projectId}/areas` (§ 4). An item on a sub-area blocks the sub-area's deletion
(`not_empty`). The exact fields and defaults are mod-003's `api-contract.md` as it stood before mod-018, unchanged.

## 7. Codes

New: `organizations.area.type_not_allowed` (409), `organizations.area.outside_project` (409),
`organizations.area.link_type_not_allowed` (409), `organizations.area.depth_exceeded` (409, «Дерево областей не может быть
глубже 10 уровней»). Changed text: `organizations.area.not_empty` (children, sub-areas and work items). Back in use:
`organizations.area.root_is_fixed` (a project's node changes only with its project). Unchanged: `area.not_found`, `area.name_taken`,
`area.move_into_own_subtree`, `project.not_found`, `team.not_found`, `organization.not_found`, `access.forbidden`,
`validation_failed`, `name_length`, `description_length`. Texts: `docs/error-codes.md`.
Permission codes: `organizations.area.read`, `.write`, `.create`, `.link` (area.link no longer covers projects).

## 8. The structure contract (`IOrganizationStructure`)

By-id reads taking the organization, answering nothing for a foreign id: `FindAreaAsync` (with `Type`, `ProjectId`, `Path`; no production caller in this line, it is the read for mod-019 and feat-042),
`ListAreasOfTeamAsync`. There is no `ListAreasOfProjectAsync`: a project's subtree is `GET /projects/{id}/areas`. `ProjectInfo.RootAreaId` stays and is the project's node (a new item takes it).

## 9. The upgrade

The migration `OrganizationAreaWorkspace` turns each project's existing area tree into a project node (the old root) with
sub-areas, at the top level; work items keep their `area_id`. It fails, naming the project, if a project's tree is deeper than
10 levels (the area tree depth is 10 counted from the top level, a project node being level 1). No data is lost.
