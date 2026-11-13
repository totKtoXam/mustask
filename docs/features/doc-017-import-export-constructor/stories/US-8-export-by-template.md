# US-8: A member exports items by a template

- story: US-8
- depends-on: US-7
- shippable: no — an analysis story; delivered by `feat-069-import-export-by-template`
- status: pending

As an **organization member**, I want **to export the items I see into a source's own columns and values**, so that **a report and anyone still on the spreadsheet get a file they can read, and it comes back in by the same template**.

## Acceptance criteria

```gherkin
Scenario: The export follows the template's columns and order
  Given «Реестр — задачи» mapping «Идентификатор», «Проект», «Заголовок», «Вид», «Статус», «Ответственный», «Важность», «Ссылка»
  And the «Рабочие элементы» list filtered to the project «Платформа», 40 items
  When a member chooses «Экспорт по шаблону», the template and .xlsx
  Then the file has a header row with those eight texts in that order and 40 rows (BR-IE-19)

Scenario: The identifier column is always written
  Given a template that maps no column to «Идентификатор MUSTASK»
  When a member exports by it
  Then the file has its mapped columns and a last column «Идентификатор» holding each item's identifier (BR-IE-19)
  And imported back by the same template, its unedited rows are «Без изменений», not new items (BR-IE-10)

Scenario: Values go back through the recoding tables
  Given the entries Story → «Пользовательская история» and «Срочно» → 1
  When an item of that type with priority 1 is exported
  Then its row reads «Story» and «Срочно» (BR-IE-19)

Scenario: A shared target goes back to the value marked for export
  Given «Открыта» and «В анализе» both → «Новая», with «Открыта» marked «при экспорте»
  When an item in «Новая» is exported
  Then its «Статус» reads «Открыта» (BR-IE-19)

Scenario: Dates, links and lists go out in the file's own form
  Given a template mapping «Срок» to «Целевая дата», «Ссылка» to «Внешние ссылки» and «Теги» split by «,»
  And an item with «Целевая дата» 01.01.2026, the link «https://docs.example.com/d/1» titled «Документ», and tags front and back
  When it is exported to .xlsx
  Then «Срок» is a date cell, «Ссылка» a hyperlink cell to that address showing «Документ», and «Теги» reads «front, back» (BR-IE-19)
  When it is exported to .csv
  Then «Срок» is written by the template's date format, and the file is UTF-8 with a BOM and the template's delimiter (BR-IE-19)

Scenario: A number goes back to its lookup text only when one entry gives it
  Given «По таблице» «M» → 8 and «S» → 2
  When an item with «Исходная оценка» 8 and another with 3 are exported
  Then the first reads «M» and the second 3 (BR-IE-19)

Scenario: Columns that do not reverse are named before the file is made
  Given a template with a «Константа», an «Объединить» and an «Извлечь по шаблону» column
  When the member opens the export dialog
  Then those columns are listed as not restored
  And in the file the first two are empty and the third holds the field's value (BR-IE-20)

Scenario: The exported file comes back in by identifier
  Given a file exported by «Реестр — задачи», edited in two rows and uploaded again with the same template
  When the dry run runs
  Then the two edited rows are «Будет обновлён» and every other row «Без изменений» (BR-IE-10, BR-IE-11)

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
