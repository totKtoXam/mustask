# US-5 (release 2): A team plans a sprint from the backlog

- story: US-5
- depends-on: US-3, US-11
- shippable: no — release-2 ground, not built by any MVP task; founded by `mod-NNN-sprint-views`
- status: pending

As a **team member**, I want **a view of one iteration holding the work
committed to it with its child tasks**, so that **the team can see and adjust
the content of a sprint in one place instead of filtering the product backlog
every time (screenshots 03, 11)**. Scheduling items from the product backlog
is the MVP planning pane (US-11); this view reuses it and adds no second
scheduling mechanism.

## Acceptance criteria

```gherkin
Scenario: The sprint backlog shows one iteration's work
  Given a team with a selected iteration and work items pointing at it
  When a member opens the sprint view for that iteration
  Then only work items of the requirement level pointing exactly at that
    iteration and owned by the team are listed (BR-7, doc-001 BR-3)
  And each of them can be expanded to its child Задача items

Scenario: The view opens on the current iteration
  Given a team with a resolvable current iteration (US-4)
  When a member opens the sprint view without choosing an iteration
  Then the current iteration is shown, and the member can switch to any
    other iteration the team uses (BR-5, BR-8)

Scenario: An empty sprint
  Given a team iteration with no work items pointing at it
  When a member opens its sprint view
  Then an explicit empty state is shown, with a way to schedule items from
    the product backlog through the planning pane (US-11) — not an error

Scenario: Removing work from a sprint
  Given a work item in a sprint
  When a member takes it out of the sprint view
  Then its iteration pointer is cleared — the same single pointer as US-3
    (BR-7) — and the item keeps its backlog order (doc-001 BR-12)

Scenario: An undated iteration still has a sprint backlog
  Given a team iteration with no dates
  When a member opens its sprint view
  Then the content is shown normally, and only the date-derived parts
    (burndown, capacity days) are unavailable with a stated reason (BR-4,
    BR-16)
```
