# US-9: A team member plans by iteration

- story: US-9
- depends-on: US-4
- shippable: no — delivered by mod-iterations
- status: pending

As a **team member**, I want **to assign a work item to an iteration and see
which one is current for my team**, so that **I can tell what my team is
doing now from what is planned for later (BR-9, D-11)**.

## Acceptance criteria

```gherkin
Scenario: An administrator adds iterations, with and without dates
  Given a project (US-1)
  When an administrator adds an iteration with a name and start/finish
    dates, and a second iteration with a name only
  Then both belong to the project (D-11) and appear in its flat iteration
    list (MVP: no tree/hierarchy — module map M5)

Scenario: A team selects which of the project's iterations it uses
  Given a project with several iterations (previous scenario)
  When an administrator selects a subset of them for the team to use
  Then only those selected iterations are offered when assigning a work
    item or filtering that team's board/backlog by iteration (`TEAM }o--o{
    ITERATION`, D-11)

Scenario: A team member assigns a work item to an iteration
  Given an existing work item and an existing iteration in the same project
  When the team member sets the work item's iteration
  Then the work item shows that iteration
  And it points at exactly one iteration at a time (BR-7)

Scenario: The team's current iteration is derived from dates
  Given a team that has selected several iterations, one of which has
    start/finish dates covering today and the others do not
  When a team member filters the board or the backlog by "current
    iteration"
  Then only work items assigned to the dated, covering iteration are shown
    (BR-9)

Scenario: Overlapping iterations resolve to the earliest finish
  Given a team that has selected two iterations whose dates both cover
    today, with different finish dates
  When a team member filters by "current iteration"
  Then only work items assigned to the iteration with the earlier finish
    date are shown (BR-9)

Scenario: The team's default iteration is current when no dates decide it
  Given a team whose selected iterations have no dates covering today
    (missing dates, or none matching), and that has set one of its selected
    iterations as its default (a team setting, BR-9)
  When a team member filters by "current iteration"
  Then only work items assigned to the team's default iteration are shown

Scenario: No current iteration
  Given a team whose selected iterations' dates do not cover today and that
    has no default iteration set
  When a team member filters by "current iteration"
  Then the filter returns an explicit "no current iteration" state, not an
    error and not every work item

Scenario: A deleted iteration's work items become unassigned
  Given work items pointing at an iteration
  When an administrator deletes that iteration
  Then those work items' iteration pointer becomes empty (Operation
    completeness: Iteration)
  And the change is recorded in history like any other field change (BR-4)

Scenario: Only an administrator manages iterations and team selection
  Given a user signed in with the "member" role and a user with only the
    "viewer" role
  When either attempts to add, edit the dates of, delete an iteration, or
    change which iterations the team selects or defaults to
  Then the action is refused (Operation completeness: Iteration)
  And a member may still assign a work item to an existing iteration

Scenario: Assigning a work item to another project's iteration is refused
  Given a work item in project A and an iteration that belongs to project B
  When a team member attempts to set the work item's iteration to it
  Then the action is refused
```
