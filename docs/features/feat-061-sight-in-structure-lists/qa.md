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
| Pickers (`linkableFor`) | a caller holding the link codes by grants (Manager on «Логистика», VENUS, MARS) gets exactly the visible linkable objects, no hidden one | `TheLinkPickers_OfferExactlyTheVisibleLinkableObjects...` | pass (non-empty exact sets, after review R-1) |
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

## Frontend

Plan written from doc-016 spec (BR-TD-17, BR-TD-21, US-6), design.md § 3.1 and § 7.2, and feat-041 review R-4, before the code was read. Sight itself is the server's (backend QA); the frontend must not add a hidden object back and must not invent a second match.

### Coverage matrix

| Criterion | Check | Test | Status |
|---|---|---|---|
| BR-TD-21 label | placeholder «Найти департамент, команду или проект» exact (design § 7.2) | `AreasPage.test.tsx` (field) | pass |
| ~360 px, under the title | `fieldWidth={360}` in the filter band under the title | code read | pass |
| From the first character, case-insensitive | «prm» hits at 3 chars; «СТАРЫЙ» upper case finds a team | AreasPage, `structureFilter.test.ts` | pass |
| Name and project key | key `WH`/`BUD` via `GET projects`; no key given, no match | `structureFilter.test.ts` | pass |
| Hides non-matches, a department stays while a child matches | ancestors kept; matching department keeps its descendants (design § 7.2) | `structureFilter.test.ts` | pass |
| Not remembered, never navigates | plain `useState`; a fresh mount starts empty; the filter only narrows rows | added `is not remembered` | pass |
| «Ничего не найдено» | frame text | AreasPage | pass |
| Esc / × clears | Esc and «Очистить фильтры» | AreasPage, added | pass |
| Sight-scoped | candidates are the server-narrowed tree, projects and teams reads (both narrowed by backend QA); no client list of hidden objects | code read + backend `StructureListsSightTests` | pass |
| R-4 selectors never offer a hidden object | offers, remembered-selection repair, «Остальные» read only server offers | unchanged code, backend offers tests | pass |
| R-4 gone selection: no generic error, no «Повторить» | backlog, flat «Рабочие элементы», board answer the not-found frame, generic error kept for other failures | `AggregateViews.gone.test.tsx` (backlog, flat, board added, 500) | pass |

Copy checked exactly against design.md: label, «Ничего не найдено», «Очистить фильтры» (existing FilterBar). Not checked in a browser (no dev server, economy).

### Deviations judged

| Developer deviation | Verdict |
|---|---|
| Filter acts on the existing «Дерево» tab; «Список»/«Дерево» redesign is not in this branch | Acceptable. The design's views do not exist here; the tree view the page has is the one design § 7.2 describes for «Дерево». Nothing hidden can show on it. |
| Diagram's own search («Найти узел») not narrowed; the field is absent on the diagram tab | Acceptable, minor (m-2): BR-TD-21 says the field filters "whichever view is open", but the diagram has its own search and, like all views, is built from the server-narrowed tree, so no leak. |
| A team match keeps only its department | Acceptable, minor (m-3): the tree has no team rows, so the user finds a department with no visible reason; the design's list cards show team chips. Closes with the «Список» redesign. |
| Stale-offers 404 shows the object's not-found frame, not the ADR-0032 § 6 repair notice | Acceptable. R-4 asks for no generic error and no «Повторить»; the frame with navigation kept ends that. The remembered-selection repair stays the path for the stored selection. |

### Tests added

- `AreasPage.test.tsx` +3: a team name finds its department and keeps nothing else; «Очистить фильтры» empties the field and the diagram tab has no field; not remembered across a fresh visit.
- `AggregateViews.gone.test.tsx` +1: the board shows «Область не найдена» without «Повторить».

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-17 | `vitest run` structureFilter.test.ts, AreasPage.test.tsx, AggregateViews.gone.test.tsx | 6, 32, 4 passed |
| 2026-11-17 | ESLint, Prettier on the two touched test files | clean (after `prettier --write`) |

Developer's other runs (6 `AggregateViews*` files, `tsc -b`) not repeated. E2E: not asked, standing answer NO.

### Defects

- m-2 (minor): the diagram tab has no «Структура» filter field; "whichever view is open" is only half met. Debt to gate-3, with the «Список»/«Дерево» redesign.
- m-3 (minor): a team match highlights only its department; a sub-area (type `area`) name also matches though the spec names only departments, teams, projects and keys. Debt to gate-3.

No blocker or major found.

### Verdict

Frontend QA passed: no blocker, no major, two minors. Stage: 8-review.
