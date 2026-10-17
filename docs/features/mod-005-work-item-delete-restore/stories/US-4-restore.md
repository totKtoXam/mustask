# US-4: A member restores a deleted work item

- story: US-4
- depends-on: US-1, US-3
- shippable: no — the way back from Delete (US-1); without it a deletion is one-way
- status: pending

As a **member or an administrator**, I want **to restore a deleted work item to the state it held, or to another one**, so that **a mistake costs one click and nothing about the item is lost (doc-001 BR-6, BR-10)**.

## Acceptance criteria

Data: spec § Roles, "Common data"; MSK-US-12 was in «Активна» when Ann deleted it. Rules: spec BR-DR-2, -5, -10, -11, -17; doc-001 BR-10, BR-14 (d); mod-006 § The stack rank. How the user reaches a deleted card: US-2 (the card that stayed open after Delete, the address, a live child's «Родитель»).

```gherkin
Scenario: «Восстановить» proposes the state the item held
  Given Mia has the read-only card of MSK-US-12 open
  When she chooses «Восстановить»
  Then a confirmation titled «Восстановить рабочий элемент?» reads «Рабочий элемент MSK-US-12 вернётся в списки, на доски и в бэклоги.»
  And it holds the field «Статус после восстановления», set to «Активна»
  And that field offers «Новая», «Активна», «Разрешена» and «Закрыта», the states of a Пользовательская история, never «Удалена»
  And its buttons are «Отмена» and «Восстановить», the primary not in the error colour; «Отмена», Escape and «Закрыть» change nothing

Scenario: Accepting the proposal returns the item as it was
  When Mia confirms with «Активна»
  Then MSK-US-12 is in «Активна» and the notice «Рабочий элемент MSK-US-12 восстановлен.» shows
  And the card is editable again, with the Save group in place of «Восстановить», and no longer says that it was deleted
  And «Изменён» shows the restore's time
  And the item is back in the list, on MARS's «Истории» backlog and board, in the «Активна» column, and on the project's backlog and board

Scenario: Any other state of the type may be chosen
  When Mia chooses «Новая» in the field and confirms
  Then MSK-US-12 is in «Новая» and in that column of the board, with no reason asked
  And the state it held before the deletion is forgotten

Scenario: The choice is limited to the item's own type
  Given MSK-TASK-13 («Задача») is deleted, and a Задача has no «Разрешена»
  Then its confirmation offers «Новая», «Активна» and «Закрыта» only
  And a restore sent directly with the state of another type, with «Удалена», or with something that is no state, is refused with 409 `workitems.workitem.state_not_allowed`
  And the item stays in «Удалена»

Scenario: A restore changes nothing but the state
  Given MSK-US-12 kept the team MARS, the assignee Mia, its fields and the parent MSK-FEAT-8
  When Mia restores it
  Then all of them are as they were, MSK-FEAT-8's «Изменён» time included
  And MSK-TASK-13, its child, is as it was, whatever its state

Scenario: The item returns to its owner as stored
  Given MARS still exists and owns the deleted MSK-US-12
  When Mia restores it
  Then it is on MARS's backlog and board and on the project's

Scenario: An owner deleted in the meantime leaves the item «Без команды»
  Given MARS was deleted while MSK-US-12 was deleted, and that deletion made the item «Без команды» (BR-OT-12)
  When Mia restores it
  Then it is on the project's backlog and board and on no team's

Scenario: The backlog place follows mod-006
  Given nobody reordered MARS's «Истории» backlog while MSK-US-12 was deleted
  Then after the restore it stands where it stood
  Given the backlog was reordered meanwhile
  Then after the restore it stands below every placed item

Scenario: A second deletion remembers the latest state
  Given Mia restored MSK-US-12 to «Новая» and Ann then moved it to «Разрешена» and deleted it again
  When Mia opens «Восстановить»
  Then the field is set to «Разрешена»

Scenario: Restoring something not deleted is refused
  Given Ann and Mia both restore MSK-US-12 from the same card
  When Ann's request lands first
  Then Mia's answer is 409 `workitems.workitem.not_deleted` «Рабочий элемент не удалён.» and her chosen state is not applied
  And her card reads the item and shows it live
  And a restore of MSK-BUG-7, which was never deleted, is refused the same way

Scenario: A restore and a save of one item end in one order
  Given MSK-US-12 is deleted, and at the same moment Mia restores it and Ann saves a new title from a card that still shows it live
  Then the save is refused with 409 `workitems.workitem.deleted` if it is decided first, and lands on the restored item if it is decided second
  And a restore and a purge of one item end with one of them, the other answering 409 `workitems.workitem.not_deleted` or 404 `workitems.workitem.not_found`
  And none answers with a server error

Scenario: Retention has no clock
  Given MSK-US-12 has been deleted for a year
  When Mia restores it
  Then it comes back with nothing lost

Scenario: A deleted item with no remembered state still has a way back
  Given an item is in «Удалена» although it did not get there through Delete (set directly, as feat-001's tests did)
  When Mia opens «Восстановить»
  Then the field proposes the first state of its type, «Новая»

Scenario: Who may restore
  Given Rita is a «Читатель»
  Then her card of MSK-US-12 offers no «Восстановить», and a restore sent directly answers 403 «Недостаточно прав» and changes nothing
  And Oleg, who administers «Другая», gets «Организация не найдена.»
  And Mia and Ann can restore

Scenario: A deactivated project is not reached
  Given «Платформа» is deactivated
  When Mia sends a restore for MSK-US-12
  Then the answer is `organizations.project.not_found` «Проект не найден.» and nothing changes
  And after Ann restores the project, MSK-US-12 can be restored
```
