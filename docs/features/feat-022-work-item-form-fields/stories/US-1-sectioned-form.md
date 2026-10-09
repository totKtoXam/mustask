# US-1: A user works on a form laid out in sections that fold at narrow widths

- story: US-1
- depends-on: -
- shippable: no — the sections hold the fields US-2..US-5 add
- status: pending

As a **signed-in user**, I want **the work item form split into the
sections each type needs**, so that **I find a field where I expect it and
fill in several of them in one save**.

## Acceptance criteria

Spec § Screens and copy (The form's sections, Saving, States); BR-M3-30,
BR-M3-38, BR-M3-40; mod-003 BR-M3-25.

```gherkin
Scenario Outline: Each type shows its own sections
  Given a «<type>» opened in a window 1440 px wide
  Then the left column holds the sections <left>
  And the right column holds, top to bottom, <right>
  And no other section is shown, and the form has no tab bar

  Examples:
    | type                     | left                                     | right                                              |
    | Эпик                     | «Описание»                               | «Планирование», «Классификация», «Связанная работа» |
    | Фича                     | «Описание»                               | «Планирование», «Классификация», «Связанная работа» |
    | Пользовательская история | «Описание», «Критерии приёмки»           | «Планирование», «Классификация», «Связанная работа» |
    | Задача                   | «Описание»                               | «Планирование», «Трудозатраты (часы)», «Связанная работа» |
    | Баг                      | «Описание», «Шаги воспроизведения», «Сведения о системе» | «Планирование», «Трудозатраты (часы)», «Связанная работа» |

Scenario: The columns fold into one below 1280 px
  Given a «Баг» opened in a window 1280 px wide
  Then «Планирование» stands to the right of «Описание»
  When the window is narrowed to 1279 px
  Then the sections stand in one column in the order «Описание», «Шаги
    воспроизведения», «Сведения о системе», «Планирование», «Трудозатраты
    (часы)», «Связанная работа»
  And no control of «Планирование» or «Трудозатраты (часы)» is wider than
    480 px
  And Tab moves through the fields in that same order

Scenario: The form stays usable at 640 px
  Given an «Эпик» opened in a window 640 px wide
  Then every section and field is reachable and operable
  And the page has no horizontal scroll and no control overlaps another

Scenario: The band, the strip and the other screens do not change
  Given a «Задача» opened on the form
  Then the band shows «Задача <ID>», the editable title and «Сохранить»
  And the strip shows «Исполнитель», «Статус» and «Область» only
  And the dialog «Новый рабочий элемент» still asks only «Тип» and «Заголовок»
  And the project's list keeps its seven columns

Scenario: Several new fields are saved in one save
  Given a «Пользовательская история» opened on the form
  When the user sets «Оценка в пунктах» to «5», «Риск» to «2 — Средний» and
    «Критерии приёмки» to «Вход без пароля», then activates «Сохранить»
  Then the notice «Изменения сохранены.» is shown and «Сохранить» is disabled
  And reopening the form shows the three values

Scenario: A save sends only the fields the user changed
  Given users A and B opened the same «Задача» at the same time
  When A saves «Оставшаяся работа» = 4 and then B saves «Вид работ» =
    «Тестирование»
  Then the item holds «Оставшаяся работа» 4 and «Вид работ» «Тестирование»

Scenario: One refused value stores nothing
  Given a «Фича» with no «Риск»
  When a save sent directly to the server sets «Риск» to «1 — Высокий» and
    «Бизнес-ценность» to 10 000
  Then it is refused with «Допустимо значение от 0 до 9 999.» for
    «Бизнес-ценность»
  And «Риск» is unchanged

Scenario: Nothing to save
  Given a «Пользовательская история» whose «Оценка в пунктах» is 2,5
  When the user types «2,50» in it, or changes a field and changes it back
  Then «Сохранить» stays disabled

Scenario: While the save runs
  When the user activates «Сохранить» on a changed form
  Then every field, the strip and «Сохранить» are disabled until the answer
```
