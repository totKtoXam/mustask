# US-8: A member exports items by a template

- story: US-8
- depends-on: US-7
- shippable: no — an analysis story; delivered by `feat-NNN-import-export-by-template`
- status: pending

As an **organization member**, I want **to export the items I see into the sheet's own columns and values**, so that **the weekly report and anyone still on the sheet get a file they can read, and it comes back in by the same template**.

## Acceptance criteria

```gherkin
Scenario: The export follows the template's columns and order
  Given «DevEx — реестр» mapping «Id», «Проект», «Заголовок», «TaskType», «Статус», «Changer», «Priority», «Срок», «Ссылка»
  And the «Рабочие элементы» list filtered to the project «Платформа», 40 items
  When a member chooses «Экспорт по шаблону», the template and .xlsx
  Then the file has a header row with those nine texts in that order and 40 rows (BR-IE-19)

Scenario: Values go back through the recoding tables
  Given the entries UserStory → «Пользовательская история» and «00 urgent» → 1
  When an item of that type with priority 1 is exported
  Then its row reads «UserStory» and «00 urgent» (BR-IE-19)

Scenario: A shared target goes back to the value marked for export
  Given «01. Новая заявка» and «02. Анализ» both → «Новая», with «01. Новая заявка» marked «при экспорте»
  When an item in «Новая» is exported
  Then its «Статус» reads «01. Новая заявка» (BR-IE-19)

Scenario: Dates, links and lists go out in the file's own form
  Given an item with «Целевая дата» 01.01.2026, the link «https://docs.example.com/d/1» titled «Документ», and tags FE and BE
  When it is exported to .xlsx
  Then «Срок» is a date cell, «Ссылка» a hyperlink cell to that address showing «Документ», and «Теги» reads «FE, BE» (BR-IE-19)
  When it is exported to .csv
  Then «Срок» is written by the template's date format, and the file is UTF-8 with a BOM and the template's delimiter (BR-IE-19)

Scenario: Hours go back to a bucket only when one bucket gives them
  Given «По таблице» «1-2 часа» → 2 and «10-20 мин» → 0,5
  When an item with «Исходная оценка» 2 and another with 3 are exported
  Then the first reads «1-2 часа» and the second 3 (BR-IE-19)

Scenario: Columns that do not reverse are named before the file is made
  Given a template with a «Константа», an «Объединить» and an «Извлечь по шаблону» column
  When the member opens the export dialog
  Then those columns are listed as not restored
  And in the file the first two are empty and the third holds the field's value (BR-IE-20)

Scenario: The exported file comes back in by key
  Given a file exported by «DevEx — реестр» of items that all carry a «DevEx» key, edited and uploaded again with the same template
  When the dry run runs
  Then every unedited row is «Без изменений» and every edited row «Будет обновлён» (BR-IE-10, BR-IE-11)

Scenario: An item without a key of the template's source is counted before the file is made
  Given the filtered list holds 40 items, 3 of them created by hand
  When the member opens the export dialog for «DevEx — реестр»
  Then the dialog says 3 rows will have an empty «Id» and will not be matched when brought back
  And in the file those 3 rows have an empty «Id» (BR-IE-19)

Scenario: Only what the user may read is exported
  Given a project the member may not see
  When the member exports the whole list
  Then no row of that project is in the file, and the row count says nothing of it (doc-012 BR-MX-21)
  # owed by feat-041 if it merges after the export line

Scenario: A reader may export
  Given an organization «Читатель»
  When they export by a template
  Then they get the file (§ Roles)

Scenario: An export over 5 000 items is refused, never cut
  Given a list filter showing 5 001 items
  When the member exports
  Then no file is made, and «Уточните фильтр: в выгрузке больше 5 000 строк.» is shown (BR-IE-21)

Scenario: A text that a spreadsheet would run is neutralised
  Given an item whose title is «=СУММ(A1:A9)»
  When it is exported
  Then the cell holds the text with a leading apostrophe, and opening the file runs nothing (BR-IE-22)

Scenario: 5 000 items export within 30 seconds
  Given a filter showing 5 000 items
  When the member exports to .xlsx
  Then the file is ready within 30 s (NFR volumes)
```
