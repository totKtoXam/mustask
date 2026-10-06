# US-5: A user changes a row's parent from the backlog

- story: US-5
- depends-on: US-2, US-4
- shippable: no — the mapping is the backlog side of US-1's parent write
- status: pending

As a **signed-in user**, I want **to give a backlog row its parent one
level up without opening its form**, so that **I can sort a team's stories
into features, and features into epics, straight from the list**.

## Acceptance criteria

Data: US-4's. Spec BR-H-3, -4, -14, § Page «Бэклоги» (the «Действия» menu,
the dialog «Изменить родителя», «Убрать родителя»), § Notices on «Бэклоги».

```gherkin
Scenario: A story gets a feature as its parent
  Given the «Истории» backlog of «Платформа», MSK-US-22's «Родитель» reading
    MSK-EPIC-1
  When the user opens «Ещё действия: MSK-US-22» and chooses «Изменить
    родителя…»
  Then the dialog «Изменить родителя» reads «Пользовательская история
    MSK-US-22 «Запомнить устройство»», MSK-EPIC-1 preselected
  And the picker offers only «Фича» items, with the helper «Предлагаются
    элементы уровня «Фичи». Родителя другого уровня можно выбрать в форме
    рабочего элемента.»
  When the user picks MSK-FEAT-8 and chooses «Сохранить»
  Then the dialog closes, «У MSK-US-22 теперь родитель MSK-FEAT-8.» is shown,
    the row's «Родитель» reads MSK-FEAT-8, focus is on «Ещё действия:
    MSK-US-22»
  And on the «Фичи» backlog MSK-US-22 now shows beneath an expanded
    MSK-FEAT-8

Scenario: A feature gets an epic, replacing its old one in one write
  Given the «Фичи» backlog of «Платформа»
  When the user maps MSK-FEAT-6 to MSK-EPIC-20
  Then MSK-FEAT-6 has exactly one parent, MSK-EPIC-20
  And on the «Эпики» backlog it shows beneath MSK-EPIC-20, not MSK-EPIC-1,
    with MSK-US-2 and MSK-BUG-4 still beneath it

Scenario: «Убрать родителя» clears the parent
  When the user chooses «Убрать родителя» for MSK-FEAT-6 on «Фичи»
  Then its «Родитель» reads «Нет» at once and «У MSK-FEAT-6 больше нет
    родителя.» is shown
  And it keeps its row and its «Порядок» on «Фичи»
  And it is beneath no row of «Эпики»

Scenario: What a row's menu offers
  Then on «Фичи» and «Истории» each top-level row has «Ещё действия: <ID>»
    with «Изменить родителя…» and «Убрать родителя»
  And «Убрать родителя» is disabled on a row whose «Родитель» reads «Нет»
  And «Эпики» rows and nested rows have no «Ещё действия»

Scenario: «Сохранить» waits for a different parent
  When the dialog opens on MSK-FEAT-6, MSK-EPIC-1 preselected
  Then «Сохранить» is disabled until another epic is picked

Scenario: The mapping changes no order and no other field
  When MSK-US-2 is mapped to MSK-FEAT-8
  Then its place on «Истории», its Статус, Исполнитель and Область are
    unchanged
  And among MSK-FEAT-8's children it shows in the «Истории» order

Scenario: A parent changed meanwhile refuses the mapping
  Given Борис's «Истории» backlog shows MSK-US-2 with «Родитель» MSK-FEAT-6
  And Анна has since mapped MSK-US-2 to MSK-FEAT-8
  When Борис maps MSK-US-2 to MSK-FEAT-23
  Then the dialog shows under «Родитель» «Родитель уже изменён другим
    участником. Данные обновлены — проверьте и повторите.»
  And the backlog is read again, MSK-FEAT-8 now preselected
  And MSK-US-2's parent is MSK-FEAT-8

Scenario: A refused clear puts the cell back
  Given the server refuses «Убрать родителя» on MSK-FEAT-6 with
    `workitems.link.parent_changed`
  Then the cell shows the parent again after the backlog is read again
  And the code's text is shown as an error notice
  And with no answer at all, «Не удалось сохранить родителя. Попробуйте ещё
    раз.» is shown

Scenario: One write at a time
  Given a parent write from a row being saved
  Then every handle and every «Ещё действия» is disabled until the answer
    arrives and the backlog is read again

Scenario: The menu is the complete path by keyboard
  Given focus on «Ещё действия: MSK-US-2»
  When the user presses Enter, ArrowDown to «Изменить родителя…», Enter,
    types «sso», picks MSK-FEAT-8 with the arrows and Enter, then Tab to
    «Сохранить» and Enter
  Then MSK-US-2's parent is MSK-FEAT-8 and focus is on «Ещё действия:
    MSK-US-2»
  And no pointer drag onto a row maps a parent
```
