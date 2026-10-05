# US-6: An area that holds work items cannot be deleted

- story: US-6
- depends-on: US-1, US-4
- shippable: no — it adds a condition to mod-002's deletion and needs items in areas (US-1, US-4)
- status: pending

As a **signed-in user who maintains the area tree**, I want **an area to be
deletable only once no work item is in it**, so that **no work item is ever
left pointing at an area that no longer exists**.

## Acceptance criteria

mod-002 already refuses to delete an area that has children or is a team's
default area. This story adds work items to that check, with mod-002's
existing code `organizations.area.not_empty` and text (spec BR-M3-24).
«Отчёты» below is a leaf of «Мастаск» and no team's default area.

```gherkin
Scenario: An area holding a work item is not deleted
  Given MSK-TASK-5 has Область «Отчёты»
  When the user deletes «Отчёты» on the page «Области» and confirms
  Then the dialog «Удаление области» shows «Нельзя удалить область, в
    которой есть рабочие элементы (включая удалённые) или вложенные
    области» and stays open
  And «Отчёты» is still in the tree and MSK-TASK-5 still has it

Scenario: Every item counts, whatever its state
  Given the only item in «Отчёты» is MSK-TASK-5 in «Закрыта»
  When «Отчёты» is deleted
  Then the deletion is refused as above

Scenario: The area is deleted once its items are moved out
  Given MSK-TASK-5 was the only item in «Отчёты»
  And the user moved it to «Платформа» and saved
  When «Отчёты» is deleted and confirmed
  Then «Отчёты» leaves the tree with the notice «Область «Отчёты»
    удалена.»

Scenario: Items of other areas do not block a deletion
  Given «Отчёты» holds no item and «Платформа» holds several
  When «Отчёты» is deleted and confirmed
  Then the deletion succeeds

Scenario: A deletion and an item entering the area at the same moment
  Given «Отчёты» holds no item and MSK-TASK-5 is in «Платформа»
  When one user deletes «Отчёты» while another saves MSK-TASK-5 with
    Область «Отчёты», both at the same moment, in either order
  Then exactly one of the two succeeds
  And if the deletion succeeded, the save is refused with «Область не
    найдена.» and MSK-TASK-5 is still in «Платформа»
  And if the save succeeded, the deletion is refused with the text above
```

A creation cannot race a deletion in this task: the list's dialog always
creates in the root area, which is never deleted (spec BR-M3-20; mod-002
BR-M1-11).
