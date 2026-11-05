# US-9: A team's sprint lifecycle

- story: US-9
- depends-on: US-2, US-3, US-5, US-6, US-7, US-8
- shippable: no — an analysis story: `mod-007-iterations` delivers it with US-12 (spec § Decomposition); the sprint filter of views is US-11's
- status: pending

As a **team planner (team «Планирование»)**, I want **to create, name, date, start and remove my team's sprints, numbered in the team's own sequence**, so that **the team plans its time once, whatever number of projects it works on**.

## Acceptance criteria

Unless a scenario says otherwise: projects A, B and C have visibility «Организация»; team MARS is linked to A, B and C, and team VENUS to A. Pavel is an organization «Участник» who holds «Планирование» on MARS. Vera is an organization «Участник» with no team grant, so she may change items but not manage sprints. MARS owns the items API of A (5 SP), integration of B (8 SP) and notifications of C (3 SP). "Refused" means nothing is written; a sprint's own refusal is shown on its form. What goes into a sprint, the close, the outcome and the boards are US-12's.

```gherkin
Scenario: A sprint takes the next number of its team's own sequence (BR-MX-15; brief § 8.9)
  Given MARS has sprints 1 to 11 and VENUS has sprints 1 and 2
  When Pavel creates a MARS sprint with no name and no dates
  Then it is MARS's sprint 12, named «Спринт 12», Planned and undated, and VENUS's next number is still 3
  And the creation asks only for a name and dates: a sprint has no kind, and no release or milestone is offered
  And two planners creating a MARS sprint at the same moment get 13 and 14, never one number twice

Scenario: A team's sprint list shows every sprint of the team, Closed ones included (brief § 14; BR-MX-14, BR-MX-15)
  Given MARS has the Closed «Спринт 11», the Active «Спринт 12» and the Planned «Спринт 13», and VENUS has «Спринт 3»
  When Vera, or any other member of the organization, opens MARS's sprint list
  Then it lists «Спринт 13», «Спринт 12» and «Спринт 11», highest number first, each with its dates and its state: «Запланирован», «Активен» or «Завершён»
  And it lists no VENUS sprint, and a removed Planned sprint is listed nowhere

Scenario: A creation whose default name is taken is refused and issues no number (BR-MX-15)
  Given MARS's latest sprint is number 13, renamed by hand to «Спринт 14»
  When Pavel creates a MARS sprint with no name
  Then it is refused with «Спринт с таким названием уже есть в команде.», and a creation named «Финальный» then becomes number 14

Scenario: Names are 1-100 characters, unique among the team's own sprints, and renaming keeps the number (BR-MX-15)
  Given MARS has the Closed «Спринт 12» and the Planned «Спринт 13», and VENUS has «Спринт 3»
  When Pavel renames MARS's «Спринт 13» to «Релизный», and then to «Спринт 3»
  Then both renames are saved and the sprint is still number 13: VENUS's names do not count
  And renaming it to «Спринт 12», the name of the Closed MARS sprint, or to « спринт 12 » is refused with «Спринт с таким названием уже есть в команде.»
  And a name of 101 characters, or of spaces only, is refused with «Название спринта — от 1 до 100 символов.»
  And the Closed «Спринт 12» itself can be renamed, and the name of a removed sprint can be used again

Scenario: Dates are optional but paired, finish not before start, on creation and on edit (BR-MX-14)
  When Pavel saves a Planned MARS sprint
  Then it is accepted with no dates, with 2026-11-02 to 2026-11-13, and with 2026-11-02 to 2026-11-02 (a one-day sprint)
  And it is refused with a start but no finish by «Укажите обе даты спринта или ни одной.», and with 2026-11-13 to 2026-11-02 by «Дата окончания не может быть раньше даты начала.»

Scenario: A started sprint keeps both dates, and a Closed sprint's dates never change (BR-MX-14)
  Given MARS's Active «Спринт 12» has 2026-11-02 to 2026-11-13, and its Closed «Спринт 11» has 2026-10-19 to 2026-10-30
  When Pavel changes the dates of «Спринт 12» to 2026-11-02 to 2026-11-20, and then clears them
  Then the change is saved, and clearing is refused with «У активного спринта должны быть обе даты.»
  And changing or clearing the dates of «Спринт 11» is refused with «Даты завершённого спринта изменить нельзя.», and it keeps them

Scenario: Only the team's planners and the organization admin manage its sprints (BR-MX-15, BR-MX-23, BR-MX-24)
  When a user creates, renames, dates, starts, closes or removes a MARS sprint
  Then it is allowed for Pavel, for a holder of «Управление» on MARS and for an organization «Администратор» with no team grant
  And it is refused with 403 for Vera, for a holder of «Планирование» on VENUS only and for an organization «Читатель»

Scenario: A sprint is never created for a deleted team (BR-MX-12, BR-MX-15; spec NFR concurrency)
  When Pavel creates a MARS sprint while the organization admin deletes MARS
  Then either the sprint is created first and the deletion follows, or the creation is refused with «Команда не найдена.», creates no sprint and takes no number
  And neither request ends in a server error

Scenario: A team has at most one Active sprint (BR-MX-14, OQ-6; NFR concurrency)
  Given MARS's «Спринт 12» is Active, and its «Спринт 13» is Planned with both dates
  When Pavel starts «Спринт 13»
  Then it is refused with «У команды уже есть активный спринт.» and «Спринт 13» stays Planned, while VENUS can still start a sprint of its own
  And when two planners start two dated Planned sprints of a team with no Active sprint at the same moment, exactly one becomes Active

Scenario: Starting needs both dates; a sprint never goes back, and only an Active one closes (BR-MX-14)
  Given MARS has no Active sprint
  When Pavel starts an undated Planned sprint, starts a Closed sprint, and closes a Planned or a Closed sprint
  Then they are refused with «Чтобы начать спринт, укажите обе даты.», «Начать можно только запланированный спринт.» and «Завершить можно только активный спринт.», and each sprint keeps its state and its dates

Scenario Outline: Only a Planned sprint that holds no item outside «Удалена» can be removed (BR-MX-14, BR-MX-15)
  Given MARS's latest sprint, «Спринт 13», is <state> and holds <items>
  When Pavel removes it
  Then <result>, and every refusal reads «Удалить можно только запланированный спринт без элементов.»
  Examples:
    | state   | items                                                           | result                                                                     |
    | Planned | no item                                                         | it is gone from every list and filter, and MARS's next sprint is number 14 |
    | Planned | only an item in «Удалена»                                       | it is gone, and that item has no sprint                                    |
    | Planned | integration                                                     | refused                                                                    |
    | Planned | only notifications, while C is hidden from him or deactivated   | refused, and the refusal names no item, project or count                   |
    | Active  | no item                                                         | refused                                                                    |
    | Closed  | its completed items                                             | refused: a Closed sprint is never removed                                  |
```
