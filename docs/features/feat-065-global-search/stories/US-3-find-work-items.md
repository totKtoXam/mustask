# US-3: Typing finds work items by identifier or title

- story: US-3
- depends-on: US-1
- shippable: no — shipped with the palette (`feat-NNN-search-palette`) after `feat-NNN-search-work-items-read`
- status: pending

As a **member of an organization**, I want **to type a work item's identifier or part of its title and get the item**, so that **I open a card I have heard about in chat or on a board without finding its project first**.

Identifier = `<project key>-<type key>-<number>`, one number sequence per project across types (mod-003 BR-M3-5). This is the quick search; the description is searched on the full search page (US-7).

## Acceptance criteria

```gherkin
Scenario: Full identifier
  Given Max sees ALP-US-7 «Оплата картой»
  When he types «ALP-US-7»
  Then «Рабочие элементы» holds ALP-US-7 first, with the title, state and project «Альфа»
  And the group is the first group of the result

Scenario: Identifier is case-insensitive
  When Max types «alp-us-7»
  Then ALP-US-7 is found

Scenario: Project key and number
  When Max types «ALP-7»
  Then ALP-US-7 is found, whatever its type key
  And only one item can match, since the number is unique in the project

Scenario: Identifier prefix
  Given ALP-US-70, ALP-US-71 and ALP-BUG-72 exist
  When Max types «ALP-US-7»
  Then ALP-US-7, ALP-US-70 and ALP-US-71 are listed, the exact identifier first, the rest by latest change
  And ALP-BUG-72 is not listed

Scenario: Title contains the query
  Given ALP-US-7 «Оплата картой» and CHR-TASK-5 «Оплата счёта» are visible to Max
  When he types «оплата»
  Then both are found, the one changed latest first within the same tier
  And the emphasis marks «Оплата» in each title

Scenario: A bare number does not match by number
  When Max types «42»
  Then only items whose title contains «42» are found, and none by number alone

Scenario: Rows
  Then each row shows the identifier, the title, the state name and the project name
  And a title longer than the row ends with an ellipsis and a tooltip with the full title

Scenario: Removed items are not found
  Given ALP-US-9 «Оплата подарка» is in the Removed state
  When Max types «подарка» or «ALP-US-9»
  Then it is not found

Scenario: Items of a deactivated project are not found
  Given project «Старый» is deactivated and holds OLD-US-1 «Оплата старая»
  When Max types «старая»
  Then OLD-US-1 is not found

Scenario: Same title in several projects
  Given Ann sees ALP-US-7, BRV-BUG-3 and ZED-US-1 (the last in «Другая») all titled «Оплата картой»
  When she types «оплата картой» in the palette scoped to «Ромашка»
  Then ALP-US-7 and BRV-BUG-3 are listed and ZED-US-1 is not

Scenario: More rows
  Given Max sees 9 items whose titles contain «оплата»
  When he types «оплата»
  Then the group shows 5 rows and «Показать ещё»
  When he chooses «Показать ещё»
  Then the group shows all 9 (at most 20)

Scenario: Typo tolerance fills a short group
  Given Max sees one item titled «Оплата картой» and no other title near «оплата»
  When he types «оплта» (4 characters, one letter missing)
  Then the group shows «Оплата картой» below any exact, prefix or contains rows, with no emphasis on it
  And typing «опл» (3 characters) finds it only as a prefix, and a 3-character typo «олп» finds nothing

Scenario: Typo tolerance does not displace exact rows
  Given 5 items contain «оплата» in the title and one more is only similar
  When Max types «оплата»
  Then the similar item is not among the 5 rows

Scenario: The full-search row
  When Max types «оплата»
  Then the last row of the whole palette is «Искать везде, включая описания», after every group
  And choosing it opens the full search page (US-7) with «оплата» in its field

Scenario: The full-search row stays when nothing matches
  When Max types «рефакторинг» which matches no title or name he may see
  Then the palette says «Ничего не найдено» and still offers «Искать везде, включая описания»

Scenario: Characters with a meaning elsewhere are literal
  When Max types «100%» or «a_b» or «\»
  Then they match only titles that contain those characters

Scenario: Speed
  Given an organization with 100 000 work items and a user who sees all of them
  When a query of 2 characters or a full title word is sent
  Then the read answers within 300 ms at p95 and the rows are on screen within 600 ms of the last keystroke at p95
```
