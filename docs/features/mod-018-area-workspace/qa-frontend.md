# QA (frontend) — mod-018-area-workspace

QA session: clean context, test plan written from spec + design before the implementation was read.
E2E: skipped by the human's standing decision (NO); nothing under `src/frontend/e2e/` touched.

## Test plan vs coverage

| # | Design reference / check | Coverage | Result |
|---|---|---|---|
| F1 | § 1 nav: «Организация» order Общие, Проекты, Команды, Области, Участники, Процесс; «Проект» loses «Области»; code `area.read` | `model.ts` read; navigation tests in src/app | pass |
| F2 | § 3 tree: columns, link to page, row menu, expand/collapse all, D-3 rule, empty (admin / non-admin), loading, error | `AreasPage.test.tsx`, `areaTree.test.ts`, `TreeRegistry.labelTo.test.tsx` | pass |
| F3 | § 4 area page: facts, children, projects, teams, states, NotFound with nav kept | `AreaPage.test.tsx` | pass |
| F4 | § 5.1-5.3 create / rename / describe / move dialogs, refusals on fields, texts and notices | `AreasPage.test.tsx`, `AreaPage.test.tsx`; texts diffed against design.md by reading `lib/areaTexts.ts` | pass |
| F5 | § 5.4 deletion preview: counting, links named (deactivated tag), children warning, 409/404/403 | `AreaPage.test.tsx`, `AreasPage.test.tsx` | pass |
| F6 | § 6 link / unlink from the area, project and team ends; four texts each; refusals | `AreaPage.test.tsx`, `EndAreasSection.test.tsx` | pass |
| F7 | § 7 «Области» section of project/team; creation forms' «Области»; changed words (rename project, deactivate, team deletion) | `EndAreasSection.test.tsx`, `ProjectGeneralPage.test.tsx`, `teamTexts.ts` read | pass |
| F8 | § 8-9 item «Область» retired: card (block B, «Команда» row 1), forms, drafts, filters, list column (7 headers), create dialog line, project-nested route gone | workitems slice (59 files); grep of source: no `AreaField`, `draft.area`, `AREAS_ROUTE`, `areasPath` left (`valueArea` is the unrelated business-value field) | pass |
| F9 | OQ-2 B: «Наследовать доступ» / `inheritsAccess` not shown anywhere | grep over `src/` finds none | pass |
| F10 | § 10 permissions: only holders see buttons/menus; Admin only until mod-019 | `OrganizationsPages.roles.test.tsx`, `organizationRoles.test.ts` | pass |
| F11 | D-1 picker: 50-option cap, «Показаны первые 50 из N. Уточните запрос.», narrowing by typing | **new** `AreaPickerCap.test.tsx` (the developer's known gap) | pass after DEF-1 |
| F12 | § 12 accessible names (treegrid «Области», table names, row menu names) | covered inside the page tests by role/name queries | pass |

## Defects

| ID | Severity | Where | Description | Status |
|---|---|---|---|---|
| DEF-1 | major | `components/AreaPickers.tsx` (`LocalMultiPicker`, `LocalPicker`) | The chosen value was rebuilt as a new array/object on every render and handed to the autocomplete. MUI reads a new identity as a changed value and resets the input text, so typed text vanished on each keystroke: the D-1 search ("typing narrows") did not work in any area picker (link dialogs, create/move area, create project/team). No existing test typed into a picker. Repro: project «Общие» → «Добавить область» → focus «Области», type any letters: the field empties and the list stays unfiltered. | **fixed** (`useMemo` on `value`); regression test `AreaPickerCap.test.tsx` |
| DEF-2 | minor | `pages/WorkItemsPage.tsx` and three workitems test files | Not Prettier-formatted (a mis-indented `<TableCell>` after the area cell was removed). | **fixed** (`prettier --write` on the four files) |
| DEF-3 | minor | `CardTeamField.tsx`, `StateField.tsx`, `ProjectField.tsx` comments | Comments still mention «Область» of the band (block B "under «Область»"). Comment-only. | open, carried to gate-3 |
| DEF-4 | minor | `pages/AreasPage.tsx` | The create dialog is wrapped in `RequiresOrganizationCode areaWrite`, while «Новая область» at the top level is offered by `area.create`. Equal today (only Admin holds both); a holder of `create` without `write` (mod-019) would get no dialog. | open, carried to mod-019 |

## Results

- `npx tsc --noEmit -p tsconfig.app.json`: clean.
- `npx eslint src`: clean.
- Prettier: changed files clean after the DEF-2 fix.
- Vitest, `src/features/workitems`: 59 files, 896 tests passed (includes `WorkItemsPage.filters.test.tsx` with the 7 headers).
- Vitest, rest of `src` (organizations, shared, app, test): see the line below.
- Vitest, organizations + shared + app + test + WorkItemsPage: 192 files, 3269 passed, 6 skipped, 1 failed (`WorkItemsPage.keyword.test.tsx`, a timing flake under load: it passed in the workitems-slice run and passes alone, 15/15). With the earlier workitems run, no test of `src/` is red. The new `AreaPickerCap.test.tsx` is in that run and green.
- Process: no background process is left running.
- E2E: not run (human's standing NO).

Open for gate-3: DEF-3, DEF-4 (minor). No blocker or major is open.
