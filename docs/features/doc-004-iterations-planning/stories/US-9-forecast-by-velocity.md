# US-9 (release 3): A member forecasts the backlog by velocity

- story: US-9
- depends-on: US-8
- shippable: no — release-3 ground, not built by any MVP task; delivered by `feat-012-backlog-forecast`
- status: pending

As a **member**, I want **the product backlog to show where the team's
iterations would land if it keeps its current velocity**, so that **I can
answer "when will this item be done" without building a plan by hand
(research § Backlogs & sprints)**.

## Acceptance criteria

```gherkin
Scenario: Forecast lines on the ordered backlog
  Given a team with a velocity (US-8) and an ordered backlog of items
    estimated in «Оценка в пунктах»
  When a member turns «Прогноз» on
  Then a line is drawn between backlog items marking where each of the
    team's next iterations would end at the given velocity
  And the lines follow the backlog order, never reorder it (doc-001 BR-12)

Scenario: The velocity used is visible and adjustable
  Given the forecast is on
  When a member changes the velocity value used for the forecast
  Then the lines move accordingly
  And nothing is written to any work item or team setting — the forecast is
    a view (the read-only principle of BR-M5-17)

Scenario: Unestimated items break the forecast honestly
  Given a backlog where some items have no estimate
  When the forecast is on
  Then those items are shown as not forecastable, and they are not counted
    as zero

Scenario: No velocity yet
  Given a team with no completed iteration
  When a member turns the forecast on
  Then an explicit state says a velocity is needed and offers to enter one
    by hand, instead of drawing lines from nothing

Scenario: The forecast respects the team's iterations
  Given a team that uses three iterations, two of them in the future
  When the forecast is on
  Then the lines are labelled with those iterations and stop when the team's
    iterations run out (BR-M5-5)
```
