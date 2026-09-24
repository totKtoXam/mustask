# US-10: A team member deletes, restores and purges a work item

- story: US-10
- depends-on: US-1, US-3
- shippable: no — analysis only; delivered by `mod-005-work-item-delete-restore`
- status: pending

As a **team member**, I want **to delete a work item I no longer need and get
it back if that was a mistake**, so that **the backlog stays clean and
nothing is lost by accident (doc-001 BR-6)**.

## Acceptance criteria

```gherkin
Scenario: Deleting moves the item into «Удалена»
  Given a work item in a non-Removed state, visible in the list and on its
    team's board and backlog
  When a team member deletes it and confirms
  Then its Статус becomes «Удалена» — that state is the recycle bin itself,
    there is no second place and no «Корзина» screen (D-5)
  And it disappears from the list's default view, from every board, backlog
    and filter
  And the deletion is recorded in history with who and when (BR-10)

Scenario: Everything the item carries survives the deletion
  Given a work item with a parent, children, comments, attachments and tags
  When it is deleted
  Then all of them are still attached to it
  And a later restore brings the item back exactly as it was (BR-9)

Scenario: Children are not deleted with their parent
  Given an «Эпик» with three children in various states
  When the epic is deleted
  Then the three children keep their own states and stay on their boards
  And each keeps its parent link, and is treated as unparented by backlog
    and hierarchy views while the parent is deleted (BR-9)

Scenario: A deleted work item is read-only
  Given a work item in «Удалена»
  When a team member opens it
  Then no field can be edited, no comment posted, no attachment uploaded, no
    link or tag changed (BR-14)
  And only «Восстановить» is offered — plus «Удалить навсегда» for an
    organization administrator

Scenario: Restoring returns the previous state by default
  Given a work item that was in «Активна» when it was deleted
  When a team member restores it and accepts the suggested state
  Then its Статус is «Активна» again (doc-001 BR-10)
  And it reappears in the list and on its team's board and backlog
  And the restore is recorded in history (BR-10)

Scenario: Restoring to a different state is allowed
  Given a work item in «Удалена»
  When a team member restores it and picks another non-Removed state of its
    type
  Then the item takes the chosen state — any non-Removed state is a legal
    target (doc-001 BR-10)

Scenario: Retention is indefinite
  Given a work item that has been in «Удалена» for a long time
  When nobody acts on it
  Then it is still restorable — nothing expires it and no job cleans it up
    (doc-001 BR-6)

Scenario: An administrator purges permanently
  Given a work item in «Удалена»
  When an organization administrator chooses «Удалить навсегда» and confirms
    against the item's identifier shown in the dialog
  Then the work item, its comments, attachments, tags, links and history are
    gone for good — the attachment files in the organization's Google Drive
    included (BR-16)
  And a purge never ends with the item gone while its files remain in Drive
  And its number is never issued again in the project, under any type key
    (BR-5, US-4)
  And the operation is not undoable, which the confirmation says plainly

Scenario: A purge that cannot reach the Drive is refused
  Given a work item in «Удалена» that has attachments, in an organization
    whose Drive is disconnected (US-13), unavailable or no longer accessible
  When an organization administrator tries to purge it
  Then the purge is refused with a message saying the organization's file
    storage must be reachable to delete the item's files
  And the item stays in «Удалена», unchanged and still restorable (BR-17)
  And a work item with no attachments is purged regardless of the Drive
  And if the Drive is never coming back, the way out is to restore the item,
    remove its attachments — which never waits on the Drive (BR-17) — and
    delete and purge it again

Scenario: Only an administrator may purge
  Given a work item in «Удалена»
  When a user with the "member" role opens it
  Then «Удалить навсегда» is not offered
  And a purge sent directly to the server is refused

Scenario: A purged parent leaves its children consistent
  Given an «Эпик» in «Удалена» with children
  When an administrator purges it
  Then the children survive and are simply left without a parent link
  And nothing in the interface still points at the purged identifier

Scenario: Deleting an item someone else has open
  Given a work item open in another user's browser
  When a team member deletes it
  Then the delete succeeds regardless of the other session
  And the other user sees it gone the next time their view reads data —
    MUSTASK promises no live push in MVP

Scenario: A reader can neither delete nor restore
  Given a user with only the "viewer" role
  When they open any work item, deleted or not
  Then no delete, restore or purge control is offered
  And such a request sent directly to the server is refused
```
