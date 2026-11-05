# US-2: A user closes or reopens a child from its checkbox

- story: US-2
- depends-on: US-1
- shippable: no — the checkboxes are US-1's rows
- status: pending

As a **signed-in user**, I want **to tick a task or a bug in a card's checklist
to close it, and untick it to reopen it**, so that **I keep the work's state
current from the board, without opening each child**.

## Acceptance criteria

Data: US-1's. Spec BR-C-3, -7, -8, -12 and the BPMN. The seeded process: a
task's and a bug's Completed state «Закрыта», initial state «Новая». The
announcement is made at once with the tick, in the form «<ID>: статус «<состояние>».».

```gherkin
Scenario: Ticking closes the child
  Given the task list of MSK-US-2 is open, MSK-TASK-4 in «Новая»
  When the user ticks MSK-TASK-4's checkbox
  Then the row shows it checked and the counter reads «2/3» at once
  And «MSK-TASK-4: статус «Закрыта».» is announced at once
  And after the board is read again MSK-TASK-4 is in «Закрыта» on its form
  And its «Изменён» time moved, its title, assignee and Область are unchanged

Scenario: Unticking reopens the child in its initial state
  Given MSK-TASK-3 in «Закрыта»
  When the user unticks its checkbox
  Then the counter reads «0/3», «MSK-TASK-3: статус «Новая».» is announced
  And MSK-TASK-3 is in «Новая» on its form

Scenario: A bug is closed the same way
  Given the bug list of MSK-US-2 is open, MSK-BUG-8 in «Разрешена»
  When the user ticks its checkbox
  Then MSK-BUG-8 is in «Закрыта» and the bug counter reads «2/2»

Scenario: A ticked then unticked «Разрешена» bug ends in «Новая»
  Given MSK-BUG-8 in «Разрешена»
  When the user ticks and then unticks it
  Then MSK-BUG-8 is in «Новая»

Scenario: A closed child that is also a card moves column at the read
  Given MSK-BUG-8 is also a card in «Разрешена» on the «Истории» board
  When the user ticks MSK-BUG-8 in MSK-US-2's list
  Then its own card is still in «Разрешена» at once
  And after the board is read again it is in «Закрыта»

Scenario: The checkbox can be used by keyboard
  When the user tabs to MSK-TASK-4's checkbox and presses Space
  Then it is ticked as by a click, and focus stays on it after the read

Scenario: A refused write puts the row back
  Given the server refuses the change with a code
  Then the checkbox and the counter return to what they were
  And the server's text is shown as an error notice
  And the board is read again

Scenario: No answer puts the row back
  Given the server gives no answer
  Then the checkbox and the counter return, and «Не удалось сохранить статус.
    Попробуйте ещё раз.» is shown

Scenario: A child removed since the read is deferred to mod-005
  Given no item can be in «Удалена» before mod-005
  Then what a tick on a child removed since the read does is mod-005's (its
    delete and restore rules); this task adds no check

Scenario: A child whose type has no Completed state cannot be ticked
  Given a process where a child's type has no Completed state
  Then that row's checkbox is disabled

Scenario: One write at a time
  Given a tick being saved and an add field open with a title typed
  Then every checkbox, every «Ещё действия: …», every «Добавить задачу» is off
    and no card can be dragged
  And the add field keeps its text but its Enter does nothing
  And once the answer arrives the board is read again and they work

Scenario: The later of two writes decides
  Given Борис's board, not read again since, shows MSK-TASK-3 in «Закрыта»
  When Анна sets MSK-TASK-3 to «Активна» on its form
  And Борис unticks it on his board
  Then MSK-TASK-3 is in «Новая», Анна's «Активна» is overwritten, and neither
    is shown any notice

Scenario: A test case has no checkbox to use
  Then no tick or untick of a test case is possible from a card
```
