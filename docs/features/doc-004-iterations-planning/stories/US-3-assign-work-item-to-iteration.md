# US-3 (MVP): A member schedules a work item into an iteration

- story: US-3
- depends-on: US-1, US-2
- shippable: no — analysis story, delivered by `mod-007-iterations`
- status: pending

As an **organization member**, I want **to put a work item into one iteration from its
form, change it and take it out again**, so that **it is clear when the item
is meant to be done (BR-M5-7, doc-001 BR-7)**. Scheduling from the backlog is
US-11; the same rule applies there.

## Acceptance criteria

```gherkin
Scenario: A work item is scheduled
  Given a work item whose area is owned by team A, and an iteration team A
    uses
  When the member sets the work item's «Итерация» to it
  Then the work item shows that iteration with its path
  And the change is in the work item's history with who and when (doc-001
    BR-4)

Scenario: A parent node can be pointed at
  Given team A has selected «Релиз 1» but none of its children
  When the member sets a team A work item's iteration to «Релиз 1»
  Then the work item points at «Релиз 1» (BR-M5-7)

Scenario: A work item holds one iteration at a time
  Given a work item already pointing at iteration 1
  When the member sets its iteration to iteration 2
  Then the work item points at iteration 2 only — there is no way to hold
    both (BR-M5-7)
  And the history shows the previous value

Scenario: The iteration is cleared
  Given a work item pointing at an iteration
  When the member clears the «Итерация» field
  Then the work item is unscheduled and unchanged in every other respect
  And the clearing is in the history

Scenario: The picker offers only the owning team's iterations
  Given a project with five iterations, of which the item's owning team uses
    three
  When the member opens the work item's iteration picker
  Then exactly those three are offered, in tree order with their paths
    (BR-M5-5)

Scenario: The owning team has no iterations to offer
  Given a work item whose owning team has selected no iteration at all (or
    whose project has none)
  When the member opens the work item's iteration picker
  Then the picker shows «Команда ещё не выбрала итерации» as an explicit
    empty state — not an error and not the project's whole tree (BR-M5-5)

Scenario: An iteration the owning team does not use is refused on write
  Given a work item owned by team A and a project iteration team A has not
    selected
  When a request sets the work item's iteration to it by any path — form,
    pane or API — whether made by a member or an administrator
  Then it is refused with «Команда не использует эту итерацию» and the
    pointer is unchanged (BR-M5-7)

Scenario: Another project's iteration is refused
  Given a work item in project A and an iteration belonging to project B
  When a member tries to set the work item's iteration to it
  Then the action is refused and the pointer is unchanged (BR-M5-7)

Scenario: An iteration outside the team's selection is kept but not offered
  Given a work item pointing at an iteration its owning team has since
    unselected, or kept after its area moved to another team — also when
    its former team was deleted
  When a member opens the work item and saves a change to its title
  Then the iteration is still shown, marked as outside the team's selection,
    and the save leaves the pointer as it was
  And the picker does not offer that iteration again (BR-M5-7)

Scenario: Scheduling into a past or a future iteration is allowed
  Given team iterations whose finish date was last month and one starting
    next quarter
  When a member assigns a work item in any state to either
  Then the assignment succeeds — dates constrain nothing (BR-M5-11)

Scenario: A viewer cannot schedule
  Given a user whose role in the organization is viewer
  When the user tries to set or clear a work item's iteration
  Then the action is refused (spec § Roles)

Scenario: Two members change the same work item's iteration at once
  Given two members with the same work item open
  When both save a different iteration
  Then the second save is rejected as a conflict to reload and reapply —
    Итерация is part of the form save (doc-001 BR-11)
```
