# US-11 (MVP): A member schedules from a team's backlog planning pane

- story: US-11
- depends-on: US-3, US-4
- shippable: no — analysis story, delivered by `feat-009-iteration-planning-pane`
- status: pending

As an **organization member**, I want **a pane beside the team's backlog listing our
iterations, onto which I drag backlog items**, so that **I plan sprints from
the ordered backlog without opening every item (BR-M5-13, E-6, screenshot
03)**.

## Acceptance criteria

```gherkin
Scenario: The pane lists the team's iterations with counts
  Given team A that has selected «Релиз 1», «Релиз 1 / Спринт 1» and
    «Спринт 2», with 3 of team A's Истории-level items pointing at
    «Релиз 1 / Спринт 1»
  When a member opens «Планирование» on team A's Истории backlog
  Then the pane lists those three in tree order with their paths, followed by
    «Без итерации»
  And «Релиз 1 / Спринт 1» shows 3, «Релиз 1» shows only the items pointing
    at it directly — not its children's (BR-M5-13)
  And an item in «Удалена» pointing at an entry is not counted (BR-M5-13)
  And the team's current iteration (US-4) is marked «Текущая»

Scenario: Dropping an item on an iteration schedules it
  Given team A's backlog with the pane open
  When a member drags a backlog item onto «Спринт 2»
  Then the item points at «Спринт 2», exactly as if set on its form (US-3)
  And the change is in the item's history, and «Спринт 2»'s count rises by 1
  And the item keeps its place in the backlog order (doc-001 BR-12)

Scenario: Dropping an item on «Без итерации» unschedules it
  Given a backlog item pointing at «Спринт 2»
  When a member drags it onto «Без итерации»
  Then its pointer is cleared and recorded in history (BR-M5-7)

Scenario: Dropping onto the iteration it already has changes nothing
  Given a backlog item pointing at «Спринт 2»
  When a member drops it onto «Спринт 2»
  Then no write happens and no history entry is added

Scenario: The pane offers only the team's iterations
  Given a project iteration team A has not selected
  When a member opens the pane on team A's backlog
  Then that iteration is not listed and cannot be a drop target (BR-M5-5,
    BR-M5-7)

Scenario: A team with no iterations
  Given a team that has selected no iteration
  When a member opens the pane
  Then it shows «Команда ещё не выбрала итерации» and only «Без итерации»,
    not an error

Scenario: A drop that the write rule refuses
  Given the pane lists an iteration that an administrator has meanwhile
    unselected for the team or removed
  When a member drops an item onto it
  Then the drop is refused, the item's pointer is unchanged and the pane
    refreshes (BR-M5-7)

Scenario: A drop on an item someone else changed meanwhile
  Given two members, one with the item's form open and one with the pane
  When the first saves the item and then the second drops the stale card
  Then the drop is rejected as a conflict — a pane drop is revision-checked
    because it writes the pointer — and the backlog and the pane refresh
    (doc-001 BR-11)

Scenario: A drop on an item deleted meanwhile
  Given a backlog still showing an item another member has since deleted
  When a member drops it onto «Спринт 2»
  Then the drop is refused, the item stays in «Удалена» with its pointer
    unchanged, and the backlog and the pane refresh (doc-001 BR-14)

Scenario: A viewer cannot schedule from the pane
  Given a user whose role in the organization is viewer
  When the user drags a backlog item onto a pane entry
  Then the drop is refused and nothing changes (spec § Roles)
  And the pane with its counts is still shown
```
