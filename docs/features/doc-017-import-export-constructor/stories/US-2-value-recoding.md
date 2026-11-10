# US-2: An administrator recodes file values into MUSTASK values

- story: US-2
- depends-on: US-1
- shippable: no — an analysis story; delivered by `mod-NNN-import-constructor`
- status: pending

As an **organization administrator**, I want **a table that turns each value of a dictionary column into a MUSTASK value**, so that **the sheet's own names for types, states, priorities, projects and people land on the right objects**.

## Acceptance criteria

```gherkin
Scenario: The table lists every distinct value of the uploaded file
  Given «TaskType» mapped to «Тип» and a file whose «TaskType» holds UserStory, Bug and HotFix
  When the administrator opens the recoding table
  Then it lists UserStory, Bug and HotFix once each, with how many rows hold each (BR-IE-7)

Scenario: A suggestion is offered and applied only when accepted
  Given «Priority» mapped to «Приоритет» and the value «1»
  When the table is opened
  Then «1» is suggested for the file value «1»
  And the entry stays unset until the administrator accepts the suggestion (BR-IE-7)

Scenario: People are suggested by name or email
  Given «Changer» mapped to «Исполнитель» and an active member «Пользователь А» with the email «user.a@example.com»
  When the file value is «user.a@example.com» or «пользователь а»
  Then «Пользователь А» is suggested (BR-IE-7)

Scenario: Several file values share one state
  Given «Статус» mapped to «Статус» with eleven values «01. Новая заявка» … «11. Отменена»
  When the administrator maps «01. Новая заявка» and «02. Анализ» to «Новая» and «03. В работе» … «07. Ревью» to «Активна»
  Then a row with «02. Анализ» on a «Пользовательская история» gets «Новая» (BR-IE-7, OQ-4)

Scenario: A state target must belong to the row's type
  Given «08. Тестирование» mapped to «Разрешена»
  When a row of type «Задача» holds «08. Тестирование»
  Then the row is an error: «Задача» has no state «Разрешена» (BR-IE-7, BR-IE-12)

Scenario: A value missing from the table follows the table's policy
  Given a recoding table for «Исполнитель» whose policy is «Ошибка строки»
  When a later file holds «Пользователь Б», who is in no entry
  Then that row is an error naming the column, the cell and «Пользователь Б»
  When the policy is «Оставить пустым»
  Then the row is created with no assignee and a warning
  When the policy is «Значение по умолчанию» with «Пользователь А»
  Then the row gets «Пользователь А» and a warning (BR-IE-7)

Scenario: «Оставить пустым» is not offered for an always-set field
  Given a recoding table for «Тип»
  When the administrator opens its policy
  Then only «Ошибка строки» and «Значение по умолчанию» are offered (BR-IE-7)

Scenario: Values compare ignoring case and surrounding spaces
  Given an entry «bug» → «Баг»
  When a row holds « Bug »
  Then it gets «Баг» (BR-IE-7)

Scenario: Recoding points only at what exists
  Given the file value «Новый проект» in «Проект» and no such project in the organization
  When the administrator opens the table
  Then the entry offers only the organization's active projects, and no way to create one (BR-IE-7, OQ-6)

Scenario: A person who is not a member cannot be chosen
  Given a person who has never joined the organization
  When the administrator searches the people picker of the table
  Then that person is not offered (BR-IE-7; feat-053 brings them in)

Scenario: An assignee who cannot read the row's project is refused
  Given «Пользователь В» is a member who cannot read the project «Финансы»
  And an entry mapping a file value to «Пользователь В»
  When a row of «Финансы» holds that value
  Then the row is an error with the form's own reason (BR-IE-12, doc-012 BR-MX-25)

Scenario: A list column is recoded part by part
  Given «Team» split by «,» and mapped to «Участники»
  When a row holds «Пользователь А, Пользователь Б»
  Then each part is recoded on its own, and an unknown part follows the policy (BR-IE-9)
  # the «Участники» target is owed by feat-NNN-work-item-participants

Scenario: The owning team must be linked to the row's project
  Given an entry mapping a file value to the team «Команда 1»
  When a row's project has no link to «Команда 1»
  Then the row is an error «Команда не участвует в проекте.» (BR-IE-12)

Scenario: A deactivated target is refused at the dry run
  Given an entry pointing at the project «Архив», deactivated after the template was saved
  When the dry run runs
  Then every row reaching that entry is an error «Проект не найден.» (BR-IE-7)
```
