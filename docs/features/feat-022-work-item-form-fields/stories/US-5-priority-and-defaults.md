# US-5: Every item has a priority and the defaults, new and existing alike

- story: US-5
- depends-on: US-1
- shippable: no — Приоритет sits in US-1's «Планирование»
- status: pending

As a **signed-in user**, I want **every work item to carry a priority and
sensible starting values**, so that **a new item and one created before
this change look and behave the same**.

## Acceptance criteria

Spec § The field table; BR-M3-30, BR-M3-31, BR-M3-37, BR-M3-40.

```gherkin
Scenario: Every type has Приоритет 1..4
  Given one work item of each of the five types
  When each form is opened
  Then «Планирование» shows «Приоритет» with the options «1», «2», «3», «4»,
    no empty choice and the helper «1 — самый высокий»
  And choosing «1» and saving shows «1» on reopening

Scenario Outline: A new item takes its type's defaults
  When the user creates a «<type>» from the dialog with only a title
  Then its form shows <defaults>
  And every other field of this task is empty

  Examples:
    | type                     | defaults                                        |
    | Эпик                     | «Приоритет» «2», «Вид ценности» «Бизнес»        |
    | Фича                     | «Приоритет» «2», «Вид ценности» «Бизнес»        |
    | Пользовательская история | «Приоритет» «2», «Вид ценности» «Бизнес»        |
    | Задача                   | «Приоритет» «2»                                 |
    | Баг                      | «Приоритет» «2», «Серьёзность» «3 — Средняя»    |

Scenario: An item created before this change gets the same defaults
  Given a «Баг» MSK-BUG-4 and a «Фича» MSK-FEAT-5 created before this task
    shipped, with their «Изменён» times noted and the list's order noted
  When the forms are opened after it shipped
  Then MSK-BUG-4 shows «Приоритет» «2» and «Серьёзность» «3 — Средняя»
  And MSK-FEAT-5 shows «Приоритет» «2» and «Вид ценности» «Бизнес»
  And every other new field of both is empty
  And both «Изменён» times and the list's order are unchanged

Scenario: Приоритет cannot be emptied
  When a save sent directly to the server empties «Приоритет» of a «Задача»
    whose priority is «4»
  Then the task's «Приоритет» is «2»

Scenario: A priority outside 1..4 is refused
  When a save sent directly to the server sets «Приоритет» to 0 or to 5
  Then it is refused with «Выберите значение из списка.», the item unchanged

Scenario Outline: A field of another type is refused
  When a save sent directly to the server sets <field> on a «<type>»
  Then it is refused with «Это поле недоступно для рабочего элемента этого
    типа.» and the item is unchanged

  Examples:
    | type                     | field                   |
    | Задача                   | «Оценка в пунктах»      |
    | Пользовательская история | «Исходная оценка»       |
    | Баг                      | «Вид ценности»          |
    | Эпик                     | «Серьёзность»           |
    | Задача                   | «Дата начала»           |

Scenario: The list and the create dialog do not change
  When the project's list and the dialog «Новый рабочий элемент» are opened
  Then the list shows «ID», «Тип», «Заголовок», «Исполнитель», «Статус»,
    «Область», «Изменён» and no other column
  And the dialog asks only «Тип» and «Заголовок»
```
