# US-4: A user adds a child to a row

- story: US-4
- depends-on: US-2
- shippable: no — the new child is seen only through US-2's tree
- status: pending

As a **signed-in user**, I want **to add a feature to an epic, a story or
a bug to a feature, a task or a bug to a story right from its row, in the
tree**, so that **I break work down where I see it, without a dialog and
without linking it afterwards**.

D-7 (2026-10-09) replaced the dialog of the first draft by an inline row, as
in the reference screenshot (D-6 OQ-3 (a) is overridden), and D-6 OQ-6 (b) took
the entry off a bug.

## Acceptance criteria

Data: US-1's; MSK-FEAT-6's area is «Платформа / API», every other
«Платформа» item's «Платформа». Spec BR-T-9, BR-T-10, BR-T-15, BR-T-17;
`design.md` § 4.5 (the row «+»), § 4.8 (the inline add row), § 5 (names),
the spec's § Notices.

```gherkin
Scenario Outline: Each row offers the children its type takes
  Then the row of <row> has <control>, offering <entries>

  Examples:
    | row         | control                                          | entries                                                  |
    | MSK-EPIC-1  | «Добавить: Фича (MSK-EPIC-1)»                    | the inline row for «Фича» at once                        |
    | MSK-FEAT-6  | «Добавить дочерний элемент (MSK-FEAT-6)», a menu | «Добавить: Пользовательская история», «Добавить: Баг»    |
    | MSK-US-2    | «Добавить дочерний элемент (MSK-US-2)», a menu   | «Добавить: Задача», «Добавить: Баг»                      |
    | MSK-BUG-4   | none                                             | — (D-6 OQ-6)                                             |
    | MSK-TASK-3  | none                                             | —                                                        |

Scenario: The row's «+» is pale grey and shows on hover, focus and selection
  Then the control is a small pale grey «+» (D-10: the reference's cross, a little darker; D-7 had said orange) in the hierarchy column
  And it is transparent at rest, shown when the pointer is over the row,
    when focus is inside the row, and when the row is selected
  And it stays in the Tab order while transparent

Scenario: Nested rows offer them too, on every level
  Given the «Эпики» backlog with MSK-EPIC-1 and MSK-FEAT-6 expanded
  Then MSK-FEAT-6 and MSK-US-2 have their «Добавить дочерний элемент»
    controls as on their own levels

Scenario: A story is added to a feature
  Given the «Фичи» backlog of «Платформа»
  When the user opens «Добавить дочерний элемент (MSK-FEAT-6)» and chooses
    «Добавить: Пользовательская история»
  Then an editable row stands directly beneath MSK-FEAT-6, one depth deeper,
    and MSK-FEAT-6 is expanded
  And its «Тип» reads «Пользовательская история», the type's icon stands
    before a focused field named «Заголовок: Пользовательская история
    (MSK-FEAT-6)» with the placeholder «Введите заголовок», and the row is
    drawn selected
  When the user types «Повторная отправка письма» and presses Enter
  Then the field is read-only with a progress indicator until the backlog is
    read again
  And the row closes and «Рабочий элемент MSK-US-24 создан.» is shown
  And MSK-FEAT-6 is expanded with MSK-US-24 beneath it, after MSK-US-2 and
    MSK-BUG-4, selected and in view
  And focus is back on «Добавить дочерний элемент (MSK-FEAT-6)»

Scenario: An epic gets a feature at once
  When the user activates «Добавить: Фича (MSK-EPIC-1)»
  Then the inline row for «Фича» opens beneath MSK-EPIC-1 with no menu first

Scenario: Escape or an empty title cancels
  Given an add row beneath MSK-FEAT-6 with «Повторная отправка» typed
  When the user presses Escape
  Then the row is gone, nothing was sent, and focus is back on the control
    that opened it
  When the user opens it again and presses Enter with nothing typed
  Then the row is gone and nothing was sent, with no error text
  When the user opens it again and leaves the field with nothing typed
  Then the row is gone and nothing was sent

Scenario: Leaving the field with a title typed keeps the row
  Given an add row with «Повторная отправка» typed
  When the user clicks elsewhere on the page
  Then the row stays open with its text, and nothing was sent
  When the user activates another row's «+» meanwhile
  Then no second row opens and focus goes to the open row's field

Scenario: The child takes the parent's area, state and defaults
  Given MSK-US-24 created as above
  Then its form shows «Родитель» MSK-FEAT-6, «Область» «Платформа / API»,
    «Статус» «Новая», «Исполнитель» «Не назначен», «Приоритет» 2
  And it is the last top-level row of «Платформа»'s «Истории» backlog
  And MSK-FEAT-6's «Изменён» time has moved

Scenario: A task is added to a nested story on «Эпики»
  Given the «Эпики» backlog with MSK-EPIC-1, MSK-FEAT-6 expanded
  When the user adds «Задача» «Проверить ссылку» to MSK-US-2
  Then MSK-US-2 is expanded and the new task stands last beneath it, after
    MSK-BUG-5 and MSK-TASK-3

Scenario: The type is recognised by its appearance, not its name
  Given the process names the type of appearance `feature` «Возможность»
  Then MSK-EPIC-1's control reads «Добавить: Возможность (MSK-EPIC-1)» and
    creates an item of that type

Scenario: The create carries its parent in one request
  When a child is created from a row
  Then one create request names the type, the title and the parent
  And no separate parent write is sent

Scenario: A double Enter creates one child
  When the user presses Enter twice, or it is sent again after a lost answer
  Then one item is created and linked, with one identifier, from one
    `requestId` made at the first Enter

Scenario: A parent gone meanwhile refuses the create, at the field
  Given MSK-FEAT-6's state is placed in the Removed category after the page
    was read
  When the user creates a story from MSK-FEAT-6's row
  Then «Удалённый рабочий элемент нельзя связывать.» shows under the field,
    marked invalid, the title kept, and nothing is created
  When the user presses Escape
  Then the backlog is read again and MSK-FEAT-6 is gone from it

Scenario: mod-003's title check holds
  When the user types a title of 256 characters and presses Enter
  Then «Не более 255 символов.» shows under the field and nothing is sent

Scenario: One write at a time
  Given a create from a row being saved
  Then every row is undraggable and every row «+», «Ещё действия» and both header buttons is off
    until the backlog is read again

Scenario: «Новый рабочий элемент» still creates a top-level item
  When the user creates a «Фича» through «Новый рабочий элемент» on «Фичи»
  Then it has no parent, the team's default area, and is the last top-level
    row
  And mod-003's dialog is what opened (the head's primary, unchanged)
```
