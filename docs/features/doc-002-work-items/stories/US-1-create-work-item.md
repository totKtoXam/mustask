# US-1: A member creates a work item of one of the five types

- story: US-1
- depends-on: -
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As an **organization member**, I want **to create a work item of any of the five
built-in types with a single required field**, so that **capturing a piece of
work costs one sentence, and everything else can be filled in later**.

## Acceptance criteria

Creating from a team's board or backlog, with that team's default area, is
doc-003 US-4; this story creates from the project's work item list.

```gherkin
Scenario: The type picker offers exactly the five built-in types
  Given a user whose role in the project's organization is member
  When they open «Создать рабочий элемент»
  Then the picker offers exactly «Эпик», «Фича», «Пользовательская история»,
    «Задача», «Баг» and nothing else (D-3, D-12)
  And no option to define a new type is offered anywhere on this screen
    (M2 ships fixed, release 3)

Scenario: A work item is created with only a title
  Given the create form for «Задача» in project «Проект А», opened from its
    work item list
  When the member enters a title and saves
  Then the work item exists in «Новая», Область = the project's root area,
    Приоритет = 2, no Исполнитель, no Теги, no Итерация (spec § Fields)
  And it carries an identifier that was not in use before (US-4)
  And revision 1 of its history names the author and the timestamp
    (doc-001 BR-4)

Scenario: Each type is created in its own type's first state
  Given the create form
  When the member creates one work item of each of the five types
  Then Статус is shown on the form read-only, and cannot be set before the
    first save (spec § Fields)
  And every one of the five is created in «Новая», category Proposed
    (doc-007 BR-M2-2, doc-001 BR-2)

Scenario: An empty or whitespace-only title is refused
  Given the create form with Заголовок left empty, or containing only spaces
  When the member saves
  Then the save is refused with «Заголовок обязателен» shown on the field
  And no work item is created (BR-M3-3)
  And whether the attempt consumed a number is not promised either way —
    gaps in the sequence are legal (BR-M3-5, US-4)

Scenario: Submitting the create form twice creates one work item
  Given the create form filled in with a valid title
  When the member triggers the save twice — a double click, or the same
    request sent again after a timeout
  Then exactly one work item exists afterwards, with one identifier and one
    revision 1 in its history
  And the duplicate submission is refused or ignored, never silently turned
    into a second item

Scenario: A title longer than the limit is refused by the server too
  Given a title of 256 characters after trimming
  When the save is attempted, including by calling the system directly
    without the browser's own check
  Then it is refused, and the message names the 255-character limit
    (BR-M3-3)

Scenario: The type cannot be changed after creation
  Given an existing «Задача»
  When the member opens its form
  Then the type is shown but is not editable, and no «Изменить тип» action
    exists (BR-M3-1; release 2)

Scenario: Область must belong to the work item's own project
  Given the create form in project «Проект А»
  When the member opens the Область picker
  Then only nodes of «Проект А»'s own area tree are offered (BR-M3-2)
  And a node of another project is refused even if named directly

Scenario: A reader cannot create a work item
  Given a user whose role in the organization is viewer
  When they open the work item list
  Then no «Создать рабочий элемент» control is offered
  And a create request made directly is refused by the server (doc-001 BR-5)

Scenario: A user outside the organization sees nothing to create in
  Given a user who belongs to organization «Орг Б» only
  When they try to create a work item in a project of «Орг А»
  Then the answer is "not found", never "forbidden", and the project is not
    listed for them at all (doc-001 BR-5)
```
