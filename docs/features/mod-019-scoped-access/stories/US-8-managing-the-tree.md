# US-8: Department and project managers manage the tree below them

- story: US-8
- depends-on: US-1, US-2, US-3
- shippable: no — the codes it uses come with US-3's policy
- status: pending

As a **department manager or a project manager**, I want **to create projects in my department and to manage the sub-areas of my projects**, so that **the people who run the work shape its structure without the organization admin (BR-SA-16, BR-SA-17, doc-012 US-1's owed clauses)**.

## Acceptance criteria

Fixture and users of US-1 (everyone a «Читатель» unless said). Pavel holds «Управление» on «Логистика»; Kirill holds the project role «Управление» on A; Olga holds «Изменение» on A. Departments do not nest and hold only project nodes; sub-areas stand only below a project node (mod-018 OQ-3, OQ-7); a project node is created, renamed and deleted with its project, and moved by the area move.

```gherkin
Scenario: A department manager creates a project in the department (OQ-4 B)
  When Pavel creates the project E in «Логистика»
  Then it is created with its node under «Логистика», and it answers as the creation of any project does
  When he creates a project at the top level, or in «Финансы», or a department
  Then each answers 403 «Недостаточно прав» and stores nothing
  And Anna creates in any place, a top-level one included

Scenario: A department manager runs the projects it reaches (OQ-8 A)
  Given Pavel holds «Управление» on «Логистика» and has created the project E in it
  When he renames E, sets its default team, links a team he also manages to E, and gives Irina «Изменение» on E
  Then each is kept, and the same on A and B
  And the same on D (top level) answers 403
  When Anna switches A's node off
  Then his rename of A, its default team, its links and its grants each answer 403 at his next request, and E is unaffected
  And he cannot set A's visibility (`organizations.visibility.write`, feat-041) or deactivate it: those stay the project role's and the admin's

Scenario: The place decides, and the order of the codes is fixed (mod-018 TC-2 kept)
  When Olga, who manages nothing, creates a project in «Логистика»
  Then it answers 403, before a name taken or a malformed body is looked at
  And a non-admin naming a place that does not exist, was deleted, belongs to another organization or is not a department gets the same 403, so nothing is disclosed
  And Anna naming an unknown place gets «Область не найдена.»
  And the route is on the inventory's exception list: the place is in the body and is decided at step 0, before any lookup

Scenario: A department manager manages the sub-areas below its project nodes
  Given the switches are on
  When Pavel creates the sub-area «Тесты» under A's node, renames it, describes it, moves it under «Платформа», and deletes it
  Then each is kept
  When he creates a department, renames «Финансы», or renames A's node through the area route
  Then each answers 403 (the node is renamed with its project, `root_is_fixed`)

Scenario: A project manager manages the sub-areas of its project (OQ-3 B)
  When Kirill creates «Тесты» under A's node, renames «Платформа», moves «Тесты» under «Платформа» and deletes it
  Then each is kept
  And the same under B's node or on «Склад» answers 403
  And Olga, holding «Изменение», gets 403 on each

Scenario: A project manager's reach does not depend on the sub-areas' switches
  Given Anna switches «Платформа» off, cutting the department's grants from it
  When Pavel renames «Платформа», and Kirill renames it
  Then Pavel's answers 403 and Kirill's is kept

Scenario: A project manager cannot place or switch its own project
  When Kirill moves A's node under «Финансы», switches A's node off, or renames the node through the area route
  Then each answers 403, and «Общие» shows him no placement control
  And his rename of the project itself is kept

Scenario: A department manager moves a project between departments they both manage (BR-MX-3)
  Given Pavel also holds «Управление» on «Финансы»
  When he moves A's node from «Логистика» to «Финансы»
  Then it is kept, with his reach changing at his next request
  When he holds «Логистика» only and moves A's node to «Финансы» or to the top level
  Then it answers 403 and nothing changes
  And a move needs the area code on the node, on its current parent and on its new parent; a top-level destination is the admin's

Scenario: A name taken at the destination is refused after the codes
  Given Pavel manages «Логистика» and «Финансы», and «Финансы» holds a project node named like A
  When he moves A's node to «Финансы»
  Then it is refused with «Область с таким названием уже есть на этом уровне»

Scenario: Nobody else writes to the tree
  When a «Читатель», a «Участник», and holders of «Изменение» on «Логистика» or on A, or of «Планирование» on MARS, create, rename, move, switch or delete a node
  Then each is answered 403, on the page and by a direct request, and «Структура» shows them the tree with no such command
```
