# API contract: Area, project and team navigation (feat-043)

Stage 4 artifact. **Additive only**: two new reads, nothing existing changes in route, shape, code or test. Everything else this line shows is served by reads that exist (`GET /areas`, `…/projects`, `…/projects/deactivated`, `…/teams`, `…/teams/{t}/projects`, `…/projects/{p}/teams`, the link, unlink and placement writes). The work-item and view reads of `feat-048-aggregate-level-views` are not here. Errors are the catalogue's (`docs/error-codes.md`); this line adds no code.

Conventions as the sibling contracts (mod-002, feat-028 § 0): camelCase JSON, ids as GUID strings in responses, a route parameter is an untyped string and a malformed one answers the level's not-found (BR-M1-20), lists are in the order the server gives and the panel renders them as received, no envelope (a bare object).

## 0. Access and the caller

- Both routes are `GET /api/organizations/{organizationId}/…` and declare the organization's own read code, `organizations.organization.read` (`RequiresOrganizationPermission`); every role holds it, so «Администратор», «Участник» and «Читатель» read. The scoped check answers first: 401 without a token, **404 `organizations.organization.not_found`** for a non-member (an instance administrator included) and for a deactivated organization, never a 403. Until `mod-019` the role bundle decides; mod-019 may split the code per kind.
- **BR-NV-15.** Each read takes the caller (`CallerAccess`: organization, user, role) as its first, required argument, so that `feat-041-project-visibility` adds the set of visible projects inside it. A signature test pins it (`NavigationReadSignatureTests`).
- Reachability (ADR-0026): a deactivated project, a deleted team and another organization's rows never appear, in a count, a row or a link.

## 1. `GET /api/organizations/{organizationId}/overview`: «Обзор» (US-2)

The three counts are of every object; the lists hold only the first `5` by name (Russian collation, letter case not deciding, then id); `myTeams` are all of the caller's own teams by name. **Seven statements** whatever the volume (three counts, three first-rows reads, the caller's teams); a test pins it at 5 and at 200 projects.

200 OK:

```json
{
  "departmentCount": 2,
  "projectCount": 4,
  "teamCount": 3,
  "departments": [ { "id": "…", "name": "Логистика" } ],
  "projects":    [ { "id": "…", "name": "A", "key": "A" } ],
  "teams":       [ { "id": "…", "name": "MARS", "isMember": false } ],
  "myTeams":     [ { "id": "…", "name": "VENUS", "isMember": true } ]
}
```

- `departmentCount` counts **departments only** (OQ-2 A): project nodes count as projects, sub-areas in nothing. `projectCount` is the active projects (BR-NV-13). `teamCount` is the active teams.
- The panel renders the links «Все … (N)» from the counts, the user's organizations beside it from the existing `GET /api/organizations` (first five client-side).
- Errors: 401; 404 `organizations.organization.not_found`.

## 2. `GET /api/organizations/{organizationId}/selector-offers`: the selectors and the lists (US-2, US-8)

Every department, active project and active team of the organization, by name, with the links the panel judges BR-NV-17's fit from. Bounded by the organization's own volume (1 000 areas, 200 projects, 200 teams, a department being a top-level node); **five statements** whatever the volume (departments, projects with their place, project–team links, teams with the caller's membership, department–team links). It serves the four selectors' offers, the «Остальные» split (computed on the panel) and the rows of the list «Проекты» (name, key, place). «Команды» keeps feat-028's `GET …/teams` (the active teams with their projects and the mark «Моя команда»), and «Области» keeps `GET …/areas` (the tree with paths), both unchanged.

200 OK:

```json
{
  "departments": [ { "id": "…", "name": "Логистика" } ],
  "projects": [
    { "id": "…", "name": "A", "key": "A",
      "departmentId": "…", "departmentName": "Логистика",
      "teamIds": [ "…", "…" ] }
  ],
  "teams": [
    { "id": "…", "name": "MARS", "isMember": false, "departmentIds": [ "…" ] }
  ]
}
```

- `departmentId` / `departmentName` are `null` for a project at the top level; a project's place is the department holding its node (ADR-0031).
- `teamIds` are the **active teams linked to the project**; `departmentIds` are the departments **linked to the team directly** (mod-018 BR-AW-8). The panel derives the rest: a project fits department D when `departmentId = D`, team T when `T ∈ teamIds`; a team fits department D when `D ∈ departmentIds` or when it is linked to a project placed in D; a department fits project P when it is P's `departmentId`, team T when `D ∈ T.departmentIds` or T is linked to a project placed in D.
- Project nodes, sub-areas, deactivated and deleted objects are not in it (BR-NV-17). The deactivated projects of the administrator's «Деактивированные» view stay on the existing `GET …/projects/deactivated`.
- Errors: 401; 404 `organizations.organization.not_found`.

## 3. Not in this contract

- No retired API address: the retired addresses of BR-NV-6 are panel routes, no API route is retired or changed. The retired-address table and the menu table are therefore one frontend source (ADR-0032 §§ 2, 4; plan-backend § 6).
- No batch move of projects (H-3 A), no overview figures (feat-042), no work-item or view read (feat-048), no per-object code (mod-019).
- Existing shapes the panel keeps using unchanged: `TeamResponse.isMember` (an ordering and marking flag; it chooses no team since feat-043), the area tree `GET …/areas`, the link and unlink writes.
