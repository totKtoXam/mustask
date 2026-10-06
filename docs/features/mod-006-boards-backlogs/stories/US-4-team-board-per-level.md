# US-4: A user sees a team's board per level

- story: US-4
- depends-on: US-1
- shippable: no — the board shows the backlog's items in the backlog's order (US-1)
- status: pending

As a **signed-in user**, I want **to see my team's work items of one level
as cards in columns by state**, so that **I see where the work stands
without opening every item**.

## Acceptance criteria

Data: US-1's. Spec § Model (the board's columns), BR-M4-1, -2, -14, -15.

```gherkin
Scenario Outline: Each level's board has its own columns
  When the user opens the <level> board of «Платформа»
  Then its columns are <columns>, in that order, each header with its count

  Examples:
    | level     | columns                                                          |
    | «Эпики»   | «Новые», «В работе», «Разрешена», «Завершённые»                  |
    | «Фичи»    | «Новые», «В работе», «Разрешена», «Завершённые»                  |
    | «Истории» | «Новые», «В работе», «Разрешена», «Решённые», «Завершённые»      |

Scenario Outline: A card sits in the column its type and state map to
  Given a <type> of «Платформа» in <state>
  Then its card is in <column> of its level's board

  Examples:
    | type                        | state       | column         |
    | «Пользовательская история»  | «Новая»     | «Новые»        |
    | «Баг»                       | «Активна»   | «В работе»     |
    | «Пользовательская история»  | «Разрешена» | «Разрешена»    |
    | «Баг»                       | «Разрешена» | «Решённые»     |
    | «Баг»                       | «Закрыта»   | «Завершённые»  |
    | «Эпик»                      | «Разрешена» | «Разрешена»    |

Scenario: The board holds the backlog's items in the backlog's order
  Given the «Истории» backlog of «Платформа» reads MSK-US-3, MSK-BUG-5,
    MSK-US-9, all in «Новая», and MSK-US-5 in «Мастаск»
  When the user opens the «Истории» board of «Платформа»
  Then «Новые» shows MSK-US-3, MSK-BUG-5, MSK-US-9 from top to bottom, count 3
  And no column shows MSK-US-5
  When MSK-US-9 is moved to the top of the backlog and the board is read again
  Then «Новые» shows MSK-US-9 first

Scenario: A card shows the item at a glance and opens it
  Given MSK-BUG-5 «Неверный счётчик» in «Активна», assigned to «Анна Смирнова»
  Then its card shows the «Баг» stripe and icon, «MSK-BUG-5», the title as a
    link, the category dot with «Активна», the initials «АС» with «Анна
    Смирнова», and the button «Ещё действия: MSK-BUG-5»
  When the user activates the title
  Then the form opens at `/organizations/<o>/projects/<p>/workitems/MSK-BUG-5`

Scenario: A column with no cards stays on the board
  Given the «Истории» items of «Платформа» are all in «Новая»
  Then all five columns are shown, the other four with the count 0

Scenario: A level with no items shows the empty frame
  Given «Платформа» has no «Эпик»
  When the user opens its «Эпики» board
  Then the frame «На доске нет рабочих элементов», hint «Новые рабочие
    элементы этого уровня появятся в колонке «Новые».», action «Новый
    рабочий элемент» replaces the columns, header and tabs kept

Scenario: Entry, tabs and the link to the backlog
  When the user activates «Доски» in the main navigation, context «Платформа»
  Then `<team>/boards/stories` opens, `h1` «Доски», tab «Истории» selected
  And the tabs are «Эпики», «Фичи», «Истории», and `<team>/boards/tasks`
    answers «Страница не найдена»
  When the user selects «Фичи» and activates «Показать бэклог»
  Then `<team>/backlogs/features` opens, whose «Показать доску» opens
    `<team>/boards/features`

Scenario: Failure and not found are the backlog's
  Given the board read fails
  Then the server's text and «Повторить» replace the columns
  And each address of US-1's not-found outline, with `boards` for
    `backlogs`, shows the same frame
```
