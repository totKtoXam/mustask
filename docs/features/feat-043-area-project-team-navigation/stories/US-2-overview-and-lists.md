# US-2: The organization's «Обзор» and the lists «Области», «Проекты», «Команды»

- story: US-2
- depends-on: US-1
- shippable: no — the lists are the entries' pages and the targets of every row link in US-3..US-5
- status: pending

As an **organization member**, I want **a home page for the organization and a list of its areas, of its projects and of its teams**, so that **I see everything I may open, each object once, and reach any of it in one step, whatever its links**.

## Acceptance criteria

Cast: organization «Ромашка»: Ann «Администратор», Olga «Участник» in team VENUS, Vera «Читатель». Typed tree (ADR-0031): departments «Логистика» (project nodes A and B) and «Финансы» (C); D at the top level; A's sub-areas «Склад» (with «Стеллажи») and «Транспорт». Teams MARS (A, B, C), VENUS (A), JUPITER (none); «Логистика» is linked to MARS and VENUS. Olga also belongs to «Другая», where she is an «Участник». Organization «Север» has projects and teams and no area. Project E is deactivated.

```gherkin
Scenario: «Обзор» is the organization's home page (BR-NV-1; feat-045 folded in by doc-013)
  When Olga opens «Обзор»
  Then beside the content she sees her active organizations by name, «Ромашка» marked as current, ending with «Все организации», and choosing «Другая» opens «Другая»'s «Обзор»
  And the content shows her team VENUS first, then the numbers of areas, projects and teams she may see (2, 4 and 3), each number opening its list
  And then the top-level areas, the projects and the teams, each list by name and each row opening its object's page

Scenario: The counts follow the rule of OQ-2 and each object is counted once (BR-NV-9; BR-MX-16)
  When Olga reads the counts on «Обзор»
  Then the areas are the departments «Логистика» and «Финансы» (2), the projects A, B, C and D (4), E excluded, and the teams MARS, VENUS and JUPITER (3)
  And «Склад», «Стеллажи» and «Транспорт» are in no count of «Обзор»

Scenario: «Обзор» lists are bounded at volume (BR-NV-9)
  Given an organization of 1 000 areas, 200 projects and 200 teams
  When Olga opens «Обзор»
  Then each of the three lists shows only its first rows, as many as the design sets, and ends with a link to its full list («Области», «Проекты» or «Команды»)
  And the page is read within 2 s, with a number of statements that does not grow with the volume

Scenario: «Проекты» lists every project once, whatever its place (BR-NV-4, BR-NV-9)
  When Olga opens «Проекты»
  Then she sees A, B, C and D once each, by name, each with its place as a path («Логистика», «Финансы», and none for D), and no list row nests a project under a department
  And every row opens the project's page, and D, which stands at the top level, is listed like the others

Scenario: «Команды» lists every team once (BR-NV-9; BR-MX-16)
  When Olga opens «Команды»
  Then she sees MARS, VENUS and JUPITER once each, her own team VENUS marked, and every row opens the team's page
  And JUPITER, which has no link, is listed

Scenario: «Области» shows the typed tree (BR-NV-4; ADR-0031; OQ-1 A)
  When Olga opens «Области»
  Then she sees «Логистика» with A and B beneath it, «Финансы» with C, D at the top level, and under A «Склад» (with «Стеллажи») and «Транспорт», each node once
  And a department opens its page, a project's node opens the node's page and a sub-area opens its own page
  And the type of every node is shown («Департамент», «Проект», «Область»)

Scenario: A deactivated project's node is marked and opens nothing (BR-NV-13; OQ-3 A)
  Given Ann deactivated D
  When Olga opens «Области»
  Then D's node stays in the tree marked «деактивирован» and is not a link
  And «Проекты» does not list D

Scenario: An administrator creates from the lists (BR-NV-14)
  When Ann opens «Проекты», «Команды» and «Области»
  Then each offers its create command («Создать проект» with the project's place, «Создать команду», «Создать департамент»), with the rules, the codes and the messages the interim pages had
  And Olga and Vera see no create command

Scenario: Deactivated projects are restored from «Проекты» (BR-NV-13)
  When Ann opens «Проекты» and chooses «Деактивированные»
  Then she sees E with the command «Восстановить», and Olga does not see the view or the command
  And after the restore E is listed in «Проекты» with its place and its links as they were

Scenario: Empty states (BR-AW-2; BR-MX-2; brief §§ 11.5, 11.6.7)
  Given «Север» has projects and teams and no area
  When Ann and then a member open «Области»
  Then both see «В организации пока нет областей», and only Ann is offered to create one
  And «Проекты», «Команды» and every project's and team's page work as in «Ромашка», and nothing asks for an area first
  And an organization with no project shows «Проектов пока нет» and, for Ann only, the command to create one

Scenario: A list opened at the volume is narrowed by typed text (BR-NV-9)
  Given an organization of 200 projects
  When Olga types part of a name in «Проекты»
  Then only the matching projects remain, and an empty result reads «Ничего не найдено» with «Сбросить фильтр»
```
