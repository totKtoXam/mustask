# US-5: A user filters the «Рабочие элементы» list and a project's views by team

- story: US-5
- depends-on: US-4
- shippable: no — one of the seven stories of `mod-017-work-item-owning-team`; it reads the field of US-1 and the views of US-4, and founds the filter specification that `feat-003-work-item-search` and `feat-013-board-backlog-filter` extend
- status: pending

As an **organization member reading a project's work items**, I want **to narrow the list, the project's backlog and the project's board to one team, several, or the items with none**, so that **I find a team's share of a project without leaving the project's own view** (BR-MX-16, BR-MX-17; doc-012 US-7, US-11).

## Acceptance criteria

Data: «Альфа» (A) has MARS and VENUS. Its items: the stories ALP-US-12 and ALP-US-14 (MARS's), ALP-US-20 (VENUS's), ALP-US-21 (no team), the feature ALP-FEAT-2 (MARS's), the story ALP-US-30 (MARS's, «Закрыта»), and under ALP-US-12 the tasks ALP-TASK-13 (MARS's) and ALP-TASK-15 (VENUS's). Every other story is «Новая». The level is «Истории». U is an «Участник», V a «Читатель». The filter is the first of the shared specification (BR-OT-8): one meaning of "owned by" on every surface. Team views carry no team filter, because the team is the filter; the project filter of a team view is `feat-040-team-surfaces-across-projects`'s.

```gherkin
Scenario: The list shows each item's team (BR-OT-9)
  When U opens A's «Рабочие элементы»
  Then the columns read «ID», «Тип», «Заголовок», «Исполнитель», «Статус», «Команда», «Область», «Изменён»
  And ALP-US-12's row reads «MARS» and ALP-US-21's «Без команды»

Scenario: The list is filtered by team, by several teams, and by none (BR-OT-8)
  When U opens the filter «Команда» in the band «Фильтры»
  Then it offers «MARS» and «VENUS» by name, then «Без команды», without counts, and nothing of B or of another organization
  When U chooses «MARS»
  Then the list holds MARS's items of every type and state: ALP-US-12, ALP-US-14, ALP-FEAT-2, ALP-TASK-13 and ALP-US-30, and the page's total reads 5
  When U also chooses «Без команды»
  Then ALP-US-21 joins them: an item matches any one chosen value
  When U presses «Очистить фильтры»
  Then the list holds every item of A again

Scenario: A filtered list pages from its first page (BR-OT-8; spec NFR volumes)
  Given MARS owns 120 items of A and VENUS 30
  When U filters by «MARS» from page 3 of the unfiltered list
  Then the first page of 50 shows, the total reads 120, and the last page holds 20
  And changing the filter returns to the first page

Scenario: A filter that matches nothing says so (BR-OT-8)
  Given ALP-US-20 and ALP-TASK-15 are MARS's instead, so VENUS owns no item of A
  When U filters the list by «VENUS»
  Then the frame reads «Ничего не найдено» with «Ни один рабочий элемент не подходит под заданные условия фильтра.» and the action «Сбросить фильтр», which shows every item again

Scenario: A project backlog is filtered as the team's view would show the project (BR-OT-8; feat-027 BR-T-4, BR-T-5)
  When U opens A's «Истории» backlog and filters by «MARS»
  Then it lists ALP-US-12, ALP-US-14 and ALP-US-30, with ALP-TASK-13 beneath ALP-US-12 and not ALP-TASK-15
  And «Порядок» numbers the three top-level rows 1..3, in the project's order
  And a MARS bug whose parent story is VENUS's is a top-level row, its parent being outside the filter, as on MARS's own backlog (BR-T-5)
  And with «Без команды» chosen it lists ALP-US-21 alone

Scenario: A project board is filtered the same way, and a card's checklist is not narrowed (BR-OT-8; feat-032 BR-C-1)
  When U filters A's «Истории» board by «VENUS»
  Then «Новая, рабочих элементов: 1» holds the card of ALP-US-20 and the other columns read 0
  And with «MARS» chosen, ALP-US-12's card keeps ALP-TASK-15 in its checklist and its counter, VENUS's though it is

Scenario: One specification: the three surfaces agree (BR-OT-8; NFR Query shape)
  When U filters A's list, A's «Истории» backlog and A's «Истории» board by «MARS» and «Без команды»
  Then the backlog's and the board's items are exactly the list's items of the types of «Истории» outside Removed, each surface in its own order
  And the same holds for «VENUS» and for every combination of the three values

Scenario: A filtered backlog reorders against the full list (BR-OT-8; doc-003 US-2; BR-MX-18)
  Given A's backlog reads ALP-US-20, ALP-US-12, ALP-US-21, ALP-US-14, ALP-US-30, all placed
  When U filters it by «MARS» and moves ALP-US-12 below ALP-US-14
  Then the unfiltered backlog reads ALP-US-20, ALP-US-21, ALP-US-14, ALP-US-12, ALP-US-30: the items the filter hid keep their relative order
  And MARS's own backlog is untouched

Scenario: A value that is not a team of the project selects nothing and discloses nothing (BR-MX-1; doc-012 US-11)
  When A's list is requested filtered by JUPITER (a team of B), by SATURN (of «Другая»), by a deleted team, and by an id that names nothing
  Then the four answers are identical, status and body: no item, total 0

Scenario: The choice lives with the page's visit and narrows only the viewer's view (BR-OT-8)
  Given U filtered A's board by «MARS»
  When U moves a card, the tab regains focus, or U changes the level to «Фичи»
  Then the board is read again and still filtered by «MARS»
  When U leaves the page and opens it again
  Then no filter is set (remembering a choice per view and user is `feat-013-board-backlog-filter`'s)
  And another user's views of A, and MARS's own, were never narrowed, and no item, team or place changed

Scenario: Everyone who reads may filter (BR-OT-15)
  Given V is an organization «Читатель»
  Then V's filter works as U's, and offers the same options

Scenario: A filter keeps its time budget (spec NFR: volumes, query shape)
  Given A holds 5 000 items, 2 000 of them MARS's
  When U filters the list, A's backlog and A's board by «MARS»
  Then the list's first page arrives within 2 s, and so do the backlog and the board
  And the number of statements is the same filtered as unfiltered, and for 50 items as for 5 000
```

## Owed by later lines

```gherkin
# `feat-003-work-item-search`: the list's state, assignee, keyword filters join this specification without changing «Команда»'s meaning
# `feat-013-board-backlog-filter`: project, assignee, type, state, priority, sprint, date and keyword on the views; remembered per view and user;
#   the project filter of a team view; options after visibility
# `feat-041-project-visibility`: options and totals after what the user may see (doc-012 US-11)
# `feat-028-org-level-teams`: a team's removal from a project takes its filter value out of the project's options
```
