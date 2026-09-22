# US-10: A team member finds work items by filter

- story: US-10
- depends-on: US-4
- shippable: no — delivered by feat-work-item-search
- status: pending

As a **team member**, I want **to filter the work item list and board by
state, assignee, tag and area**, so that **I can find the work relevant to
me without opening every item**.

## Acceptance criteria

```gherkin
Scenario: Filtering the work item list by state
  Given a project with work items in different states (US-4)
  When a team member filters the list by state «Выполняется»
  Then only work items currently in that state are shown

Scenario: The same filter applies on a board
  Given a team's board (US-7)
  When a team member filters it by assignee and by a tag together
  Then only cards matching both conditions are shown — filtering is a
    property of the list and the board surfaces (M3/M4), not a separate
    module (module map M6, D-4)

Scenario: Filters are inline, not a saved query
  Given a team member has set a filter on the work item list
  When they navigate away and come back
  Then the filter is not preserved as a named, reusable query — MVP has no
    saved/shared queries (module map M6, MVP boundary table); it is session
    state, nothing more

Scenario: Tags from another project never appear in the filter
  Given a team member working in project A, and a tag that exists only in
    project B
  When they open project A's tag filter
  Then project B's tag is not offered (BR-8)

Scenario: An empty result is shown clearly
  Given a filter combination that matches no work item
  When a team member applies it
  Then the list shows a clear "no results" state, not an error and not a
    stale previous result
```
