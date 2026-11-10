# US-4: An item's sprint follows the team rule

- story: US-4
- depends-on: US-1
- shippable: yes — the card's sprint field works on its own over Planned sprints
- status: pending

As a **member who may change an item**, I want **to put the item into a Planned or Active sprint of its owning team, from any project**, so that **the team's sprint holds all of the team's work, not one project's**.

## Acceptance criteria

Projects A, B and C have visibility «Организация»; team MARS is linked to A, B, C, VENUS to A. MARS owns API of A (5 SP), integration of B (8 SP) and notifications of C (3 SP). Vera is an organization «Участник» with no team grant: she may change items but not manage sprints. The field is the card's «Спринт»: it fills the iteration slot of feat-029 design § 4.8, whose label «Итерация» becomes «Спринт» (doc-012's term); "refused" means nothing is written and the refusal is shown under «Спринт». Source: spec BR-1, BR-2, BR-5; doc-012 US-12, US-4.

```gherkin
Scenario: One MARS sprint holds items of three projects
  Given MARS's «Спринт 12» is Planned
  When Vera sets the sprint of API, integration and notifications to «Спринт 12»
  Then «Спринт 12» holds the three items, each keeping its project and identifier, 16 SP in total

Scenario Outline: An item joins only a Planned or Active sprint of its own owning team
  Given API is owned by <owner>
  When Vera sets its sprint to <sprint>
  Then the save is refused with <text>, and API's sprint and other fields are unchanged
  Examples:
    | owner         | sprint                           | text                                                               |
    | MARS          | VENUS's Active «Спринт 3»        | «Спринт принадлежит другой команде.»                               |
    | «Без команды» | MARS's Planned «Спринт 12»       | «Сначала выберите команду: спринт есть только у команды.»          |
    | MARS          | MARS's Closed «Спринт 11»        | «Спринт завершён: в него нельзя добавить элемент.»                 |
    | MARS          | a sprint of another organization | «Спринт не найден.», the answer for a sprint id that names nothing |
    | MARS, API in «Удалена» | MARS's Planned «Спринт 12» | «Рабочий элемент удалён и доступен только для чтения.» (mod-005) |

Scenario: Removing a sprint or deleting its team clears a deleted item's pointer
  Given the Planned «Спринт 13» holds only a story in «Удалена»
  When Pavel removes «Спринт 13»
  Then the story has no sprint, although a user's own save of it would be refused as read-only

Scenario: An item that keeps a Closed sprint is saved like any other
  Given API is «Закрыта» and its sprint is MARS's Closed «Спринт 11»
  When Vera reopens API, changes its title and saves without touching «Спринт»
  Then the save succeeds and API's sprint still reads «Спринт 11»
  And when she sets it to MARS's Planned «Спринт 13», or to «Без спринта», it is saved; «Спринт 11» is then no longer offered for API

Scenario: The sprint picker offers only open sprints of the item's team
  Given API is owned by MARS, which has a Closed, an Active and a Planned sprint
  When Vera opens the «Спринт» field of API
  Then it offers the Active and the Planned sprint and «Без спринта», not the Closed one and not VENUS's
  And for an item with no owning team the field is empty and says to choose a team first

Scenario Outline: Changing the owning team keeps the sprint consistent
  Given API is in MARS's Active «Спринт 12», and VENUS's «Спринт 3» is Planned
  When Vera saves API with the owning team <team> and the sprint <sprint>
  Then <result>
  Examples:
    | team          | sprint             | result                                                                 |
    | VENUS         | left unchanged     | saved: API has no sprint, and «Спринт 12» falls to 11 SP               |
    | VENUS         | VENUS's «Спринт 3» | saved: API is in VENUS's «Спринт 3»                                    |
    | VENUS         | MARS's «Спринт 12» | refused with «Спринт принадлежит другой команде.»                      |
    | «Без команды» | left unchanged     | saved: API has no sprint                                               |
    | «Без команды» | MARS's «Спринт 12» | refused with «Сначала выберите команду: спринт есть только у команды.» |

Scenario: The form says before saving that a team change clears the sprint
  Given API is in MARS's «Спринт 12»
  When Vera changes the owning team to VENUS in the card without choosing a sprint
  Then the form shows that the sprint will be cleared before she saves

Scenario: Unlinking a team from a project, or deleting it, clears the pointers to its sprints
  Given MARS's Active «Спринт 12» holds API, integration and notifications, and a Closed MARS sprint's outcome lists A's item login
  When MARS is unlinked from A with VENUS as the destination
  Then API and login are owned by VENUS with no sprint; «Спринт 12» holds 11 SP, and the Closed sprint's outcome still lists login
  When the organization admin then deletes MARS
  Then integration and notifications are «Без команды» with no sprint, keeping their projects, states and story points

Scenario: Setting a sprint is an item change, not a sprint-management act
  When a viewer with the organization role «Читатель» sets API's sprint
  Then it is refused with 403, as any item change is
```
