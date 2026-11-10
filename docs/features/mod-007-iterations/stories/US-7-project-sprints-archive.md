# US-7: A project's «Спринты» archive, a deleted team's included

- story: US-7
- depends-on: US-5
- shippable: yes — a read-only section of the project's page
- status: pending

As a **user who can see a project**, I want **the project's «Спринты» to list the Closed sprints that recorded its items, with those items, even after the sprint's team is deleted**, so that **the history of the project's delivered work stays readable**.

## Acceptance criteria

Projects A, B, C visible to everyone unless said otherwise. VENUS's Closed «Спринт 4» recorded A's item search at 2 SP; MARS's «Спринт 11», closed after it, recorded API of A at 5 SP and notifications of C at 3 SP. «Спринты» sits between «Команды» and «Участники и доступ» on a project's page (doc-012 US-10), read-only. Source: spec BR-11, BR-9; doc-012 US-12, US-10.

```gherkin
Scenario: A project's «Спринты» lists the Closed sprints that recorded its items
  When Vera opens A's «Спринты»
  Then it lists «Спринт 11» of MARS with API at 5 SP and «Спринт 4» of VENUS with search at 2 SP, the latest closed first, each read-only
  And it lists no item of C, whose «Спринты» lists notifications under «Спринт 11»
  And it offers no create, start, close or remove command

Scenario: A deleted team's Closed sprints stay readable
  When the organization admin deletes MARS, and Vera opens A's «Спринты»
  Then «Спринт 11» is listed marked «команда удалена», with API at 5 SP, and VENUS's «Спринт 4» is unchanged
  And API and notifications have no sprint (their «Спринт» reads empty), and MARS's own address answers «Команда не найдена»

Scenario: A live team's Closed sprint stays listed after the team leaves the project
  Given MARS, not deleted, is unlinked from A with VENUS as the destination
  When Vera opens A's «Спринты»
  Then «Спринт 11» of MARS is still listed with API at 5 SP, not marked «команда удалена»

Scenario: A deleted team's sprint that recorded nothing is listed nowhere
  Given a MARS sprint closed with no Completed item
  When MARS is deleted
  Then no project's «Спринты» lists it

Scenario: A project the user may not see answers not found
  Given C becomes «Ограниченный» with no grant for Vera
  When she opens C's «Спринты» by its address
  Then it answers «Проект не найден» and A's list is unchanged

Scenario: A project with no Closed sprint
  Given project D has no recorded item
  When Vera opens D's «Спринты»
  Then it reads «Завершённых спринтов с работой этого проекта пока нет»

Scenario: The list shows the recorded figures, not today's
  Given API was re-estimated at 8 SP after «Спринт 11» closed
  When Vera opens A's «Спринты»
  Then «Спринт 11» still lists API at 5 SP

Scenario: A deactivated project has no archive page
  Given B is deactivated
  When Vera opens B's «Спринты» by its address
  Then it answers «Проект не найден»; after B is restored its list is back unchanged
```
