# US-7: Team and project boards and backlogs over one item collection

- story: US-7
- depends-on: US-4
- shippable: no — an analysis story; `feat-NNN-project-board`, `mod-NNN-work-item-owning-team` (owned items, the project views' team filter) and `feat-NNN-team-surfaces-across-projects` deliver it (spec § Decomposition); a scenario marked owed is verified by the line it names, and the other filters are US-11's
- status: pending

As an **organization member who plans or delivers a team's or a project's work**, I want **a backlog and a board per team over all its projects, and per project over all its teams, reading the same work items**, so that **a team plans across its projects and a project sees all its work, whatever team owns it, with no copies to keep in step**.

## Acceptance criteria

Unless a scenario says otherwise: organization O has projects A, B and C, each with visibility «Организация», and teams MARS and VENUS. MARS is linked to A, B and C, VENUS to A only, and no project names a default team. U is an organization member («Участник») with no grant. The level is «Истории».

```gherkin
# Team views
Scenario: A team's views hold its items of every project (BR-MX-17, BR-MX-27, SC-01, SC-02)
  Given MARS owns 5 stories in A, 10 in B and 7 in C
  And in A VENUS owns 4 stories and 2 stories have no owning team
  When U opens MARS's backlog and MARS's board
  Then each shows 22 items: the 5 of A, the 10 of B and the 7 of C
  And neither shows an item of VENUS or an item with no owning team
  And the project filter offers A, B and C; choosing B shows the 10 of B in MARS's order
  And the address names O, MARS and the level, and places MARS under no project

Scenario: Items of a project the user cannot see are absent (BR-MX-17, BR-MX-21, BR-MX-24, SC-01; owed by `feat-NNN-project-visibility`)
  Given the same items, and C is «Ограниченный» with no grant of U on C
  When U opens MARS's backlog and board, and then C's backlog and board
  Then MARS's backlog and board each show the 15 of A and B
  And the project filter, and «Проект» of a new item started there, offer A and B only
  And each answer, totals included, is identical to the answer for the same data without C
  And C's views answer «Проект не найден», the same as a project that does not exist

Scenario: A view holds its level's items in every state but Removed (BR-MX-17)
  Given MARS owns, in A, a story in «Закрыта», a story in «Удалена» and an epic
  When U opens the «Истории» backlogs of MARS and of A
  Then each shows the story in «Закрыта», and neither the story in «Удалена» nor the epic

Scenario: Rows and cards show their project and owning team (mod-006 BR-M4-14 extended, brief § 11.6.3)
  Given MARS owns story S of B, and story N of B has no owning team
  When U opens the backlogs and boards of MARS and of B
  Then S's row and card show «Проект» B and «Команда» MARS on each view that holds S
  And N's row and card on B's views show «Команда» «Без команды»

Scenario: A card opened on a team view opens in its own project (BR-MX-9, SC-01)
  Given MARS owns story SA of A and story SB of B, both with number 3
  When U opens SB's card from MARS's board and saves a new title
  Then the card shows SB with «Проект» B, the title is written to SB, and SA is unchanged

Scenario: A missing, foreign or unlinked team (BR-MX-1, BR-MX-2, BR-MX-24)
  Given team T of another organization, team D of O, deleted, and team JUPITER of O, linked to no project
  When U opens a backlog or a board of T, of D and of JUPITER under O
  Then T and D answer «Команда не найдена», the same as an id that names nothing
  And JUPITER's views show no item and offer no create command

# Project views
Scenario: A project's views hold every item of the project (BR-MX-17, SC-02, brief § 4)
  Given in A MARS owns 5 stories, VENUS 4, and 2 have no owning team; MARS owns 10 stories in B
  When U opens A's backlog and A's board
  Then each shows the 11 stories of A and none of B
  And MARS's views show its 5 of A, VENUS's its 4, and the 2 are on no team's view

Scenario: A team filter narrows a project view and removes nothing elsewhere (BR-MX-16, SC-02, SC-03)
  Given the stories of A above
  When U filters A's board by team
  Then the filter offers MARS, VENUS and «Без команды», which show 5, 4 and 2 cards
  And meanwhile A's board shows another user all 11, and MARS's board still shows its 5 of A
  And no item, owning team or backlog place has changed

Scenario: Children beneath a row follow the view (feat-027 BR-T-4 kept)
  Given story S of A, owned by MARS, has a task owned by MARS and a task owned by VENUS
  When U opens MARS's backlog and A's backlog
  Then on MARS's only the MARS task stands beneath S, and on A's both tasks do

# One collection, one order per view
Scenario: A team's order and a project's order are independent (BR-MX-18)
  Given the stories of A are S1, S2, S3, all owned by MARS, and both backlogs list them in that order
  When U moves S3 to the top of MARS's backlog
  Then MARS's backlog lists S3, S1, S2 and A's still lists S1, S2, S3
  And moving S1 to the bottom of A's backlog then leaves MARS's order S3, S1, S2
  And each board lists the cards of a column in the order of its own view's backlog

Scenario: Unplaced items sort by creation time, then identifier (BR-MX-18)
  Given MARS's backlog holds placed items, and A's key sorts before B's
  And MARS then comes to own unplaced stories, by project and number: B 2 created at 10:00, A 1 at 10:05, A 3 and B 3 both at 10:10
  When U reads MARS's backlog twice
  Then both reads list the placed items first, then B 2, A 1, A 3, B 3

Scenario: An item that changes team arrives unplaced and keeps its project place (BR-MX-18, mod-006 BR-M4-6, -26)
  Given story S of A is placed first on MARS's backlog and second on A's backlog
  When S's owning team is changed to VENUS
  Then S leaves MARS's views and is unplaced on VENUS's backlog, below every placed item
  And S is still second on A's backlog
  And a reorder of MARS's backlog that moves or names S is refused as stale, and nothing changes

Scenario: A user who cannot see some items reorders among the visible ones (BR-MX-18, BR-MX-21; owed by `feat-NNN-project-visibility`)
  Given MARS's backlog is A1, C1, B1, C2, A2, each named for its project, and C is «Ограниченный» with no grant of U on C
  When U, who sees A1, B1, A2, moves A2 directly after A1
  Then U sees A1, A2, B1
  And a user who sees C sees A1, A2, C1, B1, C2: every item but A2 keeps its relative order
  And a reorder by U that names C1 gets the answer for an item not on this backlog, and nothing changes

Scenario: Who may reorder a team's backlog and a project's backlog (BR-MX-18, BR-MX-22, BR-MX-23; owed by `mod-NNN-scoped-access`)
  Given organization viewers («Читатель») P, with team «Планирование» on MARS, E, with project «Изменение» on A, and R, with no grant
  When each of U, P, E and R reorders MARS's backlog and then A's backlog
  Then on MARS's backlog the reorders of U and P take effect, and those of E and R are refused with 403
  And on A's backlog the reorders of U and E take effect, and those of P and R are refused with 403
  And P's reorder of VENUS's backlog is refused with 403, and no refused reorder changes anything

# Card moves
Scenario: A card move writes only its item's state, and every view shows it (BR-MX-16, BR-MX-18, SC-01)
  Given MARS owns story S of B in «Новая»
  When U moves S's card to «Активна» on MARS's board
  Then S is in «Активна», still in B, owned by MARS, with its identifier and both backlog places unchanged
  And the next read of MARS's and B's backlogs and boards shows S on each exactly once, in «Активна»

Scenario: A move offers only the states of the card's own type (BR-MX-19, mod-006 BR-M4-18, SC-10)
  Given MARS's board shows stories and bugs of A, B and C
  When U opens it and drags a card
  Then the columns are those the organization's process gives the level, and the read changes no item's state
  And only columns that map a state of the card's type in its project's process are drop targets
  And a direct write of a state of another type is refused as mod-006 BR-M4-18 states, and nothing changes

Scenario: A shared board allows a move only where the user may change the item (BR-MX-23; owed by `mod-NNN-scoped-access`)
  Given E is an organization viewer with project «Изменение» on A
  When E opens MARS's board
  Then E can move the cards of A, and not those of B or C
  And E's direct state write to an item of B is refused with 403, and nothing changes

# Creation
Scenario: Creating from a team view sets the team and a project of the team (BR-MX-10)
  When U starts a new item on MARS's backlog with no project filter
  Then «Команда» is MARS, «Проект» offers A, B and C with none chosen, and the item is not saved without one
  And with the project filter on B, «Проект» starts as B; on VENUS's board, linked to A only, it starts as A
  And saved in B, the item takes B's next number, is owned by MARS, and is unplaced on MARS's and B's backlogs
  And if U changes «Команда» to «Без команды» before saving, the item is on B's views and on no team's
  And if MARS is unlinked from B before U saves, the save is refused and no item is created (BR-MX-9, BR-MX-12)

Scenario: Creating from a project view starts with the project default team (BR-MX-10, BR-MX-11)
  Given A names VENUS as its default team
  When U starts a new item on A's board
  Then «Проект» is A, «Команда» is VENUS, and U may change it to MARS or «Без команды» before saving
  And with no default named, a new item of A starts with «Без команды», two teams being linked
  And a new item started on B's board starts with MARS, B's only linked team

Scenario: Large team and project views (spec NFR: volumes, client load)
  Given MARS owns 5 000 stories across A, B and C, and A holds 5 000 stories
  When U opens MARS's backlog, MARS's board, A's backlog and A's board
  Then each answer returns within 2 s and holds only the items of that view
  And each page draws at most 2 000 rows or cards at a time
```
