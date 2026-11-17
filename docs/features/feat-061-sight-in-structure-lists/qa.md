# QA — feat-061-sight-in-structure-lists

## Backend

Plan written from doc-016 spec (BR-TD-12..-17, BR-TD-21, US-6) and api-contract before the code was read.

### Coverage matrix

| Criterion | Check | Test | Status |
|---|---|---|---|
| BR-TD-17 / US-6 Olga, Vera, Max | items AND counts (`departmentCount`, `projectCount`, `teamCount`), nested lists, on selector-offers, overview, projects, teams, areas tree, writable-ids; 6 hidden filler per kind sorted first | `StructureListsSightTests` (3 persona tests) | pass |
| Admin sees all | every surface | `Ann_...` | pass |
| «Обзор» cut after narrowing | filler sorted before visible | `Overview_TheCutIs...` | pass |
| Visible team does not disclose its projects (D off on node) | teams nested projects, offers, overview | `SeeingATeam...` | pass |
| Visible department shows only visible children | area page, `areas/{a}/teams`, `teams/{t}/projects`, `projects/{p}/team-links`, `/teams` | `ADepartmentsPage...` | pass |
| Flag on D | lists, tree | `TheFlagOnD...` | pass |
| Sight changes on next request | add/remove team member | `SightChanges...` | pass |
| Pickers (`linkableFor`) | narrowed before link filter | `TheLinkPickers...` | pass |
| Hidden area / project / team addresses | 404 with own code on areas/{a}, /teams, /deletion, node, projects/{p}/teams, /team-links, /areas, teams/{t}/projects | added `HiddenObjectsListRoutes_AnswerNotFound...` | pass |
| Deactivated project | leaves offers, counts, projects | added `ADeactivatedVisibleProject...` | pass |
| Fail closed | read model throws for non-admin without sight; statement counts 7/4 | `NavigationReadModelTests` (developer) | pass |
| BR-TD-21 filter | client-side over narrowed lists, no endpoint | frontend QA | n/a here |

### Additional scenarios checked by reading the Organizations read endpoints

All `MapGet` of Features/Organizations reviewed. Narrowed: selector-offers, overview, projects, projects/deactivated, teams, teams/{t}/projects, projects/{p}/teams|team-links, areas, areas/{a}, areas/{a}/teams|deletion, writable-ids. Not narrowed but not leaking: `GetOrganizations/GetOrganization` (no counts), `teams/{t}/deletion-counts` and `projects/deactivated` (organization-source codes held by the administrator role only: `OrganizationRoleBundles` Admin = all org codes, Member/Viewer read codes), grants lists (per visible object), `projects/{p}/areas` (project gate), members lists (no object names). Aggregate views (`/backlogs|boards|workitems/groups`) name teams/projects: out of scope (feat-062).

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-17 | `dotnet test --filter StructureListsSightTests` (Api.IntegrationTests), before and after adding 2 tests | 10 passed, then 12 passed (one first run of a new test failed on my expectation, see m-1; expectation fixed to the rule) |

Developer's runs (Application 889, Infrastructure Organizations 215, Api Organizations 1657/1658, `UnlinkTeamVolumeTests` timing flake under load) were not repeated.

### Defects

#### m-1: a team (and its department) stays visible through a deactivated project that appears on no surface

- Severity: minor
- Steps: Olga (member of VENUS, sees A; MARS linked to A and B). Admin deactivates A. Olga's `selector-offers`/`overview` list MARS (teamCount 2) with no project A anywhere.
- Expected / Actual: sight data counts deactivated projects, as feat-041's `AccessSet` does, so the team stays by BR-TD-13 "sees a project linked to it". No hidden object is revealed (Olga was entitled to A); the cosmetic effect is a team/department justified by an invisible project. Same rule as feat-041, left as is; the test pins it.
- Status: open, carried to gate-3 as debt.

No blocker or major found.

### Verdict

Backend QA passed: no blocker, no major, one minor. E2E: not asked (standing answer NO).
