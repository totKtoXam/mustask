# US-12: A story carries acceptance criteria, a bug carries repro steps

- story: US-12
- depends-on: US-2
- shippable: no — analysis only; delivered by `feat-NNN-work-item-type-fields`
- status: pending

As a **team member**, I want **a user story to have its own «Критерии
приёмки» field and a bug its own «Шаги воспроизведения» field**, so that
**what "done" means and how to reproduce a defect do not get lost inside
Описание**.

## Acceptance criteria

Both fields are rich text under the same rules as Описание — the safe subset
of BR-4 and ≤ 100 000 characters — and belong to the form's field set, so
they are saved, history-recorded and concurrency-guarded with it (BR-10,
BR-15). They are the only type-specific fields in MVP (W-5; spec § Fields).

```gherkin
Scenario: Each field appears only on its own type
  Given one work item of each of the five types
  When a team member opens each form
  Then «Критерии приёмки» is shown on the «Пользовательская история» only
  And «Шаги воспроизведения» is shown on the «Баг» only
  And «Эпик», «Фича» and «Задача» show neither field

Scenario: The field is optional and starts empty
  Given the create form of a «Пользовательская история», or of a «Баг»
  When the team member saves it with only a title (US-1)
  Then the work item is created with its type-specific field empty

Scenario: Editing the field is part of the one save
  Given a «Баг» opened by a team member
  When they fill in «Шаги воспроизведения» and change Приоритет, then save
    once
  Then both changes are stored
  And the history gains exactly one revision listing both fields with their
    previous and new values (BR-10)

Scenario: Rich text keeps formatting but drops anything executable
  Given «Критерии приёмки» containing a numbered list, bold text, a table,
    a <script> tag and an onclick attribute
  When the team member saves
  Then the formatting is preserved on reopening
  And the script tag and the event attribute are gone (BR-4)

Scenario: The length limit is enforced by the server
  Given «Шаги воспроизведения» of 100 001 characters
  When the save is attempted, including by calling the system directly
  Then it is refused, and the message names the 100 000-character limit

Scenario: The field cannot be written onto another type
  Given a «Задача»
  When a request sets «Критерии приёмки» or «Шаги воспроизведения» on it
    directly, bypassing the form
  Then the request is refused and the item is unchanged

Scenario: A concurrent edit of the field is a conflict
  Given two team members who opened the same «Пользовательская история» at
    the same revision
  When both change «Критерии приёмки» and save
  Then the second save is refused with «Элемент изменён другим
    пользователем» (BR-15)

Scenario: Items created before the slice ships
  Given a «Баг» created before `feat-NNN-work-item-type-fields` shipped
  When a team member opens it afterwards
  Then «Шаги воспроизведения» is shown, empty, and can be filled in

Scenario: The keyword filter does not search the field
  Given a «Баг» whose «Шаги воспроизведения» alone contains «таймаут»
  When a team member types «таймаут» in the list's keyword filter (US-11)
  Then the bug is not found — full text is release 2/3 (spec § Out of scope)

Scenario: A deleted item and a reader see the field read-only
  Given a «Пользовательская история» in «Удалена», or any user with only the
    "viewer" role
  When they open the form
  Then «Критерии приёмки» is shown read-only with its content (BR-14,
    doc-001 BR-5)
  And a write sent directly to the server is refused
```
