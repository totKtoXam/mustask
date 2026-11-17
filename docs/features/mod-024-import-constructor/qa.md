# QA — mod-024 import constructor

E2E: not run. The human's standing answer is NO (the task's status.md Plan: "E2E: no (standing answer)"); the question was not asked again.

## Backend

### Test plan (written from spec + stories + ADR-0042 + api-contract BEFORE reading the implementation)

Scope: doc-017 US-1, US-2, US-3, US-4, US-7; BR-IE-1..10, -13, -18, -21; NFR file safety; ADR-0042 decision 5 (codes). Out: write/batch/undo, export, links, participants.

| # | Criterion | Check | Type |
|---|-----------|-------|------|
| P1 | BR-IE-2 formats | .xls/.xlsm/other refused `unsupported_format`; csv UTF-8 with/without BOM, Windows-1251, delimiters `;` `,` tab, detection + override; RFC 4180 quotes with line breaks | unit (reader) + API |
| P2 | BR-IE-3 workbook | sheet / header row 1-50 / first data row; formula = cached value, never evaluated; merged cell only top-left; hyperlink kept; all-empty row skipped and counted; csv values are text | unit |
| P3 | BR-IE-4 mapping | header match ignoring case/spaces, not by position; proposal by Russian name/id; missing mapped column refuses whole file naming headers; extra columns ignored; project/type/title need a source on save | unit + API |
| P4 | BR-IE-5/6 | `field_not_on_type` error vs drop+warning; empty cell keep/clear; clear refused for project/type/title/identifier | unit |
| P5 | BR-IE-7 recoding | case/space-insensitive; policies error/default/empty (empty refused for project/type/priority); type-specific state entry wins; Removed state never a target; 1000 entries limit; outdated target marked; team must be linked; assignee must read the project | unit + API |
| P6 | BR-IE-8/9 converters | 12 converters on boundary values (Excel 1900/1904, 2000-2099 range, ISO with offset, split empties, regex first group / whole match / none, merge placeholders); chain <= 10; list into single field refused; error names column, cell, value, converter | unit |
| P7 | BR-IE-10 identifier | mapped column; unmapped "Идентификатор" column auto-used; ignored column not used; no column -> note, all create; not found / unreadable -> error never creates; empty -> create/skip; case/space-insensitive; duplicates both error naming each other | unit + API |
| P8 | BR-IE-11 | update never moves type/project; deleted item error (not_found first); equal row -> unchanged | unit |
| P9 | BR-IE-13 dry run | totals sum to all; one result per row; all reasons at once; preview = first 50 rows of the SELECTED result; fields old -> new; nothing written; xlsx report; formula-injection guard; stale after version change/delete; importable flag | API integration |
| P10 | BR-IE-18 templates | name 1-100 trimmed, unique ignoring case; copy "Копия X", " 2"; version +1; 100 limit; last save wins; delete final; other org -> 404 | API |
| P11 | Permissions | admin / project manager / department manager / member / viewer / non-member x each route; flags canEdit/canDelete/canCopy/canRun/canCreate; manager edits only own; author kept after losing role, admin still can; handler-decided routes; 403-before-404 | API integration |
| P12 | BR-IE-21 limits | > 10 MB, 5001 rows, 201 columns, 51 sheets refused whole before rows are read | unit + API |
| P13 | File safety | expansion > 100 MB; macros; external references; corrupt file; catastrophic regex > 100 ms -> row error; pattern > 500 | unit |
| P14 | Privacy | file and dry run belong to the uploader: another user of the same org and a user of another org -> 404; 24 h expiry; no work items written | API |
| P15 | Migration | applies on a clean DB, additive | infra |

### Coverage matrix (after reading the code and the developer's tests)

| Plan | Covered by | Status |
|---|---|---|
| P1, P2, P12, P13 | `CsvSourceReaderTests`, `XlsxSourceReaderTests` (encodings, delimiters, quotes, formula cached value, merged, hyperlink, 5000/5001 rows, 200/201 columns, 50/51 sheets, > 10 MB, zip bomb, many tiny entries, vba, macroEnabled, externalLink, DOCTYPE), `ConverterEngineTests` (regex timeout). My `Upload_*` API tests add the HTTP edge: 10 MB + 1 and 12 MB bodies, `.xls`, `.xlsm`, 5 001 rows | covered |
| P3, P4, P5, P7, P8 | `TemplateValidatorTests`, `DryRunEngineTests` + my `DryRunEngineQaTests` (team link, type-specific state entry, state of another type, several reasons, skip without an identifier column, unreadable item, duplicates by two spellings, project change, merge placeholder) | covered, with D-1, D-2 |
| P6 | `ConverterEngineTests` + my chain checks through the real template path | D-3, D-4 |
| P9 | `ImportEndpointTests` + my `DryRun_PreviewIsTheFirst50RowsOfTheSelectedResult…`, `DryRun_WritesNoWorkItem`, manager project reach | covered |
| P10, P11 | `TemplateHandlersTests`, `ImportEndpointTests` + my name boundaries (100/101/blank, numbered copies), 403-before-404 on all 7 handler-decided routes with an unknown id, a foreign project in a template | covered |
| P14 | my `Isolation_*` (other organization: template, columns, PUT, DELETE, file, dry run, report, cross-org dry run) and `Privacy_*` (same org, other user: dry run, report, file reuse, values) | covered, D-5 |
| P15 | migration `20261113135300_AddImportTemplates` is three `CreateTable` + indexes, `Down` drops the tables; applied on a clean Testcontainers database by every integration run | covered |
| Volume NFR | `Volume_5000RowsBy50Columns…` (domain engine alone, < 10 s; real run ~0.1 s) | covered at the engine level; the file read and the DB lookups are not timed |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A1 | Admin of org B asks org A's template/file/dry-run/report by id | 404, never the data | pass |
| A2 | Manager asks the admin's dry run, report, file (same org) | 404 | pass |
| A3 | Viewer asks an unknown id on 7 handler-decided routes | 403 `imports.access_forbidden`; admin gets 404 | pass |
| A4 | Manager's template aimed at a project they do not manage | row error `project_forbidden`, `importable=false`; admin passes | pass |
| A5 | Dry run on 60 valid + 3 invalid rows | totals 60/3/63; `error` filter shows 3, `created` shows 50, bad filter 400 | pass |
| A6 | Dry run writes work items | count of the project's items unchanged | pass |
| A7 | Case-only change of title/description by identifier | `updated` | FAIL, D-2 (test skipped) |
| A8 | `extract` with a capture group, `isoDate` with a time/offset | group / date as written | FAIL, D-4, D-3 (tests skipped) |
| A9 | Assignee who cannot read the row's project | row error | FAIL, D-1 (no hook to test; see D-1) |

### Runs

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-11-13 | `dotnet test src/backend/tests/Mustask.Domain.Tests --filter Imports` | 78 passed, 4 skipped (the open defects), 0 failed |
| 2026-11-13 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter Features.Imports` (Testcontainers; clean DB, migrations applied) | 22 passed (11 developer, 11 mine), 0 failed |
| 2026-11-13 | the four skipped tests with Skip removed, to prove they reproduce | 4 failed as described in D-2, D-3, D-4 |

Not re-run: the developer's Application, Infrastructure, Contracts and Architecture sets (green in stage 4, nothing of them touched). The full `verify.sh` is left to CI (economy mode). E2E: no (standing answer).

### Tests added

- `src/backend/tests/Mustask.Domain.Tests/Imports/DryRunEngineQaTests.cs`: 14 tests (3 skipped for D-2/D-3/D-4).
- `src/backend/tests/Mustask.Api.IntegrationTests/Features/Imports/ImportQaEndpointTests.cs`: 11 tests.

### Defects

#### D-1: an assignee who cannot read the row's project is accepted

- Severity: major
- Steps: a template recodes a file value to an active member who has no access to the target project; dry-run a row for that project with that value.
- Expected: the row is an error with the form's reason (BR-IE-12, doc-012 BR-MX-25, US-2 «An assignee who cannot read the row's project is refused»). Actual: `Created`/`Updated`; `DryRunEngine.Canonical` (`src/backend/Mustask.Domain/Imports/DryRun/DryRunEngine.cs:552-568`) checks only that the member exists and is active, and `DryRunContext` (`DryRunContext.cs:22-40`) carries no per-member project access. plan-backend lists no deviation for it (D-1 there covers description, parent rules and concurrency only), so the dry run would promise a row the import then fails.
- Status: open

#### D-2: a case-only change reads as «Без изменений»

- Severity: major
- Steps: item `PLT-TASK-1` has title «Старое»; a row with its identifier and title «СТАРОЕ» (same for description «текст» -> «Текст»).
- Expected: `updated`, preview «Старое → СТАРОЕ». Actual: `unchanged`, the row would be dropped silently. Cause: `DryRunEngine.cs:411` compares with `OrdinalIgnoreCase` for every field; only identifiers, ids and keys should ignore case.
- Test: `DryRunEngineQaTests.Update_ThatOnlyChangesLetterCase_IsAnUpdate` (skipped).
- Status: open

#### D-3: «Дата из ISO 8601» refuses a date-time

- Severity: major
- Steps: chain `isoDate` on «2026-03-15T23:30:00-05:00» or «2026-03-15T23:30:00».
- Expected: 15.03.2026 (BR-IE-8 catalogue: date, or date-time with/without offset; the date as written). Actual: the row is `convert_failed`. `ConverterEngine.cs:150` reuses the date-only pattern `yyyy-M-d` (`ParseDate`, `ConverterEngine.cs:225-242`). The other-tracker source in the spec (§ Other sources) relies on exactly this converter.
- Test: `DryRunEngineQaTests.IsoDate_OnATemplateStep_ReadsADateTimeWithOffsetAsWritten` (skipped).
- Status: open

#### D-4: «Извлечь по шаблону» returns the whole match, not the first group

- Severity: major
- Steps: chain `extract` with pattern `#(\d+)` on «Задача #4521».
- Expected: «4521» (the spec's own example). Actual: «#4521». `ChainParams.ToEngine` (`src/backend/Mustask.Domain/Imports/Templates/TemplateValidator.cs:68`) passes only `Pattern`, so `Group` stays null and `ConverterEngine.Extract` (`ConverterEngine.cs:339`) takes group 0. The unit test hides it by passing `Group: 1` by hand.
- Test: `DryRunEngineQaTests.Extract_OnATemplateStep_TakesTheFirstGroup_NotTheWholeMatch` (skipped).
- Status: open

#### D-5: an expired file is deleted only when somebody uploads again

- Severity: minor
- Steps: upload a file, wait 24 h, upload nothing. Expected (NFR Privacy, BR-IE-14): the file is gone within 24 h. Actual: it is unreachable through the API (queries filter on `ExpiresAt`) but its bytes and the stored dry-run rows stay in `import_files` / `import_dry_runs` until the next upload anywhere in the instance triggers `DeleteExpiredAsync` (`UploadFileHandler.cs:48`, `ImportRepositories.cs:46`). There is no scheduled sweep.
- Carry to gate-3 as a debt, or to feat-067 (the background run) which needs a worker anyway.
- Status: open (minor, not sent back)

### Re-check 1 (fixes in 8706eda7)

First-pass verdict was fail: 4 majors (D-1..D-4), 1 minor (D-5).

| Defect | Verified against the requirement | Result |
|---|---|---|
| D-1 | the Assignee case now also asks `context.CanMemberRead(member.Id, projectId)`; `ImportReferenceReader` builds it from the mod-019 `AccessSet` (role + area/project/team grants) and tests `workitems.workitem.read` on the project; refusal is `assignee_not_found`. BR-IE-12 / BR-MX-25 met | fixed |
| D-2 | title, description, reproSteps, systemInfo compare ordinally; ids and enum names stay case-insensitive. Both skipped tests un-skipped and green | fixed |
| D-3 | `ParseIsoDate`: a date, or a date-time with/without offset (`T` or space, seconds and fraction optional, `Z` or `±hh[:mm]`); the date as written. Test green for the date, offset and offset-free forms | fixed |
| D-4 | `Extract` takes group 1 when the pattern has a group, else the whole match; the test through the template path is green | fixed |

Integration gap of D-1: every organization role (admin, member, viewer) holds `workitems.workitem.read` at organization level (`OrganizationRoleBundles.ReadCodes`), so no real member can be made unable to read a project and the refusal cannot be reproduced over HTTP. The Domain test covers the refusal; the new integration test `DryRun_Assignee_WhoReadsTheProject_IsAccepted_ThroughTheRealAccessModel` covers that the real reader wires the positive path. Judged acceptable; a negative integration test becomes possible once a role can narrow reads.

| Date | What was run | Result |
|---|---|---|
| 2026-11-13 | Domain `Imports` | 84 passed, 0 skipped, 0 failed |
| 2026-11-13 | Application `Imports` | 19 passed |
| 2026-11-13 | Api integration `Features.Imports` | 23 passed |

Nothing nearby broke. D-5 stays open as a minor for gate-3.

### Verdict

blockers: 0 · majors open: 0 (D-1..D-4 fixed) · minor: 1 (D-5, carried to gate-3) · result: pass.

## Frontend

E2E: skipped by the human's standing answer (NO until revoked); the question was not asked again and nothing under `src/frontend/e2e/` was written or run. No dev server was started.

### Test plan (written from spec + US-1..-4, -7, design.md, status.md decisions and api-contract BEFORE reading the implementation)

| # | Criterion / screen | Check | Type |
|---|---|---|---|
| F1 | S1 list: rights from served flags (`canCreate`, `canRun`; per row `canEdit`, `canCopy`, `canDelete`, `canRun`); a member sees rows + «Столбцы» only; «Импорт» visible to every member | no control the flag forbids | page |
| F2 | S1 states: loading, error + retry, empty (can / cannot create), empty after filter, copy notice, limit alert, delete confirm, delete of a gone template | each state's text | page |
| F3 | S2 editor: four tabs, save (whole template), server `errors` onto fields/tabs, name taken, no-file frame, read-only (S2b), not found, access denied | | page |
| F4 | «Проверить файл» enabled only for a saved, unchanged template; absent without `canRun` | link / disabled / absent | page |
| F5 | Source tab: pick a file (multipart), preview with cell addresses, sheet / header row / first data row / csv options re-read the columns, remove file, refusal texts for every file reason, template untouched on refusal, no picker when read-only | | component |
| F6 | Mapping tab: one row per column, proposals (BR-IE-4), one source per field, «Очистить» only if `supportsClear`, «Тип без поля» only for typed fields, required-field lines, chain dialog (add / remove / reorder / params / 10 limit / apply only to the draft) | | component |
| F7 | Values tab (US-2): one section per recoded mapping, counts from the file or «—», suggestion applies only on acceptance, «Принять все», no removed state as a target, «Оставить пустым» only where allowed, default policy needs a value, outdated mark, type-specific entry, >1 000 values warning, read-only | | component |
| F8 | Identifier tab (BR-IE-10): column or «Нет», empty-identifier policy disabled without a column | | component |
| F9 | S3: no-run frame, progress + cancel, refusals incl. `columns_missing` / `sheet_missing`, error banner + retry, totals filter refetches `?result=`, 50-row caption, notes by catalogue text, stale, all-error / empty-file notices, row detail old -> new, report download, only runnable templates offered, `AccessDenied` without `canRun` | | page |
| F10 | Error codes render the Russian text from `contracts/errors/imports.json` | | unit |
| F11 | Nothing clickable for feat-066/067/068/069 | no «Импортировать», «Экспорт», «Внешние ссылки», «Участники», «скоро» on the page | page |

### Coverage matrix (after reading `plan-frontend.md` and the diff)

| Plan | Covered by | Status |
|---|---|---|
| F1, F2 | `TemplatesListPage.test.tsx` (8) | covered; sort / pager / filter requests not covered (low risk) |
| F3 | `TemplateEditorPage.test.tsx` (7), `draft.test.ts`, `serverErrors.test.ts` | covered |
| F4, F11, editor rights | added `TemplateEditorPage.qa.test.tsx` | covered now |
| F5 | added `SourceTab.test.tsx` (18, incl. 9 refusal codes) | covered now; found QA-FD-1 |
| F6 | `TemplateEditorPage.mapping.test.tsx` (2) + added chain dialog tests | partly: reorder, the 10-step limit, per-converter params and the sample table remain untested (minor debt) |
| F7 | added `ValuesTab.test.tsx` (14) | covered now |
| F8 | none | not covered (70 lines; the identifier logic is in `draft.test.ts`); minor debt |
| F9 | `CheckFilePage.test.tsx` (7) + added `CheckFilePage.qa.test.tsx` (11 + 1 skipped) | covered; found QA-FD-4 |
| F10 | `errorCodes.imports.test.ts` (drift); refusals asserted with `problemText` | covered |

### Reconciliation with design.md: the developer's deviations

| Deviation | Verdict |
|---|---|
| Kit components instead of raw MUI (P-1) | acceptable: `ui-kit.md` and `kitGuard` require it; texts are design.md's |
| Tab error count instead of a dot (P-5) | acceptable: more information, and it is the tab's accessible name; tested |
| Values filter is a select, not a toggle group | acceptable: same four options, labelled |
| Fields already taken left out of the field select (design: marked «Уже сопоставлено») | acceptable in effect (one source per field holds) but the reason is invisible to the user: minor, QA-FD-8 |
| No drag-and-drop area | acceptable: the hint is truthful, nothing unusable is drawn |
| No «Открыть» on the copy notice | acceptable: a convenience; the copy is in the list |
| Skeleton instead of a progress bar | acceptable (kit rule); «Проверяем файл…» with `role="status"` and «Отменить» are present |
| Extra a11y labels | acceptable |
| Merge / constant are sources, not chain steps | matches api-contract (`isSource`) |
| «Проверить файл» absent on an unsaved template; a notice instead of a tooltip when dirty | acceptable |
| Extra «Все» button in the totals group | acceptable: needed to leave a filter |
| «Оставить как есть» policy of «По таблице» | conflicts with the contract: QA-FD-3 |
| Stale report not greyed (design § 5) | minor deviation, carried (QA-FD-8) |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| A1 | Edit the header row / first data row, leave the field | columns re-read (design § 3.1) | FAIL, QA-FD-1 (test skipped) |
| A2 | Upload a sample, save, keep working | sample still held | FAIL, QA-FD-2 (test skipped) |
| A3 | «По таблице» with the third policy, save | API accepts | FAIL, QA-FD-3 (test skipped) |
| A4 | Stale report with fine rows | only the stale warning | FAIL, QA-FD-4 (test skipped) |
| A5 | Dry run refused with `columns_missing` / `sheet_missing` | Russian reason with the names | pass |
| A6 | Row with no flags | no copy, delete, open; only «Столбцы» | pass |
| A7 | Out-of-scope controls on the editor page | none | pass |

### Defects

#### QA-FD-1: leaving the header-row or first-data-row field never re-reads the columns

- Severity: major
- Steps: upload a sample, change «Строка заголовков», press Tab.
- Expected: the columns are read again; a row without headers says «В этой строке нет заголовков. Выберите другую строку.» (design § 3.1). Actual: no request is sent. `SourceTab.tsx` (the two `InlineNumberField`s) attaches the re-read to `inputProps.onBlur`; the kit's `InputField` (`shared/ui/fields/InputField.tsx:79-90`) hands `inputProps` to MUI through `slotProps.input` and that blur handler never fires. The draft keeps a new `headerRow` with the headers of the old one and the save sends the mismatch. Sheet, encoding and delimiter work (re-read on change).
- Test: `SourceTab.test.tsx` «says a header row without headers under the field (QA-FD-1)», skipped; fails as described when un-skipped.
- Status: open

#### QA-FD-3: the third «По таблице» policy is refused by the API

- Severity: major
- Steps: mapping tab -> chain dialog -> «По таблице» -> «Если значения нет в таблице» = «Оставить как есть» -> add an entry -> Apply -> Save.
- Expected: a saved template. Actual: the panel sends `policy: "keep"` (`ChainDialog.tsx` LookupForm, `texts.ts:178`); the API takes only `error | default | empty` (`TemplateValidator.cs:305-307`, api-contract § 2.1) and answers `converter_param_invalid`. Design § 3.2: the policy is "as § 3.3", i.e. «Оставить пустым» = `empty`.
- Test: `TemplateEditorPage.qa.test.tsx` (QA-FD-3), skipped.
- Status: open

#### QA-FD-2: the sample file is lost right after «Сохранить»

- Severity: minor (a re-upload fixes it, but the notice it triggers is false)
- Steps: upload a sample, change anything, save. Expected: the sample stays (design § 7.2, 24 h). Actual: `useReplaceTemplate` writes the new version into the query and `TemplateEditorPage.tsx` keys the editor by `id:version`, so it remounts and the `sample` state is dropped; a create navigates to the saved route with the same effect. The source tab then says «Файл-образец больше не хранится…» though the file is stored, and the values counts read «—».
- Test: `TemplateEditorPage.qa.test.tsx` (QA-FD-2), skipped.
- Status: open

#### QA-FD-4: a stale report also says «Нет строк, которые можно импортировать»

- Severity: minor
- Steps: dry run, then the template is saved; rows are fine, `importable=false` only because of `stale`. Actual: both the stale warning and the all-errors warning show (`CheckFilePage.tsx`, `!report.importable && !empty`); the second is untrue.
- Test: `CheckFilePage.qa.test.tsx` (QA-FD-4), skipped.
- Status: open

#### Other minors (read from the code, no test)

- QA-FD-5: `ChainDialog.tsx:53` `stepError` lets a «По таблице» with no entries, or the default policy with an empty default, pass; the API refuses both (`TemplateValidator.cs:307,315`), so the refusal returns as a generic row error, not the dialog's text.
- QA-FD-6: `hooks/useReferences.ts:42-47` lists the states of every type in one select, so «Новая» appears five times with no hint of the type; a type-less entry then stores the first type's state id.
- QA-FD-7: ESLint `react-refresh/only-export-components` warnings at `ChainDialog.tsx:53` and `SourceTab.tsx:37` (0 errors).
- QA-FD-8: stale report not greyed (design § 5); a field taken by another row is not marked «Уже сопоставлено».

### Tests added

- `src/frontend/src/features/imports/components/SourceTab.test.tsx`: 18 (1 skipped: QA-FD-1).
- `src/frontend/src/features/imports/components/ValuesTab.test.tsx`: 14.
- `src/frontend/src/features/imports/pages/TemplateEditorPage.qa.test.tsx`: 12 (2 skipped: QA-FD-2, -3).
- re-check 1: `TemplateEditorPage.qa.test.tsx` +1 (skipped: QA-FD-9); `shared/ui/fields/InputField.test.tsx` +1 (inputProps.onBlur once). The four QA-FD-1..4 tests un-skipped by the developer pass.
- `src/frontend/src/features/imports/pages/CheckFilePage.qa.test.tsx`: 12 (1 skipped: QA-FD-4).
- `src/frontend/src/features/imports/testFixtures.ts`: `fileRead`, `uploaded`, `serveFile`.

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-13 | `vitest run src/features/imports` before adding tests | 35 passed |
| 2026-11-13 | the four skipped tests with `skip` removed | 4 failed as described |
| 2026-11-13 | `vitest run src/features/imports src/shared/api src/shared/ui/actions src/test/kitGuard src/test/colourLiterals src/test/noRawHtml src/app/navigation src/app/AppLayout.access` | 39 files, 929 passed, 5 skipped (4 are the open defects), 0 failed |
| 2026-11-13 | `tsc -b`; `eslint src/features/imports`; prettier on the new files | clean; 0 errors, 2 warnings (QA-FD-7) |

Not run: the whole frontend suite and `verify.sh` (CI, economy mode); E2E (standing answer: no).

### Verdict

blockers: 0 · majors open: 2 (QA-FD-1, QA-FD-3, back to the frontend developer) · minors: 6 (QA-FD-2, -4, -5, -6, -7, -8; carried to gate-3 unless taken with the majors) · result: not passed yet; the re-check covers QA-FD-1, -3 and un-skips the four tests as each is fixed.

### Re-check 1 (fixes in 2be3e682)

Checked against the defect text and `api-contract.md`, not against the developer's claims.

| Defect | Fixed? | Verdict | Evidence |
|---|---|---|---|
| QA-FD-1 header-row / first-data-row blur | yes | pass | The kit `InputField` now has `onBlur` on the `InputBase`, chained to `inputProps.onBlur`; `InlineNumberField` exposes it; `SourceTab` uses it. The un-skipped `SourceTab` test passes. Other kit users: `ChecklistAddField` passes `inputProps.onBlur`, `KeywordFilter` blurs by bubbling, `AddChildRow` / `CardChecklists` use their own handlers; none passes the new prop. Added `InputField.test.tsx` «calls the host's inputProps.onBlur once per blur…»: passes, so `inputProps.onBlur` is not called twice and `onBlur` fires once. Their test sets are green. |
| QA-FD-3 third «По таблице» policy | yes | pass | The option now sends `empty`, text «Оставить пустым»; `api-contract.md` § 2.1 / recoding enum is `error\|default\|empty`. The un-skipped test passes. |
| QA-FD-2 sample lost after save | yes | pass for the case reported | The un-skipped test passes (save of an existing template, create moving to the saved id). But see QA-FD-9. |
| QA-FD-4 stale report says «nothing to import» | yes | pass | `!report.stale` added; un-skipped test passes. |
| QA-FD-5 `stepError` lookup | yes | pass | An empty table and the default policy with an empty default now give «fillValue», as `TemplateValidator.cs` refuses them. |
| QA-FD-6 states of every type in one select | yes | pass | A type-less entry labels the state «Тип · Состояние»; the `ValuesTab` test adjusted. |
| QA-FD-7 react-refresh warnings | yes | pass | `stepError` and `sampleOf` no longer exported; ESLint on `features/imports` and `shared/ui/fields`: clean. |
| QA-FD-8 stale report not greyed; taken field not marked | no (left open by design) | minor, carried to gate-3 | Unchanged. |

New defect found while verifying QA-FD-2:

#### QA-FD-9 (minor): the held sample leaks between templates

- `lib/heldSample.ts` is module state; the `new` key is cleared only by `promote` after a first save, and no key is cleared on leaving the editor, on deleting a template or on switching the organization.
- Steps: open «Новый шаблон» in any organization, upload a sample, leave without saving, open «Новый шаблон» again (or the same in another organization). The old sample (`tasks.xlsx · 20 КБ`) is shown in the new, unrelated draft. It is the user's own file in memory only, so no cross-user exposure, but the new draft is told it has a sample whose columns it never chose; the same key `new` is shared by all organizations.
- Fix: clear `new` (and the template's key) when the editor unmounts for a reason other than a save/refresh remount, or key the map by organization and clear on leave / on delete.
- Test: `TemplateEditorPage.qa.test.tsx` «does not offer the sample of an abandoned new template…» (QA-FD-9), skipped; fails as described when un-skipped.

### Runs (re-check 1)

| What was run | Result |
|---|---|
| `vitest run src/features/imports src/shared/ui/fields src/test/kitGuard.test.ts` + the `ChecklistAddField`, `KeywordFilter` tests | 23 files, 957 passed, 7 skipped (6 `skipIf` cases of the kit field frame, QA-FD-9), 0 failed |
| `vitest run src/shared/ui/fields/InputField.test.tsx` after the new test | 11 passed |
| QA-FD-9 test with `skip` removed | fails as described |
| `tsc -b`; `eslint` and `prettier --check` on `features/imports`, `shared/ui/fields` | clean |

Not run: `verify.sh` (CI); E2E (standing answer: no).

### Verdict (re-check 1)

blockers: 0 · majors open: 0 (QA-FD-1, -3 fixed) · minors open: 2 (QA-FD-8, QA-FD-9), carried to gate-3 unless taken · result: passed with minors.

Wider run of the other screens that use the kit fields (`features/boards`, `features/workitems`, `features/uikit`): under parallel load 6-7 `WorkItemFormPage*` / `CreateWorkItemDialog` tests failed with a different set each run (`Unable to find role="textbox" and name "Описание"`, the rich-text description, not `InputField`); `WorkItemFormPage.test.tsx` and `.readOnly.test.tsx` re-run alone: 38 passed. Treated as load-dependent timing, not caused by the kit change (boards and uikit fully green); CI's full run decides.
