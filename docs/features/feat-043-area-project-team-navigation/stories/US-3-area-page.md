# US-3: An area's page: a department, a project's place, a sub-area

- story: US-3
- depends-on: US-1, US-2
- shippable: no — the page of the tree that US-2's «Области» opens; the interim area pages retire with it (US-9)
- status: pending

As an **organization member**, I want **each node of the area tree on a page of its own with its sections**, so that **I see what a department groups, where a project stands and what a sub-area holds, and an administrator keeps the structure where it is read**.

## Acceptance criteria

Cast as US-2: departments «Логистика» (project nodes A and B, linked to MARS and VENUS) and «Финансы» (C); D at the top level; A's sub-areas «Склад» (with «Стеллажи») and «Транспорт»; Ann «Администратор», Olga «Участник». Until `mod-019-scoped-access` the commands below are the organization administrator's (spec § Roles). The sections `feat-042-area-board-and-overview` adds (the figures of «Обзор», «Канбан», «Бэклог») and `mod-019-scoped-access` adds («Участники») are absent here, each in its place from the section table of the spec, and those owners re-run the clauses that need them (BR-NE-3, BR-NE-4).

```gherkin
Scenario: A department's page (BR-NV-3; BR-AW-0, BR-AW-6)
  When Olga opens «Логистика»
  Then its sections are «Обзор», «Проекты» and «Команды»; «Настройки» is added only for Ann
  And «Обзор» shows its description, that it stands at the top level, and the numbers of its projects (2) and teams (2), each opening its section
  And «Проекты» lists the project nodes A and B once each, by name, each opening the project's page
  And «Команды» lists MARS and VENUS, the area–team links of the department, each opening the team's page
  And there is no analytics section and no «Участники» section

Scenario: A project's place opens a page that names its project (ADR-0031 rule 5; OQ-1 A)
  When Olga opens the node of A from «Области» or from «Логистика»'s «Проекты» and opens the node's address
  Then she sees A's name and type «Проект», its parent «Логистика», its description and its sub-areas «Склад» and «Транспорт» (with «Стеллажи» under «Склад»), and the link «Открыть проект», which opens A's page
  And the sections are «Обзор» and, for Ann, «Настройки»; no «Команды» section: the project's teams are on A's page
  And the page shows no area–team link, because none can join a project's node (BR-AW-6)

Scenario: A sub-area's page (BR-AW-3, BR-AW-13)
  When Olga opens «Стеллажи»
  Then she sees its path «Логистика / A / Склад / Стеллажи», its parent «Склад», its description, its child areas, and the project A with a link to it
  And every area and project shown opens its own page

Scenario: The trail of an area shows its ancestors (BR-NV-4)
  When Olga opens «Склад»
  Then the trail reads «Ромашка / Области / Логистика / A / Склад / Обзор», and every part but the last opens its page

Scenario: Settings are the page's own (BR-NV-5, BR-NV-14)
  When Ann opens «Логистика» and then «Настройки»
  Then she renames it, changes its description, deletes it, and links teams from «Команды»; a department stands at the top and has no move command
  And on a sub-area's «Настройки» she renames, describes, moves it within its project, and deletes it; on a project node's, only describes it, because the name follows the project (BR-AW-4, OQ-10 A of mod-018)

Scenario: A sub-area is created under a project's node or a sub-area (BR-AW-3)
  When Ann chooses «Добавить область» on A's node page and names it «Шлюз»
  Then «Шлюз» is a child of A's node, and the tree in «Области» shows it once
  And a name taken among its siblings answers «Область с таким названием уже есть на этом уровне» and nothing is created
  And a sub-area is not offered on a department's page, because a department holds no sub-area (OQ-7 A of mod-018)

Scenario: Deleting a node follows the rules of the tree (BR-AW-9)
  When Ann deletes «Транспорт», which has no child and no work item
  Then the confirmation names its team links, and after it «Транспорт» is gone from every list and no project, team or item changes
  And «Склад», which has a child, answers «Нельзя удалить область, в которой есть вложенные области или рабочие элементы»
  And a project's node offers no delete command and answers «Корневая область меняется только вместе с проектом» to a request for it

Scenario: A deleted or foreign node answers not found (BR-NV-7)
  When Olga opens the page of «Транспорт» after it was deleted, and an area of «Другая» by its address in «Ромашка»
  Then she sees «Область не найдена» for each, and the switcher's context does not change

Scenario: A department's «Проекты» and «Команды» when empty (BR-NV-11)
  Given Ann created the department «Резерв»
  When Olga opens its «Проекты» and «Команды»
  Then they read «В департаменте нет проектов» and «К департаменту не привязаны команды», each offering to add only to Ann
```
