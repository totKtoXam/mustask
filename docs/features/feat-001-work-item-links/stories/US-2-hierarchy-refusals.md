# US-2: A parent that breaks the hierarchy is refused

- story: US-2
- depends-on: US-1
- shippable: no — the refusals guard the writes of US-1 and US-5
- status: pending

As a **signed-in user**, I want **a parent that would break the hierarchy
to be refused with a reason**, so that **the tree stays a tree and nobody
re-parents an item over someone else's change unawares**.

## Acceptance criteria

Data: US-1's, plus MSK-TASK-10, MSK-TASK-11 and MSK-TASK-12 in «Платформа»,
MSK-TASK-10 the parent of MSK-TASK-11, MSK-TASK-11 the parent of MSK-TASK-12;
project «Склад», key `WH`, in the same organization, with WH-EPIC-1. The
refusals apply to every parent write — the form's save (US-1) and the
backlog's menu (US-5). Spec BR-H-1, -2, -3, -4, § Error codes.

```gherkin
Scenario: A parent on a lower level is refused
  When a parent write makes MSK-TASK-3 the parent of MSK-US-2
  Then it is refused with «Родителем может быть только рабочий элемент того
    же или более высокого уровня.»
  And MSK-US-2 has no parent
  And the form's picker never offered MSK-TASK-3 for MSK-US-2

Scenario: Same level and skipped levels are allowed
  When MSK-BUG-4 is given the parent MSK-US-2, and MSK-TASK-3 the parent
    MSK-EPIC-1
  Then both links are created (doc-002 W-1)

Scenario: An item is not its own parent
  When a parent write names MSK-FEAT-6 as the parent of MSK-FEAT-6
  Then it is refused with «Рабочий элемент не может быть родителем самого
    себя.»
  And the picker on MSK-FEAT-6's form never offers MSK-FEAT-6

Scenario: A cycle is refused, naming its path
  When the user picks MSK-TASK-12 as the parent of MSK-TASK-10 and saves
  Then the save is refused, and under «Родитель» reads «Нельзя выбрать
    родителем MSK-TASK-12: он вложен в MSK-TASK-10 (MSK-TASK-10 →
    MSK-TASK-11 → MSK-TASK-12).»
  And every link stays as it was

Scenario: An item of another project is refused as not found
  When a parent write names WH-EPIC-1 as the parent of MSK-FEAT-6
  Then it is refused with «Родитель не найден в этом проекте.»
  And no item of «Склад» is ever offered by the picker on a «Мастаск» form

Scenario: A refused replace keeps the old parent
  Given MSK-US-2 has the parent MSK-FEAT-6
  When a parent write replaces it with MSK-TASK-3
  Then it is refused and MSK-US-2's parent is still MSK-FEAT-6

Scenario: A parent chosen from a stale view is refused
  Given Анна's form of MSK-US-2 loaded while it had no parent
  And Борис has since given it the parent MSK-FEAT-8
  When Анна picks MSK-FEAT-6 and saves
  Then the save is refused, and under «Родитель» reads «Родитель уже
    изменён другим участником. Данные обновлены — проверьте и повторите.»
  And «Родитель» now reads «MSK-FEAT-8 Вход через SSO», her other changes
    kept on the form
  And MSK-US-2's parent is MSK-FEAT-8

Scenario: Two users map the same child at once, the later is refused
  Given Анна and Борис both see MSK-US-2 with no parent
  When Анна gives it MSK-FEAT-6 and then Борис gives it MSK-FEAT-8
  Then MSK-US-2's parent is MSK-FEAT-6
  And Борис is refused with «Родитель уже изменён другим участником. Данные
    обновлены — проверьте и повторите.»

Scenario: Two writes that would close a cycle never both succeed
  Given MSK-TASK-3 and MSK-TASK-12 with no link between them, MSK-TASK-3
    with no parent
  When at the same moment one write makes MSK-TASK-3 the parent of
    MSK-TASK-10 and another makes MSK-TASK-12 the parent of MSK-TASK-3
  Then at most one of them is stored, and no item is its own ancestor

Scenario: A save with no answer keeps the input
  Given the server gives no answer to a form save with a parent
  Then mod-003's error `Alert` shows above the band
  And the form keeps the chosen parent, «Сохранить» enabled
```
