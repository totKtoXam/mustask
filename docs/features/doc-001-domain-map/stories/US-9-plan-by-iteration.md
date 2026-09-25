# US-9: A member plans by iteration

- story: US-9
- depends-on: US-4
- shippable: no — delivered by mod-007-iterations; the «Текущая итерация» filter scenarios by feat-008-iteration-filter
- status: pending

As a **user whose role in the organization is member**, I want **to assign a
work item to an iteration and see which one is current for a team**, so
that **I can tell what the team is doing now from what is planned for later
(BR-9, D-11)**.

## Acceptance criteria

```gherkin
Scenario: An administrator builds the project's iteration tree
  Given a project (US-1)
  When an administrator adds a release «Релиз 1» with start/finish dates,
    and under it a sprint with dates and a sprint with a name only
  Then all three belong to the project (D-11) and appear in its iteration
    tree, the sprints under the release (doc-004)

Scenario: A team selects which of the project's iterations it uses
  Given a project with several iterations (previous scenario)
  When an administrator selects a subset of them for a team to use
  Then only those selected iterations are offered when assigning a work
    item owned by that team or filtering that team's board/backlog by
    iteration (`TEAM }o--o{ ITERATION`, D-11)

Scenario: A member assigns a work item to an iteration
  Given a work item whose Область the team owns, and an iteration that team
    has selected
  When the member sets the work item's iteration
  Then the work item shows that iteration
  And it points at exactly one iteration at a time (BR-7)

Scenario: The team's current iteration is derived from dates
  Given a team that has selected several iterations, one of which has
    start/finish dates covering today and the others do not
  When a member filters the team's board or backlog by «Текущая итерация»
  Then only the team's work items assigned to the covering iteration or to
    any of its descendants are shown (BR-9, BR-15)

Scenario: Overlapping iterations resolve to the earliest finish
  Given a team that has selected two iterations whose dates both cover
    today, with different finish dates
  When a member filters by «Текущая итерация»
  Then the team's work items assigned to the iteration with the earlier
    finish date, or to its descendants, are shown (BR-9)

Scenario: The team's default iteration is current when no dates decide it
  Given a team whose selected iterations have no dates covering today
    (missing dates, or none matching), and that has set one of its selected
    iterations as its default (a team setting, BR-9)
  When a member filters by «Текущая итерация»
  Then the team's work items assigned to its default iteration, or to its
    descendants, are shown

Scenario: The default iteration is never given to a new work item
  Given a team with a default iteration
  When a member creates a work item from that team's backlog
  Then the new item has no iteration (BR-9)

Scenario: No current iteration
  Given a team whose selected iterations' dates do not cover today and that
    has no default iteration set
  When a member filters by «Текущая итерация»
  Then the filter returns an explicit «Нет текущей итерации» state, not an
    error and not every work item

Scenario: A removed iteration's work items become unassigned
  Given work items pointing at an iteration, one of them in «Удалена»
  When an administrator removes that iteration, confirming the count, which
    includes the deleted item
  Then every one of those work items' iteration pointer becomes empty
    (Operation completeness: Iteration; BR-14)
  And each change is recorded in history like any other field change (BR-4)

Scenario: Only an administrator manages iterations and team selection
  Given a user whose role in the organization is member and a user whose
    role is viewer
  When either attempts to add, edit the dates of, remove an iteration, or
    change which iterations a team selects or defaults to
  Then the action is refused (Operation completeness: Iteration)
  And the member may still assign a work item to an iteration its team
    selected

Scenario: Assigning a work item to another project's iteration is refused
  Given a work item in project A and an iteration that belongs to project B
  When a member attempts to set the work item's iteration to it
  Then the action is refused
```
