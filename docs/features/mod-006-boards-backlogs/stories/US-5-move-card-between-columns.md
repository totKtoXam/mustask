# US-5: A user moves a card between columns

- story: US-5
- depends-on: US-4
- shippable: no — the move acts on the board of US-4
- status: pending

As a **signed-in user**, I want **to move a card to another column, by mouse
or by keyboard**, so that **the item's state follows the work without
opening its form**.

## Acceptance criteria

Data: US-1's; the «Истории» board of «Платформа». Users «Анна Смирнова» and
«Борис Петров». Spec § Model (what a card move writes), BR-M4-3, -15, -18;
mod-003 BR-M3-21, BR-M3-25.

```gherkin
Scenario: A move writes the state its column maps for the card's type
  Given MSK-US-2 and MSK-BUG-5 in «Активна», in «В работе»
  When the user drags MSK-US-2 to «Разрешена» and MSK-BUG-5 to «Решённые»
  Then both cards show in their new columns at once
  And both items are in «Разрешена» on their forms
  And their «Заголовок», «Исполнитель» and «Область» are unchanged, and their
    «Изменён» time is the time of the move
  And both keep their «Порядок» on the backlog

Scenario: A column without a state for the type is no drop target
  When the user drags MSK-BUG-5 over «Разрешена», or MSK-US-2 over «Решённые»
  Then that column shows no drop area and a drop there changes nothing
  And a request sent directly to set MSK-BUG-5 to the «Разрешена» state of
    «Пользовательская история» is refused with «Этот статус недоступен для
    рабочего элемента этого типа.», MSK-BUG-5 unchanged

Scenario: The card's menu moves it by keyboard
  Given MSK-BUG-5 in «Новые»
  When the user activates «Ещё действия: MSK-BUG-5»
  Then a menu headed «Переместить в колонку» lists «В работе», «Решённые»,
    «Завершённые», and neither «Новые» nor «Разрешена»
  When the user chooses «Решённые»
  Then the card is in «Решённые» and MSK-BUG-5 is in «Разрешена»
  And focus is on «Ещё действия: MSK-BUG-5» in «Решённые»
  And «MSK-BUG-5 перемещён в колонку «Решённые».» is announced

Scenario: The card lands at its backlog place in the new column
  Given the backlog reads MSK-US-3, MSK-US-9, MSK-US-11, with MSK-US-3 and
    MSK-US-11 in «Активна» and MSK-US-9 in «Новая»
  When the user drops MSK-US-9 at the top of «В работе»
  Then «В работе» shows MSK-US-3, MSK-US-9, MSK-US-11

Scenario: Two users move the same card, the later decides
  Given Анна and Борис both see MSK-US-2 in «Новые»
  When Анна moves it to «Завершённые» and then Борис moves it to «В работе»
  Then MSK-US-2 is in «Активна», and neither of them is shown any notice

Scenario: A form opened before the move keeps the moved state
  Given Анна opened the form of MSK-US-2 while it was «Новая»
  And Борис then moved its card to «Разрешена»
  When Анна changes only «Заголовок» and saves
  Then MSK-US-2 is still in «Разрешена»
  When Анна then sets «Статус» to «Активна» and saves
  Then MSK-US-2 is in «Активна»

Scenario: A failed move puts the card back
  Given the server gives no answer to a move
  Then the card returns to its former column
  And «Не удалось сохранить перемещение. Попробуйте ещё раз.» is shown

Scenario: One move at a time
  Given a move being saved
  Then every «Ещё действия: …» button is disabled and no card can be dragged
  And once the answer arrives the board is read again
```
