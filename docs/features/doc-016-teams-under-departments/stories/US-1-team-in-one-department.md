# US-1: A team stands in exactly one department

- story: US-1
- depends-on: -
- shippable: yes
- status: pending

As an **organization administrator or a department «Менеджер»**, I want **every team to stand in one department, chosen when it is created and changed by a move**, so that **the structure reads organization → department → team and nobody has to keep many-to-many department links in step**.

Cast: spec § Stories; SATURN below is a team of «Логистика» linked to no project. Delivered by `mod-NNN-teams-in-departments`; the scenarios naming a department «Менеджер» by `mod-NNN-department-managers-run-teams` (OQ-6 A, the human, 2026-11-12). The move refusal follows OQ-9 A, the open recommendation; under OQ-9 B the refusal scenario reads "the move cuts the links after a confirmation counting the items going to «Без команды»".

## Acceptance criteria

```gherkin
Scenario: A team is created in a department (BR-TD-1)
  Given Ann is on «Логистика»'s «Команды»
  When she chooses «Создать команду» and saves the name «NEPTUNE»
  Then NEPTUNE stands in «Логистика», linked to no project, and its page shows «Департамент: Логистика»
  And «Логистика»'s «Команды» lists MARS, NEPTUNE, SATURN and VENUS by name

Scenario: The department is required
  Given Ann opens «Новая команда» from the organization's «Команды»
  When she saves the name «NEPTUNE» with «Департамент» empty
  Then the field shows «Выберите департамент» and no team is created
  And the «Департамент» picker offers «Логистика» and «Финансы», never a project node, a sub-area or «Верхний уровень»

Scenario: A department manager creates a team in their department only (OQ-6 A)
  Given Max holds department «Менеджер» on «Логистика» and nothing on «Финансы»
  When he opens «Новая команда»
  Then the «Департамент» picker offers «Логистика» alone
  And a request to create a team in «Финансы», in a deleted department or in one of «Другая» is refused with «Недостаточно прав» and creates nothing
  And Vera, with no grant, sees no «Создать команду» anywhere

Scenario: An unlinked team is moved to another department (BR-TD-4)
  Given SATURN stands in «Логистика», has members, a team grant and a sprint, and is linked to no project
  When Ann moves SATURN to «Финансы»
  Then SATURN stands in «Финансы» and its members, grants, sprints and backlog order are unchanged
  And «Логистика»'s «Команды» no longer lists SATURN, «Финансы»'s does

Scenario: A linked team is not moved (BR-TD-4, OQ-9 A)
  Given VENUS is linked to A in «Логистика»
  When Ann moves VENUS to «Финансы»
  Then she is refused with «Команда связана с проектами. Отключите её от них перед перемещением.» and nothing changes
  And once she unlinks VENUS from A (feat-028's dialog, choosing where its items go), the move succeeds

Scenario: Moving needs the code on both departments
  Given Max is department «Менеджер» on «Логистика» only
  When he moves SATURN to «Финансы»
  Then he is refused with «Недостаточно прав» and SATURN stays in «Логистика»

Scenario: A move to the same department, or to a missing one
  When Ann moves MARS to «Логистика», where it stands
  Then the request succeeds and nothing changes, links or not
  When she moves SATURN to a department of «Другая» or to one that was deleted
  Then she gets «Область не найдена.» and SATURN stays in «Логистика»

Scenario: A link against a move ends in one order (BR-TD-3)
  When Ann moves SATURN to «Финансы» while Ivan links SATURN to A
  Then either the move commits and the link is refused with «Команду можно связать только с проектом своего департамента.», or the link commits and the move is refused with «Команда связана с проектами. Отключите её от них перед перемещением.»
  And no server error occurs and no link joins two departments

Scenario: No team-department link remains (BR-TD-2)
  When Ann opens «Логистика»'s page
  Then «Команды» lists the teams standing in it, with no «Добавить команду» link picker and no «Отключить» command
  And MARS's page has no «Области» multiselect, only «Департамент» with «Переместить»

Scenario: A department holding a team is not deleted (BR-TD-5)
  Given the department «Маркетинг» holds the team PHOBOS and no project
  When Ann deletes «Маркетинг»
  Then she is refused with «Нельзя удалить департамент, в котором есть команды» and nothing changes
  And «Финансы», holding JUPITER and the projects C and D, is refused with the same text: the team answer comes first
  And once PHOBOS is deleted, deleting «Маркетинг» succeeds: a deleted team keeps no department

Scenario: A creation against the department's deletion ends in one order
  Given «Маркетинг» holds no team and no project
  When Ann creates NEPTUNE in «Маркетинг» while Ivan deletes «Маркетинг»
  Then either the creation commits and the deletion is refused, or the deletion commits and the creation answers «Область не найдена.»
  And no server error occurs and no team stands in a deleted department

Scenario: A move against the destination's deletion ends in one order
  Given «Маркетинг» holds no team and no project
  When Ann moves SATURN to «Маркетинг» while Ivan deletes «Маркетинг»
  Then either the move commits and the deletion is refused, or the deletion commits and the move answers «Область не найдена.»
  And no server error occurs and no team stands in a deleted department
```
