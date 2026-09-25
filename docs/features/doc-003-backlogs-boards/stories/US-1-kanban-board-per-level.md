# US-1: A member works a team's Kanban board for a level

- story: US-1
- depends-on: -
- shippable: no — delivered by `mod-006-boards-backlogs`
- status: pending

As an **organization member**, I want **to see and move work items on the board of
each portfolio/requirement level**, so that **I can track my team's flow
visually instead of opening every work item**.

## Acceptance criteria

```gherkin
Scenario: Every team has one board per portfolio/requirement level
  Given a project with teams A and B and work items of every built-in type
  When a member opens team A's boards, then team B's
  Then each team has exactly three — Эпики, Фичи, Истории (D-3, D-12) —
    and there is no board for the Задачи level in MVP (BR-M4-1)
  And the Эпики and Фичи boards show the columns «Новые», «В работе»,
    «Разрешена», «Завершённые», and the Истории board «Новые», «В работе»,
    «Разрешена», «Решённые», «Завершённые», in that order (BR-M4-2)
  And each board shows only items whose Area its team owns (BR-M4-5)

Scenario: Two cards labelled «Разрешена» sit in two columns
  Given a «Пользовательская история» and a «Баг», both in «Разрешена»
  When a member opens the Истории board
  Then the story's card is in the column «Разрешена» and the bug's card in
    «Решённые» — the story's state is InProgress, the bug's Resolved
    (BR-M4-2, `doc-002` § Types, levels and states)

Scenario: Moving a card writes the state its target column maps
  Given a «Пользовательская история» card in «В работе» and a «Баг» card in
    «В работе» on the Истории board
  When the member drags the story to «Разрешена» and the bug to
    «Решённые»
  Then both work items are in «Разрешена» (BR-M4-2, BR-M4-3)
  And this is the same state change editing the work item directly would
    make — the board holds no separate board-position field
  And within the target column each card sits at its unchanged stack-rank
    position, wherever in the column it was dropped (BR-M4-15)

Scenario: A card cannot be dropped on a column its type has no state in
  Given the Истории board
  When a member drags a «Баг» card toward «Разрешена», or a
    «Пользовательская история» card toward «Решённые»
  Then the drop is refused — the board offers no drop target there for
    that card (BR-M4-18)
  And a direct request to move it there is rejected, with the work item's
    state unchanged

Scenario: A card shows its minimum required fields
  Given a work item card on any level's board
  When a member looks at the card without opening the work item
  Then it shows the identifier, Заголовок, Исполнитель, Приоритет and Теги
    (BR-M4-14)
  And no other field is required to be visible in MVP

Scenario: Cards within a column are ordered by the backlog's stack rank
  Given a column with more than one card
  When a member opens the board
  Then the cards appear in the same relative order as their team+level
    backlog (BR-M4-15) — there is no separate, board-only card order

Scenario: Dragging a card vertically within its column reorders the backlog
  Given two cards in the same column
  When a member drags one above the other without leaving the column
  Then their relative order changes in the team's backlog for that level
    exactly as a backlog drag would (BR-M4-8, BR-M4-15)
  And neither card's state changes

Scenario: Deleting a work item removes its card from the board
  Given a work item card on its level's board
  When the work item is deleted (`doc-002` US-10)
  Then its card no longer appears on any column of that board (BR-M4-16)

Scenario: Restoring a deleted work item returns its card to the board
  Given a work item that was on a board before being deleted
  And no other entry of that backlog was reordered while it was deleted
  When the work item is restored (`doc-002` US-10)
  Then its card reappears in the column matching its restored state
  And it holds the same position within that column it held before
    deletion (BR-M4-17)

Scenario: A card deleted meanwhile cannot be dragged
  Given a board open in a member's browser, still showing a card whose work
    item another member has since deleted
  When the member drags that card to another column
  Then the move is refused: the item stays in «Удалена» and its history
    gains nothing (BR-M4-16)
  And the member sees «Порядок изменён другим участником — список
    обновлён» and a refreshed board without the card

Scenario: Every card sits in the single default swimlane
  Given a team's board with cards of every type at that level
  When a member opens the board
  Then all cards appear in one, unnamed default lane (BR-M4-4)
  And no swimlane picker or "add swimlane" control is shown

Scenario: An empty board shows empty columns, not an error
  Given a team with no work items yet at a given level
  When a member opens that level's board
  Then every column is shown, empty, with no error or blank screen

Scenario: A reader can watch a board but not move cards
  Given a user whose role in the organization is viewer
  When they open any team's board
  Then they see all columns, the default lane and all cards
  And no card offers a drag handle or a drop target
  And a card-move request sent directly is rejected on the permission
    check, with the work item's state and history unchanged

Scenario: Two members drag the same card at once
  Given two members with the same board open, looking at the same card
  When both drag that card to different columns at nearly the same time
  Then the drag that arrives second decides the card's column
  And the member whose drag arrived second gets «Порядок изменён другим
    участником — список обновлён» and a refreshed board; the first member
    sees the final column at their next interaction (`doc-001` BR-11)

Scenario: A drag always lands even while a form save is pending
  Given a member has the work item's own form open, loaded before
    another member drags its card to a different column
  When the drag lands
  Then the work item's state becomes the dragged-to column's mapped state
    immediately, with no check and no notice to the open form
  When the member with the form open then saves it
  Then the save is rejected with the normal edit conflict and the member
    reloads to reapply their edit (`doc-001` BR-11)
```
