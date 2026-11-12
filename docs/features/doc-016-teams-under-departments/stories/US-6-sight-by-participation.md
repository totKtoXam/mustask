# US-6: A user sees only what they take part in or their access allows

- story: US-6
- depends-on: US-1
- shippable: no — the rule (feat-041, reworded) and its application on every surface (`feat-NNN-sight-in-structure-lists`, `feat-NNN-sight-in-work-views`) ship in order, and the selectors of US-3 read it
- status: pending

As an **organization member**, I want **to see only the departments, teams and projects I take part in or my access allows, everywhere**, so that **the panel shows my work, not the whole organization, and nothing hidden leaks through a count or a name**.

Cast: spec § Stories. Grants: Max holds department «Участник» on «Финансы»; Vera holds project «Читатель» on B. Decided by the human, 2026-11-12: OQ-4 A (grant, team membership or «Видим всей организации») and OQ-5 A (every existing project's «Видим всей организации» is off).

## Acceptance criteria

```gherkin
Scenario: Sight through a team membership (BR-TD-12..-14)
  When Olga, a member of VENUS with no grant, opens «Ромашка»
  Then she sees the team VENUS, the project A (linked to VENUS), the team MARS (linked to A) and the department «Логистика» (holding VENUS and A)
  And she does not see B, C, D, JUPITER or «Финансы», in the selectors, «Структура», «Проекты», «Команды», «Обзор», pickers or search
  And «Логистика» on her «Структура» shows VENUS, MARS and A only

Scenario: Sight through a department grant
  When Max opens «Ромашка»
  Then he sees «Финансы», its team JUPITER (a grant on its department) and its projects C and D (reached through their nodes)
  And he does not see «Логистика», A, B, MARS or VENUS: nothing he sees is linked to them or stands in it

Scenario: Seeing a team does not disclose its projects
  Given Ann switches «Наследовать доступ» off on D's node
  When Max opens «Ромашка»
  Then he still sees JUPITER and C, and no longer sees D, though D is linked to JUPITER

Scenario: Sight through a project grant
  When Vera opens «Ромашка»
  Then she sees B, MARS (linked to B) and «Логистика» (holding B), and nothing else

Scenario: «Видим всей организации»
  Given Ann turns «Видим всей организации» on for D
  Then Olga and Vera see D under «Финансы» in «Структура», in «Проекты» and in the project selector, and JUPITER (linked to D)
  And they see «Финансы», which holds D and JUPITER, showing D and JUPITER alone and not C
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
  And A's address with `teamId` naming JUPITER answers «Команда не найдена.», and with `departmentId` naming «Финансы», «Область не найдена.»
  And a request to reorder JUPITER's backlog answers «Команда не найдена.», not «Недостаточно прав»
  And no count, «ещё», heading, empty text or notice is changed by C, JUPITER or «Финансы»

Scenario: Organizations are those one is a member of (BR-TD-15)
  Then Olga's organization selector and «Организации» list «Другая» and «Ромашка», and Ann's do not list «Другая»

Scenario: Sight changes on the next request
  When Ann adds Olga to JUPITER
  Then on Olga's next request she also sees JUPITER, C, D and «Финансы»
  And when Ann removes her from VENUS, A leaves her selectors and lists on the next request, and a selection naming A is reset with «Выбор сброшен: проект A.»

Scenario: The upgrade's effect is named (OQ-5 A)
  When the upgrade sets every existing project's «Видим всей организации» off
  Then the upgrade logs, per project, the members who saw it before and do not after, and the MR quotes that log
```
