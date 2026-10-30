# US-8: The header selectors, the trail and the remembered context

- story: US-8
- depends-on: US-1, US-2, US-3, US-4, US-5
- shippable: no — the selectors drive the menu of US-1 and the content of US-3..US-6, and the selection they hold is read by every page
- status: pending

As an **organization member**, I want **a header of four selectors — organization, department, team, project — each «все» or a chosen one, that rebuild the left menu and narrow the content**, so that **I reach any section of any object without first opening a list, and keep my place**.

## Acceptance criteria

Cast as US-2: «Логистика» holds A and B and is linked to MARS and VENUS; «Финансы» holds C; D at the top level; MARS is linked to A, B, C; VENUS to A; JUPITER to none. Olga «Участник» belongs to VENUS and to «Другая». The filters of the views and their memory are `feat-013`'s (US-11 of doc-012).

```gherkin
Scenario: The trail is four selectors (BR-NV-10; gate-2)
  When Olga opens «Обзор» in «Ромашка»
  Then the header trail reads «Ромашка» / «все» / «все» / «все» as four selectors in the order organization, department, team, project, each choosable
  And choosing a value in one sets it, shows it, and rebuilds the left menu to the sections of the most specific chosen object (US-1)

Scenario: Each selector offers the objects the user may see, narrowed by the others (BR-NV-17; Q-2 A)
  When Olga opens the project selector with «Логистика» chosen in the department selector
  Then it offers «все», A and B, and not C or D
  And with MARS also chosen it still offers A and B, and with VENUS in place of MARS it offers A only
  And the team selector with A chosen offers «все», MARS and VENUS, the user's own team first, then the others by name; with «Финансы» chosen it offers «все» and MARS
  And the department selector with A chosen offers «все» and «Логистика», and none offers project nodes, sub-areas, deactivated or deleted objects, or an object of «Другая»

Scenario: A chosen value the others no longer fit resets to «все» with a notice (BR-NV-17)
  Given the selection is «Логистика» / MARS / A
  When Olga chooses «Финансы» in the department selector
  Then the project selector resets to «все» because A is not in «Финансы», MARS stays, and she sees «Выбор сброшен: проект A.»

Scenario: «все» is no narrowing (BR-NV-17; Q-3 A)
  When Olga has MARS in the team selector and «все» in the project selector, and opens «Канбан»
  Then the board spans MARS's projects, and after she chooses A it shows MARS's items in A only
  And with A chosen and «все» in the team selector, A's «Рабочие элементы» show every item of A, and with only a department chosen no work-item view is narrowed by it

Scenario: Choosing a value rebuilds the menu and keeps the section where it can (BR-NV-10; Q-4 A)
  Given Olga is on MARS's «Бэклог» at the level «Истории»
  When she chooses VENUS in the team selector
  Then VENUS's «Бэклог» opens at «Истории» with none of the old filters in the address
  And choosing A in the project selector opens A's «Бэклог» narrowed to VENUS, choosing «все» in the team selector then widens it to A's whole backlog
  And a section the new menu lacks, such as «Участники и доступ» after choosing a project, gives way to the new menu's first entry

Scenario: Choosing «все» in every object selector returns to the organization's menu (BR-NV-1)
  Given the selection is «Логистика» / MARS / A
  When Olga sets the project, then the team, then the department selector to «все»
  Then each step rebuilds the menu (project, team, department, organization) and the page moves to the same-named section or the menu's first entry

Scenario: The organization selector (BR-NV-16; Q-5 A)
  When Olga opens the organization selector
  Then it lists her active organizations by name and ends with «Все организации»
  And choosing «Другая» opens its «Обзор» with «все» in the other three, and «Все организации» clears all four

Scenario: The trail names the selection, the entry and the section (BR-NV-4)
  When Olga opens the node page of «Склад» from «Области»
  Then the selectors read «Ромашка» / «Логистика» / «все» / A, the menu marks «Структура», and the page title trail reads «Структура / Склад»
  And no trail or menu puts a team under a project or a project under a department in «Проекты» or «Команды»

Scenario: Typing narrows a selector at volume (BR-NV-9)
  Given an organization of 1 000 areas, 200 projects and 200 teams
  When Olga types part of a name in a selector
  Then the offer narrows to the matching objects within 2 s, and a result of none reads «Ничего не найдено»

Scenario: The selection is remembered per user and organization, and the address wins in its tab (BR-NV-10; BR-MX-28)
  Given Olga chose MARS and A in «Ромашка», then «Другая», then returned to «Ромашка»
  When she reloads the panel on «Участники», or signs out and in again in the same browser
  Then the selectors read «Ромашка» / «все» / MARS / A, and «Другая» keeps the selection she left there
  And opening B's page by a link sets the project selector to B in that tab and makes it the remembered one
  And a second tab opened on VENUS's address shows VENUS, while a reload of the first tab, whose address names MARS, shows MARS
  And another user signing in in that browser gets her own remembered selection
  And opening an address in «Другая» while «Ромашка» is remembered changes the selection with no notice

Scenario: A remembered object that is gone drops to «все» with a notice (BR-NV-10)
  Given Olga's remembered selection is «Ромашка» / MARS / D
  When Ann deactivates D, and Olga reloads the panel
  Then the project selector reads «все», MARS stays, and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка.»
  And when she is no longer a member of «Ромашка», the selection falls back to her first organization, with the notice naming it
  And when she has no organization, the selectors are empty, «Главная» opens and the notice reads «Ранее выбранный контекст недоступен.»
  And an address that names the remembered gone object answers not found and drops it to «все» as well

Scenario: A page that carries no context leaves the selection alone (ADR-0024 § 2)
  Given Olga's selection is «Ромашка» / MARS
  When she opens «Справочники → Языки»
  Then the selectors still read «Ромашка» / MARS, and the remembered selection is unchanged

Scenario: People move and the selectors follow (BR-MX-26; SC-04)
  When Ann moves Olga from VENUS to MARS
  Then Olga's next request offers MARS first in the team selector and VENUS among the others, and no project, department, link or item changes

Scenario: No team is chosen for her (BR-NV-10; BR-MX-11)
  Given Olga chose no team and A's default team is VENUS
  When she chooses A
  Then the team selector reads «все»: the default team chooses nothing for her, and the interim fallback is gone

Scenario: The selectors work by keyboard and at 640 px (ADR-0024 § 5)
  When Olga operates the four selectors with the keyboard only at a width of 640 CSS px
  Then she opens each, narrows its offer, chooses a value or «все», and focus lands on the opened page's heading
```
