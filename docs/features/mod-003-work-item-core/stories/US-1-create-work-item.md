# US-1: A user creates a work item from the project's list

- story: US-1
- depends-on: -
- shippable: no — a created item is reached only through the list (US-2) and the form (US-3)
- status: pending

As a **signed-in user**, I want **to create a work item of any of the five
types by typing only its title**, so that **capturing a piece of work costs
one sentence and the rest can be filled in later**.

## Acceptance criteria

Data: organization «Пример организации»; project «Мастаск», key `MSK`, root
area «Мастаск»; project «Операции», key `OPS`. Spec BR-M3-1, BR-M3-3,
BR-M3-5, BR-M3-20, BR-M3-26.

```gherkin
Scenario: The type picker offers the organization's five types
  Given the page «Рабочие элементы» of «Мастаск»
  When the user activates «Новый рабочий элемент»
  Then a menu lists exactly «Эпик», «Фича», «Пользовательская история»,
    «Баг», «Задача», in that order, each with its type icon
  And choosing «Задача» opens the dialog «Новый рабочий элемент» with «Тип»
    set to «Задача» and «Заголовок» focused

Scenario: A work item is created with only a title
  Given the dialog for «Задача» in «Мастаск»
  And its text reads «Рабочий элемент будет создан в статусе «Новая» в
    области «Мастаск». Остальные поля заполняются в его форме.»
  When the user types «  Настроить сборку  » and activates «Создать»
  Then the dialog closes and the notice «Рабочий элемент MSK-TASK-<n>
    создан.» is shown
  And the first row of the list's first page is that item, with Заголовок
    «Настроить сборку», Статус «Новая», Область «Мастаск», Исполнитель
    «Не назначен»
  And its form shows an empty Описание

Scenario: Each type gets its own type key and its initial state
  Given a project «Мастаск» with no work items
  When the user creates an «Эпик», a «Фича», a «Пользовательская история»,
    a «Баг» and a «Задача», in that order
  Then their identifiers are MSK-EPIC-1, MSK-FEAT-2, MSK-US-3, MSK-BUG-4 and
    MSK-TASK-5, and all five are in «Новая»
  And a «Баг» then created in «Операции», which holds no item, is OPS-BUG-1

Scenario: Two creations at the same moment get two numbers
  When two users create an item in «Мастаск» at the same moment
  Then the two identifiers carry different numbers, each greater than every
    number «Мастаск» issued before

Scenario Outline: An invalid title is refused and nothing is created
  Given the dialog for «Баг» in «Мастаск»
  When the user submits the title <title>
  Then <message> is shown under «Заголовок» and the dialog stays open
  And a create request sent directly to the server with that title is
    refused with the same text
  And the list holds no new item

  Examples:
    | title                                | message                                |
    | empty                                | «Введите заголовок.»                   |
    | three spaces                         | «Введите заголовок.»                   |
    | 256 characters after trimming        | «Не более 255 символов.»               |
    | «Сборка» + a line break + «падает»   | «Заголовок должен быть в одну строку.» |

Scenario: A title of exactly 255 characters is accepted
  When a title of 255 characters is submitted
  Then the item is created with that title

Scenario: A repeated submission creates one item
  Given the dialog filled with a valid title
  When «Создать» is activated twice in quick succession, or the same
    submission reaches the server twice
  Then exactly one new work item exists, with one identifier

Scenario: The project is deactivated while the dialog is open
  Given the dialog is open and another user deactivates «Мастаск»
  When the user activates «Создать»
  Then the dialog shows the error «Проект не найден.» and nothing is created
  And closing the dialog shows the frame «Проект не найден»
```
