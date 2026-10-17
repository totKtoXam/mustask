# US-5: Deleting never cascades: children, links and checklists

- story: US-5
- depends-on: US-1, US-2, US-4
- shippable: no — the hierarchy half of Delete; it re-runs feat-001's US-7 through the real Delete
- status: pending

As a **member**, I want **deleting an item to leave its children, their states and their links alone**, so that **a child is never lost with its parent, can let go of a deleted parent, and finds its parent again when that is restored (BR-M3-9)**.

## Acceptance criteria

Data: spec "Common data", plus MSK-EPIC-20 «Отчётность» («Активна») and its child MSK-FEAT-23 «Вход по коду» («Новая»). MSK-EPIC-1 is deleted by Ann unless said otherwise. Rules: spec BR-DR-4, -8, -9, § Surfaces and writes; doc-002 BR-M3-9; feat-001 BR-H-4, BR-H-8 and its US-7, whose set-up (the state written directly) is replaced by Delete here; doc-001 BR-14 (c).

```gherkin
Scenario: Deleting a parent keeps every child, with its state and its link
  Given MSK-EPIC-1 has the children MSK-FEAT-6 («Новая») and MSK-FEAT-8 («Активна»)
  When Ann deletes MSK-EPIC-1
  Then MSK-FEAT-6 and MSK-FEAT-8 are in the states they were in, on their teams' boards and backlogs
  And each keeps MSK-EPIC-1 as its stored parent, and MSK-US-2 and MSK-BUG-4 keep MSK-FEAT-6
  And no child's «Изменён» time changes

Scenario: A child of a deleted parent is unparented in the backlogs
  When Mia opens the project's «Фичи» backlog
  Then MSK-FEAT-6 and MSK-FEAT-8 are top-level rows of their own level, beneath no row
  And the «Эпики» backlog does not list MSK-EPIC-1, so nothing expands to them

Scenario: The child's form shows the deleted parent and lets go of it
  When Mia opens MSK-FEAT-8
  Then «Родитель» reads «MSK-EPIC-1 Вход и регистрация» with the state «Удалена»
  When she picks «Нет родителя» and saves
  Then MSK-FEAT-8 has no parent
  And MSK-EPIC-1 is unchanged, its «Изменён» time included, and the deletion is not touched

Scenario: The backlog's mapping replaces the link to a deleted parent
  Given the backlog shows MSK-FEAT-6 beneath no row
  When Mia maps MSK-FEAT-6 to MSK-EPIC-20 with «Изменить родителя…»
  Then the write is accepted although the stored parent is the deleted MSK-EPIC-1
  And MSK-FEAT-6's only parent is MSK-EPIC-20, and MSK-EPIC-1 is unchanged

Scenario: A deleted item is not offered as a parent
  Then neither the form's «Родитель» nor the dialog «Изменить родителя» offers MSK-EPIC-1, whatever is typed

Scenario: No link is written to or from a deleted item
  When a parent write names MSK-EPIC-1 as the parent of MSK-FEAT-23, or sets any parent on the deleted MSK-US-12
  Then it is refused with 409 `workitems.link.item_deleted` «Удалённый рабочий элемент нельзя связывать.»
  And MSK-FEAT-23 keeps its parent, and a child created under MSK-EPIC-1 is refused the same way

Scenario: A deleted child leaves its parent's form, checklist and counter
  Given MSK-BUG-4 is deleted
  When Mia opens MSK-FEAT-6
  Then «Дочерние элементы (1)» lists MSK-US-2 only
  And MSK-FEAT-6's «Изменён» time and links are as they were

Scenario: Restoring the parent brings its kept links back
  Given Ann deleted MSK-EPIC-1, MSK-FEAT-8 released its link meanwhile, and MSK-FEAT-6 did not
  When Mia restores MSK-EPIC-1 (US-4)
  Then MSK-FEAT-6 is again a child of MSK-EPIC-1 and nests beneath it in the project's «Эпики» backlog
  And MSK-FEAT-8 stays without a parent: nothing restores a link that was released

Scenario: Restoring a child does not restore its parent
  Given MSK-EPIC-1 and MSK-FEAT-6 are both deleted
  When Mia restores MSK-FEAT-6
  Then it is live again, still has MSK-EPIC-1 as its stored parent, and counts as unparented while MSK-EPIC-1 stays deleted
  And MSK-EPIC-1 is still deleted

Scenario: Deleting a chain deletes nothing below it
  When Ann deletes MSK-EPIC-1 and then MSK-FEAT-6
  Then MSK-US-2 and MSK-BUG-4 are live, beneath no row, and keep MSK-FEAT-6 as their stored parent
  And each can be restored on its own in any order, with no check of the others

Scenario: A deletion leaves the item's own parent as it was
  Given MSK-TASK-13 is a child of the live MSK-US-12
  When Mia deletes MSK-TASK-13
  Then MSK-US-12's «Изменён» time, state and other children are unchanged
  And the checklist of MSK-US-12's board card counts one item fewer
```
