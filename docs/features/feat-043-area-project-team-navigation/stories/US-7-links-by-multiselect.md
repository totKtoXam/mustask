# US-7: Links and placements

- story: US-7
- depends-on: US-3, US-4, US-5
- shippable: no — the commands of the pages of US-3..US-5; without it the interim link pages cannot retire
- status: pending

As an **organization administrator**, I want **to add and remove the links between teams, projects and departments, and to place a project, from the page of either end through a searchable multiselect (a single-select for a placement)**, so that **the structure is edited where it is read, and both ends show the change at once**.

## Acceptance criteria

Cast as US-2: B is linked to MARS; JUPITER is linked to nothing; VENUS to A; «Логистика» holds A and B, «Финансы» holds C; D stands at the top level. Ann «Администратор», Olga «Участник». The multiselect offers only what the user may link; the finer offer for a holder of «Управление» on one end is owed by `mod-019-scoped-access`.

```gherkin
Scenario: Teams are added to a project through a multiselect (BR-NV-11; BR-MX-5; feat-028)
  When Ann opens the command to add teams on B's «Команды»
  Then the multiselect offers VENUS and JUPITER, and not MARS, which is linked already
  And typing «ven» narrows it to VENUS; Ann confirms, and without a page reload VENUS is listed in B's «Команды», opening VENUS's page, and B is listed in VENUS's «Проекты»
  And choosing several teams links them in one operation, all or none

Scenario: The same link is added from the team's page (BR-NV-11)
  When Ann opens the command to add projects on JUPITER's «Проекты», picks D and confirms
  Then D is listed in JUPITER's «Проекты» and JUPITER in D's «Команды», without a reload
  And the project selector, with JUPITER chosen, offers D (it replaces the project filter of JUPITER's «Канбан» and «Бэклог»)

Scenario: A team is linked to a department from either end (BR-AW-6; ADR-0031 rule 4)
  When Ann adds JUPITER on «Финансы»'s «Команды», and then adds «Финансы» on VENUS's «Обзор»
  Then «Финансы» lists JUPITER and VENUS once each, and each team's «Обзор» lists «Финансы»
  And a linked pair is not offered again, and linking it twice changes nothing

Scenario: Only departments are offered as a team's areas (BR-AW-6)
  When Ann opens the command to add departments on MARS's «Обзор»
  Then the multiselect offers departments only, never a project's node or a sub-area, because an area–team link joins a department only
  And a project's teams are added on the project's «Команды», never on its node's page

Scenario: A project is placed in a department one at a time (BR-NV-11; BR-AW-3; H-3 A)
  When Ann chooses «Добавить проект» on «Финансы»'s «Проекты»
  Then a searchable single-select offers the active projects that stand elsewhere, A, B and D, each with its present place, and not C and not the deactivated E
  And choosing D and confirming places D under «Финансы» without a reload: D is listed there, D's «Общие» reads «Финансы», and its sub-areas, items and teams are unchanged
  And choosing A names its move («A перемещается из «Логистика»») in the confirmation, and after it A is in one department only

Scenario: A project leaves a department by being moved to the top (BR-AW-3)
  When Ann chooses «Вынести на верхний уровень» on B's row in «Логистика»'s «Проекты»
  Then B stands at the top level, is listed in «Проекты» with no place, and is in no department's list
  And no team link and no item of B changes

Scenario: A refused placement changes nothing and says why (BR-AW-3, BR-AW-4, BR-AW-14)
  Given the top level holds a node named «B» besides B's own, and a sub-area chain puts B at the depth limit under «Финансы»
  When Ann moves B to the top level, and then moves B under «Финансы»
  Then the first answers «Область с таким названием уже есть на этом уровне» and the second «Дерево областей не может быть глубже 10 уровней», each in the dialog
  And B stays where it was, and so does the same refusal on «Вынести на верхний уровень»

Scenario: Unlinking a team that owns items asks where they go (BR-MX-12; feat-028)
  Given MARS owns 3 items in B
  When Ann removes MARS from B's «Команды»
  Then the confirmation counts the 3 items and offers one destination for them: another team linked to B, or «Без команды»
  And after the confirmation the items have that owning team, B's default team is cleared if it named MARS, and the link is gone from both pages

Scenario: The same unlink from the team's end (BR-MX-12; BR-NV-11)
  Given MARS owns 3 items in B
  When Ann removes B from MARS's «Проекты»
  Then the confirmation counts the 3 items and offers the same destinations, and the result is the one of the project's end

Scenario: Unlinking leaves both ends and the work as they are (BR-AW-8; BR-MX-2)
  When Ann removes VENUS from «Логистика»'s «Команды»
  Then VENUS and «Логистика» both exist, VENUS's project links and items are unchanged, and the link is gone from both pages

Scenario: Link changes refresh the selectors' offers (BR-NV-17)
  Given Ann has JUPITER chosen in the team selector and the project selector open
  When she links D to JUPITER, and then unlinks D
  Then the project selector offers D after the first and no longer after the second, without a reload
  And when she unlinks A from MARS while A and MARS are both chosen, the project selector resets to «все» with the notice «Выбор сброшен: проект A.»
  And a department's team link or a project's move changes the team and department offers the same way

Scenario: A reader sees the rows and no command (BR-NV-11)
  When Olga opens B's «Команды», «Логистика»'s «Проекты» and MARS's «Обзор»
  Then she sees the linked rows, each opening its page, and no add command and no unlink command

Scenario: A multiselect at volume (BR-NV-9)
  Given an organization of 200 teams and 200 projects
  When Ann opens the multiselect and types part of a name
  Then the offer narrows to the matching objects within 2 s, and a result of none reads «Ничего не найдено»

Scenario: An object that vanished while the dialog was open (BR-NV-7)
  Given Ann opened the multiselect on B's «Команды» and another administrator deleted VENUS
  When she confirms VENUS
  Then she sees «Команда не найдена», nothing is linked, and the dialog stays open with the offer refreshed

Scenario: A department or a project that changed while the dialog was open (BR-NV-7; BR-AW-9, BR-AW-10)
  Given Ann opened the placement dialog on «Финансы»'s «Проекты» and another administrator deleted «Финансы», or deactivated the chosen project
  When she confirms
  Then she sees «Область не найдена» or «Проект не найден», nothing is moved, and the dialog closes onto the refreshed page
```
