# US-3: Team–project relations in both directions

- story: US-3
- depends-on: US-2
- shippable: no — it annotates the diagram of US-2
- status: pending

As a **member of an organization**, I want **to see which teams work on a project and which projects each team works on**, so that **I understand who is responsible for what without opening each object**.

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

Scenario: What choosing emphasises
  When I choose a node of each choosable type
  Then the emphasised nodes and lines, and the summary, are:
    | chosen | emphasised | summary |
    | department | itself, its directly linked teams, the lines between | «Команды департамента: …», or «Команды департамента: нет» |
    | active project | itself, its teams, the lines between | «Команды проекта: …», or «Команды проекта: нет» |
    | team | itself, its projects, its directly linked departments, the lines between | «Проекты команды: …» and «Департаменты команды: …», each «нет» when empty |
  And everything else is dimmed, still readable, and a project's sub-areas stay as they are
  And a department's projects are not emphasised (BR-SD-7)

Scenario: From a project to its teams
  Given project A lists MARS and VENUS, project B lists MARS
  When I choose A (select, not open)
  Then A, MARS and VENUS are emphasised, everything else is dimmed
  And a text summary states «Команды проекта: MARS, VENUS»

Scenario: From a team to its projects
  When I choose MARS
  Then MARS, A and B are emphasised, everything else is dimmed
  And a text summary states «Проекты команды: A, B»

Scenario: Choosing reveals what it emphasises
  Given A is inside a collapsed branch
  When I choose MARS, a team of A
  Then the branches that hide emphasised nodes are expanded so that A is seen emphasised

Scenario: Clearing the emphasis
  Given MARS is chosen
  When I choose it again, press Escape, or choose «Сбросить»
  Then nothing is emphasised and the summary is gone

Scenario: A team's departments
  Given team JUPITER is linked directly to department «Логистика»
  When I open the diagram
  Then a line of the same relation style joins «Логистика» to JUPITER
  And choosing «Логистика» emphasises JUPITER and its other directly linked teams, per the table above

Scenario: Many relations
  Given the organization has more relation lines than the threshold of OQ-6
  When I open the diagram
  Then the teams and the hierarchy are drawn, and the relation lines are drawn only for the chosen node
  And a note «Линии связей показаны для выбранного узла» is shown while nothing is chosen
  And below the threshold every line is drawn (OQ-6 A)

Scenario: A team linked to a project only
  Given team MARS works on project A in «Логистика» and has no direct department link
  When I open the diagram
  Then MARS is joined to A and not to «Логистика»

Scenario: A team with no relation
  Given team ATLAS is linked to no project and no department
  When I open the diagram
  Then ATLAS is listed in the group «Без проектов и департаментов» (OQ-4 A)
  And the group stands after the hierarchy, its teams in the order of the offers (by name)
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

Scenario: Read-only
  When I use the diagram with any role
  Then no command to link, unlink, move or edit is offered

Scenario: Volume
  Given 200 projects and 200 teams
  When I open the diagram and choose a team
  Then no read is sent and the layout is not rebuilt (checked by a test)
  And the emphasis changes in under 200 ms, measured on the stand with a seeded organization of that size in a named browser at stage 6 (not in jsdom)
```
