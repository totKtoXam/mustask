# US-8 (release 2): A team tracks burndown and velocity

- story: US-8
- depends-on: US-5
- shippable: no — release-2 ground, not built by any MVP task; founded by `mod-NNN-iteration-metrics`
- status: pending

As a **team member**, I want **to see how the remaining work of the current
sprint falls day by day and how much the team completed in each past
iteration**, so that **we can tell whether this sprint is on track and how
much to take into the next one (BR-16)**.

## Acceptance criteria

```gherkin
Scenario: The burndown covers the iteration's days
  Given a dated team iteration with estimated work in it
  When a member opens «Диаграмма сгорания» for it
  Then there is one point per day from the start date to the finish date
  And each point is the remaining work of the team's items in that iteration
    as of that day, reconstructed from history (BR-16, doc-001 BR-4)

Scenario: A change made today moves today's point only
  Given a burndown for a running iteration
  When a member closes a task today
  Then today's point drops and no earlier point changes — past days are
    never rewritten (BR-16)

Scenario: An item added mid-sprint is visible as such
  Given a sprint that is half over
  When a member schedules a new estimated item into it
  Then the remaining work rises on that day, and the chart does not hide the
    increase by rescaling silently

Scenario: Work moved on by «Перенести незавершённые» leaves the burndown
  Given a finished sprint whose unfinished rest was moved to the next one
    (US-12)
  When a member opens both sprints' burndowns
  Then the moved work drops out of the old sprint on the day of the move and
    enters the new one on that day — neither chart rewrites earlier days

Scenario: Velocity counts completed iterations
  Given a team with three finished iterations
  When a member opens «Скорость»
  Then there is one bar per iteration, showing the effort of the items in it
    that reached a Completed-category state by its finish date (doc-001
    BR-2, BR-16)
  And an item completed after the finish date does not count towards that
    iteration

Scenario: An undated iteration has no burndown
  Given a team iteration without dates
  When a member opens its charts
  Then the burndown states that dates are required, and velocity simply
    omits that iteration (BR-4, BR-16)

Scenario: Charts are read-only
  Given any chart of this story
  When a member interacts with it
  Then nothing about a work item, an iteration or a team setting changes —
    the chart is derived data only (BR-16)

Scenario: Estimation fields are a prerequisite
  Given a team whose work items carry no estimate
  When a member opens the burndown
  Then the chart states that estimates are missing rather than drawing a
    flat line at zero (BR-16; the estimation fields are doc-002's, E-8)
```
