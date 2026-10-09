# US-3: A user estimates tasks and bugs and grades a bug's severity

- story: US-3
- depends-on: US-1
- shippable: no — its fields live in US-1's sections
- status: pending

As a **signed-in user**, I want **to record how many hours a task or a bug
takes, how much is left and done, what kind of work it is, and how serious
a bug is**, so that **the team sees the remaining work without a side
spreadsheet**.

## Acceptance criteria

Spec § The field table; BR-M3-31..BR-M3-34; § Controls and copy.

```gherkin
Scenario: A task's estimates are saved in hours
  Given a «Задача» opened on the form
  When the user sets «Вид работ» «Разработка», «Исходная оценка» «8»,
    «Оставшаяся работа» «5,5» and «Выполненная работа» «2.5», then saves
  Then reopening the form shows «Разработка», «8», «5,5» and «2,5»
  And the section reads «Трудозатраты (часы)»
  And the task shows no «Оценка в пунктах» and no «Серьёзность»

Scenario: A bug carries points, severity, activity and hours
  Given a «Баг» opened on the form
  Then «Планирование» holds «Оценка в пунктах», «Приоритет», «Серьёзность»
    and «Вид работ», in that order
  And «Трудозатраты (часы)» holds «Исходная оценка», «Оставшаяся работа» and
    «Выполненная работа»
  When the user sets «Оценка в пунктах» «1», «Серьёзность» «1 — Критическая»
    and «Оставшаяся работа» «3», then saves
  Then reopening the form shows those values

Scenario: The lists offer their values in order
  Given a «Баг» opened on the form
  Then «Серьёзность» offers «1 — Критическая», «2 — Высокая», «3 — Средняя»,
    «4 — Низкая» and no empty choice
  And «Вид работ» offers «Не указан», «Развёртывание», «Проектирование»,
    «Разработка», «Документирование», «Требования», «Тестирование»

Scenario: Hours are refused beyond two decimals, never rounded
  Given a «Задача» opened on the form
  When the user types «1,333» in «Исходная оценка» and activates «Сохранить»
  Then «Не более двух знаков после запятой.» is shown and nothing is sent
  And a save sent directly to the server with 1.333 is refused with the same
    text, the stored value unchanged

Scenario: An empty estimate is not zero
  Given a new «Задача»
  Then «Оставшаяся работа» is empty with the placeholder «Не оценено»
  When the user saves «0» in it
  Then the form shows «0», and clearing it again leaves it empty

Scenario: Closing a task leaves its remaining work as entered
  Given a «Задача» with «Оставшаяся работа» 3
  When the user changes «Статус» to «Закрыта» and saves
  Then «Оставшаяся работа» is still 3

Scenario: No field changes another
  Given a «Баг» with «Исходная оценка» 8, «Оставшаяся работа» 5 and
    «Выполненная работа» 3
  When the user changes «Выполненная работа» to 6 and saves
  Then «Исходная оценка» is 8 and «Оставшаяся работа» is 5

Scenario: Severity cannot be emptied
  When a save sent directly to the server empties «Серьёзность» of a «Баг»
    whose severity is «1 — Критическая»
  Then the bug's «Серьёзность» is «3 — Средняя»

Scenario: A value outside the list is refused
  When a save sent directly to the server sets «Вид работ» of a «Задача» to
    a key the list does not hold
  Then it is refused with «Выберите значение из списка.», the item unchanged
```
