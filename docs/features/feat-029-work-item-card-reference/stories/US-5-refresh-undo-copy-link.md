# US-5: A user refreshes the card, undoes all edits and copies its link

- story: US-5
- depends-on: US-4
- shippable: no — the three actions stand in the header of US-2
- status: pending

As a **signed-in user**, I want **to reload the item, throw away what I typed,
and take the item's address**, so that **I see what others changed without
losing my work, start over when I mistyped, and send a colleague the link**.

## Acceptance criteria

Data: US-1's. Spec BR-C-9..BR-C-11, BR-C-21; `design.md` § 4.2, § 6, § 9. The
card is the dialog of MSK-US-2 unless a scenario says otherwise.

```gherkin
Scenario: «Обновить» shows what others changed
  Given another user changed MSK-US-2's «Область» to «Платформа / API» and
    «Приоритет» to «1» after the card was read
  When the user activates «Обновить»
  Then «Область» shows «Платформа / API», «Приоритет» shows «1», and «Изменён»
    shows the new time
  And the notice «Данные обновлены.» shows

Scenario: «Обновить» keeps what the user typed
  Given the user changed «Описание» and, meanwhile, another user changed
    «Описание» and «Приоритет»
  When the user activates «Обновить»
  Then «Описание» still holds the user's text, «Приоритет» shows the stored
    value, «Сохранить» is still enabled, and the notice reads «Данные
    обновлены. Несохранённые изменения остались на месте.»
  And a save then sends «Описание» alone

Scenario: A refresh that fails
  When the read fails
  Then the card's alert shows the text `apiErrorText` gives (the server's
    `detail`, or «Не удалось соединиться с API. Проверьте, что Mustask.Api
    запущен.» when there was no answer), and the card is as it was

Scenario: «Отменить все изменения» asks first
  Given the user changed «Заголовок», «Приоритет» and «Описание»
  When the user activates «Отменить все изменения»
  Then a dialog «Отменить все изменения?» opens with «Поля вернутся к
    сохранённым значениям. Несохранённые изменения рабочего элемента MSK-US-2
    будут потеряны.», «Продолжить редактирование» and the error-coloured
    «Отменить изменения», focus on «Продолжить редактирование»
  When the user chooses «Продолжить редактирование»
  Then the card is as it was, the edits kept
  When the user opens it again and chooses «Отменить изменения»
  Then the three fields show the stored values, «Сохранить» and «Отменить все
    изменения» are disabled, and the notice «Изменения отменены.» shows

Scenario: Undo is not offered when there is nothing to undo
  Given no field is changed
  Then «Отменить все изменения» is disabled

Scenario: «Копировать ссылку»
  When the user opens «Ещё действия» and chooses «Копировать ссылку»
  Then the clipboard holds the full page's address of MSK-US-2, with the
    organization's and the project's ids and the identifier, and no
    `?workitem`
  And the notice «Ссылка скопирована.» shows
  When the browser refuses the clipboard
  Then the notice reads «Не удалось скопировать ссылку.»

Scenario: «Ещё действия» holds only what the product has
  When the user opens «Ещё действия»
  Then the menu lists «Копировать ссылку» and nothing else — no «Удалить»,
    no «Изменить тип», no «Шаблоны»

Scenario: The menu has the menus' keyboard model
  When «Ещё действия» has focus and the user presses Enter, Space or ArrowDown
  Then the menu opens on its first item; ArrowUp and ArrowDown move, Enter
    chooses, Escape closes it and returns focus to the button
  And Escape in the open menu does not close the dialog

Scenario: Refresh and the host
  Given the dialog is over the «Истории» backlog
  When the user activates «Обновить» after another user changed the title
  Then, once the dialog closes, the backlog row shows that title
```
