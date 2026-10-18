# US-5: The upgrade drops the per-project areas

- story: US-5
- depends-on: US-1, US-4
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 § Existing data; feat-028 US-7's owed clause
- status: pending

As the **owner of a running stand**, I want **the upgrade to clear every area the per-project model made, and the area every work item points at**, so that **the organization's tree starts from an empty slate that an administrator builds once, against the real structure, and nothing is left pointing at a tree that is gone (BR-AW-14, OQ-1 A)**.

The human waived preservation of existing data (doc-012 § Existing data). The upgrade is new migrations on top of the merged ones, editing none (ADR-0028 rule 12). It drops the per-project trees and the items' pointers and remaps nothing (OQ-1 A; under OQ-1 B this story is rewritten).

Data before the upgrade, in «Ромашка»: Alpha (ALP) has its root «Alpha» with the children «Платформа» and «Отчёты», and «Платформа» has «API»; Beta (BET) has its root «Beta» alone; Gamma (GAM), deactivated, has its root «Gamma» with «Ops». Alpha has 18 items: 12 in «Платформа / API», 4 in «Отчёты», 2 in the root, 3 of them in «Удалена»; Beta has 3 items in its root, Gamma 1 in «Ops». MARS owns 5 of Alpha's items, and Alpha's settings name MARS the default team. «Другая» has Zeta (ZET) with its root and 2 items. New interface copy is the design's.

## Acceptance criteria

```gherkin
Scenario: Every per-project area is dropped, and every item's pointer with it (BR-AW-14)
  When the upgrade runs
  Then «Ромашка» and «Другая» have no area: the roots «Alpha», «Beta», «Gamma» and Zeta's, and «Платформа», «API», «Отчёты» and «Ops», are gone
  And no work item points at an area, the 3 in «Удалена» and Gamma's included

Scenario: Nothing else about an item or a project moves (BR-AW-14, BR-AW-11)
  When the upgrade has run
  Then Alpha's 18 items, Beta's 3, Gamma's 1 and Zeta's 2 keep their project, identifier, type, state, owning team, assignee,
    estimates, parent and children, and their places on the team and project backlogs
  And no item's «Изменён» moves: the upgrade is no actor's edit, as feat-028 BR-OL-13's was none
  And MARS still owns its 5 items, Alpha's settings still name MARS, and every project–team link is as before
  And when Gamma is restored, its item is there with no area, and Gamma has no tree

Scenario: Nothing but areas is dropped, and the counts are logged (BR-AW-14; spec § NFR)
  When the upgrade has run on this data
  Then the database holds the same count as before of organizations, memberships, projects (Gamma included), teams, team memberships,
    project–team links, default-team settings, work items and backlog places
  And the log names, per kind, how many it dropped: 8 areas, and 24 items whose area it cleared
  And the MR states "data dropped: every per-project area and every work item's area; remapped: none"

Scenario: What a user sees: no area anywhere until an administrator creates one (BR-AW-12; US-1, US-4)
  Given the upgrade has run
  When Ann opens the organization's «Области»
  Then it reads «В организации пока нет областей» and offers her «Новая область»
  When Max opens it
  Then it reads the same with «Области создаёт администратор организации.» and no action
  And Alpha's «Общие» reads «Проект не входит ни в одну область», and MARS's page «Команда не входит ни в одну область»
  And Alpha's cards and list name no area, and its backlog and board show all its items as before
  And `…/projects/<Alpha>/settings/areas`, bookmarked before the upgrade, reads «Страница не найдена»

Scenario: An administrator builds the tree with this line's own tools (US-1, US-3)
  Given the upgrade has run
  When Ann creates the top-level «Платформа» and links it to Alpha and Beta
  Then the name is free, though a per-project area of that name existed before, and «Платформа» lists Alpha and Beta
  And no item of Alpha or Beta changed: the link moves nothing into the area

Scenario: Databases with little to drop migrate (BR-AW-14)
  Given an organization with no project, and so no area
  When the upgrade runs
  Then it is as before, the counts logged are 0, and its administrator can create the first area (US-1)
  And on a database with no organization at all the migrations apply and change no row
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: nothing to upgrade; grants and participants start empty
# `feat-015-work-item-history`: an area cleared by the upgrade is no actor's change and writes no entry
```
