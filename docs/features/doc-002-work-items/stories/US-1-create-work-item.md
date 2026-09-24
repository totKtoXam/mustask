# US-1: A team member creates a work item of one of the five types

- story: US-1
- depends-on: -
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As a **team member**, I want **to create a work item of any of the five
built-in types with a single required field**, so that **capturing a piece of
work costs one sentence, and everything else can be filled in later**.

## Acceptance criteria

```gherkin
Scenario: The type picker offers exactly the five built-in types
  Given a team member with the "member" role in a project (doc-001 US-1)
  When they open «Создать рабочий элемент»
  Then the picker offers exactly «Эпик», «Фича», «Пользовательская история»,
    «Задача», «Баг» and nothing else (D-3, D-12)
  And no option to define a new type is offered anywhere on this screen
    (M2 ships fixed, release 3)

Scenario: A work item is created with only a title
  Given the create form for «Задача» in project «Проект А»
  When the team member enters a title and saves
  Then the work item exists in «Новая», Область = the project's root area,
    Приоритет = 2, no Исполнитель, no Теги, no Итерация (spec § Fields)
  And it carries an identifier that was not in use before (US-4)
  And revision 1 of its history names the author and the timestamp (BR-10)

Scenario: Each type is created in its own type's initial state
  Given the create form
  When the team member creates one work item of each of the five types
    without touching Статус
  Then «Эпик», «Фича», «Пользовательская история» and «Баг» are in «Новая»
  And «Задача» is in «Новая»
  And every one of them resolves to category Proposed (doc-001 BR-2)

Scenario: An empty or whitespace-only title is refused
  Given the create form with Заголовок left empty, or containing only spaces
  When the team member saves
  Then the save is refused with «Заголовок обязателен» shown on the field
  And no work item is created (BR-3)
  And whether the attempt consumed a number is not promised either way —
    gaps in the sequence are legal (BR-5, US-4)

Scenario: Submitting the create form twice creates one work item
  Given the create form filled in with a valid title
  When the team member triggers the save twice — a double click, or the same
    request sent again after a timeout
  Then exactly one work item exists afterwards, with one identifier and one
    revision 1 in its history
  And the duplicate submission is refused or ignored, never silently turned
    into a second item

Scenario: A title longer than the limit is refused by the server too
  Given a title of 256 characters after trimming
  When the save is attempted, including by calling the system directly
    without the browser's own check
  Then it is refused, and the message names the 255-character limit (BR-3)

Scenario: The type cannot be changed after creation
  Given an existing «Задача»
  When the team member opens its form
  Then the type is shown but is not editable, and no «Изменить тип» action
    exists (BR-1; release 2)

Scenario: Область must belong to the work item's own project
  Given the create form in project «Проект А»
  When the team member opens the Область picker
  Then only nodes of «Проект А»'s own area tree are offered (BR-2)
  And a node of another project is refused even if named directly

Scenario: A reader cannot create a work item
  Given a user signed in with only the "viewer" role in the organization
  When they open the work item list
  Then no «Создать рабочий элемент» control is offered
  And a create request made directly is refused by the server (doc-001 BR-5)

Scenario: A user outside the organization sees nothing to create in
  Given a user who belongs to organization «Орг Б» only
  When they try to create a work item in a project of «Орг А»
  Then the request is refused, and the project is not listed for them at all
```
