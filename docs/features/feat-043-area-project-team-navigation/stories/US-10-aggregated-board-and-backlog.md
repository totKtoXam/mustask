# US-10: «Канбан» и «Бэклог» at the organization and the department; one group per project

- story: US-10
- line: the second line (M, right after `feat-043-area-project-team-navigation`; its id is issued when it is queued), not this line (OQ-9)
- depends-on: US-1 (merged first), US-6 (same line)
- shippable: no — until it lands the organization and department menus have no «Канбан» and «Бэклог», and the team and project boards and backlogs stay as today; it carries the board and backlog part of `feat-042-area-board-and-overview` (human, gate-2; doc-012 BR-MX-17, US-8), whose "no order of their own" is replaced by one group per project in the project's own order (Q-10, human)
- status: pending

As an **organization member**, I want **«Канбан» и «Бэклог» at the organization and the department, gathering the work of many projects as one collapsible group per project, each in its project's own order**, so that **I follow a whole department or the whole organization without opening each project, each item once, only what I may see, and the order I set in a project is the order I see in its group**.

## Acceptance criteria

Cast as US-6, with one more item in B: «Ромашка», «Логистика» (A, B), «Финансы» (C), D at the top level, E deactivated; teams MARS, VENUS, JUPITER. All items are «Истории», state «Новая» unless stated. Project backlogs, top first: A (MSK): MSK-3, MSK-1, MSK-2 (MARS owns MSK-1, VENUS MSK-2, MSK-3 none); B (BET): BET-2, BET-1 (MARS owns both); C (FIN): FIN-1 (MARS); D (DDD): DDD-1 (none); EEE-1 of E; MSK-4 deleted. Project names order A, B, C, D by name. Ann «Администратор», Olga «Участник», Vera «Читатель». The switch «Включая дочерние области» and views of a sub-area are not built (Q-8 A); the overview's figures are `feat-042`'s. Board scenarios assume Q-12 A (a swimlane per project), paging scenarios Q-13 A (a cursor).

```gherkin
Scenario: The organization's backlog is one group per project (BR-NV-20; Q-10, human)
  When Olga opens «Бэклог» at «Истории» with nothing chosen
  Then she sees four groups in the order of the project names, A, B, C, D, each header with the project's name, key and row count (3, 2, 1, 1)
  And the first group (A) is open on this first visit and shows MSK-3, MSK-1, MSK-2 in A's own order; the others are closed
  And MSK-4 and EEE-1 are in no group, count or row, and «Эпики» and «Фичи» hold none of these items
  And every row shows its «Команда», MSK-3 and DDD-1 with «Без команды», and opens its item in its own project
  And the columns are not sortable and there is no merged list, no ordinal and no hint about another order

Scenario: Opening a group reads that project (BR-NV-20; Q-11, human)
  When Olga opens the group B
  Then BET-2 and BET-1 appear in B's own order, from one read of B alone
  And closing and reopening it shows the same rows, and the header counts did not change
  And the counts of all four headers came from one grouped read when the page opened, with no read per project

Scenario: A department's views hold the groups of its projects (BR-NV-20)
  When Olga chooses «Логистика» and opens «Бэклог»
  Then she sees two groups, A (3) and B (2), and not C or D
  And «Канбан» shows two swimlanes, A and B, with 5 cards in all, each in the column of its state
  And a department with no project says «В департаменте нет проектов» in the views too, and no switch «Включая дочерние области» is offered

Scenario: Filters change the counts and drop the empty groups (BR-NV-20)
  When Olga types a keyword that keeps MSK-1, MSK-2 and BET-1 only
  Then the groups read A «2 из 3» (MSK-1, MSK-2, in A's order) and B «1 из 2» (BET-1), and C and D have no group
  And the open or closed state of A and B is as it was, and clearing the filter returns the four groups
  And a filter that keeps nothing says «Ничего не найдено»

Scenario: Open groups are remembered, and the number is bounded (BR-NV-20)
  Given Olga has opened A and B and left the page
  When she returns to the same view in the same browser
  Then A and B are open and C and D closed
  And with 20 groups left open only 10 are restored, in the order of the headers, the rest closed, and the reads of those 10 run at most 4 at a time
  And «Свернуть все» closes every group, and no «Развернуть все» is offered

Scenario: Paging inside a group is a cursor, forward only (BR-NV-20; Q-13 A)
  Given A holds 120 «Историй» visible to Olga
  When she opens A
  Then 50 rows show in A's order and «Показать ещё» offers the next 50, then the last 20, the header reading 120 throughout
  And there is no numbered pager and no jump to a page, and a child stands as a row of its own, never beneath its parent
  And when someone moves a row of A elsewhere between her two reads, no row is shown twice and no row after the cursor is skipped because its neighbours shifted: a row moved from above the cursor to below it appears once on a later page, one moved from below the cursor to above it is not shown until she reopens the group
  And a cursor whose last row was deleted still works, and a malformed cursor is refused with the existing validation error (400)

Scenario: A move inside a group changes that project's rank (BR-NV-20; Q-10 A, for the human's confirmation at gate-2)
  When Olga drags MSK-1 above MSK-3 in the group A with no filter and no team or department narrowing it
  Then A's own backlog reads MSK-1, MSK-3, MSK-2, under the code of A's «Бэклог» (the role bundle until mod-019), and the group shows the same at the next read
  And the other groups and the other projects' ranks are unchanged, and a reorder in MARS's backlog (the team's own rank) changes nothing in a group
  And a drop outside the group, on another group's header or rows, is refused and nothing changes
  And Vera's rows offer no drag handle, and a direct request to move one is refused with 403
  And a reorder made in A's backlog by someone else shows in the group at the next read

Scenario: A narrowed group offers no reorder (BR-NV-17, BR-NV-20; Q-9 A)
  When Olga types a keyword, or chooses MARS in the team selector, with the group A open
  Then A's rows keep A's rank order, there is no drag handle, and the page reads «Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.»
  And choosing «все» or clearing the filter brings the handles back

Scenario: A board has one swimlane per project (BR-NV-20; Q-12 A)
  Given 120 items are «Новая» in A
  When Olga opens «Канбан» at the organization
  Then the lanes are A, B, C, D in the order of the project names, the columns are the organization process's states, and each lane header shows the project and its card count
  And the cell A × «Новая» shows 50 cards in A's rank order, «Показать ещё» shows the next 50 by cursor, and the column header «Новая» reads the count of all projects
  And a lane opens and closes, is remembered and is dropped when a filter empties it, as a backlog group does, and the cards of a closed lane are not read

Scenario: A card move writes the item's state in its own project (BR-NV-20; doc-012 BR-MX-18)
  When Olga moves the card of MSK-1 to «Активна» on the department's board, inside lane A
  Then MSK-1 is «Активна» in A, with its project, owning team and backlog places unchanged, and at the next read it is «Активна» on MARS's board and on A's too
  And a card cannot be dropped into another project's lane, and the board has no reorder
  And Vera's cards offer no move, and a direct request to move one is refused with 403, the item unchanged

Scenario: A chosen team narrows as BR-NV-17 says (BR-NV-17, BR-NV-20; Q-9 A)
  When Olga has «Логистика» chosen and then chooses MARS
  Then the menu is MARS's, and its «Бэклог» and «Канбан» hold MARS's items in A and B (MSK-1, BET-1, BET-2), one group per project, A «1 из 3» and B «2 из 2», each in its project's rank order, and the page says «сужено: команда MARS, департамент Логистика»
  And with A also chosen the view is A's own backlog narrowed to MSK-1, with no reorder while narrowed, reading «Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.»
  And MARS's «Бэклог» without a department and A's without a team are as today, with the reorder they have

Scenario: A project the user may not see is neither shown nor counted (BR-NV-15; feat-041 owed)
  Given every project is visible to every member until `feat-041-project-visibility`
  When the signature test over the aggregate reads is run
  Then each takes the caller's membership and role as a required argument, and a read without it does not compile (the grouped read, the group read and the cell read)
  And a project hidden by feat-041 later leaves the groups, lanes, headers and column counts of Olga's views and not Ann's, with no other change in this story

Scenario: Access follows the codes served today (BR-NV-3; mod-019 owed)
  When Vera opens the aggregate views
  Then she reads them and moves no card and no row; the per-object codes of mod-019 replace the role bundle without changing a row of this story

Scenario: An empty organization (BR-MX-2)
  Given «Пустая» has no project
  When a member opens «Бэклог» and «Канбан»
  Then each says «Проектов пока нет» with a link to «Проекты», and «Рабочих элементов пока нет» when projects exist and no item does

Scenario: A deactivated project leaves and returns (BR-NV-13)
  When Ann deactivates A, and later restores it
  Then A's group, lane and its items leave every aggregate view and count for everyone, Ann included, and return in A's own order with their states and places as they were

Scenario: The page comes within the budget and reads stay bounded (NFR; Q-11, human)
  Given 200 projects with 5 000 items each
  When Olga opens the organization's backlog or board, unfiltered or filtered by a team, an assignee or a state
  Then the headers' counts (and the column counts) arrive within 2 s from one grouped read, and the first open group's 50 rows within 2 s of its read, the first screen within 4 s
  And each further group or «Показать ещё» arrives within 2 s from its own read of one project
  And the statements for opening the page are the same at 5 and at 200 projects, the statements for opening one group are the same whatever its volume, and no read returns a project's whole backlog
  And with 10 groups restored never more than 4 reads are in flight from the tab, which protects the shared connection pool, the page limit and the browser's connection limit
  And a title keyword on the counts arrives within 5 s, the weaker budget being recorded

Scenario: A missing department answers by level (BR-NV-7)
  When Olga opens the board address of a department that was deleted, or of «Другая»
  Then she sees «Область не найдена», the selectors do not change, and a section the kind lacks answers «Страница не найдена»
```
