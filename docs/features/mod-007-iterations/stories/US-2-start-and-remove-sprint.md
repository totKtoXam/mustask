# US-2: A sprint is started and removed; it never goes back

- story: US-2
- depends-on: US-1
- shippable: no — starting is half of the lifecycle whose other half is the close (US-5); it ships with US-1 and US-3 as `feat-NNN-team-sprint-lifecycle`
- status: pending

As a **team planner**, I want **to start a Planned sprint and to remove one I no longer need**, so that **the team has one running sprint at a time and no dead sprints clutter the list**.

## Acceptance criteria

Setting as in US-1. MARS owns the items API of A (5 SP), integration of B (8 SP) and notifications of C (3 SP). The close is US-5's; what a sprint holds is US-4's. Source: spec BR-3, BR-8, BR-9, BR-12; doc-012 US-9.

```gherkin
Scenario: Starting a dated Planned sprint makes it the team's Active sprint
  Given MARS has no Active sprint and «Спринт 13» is Planned with 2026-11-02 to 2026-11-13
  When Pavel starts «Спринт 13»
  Then it is Active and keeps its dates and its items

Scenario: A team has at most one Active sprint
  Given MARS's «Спринт 12» is Active and its «Спринт 13» is Planned with both dates
  When Pavel starts «Спринт 13»
  Then it is refused with «У команды уже есть активный спринт.» and stays Planned
  And VENUS can still start a sprint of its own

Scenario: Two planners starting two sprints at once get exactly one Active sprint
  Given MARS has no Active sprint and two dated Planned sprints
  When two planners start the two sprints at the same moment
  Then exactly one becomes Active, the other is refused with «У команды уже есть активный спринт.», and no request ends in a server error

Scenario: Starting needs both dates, and a sprint never goes back
  Given MARS has no Active sprint
  When Pavel starts an undated Planned sprint, starts a Closed sprint, and closes a Planned or a Closed sprint
  Then they are refused with «Чтобы начать спринт, укажите обе даты.», «Начать можно только запланированный спринт.» and «Завершить можно только активный спринт.»
  And each sprint keeps its state and its dates

Scenario Outline: Only a Planned sprint that holds no item outside «Удалена» can be removed
  Given MARS's latest sprint, «Спринт 13», is <state> and holds <items>
  When Pavel removes it
  Then <result>, and every refusal reads «Удалить можно только запланированный спринт без элементов.»
  Examples:
    | state   | items                                                         | result                                                                     |
    | Planned | no item                                                       | it is gone from every list and sprint picker, and MARS's next sprint is number 14 |
    | Planned | only an item in «Удалена»                                     | it is gone, and that item has no sprint                                    |
    | Planned | integration                                                   | refused                                                                    |
    | Planned | only API, in «Закрыта»                                        | refused                                                                    |
    | Planned | only notifications, while C is hidden from him or deactivated | refused, and the refusal names no item, project or count                   |
    | Active  | no item                                                       | refused                                                                    |
    | Closed  | its completed items                                           | refused: a Closed sprint is never removed                                  |

Scenario: Removal is refused for someone who may not manage the team's sprints
  When Vera, who holds no team grant, removes or starts a Planned MARS sprint
  Then it is refused with 403 «Недостаточно прав» and the sprint stays as it was

Scenario: Two lifecycle acts on one sprint end in one order
  When two planners close MARS's Active «Спринт 12» at the same moment, or one starts a Planned sprint while another removes it
  Then one completes and the other is refused with the refusal its state now gives («Завершить можно только активный спринт.», «Спринт не найден.» or «Удалить можно только запланированный спринт без элементов.»), and no request ends in a server error

Scenario: A removal and a save into the sprint end in one order
  Given «Спринт 13» is Planned and empty
  When Vera sets an item's sprint to «Спринт 13» and Pavel removes «Спринт 13» at the same moment
  Then her save is refused with «Спринт не найден.», or it saves first and the removal is refused with «Удалить можно только запланированный спринт без элементов.», and no request ends in a server error
```
