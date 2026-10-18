# US-1: An administrator creates the organization's areas, and every member reads the tree

- story: US-1
- depends-on: -
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 US-1's creation and reading
- status: pending

As an **organization administrator**, I want **to create the organization's areas as one tree, at the top level or under a parent, each with an optional description**, so that **the organization's departments, programs and working groups get workspaces that projects and teams can be linked to, with nothing stored inside them (BR-AW-2, BR-AW-3)**.

The tree belongs to the organization, not to a project. Its pages are «Области» in the organization's settings (`…/settings/areas`) and an area's page (`…/settings/areas/:a`) until `feat-043-area-project-team-navigation` (BR-AW-12). Until `mod-019-scoped-access` only «Администратор» creates (spec § Roles). Links chosen on the create form are US-3's; renaming, moving and deleting are US-2's. Cast and conventions: spec § Stories.

## Acceptance criteria

```gherkin
Scenario: The administrator builds the example tree (brief § 3.2; BR-AW-3)
  Given «Ромашка» has no areas, and Ann is its administrator
  When she creates «Логистика», «Финансы» and «Инфраструктура» with no parent,
    then «Складские системы» and «Транспортные системы» with the parent «Логистика»
  Then «Области» shows «Инфраструктура», «Логистика» and «Финансы» at the top level, in that order,
    and «Складские системы» and «Транспортные системы» beneath «Логистика», each area once
  And each area has no description, and its row opens its page
  And the page of «Складские системы» names its parent «Логистика», opening that page, and the page of «Логистика» lists its two child areas, each opening its own page
  And every place outside the tree names the area by its path, «Логистика / Складские системы» (BR-AW-13)
  And each area is stored with «Наследовать доступ» on, which no page shows or changes (BR-AW-5, OQ-2 B)

Scenario: Creating an area creates nothing else (BR-AW-2, BR-MX-2)
  Given the counts of «Ромашка»'s projects, teams, team memberships, project–team links, default-team settings and work items
  When Ann creates «Аналитика» with only a name
  Then «Аналитика» has no parent, child, project or team, and every count is unchanged
  And a project and a team created afterwards are linked to no area

Scenario: Only the name is required on the create form (brief § 11.5)
  When Ann opens «Новая область» on «Области»
  Then only «Название» is required; «Родительская область» and «Описание» are optional, and so are «Проекты» and «Команды» (US-3)
  And «Родительская область» offers the top level and every area of the tree by its path, narrowed by typed text
  When she opens it from the row of «Логистика» as «Новая дочерняя область»
  Then «Родительская область» is «Логистика» already, and she may change it before saving
  When she creates «Транспортные системы» under «Логистика» with a description
  Then the area exists with its parent and its description after the one operation

Scenario: A name is 1–100 characters and is saved trimmed; a description holds at most 2 000 (BR-AW-4)
  When Ann creates an area with an empty name, only spaces, or 101 characters after trimming
  Then it is refused with «Название должно содержать от 1 до 100 символов»
  And the top-level area «  Аналитика  » is saved and shown as «Аналитика»
  And a description of 2 001 characters is refused with «Описание должно содержать не более 2000 символов»,
    and one of 2 000 characters with line breaks is kept and shown as typed

Scenario: Sibling names are unique, and top-level areas are siblings of each other (BR-AW-4)
  Given the example tree
  When Ann creates a top-level «логистика », or «СКЛАДСКИЕ системы» under «Логистика»
  Then each is refused with «Область с таким названием уже есть на этом уровне», and the tree is unchanged
  But «Складские системы» can be created under «Инфраструктура», whose children are other siblings
  And «Другая», which has its own top-level «Логистика», is unchanged, and the name did not clash with it

Scenario: Two writes of one name at one level keep one (spec § NFR Concurrency)
  When Ann and Ivan create a top-level «Аналитика» at the same moment,
    or Ann creates «Аналитика» under «Логистика» while Ivan renames «Транспортные системы» «Аналитика»
  Then exactly one «Аналитика» exists at that level, the other request is refused with «Область с таким названием уже есть на этом уровне»,
    and neither is answered with a server error

Scenario: The tree is at most 10 levels deep at creation (BR-AW-3)
  Given a chain from the top-level «Финансы» down to «Ф10» at level 10, with «Ф9» at level 9
  When Ann creates a child of «Ф10»
  Then it is refused with «Дерево областей не может быть глубже 10 уровней»
  But a second child of «Ф9» is created at level 10
  And a creation and a move at the same moment that together would pass 10 levels end with one refused (US-2)

Scenario: A parent from another organization, or a missing one, names nothing (BR-AW-1, BR-AW-15)
  When Ann creates an area under «Другая»'s «Логистика», under an id that names no area, or under an area deleted while her form was open
  Then each is answered «Область не найдена.», identically, and no area is created in «Ромашка» or «Другая»

Scenario: Every member reads the whole tree, within 2 s at the stated volume (spec § NFR)
  Given the example tree, with descriptions on «Логистика» and «Финансы»
  When Max, a «Участник», or Vera, a «Читатель», opens «Области» and the page of «Складские системы»
  Then they see the five areas with their paths and descriptions, and the page with its parent and its links
  And they are offered no create, rename, description, move or delete action, and «Новая область» is absent
  And Olga, who is in no organization, is answered «Организация не найдена.» on «Области» and on any area's page
  And in an organization of 1 000 areas over 10 levels, «Области» shows the whole tree within 2 s

Scenario: An organization with no areas works fully (brief § 3.1.4; SC-07; BR-AW-2)
  Given «Ромашка» has no areas
  When Ann opens «Области»
  Then it reads «В организации пока нет областей» and offers her «Новая область»
  When Max opens it
  Then it reads «В организации пока нет областей» with «Области создаёт администратор организации.» and no action
  And a project and a team are created, a work item is created in the project, and the project's board opens with it, with no area asked for anywhere

Scenario: The organization's settings name «Области» once (BR-AW-12)
  When Max opens the organization's settings
  Then its navigation holds «Области», which opens the organization's tree
  And no project's settings hold «Области» any more (US-4)
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: a holder of «Управление» on an area creates child areas under it, and the parent picker
#   offers only the areas the user may create in, the top level only to the administrator; participants on the
#   create form; «Наследовать доступ» shown, set on the form and applied last (BR-MX-3; OQ-2 B)
# `feat-043-area-project-team-navigation`: the top-level «Области» and the area's own page, where these pages move
# `feat-042-area-board-and-overview`: the area's views and its overview's figures
```
