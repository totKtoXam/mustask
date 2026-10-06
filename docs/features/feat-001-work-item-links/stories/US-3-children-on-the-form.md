# US-3: A user sees a work item's children on its form

- story: US-3
- depends-on: US-1
- shippable: no — the list shows the links US-1 and US-5 write (decided by OQ-3)
- status: pending

As a **signed-in user**, I want **to see on a work item's form everything
hung under it**, so that **I see what an epic or a story is made of without
going to a backlog**.

## Acceptance criteria

Data: US-1's. MSK-EPIC-1 is the parent of MSK-FEAT-6 and MSK-TASK-3;
MSK-FEAT-6 is the parent of MSK-US-2 and MSK-BUG-4. Spec BR-H-7, § Screens
and copy (section «Связанная работа»).

```gherkin
Scenario: The children are listed by level, then by number
  When the user opens the form of MSK-EPIC-1
  Then «Связанная работа» shows «Дочерние элементы (2)»
  And the list reads MSK-FEAT-6 «Вход по ссылке», then MSK-TASK-3 «Шаблон
    письма», each with its type icon and its state

Scenario: Only direct children are listed
  When the user opens the form of MSK-EPIC-1
  Then MSK-US-2 and MSK-BUG-4, the children of MSK-FEAT-6, are not listed

Scenario: A child opens from the list
  When the user activates MSK-FEAT-6 in the list on MSK-EPIC-1's form
  Then the form of MSK-FEAT-6 opens, its «Родитель» reading «MSK-EPIC-1
    Вход и регистрация»

Scenario: An item with no child says so
  When the user opens the form of MSK-US-2
  Then «Дочерние элементы (0)» and «Дочерних элементов нет.» are shown

Scenario: The list follows a link written elsewhere at the next read
  Given the form of MSK-FEAT-8 is open in one tab
  When MSK-US-2 is given the parent MSK-FEAT-8 in another tab
  Then the first tab's list is unchanged until it is reloaded or saved
  And then it reads MSK-US-2

Scenario: The list is read-only
  When the user opens the form of MSK-FEAT-6
  Then the list offers no control to add or remove a child; a child is
    re-parented on its own form or its backlog row
```
