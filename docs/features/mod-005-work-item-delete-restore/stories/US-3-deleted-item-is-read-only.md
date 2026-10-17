# US-3: A deleted work item takes no write

- story: US-3
- depends-on: US-1
- shippable: no — the writes half of Delete (US-1); without it a stale card or a save would silently bring a deleted item back
- status: pending

As a **member**, I want **a deleted work item to be read-only until it is restored**, so that **nothing changes it behind the deletion, and no save or card move brings it back without Restore (doc-001 BR-10, BR-14 c)**.

## Acceptance criteria

Data: spec § Roles, "Common data"; MSK-US-12 («Активна», MARS) is deleted by Ann unless said otherwise. Rules: spec BR-DR-7, -8, -9, § Surfaces and writes, § Error codes; feat-029 BR-C-19; doc-001 BR-10, BR-14 (c); tech-012 m1.

```gherkin
Scenario: The card of a deleted item is read-only
  When Mia opens MSK-US-12
  Then every field reads as a value with no frame, and the state reads «Удалена»
  And the Save group and «Отменить все изменения» are not drawn; «Обновить» and «Копировать ссылку» stay
  And an information notice reads «Рабочий элемент удалён и доступен только для чтения. Восстановите его, чтобы вносить изменения.»
  And «Восстановить» stands where «Сохранить» stood, and the card says that Ann deleted it, and when
  And the state, assignee, team, parent and acceptance criteria offer no choice

Scenario: A reader sees the same card without the way back
  When Rita opens MSK-US-12
  Then the notice reads «Рабочий элемент удалён и доступен только для чтения.» and no «Восстановить» is drawn

Scenario: A save of any field is refused, and nothing changes
  Given Mia's request to save MSK-US-12 names a new title, or an assignee, or a team, or a field of its type
  Then the answer is 409 `workitems.workitem.deleted` «Рабочий элемент удалён и доступен только для чтения.»
  And the item, its «Изменён» time included, is as it was

Scenario: The refusal comes before the value checks
  Given the request names a title of 300 characters
  Then the answer is the same 409, not a 400 about the title
  And for an unknown identifier it is `workitems.workitem.not_found`, before the 409

Scenario: A live state sent together with a parent does not slip past (tech-012 m1)
  Given MSK-US-12 is deleted and MSK-FEAT-6 is a live Фича
  When a save names the state «Новая» and the parent MSK-FEAT-6 in one request
  Then the answer is 409 `workitems.workitem.deleted`
  And MSK-US-12 is still in «Удалена» with its previous parent, and MSK-FEAT-6 has no new child and its «Изменён» is unchanged

Scenario: No state write brings the item back without Restore (BR-10)
  When a save names the state «Новая» or «Активна» for MSK-US-12 alone
  Then the answer is the same 409 and the item stays in «Удалена»

Scenario: A stale board card of a deleted item cannot be moved
  Given Ann's «Истории» board on MARS still shows MSK-US-12's card while Mia has deleted the item
  When Ann drags the card to the column «Разрешена»
  Then the move is refused with 409 `workitems.workitem.deleted`, the board shows «Рабочий элемент удалён и доступен только для чтения.» and reads again
  And the item stays in «Удалена» and the card is gone from the board

Scenario: A stale checklist row cannot be ticked
  Given MSK-US-12's card shows MSK-TASK-13 in its checklist while another user has deleted MSK-TASK-13
  When Ann ticks it
  Then the tick is refused with 409 `workitems.workitem.deleted` and the board shows the server's text
  And MSK-TASK-13 stays in «Удалена»

Scenario: A stale reorder naming a deleted item is refused
  Given a backlog page still shows MSK-US-12 as a row, and Mia has deleted it
  When Ann moves it, or moves another row after it
  Then the answer is 409 `boards.backlog.reorder_stale` and the order does not change

Scenario: Link writes keep their own refusal (feat-001 BR-H-8, re-run)
  When the parent route («Изменить родителя…», «Убрать родителя») or MSK-FEAT-8's own form save sets MSK-FEAT-8 under the deleted MSK-EPIC-1, or the parent route sets, replaces or clears the parent of the deleted MSK-US-12, or a child is created under a deleted parent
  Then each is refused with 409 `workitems.link.item_deleted` «Удалённый рабочий элемент нельзя связывать.»
  And a form save of the deleted MSK-US-12 that names a parent answers `workitems.workitem.deleted` instead (BR-DR-8)
  And no link and no «Изменён» time changes

Scenario: A draft in an open card is not saved after the deletion
  Given Mia has typed a new title on MSK-US-12's card and not saved, and Ann deletes the item
  When Mia presses «Сохранить»
  Then the save is refused with 409 `workitems.workitem.deleted`
  And her card reads the item again and turns read-only, and the title stored is the old one

Scenario: A reader's write answers as it would on a live item
  When Rita's request saves MSK-US-12
  Then the answer is 403 «Недостаточно прав», the same as for a live item, and tells nothing of the deletion

Scenario: System clean-up still reaches a deleted item
  Given MARS owns the deleted MSK-US-12
  When Ann deletes the team MARS
  Then MSK-US-12 becomes «Без команды», still deleted, and its «Изменён» shows the deletion of the team (BR-OT-12)
  And no other field of it changes

Scenario: Restore and Purge are the writes it takes
  Then only «Восстановить» (US-4) and, for an administrator, «Удалить навсегда» (US-6) are offered on the card
```
