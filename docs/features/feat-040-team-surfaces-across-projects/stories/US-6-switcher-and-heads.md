# US-6: The switcher's interim team level and the page heads

- story: US-6
- depends-on: US-1
- shippable: no — a team address with no project needs the switcher and the heads to follow, in the same MR
- status: pending

As an **organization member**, I want **the switcher and the page head to offer the organization's teams whatever project I have chosen**, so that **I reach any team's backlog or board in one step, and the project views stay reachable without a team menu**.

## Acceptance criteria

The common cast of spec § Stories. Rules: BR-TS-10, -11, -12; ADR-0024 § 2 (the switcher shows the address's context; a level the address does not carry shows the current selection, and changing it does not move the page). Interim until `feat-043-area-project-team-navigation`, which retires this level (doc-013 BR-NE-9). Proposed copy: the head's menu «Команды».

```gherkin
Scenario: The team level lists the organization's teams, the user's own first (BR-TS-10)
  Given team D of «Ромашка» is deleted
  When Max opens the switcher's team level with Alpha chosen, and then with Delta chosen
  Then both times it lists «VENUS», then «JUPITER» and «MARS», with no headings
  And D and Orion are not listed
  When Ann, a member of no team, opens it
  Then it lists «JUPITER», «MARS», «VENUS»

Scenario: The team level is the organization's, whatever its projects (BR-TS-10)
  Given an organization with team T1 and no project, Max a member of it
  When Max chooses it in the switcher
  Then the team level lists «T1», and «Бэклоги» and «Канбан» land on T1's «Истории» pages
  When the organization's teams fail to read
  Then the team level holds no team, «Бэклоги» and «Канбан» are hidden, and every other page works

Scenario: A project change keeps a chosen team (BR-TS-10)
  Given Max chose MARS in the switcher, which holds «Ромашка», Alpha and MARS
  When Max chooses Delta at the project level on Alpha's «Рабочие элементы»
  Then the switcher holds Delta and MARS, and «Бэклоги» still lands on MARS's «Истории» backlog

Scenario: A team address sets the organization and the team (BR-TS-10)
  Given Max's switcher holds «Другая», Zeta and Orion, Max being a member of both organizations
  When Max follows a link to MARS's board of «Фичи»
  Then the switcher holds «Ромашка» and MARS, and its project level shows Alpha, the level's first entry after the organization change (mod-002 BR-M1-24)
  When Max then chooses Beta at the project level
  Then the page stays MARS's board of «Фичи», and the switcher holds Beta and MARS

Scenario: Another team on a team page (BR-TS-10, -11; mod-015 BR-18; mod-006 US-1)
  Given Max is on MARS's board of «Фичи»
  When Max chooses VENUS in the switcher
  Then VENUS's «Истории» board opens, the same entry's landing
  When Max is back on MARS's board of «Фичи» and chooses VENUS in the head's drop-down
  Then VENUS's board of «Фичи» opens, the same level

Scenario: Another organization on a team page (BR-TS-10; mod-015 BR-18)
  Given Max, a member of «Другая» too, is on MARS's backlog of «Эпики»
  When Max chooses «Другая» in the switcher
  Then the backlog entry's landing in «Другая» opens: the «Истории» backlog of the team its switcher falls back to, Orion
  And with no team in «Другая», «Главная» opens

Scenario: On a project page a team choice moves nothing (BR-TS-10; feat-039 BR-PB-9 (c) kept)
  Given Max is on Alpha's board of «Фичи»
  When Max chooses MARS in the switcher
  Then the page stays, and «Бэклоги» and «Канбан» land on MARS's «Истории» pages
  When Max chooses Beta in the switcher
  Then Beta's board of «Фичи» opens

Scenario: «Бэклоги» and «Канбан» need the organization and a team (BR-TS-10; feat-039 BR-PB-9 (a))
  Given Max's switcher holds «Ромашка», Delta and MARS
  Then «Бэклоги» and «Канбан» are shown and land on MARS's «Истории» backlog and board
  And «Бэклоги» is current on each of MARS's backlog pages and on Delta's project backlog pages, and «Канбан» on the boards
  When an organization has no team at all
  Then both entries are hidden and every other page works

Scenario: With no team chosen, the level's first entry stands (BR-TS-10; mod-016 BR-M1-27 generalized)
  Given Max has chosen no team in «Ромашка»
  Then the switcher holds VENUS, and «Бэклоги» lands on VENUS's «Истории» backlog
  And for Ann, under the same conditions, it holds JUPITER
  When MARS, which Max had chosen, is deleted
  Then Max's switcher holds VENUS at the next read, with no notice

Scenario: The fallback once both this line and mod-020 are merged (BR-TS-10; doc-013; verified by whichever of the two merges second)
  Given Alpha names VENUS as its default team, and Ann has chosen no team, with Alpha chosen
  Then Ann's switcher holds VENUS
  When Ann chooses Delta, which names no default and has no linked team
  Then the switcher holds the level's first entry, JUPITER: a team held by the fallback is not chosen, so the project change applies the fallback again
  When Ann then chooses MARS, and then Alpha at the project level
  Then the switcher holds Alpha and MARS: a chosen team stays

Scenario: A team page's head lists the organization's teams (BR-TS-11)
  When Max opens MARS's backlog of «Фичи»
  Then the h1 reads «MARS», and its drop-down lists «VENUS», «JUPITER», «MARS», with «MARS» selected
  And no entry reads «Весь проект», and the menu is no longer named «Команды проекта»
  And its keyboard path is today's: arrows move among the teams, Escape closes it with focus back on its button

Scenario: A project page's head has no team menu (BR-TS-11; BR-MX-27)
  When Max opens Alpha's backlog of «Истории»
  Then the h1 reads «Alpha», with no team drop-down beside it, and the level drop-down is as today

Scenario: The project views are reached from the project's «Команды» (BR-TS-12; OQ-1 A)
  When Ann opens Alpha's «Команды», with MARS and VENUS linked, and Delta's, with no team linked
  Then each offers «Бэклог проекта» and «Доска проекта»
  And they open that project's «Истории» backlog and board
```
