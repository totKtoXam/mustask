# US-2: A team member orders the team's backlog for a level

- story: US-2
- depends-on: -
- shippable: no — delivered by mod-boards-backlogs
- status: pending

As a **team member**, I want **to reorder the work items in my team's
backlog at a given level**, so that **the team agrees on what to work on
next (BR-7)**.

## Acceptance criteria

```gherkin
Scenario: Reordering two items persists
  Given a team's ordered backlog at the Истории level, with at least two
    work items
  When a team member drags one item above another (between two neighbours)
  Then the new order is reflected the next time the backlog is opened
    (BR-8)

Scenario: Dragging an entry to either end of the list makes it first or last
  Given a team's ordered backlog with at least three entries
  When a team member drags an entry to the top of the list
  Then it is the first entry the next time the backlog is opened
  When the same member drags it to the bottom of the list
  Then it is the last entry, and no numeric rank field was typed anywhere —
    drag and drop is the only reorder primitive in MVP (BR-8)

Scenario: The Задачи level has an ordered backlog although it has no board
  Given work items of type «Задача» owned by the team
  When a team member opens the team's Задачи backlog
  Then the items are listed in stack-rank order and can be dragged to
    reorder, exactly as on the other three levels (BR-1, BR-5)

Scenario: An empty backlog shows an empty list, not an error
  Given a team with no work items at a given level
  When a team member opens that level's backlog
  Then an empty, ordered list is shown with no error and no blank screen

Scenario: A newly created item lands at the bottom by default
  Given a team's ordered backlog at a given level, with existing entries
  When a new work item is created that belongs to that team and level
  Then it appears as the last entry until someone drags it
    elsewhere (BR-6)

Scenario: The order is global, not per filtered view
  Given a team's backlog with a filter applied (e.g. by tag)
  When a team member reorders two items within the filtered view
  Then the underlying, unfiltered order changes accordingly
  And clearing the filter shows the same relative order the filtered view
    implied — a filter hides non-matching items, it never keeps its own
    separate order (BR-7)

Scenario: Two members reorder the same backlog at once
  Given two team members with the same backlog open
  When both drag a different item at nearly the same time
  Then the *stale-move conflict notice* rule applies (BR-12): the last
    write wins, and the other member sees a one-time notice and their view
    refreshes to the current order

Scenario: A work item's Area is re-pointed to an unowned node
  Given a work item on the team's backlog at a given level
  When its Area is re-pointed to a node with no owning team (MVP has one
    team per project, BR-13, so there is no second team for it to move to)
  Then it no longer appears on that team's backlog at that level
  And it has no stack rank until its Area is owned by a team again (BR-5)

Scenario: A backlog row shows its minimum required columns
  Given a backlog entry at any level
  When a team member looks at the row without opening the work item
  Then it shows the identifier, Заголовок, Исполнитель, Приоритет, Теги
    and Статус (BR-14)

Scenario: Deleting a work item removes it from the backlog
  Given a work item entry in a team's backlog
  When the work item is deleted (`doc-001` US-6)
  Then it no longer appears in that team's backlog list (BR-16)

Scenario: Restoring a work item returns it to its former backlog position
  Given a work item that was in a team's backlog before being deleted
  And no other entry of that backlog was reordered while it was deleted
  When the work item is restored (`doc-001` US-6)
  Then it reappears in the backlog at the same position it held before
    deletion, not appended at the bottom (BR-17)

Scenario: A work item with no team owner has no backlog position
  Given a work item whose Area has no owning team, directly or by
    inheritance
  When any team opens its backlog at that item's level
  Then the item does not appear on it and has no stack rank to reorder
    (BR-5)

Scenario: A reader cannot reorder the backlog
  Given a user signed in with only the "viewer" role
  When they open the backlog
  Then they see every row in the current order, with no drag handle and no
    drop target
  And a reorder request sent directly to the API is rejected on the
    permission check, with the stack ranks unchanged
```
