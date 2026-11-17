# Plan (backend): import constructor — templates, source reading, mapping, recoding, converters, dry run (mod-024)

Stage 4a. Inputs: doc-017 `spec.md` (BR-IE-1..-10, -13, -18, -21, the converter catalogue, NFRs), stories US-1..-4 and US-7, ADR-0042 (accepted), `design.md` (gate-2 approved with a revisit caveat). The contract is [api-contract.md](api-contract.md). The import write, the batch and undo (feat-067), export (feat-069), external links (feat-066) and participants (feat-068) are out.

## 1. Order of work (the human wants the real parser first)

1. This plan and `api-contract.md`.
2. **The source reader**, its limits and file-safety refusals, with unit tests on generated made-up files, plus the dev probe (§ 3). Own commit.
3. Domain (template, converters, chains, recoding, field catalogue), persistence + additive migration, permission codes, application handlers, endpoints, the dry run and its report, error codes. Tests with each piece.

## 2. Decisions

- **No ADR.** The write contract, batch and undo are ADR-0042's; the permission codes are ADR-0042 § 5. What follows are module-level choices, recorded here.
- **The .xlsx library: `DocumentFormat.OpenXml` 3.5.1** (Microsoft's Open XML SDK; MIT; the library every other .NET spreadsheet package, ClosedXML and EPPlus-free forks included, is built on; actively released). It reads and writes (the same package writes the dry-run report .xlsx). **It adds one NuGet dependency to `Mustask.Infrastructure`** and one `PackageVersion` line to `Directory.Packages.props` (a shared file: the orchestrator is told). Rejected: ClosedXML (loads the whole workbook into a DOM, so the expansion cap would be after the fact; it also sits on the same SDK); ExcelDataReader (no hyperlinks, which «URL из гиперссылки» needs); EPPlus (non-commercial licence, not MIT/Apache). The SDK is used through its streaming `OpenXmlReader` on a sheet, so memory stays bounded by the sheet's own cap.
- **The .csv reader is our own (≈150 lines, no dependency):** RFC 4180 quoting, line breaks inside quotes, BOM, UTF-8 or Windows-1251 (`System.Text.Encoding.CodePages` is in the shared framework), delimiter detection among `;` `,` tab. A library would not remove the detection and the caps, which are ours.
- **Safety is checked before any row is read**, in this order, on the zip container itself (`System.IO.Compression`, no SDK yet): extension and magic bytes (.xls/.xlsm/other refused; an .xlsx is a zip, a macro workbook has `xl/vbaProject.bin` or a macro content type); the entry count; the **actual** decompressed byte count while streaming each entry, never the header's claim (a zip-bomb cap of 100 MB in total); relationships of type `externalLink` / any `TargetMode="External"` that is not a hyperlink (an external workbook reference). Only then the SDK opens the file. XML is read with `DtdProcessing.Prohibit` and no resolver (the SDK's defaults, asserted by a test with an XXE file). The row/column/sheet caps (5 000 data rows, 200 columns, 50 sheets) are enforced on the chosen sheet's dimension and while counting; a violation refuses the whole file and no cell value is returned.
- **Storage of an uploaded file:** the bytes in the database, table `import_files`, owner = uploader, expiry 24 h (BR-IE-14), same decision as the images (bytes in DB). Deleted by a clean-up on every upload (rows older than their expiry), not a background job in this task; reads of an expired file answer `imports.file.not_found`. feat-067 will reuse the row.
- **Stored dry run:** `import_dry_runs` keeps the report (rows as one `jsonb`), the template version and the file id, expiring with the file. The report is not a batch and not a work item; it is how the page filters by result, how the .xlsx is made from the same data, and how feat-067 can later tell "a dry run of the same file and template version" (BR-IE-14). The stale check compares the stored version with the template's current one.
- **Template storage:** one table `import_templates` with columns for name, normalized name (unique per organization), author, version, timestamps, and the settings as one `jsonb` document (source, mappings with chains, recoding tables, identifier setting). A snapshot for a batch (feat-067) is then a copy of the document. Table names carry the `import_` prefix in the default schema, like every other module (the repo has no module schemas).
- **Last save wins** (BR-IE-18): the update locks the template row (`FOR UPDATE`, ADR-0011) and increments `version`; no `If-Match`.
- **The pure core in Domain** (`Mustask.Domain/Imports`): the source model, the converter engine and chain runner, recoding policy, the field catalogue. No I/O, so the catalogue is unit-tested without mocks. The reader implementation, the template/file/dry-run persistence and the report writer are Infrastructure.
- **Permissions** (ADR-0042 § 5, mod-019 mechanism, ADR-0029/-0038): three codes in `PermissionCatalog`.
  - `imports.template.read` — scope Organization; admin, member, viewer bundles.
  - `imports.template.write` — scope Project; the administrator holds it through the admin bundle (every organization code); the project «Менеджер» and the department «Менеджер» bundles hold it. "Create/copy anywhere" is the new `AccessSet.HoldsAnywhere(code)` (a code held on at least one project the caller reaches, or an area grant whose bundle holds it); edit/delete additionally requires `author == caller` unless the caller is the administrator (BR-IE-18, the author who lost «Менеджер» everywhere stops holding the code, so the server alone decides `canEdit`).
  - `imports.import.run` — scope Project; admin bundle; project and department «Менеджер» bundles. A row's project is judged by `AccessSet.Holds(code, projectId)`; a refusal is the row error `imports.row.project_forbidden` («Нет прав на импорт в этот проект.»). A holder on any scope also reads a template's settings.
  - Routes that are not on one object declare `RequiresOrganizationPermissionDecidedInHandler(code, reason)` and decide through `IOrganizationAccess.SetAsync()`; template list/columns/catalogue use `RequiresOrganizationPermission(imports.template.read)`. `contracts/access/role-bundles.json` is regenerated by the test command (never by hand).
  - Row project reach also needs `workitems.workitem.write` on the project (BR-IE-12): the dry run reports `imports.row.project_forbidden` when either is missing.
- **Answers for the page** (design § 7 API needs): the list response carries page-level `canCreate`, `canRun`; each row `canEdit`, `canDelete`, `canRun`, `canCopy`. The catalogue serves per field `allowsEmptyPolicy` («Оставить пустым» is false for project, type, priority) and `supportsClear` («Очистить» is false for list fields and fields with no empty state).
- **The dry run's scope of "checked as the form checks it"** (BR-IE-12): the value rules (title, description plain text, every feat-022 field's range/date rules and target-not-before-start, field on type), the references (active project in the organization, type and state of the process, never «Удалена»; an active member who can read the project; a team linked to the project), the identifier match (readable item, recycle bin, same project/type, duplicates in the file), the importer's reach per project and `workitems.workitem.write`. **Not in this task and handed to feat-067's write (which re-checks every row anyway): description rich-text reduction, parent-hierarchy rules (the parent is only parsed and looked up), concurrency on the item.** Listed in the status.md log as deviation D-1.

## 3. The reader and the dev probe

Port: `Mustask.Application/Abstractions/Imports/ISourceFileReader` — `Inspect` (format, sheets, detected CSV encoding/delimiter) and `Read` (headers + rows by `SourceReadOptions`). Result model in `Mustask.Domain/Imports/Source` (`SourceCell`: kind text/number/date/boolean/empty, shown text, raw number, date, hyperlink, A1 address; `SourceTable`: headers with address, rows with 1-based row numbers, data row count, date system). Implementation: `Mustask.Infrastructure/Imports/Reading` (`SourceFileReader`, `ZipSafetyGate`, `XlsxSheetReader`, `CsvReader`, `CsvDialectDetector`, `NumberFormatClassifier`).

Reading rules (BR-IE-3): the last computed value of a formula is read, formulas are never evaluated; a merged cell gives its value at its top-left position only; a cell's hyperlink (inline `hyperlink` element and relationship target) is kept on the cell; a date is a numeric cell whose style number format is a date format (built-in ids and custom codes with date tokens); the workbook's 1900 or 1904 system is reported; shared and inline strings; booleans; an error cell (`#N/A`) reads as text of its code.

**Dev probe (for the human, not part of the solution file):** `tests/tools/ImportParserProbe` (a console project referencing `Mustask.Infrastructure`, not in `Mustask.sln`). It opens the file read-only, never copies or writes it anywhere in the repo, and prints the sheets, the headers (text and address), the first rows (default 10) and the refusal (code and Russian text) if any.

```
dotnet run --project src/backend/tests/tools/ImportParserProbe -- "C:\path\to\file.xlsx"
dotnet run --project src/backend/tests/tools/ImportParserProbe -- "C:\path\to\file.xlsx" --sheet "Задачи" --header-row 2 --first-data-row 3 --rows 20
dotnet run --project src/backend/tests/tools/ImportParserProbe -- "C:\path\to\file.csv" --encoding windows-1251 --delimiter ";"
```

Output is printed in the console only. Without `--sheet` the first sheet is read; the defaults are header row 1, first data row 2.

## 4. Slices (`Mustask.Application/Features/Imports/<UseCase>/`, one endpoint each)

| Use case | Endpoint | Notes |
|---|---|---|
| GetCatalogue | GET catalogue | fields, converters, limits, flags |
| ListTemplates | GET templates | filter name/author, sort, page 10; per-row flags; page `canCreate`/`canRun` |
| GetTemplate | GET templates/{id} | settings; `canEdit` |
| GetTemplateColumns | GET templates/{id}/columns | members: name + export header texts |
| CreateTemplate | POST templates | validation of every tab, limit 100 |
| UpdateTemplate | PUT templates/{id} | version +1, last write wins |
| CopyTemplate | POST templates/{id}/copy | «Копия <имя>», author = caller |
| DeleteTemplate | DELETE templates/{id} | final |
| UploadFile | POST files | multipart, refusals, stores the file |
| ReadFile | GET files/{fileId}/columns | sheet/rows/encoding re-read, 5 sample rows |
| GetFileValues | POST files/{fileId}/values | distinct values of a mapping's chain result, ≤ 1 000 |
| SampleChain | POST files/{fileId}/chain-sample | first 5 rows through each step |
| RunDryRun | POST templates/{id}/dry-runs | whole file, stored report |
| GetDryRun | GET dry-runs/{id} | summary + first 50 rows of the chosen result, `stale` |
| DownloadDryRunReport | GET dry-runs/{id}/report | .xlsx |

Slices do not reference each other; the shared logic is in Domain (`Imports/Converters`, `Imports/Recoding`, `Imports/Fields`) and the abstractions. No lanes: one agent, one worktree.

## 5. Persistence and migration

New tables (additive, one migration `AddImportTemplates`, reversible): `import_templates` (id, organization_id FK, name, name_key, author_id, version, settings jsonb, created_at, updated_at; unique `(organization_id, name_key)`; index on organization_id), `import_files` (id, organization_id FK, user_id, file_name, content bytea, size, created_at, expires_at), `import_dry_runs` (id, organization_id, template_id, template_version, file_id, user_id, created_at, expires_at, totals jsonb, notes jsonb, rows jsonb). No table references `work_items`. Deleting a template does not delete its dry runs' rows (they expire); a dry run of a deleted template answers not found.

## 6. Tests

- Domain unit: converters (each, edge cases of the catalogue examples), chains (10 max, list vs single field), recoding policy, field catalogue, limits, template name rules.
- Reader unit (Infrastructure.Tests, no Docker): generated made-up .xlsx (built by the test with the SDK and by raw zip for hostile ones) and .csv — every US-1 scenario and every refusal row of design § 3.1: sheets, header/data rows, formulas' cached values, merged cells, dates (1900/1904), hyperlinks, the three CSV delimiters, Windows-1251, BOM, quoted line breaks, .xls/.xlsm/macros/external link/zip-bomb/too many rows/columns/sheets/10 MB, damaged file, XXE.
- Application unit: handlers with fakes (permissions flags, limits, copy naming, dry-run outcomes per US-4 scenario).
- Api integration (Testcontainers): every endpoint incl. 401/404/403, the managers' scope (project «Менеджер», department «Менеджер», author lost the role), template limit, copy, last-write-wins, stale dry run, the report download.
- Architecture/contract tests: error catalog completeness (`contracts/errors/imports.json`, `docs/error-codes.md` regenerated), role bundles JSON, inventory tests (`OrganizationAccessInventoryTests`, `WorkItemReferenceInventoryTests` unaffected: no reference to `work_items`).

## 7. Error codes

Module `imports`; the registry, ru texts from design § 3.1/§ 5, in `contracts/errors/imports.json` and `docs/error-codes.md` (kk texts marked `kkNeedsReview` like the other modules). Listed in api-contract.md § 6.
