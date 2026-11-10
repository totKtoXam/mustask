# US-6: A user sees only what they take part in or their access allows

- story: US-6
- depends-on: US-1
- shippable: no — the rule (feat-041, reworded) and its application on every surface (`feat-NNN-sight-on-every-surface`) ship in order, and the selectors of US-3 read it
- status: pending

As an **organization member**, I want **to see only the departments, teams and projects I take part in or my access allows, everywhere**, so that **the panel shows my work, not the whole organization, and nothing hidden leaks through a count or a name**.

Cast: spec § Stories. Grants: Max holds department «Участник» on «Финансы»; Vera holds project «Читатель» on B. OQ-4 A and OQ-5 A assumed: every existing project's «Видим всей организации» is off. Under OQ-4 B the team-membership clauses do not hold; under OQ-4 C read doc-012 BR-MX-21.

## Acceptance criteria

```gherkin
Scenario: Sight through a team membership (BR-TD-12..-14)
  When Olga, a member of VENUS with no grant, opens «Ромашка»
  Then she sees the team VENUS, the project A (linked to VENUS), the team MARS (linked to A) and the department «Логистика» (holding VENUS and A)
  And she does not see B, C, D, JUPITER or «Финансы», in the selectors, «Структура», «Проекты», «Команды», «Обзор», pickers or search
  And «Логистика» on her «Структура» shows VENUS, MARS and A only

Scenario: Sight through a department grant
  When Max opens «Ромашка»
  Then he sees «Финансы», its team JUPITER (a grant on its department), its project C (reached through C's node) and MARS (linked to C)
  And he does not see D: seeing JUPITER does not disclose JUPITER's projects
  And he does not see «Логистика», A, B or VENUS

Scenario: Sight through a project grant
  When Vera opens «Ромашка»
  Then she sees B, MARS (linked to B) and «Логистика» (holding B), and nothing else

Scenario: «Видим всей организации»
  Given Ann turns «Видим всей организации» on for D
  Then Olga and Vera see D at the top level of «Структура», in «Проекты» and in the project selector, and JUPITER (linked to D)
  And they see «Финансы», which holds JUPITER, showing JUPITER alone and not C
  And only a holder of `organizations.visibility.write` on D (project «Менеджер», the administrator) sees the control

Scenario: The administrator sees everything
  When Ann opens «Ромашка»
  Then she sees every department, team and project, whatever the grants, memberships and settings

Scenario: The organization role applies on what one sees (BR-TD-16)
  Given Olga, organization «Участник», sees A through VENUS
  Then she may create and change A's work items and move cards on VENUS's board in A
  And Vera, organization «Читатель», reads B and changes nothing there

Scenario: A hidden object does not exist
  When Olga opens C's address, or a work item of C, or a team page of JUPITER
  Then she gets «Проект не найден.» or «Команда не найдена.», as for an address that names nothing
  And no count, «ещё», heading, empty text or notice is changed by C, JUPITER or «Финансы»

Scenario: Organizations are those one is a member of (BR-TD-15)
  Then Olga's organization selector and «Организации» list «Другая» and «Ромашка», and Ann's do not list «Другая»

Scenario: Sight changes on the next request
  When Ann adds Olga to JUPITER
  Then on Olga's next request she also sees JUPITER, C, D and «Финансы»
  And when Ann removes her from VENUS, A leaves her selectors and lists on the next request, and a selection naming A is reset with «Выбор сброшен: проект A.»

Scenario: The upgrade's effect is named (OQ-5 A)
  When the upgrade sets every existing project's «Видим всей организации» off
  Then the MR lists, per project, the members who saw it before and do not after
```
