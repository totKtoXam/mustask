# API contract: feat-061-sight-in-structure-lists

Fixed before the code (stage 4a). No new route, no new field, no new error code, no migration. Base: feat-041's
[api-contract](../feat-041-project-visibility/api-contract.md) (§ 1 answer order, § 5 "Not changed here", which this file fulfils for the
surfaces below), mod-016, mod-018, mod-019, feat-028, feat-043 contracts. Routes are under `/api/organizations/{organizationId}`; the
shapes are those of the cited contracts, only the **set of rows** changes.

## The rule (doc-016 BR-TD-12..-15, BR-TD-17)

For a non-administrator each list below returns exactly the departments, teams and projects the caller sees: a project through a reaching
grant, a linked team's membership or «Видим всей организации»; a team through membership, a grant on it or its department, or seeing a
linked project; a department through a grant or seeing a team or project standing in it; a project node and its sub-areas exactly when
their project is seen. The set is narrowed **before** any cut or count, so no total, count, `ещё` threshold, nested list or empty text
reveals a hidden object. A visible object never lists a hidden one nested in it (BR-TD-14). The administrator sees everything. A sight
change applies on the next request. An unknown, foreign and hidden object are not told apart: a hidden one is simply absent.

## Changed reads (rows only)

| Route | Now returns |
|---|---|
| `GET /selector-offers` | `departments`, `projects`, `teams` limited to the visible ones; a project's `teamIds` lists only visible teams |
| `GET /overview` | `departmentCount`, `projectCount`, `teamCount` count the visible objects; `departments`, `projects`, `teams` are the first 5 **of the visible** by name; `myTeams` as before (the caller's own teams are visible by membership) |
| `GET /projects`, `GET /projects?linkableFor=` | the visible active projects (the link filter runs over them) |
| `GET /projects/deactivated` | the visible deactivated projects (holders of `project.deactivate`) |
| `GET /teams`, `GET /teams?linkableFor=` | the visible teams; each team's `projects` lists only the visible projects (seeing a team does not disclose its projects) |
| `GET /teams/{teamId}/projects` | the team's linked projects the caller sees |
| `GET /projects/{projectId}/teams`, `GET /projects/{projectId}/team-links` | the linked teams the caller sees |
| `GET /areas`, `GET /areas?linkableFor=` (tree and list) | the visible nodes, pre-order: departments seen, project nodes and sub-areas of seen projects |
| `GET /areas/{areaId}` | `children` lists the visible children only; a hidden `areaId` answers 404 `organizations.area.not_found` as before |
| `GET /areas/{areaId}/teams`, `GET /areas/{areaId}/deletion` | the visible teams standing in the department |
| `GET /areas/writable-ids` | the ids of visible nodes where the caller holds `area.write` |

No status codes, bodies' property names or error codes change. A list the caller may read and that holds nothing visible is `[]` (never a
403 and never a different text).

## «Структура» filter (BR-TD-21)

Done by the client over the lists above: they carry only visible objects, so the match cannot find or reveal a hidden one. There is no
new parameter and no server-side match on this surface.

## Statement counts (feat-041 R-7)

Unchanged: the access set is read once per request (8 statements for a non-administrator, none for the administrator) and shared by the
handler; `selector-offers` stays at 4 statements and `overview` at 7 after the set.

## Not changed here

Multi-project item views, search, work-item counts and the sprint clauses (feat-062); the context path button and its panel search
(feat-063).
