# US-6: An importer undoes an import batch

- story: US-6
- depends-on: US-5
- shippable: no — an analysis story; delivered by `feat-NNN-import-run-and-undo`
- status: pending

As an **importer**, I want **to undo an import that went wrong**, so that **a bad template costs one click, not a week of cleanup, and never someone else's work**.

## Acceptance criteria

```gherkin
Scenario: Undo removes the untouched items the batch created
  Given a batch that created 460 items, none changed since
  When the importer undoes it and confirms
  Then the 460 items are purged with their links and tags
  And none of them is in the recycle bin
  And the batch reads «Отменён» (BR-IE-16, OQ-3 A)

Scenario: A corrected import can follow an undo
  Given a batch from a template without an identifier column has been undone
  When a corrected template imports the same file
  Then each row creates one item, and the project holds no item left from the first try (BR-IE-10, BR-IE-16)

Scenario: A created item changed after the import is kept
  Given «Пользователь А» moved one created item to «Активна» after the import
  When the batch is undone
  Then that item stays as it is, and the others are purged
  And the batch reads «Отменён частично», listing the item and why (BR-IE-16, BR-IE-17)

Scenario: Undo restores overwritten fields that still hold the imported value
  Given the batch changed an item's title from «А» to «Б» and its priority from 3 to 1
  And afterwards «Пользователь А» set the priority to 2
  When the batch is undone
  Then the title is «А» again and the priority stays 2
  And the batch reads «Отменён частично», listing the priority as changed since (BR-IE-16)

Scenario: A reorder does not count as a change
  Given a created item that someone only moved within a backlog
  When the batch is undone
  Then the item is purged (BR-IE-17)

Scenario: Links, tags and participants the batch added are removed
  Given the batch added the link «https://docs.example.com/d/1» and the participant «Пользователь Б» to an existing item
  When the batch is undone
  Then both are gone, and links and participants added by people stay (BR-IE-16)

Scenario: A later batch over the same items blocks the undo
  Given batch 2 updated items that batch 1 created, and batch 2 is not undone
  When the importer undoes batch 1
  Then it is refused, naming batch 2, and nothing changes
  When batch 2 is undone first
  Then batch 1 can be undone (BR-IE-16)

Scenario: The undo window is 30 days
  Given a batch that ended 31 days ago
  When the importer opens it
  Then no undo is offered, and a request to undo is refused (BR-IE-16)

Scenario: An undo is final
  Given a batch that was undone
  When the importer opens it
  Then neither undo nor redo is offered; a new import runs from a new dry run (BR-IE-16)

Scenario: The confirmation counts what will happen
  Given a batch that created 460 items and updated 30
  When the importer asks to undo it
  Then the confirmation names 460 created items to remove and 30 updated items to restore, before anything changes

Scenario: An item deleted after the import counts as changed
  Given a created item that someone moved to «Удалена»
  When the batch is undone
  Then it stays in the recycle bin and is listed (BR-IE-17)

Scenario: An updated item that left reach is listed, not touched
  Given the batch updated three items, and since then one was moved to «Удалена», one was purged and one's project was deactivated
  When the batch is undone
  Then none of the three is written, and the batch reads «Отменён частично», listing each with its reason (BR-IE-16)

Scenario: An old value that no longer passes the form's checks is not restored
  Given the batch changed an item's owning team from «Команда 1» to «Команда 2», and «Команда 1» was unlinked from the project since
  When the batch is undone
  Then the owning team stays «Команда 2» and is listed (BR-IE-16, BR-IE-12)

Scenario: An undo purge is the mod-005 purge of a live item
  Given an untouched item the batch created, outside «Удалена»
  When the batch is undone
  Then the item is purged through the purge path with its log line, as mod-005 BR-DR-12 is amended for this caller (BR-IE-16, OQ-3 A)

Scenario: Undo of 5 000 rows completes within 5 minutes and runs alone
  Given a batch of 5 000 rows
  When it is undone
  Then the undo ends within 5 min, shown with its progress
  And no import starts in the organization until it ends (BR-IE-14, NFR volumes)

Scenario: A manager undoes their own batch while they manage its projects
  Given «Пользователь Д», the project «Менеджер» of «Платформа», ran a batch into «Платформа»
  When they undo it
  Then it is undone
  When instead they had lost «Менеджер» on «Платформа» before asking
  Then the undo is refused with «Недостаточно прав», and the administrator may still undo it (§ Roles, OQ-1 B)

Scenario: A manager cannot undo another's batch
  Given a batch started by the organization administrator into «Платформа»
  When the project «Менеджер» of «Платформа» asks to undo it
  Then it is refused with «Недостаточно прав» (§ Roles)

Scenario: A member who may not import does not undo
  Given an organization «Участник» who manages nothing
  When they ask to undo a batch
  Then it is refused with «Недостаточно прав» (§ Roles)
```
