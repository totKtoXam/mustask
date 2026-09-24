# US-7 (release 2): A team plans sprint capacity

- story: US-7
- depends-on: US-5
- shippable: no — release-2 ground, not built by any MVP task; delivered by `feat-NNN-sprint-capacity`
- status: pending

As a **team member planning a sprint**, I want **to record how many hours a
day each member has for this iteration and which days they are away**, so
that **the team can see whether the work it has committed fits the time it
actually has (BR-15, screenshot 12)**.

## Acceptance criteria

```gherkin
Scenario: Capacity is recorded per member per iteration
  Given a team iteration with dates and three team members
  When a member sets hours per day and one activity for each of them
  Then «Ёмкость» for that iteration shows those values, and they apply to
    that iteration only — another iteration starts empty (BR-15)

Scenario: Days off reduce the available time
  Given a member with 6 hours a day in a two-week iteration
  When two days are marked as that member's «Дни отсутствия» and one day as
    a team day off
  Then the member's total for the iteration drops by the three days
  And the team's total drops accordingly

Scenario: Only the team's working days count
  Given a team whose working days are Monday to Friday
  When the iteration spans two calendar weeks
  Then weekend days count for nobody's capacity (BR-15)

Scenario: Capacity never blocks scheduling
  Given a team whose committed work exceeds its capacity for the iteration
  When a member schedules one more item into that iteration
  Then the scheduling succeeds, and the over-commitment is shown as a
    warning only (BR-15)

Scenario: An undated iteration has no capacity
  Given a team iteration without dates
  When a member opens «Ёмкость» for it
  Then the view states that dates are required and offers to set them
    instead of showing zeroes (BR-4, screenshot 12)

Scenario: A person who leaves the team
  Given capacity recorded for a member of the team
  When that member is removed from the team
  Then their capacity row is no longer counted in the team's total, and the
    recorded value is not silently reassigned to anyone else
```
