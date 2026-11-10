# US-8: The area overview shows linked teams' Active sprints

- story: US-8
- depends-on: US-6
- shippable: no — it needs the area overview (`feat-042-area-board-and-overview`) to put the figure on; if OQ-2 moves it there, this story moves with it
- status: pending

As an **organization member**, I want **an area's «Обзор» to show the Active sprint of each team linked to the area**, so that **I see what the area's teams are running without opening each team**.

## Acceptance criteria

Area «Логистика» is linked to MARS and VENUS and to no project; VENUS has no Active sprint; C is «Ограниченный» with no grant for Vera. MARS's Active «Спринт 12» holds API in «Закрыта», and integration and notifications in «Активна». Source: spec BR-9, BR-10; doc-012 BR-MX-17, US-12. "Area" is the department of the typed tree (ADR-0031); how a team reaches it after doc-016 is OQ-1.

```gherkin
Scenario: The overview shows each linked team's Active sprint, counted over visible items
  When Vera opens the overview of «Логистика»
  Then it shows MARS's «Спринт 12» with 2 items, 1 of them in the Completed category, whatever their project
  And it shows VENUS with «У команды нет активного спринта.»
  And the figure is the one MARS's sprint board gives Vera at «Истории»

Scenario: A team not linked to the area is not shown
  Given JUPITER has an Active sprint and no link to «Логистика»
  When Vera opens the overview
  Then JUPITER and its sprint are not shown

Scenario: A sprint's figure follows a close
  When Pavel closes «Спринт 12» choosing no sprint
  Then Vera's next read of the overview shows MARS with «У команды нет активного спринта.»

Scenario: An area with no linked team shows no sprint block
  Given «Транспорт» has no team link
  When Vera opens its overview
  Then no sprint block is shown, and nothing else changes

Scenario: Only the teams linked to the department itself are shown
  Given team JUPITER, with an Active sprint, is linked to a plain «Область» below a project node of «Логистика» and not to «Логистика»
  When Vera opens the overview of «Логистика»
  Then JUPITER's sprint is not shown, and it shows on that «Область»'s own overview
```

> The first draft had a «Child areas follow the switch» scenario. It is
> withdrawn: in the typed tree (ADR-0031) a department holds project nodes and
> a plain «Область» stands only below a project node, and «Включая дочерние
> области» does not exist in the code yet. Whether the overview aggregates
> teams of deeper nodes is feat-042's to decide with its own switch; this story
> then follows (OQ-2).
