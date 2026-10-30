# US-8: Breadcrumbs, the switcher and the remembered context

- story: US-8
- depends-on: US-1, US-2, US-3, US-4, US-5
- shippable: no — the switcher opens the pages of US-3..US-5, and the context it holds is read by every page
- status: pending

As an **organization member**, I want **a trail that names where I am without inventing a hierarchy, a switcher that opens the object I choose, and a context that is still there when I come back**, so that **I move between areas, projects and teams in one step and keep my place**.

## Acceptance criteria

Cast as US-2. Olga «Участник» belongs to VENUS and to «Другая». Her teams in «Ромашка»: VENUS (own), MARS, JUPITER. The context is the switcher's display and the start point of a choice; it narrows no page (BR-NV-10). The filters of the views and their memory are `feat-013`'s.

```gherkin
Scenario: The trail names the context, the page and the section (BR-NV-4; ADR-0028 rule 7)
  When Olga opens MARS's «Канбан» from A's «Команды», from «Команды» and from «Логистика»
  Then the address is the same each time and the trail reads «Ромашка / Команды / MARS / Канбан»
  And A's «Команды» reads «Ромашка / Проекты / A / Команды», «Склад»'s «Обзор» reads «Ромашка / Области / Логистика / A / Склад / Обзор», and «Обзор» reads «Ромашка / Обзор»
  And every part of a trail but the last opens its page, and the trail of a project or a team names no department and no project

Scenario: The switcher offers the objects the user may see (BR-NV-10; BR-MX-28)
  When Olga opens the switcher in «Ромашка»
  Then it offers «Ромашка» alone first, then her own team VENUS, then the departments, the projects and the other teams, each group by name
  And it does not offer project nodes or sub-areas, a deactivated or deleted object, or an object of «Другая»
  And its organization part lists her active organizations by name and ends with «Все организации»

Scenario: Typing narrows the offer at volume (BR-NV-9)
  Given an organization of 1 000 areas, 200 projects and 200 teams
  When Olga types part of a name in the switcher
  Then the offer narrows to the matching objects within 2 s, and a result of none reads «Ничего не найдено»

Scenario: Choosing an object of the same kind keeps the section and the level (BR-NV-10; ADR-0028 rule 7)
  Given Olga is on MARS's «Бэклог» at the level «Истории» with the filter `teamId` of another page in the address
  When she chooses VENUS in the switcher
  Then VENUS's «Бэклог» opens at «Истории», the switcher shows «Ромашка» / VENUS, and the address carries none of the old filters

Scenario: Choosing an object of another kind opens its first section (BR-NV-10; ADR-0028 rule 7)
  Given Olga is on MARS's «Бэклог»
  When she chooses A, then «Логистика», then «Ромашка» alone
  Then A's «Общие» opens, then «Логистика»'s «Обзор», then the organization's «Обзор»
  And a section the chosen object does not show her, such as «Настройки», gives way to that object's first section
  And choosing «Другая» in the organization part opens «Другая»'s «Обзор»

Scenario: The context holds the organization and one object (BR-NV-10; BR-MX-28)
  Given Olga chose A and then chooses MARS
  Then the context is «Ромашка» / MARS, replacing A, never three nested levels
  And on an organization page such as «Участники» the switcher shows that context and no list narrows by it: «Рабочие элементы» under the context MARS still lists every project's items

Scenario: A project's node and a sub-area set the context to the project (BR-NV-4, BR-NV-10)
  When Olga opens A's node page, and then «Склад»
  Then the switcher shows «Ромашка» / A on both, and «Области» is the marked entry

Scenario: The context is remembered per user and organization, and the address wins in its tab (BR-NV-10; BR-MX-28)
  Given Olga chose MARS in «Ромашка», then «Другая», then returned to «Ромашка»
  When she reloads the panel on «Участники», or signs out and in again in the same browser
  Then the switcher shows «Ромашка» / MARS, and «Другая»'s context is the one she left there
  And opening A's page by a link sets the context to «Ромашка» / A in that tab and makes it the remembered one
  And a second tab opened on VENUS's address shows «Ромашка» / VENUS, while a reload of the first tab, whose address names MARS, shows MARS
  And another user signing in in that browser gets her own remembered context
  And opening an address in «Другая» while «Ромашка» / MARS is remembered changes the context with no notice

Scenario: A remembered object that is gone falls back with a notice (BR-NV-10; BR-MX-28)
  Given Olga's remembered context is «Ромашка» / D
  When Ann deactivates D, or deletes a remembered team or department, and Olga reloads the panel on an organization page
  Then the context becomes «Ромашка» alone and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка.», the same notice in every case
  And when she is no longer a member of «Ромашка», the context falls back to her first organization, with the notice naming it
  And when she has no organization, the context is empty, «Главная» opens and the notice reads «Ранее выбранный контекст недоступен.»

Scenario: A page that carries no context leaves the context alone (ADR-0024 § 2)
  Given Olga's context is «Ромашка» / MARS
  When she opens «Справочники → Языки»
  Then the switcher still shows «Ромашка» / MARS, and the remembered context is unchanged

Scenario: People move and the switcher follows (BR-MX-26; SC-04)
  When Ann moves Olga from VENUS to MARS
  Then Olga's next request offers MARS first and VENUS among the other teams, and no project, department, link or item changes

Scenario: No team is chosen for her (BR-NV-10; BR-MX-11)
  Given Olga chose no team and A's default team is VENUS
  When she opens the switcher on A
  Then no team is selected: the default team chooses nothing for her, and the interim fallback is gone

Scenario: The switcher is usable by keyboard and at 640 px (ADR-0024 § 5)
  When Olga operates the switcher with the keyboard only at a width of 640 CSS px
  Then she opens it, narrows the offer, moves through the groups and chooses an object, and focus lands on the opened page's heading
```
