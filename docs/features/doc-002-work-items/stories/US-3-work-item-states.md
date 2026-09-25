# US-3: A member moves a work item through its states

- story: US-3
- depends-on: US-1
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As an **organization member**, I want **to set a work item's status to whatever
reflects reality, forwards or backwards**, so that **the board and the
backlog show where the work actually stands without a workflow arguing with
me**.

## Acceptance criteria

The per-type states, their order and their categories are doc-007's seed
table (spec § Types, levels and states). One label set serves all five types
(W-7): «Новая», «Активна», «Закрыта» and «Удалена» everywhere, «Разрешена»
on every type except «Задача» (W-2, W-9).

«Разрешена» is the one label with two categories — InProgress on «Эпик»,
«Фича» and «Пользовательская история», Resolved on «Баг». Consequences
owned elsewhere: the Истории board maps columns per (type, state), so a
История in «Разрешена» has its own column and a Баг in «Разрешена» sits in
the «Решённые» column (doc-003 BR-M4-2); a filter or clause on «Разрешена»
spans both categories (US-11; doc-005 BR-M6-5).

```gherkin
Scenario: The status control offers only this type's states
  Given a «Задача» in «Новая»
  When the member opens Статус
  Then exactly «Активна» and «Закрыта» are offered besides the current state
  And «Разрешена» — a state of the other four types — is not offered

Scenario: Every type uses the same labels for the same states
  Given one work item of each of the five types
  When the member opens Статус on each
  Then «Новая», «Активна», «Закрыта» are spelled identically on all five,
    whatever the type's grammatical gender (W-7)
  And «Разрешена» is offered on «Эпик», «Фича», «Пользовательская история»
    and «Баг» (W-9), and on «Задача» never

Scenario: A backward transition needs no reason
  Given a «Пользовательская история» in «Закрыта»
  When the member sets Статус to «Новая»
  Then the change is saved immediately
  And no reason is asked for and no rule blocks it (doc-001 BR-10, D-6)
  And the history records the transition with who and when (doc-001 BR-4)

Scenario: «Удалена» is never offered by the status control
  Given a work item of any type in any non-Removed state
  When the member opens Статус
  Then «Удалена» is not among the options (doc-001 BR-10)
  And the only way into it is the «Удалить» action (US-10)

Scenario: Every state resolves to exactly one category
  Given the five built-in types
  When any module reads a work item's state category
  Then «Новая» is Proposed, «Активна» is InProgress, «Закрыта» is
    Completed and «Удалена» is Removed, on every type
  And «Разрешена» is InProgress on «Эпик», «Фича» and «Пользовательская
    история», and Resolved on «Баг» (doc-001 BR-2)

Scenario: A state change on the form is revision-checked
  Given a work item opened by two members at the same revision
  When both change Статус on the form and save
  Then the second save is refused as a conflict, exactly as for any other
    field (doc-001 BR-11)
  And a card move on a board is the one state write without that check —
    it is last write wins and advances the revision (doc-001 BR-11, doc-003)

Scenario: Changing the state does not touch the assignee
  Given a work item assigned to a user, in «Активна»
  When the member sets Статус to «Закрыта»
  Then Исполнитель is unchanged — MVP has no rule that reassigns or clears
    a field on a transition (D-6; the reference product's Agile rules are
    release 3)

Scenario: A reader cannot change the state
  Given a user whose role in the organization is viewer
  When they open a work item
  Then Статус is shown as read-only text with no control to change it
  And a state change sent directly to the server is refused
```
