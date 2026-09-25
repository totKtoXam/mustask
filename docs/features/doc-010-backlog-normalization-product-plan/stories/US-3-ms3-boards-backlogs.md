# US-3: MS-3 «Доски и бэклоги по уровням»

- story: US-3
- depends-on: US-2
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-3 «Доски и бэклоги по уровням»**, so
that **each team works its own ordered backlog and Kanban board per level.
Work created on a team's surface lands in that team's area, and the hierarchy
stays visible.**

Delivered by `mod-006-boards-backlogs`, `feat-004-backlog-hierarchy` and
`feat-NNN-board-backlog-filter`.

## Acceptance criteria

```gherkin
Scenario: Demo — a team works its own backlog and board
  Given MS-2's project with its first team and the team «Платформа», whose
    default area is «Платформа»
  And «Анна Смирнова», a member of «Платформа»
  And a «Фича» «Отчёты» in the area «Платформа»
  And «Импорт данных», a «Пользовательская история» on that team's backlog
    whose «Исполнитель» is Анна
  When Анна opens the «Истории» backlog of «Платформа» and creates a
    «Пользовательская история» «Экспорт отчёта» there
  Then its area is «Платформа» and it is the last row of that backlog (D-7)
  And it is on no board or backlog of the first team
  When she makes herself its «Исполнитель», filters the backlog by
    «Исполнитель» «Анна Смирнова» and, in the filtered view, drags
    «Экспорт отчёта» above «Импорт данных»
  And clears the filter
  Then «Экспорт отчёта» is above «Импорт данных» in the full backlog (D-14)
  When she assigns it the parent «Отчёты» from the backlog
  Then expanding «Отчёты» on the «Фичи» backlog shows «Экспорт отчёта»
    beneath it
  When she drags its card on the «Истории» board to the column «Разрешена»
  Then the item's state is «Разрешена»
  And the card sits in the column of «Разрешена», not in the column
    «В работе» (D-4)

Scenario: Guard — a stale drag of a deleted item is refused
  Given two members with the «Истории» board of «Платформа» open
  When the first deletes «Экспорт отчёта»
  And the second, whose view still shows its card, drags it to another column
  Then the move is refused and the item stays in «Удалена»
  And the second member sees «Порядок изменён другим участником — список
    обновлён», and the refreshed board no longer shows the card
```
