# US-2: A member narrows the list by assignee, former members included

- story: US-2
- depends-on: US-1
- shippable: no — a field of US-1's band, under its address, combination and empty states; the line is one MR
- status: pending

As a **member of the organization**, I want **to narrow the project's «Рабочие элементы» to the items of the people I choose, or to the unassigned ones, a person who has left or whose account is disabled included**, so that **I find who carries what, and the work a departed colleague left behind is never lost from sight (doc-002 US-11; doc-001 BR-13)**.

## Acceptance criteria

Data: spec § Common data; Mia is signed in unless a scenario says otherwise. Rules: spec BR-WS-2, -4, -6..-8, -10, -11, -15. The address's parameter names are the contract's. Marks in the «Исполнитель» column follow OQ-2 A.

```gherkin
Scenario: The assignee field offers the people assigned to the project's live items, then «Не назначен»
  When Mia opens the «Исполнитель» field of the band «Фильтры»
  Then it offers «Иван Котов (не в организации)», «Мия Петрова», «Пётр Ли (неактивен)», «Не назначен», in that order, with no counts
  And it offers neither Ann nor Rita, who are assigned nothing, nor Zoya, assigned only the deleted MSK-FEAT-6, nor Oleg
  And with nothing chosen the field reads «Все исполнители»

Scenario: A former member's items can still be found
  When Mia chooses «Иван Котов (не в организации)»
  Then the list reads MSK-BUG-2 alone, count 1: MSK-US-5, also his, is deleted
  And its «Исполнитель» cell reads «Иван Котов (не в организации)»

Scenario: An inactive member's items can still be found
  When Mia chooses «Пётр Ли (неактивен)»
  Then the list reads MSK-TASK-4 alone, and its «Исполнитель» cell reads «Пётр Ли (неактивен)»

Scenario: «Не назначен» finds the unassigned items
  When Mia chooses «Не назначен»
  Then the list reads MSK-US-3 alone, its «Исполнитель» cell «Не назначен»

Scenario: Any chosen value matches
  When Mia chooses «Мия Петрова» and «Не назначен»
  Then the list reads MSK-BUG-42, MSK-US-3, MSK-US-1, in the order of the unfiltered list, count 3

Scenario: With «Удалена» chosen, the people of deleted items are offered too
  When Mia chooses «Удалена» in «Статус» and opens «Исполнитель»
  Then it offers «Зоя Ким», «Иван Котов (не в организации)», «Мия Петрова», «Пётр Ли (неактивен)», «Не назначен»
  When she chooses «Иван Котов (не в организации)»
  Then the list reads MSK-US-5 alone
  And with «Активна» added to «Статус» it still reads MSK-US-5 alone, since MSK-BUG-2 is «Разрешена»

Scenario: A person offered only under «Удалена» names nobody once «Удалена» is cleared
  Given Mia has chosen «Удалена» in «Статус» and «Зоя Ким» in «Исполнитель», and the list reads MSK-FEAT-6
  When she clears «Статус»
  Then the list shows «Ничего не найдено», the address still carries Zoya's id, and «Исполнитель» offers no «Зоя Ким» and names nobody
  And the band offers «Очистить фильтры»

Scenario: The assignee filter combines with the state and team filters
  When Mia chooses «Мия Петрова», «Новая» in «Статус» and VENUS in «Команда»
  Then the list reads MSK-BUG-42 alone
  And clearing «Команда» still gives MSK-BUG-42, and clearing «Статус» as well gives MSK-BUG-42 and MSK-US-1

Scenario: A person the directory does not know reads without «(неактивен)»
  Given the directory does not know Petr's id (mod-014 BR-UP-17), and Petr is still a member of «Альфа»
  When Mia opens «Исполнитель»
  Then Petr's option reads «Неизвестный пользователь», with no mark, after every named person and before «Не назначен»
  And MSK-TASK-4's «Исполнитель» cell reads «Неизвестный пользователь»
  And if his membership is also removed, both read «Неизвестный пользователь (не в организации)»

Scenario: The marks follow the membership at each read
  Given Mia's list is filtered by «Иван Котов (не в организации)»
  When Ann adds Ivan back to «Альфа» as «Участник» and Mia's list and its options read again
  Then the option and the MSK-BUG-2 cell read «Иван Котов», and the filter still lists MSK-BUG-2

Scenario: The filter lives in the address, and a person the address names may have no item
  Given Mia has chosen «Пётр Ли (неактивен)» and sends the address to Rita
  When Rita opens it
  Then she sees MSK-TASK-4 alone, with «Пётр Ли (неактивен)» shown chosen
  When Petr's only item, MSK-TASK-4, is reassigned to Mia and Rita reloads
  Then the list shows «Ничего не найдено», the band shows that a filter is on and offers «Очистить фильтры»
  And Petr is no longer among the options

Scenario Outline: A value that names nobody assigned in the project selects nothing, alike
  When Mia opens the list's address whose assignee value is <value>
  Then the list shows «Ничего не найдено», with the same status and body for every row of this table
  And the field names nobody and the band offers «Очистить фильтры»
  And the address carrying <value> beside Mia's id lists MSK-BUG-42 and MSK-US-1, Mia's items, alone

  Examples:
    | value                                              |
    | a malformed id                                     |
    | an id the directory does not know                  |
    | Oleg's id, assigned only in «Другая»               |
    | Ann's id, a member of «Альфа» assigned nothing     |

Scenario: A reader filters by assignee
  Given Rita, a «Читатель», who may not assign anyone
  When she opens «Исполнитель»
  Then she is offered the same people as Mia, and filtering works for her as above

Scenario: The options' read is contained like the list's
  When Oleg, no member of «Альфа», requests the «Исполнитель» options of «Платформа»
  Then the answer is «Организация не найдена.», as for the list
  And Mia's request for them at the address of «Чужой» under «Альфа», or of a deactivated project of «Альфа», answers «Проект не найден.»
```
