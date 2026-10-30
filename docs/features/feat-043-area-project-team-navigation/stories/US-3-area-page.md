# US-3: A department chosen: its menu; a project's place and a sub-area

- story: US-3
- depends-on: US-1, US-2
- shippable: no — the page of the tree that US-2's «Области» opens; the interim area pages retire with it (US-9)
- status: pending

As an **organization member**, I want **choosing a department to give me its sections in the left menu, and every node of the area tree a page of its own**, so that **I see what a department groups, where a project stands and what a sub-area holds, and an administrator keeps the structure where it is read**.

## Acceptance criteria

Cast as US-2: departments «Логистика» (project nodes A and B, linked to MARS and VENUS) and «Финансы» (C and the deactivated E); D at the top level; A's sub-areas «Склад» (with «Стеллажи») and «Транспорт»; Ann «Администратор», Olga «Участник». Until `mod-019-scoped-access` the commands below are the organization administrator's (spec § Roles). The sections `feat-042-area-board-and-overview` adds (the figures of «Обзор», «Канбан», «Бэклог») and `mod-019-scoped-access` adds («Участники») are absent here, each in its place from the section table of the spec, and those owners re-run the clauses that need them (BR-NE-3, BR-NE-4).

```gherkin
Scenario: A department's page (BR-NV-3; BR-AW-0, BR-AW-6)
  When Olga chooses «Логистика» in the department selector
  Then the left menu holds «Обзор», «Проекты», «Команды» and, last, the derived «Настройки» (BR-NV-18), and the page opens at «Обзор»
  And «Обзор» shows its description, that it stands at the top level, and the numbers of its projects (2) and teams (2), each opening its section
  And «Проекты» lists the project nodes A and B once each, by name, each opening the project's page
  And «Команды» lists MARS and VENUS, the area–team links of the department, each opening the team's page
  And there is no analytics section and no «Участники» section

Scenario: A project's place opens a page that names its project (ADR-0031 rule 5; OQ-1 A)
  When Olga opens A from «Области» or from «Логистика»'s «Проекты» (it opens A's «Общие», BR-NV-19, OQ-1 A) and then «Структура»
  Then she sees A's name and type «Проект», its parent «Логистика», the node's own description (not the project's, which is on A's «Общие») and its sub-areas «Склад» and «Транспорт» (with «Стеллажи» under «Склад»), and the link «Открыть проект», which opens A's «Общие»
  And the selectors read «Логистика» / «все» / A, the menu is A's (US-4) with «Структура» marked; no «Команды» of the node: the project's teams are in A's menu
  And the page shows no area–team link, because none can join a project's node (BR-AW-6)

Scenario: A sub-area's page (BR-AW-3, BR-AW-13)
  When Olga opens «Стеллажи»
  Then she sees its path «Логистика / A / Склад / Стеллажи», its parent «Склад», its description, its child areas, and the project A with a link to it
  And every area and project shown opens its own page

Scenario: A sub-area shows its ancestors as a path, not a trail (BR-NV-4)
  When Olga opens «Склад» from «Области»
  Then the page's path reads «Логистика / A / Склад», and every part but the last opens its page, while the selectors read «Ромашка» / «Логистика» / «все» / A

Scenario: Settings are the settings navigation's «Параметры» (BR-NV-5, BR-NV-14, BR-NV-18)
  When Ann has «Логистика» chosen and opens «Настройки», then the group «Департамент» and its entry «Параметры»
  Then she renames it, changes its description, deletes it, and links teams from «Команды» in the menu; a department stands at the top and has no move command
  And for a sub-area opened from «Области», «Параметры» lets her rename, describe, move it within its project and delete it; for a project node, only describe it, because the name follows the project (BR-AW-4, OQ-10 A of mod-018)
  And Olga finds no «Департамент» group in «Настройки», and the address of its «Параметры» shows her «Доступ ограничен»

Scenario: A sub-area is created under a project's node or a sub-area (BR-AW-3)
  When Ann chooses «Добавить область» on A's node page and names it «Шлюз»
  Then «Шлюз» is a child of A's node, and the tree in «Области» shows it once
  And a name taken among its siblings answers «Область с таким названием уже есть на этом уровне» and nothing is created
  And a sub-area is not offered on a department's page, because a department holds no sub-area (OQ-7 A of mod-018)

Scenario: Deleting a node follows the rules of the tree (BR-AW-9)
  When Ann deletes «Транспорт», which has no child and no work item
  Then after the confirmation «Транспорт» is gone from every list and no project, team or item changes
  And «Склад», which has a child, answers «Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы»
  And a project's node offers no delete command and answers «Корневая область меняется только вместе с проектом» to a request for it

Scenario: Deleting a department names its links and refuses a holder of projects (BR-AW-9; BR-NV-13)
  When Ann deletes «Резерв», a department with the team link to JUPITER and no project
  Then the confirmation names the link and says it cannot be undone, and after it «Резерв» is gone, JUPITER and every project unchanged
  And deleting «Финансы», which holds C and the deactivated E, answers the message above, and its «Проекты» lists E marked «деактивирован», not a link, counted apart, which explains the refusal

Scenario: A deleted or foreign node answers not found (BR-NV-7)
  When Olga opens the page of «Транспорт» after it was deleted, and an area of «Другая» by its address in «Ромашка»
  Then she sees «Область не найдена» for each, and the selectors do not change

Scenario: A node's page sets the project selector and shows the project's menu (BR-NV-4, BR-NV-10)
  When Olga opens the node page of A (through «Структура»), and then a sub-area's page «Склад»
  Then the selectors read «Ромашка» / «Логистика» / «все» / A on both, and «Структура» is the marked entry
  And a department's menu entries leave the project selector at «все»

Scenario: A deactivated project's node and sub-areas open nothing (BR-NV-13; OQ-3 A)
  Given E is deactivated
  When Olga opens the address of E's node or of one of E's sub-areas
  Then she sees «Проект не найден»

Scenario: A row link makes its object the most specific (BR-NV-19; design D-3 A)
  Given the selectors read «Логистика» / MARS / «все»
  When Olga opens A from «Логистика»'s «Проекты»
  Then the selectors read «Логистика» / MARS / A, because A is the most specific object and «Логистика» and MARS both fit it, A's menu opens at «Общие», and no notice appears
  And opening B from MARS's «Проекты» with the selectors «Финансы» / MARS leaves MARS and B, and the department resets to «все» silently because «Финансы» does not hold B
  And with the selectors «Финансы» / MARS / C, opening «Логистика» from «Области» resets the team and the project to «все» silently, as they are more specific than a department

Scenario: A department's «Проекты» and «Команды» when empty (BR-NV-11)
  Given Ann created the department «Резерв»
  When Olga opens its «Проекты» and «Команды»
  Then they read «В департаменте нет проектов» and «К департаменту не привязаны команды», each offering to add only to Ann
```
