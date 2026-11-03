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

Scenario: From a project to its teams
  Given project A lists MARS and VENUS, project B lists MARS
  When I choose A (select, not open)
  Then A, MARS and VENUS are emphasised, everything else is dimmed
  And a text summary states «Команды проекта: MARS, VENUS»

Scenario: From a team to its projects
  When I choose MARS
  Then MARS, A and B are emphasised, everything else is dimmed
  And a text summary states «Проекты команды: A, B»

Scenario: Clearing the emphasis
  Given MARS is chosen
  When I choose it again, press Escape, or choose «Сбросить»
  Then nothing is emphasised and the summary is gone

Scenario: A team's departments
  Given team JUPITER is linked directly to department «Логистика»
  When I open the diagram
  Then a line of the same relation style joins «Логистика» to JUPITER
  And choosing «Логистика» emphasises its directly linked teams too

Scenario: A team linked to a project only
  Given team MARS works on project A in «Логистика» and has no direct department link
  When I open the diagram
  Then MARS is joined to A and not to «Логистика»

Scenario: A team with no relation
  Given team ATLAS is linked to no project and no department
  When I open the diagram
  Then ATLAS is listed in the group «Без проектов и департаментов» (OQ-4 A)

Scenario: A team opens its page
  When I activate a team's name
  Then the team's page opens

Scenario: Deactivated project, deactivated team
  Given project C is deactivated and team OLD is deactivated
  When I open the diagram
  Then OLD is not drawn and C is drawn tagged with no team lines (BR-SD-4, BR-SD-7)

Scenario: Selection by keyboard
  When I focus a project or team and press Enter or Space
  Then it is chosen as by a click, and the summary is announced to a screen reader

Scenario: Read-only
  When I use the diagram with any role
  Then no command to link, unlink, move or edit is offered

Scenario: Volume
  Given 200 projects and 200 teams
  When I open the diagram and choose a team
  Then the emphasis changes in under 200 ms and no read is sent
```
