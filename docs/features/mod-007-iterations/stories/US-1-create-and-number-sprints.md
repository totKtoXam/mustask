# US-1: A planner creates, names and dates a team's sprints

- story: US-1
- depends-on: -
- shippable: no — a sprint nobody can list is not usable; it ships with US-2 and US-3 as `feat-NNN-team-sprint-lifecycle`
- status: pending

As a **team planner**, I want **to create, name and date my team's sprints, numbered in the team's own sequence**, so that **the team plans its time once, whatever number of projects it works on**.

## Acceptance criteria

Unless a scenario says otherwise: team MARS is linked to projects A, B and C, and team VENUS to A. Pavel is an organization «Участник» who holds the team role «Участник» on MARS (doc-012's «Планирование», renamed by mod-019). Vera is an organization «Участник» with no team grant. "Refused" means nothing is written; a sprint's refusal is shown on its form. Source: spec BR-1, BR-4, BR-7, BR-13; doc-012 US-9.

```gherkin
Scenario: A sprint takes the next number of its team's own sequence
  Given MARS has sprints 1 to 11 and VENUS has sprints 1 and 2
  When Pavel creates a MARS sprint with no name and no dates
  Then it is MARS's sprint 12, named «Спринт 12», Planned and undated
  And VENUS's next number is still 3
  And the creation asks only for a name and dates: no kind, release or milestone is offered

Scenario: Concurrent creations never share a number
  Given MARS has sprints 1 to 11
  When two planners create a MARS sprint at the same moment
  Then the sprints are 12 and 13, and no request ends in a server error

Scenario: A creation whose default name is taken is refused and issues no number
  Given MARS's latest sprint is number 13, renamed by hand to «Спринт 14»
  When Pavel creates a MARS sprint with no name
  Then it is refused with «Спринт с таким названием уже есть в команде.»
  And a creation named «Финальный» then becomes number 14

Scenario: Names are 1-100 characters, unique among the team's own sprints, and renaming keeps the number
  Given MARS has the Closed «Спринт 12» and the Planned «Спринт 13», and VENUS has «Спринт 3»
  When Pavel renames «Спринт 13» to «Релизный», and then to «Спринт 3»
  Then both renames are saved and the sprint is still number 13
  And renaming it to «Спринт 12» or to « спринт 12 » is refused with «Спринт с таким названием уже есть в команде.»
  And a name of 101 characters, or of spaces only, is refused with «Название спринта — от 1 до 100 символов.»
  And the Closed «Спринт 12» itself can be renamed

Scenario: Dates are optional but paired, finish not before start
  When Pavel saves a Planned MARS sprint
  Then it is accepted with no dates, with 2026-11-02 to 2026-11-13, and with 2026-11-02 to 2026-11-02
  And a start with no finish is refused with «Укажите обе даты спринта или ни одной.»
  And 2026-11-13 to 2026-11-02 is refused with «Дата окончания не может быть раньше даты начала.»

Scenario: A started sprint keeps both dates, and a Closed sprint's dates never change
  Given MARS's Active «Спринт 12» has 2026-11-02 to 2026-11-13 and its Closed «Спринт 11» has 2026-10-19 to 2026-10-30
  When Pavel changes the dates of «Спринт 12» to 2026-11-02 to 2026-11-20
  Then the change is saved
  When he clears them
  Then it is refused with «У активного спринта должны быть обе даты.»
  And changing or clearing the dates of «Спринт 11» is refused with «Даты завершённого спринта изменить нельзя.»

Scenario: Only the team's planners and the organization admin manage its sprints
  When a user creates, renames or dates a MARS sprint
  Then it is allowed for Pavel, for a holder of the team role «Менеджер» on MARS and for an organization «Администратор» with no team grant
  And it is refused with 403 «Недостаточно прав» for Vera, for a holder of the team role «Участник» on VENUS only, for a holder of the team role «Читатель» on MARS and for an organization «Читатель»

Scenario: A sprint is never created for a deleted team or another organization's team
  When Pavel creates a MARS sprint while the organization admin deletes MARS
  Then either the sprint is created first and the deletion follows, or the creation is refused with «Команда не найдена.» and takes no number
  And a team id of another organization answers «Команда не найдена.» and writes nothing
```
