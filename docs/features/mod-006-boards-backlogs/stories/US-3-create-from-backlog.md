# US-3: A user creates a work item from a team's backlog

- story: US-3
- depends-on: US-1
- shippable: no — creation lands on the backlog of US-1
- status: pending

As a **signed-in user**, I want **to create a work item right on my team's
backlog**, so that **it lands in my team's area and at the end of our order
without me picking an area**.

## Acceptance criteria

Data: US-1's. The dialog is mod-003's «Новый рабочий элемент»; its title,
validation and one-item-per-submission rules are mod-003's. Spec BR-M4-6,
-21; doc-010 D-7.

```gherkin
Scenario: The primary offers only the level's types
  When the user activates «Новый рабочий элемент» on the «Истории» backlog
  Then a menu lists «Пользовательская история» and «Баг», each with its icon
  When the user activates it on the «Эпики» backlog
  Then the dialog opens at once with «Тип» «Эпик», which offers no other type
  And on the «Фичи» backlog likewise with «Фича»

Scenario: The item takes the team's default area and lands last
  Given the «Истории» backlog of «Платформа», reordered, reads MSK-US-3,
    MSK-US-9
  When the user chooses «Пользовательская история»
  Then the dialog reads «Рабочий элемент будет создан в статусе «Новая» в
    области «Платформа» и встанет в конец бэклога команды «Платформа».
    Остальные поля заполняются в его форме.»
  When the user types «Экспорт отчёта» and activates «Создать»
  Then the dialog closes and «Рабочий элемент MSK-US-<n> создан.» is shown
  And the backlog reads MSK-US-3, MSK-US-9, MSK-US-<n>
  And MSK-US-<n> has Область «Платформа», Статус «Новая», «Не назначен»
  And it is on no backlog of «Мастаск»

Scenario: The first team's default area is the root
  When the user creates a «Фича» with only a title on the «Фичи» backlog
    of «Мастаск»
  Then its Область is «Мастаск» and it is the last row of that backlog

Scenario: The default area is read when the item is created
  Given MSK-US-3 and MSK-US-9 in «Платформа», on its «Истории» backlog
  And the dialog open there
  And another user then gives «Платформа / API» the owner «Платформа» and
    makes it the default area of «Платформа»
  When the user creates the item
  Then its Область is «Платформа / API», and it is the last row of the
    «Истории» backlog of «Платформа»
  And MSK-US-3 and MSK-US-9 keep «Платформа»

Scenario: Creation rules stay mod-003's
  Given the dialog on a team's backlog
  When the user activates «Создать» with an empty «Заголовок»
  Then «Введите заголовок.» is shown under «Заголовок», the dialog stays open
    and nothing is created
  When the user types a title and activates «Создать» twice quickly
  Then exactly one item is created

Scenario: The team was deleted while the dialog was open
  Given the dialog open on the «Истории» backlog of «Платформа»
  And another user then deletes «Платформа»
  When the user activates «Создать»
  Then the dialog shows «Команда не найдена.» and nothing is created
  When the user closes the dialog
  Then the page shows the frame «Команда не найдена»

Scenario: Creating from the empty backlog
  Given the «Истории» backlog of «Аналитика», which is empty
  When the user activates «Новый рабочий элемент» in the empty frame
  Then the same type menu opens
  And after a «Баг» is created the frame gives way to the table with one row
```
