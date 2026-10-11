# US-6: A user sees the priority on the backlog and the board

- story: US-6
- depends-on: US-5
- shippable: no — it shows the Приоритет that US-5 adds; OQ-1 (b) would make it its own line after this task
- status: pending

As a **signed-in user**, I want **to see each item's priority on the
backlog and on the board**, so that **I can weigh items without opening
each one**.

## Acceptance criteria

Spec BR-M3-39, § Backlog and board; doc-003 BR-M4-14; mod-006 BR-M4-14.
Holds only while OQ-1 keeps option (a).

> **Superseded in part** by feat-027 BR-T-1, BR-T-3 (D-7): the backlog has no
> «Приоритет» column, so the first two scenarios below ("The backlog shows a
> «Приоритет» column", "The backlog skeleton names the new column") and the
> backlog halves of the last two no longer hold; the board card's parts stand.
> Left as written.

```gherkin
Scenario: The backlog shows a «Приоритет» column
  Given a team's «Истории» backlog with MSK-US-12 of «Приоритет» 1 and
    MSK-BUG-4 of «Приоритет» 3
  When the user opens it
  Then the table has a column «Приоритет» right after «Статус»
  And MSK-US-12's row shows «1» and MSK-BUG-4's row shows «3»
  And the «Эпики» and «Фичи» backlogs show the same column

Scenario: The backlog skeleton names the new column
  When the backlog is loading
  Then the skeleton's headers include «Приоритет» after «Статус»

Scenario: A card shows the priority
  Given the team's «Истории» board with MSK-US-12 of «Приоритет» 1
  When the user opens it
  Then MSK-US-12's card shows «Приоритет: 1» on its third line, after the
    assignee

Scenario: A change on the form shows on the backlog and the board
  Given MSK-US-12 of «Приоритет» 1
  When the user changes it to «4» on the form and saves
  And then opens the backlog and the board
  Then the row shows «4» and the card «Приоритет: 4»

Scenario: Nothing else on the backlog or the board changes
  When the backlog and the board are opened
  Then the backlog's order, its other columns and the board's columns are as
    mod-006 left them
  And moving a card or a row does not change any item's «Приоритет»
```
