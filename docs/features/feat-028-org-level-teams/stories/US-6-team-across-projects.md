# US-6: A team that works on several projects

- story: US-6
- depends-on: US-3, US-4
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories); doc-012 SC-01 and SC-02, as far as this line reaches
- status: pending

As a **member of an organization**, I want **everything that belongs to a team to behave sensibly when the team works on several projects**, so that **one team keeps one name, one member list and one order of its work, while every project keeps its own items, its own default team and its own views**.

Cast (spec § Stories): MARS is linked to Alpha and Beta, VENUS to Alpha only, JUPITER to Beta only; Gamma is linked to no team. MARS owns the stories ALP-US-1..3 in Alpha and BET-US-1..2 in Beta; VENUS owns ALP-US-4. Max is in MARS. Team views are still read at a project's address, `…/projects/<P>/teams/<T>/backlogs/stories` and `…/boards/stories`, until `feat-040-team-surfaces-across-projects` (BR-OL-11). A team's view across projects is that line's. New copy is proposed and fixed by the design.

## Acceptance criteria

```gherkin
Scenario: One team shows the same name and people wherever it is used (BR-OL-3, BR-OL-11)
  When Ann renames MARS to "Mars"
  Then Alpha's and Beta's «Команды», the page head's team drop-down «Команды проекта» of each, the cards' pickers, the list's filter and the switcher all read "Mars"
  And Max is marked as a member of Mars in Alpha's and in Beta's list of teams, and in no other team's

Scenario: A team's views at a project's address hold its items of that project only (BR-OL-11; mod-017 BR-OT-5; BR-MX-17)
  When Max opens MARS's «Истории» backlog at Alpha's address
  Then it holds ALP-US-1..3 and not BET-US-1..2, and its board does the same
  When he opens MARS's «Истории» backlog at Beta's address
  Then it holds BET-US-1..2 and none of Alpha's
  And Alpha's project backlog holds ALP-US-1..4, whatever their teams, and Beta's holds BET-US-1..2

Scenario: Reordering in one project's view moves nothing in the other (BR-OL-11, BR-MX-18; OQ-3 A)
  Given MARS's «Истории» backlog at Alpha's address reads ALP-US-1, ALP-US-2, ALP-US-3 and at Beta's address BET-US-2, BET-US-1, all placed
  When Max moves ALP-US-3 above ALP-US-1 in MARS's backlog at Alpha's address
  Then Alpha's view reads ALP-US-3, ALP-US-1, ALP-US-2
  And Beta's view still reads BET-US-2, BET-US-1, and the places of BET-US-1 and BET-US-2 are unchanged
  And Alpha's and Beta's project orders and VENUS's order are unchanged
  And a reorder made in Beta's view moves none of Alpha's items, whatever positions their places hold

Scenario: The default team is each project's own (BR-OL-7)
  Given Alpha's settings name MARS, and Beta's name none and link MARS and JUPITER
  When Max creates an item from Alpha's «Рабочие элементы», and another from Beta's
  Then the first starts with MARS and the second with «Без команды»
  When Ann unlinks MARS from Alpha, naming VENUS as the destination
  Then Alpha's setting is empty, and Beta's settings, its links and its items' teams are as before

Scenario: A team's address under a project it is not linked to finds no team (BR-OL-11; mod-002 BR-M1-22)
  When Max opens MARS's backlog or board at Gamma's address, or JUPITER's at Alpha's
  Then each is answered «Команда не найдена.», the same as for a deleted team
  And a reorder sent to either address is answered «Команда не найдена.» for Max (an «Участник») and 403 «Недостаточно прав» for Vera (a «Читатель»), as the order of today's answers has it

Scenario: Creating an item from a team's surface keeps the page's project (mod-017 BR-OT-3)
  When Max opens «Новый рабочий элемент» on MARS's backlog at Beta's address
  Then the dialog's «Команда» reads «MARS», it offers «Без команды», «JUPITER» and «MARS», and the item is created in Beta, MARS's, last on MARS's «Истории» backlog at Beta's address

Scenario: Unlinking a team the switcher holds makes it fall back, with no error (mod-002 DC-7; BR-OL-8)
  Given Max's switcher holds Beta and MARS
  When Ann unlinks MARS from Beta, naming JUPITER as the destination, and Max's page re-reads Beta's teams
  Then the switcher falls back to another team of Beta, as for a deleted team, with the fallback notice, and the page shows no error
  And which team it falls back to is the switcher's rule today (mod-016 BR-M1-27), changed by `mod-020`

Scenario: An item's owning team is chosen among the project's linked teams only (BR-OL-6)
  When Max opens «Команда» on BET-US-1's card
  Then the picker offers «Без команды», JUPITER and MARS, never VENUS
  And on ALP-US-1 it offers «Без команды», MARS and VENUS, never JUPITER
  And setting BET-US-1's team to MARS keeps its project Beta, its identifier BET-US-1 and its assignee, whatever MARS's other projects are

Scenario: The «Команда» filter offers a project's linked teams (BR-OL-6; mod-017 BR-OT-8)
  When Max opens the filter «Команда» on Alpha's «Рабочие элементы», or on its project backlog or board
  Then it offers MARS and VENUS by name, then «Без команды», and never JUPITER
  And a filter value that names JUPITER, an unlinked team, selects nothing and is no error, as for an unknown or foreign id
```

## Owed by later lines

```gherkin
# `feat-040-team-surfaces-across-projects`: MARS's backlog and board addressed by the team alone, over Alpha and Beta together, with a project filter and each row's project;
#   how items of different projects whose places share a position sort in one team order; the project-nested team addresses and the page head's «Весь проект» entry retire
# `mod-007-iterations`: one sprint of MARS holds items of Alpha and Beta; a team's load is counted across its projects
# `mod-009-dashboards-widgets`: a team's dashboard is the organization team's, not a project's
# `mod-018-area-workspace`, `mod-019-scoped-access`, `feat-041-project-visibility`: the same team linked to areas; a team grant reaches nothing of its projects; the projects a user may not see leave the team's lists
# `mod-020`: the switcher's fallback team; the team filter in the address
```
