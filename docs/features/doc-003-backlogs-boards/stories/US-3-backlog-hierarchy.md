# US-3: A team member navigates the parent/child hierarchy on the backlog

- story: US-3
- depends-on: US-2
- shippable: no — delivered by `feat-NNN-backlog-hierarchy` (DD-3), not `mod-boards-backlogs`
- status: pending

As a **team member**, I want **to see and set the parent/child hierarchy
while looking at my team's backlog**, so that **I can plan child items under
their parent without leaving the list**.

## Acceptance criteria

```gherkin
Scenario: Expanding a parent row reveals its children
  Given a backlog entry with one or more Parent/Child links to entries one
    level below (e.g. a Feature with linked Stories)
  When a team member expands that row
  Then its linked children appear nested beneath it
  And collapsing the row hides them again, with no change to any entry's
    stack rank (BR-9)

Scenario: Assigning a parent from the backlog view links the item
  Given two backlog entries, a prospective parent one level up and a child
    with no parent yet
  When a team member uses the mapping action to assign the parent to the
    child
  Then the same Parent/Child link a team member could set from the work
    item's own form now exists between them
  And no second, board/backlog-only link is created (BR-10)

Scenario: Assigning a different parent replaces the existing link
  Given a backlog entry that already has a parent one level up
  When a team member uses the mapping action to assign a different parent
    one level up
  Then the entry has exactly one Parent/Child link, to the new parent
  And the link to the previous parent is gone — no second parent and no
    duplicate link is created (BR-10)

Scenario: Clearing a parent removes the link
  Given a backlog entry that already has a parent assigned
  When a team member clears its parent from the backlog view
  Then the Parent/Child link is removed
  And the entry no longer appears beneath its former parent when that
    parent's row is expanded (BR-11)
  And on its own level's backlog it stays a top-level row at its unchanged
    stack-rank position

Scenario: An item with no parent is shown at the top level
  Given a backlog entry with no Parent/Child link
  When a team member opens that team's backlog
  Then the entry is shown at the top level, ungrouped
  And this is the normal state, not an error or a warning

Scenario: A child's linked parent is not one level above it
  Given a backlog entry whose Parent/Child link points to an item more than
    one level above it (e.g. a Story linked directly to an Epic, skipping
    Features) — reachable in MVP without a second team (BR-13)
  When a team member opens the Эпики backlog and expands that Epic's row
  Then the Story is not shown beneath it — only children exactly one level
    below appear there (BR-11)
  And on the Истории backlog the Story is an ordinary top-level row, with no
    error or warning

Scenario: A reader can expand and collapse but not assign a parent
  Given a user signed in with only the "viewer" role
  When they expand or collapse a row with children
  Then the hierarchy display changes for them as normal
  And the backlog offers them no mapping action — no "assign parent" and no
    "clear parent" control
  And such a request sent directly to the API is rejected on the permission
    check, with the existing Parent/Child links unchanged
```
