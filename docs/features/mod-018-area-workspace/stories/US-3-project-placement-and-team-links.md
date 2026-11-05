# US-3: A project has one place in the tree; teams are linked to departments

- story: US-3
- depends-on: US-1
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); reworked 2026-10-27 (typed tree) and 2026-10-28 (OQ-11 A, OQ-8 A). The file name is current; the title is the story's
- status: pending

As an **administrator** (until `mod-019-scoped-access`, «Администратор»), I want **to place a project under one department and move it, and to link teams to departments from either end**, so that **the structure «организация → департамент → проект» is one tree, a team works in several departments with no copy of it, and no link or move changes the work (BR-AW-0, BR-AW-6, BR-AW-8)**.

The ends of a team link are a department's page («Команды») and a team's page («Области») (BR-AW-12); outside the tree a node is shown by its path (BR-AW-13). A project's place is set on «Новый проект» and on its «Общие» («Департамент»; «Организация» for no department) and by the move of its node (US-2). **A project node's «Команды» are feat-028's project–team links** (OQ-11 A): they are linked, unlinked and read by feat-028's rules and codes, and this line adds no second link to a project node, a sub-area or the organization (OQ-8 A). Cast and conventions: spec § Stories; Gamma is deactivated where a scenario says so, SATURN is deleted.

## Acceptance criteria

```gherkin
Scenario: A project is placed under one parent, a department or the top (BR-AW-0, BR-AW-3; OQ-5 A, OQ-12 A)
  When Ann creates project Delta (DEL) choosing the department «Логистика»
  Then Delta's node stands under «Логистика», and its path is «Логистика / Delta»
  When she creates project Epsilon (EPS) choosing no department
  Then Epsilon's node stands at the top, beside the departments
  And a project has exactly one node and the node one parent: no request places it under two departments
  And a project named like a department or a project node at its level, «Финансы» for one, is refused with «Область с таким названием уже есть на этом уровне», nothing is created, and the project's key stays free

Scenario: A project moves between departments and keeps everything (BR-AW-3, BR-AW-8)
  Given Alpha is under «Логистика», has the sub-area «Платформа», items in it, and the team MARS linked to it (feat-028)
  When Ann moves Alpha from «Логистика» to «Финансы», from Alpha's «Общие» or the tree
  Then Alpha's path is «Финансы / Alpha», and «Платформа» and every item's «Область» are unchanged
  And no work item, owning team, backlog place or project–team link changed, and MARS is still linked to Alpha
  And a move of Alpha under a project node, under a sub-area, or under a department that would put its subtree past level 10 is refused
    with `type_not_allowed`'s or `depth_exceeded`'s text, and nothing changes

Scenario Outline: A team link is made from the page of either end (BR-AW-6, BR-AW-12; OQ-8 A)
  Given <department> and the team <team> are not linked
  When Ann opens <page>, presses its add command, types part of the other end's name into the searchable multiselect, picks it and confirms
  Then they are linked, the link records Ann and the time, and both pages list the other end with a way to open it
  Examples:
    | department  | team  | page                          |
    | «Логистика» | MARS  | the department's «Команды»    |
    | «Логистика» | MARS  | MARS's page, its «Области»    |
    | «Финансы»   | VENUS | the department's «Команды»    |
    | «Финансы»   | VENUS | VENUS's page, its «Области»   |

Scenario: Several teams or departments are linked in one confirmation, all or none (BR-AW-6)
  When Ann picks MARS and VENUS together in the «Команды» of «Финансы» and confirms
  Then «Финансы» is linked to both
  And when VENUS is deleted after she picked it and before she confirms, «Финансы» is linked to neither, and the answer is «Команда не найдена.»
  And picking «Финансы» and «Инфраструктура» together on MARS's page links MARS to both

Scenario: The pickers offer only what may be linked (BR-AW-1, BR-AW-6; OQ-8 A)
  Given «Логистика» is linked to MARS, and SATURN is deleted
  When Ann searches the team multiselect of «Логистика»
  Then it offers VENUS and JUPITER, and never MARS (linked), SATURN (deleted) or Orion («Другая»)
  And the node multiselect on MARS's page offers the departments of «Ромашка» not yet linked by their names,
    and neither a project node, a sub-area, nor any node of «Другая»
  And in an organization of 1 000 nodes, typing part of a name narrows the node multiselect's offer

Scenario: A project node's teams are the project–team links, not a second link (OQ-11 A, BR-AW-6)
  Given MARS is linked to Alpha by feat-028's project–team link
  When Ann opens Alpha's node page
  Then its «Команды» lists MARS, the same link that Alpha's own «Команды» lists, with feat-028's way to add and to remove it
  And unlinking MARS from either place removes that one link, by feat-028's rules (its items are reassigned as feat-028 states), and both lists drop MARS
  And a request that links a team with a project node as an area–team link, or with a sub-area, is refused, and nothing changes
    (the code and text are the design's), while the same request on a department succeeds

Scenario: Each end lists its own links, and no node lists a project as a link (BR-AW-2, BR-AW-6)
  Given «Логистика» is linked to MARS, and «Финансы» to MARS and VENUS
  Then the page of «Логистика» lists MARS under «Команды», not VENUS, and its project nodes as children in the tree, not as links
  And MARS's page lists «Логистика» and «Финансы» under «Области», and VENUS's page «Финансы»
  And a department with no team link reads «К области не привязаны команды», and a team with none «Команда не входит ни в одну область»,
    each offering the add command only to the administrator

Scenario: A link changes nothing else and grants nothing (BR-AW-8)
  Given Max and Vera read and change Alpha's items as their roles allow, and MARS owns some of them
  When Ann links MARS with «Логистика» and with «Финансы»
  Then no work item, owning team, backlog place, project–team link or other link changed
  And Max and Vera read and change exactly what they did before

Scenario: Unlinking removes only that link (BR-AW-6, BR-AW-7)
  Given MARS is linked to «Логистика», «Финансы» and Alpha (feat-028)
  When Ann unlinks MARS from «Логистика», from the department's page or from MARS's page
  Then MARS's links to «Финансы» and to Alpha stay, and MARS, «Логистика» and the items MARS owns are unchanged

Scenario: Linking a linked pair or unlinking an unlinked one changes nothing (BR-AW-6)
  Given Ivan linked «Логистика» with MARS at time T
  When Ann links them again from either page, and unlinks «Финансы» from JUPITER, which are not linked
  Then each is answered as a success, «Логистика» and MARS are linked once and the link still records Ivan and T
  And two requests that link the same unlinked pair at once leave exactly one link, and neither is answered with a server error

Scenario: Links chosen at creation are made in the same operation (BR-AW-6)
  When Ann creates the department «Аналитика» with the teams MARS and VENUS, and the team NEPTUNE with the departments «Финансы» and «Логистика»
  Then «Аналитика» is linked to exactly MARS and VENUS and NEPTUNE to those two departments, each link recording Ann and the time
  And a creation naming a deleted or foreign team, or a deleted, foreign or non-department node, is refused
    with «Команда не найдена.» or «Область не найдена.» (a node that is not a department: the design's text), nothing is created, and the name stays free

Scenario: A deleted team's links end; a deactivated project changes no link (BR-AW-10; feat-028 BR-OL-9)
  Given MARS is linked to «Логистика», «Финансы» and Alpha
  When Ann deletes MARS
  Then neither department lists MARS, no node is deleted, and the deletion is otherwise as feat-028 US-5 states
  And a link of MARS with a department that runs at once with its deletion leaves no link to the deleted team, and neither request is answered with a server error
  And when Gamma is deactivated and restored, its node and its project–team links are as before (OQ-6 A)

Scenario: A link gives no power over its other end (BR-AW-7)
  Given «Логистика» is linked to MARS
  When Ann opens MARS's page
  Then «Логистика» is listed with a way to open it and the unlink command, and nothing there renames, moves or deletes it
  And nothing on the department's page renames, staffs or deletes MARS
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: «Управление» on one end makes no link; a team link ends on either end's «Управление»;
#   grants on a department reach the projects below it through the tree (doc-012 US-3 reworded)
# `feat-041-project-visibility`: a project the user may not see is absent from the tree's project nodes and the pickers
# `feat-043-area-project-team-navigation`: the links on the objects' own pages (the department's «Команды», the team's «Обзор»)
# `feat-042-area-board-and-overview`: a department's overview counts its child projects and linked teams, a project node's its items
```
