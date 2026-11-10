# US-6: The team sprint board

- story: US-6
- depends-on: US-3, US-4
- shippable: yes — a board over the Active sprint, usable without the project archive
- status: pending

As an **organization member**, I want **a board of my team's Active sprint in the team's «Спринты»**, so that **I see the whole sprint at one level, across projects, and move cards as on the team board**.

## Acceptance criteria

Setting as in US-4. The board is a view in the team's «Спринты» (above or beside the list); the sprint backlog and the «Задачи» taskboard are mod-011's. Source: spec BR-10, BR-9; doc-012 BR-MX-14, BR-MX-18, BR-MX-19, US-12.

```gherkin
Scenario: The board shows the Active sprint at one level, in the team's order
  Given MARS's Active «Спринт 12» holds the stories API of A («Закрыта», 5 SP), integration of B and notifications of C (both «Активна»), and the feature «Платежи» of A
  And MARS's order of «Истории» lists notifications before integration
  When Vera opens MARS's sprint board
  Then it shows «Спринт 12» at «Истории», the three cards once each with their projects, in the columns of the organization's process, notifications above integration in «Активна»
  And its totals read 3 items and 16 SP
  And the level drop-down at «Фичи» shows «Платежи» alone, 1 item

Scenario: Moving a card writes only its state
  When Vera moves integration to «Закрыта»
  Then only its state is written, in its own project, and the next read shows it there
  And the board offers no reorder

Scenario: A card move is offered only the states of the card's own type
  When Vera drags a story over the columns
  Then only the states of a story in its own project's process are drop targets

Scenario: The board counts only what the viewer sees
  Given C becomes «Ограниченный» with no grant for Vera
  When she opens MARS's sprint board
  Then it shows API and integration, 2 items and 13 SP: the same answer as if C did not exist
  And a deactivated project's items count nowhere on it

Scenario: A team with no Active sprint says so
  Given MARS's Active «Спринт 12» is closed choosing no sprint
  When Vera opens MARS's sprint board
  Then it reads «У команды нет активного спринта.» with a way to MARS's sprint list, and shows no card

Scenario: The board's next read follows a start made elsewhere
  Given Vera's board shows «У команды нет активного спринта.»
  When Pavel starts «Спринт 13» in another session, and Vera then opens the board again or moves to it from another section of MARS
  Then it shows «Спринт 13», with no reload of the page

Scenario: The sprint board creates nothing
  When Pavel opens MARS's sprint board
  Then it offers no create command, since a new item has no sprint (OQ-3), and a card opens the item's card in its own project, as on the team board

Scenario: A user who may only read moves nothing
  When a «Читатель» opens the board
  Then the cards show and cannot be moved

Scenario: A project board shows items from the sprints of several teams
  Given API of A is in MARS's Active «Спринт 12», and A's item search, owned by VENUS, is in VENUS's Active «Спринт 3»
  When Vera opens A's board
  Then both cards show, each with its owning team and its sprint
```
