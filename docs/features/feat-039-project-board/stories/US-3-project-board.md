# US-3: A member reads a project's board, per level, and moves a card

- story: US-3
- depends-on: US-1
- shippable: no — one of the three stories of `feat-039-project-board`; it reads the order and the entry that US-1 and US-2 found
- status: pending

As an **organization member who delivers a project's work**, I want **a board per level over every item of a project, with every card of every team in the process's columns, and to move a card between columns**, so that **I see where the project's work stands and change a state without opening the item**.

## Acceptance criteria

Unless a scenario says otherwise: the data of US-1, with S1 and S3 in «Новая» and S2 in «Активна». The organization's process is the seeded one, whose columns at «Истории» are «Новая», «Активна», «Разрешена» and «Закрыта». U and U2 are organization members («Участник»), V a viewer («Читатель»). Addresses are below `/organizations/O`.

```gherkin
Scenario: The project's board holds the backlog's items in the process's columns (BR-MX-17, BR-MX-19, BR-MX-16)
  When U opens `/projects/A/boards/stories`
  Then the lists are named «Новая, рабочих элементов: 2», «Активна, рабочих элементов: 1», «Разрешена, рабочих элементов: 0» and «Закрыта, рабочих элементов: 0»
  And «Новая» holds S1 and S3, «Активна» holds S2, and BET-US-1 is on no card
  And the cards of MARS's board and of VENUS's board together are the cards of A's board, each once

Scenario: The head of the project's board (BR-PB-6, BR-PB-7; OQ-1, OQ-2)
  When U opens A's board
  Then the `h1` reads «Альфа», the only tab is «Канбан», the level drop-down is named «Уровень: Истории» and the browser tab reads «Канбан — Mustask»
  And «Показать бэклог» opens `/projects/A/backlogs/stories`
  And there is no primary command and no «Новый элемент» in the first column

Scenario: The shell on a project's board (BR-PB-9, OQ-6; retired by `feat-043-area-project-team-navigation`)
  Given U last chose VENUS at the switcher's team level in A
  When U opens `/projects/A/boards/features`
  Then in the main navigation «Канбан» is current (`aria-current="page"`), «Бэклоги» is not, and the page shows no trail
  And the switcher's levels name A and VENUS, and pressing «Канбан» opens `/projects/A/teams/VENUS/boards/stories`
  When U chooses project B at the switcher's project level
  Then `/projects/B/boards/features` opens and «Канбан» is current
  When U is on A's «Фичи» board again and chooses MARS at the switcher's team level
  Then the board stays, the switcher names MARS, and «Канбан» now opens `/projects/A/teams/MARS/boards/stories`

Scenario: A column lists its cards in the project's order (mod-006 BR-M4-15, BR-MX-18)
  Given U has moved S3 to the top of A's backlog
  When U opens A's board
  Then «Новая» lists S3, then S1
  And a reorder of MARS's backlog does not change that order
  And dragging a card within its column reorders nothing (owed by `feat-024-board-in-column-reorder`)

Scenario: A card move writes only the item's state, and every view shows it (BR-MX-16, BR-MX-18, mod-006 BR-M4-3)
  When U drags the card of S1 from «Новая» to «Активна», or chooses «Активна» under «Переместить в колонку» in its «Ещё действия: ALP-US-1»
  Then S1 is in «Активна», still an item of A, and its identifier, its place on A's backlog and its place on MARS's backlog are unchanged
  And the card is in «Активна» at once, focus is on its «Ещё действия: ALP-US-1» there, and the status region announces «ALP-US-1 перемещён в колонку «Активна».»
  And the next read of A's and MARS's backlogs and boards shows S1 once on each, in «Активна»

Scenario: A move offers only the states of the card's own type (BR-MX-19, mod-006 BR-M4-18)
  Given a process in which a column maps no state of bugs (the seeded one has none)
  When U opens A's board and drags the card of a bug
  Then that column is no drop target for it and is not in its «Переместить в колонку» menu
  And a direct write of a state of another type answers 409 `workitems.workitem.state_not_allowed` «Этот статус недоступен для рабочего элемента этого типа.» and changes nothing
  And no status of any item is rewritten to make the columns match

Scenario: A card's checklist is the team board's (BR-PB-6, feat-032 BR-C-1, -7, -9)
  Given S1 has task T1 on MARS's surfaces and task T2 on VENUS's, both in «Новая»
  When U opens A's board
  Then S1's task counter reads «0/2», as on MARS's board
  When U ticks T2 in S1's task list
  Then T2 is in «Закрыта», and no other field, item or place has changed
  When U adds «Задача проекта» from «Добавить задачу» on S1's card and presses Enter
  Then a task with S1 as its parent is created, on MARS's surfaces, the team of its parent, and the creation writes no place on any order

Scenario: A refused move returns the card (mod-006 § Notices)
  When U moves a card and the request gets no answer
  Then the card returns to its column and the notice reads «Не удалось сохранить перемещение. Попробуйте ещё раз.»

Scenario: Who moves cards (BR-PB-5)
  When V opens A's board
  Then V sees every card, no card can be dragged and none has «Ещё действия»
  And the checkboxes and «Добавить задачу» of V's checklists are disabled (spec § Roles)
  And V's direct write of a state answers 403 `organizations.access.forbidden` «Недостаточно прав» and changes nothing
  And the moves of U and of an administrator take effect

Scenario: A board allows a move only where the user may change the item (BR-MX-23; owed by `mod-019-scoped-access`)
  Given V1 is a viewer with project «Изменение» on A and V3 a viewer with no grant
  When each of them opens A's board
  Then V1 can move the cards of A and V3 cannot
  And V3's direct write of a state is refused with 403 and changes nothing

Scenario: A change made elsewhere shows at the next read (mod-006 BR-M4-28, BR-MX-16)
  Given U has A's board open, and U2 sets S2 to «Закрыта» on the item's form
  When U's tab regains focus
  Then A's board is read again and shows S2 in «Закрыта», with no notice
  And when U opens the title of S2's card, the item's card opens over the board for ALP-US-2 in A, and a change saved there shows on the board after the next read

Scenario: An empty board (BR-PB-6; OQ-2)
  Given project D has no story
  When U opens `/projects/D/boards/stories`
  Then the frame reads «Канбан пуст» with the hint «Новые рабочие элементы этого уровня появятся в колонке «Новая».» and no action

Scenario: The same missing addresses as the backlog's (BR-MX-1; US-1)
  When U opens `/projects/A/boards/tasks`
  Then the page reads «Страница не найдена»
  And the board of a deactivated project, of a project of another organization or of an id that names nothing reads «Проект не найден.»

Scenario: A large project board (spec NFR: volumes, query shape)
  Given A holds 5 000 stories
  When U opens A's «Истории» board and moves a card
  Then the board arrives within 2 s and holds only the items of A, and the move is saved within 1 s
  And the number of statements the read takes is the same for 50 stories as for 5 000
```
