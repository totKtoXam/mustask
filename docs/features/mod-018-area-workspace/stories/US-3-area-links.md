# US-3: Areas are linked with projects and teams from either end

- story: US-3
- depends-on: US-1
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 US-3's area part, and the area clauses feat-028 left owed (its US-1, US-2, US-3, US-5)
- status: pending

As a **manager of an area and of a project or a team** (until `mod-019-scoped-access`, «Администратор»), I want **to link and unlink them from the page of either end, or at creation**, so that **one project sits in several areas and one area gathers several projects and teams, with no copy of any of them, and no link changes the work (BR-AW-6, BR-AW-8)**.

The ends are an area's page («Проекты», «Команды»), a project's «Общие» («Области») and a team's page («Области») (BR-AW-12). Outside the tree an area is shown by its path (BR-AW-13). Cast and conventions: spec § Stories; the example tree of US-1 stands, Gamma is deactivated where a scenario says so, and SATURN is deleted.

## Acceptance criteria

```gherkin
Scenario Outline: A link is made from the page of either end (BR-AW-6, BR-AW-12; brief § 11.6.5-6)
  Given «Логистика» and <other> are not linked
  When Ann opens <page>, presses its add command, types part of the other end's name (an area: part of its path) into the searchable multiselect, picks it and confirms
  Then «Логистика» and <other> are linked, the link records Ann and the time,
    and both pages list the other end with a way to open its page
  Examples:
    | other       | page                                    |
    | project Alpha | the area's «Проекты»                  |
    | project Alpha | Alpha's «Общие», its «Области»        |
    | team MARS   | the area's «Команды»                    |
    | team MARS   | MARS's page, its «Области»              |

Scenario: Several ends are linked in one confirmation, all or none (BR-AW-6)
  When Ann picks Alpha and Beta together in the «Проекты» of «Финансы» and confirms
  Then «Финансы» is linked to both
  And when Beta is deactivated after she picked it and before she confirms, «Финансы» is linked to neither, and the answer is «Проект не найден.»
  And picking «Финансы» and «Логистика / Складские системы» together on MARS's page links MARS to both

Scenario: The multiselects offer only what may be linked (BR-AW-1, BR-AW-6)
  Given «Логистика» is linked to Alpha and MARS, and Gamma is deactivated
  When Ann searches the project multiselect of «Логистика»
  Then it offers Beta, and never Alpha (linked), Gamma (deactivated) or Zeta («Другая»)
  And its team multiselect offers VENUS and JUPITER, and never MARS (linked), SATURN (deleted) or Orion («Другая»)
  And the area multiselect on Alpha's «Общие» offers every area of «Ромашка» but «Логистика», each by its path, and none of «Другая»
  And in an organization of 1 000 areas, typing part of a path narrows the area multiselect's offer

Scenario: Each end lists its own links (BR-AW-2, BR-AW-6, BR-AW-13; doc-012 US-10)
  Given «Логистика» is linked to Alpha and MARS, «Логистика / Складские системы» to Beta and VENUS
  Then the page of «Логистика» lists Alpha under «Проекты» and MARS under «Команды», and neither Beta nor VENUS
  And Beta's «Общие» lists «Логистика / Складские системы» under «Области», and VENUS's page the same
  And no list shows a project or a team inside an area: the tree lists areas only
  And an area with no links reads «К области не привязаны проекты» and «К области не привязаны команды»,
    a project with none «Проект не входит ни в одну область», and a team with none «Команда не входит ни в одну область»,
    each offering the add command only to the administrator

Scenario: SC-06 — one project in two areas stays one project (BR-AW-2, BR-MX-16)
  Given Alpha has work items
  When Ann links Alpha with «Логистика» and with «Финансы»
  Then both areas list Alpha, Alpha's «Общие» lists both, and the organization's project list holds Alpha once
  And Alpha has the same work items with the same identifiers, owning teams and backlog places, and no project, team, area or item was created

Scenario: A link changes nothing else and grants nothing (BR-AW-8)
  Given Max and Vera read and change Alpha's items as their roles allow, and MARS owns some of them
  When Ann links Alpha with «Логистика», and MARS with «Финансы»
  Then no work item, owning team, backlog place, project–team link or other area link changed
  And Max and Vera read and change exactly what they did before

Scenario: Unlinking removes only that link (BR-AW-6, BR-AW-7; SC-06)
  Given Alpha is linked to «Логистика», «Финансы» and MARS, and MARS to «Логистика»
  When Ann unlinks Alpha from «Логистика», from the area's page or from Alpha's «Общие»
  Then Alpha–«Финансы», Alpha–MARS and MARS–«Логистика» stay, and Alpha, «Логистика», MARS and Alpha's items are unchanged
  When she then unlinks MARS from «Логистика», from either page
  Then MARS's project links and the items it owns are unchanged, and «Логистика» still exists

Scenario: Linking a linked pair or unlinking an unlinked one changes nothing (BR-AW-6)
  Given Ivan linked «Логистика» with Alpha at time T
  When Ann links them again from either page, and unlinks «Финансы» from Beta and from VENUS, which are not linked
  Then each is answered as a success, «Логистика» and Alpha are linked once and the link still records Ivan and T, and nothing else changes
  And two requests that link the same unlinked pair at once leave exactly one link, and neither is answered with a server error

Scenario: Links chosen at creation are made in the same operation (BR-AW-6; doc-012 US-1, US-2)
  When Ann creates «Аналитика» under «Финансы» with the projects Alpha and Beta and the team MARS
  Then «Аналитика» exists linked to exactly Alpha, Beta and MARS, each link recording Ann and the time
  When she creates project Delta (DEL) with the area «Логистика / Складские системы», and team NEPTUNE with the area «Финансы»
  Then Delta and NEPTUNE are each linked to that area, and nothing else is created
  And a creation naming a deactivated or foreign project, a deleted or foreign team, or a deleted or foreign area is refused
    with «Проект не найден.», «Команда не найдена.» or «Область не найдена.», nothing is created, and the name and key stay free

Scenario: Only active ends are linked; a deactivated project keeps its area links (BR-AW-10; ADR-0026 rules 2-3)
  Given Gamma is linked to «Логистика» and is then deactivated, and SATURN is deleted
  When Ann asks to link Gamma with «Финансы», to unlink Gamma from «Логистика», or to link SATURN with «Логистика»
  Then the first two are answered «Проект не найден.» and the third «Команда не найдена.», and nothing changes
  And «Логистика» no longer lists Gamma, yet the link is kept
  When Ann restores Gamma
  Then «Логистика» lists Gamma again, and Gamma's «Общие» lists «Логистика»

Scenario: Deleting a team ends its area links (BR-AW-10; feat-028 BR-OL-9)
  Given MARS is linked to Alpha, to «Логистика» and to «Финансы»
  When Ann deletes MARS
  Then neither area lists MARS, no area is deleted, and the deletion is otherwise as feat-028 US-5 states
  And a link of MARS with an area that runs at once with its deletion leaves no link to the deleted team, whichever finishes first,
    and neither request is answered with a server error

Scenario: A link gives no power over its other end (BR-AW-7)
  Given «Логистика» is linked to Alpha and MARS
  When Ann opens Alpha's «Общие» or MARS's page
  Then «Логистика» is listed with a way to open its page and the unlink command, and nothing there renames, describes, moves or deletes it
  And nothing on the area's page renames, describes, links teams to or deactivates Alpha, or renames, staffs or deletes MARS
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: «Управление» on one end makes no link; an area link ends on either end's «Управление»;
#   a holder of «Изменение» sees the links and no command to link or unlink (doc-012 US-3)
# `feat-041-project-visibility`: a project the user may not see is absent from an area's «Проекты», from the multiselects and from every count,
#   and linking it answers «Проект не найден.»
# `feat-043-area-project-team-navigation`: the links on the objects' own pages (the area's «Проекты» and «Команды», the project's «Общие», the team's «Обзор»)
# `feat-042-area-board-and-overview`: the area's overview counts its linked projects and teams, with its child areas' on request
```
