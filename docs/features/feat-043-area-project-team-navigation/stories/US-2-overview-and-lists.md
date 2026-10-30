# US-2: The organization's «Обзор», the organizations list and the lists «Области», «Проекты», «Команды»

- story: US-2
- depends-on: US-1
- shippable: no — the lists are the entries' pages and the targets of every row link in US-3..US-5
- status: pending

As an **organization member**, I want **a home page for the organization and a list of its areas, of its projects and of its teams**, so that **I see everything I may open, each object once, and reach any of it in one step, whatever its links**.

## Acceptance criteria

Cast: organization «Ромашка»: Ann «Администратор», Olga «Участник» in team VENUS, Vera «Читатель». Typed tree (ADR-0031): departments «Логистика» (project nodes A and B) and «Финансы» (C and E); D at the top level; A's sub-areas «Склад» (with «Стеллажи») and «Транспорт». Teams MARS (A, B, C, E), VENUS (A), JUPITER (none); «Логистика» is linked to MARS and VENUS. E is deactivated, has items and a team link to MARS. Olga also belongs to «Другая». Every scenario holds with nothing chosen in the department, team and project selectors; the way back to this menu from a chosen object is to set the selectors to «все» (US-8), and the create, restore and deactivate commands below live in this nothing-chosen menu's lists. Organization «Север» has projects and teams and no department; organization «Пустая» has nothing.

```gherkin
Scenario: «Обзор» is the organization's home page (BR-NV-1; feat-045 folded in by doc-013)
  When Olga opens «Обзор»
  Then beside the content she sees her active organizations by name, «Ромашка» marked as current, ending with «Все организации», and choosing «Другая» opens «Другая»'s «Обзор»
  And the content shows her team VENUS first, then the numbers of departments, projects and teams she may see (2, 4 and 3), each number opening its list
  And then the departments, the projects and the teams, each list by name and each row opening its object's page and setting its selector (BR-NV-19)

Scenario: The team block follows her teams (BR-NV-10)
  When Vera, who belongs to no team, and then a member of VENUS and MARS open «Обзор»
  Then Vera sees no own-team block, and the other sees both teams first, by name
  And a user of many organizations sees the first rows of her organizations as the design sets, ending with «Все организации»

Scenario: The counts count each object once, active only (BR-NV-9, BR-NV-13; BR-MX-16)
  When Olga reads the counts on «Обзор»
  Then the departments are «Логистика» and «Финансы» (2), the projects A, B, C and D (4), E excluded, and the teams MARS, VENUS and JUPITER (3)
  And «Склад», «Стеллажи», «Транспорт» and the project nodes are in no count of departments

Scenario: «Обзор» lists are bounded at volume (BR-NV-9)
  Given an organization of 1 000 areas, 200 projects and 200 teams
  When Olga opens «Обзор»
  Then each of the three lists shows only its first rows, as many as the design sets, and ends with a link to its full list
  And the page is read within 2 s, with a number of statements that does not grow with the volume

Scenario: «Проекты» lists every active project once, whatever its place (BR-NV-4)
  When Olga opens «Проекты»
  Then she sees A, B, C and D once each, by name, each with its place as a path («Логистика», «Финансы», none for D), and no row nests a project under a department
  And every row opens the project's «Общие», and A becomes the project selector's value (BR-NV-19), so the menu turns to A's; Ann deactivates a project from its row here (BR-NV-14)

Scenario: «Команды» lists every team once (BR-NV-9; BR-MX-16)
  When Olga opens «Команды»
  Then she sees MARS, VENUS and JUPITER once each, her own team VENUS marked, and every row opens the team's «Обзор» and sets the team selector to it (BR-NV-19), so the menu turns to the team's

Scenario: «Области» shows the typed tree (BR-NV-4; ADR-0031; OQ-1 A)
  When Olga opens «Области»
  Then she sees «Логистика» with A and B beneath it, «Финансы» with C and E, D at the top level, and under A «Склад» (with «Стеллажи») and «Транспорт», each node once, the type of each shown («Департамент», «Проект», «Область»)
  And a department row opens the department's «Обзор» and sets the department selector, a project row opens the project's «Общие» and sets the project selector (not the node's page), and a sub-area opens its own page under its project's menu with «Структура» marked
  And E's node and its sub-areas are marked «деактивирован» and are not links (OQ-3 A)

Scenario: An administrator creates from the lists (BR-NV-14)
  When Ann opens «Проекты», «Команды» and «Области»
  Then each offers its create command («Создать проект» with the project's place, «Создать команду», «Создать департамент»), with the rules, codes and messages the interim pages had
  And Olga and Vera see no create command

Scenario: Deactivated projects are restored from «Проекты» (BR-NV-13)
  When Ann opens «Проекты» and chooses «Деактивированные»
  Then she sees E with the command «Восстановить», and Olga does not see the view or the command
  And after the restore E is listed in «Проекты» with its place, its team link and its items as they were

Scenario: Empty states by what exists (BR-AW-2; BR-MX-2; ADR-0031; OQ-1 A)
  When a member opens «Области» in «Пустая», which has no node, and in «Север», which has projects and no department
  Then «Пустая» reads «В организации пока нет областей», and only Ann is offered «Создать департамент»
  And «Север» shows the tree of its top-level project nodes, Ann is offered «Создать департамент», its «Обзор» counts 0 departments and its department list is empty, and «Проекты», «Команды» and every page work with no department
  And «Пустая» shows «Проектов пока нет» in «Проекты», with the create command for Ann only

Scenario: A list is narrowed by typed text (BR-NV-9)
  Given an organization of 200 projects, 200 teams and 1 000 areas
  When Olga types part of a name in «Проекты», «Команды» and «Области»
  Then each keeps the matching rows within 2 s, and the tree keeps the ancestors of a match
  And an empty result reads «Ничего не найдено» with «Сбросить фильтр»

Scenario: The organizations list and its create flow open the new pages (BR-NV-16; OQ-8 A)
  When Olga chooses «Ромашка» in the organizations list, and Ann creates the organization «Новая»
  Then the first opens «Ромашка»'s «Обзор», and the second opens «Новая»'s «Проекты», where the first project is created
  And the same holds for the organization selector, and no feat-038 landing is needed on main

Scenario: No link in the panel targets a retired address (BR-NV-6)
  When a test walks the links of the navigation, the lists, the organizations list, the create flows, the header selectors and the settings navigation
  Then none is an address of the table of retired addresses
```
