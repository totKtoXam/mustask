# US-1: A team's views hold its items of every project, at the team's address

- story: US-1
- depends-on: -
- shippable: no — one breaking L: the team address, the retired project-nested one and the switcher (US-6) change together
- status: pending

As an **organization member who plans or delivers a team's work**, I want **the team's backlog and board to hold the items it owns in every project, at an address that names the team alone**, so that **the team sees all its work in one place, and every row says which project it belongs to**.

## Acceptance criteria

The common cast of spec § Stories. Rules: BR-TS-1, -2, -3.

```gherkin
Scenario: A team's views hold its items of every project (BR-TS-2; doc-012 US-7)
  Given MARS owns 5 stories in Alpha, 10 in Beta and 7 in Gamma
  And in Alpha VENUS owns 4 stories and 2 stories have no owning team
  When Max opens MARS's backlog and MARS's board
  Then each shows 22 items: the 5 of Alpha, the 10 of Beta and the 7 of Gamma
  And neither shows an item of VENUS or an item with no owning team

Scenario: The address names the organization, the team and the level, and no project (BR-TS-1)
  When Max opens MARS's backlog of «Истории» and then its board
  Then the addresses are /organizations/<Ромашка>/teams/<MARS>/backlogs/stories and /organizations/<Ромашка>/teams/<MARS>/boards/stories
  And whatever project the switcher holds, both pages show the same 22 items
  And …/teams/<MARS>/backlogs/epics and …/features show the epics and features MARS owns in the three projects
  And …/teams/<MARS>/backlogs/tasks shows «Страница не найдена»
  And a level the organization's process lacks shows «Уровень бэклога не найден.»

Scenario: A view holds its level's items in every state but Removed (BR-TS-2)
  Given MARS owns, in Beta, a story in «Закрыта», a story in «Удалена» and an epic
  When Max opens MARS's «Истории» backlog
  Then it shows the story in «Закрыта», and neither the story in «Удалена» nor the epic

Scenario: Children beneath a row are the team's own (BR-TS-2; mod-017 BR-OT-5 kept)
  Given story S of Alpha, owned by MARS, has a task owned by MARS and a task owned by VENUS
  When Max opens MARS's «Истории» backlog
  Then only the MARS task stands beneath S
  And Alpha's backlog shows both tasks beneath S

Scenario: Rows and cards show their project and their owning team (BR-TS-3)
  Given MARS owns story S of Beta, and story N of Beta has no owning team
  When Max opens the backlogs and boards of MARS and of Beta
  Then S's row and card show «Проект» «Beta» and «Команда» «MARS» on each of the four views
  And N's row and card on Beta's views show «Проект» «Beta» and «Команда» «Без команды»
  And the card dialog of S shows the same project and team

Scenario: A project's views still hold every item of the project and no other (feat-039 BR-PB-2 kept)
  Given the items of the first scenario
  When Max opens Alpha's backlog and Alpha's board
  Then each shows the 11 stories of Alpha and none of Beta or Gamma

Scenario: A deactivated project's items leave the team's views and come back with their places (BR-TS-2; ADR-0028 rule 9)
  Given the items of the first scenario, MARS's backlog ordered, and Gamma's 7 placed on it
  When Ann deactivates Gamma
  Then MARS's backlog and board show the 15 of Alpha and Beta
  When Ann restores Gamma
  Then MARS's views show 22 items again, and Gamma's 7 stand at their former places

Scenario: A missing, foreign or empty team (BR-TS-2)
  Given team D of «Ромашка», deleted, and team Orion of «Другая»
  When Max opens a backlog or a board under «Ромашка» of D, of Orion, of an unknown id and of "not-a-guid"
  Then each shows «Команда не найдена», the four alike
  When Max opens JUPITER's backlog and board, JUPITER being linked to no project
  Then each shows no item and offers no create command, with an empty state saying the team works on no project (proposed: «Команда не участвует ни в одном проекте»)
  When VENUS owns nothing
  Then VENUS's views show no item and still offer the create command (US-5)
  When Ann deactivates Alpha, VENUS's only project
  Then VENUS's views show what JUPITER's show: JUPITER's empty state, no project to filter by and no create command, and nothing names Alpha or the deactivation

Scenario: The project-nested team addresses are retired (BR-TS-1; ADR-0028 § 7)
  When Max opens /organizations/<Ромашка>/projects/<Alpha>/teams/<MARS>/backlogs/stories
  And …/projects/<Alpha>/teams/<MARS>/boards/stories
  And the same with Delta, with an unknown project, and with an unknown team
  Then each shows «Страница не найдена», with no redirect and nothing of MARS or Alpha in the page
  And the API's former team-view routes under a project answer as any unknown path does, for every method

Scenario: A team page that fails to read keeps its frame (feat-039 BR-PB-8 kept)
  Given MARS's board is shown and its re-read fails
  When the error is shown
  Then the columns, their cards and open checklists stay, with the error above them
```
