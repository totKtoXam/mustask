# US-2: An administrator renames, describes, moves and deletes an area

- story: US-2
- depends-on: US-1
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 US-1's changes and deletion
- status: pending

As an **organization administrator**, I want **to rename and describe an area, move it with everything below it, and delete a leaf**, so that **the tree follows the organization as it changes, without touching a project, a team or a work item (BR-AW-2, BR-AW-3, BR-AW-9)**.

The commands are on the area's row in «Области» and on its page (BR-AW-12). Until `mod-019-scoped-access` only «Администратор» runs them (spec § Roles). Links are US-3's; deleting an area ends them here. Cast and conventions: spec § Stories; the example tree of US-1 is the Given of every scenario.

## Acceptance criteria

```gherkin
Scenario: A rename and a description show everywhere at once (BR-AW-4, BR-AW-13)
  Given «Логистика» is linked to Alpha and MARS
  When Ann renames «Логистика» «Логистика и доставка», then sets its description and clears it again
  Then the tree shows «Логистика и доставка» with «Складские системы» and «Транспортные системы» beneath it
  And Alpha's «Общие» and MARS's page name it «Логистика и доставка», and the path of its children follows
  And its description is empty again, and its children, links and parent are unchanged
  And renaming «Финансы» «ЛОГИСТИКА И ДОСТАВКА» is refused with «Область с таким названием уже есть на этом уровне»,
    while renaming «Финансы» «финансы» succeeds: its only match is itself

Scenario: An area moves with its whole subtree (BR-AW-3, BR-AW-2)
  Given «Логистика» is linked to Alpha and MARS, and has a description
  When Ann moves «Логистика» under «Финансы»
  Then the tree shows «Финансы / Логистика / Складские системы» and «Финансы / Логистика / Транспортные системы», and no top-level «Логистика»
  And its links and description, and its children's, are unchanged, and no project, team, link or work item changed
  When she moves «Финансы / Логистика» to the top level
  Then the tree is as it was in the Given
  And a move of an area to the parent it already has, the top level included, by a direct request, succeeds and changes nothing

Scenario: The move dialog offers what a move may reach (BR-AW-3)
  When Ann opens «Переместить» on «Логистика»
  Then it names what moves, «Логистика» with its two child areas, and offers the top level and the other areas by path
  And it offers neither «Логистика» nor «Логистика / Складские системы» nor «Логистика / Транспортные системы»
  And typing part of a path narrows the offer, in an organization of 1 000 areas too

Scenario: A move needs a free name at its destination (BR-AW-4)
  Given «Инфраструктура» has a child «складские СИСТЕМЫ»
  When Ann moves «Логистика / Складские системы» under «Инфраструктура»
  Then it is refused with «Область с таким названием уже есть на этом уровне», and the tree is unchanged
  And so is a move to the top level of a child named like a top-level area

Scenario: An area never moves under itself or a descendant, even by a direct request (BR-AW-3; SC-08)
  When Ann asks, by a direct request, to move «Логистика» under «Логистика» or under «Логистика / Складские системы»
  Then each is refused with «Нельзя переместить область внутрь неё самой или её вложенной области», and the tree is unchanged

Scenario: Two moves that together would form a cycle: one is refused (BR-AW-3; spec § NFR Concurrency)
  When Ann moves «Финансы» under «Инфраструктура» and Ivan moves «Инфраструктура» under «Финансы» at the same moment
  Then exactly one move is applied, the other is refused with «Нельзя переместить область внутрь неё самой или её вложенной области»,
    and following parents from any area ends at a top-level area
  And neither request is answered with a server error

Scenario: The tree is at most 10 levels deep after a move (BR-AW-3)
  Given a chain from the top-level «Финансы» down to «Ф10» at level 10, with «Ф9» at level 9 and «Ф8» at level 8
  When Ann moves «Логистика», with its children, under «Ф9»
  Then it is refused with «Дерево областей не может быть глубже 10 уровней»
  But «Логистика» moved under «Ф8» is applied: its children land on level 10
  And, from the Given again, when «Логистика» is moved under «Ф8» while Ivan creates a child of «Логистика / Складские системы», each allowed alone,
    exactly one of them is applied, the other refused with «Дерево областей не может быть глубже 10 уровней», and no area stands on level 11

Scenario: Deleting a leaf ends its links and nothing else (BR-AW-9, BR-AW-2, BR-AW-10)
  Given «Логистика / Складские системы» is linked to Alpha, to Gamma and to MARS, Gamma is deactivated,
    and Alpha and MARS are also linked to «Финансы»
  When Ann deletes «Логистика»
  Then it is refused with «Нельзя удалить область, в которой есть вложенные области», and nothing changes
  When she asks to delete «Складские системы»
  Then the confirmation names its team MARS and its projects Alpha and Gamma, Gamma marked «деактивирован», and says the deletion cannot be undone
  And cancelling it changes nothing
  When she confirms
  Then «Складские системы» is gone from the tree, its page answers «Область не найдена.», and its three links are gone
  And Alpha, Gamma, MARS, their links to «Финансы» and to each other, Alpha's work items, «Логистика» and «Транспортные системы» are as before
  And when Gamma is restored it is linked to no area: its link ended with the area (BR-AW-10)
  And no action restores the area: «Складские системы» created again under «Логистика» has no link

Scenario: A deletion and a write that needs the area at the same moment (BR-AW-9; spec § NFR Concurrency)
  When Ann deletes the leaf «Транспортные системы» while Ivan creates a child under it, or moves «Финансы» under it
  Then one of the two is refused, and no area is left under a deleted parent
  And a rename, a description, a move or a link of the area that comes after its deletion is answered «Область не найдена.»
  And a link of the area that committed first ends with it, and none of these requests is answered with a server error
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: a holder of «Управление» on an area renames, describes and deletes it, and moves it under an area
#   they also manage; a move away from a parent also needs «Управление» on the current parent, the top level the administrator;
#   the area's grants end with it and the confirmation names them (BR-MX-3, BR-MX-8)
# `feat-041-project-visibility`: the confirmation names no project the actor may not see, nor counts it, and that link still ends
```
