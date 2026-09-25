# US-8: A member orders the backlog

- story: US-8
- depends-on: US-7
- shippable: no — delivered by mod-006-boards-backlogs; the filtered-view scenario by feat-NNN-board-backlog-filter
- status: pending

As a **user whose role in the organization is member**, I want **to reorder
the work items in a team's backlog at a given level**, so that **the team
agrees on what to work on next (BR-12)**.

## Acceptance criteria

```gherkin
Scenario: Reordering two items persists
  Given a team's ordered backlog at the Истории level, with at least two
    work items
  When a member drags one item above another
  Then the new order is reflected the next time the backlog is opened
    (stack rank owned by M4, BR-12)

Scenario: The order is global, not per filtered view
  Given a team's backlog with a filter applied (e.g. by tag, US-10)
  When a member reorders two items within the filtered view
  Then the underlying, unfiltered order changes accordingly
  And clearing the filter shows the same relative order the filtered view
    implied — a filter hides non-matching items, it never keeps its own
    separate order (BR-12)

Scenario: Two members reorder the same backlog at once
  Given two members with the same backlog open
  When both drag a different item at nearly the same time
  Then both moves apply, in the order they arrived
  And the member whose move arrived second receives «Порядок изменён другим
    участником — список обновлён» and a refreshed view of the combined
    order (BR-11, § Concurrency policy)
  And the first member sees the combined order at their next interaction

Scenario: An item re-owned to another team moves to that team's backlog
  Given a work item on the first team's Истории backlog, in an area the
    first team owns
  When an administrator re-owns that area to the team «Платформа»
  Then the item leaves the first team's backlog and is appended at the
    bottom of «Платформа»'s (BR-3, doc-003)

Scenario: A reader cannot reorder the backlog
  Given a user whose role in the organization is viewer
  When they attempt to drag an item in the backlog
  Then the action is refused and the order is unchanged
```
