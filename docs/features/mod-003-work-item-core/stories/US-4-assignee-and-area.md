# US-4: A user assigns a work item and places it in an area

- story: US-4
- depends-on: US-3
- shippable: no — the pickers live on the form (US-3)
- status: pending

As a **signed-in user**, I want **to name who does a work item and where in
the project it belongs**, so that **everyone sees its owner and the team
whose area it falls in**.

## Acceptance criteria

Directory: «Анна Смирнова» (anna.smirnova@example.com) and «Анна Кузнецова»,
both active; «Пётр Иванов», inactive. Areas of «Мастаск»: «Мастаск» (root),
«Платформа», «Платформа / API», «Отчёты»; «Операции» has «Склад». Spec
BR-M3-22, BR-M3-23.

```gherkin
Scenario: An assignee is found by name and saved
  Given the form of MSK-BUG-4 with Исполнитель «Не назначен»
  When the user types «анна» into «Исполнитель»
  Then «Не назначен», «Анна Кузнецова» and «Анна Смирнова» are offered, each
    user with initials and e-mail
  When the user picks «Анна Смирнова» and saves
  Then the form and the list show «Анна Смирнова» as Исполнитель

Scenario: A user is found by e-mail
  When the user types «smirnova@» into «Исполнитель»
  Then «Анна Смирнова» is offered

Scenario Outline: The picker's hints
  When the user types <text> into «Исполнитель»
  Then the list shows <shown>

  Examples:
    | text                                  | shown                                                        |
    | «а»                                   | «Не назначен» and «Введите не менее 2 символов имени или почты» |
    | «Ёжиков» (nobody matches)             | «Не назначен» and «Никого не найдено»                        |
    | a text 25 active users match          | «Не назначен», 20 users, «Показаны первые 20. Уточните запрос.» |

Scenario: An inactive user cannot be newly assigned
  When the user types «Пётр» into «Исполнитель»
  Then «Пётр Иванов» is not offered
  And a save that sets him directly at the server is refused with
    «Этого пользователя нельзя назначить исполнителем.» and nothing changes

Scenario: An assignee whose account is disabled later stays
  Given MSK-BUG-4 is assigned to «Анна Смирнова»
  And her account is then disabled
  When the user opens the form, changes the title and saves
  Then the save succeeds and Исполнитель still reads «Анна Смирнова»
  And neither the form nor the list adds any mark to her name

Scenario: An assignment is cleared
  When the user picks «Не назначен» and saves
  Then the form and the list show «Не назначен»

Scenario: The area picker offers the project's whole tree
  When the user opens «Область» on an item of «Мастаск»
  Then it offers «Мастаск», «Отчёты», «Платформа», «Платформа / API» in
    tree order, the root first
  And «Склад» of «Операции» is not offered

Scenario: An area is chosen and saved
  When the user picks «Платформа / API» and saves
  Then the form and the list show «Платформа / API» as Область

Scenario: An area of another project is refused
  When a save sends the area «Склад» of «Операции» for MSK-BUG-4 directly
    to the server
  Then it is refused with «Область не найдена.» and Область is unchanged

Scenario: The item follows its area's changes
  Given MSK-BUG-4 is in «Платформа / API»
  When an administrator renames «API» to «Шлюз», then moves it under
    «Отчёты»
  Then the item's Область reads «Отчёты / Шлюз» in the form and the list
  And deleting the team that owns that area leaves the item's Область as is
```
