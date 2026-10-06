# US-7: A deleted item takes no new link and frees its children

- story: US-7
- depends-on: US-2, US-4
- shippable: no — the rules guard US-1's and US-5's writes; no item can be deleted before mod-005 (OQ-1)
- status: pending

As a **signed-in user**, I want **a deleted work item to stay out of the
hierarchy without trapping its children**, so that **a child under a deleted
parent can take a new parent or none, and nothing is hung under deleted
work**.

## Acceptance criteria

Data: US-4's. No Delete exists before mod-005 (mod-003 BR-M3-21 refuses
«Удалена»), so "in «Удалена»" below means the item's state set to its type's
«Удалена» directly in the database by the test; these scenarios are
integration tests, not E2E. mod-005 re-runs them through its Delete. Spec
BR-H-4, -8, -10, -13; doc-001 BR-14; doc-002 BR-M3-9. Built only if OQ-1 is
(a).

```gherkin
Scenario: A deleted item is not offered as a parent
  Given MSK-EPIC-20 in «Удалена»
  Then neither the form's «Родитель» nor the dialog «Изменить родителя»
    offers MSK-EPIC-20, whatever is typed

Scenario: No link is written to a deleted parent
  Given MSK-EPIC-20 in «Удалена»
  When a parent write names MSK-EPIC-20 as the parent of MSK-FEAT-23
  Then it is refused with «Удалённый рабочий элемент нельзя связывать.»
  And MSK-FEAT-23 keeps its parent MSK-FEAT-8

Scenario: A deleted child takes no link write
  Given MSK-FEAT-23 in «Удалена»
  When a parent write sets, replaces or clears its parent
  Then it is refused with «Удалённый рабочий элемент нельзя связывать.»
  And its link to MSK-FEAT-8 stays

Scenario: Deleting a parent keeps its children's links
  Given MSK-EPIC-1 is put in «Удалена»
  Then MSK-FEAT-6 and MSK-FEAT-8 keep MSK-EPIC-1 as their stored parent
  And their states are unchanged

Scenario: A child of a deleted parent is unparented on the backlog
  Given MSK-EPIC-1 in «Удалена»
  When the user opens the «Фичи» backlog of «Платформа»
  Then MSK-FEAT-6's and MSK-FEAT-8's «Родитель» cells read «Нет»
  And MSK-EPIC-1 is not on the «Эпики» backlog, and they nest under no row

Scenario: The mapping replaces the link to a deleted parent
  Given MSK-EPIC-1 in «Удалена», the backlog showing MSK-FEAT-6 with
    «Родитель» «Нет»
  When the user maps MSK-FEAT-6 to MSK-EPIC-20
  Then the write is accepted although the stored parent was MSK-EPIC-1
  And MSK-FEAT-6's only parent is MSK-EPIC-20

Scenario: The child's form lets go of a deleted parent
  Given MSK-EPIC-1 in «Удалена»
  When the user opens the form of MSK-FEAT-8
  Then «Родитель» reads «MSK-EPIC-1 Вход и регистрация» with the state
    «Удалена»
  When the user picks «Нет родителя» and saves
  Then MSK-FEAT-8 has no parent
  And MSK-EPIC-1 is unchanged, its «Изменён» time included

Scenario: A deleted child is not listed on its parent's form
  Given MSK-BUG-4 in «Удалена»
  When the user opens the form of MSK-FEAT-6
  Then «Дочерние элементы (1)» lists MSK-US-2 only
```
