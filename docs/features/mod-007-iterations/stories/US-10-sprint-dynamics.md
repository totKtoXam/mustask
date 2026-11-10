# US-10: Dynamics of a Closed sprint against the previous ones

- story: US-10
- depends-on: US-5
- shippable: no — US-1..US-10 ship together as `mod-007-iterations` (one L, human's scope delta 2026-11-10)
- status: pending

As an **organization member**, I want **the outcome page of a Closed sprint to show the sprint's figures against the team's previous Closed sprints, over a range I choose**, so that **I see whether the team delivers more or less than usual**.

## Acceptance criteria

Team MARS has Closed sprints 8 to 12, closed in that order, and a Planned «Спринт 13». Figures of a sprint come from its close-time snapshot (spec BR-14), after visibility (BR-9). A sprint's «закрыто» is the snapshot's done flag. Percent is closed over held, rounded half up to a whole percent; an average of counts has one decimal. "Previous" means Closed sprints of the same team closed before this one, in close order. Source: spec BR-14, BR-15; the human's request of 2026-11-10. This reads only the records of closed sprints, never item history: burndown and velocity from history stay `mod-012-iteration-metrics`'s.

| Sprint | Items | SP | Closed items | Closed SP |
|--------|-------|----|--------------|-----------|
| «Спринт 12» (this) | 10 | 40 | 6 | 25 |
| «Спринт 11» | 8 | 30 | 8 | 30 |
| «Спринт 10» | 12 | 50 | 6 | 20 |
| «Спринт 9» | 10 | 0 | 5 | 0 |
| «Спринт 8» | 0 | 0 | 0 | 0 |

```gherkin
Scenario: The page shows this sprint's figures and its previous sprints with min, average and max
  When Vera opens «Спринт 12» of MARS and chooses the last 3 closed sprints
  Then the figures of «Спринт 12» read 10 items, 40 SP, 6 closed items (60 %), 25 closed SP (63 %)
  And the previous sprints are listed with their own figures, the latest first: «Спринт 11», «Спринт 10», «Спринт 9»
  And minimum, average and maximum are shown for the closed items and for their percent, and for the closed SP and for their percent
  And closed items read min 5, average 6.3, max 8; their percent reads min 50 %, average 67 %, max 100 %
  And closed SP read min 0, average 16.7, max 30; their percent reads min 40 %, average 70 %, max 100 % (over «Спринт 11» and «Спринт 10» only: «Спринт 9» has no SP percent, next scenario)

Scenario: A sprint with no story points has no SP percent and is left out of that percent's min, average and max
  Given «Спринт 9» held 10 items of 0 SP
  When Vera opens «Спринт 12» with the last 3 sprints
  Then «Спринт 9» shows «—» for the percent of closed SP
  And the minimum, average and maximum of that percent are over «Спринт 11» (100 %) and «Спринт 10» (40 %) only
  And no figure is NaN, infinite or a server error

Scenario: A sprint that held no items counts nowhere in the percents
  Given «Спринт 8» closed with an empty snapshot
  When Vera opens «Спринт 9» with the last 3 sprints
  Then «Спринт 8» shows 0 items and 0 SP, «—» for both percents, and counts in the averages of the counts as 0 and in no percent

Scenario: The range is chosen by the user, with a default and bounds
  When Vera opens a Closed sprint without choosing a range
  Then the last 5 previous closed sprints are compared
  And the control offers 3, 5, 10 and 20, and the choice is kept in the address (`?last=10`), not stored for the user
  And an address with `?last=7`, `?last=0` or a word falls back to 5, and a range larger than the number of previous sprints compares all of them, with the control saying how many were found

Scenario: A sprint with no previous Closed sprint says so
  When Vera opens the first Closed sprint of a team
  Then the dynamics read «Нет предыдущих завершённых спринтов для сравнения.»
  And this sprint's own figures are shown, and minimum, average and maximum read «—»

Scenario: Averages use exact values, rounded only for display
  Given a team's only two previous sprints closed 1 of 6 items and 1 of 2 items
  When Vera opens a later sprint with the last 3 sprints
  Then their percents read 17 % and 50 %, and the average percent (OQ-11 A) reads 33 %, the exact mean 33.33… rounded, not 34 % from the rounded 17 and 50
  And an average of 2 and 3 closed items reads 2.5, and of 1, 1 and 2 reads 1.3

Scenario: Only the team's own Closed sprints are compared
  Given VENUS has Closed sprints with figures, and MARS's «Спринт 13» is Planned
  When Vera opens «Спринт 12» of MARS
  Then no VENUS sprint and no Planned or Active MARS sprint appears among the previous sprints

Scenario: Figures count only what the viewer sees (owed by feat-041)
  Given the items of project C are in the snapshots of «Спринт 12» and «Спринт 11», and C is «Ограниченный» with no grant for Vera
  When she opens «Спринт 12»
  Then every figure of this sprint and of each previous sprint, totals and percents included, equals what a twin organization without C gives her

Scenario: Closed sprints closed out of number order are compared by close order
  Given «Спринт 14» was closed before «Спринт 13» was started
  When Vera opens «Спринт 13» with the last 3 sprints
  Then «Спринт 14» is among them, since it closed before

Scenario: A reopened or re-estimated item never changes a past figure
  Given API of «Спринт 12» was closed at 5 SP and is later reopened and re-estimated at 8 SP
  When Vera opens the page of «Спринт 12» and of a later sprint
  Then both still show API as it was recorded

Scenario: A deleted team's sprints have no dynamics page
  When MARS is deleted and Vera opens «Спринт 12» by its address
  Then it answers «Команда не найдена»; the project's «Спринты» (US-7) lists the sprint's items and shows no dynamics

Scenario: The comparison reads a bounded number of records in a bounded time
  Given «Спринт 12» held 5 000 items and MARS has 20 previous Closed sprints of 5 000 items each, over 50 projects and 300 assignees
  When Vera opens the page with the last 20 sprints
  Then it answers in ≤ 2 s and in a number of statements that does not grow with the range, the projects or the assignees
```
