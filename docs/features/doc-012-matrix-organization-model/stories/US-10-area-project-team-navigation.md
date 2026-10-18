# US-10: A user moves between areas, projects and teams

- story: US-10
- depends-on: US-1, US-2, US-3, US-4, US-5, US-6, US-7, US-8, US-9, US-12
- shippable: no — an analysis story; `feat-043-area-project-team-navigation` delivers it right after `mod-018-area-workspace` (doc-013), over the links and views of US-1 to US-4 and US-7; its grant, visibility, area-view and sprint clauses are owed by `mod-019-scoped-access`, `feat-041-project-visibility`, `feat-042-area-board-and-overview` and `mod-007-iterations`
- status: pending

As an **organization member**, I want **to reach each area, project and team of my organization from the main navigation and the context switcher, each on a page of its own**, so that **I find work by the object it belongs to, am never led through a hierarchy the model does not have, and keep my place as I move**.

## Acceptance criteria

The filters of the views and how they are remembered are US-11's. A section whose content a later line brings is absent until that line adds it to the page, and a clause that needs a grant, a visibility other than «Организация», an area view or a sprint is owed by that line, which re-runs it (doc-013).

```gherkin
Background:
  Given organization «Ромашка» has the area «Логистика» with the child areas «Склад» and «Транспорт», projects A, B, C and D, and teams MARS, VENUS and JUPITER
  And MARS is linked to A, B and C, and VENUS to A; «Логистика» is linked to A, B, MARS and VENUS, and «Склад» to A and C; D and JUPITER have no link
  And Olga is an organization «Участник» who belongs to VENUS and holds no grant
  And Pavel is an organization «Участник» who holds «Управление» on project B and on team VENUS only
  And Irina is an organization «Участник» who holds «Чтение» on «Логистика» only; Pavel's and Irina's grants, and every clause that reads them, are owed by `mod-019-scoped-access`

Scenario: The main navigation names areas, projects and teams as separate entries (BR-MX-27; brief § 11.1)
  When Olga opens «Ромашка»
  Then the main navigation shows «Обзор», «Рабочие элементы», «Области», «Проекты», «Команды», «Участники» and «Настройки», in this order, none of them waiting for an object to be chosen in the switcher
  And «Канбан» and «Бэклоги» are no longer entries: a board and a backlog are sections of an area's (from `feat-042-area-board-and-overview` on), a project's or a team's page

Scenario: An entry or a section whose page the user may not open is not shown (BR-MX-23, BR-MX-24; ADR-0024 § 1)
  Given the effective permissions served for Olga lack the code the «Участники» page needs
  When she looks at the main navigation
  Then «Участники» is absent, nothing is shown disabled, and the other entries keep their order
  And opening the «Участники» page by its address shows «Доступ ограничен»; the same holds for every section of an object's page, by the object's own codes from `mod-019-scoped-access` on

Scenario: Each object appears once in its list, whatever its links (BR-MX-2, BR-MX-16, BR-MX-21; SC-03, SC-06, SC-07, SC-08; brief § 11.6.2)
  When Olga opens «Обзор», «Проекты», «Области» and «Команды» in turn
  Then «Обзор», the organization's home page, lists her active organizations beside its content (feat-045, folded in by doc-013), shows her team VENUS first, then the numbers of areas, projects and teams she may see (3, 4 and 3), each opening its list, and lists the area tree, the projects and the teams, each row opening its object's page
  And «Проекты» lists A, B, C and D once each, D with no link included, and «Команды» lists MARS, VENUS and JUPITER once each
  And «Области» shows «Логистика» with «Склад» and «Транспорт» beneath it, each area once
  And no list puts a project inside an area or a team inside a project, and every row opens its object's page

Scenario: The organization's settings hold only what is the organization's (replaces mod-002 BR-M1-23)
  When an organization admin opens «Настройки»
  Then it holds the organization's own pages and the instance sections, and no project's, team's or area's pages: the team and area pages that earlier lines placed there are gone from it
  And projects are created, deactivated and restored from «Проекты», memberships managed from «Участники», and an area's, a project's and a team's settings are the «Настройки» section of its own page

Scenario: An area's page (brief § 11.2; SC-03, SC-08)
  When Olga opens «Логистика»
  Then its sections are «Обзор», «Канбан» and «Бэклог» (owed by `feat-042-area-board-and-overview`), «Проекты», «Команды» and «Участники» (owed by `mod-019-scoped-access`); «Настройки» is added only for a holder of «Управление» on the area
  And until `feat-044-area-analytics` there is no analytics section: the area's figures are those of its «Обзор» (US-8)
  And «Обзор» shows the child areas «Склад» and «Транспорт» and (owed by `feat-042-area-board-and-overview`) counts 3 projects (A, B and C, as «Включая дочерние области» is on), 2 teams and 1 participant (US-8)
  And «Проекты» lists A and B, its own links, and not C, which «Склад»'s «Проекты» lists and «Логистика»'s «Обзор» counts; «Команды» lists MARS and VENUS; «Участники» lists Irina, direct, and «Склад»'s lists her, inherited (US-5)
  And «Склад»'s «Обзор» shows «Логистика» as its parent, and every area, project and team shown opens its own page

Scenario: A project's page (brief § 11.3; SC-02, SC-06; BR-MX-11)
  When Olga opens project A
  Then its sections are «Общие», «Рабочие элементы», «Канбан», «Бэклог», «Команды», «Спринты» (owed by `mod-007-iterations`) and «Участники и доступ» (owed by `mod-019-scoped-access`); «Настройки» is added only for a holder of «Управление» on A
  And there is no releases or milestones section (spec § Out of scope), and «Спринты» is the read-only list of the Closed sprints that recorded A's items (US-12)
  And «Общие» shows A's areas «Логистика» and «Логистика / Склад», and «Команды» shows every team linked to A, MARS and VENUS, each opening its own page
  And B's «Команды» shows MARS marked «По умолчанию: единственная команда», as B's only linked team, and a team that a project's settings name is marked «По умолчанию»

Scenario: Work items are listed across projects and filtered by team (BR-MX-9, BR-MX-16, BR-MX-21; brief § 11.3)
  Given A has items owned by MARS, items owned by VENUS and items with no owning team
  When Olga opens «Рабочие элементы»
  Then she sees the items of every project she may see, at most 50 to a page, newest first and then by identifier, each once, with its project and owning team
  And A's «Рабочие элементы» section is the same list fixed to A, whose «Команда» filter offers exactly MARS, VENUS and «Без команды»
  And MARS lists only the items MARS owns, «Без команды» only those with no owning team, and clearing the filter lists every item of A again

Scenario: A project with no team or area has its whole page (BR-MX-2; SC-07; brief § 11.6.7)
  When Olga opens project D
  Then every section of D is shown, «Канбан» and «Бэклог» included, though no team is linked
  And «Команды» shows «К проекту не привязана ни одна команда», with the command to link a team only for a holder of «Управление» on D, and «Общие» shows «Проект не входит ни в одну область», with the command to link an area only for a holder of «Управление» on D and no prompt to create an area

Scenario: A team's page spans its projects (brief § 11.4; SC-01, SC-05)
  When Olga opens MARS
  Then its sections are «Обзор», «Канбан», «Бэклог», «Спринты» (owed by `mod-007-iterations`), «Проекты» and «Участники и доступ»; «Настройки» is added only for a holder of «Управление» on MARS
  And there is no capacity or analytics section (spec § Out of scope)
  And «Проекты» lists A, B and C, «Обзор» shows its area «Логистика», each opening its own page, and «Спринты» is the team's sprint list and sprint board (US-9, US-12)
  And MARS is one page, the same whether it is reached from A, B, C or «Логистика»
  And VENUS's «Участники и доступ» lists Olga as a member, who gets no role from it, apart from the grants on VENUS: Pavel's «Управление»

Scenario: A project linked to a team shows up on the team's surfaces (BR-MX-5, BR-MX-17; brief § 11.4)
  When an organization admin links D to VENUS from VENUS's «Проекты»
  Then without a page reload D is listed in VENUS's «Проекты» and VENUS in D's «Команды»
  And VENUS's «Канбан» and «Бэклог» offer D in their project filter, and an item of D that VENUS comes to own shows on them

Scenario: Links are added through a searchable multiselect (BR-MX-5, BR-MX-6, BR-MX-27; brief §§ 11.6.5-6)
  When Pavel opens the command to add teams on B's «Команды»
  Then the multiselect offers VENUS only: not MARS, which is linked already, nor JUPITER, on which he holds no «Управление»
  And typing «ven» narrows it to VENUS; he confirms, and without a page reload VENUS is listed in B's «Команды», opening VENUS's page
  And B's areas on «Общие», an area's projects and teams, and a team's projects and areas are added the same way
  And a linked row carries the unlink command (US-3) for a holder of «Управление» on both ends of a project–team link, or on either end of an area link; Olga sees the rows and no command

Scenario: A project the user may not see is absent from every list (BR-MX-7, BR-MX-21; SC-05; owed by `feat-041-project-visibility`)
  Given C is «Ограниченный» and Olga holds no grant on C
  When Olga opens MARS's «Проекты», «Проекты», «Обзор», «Рабочие элементы» and the context switcher
  Then C appears in none of them, and each of them, rows, counts and pages, equals what Olga sees in a twin of «Ромашка» seeded without C
  And C's page, any of its sections and any of its items, opened by address, answer «Проект не найден», as a project that does not exist does

Scenario: An address names only the object and its organization (BR-MX-27; brief § 11.6.9)
  When Olga opens MARS's «Канбан» from A's «Команды», from «Команды» and from «Логистика»
  Then the address is the same each time and names only «Ромашка» and MARS, and the trail reads «Ромашка / Команды / MARS / Канбан»
  And A's «Команды» reads «Ромашка / Проекты / A / Команды», and «Склад»'s «Обзор» reads «Ромашка / Области / Логистика / Склад / Обзор»
  And no trail or menu puts a team under a project or a project under an area, and every part of a trail but the last opens its page

Scenario: An address that names nothing answers not found, and nothing redirects (BR-MX-1, BR-MX-24, BR-MX-27; spec § Existing data)
  When Olga opens the page of an area, a team or a project that does not exist, was deleted or deactivated, or belongs to another organization
  Then she sees «Область не найдена», «Команда не найдена» or «Проект не найден», and the switcher's context does not change
  And an address of a retired kind — a team's board or backlog nested under a project, a project's settings of teams, of areas or its general settings, a team's or an area's page in the organization's settings, or the organization settings' project list or members page — shows «Страница не найдена», whether or not she may see the project, and redirects nowhere; each kind answers so from the line that retired it (spec § Decomposition, Breaks)
  And opening MARS's «Настройки» by its address, without «Управление» on MARS, shows «Доступ ограничен»
  And a user who is not a member of «Ромашка» sees «Организация не найдена» for each of these addresses

Scenario: The switcher offers the objects the user may see, her own teams first (BR-MX-21, BR-MX-28)
  When Olga opens the context switcher in «Ромашка»
  Then it offers «Ромашка» alone first, then her own teams (VENUS), then the areas, the projects and the other teams, each group by name, an area with its path
  And its organization part lists her active organizations by name and ends with «Все организации», as today
  And typing part of a name narrows the offer, in an organization of 1 000 areas, 200 projects and 200 teams too
  And choosing an object sets the context to «Ромашка» and that one object, replacing the object chosen before
  And once Olga is moved from VENUS to MARS, her next request offers MARS first and VENUS among the other teams, and no project, area, link or item changes (BR-MX-26; SC-04)

Scenario: Choosing an object opens its page, keeping the section where it can (BR-MX-28; ADR-0024 § 2 as amended by ADR-0028)
  Given Olga is on MARS's «Бэклог»
  When she chooses VENUS in the switcher
  Then VENUS's «Бэклог» opens
  And choosing A instead opens A's «Общие», «Логистика» its «Обзор», and «Ромашка» alone the organization's «Обзор»
  And a section the chosen object does not show her, such as «Настройки», gives way to that object's first section

Scenario: The context is remembered per user in the browser, and an address sets it (BR-MX-28)
  Given Olga chose MARS in the switcher
  When she reloads the panel on «Участники», or signs out and in again in the same browser
  Then the switcher shows «Ромашка» / MARS
  And opening A's page by a link sets the context to «Ромашка» / A, which is then the remembered one
  And another user signing in in that browser gets their own remembered context, not Olga's

Scenario: A remembered object that is gone or hidden falls back, with a notice (BR-MX-21, BR-MX-28)
  Given Olga's remembered context is «Ромашка» / C
  When C is deactivated, or (owed by `feat-041-project-visibility`) becomes «Ограниченный» with no grant of hers, and she reloads the panel
  Then the context becomes «Ромашка» alone and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка.», the same notice in both cases
  And when she is no longer a member of «Ромашка», the context falls back to her first organization, with the same notice

Scenario: Empty states — an organization with no areas, objects with no links (BR-MX-2; brief §§ 11.5, 11.6.7)
  Given organization «Север» has projects and teams and no area
  When an organization admin and then a member open «Области»
  Then both see «В организации пока нет областей», and only the admin is offered to create one
  And «Проекты», «Команды» and every project's and team's page work as in «Ромашка», and nothing asks for an area first
  And in «Ромашка», JUPITER's «Проекты» shows «Команда не привязана ни к одному проекту», and «Транспорт»'s «Проекты» and «Команды» show «К области не привязаны проекты» and «К области не привязаны команды», each offering to link only to a holder of «Управление» on the object

Scenario: Moving between sections and objects does not reload the page (brief § 17)
  When Olga moves through MARS's sections, opens A from MARS's «Проекты», and chooses «Логистика» in the switcher
  Then the browser requests no new document, the top bar and the navigation stay in place throughout, and its back button returns through each step in turn
```
