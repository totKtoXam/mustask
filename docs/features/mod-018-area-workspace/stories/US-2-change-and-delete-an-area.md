# US-2: An administrator renames, describes, moves and deletes a node

- story: US-2
- depends-on: US-1
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 US-1's changes and deletion, reworked for the typed tree (2026-10-27)
- status: pending

As an **organization administrator**, I want **to rename and describe a node, move it with everything below it (a project with its sub-areas, a sub-area within its project), and delete a leaf**, so that **the tree follows the organization as it changes, without touching a project's items, a team or a work item (BR-AW-2, BR-AW-3, BR-AW-9)**.

The commands are on the node's row in «Области» and on its page (BR-AW-12). Until `mod-019-scoped-access` only «Администратор» runs them (spec § Roles). Team links are US-3's; deleting a node ends its team links here. Defaults: OQ-3 A, OQ-4 A, OQ-5 A, OQ-6 A, OQ-7 A, OQ-8 A, OQ-10 A, OQ-11 A, OQ-12 A. Cast: the tree of US-1 is the Given of every scenario unless a scenario adds to it: departments «Логистика» (linked to MARS), «Финансы» and «Инфраструктура» (linked to MARS and VENUS); Alpha's and Beta's nodes under «Логистика»; «Платформа» (with «API») and «Отчёты» under Alpha's node; Gamma, deactivated, at the top. A move "by Ann" is made from «Переместить» where the dialog offers its destination, and by a direct request where it does not; the server answers it as stated either way.

## Acceptance criteria

```gherkin
Scenario: A rename and a description show everywhere at once (BR-AW-4, BR-AW-13)
  When Ann renames «Логистика» «Логистика и доставка», then sets its description and clears it again
  Then the tree shows «Логистика и доставка» with Alpha's and Beta's nodes beneath it, and the paths of everything below follow
  And MARS's page names it «Логистика и доставка», its description is empty again, and its children, links and parent are unchanged
  And renaming «Финансы» «ЛОГИСТИКА И ДОСТАВКА» is refused with «Область с таким названием уже есть на этом уровне»,
    while renaming «Финансы» «финансы» succeeds: its only match is itself
  And renaming the sub-area «Платформа» «Платформа 2» renames that sub-area alone

Scenario: A project's node is renamed with its project, not on its own (OQ-10 A, OQ-5 A, OQ-12 A)
  When Ann renames Alpha «Alpha 2» in the project's settings
  Then its node is renamed too, every path reads «Логистика / Alpha 2 / Платформа», and the node's description is unchanged
  And a rename of Alpha's node on its own, by a direct request, is refused with «Корневая область меняется только вместе с проектом» (`organizations.area.root_is_fixed`),
    while a description of the node is saved
  And renaming Beta «alpha 2» is refused with «Область с таким названием уже есть на этом уровне», the sibling node's name being taken, and Beta keeps its name

Scenario: A project moves between departments with its whole subtree (BR-AW-3, BR-AW-2)
  Given Alpha holds items in «Платформа / API» and «Отчёты»
  When Ann moves Alpha's node from «Логистика» to «Финансы»
  Then the tree shows «Финансы / Alpha / Платформа / API» and «Финансы / Alpha / Отчёты», and no «Alpha» stands under «Логистика»
  And the descriptions of the node and its sub-areas are unchanged, every item's «Область» is the same area, and no project, team, link or work item changed
  When she moves it to the top
  Then its path is «Alpha», and moving it back to «Логистика» restores the Given
  And a move of a node to the parent it already has, by a direct request, succeeds and changes nothing

Scenario: No node moves to a place its type does not allow (BR-AW-3; OQ-3 A, OQ-7 A)
  When Ann, by a direct request, moves «Логистика» under «Финансы», under Alpha's node or under «Платформа»,
    Alpha's node under Beta's node or under «Платформа», or «Платформа» to the top or under «Логистика»
  Then each is refused with «Область этого типа нельзя разместить в выбранном месте» (`organizations.area.type_not_allowed`), and the tree is unchanged

Scenario: A sub-area never leaves its project (BR-AW-3, BR-AW-11)
  When Ann, by a direct request, moves «Платформа» under Beta's node, or «API» under a sub-area of Beta
  Then each is refused with «Вложенную область нельзя переместить за пределы её проекта» (`organizations.area.outside_project`), and no item's area changed
  But «Платформа» moved under «Отчёты», and «API» moved to Alpha's node directly, are applied, with their children and the items' areas following

Scenario: The move dialog is per type (BR-AW-3)
  When Ann opens «Переместить» on Alpha's node
  Then it names what moves, the node with its two sub-areas and «API», and offers the top and the departments «Финансы» and «Инфраструктура» by path
  And on a department «Переместить» is absent, and on «Платформа» it offers Alpha's node and «Alpha / Отчёты» by path, and never
    «Платформа» itself, «Платформа / API», a node of Beta or a department
  And typing part of a path narrows the offer, in an organization of 1 000 nodes too
  And whether it also offers the current place, or a destination that the depth or the name rule refuses, is the design's:
    a destination it offers and the server refuses is refused on saving with that rule's text, and the tree is unchanged

Scenario: A move needs a free name at its destination (BR-AW-4; OQ-12 A)
  Given a department «Alpha» exists at the top, and «Отчёты» has a child «api»
  When Ann moves Alpha's node to the top, and then «Платформа / API» under «Отчёты»
  Then each is refused with «Область с таким названием уже есть на этом уровне», and the tree is unchanged

Scenario: A node never moves under itself or a descendant, even by a direct request (BR-AW-3; SC-08)
  When Ann asks, by a direct request, to move «Платформа» under «Платформа» or under «Платформа / API»
  Then each is refused with «Нельзя переместить область внутрь неё самой или её вложенной области», and the tree is unchanged

Scenario: Two moves that together would form a cycle: one is refused (BR-AW-3; spec § NFR Concurrency)
  When Ann moves «Платформа» under «Отчёты» and Ivan moves «Отчёты» under «Платформа» at the same moment
  Then exactly one move is applied, the other is refused with «Нельзя переместить область внутрь неё самой или её вложенной области»,
    and following parents from any node ends at a top-level node
  And neither request is answered with a server error

Scenario: The tree is at most 10 levels deep after a move (BR-AW-3)
  Given project Delta's node stands at the top with a chain of sub-areas «Д2» at level 2 down to «Д9» at level 9
  When Ann moves Delta's node under «Логистика»
  Then it is applied: «Д9» lands on level 10
  But, with a chain down to «Д10» at level 10 instead, the move is refused with «Дерево областей не может быть глубже 10 уровней», and nothing changes
  And, from the first Given, when Delta's node is moved under «Логистика» while Ivan creates a child of «Д9», each allowed alone,
    exactly one of them is applied, the other refused with «Дерево областей не может быть глубже 10 уровней», and no node stands on level 11

Scenario: Deleting a leaf department ends its team links and nothing else (BR-AW-9, BR-AW-2, BR-AW-10)
  When Ann deletes «Логистика»
  Then it is refused with «Нельзя удалить область, в которой есть вложенные области или рабочие элементы» (`organizations.area.not_empty`), and nothing changes
  When she asks to delete «Инфраструктура»
  Then the confirmation names its teams MARS and VENUS and says the deletion cannot be undone
  And cancelling it changes nothing
  When she confirms
  Then «Инфраструктура» is gone from the tree, its page answers «Область не найдена.», and its two links are gone
  And MARS, VENUS, their links to «Финансы» and to «Логистика», every project and every item are as before
  And no action restores it: «Инфраструктура» created again has no link

Scenario: A sub-area with items or children is not deleted; a project's node is not deleted alone (BR-AW-9; mod-002 BR-M1-13; OQ-5 A)
  Given «Платформа / API» is a leaf with items, and «Отчёты» is a leaf with none
  When Ann deletes «Платформа / API», or «Платформа»
  Then each is refused with `organizations.area.not_empty`'s text, and nothing changes
  When she deletes «Отчёты» after its confirmation
  Then it is gone, and no item changed
  And a deletion of Alpha's node on its own, by a direct request, is refused with `organizations.area.root_is_fixed`'s text

Scenario: A deletion and a write that needs the node at the same moment (BR-AW-9; spec § NFR Concurrency)
  When Ann deletes the leaf department «Инфраструктура» while Ivan moves Alpha's node under it
  Then one of the two is refused, and no node is left under a deleted parent
  And a rename, a description, a move or a link of «Инфраструктура» that comes after its deletion is answered «Область не найдена.»
  And a link of it that committed first ends with it, and none of these requests is answered with a server error

Scenario: A deactivated project's node is read-only (BR-AW-10; OQ-6 A)
  Given Gamma's node stands under «Инфраструктура», and Gamma is deactivated
  When Ann renames or describes Gamma's node or one of its sub-areas, moves it, creates a child under it or deletes it
  Then each is answered «Проект не найден.», and nothing changes
  And «Инфраструктура» is not deleted while Gamma's node stands in it, and the node's name stays taken among its siblings
  When Ann restores Gamma
  Then its node and sub-areas are as stored, and the writes above are allowed again
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: a holder of «Управление» on a node renames, describes and deletes it, and moves it under a node
#   they also manage; a move away from a parent also needs «Управление» on the current parent, the top level the administrator;
#   the node's grants end with it and the confirmation names them (BR-MX-3, BR-MX-8)
# `feat-041-project-visibility`: a project node the actor may not see is absent from the tree and the move dialog
```
