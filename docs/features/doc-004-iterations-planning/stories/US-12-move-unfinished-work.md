# US-12 (MVP): A member moves a team's unfinished work to another iteration

- story: US-12
- depends-on: US-11
- shippable: no — analysis story, delivered by `feat-010-move-unfinished-work`
- status: pending

As an **organization member closing a team's sprint**, I want **to move everything my team
did not finish in it to another iteration in one action**, so that **the
next sprint starts with the carried-over work without repointing items one by
one (BR-M5-12, E-9)**. Nothing happens by itself when a sprint ends
(BR-M5-11); this action is always started by a person, from the iteration's
entry in the planning pane (US-11).

## Acceptance criteria

```gherkin
Scenario: The unfinished rest is moved after a confirmation
  Given team A's «Спринт 2» holding team A items in states of the categories
    Proposed (2), InProgress (3), Resolved (1) and Completed (4)
  When a member chooses «Перенести незавершённые» on «Спринт 2», picks
    «Спринт 3» and confirms a prompt stating that 6 items will be moved
  Then those 6 items point at «Спринт 3», each repoint in its history with
    who and when
  And the 4 Completed items still point at «Спринт 2» (BR-M5-12)

Scenario: Cancelling the confirmation changes nothing
  Given the prompt of the previous scenario
  When the member cancels it
  Then no work item's pointer changes

Scenario: Every level moves, whichever backlog the pane is opened on
  Given «Спринт 2» holding an unfinished team A «Пользовательская история»
    and an unfinished team A «Задача»
  When a member moves its unfinished rest from the pane of the Истории
    backlog
  Then both items move, and the prompt counted both (BR-M5-12)

Scenario: Deleted items stay
  Given a work item in «Удалена» that points at «Спринт 2»
  When the member moves the unfinished rest of «Спринт 2»
  Then that item still points at «Спринт 2», and the prompt did not count it
    (BR-M5-12, doc-001 BR-14)

Scenario: Another team's items stay
  Given «Спринт 2» selected by teams A and B, with unfinished items of both
  When a member moves team A's unfinished rest from team A's pane
  Then only team A's items move, team B's still point at «Спринт 2» (doc-001
    BR-3)

Scenario: Only items pointing exactly at the source move
  Given «Релиз 1» with a child «Релиз 1 / Спринт 1», both holding unfinished
    team A items
  When a member moves the unfinished rest of «Релиз 1»
  Then only the items pointing at «Релиз 1» itself move (BR-M5-12)

Scenario: The count changed before the confirmation
  Given the prompt stating that 6 items will be moved
  When another member schedules a seventh unfinished team A item into
    «Спринт 2» and then the first member confirms
  Then nothing is written, and the prompt is shown again stating 7
    (BR-M5-12)

Scenario: The target must be another iteration the team uses
  Given team A's pane
  When a member chooses the target for «Перенести незавершённые» on «Спринт 2»
  Then the choice offers team A's selected iterations other than «Спринт 2»
    and nothing else (BR-M5-5, BR-M5-12)

Scenario: Nothing to move
  Given a team iteration whose team items are all Completed
  When a member chooses «Перенести незавершённые» on it
  Then «Нет незавершённых элементов для переноса» is shown and nothing is
    written

Scenario: The move is all or nothing
  Given a move of 6 items that fails part-way
  When the member looks at the items
  Then all 6 still point at «Спринт 2» (BR-M5-12, NFR § Bulk writes)

Scenario: Someone is editing a moved item meanwhile
  Given a member with one of the 6 items open
  When the move is confirmed and then that member saves the item
  Then the save is rejected as a conflict to reload and reapply, and after
    reloading the item points at «Спринт 3» (doc-001 BR-11)

Scenario: A viewer cannot move work
  Given a user whose role in the organization is viewer
  When the user looks at a pane entry
  Then «Перенести незавершённые» is not available, and a direct request to
    move is refused (spec § Roles)
```
