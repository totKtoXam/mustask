# US-1: A member reads a project's backlog, per level, over every item of the project

- story: US-1
- depends-on: -
- shippable: no — one of the three stories of `feat-039-project-board`, which doc-012 § Decomposition fixes as one line and one MR; US-2 reorders what this story lists and US-3 reads the same order
- status: pending

As an **organization member**, I want **a backlog per level over every item of a project, whatever team's backlog it stands on, reached from the page head's team drop-down**, so that **I see all of the project's work in one list, in the project's own order**.

## Acceptance criteria

Unless a scenario says otherwise: organization O has projects A («Альфа», key ALP) and B («Бета», key BET), both active and visible to every member. A has teams MARS and VENUS, B has team JUPITER. At the level «Истории», ALP-US-1 (S1) and ALP-US-2 (S2) stand on MARS's backlog, ALP-US-3 (S3) on VENUS's and BET-US-1 on JUPITER's, by their areas (mod-006 § Model); nothing has been reordered, and every state is «Новая». U is an organization member («Участник»), V a viewer («Читатель»). Addresses are below `/organizations/O`.

```gherkin
Scenario: A project's backlog holds every item of the project, from every team (BR-MX-17, BR-MX-16, BR-PB-2)
  When U opens `/projects/A/backlogs/stories`
  Then it lists S1, S2 and S3, with «Порядок» 1, 2 and 3, and no BET-US-1
  And MARS's backlog still lists S1 and S2, and VENUS's S3
  And no item stands twice on the page

Scenario: An item with no owning team is on the project's views (BR-MX-17, SC-02; owed by `mod-017-work-item-owning-team`)
  Given story S4 of A is owned by no team
  When U opens A's «Истории» backlog and A's board
  Then each shows S4 beside S1, S2 and S3
  And S4 is on no team's backlog or board

Scenario: A view holds its level's items in every state but Removed (BR-MX-17, mod-006 BR-M4-23; the Removed half owed by `mod-005-work-item-delete-restore`)
  Given A also has story S5 in «Закрыта», feature F1 and epic E1
  When U opens the «Истории» backlog of A
  Then it lists S1, S2, S3 and S5, and neither F1 nor E1
  And the «Фичи» backlog lists F1 alone and the «Эпики» backlog E1 alone
  And a story in the Removed category is on no level's view

Scenario: Children stand beneath their parent whatever the team (feat-027 BR-T-4..-6; re-run by `mod-017-work-item-owning-team` with owned items)
  Given S1 has task T1 on MARS's surfaces and task T2 on VENUS's, and bug B1 whose parent is S3
  When U opens A's «Истории» backlog and then MARS's
  Then on A's both tasks stand beneath S1 and B1 beneath S3, B1 not among the top-level rows
  And «Порядок» numbers the top-level rows 1..n only
  And on MARS's only T1 stands beneath S1

Scenario: The rows are drawn as the team backlog draws them, and a row's actions work (BR-PB-3, feat-027 BR-T-1..-3, -7, -9, -10, -13..-15; OQ-5)
  When U opens A's «Истории» backlog
  Then the columns are the hierarchy column, «Порядок», «Тип», «Заголовок», «Статус», «Трудоёмкость», «Бизнес-ценность», «Вид ценности»
  And the first state is expanded as deep as 2 000 drawn rows allow
  When U adds a child to S1 from its «+», types «Задача проекта» and presses Enter
  Then the task stands last beneath S1, the notice reads «Рабочий элемент ALP-TASK-<n> создан.», and it is on MARS's surfaces, the team of its parent
  And the creation writes no place on any order

Scenario: The head and the empty states carry no create command (BR-PB-6; OQ-2)
  When U opens A's backlog
  Then the `h1` reads «Альфа», the only tab «Бэклог», the level drop-down is named «Уровень: Истории»
  And the browser tab reads «Бэклоги — Mustask», and «Показать канбан» opens `/projects/A/boards/stories`
  And there is no «Новый рабочий элемент»
  Given a project with no story
  When U opens its «Истории» backlog
  Then the frame reads «Бэклог пуст» with the hint «В проекте нет рабочих элементов этого уровня.» and no action

Scenario: The team drop-down reaches the project's views (BR-PB-7; retired by `feat-043-area-project-team-navigation`)
  Given U is on `/projects/A/teams/MARS/backlogs/epics`
  When U opens «Сменить команду»
  Then the menu «Команды проекта» holds MARS (selected) and VENUS in their order, and «Весь проект» where the design puts it
  And choosing «Весь проект» opens `/projects/A/backlogs/epics`, the level kept, with «Весь проект» selected and `aria-current="page"`
  And focus is on «Сменить команду» of the new page, and on that page choosing VENUS opens `/projects/A/teams/VENUS/backlogs/epics`
  And ArrowDown opens the menu, Escape closes it and focus returns to the button
  And the same holds on «Канбан», where «Весь проект» opens `/projects/A/boards/epics`

Scenario: The shell on a project's backlog (BR-PB-9, OQ-6; retired by `feat-043-area-project-team-navigation`)
  Given U last chose VENUS at the switcher's team level in A
  When U opens `/projects/A/backlogs/epics`, `/projects/A/backlogs/features` and `/projects/A/backlogs/stories` in turn
  Then in the main navigation «Бэклоги» is current (`aria-current="page"`) at each level, «Канбан» is not, and the page shows no trail
  And the switcher's levels name A and VENUS, and none names «Весь проект»
  And pressing «Бэклоги» opens `/projects/A/teams/VENUS/backlogs/stories`
  When U is on `/projects/A/teams/MARS/backlogs/epics` and chooses «Весь проект» in the head's drop-down
  Then the switcher's team level still names MARS, and pressing «Бэклоги» opens `/projects/A/teams/MARS/backlogs/stories`

Scenario: A choice in the switcher on a project's backlog (BR-PB-9, OQ-6)
  Given U is on `/projects/A/backlogs/features`
  When U chooses project B at the switcher's project level
  Then `/projects/B/backlogs/features` opens, «Бэклоги» is current, and the switcher's team level names B's default team
  When U is on A's «Фичи» backlog again and chooses another organization
  Then the «Фичи» backlog of that organization's first project opens
  And «Главная» opens instead when that organization has no project
  When U is on A's «Фичи» backlog again and chooses MARS at the switcher's team level
  Then the page stays, the switcher names MARS, and «Бэклоги» now opens `/projects/A/teams/MARS/backlogs/stories`
  And a team page's own switches are as before: from `/projects/A/teams/MARS/backlogs/features` choosing project B opens B's default team's «Истории» backlog

Scenario: With no team at the switcher there is no current entry, and the page works (BR-PB-9; "no team in the project" is owed by `feat-028-org-level-teams`, before which every project keeps its first team)
  Given the switcher holds no team for project E: its teams are not read yet or failed to read, or E has none
  When U opens `/projects/E/backlogs/stories`
  Then the backlog lists E's stories, in the project's order
  And «Бэклоги» and «Канбан» are not shown in the main navigation, and none is current

Scenario: Team views are unchanged (BR-PB-8)
  When U opens MARS's, VENUS's and JUPITER's backlogs and boards
  Then each holds the items, in the order, at the address and by the requests that mod-006 and feat-027 give it
  And the menu's teams keep their order, the selected team and the keyboard path among them
  And a team board whose re-read fails keeps its columns, cards and open checklists with the error above them, the retry taking it away (feat-032 design § 7.1, the one change BR-PB-8 admits; the project's board does the same)

Scenario: Who reads (BR-PB-5)
  When V opens A's backlog and A's board
  Then V sees the same rows and cards as U
  And V's rows have no «+», no «Ещё действия» and no grip, and no row or card can be dragged (spec § Roles)
  And a person who is not a member of O gets «Организация не найдена.» and no row
  And an instance administrator with no membership in O gets the same

Scenario: A missing address is told from the outside in (BR-MX-1, mod-006 api-contract § 0.3)
  When U opens `/projects/A/backlogs/tasks` or `/projects/A/backlogs/sprints`
  Then the page reads «Страница не найдена»
  When U opens the backlog of a deactivated project, of a project of another organization, or of an id that names nothing
  Then the frame reads «Проект не найден.», and no «Команда не найдена.» can appear on a project page
  And a level the organization's process lacks answers 404 `boards.level.not_found` with «Уровень бэклога не найден.»

Scenario: A project the user may not see does not exist for them (BR-MX-21, BR-MX-24; owed by `feat-041-project-visibility`)
  Given project C is «Ограниченный» and U holds no grant on it
  When U opens C's backlog and board
  Then each answers «Проект не найден.», the same as a project that does not exist
  And A's views, their counters and the drop-down are the same as with no C

Scenario: A large project backlog (spec NFR: volumes, client load)
  Given A holds 5 000 stories across its teams
  When U opens A's «Истории» backlog
  Then the answer arrives within 2 s and holds only the items of A
  And the first state is feat-027 BR-T-7's: the 5 000 top-level rows alone pass 2 000, so every top-level row is drawn and none is expanded (the bound on large views is owed by `feat-040-team-surfaces-across-projects`, OQ-4)
  And the number of statements the read takes is the same for 50 stories as for 5 000
```
