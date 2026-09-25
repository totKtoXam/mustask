# US-6: MS-6 «Спринты: оценки, доска задач, ёмкость, метрики» (release 2 complete)

- story: US-6
- depends-on: US-5
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-6 «Спринты: оценки, доска задач,
ёмкость, метрики»**, so that **a team can estimate its work, run the sprint
day to day on a taskboard, check it against its capacity, and see from its
burndown and velocity whether it is on track — release 2 is then complete**.

Delivered by `feat-NNN-work-item-estimation-fields`, `mod-011-sprint-views`,
`feat-011-sprint-capacity` and `mod-012-iteration-metrics`, together with
every line `doc-NNN-release-2-scope` queues.

## Acceptance criteria

```gherkin
Scenario: Demo — an estimated sprint on the taskboard, with capacity and charts
  Given «Спринт 2» of the team «Платформа», dated and currently running
  And in it, stories with «Оценка в пунктах» and child «Задача» items with
    «Исходная оценка» and «Оставшаяся работа» (D-15)
  When a member opens the sprint backlog of «Спринт 2»
  Then it lists the team's items that point at «Спринт 2», with their
    «Оценка в пунктах»
  When the member enters their «Ёмкость» for «Спринт 2», for one «Вид работ»,
    and one day in «Дни отсутствия»
  Then the sprint shows the team's capacity next to its «Оставшаяся работа»
  When the member sets a «Задача»'s «Оставшаяся работа» to 0 and moves it to
    «Закрыта» on the «Доска задач»
  Then today's point of «Диаграмма сгорания» drops by the hours it had
  And no earlier point of «Диаграмма сгорания» changes
  When, after the sprint's finish date, the member opens «Скорость»
  Then «Спринт 2» shows the «Оценка в пунктах» of the items that reached a
    Completed-category state by that date
  And release 2 is complete once every line doc-NNN-release-2-scope queued is
    merged as well

Scenario: Guard — no estimate, no fake chart
  Given a dated sprint whose items carry no estimate
  When a member opens «Диаграмма сгорания»
  Then the chart says the estimates are missing, instead of drawing a flat
    line at zero
```
