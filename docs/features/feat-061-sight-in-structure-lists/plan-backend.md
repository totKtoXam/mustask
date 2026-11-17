# Plan (backend): feat-061-sight-in-structure-lists

Route L from stage 4. Requirements: `doc-016-teams-under-departments` (`spec.md` BR-TD-12..-17, BR-TD-21; story US-6, surfaces part 1;
`design.md`), decisions in [ADR-0040](../../adr/0040-teams-in-departments-cascading-selectors-sight-by-participation.md) §§ 4-5 and
[ADR-0029](../../adr/0029-scoped-effective-permissions.md) § 7. Contract: [api-contract.md](api-contract.md). No ADR: the rule is
feat-041's, and this task only applies it. No migration, no new route, no new error code. Lanes: none (one connected change).

## The one rule

feat-041's set form: `AccessSet.SeesProject / SeesTeam / SeesArea` over the per-request set (`IOrganizationAccess.SetAsync`, 8
statements for a non-administrator, none for the administrator, read once and kept in the request). No second implementation:

- `AccessSet` gains `Sighted()`, the ids of the visible projects, teams and departments (`SightedIds`; `null` for the administrator),
  decided by the same three `Sees*` methods. It is for the two reads that must narrow in the database (counts and a cut).
- `AccessSet` memoizes `SeesProject` / `SeesTeam` and indexes the team-to-projects, department-to-teams and department-to-projects
  walks in memory, so a whole-list decision is linear, not quadratic (R-7: no extra statement; the set is read once).
- `CallerAccess` gains a trailing `Sight` (`SightedIds?`). `NavigationReadModel` throws for a non-administrator caller that carries none:
  a read written without the sight fails closed, it never returns the whole organization.

## Surfaces and how each is narrowed

| Surface (route) | Read | Narrowed by |
|---|---|---|
| Header selector offers `GET selector-offers` | `INavigationReadModel.ReadSelectorOffersAsync` | `caller.Sight` inside the four statements (departments, projects, links, teams); still 4 statements |
| «Обзор» `GET overview` | `INavigationReadModel.ReadOverviewAsync` | `caller.Sight` in every statement, so the three counts and the three first-5 reads run over the visible set; still 7 statements |
| «Проекты» `GET projects` (and `?linkableFor`) | `IProjectReadModel.ListAsync` | handler, `set.SeesProject` before the link filter and before the response (no paging on this route) |
| `GET projects/deactivated` | `IProjectReadModel.ListAsync` | handler, `set.SeesProject` |
| «Команды» `GET teams` (and `?linkableFor`) | `ITeamReadModel.ListOrganizationTeamsAsync` | handler, `set.SeesTeam` on the rows and `set.SeesProject` on each team's nested projects (seeing a team does not disclose its projects, BR-TD-13) |
| `GET teams/{t}/projects` | `ITeamReadModel.ListProjectsOfTeamAsync` | `set.SeesProject` |
| `GET projects/{p}/teams`, `GET projects/{p}/team-links` | `ITeamReadModel.ListLinkedAsync`, `IOrganizationStructure.ListLinkedTeamsAsync` | `set.SeesTeam` (a team linked to a visible project is visible; applied so the rule, not the data, decides) |
| «Структура», list and tree `GET areas` (and `?linkableFor=area`) | `IAreaTreeReadModel.GetRowsAsync` | `set.SeesArea` per node (a department BR-TD-14; a project node and its sub-areas follow their project); paths are built from the full tree, a visible node's ancestors are visible by construction (departments are top-level) |
| A department's or node's page `GET areas/{a}` | same | `children` narrowed by `set.SeesArea`; a hidden `areaId` is a 404 (the gate already answers it, repeated in the handler) |
| `GET areas/{a}/teams`, `GET areas/{a}/deletion` (the pickers of a department's teams) | `IAreaTreeReadModel.ListTeamsAsync` | `set.SeesTeam` |
| `GET areas/writable-ids` (move and add targets) | `IAreaTreeReadModel.GetRowsAsync` | `set.SeesArea` before the code test |

Already narrowed by feat-041 and unchanged: every object address, the query ids (`teamId`, `departmentId`, `projectId`), `GET projects/{p}`,
`GET teams/{t}`, assignee candidates.

## Paging and counts

Only «Обзор» cuts a list (the first five by name) and counts: its counts and its cut run over the sight-narrowed queries (`ids.Contains`
over an array parameter), so no total, no `ещё` threshold and no row is ever taken from a hidden object. The other lists have no paging;
they are narrowed before they are returned, so their `length` is the count.

Deliberate deviation from `docs/conventions/backend.md` (no client-side filtering after `ToListAsync()`): the handler-side lists
(projects, teams, areas tree, area teams/deletion, team projects, project teams/team-links, writable ids, deactivated projects) apply
`AccessSet.Sees*` in memory after the read. Allowed because these lists are unpaged whole-organization lists, the rows read are those read
before, the statement count is fixed, and sight is decided by the one rule (mod-019 `linkableFor` `Holds` set the precedent). Rule: a
list that gains paging moves the filter into SQL through `AccessSet.Sighted()` ids (as «Обзор» and selector-offers already do).

## «Структура» filter (BR-TD-21)

Client-side over the already narrowed tree: the page reads `GET areas` (and the team and project lists), which now carry only visible
objects, and matches the typed text in the browser. No new endpoint and no server-side match on this surface; the context panel's
global search (feat-063) is out of scope. The filter therefore cannot find or reveal a hidden object, because it never receives one.

## Statement counts (R-7)

Unchanged: the set read stays at its 8 statements per request (the handlers share the request's set); the navigation reads keep 7 and 4;
`ScopedAccessStatementCountTests` is not touched and must stay green. The id arrays are parameters, not statements.

## Tests

- Application: `AccessSetSightTests` (`Sighted()` equals the `Sees*` answers for every kind, the administrator is `null`, a team grant does
  not disclose a project); `NavigationReadsHandlerTests` (a non-administrator read carries the sight).
- Api integration: `StructureListsSightTests`, the US-6 cast (A, B in «Логистика»; C, D in «Финансы»; VENUS, MARS, JUPITER) plus six hidden
  filler objects per kind sorted first: for Olga, Vera and Max every surface above is asserted for items and for counts; the administrator
  sees all; «Обзор»'s cut runs after the narrowing; a team's nested projects; the flag, the switch and the membership change on the next
  request; the link pickers.
- Infrastructure: `NavigationReadModelTests` (statement counts; the read model fails closed for a non-administrator without sight; a
  sight narrows counts and rows).

## Not in this task

Multi-project item views, the search endpoint, work-item counts and sprint clauses (feat-062); the context path button and its panel search
(feat-063); a `GET areas/{a}/deletion` count of work items is not read.
