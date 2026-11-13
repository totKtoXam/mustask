# US-5: An import creates items, and updates them by the MUSTASK identifier, into a batch

- story: US-5
- depends-on: US-4
- shippable: no — an analysis story; delivered by `feat-067-import-run-and-undo`
- status: pending

As an **importer**, I want **to run the checked import and get one batch with a report**, so that **the whole register lands at once and I can see, and later undo, what happened**.

## Acceptance criteria

```gherkin
Scenario: The import writes the rows and records one batch
  Given a dry run of 500 rows: 460 «Будет создан», 30 «Будет обновлён», 10 «Ошибка»
  When the importer presses «Импортировать»
  Then 460 items are created and the 30 items their identifiers name are updated
  And one batch records who, when, the file name, the template as it was, the counts and the per-row report (BR-IE-14, BR-IE-15)

Scenario: An exported file comes back as updates
  Given a file exported by a template, with the title of «PLT-TASK-12» edited and one new row with an empty «Идентификатор»
  When it is imported by the same template
  Then «PLT-TASK-12» is updated, the new row creates an item, and every other row is «Без изменений» (BR-IE-10, BR-IE-11)

Scenario: The same file without an identifier column, imported twice, creates twice
  Given a template that maps no identifier and a file of 20 rows already imported once
  When it is imported again
  Then 20 more items are created; the dry run had said every row creates (BR-IE-10)

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
  Given a template that maps only «Идентификатор», «Теги» and «Участники», and «PLT-TASK-12» whose title was edited in MUSTASK
  When the import writes its row
  Then the title stays as edited, and only the tags and participants change (BR-IE-6, BR-IE-11)

Scenario: An empty cell keeps or clears as the mapping says
  Given an item with the assignee «Пользователь А» and a row with an empty «Ответственный»
  When the mapping says «Не менять»
  Then the assignee stays «Пользователь А»
  When the mapping says «Очистить»
  Then the item has no assignee (BR-IE-6)

Scenario: A multi-value field is merged on update, and a comment is not repeated
  Given «PLT-TASK-12» with the tags front and qa, and the comment «Согласовать макет» added by an earlier import
  When a row naming it holds the tags «front, back» and the comment «Согласовать макет»
  Then the item's tags are front, qa and back, and it still has one such comment (BR-IE-11)
  # tags owed by feat-016, comments by feat-002

Scenario: A created item takes the form's fields in one write
  Given a row creating a «Задача» with «Вид работ» «Разработка» and «Исходная оценка» 2
  When the import writes it
  Then the item is created with both values, in the row's one transaction (BR-IE-12)

Scenario: The importer losing the right to import stops the run
  Given an import running for «Пользователь Г»
  When «Пользователь Г» stops being the organization «Администратор» after row 100, or a manager loses «Менеджер» on the rows' project
  Then no row after 100 is written, and the batch reads «Прерван» (BR-IE-14)

Scenario: An imported write is the importer's
  Given «Пользователь Г» runs the import of a file whose «Создано» column holds «2025-01-10»
  When an item is created by it
  Then its «Создал» is «Пользователь Г» and its «Создан» the time of the write (BR-IE-23)

Scenario: The import runs in the background
  Given an import of 5 000 rows
  When it is started
  Then the page shows «Обработано N из 5000», rising
  And the importer may leave and come back to the same progress
  And it ends within 5 min (BR-IE-14, NFR volumes)

Scenario: One import at a time per organization
  Given an import running in the organization
  When anyone starts another import or an undo there
  Then it is refused with «Импорт уже выполняется.» (BR-IE-14)

Scenario: Import needs a fresh dry run
  Given no dry run of this file with this template version
  When the importer asks to import
  Then the import is refused, and nothing is written (BR-IE-14)

Scenario: A stop mid-run leaves a partial, undoable batch
  Given the service stops after row 200 of 500 is written
  When it starts again
  Then the batch reads «Прерван» with 200 rows written and 300 not written
  And the batch can be undone like any other (BR-IE-14, BR-IE-16)

Scenario: The uploaded file is not kept
  Given a finished import
  When 24 h have passed since the upload
  Then the file is gone and the batch still shows its report (BR-IE-14)

Scenario: The batch list
  Given three batches in the organization, one started by the project «Менеджер» «Пользователь Д»
  When the administrator opens «Пакеты импорта»
  Then all three are listed newest first with who, when, the template, the file name, the counts and the state
  When «Пользователь Д» opens it
  Then only their batch is listed (BR-IE-15, § Roles)

Scenario: Deleting the template keeps its batches
  Given a batch made with the template «Реестр — задачи»
  When the template is deleted
  Then the batch still shows its report and can still be undone (BR-IE-15, BR-IE-18)

Scenario: History records the import
  Given feat-015's history has merged
  When the import updates an item
  Then the item's history shows the change by the importer, naming the batch (BR-IE-23)
  # owed by feat-015
```
