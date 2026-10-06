# US-2: A user reorders a team's backlog

- story: US-2
- depends-on: US-1
- shippable: no — reordering acts on the backlog of US-1
- status: pending

As a **signed-in user**, I want **to move a work item up or down my team's
backlog, by mouse or by keyboard**, so that **the order says what the team
does next**.

## Acceptance criteria

Data: US-1's. The «Истории» backlog of «Платформа» reads MSK-US-3,
MSK-BUG-5, MSK-US-9, MSK-US-11. Users «Анна Смирнова» and «Борис Петров».
Spec § Model (the stack rank), BR-M4-8, -24, -25, -26.

```gherkin
Scenario: A drag between two rows persists
  When the user drags MSK-US-11 by its handle and drops it between MSK-US-3
    and MSK-BUG-5
  Then the rows read MSK-US-3, MSK-US-11, MSK-BUG-5, MSK-US-9 at once, with
    «Порядок» 1 to 4
  And they read the same after a reload

Scenario: A drop at either end
  When the user drops MSK-US-9 above the first row, then below the last row
  Then it is first, then last

Scenario Outline: The handle's menu moves a row by keyboard
  Given focus on «Переместить MSK-BUG-5», the second row
  When the user presses Enter and chooses <entry>
  Then the rows read <order>
  And focus is on «Переместить MSK-BUG-5»
  And «MSK-BUG-5 перемещён: позиция <n> из 4.» is announced

  Examples:
    | entry      | order                                   | n |
    | «В начало» | MSK-BUG-5, MSK-US-3, MSK-US-9, MSK-US-11 | 1 |
    | «Выше»     | MSK-BUG-5, MSK-US-3, MSK-US-9, MSK-US-11 | 1 |
    | «Ниже»     | MSK-US-3, MSK-US-9, MSK-BUG-5, MSK-US-11 | 3 |
    | «В конец»  | MSK-US-3, MSK-US-9, MSK-US-11, MSK-BUG-5 | 4 |

Scenario: Entries that would not move a row are disabled
  When the user opens the handle's menu on the first row, then on the last
  Then «В начало» and «Выше» are disabled on the first, «Ниже» and «В конец»
    on the last

Scenario: Reordering changes nothing on the work item
  When the user moves MSK-US-11 to the top
  Then its «Изменён» time and its place in «Рабочие элементы» are unchanged

Scenario: Two users reorder different items, both moves count
  Given Анна and Борис both see MSK-US-3, MSK-BUG-5, MSK-US-9, MSK-US-11
  When Анна moves MSK-US-11 to the top
  And then Борис, whose page still shows the old order, drops MSK-US-3
    directly below MSK-US-9
  Then the backlog reads MSK-US-11, MSK-BUG-5, MSK-US-9, MSK-US-3
  And neither of them is shown any notice
  And Анна sees that order once her page is read again

Scenario: Two users move the same item, the later decides
  When Анна moves MSK-US-9 to the top and then Борис moves MSK-US-9 to the
    bottom
  Then MSK-US-9 is last and the other rows keep their order, with no notice

Scenario: A drop next to an item that has left is refused
  Given Борис's page still shows MSK-BUG-5, whose Область has since been set
    to «Мастаск»
  When Борис drops MSK-US-11 directly below MSK-BUG-5
  Then no place changes
  And the backlog is read again, without MSK-BUG-5, and «Не удалось
    переместить: бэклог изменился. Список обновлён — повторите
    перемещение.» is shown
  And the same happens when the moved item itself has left the backlog

Scenario: A failed save puts the row back
  Given the server gives no answer to a reorder
  Then the row returns to its former place
  And «Не удалось сохранить перемещение. Попробуйте ещё раз.» is shown

Scenario: One move at a time
  Given a reorder being saved
  Then every «Переместить …» handle is disabled and no row can be dragged,
    until the answer arrives and the backlog is read again
```
