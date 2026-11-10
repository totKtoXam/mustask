# US-4: A dry run shows what each row would do, writing nothing

- story: US-4
- depends-on: US-1, US-2, US-3, US-9
- shippable: no — an analysis story; delivered by `mod-NNN-import-constructor`
- status: pending

As an **organization administrator**, I want **to see what every row would do before anything is written**, so that **I fix the template, not hundreds of items**.

The examples use the source «DevEx» and keys like `010125 - 01`.

## Acceptance criteria

```gherkin
Scenario: The dry run reports each row and writes nothing
  Given a template keyed by «Внешний ключ», source «DevEx», and a file of 543 rows
  When the administrator runs «Проверить»
  Then each row gets one of «Будет создан», «Будет обновлён», «Без изменений», «Пропущен», «Ошибка»
  And the totals per result are shown and add up to 543
  And no item, key, tag, link or batch exists afterwards that did not exist before (BR-IE-13)

Scenario: A row whose key names an item will update it
  Given an item carrying «DevEx: 010125 - 01» whose title is «Старое название»
  When a row with the key «010125 - 01» and the title «Новое название» is checked
  Then the row is «Будет обновлён» and the preview shows «Заголовок: Старое название → Новое название» (BR-IE-10, BR-IE-13)

Scenario: A row equal to its item changes nothing
  Given a row whose mapped values equal those of the item its key names
  When the dry run runs
  Then the row is «Без изменений» (BR-IE-11)

Scenario: An error names the column, the cell, the value and the reason
  Given a row 12 whose «Priority» holds «07 очень низкий», in no entry, under «Ошибка строки»
  When the dry run runs
  Then row 12 is «Ошибка» with «Priority», «G12», «07 очень низкий» and the reason (BR-IE-7, BR-IE-13)

Scenario: A row may carry several reasons at once
  Given a row with an unknown type and an unreadable date
  When the dry run runs
  Then the row lists both reasons (BR-IE-13)

Scenario: The same key twice in one file
  Given rows 20 and 41 both keyed «010125 - 07»
  When the dry run runs
  Then both are «Ошибка», row 20 naming row 41 and row 41 naming row 20 (BR-IE-10)

Scenario: A key of an item in the recycle bin
  Given the key «010125 - 09» on an item in «Удалена»
  When its row is checked
  Then the row is «Ошибка» with «Элемент находится в корзине.» (BR-IE-11)

Scenario: A key whose item is in another project, or of another type
  Given the item «DevEx: 010125 - 03» in the project «Платформа», of type «Баг»
  When its row targets «Финансы», or recodes to «Пользовательская история»
  Then the row is «Ошибка»: an item changes neither project nor type (BR-IE-11)

Scenario: Rows without a key follow the template
  Given 111 rows with an empty «Id» and «Строки без ключа» set to «Создавать»
  When the dry run runs
  Then they are «Будет создан» with the warning that a later import will not find them
  When the setting is «Пропускать»
  Then they are «Пропущен» (BR-IE-10)

Scenario: A key in another source is not matched
  Given an item carrying «ТОС: 010125 - 01»
  When a «DevEx» row keyed «010125 - 01» is checked
  Then the row is «Будет создан» (BR-IE-10, OQ-2)

Scenario: Rows matched by the MUSTASK identifier
  Given a template keyed by «Идентификатор MUSTASK» and a row naming «PLT-US-12»
  When the item exists in the row's project
  Then the row is «Будет обновлён»
  And a row naming an identifier that does not exist is «Ошибка» and never creates an item (BR-IE-10)

Scenario: A new item may start in any state of its type
  Given a row of type «Пользовательская история» recoded to «Закрыта» whose key names no item
  When the dry run runs
  Then the row is «Будет создан» in «Закрыта» (BR-IE-12)

Scenario: The preview shows the first 50 result rows
  Given a file of 543 rows
  When the dry run ends
  Then the preview lists rows 1–50 as fields and values
  And «Скачать отчёт» gives a .xlsx with all 543 rows, their result, reasons and warnings (BR-IE-13)

Scenario: Editing the template makes the dry run stale
  Given a finished dry run
  When the administrator changes a recoding entry
  Then «Импортировать» is no longer offered until the dry run runs again (BR-IE-14)

Scenario: A dry run with only errors offers no import
  Given a dry run whose every row is «Ошибка» or «Пропущен»
  When it ends
  Then «Импортировать» is not offered (BR-IE-14)

Scenario: 5 000 rows are checked within a minute
  Given a file of 5 000 rows and 50 columns
  When the dry run runs
  Then it ends within 60 s (NFR volumes)

Scenario: Only the organization administrator runs a dry run
  Given an organization «Участник» or a project «Менеджер»
  When they ask for a dry run
  Then it is refused with «Недостаточно прав» (§ Roles, OQ-1)
```
