# US-2: A user plans and classifies epics, features and stories

- story: US-2
- depends-on: US-1
- shippable: no — its fields live in US-1's sections
- status: pending

As a **signed-in user**, I want **to record an epic's, a feature's and a
story's size, risk, value and dates**, so that **the backlog can be weighed
and ordered by more than its titles**.

## Acceptance criteria

Spec § The field table; BR-M3-31, BR-M3-32, BR-M3-33, BR-M3-35; § Controls
and copy. Приоритет is US-5's.

```gherkin
Scenario: An epic's planning and classification are saved
  Given an «Эпик» opened on the form
  When the user sets «Риск» «1 — Высокий», «Трудоёмкость» «13,5»,
    «Бизнес-ценность» «40», «Критичность по времени» «8», «Дата начала»
    01.11.2026, «Целевая дата» 31.03.2027 and «Вид ценности» «Архитектура»,
    then saves
  Then reopening the form shows exactly those values, «Трудоёмкость» as «13,5»
  And a «Фича» offers and keeps the same fields the same way

Scenario: A story's planning and classification are saved
  Given a «Пользовательская история» opened on the form
  When the user sets «Оценка в пунктах» «3», «Риск» «3 — Низкий» and «Вид
    ценности» «Бизнес», then saves
  Then reopening the form shows those values
  And the story shows no «Трудоёмкость», «Бизнес-ценность», «Критичность по
    времени», «Дата начала» or «Целевая дата»

Scenario: Risk can be emptied, the value area cannot
  Given a «Фича» with «Риск» «2 — Средний»
  When the user opens «Риск»
  Then the options are «Не указан», «1 — Высокий», «2 — Средний», «3 — Низкий»
  And choosing «Не указан» and saving leaves «Риск» empty
  And «Вид ценности» offers only «Бизнес» and «Архитектура»

Scenario: A number field may be emptied, and 0 is kept as 0
  Given an «Эпик» with «Трудоёмкость» 5
  When the user clears «Трудоёмкость» and saves
  Then the field is empty with the placeholder «Не оценено»
  And when the user saves «0» instead, the field shows «0»

Scenario Outline: An invalid number is refused, not rounded
  Given an «Эпик» opened on the form
  When the user types <text> in <field> and activates «Сохранить»
  Then <message> is shown under <field> and nothing is sent
  And a save sent directly to the server with <value> is refused with the
    same text, the item unchanged

  Examples:
    | field                    | text    | value  | message                                |
    | «Трудоёмкость»           | «-1»    | -1     | «Допустимо значение от 0 до 9 999.»    |
    | «Критичность по времени» | «10000» | 10000  | «Допустимо значение от 0 до 9 999.»    |
    | «Трудоёмкость»           | «2,555» | 2.555  | «Не более двух знаков после запятой.»  |
    | «Бизнес-ценность»        | «2,5»   | 2.5    | «Введите целое число.»                 |

Scenario: Text that is not a number never leaves the browser
  When the user types «пять» in «Трудоёмкость» and activates «Сохранить»
  Then «Введите число.» is shown under it and nothing is sent
  And the message clears as soon as the field is edited

Scenario: The target date may not precede the start date
  Given an «Эпик» with «Целевая дата» 31.03.2027 and no «Дата начала»
  When the user sets «Дата начала» 01.04.2027 and saves
  Then «Целевая дата не может быть раньше даты начала.» is shown under
    «Целевая дата» and nothing is stored
  And setting «Дата начала» 01.04.2027 together with «Целевая дата»
    30.06.2027 in one save is accepted
  And either date alone may be set or cleared

Scenario: A date outside 2000–2099 is refused
  When a save sent directly to the server sets «Дата начала» of an «Эпик»
    to 31.12.1999
  Then it is refused with «Укажите дату с 2000 по 2099 год.»
```
