# US-2: The upgrade places every existing team in a department

- story: US-2
- depends-on: US-1
- shippable: no — the required department of US-1 cannot exist while unplaced teams do; both ship in one line
- status: pending

As an **organization administrator**, I want **the upgrade to give every existing team a department without losing its members, grants, links or items**, so that **the stand keeps working after teams move under departments**.

Delivered by `mod-NNN-teams-in-departments`. The scenarios for several links and for none follow OQ-1 A; under OQ-1 B they read "the upgrade fails before changing anything and names the team", under OQ-1 C "the team is deleted as feat-028 BR-OL-9 deletes one".

Cast for this story (before the upgrade): MARS linked to «Логистика» only; VENUS linked to «Логистика» and «Финансы», with links to A and B (both in «Логистика»); JUPITER linked to «Логистика» and «Финансы», with links to C (in «Финансы») and A (in «Логистика»); ORION in «Другая» linked to no department, with a link to Z (in «Склад»); PLUTO in «Ромашка» linked to no department and only to D (top level); SATURN deleted earlier.

## Acceptance criteria

```gherkin
Scenario: A team with one department link stands in that department
  When the upgrade runs
  Then MARS stands in «Логистика»

Scenario: Several links: the department holding most of its projects (OQ-1 A)
  When the upgrade runs
  Then VENUS stands in «Логистика», which holds both its projects, and its link to «Финансы» is dropped
  And JUPITER, with one project in each, stands in «Логистика», first of the tie by name, and its link to «Финансы» is dropped

Scenario: No link: the department of its projects (OQ-1 A)
  When the upgrade runs
  Then ORION stands in «Склад», which holds Z

Scenario: No link and no project in any department: a created department (OQ-1 A)
  When the upgrade runs
  Then «Ромашка» gains one department «Команды без департамента», holding PLUTO
  And an organization with no such team gains no such department
  And if «Ромашка» already has a department «Команды без департамента», PLUTO is placed in it and none is created
  And if a top-level project node of «Ромашка» is named «Команды без департамента», the upgrade names the organization and fails before changing anything

Scenario: Deactivated projects count
  Given MARS2 is linked to «Логистика» and «Финансы», to the deactivated project F in «Финансы» and to nothing else
  When the upgrade runs
  Then MARS2 stands in «Финансы»: a linked project counts whether active or deactivated

Scenario: Nothing else changes
  When the upgrade runs
  Then every team keeps its name, description, members, team grants, project–team links, owned items, sprints, backlog places and order
  And no work item's «Изменён» moves, and no project, department or area node changes its place
  And the deleted team SATURN gets no department and stays deleted

Scenario: The upgrade is reported
  When the upgrade runs
  Then it logs, per organization, how many teams it placed by one link, by most projects, by tie, by projects with no link, and in a created department, and how many department–team links it dropped
  And the MR names the dropped links and the created departments

Scenario: The remembered selection meets the moved teams
  Given Olga's remembered selection in «Ромашка» is «Финансы» / VENUS
  When she opens the panel after the upgrade
  Then the department selector reads «Логистика» and the team selector VENUS, and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика / VENUS.»
```
