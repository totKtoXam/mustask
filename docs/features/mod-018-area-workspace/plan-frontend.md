# mod-018-area-workspace - frontend plan (typed single tree)

Slice: `src/frontend/src/features/organizations`; the work item's «Область» in `features/workitems`. Contract: `api-contract.md`, design: `design.md`.

## Screens and components

- `pages/AreasPage.tsx` - the organization's typed tree (TreeRegistry): Название / Тип / Описание, «деактивирован» mark, row commands from `lib/areaCommands.ts`, primary «Новый департамент», loading, empty, error and permission states.
- `pages/AreaPage.tsx` - one node for the three types. Department: «Проекты», «Команды» (links to departments only). Project node: «Расположение», «Команды» (the feat-028 project-team links, `components/ProjectNodeTeams.tsx`), «Вложенные области», «Открыть проект». Sub-area: parent and project, sub-areas. A deactivated project's node is read-only.
- `pages/ProjectAreasPage.tsx` - the project's subtree under its settings, «Новая вложенная область».
- `pages/ProjectGeneralPage.tsx` - «Расположение» fact and «Переместить»; `pages/TeamPage.tsx` - «Департаменты» section (`components/TeamDepartmentsSection.tsx`).
- `components/AreaNodeDialogs.tsx` - the shared dialog host (create, rename, move, delete) used by the three pages; `AreaDialogs.tsx`, `AreaLinkDialog.tsx`, `AreaPickers.tsx` (50-option cap, D-1), `AreaTypeMark.tsx`.
- `CreateProjectDialog` sends `parentAreaId`, `CreateTeamDialog` sends `areaIds`.
- Removed: area-project links and pickers, `EndAreasSection`.
- Work item: `AreaField` and `CreateWorkItemDialog` offer only the project's subtree (`useProjectAreaTree`).

## Data

- `api/organizationsApi.ts`, `api/queryKeys.ts`, `hooks/queries.ts`, `hooks/mutations.ts`: invalidation of area, project and team keys after each write.
- Permissions: `area.read`, `area.write`, `area.create`, `area.link`.
- Errors per `lib/refusals.ts`: type_not_allowed, outside_project, link_type_not_allowed, depth_exceeded, root_is_fixed, plus the earlier codes. An item area outside its project answers 404 `area.not_found` in the code (api-contract § 6 says 409; the code is followed).

## Tests

Vitest with the MSW fake (`testApi.ts`) enforcing the typed rules, including the department "to the top" no-op: tree, node pages, project areas, team departments, dialogs, pickers, roles, work item area.
