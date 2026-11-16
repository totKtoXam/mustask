# US-3: A team's sprint list

- story: US-3
- depends-on: US-1, US-2
- shippable: no — it is the page that shows US-1 and US-2; US-1..US-5 ship together as `mod-007-iterations` (one L, OQ-8)
- status: pending

As an **organization member**, I want **to open a team's «Спринты» and see every sprint of the team with its state and dates**, so that **I know what the team is running, has planned and has finished**.

## Acceptance criteria

«Спринты» is a section of the team's page (address `teams/:t/sprints`), placed after «Бэклог» and before «Проекты» (doc-012 US-10); the team's own page already exists, and since feat-043's second line its sections include «Рабочие элементы» after «Бэклог», so «Спринты» follows it. Setting as in US-1. Source: spec BR-7, BR-13; doc-012 US-9, US-10.

```gherkin
Scenario: The list shows every sprint of the team, Closed ones included, highest number first
  Given MARS has the Closed «Спринт 11», the Active «Спринт 12» and the Planned «Спринт 13», and VENUS has «Спринт 3»
  When Vera, or any other member of the organization, opens MARS's «Спринты»
  Then it lists «Спринт 13», «Спринт 12» and «Спринт 11», each with its dates and its state: «Запланирован», «Активен» or «Завершён»
  And it lists no VENUS sprint, and a removed Planned sprint is listed nowhere

Scenario: Each row shows how much the sprint holds, after visibility
  Given «Спринт 13» is Planned and holds API of A (5 SP) and notifications of C (3 SP)
  When Vera opens MARS's «Спринты»
  Then the row of «Спринт 13» reads 2 items and 8 SP
  When C becomes «Ограниченный» with no grant for Vera (owed by feat-041)
  Then the row reads 1 item and 5 SP, as if C did not exist, and a deactivated project's items count nowhere
  And an empty sprint reads 0 items and 0 SP

Scenario: Managing commands are shown only to those who may use them
  When Pavel (holds the team role «Участник» on MARS) and then Vera (no team grant) open MARS's «Спринты»
  Then Pavel is offered create, rename, dates, start, close and remove where they apply to the sprint's state
  And Vera sees the list and none of those commands, and nothing is shown disabled

Scenario: The section is part of the team's page and keeps the page's rules
  When Vera opens MARS
  Then the sections read «Обзор», «Канбан», «Бэклог», «Рабочие элементы», «Спринты», «Проекты», «Участники и доступ» in this order
  And moving to «Спринты» requests no new document and the back button returns to the previous section

Scenario: A team of another organization or a deleted team has no list
  When a user opens «Спринты» of a team that was deleted or belongs to another organization
  Then the page reads «Команда не найдена» and lists nothing

Scenario: A long list stays usable
  Given MARS has 1 000 sprints
  When Vera opens «Спринты»
  Then the first page shows in ≤ 2 s, the highest numbers first, and the rest load on request (the page size is OQ-5's)

Scenario: An empty team
  Given team JUPITER has no sprint
  When Pavel opens JUPITER's «Спринты»
  Then it reads «У команды пока нет спринтов» and offers creation to a planner only
```
