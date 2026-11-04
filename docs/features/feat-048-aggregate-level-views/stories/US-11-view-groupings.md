# US-11: The grouping switch on «Бэклог», «Канбан» and «Рабочие элементы»

- story: US-11
- depends-on: US-6, US-10 (F43, built in this line)
- shippable: no — ships with US-6 and US-10 in this line; it amends BR-NV-20 (BR-AV-1..12)
- status: pending

As an **organization member**, I want **to group the backlog, the board and the work-item list by department, project or team, or not at all**, so that **I see the same work cut the way my question needs, each item once, without opening each project**.

## Acceptance criteria

Data as US-10 (F43): «Ромашка», «Логистика» (A, B), «Финансы» (C), D at the top level, E deactivated; teams MARS, VENUS. Items, change time newest first: MSK-2 (VENUS), DDD-1 (none), BET-2 (MARS), MSK-1 (MARS), FIN-1 (MARS), MSK-3 (none), BET-1 (MARS). Project ranks as US-10: A: MSK-3, MSK-1, MSK-2; B: BET-2, BET-1. Ann «Администратор», Olga «Участник».

```gherkin
Scenario: A level offers only the groupings that give more than one group (BR-AV-2)
  When Olga opens «Бэклог» with nothing chosen
  Then «Группировка» offers «Без группировки», «По департаменту», «По проекту», «По команде», and no option for the organization
  And with «Логистика» chosen it offers the same minus «По департаменту»; with MARS chosen, only «Без группировки» and «По проекту»
  And with a project chosen and MARS chosen, the control is absent

Scenario: The default and the memory (BR-AV-3)
  When Olga opens «Бэклог» and «Рабочие элементы» for the first time with nothing chosen
  Then «Бэклог» is grouped «По проекту» and «Рабочие элементы» reads «Без группировки»
  When she chooses «По команде» in «Бэклог» and reloads the page
  Then «Бэклог» is grouped «По команде», «Рабочие элементы» is still «Без группировки», and another organization keeps its own choice

Scenario: By department, «Без департамента» first (BR-AV-4, BR-AV-5)
  When Olga chooses «По департаменту» in «Бэклог»
  Then she sees «Без департамента» (1), «Логистика» (5) and «Финансы» (1) in that order, each count from one grouped read
  And «Логистика» shows MSK-2, BET-2, MSK-1, MSK-3, BET-1, the rows by change time
  And EEE-1 of E is in no group

Scenario: By team, «Без команды» first (BR-AV-4)
  When Olga chooses «По команде»
  Then she sees «Без команды» (2), MARS (4) and VENUS (1) in that order, «Без команды» first
  And MARS shows BET-2, MSK-1, FIN-1, BET-1 and «Без команды» shows DDD-1, MSK-3

Scenario: Outside by project there is no reorder (BR-AV-6)
  When Olga has «По команде» or «Без группировки» chosen in «Бэклог» at the organization
  Then no row has a move menu or a drag handle
  And the page says «При этой группировке строки идут по времени изменения, порядок не меняется. Выберите «По проекту», чтобы менять его.» once under the header
  And with «Без группировки» one list of seven rows is shown by change time, MSK-2 first, without headers

Scenario: By project keeps each project's rank and the move (BR-AV-6)
  Given Ann has «Бэклог» grouped «По проекту» with nothing chosen
  When she moves MSK-2 to the top of the group A
  Then the group A reads MSK-2, MSK-3, MSK-1 at the next read
  And a team or a filter that narrows the group shows the Q-9 hint and no move

Scenario: The board in lanes, and as one board (BR-AV-7)
  When Olga opens «Канбан» at «Истории» and chooses «По команде»
  Then she sees the lanes MARS (4 cards), VENUS (1) and «Без команды» (2), each card in the column of its state
  And a card moves between columns inside its lane only, and a card cannot be dropped into another lane
  And with «Без группировки» the board has no lanes: seven cards in the columns of their states, newest first in each

Scenario: «Рабочие элементы» grouped is a different shape from the flat list (BR-AV-8)
  When Olga chooses «По проекту» in «Рабочие элементы»
  Then the groups open and close, each with «Показать ещё», the rows by change time, and there is no pager and no sortable column
  And choosing «Без группировки» returns the paged list with its pager, total and the fixed order of BR-WS-12, the filters unchanged

Scenario: An item that changes team moves group at the next read (BR-AV-10)
  Given «Бэклог» is grouped «По команде» with MARS open showing MSK-1
  When someone assigns MSK-1 to VENUS and Olga reopens the page
  Then MARS counts 3, VENUS counts 2 and MSK-1 is in VENUS, never in two groups at once
  And a group that lost its last row (a department emptied by moving its project) has no header and is not restored from memory

Scenario: Every grouping goes through the limiter (BR-AV-9)
  Given Olga restored 10 open groups under any of the four groupings
  Then no more than 4 group reads are in flight in the tab, a refusal is handled as OQ-1 A says, and the cursor under change-time order shows no row twice
```
