# Design: Import constructor — templates, mapping, recoding, converters, dry run (mod-024)

Inherits doc-017 (`spec.md`, US-1..-4, US-7, ADR-0042); BR-IE-* ids are cited, not repeated. Patterns come from mod-015's design (registry § 8, dialogs § 9, state canon § 7, form page § 9.3) and the kit (`docs/conventions/ui-kit.md`): this file fills them with its own fields and words. Clickable prototype: [prototype/index.html](prototype/index.html) (made-up data only; the role and state switchers on its top strip exist only there). All copy below is final Russian.

**Out of scope, absent from the screens:** «Импортировать» and the batch list (feat-067), «Экспорт по шаблону» (feat-069), the field «Внешние ссылки» (feat-066), «Участники» (feat-068), tags, comments. Nothing is drawn disabled or «скоро» (mod-015 § 4.2). Where they will land: «Импортировать» takes the empty primary slot of the check page's header (§ 5); a «Партии» tab joins the list page's tabs; the fields join the mapping's field list as their lines merge.

## 1. Screen map

| # | Screen | Address (under `/organizations/:o/settings/imports`) | Stories |
|---|---|---|---|
| S1 | Template list | `` (the settings entry «Импорт») | US-7 |
| S2 | Template editor: tabs «Источник», «Сопоставление», «Значения», «Идентификатор» | `/templates/new`, `/templates/:t` | US-1, -2, -3, -7 |
| S2a | Chain dialog (converters of one mapping) | dialog over S2 | US-3 |
| S2b | Read-only template (importer who may not edit) | `/templates/:t` | US-7 |
| S2c | Export columns dialog (members) | dialog over S1 | US-7 |
| S3 | Check a file (dry run and its report) | `/check?template=:t` | US-4 |

Navigation model: one settings entry «Импорт» in the section «Организация», code `imports.template.read`, organization scope, landing `/organizations/:o/settings/imports`, no icon (settings entries carry none). S1 → S2 by the row or «Создать шаблон»; S2 → S3 by «Проверить файл»; S1 → S3 by «Проверить файл». Trail: «Настройки / Импорт» on S1; S2 and S3 pass the template name or «Проверка файла» as the only ancestor chain below the entry. Page `h1`: «Импорт» (S1), the template name or «Новый шаблон» (S2), «Проверка файла» (S3). Every page sits under `OrganizationGate`.

### What each role sees

| Role (BR / ADR-0042 decision 5) | S1 | S2 | S3 |
|---|---|---|---|
| Организация «Администратор» (`template.write`, `import.run`) | all; «Создать шаблон»; edit/copy/delete on every row | edits any | any project |
| «Менеджер» of a project or department (`template.write` as author, `import.run` on its projects) | all; «Создать шаблон»; copy on every row; edit/delete only on own | own: edit; others: S2b | runs; rows outside their projects are row errors |
| «Участник», «Читатель» (`template.read` only) | all rows, no actions but «Столбцы» (S2c) | none: the row opens S2c | none: «Проверить файл» absent; by address `AccessDenied` («У вас нет прав…», mod-015 § 7.2) |
| No `template.read` | `AccessDenied` frame, no request | same | same |

The author of a template who lost «Менеджер» everywhere is treated as not an author (BR-IE-18): the server answers `canEdit`/`canDelete` per row; the page keeps no copy of the rule. Needed from the API (for api-contract.md): page-level `canCreate`, `canRun`; per row `canEdit`, `canDelete`, `canRun`. «Managers' scope» is thereby served, not recomputed (`useObjectAccess`/served flags, frontend.md "Permissions").

## 2. S1 — Template list

```
Trail: Настройки / Импорт
Импорт                                   [Проверить файл]  [+ Создать шаблон]
+----------------------------------------------------------------------------+
| ⚲ [Название] [Автор: Все ▾]                              [⊘ Очистить фильтры]|
+----------------------------------------------------------------------------+
| Название ↑ | Источник           | Автор            | Изменён        |  actions |
| Реестр…    | .xlsx · «Задачи»   | Пользователь А   | 12.11.2026 …   | ✎ ⧉ 🗑 ⋯ |
|                                              1–10 из 14   ‹ ›               |
```

| Block | MUI / kit | Notes |
|---|---|---|
| Header | `PageHeader` «Импорт»; primary «Создать шаблон» (`Add`); text command «Проверить файл» (`PlaylistAddCheck`) | the primary hides without `canCreate`; the command without `canRun` |
| Filter bar | `FilterBar`: `TextField size="small"` «Название»; `TextField select` «Автор» («Все», «Только мои»); «Очистить фильтры» | the name filter is a substring, ignoring case |
| Table | `TableContainer`/`Table size="small" aria-label="Шаблоны импорта"`, `SortableHeaderCell` on «Название», «Автор», «Изменён» | 10 per page, `TablePagination` |
| «Источник» | text «.xlsx · лист «Задачи»» or «.csv · UTF-8, «;»» | |
| «Автор» | display name; «—» when the account is gone | |
| Row actions | `IconButton`+`Tooltip`: `Edit` «Редактировать <название>», `ContentCopy` «Копировать <название>», `DeleteOutline` «Удалить <название>» (destructive colour on hover only); overflow `MoreVert` «Ещё действия» → «Проверить файл по этому шаблону» | only what the row's flags allow; a row with no edit right shows `VisibilityOutlined` «Открыть» instead of `Edit` |
| Copy | immediate request; `Snackbar` 6 s «Создан шаблон «Копия Реестр — задачи».» with action «Открыть» | name from the server (BR-IE-18) |
| Delete | `ConfirmDialog destructive`: title «Удалить шаблон?», text «Шаблон «<название>» будет удалён безвозвратно. Уже выполненные по нему импорты не изменятся.», «Отмена» / «Удалить» | success `Snackbar` «Шаблон удалён.» |
| Limit | at 100 templates «Создать шаблон» and «Копировать» answer `Alert` in the page: «В организации уже 100 шаблонов — это предел. Удалите ненужный шаблон.» | |

States:

- **Loading:** header and filter bar shown, `RegistrySkeleton` (real headers, 5 rows).
- **Error:** `ErrorBanner` title «Не удалось загрузить шаблоны» with the server text and «Повторить».
- **Empty, can create:** frame «Шаблонов пока нет», hint «Шаблон описывает, как столбцы вашей таблицы попадают в поля рабочих элементов. Создайте первый.», action «Создать шаблон» (`Add`).
- **Empty, cannot create:** «Шаблонов пока нет», hint «Шаблоны ещё не созданы. Их создаёт администратор организации или менеджер проекта.», no action.
- **Empty after a filter:** «Ничего не найдено», hint «Ни один шаблон не подходит под заданные условия фильтра.», action «Сбросить фильтр».
- **Delete/copy failure:** `Snackbar` severity error with the server text; a delete of a template already gone: «Шаблон уже удалён.» and the list refreshes.

**S2c, export columns (members):** `Dialog maxWidth="sm"`, title «Столбцы шаблона «<название>»», `List dense` of the template's export header texts in order, a caption «Эти столбцы попадут в файл при экспорте по шаблону.» (the sentence stays true once feat-069 lands; until then members just read the list). Close «Закрыть». Empty: «В шаблоне пока нет столбцов.»

## 3. S2 — Template editor

A form page (mod-015 § 9.3) with `PageHeader`: title, primary «Сохранить» (`Save`), text commands «Проверить файл» (`PlaylistAddCheck`; enabled only with no unsaved changes and a saved template, tooltip «Сначала сохраните шаблон») and, for a saved template, «Копировать»/«Удалить» in «Ещё действия». A name field sits under the header (`TextField` «Название», required, `maxLength` 100), and below it `Tabs` (scrollable below 1024 px, `aria-label="Разделы шаблона"`) with the four tabs. Each tab has an error dot (`Badge`) when it holds an invalid field after a save attempt. No leave guard (mod-015 D-22). The tabs 2..4 before any file is uploaded and nothing saved show the frame «Сначала загрузите файл-образец», hint «Заголовки столбцов берутся из файла на вкладке «Источник».», action «К источнику».

Save: validates all tabs; on failure the first tab with an error opens, the field gets `error`/`helperText`, and the form banner says «Шаблон не сохранён: исправьте отмеченные поля.» Success `Snackbar` «Шаблон сохранён.»; if a check result of the older version is open elsewhere nothing is pushed (the check page notices itself, § 5).

Name validation: empty «Укажите название.»; over 100 «Название — не больше 100 символов.»; taken (server, ignoring case) «Шаблон с таким названием уже есть.»

### 3.1 Tab «Источник» (US-1)

| Block | MUI | Notes |
|---|---|---|
| File | a dashed `Paper variant="outlined"` drop area with `Button` «Выбрать файл» (`UploadFile`) and the hint «Файл .xlsx или .csv, до 10 МБ, до 5 000 строк данных. Файл хранится не дольше 24 часов и нужен только для образца.»; after a pick: file name, size, `IconButton` `Close` «Убрать файл» | one file; a second pick replaces |
| Workbook | `TextField select` «Лист» (all sheets; only for .xlsx), `TextField type="number"` «Строка заголовков» (1–50), «Первая строка данных» | changing any re-reads the columns; `LinearProgress` meanwhile |
| CSV | `TextField select` «Кодировка» («UTF-8», «Windows-1251»), `TextField select` «Разделитель» («;», «,», «Табуляция»); auto-detected, shown with the caption «Определено автоматически» until changed | changing re-reads the columns |
| Preview | `Table size="small"` of the headers with the first 5 data rows (sticky header, scrolls inside) | each header cell captioned with its cell address row/col letter |

Validation: «Строка заголовков — число от 1 до 50.»; «Первая строка данных должна быть ниже строки заголовков.»; a sheet without headers on that row: «В этой строке нет заголовков. Выберите другую строку.»

**Refusals (before any row is read; BR-IE-2, -21, NFR file safety).** One `Alert severity="error"` with `AlertTitle` «Файл не принят» in place of the preview, the reason below, the template untouched. On the check page the same reasons apply (§ 5).

| Cause | Text |
|---|---|
| .xls, .xlsm, other | «Поддерживаются файлы .xlsx и .csv.» |
| over 10 MB | «Файл больше 10 МБ.» |
| over 5 000 data rows | «В файле больше 5 000 строк данных. Разделите его на части.» |
| over 200 columns | «В файле больше 200 столбцов.» |
| over 50 sheets | «В книге больше 50 листов.» |
| expands past 100 MB | «Файл слишком велик после распаковки и не принят.» |
| external-file reference | «Файл ссылается на другой файл и не принят.» |
| macro | «В файле есть макросы, такой файл не принят.» |
| unreadable / damaged | «Файл не удалось прочитать. Проверьте, что он не повреждён.» |
| (check page only) mapped column missing | «В файле нет столбцов, которые указаны в шаблоне: <список заголовков>.» |
| (check page only) workbook has no such sheet | «В файле нет листа «<название>», указанного в шаблоне.» |

The wording of every row but the first is this design's; the developer adds each to `docs/error-codes.md` and the error contracts with these texts.

### 3.2 Tab «Сопоставление» (US-1, US-3)

One row per **mapping** (a source feeding a field). A new template lists one row per file column, pre-filled by BR-IE-4 (header equals a field's Russian name or code, else «Не переносить»); the rows of a saved template are its mappings.

```
Сопоставление                                           [+ Добавить сопоставление]
| Источник        | Поле MUSTASK     | Преобразования      | Пустая ячейка | Тип без поля | ⋯ |
| Столбец «Вид»   | Тип              | [Разделить ›] 2 шага | Не менять ▾  | Ошибка ▾     | 🗑 |
| Константа       | Проект           | —                   | —             | —            |    |
| Столбец «Срок»  | Целевая дата     | Дата по формату     | Не менять ▾  | Не переносить для типов без поля ▾ | |
```

| Block | MUI | Notes |
|---|---|---|
| Table | `TableContainer`+`Table size="small" aria-label="Сопоставление столбцов"`; below 1024 px scrolls inside | |
| «Источник» | `Autocomplete` over the file's headers, plus the two groups «Константа» and «Объединить»; a column absent from the saved file is marked `Chip` «Нет в файле» (`warning`) | «Константа» opens an inline `TextField`/`Autocomplete` for the value (project, type, state… as the field is a dictionary one); «Объединить» an inline `TextField` «Шаблон» with the hint «Столбцы в фигурных скобках: {Система} / {Модуль}»; unknown `{name}`: «В файле нет столбца «<имя>».» |
| «Поле MUSTASK» | `Autocomplete` grouped «Основные» (Проект, Тип, Заголовок, Описание, Статус, Исполнитель, Приоритет, Команда-владелец, Родитель, Идентификатор MUSTASK) and «Поля типов» (feat-022's fields, each option captioned with the types that carry it); «Не переносить» first | a field already fed elsewhere is marked «Уже сопоставлено» and not selectable (one source per field, BR-IE-4); several fields may take one column |
| «Преобразования» | a `Chip` per converter in order (`variant="outlined"`) and `Button variant="text"` «Изменить» (`Tune`) opening S2a; «Добавить» when none | a recoded field adds `Chip` «Значения: N» that jumps to tab «Значения» |
| «Пустая ячейка» | `Select size="small"`: «Не менять» (default), «Очистить» | «Очистить» is absent for list fields (BR-IE-11) |
| «Тип без поля» | `Select size="small"`: «Ошибка строки» (default), «Не переносить для типов без поля» | shown only for a field not on every type |
| Row actions | `IconButton` `DeleteOutline` «Удалить сопоставление» | |
| Footer | caption «Столбцы файла, которые не указаны здесь, игнорируются.» | |

Validation (on save, on the row): Проект, Тип or Заголовок without a source — «Укажите столбец или константу.» (also shown as a line above the table, per missing field, so a deleted row is explained); a list into a single-valued field — «Поле «<поле>» хранит одно значение: уберите «Разделить» из цепочки.»; a «Константа» outside the field's allowed values — the field's own text.

**S2a, chain dialog** (`Dialog maxWidth="md" fullWidth`, title «Преобразования: <поле>»): left, an ordered `List` of the chain (each item: index, converter name, one-line parameters, `IconButton`s `ArrowUpward`/`ArrowDownward` «Выше»/«Ниже» and `DeleteOutline` «Убрать»); «Добавить преобразование» (`Add`) opens a `Menu` grouped by the catalogue's classes (Текст, Числа, Даты, Списки, Ссылки, Таблицы значений, Шаблоны, Константы). Selecting an item shows its parameters in a form on the right; right, under it, «Образец» — a `Table` of the first 5 rows: «Исходное значение → после шага 1 → … → результат» (live, per the saved file; failures marked `error` with the reason). Parameters:

| Converter | Parameters (MUI) |
|---|---|
| Обрезать пробелы | `FormControlLabel Checkbox` «Сжимать пробелы внутри» |
| Обрезать до длины | `TextField type=number` «Длина» |
| Число; Дата из числа Excel; Дата из ISO 8601; URL из гиперссылки | none (Excel: `Select` «Система дат» — «1900», «1904») |
| Дата по формату | `Select` «Формат»: «ДД.ММ.ГГГГ», «ГГГГ-ММ-ДД», «ДД/ММ/ГГГГ», «ММ/ДД/ГГГГ» |
| Разделить | `TextField` «Разделитель» |
| По таблице | editable `Table` «Значение в файле → результат» + the missing-value policy (as § 3.3) |
| Извлечь по шаблону | `TextField` «Регулярное выражение» (≤ 500), caption «Берётся первая группа, иначе всё совпадение. Нет совпадения — пустое значение.» |
| Объединить; Константа | the same inputs as in the table's «Источник»; these two ignore the cell and are the chain's only step |

Validation: 11th converter — «В цепочке не больше 10 преобразований.» («Добавить преобразование» goes inactive with that helper text); expression over 500 — «Шаблон — не больше 500 символов.»; invalid expression — «Некорректное регулярное выражение.»; empty parameter — «Укажите значение.»; a «По таблице» over 1 000 entries — «В таблице не больше 1 000 значений: столбец с большим числом разных значений — свободный текст.» Footer: «Отмена» / «Применить» (the one filled button; nothing is saved to the server until «Сохранить» on S2). Narrow (< 1024 px): the two panes stack.

### 3.3 Tab «Значения» (US-2)

One `Accordion` per recoded mapping (Проект, Тип, Статус, Приоритет, Исполнитель, Команда-владелец, list fields), title «<Поле> ← «<столбец>»» with `Chip`s «Без значения: N» (`warning`), «Новых: N» (`info`). Expanded:

| Block | MUI | Notes |
|---|---|---|
| Policy | `RadioGroup row`: «Ошибка строки», «Значение по умолчанию» (+`Autocomplete` for the value, required then: «Выберите значение.»), «Оставить пустым» | «Оставить пустым» absent for Проект, Тип, Приоритет (always-set or required, BR-IE-6/-7); the server serves which fields |
| Commands | text buttons «Принять все предложенные» (`DoneAll`), «Обновить значения из файла» (`Refresh`) | the second re-reads the saved file's distinct values |
| Filter | `ToggleButtonGroup size="small"`: «Все», «Без значения», «Новые», «Устарели» | |
| Table | `Table size="small"`, `TablePagination` 25: «Значение в файле» · «Строк» · «Для типа» (state tables only) · «Значение MUSTASK» · status | |
| Value cell | `Autocomplete` over what exists only (BR-IE-7): active projects; the process's types and states (never «Удалена»); active members (by name or email); teams; the priority list | no free text, no «Создать»; empty option text «Ничего не найдено. Здесь можно выбрать только то, что уже есть в MUSTASK.» |
| Suggestion | `Chip variant="outlined"` «Предложено: Баг» + `Button` «Принять» in the row, until accepted | applies only on acceptance |
| Type-specific state | row action `AddCircleOutline` «Значение для типа» adds an entry under the row with `Select` «Для типа»; it wins on that type's rows; helper «Действует только для строк этого типа.» | |
| Status | «Новое» (`info`, with the file's row count), «Устарело» (`error`, the target no longer qualifies) | an outdated entry's cell shows the old target struck-through and `helperText` «Значение больше недоступно. Выберите другое.» |

Counts: «Строк» comes from the uploaded file; without a file in the session the column reads «—» and a caption says «Загрузите файл на вкладке «Источник», чтобы увидеть новые значения.» A saved template with a new file: new values appear as «Новое» and saved entries stay (US-7). Over 1 000 distinct values: the accordion is replaced by `Alert severity="warning"` «В столбце «<столбец>» больше 1 000 разных значений — это свободный текст, а не справочник. Уберите преобразование «Значения» или выберите другой столбец.» Several file values may share one target; saving with a «Значение по умолчанию» policy and no default is the field's error. Empty tab: frame «Значения не нужно сопоставлять», hint «Поля, которым нужна таблица значений (тип, статус, исполнитель…), здесь появятся после сопоставления.»

### 3.4 Tab «Идентификатор» (US-4's setting; BR-IE-10)

| Block | MUI | Notes |
|---|---|---|
| Column | `Autocomplete` «Столбец с идентификатором MUSTASK» over the file's headers, plus «Нет» | choosing a column creates/updates the mapping to «Идентификатор MUSTASK» (it appears on «Сопоставление» too); one source of truth in the model |
| Hint | `Alert severity="info"`: «Если в таблице есть столбец «Идентификатор» (его добавляет экспорт) и он не сопоставлен, шаблон использует его для обновления. Без столбца идентификатора каждая строка создаёт новый элемент.» | the second sentence is also the dry run's note |
| Empty identifier | `RadioGroup`: «Создавать» (default), «Пропускать» — label «Строки без идентификатора» | disabled with a caption while no identifier column exists |

## 4. S2b — read-only template

For `imports.import.run` holders who cannot edit: the same tabs, every control `disabled`/read-only (`TextField InputProps readOnly`), no «Сохранить», `Alert severity="info"` above the tabs: «Шаблон создал <автор>. Вы можете проверять по нему файлы, но изменять его может только автор или администратор организации.» Commands: «Проверить файл», «Копировать» (copy is allowed to a «Менеджер»). Loading: `FormSkeleton`; error: `ErrorBanner` «Не удалось загрузить шаблон»; unknown id or another organization's: `NotFound` frame «Шаблон не найден» (hint «Возможно, его удалили. Вернитесь к списку шаблонов.», action «К списку»); no right to read settings: `AccessDenied`.

## 5. S3 — Check a file (dry run, US-4)

```
Проверка файла
+ Шаблон [Реестр — задачи ▾]  Файл [Реестр.xlsx ✕] [Выбрать файл]    [Проверить]
+---------------------------------------------------------------------------+
| (alert: identifier note / stale / refusal)                                |
| [Будет создан 120] [Будет обновлён 30] [Без изменений 40] [Пропущен 2] [Ошибка 8]   [Скачать отчёт]
| № | Результат | Элемент | Проект | Причины        (expand ▾ → fields / old → new)
```

| Block | MUI | Notes |
|---|---|---|
| Inputs | `Paper variant="outlined"`: `Autocomplete` «Шаблон» (only templates the user may run), file picker as § 3.1, `Button variant="contained"` «Проверить» (the only filled button) | the template comes from `?template=`; the file's sheet/columns are read per the template |
| Progress | `LinearProgress` + text «Проверяем файл… Это может занять до минуты.» (`role="status"`), «Отменить» (text button) aborts the request client-side | no result is kept after «Отменить» |
| Notes | `Alert severity="info"` «Без столбца идентификатора каждая строка создаёт новый элемент.» when the template and file have none (BR-IE-10) | |
| Totals | `ToggleButtonGroup` of five buttons (name + count; the active one filters); counts cover the whole file; a `Chip` per result in the table uses text and icon (`AddCircleOutline`, `EditOutlined`, `RemoveCircleOutline`, `SkipNext`, `ErrorOutline`) plus the semantic colour, never colour alone | «Скачать отчёт» (`FileDownload`, text button): the full report .xlsx |
| Preview | `Table size="small"` + `Collapse` per row. Columns: «Строка», «Результат», «Элемент» (title; identifier for updates), «Проект», «Причины» (the first reason + «ещё N») | the server returns the first **50 rows of the selected result**, caption: «Показаны первые 50 строк. Полный отчёт — в файле.» |
| Row detail | create: `Table` «Поле → значение»; update: «Поле: было → стало»; reasons as `List`: «Столбец «Важность», ячейка G12, значение «7»: …»; warnings with `WarningAmber` | a row may carry several reasons |
| Stale | after the template is saved in another tab, the next interaction shows `Alert severity="warning"` «Шаблон изменён после проверки. Проверьте файл снова.» and «Проверить» is offered again; the report greys but stays readable | the reserved place for «Импортировать» (feat-067) simply stays empty; it also must not appear after a stale or all-error run (BR-IE-14) |

States: before a run — frame `InboxOutlined` «Выберите шаблон и файл», hint «После проверки здесь появится отчёт: что будет создано, обновлено и какие строки с ошибками. Ничего не записывается.». **Loading:** progress as above. **Refusal:** `Alert` «Файл не принят» with a § 3.1 reason, no table. **All rows errors or skipped:** `Alert severity="warning"` «Нет строк, которые можно импортировать. Исправьте файл или шаблон и проверьте снова.» **Empty file (no data rows):** the same alert with «В файле нет строк данных.» **Filter without rows:** «Нет строк с таким результатом.» **Error (server/timeout):** `ErrorBanner` «Не удалось проверить файл», «Повторить». **Not permitted:** a user with no `canRun`: `AccessDenied`; a «Менеджер»'s rows into other projects: row error «Нет прав на импорт в этот проект.» (a normal result, not a banner); a loss of right mid-check: `ErrorBanner` with the server text. Row-level texts come from the form's error contracts (`Элемент не найден.`, `Элемент находится в корзине.`, `Команда не участвует в проекте.`, `Проект не найден.`, `Это поле недоступно для рабочего элемента этого типа.`, `Укажите дату с 2000 по 2099 год.`, `Шаблон выполняется слишком долго.`); the check's own: «Пустая строка» (reason of a skipped empty row), «Строка без идентификатора пропущена по настройке шаблона.», «Тот же идентификатор в строке <N>.», «Не удалось преобразовать: столбец «<имя>», ячейка <A1>, значение «<значение>», преобразование «<имя>».» The first two plus the last are this design's wording.

## 6. Cross-cutting

- **Widths:** 640 px and up. Tables scroll inside their container; dialogs `fullWidth`; `Tabs` scroll; the filter bar wraps.
- **Focus and a11y:** a dialog returns focus to its trigger; the progress and the result counts sit in `aria-live="polite"` regions; every icon-only button has an `aria-label` including the object's name.
- **Server state:** keys `['imports','templates',params]`, `['imports','template',id]`; the dry run is a mutation, not cached. Slice `features/imports`, kit parts only (the drop area is `Paper`+`Button`).

## 7. Open points for gate-2

1. **Preview of 50.** BR-IE-13 says "first 50 result rows". Designed as: the first 50 rows of the selected result (so every error is reachable beyond row 50 of the file); the full report is the .xlsx. The alternative — the file's first 50 rows — hides late errors.
2. **Sample file lifetime.** The editor keeps the uploaded sample under BR-IE-14's 24 h; afterwards the saved template still shows its headers and entries, and the counts/new-value detection needs a re-upload (the «Строк» column reads «—»). No deviation from doc-017; the template stores header texts.
3. **Check page, not editor.** The dry run is its own page by design (the editor's file is a sample; the run is by the saved version). No spec change.
4. **Wording of refusals and a few row reasons** marked above is new (spec fixes one text).
5. **Entry visibility.** «Импорт» in settings follows `imports.template.read`, which every member holds, so «Участник» and «Читатель» see the list and the column dialog only. If the human prefers the entry only for those who may run or write, the member's view (S2c) moves to the future export dialog.
