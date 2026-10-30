# US-10: «Канбан» и «Бэклог» at the organization and the department; the aggregates keep each project's order

- story: US-10
- line: the second line (M, right after `feat-043-area-project-team-navigation`; its id is issued when it is queued), not this line (OQ-9)
- depends-on: US-1 (merged first), US-6 (same line)
- shippable: no — until it lands the organization and department menus have no «Канбан» and «Бэклог», and the team and project boards and backlogs stay as today; it carries the board and backlog part of `feat-042-area-board-and-overview` (human, gate-2; doc-012 BR-MX-17, US-8), whose "no order of their own" is replaced by Q-7 B
- status: pending

As an **organization member**, I want **«Канбан» and «Бэклог» at the organization and the department, gathering the work of many projects in each project's own order**, so that **I follow a whole department or the whole organization without opening each project, each item once, only what I may see, and the order I set in a project is the order I see there**.

## Acceptance criteria

Cast as US-6, with one more item in B: «Ромашка», «Логистика» (A, B), «Финансы» (C), D at the top level, E deactivated; teams MARS, VENUS, JUPITER. All items are «Истории», state «Новая» unless stated. Project backlogs, top first: A: MSK-3, MSK-1, MSK-2 (MARS owns MSK-1, VENUS MSK-2, MSK-3 none); B: BET-2, BET-1 (MARS owns both); C: FIN-1 (MARS); D: DDD-1 (none); EEE-1 of E; MSK-4 deleted. Ann «Администратор», Olga «Участник», Vera «Читатель». The switch «Включая дочерние области» and views of a sub-area are not built (Q-8 A); the overview's figures are `feat-042`'s.

```gherkin
Scenario: The organization's backlog interleaves each project's own order (BR-NV-20; Q-7 B)
  When Olga opens «Бэклог» at «Истории» with nothing chosen
  Then the rows read BET-2, DDD-1, FIN-1, MSK-3, then BET-1, MSK-1, then MSK-2, total 7, each item once
  And the rule is: the first row of every project (projects by key), then the second row of every project that has one, and so on
  And within each project the rows keep that project's own order, MSK-3 before MSK-1 before MSK-2
  And MSK-4 and EEE-1 are in no row, card, column count or total, and «Эпики» and «Фичи» hold none of these items
  And every row shows its «Проект» and «Команда», MSK-3 and DDD-1 with «Без команды», and opens its item in its own project

Scenario: A department's views hold the items of its projects, in the same interleave (BR-NV-20)
  When Olga chooses «Логистика» and opens «Бэклог»
  Then the rows read BET-2, MSK-3, BET-1, MSK-1, MSK-2, total 5, and not FIN-1 or DDD-1
  And «Канбан» holds the same 5 cards, each in the column of its state, in this order within a column
  And a department with no project says «В департаменте нет проектов» in the views too, and no switch «Включая дочерние области» is offered

Scenario: The interleave counts the rows that pass the filters (BR-NV-20)
  When Olga types a keyword that keeps MSK-1, MSK-2, BET-1 only
  Then the rows read BET-1, MSK-1, MSK-2: the ordinals are counted again over the remaining rows, and each project's own order still holds
  And clearing the filter returns the order of the first scenario

Scenario: Re-sorting keeps the order inside the ties (BR-NV-20)
  When Olga sorts the organization's backlog by «Проект», then by «Команда», then by «Статус», then chooses «Сбросить сортировку»
  Then by «Проект» the rows read BET-2, BET-1, DDD-1, FIN-1, MSK-3, MSK-1, MSK-2 (each project's rows in its own order)
  And by «Команда» MARS's rows come first by name, in the interleave of the first scenario among themselves (BET-2, FIN-1, BET-1, MSK-1), then VENUS's (MSK-2), then «Без команды» last (DDD-1, MSK-3)
  And by «Статус» the rows stand by the process order of the states, ties in the default interleave
  And «Сбросить сортировку» returns to the first scenario's order

Scenario: Paging is total (BR-NV-20)
  Given the organization has 120 «Истории» visible to Olga
  When she reads the backlog page by page
  Then each page holds 50 rows, there are 3 pages, the total reads 120, and every item is on exactly one page, also when sorted by «Проект», «Команда» or «Статус»
  And a child stands as a row of its own, never beneath its parent
  And when a reorder in A's backlog lands between her two page reads, a row may repeat or be skipped, as on any list, and the next read is consistent again

Scenario: An aggregate offers no reorder, and says whose order it shows (BR-NV-20; Q-10 A)
  When Olga looks at the organization's or the department's backlog
  Then there is no drag handle and no reorder command, and the page reads «Порядок каждого проекта сохранён. Менять его можно в бэклоге проекта или команды.»
  And a reorder in A's backlog shows in the aggregate at the next read, MSK-1 moved above MSK-3 reading BET-2, DDD-1, FIN-1, MSK-1, then BET-1, MSK-3, then MSK-2
  And a reorder in MARS's backlog (the team's own rank) changes nothing in an aggregate, and no aggregate has a create command

Scenario: A board column holds at most 50 cards (BR-NV-20)
  Given 120 items are «Новая» in the organization's board
  When Olga opens «Канбан»
  Then the column «Новая» shows 50 cards in the interleaved order, a command shows the next ones, and the column header reads 120
  And the other columns show their own counts

Scenario: A card move writes the item's state in its own project (BR-NV-20; doc-012 BR-MX-18)
  When Olga moves the card of MSK-1 to «Активна» on the department's board
  Then MSK-1 is «Активна» in A, with its project, owning team and backlog places unchanged, and at the next read it is «Активна» on MARS's board and on A's too
  And Vera's cards offer no move, and a direct request to move one is refused with 403, the item unchanged

Scenario: A chosen team narrows as BR-NV-17 says (BR-NV-17, BR-NV-20; Q-9 A)
  When Olga has «Логистика» chosen and then chooses MARS
  Then the menu is MARS's, and its «Бэклог» and «Канбан» hold MARS's items in A and B (MSK-1, BET-1, BET-2) in MARS's own rank order, and the page says «сужено: команда MARS, департамент Логистика»
  And with A also chosen they hold MSK-1 alone in A's order, and with only A chosen and then MARS in the team selector, A's backlog shows no reorder while narrowed and reads «Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.»
  And MARS's «Бэклог» without a department and A's without a team are as today, with the reorder they have

Scenario: A project the user may not see is neither shown nor counted (BR-NV-15; feat-041 owed)
  Given every project is visible to every member until `feat-041-project-visibility`
  When the signature test over the aggregate reads is run
  Then each takes the caller's membership and role as a required argument, and a read without it does not compile
  And a project hidden by feat-041 later leaves rows, cards, column counts, totals and the interleave of Olga's views and not Ann's, the ordinals counted over what she sees, with no other change in this story

Scenario: Access follows the codes served today (BR-NV-3; mod-019 owed)
  When Vera opens the aggregate views
  Then she reads them and moves no card; the per-object codes of mod-019 replace the role bundle without changing a row of this story

Scenario: An empty organization (BR-MX-2)
  Given «Пустая» has no project
  When a member opens «Бэклог» and «Канбан»
  Then each says «Проектов пока нет» with a link to «Проекты», and «Рабочих элементов пока нет» when projects exist and no item does

Scenario: A deactivated project leaves and returns (BR-NV-13)
  When Ann deactivates A, and later restores it
  Then MSK-1, MSK-2 and MSK-3 leave every aggregate view and count for everyone, Ann included, and return in A's own order with their states and places as they were

Scenario: The first page of a large aggregate comes within 2 s, a deep page or a team sort within 5 s (NFR; Q-11 A)
  Given 200 projects with 5 000 items each
  When Olga opens the organization's backlog or board, in the default order or sorted by «Проект», or filtered by a team
  Then the first page and the column counts arrive within 2 s, and the number of statements does not grow with the number of projects
  And a page deep in the list, a sort by «Команда» or «Статус» over the whole organization with no filter, and a title keyword arrive within 5 s, the weaker budget being recorded

Scenario: A missing department answers by level (BR-NV-7)
  When Olga opens the board address of a department that was deleted, or of «Другая»
  Then she sees «Область не найдена», the selectors do not change, and a section the kind lacks answers «Страница не найдена»
```
