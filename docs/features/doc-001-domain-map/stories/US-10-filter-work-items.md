# US-10: A member finds work items by filter

- story: US-10
- depends-on: US-4
- shippable: no — delivered by feat-003-work-item-search (the work item list) and feat-NNN-board-backlog-filter (the board and the backlog)
- status: pending

As a **user whose role in the organization is member**, I want **to filter
the work item list, the board and the backlog by state, assignee, tag and
area**, so that **I can find the relevant work without opening every
item**.

## Acceptance criteria

```gherkin
Scenario: Filtering the work item list by state
  Given a project with work items in different states (US-4)
  When a member filters the list by state «Активна»
  Then only work items currently in that state are shown

Scenario: One state label can span two categories
  Given a «Пользовательская история» and a «Баг», both in «Разрешена»
  When a member filters the list by state «Разрешена»
  Then both are shown, although the first is InProgress and the second
    Resolved (BR-2, doc-010 D-4)

Scenario: Deleted items appear only when the filter names «Удалена»
  Given a work item in «Удалена»
  When a member filters the list with no state condition, and then by
    state «Удалена»
  Then the item is absent from the first result and present in the second
    (BR-14)

Scenario: An area filter includes the area's subtree
  Given work items in the area «Платформа» and in its child «Платформа /
    API»
  When a member filters the list by the area «Платформа»
  Then the items of both areas are shown (BR-15)

Scenario: The same kind of filter applies on a board
  Given a team's board (US-7)
  When a member filters it by assignee and by a tag together
  Then only cards matching both conditions are shown — filtering is a
    property of the list, board and backlog surfaces (M3/M4), not a separate
    module (module map M6, D-4)

Scenario: Filters are inline, not a saved query
  Given a member has set a filter on the work item list
  When they navigate away and come back
  Then the filter is not preserved as a named, reusable query — MVP has no
    saved/shared queries (module map M6, MVP boundary table); it is session
    state, nothing more

Scenario: Tags from another project never appear in the filter
  Given a member working in project A, and a tag that exists only in
    project B
  When they open project A's tag filter
  Then project B's tag is not offered (BR-8)

Scenario: An empty result is shown clearly
  Given a filter combination that matches no work item
  When a member applies it
  Then the list shows a clear "no results" state, not an error and not a
    stale previous result
```
