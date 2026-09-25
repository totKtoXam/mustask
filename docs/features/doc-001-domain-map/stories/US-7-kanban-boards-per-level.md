# US-7: A member works the Kanban boards

- story: US-7
- depends-on: US-4
- shippable: no — delivered by mod-006-boards-backlogs
- status: pending

As a **user whose role in the organization is member**, I want **to see and
move work items on the board of each portfolio/requirement level**, so that
**I can track a team's flow visually instead of opening every work item**.

## Acceptance criteria

```gherkin
Scenario: A team has one board per portfolio/requirement level
  Given a project with work items of every built-in type (US-4)
  When a member opens a team's boards
  Then there are exactly three — Эпики, Фичи, Истории (D-3, D-12) — each with
    columns mapped per (type, state) from that level's state categories
    (BR-2)
  And there is no board for the Задачи level in MVP (the sprint taskboard
    is release 2)

Scenario: Every team of the project has its boards
  Given a project with its first team and a second team «Платформа»
  When a member opens «Платформа»'s boards
  Then its three boards exist, showing only the items whose Область
    resolves to «Платформа» (BR-3)
  And a member who is not in «Платформа» may open and work them just the
    same — team membership carries no access (§ Roles)

Scenario: Moving a card changes the work item's state
  Given a work item card on its level's board, in a column mapped to
    category InProgress
  When the member drags it to a column mapped to category Completed
  Then the work item's state becomes the state that column maps to for its
    type
  And this is the same state change as editing the work item directly (US-4
    scenario "edits"), recorded as a state revision authored by the mover
    (BR-4) — the board is a view, never a copy (Module map M4)

Scenario: A «Баг» appears on the Истории board next to user stories
  Given a project with both «Пользовательская история» and «Баг» work
    items (both belong to the Истории backlog level, D-3)
  When a member opens the Истории board
  Then cards for both types appear on it, each in the column its type and
    state map to (BR-2)

Scenario: A story in «Разрешена» is not shown as work in progress
  Given a «Пользовательская история» in «Активна» and another in
    «Разрешена», both owned by the same team
  When a member opens the team's Истории board
  Then the «Активна» card is in the «В работе» column
  And the «Разрешена» card is in a column of its own, not in «В работе»
    (BR-2, doc-010 D-4)

Scenario: A member creates a work item from the board
  Given a team's Истории board
  When a member creates a «Баг» from it
  Then the item gets the team's default area and appears on this board in
    the column of its initial state (BR-3, doc-006 BR-M1-14)

Scenario: An empty board shows empty columns, not an error
  Given a team that has no work items yet at a given level
  When a member opens that level's board
  Then every column is shown, empty, with no error or blank screen

Scenario: A reader can watch a board but not move cards
  Given a user whose role in the organization is viewer
  When they open any board
  Then they see all columns and cards
  And attempting to drag a card is refused, with the work item's state
    unchanged
```
