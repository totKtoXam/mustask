# US-5: The upgrade keeps every project's tree as a project node at the top of the organization's tree

- story: US-5
- depends-on: US-1, US-4
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); reworked 2026-10-27. The file's slug is stale from «drops»
- status: pending

As the **owner of a running stand**, I want **the upgrade to turn each project's existing tree into the project's node of the organization's tree, with every item's area kept**, so that **nothing is lost and an administrator only adds the departments and moves the projects into them (BR-AW-14)**.

The upgrade is new migrations on top of the merged ones, editing none (ADR-0028 rule 12). The first build's migration (it dropped the trees) is replaced by this one on the same branch before merge: the developer decides whether it is reworked in place or deleted and written anew, as it never reached `main`; it is not kept with a new migration layered on top, which would upgrade from data it already dropped (spec § NFR Existing data). The organization is the tree's top and not a stored node (OQ-4 A), so project nodes start at the top level.

Data before the upgrade, in «Ромашка»: Alpha (ALP) has its root «Alpha» with the children «Платформа» and «Отчёты», and «Платформа» has «API»; Beta (BET) has its root «Beta» alone; Gamma (GAM), deactivated, has its root «Gamma» with «Ops». Alpha has 18 items: 12 in «Платформа / API», 4 in «Отчёты», 2 in the root, 3 of them in «Удалена»; Beta has 3 items in its root, Gamma 1 in «Ops». MARS owns 5 of Alpha's items, and Alpha's settings name MARS the default team. «Другая» has Zeta (ZET) with its root and 2 items.

## Acceptance criteria

```gherkin
Scenario: Every project's tree becomes its node and sub-areas (BR-AW-14)
  When the upgrade runs
  Then the roots «Alpha», «Beta», «Gamma» and Zeta's are nodes of type «Проект» at the top of their organization's tree, each tied to its project
  And «Платформа», «API», «Отчёты» and «Ops» are sub-areas below them, with their names and places unchanged
  And «Ромашка» has no department and «Другая» none

Scenario: Every item keeps its area and nothing else moves (BR-AW-14, BR-AW-11)
  When the upgrade has run
  Then each of Alpha's 18 items, Beta's 3, Gamma's 1 and Zeta's 2 keeps its area, project, identifier, type, state, owning team, assignee,
    estimates, parent and children, and its place on the team and project backlogs
  And no item's «Изменён» moves: the upgrade is no actor's edit
  And MARS still owns its 5 items, Alpha's settings still name MARS, and every project–team link is as before
  And when Gamma is restored, its node and «Ops» are there as before

Scenario: Nothing is dropped, and the counts are logged (BR-AW-14; spec § NFR)
  When the upgrade has run on this data
  Then the database holds the same count as before of organizations, memberships, projects (Gamma included), teams, team memberships,
    project–team links, default-team settings, work items, backlog places and areas
  And the log names, per kind, how many nodes it typed: 4 project nodes, and how many sub-areas it kept: 4
  And the MR states "data dropped: none; remapped: the per-project trees into project nodes"

Scenario: What a user sees (BR-AW-12; US-1, US-4)
  Given the upgrade has run
  When Ann opens the organization's «Области»
  Then it shows the project nodes «Alpha», «Beta» and «Gamma» (marked «деактивирован», OQ-6 A) with their sub-areas, and offers her «Новый департамент»
  When Max opens it
  Then he sees the same tree and no action
  And Alpha's cards and list name their areas as before, and its backlog and board show all its items as before

Scenario: An administrator adds departments and moves projects in (US-1, US-3)
  Given the upgrade has run
  When Ann creates the department «Логистика» and moves Alpha and Beta under it
  Then their paths are «Логистика / Alpha» and «Логистика / Beta», their sub-areas and items' areas are unchanged, and no item changed

Scenario: A tree the upgrade cannot keep is reported, not cut (BR-AW-14; OQ-13 A)
  Given a project whose own tree is 11 levels deep counting its node as level 1, and another exactly 10
  When the upgrade runs
  Then it names the first project and fails before changing any row
  And without the first project the upgrade keeps the second at the top level, and a move of it into a department is refused with «Дерево областей не может быть глубже 10 уровней»

Scenario: Databases with little to upgrade migrate (BR-AW-14)
  Given an organization with no project
  When the upgrade runs
  Then it is as before, the counts logged are 0, and its administrator can create the first department (US-1)
  And on a database with no organization at all the migrations apply and change no row
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: nothing to upgrade; grants and participants start empty
# `feat-015-work-item-history`: the upgrade is no actor's change and writes no entry
```
