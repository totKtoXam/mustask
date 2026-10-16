# US-11: A user filters team, project and area views

- story: US-11
- depends-on: US-7, US-8, US-9, US-12
- shippable: no — an analysis story: `feat-013-board-backlog-filter` delivers it (spec § Decomposition)
- status: pending

As an **organization member reading a team's, a project's or an area's backlog or board**, I want **to narrow the view by
its items' fields and find my choices kept when I come back**, so that **I find the work I need in a view that spans
projects, and never see or count what I may not see** (brief §§ 7.1, 11.6.10, 14).

## Acceptance criteria

Data. Organization O has projects A and B, visibility «Организация», and C, «Ограниченный»; team MARS is linked to A, B
and C, team VENUS to A, and the area Logistics to A, B and C. U is an organization «Участник» with no grant, so C is
hidden from U. The level is «Истории». Each item is on the views of its owning team, of its project and of Logistics:

| Item | Project | «Тип» | «Команда» | «Статус» | «Приоритет» | «Исполнитель» | «Спринт» | «Заголовок» |
|---|---|---|---|---|---|---|---|---|
| A1 | A | История | MARS | «Новая» | 1 | Анна | MARS's «Спринт 3» | «Оплата картой» |
| A2 | A | Баг | VENUS | «Активна» | 2 | Борис | VENUS's «Спринт 5» | «Ошибка оплаты» |
| A3 | A | История | «Без команды» | «Новая» | 2 | «Не назначен» | none | «Склад» |
| B1 | B | История | MARS | «Активна» | 1 | Борис | none | «Отчёт» |
| C1 | C | История | MARS | «Новая» | 1 | Анна | MARS's «Спринт 3» | «Оплата» |

With no filter, U's MARS backlog therefore holds A1 and B1, A's holds A1, A2 and A3, and Logistics' holds A1, A2, A3 and B1.

A filter's options are:

- «Проект»: the projects the view reaches that the user may see;
- «Команда»: the teams linked to those projects, then «Без команды»;
- «Исполнитель»: the people assigned to the view's items that the user may see, then «Не назначен»;
- «Спринт»: on a team's views «Текущий спринт» (its Active sprint) and the team's sprints; elsewhere the sprints of the
  teams that own the view's visible items; newest first, each naming its team, Closed ones included, then «Без спринта»;
- «Тип» and «Статус»: those the process gives the level.

No option shows a count. The brief's "date" (§ 7.1) is «Целевая дата», the date that plans an item's delivery; «Дата
начала» and the creation and change times are not view filters.

```gherkin
Scenario Outline: Each filter narrows each kind of view to its matching items (BR-MX-16, BR-MX-17, BR-MX-21)
  When U filters MARS's backlog, A's backlog and Logistics' backlog by <filter> <value>
  Then MARS's holds <team>, A's <project> and Logistics' <area>, each total reading that many; «—» means the view does not offer the filter
  And each of the three boards holds the same items, each in the column of its «Статус»

  Examples:
    | filter        | value             | team   | project | area   |
    | «Проект»      | B                 | B1     | —       | B1     |
    | «Команда»     | «Без команды»     | —      | A3      | A3     |
    | «Команда»     | VENUS             | —      | A2      | A2     |
    | «Исполнитель» | Борис             | B1     | A2      | A2, B1 |
    | «Исполнитель» | «Не назначен»     | none   | A3      | A3     |
    | «Тип»         | Баг               | none   | A2      | A2     |
    | «Статус»      | «Активна»         | B1     | A2      | A2, B1 |
    | «Приоритет»   | 1                 | A1, B1 | A1      | A1, B1 |
    | «Спринт»      | MARS's «Спринт 3» | A1     | A1      | A1     |
    | «Спринт»      | «Без спринта»     | B1     | A3      | A3, B1 |
    | keyword       | «оплат»           | A1     | A1, A2  | A1, A2 |

Scenario: A keyword matches the title or the identifier, ignoring letter case
  When U filters A's backlog by the keyword «ОПЛАТ», and then by A2's identifier
  Then the first holds A1 and A2, and the second A2 alone

Scenario: «Целевая дата» filters the «Эпики» and «Фичи» views by a range
  Given MARS owns the features F1 of A, due 2026-11-10, F2 of B, due 2026-12-01, and F3 of A, with no «Целевая дата»
  When U filters MARS's «Фичи» backlog by «Целевая дата» from 2026-11-01 to 2026-11-30
  Then it holds F1 alone; with only the end 2026-11-30 set it holds F1, and with only the start 2026-11-15 set, F2
  And no range holds F3, and the «Истории» views, whose types carry no «Целевая дата», do not offer the filter

Scenario: Filters combine: an item matches every filter chosen, and any one value chosen within a filter (brief § 14)
  When U filters Logistics' backlog by «Статус» «Новая» or «Активна», «Приоритет» 1, and «Проект» A or B
  Then it holds A1 and B1 and reads 2 in total
  And clearing «Приоритет» adds A2 and A3, and clearing every filter gives back A1, A2, A3 and B1, each once

Scenario: Options and totals come after visibility, and a filter discloses nothing (BR-MX-1, BR-MX-21; NFR Non-disclosure)
  When U opens the filters of MARS's backlog and of Logistics' board
  Then «Проект» offers A and B and never C, «Команда» on Logistics offers MARS, VENUS and «Без команды», and every
    option, total and column count equals the one for the same data without C
  And a view requested filtered by C, by a project or a team of another organization, or by an id that names nothing
    gives one and the same answer, which reveals neither C nor the other organization
  And U's filters narrow only U's view: another user's views of Logistics and its overview stay as they were (SC-03)

Scenario: The sprint filter lists the sprints of the teams that own items in the view (brief § 8.7; SC-02)
  Given team ORION is linked to A and owns no item there
  And MARS's «Спринт 2» (Closed), VENUS's «Спринт 5» (Planned) and MARS's «Спринт 3» (Active) were created in that
    order, and VENUS has no Active sprint
  When U opens the «Спринт» filter of A's views
  Then it lists MARS's «Спринт 3», VENUS's «Спринт 5» and MARS's «Спринт 2», newest first, each entry naming its team,
    then «Без спринта», and none of ORION's sprints
  And on MARS's views it lists «Текущий спринт» (MARS's «Спринт 3»), «Спринт 3», «Спринт 2», then «Без спринта»;
    choosing «Текущий спринт» leaves A1 alone; on VENUS's views it offers no «Текущий спринт»
  And choosing VENUS's «Спринт 5» leaves A2 alone, and once it is renamed «Спринт 3» the two sprints of that name are
    still told apart by their team

Scenario: A filtered team or project backlog reorders against the full list (doc-003 US-2, kept; BR-MX-18)
  Given U's MARS «Истории» backlog reads A1, B1, A4, B2, where A4 is a «Новая» story of A and B2 an «Активна» one of B
  When U filters it by «Статус» «Новая», sees A1 and A4, and moves A4 above A1
  Then the unfiltered backlog reads A4, A1, B1, B2: every item the filter hid keeps its relative order
  And the same holds on A's backlog, while an area view, which has no order, offers no reordering, filtered or not

Scenario: The level and the filters are remembered per view and per user, and «Включая дочерние области» per area and per user (brief §§ 11.6.10, 17)
  Given on MARS's board U picked the level «Фичи» and the project filter A, and turned «Включая дочерние области» off
    on Logistics
  When U opens MARS's backlog and comes back, reloads the page, or signs out and in again in the same browser
  Then MARS's board shows «Фичи» filtered to A, and Logistics' board, backlog and overview have the switch off
  And VENUS's and A's boards keep their own choices, another user in that browser gets none of U's, and an area view
    U opens for the first time has the switch on
  And a remembered value U may no longer use, such as B once unlinked from MARS or a project that turned hidden, is
    dropped at the next open: the view shows none of its items and names it nowhere (BR-MX-21)

Scenario: A filtered view keeps its view's time budget (NFR Volumes)
  Given an organization at the spec's NFR volumes
  When a member filters a team backlog of 5 000 items, and an area view over 50 000 items, by one filter and by three
  Then the backlog arrives within 2 s, and so does the area view's first page
```
