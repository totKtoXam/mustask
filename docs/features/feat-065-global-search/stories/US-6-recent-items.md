# US-6: An empty field shows the user's recent items

- story: US-6
- depends-on: US-5
- shippable: no — it is the empty state of the palette; delivered by its own line `feat-NNN-search-recent` after the palette, but with no use before it
- status: pending

As a **member of an organization**, I want **the palette to list what I opened lately when the field is empty**, so that **I return to my work in one keystroke**.

Defaults (OQ-4 A, OQ-5 A): "opened" = an object's own page or a work item's card displayed, from anywhere; at most 10, newest first, one entry per object, per user and organization, stored in the browser as type, id and organization.

## Acceptance criteria

```gherkin
Scenario: Recent after opening
  Given Max opened ALP-US-7, then the team VENUS, then project «Альфа» (by board, link or palette)
  When he opens the palette with an empty field
  Then «Недавние» lists «Альфа», VENUS, ALP-US-7 in that order, each with the type icon, current name and grey line as in search

Scenario: Re-opening moves to the top
  Given the list is «Альфа», VENUS, ALP-US-7
  When Max opens VENUS again
  Then the list is VENUS, «Альфа», ALP-US-7 with no duplicate

Scenario: Keyboard and choosing
  Given the list is shown
  Then the first row is active, ↑ ↓ and Enter work as in US-1, and Enter opens the object as in US-5

Scenario: At most 10
  Given Max opened 12 distinct objects
  Then the list shows the 10 latest and storage keeps no more

Scenario: Per organization and per user
  Given Max opened ALP-US-7 in «Ромашка» and ZED-US-1 in «Другая»
  When he opens the palette scoped to «Ромашка»
  Then only ALP-US-7 is listed
  And another user on the same browser sees none of Max's entries

Scenario: Names are current
  Given Max opened project «Альфа», then it was renamed «Альфа-2»
  Then the list shows «Альфа-2»

Scenario: An object left sight
  Given Olga opened project «Альфа», then lost every route to it
  When she opens the palette
  Then «Альфа» is not listed, no message is shown, and her next stored write drops its id

Scenario: An object was deleted
  Given Max opened ALP-US-9 which was then deleted (ADR-0030) or its project was deactivated
  Then it is not listed

Scenario: The same object regains sight
  Given an entry was dropped from view but its id is still stored (no write happened)
  When sight returns
  Then it is listed again

Scenario: Nothing yet
  Given Vera has never opened anything
  Then the palette says «Здесь появятся недавно открытые объекты»

Scenario: Clear
  When Max clicks «Очистить недавние», the small text button at the right end of the «Недавние» heading row, or reaches it by Tab and presses Enter
  Then arrow keys never land on it (it is not a list row)
  And the list for this organization is emptied at once, with no confirmation step, and the stored value is removed

Scenario: Unreadable storage
  Given the stored value is malformed or localStorage is unavailable
  Then the list is empty, the palette works, and nothing throws

Scenario: Which page records what
  When Max opens project «Альфа»'s «Канбан», then «Альфа»'s «Структура» node page, then the sub-area «Сборка»'s page, then ALP-US-7's card with `?teamId=` of MARS in the address
  Then the list is ALP-US-7, «Сборка», «Альфа» (once), and MARS is not recorded

Scenario: The resolving read fails
  Given the list holds an id the server would omit
  When the resolving read fails
  Then the palette shows «Не удалось выполнить поиск. Попробуйте ещё раз.» with «Повторить» and no stored id is dropped

Scenario: Not recorded
  Then a search query, a hidden or not-found object, and a page that answered «не найден» are never recorded

Scenario: Resolve is one read
  When the list is shown
  Then at most one request resolves up to 10 ids, in a fixed number of statements, scoped to the organization
  And an id of another organization resolves to nothing
```
