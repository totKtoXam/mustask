# US-10: «Канбан» and «Бэклог» at every level; the aggregates of the organization and the department

- story: US-10
- depends-on: US-1, US-6
- shippable: no — the entries exist at every level only when this story is in; it carries the board and backlog part of `feat-042-area-board-and-overview` (human, gate-2; doc-012 BR-MX-17, US-8)
- status: pending

As an **organization member**, I want **«Канбан» and «Бэклог» in the menu at the organization, the department, the team and the project, the first two gathering the work of many projects**, so that **I follow a whole department or the whole organization without opening each project, each item once and only what I may see**.

## Acceptance criteria

Cast as US-6: «Ромашка», «Логистика» (A, B), «Финансы» (C), D at the top level, E deactivated; teams MARS, VENUS, JUPITER. All items are «Истории», state «Новая» unless stated: MSK-1 (MARS), MSK-2 (VENUS), MSK-3 (no team), BET-1 (MARS), FIN-1 (MARS), DDD-1 (no team, project D), EEE-1 (deactivated E), MSK-4 deleted. Ann «Администратор», Olga «Участник», Vera «Читатель». The switch «Включая дочерние области» and views of a sub-area are not built here (Q-8 A); the overview's figures are `feat-042`'s.

```gherkin
Scenario: The organization's views hold every active project's items, each once (BR-NV-20; BR-MX-16)
  When Olga opens «Бэклог» and then «Канбан» at «Истории» with nothing chosen
  Then both hold MSK-1, MSK-2, MSK-3, BET-1, FIN-1 and DDD-1, each once, and the backlog reads 6 in total
  And every row shows its «Проект» and «Команда», MSK-3 and DDD-1 with «Без команды»
  And MSK-4 and EEE-1 are in no row, card, column count or total, and «Эпики» and «Фичи» hold none of these items
  And a card or a row opens its item in its own project

Scenario: A department's views hold the items of the projects placed in it (BR-NV-20)
  When Olga chooses «Логистика» and opens «Бэклог» and «Канбан»
  Then they hold MSK-1, MSK-2, MSK-3 and BET-1, total 4, and not FIN-1 or DDD-1
  And «Финансы» holds FIN-1 only, and a department with no project says «В департаменте нет проектов» in the views too
  And no switch «Включая дочерние области» is offered, because the department holds project nodes only

Scenario: An aggregate has no order of its own, and says so (BR-NV-20; Q-7 A)
  Given the organization has 120 «Истории» visible to Olga
  When she opens the backlog
  Then it shows 50 rows a page on 3 pages, sorted by creation time and then by identifier, and reads 120 in total
  And every item is on exactly one page, also when she sorts by «Проект», «Команда» or «Статус», whose ties keep the creation-time-then-identifier order
  And a child stands as a row of its own, never beneath its parent
  And there is no drag handle and no reorder command, and the page reads «Порядок задаётся в бэклоге проекта или команды»
  And a reorder in A's or MARS's backlog changes nothing in the aggregate, and no aggregate has a create command

Scenario: A board column holds at most 50 cards (BR-NV-20)
  Given 120 items are «Новая» in the organization's board
  When Olga opens «Канбан»
  Then the column «Новая» shows 50 cards, a command shows the next ones, and the column header reads 120
  And the other columns show their own counts

Scenario: A card move writes the item's state in its own project (BR-NV-20; doc-012 BR-MX-18)
  When Olga moves the card of MSK-1 to «Активна» on the department's board
  Then MSK-1 is «Активна» in A, with its project, owning team and backlog places unchanged, and at the next read it is «Активна» on MARS's board and on A's too
  And Vera's cards offer no move, and a direct request to move one is refused with 403, the item unchanged

Scenario: A chosen team narrows an aggregate as BR-NV-17 says (BR-NV-17, BR-NV-20)
  When Olga has «Логистика» chosen and then chooses MARS
  Then the menu is MARS's, «Бэклог» and «Канбан» hold MSK-1 and BET-1, and the page says «сужено: команда MARS, департамент Логистика»
  And with A also chosen they hold MSK-1 alone, and with MARS and «все» in the department they hold MSK-1, BET-1 and FIN-1
  And a project's or team's backlog narrowed this way keeps its rank order and shows no reorder while narrowed, with the same hint (Q-9 A)

Scenario: The team's and the project's views are as before (BR-NV-20; feat-040)
  When Olga opens MARS's «Бэклог» without a department and A's «Бэклог» without a team
  Then MARS's holds MSK-1, BET-1 and FIN-1 in MARS's rank order with the reorder it had, A's holds MSK-1, MSK-2 and MSK-3 in A's rank order, and their addresses are unchanged

Scenario: A project the user may not see is neither shown nor counted (BR-NV-15; feat-041 owed)
  Given every project is visible to every member until `feat-041-project-visibility`
  When the signature test over the aggregate reads is run
  Then each takes the caller's membership and role as a required argument, and a read without it does not compile
  And a project hidden by feat-041 later leaves rows, cards, column counts, totals and the page count of Olga's views and not Ann's, with no other change in this story

Scenario: Access follows the codes served today (BR-NV-3; mod-019 owed)
  When Vera opens the aggregate views
  Then she reads them and moves no card; per-object codes of mod-019 replace the role bundle without changing a row of this story

Scenario: An empty organization (BR-MX-2)
  Given «Пустая» has no project
  When a member opens «Бэклог» and «Канбан»
  Then each says «Проектов пока нет» with a link to «Проекты», and «Рабочих элементов пока нет» when projects exist and no item does

Scenario: A deactivated project leaves and returns (BR-NV-13)
  When Ann deactivates A, and later restores it
  Then MSK-1, MSK-2 and MSK-3 leave every aggregate view and count for everyone, Ann included, and return with their states and places as they were

Scenario: The first page of a large aggregate comes within 2 s (NFR)
  Given 200 projects with 5 000 items each
  When Olga opens the organization's backlog or board, sorts by «Проект», and filters by a team
  Then the first page and the column counts arrive within 2 s, a title keyword within 5 s, and the number of statements does not grow with the number of projects

Scenario: A missing department answers by level (BR-NV-7)
  When Olga opens the board address of a department that was deleted, or of «Другая»
  Then she sees «Область не найдена», the selectors do not change, and a section the kind lacks answers «Страница не найдена»
```
