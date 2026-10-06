# US-1: A user sets, replaces and clears a work item's parent on its form

- story: US-1
- depends-on: -
- shippable: no — the backlog tree (US-4) and the mapping (US-5) are the other half of the hierarchy the queue lines promise
- status: pending

As a **signed-in user**, I want **to hang a work item under a bigger one
from its form**, so that **an epic shows what it is made of and a story
shows what it serves**.

## Acceptance criteria

Data for every story of this task, as mod-006's: organization «Пример
организации»; project «Мастаск», key `MSK`, root area «Мастаск», first team
«Мастаск»; team «Платформа», owning its default area «Платформа», with
«Платформа / API» below it. In «Платформа»: MSK-EPIC-1 «Вход и регистрация»,
MSK-FEAT-6 «Вход по ссылке», MSK-FEAT-8 «Вход через SSO», MSK-US-2 «Письмо
со ссылкой», MSK-BUG-4 «Ссылка истекает раньше срока», MSK-TASK-3 «Шаблон
письма»; none has a parent. Users «Анна Смирнова» and «Борис Петров». Spec
BR-H-2, -3, -5, -6, § Screens and copy (section «Связанная работа»).

```gherkin
Scenario: A parent is set and saved
  Given the form of MSK-US-2, whose «Родитель» reads «Нет родителя»
  When the user opens «Родитель»
  Then «Нет родителя» is the first option, then the most recently changed
    items of the types «Эпик», «Фича», «Пользовательская история», «Баг»,
    MSK-US-2 itself not among them
  And the helper reads «Родителем может быть: Эпик, Фича, Пользовательская
    история, Баг.»
  When the user types «ссылк», picks «MSK-FEAT-6 Вход по ссылке» and saves
  Then «Изменения сохранены.» is shown and «Родитель» reads «MSK-FEAT-6 Вход
    по ссылке», with «Открыть MSK-FEAT-6» beside it
  And after a reload it reads the same

Scenario: The picker finds an item by its identifier
  When the user types «feat-8» into «Родитель» on the form of MSK-US-2
  Then «MSK-FEAT-8 Вход через SSO» is offered, with its state as secondary
    text

Scenario: Each type is offered the parents its level allows
  Then «Родитель» offers, by the item's type:
    | item       | offered types                                         |
    | MSK-EPIC-1 | «Эпик»                                                |
    | MSK-FEAT-6 | «Эпик», «Фича»                                        |
    | MSK-BUG-4  | «Эпик», «Фича», «Пользовательская история», «Баг»     |
    | MSK-TASK-3 | all five                                              |
  And a «Задача» is offered only to a «Задача»

Scenario: Choosing another parent replaces the stored one in one save
  Given MSK-US-2 has the parent MSK-FEAT-6
  When the user picks MSK-FEAT-8 on its form and saves
  Then MSK-US-2 has exactly one parent, MSK-FEAT-8
  And MSK-FEAT-6 no longer lists MSK-US-2 among its children (US-3)
  And the «Изменён» times of MSK-US-2, MSK-FEAT-6 and MSK-FEAT-8 have moved
    to the time of the save

Scenario: «Нет родителя» clears the parent
  Given MSK-US-2 has the parent MSK-FEAT-6
  When the user picks «Нет родителя» and saves
  Then «Родитель» reads «Нет родителя» and «Открыть …» is gone
  And MSK-FEAT-6 lists no child

Scenario: The parent is saved with the other fields, all or none
  Given the form of MSK-US-2 with Заголовок changed and MSK-FEAT-6 chosen as
    «Родитель»
  When the user saves
  Then both are stored by that one save
  When instead the server refuses the parent
  Then neither is stored, the refusal shows under «Родитель», and both
    inputs stay on the form

Scenario: «Сохранить» follows the field
  Given the form of MSK-US-2 just loaded
  When the user picks MSK-FEAT-6, then picks «Нет родителя» again
  Then «Сохранить» was enabled after the first and is disabled after the
    second

Scenario: A save of other fields leaves the parent alone
  Given Анна's form of MSK-US-2 loaded while it had no parent
  And Борис has since given it the parent MSK-FEAT-6
  When Анна changes only Исполнитель and saves
  Then MSK-US-2 keeps the parent MSK-FEAT-6

Scenario: A link changes no other field and no backlog place
  When MSK-US-2 is given the parent MSK-FEAT-6
  Then its Статус, Исполнитель, Область and its place on the «Истории»
    backlog of «Платформа» are unchanged

Scenario: The field is read-only while the form is saved
  Given a save in progress
  Then «Родитель» is disabled with the other fields until the answer

Scenario: The parent opens from the field
  Given MSK-US-2 has the parent MSK-FEAT-6
  When the user activates «Открыть MSK-FEAT-6»
  Then the form of MSK-FEAT-6 opens

Scenario: A failed search can be retried
  Given the server gives no answer to the picker's search
  Then «Не удалось выполнить поиск. Попробуйте ещё раз.» is shown in the list
  And typing again searches again
```
