# US-2: MS-2 «Рабочие элементы с историей, комментариями, вложениями, корзиной»

- story: US-2
- depends-on: US-1
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-2 «Рабочие элементы с историей,
комментариями, вложениями, корзиной»**, so that **a team can carry a piece of
work end to end: create, describe, discuss, attach, link and move it through
its states. Nothing deleted by mistake is lost.**

Delivered by `mod-003-work-item-core`, `feat-001-work-item-links`,
`feat-002-work-item-comments`, `feat-007-work-item-type-fields`,
`mod-005-work-item-delete-restore`, `mod-004-work-item-attachments` and
`feat-003-work-item-search`.

## Acceptance criteria

```gherkin
Scenario: Demo — a story's life, from creation to the recycle bin and back
  Given MS-1's project «Пример проекта» with the key MSK
  And the organization's Google Drive connected by its administrator
  And «Анна Смирнова», a member
  When Анна creates a «Пользовательская история» «Оплата картой» from the
    project's work item list
  Then it gets the identifier MSK-US-1, the state «Новая» and the project's
    root area
  When she fills «Критерии приёмки», adds a child «Задача» «Форма оплаты»,
    posts a comment and then edits it, attaches a file and sets «Активна»
  Then the item's history lists each change with who and when
  And the comment is marked as edited, with the time of the edit
  When she types «MSK-US-1» into the list's keyword filter
  Then the story is found
  When she deletes the story and confirms
  Then it disappears from the list
  And the child «Задача» keeps its own state
  When she filters the list by «Удалена»
  Then exactly the deleted story is listed
  When she restores it and accepts the suggested state
  Then it is «Активна» again, with its criteria, comment, file and child link

Scenario: Guard — a deleted item is read-only, and only an administrator purges it
  Given the story «Оплата картой» deleted again
  When Анна opens it
  Then she cannot edit a field, post a comment, attach a file or change a link
    (BR-14)
  And only «Восстановить» is offered
  When the organization administrator opens it
  Then «Удалить навсегда» is offered as well
  When the administrator purges it and confirms
  Then MSK-US-1 is listed neither in the list nor under «Удалена»
  And the history of «Форма оплаты» records the purge of its parent MSK-US-1
  And the story's file is gone from the organization's Google Drive (D-13,
    doc-002 US-10)
```
