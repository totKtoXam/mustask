# mod-024-import-constructor: code review

## Round 1

One Opus pass over `git diff origin/main...HEAD` (merge-base 1dbcbfee, HEAD f6ed981b), security pass included (economy mode: the only review). Already accepted for gate-3 and not repeated: QA-D-5, QA-FD-8, dev deviation D-1, the frontend deviations listed in status.md. Evidence run: `dotnet test Mustask.Domain.Tests --filter Imports` 84/84 green; no other suite run.

**Verdict: changes requested.** 0 blockers, 2 majors, 4 minors.

### Findings

- **R1-1 major** — `src/backend/Mustask.Application/Features/Imports/RunDryRun/RunDryRunHandler.cs:64-79` vs `src/backend/Mustask.Domain/Imports/DryRun/DryRunEngine.cs:151-153, 618`. The handler loads `context.Items` only for the **raw cell text** of the identifier column. The engine looks items up by two other keys:
  (a) «Родитель»: `Canonical` looks the parent identifier up in `context.Items`, but the parent column's values are never loaded. So every row with a parent is `identifier_not_found` unless the same identifier also sits in the identifier column. This breaks BR-IE-5 / OQ-13 A and plan-backend § 2 ("the parent is only parsed and looked up"). D-1 dropped only the hierarchy *rules*, not the lookup.
  (b) The identifier mapping's chain result (e.g. «Извлечь по шаблону» on a URL cell): the handler loads the raw text and the engine looks up the converted value, so a matching item reads «Элемент не найден.».
  No test covers either. No test of any kind finds «Родитель» under `tests/**/Imports`, and `ImportReferenceReader.LoadItemsAsync` (the update path, the `was` values) has no DB-backed test.
  Fix: build the lookup set the way the engine reads it: `DryRunEngine.Evaluate` of the identifier mapping, plus the results of the parent mapping, normalized. Expose one engine helper so the two cannot drift. Add an integration test for an update by identifier and for a parent present in the organization but absent from the identifier column.
- **R1-2 major (security: DoS / input validation)** — `src/backend/Mustask.Application/Abstractions/Imports/ImportFileInput.cs:30-40`, `.../GetFileValues/GetFileValuesHandler.cs:26-50`, `.../SampleChain/SampleChainHandler.cs:23-34`. The probe routes (`POST files/{id}/values`, `chain-sample`) take a `FieldMapping` from the body and run it with only a null check. None of the template validator's limits apply:
  - the chain length (BR-IE-9, ≤ 10);
  - lookup `entries` (≤ 1 000);
  - step params and pattern validity.
  A manager can post thousands of `extract` steps. Each builds a `Regex` per cell, with up to 100 ms per timeout, and `values` runs on all 5 000 rows. Or they can post a lookup with ~10⁶ entries, searched linearly per row. Either pins a request thread for minutes to hours, and the row loops never observe the `CancellationToken`.
  Fix: validate the probe mapping with the same per-mapping rules as `TemplateValidator.ValidateMapping` / `ValidateStep` (400 `imports.validation_failed`, keyed `mapping.*`), and check `cancellationToken` in the row loops. Add a test: an 11-step chain, or 1 001 lookup entries, gives 400.
- **R1-3 minor** — `docs/adr/0042-import-batch-through-work-item-writes-with-undo.md:33` says "For templates, organization scope: `imports.template.read` … and `imports.template.write`". The code registers `imports.template.write` as `ResourceScope.Project` (`src/backend/Mustask.Domain/Identity/PermissionCatalog.cs:186`), and the generated `role-bundles.json` follows the code. Relatedly, `AccessSet.HoldsAnywhere` (`src/backend/Mustask.Application/Abstractions/Organizations/AccessSet.cs:86`) grants write/run "anywhere" to a department «Менеджер» even when the grant reaches no project. The ADR says "on the projects its grant reaches". The effect is limited to `canCreate`/`canRun`, since every row is still judged per project. Fix: the human amends ADR-0042 § 5's scope sentence (a note in the ADR's log), and `HoldsAnywhere` counts an area grant only when it reaches a project, or the ADR says otherwise.
- **R1-4 minor** — Upload edge cases answer 500, not a refusal, because `BusinessExceptionHandler` only handles `MustaskException`:
  - a client file name over 260 characters is stored as is (`ImportEndpoints.cs:206` → `ImportConfigurations.cs:43`, `varchar(260)`), so `DbUpdateException`;
  - a malformed multipart body throws `InvalidDataException` from `ReadFormAsync`, and `ImportEndpoints.cs:184-191` catches only `BadHttpRequestException`.
  Fix: cut or refuse the name (`imports.file.unreadable`), and catch `InvalidDataException` as `imports.file.unreadable`.
- **R1-5 minor** — Storage is unbounded per caller. `import_files` takes any number of 10 MB uploads per user within 24 h. Together with QA-D-5 (purged only on the next upload), a manager can fill the database. The template document is not bounded beyond Kestrel's 30 MB body:
  - `source.headers` count and length are unchecked;
  - the number of `recoding` tables, and duplicate `mappingId`s / mapping `id`s, are unchecked (`src/backend/Mustask.Domain/Imports/Templates/TemplateValidator.cs:113, 133`).
  Fix: a per-user cap on live files (e.g. 20; the oldest goes, or 409), headers ≤ 200 × a sane length, one recoding table per mapping, unique mapping ids. Fold into feat-067's retention sweep if preferred.
- **R1-6 minor (prose)** — `docs/features/mod-024-import-constructor/api-contract.md:173` (§ 4.3) lists `imports.row.value_truncated` as a warning. Nothing emits it: `ImportErrorCodes.cs:139` is unused. The same list omits `imports.row.required_missing`, which the engine does emit (`DryRunEngine.cs:320, 325, 385`). `status.md:6` still says `stage: 04-backend`. Fix: drop or implement `value_truncated`, add `required_missing` to § 4.3 (with a status.md note, as the contract rule asks), and update the stage line.

### Security pass (checked, no further finding)

- **Authorization.** 3 policy routes (`template.read`) and 12 handler-decided routes, all through `ImportRights.LoadAsync` (`ImportViews.cs:24-39`). The handlers load rights before parsing or looking up an id. `canEdit`/`canDelete` = admin, or (holds write anywhere and is the author) (`ImportViews.cs:46`). Handler 403s are logged as refusals (`BusinessExceptionHandler.cs:78`).
- **IDOR / cross-organization.** Templates are filtered by organization (`ImportRepositories.cs:94-95, 101`). Files and dry runs are filtered by organization, user and expiry (`ImportRepositories.cs:123-125, 139-141`). Items named by identifiers are limited to the organization's projects, and an unreadable item is `identifier_not_found`, so no `was` value leaks (`DryRunEngine.cs:239`). The parent lookup is guarded the same way (`:618`). The member access map is built per organization (`ImportReferenceReader.cs:85-110`).
- **File safety.** OLE and zip signature check. The zip gate counts entries and the **actual** expanded bytes (100 MB). It refuses `vbaProject` and macrosheets, `externalLinks`, `connections.xml` and non-hyperlink external relationships. It parses with DTDs prohibited and no resolver (`ZipSafetyGate.cs:53-238`), before the SDK opens the file with `MaxCharactersInPart`. The sheet is read through the streaming `OpenXmlReader`. Rows, columns and sheets are capped while reading. The CSV parse is capped at `firstDataRow + 5 000` records and refuses UTF-16. Content type is decided by extension plus magic bytes, never by the client's header. The file name passes through `Path.GetFileName`.
- **Formula injection.** The report writes inline strings and guards `= + - @ \t \r` (`DryRunReportWriter.cs:17-25`).
- **Regex.** 100 ms timeout per cell, 500 characters (per spec). The probe-route bypass is R1-2.
- **Migration.** Additive only: three new tables, FK `Restrict` to `organizations`, indexes. `Down` drops only them. The snapshot diff removes nothing but the product version.
- **Frontend.** No `dangerouslySetInnerHTML`, and no file value rendered as a link. Ids are `encodeURIComponent`-ed. Rights come only from the served flags.

### Architecture and tests

The slices follow `docs/conventions/backend.md`: one folder per use case, shared logic in `Abstractions/Imports` and Domain, and Infrastructure behind ports. The new package is recorded in plan-backend § 2. Tests are broad on the reader (hostile corpus), the converters, the engine and the HTTP rights matrix. The gaps are in R1-1: no parent test, and no DB-backed identifier or update path.

### Behavioural claims checked

- api-contract § 0 rights table → `ImportViews.cs:22-70`, each handler's `Ensure*` — anchored.
- api-contract § 0 "another user's file/dry run answers 404" → `ImportRepositories.cs:123-125, 139-141` — anchored.
- api-contract § 3.1 body cap 10 MB + overhead → `ImportEndpoints.cs:170-198` — anchored.
- api-contract § 3.3 ≤ 1 000 values, `exceeded` → `GetFileValuesHandler.cs:52-55` — anchored.
- api-contract § 4.1 "writes no work item" → `RunDryRunHandler.cs:89-95` (adds only the dry run) — anchored.
- api-contract § 4.3 apostrophe guard → `DryRunReportWriter.cs:17-25` — anchored.
- api-contract § 4.3 row reasons list → R1-6.
- api-contract § 2.1/BR-IE-9 "at most 10 converters" on every route that runs a chain → R1-2.
- plan-backend § 2 "the parent is only parsed and looked up" → R1-1.
- plan-backend § 2 safety before any row, DTD prohibited → `ZipSafetyGate.cs:53-91, 229-238`, `XlsxSourceReader.cs:27` — anchored.
- plan-backend § 2 clean-up on every upload → `UploadFileHandler.cs:48` — anchored (its gap is QA-D-5, accepted).
- ADR-0042 § 5 scopes of the codes → R1-3.
- status.md Log (2) "rights before any id lookup" → `GetTemplateHandler.cs:18-20` and the same order in all 12 handler-decided routes — anchored.
- status.md Log (4) "upload checks the right before reading the body" → `ImportEndpoints.cs:166` — anchored.
- status.md `stage:` line → R1-6.
- ui-kit.md `FilePickerButton` (hidden input named like the button, cleared after each pick) → `FilePickerButton.tsx:36-61` — anchored.
- Direction 2, three behaviours swept:
  - role bundles: the Manager bundles gain two codes and every role gains `imports.template.read`. Grepped `organizations.project.grant|boards.projectbacklog.reorder|imports.template|imports.import.run` in `docs/` (outside feature folders), the AGENTS.md files and `.claude/`. Hits: ADR-0029's and ADR-0038's code tables (historical; ADR-0042 § 5 declares the addition) and ADR-0042 § 5 (R1-3).
  - `httpClient` sends `FormData` and reads blobs: grepped `Content-Type|FormData|apiFetch` in `docs/conventions/frontend.md`, `src/frontend/AGENTS.md`, `ui-kit.md`. None.
  - `InputField` `onBlur`: grepped `onBlur` in the same files. None.
  Not swept: design.md screen copy against the frontend beyond the deviations already listed in status.md (QA did that reconciliation), and the prototype.
