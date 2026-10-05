# US-5: A user changes a work item's state

- story: US-5
- depends-on: US-3
- shippable: no — the state control lives on the form (US-3)
- status: pending

As a **signed-in user**, I want **to set a work item's state to whatever
reflects reality, forwards or backwards**, so that **the project shows where
the work actually stands without a workflow arguing with me**.

## Acceptance criteria

States and their order are mod-001's seed (its api-contract § 1.4). The
state is saved with «Сохранить», like every field (spec BR-M3-21,
BR-M3-25). «Удалена» belongs to mod-005.

```gherkin
Scenario Outline: The control offers the type's states without «Удалена»
  Given a <type> in «Новая»
  When the user opens «Статус»
  Then it offers exactly <states>, in that order, each with its category dot
  And «Новая» is selected

  Examples:
    | type                        | states                                       |
    | «Задача»                    | «Новая», «Активна», «Закрыта»                |
    | «Баг»                       | «Новая», «Активна», «Разрешена», «Закрыта»   |
    | «Пользовательская история»  | «Новая», «Активна», «Разрешена», «Закрыта»   |
    | «Эпик»                      | «Новая», «Активна», «Разрешена», «Закрыта»   |
    | «Фича»                      | «Новая», «Активна», «Разрешена», «Закрыта»   |

Scenario: A forward change is saved
  Given MSK-BUG-4 in «Новая»
  When the user picks «Разрешена» and saves
  Then the form and the list show «Разрешена»

Scenario: A backward change needs no reason
  Given a «Пользовательская история» in «Закрыта»
  When the user picks «Новая» and saves
  Then the change is saved with no question asked

Scenario: A change may skip states
  Given a «Задача» in «Новая»
  When the user picks «Закрыта» and saves
  Then it is in «Закрыта»

Scenario: A state change touches no other field
  Given MSK-BUG-4 in «Активна», assigned to «Анна Смирнова», in «Платформа»
  When the user saves the state «Закрыта» only
  Then Исполнитель and Область are unchanged

Scenario Outline: A state outside the type's own is refused
  Given a «Задача» MSK-TASK-5 in «Активна»
  When a save sends <state> directly to the server
  Then it is refused with «Этот статус недоступен для рабочего элемента
    этого типа.» and the item stays in «Активна»

  Examples:
    | state                                           |
    | the «Удалена» state of «Задача»                 |
    | the «Разрешена» state of «Баг»                  |
    | the «Активна» state of another organization's «Задача» |

Scenario: Choosing the current state changes nothing
  Given a «Задача» in «Активна»
  When the user picks «Активна» again
  Then «Сохранить» stays disabled
```
