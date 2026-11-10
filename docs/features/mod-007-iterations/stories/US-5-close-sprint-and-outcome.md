# US-5: Closing a sprint moves unfinished work and records the outcome

- story: US-5
- depends-on: US-2, US-4
- shippable: no — US-1..US-5 ship together as `mod-007-iterations` (one L, OQ-8)
- status: pending

As a **team planner**, I want **to close the Active sprint, choosing where its unfinished items go, and keep a record of what the sprint held and what was completed**, so that **the team starts the next sprint with its unfinished work in hand and the finished sprint stays a true history, which US-9 and US-10 read**.

## Acceptance criteria

Setting as in US-4. API and notifications are in «Закрыта» (Completed category), integration in «Активна» (In Progress), a bug of A in «Разрешена» (Resolved), a story in «Удалена». Pavel holds the team role «Участник» on MARS. Source: spec BR-6, BR-9, BR-12; doc-012 US-12.

```gherkin
Scenario: Closing moves the unfinished items and records the outcome
  Given MARS's Active «Спринт 12» holds API, notifications, integration, the bug and the story, and «Спринт 13» is Planned
  When Pavel closes «Спринт 12», choosing «Спринт 13» as the destination
  Then in one operation «Спринт 12» becomes Closed, and integration and the bug move to «Спринт 13»
  And API and notifications stay in «Спринт 12», whose outcome lists API of A at 5 SP and notifications of C at 3 SP
  And the story in «Удалена» stays in «Спринт 12», outside the outcome

Scenario: The close records a snapshot of every item the sprint held, done or moved (BR-6, BR-14)
  Given «Спринт 12» holds API (5 SP, assignee Анна), notifications (3 SP, Борис), integration (8 SP, Анна), the bug (no story points, no assignee) and the story in «Удалена»
  When Pavel closes it towards «Спринт 13»
  Then the record holds four items, each with its project, story points as then, assignee as then, and a done flag
  And API and notifications are done; integration and the bug, which moved, are not done
  And the story in «Удалена» is not in the record, and an item without story points records 0 SP and without assignee records no assignee
  And «Done» is the Completed category at the close, the same rule that decides which items stay recorded as completed
  And API's later reassignment to Борис, re-estimate or reopening changes nothing in the record

Scenario: The snapshot records each item's level and survives a purge (BR-14; OQ-14, OQ-15)
  Given «Спринт 12» held the story API and the feature «Платежи», both recorded at the close
  Then the record holds each item's level (mod-006), so that figures can be read per level
  When an administrator purges API (mod-005)
  Then the figures of «Спринт 12» still count API at its recorded points, assignee and done flag
  And the outcome list shows its row by identifier with «Удалён навсегда», no title and no link

Scenario: A moved item's «Изменён» becomes the close's time
  Given integration was last changed on 2026-11-03 and API on 2026-11-04
  When Pavel closes «Спринт 12» towards «Спринт 13» on 2026-11-13
  Then integration's «Изменён» reads 2026-11-13, as for an item reassigned by an unlink
  And API, which stays in «Спринт 12», keeps 2026-11-04

Scenario: A Closed sprint shows its outcome
  Given «Спринт 12» closed with API of A at 5 SP and notifications of C at 3 SP in its outcome
  When Vera opens «Спринт 12» from MARS's «Спринты»
  Then it shows its dates, «Завершён» and its outcome: API of A at 5 SP and notifications of C at 3 SP, 2 items and 8 SP
  And it offers a planner only the rename
  When C becomes «Ограниченный» with no grant for Vera (owed by feat-041)
  Then the outcome shows API alone, 1 item and 5 SP

Scenario: A later close never moves an item that kept a Closed sprint
  Given «Спринт 12» closed with API completed, API was reopened and still has the sprint «Спринт 12», and «Спринт 13» is Active
  When Pavel closes «Спринт 13» choosing no sprint
  Then API's sprint still reads «Спринт 12»

Scenario: The close dialog states what will move before it happens
  When Pavel opens the close dialog of «Спринт 12»
  Then it shows the number of items that will move and of those that will stay recorded, a destination chosen among the team's Planned sprints and «Без спринта», and the confirm command
  And the Planned sprint with the lowest number is preselected when the team has one, and nothing is preselected otherwise (OQ-4)

Scenario: The destination of the unfinished items is chosen at close
  Given MARS's Active «Спринт 12» holds integration in «Активна»
  When Pavel closes it choosing no sprint
  Then it closes and integration has no sprint
  And choosing VENUS's Planned «Спринт 3» or MARS's Closed «Спринт 11» instead refuses the close with «Незавершённые элементы можно перенести только в запланированный спринт этой команды.»

Scenario: The outcome never changes afterwards, and a reopened item keeps its sprint
  Given «Спринт 12» closed with API of A completed at 5 SP
  When API is later reopened
  Then its «Спринт» still reads «Спринт 12» and the outcome still lists it at 5 SP
  When API is re-estimated at 8 SP, put into «Спринт 13», or given the owning team VENUS
  Then the outcome of «Спринт 12» still lists API of A at 5 SP

Scenario: The snapshot holds hidden and deactivated projects' items, which no read shows (BR-9, BR-14)
  Given B is deactivated and «Спринт 12» holds integration of B and API of A
  When Pavel closes «Спринт 12»
  Then the record holds both; reads of the outcome, the participant figures and the dynamics count API alone until B is restored
  And once B is restored they count integration too, as recorded

Scenario: Closing moves items the closer cannot see, without disclosing them (owed by feat-041)
  Given C is «Ограниченный» with no grant for Pavel, and the Active «Спринт 12» holds integration and notifications in «Активна»
  When Pavel closes «Спринт 12» towards «Спринт 13»
  Then both items move to «Спринт 13»
  And neither the close confirmation, nor the outcome, nor any board Pavel opens names, lists or counts notifications or C

Scenario: A close reaches a deactivated project's items without showing them
  Given B is deactivated, and the Active «Спринт 12» holds integration of B in «Активна» and API of A in «Закрыта»
  When Pavel opens the close dialog and closes «Спринт 12» towards «Спринт 13»
  Then the confirmation shows API alone and names neither integration nor B
  And once B is restored, integration is in «Спринт 13», and the outcome of «Спринт 12» lists API of A

Scenario: Only a planner closes
  When Vera, who holds no team grant, closes the Active «Спринт 12»
  Then it is refused with 403 and nothing changes

Scenario Outline: A close race ends in one order and never in a server error
  Given MARS's Active «Спринт 12» holds login of A and integration of B in «Активна», «Спринт 13» is Planned and empty, and migration of B is in no sprint
  When <requests> arrive at the same moment
  Then <result>
  Examples:
    | requests                                                                              | result                                                                                                                                  |
    | Vera sets migration's sprint to «Спринт 12»; Pavel closes it towards «Спринт 13»      | her save is refused with «Спринт завершён: в него нельзя добавить элемент.», or it saves first and the close moves it to «Спринт 13»  |
    | Pavel closes towards «Спринт 13»; a holder of the team role «Менеджер» on MARS removes «Спринт 13» | the close is refused with «Спринт не найден.», or the close moves its items first and the removal is refused with «Удалить можно только запланированный спринт без элементов.» |
    | Pavel closes «Спринт 12»; the organization admin deletes MARS                         | the close is refused with «Команда не найдена.», or the close commits first, moving login and integration to «Спринт 13», and the deletion leaves both «Без команды» with no sprint |
    | Pavel closes «Спринт 12»; an organization «Администратор» unlinks MARS from B, choosing «Без команды» | neither is refused: integration ends owned by no team with no sprint, outside the outcome and not in «Спринт 13», in either order |
    | Pavel closes «Спринт 12»; Vera saves login with the owning team VENUS                 | neither is refused: login ends owned by VENUS with no sprint, outside the outcome and not in «Спринт 13», in either order |
```
