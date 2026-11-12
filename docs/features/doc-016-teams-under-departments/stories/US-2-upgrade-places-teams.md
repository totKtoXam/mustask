# US-2: The upgrade places every team and project in a department

- story: US-2
- depends-on: US-1, US-5
- shippable: no — the required places of US-1 and US-5 cannot exist while unplaced teams and top-level projects do; each step ships with its line
- status: pending

As an **organization administrator**, I want **the upgrade to put every existing team, then every top-level project, into a department, and to cut the links that would cross departments, without losing members, grants or items**, so that **the stand keeps working as a strict tree: organization → department → teams and projects**.

Delivered in two parts (spec BR-TD-19): step 1 by `mod-NNN-teams-in-departments`, steps 2-3 by `mod-NNN-projects-in-departments`. Decided by the human, 2026-11-12: OQ-1 A, OQ-2 B, OQ-8 with the recommended placement of top-level projects.

Cast for this story (before the upgrade), organization «Ромашка»: departments «Логистика» and «Финансы»; projects A, B in «Логистика», C in «Финансы», F deactivated in «Финансы», D, E and H at the top level.

| Team | Department links | Project links |
|---|---|---|
| MARS | «Логистика» | A, B, C, D |
| VENUS | «Логистика», «Финансы» | A, B, H |
| JUPITER | «Логистика», «Финансы» | A, C, D |
| MARS2 | «Логистика», «Финансы» | F, H |
| PLUTO | none | D |
| SATURN | deleted earlier | - |

Organization «Другая»: department «Склад», project Z in it, team ORION with no department link and a link to Z. MARS owns C-7 in C (and C-8, deleted, mod-005); C's default team is JUPITER.

## Acceptance criteria

```gherkin
Scenario: Step 1, a team with one department link stands in that department
  When the upgrade runs
  Then MARS stands in «Логистика», though it is also linked to C in «Финансы»

Scenario: Step 1, several links: the department holding most of its linked projects
  When the upgrade runs
  Then VENUS stands in «Логистика», which holds A and B, and its link to «Финансы» is dropped
  And JUPITER, with A in «Логистика» and C in «Финансы», stands in «Логистика», first of the tie by name
  And MARS2 stands in «Финансы»: its deactivated project F counts, H at the top level does not

Scenario: Step 1, no department link
  When the upgrade runs
  Then ORION stands in «Склад», which holds Z
  And PLUTO, whose only project D stands in no department, stands in a created department «Команды без департамента» of «Ромашка»
  And if «Ромашка» already has a department «Команды без департамента», PLUTO is placed in it and none is created
  And an organization with no such team gains no such department

Scenario: Step 2, top-level projects follow their teams
  When the upgrade runs, after step 1
  Then D, linked to MARS and JUPITER («Логистика») and PLUTO («Команды без департамента»), stands in «Логистика»
  And H, linked to VENUS («Логистика») and MARS2 («Финансы»), stands in «Логистика», first of the tie by name
  And E, linked to no team, stands in a created department «Проекты без департамента», reused if one exists
  And no project of any organization stands at the top level, a deactivated one included

Scenario: Step 3, links crossing departments are cut
  When the upgrade runs, after step 2
  Then the links MARS–C, JUPITER–C, PLUTO–D and MARS2–H are removed, and every other link stays
  And C-7 and the deleted C-8 are «Без команды», on no team backlog and in no team sprint, with their project, state, assignee, «Область» and project backlog place unchanged
  And C's default team is «Не выбрана», since it named JUPITER
  And no item's «Изменён» moves and no history entry is written

Scenario: Nothing else changes
  When the upgrade runs
  Then every team keeps its name, description, members, team grants, sprints and backlog order, and every project its name, key, sub-areas, items and grants
  And the deleted team SATURN gets no department and stays deleted

Scenario: The upgrade is reported
  When the upgrade runs
  Then it logs, per organization: teams placed by one link, by most projects, by tie, by first link, in a created department; projects placed by most teams, by tie, in a created department; department–team links dropped; project–team links cut, each by name, with the count of items made «Без команды»; departments created
  And the MR names the created departments, the moved projects and the cut links from that log

Scenario: The remembered selection meets the moved objects
  Given Olga's remembered selection in «Ромашка» is «Финансы» / VENUS
  When she opens the panel after the upgrade
  Then the department selector reads «Логистика» and the team selector VENUS, and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика / VENUS.»

Scenario: The upgrade colours every department (BR-TD-20)
  Given «Ромашка» holds «Логистика» and «Финансы» with no colour, in that order of creation
  When the upgrade runs
  Then «Логистика» is blue and «Финансы» orange, and the created «Команды без департамента» and «Проекты без департамента» take the next farthest colours, in that order
  And «Склад» of «Другая» is blue, colours being per organization
  And running the upgrade again changes no colour
```
