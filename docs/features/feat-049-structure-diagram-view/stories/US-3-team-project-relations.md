# US-3: Team–project relations in both directions, branch emphasis, three display modes

- story: US-3
- depends-on: US-2
- shippable: no — it annotates the diagram of US-2
- status: pending

As a **member of an organization**, I want **to see which teams work on a project and which projects each team works on, to see a whole branch when I choose its parent, and to choose how choosing a node shows the rest**, so that **I understand who is responsible for what without opening each object, in the way that reads best to me**.

## Acceptance criteria

```gherkin
Scenario: Relation lines are drawn
  Given teams MARS and VENUS exist and project A lists MARS and VENUS
  When I open the diagram
  Then both teams are drawn as cards (US-2), and a dashed line joins A to MARS and A to VENUS
  And the line differs from the nesting joins by style and not by colour alone (BR-SD-17)

Scenario: Which copy a line uses
  Given team JUPITER is linked to «Логистика» and «Финансы» and works on project WH of «Логистика» and project BUD of «Финансы»
  When I open the diagram
  Then the line to WH starts at JUPITER's copy under «Логистика», the line to BUD at the copy under «Финансы»
  And a team working on a project whose department it is not linked to uses its first copy, or its card in «Без департамента»

Scenario: Choosing is separate from opening
  When I click the body of a department, an active project or a team card, or focus it and press Space
  Then it is chosen and nothing opens
  And a sub-area, a deactivated project and its sub-areas cannot be chosen (they have no relations in the data)

Scenario: What is emphasised by a chosen node
  When I choose a node of each choosable type
  Then the emphasised cards (besides the chosen one) and the summary are, in every display mode:
    | chosen | emphasised | summary |
    | department | every project and sub-area under it, every team under it with all their copies, the project–team lines of that branch, and the far ends of those lines | «Ветка департамента: проектов N, областей N, команд N. Связано вне ветки: …», or «Связано вне ветки: нет» |
    | active project | its sub-areas at any depth, the teams working on it with all their copies, those lines | «Ветка проекта: областей N. Команды проекта: …», or «Команды проекта: нет» |
    | team (any copy) | all its copies, its projects with their lines, its departments | «Проекты команды: …. Департаменты команды: …», each «нет» when empty |
  And the summary is announced to a screen reader and holds the full lists (BR-SD-18)

Scenario: Choosing any copy of a team
  Given team JUPITER stands under «Логистика» and «Финансы»
  When I choose the copy under «Финансы»
  Then both copies are emphasised, with the dash-dot join between them, and the summary lists both departments

Scenario: The display mode control
  When I open the menu of the icon button «Показ при выборе»
  Then it offers «Затемнение», «Только связанные», «Подсветка», each with a one-line hint, the current one marked
  And I move between them with the arrow keys, confirm with Enter, and close the list with Esc
  And the button's tooltip and accessible name read «Показ при выборе: <режим>», «Затемнение» when I never chose (BR-SD-19)
  And a mode I pick applies at once to the node already chosen and to every later choice

Scenario: «Затемнение» emphasises the branch and dims the rest
  Given the mode is «Затемнение»
  When I choose department «Логистика» with project A, which lists MARS, and team VENUS under it, while department «Финансы» holds an unrelated project
  Then «Логистика», A with its sub-areas, MARS, VENUS and the lines of that branch are emphasised
  And everything else is dimmed, still readable, and the lines of other nodes are faded
  And no card is added or removed

Scenario: «Подсветка» recolours and dims nothing
  Given the mode is «Подсветка»
  When I choose team MARS, which works on A and B
  Then MARS and its copies, A and B get an accent frame and a stronger tint, and their lines are thicker in the accent style
  And the other lines turn neutral grey
  And no card is dimmed, hidden or removed

Scenario: «Только связанные» keeps the branch and its relations
  Given the mode is «Только связанные»
  When I choose project A in department «Логистика», with sub-area «Склад» and teams MARS and VENUS
  Then only A, its sub-areas, the teams working on it, and the ancestors of those cards (departments of the teams' copies included) are shown
  And a team's copy shows only under a kept department
  And all other cards are removed and the picture closes up
  And only the chosen branch's lines are drawn
  And the summary also says «Показаны только ветка выбранного узла и связанные с ней.»
  And the ancestors are shown open without changing the expansion I set

Scenario: «Только связанные» restores on clearing
  Given the mode is «Только связанные» and A is chosen
  When I clear the choice
  Then the full picture returns, with my own expansion as it was before the choice

Scenario: A team with no relations in «Только связанные»
  Given the mode is «Только связанные» and team ATLAS from «Без департамента», working on no project, is chosen
  Then ATLAS alone is shown

Scenario: Choosing reveals what it relates to
  Given A is inside a collapsed branch and the mode is «Затемнение» or «Подсветка»
  When I choose MARS, a team of A
  Then the branches, and a folded section, that hide emphasised cards are expanded so that A is seen emphasised

Scenario: Clearing the choice
  Given MARS is chosen, in any mode
  When I choose it again, press Escape, or choose «Сбросить»
  Then nothing is emphasised, dimmed or removed, the lines return to their state without a choice, the summary is gone and «Выбор сброшен» is announced

Scenario: A team's departments
  Given team JUPITER is linked directly to department «Логистика»
  When I open the diagram
  Then JUPITER stands inside «Логистика» (US-2) and no department–team line is drawn
  And choosing «Логистика» emphasises JUPITER as part of the branch, per the table above

Scenario: Many relations
  Given the organization has more than 150 relation lines (project–team pairs plus copy joins, counted whatever is expanded)
  When I open the diagram
  Then the cards are drawn, and while nothing is chosen no relation line is drawn, in every mode
  And a note «Линии связей показаны для выбранного узла» is shown
  And when a node is chosen only the lines of its chosen branch are drawn
  And with 150 lines or fewer every line is drawn (BR-SD-17)

Scenario: A team linked to a project only
  Given team MARS works on project A in «Логистика» and has no direct department link
  When I open the diagram
  Then MARS stands in «Без департамента», joined by a dashed line to A and not placed under «Логистика»

Scenario: A team with no relation
  Given team ATLAS is linked to no project and no department
  When I open the diagram
  Then ATLAS stands in «Без департамента» (US-2), has no lines, and is choosable
  And choosing ATLAS shows «Проекты команды: нет» and «Департаменты команды: нет»

Scenario: A team opens its page
  When I click a team card's name, or focus the card and press Enter
  Then the team's page opens

Scenario: Deactivated project, deactivated team
  Given project C is deactivated and team OLD is deactivated
  When I open the diagram
  Then OLD is not drawn and C is drawn tagged with no team lines (BR-SD-4, BR-SD-16)

Scenario: Selection by keyboard
  When I focus a choosable card and press Space
  Then it is chosen as by a click on its body, and the summary is announced to a screen reader
  And Enter on the same card opens its page, never chooses it

Scenario: Search and «Только связанные»
  Given the mode is «Только связанные» and A is chosen
  When I find, with «Найти узел», a card the mode has removed and press Enter
  Then the toast «Узел скрыт режимом «Только связанные»» is shown and focus does not move to it

Scenario: Emphasis in the vertical layout and under a level choice
  Given the layout is «Вертикально», or levels are chosen
  When I choose a node in any mode
  Then the same nodes are related and the same summary is announced as in the horizontal layout with every level shown
  And emphasis, dimming and the removal in «Только связанные» apply to the cards that are drawn, a related card on a hidden level is not drawn and its line goes dotted to the nearest visible ancestor (US-2)
  And a chosen card keeps its level outline and adds a ring

Scenario: Read-only
  When I use the diagram with any role
  Then no command to link, unlink, move or edit is offered

Scenario: Volume
  Given 200 projects and 200 teams
  When I open the diagram and choose a team in «Затемнение» or «Подсветка»
  Then no read is sent and the layout is not rebuilt (checked by a test)
  And choosing a node changes the picture in under 200 ms in every mode, «Только связанные» included, in both layouts, measured on the stand with a seeded organization of that size in a named browser at stage 6 (not in jsdom)
  And in «Только связанные» no read is sent either, and switching the layout or the level choice sends none (US-2)
  And switching the layout or the level choice at that size changes the picture within 200 ms, measured the same way
```
