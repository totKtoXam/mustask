# US-6: A team member deletes and restores a work item

- story: US-6
- depends-on: US-4
- shippable: no — delivered by mod-work-item-delete-restore
- status: pending

As a **team member**, I want **to delete a work item I no longer need and
restore it if that was a mistake**, so that **the backlog stays clean
without losing data (BR-6)**.

## Acceptance criteria

```gherkin
Scenario: A team member deletes a work item
  Given an existing work item in any non-Removed state, on a team's board
    and backlog
  When a team member deletes it
  Then its state becomes «Удалена» — this IS the recycle bin, not a second
    place (BR-6)
  And it disappears from every board, backlog and filter it was on
  And the deletion is recorded in history with who and when (BR-4)

Scenario: A team member restores a deleted work item
  Given a work item in state «Удалена», previously in «Выполняется»
  When a team member restores it, accepting the suggested state
  Then its state returns to «Выполняется» — the state it held immediately
    before deletion (BR-10)
  And it reappears on its team's board and backlog

Scenario: Restoring to a different state is allowed
  Given a work item in state «Удалена»
  When a team member restores it, choosing a different state than the
    suggested one
  Then its state becomes the one chosen (BR-10: any non-Removed state is a
    legal target)

Scenario: Retention is indefinite; only an administrator purges by hand
  Given a work item that has been in state «Удалена» for a long time
  When nobody acts on it
  Then it stays recoverable indefinitely (BR-6)
  And only an administrator's explicit "purge permanently" action removes
    it for good — a separate action from Delete, not a side effect of time
    passing

Scenario: A deleted item leaves a board another user has open
  Given a work item card visible on a team's board, open in another user's
    browser
  When a team member deletes that work item
  Then the delete succeeds regardless of the other user's open view
  And the item is gone from the board the next time that view reads data
    (MUSTASK does not promise a live push in MVP)

Scenario: A reader cannot delete or restore
  Given a user signed in with only the "viewer" role
  When they attempt to delete a work item, or to restore one already
    deleted
  Then the action is refused
```
