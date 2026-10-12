# US-6: A user creates a work item from a team's board

- story: US-6
- depends-on: US-3, US-4
- shippable: no — the board's creation reuses US-3's and shows on US-4's board
- status: pending

As a **signed-in user**, I want **to create a work item right on my team's
board**, so that **it appears in «Новые» in my team's area without leaving
the board**.

## Acceptance criteria

Data: US-1's. The dialog and its rules are US-3's. Spec BR-M4-15, -21.

> **Superseded in part by bug-004 D-3 (2026-10-12), one name, one meaning:**
> the board's columns are headed by the names of the states they map, so the
> first column of the seeded board is «Новая», not «Новые». Read every «Новые»
> in this story — the persona line and the scenarios' titles and steps — as
> «Новая». «Новый элемент» still sits at the top of the first column, and a new
> card is still the last one in it. Everything else stands. The text is left as
> written (spec § Model's note, US-4's note).

```gherkin
Scenario: «Новый элемент» sits at the top of the first column
  When the user opens the «Истории» board of «Платформа»
  Then «Новый элемент» is the first control in «Новые»
  When the user activates it
  Then a menu lists «Пользовательская история» and «Баг»
  And on the «Фичи» board it opens the dialog at once with «Тип» «Фича»

Scenario: The new card is the last of «Новые», in the team's area
  Given the «Фичи» board of «Платформа» with MSK-FEAT-6 in «Новые»
  When the user creates a «Фича» «Отчёты по командам» from «Новый элемент»
  Then «Рабочий элемент MSK-FEAT-<n> создан.» is shown
  And «Новые» shows MSK-FEAT-6, then MSK-FEAT-<n>
  And MSK-FEAT-<n> has Область «Платформа» and is the last row of the
    «Фичи» backlog of «Платформа»

Scenario: On the first team's board the area is the root
  When the user creates a «Баг» on the «Истории» board of «Мастаск»
  Then its Область is «Мастаск» and its card is the last in «Новые»

Scenario: Creating from the empty board
  Given the «Эпики» board of «Аналитика», showing the empty frame
  When the user activates «Новый рабочий элемент» in the frame
  Then the dialog opens with «Тип» «Эпик»
  When the user creates «Миграция данных»
  Then the frame gives way to the four columns, with its card in «Новые»

Scenario: The team was deleted while the dialog was open
  Given the dialog open on the «Истории» board of «Платформа»
  And another user then deletes «Платформа»
  When the user activates «Создать»
  Then the dialog shows «Команда не найдена.» and nothing is created
  When the user closes the dialog
  Then the page shows the frame «Команда не найдена»
```
