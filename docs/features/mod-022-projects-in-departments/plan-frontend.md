# Frontend plan: mod-022-projects-in-departments

Stage 6. Built against `api-contract.md` (fixed) and doc-016's `design.md` §§ 5, 7 (copy as written there; the few strings it
lacks are listed in § 4 for gate-3). Slice: `src/frontend/src/features/organizations`; the pattern is mod-021's
([plan](../mod-021-teams-in-departments/plan-frontend.md)).

## 1. What changes

| Surface | Change |
|---|---|
| Error codes (`shared/api/errorCodes.ts`) | `organizations.team.other_department`, `organizations.area.project_has_teams`, `organizations.area.project_needs_department`, `organizations.project.department_required` (the drift test pins them to the catalog) |
| Refusal placing (`lib/refusals.ts`) | body key `parentAreaId` is the field `location`; `project_needs_department` lands on «Расположение»; `team.other_department` on the link pickers' field |
| «Новый проект» (`CreateProjectDialog`) | «Расположение» required, departments only (`placementOptionsFor` has no top level), helper «Проект всегда стоит в департаменте.», «Выберите департамент» on submit; pre-set: the department the entry point names, else the only department the caller may write, else empty; «Команды» offers the chosen department's teams only and drops a choice that no longer fits |
| Project move (`MoveAreaDialog`, project branch) | offers departments only, current excluded; 409 `project_has_teams` stays in the dialog with the server's text and the project's teams, each with «Отключить» (feat-028's `UnlinkDialog` stands in the dialog's place and the dialog returns with the chosen department kept) |
| Department page «Проекты» (`AreaPage`) | «Вынести на верхний уровень» and its dialog removed; «Добавить проект» (`PlaceProjectDialog`) offers projects of other departments only (a refusal for a linked project names its teams with «Отключить», shared `ProjectTeamsRefusal`, QA F-3) |
| «Проекты» list | creation needs a writable department (no top-level code); the name and «Расположение» columns carry the colour marks (BR-TD-20) |
| Project «Общие» | title carries the project's dot; «Расположение: Департамент» is a coloured link |
| Link pickers (`LinkDialog`) | the project's «Команды» picker offers the teams of the project's department; the team's «Проекты» picker the projects of the team's department (department of a project from `selector-offers`); the empty states name the department; a stale list (`other_department`) is the server's text in a banner and the list is read again (QA F-2) |
| MSW mock (`testApi.ts`) | follows the contract: department required, `other_department` on the three link routes and on creation, `project_needs_department`, `project_has_teams`, same-department move a no-op |

## 2. Tests

- `areaTree` (placement and move options without the top level), `refusals` (new codes and the `parentAreaId` key), the
  drift test of the error codes.
- Page tests: `CreateProjectByPlace` (required department, preset by entry point and by the only department, teams of the
  department only, server `department_required`), the project move dialog (departments only, refusal with the teams,
  unlink then move), `AreaPage` (no lift command), the link pickers by department (both ends, empty states, stale refusal).
- Run: affected Vitest files, `tsc -b`, ESLint and Prettier on touched files. No full `verify.sh`, no E2E (standing answer: NO).

## 3. Not built

- The selector's «＋ Создать проект» (feat-052 is not built; BR-TD-6's preset for it arrives with that line, the dialog's
  pre-set by `presetPlaceId` is what it will use) and the organization-«все» entries (the cascading selectors, feat-063).
- Scoped team links at creation and the card flag (feat-054), department managers (mod-023).
- The project row menu «Переместить» on the department's «Проекты» (design § 3 lists it; the move stays on the project's
  «Общие» and in «Добавить проект»).

## 4. Copy the developer had to write (for gate-3)

- «Сначала выберите расположение.» (the teams picker before a department is chosen).
- «Нет другого департамента, в который вы можете переместить проект.» (project move with no other department; mirrors the team's).
- «Команды:» label and «Отключить» for the move refusal (mirrors the team move's «Проекты:»).
- «Команда уже подключена ко всем проектам департамента.» and «В департаменте «X» нет проектов. Создайте проект в этом
  департаменте.» (the team's picker; design § 7.5 says it mirrors the project's).
