# US-4: A dry run shows what each row would do, writing nothing

- story: US-4
- depends-on: US-1, US-2, US-3
- shippable: no — an analysis story; delivered by `mod-024-import-constructor`
- status: pending

As an **importer**, I want **to see what every row would do before anything is written**, so that **I fix the template, not hundreds of items**.

Identifiers like «PLT-TASK-12» are made up; «Идентификатор» is the column a template maps to «Идентификатор MUSTASK».

## Acceptance criteria

```gherkin
Scenario: The dry run reports each row and writes nothing
  Given a template and a file of 500 rows
  When the importer runs «Проверить»
  Then each row gets one of «Будет создан», «Будет обновлён», «Без изменений», «Пропущен», «Ошибка»
  And the totals per result are shown and add up to 500
  And no item, link, tag or batch exists afterwards that did not exist before (BR-IE-13)

Scenario: A template without an identifier column only creates, and says so
  Given a template that maps no column to «Идентификатор MUSTASK», and a file with no column «Идентификатор»
  When the dry run runs
  Then every row that is not an error or skipped is «Будет создан»
  And the report says «Без столбца идентификатора каждая строка создаёт новый элемент.» (BR-IE-10)

Scenario: The identifier column an export added is used by a template that maps none
  Given a template that maps no identifier, and a file it exported, whose last column «Идентификатор» names «PLT-TASK-12»
  When the dry run runs
  Then that row matches «PLT-TASK-12» and is «Без изменений» or «Будет обновлён», never «Будет создан» (BR-IE-10, BR-IE-19)
  But when the template sets a column «Идентификатор» to «Не переносить», that column is not used and the rows create

Scenario: A row whose identifier names an item will update it
  Given the item «PLT-TASK-12» whose title is «Старое название»
  When a row with «Идентификатор» «PLT-TASK-12» and the title «Новое название» is checked
  Then the row is «Будет обновлён» and the preview shows «Заголовок: Старое название → Новое название» (BR-IE-10, BR-IE-13)

Scenario: A row equal to its item changes nothing
  Given a row whose mapped values equal those of the item its identifier names
  When the dry run runs
  Then the row is «Без изменений» (BR-IE-11)

Scenario: An identifier that names no readable item never creates
  Given a row naming «PLT-TASK-999», which does not exist, and a row naming an item of a project the importer cannot read
  When the dry run runs
  Then both rows are «Ошибка» with «Элемент не найден.», and neither would create an item (BR-IE-10)

Scenario: Rows with an empty identifier follow the template
  Given 40 rows with an empty «Идентификатор» and «Строки без идентификатора» set to «Создавать»
  When the dry run runs
  Then they are «Будет создан»
  When the setting is «Пропускать»
  Then they are «Пропущен» (BR-IE-10)

Scenario: The same identifier twice in one file
  Given rows 20 and 41 both naming «PLT-TASK-7»
  When the dry run runs
  Then both are «Ошибка», row 20 naming row 41 and row 41 naming row 20 (BR-IE-10)

Scenario: An identifier of an item in the recycle bin
  Given «PLT-TASK-9» is in «Удалена»
  When its row is checked
  Then the row is «Ошибка» with «Элемент находится в корзине.» (BR-IE-11)

Scenario: An identifier whose item is in another project, or of another type
  Given the item «PLT-BUG-3» in the project «Платформа», of type «Баг»
  When its row targets «Финансы», or recodes to «Задача»
  Then the row is «Ошибка»: an item changes neither project nor type (BR-IE-11)

Scenario: An error names the column, the cell, the value and the reason
  Given a row 12 whose «Важность» holds «7», in no entry, under «Ошибка строки»
  When the dry run runs
  Then row 12 is «Ошибка» with «Важность», «G12», «7» and the reason (BR-IE-7, BR-IE-13)

Scenario: A row may carry several reasons at once
  Given a row with an unknown type and an unreadable date
  When the dry run runs
  Then the row lists both reasons (BR-IE-13)

Scenario: A new item may start in any state of its type
  Given a row of type «Пользовательская история» recoded to «Закрыта» with an empty identifier
  When the dry run runs
  Then the row is «Будет создан» in «Закрыта» (BR-IE-12)

Scenario: A manager's row aimed at a project they do not manage
  Given «Пользователь Д» is the project «Менеджер» of «Платформа» only
  When they dry-run a file whose rows target «Платформа» and «Финансы»
  Then the «Платформа» rows are checked as usual
  And each «Финансы» row is «Ошибка» with «Нет прав на импорт в этот проект.» (§ Roles, BR-IE-12, OQ-1 B)

Scenario: A department manager imports into the department's projects
  Given «Пользователь Е» is the department «Менеджер» whose grant reaches «Платформа» and «Финансы»
  When they dry-run rows targeting both
  Then no row is refused for its project (§ Roles, ADR-0038)

Scenario: The preview shows the first 50 result rows
  Given a file of 500 rows
  When the dry run ends
  Then the preview lists rows 1–50 as fields and values
  And «Скачать отчёт» gives a .xlsx with all 500 rows, their result, reasons and warnings (BR-IE-13)

Scenario: Editing the template makes the dry run stale
  Given a finished dry run
  When the administrator changes a recoding entry
  Then «Импортировать» is no longer offered until the dry run runs again (BR-IE-14, BR-IE-18)

Scenario: A dry run with only errors offers no import
  Given a dry run whose every row is «Ошибка» or «Пропущен»
  When it ends
  Then «Импортировать» is not offered (BR-IE-14)

Scenario: 5 000 rows are checked within a minute
  Given a file of 5 000 rows and 50 columns
  When the dry run runs
  Then it ends within 60 s (NFR volumes)

Scenario: A member who manages nothing cannot run a dry run
  Given an organization «Участник» who is «Менеджер» of no project or department
  When they ask for a dry run
  Then it is refused with «Недостаточно прав» (§ Roles)
```
