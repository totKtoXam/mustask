# US-6: A member deletes and restores a work item

- story: US-6
- depends-on: US-4
- shippable: no — delivered by mod-005-work-item-delete-restore; the refused move of a deleted card by mod-006-boards-backlogs
- status: pending

As a **user whose role in the organization is member**, I want **to delete a
work item I no longer need and restore it if that was a mistake**, so that
**the backlog stays clean without losing data (BR-6)**.

## Acceptance criteria

```gherkin
Scenario: A member deletes a work item
  Given an existing work item in any non-Removed state, on a team's board
    and backlog
  When a member deletes it («Удалить»)
  Then its state becomes «Удалена» — this IS the recycle bin, not a second
    place (BR-6)
  And it disappears from every list, board, backlog and filter it was on,
    except a filter that names «Удалена» (BR-14)
  And the deletion is recorded in history with who and when (BR-4)

Scenario: A member restores a deleted work item
  Given a work item in state «Удалена», previously in «Активна»
  When a member restores it («Восстановить»), accepting the suggested state
  Then its state returns to «Активна» — the state it held immediately
    before deletion (BR-10)
  And it reappears on its team's board and backlog

Scenario: Restoring to a different state is allowed
  Given a work item in state «Удалена»
  When a member restores it, choosing a different state than the
    suggested one
  Then its state becomes the one chosen (BR-10: any non-Removed state is a
    legal target)

Scenario: Retention is indefinite; only the organization administrator purges
  Given a work item that has been in state «Удалена» for a long time
  When nobody acts on it
  Then it stays recoverable indefinitely (BR-6)
  And only the organization administrator's explicit «Удалить навсегда»
    removes it for good — a separate action from Delete, not a side effect
    of time passing

Scenario: A deleted item leaves a board another user has open
  Given a work item card visible on a team's board, open in another user's
    browser
  When a member deletes that work item
  Then the delete succeeds regardless of the other user's open view
  And the item is gone from the board the next time that view reads data
    (MUSTASK does not promise a live push in MVP)

Scenario: A stale card of a deleted item cannot be moved
  Given the other user's board from the previous scenario, still showing
    the deleted item's card
  When they drag that card to another column, or reorder it
  Then the move is refused, the item stays in «Удалена», and their view
    refreshes (BR-10, BR-14)

Scenario: A reader cannot delete or restore
  Given a user whose role in the organization is viewer
  When they attempt to delete a work item, or to restore one already
    deleted
  Then the action is refused
```
