# US-2: A member orders a team's backlog for a level

- story: US-2
- depends-on: -
- shippable: no — delivered by `mod-006-boards-backlogs`; the filtered-order
  scenario by `feat-NNN-board-backlog-filter`
- status: pending

As an **organization member**, I want **to reorder the work items in my team's
backlog at a given level**, so that **the team agrees on what to work on
next (`doc-001` BR-12)**.

## Acceptance criteria

```gherkin
Scenario: Reordering two items persists
  Given a team's ordered backlog at the Истории level, with at least two
    work items
  When a member drags one item above another (between two neighbours)
  Then the new order is reflected the next time the backlog is opened
    (BR-M4-8)

Scenario: Dragging an entry to either end of the list makes it first or last
  Given a team's ordered backlog with at least three entries
  When a member drags an entry to the top of the list
  Then it is the first entry the next time the backlog is opened
  When the same member drags it to the bottom of the list
  Then it is the last entry, and no numeric rank field was typed anywhere —
    drag and drop is the only reorder primitive in MVP (BR-M4-8)

Scenario: The Задачи level has an ordered backlog although it has no board
  Given work items of type «Задача» owned by the team
  When a member opens the team's Задачи backlog
  Then the items are listed in stack-rank order and can be dragged to
    reorder, exactly as on the other three levels (BR-M4-1, BR-M4-5)

Scenario: An empty backlog shows an empty list, not an error
  Given a team with no work items at a given level
  When a member opens that level's backlog
  Then an empty, ordered list is shown with no error and no blank screen

Scenario: A newly created item lands at the bottom by default
  Given a team's ordered backlog at a given level, with existing entries
  When a new work item is created that belongs to that team and level
  Then it appears as the last entry until someone drags it
    elsewhere (BR-M4-6)

Scenario: Items created before M4 shipped are ordered by identifier
  Given work items MSK-US-3, MSK-BUG-5 and MSK-US-9 of team A, created
    before boards and backlogs shipped
  When a member first opens team A's Истории backlog
  Then all three are listed, in the order MSK-US-3, MSK-BUG-5, MSK-US-9
    (BR-M4-13)

Scenario: The order is global, not per filtered view
  Given a team's backlog with a filter applied (e.g. by tag, BR-M4-22)
  When a member reorders two items within the filtered view
  Then the underlying, unfiltered order changes accordingly
  And clearing the filter shows the same relative order the filtered view
    implied — a filter hides non-matching items, it never keeps its own
    separate order (`doc-001` BR-12)

Scenario: Two members reorder the same backlog at once
  Given two members with the same backlog open
  When both drag a different item at nearly the same time
  Then both moves apply, in the order they arrive
  And the member whose drag arrived second gets «Порядок изменён другим
    участником — список обновлён» and a refreshed list; the first member
    sees the combined order at their next interaction (`doc-001` BR-11)

Scenario: A work item's Area is re-owned from team A to team B
  Given a project with teams A and B, and a work item on team A's Истории
    backlog
  When an administrator re-owns its Area to team B (doc-006 US-7), or a
    member sets its Область to an area team B owns
  Then it no longer appears on team A's backlog
  And it is the last entry of team B's Истории backlog — its rank on A's
    backlog is not carried over (BR-M4-6)

Scenario: A deleted team's items move to the first team's backlog
  Given team B, not the project's first team, with items X above Y on its
    Истории backlog
  When an administrator deletes team B (doc-006 BR-M1-18)
  Then team B's boards and backlogs are no longer offered anywhere
  And X and Y are the last two entries of the first team's Истории backlog,
    X above Y (BR-M4-20)

Scenario: A backlog row shows its minimum required columns
  Given a backlog entry at any level
  When a member looks at the row without opening the work item
  Then it shows the identifier, Заголовок, Исполнитель, Приоритет, Теги
    and Статус (BR-M4-14)

Scenario: Deleting a work item removes it from the backlog
  Given a work item entry in a team's backlog
  When the work item is deleted (`doc-002` US-10)
  Then it no longer appears in that team's backlog list (BR-M4-16)

Scenario: Restoring a work item returns it to its former backlog position
  Given a work item that was in a team's backlog before being deleted
  And no other entry of that backlog was reordered while it was deleted
  When the work item is restored (`doc-002` US-10)
  Then it reappears in the backlog at the same position it held before
    deletion, not appended at the bottom (BR-M4-17)

Scenario: A restored item whose Area changed team lands at the bottom
  Given a work item deleted from team A's backlog, whose Area is then
    re-owned to team B
  When the work item is restored
  Then it is the last entry of team B's backlog at its level, and it is not
    on team A's (BR-M4-17)

Scenario: A reorder of an item deleted meanwhile is refused
  Given a backlog open in a member's browser, still showing an item another
    member has since deleted
  When the member drags that item to another position
  Then the reorder is refused and no stack rank changes (BR-M4-16)
  And the member sees «Порядок изменён другим участником — список
    обновлён» and a refreshed list without the item

Scenario: A reader cannot reorder the backlog
  Given a user whose role in the organization is viewer
  When they open any team's backlog
  Then they see every row in the current order, with no drag handle and no
    drop target
  And a reorder request sent directly is rejected on the permission check,
    with the stack ranks unchanged
```
