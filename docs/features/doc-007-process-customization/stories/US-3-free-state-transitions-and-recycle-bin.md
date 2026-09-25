# US-3: A member moves a work item freely among non-Removed states

- story: US-3
- depends-on: US-1
- shippable: no — delivered by mod-003-work-item-core (state transitions)
  and mod-005-work-item-delete-restore (delete/restore to «Удалена»);
  mod-001-process-seed only defines the states/categories these enforce (R-7)
- status: pending

As a **user whose role in the item's organization is member**, I want **to
move a work item between any two active states without being asked for a
reason, and reach «Удалена» only by deleting it**, so that **everyday status
changes stay quick and the recycle bin stays a single, predictable
mechanism**.

## Acceptance criteria

```gherkin
Scenario: Any non-Removed state can move to any other with no reason
  Given a work item of any type in a non-Removed state
  When a member sets its state directly to any other non-Removed state,
    including a "backward" one (e.g. «Закрыта» → «Новая»)
  Then the transition succeeds immediately with no reason required
    (doc-001 BR-10, D-6)

Scenario: «Удалена» is never offered by the ordinary state control
  Given a work item in any non-Removed state
  When a member opens the state field to pick a new value
  Then «Удалена» is not among the offered choices

Scenario: «Удалена» is reached only through Delete
  Given a work item in any non-Removed state
  When a member deletes it (doc-001 US-6)
  Then its state becomes «Удалена»
  And a direct state change to «Удалена» through the ordinary state field is
    refused (doc-001 BR-10)

Scenario: Restoring returns the item to a working state
  Given a work item currently in «Удалена», previously in «Разрешена»
  When a member restores it
  Then its state returns by default to «Разрешена», the one it held
    immediately before deletion
  And the restorer may pick a different non-Removed state instead — the
    previous state is the default, not a requirement (doc-001 BR-6/BR-10)
```
