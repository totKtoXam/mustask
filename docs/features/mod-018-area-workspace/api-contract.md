# API contract: the organization's area workspace (mod-018)

Fixed at stage 04 (backend plan). Rules are `spec.md` (BR-AW-n) and `stories/`; this file is the interface the
frontend reads. A change after this point is a note in `status.md` and a Log line; once stage 6 has started it is
escalated to the orchestrator. JSON is camelCase; ids are GUID strings; errors are ProblemDetails with `errorCode`
(registry: `docs/error-codes.md`, texts in the spec's § Error codes).

## 0. Conventions

- **Address.** Every route is under `/api/organizations/{organizationId}`. An area, a project and a team are each addressed
  under the organization alone (ADR-0028 rule 4, § 7); no route reads or writes an area through a project's address except the
  project's own end of the links (§ 3).
- **Order of answers (BR-AW-15).** 401; 404 `organizations.organization.not_found` for a non-member (the scoped check); 403
  `organizations.access.forbidden` for a role lacking the route's declared code; then the **second codes**
  (`area.create`, `area.link`, the other end's link code) — **first of all, before any id is looked at**, as feat-028 (D-7); then the
  not-found of the address outside in; 400 (values, every reason at once); the other ids a body names, each with its own 404
  (`area.not_found`, `project.not_found`, `team.not_found`); then 409. A malformed, unknown, foreign or deleted id is the same
  404 of its level (BR-AW-1). Until `mod-019-scoped-access` a code is checked against the organization role alone.
- **Declared codes.** `organizations.area.read` on reads; `area.write` on create, rename, describe, move, delete; `area.link` on an
  area's link routes; `project.link` / `team.link` on a project's / team's end. Handlers add: `area.create` for a top-level creation and
  for a move to or from the top level; the other end's link code for a link; the link codes of every end a creation names.
- **Description.** Optional, kept as typed, at most 2 000 characters (UTF-16 units); `null`, absent and `""` are none.
  Name: trimmed 1-100, unique among siblings ignoring case and surrounding spaces; top-level areas are siblings.
- **Path.** Names from the top level joined by `" / "`; level 1 is the top level; the tree is at most 10 levels deep.
- **Links.** All-or-none per request; a linked pair re-linked, or an unlinked pair unlinked, is a success that changes nothing.
  Only active projects (not deactivated) and active teams (not deleted) of the organization are linked or listed.
- `inheritsAccess` is stored (always `true`) and is **in no response and no request** (BR-AW-5, OQ-2 B).

## 1. Shapes

```
AreaNode      { id, parentId: Guid|null, name, description: string|null, path, depth }   // pre-order, siblings by name
Area          { id, parentId: Guid|null, name, description: string|null, path }          // every area write returns it
AreaRef       { id, name, path }
AreaPage      { id, parentId, name, description, path, parent: AreaRef|null,
                children: [{ id, name, description }] }                                  // direct children, by name
LinkedProject { id, name, key }          LinkedTeam { id, name }
AreaDeletion  { projects: [{ id, name, key, active }], teams: [{ id, name }] }           // every link, deactivated projects included
```

## 2. The organization's areas

| Route | Code (declared) | Body → Answer |
|---|---|---|
| `GET /areas` | `area.read` | → 200 `AreaNode[]` (the whole tree, pre-order; 1 statement) |
| `POST /areas` | `area.write` (+ `area.create` top level; + `area.link` and `project.link` / `team.link` when `projectIds` / `teamIds` are not empty) | `{ name, parentId?, description?, projectIds?, teamIds? }` → 201 `Area`, `Location` `/areas/{id}` |
| `GET /areas/{areaId}` | `area.read` | → 200 `AreaPage` |
| `PUT /areas/{areaId}/name` | `area.write` | `{ name }` → 200 `Area` |
| `PUT /areas/{areaId}/description` | `area.write` | `{ description }` → 200 `Area` (empty clears) |
| `PUT /areas/{areaId}/parent` | `area.write` (+ `area.create` when the destination or the current parent is the top level) | `{ parentId: Guid|null }` (`null` or absent: the top level) → 200 `Area`; the parent it already has: 200, nothing changes |
| `DELETE /areas/{areaId}` | `area.write` | → 204; its links end with it |
| `GET /areas/{areaId}/deletion` | `area.write` | → 200 `AreaDeletion` (what the deletion ends) |

Answers: create — 404 `area.not_found` (parent), 404 `project.not_found` / `team.not_found` (an end named), 400
`validation_failed` (`name`: `name_length`; `description`: `description_length`), 409 `area.name_taken`, 409 `area.depth_exceeded`.
Rename/describe — 404 `area.not_found`, 400, 409 `area.name_taken` (rename). Move — 404 `area.not_found` (the area, then the
destination), 409 `area.move_into_own_subtree`, `area.depth_exceeded`, `area.name_taken`. Delete — 404, 409 `area.not_empty`
(a child exists; text «Нельзя удалить область, в которой есть вложенные области»).

## 3. The links

| Route | Code (declared; second) | Body → Answer |
|---|---|---|
| `GET /areas/{areaId}/projects` | `area.read` | → 200 `LinkedProject[]` (active projects, by name; the area's own links only) |
| `POST /areas/{areaId}/projects` | `area.link` (`project.link`) | `{ projectIds }` → 204 |
| `DELETE /areas/{areaId}/projects/{projectId}` | `area.link` alone | → 204 |
| `GET /areas/{areaId}/teams` | `area.read` | → 200 `LinkedTeam[]` |
| `POST /areas/{areaId}/teams` | `area.link` (`team.link`) | `{ teamIds }` → 204 |
| `DELETE /areas/{areaId}/teams/{teamId}` | `area.link` alone | → 204 |
| `GET /projects/{projectId}/areas` | `area.read` | → 200 `AreaRef[]` (the project's linked areas, by path) |
| `POST /projects/{projectId}/areas` | `project.link` (`area.link`) | `{ areaIds }` → 204 |
| `DELETE /projects/{projectId}/areas/{areaId}` | `project.link` alone | → 204 |
| `GET /teams/{teamId}/areas` | `area.read` | → 200 `AreaRef[]` |
| `POST /teams/{teamId}/areas` | `team.link` (`area.link`) | `{ areaIds }` → 204 |
| `DELETE /teams/{teamId}/areas/{areaId}` | `team.link` alone | → 204 |

An absent, `null` or empty list is a 204 no-op. The address object is judged first (area / project / team 404; a deactivated
project or a deleted team of the address is that 404 too, so a link or an unlink naming it changes nothing), then the ids in the body.
`GET /projects/{projectId}/areas` **replaces** mod-002's tree read at that path; mod-002 § 4's `POST /projects/{projectId}/areas`
(create), `PUT …/areas/{areaId}/name`, `PUT …/areas/{areaId}/parent` and the old `DELETE …/areas/{areaId}` are **gone** (a
`DELETE` there now unlinks). No other route is kept under a project's address for the tree.

## 4. Creation with links

- `POST /projects` gains `areaIds?` (each an area of the organization, 404 `area.not_found`; needs `project.link` and `area.link`).
- `POST /teams` gains `areaIds?` (same, with `team.link`).
- `POST /areas` takes `projectIds?`, `teamIds?` (§ 2). Each is all or nothing; nothing is created when one is refused.

## 5. Retired from other contracts (breaks, P-10)

- mod-002 § 4 (the project-nested tree routes); § 6 `organizations.area.root_is_fixed` (deprecated, entry stays) and
  `organizations.area.not_empty`'s text; § 7 `ProjectInfo.RootAreaId` goes.
- mod-003 § 1.1 and the item routes: no `area` in `WorkItem`, `WorkItemRow` or a backlog row; `areaId` in a save and an
  unknown property on a create are ignored; the `area.not_found` answer of a save is gone; the list reads no area path.
- `IAreaUsage` (the delete guard) is gone; `OrganizationAccessInventoryTests` and `GET …/access` list the new codes
  (`area.create`, `area.link`; `area.read`, `area.write` redefined to the organization tree).

## 6. Codes

`organizations.area.not_found`, `.name_taken`, `.move_into_own_subtree`, `.not_empty` (new text), **new** `.depth_exceeded`
(409, «Дерево областей не может быть глубже 10 уровней»), `organizations.project.not_found`, `.team.not_found`,
`.organization.not_found`, `.access.forbidden`, `organizations.validation_failed`, `.name_length`, `.description_length`.
Deprecated: `organizations.area.root_is_fixed`.

## 7. The structure contract (`IOrganizationStructure`)

New by-id reads, all taking the organization and answering nothing for a foreign id (ADR-0028 rule 4): `FindAreaAsync`,
`ListAreasOfProjectAsync`, `ListAreasOfTeamAsync`, `ListLinkedProjectsOfAreaAsync(areaId, includeDescendants)` (rule 8's
area-to-projects resolution: each project once, deactivated left out, one statement). `ProjectInfo.RootAreaId` is removed.
