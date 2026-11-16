# API contract: import constructor (mod-024)

Fixed at stage 4a, 2026-11-13. A change after this point needs a note in `status.md` and a Log line; after the frontend stage starts it is escalated to the orchestrator. Requirements: doc-017 (BR-IE-*), design: [design.md](design.md), plan: [plan-backend.md](plan-backend.md).

Conventions: base `/api/organizations/{organizationId}/imports`; JSON `camelCase`; enums as strings; ids are GUIDs; times are UTC ISO 8601; errors are ProblemDetails with `errorCode`, `detail` (Russian text from the catalog) and, for a validation error, `errors` (field -> list of `{code, params}`) per ADR-0004/-0006. Every route is under the organization scoped check: 401, then 404 «Организация не найдена» for a non-member, then 403 `imports.access_forbidden` for a member without the code. All requests are Bearer-authenticated.

## 0. Rights and the served flags

| Code | Scope | Held by |
|---|---|---|
| `imports.template.read` | organization | administrator, member, viewer |
| `imports.template.write` | project | administrator; project «Менеджер»; department «Менеджер» (on reached projects) |
| `imports.import.run` | project | administrator; project «Менеджер»; department «Менеджер» (on reached projects) |

"Holds anywhere" = administrator, or a code held on at least one project/department the caller reaches. Served flags (the client keeps no copy of the rule):

- page: `canCreate` = holds `template.write` anywhere (the 100-template limit is not folded in: it is its own error); `canRun` = holds `import.run` anywhere;
- per template: `canEdit`, `canDelete` = administrator, or (holds `template.write` anywhere and is the author) — BR-IE-18; `canCopy` = holds `template.write` anywhere; `canRun` = holds `import.run` anywhere.

| Route | Needs |
|---|---|
| GET catalogue; GET templates; GET templates/{id}/columns | `imports.template.read` |
| GET templates/{id} | `imports.template.read`, and `template.write` or `import.run` anywhere, else 403 |
| POST templates; POST templates/{id}/copy | `template.write` anywhere |
| PUT / DELETE templates/{id} | `canEdit` / `canDelete` rule, else 403 |
| POST files; GET files/{id}/columns; POST files/{id}/values; POST files/{id}/chain-sample | `template.write` or `import.run` anywhere |
| POST templates/{id}/dry-runs; GET dry-runs/{id}; GET dry-runs/{id}/report | `import.run` anywhere (the row's project decides each row) |

A file or a dry run belongs to the user who made it; another user's id answers 404.

## 1. Catalogue

`GET /imports/catalogue` -> 200

```json
{
  "limits": { "maxFileBytes": 10485760, "maxDataRows": 5000, "maxColumns": 200, "maxSheets": 50, "maxHeaderRow": 50,
              "maxTemplates": 100, "maxChainLength": 10, "maxRecodingEntries": 1000, "maxPatternLength": 500, "previewRows": 50, "sampleRows": 5 },
  "fields": [ { "id": "title", "name": "Заголовок", "group": "main|typed", "required": true, "multiValued": false,
                "recoding": "none|project|type|state|priority|assignee|team",
                "allowsEmptyPolicy": false, "supportsClear": true, "typeKinds": ["epic","feature","story","task","bug"] | null,
                "valueKind": "text|integer|decimal|date|reference|identifier" } ],
  "converters": [ { "id": "trim", "name": "Обрезать пробелы", "class": "text", "isSource": false,
                    "params": [ { "name": "collapse", "kind": "bool", "required": false } ] } ],
  "dateFormats": [ { "id": "dmy_dot", "name": "ДД.ММ.ГГГГ" } ]
}
```

`fields[].id` (stable): `project, type, title, description, state, assignee, priority, owningTeam, parent, identifier, risk, storyPoints, effort, businessValue, timeCriticality, startDate, targetDate, severity, activity, originalEstimate, remainingWork, completedWork, valueArea, reproSteps, systemInfo`. Required (need a source to save): `project, type, title`. `allowsEmptyPolicy` is false for `project`, `type`, `priority`; `supportsClear` is false for list fields (none yet) and for `project, type, title, identifier`. `converters[].id`: `trim, truncate, number, excelDate, dateFormat, isoDate, split, hyperlink, lookup, extract, merge, constant`; `isSource` is true for `merge` and `constant` (they are a mapping's source, not a chain step; the chain of such a mapping is empty).

## 2. Templates

### 2.1 Model

```json
TemplateSettings {
  "source": { "format": "xlsx|csv", "sheet": "Задачи", "headerRow": 2, "firstDataRow": 3,
              "encoding": "utf-8|windows-1251|null", "delimiter": ";|,|\\t|null",
              "headers": ["Номер", "Проект", "Заголовок"] },
  "mappings": [ {
      "id": "guid (client-made, stable inside the template)",
      "field": "title",
      "source": { "kind": "column|constant|merge", "column": "Заголовок", "value": "Платформа", "pattern": "{Система} / {Модуль}" },
      "chain": [ { "converter": "trim", "params": { "collapse": true } } ],
      "emptyCell": "keep|clear",
      "fieldAbsent": "error|drop" } ],
  "recoding": [ { "mappingId": "guid", "policy": "error|default|empty", "defaultValue": "target id or null",
                  "entries": [ { "fileValue": "Баг", "typeId": "guid|null", "target": "guid or priority number as text", "onExport": false } ] } ],
  "identifier": { "emptyIdentifier": "create|skip" },
  "ignoredColumns": [ "Идентификатор" ]
}
```

`ignoredColumns` (added at stage 4, see status.md Log): header texts the template leaves on «Не переносить» on purpose, so BR-IE-10 does not take such a column for the export's «Идентификатор»; default `[]`.

`source.headers` are the header texts of the sample file (header-based matching, BR-IE-4); a `constant`'s `value` for a reference field is the target id (project, type, state, user, team id; priority 1..4), for others the typed text. `lookup` params: `{entries:[{from,to}], policy, defaultValue}`; `dateFormat`: `{format: "dmy_dot|ymd_dash|dmy_slash|mdy_slash"}`; `excelDate`: `{system:"1900|1904"}`; `split`: `{delimiter}`; `extract`: `{pattern}`; `truncate`: `{length}`; `trim`: `{collapse}`. The identifier column is the mapping with `field = "identifier"`.

`TemplateDto`: `{ id, name, version, authorId, authorName|null, createdAt, updatedAt, canEdit, canDelete, canCopy, canRun, settings }`.

### 2.2 `GET /imports/templates?name=&author=all|mine&sort=name|-name|author|-author|updatedAt|-updatedAt&page=1&pageSize=10` -> 200

```json
{ "items": [ { "id", "name", "version", "source": { "format", "sheet", "encoding", "delimiter" },
               "authorId", "authorName": null, "updatedAt", "canEdit", "canDelete", "canCopy", "canRun" } ],
  "total": 14, "page": 1, "pageSize": 10, "canCreate": true, "canRun": true }
```

`name` is a substring filter ignoring case; default sort `name`. 400 `imports.validation_failed` for a bad `sort`/`page`.

### 2.3 `GET /imports/templates/{templateId}` -> 200 `TemplateDto`; 404 `imports.template.not_found`; 403 `imports.access_forbidden`.

### 2.4 `GET /imports/templates/{templateId}/columns` -> 200 `{ "id", "name", "columns": ["Номер", "Заголовок"] }` — the export header texts in mapping order (column-sourced mappings only, no duplicates).

### 2.5 `POST /imports/templates` body `{ "name", "settings" }` -> 201 `TemplateDto`, `Location`.

Validation (400 `imports.validation_failed`, `errors` keyed `name`, `settings.source`, `settings.mappings[<i>].<part>`, `settings.recoding[<i>]`, every reason at once): `imports.template.name_required`, `name_too_long` (100), `name_taken` (409 `imports.template.name_taken`, checked ignoring case after the body is valid); source: `imports.source.header_row_invalid` (1..50), `first_data_row_invalid`, `imports.template.too_many_headers` (more than 200 `headers`); mappings: `imports.mapping.source_required` (project/type/title), `field_unknown`, `field_taken` (one source per field), `column_unknown` (not among `headers`; merge placeholder `{x}` not among `headers`), `id_taken` (two mappings with the same `id`), `list_into_single` (a split in the chain of a single-valued field), `chain_too_long` (>10), `converter_unknown`, `converter_param_invalid` (empty / out of range), `pattern_too_long` (500), `pattern_invalid`, `clear_not_allowed`; recoding: `imports.recoding.default_required`, `empty_not_allowed`, `too_many_entries` (1 000), `too_many_tables` (more tables than mappings, or two for one mapping; keyed `settings.recoding`), `target_unknown` (an id that is not an active project / type / state of the type / member / team / 1..4). Template limit: 409 `imports.template.limit_reached` (100 per organization).

### 2.6 `PUT /imports/templates/{templateId}` body as 2.5 -> 200 `TemplateDto` with `version` + 1. 403 when `canEdit` is false; 404; 409 `imports.template.name_taken`. Last save wins; no `If-Match`.

### 2.7 `POST /imports/templates/{templateId}/copy` (no body) -> 201 `TemplateDto` named «Копия <имя>» (« 2», « 3»… while taken; cut to 100), author = caller. 403 without `canCopy`; 409 `imports.template.limit_reached`.

### 2.8 `DELETE /imports/templates/{templateId}` -> 204; 403 without `canDelete`; 404 (design: «Шаблон уже удалён» is this 404).

## 3. Uploaded files

### 3.1 `POST /imports/files` multipart/form-data -> 201

Part `file` (required); optional form fields `sheet`, `headerRow` (default 1), `firstDataRow` (default `headerRow` + 1), `encoding`, `delimiter` (omitted: detected). Request body cap 10 MB + overhead; larger -> 400 `imports.file.refused` / `imports.file.too_large`.

```json
{ "fileId": "guid", "fileName": "Реестр.xlsx", "size": 20480, "expiresAt": "…", "read": FileRead }
FileRead {
  "format": "xlsx|csv", "sheets": ["Задачи"], "sheet": "Задачи", "headerRow": 2, "firstDataRow": 3,
  "encoding": "utf-8|windows-1251|null", "delimiter": ";", "encodingDetected": true, "delimiterDetected": true,
  "dateSystem": "1900|1904|null", "dataRowCount": 120,
  "columns": [ { "index": 0, "header": "Номер", "address": "A2", "proposedField": "identifier|null" } ],
  "sampleRows": [ { "row": 3, "cells": [ { "text": "PLT-1", "kind": "text|number|date|boolean|empty", "address": "A3" } ] } ]
}
```

`proposedField` is BR-IE-4's proposal (header equals a field's Russian name or id, ignoring case and spaces; else null = «Не переносить»). Refusals (whole file, nothing read): 400 `imports.file.refused` with `errors.file[0].code` one of § 6; the file is not stored. A malformed multipart body is `imports.file.refused` / `imports.file.unreadable`; a client file name over 260 characters is cut to 260, keeping its extension (the `fileName` answered is the cut one).

### 3.2 `GET /imports/files/{fileId}/columns?sheet=&headerRow=&firstDataRow=&encoding=&delimiter=` -> 200 `FileRead` (re-read with the options). 404 `imports.file.not_found` (unknown, another user's or expired). 400 `imports.source.header_empty` («В этой строке нет заголовков. Выберите другую строку.»), `header_row_invalid`, `first_data_row_invalid`; file refusals as 3.1 (a limit that depends on the sheet, e.g. 5 000 rows, applies to the chosen sheet).

### 3.3 `POST /imports/files/{fileId}/values` body `{ "source": {sheet,headerRow,firstDataRow,encoding,delimiter}, "mapping": Mapping }` -> 200

```json
{ "values": [ { "value": "Баг", "rows": 12 } ], "distinctCount": 3, "exceeded": false }
```

The distinct results of the mapping's source and chain (before recoding), counted by rows, ignoring case and surrounding spaces for identity, shown as the first spelling; at most 1 000 values — more gives `"exceeded": true` and `values` empty. A row whose chain fails is not counted. The `mapping` is validated by the template's own per-mapping rules (§ 2.5: chain of at most 10, lookup of at most 1 000 entries, step parameters, pattern) before the file is read: 400 `imports.validation_failed`, `errors` keyed `mapping.source` / `mapping.chain`.

### 3.4 `POST /imports/files/{fileId}/chain-sample` body as 3.3 -> 200

```json
{ "rows": [ { "row": 3, "original": "15.03.2026", "steps": [ { "converter": "dateFormat", "value": "2026-03-15", "error": null } ], "result": "2026-03-15", "error": { "code": "imports.row.convert_failed", "params": {…} } } ] }
```

The first 5 data rows through each step. The same `mapping` validation as 3.3 applies.

## 4. Dry run

### 4.1 `POST /imports/templates/{templateId}/dry-runs` body `{ "fileId": "guid" }` -> 201 `DryRunDto`

Reads the stored file by the **template's** source settings (sheet, rows, encoding, delimiter), refuses the whole file before any row if a mapped column or the sheet is missing, then runs every row. Writes no work item, link, tag or batch; the report is stored with the file (24 h). Ends within 60 s for 5 000 rows × 50 columns (NFR); the request timeout of the client applies.

```json
DryRunDto {
  "id": "guid", "templateId": "guid", "templateVersion": 3, "stale": false, "fileName": "Реестр.xlsx",
  "createdAt": "…", "expiresAt": "…",
  "totals": { "created": 120, "updated": 30, "unchanged": 40, "skipped": 2, "error": 8, "all": 200 },
  "notes": [ "imports.dry_run.no_identifier_column" ],
  "importable": true,
  "result": "all|created|updated|unchanged|skipped|error",
  "rows": [ DryRunRow ], "rowsShown": 50
}
DryRunRow {
  "row": 12, "result": "created|updated|unchanged|skipped|error",
  "item": { "title": "…", "identifier": "PLT-TASK-12|null" }, "project": { "id": "guid", "name": "…" } | null,
  "fields": [ { "field": "title", "name": "Заголовок", "value": "…", "was": null } ],
  "reasons": [ { "severity": "error|warning", "code": "imports.row.convert_failed", "params": {…}, "column": "Важность", "cell": "G12", "value": "7", "text": "…" } ]
}
```

`importable` = `totals.created + updated + unchanged > 0 and not stale` — the "at least one row that is not an error" gate for «Импортировать» (BR-IE-14; the button itself is feat-067's); `unchanged` counts as not an error. `fields[].was` is set on an update (old -> new). `text` is the Russian reason from the catalog. `notes` codes carry their text through the same catalog: `imports.dry_run.no_identifier_column` («Без столбца идентификатора каждая строка создаёт новый элемент.»), `imports.dry_run.empty_file` («В файле нет строк данных.»). `rows` holds the first 50 rows of the chosen `result` in file order.

Errors: 400 `imports.file.refused` (`columns_missing` with `columns`, `sheet_missing` with `sheet`, or a file reason); 404 `imports.template.not_found`, `imports.file.not_found`; 403.

### 4.2 `GET /imports/dry-runs/{dryRunId}?result=all|created|updated|unchanged|skipped|error` -> 200 `DryRunDto` (the same shape; `stale` is true when the template's version is no longer `templateVersion`, or the template was deleted). 404 `imports.dry_run.not_found` (unknown, another user's, expired).

### 4.3 `GET /imports/dry-runs/{dryRunId}/report` -> 200 `.xlsx` (`Content-Disposition` attachment), one row per file row: row number, result, item title, identifier, project, every reason and warning (text). Cells beginning with `=`, `+`, `-`, `@`, tab or carriage return get a leading apostrophe (BR-IE-22). 404 as 4.2.

Row reasons (a row may carry several): `imports.row.empty` («Пустая строка»), `convert_failed` (params `column`, `cell`, `value`, `converter`), `value_not_recoded` (policy error: «Значение «{value}» не сопоставлено»), `recoding_outdated` (target no longer qualifies; project: «Проект не найден.»), `project_forbidden` («Нет прав на импорт в этот проект.»), `project_not_found`, `type_not_found`, `state_not_found`, `assignee_not_found`, `team_not_linked` («Команда не участвует в проекте.»), `field_not_on_type` («Это поле недоступно для рабочего элемента этого типа.»; warning when `fieldAbsent = drop`), `date_out_of_range` («Укажите дату с 2000 по 2099 год.»), `regex_timeout` («Шаблон выполняется слишком долго.»), `identifier_not_found` («Элемент не найден.»), `identifier_deleted` («Элемент находится в корзине.»), `identifier_skipped` («Строка без идентификатора пропущена по настройке шаблона.»), `identifier_duplicate` («Тот же идентификатор в строке {row}.»), `project_changed`, `type_changed` (an update never moves an item), `value_invalid` (a field's own range/length rule, params `field`), `required_missing` (a required field's cell is empty). No `value_truncated` is emitted. Texts of the form's own errors are reused from `contracts/errors/workitems.json` where they exist.

## 5. Behaviour notes

- Header matching ignores letter case and surrounding spaces; extra file columns are ignored except «Идентификатор» (BR-IE-10).
- An unrecoded reference value takes the table's policy (BR-IE-7); a policy `empty` is refused on save for `project`, `type`, `priority`.
- Identifiers compare ignoring case and surrounding spaces; two rows with one identifier are both errors naming each other.

## 6. Error codes

Registry in `contracts/errors/imports.json` (ru, kk marked for review) and `docs/error-codes.md`.

| Code | Status | Ru text |
|---|---|---|
| `imports.access_forbidden` | 403 | У вас нет прав на эту операцию. |
| `imports.validation_failed` | 400 | Проверьте правильность заполнения полей. |
| `imports.template.not_found` | 404 | Шаблон не найден. |
| `imports.template.name_taken` | 409 | Шаблон с таким названием уже есть. |
| `imports.template.limit_reached` | 409 | В организации уже 100 шаблонов — это предел. Удалите ненужный шаблон. |
| `imports.template.name_required` | 400 (field) | Укажите название. |
| `imports.template.name_too_long` | 400 (field) | Название — не больше 100 символов. |
| `imports.source.header_row_invalid` | 400 (field) | Строка заголовков — число от 1 до 50. |
| `imports.source.first_data_row_invalid` | 400 (field) | Первая строка данных должна быть ниже строки заголовков. |
| `imports.source.header_empty` | 400 | В этой строке нет заголовков. Выберите другую строку. |
| `imports.mapping.source_required` | 400 (field) | Укажите столбец или константу. |
| `imports.mapping.field_unknown` / `field_taken` / `column_unknown` / `converter_unknown` / `converter_param_invalid` / `clear_not_allowed` | 400 (field) | Поле не поддерживается. / Поле уже сопоставлено. / В файле нет столбца «{column}». / Неизвестное преобразование. / Укажите значение. / Для этого поля нельзя очищать значение. |
| `imports.mapping.list_into_single` | 400 (field) | Поле «{field}» хранит одно значение: уберите «Разделить» из цепочки. |
| `imports.mapping.chain_too_long` | 400 (field) | В цепочке не больше 10 преобразований. |
| `imports.mapping.pattern_too_long` / `pattern_invalid` | 400 (field) | Шаблон — не больше 500 символов. / Некорректное регулярное выражение. |
| `imports.recoding.default_required` / `empty_not_allowed` / `target_unknown` | 400 (field) | Выберите значение. / Для этого поля нельзя оставить пустым. / Значение больше недоступно. Выберите другое. |
| `imports.recoding.too_many_entries` | 400 (field) | В таблице не больше 1 000 значений: столбец с большим числом разных значений — свободный текст. |
| `imports.file.refused` | 400 | Файл не принят. |
| `imports.file.unsupported_format` | 400 (field `file`) | Поддерживаются файлы .xlsx и .csv. |
| `imports.file.too_large` | 400 (field) | Файл больше 10 МБ. |
| `imports.file.too_many_rows` | 400 (field) | В файле больше 5 000 строк данных. Разделите его на части. |
| `imports.file.too_many_columns` | 400 (field) | В файле больше 200 столбцов. |
| `imports.file.too_many_sheets` | 400 (field) | В книге больше 50 листов. |
| `imports.file.expands_too_much` | 400 (field) | Файл слишком велик после распаковки и не принят. |
| `imports.file.external_reference` | 400 (field) | Файл ссылается на другой файл и не принят. |
| `imports.file.has_macros` | 400 (field) | В файле есть макросы, такой файл не принят. |
| `imports.file.unreadable` | 400 (field) | Файл не удалось прочитать. Проверьте, что он не повреждён. |
| `imports.file.columns_missing` | 400 (field) | В файле нет столбцов, которые указаны в шаблоне: {columns}. |
| `imports.file.sheet_missing` | 400 (field) | В файле нет листа «{sheet}», указанного в шаблоне. |
| `imports.file.not_found` | 404 | Файл не найден или срок его хранения истёк. Загрузите его снова. |
| `imports.dry_run.not_found` | 404 | Результат проверки не найден или устарел. Проверьте файл снова. |

The `imports.row.*` and `imports.dry_run.*` note codes are catalog entries too (texts as § 4.3); they appear in report rows, not as HTTP errors.
