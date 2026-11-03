# US-3: Team–project relations in both directions, three display modes

- story: US-3
- depends-on: US-2
- shippable: no — it annotates the diagram of US-2
- status: pending

As a **member of an organization**, I want **to see which teams work on a project and which projects each team works on, and to choose how choosing a node shows the rest**, so that **I understand who is responsible for what without opening each object, in the way that reads best to me**.

## Acceptance criteria

```gherkin
Scenario: Teams are drawn
  Given teams MARS and VENUS exist and project A lists MARS and VENUS
  When I open the diagram
  Then both teams are drawn, each with its name
  And a line joins A to MARS and A to VENUS, distinguishable from the nesting joins by style and not by colour alone

Scenario: Choosing is separate from opening
  When I click the body of a department, an active project or a team, or focus it and press Space
  Then it is chosen and nothing opens
  And a sub-area, a deactivated project and its sub-areas cannot be chosen (they have no relations in the data)

Scenario: What is related to a chosen node
  When I choose a node of each choosable type
  Then the related nodes and the summary are, in every display mode:
    | chosen | related | summary |
    | department | itself, its directly linked teams | «Команды департамента: …», or «Команды департамента: нет» |
    | active project | itself, its teams | «Команды проекта: …», or «Команды проекта: нет» |
    | team | itself, its projects, its directly linked departments | «Проекты команды: …» and «Департаменты команды: …», each «нет» when empty |
  And a department's projects are not related to it (BR-SD-7)
  And the summary is announced to a screen reader and holds the full lists

Scenario: The display mode control
  When I open «Показ при выборе»
  Then it offers «Затемнение», «Только связанные», «Подсветка», each with a one-line hint, the current one marked
  And I move between them with the arrow keys, confirm with Enter, and close the list with Esc
  And the trigger reads «Показ при выборе: <режим>», «Затемнение» when I never chose
  And a mode I pick applies at once to the node already chosen and to every later choice

Scenario: «Затемнение» emphasises and dims
  Given the mode is «Затемнение»
  When I choose project A, which lists MARS and VENUS, while project B lists MARS
  Then A, MARS and VENUS and the lines between them are emphasised
  And everything else is dimmed, still readable, and A's sub-areas stay as they are
  And the lines of other nodes are faded
  And no node is added or removed

Scenario: «Подсветка» recolours and dims nothing
  Given the mode is «Подсветка»
  When I choose team MARS, which works on A and B
  Then MARS, A and B get an accent frame and a stronger tint, and their lines are thicker in the accent style
  And the other lines turn neutral grey
  And no node is dimmed, hidden or removed

Scenario: «Только связанные» shows the chosen node and its relations
  Given the mode is «Только связанные»
  When I choose project A in department «Логистика», with teams MARS and VENUS
  Then only A, its sub-areas, «Логистика» and A's other ancestors, MARS and VENUS are shown
  And unrelated rows and teams are removed and the picture closes up
  And only the chosen node's lines are drawn
  And the summary also says «Показаны только выбранный узел и связанные с ним.»
  And the ancestors are shown open without changing the expansion I set

Scenario: «Только связанные» restores on clearing
  Given the mode is «Только связанные» and A is chosen
  When I clear the choice
  Then the full picture returns, with my own expansion as it was before the choice

Scenario: A team of the top group in «Только связанные»
  Given the mode is «Только связанные» and team ATLAS from «Без проектов и департаментов» is chosen
  Then ATLAS alone is shown, and the hierarchy area says «Нет связанных проектов и департаментов»

Scenario: Choosing reveals what it relates to
  Given A is inside a collapsed branch and the mode is «Затемнение» or «Подсветка»
  When I choose MARS, a team of A
  Then the branches that hide related nodes are expanded so that A is seen emphasised

Scenario: Clearing the choice
  Given MARS is chosen, in any mode
  When I choose it again, press Escape, or choose «Сбросить»
  Then nothing is emphasised, dimmed or removed, the lines return to their state without a choice, and the summary is gone

Scenario: A team's departments
  Given team JUPITER is linked directly to department «Логистика»
  When I open the diagram
  Then a line of the same relation style joins «Логистика» to JUPITER
  And choosing «Логистика» relates it to JUPITER and its other directly linked teams, per the table above

Scenario: Many relations
  Given the organization has more than 150 relation lines (project–team plus department–team pairs, counted whatever is expanded)
  When I open the diagram
  Then the teams and the hierarchy are drawn, and the relation lines are drawn only for the chosen node, in every mode
  And a note «Линии связей показаны для выбранного узла» is shown while nothing is chosen
  And with 150 lines or fewer every line is drawn

Scenario: A team linked to a project only
  Given team MARS works on project A in «Логистика» and has no direct department link
  When I open the diagram
  Then MARS is joined to A and not to «Логистика»

Scenario: A team with no relation
  Given team ATLAS is linked to no project and no department
  When I open the diagram
  Then ATLAS stands in the group «Без проектов и департаментов» (US-2), has no lines, and is choosable
  And choosing ATLAS shows «Проекты команды: нет» and «Департаменты команды: нет»

Scenario: A team opens its page
  When I click a team's name, or focus the team and press Enter
  Then the team's page opens

Scenario: Deactivated project, deactivated team
  Given project C is deactivated and team OLD is deactivated
  When I open the diagram
  Then OLD is not drawn and C is drawn tagged with no team lines (BR-SD-4, BR-SD-7)

Scenario: Selection by keyboard
  When I focus a choosable node and press Space
  Then it is chosen as by a click on its body, and the summary is announced to a screen reader
  And Enter on the same node opens its page, never chooses it

Scenario: Search and «Только связанные»
  Given the mode is «Только связанные» and A is chosen
  When I find, with «Найти узел», a node the mode has removed and press Enter
  Then the toast «Узел скрыт режимом «Только связанные»» is shown and focus does not move to it

Scenario: Read-only
  When I use the diagram with any role
  Then no command to link, unlink, move or edit is offered

Scenario: Volume
  Given 200 projects and 200 teams
  When I open the diagram and choose a team in «Затемнение» or «Подсветка»
  Then no read is sent and the layout is not rebuilt (checked by a test)
  And in every mode, «Только связанные» included, the picture changes in under 200 ms, measured on the stand with a seeded organization of that size in a named browser at stage 6 (not in jsdom)
  And in «Только связанные» no read is sent either
```
