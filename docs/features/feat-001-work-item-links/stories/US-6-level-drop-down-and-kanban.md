# US-6: A user picks the level from a drop-down on «Бэклоги» and «Канбан»

- story: US-6
- depends-on: -
- shippable: no — the human asked for it with this cut (D-3), and it reshapes the header the tree's page (US-4) sits in
- status: pending

As a **signed-in user**, I want **one level drop-down on the backlog and
the board, and the board called «Канбан»**, so that **switching levels works
the same on both pages and the names read as the team uses them**.

## Acceptance criteria

Data: US-1's. `<team>` is `/organizations/<o>/projects/<p>/teams/<t>` of
«Платформа». Spec BR-H-15, -16, -17, § The rename, § Level drop-down.

```gherkin
Scenario: The drop-down replaces the tabs on «Бэклоги»
  When the user opens `<team>/backlogs/stories`
  Then under the `h1` «Бэклоги» there are no tabs, and a button reads
    «Уровень: Истории»
  When the user activates it
  Then a menu offers «Эпики», «Фичи», «Истории», «Истории» marked current
  When the user chooses «Фичи»
  Then the address is `<team>/backlogs/features`, the «Фичи» backlog shows,
    and the button reads «Уровень: Фичи», focus on it

Scenario: The same drop-down on «Канбан»
  When the user opens `<team>/boards/epics`
  Then the `h1` reads «Канбан», the button «Уровень: Эпики»
  When the user chooses «Истории»
  Then the address is `<team>/boards/stories` and the «Истории» board shows

Scenario: Back returns to the previous level
  Given the user went from «Истории» to «Фичи» with the drop-down
  When the user presses the browser's back
  Then the «Истории» backlog shows and the button reads «Уровень: Истории»

Scenario: The drop-down works by keyboard
  Given focus on «Уровень: Истории»
  When the user presses ArrowDown
  Then the menu opens with focus on «Истории»
  When the user presses ArrowUp twice and Enter
  Then the «Эпики» level shows
  And Escape in an open menu closes it, focus back on the button

Scenario: Each level keeps its own address
  When the user opens `<team>/backlogs/epics` directly
  Then the «Эпики» backlog shows, the button reading «Уровень: Эпики»
  And `<team>/backlogs/tasks` still answers «Страница не найдена»

Scenario: The board is named «Канбан» everywhere it is named
  Then the navigation entry reads «Канбан» and opens `<team>/boards/stories`
  And the browser tab reads «Канбан — Mustask» and the board region is
    named «Канбан»
  And «Бэклоги» offers «Показать канбан», which opens the board of the
    same team and level
  And a level with no item shows «Канбан пуст» with the hint «Новые рабочие
    элементы этого уровня появятся в колонке «Новые».» and «Новый рабочий
    элемент»
  And no text on either page reads «Доски» or «Показать доску»

Scenario: The board itself is unchanged
  When the user moves a card with «Ещё действия: <ID>» → «Переместить в
    колонку» and creates an item with «Новый элемент»
  Then both behave as mod-006's US-5 and US-6
  And no card shows a parent or children

Scenario: The address and the server are untouched by the rename
  Then the board's addresses still use the segment `boards`
  And the board's answers and error texts are those of mod-006
```
