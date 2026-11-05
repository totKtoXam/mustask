# US-1: A member narrows the list by state and finds deleted items under «Удалена»

- story: US-1
- depends-on: -
- shippable: no — the band, its address and its empty states are shared with US-2 and US-3; the line is one MR
- status: pending

As a **member of the organization**, I want **to narrow the project's «Рабочие элементы» to the states I choose, «Удалена» among them**, so that **I see the work at the stage I care about, and find a deleted item again to restore it, without knowing its address (doc-002 US-11; mod-005 OQ-2 A)**.

## Acceptance criteria

Data: spec § Common data; Mia is signed in unless a scenario says otherwise. Rules: spec BR-WS-1..-5, -10..-14, -17; mod-005 BR-DR-6, -7, design § 6 (its page landing, amended by BR-WS-5). The address's parameter names are the contract's: below, "the address carries «Новая»" means the state parameter with that value.

```gherkin
Scenario: The state field offers the process's states, each once
  When Mia opens the «Статус» field of the band «Фильтры» on «Рабочие элементы» of «Платформа»
  Then it offers «Новая», «Активна», «Разрешена», «Закрыта», «Удалена», in that order, with no counts
  And with nothing chosen the field reads «Все статусы»

Scenario: One state narrows the list, and a deleted item's held state does not count
  When Mia chooses «Новая»
  Then the list reads MSK-BUG-42 alone and the pager's count is 1
  And MSK-FEAT-6, deleted while it held «Новая», is not listed
  And clearing the field gives back the five live rows in their order, count 5

Scenario: A state name spans the types that have it
  When Mia chooses «Разрешена»
  Then the list reads MSK-US-3, a «Пользовательская история», and MSK-BUG-2, a «Баг», count 2
  And no «Задача» could match, since its type has no «Разрешена»

Scenario: Any chosen state matches, and the rows keep their order
  When Mia chooses «Новая» and «Разрешена»
  Then the list reads MSK-BUG-42, MSK-US-3, MSK-BUG-2, in the order of the unfiltered list, count 3

Scenario: «Удалена» lists the recycle bin
  When Mia chooses «Удалена» alone
  Then the list reads MSK-FEAT-6, then MSK-US-5, latest change first, count 2
  And each row's «Статус» reads «Удалена» with the Removed category's mark, and its «Изменён» is its deletion's time
  And the rows carry no action and the table no column of their own (OQ-3 A)

Scenario: «Удалена» beside another state lists both
  When Mia chooses «Активна» and «Удалена»
  Then the list reads MSK-FEAT-6, MSK-US-5, MSK-US-1, count 3

Scenario: The state filter combines with the team filter
  When Mia chooses «Разрешена» and, in «Команда», VENUS
  Then the list reads MSK-BUG-2 alone
  And with «Удалена» in place of «Разрешена» it reads MSK-FEAT-6 alone

Scenario: A deleted row opens its read-only card, and a restore takes it out of the bin
  Given Mia's list shows «Удалена» alone
  When she opens MSK-US-5 by its title
  Then the card is read-only, reads «Удалил(а): Мия Петрова, 17.10.2026 10:00» and offers «Восстановить», and no «Удалить навсегда»
  When she restores it, accepting «Активна», and goes Back
  Then the list still shows «Удалена» alone, on its first page, and reads MSK-FEAT-6 alone, count 1
  And under «Активна» MSK-US-5 is listed again

Scenario: Each role reaches the bin with the actions its codes allow
  Given the address of the list with «Удалена» chosen
  When Ann, then Rita, opens it and then the card of MSK-FEAT-6 by its title
  Then both see MSK-FEAT-6 and MSK-US-5
  And Ann's card offers «Восстановить» and, in «⋮», «Удалить навсегда»; Rita's card offers neither

Scenario: A purge on a card opened from the bin returns to the bin
  Given Ann's list shows «Удалена» alone, and she opens MSK-FEAT-6 by its title
  When she purges it with «Удалить навсегда»
  Then the page shows the list under «Удалена» alone, on its first page, reading MSK-US-5 alone, count 1
  And the notice reads «Рабочий элемент MSK-FEAT-6 удалён навсегда.»
  And going Back does not show MSK-FEAT-6's address

Scenario: A purge on a card not opened from the list lands on the list unfiltered
  Given Ann opens MSK-US-5's card by its address, not from a row of the list
  When she purges it with «Удалить навсегда»
  Then the page shows the project's list unfiltered, the five live rows, count 5 (mod-005 design § 6)
  And going Back does not show MSK-US-5's address

Scenario: The card's trail opens the list unfiltered, and Back keeps the filter
  Given Mia's list shows «Удалена» alone, and she opens MSK-US-5 by its title
  When she goes Back
  Then the list shows «Удалена» alone, on its first page, with MSK-FEAT-6 and MSK-US-5
  When she opens MSK-US-5 by its title again and follows «Рабочие элементы» in the page's trail
  Then the list shows the five live rows and no filter is set

Scenario: The filter lives in the address
  Given Mia has chosen «Новая» and «Разрешена»
  Then the page's address carries both values
  When she reloads the page, or sends the address to Rita who opens it
  Then the list shows the same three rows and the field shows both states chosen
  And going Back from the page after choosing them leaves «Рабочие элементы»; it does not step through the choices
  And the main navigation's «Рабочие элементы» opens the list unfiltered

Scenario: A change of filter shows the first page
  Given «Платформа» holds 120 live items, 70 of them «Новая», and Mia is on page 3 of the unfiltered list
  When she chooses «Новая»
  Then the list shows its page 1 of 2, count 70
  When she opens a row's card from page 2 and goes Back
  Then the list shows «Новая» on page 1

Scenario: An address value that names no state selects nothing
  When Mia opens the list's address carrying «Готово», which is no state of the process
  Then the list shows «Ничего не найдено», the band shows that a filter is on and offers «Очистить фильтры»
  And the field names no state
  And the address carrying «Готово» and «Новая» lists MSK-BUG-42 alone

Scenario: A filter that matches nothing says so
  Given Mia has chosen «Закрыта» and, in «Команда», VENUS
  Then the list shows «Ничего не найдено» — «Ни один рабочий элемент не подходит под заданные условия фильтра.» with «Сбросить фильтр»
  When she chooses «Сбросить фильтр»
  Then both filters are cleared, the address carries neither, and the five live rows are back

Scenario: A project whose items are all deleted keeps the way into the bin
  Given a project «Пустой» (key PST) of «Альфа» whose only item, PST-US-1, is deleted
  When Mia opens its «Рабочие элементы» with no filter
  Then it shows «Рабочих элементов пока нет», under the band «Фильтры»
  And choosing «Удалена» lists PST-US-1

Scenario: An item created under a filter it does not match is announced
  Given Mia has chosen «Активна»
  When she creates the «Баг» «Сбой экспорта», which starts in «Новая»
  Then the notice reads «Рабочий элемент MSK-BUG-43 создан. Он не подходит под текущий фильтр.»
  And the list keeps the filter and does not show MSK-BUG-43

Scenario: Another user's restore shows at the next read
  Given Mia's list shows «Удалена» alone, with MSK-US-5
  When Ann restores MSK-US-5 meanwhile
  Then Mia's row stays until her list reads again, and then is gone
  And opening it before that read shows its live card, not the deleted one
```
