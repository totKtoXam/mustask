# US-8: Breadcrumbs, the switcher and the remembered context

- story: US-8
- depends-on: US-1, US-2, US-3, US-4, US-5
- shippable: no — the switcher opens the pages of US-3..US-5, and the context it holds is read by every page
- status: pending

As an **organization member**, I want **a trail that names where I am without inventing a hierarchy, a switcher that opens the object I choose, and a context that is still there when I come back**, so that **I move between areas, projects and teams in one step and keep my place**.

## Acceptance criteria

Cast as US-2. Olga «Участник» belongs to VENUS and to «Другая». Her teams in «Ромашка»: VENUS (own), MARS, JUPITER. The filters of the views and their memory are `feat-013`'s (US-11 of doc-012).

```gherkin
Scenario: The trail names the context, the page and the section (BR-NV-4; ADR-0028 rule 7)
  When Olga opens MARS's «Канбан» from A's «Команды», from «Команды» and from «Логистика»
  Then the address is the same each time and the trail reads «Ромашка / Команды / MARS / Канбан»
  And A's «Команды» reads «Ромашка / Проекты / A / Команды», «Склад»'s «Обзор» reads «Ромашка / Области / Логистика / A / Склад / Обзор», and «Обзор» reads «Ромашка / Обзор»
  And every part of a trail but the last opens its page, and no trail or menu puts a team under a project or a project under a department in «Проекты» or «Команды»

Scenario: The switcher offers the objects the user may see, her own teams first (BR-MX-28)
  When Olga opens the switcher in «Ромашка»
  Then it offers «Ромашка» alone first, then her own team VENUS, then the departments and the areas with their paths, the projects and the other teams, each group by name
  And its organization part lists her active organizations by name and ends with «Все организации»
  And it offers no deactivated or deleted object, and no object of «Другая»

Scenario: Typing narrows the offer at volume (BR-NV-9)
  Given an organization of 1 000 areas, 200 projects and 200 teams
  When Olga types part of a name in the switcher
  Then the offer narrows to the matching objects within 2 s, the paths of areas included, and a result of none reads «Ничего не найдено»

Scenario: Choosing an object opens its page and keeps the section where it can (BR-MX-28; ADR-0028 rule 7)
  Given Olga is on MARS's «Бэклог»
  When she chooses VENUS in the switcher
  Then VENUS's «Бэклог» opens, and the switcher shows «Ромашка» / VENUS
  And choosing A instead opens A's «Общие», choosing «Логистика» opens its «Обзор», and choosing «Ромашка» alone opens the organization's «Обзор»
  And a section the chosen object does not show her, such as «Настройки», gives way to that object's first section

Scenario: The context holds the organization and one object (BR-MX-28)
  Given Olga chose A and then chooses MARS
  Then the context is «Ромашка» / MARS, replacing A, and never three nested levels

Scenario: The context is remembered per user in the browser, and an address sets it (BR-MX-28)
  Given Olga chose MARS
  When she reloads the panel on «Участники», or signs out and in again in the same browser
  Then the switcher shows «Ромашка» / MARS
  And opening A's page by a link sets the context to «Ромашка» / A, which is then the remembered one
  And another user signing in in that browser gets her own remembered context, not Olga's

Scenario: A remembered object that is gone falls back with a notice (BR-MX-28; ADR-0028 rule 7)
  Given Olga's remembered context is «Ромашка» / D
  When Ann deactivates D and Olga reloads the panel
  Then the context becomes «Ромашка» alone and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка.», the same notice in every case below
  And when the object was a team or a department that was deleted, the same fallback and notice apply
  And when she is no longer a member of «Ромашка», the context falls back to her first organization, with the notice naming it

Scenario: A page that carries no context leaves the context alone (ADR-0024 § 2)
  Given Olga's context is «Ромашка» / MARS
  When she opens «Справочники → Языки»
  Then the switcher still shows «Ромашка» / MARS, and the remembered context is unchanged

Scenario: People move and the switcher follows (BR-MX-26; SC-04)
  When Ann moves Olga from VENUS to MARS
  Then Olga's next request offers MARS first and VENUS among the other teams, and no project, department, link or item changes

Scenario: A team the user does not choose is not forced on her (BR-NV-10; BR-MX-11)
  Given Olga chose no team and A's default team is VENUS
  When she opens the switcher
  Then no team is selected, and the default team does not choose one for her: the interim fallback is gone

Scenario: The switcher is usable by keyboard and at 640 px (ADR-0024 § 5)
  When Olga operates the switcher with the keyboard only at a width of 640 CSS px
  Then she opens it, narrows the offer, moves through the groups and chooses an object, and focus lands on the opened page's heading
```
