# US-3: A project has one place in the tree; teams are linked to departments and projects

- story: US-3
- depends-on: US-1
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); reworked 2026-10-27 (typed tree). The file's slug is stale from the earlier area–project links
- status: pending

As an **administrator** (until `mod-019-scoped-access`, «Администратор»), I want **to place a project under one department and move it, and to link teams to departments and projects from either end**, so that **the structure «организация → департамент → проект» is one tree, a team works in several nodes with no copy of it, and no link or move changes the work (BR-AW-0, BR-AW-6, BR-AW-8)**.

The ends of a team link are a node's page («Команды») and a team's page («Области») (BR-AW-12); outside the tree a node is shown by its path (BR-AW-13). A project's place is set on «Новый проект» and on its «Общие» («Департамент»; «Организация» for no department) and by the move of its node (US-2). Cast and conventions: spec § Stories; the tree is the typed one, Gamma is deactivated where a scenario says so, SATURN is deleted. Parent rules are the recommended ones of BR-AW-3 (OQ-3, OQ-4, OQ-7, OQ-8).

## Acceptance criteria

```gherkin
Scenario: A project is placed under one parent, a department or the organization (BR-AW-0, BR-AW-3)
  When Ann creates project Delta (DEL) choosing the department «Логистика»
  Then Delta's node stands under «Логистика», and its path is «Логистика / Delta»
  When she creates project Epsilon (EPS) choosing no department
  Then Epsilon's node stands directly under the organization
  And a project has exactly one node and the node one parent: no request places it under two departments

Scenario: A project moves between departments and keeps everything (BR-AW-3, BR-AW-8)
  Given Alpha is under «Логистика», has the sub-area «Платформа», items in it, and the team MARS linked to its node
  When Ann moves Alpha from «Логистика» to «Финансы», from Alpha's «Общие» or the tree
  Then Alpha's path is «Финансы / Alpha», and «Платформа» and every item's «Область» are unchanged
  And no work item, owning team, backlog place, project–team link or team link changed
  And a move of Alpha under a project node, under a sub-area or under a department that would put it past level 10 is refused
    (with the type or depth rule's text), and nothing changes

Scenario Outline: A team link is made from the page of either end (BR-AW-6, BR-AW-12)
  Given <node> and the team <team> are not linked
  When Ann opens <page>, presses its add command, types part of the other end's name (a node: part of its path) into the searchable multiselect, picks it and confirms
  Then they are linked, the link records Ann and the time, and both pages list the other end with a way to open it
  Examples:
    | node               | team   | page                           |
    | «Логистика»        | MARS   | the node's «Команды»           |
    | «Логистика»        | MARS   | MARS's page, its «Области»     |
    | «Логистика / Alpha»| VENUS  | the project node's «Команды»   |
    | «Логистика / Alpha»| VENUS  | VENUS's page, its «Области»    |

Scenario: Several ends are linked in one confirmation, all or none (BR-AW-6)
  When Ann picks MARS and VENUS together in the «Команды» of «Финансы» and confirms
  Then «Финансы» is linked to both
  And when VENUS is deleted after she picked it and before she confirms, «Финансы» is linked to neither, and the answer is «Команда не найдена.»
  And picking «Финансы» and «Логистика / Alpha» together on MARS's page links MARS to both

Scenario: The pickers offer only what may be linked (BR-AW-1, BR-AW-6)
  Given «Логистика» is linked to MARS, and SATURN is deleted
  When Ann searches the team multiselect of «Логистика»
  Then it offers VENUS and JUPITER, and never MARS (linked), SATURN (deleted) or Orion («Другая»)
  And the node multiselect on MARS's page offers the departments and project nodes of «Ромашка» not yet linked, each by its path,
    and neither a sub-area, the organization (OQ-4 A), nor any node of «Другая»
  And in an organization of 1 000 nodes, typing part of a path narrows the node multiselect's offer

Scenario: Each end lists its own links, and no node lists a project as a link (BR-AW-2, BR-AW-6)
  Given «Логистика» is linked to MARS, «Логистика / Alpha» to VENUS
  Then the page of «Логистика» lists MARS under «Команды», not VENUS, and its child project nodes as children in the tree, not as links
  And VENUS's page lists «Логистика / Alpha» under «Области»
  And a node with no team link reads «К области не привязаны команды», and a team with none «Команда не входит ни в одну область»,
    each offering the add command only to the administrator

Scenario: A link changes nothing else and grants nothing (BR-AW-8)
  Given Max and Vera read and change Alpha's items as their roles allow, and MARS owns some of them
  When Ann links MARS with «Логистика / Alpha» and with «Финансы»
  Then no work item, owning team, backlog place, project–team link or other link changed
  And Max and Vera read and change exactly what they did before

Scenario: Unlinking removes only that link (BR-AW-6, BR-AW-7)
  Given MARS is linked to «Логистика», «Финансы» and Alpha's node
  When Ann unlinks MARS from «Логистика», from the node's page or from MARS's page
  Then MARS's links to «Финансы» and Alpha's node stay, and MARS, «Логистика» and the items MARS owns are unchanged

Scenario: Linking a linked pair or unlinking an unlinked one changes nothing (BR-AW-6)
  Given Ivan linked «Логистика» with MARS at time T
  When Ann links them again from either page, and unlinks «Финансы» from VENUS, which are not linked
  Then each is answered as a success, «Логистика» and MARS are linked once and the link still records Ivan and T
  And two requests that link the same unlinked pair at once leave exactly one link, and neither is answered with a server error

Scenario: Links chosen at creation are made in the same operation (BR-AW-6)
  When Ann creates the department «Аналитика» with the teams MARS and VENUS, and team NEPTUNE with the nodes «Финансы» and «Логистика / Alpha»
  Then «Аналитика» is linked to exactly MARS and VENUS and NEPTUNE to those two nodes, each link recording Ann and the time
  And a creation naming a deleted or foreign team, or a deleted or foreign node, is refused
    with «Команда не найдена.» or «Область не найдена.», nothing is created, and the name stays free

Scenario: A deleted team's links end; a deactivated project's node keeps its links (BR-AW-10; feat-028 BR-OL-9)
  Given MARS is linked to Alpha's node, to «Логистика» and to «Финансы»
  When Ann deletes MARS
  Then no node lists MARS, no node is deleted, and the deletion is otherwise as feat-028 US-5 states
  And a link of MARS with a node that runs at once with its deletion leaves no link to the deleted team, and neither request is answered with a server error
  And when Gamma is deactivated and restored, its node's team links are as before (OQ-6 A)

Scenario: A link gives no power over its other end (BR-AW-7)
  Given «Логистика» is linked to MARS
  When Ann opens MARS's page
  Then «Логистика» is listed with a way to open it and the unlink command, and nothing there renames, moves or deletes it
  And nothing on the node's page renames, staffs or deletes MARS
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: «Управление» on one end makes no link; a team link ends on either end's «Управление»;
#   grants on a department reach the projects below it through the tree (doc-012 US-3 reworded)
# `feat-041-project-visibility`: a project the user may not see is absent from the tree's project nodes and the pickers
# `feat-043-area-project-team-navigation`: the links on the objects' own pages (the node's «Команды», the team's «Обзор»)
# `feat-042-area-board-and-overview`: a department's overview counts its child projects and linked teams, a project node's its items
```
