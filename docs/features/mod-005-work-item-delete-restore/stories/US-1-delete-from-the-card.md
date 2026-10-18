# US-1: A member deletes a work item from its card

- story: US-1
- depends-on: -
- shippable: no — a deleted item must also be read-only (US-3) and restorable (US-4), or Delete half-removes an item and is one-way
- status: pending

As a **member or an administrator**, I want **to delete a work item I no longer need from its card**, so that **the lists and boards stay clean and nothing is lost: the item can come back (doc-001 BR-6)**.

## Acceptance criteria

Data: spec § Roles, "Common data". Rules: spec BR-DR-1..-3, -5, -8, -11, -17; doc-001 BR-6, BR-10; mod-015 design § 9.4. Delete is offered on the card in its dialog and on its full page alike (feat-029). The item is reached from the list, a backlog or a board, or by its address.

```gherkin
Scenario: «Удалить» is in the card's «⋮» and asks first
  Given Mia opens MSK-US-12, which is in «Активна» and has the child MSK-TASK-13 («Новая»)
  When she opens «Ещё действия»
  Then the menu reads «Копировать ссылку» and «Удалить»
  When she chooses «Удалить»
  Then a confirmation titled «Удалить рабочий элемент?» reads «Рабочий элемент MSK-US-12 получит статус «Удалена». Его можно будет восстановить. Дочерние элементы (1) не удаляются: пока он удалён, в бэклогах они показываются без родителя.»
  And its buttons are «Отмена» and «Удалить», the primary not in the error colour: the action is reversible
  And «Отмена», Escape and «Закрыть» change nothing

Scenario: A leaf has no sentence about children
  Given MSK-BUG-7 has no children
  When Mia chooses «Удалить» on its card
  Then the text reads «Рабочий элемент MSK-BUG-7 получит статус «Удалена». Его можно будет восстановить.»

Scenario: Confirming moves the item into «Удалена» and the card stays, read-only
  When Mia confirms the deletion of MSK-US-12
  Then its Статус is «Удалена» and the notice «Рабочий элемент MSK-US-12 удалён.» shows
  And the card stays open on the same item with the read-only look of US-3, «Восстановить» in the place of «Сохранить»
  And the card says that Mia deleted it, and when
  And «Изменён» shows the deletion's time
  And the state it held, «Активна», is remembered for Restore (US-4)

Scenario: A deletion changes nothing else on the item
  Given MSK-US-12 has the owning team MARS, the assignee Mia, the area «Платформа», priority 2 and the parent MSK-FEAT-8
  When Mia deletes it
  Then its title, description, assignee, area, team, priority, every other field and its parent are as they were
  And it is still in «Платформа» under the identifier MSK-US-12
  And MSK-FEAT-8's «Изменён» time is unchanged

Scenario: The hosts of the card show the item gone once they read again
  Given the card of MSK-US-12 is open in a dialog over MARS's «Истории» backlog
  When Mia deletes the item and closes the dialog
  Then the backlog no longer lists MSK-US-12
  And the same holds when the card was opened from the list, from a board, or by its address

Scenario: An item is deleted from any non-Removed state
  Given MSK-BUG-7 is in «Разрешена»
  When Mia deletes it
  Then it is in «Удалена» and remembers «Разрешена»
  And MSK-TASK-13 («Новая») and MSK-EPIC-1 («Активна») are deletable the same way, each into the «Удалена» of its own type

Scenario: Nobody else's work is touched
  When Ann deletes MSK-US-12
  Then OTH-US-1 and every other item of both organizations are unchanged
  And no project, team, area or member changes

Scenario: Deleting something already deleted is refused
  Given Ann deleted MSK-US-12 a moment ago and Mia's card still shows it live
  When Mia confirms «Удалить»
  Then the answer is 409 `workitems.workitem.deleted` «Рабочий элемент удалён и доступен только для чтения.»
  And the item still remembers the state Ann's deletion saw, not «Удалена»
  And Mia's card reads the item again and shows it read-only

Scenario: A save and a deletion of one item end in one order
  Given two users act on MSK-US-12 at the same moment, one saving a new title and one deleting it
  Then either the save lands first and the deletion records the state the save left, or the deletion lands first and the save is refused with 409 `workitems.workitem.deleted`
  And neither answer is a server error

Scenario: A failed delete leaves the card as it was
  Given the request gets no answer or a server error
  Then the confirmation stays open with the failure's text above the body
  And the item is unchanged and still editable
  And a 409 `…deleted` follows the scenario above, a 404 shows the card's not-found frame, and a 403 is handled as every refused write of the card is (mod-016)

Scenario: Unsaved edits do not survive a deletion
  Given Mia has typed a new title on MSK-US-12's card and not saved
  When she chooses «Удалить»
  Then the confirmation's text ends «Несохранённые изменения будут потеряны.»
  And on confirming, the new title is not saved and the read-only card shows the stored values

Scenario: One confirmation sends one request
  When Mia activates «Удалить» in the confirmation twice in a row
  Then one delete is sent, and no refusal of a second one is shown

Scenario: A reader and a non-member cannot delete
  Given Rita is a «Читатель» of «Альфа»
  When she opens MSK-US-12
  Then «Ещё действия» offers «Копировать ссылку» only
  And a delete sent directly answers 403 «Недостаточно прав» and changes nothing
  And Oleg, an administrator of «Другая», gets «Организация не найдена.» for any delete addressed to «Альфа», as does an instance administrator who is no member

Scenario: Unknown, foreign or deactivated addresses
  When a delete names MSK-US-99, or OTH-US-1 under «Платформа», or MSK-US-12 under a deactivated «Платформа»
  Then the first two answer 404 `workitems.workitem.not_found` «Рабочий элемент не найден.» alike, the third `organizations.project.not_found`
  And nothing changes, and MSK-US-12 under the restored project is as it was

Scenario: Retention has no clock
  Given MSK-US-12 has been deleted for a year (the test's clock)
  Then it is still there, deleted, unchanged since the deletion
  And the backend registers no scheduled or background job that changes work items
```
