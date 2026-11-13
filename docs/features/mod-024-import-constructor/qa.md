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

### Verdict

blockers: 0 · majors: 4 (D-1..D-4) to the backend developer · minor: 1 (D-5) · result: fail until the majors are fixed; re-check covers the four fixes and the skipped tests only.

No other defects found in the other areas of the plan: rights and the managers' scope, the 403-before-404 choice, the file-safety refusals, limits before rows, organization and user isolation, no writes to work items.
