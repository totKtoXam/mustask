# US-4: A member creates and edits work items

- story: US-4
- depends-on: US-1, US-2, US-3
- shippable: no — delivered by mod-003-work-item-core, feat-001-work-item-links, mod-004-work-item-attachments; creating from a team's board or backlog by mod-006-boards-backlogs
- status: pending

As a **user whose role in the organization is member**, I want **to create
and edit work items of the five built-in types, link them and attach
files**, so that **I can capture and update the work at every level**.

## Acceptance criteria

The field catalogue is doc-002 § Fields; the defaults this story relies on:

| Field | Required | Default |
|---|---|---|
| Заголовок | yes | — |
| Описание | no | empty |
| Статус | yes | the type's initial state, read-only at create (doc-007 BR-M2-2) |
| Область | yes | created from a team's board or backlog: that team's default area; otherwise the project's root area (doc-006 BR-M1-14) |
| Исполнитель | no | unassigned |
| Приоритет | no | 2 (of 1–4) |
| Теги | no | none |

```gherkin
Scenario: A member creates a work item with the defaults applied
  Given a user whose role in the organization of project `PRM` is member
  When they create a «Задача» with only a title from the project's work item
    list
  Then the work item exists in state «Новая», Область = the project's root
    area, Приоритет = 2, no Исполнитель, no Теги
  And its identifier has the form `PRM-TASK-<number>`
  And the creation is recorded in the work item's history with the author
    and the timestamp (BR-4)

Scenario: A work item created from a team's backlog lands on that team
  Given a project with a second team «Платформа», whose default area is
    «Платформа» (US-2)
  When a member creates a «Пользовательская история» from «Платформа»'s
    Истории backlog
  Then its Область is «Платформа» and it appears on «Платформа»'s backlog,
    not on the first team's (BR-3, doc-010 D-7)

Scenario: A member edits a work item's fields
  Given an existing work item
  When the member changes its Описание, Приоритет and Теги
  Then the fields update
  And one new history revision records who changed what, and when (BR-4)

Scenario: The Assigned To candidate set is the project's organization
  Given a work item in a project belonging to organization «Орг А»
  When a member opens the Исполнитель picker
  Then every active member of «Орг А» is a candidate, whether or not they
    belong to the team that owns the item's Область (BR-13)
  And a user who belongs only to a different organization, or whose account
    is inactive, is not offered

Scenario: Two members edit the same work item at once
  Given two members who opened the same work item at the same revision
  When both save a change to it
  Then the first save succeeds
  And the second is rejected as a conflict (BR-11, form save), with the
    author asked to reload and reapply — the first save is never silently
    overwritten

Scenario: A member links two work items as parent/child
  Given two existing work items in the same project
  When the member sets one as the parent of the other
  Then the link appears on both work items
  And removing the link is a separate action, not an edit of the link
    itself (Operation completeness: WorkItemLink)

Scenario: A member attaches a file to a work item
  Given an existing work item in an organization with a connected Google
    Drive
  When the member attaches a file to it
  Then the attachment appears on the work item and can be downloaded
  And the attach is recorded in history with who and when (BR-4)
  And replacing it means removing the old attachment and adding a new one,
    never editing it in place (Operation completeness: Attachment)

Scenario: A reader cannot create, edit, link or attach
  Given a user whose role in the organization is viewer
  When they attempt to create a work item, edit an existing one, link two
    items, or attach a file
  Then every action is refused
  And the work item (if it existed) is unchanged
```
