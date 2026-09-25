# US-8: MS-8 «Прогноз, планы поставки, расширенные запросы» (release 3 complete)

- story: US-8
- depends-on: US-7
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-8 «Прогноз, планы поставки,
расширенные запросы»**, so that **the organization can see:**
- **when its backlog is likely to be done;**
- **how its teams' iterations line up across projects;**
- **what its work items looked like over time.**

Release 3 is then complete, with process customization aside (D-21).

Delivered by `feat-012-backlog-forecast`, `mod-013-delivery-plans` and
`mod-010-query-hierarchy-operators`.

## Acceptance criteria

```gherkin
Scenario: Demo — forecast, delivery plan and history-aware queries
  Given the team «Платформа» with three finished sprints and an estimated
    «Истории» backlog
  And a second project of the same organization with a team of its own
  When a member opens «Прогноз» on the «Истории» backlog of «Платформа»
  Then the backlog is divided among the coming sprints by the team's velocity
  When a member creates a delivery plan with «Платформа» and the other
    project's team
  Then the plan shows both teams' iterations, with their items, on one timeline
  When a member builds a tree query with the clause Статус Was Ever
    «Активна» and the clause Дата изменения = @Today
  Then it returns the items that were ever «Активна» and were changed today,
    each with its children
  And the demos of MS-5..MS-7 still pass on the same stand

Scenario: Guard — a delivery plan stays inside its organization
  Given a delivery plan of «Пример организации»
  When a member picks the teams to add to the plan
  Then only teams of «Пример организации» are offered
  And a request naming a team of another organization is refused as not found
```
