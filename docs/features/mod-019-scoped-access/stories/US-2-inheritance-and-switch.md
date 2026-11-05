# US-2: A department's grants reach the project nodes below it, until «Наследовать доступ» is off

- story: US-2
- depends-on: US-1
- shippable: no — the reach and the switch are read only by US-3's policy
- status: pending

As a **holder of «Управление» on a department, or an organization admin**, I want **a department's grants to reach its project nodes and their sub-areas, and to cut that off on a node with «Наследовать доступ»**, so that **the head of a department works across its projects and a project that must stand apart can stand apart (BR-SA-5..-7, doc-012 US-1's switch clauses)**.

## Acceptance criteria

Fixture of US-1, plus the sub-area «Платформа» under A's node and the sub-area «Склад» under B's node. «Наследовать доступ» is on everywhere. The switch's label is «Наследовать доступ»; its page is the node's «Структура» (project node, sub-area) or the department's «Обзор».

```gherkin
Scenario: A department grant reaches the projects below it
  Given Pavel holds «Изменение» on «Логистика» only
  When he changes an item of A, an item of B, and an item of D
  Then the changes to A and B are kept, and the change to D answers 403 «Недостаточно прав»

Scenario: Switching a project node off stops what comes from above, not what is held there
  Given Pavel holds «Управление» on «Логистика», and Kirill, a «Читатель», holds «Изменение» on project B
  When an organization admin switches «Наследовать доступ» off on B's node
  Then Pavel's next change to an item of B answers 403, and Kirill's is kept
  And Pavel still changes an item of A
  And Pavel is shown no grant control on B

Scenario: Switching back on restores the reach with no sign-out
  When the admin switches it on again
  Then Pavel's next change to an item of B is kept

Scenario: A sub-area receives from above only through its project node
  Given Pavel holds «Управление» on «Логистика»
  When he renames «Платформа», and then B's node switch is off and he renames «Склад»
  Then the rename of «Платформа» is kept, and the rename of «Склад» answers 403
  And the project's items are decided by the project, not by the sub-area's switch

Scenario: The switch of a sub-area decides area-scope codes only
  Given Pavel holds «Управление» on «Логистика»
  When the switch is off on «Платформа»
  Then Pavel's rename of «Платформа» answers 403, and his change to an item of A pointing at «Платформа» is kept

Scenario: Who may switch, and the parent's code (BR-SA-7)
  Given Pavel holds «Управление» on «Логистика» only; Kirill holds only the project role «Управление» on A
  When Pavel switches off A's node and then «Платформа»
  Then both are kept, and the first ends his own inherited «Управление» on A's node
  And Kirill's switch of A's node answers 403: a project grant is not «Управление» on the node
  And Pavel's switch on «Логистика» itself answers 403; only the admin switches a top-level node

Scenario: A top-level node's switch has no effect and says so
  Given D is a top-level project node with the switch off
  When the admin opens D's «Структура»
  Then the control is shown with a hint that nothing is above the node
  And when D is moved under «Логистика» the stored value applies: the department's grants do not reach D

Scenario: A move changes what reaches a project
  Given Pavel holds «Изменение» on «Логистика»
  When the admin moves D's node under «Логистика»
  Then Pavel's next change to an item of D is kept, with no sign-out
  When it is moved back to the top level
  Then it answers 403 again

Scenario: No link and no team carries access (BR-SA-5)
  Given A and D are both linked to MARS, and Pavel holds «Изменение» on «Логистика» and «Управление» on MARS
  When he changes an item of D owned by MARS
  Then it answers 403 «Недостаточно прав»

Scenario: A switch and a move are decided on the locked row (BR-SA-13, takes over m-2)
  Given Pavel holds «Управление» on «Логистика» only
  When he switches off A's node while an admin moves A's node to the top level, at the same moment
  Then exactly one order results: either the switch is kept and the move follows, or the move commits first and the switch answers 403 for lack of the parent; no stale pass and no server error

Scenario: A switch off asks first (OQ-6 A)
  When the admin turns the switch off on A's node
  Then a confirmation names how many users lose access to it, and nothing changes until it is confirmed

Scenario: A create form applies the switch last
  When Pavel creates a project node's sub-area with «Наследовать доступ» off in one form
  Then the creation is checked on the parent for everything the form sets, and the switch is applied last
```
