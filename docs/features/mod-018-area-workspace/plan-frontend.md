# mod-018-area-workspace - frontend plan

Slice: `src/frontend/src/features/organizations` (areas live with the organization, the area tree is no longer per project). Contract: `api-contract.md`, design: `design.md` (D-1..D-7 option A, OQ-1 A, OQ-2 B).

## Screens and components

- `pages/AreasPage.tsx` - the organization's area tree under settings, «Области»: create (top level or under a parent), expanded while there are at most 100 areas (D-3), loading, empty, error, permission states.
- `pages/AreaPage.tsx` - one area: facts, rename, description, move (D-5 offers), delete with the preview of nested areas, links; its projects and teams with link and unlink.
- `components/EndAreasSection.tsx` - the area links from the project and team ends (`ProjectGeneralPage`, `TeamPage`); `components/AreaLinkDialog.tsx`, `components/AreaPickers.tsx` (local-filter pickers, 50-option cap, D-1); `AreasField` in the create-project and create-team dialogs.
- `components/AreaDialogs.tsx` - create, move, delete dialogs.
- Retired: the per-project area tree and its routes, `AreaField` of the work item, the area column and filter of the list, the area in drafts and the card (D-6 band).

## Data

- `api/organizationsApi.ts`, `api/queryKeys.ts`, `hooks/queries.ts`, `hooks/mutations.ts`: area read/write and link calls; invalidation of the area, project and team keys after each mutation.
- Permissions: `area.read`, `area.write`, `area.create`, `area.link`; a link needs `area.link` plus the other end's code, an unlink only the end's own code.
- Errors: `area.depth_exceeded`, `area.name_taken`, `area.not_found`, `area.move_into_own_subtree`, `area.not_empty` land per `lib/refusals.ts`.

## Navigation

Organization settings: Общие, Проекты, Команды, Области, Участники, Процесс. The project section keeps Общие and Команды. `TreeRegistry` gained `labelTo` (D-7).

## Tests

Vitest with the MSW fake (`testApi.ts`): tree, area page, end sections, dialogs, refusals, navigation and routes, kit guard (allow-list only lowered), error codes.
