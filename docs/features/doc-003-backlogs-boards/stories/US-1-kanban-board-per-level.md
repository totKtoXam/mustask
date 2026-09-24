# US-1: A team member works the Kanban board for a level

- story: US-1
- depends-on: -
- shippable: no — delivered by mod-boards-backlogs
- status: pending

As a **team member**, I want **to see and move work items on the board of
each portfolio/requirement level**, so that **I can track my team's flow
visually instead of opening every work item**.

## Acceptance criteria

```gherkin
Scenario: A team has one board per portfolio/requirement level
  Given a project with work items of every built-in type
  When a team member opens the team's boards
  Then there are exactly three — Эпики, Фичи, Истории (D-3, D-12)
  And each board's column set is exactly the non-Removed categories
    (Proposed/InProgress/Resolved/Completed) that at least one of that
    level's built-in types defines a state in, in that fixed order (BR-2)
  And there is no board for the Задачи level in MVP (the Sprint Taskboard
    is release 2)

Scenario: A column exists if any one type at the level uses its category
  Given a level with two or more built-in types, where one type defines a
    state in a given category and another type at the same level does not
  When a team member opens that level's board
  Then the column for that category is shown — a category is used if any
    type at the level defines a state in it, not only if every type does
    (BR-2)
  And no column exists for a category no type at the level uses at all

Scenario: Moving a card changes the work item's state
  Given a work item card of a given type, on its level's board, in the
    column mapped for that type to a state in category C1
  When the team member drags it to a column mapped, for the same type, to
    a state in category C2
  Then the work item's state becomes that type's first state in category
    C2, in M2's seed order (BR-2) — the resulting state follows from the
    mapping rule alone, not from which category the card left
  And this is the same state change editing the work item directly would
    make — the board holds no separate board-position field (BR-3)
  And within the target column the card sits at its unchanged stack-rank
    position, wherever in the column it was dropped (BR-15)

Scenario: A card in a type's non-first state of a category still has a column
  Given a type at the level with two or more states in one category C, and
    a work item of that type in one of them other than the first in M2's
    seed order
  When a team member opens that level's board
  Then the card appears in the column for category C (BR-3)
  And dragging a card of that type into the column for C from another
    column sets the type's first state in C, not the other one (BR-2)

Scenario: A card cannot be dropped on a column its type has no state in
  Given a level with two built-in types where a column exists for a
    category because one type defines a state there while the other type
    at the level does not (BR-2)
  When a team member drags a card of the second type toward that column
  Then the drop is refused — the board offers no drop target there for
    that card (BR-18)
  And a direct API request to move it there is rejected, with the work
    item's state unchanged

Scenario: A «Баг» appears on the Истории board next to user stories
  Given a project with both «Пользовательская история» and «Баг» work
    items (both belong to the Истории backlog level, D-3)
  When a team member opens the Истории board
  Then cards for both types appear on it, each in the column mapped for
    its own type to its current state's category (BR-2)

Scenario: A card shows its minimum required fields
  Given a work item card on any level's board
  When a team member looks at the card without opening the work item
  Then it shows the identifier, Заголовок, Исполнитель, Приоритет and Теги
    (BR-14)
  And no other field is required to be visible in MVP

Scenario: Cards within a column are ordered by the backlog's stack rank
  Given a column with more than one card
  When a team member opens the board
  Then the cards appear in the same relative order as their team+level
    backlog (BR-15) — there is no separate, board-only card order

Scenario: Dragging a card vertically within its column reorders the backlog
  Given two cards in the same column
  When a team member drags one above the other without leaving the column
  Then their relative order changes in the team's backlog for that level
    exactly as a backlog drag would (BR-8, BR-15)
  And neither card's state changes

Scenario: Deleting a work item removes its card from the board
  Given a work item card on its level's board
  When the work item is deleted (`doc-001` US-6)
  Then its card no longer appears on any column of that board (BR-16)

Scenario: Restoring a deleted work item returns its card to the board
  Given a work item that was on a board before being deleted
  And no other entry of that backlog was reordered while it was deleted
  When the work item is restored (`doc-001` US-6)
  Then its card reappears in the column matching its restored state
  And it holds the same position within that column it held before
    deletion (BR-17)

Scenario: Every card sits in the single default swimlane
  Given a team's board with cards of every type at that level
  When a team member opens the board
  Then all cards appear in one, unnamed default lane (BR-4)
  And no swimlane picker or "add swimlane" control is shown

Scenario: An empty board shows empty columns, not an error
  Given a project whose team has no work items yet at a given level
  When a team member opens that level's board
  Then every column is shown, empty, with no error or blank screen

Scenario: A reader can watch a board but not move cards
  Given a user signed in with only the "viewer" role
  When they open any board
  Then they see all columns, the default lane and all cards
  And no card offers a drag handle or a drop target
  And a card-move request sent directly to the API is rejected on the
    permission check, with the work item's state and history unchanged

Scenario: Two members drag the same card at once
  Given two team members with the same board open, looking at the same card
  When both drag that card to a different column at nearly the same time
  Then the *stale-move conflict notice* rule applies (BR-12): the last
    write wins, and the other member sees a one-time notice and their view
    refreshes to the card's current column

Scenario: A drag always lands even while a form save is pending
  Given a team member has the work item's own form open, loaded before
    another team member drags its card to a different column
  When the drag lands
  Then the work item's state becomes the dragged-to column's mapped state
    immediately, with no version check and no notice sent to the open form
    (BR-19)
  When the member with the form open then saves it
  Then the save is rejected with the normal edit conflict (`doc-001`
    BR-11) — the same outcome as if another form save had raced it — and
    the member reloads to reapply their edit
```
