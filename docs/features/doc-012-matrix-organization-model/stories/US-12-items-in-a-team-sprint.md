# US-12: Items in a team's sprint

- story: US-12
- depends-on: US-9, US-2, US-3, US-5, US-6, US-7, US-8
- shippable: no — an analysis story: `mod-007-iterations` delivers it with US-9 (spec § Decomposition); the sprint filter of views is US-11's
- status: pending

As a **team planner (team «Планирование»)**, I want **my team's sprint to hold the team's items from every project it works on, to close with its unfinished items moved on and its outcome kept, and to show on a board**, so that **the team plans, runs and closes its work once, not once per project, and the history of finished sprints stays readable**.

## Acceptance criteria

Unless a scenario says otherwise: projects A, B and C have visibility «Организация»; team MARS is linked to A, B and C, and team VENUS to A. Pavel is an organization «Участник» who holds «Планирование» on MARS. Vera is an organization «Участник» with no team grant, so she may change items but not manage sprints. MARS owns the items API of A (5 SP), integration of B (8 SP) and notifications of C (3 SP). "Refused" means nothing is written; an item's refusal is shown under «Спринт». The sprint's own lifecycle is US-9's.

```gherkin
Scenario: One MARS sprint holds items of three projects (SC-01, BR-MX-13)
  Given MARS's «Спринт 12» is Planned
  When Vera sets the sprint of API, integration and notifications to «Спринт 12»
  Then «Спринт 12» holds the three items, each keeping its project and identifier, 16 SP in total

Scenario Outline: An item joins only a Planned or Active sprint of its own owning team (BR-MX-13, BR-MX-14, BR-MX-1)
  Given API is owned by <owner>
  When Vera sets its sprint to <sprint>
  Then the save is refused with <text>, and API's sprint and other fields are unchanged
  Examples:
    | owner         | sprint                           | text                                                                   |
    | MARS          | VENUS's Active «Спринт 3»        | «Спринт принадлежит другой команде.»                                   |
    | «Без команды» | MARS's Planned «Спринт 12»       | «Сначала выберите команду: спринт есть только у команды.»              |
    | MARS          | MARS's Closed «Спринт 11»        | «Спринт завершён: в него нельзя добавить элемент.»                     |
    | MARS          | a sprint of another organization | «Спринт не найден.», the answer for a sprint id that names nothing     |

Scenario: Closing moves the unfinished items and records the outcome (BR-MX-14; brief § 8.5)
  Given MARS's Active «Спринт 12» holds API and notifications in «Закрыта», integration in «Активна», a bug of A in «Разрешена» and a story in «Удалена»
  And MARS's «Спринт 13» is Planned
  When Pavel closes «Спринт 12», choosing «Спринт 13» as the destination
  Then in one operation «Спринт 12» becomes Closed, and integration and the bug move to «Спринт 13»
  And API and notifications stay in «Спринт 12», whose outcome lists API of A at 5 SP and notifications of C at 3 SP
  And the story in «Удалена» stays in «Спринт 12», outside the outcome

Scenario: The destination of the unfinished items is chosen at close (BR-MX-14)
  Given MARS's Active «Спринт 12» holds integration in «Активна»
  When Pavel closes it choosing no sprint
  Then it closes and integration has no sprint, while choosing VENUS's Planned «Спринт 3» or MARS's Closed «Спринт 11» instead refuses the close with «Незавершённые элементы можно перенести только в запланированный спринт этой команды.»

Scenario: The outcome never changes afterwards, and a reopened item keeps its sprint (BR-MX-14, BR-MX-13; brief § 8.5)
  Given «Спринт 12» closed with API of A completed at 5 SP
  When API is later reopened
  Then its «Спринт» still reads «Спринт 12», a filter by «Спринт 12» holds it, and the outcome still lists it at 5 SP
  When API is re-estimated at 8 SP, put into «Спринт 13», or given the owning team VENUS
  Then the outcome of «Спринт 12» still lists API of A at 5 SP

Scenario: Closing moves items the closer cannot see, without disclosing them (BR-MX-14, BR-MX-21)
  Given C is «Ограниченный» with no grant for Pavel, and the Active «Спринт 12» holds integration and notifications in «Активна»
  When Pavel closes «Спринт 12» towards «Спринт 13»
  Then both items move to «Спринт 13», and neither the close confirmation, nor the outcome, nor any board Pavel opens names, lists or counts notifications or C

Scenario: A close reaches a deactivated project's items without showing them (BR-MX-14; mod-002 BR-M1-20, ADR-0028 rule 3)
  Given B is deactivated, and the Active «Спринт 12» holds integration of B in «Активна» and API of A in «Закрыта»
  When Pavel opens MARS's sprint board and the close dialog, and closes «Спринт 12» towards «Спринт 13»
  Then the board and the confirmation show API alone and name neither integration nor B
  And once B is restored, integration is in «Спринт 13», and the outcome of «Спринт 12» lists API of A

Scenario Outline: A sprint race ends in one order and never in a server error (BR-MX-14; spec NFR concurrency; ADR-0028 § 10)
  Given MARS's Active «Спринт 12» holds login of A and integration of B in «Активна», «Спринт 13» is Planned and empty, and migration of B is in no sprint
  When <requests> arrive at the same moment
  Then <result>, and no request ends in a server error
  Examples:
    | requests                                                                     | result                                                                                                                                                      |
    | Vera sets migration's sprint to «Спринт 12»; Pavel closes it towards «Спринт 13» | her save is refused with «Спринт завершён: в него нельзя добавить элемент.» and migration has no sprint, or it saves first and the close moves it to «Спринт 13» |
    | Pavel closes towards «Спринт 13»; a holder of «Управление» on MARS removes «Спринт 13» | the close is refused with «Спринт не найден.», or the close moves its items first and the removal is refused with «Удалить можно только запланированный спринт без элементов.» |
    | Pavel closes «Спринт 12»; the organization admin deletes MARS                | the close is refused with «Команда не найдена.», or the close commits first, moving login and integration to «Спринт 13», and the deletion then leaves both «Без команды» with no sprint |
    | Pavel closes «Спринт 12»; an organization «Администратор» unlinks MARS from B, choosing «Без команды» | neither is refused: integration ends owned by no team with no sprint, outside the outcome and not in «Спринт 13», in either order            |
    | Pavel closes «Спринт 12»; Vera saves login with the owning team VENUS        | neither is refused: login ends owned by VENUS with no sprint, outside the outcome and not in «Спринт 13», in either order                                    |
    | Vera sets migration's sprint to «Спринт 13»; Pavel removes «Спринт 13»       | her save is refused with «Спринт не найден.», or it saves first and the removal is refused with «Удалить можно только запланированный спринт без элементов.» |

Scenario Outline: Changing the owning team keeps the sprint consistent (BR-MX-13; brief §§ 6.10, 8.4)
  Given API is in MARS's Active «Спринт 12», and VENUS's «Спринт 3» is Planned
  When Vera saves API with the owning team <team> and the sprint <sprint>
  Then <result>
  Examples:
    | team          | sprint             | result                                                                |
    | VENUS         | left unchanged     | saved: API has no sprint, and «Спринт 12» falls to 11 SP              |
    | VENUS         | VENUS's «Спринт 3» | saved: API is in VENUS's «Спринт 3»                                   |
    | VENUS         | MARS's «Спринт 12» | refused with «Спринт принадлежит другой команде.»                     |
    | «Без команды» | left unchanged     | saved: API has no sprint                                              |
    | «Без команды» | MARS's «Спринт 12» | refused with «Сначала выберите команду: спринт есть только у команды.» |

Scenario: Unlinking a team from a project, or deleting it, clears the pointers to its sprints (SC-09, BR-MX-12)
  Given the Active «Спринт 12» holds API, integration and notifications, and a Closed MARS sprint's outcome lists A's item login
  When MARS is unlinked from A with VENUS as the destination
  Then API and login are owned by VENUS with no sprint, unplaced on VENUS's backlog; «Спринт 12» holds 11 SP, and the Closed sprint's outcome still lists login
  When the organization admin then deletes MARS
  Then integration and notifications are «Без команды» with no sprint, keeping their projects, states and story points

Scenario: The sprint board shows the Active sprint at one level, in the team's order (SC-01, BR-MX-14, BR-MX-16, BR-MX-19; brief § 8.6)
  Given MARS's Active «Спринт 12» holds the stories API of A («Закрыта», 5 SP), integration of B and notifications of C (both «Активна»), and the feature «Платежи» of A
  And MARS's order of «Истории» lists notifications before integration
  When Vera opens MARS's sprint board
  Then it shows «Спринт 12» at «Истории», the three cards once each with their projects, in the columns of the organization's process, notifications above integration in «Активна»
  And its totals read 3 items and 16 SP, and the level drop-down at «Фичи» shows «Платежи» alone, 1 item
  When Vera moves integration to «Закрыта»
  Then only its state is written, the next read shows it there, and the board offers no reorder

Scenario: The sprint board counts only what the viewer sees, and says when there is no Active sprint (BR-MX-21; brief § 8.6)
  Given the Active «Спринт 12» above, and C becomes «Ограниченный» with no grant for Vera
  When she opens MARS's sprint board
  Then it shows API and integration, 2 items and 13 SP: the same answer as if C did not exist
  When Pavel closes «Спринт 12» choosing no sprint
  Then the board reads «У команды нет активного спринта.» with a way to MARS's sprint list, and shows no card

Scenario: A project board shows items from the sprints of several teams (SC-02; brief § 8.7)
  Given API of A is in MARS's Active «Спринт 12», and A's item search, owned by VENUS, is in VENUS's Active «Спринт 3»
  When Vera opens A's board
  Then both cards show, each with its owning team and its sprint

Scenario: The area overview shows each linked team's Active sprint, counted over visible items (BR-MX-17, BR-MX-21; brief § 8.8)
  Given area Logistics is linked to MARS and VENUS and to no project, VENUS has no Active sprint, and C is «Ограниченный» with no grant for Vera
  And MARS's Active «Спринт 12» holds API in «Закрыта», and integration and notifications in «Активна»
  When Vera opens the overview of Logistics
  Then it shows MARS's «Спринт 12» with 2 items, 1 of them in the Completed category, whatever their project, and VENUS with no Active sprint
  And the figure is the one MARS's sprint board gives Vera at «Истории»

Scenario: A deleted team's Closed sprints stay readable on its projects' «Спринты» (BR-MX-14; brief § 8.5)
  Given VENUS's Closed «Спринт 4» recorded A's item search at 2 SP, and MARS's «Спринт 11», closed after it, recorded API of A at 5 SP and notifications of C at 3 SP
  When the organization admin deletes MARS, and Vera opens A's «Спринты»
  Then it lists «Спринт 11» marked «команда удалена», with API at 5 SP, and VENUS's «Спринт 4», with search at 2 SP, the latest closed first, each read-only
  And it lists no item of C, whose «Спринты» lists notifications under «Спринт 11»
  And API and notifications have no sprint, a filter by «Спринт 11» finds nothing, and MARS's own address answers «Команда не найдена»
  When C becomes «Ограниченный» with no grant for Vera
  Then C's «Спринты» answers «Проект не найден» and A's list is unchanged
```
