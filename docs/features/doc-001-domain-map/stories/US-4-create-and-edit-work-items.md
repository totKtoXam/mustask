# US-4: A team member creates and edits work items

- story: US-4
- depends-on: US-1, US-2, US-3
- shippable: no — delivered by mod-work-item-core, feat-work-item-links, mod-work-item-attachments
- status: pending

As a **team member**, I want **to create and edit work items of the five
built-in types, link them and attach files**, so that **I can capture and
update my team's work at every level**.

## Acceptance criteria

Common MVP fields and their rules (per type; per-type extra fields are
doc-002's job):

| Field | Required | Default |
|---|---|---|
| Заголовок | yes | — |
| Описание | no | empty |
| Область | yes | the project's root area |
| Исполнитель | no | unassigned |
| Приоритет | no | 2 (of 1–4) |
| Теги | no | none |

```gherkin
Scenario: A team member creates a work item with the defaults applied
  Given a team member signed in with the "member" role on a project (US-1)
  When they create a «Задача» with only a title
  Then the work item exists in state «Новая», Область = the project's root
    area, Приоритет = 2, no Исполнитель, no Теги
  And the creation is recorded in the work item's history with the author
    and the timestamp (BR-4)

Scenario: A team member edits a work item's fields
  Given an existing work item
  When the team member changes its Описание, Приоритет and Теги
  Then the fields update
  And a new history entry records who changed what, and when (BR-4)

Scenario: The Assigned To candidate set is the project's organization
  Given a work item in a project belonging to organization "Орг А"
  When a team member opens the Исполнитель picker
  Then every member of "Орг А" is a candidate, whether or not they belong to
    the project's team (BR-13)
  And a user who belongs only to a different organization is not offered

Scenario: Two members edit the same work item at once
  Given two team members who opened the same work item at the same revision
  When both save a change to it
  Then the first save succeeds
  And the second is rejected as a conflict (BR-11, optimistic concurrency),
    with the author asked to reload and reapply — the first save is never
    silently overwritten

Scenario: A team member links two work items as parent/child
  Given two existing work items in the same project
  When the team member sets one as the parent of the other
  Then the link appears on both work items
  And removing the link is a separate action, not an edit of the link
    itself (Operation completeness: WorkItemLink)

Scenario: A team member attaches a file to a work item
  Given an existing work item
  When the team member attaches a file to it
  Then the attachment appears on the work item and can be downloaded
  And the attach is recorded in history with who and when (BR-4)
  And replacing it means deleting the old attachment and adding a new one,
    never editing it in place (Operation completeness: Attachment)

Scenario: A reader cannot create, edit, link or attach
  Given a user signed in with only the "viewer" role
  When they attempt to create a work item, edit an existing one, link two
    items, or attach a file
  Then every action is refused
  And the work item (if it existed) is unchanged
```
