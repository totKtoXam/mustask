# US-9: A team plans sprints with items of several projects

- story: US-9
- depends-on: US-4, US-7
- shippable: no — an analysis story: `mod-007-iterations` delivers it (spec § Decomposition); the sprint filter of views is US-11's
- status: pending

As a **team planner (team «Планирование»)**, I want **one sprint of my team to hold the team's items from every project it works on**, so that **the team plans, runs and closes its work once, not once per project**.

## Acceptance criteria

Unless a scenario says otherwise: projects A, B and C have visibility «Организация»; team MARS is linked to A, B and C, and team VENUS to A. Pavel is an organization «Участник» who holds «Планирование» on MARS. Vera is an organization «Участник» with no team grant, so she may change items but not manage sprints. MARS owns the items API of A (5 SP), integration of B (8 SP) and notifications of C (3 SP). "Refused" means nothing is written; a sprint's own refusal is shown on its form, an item's under «Спринт».

```gherkin
Scenario: A sprint takes the next number of its team's own sequence (BR-MX-15; brief § 8.9)
  Given MARS has sprints 1 to 11 and VENUS has sprints 1 and 2
  When Pavel creates a MARS sprint with no name and no dates
  Then it is MARS's sprint 12, named «Спринт 12», Planned and undated, and VENUS's next number is still 3
  And the creation asks only for a name and dates: a sprint has no kind, and no release or milestone is offered

Scenario: A team's sprint list shows every sprint of the team, Closed ones included (brief § 14; BR-MX-14, BR-MX-15)
  Given MARS has the Closed «Спринт 11», the Active «Спринт 12» and the Planned «Спринт 13», and VENUS has «Спринт 3»
  When Vera, or any other member of the organization, opens MARS's sprint list
  Then it lists «Спринт 13», «Спринт 12» and «Спринт 11», highest number first, each with its dates and its state: «Запланирован», «Активен» or «Завершён»
  And it lists no VENUS sprint, and a removed Planned sprint is listed nowhere

Scenario: A creation whose default name is taken is refused and issues no number (BR-MX-15)
  Given MARS's latest sprint is number 13, renamed by hand to «Спринт 14»
  When Pavel creates a MARS sprint with no name
  Then it is refused with «Спринт с таким названием уже есть в команде.», and a creation named «Финальный» then becomes number 14

Scenario: Names are unique among the team's own sprints, and renaming keeps the number (BR-MX-15)
  Given MARS has the Closed «Спринт 12» and the Planned «Спринт 13», and VENUS has «Спринт 3»
  When Pavel renames MARS's «Спринт 13» to «Релизный», and then to «Спринт 3»
  Then both renames are saved and the sprint is still number 13: VENUS's names do not count
  And renaming it to «Спринт 12», the name of the Closed MARS sprint, is refused with «Спринт с таким названием уже есть в команде.»

Scenario: Dates are optional but paired, finish not before start, on creation and on edit (BR-MX-14)
  When Pavel saves a Planned MARS sprint
  Then it is accepted with no dates, with 2026-11-02 to 2026-11-13, and with 2026-11-02 to 2026-11-02 (a one-day sprint)
  And it is refused with a start but no finish by «Укажите обе даты спринта или ни одной.», and with 2026-11-13 to 2026-11-02 by «Дата окончания не может быть раньше даты начала.»

Scenario: Only the team's planners and the organization admin manage its sprints (BR-MX-15, BR-MX-23, BR-MX-24)
  When a user creates, renames, dates, starts, closes or removes a MARS sprint
  Then it is allowed for Pavel, for a holder of «Управление» on MARS and for an organization «Администратор» with no team grant
  And it is refused with 403 for Vera, for a holder of «Планирование» on VENUS only and for an organization «Читатель»

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

Scenario: A team has at most one Active sprint (BR-MX-14, OQ-6; NFR concurrency)
  Given MARS's «Спринт 12» is Active, and its «Спринт 13» is Planned with both dates
  When Pavel starts «Спринт 13»
  Then it is refused with «У команды уже есть активный спринт.» and «Спринт 13» stays Planned, while VENUS can still start a sprint of its own
  And when two planners start two dated Planned sprints of a team with no Active sprint at the same moment, exactly one becomes Active

Scenario: Starting needs both dates; a sprint never goes back, and only an Active one closes (BR-MX-14)
  Given MARS has no Active sprint
  When Pavel starts an undated Planned sprint, starts a Closed sprint, and closes a Planned or a Closed sprint
  Then they are refused with «Чтобы начать спринт, укажите обе даты.», «Начать можно только запланированный спринт.» and «Завершить можно только активный спринт.», and each sprint keeps its state and its dates

Scenario: Closing moves the unfinished items and records the outcome (BR-MX-14; brief § 8.5)
  Given MARS's Active «Спринт 12» holds API and notifications in «Закрыта», integration in «Активна» and a bug of A in «Разрешена»
  And MARS's «Спринт 13» is Planned
  When Pavel closes «Спринт 12», choosing «Спринт 13» as the destination
  Then in one operation «Спринт 12» becomes Closed, and integration and the bug move to «Спринт 13»
  And API and notifications stay in «Спринт 12», whose outcome lists API of A at 5 SP and notifications of C at 3 SP

Scenario: The destination of the unfinished items is chosen at close (BR-MX-14)
  Given MARS's Active «Спринт 12» holds integration in «Активна»
  When Pavel closes it choosing no sprint
  Then it closes and integration has no sprint, while choosing VENUS's Planned «Спринт 3» or MARS's Closed «Спринт 11» instead refuses the close with «Незавершённые элементы можно перенести только в запланированный спринт этой команды.»

Scenario: The outcome never changes afterwards (BR-MX-14, BR-MX-13; brief § 8.5)
  Given «Спринт 12» closed with API of A completed at 5 SP
  When API is later reopened, re-estimated at 8 SP, put into «Спринт 13», or given the owning team VENUS
  Then the outcome of «Спринт 12» still lists API of A at 5 SP

Scenario: A save into a closing sprint is refused or becomes part of the close (BR-MX-14; NFR concurrency)
  Given B's item migration is owned by MARS, in «Активна» and in no sprint, and «Спринт 12» is Active
  When Vera sets its sprint to «Спринт 12» while Pavel closes «Спринт 12» towards «Спринт 13»
  Then either her save is refused and migration has no sprint, or it saves first and the close moves it to «Спринт 13»

Scenario: Closing moves items the closer cannot see, without disclosing them (BR-MX-14, BR-MX-21)
  Given C is «Ограниченный» with no grant for Pavel, and the Active «Спринт 12» holds integration and notifications in «Активна»
  When Pavel closes «Спринт 12» towards «Спринт 13»
  Then both items move to «Спринт 13», and neither the close confirmation, nor the outcome, nor any board Pavel opens names, lists or counts notifications or C

Scenario Outline: Only a Planned sprint with no items can be removed (BR-MX-14, BR-MX-15)
  Given MARS's latest sprint, «Спринт 13», is <state> and holds <items>
  When Pavel removes it
  Then <result>, and every refusal reads «Удалить можно только запланированный спринт без элементов.»
  Examples:
    | state   | items                                          | result                                                                     |
    | Planned | no item                                        | it is gone from every list and filter, and MARS's next sprint is number 14 |
    | Planned | integration                                    | refused                                                                    |
    | Planned | only notifications, while C is hidden from him | refused, and the refusal names no item, project or count                   |
    | Active  | no item                                        | refused                                                                    |
    | Closed  | its completed items                            | refused: a Closed sprint is never removed                                  |

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

Scenario: The team sprint board spans projects and counts only what the viewer sees (SC-01, BR-MX-16, BR-MX-21; brief § 8.6)
  Given MARS's Active «Спринт 12» holds API, integration and notifications
  When Vera opens MARS's sprint board
  Then it shows each of the three cards once, each with its project, and a total of 3 items and 16 SP
  When C becomes «Ограниченный» with no grant for Vera, and she opens the board again
  Then it shows API and integration, 2 items and 13 SP: the same answer as if C did not exist

Scenario: A project board shows items from the sprints of several teams (SC-02; brief § 8.7)
  Given API of A is in MARS's Active «Спринт 12», and A's item search, owned by VENUS, is in VENUS's Active «Спринт 3»
  When Vera opens A's board
  Then both cards show, each with its owning team and its sprint

Scenario: The area overview shows each linked team's Active sprint, counted over visible items (BR-MX-21; brief § 8.8)
  Given area Logistics is linked to MARS and VENUS, VENUS has no Active sprint, and C is «Ограниченный» with no grant for Vera
  And MARS's Active «Спринт 12» holds API in «Закрыта», and integration and notifications in «Активна»
  When Vera opens the overview of Logistics
  Then it shows MARS's «Спринт 12» with 2 items, 1 of them in the Completed category, whatever their project, and VENUS with no Active sprint
```
