# US-8: A team member orders the backlog

- story: US-8
- depends-on: US-7
- shippable: no — delivered by mod-boards-backlogs
- status: pending

As a **team member**, I want **to reorder the work items in my team's
backlog at a given level**, so that **the team agrees on what to work on
next (BR-12)**.

## Acceptance criteria

```gherkin
Scenario: Reordering two items persists
  Given a team's ordered backlog at the Истории level, with at least two
    work items
  When a team member drags one item above another
  Then the new order is reflected the next time the backlog is opened
    (stack rank owned by M4, BR-12)

Scenario: The order is global, not per filtered view
  Given a team's backlog with a filter applied (e.g. by tag, US-10)
  When a team member reorders two items within the filtered view
  Then the underlying, unfiltered order changes accordingly
  And clearing the filter shows the same relative order the filtered view
    implied — a filter hides non-matching items, it never keeps its own
    separate order (BR-12)

Scenario: Two users reorder the same backlog at once
  Given two team members with the same backlog open
  When both drag a different item at nearly the same time
  Then the *stale-move conflict notice* rule applies (BR-11): the last
    write wins, and the other member sees a one-time notice and their view
    refreshes to the current order

Scenario: A work item with no team owner has no backlog position
  Given a work item whose Area has no owning team, directly or by
    inheritance (US-2)
  When any team opens its backlog at that item's level
  Then the item does not appear on it and has no stack rank to reorder

Scenario: A reader cannot reorder the backlog
  Given a user signed in with only the "viewer" role
  When they attempt to drag an item in the backlog
  Then the action is refused and the order is unchanged
```
