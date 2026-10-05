# US-2: A user sees the project's work items in a list

- story: US-2
- depends-on: US-1
- shippable: no — the list has nothing to show without creation (US-1)
- status: pending

As a **signed-in user**, I want **one list of every work item of the
project I am in, the latest changes first**, so that **I see what the
project holds and open any item from there**.

## Acceptance criteria

The list has no filter, search or sort control (spec BR-M3-29); those are
feat-003-work-item-search. Data as in US-1.

```gherkin
Scenario: The navigation leads to the chosen project's list
  Given the switcher holds «Пример организации» / «Мастаск»
  When the user activates «Рабочие элементы» in the main navigation
  Then the address is /organizations/<id>/projects/<id of «Мастаск»>/workitems
  And the page's heading and the tab read «Рабочие элементы»
  And with no project in the switcher, the entry is not shown

Scenario: A row shows the item's columns
  Given MSK-BUG-4 «Падение при входе», assigned to «Анна Смирнова», in
    «Активна», area «Платформа / API», last saved 05.10.2026 14:30
  When the list is shown
  Then its row reads «MSK-BUG-4», the «Баг» icon and «Баг», «Падение при
    входе», «Анна Смирнова», «Активна» with its category dot, «Платформа /
    API», «05.10.2026 14:30»
  And activating «Падение при входе» opens the item's form

Scenario: Only this project's items are listed
  Given «Мастаск» and «Операции» each hold items
  When the list of «Мастаск» is shown
  Then every row's identifier starts with «MSK-»

Scenario: The latest change comes first
  Given MSK-TASK-1 was saved last yesterday and MSK-TASK-2 today
  When the user saves a change to MSK-TASK-1 and returns to the list
  Then MSK-TASK-1 is the first row and MSK-TASK-2 the second
  And items saved at the same moment are ordered by number, higher first

Scenario: The list is paged by 50
  Given «Мастаск» holds 120 items
  When the list is shown
  Then the first page shows 50 rows and reads «1–50 из 120»
  And the third page shows 20 rows

Scenario: An empty project invites the first item
  Given «Операции» holds no item
  When its list is shown
  Then the frame reads «Рабочих элементов пока нет» with the hint «Создайте
    первый рабочий элемент кнопкой «Новый рабочий элемент» выше.»
  And its action «Новый рабочий элемент» opens the type picker

Scenario: A failed read can be retried
  Given the list's read fails
  Then the header «Рабочие элементы» stays and an error banner with the
    server's text and «Повторить» replaces the table
  And «Повторить» reads the list again

Scenario Outline: A list whose context is not found
  When the user opens the list address with <case>
  Then the page shows the frame <frame>

  Examples:
    | case                                                   | frame                     |
    | an unknown or malformed organization id                | «Организация не найдена»  |
    | a deactivated project                                  | «Проект не найден»        |
    | the id of a project of another organization            | «Проект не найден»        |

Scenario: An assignee the directory does not know
  Given an item whose assignee id is not in the user directory
  Then its «Исполнитель» cell reads «Неизвестный пользователь»

Scenario: A large project stays usable
  Given «Мастаск» holds 5 000 items
  When the list is opened, or another page of it is chosen
  Then the server answers within 2 s
```
