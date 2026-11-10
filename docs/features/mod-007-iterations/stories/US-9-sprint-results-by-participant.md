# US-9: Results of a Closed sprint by participant

- story: US-9
- depends-on: US-5, US-10
- shippable: no — US-1..US-10 ship together as `mod-007-iterations` (one L, human's scope delta 2026-11-10)
- status: pending

As an **organization member**, I want **the outcome page of a Closed sprint to show, for each person who held items in it, what they had and what they closed, against their own earlier sprints**, so that **the team sees who carried what and how each person's delivery compares with their usual**.

## Acceptance criteria

Team MARS, Closed «Спринт 12». Figures come from the close-time snapshot (spec BR-14), after visibility (BR-9); an item belongs to the person who was its assignee at the close. «Закрыто» is the snapshot's done flag (Completed category at the close). Items without story points count 0 SP. Percent is closed over held, rounded half up to a whole percent, «—» when held is 0. The range of previous sprints is US-10's (`?last=N`, default 5), the same control. Source: spec BR-14, BR-16; the human's request of 2026-11-10.

The snapshot of «Спринт 12»: API (5 SP, Анна Иванова, done), integration (8 SP, Анна Иванова, moved), notifications (3 SP, Борис Петров, done), a bug (no story points, no assignee, moved). Previous sprints, in the range: «Спринт 11» — Анна 3 items of 10 SP, 3 closed (10 SP); Борис 2 items of 6 SP, 2 closed; «Спринт 10» — Анна 4 items of 20 SP, 1 closed (5 SP); «Спринт 9» — Борис only.

```gherkin
Scenario: Each participant's figures, with a «Без исполнителя» row and a totals row
  When Vera opens «Спринт 12» of MARS
  Then «Итоги по участникам» lists, by full name, then «Без исполнителя» last, then the row «Итого»:
    | Участник          | Элементов | SP | Закрыто SP | % SP | Закрыто элементов | % элементов |
    | Анна Иванова      | 2         | 13 | 5          | 38 % | 1                 | 50 %        |
    | Борис Петров      | 1         | 3  | 3          | 100 %| 1                 | 100 %       |
    | Без исполнителя   | 1         | 0  | 0          | —    | 0                 | 0 %         |
    | Итого             | 4         | 16 | 8          | 50 % | 2                 | 50 %        |
  And the totals equal the sums of the rows and the figures of this sprint on US-10

Scenario: Dynamics of a participant against the range, min, average and max
  When Vera opens «Спринт 12» with the last 3 sprints
  Then Анна Иванова's row shows, over the sprints in the range in which she held items («Спринт 11» and «Спринт 10», "2 из 3"):
    | Показатель         | Мин  | Среднее | Макс |
    | Закрыто элементов  | 1    | 2.0     | 3    |
    | % элементов        | 25 % | 63 %    | 100 % |
    | Закрыто SP         | 5    | 7.5     | 10   |
    | % SP               | 25 % | 63 %    | 100 % |
  And a sprint in which a person held nothing is left out of that person's minimum, average and maximum: «Спринт 9» is not counted for Анна
  And the row «Без исполнителя» and the row «Итого» show the same dynamics over their own sprints

Scenario: A participant with no previous sprint in the range
  Given Борис's only earlier sprint is outside the range
  When Vera opens «Спринт 12» with the last 3 sprints
  Then his dynamics read «Нет данных для сравнения» and his own figures show

Scenario: Division by zero never shows a number
  Given a participant held 2 items of 0 SP in «Спринт 12»
  When Vera opens the page
  Then the percent of SP reads «—», and no figure reads NaN, infinity or 0 %
  And in the dynamics a previous sprint with 0 SP for that person is left out of the SP percent's min, average and max and counts as 0 in the SP's

Scenario: A person who left the team still has their row
  Given Анна was removed from the team MARS after the close
  When Vera opens «Спринт 12»
  Then Анна's row is shown with the figures recorded, since the row follows the assignee at the close, not today's membership
  And a person who was never a member of MARS but was an assignee shows too

Scenario: A renamed user is shown under the current name
  Given Борис Петров is now named Борис Сидоров
  When Vera opens the page of «Спринт 12» and of an earlier sprint
  Then both pages show Борис Сидоров, one row per person, never a row for each name

Scenario: A person who left the organization keeps their row
  Given Анна's membership of the organization has ended
  When Vera opens the page
  Then her row shows her last known name marked «не в организации» and her recorded figures

Scenario: Participants of hidden projects' items do not appear (owed by feat-041)
  Given all Борис's items of «Спринт 12» are in project C, «Ограниченный» with no grant for Vera
  When she opens the page
  Then there is no row for Борис, and every figure, the totals row included, equals what a twin organization without C gives her

Scenario: A reassignment or a leaving after the close changes nothing
  When API is later reassigned to Борис
  Then «Спринт 12» still counts API for Анна

Scenario: A large sprint lists its participants in pages
  Given «Спринт 12» had 300 assignees
  When Vera opens the page
  Then the first 200 rows show, «Показать ещё» loads the rest, and the row «Итого» counts all 300 from the first read

Scenario: The page is read-only for everyone
  When a planner or a member opens the page
  Then no figure can be edited, and a «Читатель» sees the same figures
```
