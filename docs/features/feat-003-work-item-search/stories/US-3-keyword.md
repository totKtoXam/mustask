# US-3: A member finds an item by a keyword over its title and identifier

- story: US-3
- depends-on: US-1
- shippable: no — a field of US-1's band, under its address, combination and empty states; the line is one MR
- status: pending

As a **member of the organization**, I want **to type a word of a title, an identifier or just its number and see the matching items of the project**, so that **I reach the item I have in mind without paging, opening a board or writing a query (doc-002 US-11; doc-010 D-14)**.

## Acceptance criteria

Data: spec § Common data; Mia is signed in unless a scenario says otherwise. Rules: spec BR-WS-1, -2, -4, -9..-12, -15; doc-012 US-11 (the keyword on the views means the same). The address's parameter names are the contract's.

```gherkin
Scenario: A part of the title finds the item, ignoring letter case and «ё»
  When Mia types «оплат» into «Поиск» (hint «Заголовок или ID»)
  Then the list reads MSK-BUG-2 «Ошибка оплаты» and MSK-US-1 «Оплата картой», count 2
  And «ОПЛАТА КАРТОЙ» lists MSK-US-1 alone, and «отчет» lists MSK-US-3 «Отчёт склада: 100%»
  And «картой оплата» lists nothing: the keyword is one piece of text

Scenario: The whole identifier finds the item, in any letter case
  When Mia types «MSK-BUG-42», then «msk-bug-42»
  Then each time the list reads MSK-BUG-42 alone

Scenario: The number alone finds the item, and a title that contains it
  When Mia types «42»
  Then the list reads MSK-BUG-42, by its number, and MSK-TASK-4 «Проверить 42 сценария», by its title
  And «4» lists MSK-TASK-4 alone, once, though it matches by its number and its title both
  And MSK-BUG-42 is not listed for «4»: a number matches whole

Scenario Outline: A part of an identifier, or one that is not the item's, matches no identifier
  When Mia types <keyword>
  Then the list shows «Ничего не найдено», since no title contains it either

  Examples:
    | keyword       | why                                                     |
    | «MSK-BUG-4»   | a part of MSK-BUG-42                                    |
    | «BUG-42»      | the identifier without its project key                  |
    | «MSK-US-42»   | number 42 is a «Баг», MSK-BUG-42                        |
    | «OTH-US-1»    | an item of «Другая», whose title «Оплата картой» is not searched here |
    | «WRH-US-1»    | an item of «Склад», another project of «Альфа»          |
    | «042»         | not the number as the identifier writes it              |
    | «99999999999» | a number past every item's: no error                    |

Scenario: Every character of the keyword is literal
  When Mia types «%»
  Then the list reads MSK-US-3 alone, whose title holds «%»
  And «_» and «\» each show «Ничего не найдено»: neither stands for any character

Scenario: Spaces around the keyword do not count, and a blank keyword is no filter
  When Mia types «   оплат   »
  Then the list reads as for «оплат»
  When she replaces it with spaces only
  Then the five live rows are back, the address carries no keyword, and «Очистить фильтры» is not offered

Scenario: The field takes at most 255 characters
  When Mia types or pastes 300 characters into «Поиск»
  Then the field holds the first 255
  And an address carrying a keyword longer than 255 characters after trimming lists nothing and is no error

Scenario: Only the title and the identifier are searched
  When Mia types «Stripe», which stands in MSK-US-1's «Описание» alone
  Then the list shows «Ничего не найдено»

Scenario: A deleted item is found by keyword only under «Удалена»
  When Mia types «Возврат», then «MSK-US-5»
  Then each time the list shows «Ничего не найдено»
  When she also chooses «Удалена» in «Статус»
  Then each time the list reads MSK-US-5 alone, its «Статус» «Удалена»
  And «оплат» with «Удалена» and «Разрешена» chosen lists MSK-US-5 and MSK-BUG-2

Scenario: All four filters combine, and each clear widens the list
  Given «Платформа» gains the live «Баг» MSK-BUG-43 «Оплата не проходит» («Новая», Mia, MARS), changed last
  When Mia types «оплат», chooses «Новая» and «Активна» in «Статус», «Мия Петрова» in «Исполнитель» and MARS in «Команда»
  Then the list reads MSK-BUG-43 and MSK-US-1, count 2
  And clearing «Команда» adds nothing, since MSK-BUG-42's title has no «оплат»
  And clearing «Исполнитель» and «Статус» as well adds MSK-BUG-2, count 3
  And with all four set again, «Очистить фильтры» clears them at once: the six live rows are back and the address carries no filter

Scenario: The keyword lives in the address
  Given Mia has typed «оплат» and chosen «Разрешена»
  When she reloads, or Rita opens the same address
  Then «Поиск» holds «оплат», «Разрешена» is chosen, and the list reads MSK-BUG-2 alone
  And a change of the keyword shows page 1 and replaces the address's history entry

Scenario: A reader searches the same way
  When Rita, a «Читатель», types «оплат»
  Then her list reads MSK-BUG-2 and MSK-US-1, with no create, edit or delete control on the page

Scenario: The list stays fast with every filter at the stated volume
  Given an organization of 100 000 items in which «Платформа» holds 5 000, 500 of them in «Удалена», with 2 000 people assigned
  When a member opens the list unfiltered, with each filter alone, and with the keyword, two states with «Удалена» among them, one person and one team at once
  Then the first page and its count arrive within 2 s each time
  And the «Исполнитель» options arrive within 2 s
  And each read takes the same number of statements as with 50 items (doc-012 NFR Query shape)
```
