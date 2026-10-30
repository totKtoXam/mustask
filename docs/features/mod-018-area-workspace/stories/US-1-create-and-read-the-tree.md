# US-1: An administrator builds the typed tree, and every member reads it

- story: US-1
- depends-on: -
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 US-1's creation and reading, reworked for the typed tree (2026-10-27)
- status: pending

As an **organization administrator**, I want **to build the organization's one typed tree: departments, the project nodes under them, and the sub-areas below a project, each with an optional description**, so that **the structure «организация → департамент → проект» is one tree that teams can be linked to (BR-AW-0, BR-AW-2, BR-AW-3)**.

The tree belongs to the organization. The parent rules are BR-AW-3's defaults: a department stands at the top (OQ-3 A, OQ-4 A: the organization is the tree's top and is not a stored node), a project node under a department or at the top, a sub-area under a project node or a sub-area of the same project (OQ-7 A). A project's node is made with its project (OQ-5 A; US-3, US-4), so the administrator creates here departments and sub-areas. The pages are «Области» in the organization's settings (`…/settings/areas`) and a node's page (`…/settings/areas/:a`) until `feat-043-area-project-team-navigation` (BR-AW-12). Until `mod-019-scoped-access` only «Администратор» creates (spec § Roles). Team links chosen on the create form are US-3's; renaming, moving and deleting are US-2's. Cast and conventions: spec § Stories. «Родительская область» offers, for a department none (it stands at the top), for a sub-area the project nodes and sub-areas by path; whether the tree draws a node expanded or collapsed at first is the design's. A creation "by Ann" under a parent the form does not offer is made by a direct request, and the server answers it as stated.

## Acceptance criteria

```gherkin
Scenario: The administrator builds the typed tree (BR-AW-0, BR-AW-3)
  Given «Ромашка» has the projects Alpha and Beta, each with its node, at the top, and Ann is its administrator
  When she creates the departments «Логистика», «Финансы» and «Инфраструктура»,
    moves Alpha and Beta under «Логистика» (US-3), then creates «Платформа» and «Отчёты» under Alpha's node and «API» under «Платформа»
  Then «Области» shows «Инфраструктура», «Логистика» and «Финансы» at the top, in that order, each marked «Департамент»,
    «Alpha» and «Beta» beneath «Логистика» marked «Проект», «Платформа» and «Отчёты» beneath «Alpha», and «API» beneath «Платформа»,
    at once or on expanding, each node once
  And each created node has no description, and its row opens its page, which names its type and its parent
  And the page of «Логистика» lists its child nodes, each opening its own page
  And every place outside the tree names the node by its path, «Логистика / Alpha / Платформа / API» (BR-AW-13)
  And each node is stored with «Наследовать доступ» on, which no page shows or changes (BR-AW-5, OQ-2 B)

Scenario: A node's type follows its parent; a type under a parent that does not allow it is refused (BR-AW-3; OQ-3 A, OQ-7 A)
  Given the tree above
  When Ann, by a direct request, creates a department under «Логистика», under Alpha's node or under «Платформа»,
    a sub-area directly under «Логистика» or at the top, or a project node alone, under «Финансы»
  Then each is refused with «Область этого типа нельзя разместить в выбранном месте» (`organizations.area.type_not_allowed`), and the tree is unchanged
  And the create form on «Логистика» offers no «Новая дочерняя область», and on Alpha's node and on «Платформа» it does
  And «Новая область» at the top of «Области» creates a department, and offers no parent

Scenario: Creating a node creates nothing else (BR-AW-2, BR-MX-2)
  Given the counts of «Ромашка»'s projects, teams, team memberships, project–team links, default-team settings and work items
  When Ann creates the department «Аналитика» with only a name
  Then «Аналитика» has no child and no team, and every count is unchanged
  And a project and a team created afterwards are linked to no department by this creation

Scenario: Only the name is required on the create form (brief § 11.5)
  When Ann opens «Новая область» on «Области»
  Then only «Название» is required; «Описание» is optional, and so are «Команды» (US-3)
  When she opens «Новая дочерняя область» from the row of «Платформа»
  Then «Родительская область» is «Alpha / Платформа» already, and she may change it to Alpha's node or another sub-area of Alpha before saving,
    the offer narrowed by typed text and holding no node of Beta, no department and no project node but Alpha's
  When she creates «Отчёты по складу» there with a description
  Then the node exists with its parent and its description after the one operation

Scenario: A name is 1–100 characters and is saved trimmed; a description holds at most 2 000 (BR-AW-4)
  When Ann creates a department with an empty name, only spaces, or 101 characters after trimming
  Then it is refused with «Название должно содержать от 1 до 100 символов»
  And the department «  Аналитика  » is saved and shown as «Аналитика»
  And a description of 2 001 characters is refused with «Описание должно содержать не более 2000 символов»,
    and one of 2 000 characters with line breaks is kept and shown as typed

Scenario: Sibling names are unique, and the top level is one name space (BR-AW-4; OQ-12 A)
  Given the tree above, and a project Gamma with its node at the top
  When Ann creates a department «логистика », or «ПЛАТФОРМА» under Alpha's node
  Then each is refused with «Область с таким названием уже есть на этом уровне», and the tree is unchanged
  And a department «Gamma» is refused the same way, as is a project created named «Финансы» at the top
  But «Платформа» can be created under Beta's node, whose children are other siblings, and a department «Alpha» at the top is created, Alpha's node standing under «Логистика»
  And «Другая», which has its own department «Логистика», is unchanged, and the name did not clash with it

Scenario: Two writes of one name at one level keep one (spec § NFR Concurrency)
  When Ann and Ivan create a department «Аналитика» at the same moment,
    or Ann creates «Аналитика» under Alpha's node while Ivan renames «Отчёты» «Аналитика»
  Then exactly one «Аналитика» exists at that level, the other request is refused with «Область с таким названием уже есть на этом уровне»,
    and neither is answered with a server error

Scenario: The tree is at most 10 levels deep at creation (BR-AW-3)
  Given Alpha's node under «Логистика» (levels 1 and 2), and a chain of sub-areas «Ф3» at level 3 down to «Ф10» at level 10, with «Ф9» at level 9
  When Ann creates a child of «Ф10»
  Then it is refused with «Дерево областей не может быть глубже 10 уровней»
  But a second child of «Ф9» is created at level 10
  And a creation and a move at the same moment that together would pass 10 levels end with one refused (US-2)

Scenario: A parent from another organization, or a missing one, names nothing (BR-AW-1, BR-AW-15)
  When Ann creates a sub-area under «Другая»'s project node, under an id that names no node, or under a node deleted while her form was open
  Then each is answered «Область не найдена.», identically, and nothing is created in «Ромашка» or «Другая»

Scenario: Every member reads the whole tree, within 2 s at the stated volume (spec § NFR)
  Given the tree above, with descriptions on «Логистика» and «Alpha»
  When Max, a «Участник», or Vera, a «Читатель», opens «Области» and the page of «Платформа»
  Then they see the nodes with their types, paths and descriptions, and the page with its parent and its team links
  And they are offered no create, rename, description, move or delete action, and «Новая область» is absent
  And Olga, who is in no organization, is answered «Организация не найдена.» on «Области» and on any node's page
  And in an organization of 1 000 nodes over 10 levels, «Области» shows the whole tree within 2 s

Scenario: An organization with no department and no project works fully (brief § 3.1.4; SC-07; BR-AW-2)
  Given «Ромашка» has no department and no project
  When Ann opens «Области»
  Then it reads «В организации пока нет департаментов и проектов» and offers her «Новая область» (a department)
  When Max opens it
  Then it reads the same with «Области создаёт администратор организации.» and no action
  And a team is created, a project is created with its node at the top, a work item is created in it, and the project's board opens with it,
    with no department asked for anywhere

Scenario: The organization's settings name «Области» once, and the project's own tree stays in its settings (BR-AW-12)
  When Max opens the organization's settings
  Then its navigation holds «Области», which opens the whole typed tree
  And Alpha's settings hold «Области» too, showing Alpha's node and below only (US-4)
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: a holder of «Управление» on a node creates child nodes under it, and the parent picker
#   offers only the nodes the user may create in, the top level only to the administrator; participants on the
#   create form; «Наследовать доступ» shown, set on the form and applied last (BR-MX-3; OQ-2 B)
# `feat-043-area-project-team-navigation`: the top-level «Области» and the node's own page, where these pages move
# `feat-042-area-board-and-overview`: the node's views and its overview's figures
```
