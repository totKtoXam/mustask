# US-2: A department's grants reach the project nodes below it, until «Наследовать доступ» is off

- story: US-2
- depends-on: US-1
- shippable: no — the reach and the switch are read only by US-3's policy
- status: pending

As a **holder of «Менеджер» on a node, or an organization admin**, I want **a department's grants to reach its project nodes and their sub-areas, and to cut that off on a node with «Наследовать доступ»**, so that **the head of a department works across its projects and a project that must stand apart can stand apart (BR-SA-5..-7, doc-012 US-1's switch clauses)**.

## Acceptance criteria

Fixture and users of US-1 (everyone an organization «Читатель» unless said). «Наследовать доступ» is on everywhere. The control is on a project node's page («Структура», shown from a project's menu) and on a sub-area's page; a department's page shows none (OQ-2). The label is «Наследовать доступ».

```gherkin
Scenario: A department grant reaches the projects below it
  Given Pavel holds «Участник» on «Логистика» only
  When he changes an item of A, an item of B, and an item of D
  Then the changes to A and B are kept, and the change to D answers 403 «Недостаточно прав»

Scenario: Switching a project node off stops what comes from above, not what is held there
  Given Pavel holds «Участник» on «Логистика», and Kirill holds «Участник» on project B
  When Anna switches «Наследовать доступ» off on B's node
  Then Pavel's next change to an item of B answers 403, and Kirill's is kept
  And Pavel still changes an item of A
  And Pavel is no longer listed as inherited in B's «Участники и доступ»
  And if Pavel held «Менеджер» on «Логистика» instead, his rename of B, its default team, links and grants (OQ-8 A) answer 403 as well, and he still does them on A

Scenario: Switching back on restores the reach with no sign-out
  When Anna switches it on again
  Then Pavel's next change to an item of B is kept

Scenario: A sub-area receives from above through its parent
  Given Pavel holds «Менеджер» on «Логистика»
  When he renames «Платформа» (under A's node), and then B's node is switched off and he renames «Склад»
  Then the rename of «Платформа» is kept, and the rename of «Склад» answers 403
  And the project's items are decided by the project, never by a sub-area's switch
  And a sub-area under «Платформа» receives from «Платформа», and stops there if «Платформа» is off

Scenario: A sub-area's switch decides area-scope codes only
  Given Pavel holds «Менеджер» on «Логистика»
  When Anna switches «Платформа» off
  Then Pavel's rename of «Платформа» answers 403, and his change to an item of A pointing at «Платформа» is kept

Scenario: Who may switch, and who turns it on again (BR-SA-7)
  Given Pavel holds «Менеджер» on «Логистика» only; Kirill holds only the project role «Менеджер» on A
  When Pavel switches off «Платформа» and then A's node
  Then both are kept, and the second ends his own inherited «Менеджер» on A's node
  And Pavel's attempt to switch A's node back on answers 403, and Anna's is kept
  And Kirill's switch of A's node answers 403: the project role does not hold the area code on «Логистика», the parent
  And Pavel's switch on «Логистика» is not offered: a department shows no control and no request changes its value

Scenario: A top-level project node keeps the control and says nothing is above (OQ-2)
  Given D is a top-level project node
  When Anna opens D's «Структура»
  Then the control is shown with a hint that nothing is above the node, and Pavel is shown it read-only
  When Anna switches D off and moves D's node under «Логистика»
  Then the stored value applies: the department's grants do not reach D

Scenario: A move changes what reaches a project, on the next request
  Given Pavel holds «Участник» on «Логистика»
  When Anna moves D's node under «Логистика»
  Then Pavel's next change to an item of D is kept, with no sign-out
  When it is moved back to the top level
  Then it answers 403 again

Scenario: No link and no team carries access (BR-SA-5)
  Given A and D are both linked to MARS, and Pavel holds «Участник» on «Логистика» and «Менеджер» on MARS
  When he changes an item of D owned by MARS
  Then it answers 403 «Недостаточно прав»

Scenario: The switch route decides on the locked row (BR-SA-13; mod-018 m-2 kept under the split codes)
  Given Pavel holds «Менеджер» on «Логистика» only
  When he switches off A's node while Anna moves A's node to the top level, at the same moment
  Then exactly one order results: the switch kept and the move after it, or the move first and the switch answering 403 for lack of the parent; no stale pass and no server error

Scenario: A switch off counts who stops being reached and warns the actor (OQ-6)
  Given Irina holds «Участник» on «Логистика», Pavel holds «Менеджер» on it, Anna and Kirill (project role on A) are unaffected
  When Pavel turns the switch off on A's node
  Then a confirmation says that 2 holders of grants stop being reached (Irina and Pavel), counting distinct users, organization admins and holders of a project grant that covers what the switch takes excluded (OQ-12)
  And because Pavel is among them it adds a line that he will lose the management of this node (copy is the design's)
  And the count leaves out holders of a project grant that covers what the switch takes from them (department «Участник» with project «Участник» loses nothing), but not a department «Менеджер» holding only project «Читатель», who loses the project settings and grants (BR-SA-19, OQ-12)
  And the confirmation also lists, apart from the count, the project grants on A given by Irina or Pavel (say Irina gave Pavel «Менеджер» on A), each with holder, role and giver
  And it only lists them: confirming the switch ends none, and they are ended by hand from A's «Участники и доступ»
  And nothing changes until it is confirmed

Scenario: A create form applies the switch last
  When Pavel creates a sub-area under A's node with «Наследовать доступ» off in one form
  Then the creation is checked on the parent for everything the form sets, and the switch is applied last
```
