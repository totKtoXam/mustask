# US-1: A team stands in exactly one department

- story: US-1
- depends-on: -
- shippable: yes
- status: pending

As an **organization administrator or a department «Менеджер»**, I want **every team to stand in one department, chosen when it is created and changed by a move**, so that **the structure reads organization → department → team and nobody has to keep many-to-many department links in step**.

Cast: spec § Stories. Delivered by `mod-NNN-teams-in-departments`; the scenarios naming a department «Менеджер» by `mod-NNN-department-managers-run-teams`. The codes follow OQ-6 A; under OQ-6 B every clause naming a department «Менеджер» reads «Администратор» only.

## Acceptance criteria

```gherkin
Scenario: A team is created in a department (BR-TD-1)
  Given Ann is on «Логистика»'s «Команды»
  When she chooses «Создать команду» and saves the name «SATURN»
  Then SATURN stands in «Логистика», and its page shows «Департамент: Логистика»
  And «Логистика»'s «Команды» lists MARS, SATURN and VENUS by name

Scenario: The department is required
  Given Ann opens «Новая команда» from the organization's «Команды»
  When she saves the name «SATURN» with «Департамент» empty
  Then the field shows «Выберите департамент» and no team is created
  And the «Департамент» picker offers «Логистика» and «Финансы», never a project node, a sub-area or «Верхний уровень»

Scenario: A department manager creates a team in their department only (OQ-6 A)
  Given Max holds department «Менеджер» on «Логистика» and nothing on «Финансы»
  When he opens «Новая команда»
  Then the «Департамент» picker offers «Логистика» alone
  And a request to create a team in «Финансы», in a deleted department or in one of «Другая» is refused with «Недостаточно прав» and creates nothing
  And Vera, with no grant, sees no «Создать команду» anywhere

Scenario: A team is moved to another department (BR-TD-4)
  Given VENUS stands in «Логистика», has members, a team grant, links to A, items it owns and a sprint
  When Ann moves VENUS to «Финансы»
  Then VENUS stands in «Финансы» and its members, grants, links, owned items, sprints and backlog order are unchanged
  And «Логистика»'s «Команды» no longer lists VENUS, «Финансы»'s does

Scenario: Moving needs the code on both departments
  Given Max is department «Менеджер» on «Логистика» only
  When he moves MARS to «Финансы»
  Then he is refused with «Недостаточно прав» and MARS stays in «Логистика»

Scenario: A move to the same department, or to a missing one
  When Ann moves MARS to «Логистика», where it stands
  Then the request succeeds and nothing changes
  When she moves MARS to a department of «Другая» or to one that was deleted
  Then she gets «Область не найдена.» and MARS stays in «Логистика»

Scenario: No team-department link remains (BR-TD-2)
  When Ann opens «Логистика»'s page
  Then «Команды» lists the teams standing in it, with no «Добавить команду» link picker and no «Отключить» command
  And MARS's page has no «Области» multiselect, only «Департамент» with «Переместить»

Scenario: A department holding a team is not deleted (BR-TD-5)
  Given «Финансы» holds JUPITER and no project
  When Ann deletes «Финансы»
  Then she is refused with «Нельзя удалить департамент, в котором есть команды» and nothing changes
  And once JUPITER is deleted, deleting «Финансы» succeeds: a deleted team keeps no department

Scenario: A creation against the department's deletion ends in one order
  When Ann creates SATURN in «Финансы» while Ivan deletes «Финансы» with no team in it
  Then either the creation commits and the deletion is refused, or the deletion commits and the creation answers «Область не найдена.»
  And no server error occurs and no team stands in a deleted department

Scenario: A move against the destination's deletion ends in one order
  When Ann moves MARS to «Финансы» while Ivan deletes «Финансы» with no team in it
  Then either the move commits and the deletion is refused, or the deletion commits and the move answers «Область не найдена.»
  And no server error occurs and no team stands in a deleted department
```
