# US-6: A user re-parents any row by keyboard

- story: US-6
- depends-on: US-5
- shippable: no — it is the keyboard and touch path of US-5's drop
- status: pending

As a **signed-in user who does not use a mouse, or works on a touch
screen**, I want **every parent a drop can give to be reachable from the
row's menu**, so that **dragging is never the only way to restructure the
backlog**.

## Acceptance criteria

Data: US-1's. Spec BR-T-13, BR-T-14, BR-T-15; `design.md` § 4.7 («Ещё
действия»), § 4.10 (the dialog «Изменить родителя»), the spec's § Notices;
feat-001 US-5's dialog behaviour otherwise unchanged. D-7 (2026-10-09): the
«⋮» sits at the title cell's right edge, and the reorder's keyboard path
(mod-006's handle menu) moved into it.

```gherkin
Scenario: Every row has the menu, on every level
  Given the «Эпики» backlog with every row expanded
  Then every row, MSK-EPIC-1 and MSK-EPIC-20 and every nested row included,
    has «Ещё действия: <ID>» — a «⋮» at its title cell's right edge, shown
    on hover, focus and selection, in the Tab order — with «Изменить
    родителя…» and «Убрать родителя»
  And «Убрать родителя» is disabled on MSK-EPIC-1 and MSK-EPIC-20, which
    have no parent
  And no column holds the menu and no header names one

Scenario: A top-level row's menu also reorders it
  Given the «Истории» backlog: MSK-US-2 «1», MSK-BUG-4 «2», MSK-US-22 «3»
  When the user opens «Ещё действия: MSK-US-22»
  Then the menu reads the subheader «Переместить» with «В начало», «Выше»,
    «Ниже», «В конец», then «Изменить родителя…» and «Убрать родителя»
  And «Ниже» and «В конец» are disabled
  When the user chooses «В начало»
  Then MSK-US-22 reads «1», the status region says «MSK-US-22 перемещён:
    позиция 1 из 3.», and focus is on «Ещё действия: MSK-US-22»

Scenario: A nested row's menu has no moves
  Given the «Эпики» backlog with MSK-EPIC-1 expanded
  Then «Ещё действия: MSK-FEAT-8» holds «Изменить родителя…» and «Убрать
    родителя» only

Scenario: The dialog offers every type BR-H-2 allows
  When the user opens «Изменить родителя…» for MSK-BUG-4
  Then the picker offers «Эпик», «Фича», «Пользовательская история» and
    «Баг» items, MSK-BUG-4 itself not among them, and no «Нет родителя»
  And the helper reads «Родителем может быть: Эпик, Фича, Пользовательская
    история, Баг.»
  And MSK-FEAT-6 is preselected and «Сохранить» is disabled

Scenario: A nested bug gets a story as its parent by keyboard
  Given the «Эпики» backlog with MSK-EPIC-1 and MSK-FEAT-6 expanded
  When the user tabs to «Ещё действия: MSK-BUG-4», presses Enter,
    ArrowDown to «Изменить родителя…», Enter, types «MSK-US-2», picks it
    with the arrows and Enter, then Tab to «Сохранить» and Enter
  Then «У MSK-BUG-4 теперь родитель MSK-US-2.» is shown
  And after the read MSK-BUG-4 stands beneath MSK-US-2, and focus is on
    «Ещё действия: MSK-BUG-4»

Scenario: An epic gets an epic as its parent
  When the user gives MSK-EPIC-20 the parent MSK-EPIC-1 through its menu
  Then on «Эпики» MSK-EPIC-1 reads «1» and is the only numbered row
  And MSK-EPIC-20 stands beneath it before MSK-FEAT-8, MSK-FEAT-6 and
    MSK-US-22, an epic being of a higher level

Scenario: «Убрать родителя» on a nested row returns it to the top level
  Given the «Фичи» backlog with MSK-FEAT-8 expanded
  When the user chooses «Убрать родителя» for MSK-FEAT-23
  Then «У MSK-FEAT-23 больше нет родителя.» is shown
  And after the read MSK-FEAT-23 is a top-level row «3»

Scenario: A refused choice stays in the dialog
  When the user picks for MSK-FEAT-8 its descendant MSK-FEAT-23 and saves
  Then «Нельзя выбрать родителем MSK-FEAT-23: он вложен в MSK-FEAT-8
    (MSK-FEAT-8 → MSK-FEAT-23).» shows under «Родитель», the dialog open

Scenario: A parent changed meanwhile is refused, not overwritten
  Given the dialog opened on MSK-US-22 with MSK-EPIC-1 preselected
  And Анна has since given MSK-US-22 the parent MSK-FEAT-6
  When the user picks MSK-FEAT-8 and saves
  Then «Родитель уже изменён другим участником. Данные обновлены —
    проверьте и повторите.» shows under «Родитель», and MSK-FEAT-6 is
    preselected

Scenario: On a touch screen the menu is the path
  Given a touch screen, where a drag does not start
  Then every re-parenting of US-5 can be done through «Ещё действия»

Scenario: One write at a time
  Given a parent write or a move from a menu being saved
  Then every row is undraggable and every row «+», «Ещё действия» and both header buttons is off
    until the backlog is read again
```
