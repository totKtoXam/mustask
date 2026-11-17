# Frontend plan: import constructor (mod-024)

Stage 6. Inputs: [design.md](design.md) (screens S1, S2, S2a, S2b, S2c, S3), [api-contract.md](api-contract.md) (source of truth, backend done and QA-passed), the kit rule (`docs/conventions/ui-kit.md`: screens are built from `shared/ui`, not from the MUI parts design.md names). Human decisions (status.md Log): «Импорт» shown to every member (`imports.template.read`); the dry-run preview is the first 50 rows of the **selected** result (the server does it: `rows` of `GET dry-runs/{id}?result=`); the prototype's «Будет обновлён» with no identifier column is fixed (see P-6).

## 1. Slice `src/features/imports`

```
api/importsApi.ts        DTO types and requests, strictly per api-contract (catalogue, templates, files, dry runs, report)
hooks/                   TanStack Query: ['imports','catalogue'], ['imports','templates',params], ['imports','template',id],
                         ['imports','columns',id]; mutations; the file read / values / chain-sample / dry run are mutations
lib/texts.ts             every Russian string of design.md in one place (no text typed in a component)
lib/catalogText.ts       text of a code (notes, refusals) from contracts/errors/imports.json, imported at build time
lib/draft.ts             the template draft: build from a file read (BR-IE-4 proposals), edit mappings, to/from settings
lib/validate.ts          client-side validation (design § 3) + mapping of server `errors` keys to tabs/rows
components/              SourceTab, MappingTab, ChainDialog, ValuesTab, IdentifierTab, ExportColumnsDialog, ...
pages/                   TemplatesListPage (S1), TemplateEditorPage (S2/S2b), CheckFilePage (S3)
paths.ts permissions.ts index.ts
```

Registration: navigation model entry «Импорт» in «Организация» (`imports.template.read`, organization scope, landing `/organizations/:o/settings/imports`, `beneath` the sub-pages); three route groups under `OrganizationGate` + `OrganizationPage`.

## 2. Shared changes (small, each with its test)

- `shared/api/httpClient.ts`: `apiFetch` learns a `FormData` body (no JSON content type, no stringify) and a new `apiFetchBlob` returns `{ blob, fileName }` (the report download). Existing behaviour unchanged.
- `shared/api/errorCodes.ts`: `importErrorCodes`, one to one with `contracts/errors/imports.json` (drift test beside it).
- Kit: `shared/ui/actions/FilePickerButton` (an `ActionButton` + a hidden file input; the kit forbids `<input>` in a slice), exported from `shared/ui`, test, showcase entry (ButtonsDemo), guide line in ui-kit.md.

## 3. Screens and states

| Screen | States built |
|---|---|
| S1 list | loading (`TableSkeleton` via `DataTable`), error + «Повторить», empty (can create / cannot), empty after filter, sort by name/author/updatedAt, pager 10, name and author filters; row actions only from the row's `canEdit/canCopy/canDelete/canRun` flags; copy (notice with the server name), delete confirm, 404-on-delete «Шаблон уже удалён.», limit alert on 409 `limit_reached`; «Столбцы» (S2c) for rows with no edit right |
| S2 editor | create and edit; name + 4 tabs (`BoxedTabs`); loading (`FieldsSkeleton`), error, `NotFound` (404), save validation (client + server `errors`), error dots on tabs, form banner, no-file frame on tabs 2..4, copy/delete in the overflow, «Проверить файл» enabled only for a saved, unchanged template |
| S2a chain dialog | add/remove/reorder, per-converter parameters from the catalogue, live sample (`chain-sample`), validation texts, «Применить» applies to the draft only |
| S2b read-only | the same tabs read-only, info notice, «Проверить файл» (if `canRun`) and «Копировать» (if `canCopy`) |
| S3 check | no-run frame, progress with «Отменить» (AbortController), refusal notice, error banner + «Повторить», totals filter (five buttons, refetches `?result=`), table with row detail, «Показаны первые 50 строк…», all-error/empty-file notices, stale notice, notes, «Скачать отчёт»; page `AccessDenied` without `canRun` |

Rights come only from the served flags; the page keeps no copy of the rule. Out of scope and absent (no look-alike controls): «Импортировать», batches, undo, export, external links, participants.

## 4. Decisions and deviations (to be logged in status.md)

- P-1 The kit wins over design.md's MUI parts: `InlineTextField/SelectField/PickerField`, `BoxedTabs`, `InlineNotice`, `DataTable/TableCard`, `DialogShell`, `ConfirmDialog`, `useNotice`, `Tag`, `ActionButton`/`IconAction`. Texts are design.md's.
- P-2 `notes` are codes; the text comes from the shared error catalogue (`contracts/errors/imports.json`, build-time import, as `roleBundles` does), never typed in a component.
- P-3 `rows[].reasons[].text` is the server's Russian text; shown as received.
- P-4 The drop area of design § 3.1 is a `Surface` + the kit `FilePickerButton` (drag-and-drop is not offered: a hint that cannot be used is worse than none).
- P-5 Tab error marker (design: a `Badge` dot): `BoxedTabs` has no dot, so a tab with invalid fields shows their number as its `count` (named «Сопоставление (2)»), which is also its accessible name.
- P-6 Prototype inconsistency: the totals' «Будет обновлён» comes from the server only; the panel shows no invented count. A template without an identifier column gets `updated = 0` from the server and the note `imports.dry_run.no_identifier_column`; the panel renders that note and nothing else about updates.
- P-7 The values tab counts («Строк») come from `POST files/{id}/values` and exist only while a sample file of this session is held; without it the column reads «—» (design § 3.3).

## 5. Tests (Vitest + Testing Library + MSW, the repo's kit)

Unit: draft building/validation, catalogue text, api layer (paths, bodies, FormData). Component: chain dialog, mapping tab, values tab. Page: S1 (states, flags, copy, delete), S2 (create happy path with upload → map → save; server validation to the field; read-only), S3 (run, filter refetch, refusal, stale, no canRun). Affected sets only; `tsc -b`, ESLint, Prettier on touched files; full verify.sh in CI. E2E: standing answer NO.
