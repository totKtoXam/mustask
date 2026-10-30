# US-1: The left menu by scope, «Настройки», and the address model

- story: US-1
- depends-on: -
- shippable: no — one L and one MR; the entries open the pages of US-2..US-5, and the retired addresses are retired only once those pages exist
- status: pending

As an **organization member**, I want **a left menu whose entries are the sections of what I have chosen, «Настройки» where they always were, and addresses that name the chosen object**, so that **I reach work by the object it belongs to, a link to a page opens that page, and an old address never leads me through a hierarchy the model does not have**.

## Acceptance criteria

Cast: organization «Ромашка» with Ann «Администратор», Olga «Участник» and Vera «Читатель»; departments «Логистика» (project nodes A and B) and «Финансы» (C); D at the top level; teams MARS, VENUS and JUPITER. Organization «Другая» has project Zeta and Olga is not a member of it.

```gherkin
Scenario: With nothing chosen the menu holds the organization's entries (BR-NV-1; BR-MX-27)
  When Olga opens «Ромашка» with «все» in department, team and project
  Then the left menu shows «Обзор», «Рабочие элементы», «Канбан», «Бэклог», «Области», «Проекты», «Команды», «Участники» and, last, «Настройки», in this order
  And «Канбан» and «Бэклог» open the aggregate views of every active project (US-10)

Scenario: The menu is the sections of what is chosen (BR-NV-1; gate-2)
  When Olga chooses project A in the project selector, then removes it and chooses MARS in the team selector, then the department «Логистика»
  Then with A the menu reads «Общие», «Структура», «Рабочие элементы», «Канбан», «Бэклог», «Команды», «Настройки»
  And with MARS it reads «Обзор», «Рабочие элементы», «Канбан», «Бэклог», «Проекты», «Участники и доступ», «Настройки»
  And with «Логистика» it reads «Обзор», «Рабочие элементы», «Канбан», «Бэклог», «Проекты», «Команды», «Настройки», and no choice needed a list to be opened first
  And «Рабочие элементы», «Канбан» and «Бэклог» are in the menu at every level

Scenario: Opening an entry opens its page and marks it (BR-NV-1)
  When Olga chooses «Команды» in the menu
  Then the list of the organization's teams opens at an address that names «Ромашка» and the list
  And «Команды» is marked as the current entry, and so is each entry on its own page

Scenario: «Настройки» is the shell's derived entry, as before (BR-NV-18; mod-015 BR-19)
  Given Ann has chosen project A and team MARS
  When she chooses «Настройки» at the end of the menu
  Then the settings navigation opens, titled «Настройки», with the main menu as a rail, and holds the group «Организация» («Общие», «Процесс») and the groups «Команда» and «Проект», each with «Параметры»
  And for Olga it holds «Организация» alone, and no object has a «Настройки» tab or menu entry of its own

Scenario: An entry the user may not open is absent (BR-NV-1; ADR-0024 § 1)
  Given the permissions served for Vera lack the code the «Участники» page needs
  When she looks at the main navigation
  Then «Участники» is absent and nothing is shown disabled, and the other entries keep their order
  And opening the «Участники» address shows «Доступ ограничен»

Scenario: A single object has one approved address (BR-NV-2; BR-MX-27)
  When Olga chooses MARS in the team selector from «Команды», from project A's menu and from the department «Логистика», with the other selectors at «все» except the department
  Then the address of MARS's «Канбан» names «Ромашка», the team prefix and MARS, and `departmentId` only where the department is chosen
  And no address of the panel has a team under a project, a project under an area, or an area under a project

Scenario: Several selectors ride on the most specific object's address (BR-NV-2; Q-1 A)
  When Olga has A in the project selector and MARS in the team selector and opens «Бэклог»
  Then the address is A's backlog with `teamId=<MARS>`, and a link to it opens the same selection and the same narrowed backlog
  And with MARS and «Логистика» chosen and no project, the address is MARS's with `departmentId=<Логистика>`

Scenario: A section is part of the address and survives a reload (BR-NV-2, BR-NV-3)
  Given Olga is on MARS's «Бэклог»
  When she reloads the page or sends its address to Ann
  Then MARS's «Бэклог» opens for each, in the shell, with MARS in the team selector and «Бэклог» marked in the menu

Scenario: The organization's settings hold only what is the organization's (BR-NV-5)
  When Ann opens «Настройки» with nothing chosen
  Then it holds the organization's own pages and the instance sections
  And it holds no project list, no team or area list and no members page: those are menu entries

Scenario: A retired address answers «Страница не найдена» and redirects nowhere (BR-NV-6)
  When Ann opens in turn `…/settings/teams`, `…/settings/teams/<id of MARS>`, `…/settings/areas`, `…/settings/areas/<id of «Логистика»>`, `…/settings/projects`, `…/settings/projects/deactivated`, `…/settings/members`, A's `…/settings/general`, `…/settings/teams` and `…/settings/areas`
  Then each shows «Страница не найдена», the address in the bar does not change, and no navigation entry is marked
  And the same holds for Olga, whether or not she may see A
  And a user who is not a member of «Ромашка» sees «Организация не найдена» for each

Scenario: An address that names nothing answers by level (BR-NV-7; BR-MX-24)
  When Olga opens the page of an area, a project or a team that does not exist, was deleted, or belongs to «Другая»
  Then she sees «Область не найдена», «Проект не найден» or «Команда не найдена» and the switcher's context does not change
  And a section the kind does not have, such as MARS's `…/sprints` before mod-007, shows «Страница не найдена»
  And a project that was deactivated answers «Проект не найден» on every section of its page, on its node's address and on its items' addresses
  And the path address of an item of «Другая» in «Ромашка», `…/projects/<Zeta>/workitems/<id>`, answers «Проект не найден»

Scenario: A not-found address that names the remembered object also falls back (BR-NV-7, BR-NV-10)
  Given Olga's remembered context is «Ромашка» / D and she is on D's page
  When Ann deactivates D and Olga reloads
  Then the page answers «Проект не найден» and the context becomes «Ромашка» alone with the notice «Ранее выбранный контекст недоступен. Выбрано: Ромашка.»
  And when the missing address names another object than the remembered one, the context does not change and no notice appears

Scenario: A section without the user's code answers «Доступ ограничен» (BR-NV-7)
  When Olga opens MARS's «Настройки» by its address
  Then she sees «Доступ ограничен», and the section is absent from MARS's sections as she sees them

Scenario: An address in another letter case or percent-encoded opens the same page (BR-NV-8; bug-003, tech-009 F4)
  When Olga opens `/Organizations/<id>/Teams`, `/organizations/<id>/%74eams` and an object's address with its id in capital letters
  Then each opens the page that the lower-case address opens, with its entry marked, the settings menu where the page has one, and the trail
  And a team or project whose id is written in capitals is found as the lower-case id finds it, and the switcher shows it

Scenario: A page that has no context keeps its address (ADR-0024 § 2)
  When Ann opens «Главная» or the instance section «Справочники → Языки»
  Then the page opens at its present address, with no organization in it, in the shell

Scenario: Existing section addresses stand (BR-NV-2)
  When Olga opens MARS's and A's boards and backlogs, «Логистика»'s and the organization's
  Then the department's open at `…/areas/<Логистика>/boards/<level>` and `…/areas/<Логистика>/backlogs/<level>`, the organization's at `…/boards/<level>` and `…/backlogs/<level>`, and the team's and the project's open at `…/teams/<MARS>/boards/<level>`, `…/teams/<MARS>/backlogs/<level>`, `…/projects/<A>/boards/<level>` and `…/projects/<A>/backlogs/<level>` as they do today, and a card opens at `…/projects/<A>/workitems/<identifier>`

Scenario: The shell is on the UI kit (OQ-5 A; tech-019)
  When the shell's top bar, navigation and page header are read
  Then they are built from the UI kit's components, and no shell file imports the legacy look the kit replaces, checked by the kit's guard test or a source-reading test added here
  And every scenario of this story holds on the kit

Scenario: Moving between sections and objects does not reload the document (NFR)
  When Olga moves through MARS's sections, opens A from MARS's «Проекты», and chooses «Логистика» in the switcher
  Then the browser requests no new document, the top bar and the navigation stay in place, and the browser's Back button returns through each step in turn
```
