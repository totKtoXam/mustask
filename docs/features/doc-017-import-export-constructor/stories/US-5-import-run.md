# US-5: An import creates and updates items by key, row by row, into a batch

- story: US-5
- depends-on: US-4
- shippable: no — an analysis story; delivered by `feat-NNN-import-run-and-undo`
- status: pending

As an **organization administrator**, I want **to run the checked import and get one batch with a report**, so that **the whole register lands at once and I can see, and later undo, what happened**.

## Acceptance criteria

```gherkin
Scenario: The import writes the rows and records one batch
  Given a dry run of 543 rows: 500 «Будет создан», 30 «Будет обновлён», 13 «Ошибка»
  When the administrator presses «Импортировать»
  Then 500 items are created carrying their «DevEx» keys and 30 items are updated
  And one batch records who, when, the file name, the template as it was, the counts and the per-row report (BR-IE-14, BR-IE-15)

Scenario: A row that fails does not stop the others
  Given row 77 passes the dry run but its project is deactivated before the import writes it
  When the import reaches row 77
  Then row 77 is «Ошибка» with «Проект не найден.»
  And rows 78 onward are written (BR-IE-14)

Scenario: The import re-checks each row when it writes it
  Given an item changed by «Пользователь А» after the dry run, so a row that was «Без изменений» now differs
  When the import writes that row
  Then the row is «Обновлён», last write winning (BR-IE-11, BR-IE-14)

Scenario: An update writes only the mapped fields
  Given a template that maps only «Теги» and «Участники» and the item «DevEx: 010125 - 01» whose title was edited in MUSTASK
  When the re-import writes its row
  Then the title stays as edited, and only the tags and participants change (BR-IE-6, BR-IE-11)

Scenario: An empty cell keeps or clears as the mapping says
  Given an item with the assignee «Пользователь А» and a row with an empty «Changer»
  When the mapping says «Не менять»
  Then the assignee stays «Пользователь А»
  When the mapping says «Очистить»
  Then the item has no assignee (BR-IE-6)

Scenario: A multi-value field is merged on update, and a comment is not repeated
  Given the item «DevEx: 010125 - 01» with the tags FE and QA, and the comment «Согласовать макет» added by an earlier import
  When a re-import's row holds the tags «FE, BE» and the comment «Согласовать макет»
  Then the item's tags are FE, QA and BE, and it still has one such comment (BR-IE-11)
  # tags owed by feat-016, comments by feat-002

Scenario: A created item takes the form's fields in one write
  Given a row creating a «Задача» with «Вид работ» «Разработка» and «Исходная оценка» 2
  When the import writes it
  Then the item is created with both values, in the row's one transaction (BR-IE-12)

Scenario: The importer losing the administrator role stops the run
  Given an import running for «Пользователь Г»
  When «Пользователь Г» stops being the organization «Администратор» after row 100
  Then no row after 100 is written, and the batch reads «Прерван» (BR-IE-14)

Scenario: An imported write is the administrator's
  Given the administrator «Пользователь Г» runs the import
  When an item is created by it
  Then its «Создал» is «Пользователь Г» and its «Создан» the time of the write (BR-IE-23)

Scenario: The import runs in the background
  Given an import of 5 000 rows
  When it is started
  Then the page shows «Обработано N из 5000», rising
  And the administrator may leave and come back to the same progress
  And it ends within 5 min (BR-IE-14, NFR volumes)

Scenario: One import at a time per organization
  Given an import running in the organization
  When the administrator starts another import or an undo
  Then it is refused with «Импорт уже выполняется.» (BR-IE-14)

Scenario: Import needs a fresh dry run
  Given no dry run of this file with this template version
  When the administrator asks to import
  Then the import is refused, and nothing is written (BR-IE-14)

Scenario: A stop mid-run leaves a partial, undoable batch
  Given the service stops after row 200 of 543 is written
  When it starts again
  Then the batch reads «Прерван» with 200 rows written and 343 not written
  And the batch can be undone like any other (BR-IE-14, BR-IE-16)

Scenario: The uploaded file is not kept
  Given a finished import
  When 24 h have passed since the upload
  Then the file is gone and the batch still shows its report (BR-IE-14)

Scenario: The batch list
  Given three batches in the organization
  When the administrator opens «Пакеты импорта»
  Then they are listed newest first with who, when, the template, the file name, the counts and the state (BR-IE-15)

Scenario: Deleting the template keeps its batches
  Given a batch made with the template «DevEx — реестр»
  When the template is deleted
  Then the batch still shows its report and can still be undone (BR-IE-15, BR-IE-18)

Scenario: History records the import
  Given feat-015's history has merged
  When the import updates an item
  Then the item's history shows the change by the administrator, naming the batch (BR-IE-23)
  # owed by feat-015
```
