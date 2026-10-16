# US-8: The area board and overview aggregate the linked projects

- story: US-8
- depends-on: US-6, US-7
- shippable: no — an analysis story: `feat-NNN-area-board-and-overview` delivers it; the area views' filters are US-11's
- status: pending

As a **member of the organization working in an area**, I want **the area's backlog, board and overview to gather the
work of the projects linked to the area and to its child areas, each item once and only what I may see**, so that **I
follow the whole workspace without opening each project, and no hidden project leaks through a view or a figure**.

## Acceptance criteria

Data. Organization «Компания». Areas: «Логистика», with children «Складские системы» and «Транспортные системы»;
«Финансы», top-level. Projects, visibility «Организация» unless stated:
- «Проект A» (key PA), linked to «Логистика» and «Финансы»; teams MARS and VENUS;
- «Проект B» (PB), linked to «Складские системы»; team MARS;
- «Проект C» (PC), linked to «Логистика» and «Транспортные системы»; teams VENUS and ORION;
- «Проект D» (PD), «Участники областей», linked to «Финансы»; team MARS;
- «Проект R» (PR), «Ограниченный», linked to «Логистика»; no team.

Area–team links: MARS to «Логистика» and «Финансы», VENUS to «Логистика», ORION to none. Items, all «Истории» in
«Новая»: PA-US-1 (MARS), PA-US-2 (VENUS), PA-US-3 (no team), PB-US-1 (MARS), PC-US-1 (VENUS), PC-US-2 (ORION),
PD-US-1 (MARS), PR-US-1 (no team). Users: «Анна», organization admin; «Борис», member with no grant; «Вера», viewer
with «Изменение» on «Проект A»; «Глеб», member with «Чтение» on «Логистика», its only direct participant.
«Включая дочерние области» is on the first time a user opens an area's view, as an area matches itself and its
descendants (doc-001 BR-15, kept); it is then remembered per user and area, for the backlog, the board and the overview
alike (US-11). The active sprints of linked teams join the overview with US-12.

```gherkin
Scenario: An area's views hold its projects' and its child areas' items, each once (BR-MX-16, BR-MX-17; SC-03, SC-08)
  When Борис opens the «Истории» backlog of «Логистика»
  Then it holds PA-US-1, PA-US-2, PA-US-3, PB-US-1, PC-US-1 and PC-US-2, each once, and reads 6 in total
  And the items of «Проект C» show once, although it is linked both to «Логистика» and to «Транспортные системы»
  And PC-US-2 is there although ORION is linked to no area: an area holds projects' items, whatever their team
  And every row shows its «Проект» and its «Команда», PA-US-3 with «Без команды»
  And the «Истории» board of «Логистика» holds the same 6 cards, each in the column of its state, and the
    «Эпики» and «Фичи» views hold none of them
  And opening PB-US-1 from either view opens it in «Проект B»

Scenario: Aggregation reaches every depth and still counts an item once (BR-MX-16, BR-MX-17; SC-08)
  Given «Стеллажи», a child of «Складские системы», linked to «Проект B» and to «Проект E», which has PE-US-1
  When Борис opens the «Истории» backlog of «Логистика»
  Then PE-US-1 is on it, and PB-US-1 is on it once although two areas under «Логистика» link «Проект B»

Scenario: Turning child areas off narrows only that user's view (BR-MX-17; SC-08)
  When Борис turns «Включая дочерние области» off on «Логистика»
  Then its views hold PA-US-1, PA-US-2, PA-US-3, PC-US-1 and PC-US-2, and PB-US-1 is gone, from its overview's count too
  And Анна's views of «Логистика» are unchanged

Scenario: A project linked to two areas shows on both, once on each (BR-MX-16; SC-06)
  When Борис opens the «Истории» boards of «Логистика» and of «Финансы»
  Then PA-US-1, PA-US-2 and PA-US-3 are on each board once
  When Анна unlinks «Проект A» from «Финансы»
  Then at Борис's next read the views of «Финансы» no longer hold them, and those of «Логистика» still do
  And the three items keep their project, owning team, state and backlog places

Scenario: A team linked to two areas adds no item to either (BR-MX-7, BR-MX-22; SC-05)
  Given MARS is linked to «Логистика» and to «Финансы» and owns PB-US-1 and PD-US-1
  Then no view of «Логистика», and no figure of its overview but MARS's active sprint (US-12), holds PD-US-1, Анна's
    included, and none of «Финансы» holds PB-US-1
  When Глеб opens the «Истории» board of «Финансы»
  Then it holds PA-US-1, PA-US-2 and PA-US-3, and not PD-US-1: his grant on «Логистика» does not reach «Проект D»
  And the overview of «Финансы» lists «Проект A» alone and counts 3 «Истории»

Scenario: A project the user may not see is neither shown nor counted (BR-MX-21, BR-MX-22; SC-06)
  When Борис or Глеб opens the views and the overview of «Логистика»
  Then PR-US-1 and «Проект R» are absent from rows, cards, column counts, totals, «ещё» flags and the overview,
    although Глеб holds a grant on «Логистика»
  And every answer is identical to the answer for the same data without «Проект R»
  And Анна sees PR-US-1 there, with a total of 7 (OQ-3)
  When Анна gives Борис «Чтение» on «Проект R»
  Then at his next read PR-US-1 is on his views of «Логистика» and his total reads 7

Scenario: Aggregation never widens access, and inheritance off is respected (BR-MX-21, BR-MX-22; SC-08)
  Given «Проект B» is «Участники областей» and «Наследовать доступ» is off on «Складские системы»
  When Глеб opens «Логистика» with «Включая дочерние области» on
  Then PB-US-1 is on none of its views and in none of its figures
  When Анна turns «Наследовать доступ» back on
  Then at Глеб's next read PB-US-1 is there, while Борис, who holds no grant, still does not see it

Scenario: An area view has no order of its own: it sorts and pages, in flat rows (BR-MX-16, BR-MX-18; NFR)
  Given 120 items of the «Истории» views of «Логистика» are visible to Борис
  When he opens the backlog
  Then it shows 50 rows a page, on 3 pages, sorted by creation time and then by identifier, and reads 120 in total
  And every item is on exactly one page, also when he sorts by «Проект», «Команда» or «Статус», whose ties keep the
    creation-time-then-identifier order
  And a child stands as a row of its own, never beneath its parent: feat-027 BR-T-4 and BR-T-5 nest rows on team and
    project views only
  And the backlog offers no reordering, and a reorder of MARS's or «Проект A»'s backlog changes nothing in it
  And neither the backlog nor the board of an area offers a create command
  And each board column shows at most 50 cards, then the next ones on request, and its header counts all of them

Scenario: The first page of a large area view comes within 2 s (NFR)
  Given an organization at the spec's NFR volumes, where the «Истории» views of «Логистика» hold 50 000 items
  When a member opens the backlog or the board
  Then the first page arrives within 2 s, and so do the overview's figures

Scenario: A card move writes its item's state in the item's own project (BR-MX-16, BR-MX-18, BR-MX-19, BR-MX-23)
  Given the «Истории» board of «Логистика» shows the organization process's columns «Новая», «Активна»,
    «Разрешена» and «Закрыта»
  When Вера moves the card of PA-US-1 to «Активна»
  Then PA-US-1 is «Активна» in «Проект A», with its project, owning team and backlog places unchanged
  And at the next read it is «Активна» on MARS's board and on «Проект A»'s, and the overview counts it so
  And the card of PC-US-1 offers Вера no move, and a direct request to move it is refused with 403, PC-US-1 unchanged

Scenario: The overview shows the area's projects, teams, participants and figures after visibility (BR-MX-21)
  When Борис opens the overview of «Логистика»
  Then it lists the projects «Проект A», «Проект B» and «Проект C», the teams MARS and VENUS, and Глеб as a
    participant with «Чтение»
  And it counts 6 «Истории», all of them «Новая», and none at «Эпики» or «Фичи»
  And each figure but the active sprints equals the total or a column count of the matching view for Борис, under the
    same «Включая дочерние области»: turned off, the projects are «Проект A» and «Проект C», and the count is 5
  And the teams and participants are the area's own, the participants direct and inherited (US-5), whatever that switch
  And Анна's overview also lists «Проект R», and counts 7
  And (owed by `mod-007-iterations`, US-12) it shows the Active sprint of MARS and of VENUS as each team's sprint board counts it for the user,
    whatever the items' projects: PD-US-1 in MARS's Active sprint is in that figure for Анна and in no other figure or
    view of «Логистика», and not for Борис, who may not see «Проект D»

Scenario: An area with no projects, teams or participants shows empty states (brief § 3.2)
  Given «Инфраструктура», a new top-level area with no link and no participant
  When Борис opens its overview, its backlog and its board
  Then each says «К области не привязаны проекты», and the overview also says «К области не привязаны команды»
    and «У области нет участников»
  And a way to link a project or a team is offered only to a user who holds «Управление» on «Инфраструктура»
  And an area whose only project is «Проект R» shows Борис the same no-projects state, and Анна its items
  And at «Эпики», where its projects have no item, «Логистика» shows the empty-view frame, not the no-projects state
  And once «Инфраструктура» has a child area linked to «Проект B», it links no project itself: with the switch on it
    shows PB-US-1, and with the switch off the no-projects state

Scenario: Missing areas and deactivated projects (BR-MX-1, BR-MX-24; mod-002 BR-M1-20)
  When a member opens an area that does not exist, has been deleted or belongs to another organization
  Then all three answer «Область не найдена» alike; a user who is not a member gets «Организация не найдена» first
  When Анна deactivates «Проект C»
  Then PC-US-1, PC-US-2 and «Проект C» leave the views and figures of «Логистика» for everyone, Анна included
  When she restores it
  Then they are back, and «Проект C» is linked to «Логистика» and to «Транспортные системы» as before
```
