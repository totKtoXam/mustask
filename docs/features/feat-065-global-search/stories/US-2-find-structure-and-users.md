# US-2: Typing finds departments, teams, projects, sub-areas and users, grouped and ranked

- story: US-2
- depends-on: US-1
- shippable: no — the read ships with `feat-NNN-search-structure-users-read`, the UI with `feat-NNN-search-palette`; neither is used alone
- status: pending

As a **member of an organization**, I want **to type part of a name, a project key or a person's name or email and see matching departments, teams, projects, sub-areas and people grouped by type**, so that **I find the object I mean in a few keystrokes**.

## Acceptance criteria

```gherkin
Scenario: Grouped results
  Given Ann (administrator) opened the palette in «Ромашка»
  When she types «ал»
  Then the group «Проекты» holds «Альфа» (ALP) with the grey line «Логистика»
  And empty groups are absent
  And the first row of the first group is active

Scenario: Match by project key
  Given Ann opened the palette
  When she types «chr»
  Then «Чарли» (CHR) is found in «Проекты», the key emphasised

Scenario: Case and «ё»
  Given a team named «Звёзды» exists and Ann sees it
  When she types «ЗВЕЗДЫ»
  Then «Звёзды» is found

Scenario: Literal characters
  Given Ann types «%»
  Then nothing is treated as a wildcard
  And only names containing «%» match, if any

Scenario: Users by name and email
  Given «Ромашка» has the member Max «Макс Орлов» (max@example.test)
  When Ann types «орл»
  Then «Макс Орлов» is in «Пользователи» with the grey line max@example.test
  And typing «max@exa» finds the same row

Scenario: Users need the membership read code
  Given a caller without `organizations.membership.read` in the organization (no built-in role today; a handler test with such a caller)
  When the read runs for «орл»
  Then the group «Пользователи» is absent

Scenario: Former and disabled members
  Given «Ромашка» has a former member «Пётр Орлов» and a member «Ирина Орлова» whose account is disabled
  When Ann types «орлов»
  Then «Пётр Орлов» is not found
  And «Ирина Орлова» is found and reads «(неактивен)»

Scenario: Ranking within a group
  Given Ann sees teams «Орион», «Мориарти» and «Корионов»
  When she types «ори»
  Then the order is «Орион» (name starts with), then «Корионов» and «Мориарти» (contain), the latter two by name
  And a name that equals the query exactly comes before a name that merely starts with it

Scenario: Group order follows the best match
  Given «Ромашка» has a department «Снабжение» and a team «Снабжение-2», both seen by Ann
  When she types «снабжение»
  Then «Департаменты» comes before «Команды» because its best row is an exact match

Scenario: At most 5 rows a group and «Показать ещё»
  Given Ann sees 12 projects whose names contain «про»
  When she types «про»
  Then «Проекты» shows 5 rows and a row «Показать ещё»
  When she chooses «Показать ещё»
  Then the group shows up to 12 rows (at most 20), the other groups and the query unchanged
  And a group with 5 or fewer matches shows no «Показать ещё»

Scenario: Sub-areas
  Given project «Альфа» has the sub-area «Сборка»
  When Ann types «сборка»
  Then «Области» holds «Сборка» with the grey line «Альфа · Логистика»

Scenario: Deactivated and deleted objects
  Given the project «Старый» with its sub-area «Архив» is deactivated and the team «Бывшая» is deleted
  When Ann types «старый», «архив» or «бывшая»
  Then none of them is found

Scenario: Too short
  When Ann types one character
  Then the dialog says «Введите не менее 2 символов» and no read is sent

Scenario: No match
  When Ann types «яблоко» which matches nothing she may see
  Then the dialog says «Ничего не найдено»

Scenario: The query is trimmed and bounded
  When Ann types « ал » with spaces around it
  Then it searches «ал»
  And the field accepts at most 100 characters

Scenario: Stale answers are ignored
  Given Ann types «ал», pauses, then types «альф» before the first answer arrives
  Then the first request is cancelled and only the answer for «альф» is shown

Scenario: Debounce
  When Ann types 6 characters in 400 ms with no pause over 150 ms
  Then at most 2 reads are sent

Scenario: Failure
  Given the read fails
  Then the dialog says «Не удалось выполнить поиск. Попробуйте ещё раз.» with «Повторить»
  And the field keeps its text and the palette stays open

Scenario: Rate limit
  Given the read answers 429 with Retry-After: 2
  Then the dialog retries after 2 s, at most 3 times, then shows the failure message with «Повторить»
```
