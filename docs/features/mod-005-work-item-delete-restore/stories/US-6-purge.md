# US-6: An organization administrator purges a deleted work item

- story: US-6
- depends-on: US-1, US-5
- shippable: no — the last step of the recycle bin; it acts on what Delete (US-1) leaves and on the links of US-5
- status: pending

As an **organization administrator**, I want **to remove a deleted work item for good**, so that **what must not stay is really gone, and the children it leaves behind stay consistent (doc-001 BR-6; doc-010 D-13)**.

## Acceptance criteria

Data: spec "Common data", plus MSK-FEAT-23 «Вход по коду», the project's highest number. Rules: spec BR-DR-12..-17; doc-001 BR-6, BR-4; doc-002 BR-M3-5, BR-M3-9, US-10. Attachments, comments, tags and history do not exist when this ships: `mod-004`, `feat-002`, `feat-016` and `feat-015` add their data to the purge (BR-DR-16), and `mod-004`'s refusal while the Drive is unreachable (doc-002 US-10) is its own.

```gherkin
Scenario: «Удалить навсегда» is offered to an administrator on a deleted card only
  Given MSK-BUG-7 is deleted
  When Ann opens «Ещё действия» on its card
  Then the menu reads «Копировать ссылку» and «Удалить навсегда», the second in the error colour
  And on a live card, or for Mia or Rita on a deleted one, «Удалить навсегда» is not in the menu

Scenario: The confirmation says that it cannot be undone
  When Ann chooses «Удалить навсегда»
  Then a confirmation titled «Удалить рабочий элемент навсегда?» reads «Рабочий элемент MSK-BUG-7 «Падение при пустом заголовке» будет удалён без возможности восстановления.»
  And its buttons are «Отмена», holding the focus, and «Удалить навсегда» in the error colour
  And «Отмена», Escape and «Закрыть» change nothing
  And for a deleted MSK-EPIC-1 with two live children the text goes on «Дочерние элементы (2) останутся без родителя.»

Scenario: Confirming removes the item
  When Ann confirms
  Then the notice «Рабочий элемент MSK-BUG-7 удалён навсегда.» shows, and the card closes (a dialog) or the page goes to the list (a full page)
  And the address of MSK-BUG-7 answers 404 `workitems.workitem.not_found` «Рабочий элемент не найден.», as an item that never was
  And the item is on no list, board or backlog, and no later filter finds it
  And its place in every backlog order is gone, and the others keep their relative order

Scenario: The children of a purged item survive without it
  Given MSK-EPIC-1 is deleted and has the children MSK-FEAT-6 and MSK-FEAT-8
  When Ann purges MSK-EPIC-1
  Then both are live, in the states they had, with no parent link, and their «Изменён» shows the purge's time
  And their «Родитель» is empty, the picker does not offer MSK-EPIC-1, and no row of any backlog points at it
  And the purged item's own parent, if it had one, is unchanged, its «Изменён» time included

Scenario: The number is never issued again
  Given MSK-FEAT-23 is deleted and 23 is the project's highest number
  When Ann purges it and Mia creates an Эпик, then a Баг
  Then they get the numbers 24 and 25, so MSK-EPIC-24 and MSK-BUG-25
  And no item in «Платформа» ever has the number 23, under any type key

Scenario: Only a deleted item can be purged
  Given MSK-US-12 is live
  When a purge of MSK-US-12 is sent
  Then the answer is 409 `workitems.workitem.not_deleted` «Рабочий элемент не удалён.» and nothing is removed

Scenario: Only an administrator can purge
  Given MSK-BUG-7 is deleted
  When Mia sends a purge, then Rita
  Then each answers 403 «Недостаточно прав» and the item is untouched
  And Oleg, who administers «Другая», gets «Организация не найдена.», as does an instance administrator who is no member of «Альфа»
  And Ann is not refused: the administrator holds the code in every project of the organization

Scenario: A purge cannot be undone
  Given Ann purged MSK-BUG-7
  When anyone sends a restore, a save or a second purge for it
  Then each answers 404 `workitems.workitem.not_found` and nothing comes back

Scenario: A purge and a restore of one item end in one order
  Given MSK-BUG-7 is deleted, and Ann purges it as Mia restores it
  Then either the restore lands first, and the purge answers 409 `workitems.workitem.not_deleted` with the item live, or the purge lands first and the restore answers 404
  And no answer is a server error, and no half-removed item remains

Scenario: A purge and a child's release of its link end in one order
  Given MSK-EPIC-1 is deleted and Mia releases MSK-FEAT-8's link as Ann purges MSK-EPIC-1
  Then both succeed in either order, or the release finds nothing to release, and MSK-FEAT-8 ends live without a parent

Scenario: Other users' open views meet the purge as an unknown item
  Given Mia has MSK-BUG-7's read-only card open and Ann has a board that still shows a card of it
  When Ann purges it
  Then Mia's next read of the card shows the not-found frame
  And a move or tick on the stale card answers 404 `workitems.workitem.not_found`, and the board reads again without it

Scenario: A purge leaves no trace of the item
  When Ann purges MSK-BUG-7
  Then nothing in the product names it or its title: no tombstone, no copy
  And one log line records the user, the organization, the project and the identifier MSK-BUG-7, and not its title or content
  And the purge is not attributed in the product (no audit log before release 2)

Scenario: A big purge is quick
  Given a deleted Эпик with 5,000 children
  When Ann purges it
  Then it completes within 2 seconds, and every child ends live without a parent

Scenario: Data added later cannot be forgotten by the purge
  Given stored data keyed to a work item that the purge neither removes nor lists as kept, with its reason
  Then an automated check fails the build (BR-DR-16)

Scenario: A deactivated project cannot be purged from
  Given «Платформа» is deactivated
  When Ann sends a purge for a deleted item of it
  Then the answer is `organizations.project.not_found` «Проект не найден.» and the item is untouched
```
