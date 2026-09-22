# US-7: A team member works the Kanban boards

- story: US-7
- depends-on: US-4
- shippable: no — delivered by mod-boards-backlogs
- status: pending

As a **team member**, I want **to see and move work items on the board of
each portfolio/requirement level**, so that **I can track my team's flow
visually instead of opening every work item**.

## Acceptance criteria

```gherkin
Scenario: A team has one board per portfolio/requirement level
  Given a project with work items of every built-in type (US-4)
  When a team member opens the team's boards
  Then there are exactly three — Эпики, Фичи, Истории (D-3, D-12) — each with
    columns matching that level's state categories
  And there is no board for the Задачи level in MVP (the Sprint Taskboard
    is release 2)

Scenario: Moving a card changes the work item's state
  Given a work item card on its level's board, in a column mapped to
    category InProgress
  When the team member drags it to a column mapped to category Completed
  Then the work item's state becomes the state that column maps to
  And this is the same state change as editing the work item directly (US-4
    scenario 2) — the board is a view, never a copy (Module map M4)

Scenario: A «Баг» appears on the Истории board next to user stories
  Given a project with both «Пользовательская история» and «Баг» work
    items (both belong to the Истории backlog level, D-3)
  When a team member opens the Истории board
  Then cards for both types appear on it, in the column matching their
    respective state's category

Scenario: An empty board shows empty columns, not an error
  Given a project whose team has no work items yet at a given level
  When a team member opens that level's board
  Then every column is shown, empty, with no error or blank screen

Scenario: A reader can watch a board but not move cards
  Given a user signed in with only the "viewer" role
  When they open any board
  Then they see all columns and cards
  And attempting to drag a card is refused, with the work item's state
    unchanged
```
