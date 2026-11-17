# Plan (backend): feat-041-project-visibility

Route L from stage 4. Requirements: `doc-016-teams-under-departments` (`spec.md` BR-TD-12..-16, OQ-4 A, OQ-5 A; story US-6,
its core; `design.md`), decisions in [ADR-0040](../../adr/0040-teams-in-departments-cascading-selectors-sight-by-participation.md) §§ 4-5
(accepted) and [ADR-0029](../../adr/0029-scoped-effective-permissions.md) §§ 6-8, 11. Contract: [api-contract.md](api-contract.md).
No ADR is needed: the decision is ADR-0040's. Lanes: none (one connected change of the evaluator and its gate).

## Model and migration (additive)

`20261116104040_ProjectVisibleToOrganization`: `projects.visible_to_organization boolean NOT NULL DEFAULT false` (existing and new
projects are off, OQ-5 A). The same migration writes the upgrade's log (OQ-5 A; decision: **the migration**, not a startup step, so
the log is part of the one transactional change and survives the run): table `project_sight_upgrade_log` (one row per project and
member who saw it before, every member did, and does not after: not an administrator, no project grant, no department grant through
the node with its switch on, no membership of a linked team) and a `RAISE NOTICE` per project with the count. Not mapped by the
model; the MR quotes `SELECT project_key, count(*) FROM project_sight_upgrade_log GROUP BY 1`. Down drops the log and the column.

## Domain

- `Project.VisibleToOrganization`, `SetVisibleToOrganization(bool)` (a no-op returns false).
- `SightRules` (pure): `SeesProject`, `SeesTeam`, `SeesDepartment` over facts (flag, a reaching grant, a linked team's membership,
  a grant on the team or its department, seeing a linked project, a team or project inside). The administrator sees all.
- `PermissionCatalog.OrganizationsVisibilityWrite` (`organizations.visibility.write`, scope Project): held by the project
  «Менеджер», the department «Менеджер» (on the projects its grant reaches, by inheritance) and the administrator.
  `contracts/access/role-bundles.json` regenerated.

## Evaluator (Application) and the gate (Api)

- `SightData` (projects with flag, teams with department, links, the caller's team memberships), read with the set form
  (`ScopedAccessReadModel.LoadSetAsync`, four more indexed statements; nothing for the administrator).
- `AccessSet.SeesProject / SeesTeam / SeesArea` (a project node and its sub-areas follow their project, a department BR-TD-14) and
  `KnowsProject / KnowsTeam / KnowsArea`. The reach walk is the existing one: the project grant, or a department grant through its
  node while «Наследовать доступ» is on.
- `OrganizationPermissionHandler` gains the **sight gate** after membership and before the code check (ADR-0029 § 8 items 3-4): for a
  non-administrator, the path's `projectId`, then `teamId` / `areaId`, missing or hidden, answer 404 `organizations.project.not_found` /
  `organizations.team.not_found` / `organizations.area.not_found` before any 403 (new failure kind `ObjectNotFound`, same
  renderer). The query's `teamId`, `departmentId` and `projectId` that name an object the caller does not see answer the same.
  A malformed or unknown path id is a 404 for a non-administrator, so a viewer's write to a missing project is «Проект не найден»
  (`RetrofitAccessTests` restated, as ADR-0029 § 8 orders). The set read is kept in the request for the handlers.
- Codes on what one sees (BR-TD-16): the gate hides the object; on a seen one the unchanged union of the role's bundle and the
  reaching grants applies. Nothing in the union changes.

## Slices

| Slice | Change |
|---|---|
| SetProjectVisibility (new) | `PUT /projects/{projectId}/visibility`, `organizations.visibility.write` on the project |
| GetProject | `visibleToOrganization` in the read |
| GetAssigneeCandidates | with `projectId`, only those who see the project (inverse form `IScopedAccessReadModel.UsersSeeingProjectAsync`) |
| SaveWorkItem | a newly set assignee must see the project, else the existing 409 `assignee_unavailable` |

## Not in scope (feat-061, feat-062)

Lists, selectors, «Структура», «Обзор», pickers, multi-project views, search and counts. The surfaces that read the set form
(`GetProject`'s codes, the tree's writable ids) gain nothing and lose nothing: the set form still answers codes. The lists that read the
organization's projects through a read model (`GetProjects`, the aggregate views) are **not narrowed** here; the gate narrows only the
addresses, the `teamId` / `departmentId` / `projectId` of a query included.

## Tests

Domain (`SightRules`, bundles, project flag), Application (`AccessSet` sight), Infrastructure (migration on a populated cast with the
log, inverse-form parity with the set form), Api (the scenarios of US-6 on addresses: sight by grant, link, flag, department grants,
hidden answers and the order, the flag write and its codes, assignee). Existing fixtures build projects visible to the organization
(`OrganizationsTestData`) so the legacy suites keep their meaning.
