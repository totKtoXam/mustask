# US-2: A deleted item leaves every list, board and backlog and is found by its address

- story: US-2
- depends-on: US-1
- shippable: no — it is the visibility half of Delete (US-1); without it a deleted item stays on the list
- status: pending

As a **member**, I want **a deleted work item to disappear from my lists, boards, backlogs and pickers while it stays recoverable**, so that **the work in front of me is the live work, and a deleted item is still one address away (doc-001 BR-14 a, b)**.

## Acceptance criteria

Data: spec § Roles, "Common data"; MSK-US-12 is deleted by Mia unless a scenario says otherwise. Rules: spec BR-DR-6, -7, -9, -17, § Surfaces and writes; doc-001 BR-14 (a), (b), BR-12; mod-006 § The stack rank. No page and no filter lists deleted items in this line (OQ-2).

```gherkin
Scenario: The project's list neither shows nor counts a deleted item
  Given «Платформа» holds the 8 items of "Common data" and MSK-US-12 is deleted
  When Mia opens the project's «Рабочие элементы»
  Then 7 rows show and the pagination's count says 7, whichever page she opens
  And no control of the page shows deleted items: feat-003's «Удалена» filter does

Scenario: Team backlogs and boards at every level lose the item
  Given MSK-US-12 stood on MARS's «Истории» backlog and board, and on the project's
  When it is deleted
  Then neither backlog lists it, neither board has a card for it, and no column's count includes it
  And the other items keep their relative order (doc-001 BR-12)
  And MSK-BUG-7 and MSK-EPIC-1 behave the same on their own levels

Scenario: A deleted item keeps what it carried
  Given MSK-US-12 has the team MARS, the area «Платформа / API» (a leaf holding no other item), the assignee Mia and the parent MSK-FEAT-8
  When it is deleted
  Then it still has all four, on its read-only card (BR-14 b)
  And an area that holds it is not deleted: the deletion of «Платформа / API» is refused with `organizations.area.not_empty` «Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области»

Scenario: A deleted item is still counted among a team's items
  Given MARS owns MSK-EPIC-1, MSK-FEAT-6 and MSK-US-12, and MSK-US-12 is deleted
  When Ann starts to delete the team MARS
  Then the confirmation reads «Удалить команду «MARS»? Её рабочие элементы (3) станут «Без команды». Восстановить команду будет нельзя.»
  And on confirming, all three become «Без команды», the deleted one included, and MSK-US-12 stays deleted (BR-DR-9)

Scenario: A deleted item is found by its address
  When Mia opens …/workitems/MSK-US-12
  Then the card opens with the state «Удалена» and every field as it was, read-only (US-3)
  And a «Читатель» opens it the same way, with no control that would be refused
  And the address of OTH-US-1 under «Платформа» answers «Рабочий элемент не найден.» as an unknown one does

Scenario: A live child leads to its deleted parent
  Given MSK-EPIC-1 is deleted
  When Mia opens MSK-FEAT-8 and chooses its «Родитель»
  Then the field reads the identifier and title of MSK-EPIC-1 with the state «Удалена»
  And the link opens the read-only card of MSK-EPIC-1, which offers «Восстановить» (US-4)

Scenario: Pickers and lists of the form skip a deleted item
  Given MSK-EPIC-1 and MSK-BUG-4 are deleted
  When Mia opens MSK-FEAT-6 and types "Вход" into «Родитель»
  Then MSK-EPIC-1 is not among the options
  And «Дочерние элементы» of MSK-FEAT-6 lists MSK-US-2 only, and the count reads (1)

Scenario: A board card's checklist skips a deleted child
  Given MSK-US-12's card on MARS's «Истории» board shows MSK-TASK-13 in its checklist, counted 0 of 1
  When MSK-TASK-13 is deleted
  Then the checklist no longer lists it and the counter no longer counts it
  And after MSK-TASK-13 is restored (US-4) it is listed and counted again, in creation order

Scenario: Another user's open view lags until its next read
  Given Ann has MARS's «Истории» board open with the card of MSK-US-12
  When Mia deletes MSK-US-12
  Then Mia's delete succeeds whatever Ann has open
  And Ann's board shows the card until it next reads data (no live push in the MVP), then not
  And what Ann does to the stale card is refused (US-3)

Scenario: A deactivated project hides its deleted items too
  Given MSK-US-12 is deleted and Ann deactivates «Платформа»
  Then MSK-US-12's address answers `organizations.project.not_found` «Проект не найден.»
  When Ann restores «Платформа»
  Then MSK-US-12 is still deleted, with the state it remembers, and reads exactly as before

Scenario: Nothing leaks across organizations
  Given Oleg administers «Другая»
  When he asks for the address of MSK-US-12 under «Альфа», or for «Альфа»'s list
  Then he gets «Организация не найдена.» and no item of «Альфа», deleted or live
```
