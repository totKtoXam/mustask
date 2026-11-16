# US-10: Dynamics of a Closed sprint against the team's other sprints

- story: US-10
- depends-on: US-5
- shippable: no — US-1..US-10 ship together as `mod-007-iterations` (one L, human's scope delta 2026-11-10)
- status: pending

As an **organization member**, I want **the outcome page of a Closed sprint to show the sprint's figures against the team's other Closed sprints whose end dates fall in a date range I choose**, so that **I see whether the team delivers more or less than usual**.

## Acceptance criteria

Team MARS has Closed sprints 8 to 12 and a Planned «Спринт 13». Sprint dates: 8: 2026-09-07 to 09-18; 9: 09-21 to 10-02; 10: 10-05 to 10-16; 11: 10-19 to 10-30; 12 (this): 11-02 to 11-13. Figures of a sprint come from its close-time snapshot (spec BR-14), after visibility (BR-9), at one level, default «Истории» (OQ-14; the level control changes every figure on the page and `?level=` keeps it). A sprint's «закрыто» is the snapshot's done flag. Percent is closed over held, «—» when held is 0; percents and averages use exact values and round only for display (a percent to a whole percent, an average of counts to one decimal, half up). The range is a pair of dates, `?from=YYYY-MM-DD&to=YYYY-MM-DD`: it selects the team's Closed sprints, other than this one, whose end date lies in [from, to], both inclusive. Source: spec BR-14, BR-15; the human's request and answer of 2026-11-10. Only records of closed sprints are read, never item history: burndown and velocity from history stay `mod-012-iteration-metrics`'s.

| Sprint (end date) | Items | SP | Closed items | Closed SP |
|-------------------|-------|----|--------------|-----------|
| «Спринт 12» (this) | 10 | 40 | 6 | 25 |
| «Спринт 11» (10-30) | 8 | 30 | 8 | 30 |
| «Спринт 10» (10-16) | 12 | 50 | 6 | 20 |
| «Спринт 9» (10-02) | 10 | 0 | 5 | 0 |
| «Спринт 8» (09-18) | 0 | 0 | 0 | 0 |

```gherkin
Scenario: A date range selects the Closed sprints ending inside it, and the page shows min, average and max
  When Vera opens «Спринт 12» of MARS with `from=2026-09-21&to=2026-10-30`
  Then the figures of «Спринт 12» read 10 items, 40 SP, 6 closed items (60 %), 25 closed SP (63 %)
  And the sprints of the range are listed with their own figures, the latest end date first: «Спринт 11», «Спринт 10», «Спринт 9»; «Спринт 8» is not
  And minimum, average and maximum are shown for the closed items and for their percent, and for the closed SP and for their percent
  And closed items read min 5, average 6,3, max 8; their percent reads min 50 %, average 67 %, max 100 %
  And closed SP read min 0, average 16,7, max 30; their percent reads min 40 %, average 70 %, max 100 % (over «Спринт 11» and «Спринт 10» only: «Спринт 9» has no SP percent, next scenario)
  And the control reads the range and "3 спринта"

Scenario: The default range is the 90 days before this sprint's start
  When Vera opens «Спринт 12» without a range
  Then the range is 2026-08-04 to 2026-11-01, its dates shown in the control, and it selects «Спринт 11», «Спринт 10», «Спринт 9» and «Спринт 8»
  And the choice is kept in the address, not stored for the user

Scenario: The range ends are inclusive, and this sprint is never in its own baseline
  When Vera opens «Спринт 12» with `to=2026-10-30`, and then with `to=2026-10-29`, from 2026-09-01
  Then «Спринт 11», which ended on 2026-10-30, is in the first range and not in the second
  And with `from=2026-11-13&to=2026-11-13`, which holds «Спринт 12»'s own end date, no sprint is selected and «Спринт 12» is not compared with itself

Scenario: A range is a pair, with finish not before start, and an unusable address falls back
  When Vera sets the finish before the start in the control
  Then it is refused with «Дата окончания не может быть раньше даты начала.» and the page keeps its range
  And an address with one date only, a date that is not a date, or `to` before `from`, opens with the default range

Scenario: A range that selects no sprint says so
  When Vera opens «Спринт 12» with `from=2026-01-01&to=2026-01-31`
  Then the dynamics read «В выбранном диапазоне нет завершённых спринтов.»
  And this sprint's own figures are shown, minimum, average and maximum read «—», and the control stays to change the range

Scenario: A range selecting more than 20 sprints reads the 20 latest and says so
  Given MARS has 30 other Closed sprints ending in the range
  When Vera opens the page
  Then the 20 with the latest end dates are listed and counted, and it reads «Показаны последние 20 из 30 спринтов диапазона.»

Scenario: A sprint with no story points has no SP percent and is left out of that percent's min, average and max
  Given «Спринт 9» held 10 items of 0 SP
  When Vera opens «Спринт 12» with the range of the first scenario
  Then «Спринт 9» shows «—» for the percent of closed SP
  And the minimum, average and maximum of that percent are over «Спринт 11» (100 %) and «Спринт 10» (40 %) only
  And no figure is NaN, infinite or a server error

Scenario: A sprint that held no items counts nowhere in the percents
  Given «Спринт 8» closed with an empty snapshot
  When Vera opens «Спринт 12» with `from=2026-09-01&to=2026-10-02`
  Then «Спринт 8» shows 0 items and 0 SP, «—» for both percents, and counts in the averages of the counts as 0 and in no percent

Scenario: Averages use exact values, rounded only for display
  Given two sprints in the range closed 1 of 6 items and 1 of 2 items
  When Vera opens a later sprint with that range
  Then their percents read 17 % and 50 %, and the average percent reads 33 %, the exact mean 33.33… rounded, not 34 % from the rounded 17 and 50
  And an average of 2 and 3 closed items reads 2,5, and of 1, 1 and 2 reads 1,3

Scenario: The level changes every figure
  Given «Спринт 12» holds the feature «Платежи» of 13 SP beside its stories
  When Vera opens the page, and then chooses «Фичи»
  Then the first read counts stories only, as the table above, and the second counts «Платежи» alone, in this sprint and in every sprint of the range
  And the outcome list below stays whole

Scenario: Only the team's own Closed sprints are compared
  Given VENUS has Closed sprints ending in the range, and MARS's «Спринт 13» is Planned
  When Vera opens «Спринт 12» of MARS
  Then no VENUS sprint and no Planned or Active MARS sprint appears among the sprints of the range

Scenario: Figures count only what the viewer sees (owed by feat-041)
  Given the items of project C are in the snapshots of «Спринт 12» and «Спринт 11», and C is «Ограниченный» with no grant for Vera
  When she opens «Спринт 12»
  Then every figure of this sprint and of each sprint of the range, totals and percents included, equals what a twin organization without C gives her

Scenario: A reopened, re-estimated or purged item never changes a past figure
  Given API of «Спринт 12» was closed at 5 SP and is later reopened and re-estimated at 8 SP, and another item is purged (mod-005)
  When Vera opens the page of «Спринт 12» and of a later sprint
  Then both still show API as recorded, and the figures still count the purged item (OQ-15 A)

Scenario: A deleted team's sprints have no dynamics page
  When MARS is deleted and Vera opens «Спринт 12» by its address
  Then it answers «Команда не найдена»; the project's «Спринты» (US-7) lists the sprint's items and shows no dynamics

Scenario: The comparison reads a bounded number of records in a bounded time
  Given «Спринт 12» held 5 000 items and MARS has 20 other Closed sprints of 5 000 items each in the range, over 50 projects and 300 assignees
  When Vera opens the page
  Then it answers in ≤ 2 s and in a number of statements that does not grow with the range, the projects or the assignees
```

<!-- 2026-11-12: decimals written with a comma («6,3»), the human's answer D-10 B on design.md; no logic changed. -->
