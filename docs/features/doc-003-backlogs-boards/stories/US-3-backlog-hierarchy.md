# US-3: A member navigates the parent/child hierarchy on a team's backlog

- story: US-3
- depends-on: US-2
- shippable: no — delivered by `feat-004-backlog-hierarchy` (DD-3), not `mod-006-boards-backlogs`
- status: pending

As an **organization member**, I want **to see and set the parent/child hierarchy
while looking at my team's backlog**, so that **I can plan child items under
their parent without leaving the list**.

## Acceptance criteria

```gherkin
Scenario: Expanding a parent row reveals its children
  Given a backlog entry with Parent/Child links to entries one level below
    (e.g. a «Фича» with linked «Пользовательская история» items)
  When a member expands that row
  Then its linked children appear nested beneath it
  And collapsing the row hides them again, with no change to any entry's
    stack rank (BR-M4-9)

Scenario: Assigning a parent from the backlog view links the item
  Given two backlog entries, a prospective parent one level up and a child
    with no parent yet
  When a member uses the mapping action to assign the parent to the
    child
  Then the same Parent/Child link a member could set from the work
    item's own form now exists between them
  And no second, board/backlog-only link is created (BR-M4-10)

Scenario: Assigning a different parent replaces the existing link
  Given a «Пользовательская история» whose parent is «Фича» A
  When a member uses the mapping action to assign «Фича» B instead
  Then the story has exactly one Parent/Child link, to B, written in one
    step by M3's replace parent (`doc-002` BR-M3-8, BR-M4-10)
  And A's history records the removal, B's the addition, and the story's
    history one revision naming both

Scenario: The mapping offers only the level directly above
  Given a «Задача» on the team's Задачи backlog
  When a member opens the mapping action on it
  Then only Истории-level items («Пользовательская история», «Баг») are
    offered as parents (BR-M4-10)
  And a parent on the Задача's own level or two levels up is set on the work
    item form instead (`doc-002` US-5)

Scenario: Clearing a parent removes the link
  Given a backlog entry that already has a parent assigned
  When a member clears its parent from the backlog view
  Then the Parent/Child link is removed
  And the entry no longer appears beneath its former parent when that
    parent's row is expanded (BR-M4-11)
  And on its own level's backlog it stays a top-level row at its unchanged
    stack-rank position

Scenario: An item with no parent is shown at the top level
  Given a backlog entry with no Parent/Child link
  When a member opens that team's backlog
  Then the entry is shown at the top level, ungrouped
  And this is the normal state, not an error or a warning

Scenario: A child's linked parent is not one level above it
  Given a «Пользовательская история» linked directly to an «Эпик», skipping
    Фичи
  When a member opens the Эпики backlog and expands that «Эпик»'s row
  Then the story is not shown beneath it — only children exactly one level
    below appear there (BR-M4-11)
  And on the Истории backlog the story is an ordinary top-level row, with no
    error or warning

Scenario: A parent on another team's backlog does not nest the child
  Given teams A and B, a «Фича» on team A's Фичи backlog, and its child
    «Пользовательская история» whose Area team B owns
  When a member expands the «Фича» on team A's backlog
  Then the story is not nested beneath it (BR-M4-11)
  And on team B's Истории backlog the story is a top-level row

Scenario: A child of a deleted parent is treated as parentless
  Given a «Пользовательская история» whose parent «Фича» is in «Удалена»
  When a member opens the mapping action on the story and assigns
    another «Фича»
  Then the story's link to the deleted «Фича» is replaced by the link to the
    new one (BR-M4-10, `doc-002` BR-M3-9)

Scenario: Two members map the same child at once
  Given two members assigning different parents to the same child at
    nearly the same time
  When both writes arrive
  Then the child has exactly one parent, and the later write is refused
    (`doc-001` BR-11)

Scenario: A reader can expand and collapse but not assign a parent
  Given a user whose role in the organization is viewer
  When they expand or collapse a row with children
  Then the hierarchy display changes for them as normal
  And the backlog offers them no mapping action — no "assign parent" and no
    "clear parent" control
  And such a request sent directly is rejected on the permission check,
    with the existing Parent/Child links unchanged
```
