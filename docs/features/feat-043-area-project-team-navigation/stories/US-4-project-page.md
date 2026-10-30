# US-4: A project chosen: its menu and settings

- story: US-4
- depends-on: US-1, US-2
- shippable: no — the page that «Проекты» opens and that takes over the project's interim settings pages (US-9)
- status: pending

As an **organization member**, I want **choosing a project to put its sections in the left menu — its place in the structure, its teams and its settings**, so that **everything about the project is in one place, a project with no team or no department still has its whole page, and the administrator no longer hunts for the project in the organization's settings**.

## Acceptance criteria

Cast as US-2: project A stands under «Логистика», has the sub-areas «Склад» and «Транспорт», and the teams MARS and VENUS; B stands under «Логистика» and has MARS only; D stands at the top level with no team. Ann «Администратор», Olga «Участник». «Спринты» (`mod-007-iterations`), «Участники и доступ» (`mod-019-scoped-access`) and the visibility in «Настройки» (`feat-041-project-visibility`) are absent here, each in its place, and their owners re-run the clauses that need them.

```gherkin
Scenario: A project's sections (BR-NV-3; US-10 of doc-012)
  When Olga chooses A in the project selector
  Then the left menu holds «Общие», «Структура», «Рабочие элементы», «Канбан», «Бэклог», «Команды» and, last, the derived «Настройки», in this order, and A's «Общие» opens
  And there is no releases or milestones section, and no «Спринты» or «Участники и доступ» section

Scenario: «Общие» shows the project and its place (BR-NV-4; ADR-0031)
  When Olga opens A's «Общие»
  Then she sees its name, key, description, its place «Логистика» as a path that opens the department, and «Структура проекта», a link to A's node page with its sub-areas
  And the selectors read «Ромашка» / «все» / «все» / A, with no department chosen for her

Scenario: A project's place is moved from «Общие» (BR-NV-11; BR-AW-3)
  When Ann chooses «Переместить» on A's «Общие» and picks «Финансы»
  Then A's «Общие» reads «Финансы», A is listed in «Финансы»'s «Проекты» and not in «Логистика»'s, and its sub-areas, items, teams and backlog places are unchanged
  And picking the top level places A at the top; a destination that holds a sibling with A's name, the top level included, answers «Область с таким названием уже есть на этом уровне», and a destination that would pass 10 levels answers «Дерево областей не может быть глубже 10 уровней»; A stays where it was in both cases
  And the command is a single-select, one project at a time (BR-NV-11; H-3 A)
  And Olga sees the place and no «Переместить»

Scenario: A project with no team and no department has its whole page (BR-MX-2; SC-07)
  When Olga opens project D
  Then every section is shown, «Канбан» and «Бэклог» included
  And «Команды» shows «К проекту не привязана ни одна команда», with the command to link a team only for Ann
  And «Общие» shows «Проект не входит ни в один департамент» (D stands at the top level), with the command to place it only for Ann and no prompt to create a department

Scenario: «Команды» lists the project's teams and its default (BR-MX-11; feat-028)
  When Olga opens A's «Команды» and then B's
  Then A lists MARS and VENUS, each opening the team's page, and B lists MARS marked «По умолчанию: единственная команда»
  And a team that A's settings name is marked «По умолчанию»
  And each row offers Ann the unlink; when the team owns items in the project, its confirmation asks where they go (US-7)

Scenario: «Параметры» of the project hold name, description and the default team (BR-NV-14, BR-NV-18; BR-MX-11)
  When Ann has A chosen and opens «Настройки», the group «Проект» and «Параметры»
  Then she renames A, changes its description, and picks A's default team from A's linked teams; the pick is marked «По умолчанию» in «Команды»
  And the key cannot be changed, and no visibility field is shown until `feat-041-project-visibility`
  And renaming A renames its node: «Логистика» lists the new name, and a name taken among the node's siblings answers «Область с таким названием уже есть на этом уровне» on the form

Scenario: The default team affects creation only again (BR-NE-9; BR-MX-11)
  Given A's default team is VENUS and Olga has «все» in the team selector
  When Olga chooses A and opens the team selector
  Then it offers A's teams with Olga's own team first, and VENUS's mark in A's «Команды» does not move the selector, which stays «все»
  And a new item created from A's «Рабочие элементы» starts with the owning team VENUS (BR-MX-10)

Scenario: A project is deactivated from «Проекты» and has no page then (BR-NV-13; BR-NV-14)
  When Ann deactivates D from «Проекты»
  Then D's address and every section answer «Проект не найден», and «Проекты» lists D only under «Деактивированные»
  And after «Восстановить» D's page opens with its place and its links as they were

Scenario: A project is created from «Проекты» with its place (BR-NV-14; BR-AW-11)
  When Ann creates the project «Зета» in «Финансы» from «Проекты»
  Then the project exists once, its node is under «Финансы», its page opens at «Общие», and no team or department is created by it (BR-MX-2)

Scenario: The project's sections for a reader (BR-NV-3)
  When Vera chooses A
  Then her menu holds «Общие», «Структура», «Рабочие элементы», «Канбан», «Бэклог» and «Команды», and no command in them
  And her «Настройки» holds no «Проект» group, and the address of A's «Параметры» shows «Доступ ограничен»

Scenario: The project's board and backlog open from its page (BR-NV-1)
  When Olga opens A's «Канбан» and «Бэклог»
  Then she sees the project's board and backlog as before, each with the level drop-down and the «Команда» filter in the address (mod-020), and the entries «Бэклог» and «Канбан» are in the menu only once a project or a team is chosen
  And a card opens in A over the page, as it does today
```
