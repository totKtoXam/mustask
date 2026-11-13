# Frontend plan: mod-021-teams-in-departments

Stage 6. Built against `api-contract.md` (fixed) and doc-016's `design.md` §§ 4, 7, 8 (copy verbatim). Slice:
`src/frontend/src/features/organizations` (plus the shell's fit in `app/navigation`, the kit's colour mark and the
theme's palette).

## 1. What changes

| Surface | Change |
|---|---|
| API layer (`api/organizationsApi.ts`, `navigationApi.ts`) | `Team` gains `departmentId`, `departmentName`, `color`; `CreateTeamBody.departmentId` (required, `areaIds` gone); `moveTeam` (`PUT /teams/{id}/department`); `getAreaTeams` returns `{ id, name, color }`; the five retired link routes and `teamIds` of `POST /areas` removed; `color` on projects, areas, offers; offers' team `departmentIds` → `departmentId` |
| Error codes (`shared/api/errorCodes.ts`) | `organizations.team.has_links`, `organizations.area.has_teams`, `organizations.team.department_required` (the drift test pins them to the catalog) |
| «Новая команда» (`CreateTeamDialog`) | required single «Департамент» (`LocalPicker`), «Выберите департамент», helper «Команда принадлежит одному департаменту.», pre-set from a department; server `departmentId` reason lands on the field |
| Team page «Обзор» (`TeamPage`, `MoveTeamDialog`) | «Департамент: …» fact (link, colour dot) replaces the «Департаменты» section; «Переместить» command (administrator's code); refusal `has_links` stays in the dialog with the server text and the team's projects with «Отключить» (feat-028's unlink dialog, then back to the move dialog with the department kept) |
| Department page (`AreaPage`) | «Команды» lists the teams standing in it (no link picker, no «Отключить от департамента»); «Создать команду» pre-sets the department; «Новый департамент» loses the teams field and says «Цвет назначается автоматически» |
| Department deletion (`DeleteAreaDialog`) | teams that stand in it refuse with «Нельзя удалить департамент, в котором есть команды» and their names; «Удалить» stays disabled |
| «Команды» list | «Департамент» column (link) and the team's colour dot |
| Diagram (`diagramModel`) | a team is one card inside the department it stands in (a place missing from the tree: outside); the «та же команда в другом департаменте» legend line goes |
| Selector fit (`selectorFit.ts`, `workContext.ts`) | a team fits a department when its `departmentId` is it; department–team links and the via-project clause are gone |
| Colour mark (BR-TD-20) | theme: `objectColours` (key → hex) and `theme.workTracking.objectColour(key)`; kit: `marks/ColorLabel` (dot 10/8 px + name, optional link) with test, showcase demo, guide line; used on the department page's team rows, the «Команды» list and the team page's department line |
| Removed | `TeamDepartmentsSection`, `AreaLinkDialog`, `linkResets`, the department–team link hooks, queries, keys, texts |

## 2. Tests

- `ColorLabel` (kit), `selectorFit`, `diagramModel`, `organizationsApi`/`testApi` (new route, retired routes gone).
- Page tests: `CreateTeamDialog` through `OrganizationTeamsPage` (required department, preset, server reason), `TeamPage`
  (department line, move success, `has_links` refusal and unlink-then-retry), `AreaPage` (teams by place, create from the
  department), the department deletion refusal.
- MSW mock (`testApi.ts`, `testFixtures.ts`) follows the contract: teams carry a department (default: the organization's
  first by name), `PUT …/department`, `has_teams`, `departmentId` in offers, `color` everywhere.
- Run: affected Vitest files, `tsc -b`, ESLint on touched files. No full `verify.sh`, no E2E (standing answer: NO).

## 3. Not built (out of scope or not placed by design for this cut)

Projects in departments and «Верхний уров"» (mod-022), department managers (mod-023), the cascading context panel, the
search and sight (feat-061..065, feat-041/052/054), «Список»/«Дерево» of «Структура» and the upgrade notice (doc-016 § 7.2,
§ 8: they belong to the structure-list lines), the colour on the path button and in panel columns (they do not exist yet).
The panel has one (light) theme: one hex per key serves it; dark variants arrive with a dark theme.

## 4. After frontend QA (F-1..F-3)

- F-1: after a `has_links` refusal the move dialog also lists the team's deactivated linked projects (from `GET …/deletion-counts`, which already names every linked project with `active`), tagged «деактивирован», with no «Отключить», and a line. **Copy for gate-3** (design § 4.2 has none, written by the developer): «Связь с деактивированным проектом нельзя отключить. Сначала восстановите проект в списке деактивированных проектов организации.» The read needs the deletion-counts code; if the caller lacks it the deactivated rows are simply absent.
- F-2: the team page title carries the team's dot (kit `ColorDot`, the dot alone). F-3: the «Департамент» column draws the department's dot; the colour comes from the area tree (`color` of the department node), the list item has none.
