# QA — Team surfaces across projects (feat-040)

One QA pass over both layers (L, light route), clean context. The plan below was written from `spec.md`, `stories/`,
`api-contract.md` and `design.md` before any implementation or developer plan was read. Status: ☐ not yet checked.

## Backend

### Coverage matrix (plan)

| # | Criterion | Check | Type | Status |
|---|---|---|---|---|
| B1 | US-1 / BR-TS-2 | team view holds its items of every active project (5+10+7), not VENUS's, not unowned | integration | ☐ |
| B2 | US-1 / BR-TS-1 | levels epics/features/stories; `tasks` and junk → `boards.level.not_found`; a level the process lacks too | integration | ☐ |
| B3 | US-1 / BR-TS-2 | Closed shown, Removed (and other-level) not | integration | ☐ |
| B4 | US-1 / BR-TS-2 | beneath a row only the team's own children; the project view shows both | integration | ☐ |
| B5 | US-1 / BR-TS-3 | `project {id,name}` on every row, child, card of team AND project views; rename shows at once | integration | ☐ |
| B6 | US-1 / feat-039 | project views unchanged: all 11 stories of Alpha | integration | ☐ |
| B7 | US-1 / ADR-0028 r9 | deactivated project leaves the views, restored it returns with its places | integration | ☐ |
| B8 | US-1 | deleted, unknown, malformed, foreign team → 404 `organizations.team.not_found`, four alike; JUPITER and an all-deactivated team: 200, no item, `projects: []`, identical | integration | ☐ |
| B9 | US-1 / BR-TS-1 | the three project-nested routes answer `http.not_found` for GET/POST/PUT/DELETE/PATCH, member or not, existing ids or not | integration | ☐ |
| B10 | US-2 / BR-TS-8 | `projects` options = linked active projects by name; not narrowed by the filter; Delta, Zeta absent | integration | ☐ |
| B11 | US-2 | one value, two values, absent, empty value | integration | ☐ |
| B12 | US-2 | invalid value (junk, unknown, foreign, unlinked, deactivated) selects nothing, never an error; valid + invalid = valid only | integration | ☐ |
| B13 | US-2 | tree over matching items; board checklist not narrowed; reorder ignores a filter | integration | ☐ |
| B14 | US-3 / BR-TS-6 | placed first; unplaced by createdAt, project key ordinal, number numeric (9 before 10); board columns and tasks level same | integration/unit | ☐ |
| B15 | US-3 / BR-TS-14 | equal positions from per-project reorders sort the same on every read; first reorder makes the positions dense | integration | ☐ |
| B16 | US-3 / BR-TS-7 | reorder across projects: top / bottom / after; project orders untouched; board follows | integration | ☐ |
| B17 | US-3 | stale alike (status+body) for moved item AND afterItemId in {VENUS item, deactivated project's, Removed, Zeta, unknown, malformed}; nothing changes | integration | ☐ |
| B18 | US-3 | a deactivated project's places kept by a reorder, back on restore | integration | ☐ |
| B19 | US-3 / US-7 | 403 for «Читатель» before any team not-found; 400 on a bad body; 409 on an empty team | integration | ☐ |
| B20 | US-3 / BR-TS-7 | races: reorder vs unlink / team delete / team-changing save / project deactivation; read vs team delete; no 5xx | integration | ☐ |
| B21 | US-4 | card, move, child creation, parent write all in the item's project; cross-project parent → `workitems.link.parent_not_found`; foreign-type state → `state_not_allowed`; «Читатель» 403 | integration | ☐ |
| B22 | US-5 / BR-TS-9 | create in the chosen project with its next number, owned by the team or none, unplaced on both backlogs | integration | ☐ |
| B23 | US-5 | deactivated project → `project.not_found`; unlinked → `team.not_in_project`; deleted team → `team.not_found`; nothing created; create vs unlink race | integration | ☐ |
| B24 | feat-028 §1.2 / 4.2 | `TeamListItem.isMember`; `WorkItem.project` on get, create, save, parent write, restore | integration | ☐ |
| B25 | US-7 / BR-TS-13 | non-member (and instance admin) → org 404 identical for MARS, unknown, Orion | integration | ☐ |
| B26 | US-7 / tech-011 m3 | Orion id as team; Zeta id as filter; Zeta item in reorder; on every team route; project routes keep their boundary | integration | ☐ |
| B27 | US-7 | answer order 401, org, 403, team, level, 400, 409; roles Ann/Max/Vera | integration | ☐ |
| B28 | US-7 | `OrganizationAddressedRoutes` inventory: three new rows, old not listed, count 59 | integration | ☐ |
| B29 | NFR | 5 000 items / 50 projects: read ≤ 2 s (filtered or not), reorder ≤ 1 s | integration (volume) | ☐ |
| B30 | NFR | statement count equal small vs large (projects, items, depth 5, 10 filter values), reads and reorder | integration | ☐ |
| B31 | BR-TS-14 / ADR-0028 r13 | migration applies on a clean DB, repeatedly, index present, Down drops it; no data touched | integration | ☐ |
| B32 | ADR-0030 | deleted items kept off every team/project read through `WorkItemSets`; the new reads have rows in the surface-matrix and parity tests | integration/arch | ☐ |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| BA1 | `projectId` repeated 50 times / twice the same value / upper-case GUID / all-zero GUID / `projectId=` | no 5xx, no change in rows beyond the rule | ☐ |
| BA2 | Same-position tie spanning two projects with identical createdAt | a total order (key, then number), every read the same | ☐ |
| BA3 | Team with an item whose project was renamed | `project.name` current | ☐ |
| BA4 | Reorder with `after` itself, `after` missing | 400 / 409 per contract, nothing changes | ☐ |
| BA5 | Deleted organization / deactivated organization | org 404 on every team route | ☐ |
| BA6 | Team linked to a project, project deleted state (soft) with items in Removed | no leak into any view | ☐ |
| BA7 | Concurrent reorders of one backlog | serialize, one dense order | ☐ |

### Runs, defects, verdict

(to fill)

## Frontend

### Coverage matrix (plan)

| # | Criterion | Check | Type | Status |
|---|---|---|---|---|
| F1 | US-1 / BR-TS-1 | `/organizations/:o/teams/:t/backlogs\|boards/:level` render; bad level segment and any project-nested team address → «Страница не найдена», no redirect, nothing of team or project | component/route | ☐ |
| F2 | US-1 / design §10 | states: codes loading/failed, non-member, team not found (four alike), read failed, re-read failed keeps frame, JUPITER empty (a) with no filter row and no create, (b) own empty | component | ☐ |
| F3 | US-6 / BR-TS-11 | head `h1` = team; menu «Команды» lists org teams, own first, team selected, no «Весь проект»; entry opens same entry+level; keyboard (arrows, Home, End, letter, Escape) | component | ☐ |
| F4 | US-6 | project head: `h1` = project, no drop-down button | component | ☐ |
| F5 | US-2 / BR-TS-8 | filter «Проект»: options by name; address carries repeated `projectId`; reload and link carry it; kept by level menu, tab, «Показать канбан»/«Показать бэклог»; dropped by head menu, switcher, «Бэклоги»/«Канбан» | component/route | ☐ |
| F6 | US-2 | invalid / unknown value: «Фильтр задан», filtered-empty frame with «Сбросить фильтр», «Очистить фильтры» clears the address; valid + invalid = valid | component | ☐ |
| F7 | US-2 | filter by keyboard, focus stays; row wraps at 640 px | component | ☐ |
| F8 | US-1 / BR-TS-3 | «Проект» column after «Команда» on team and project backlog (header, cell, skeleton ten columns, token 120 px); card shows «Проект: Beta» | component | ☐ |
| F9 | US-4 / BR-TS-4 | `?workitem=` from a row/card opens in `project.id`; by address resolves the key through the organization's projects; unknown key sends no request; six not-found cases alike; «Закрыть» removes the parameter; project views read in the page's project | component | ☐ |
| F10 | US-1 / design §7 | card band «Проект» read-only, under «Статус» | component | ☐ |
| F11 | US-4 / BR-TS-5 | card move PATCHes in `project.id`; only mapped columns are targets | component | ☐ |
| F12 | US-4 | row «+» and card «Добавить задачу» create in the parent's project | component | ☐ |
| F13 | US-4 | drop onto a row of another project: refused target, nothing sent, notice text; «Изменить родителя…» offers the row's own project's parents | component | ☐ |
| F14 | US-5 / BR-TS-9 | dialog: «Проект» first; preselect rules (filter names exactly one / team has one / none); «Выберите проект» + focus; «Команда» read-only holding T until a project; caption texts | component | ☐ |
| F15 | US-5 | «Команда» offers the project's teams + «Без команды»; a project change resets a team not linked to it with announcement + line | component | ☐ |
| F16 | US-5 | server refusals under the right field, dialog stays, options re-read: 404 project, 409 not linked, 404 team; 403 notice | component | ☐ |
| F17 | US-5 | creation notice: «Он не принадлежит этой команде.» / «Он не подходит под текущий фильтр.» | component | ☐ |
| F18 | US-5 / US-7 | command absent for JUPITER and «Читатель»; area line follows the project | component | ☐ |
| F19 | US-6 / BR-TS-10 | switcher team level = org teams, own first, drawn with no project, «Нет команд»; loading/failed hides the team entries | component | ☐ |
| F20 | US-6 | project change keeps a chosen team; team address sets org+team; team choice on a team page opens the same entry landing; on a project page moves nothing; other org follows mod-015 BR-18 | component/route | ☐ |
| F21 | US-6 | fallback: first entry; mod-020's default team when both merged; a fallback team is not chosen, and survives a reload as not chosen; gone-team notice as today | unit/component | ☐ |
| F22 | US-6 / BR-TS-12 | project «Команды» always shows «Бэклог проекта» and «Доска проекта», for every role, from first paint | component | ☐ |
| F23 | US-7 / design §10 | «Читатель»: no drag, grip, «+», card menu, create; checklist ticks disabled | component | ☐ |
| F24 | spec / mod-005 | deleted items do not appear (server's job; the client draws what it reads) — nothing client-side to test; noted | n/a | ☐ |
| F25 | E2E | standing NO; list the specs the change breaks | manual | ☐ |

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| FA1 | 200 teams in the head's menu / 200 projects in the filter and dialog | scroll inside the window, all reachable | ☐ |
| FA2 | Reload on a team address with a filter, then change team in the switcher | filter gone | ☐ |
| FA3 | `?workitem=` plus `projectId` in one address, both kept | neither lost on a filter change / on closing | ☐ |
| FA4 | A slow or failing teams read while on a team page | page works, menu button absent | ☐ |
| FA5 | Team deleted while its page is open and re-read | «Команда не найдена» frame | ☐ |
| FA6 | Double submit of the creation dialog, project changed between attempts | one `requestId` per submission; no duplicate | ☐ |

### Runs, defects, verdict

(to fill)
