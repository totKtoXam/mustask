# US-6: Project visibility hides a project from those who may not see it

- story: US-6
- depends-on: US-3, US-5, US-7
- shippable: no — an analysis story: `feat-041-project-visibility` delivers it, over the links (US-3), grants (US-5) and views (US-7) it decides on
- status: pending

As a **holder of «Управление» on a project**, I want **to set who may see the project at all — the whole organization, the participants
of its areas, or only those granted access to it**, so that **confidential work is not merely read-only for everyone else, but absent**.

## Acceptance criteria

Visibility is decided on the server by the one policy of BR-MX-23, before any data is returned (brief § 10.4 items 1-4, § 14). The
organization admin sees every project (OQ-3, answered A). "Matches the twin" means:
U's answer equals U's answer in North-twin, seeded by the same steps as the Background but without X and its items — rows, order,
totals, page counts, «ещё» flags and figures, ids aside (§ Non-functional requirements; P-4). A surface built after `feat-041-project-visibility`
merges carries its scenario below into the line that builds it: area views and the overview (`feat-042-area-board-and-overview`),
sprints (`mod-007-iterations`), search and filters (feat-003, `feat-013-board-backlog-filter`).

```gherkin
Background:
  Given organization North with team MARS, area Logistics and projects X («Ограниченный») and Y («Организация»), each linked to MARS and to Logistics
  And X and Y each hold 50 stories owned by MARS: some children of a feature of their project, some in MARS's Active sprint, some completed in its Closed sprint (the sprints from `mod-007-iterations` on)
  And U is a «Участник» of North and a member of MARS, holds «Управление» on Logistics and on MARS, and holds no grant on X or Y
  And F, a «Участник» of North and a member of MARS, and R, a «Читатель», hold no grant; P holds «Управление» on Y only; M holds «Изменение» and K «Чтение» on X and on Y

Scenario: A new project starts «Организация» and always has one of three values (BR-MX-21, SC-07)
  When the organization admin creates project Z, linked to nothing
  Then Z's visibility is «Организация», and every member of North sees Z and its items on their next request
  And setting Z's visibility to nothing or to an unknown value is refused, and Z stays «Организация»

Scenario: Only project «Управление» or the organization admin changes it, and it applies on the next request (BR-MX-6, BR-MX-21, BR-MX-23)
  When M, F, R or U — who holds «Управление» on Logistics and on MARS, both linked to Y — sets Y's visibility
  Then each gets 403 «Недостаточно прав», and Y stays «Организация»: a link gives no say in it
  When P sets Y to «Участники областей»
  Then U's next request still shows Y, through Logistics, while F's next request for Y answers «Проект не найден»
  When P sets Y to «Ограниченный»
  Then U's next request for Y answers «Проект не найден» and Y's items leave MARS's board for U, while M, K, P and the admin still see Y
  And no item of Y changed its project, owning team, assignee, sprint or backlog place
  When the organization admin sets Y back to «Организация», U and F see Y and its items as before

Scenario: «Организация» is seen by every member through the organization role (BR-MX-21, BR-MX-23)
  When F and R open Y and save one of its items
  Then both read Y's page, views and items; F's save is kept, and R's gets 403 «Недостаточно прав», not «Проект не найден»

Scenario: «Участники областей» is reached only by a grant on a linked area, or on an area above it (BR-MX-21, BR-MX-22)
  Given project W is «Участники областей», linked to MARS and to Warehouse, a child of Logistics; Transport is Warehouse's sibling
  And four «Участник»s of North hold one grant each: A «Чтение» on Logistics, B «Чтение» on Warehouse, C «Управление» on Transport, and D «Чтение» on W
  When each of them, U, F and the organization admin requests W and saves one of its items
  Then W is visible to the admin, A, B, D and U, of whom A, B and D get 403 «Недостаточно прав» on the save, their «Участник» role adding nothing on W; C and F, though in MARS, get «Проект не найден»
  When Warehouse's «Наследовать доступ» is turned off
  Then the next requests of A and U for W answer «Проект не найден», while B and D still see W
  And with W linked to Logistics instead of Warehouse, B gets «Проект не найден»: a grant never passes up the tree

Scenario: «Ограниченный» is seen only by organization admins and holders of a grant on the project (brief § 10.3, OQ-3)
  When the organization admin, M, K, P, U and F request X and save one of its items
  Then the admin and M read and save, and K reads and gets 403 «Недостаточно прав» on the save
  And P, U and F get «Проект не найден» for both, though Logistics and MARS, which U manages, are linked to X (BR-MX-7)
  When U is granted «Чтение» on X, U's next request reads X; once that grant is removed, the next answers «Проект не найден»

Scenario: An invisible project and its single items answer as a project that does not exist (BR-MX-21, BR-MX-24)
  Given Q is a project id that names nothing in North
  When U requests X's page, settings, grants, work item list, views, assignee picker and item X-1, and the same addresses with Q
  Then every answer for X equals the one for Q: 404 «Проект не найден», with the same code and body
  And so does every write naming X — creating or saving an item, a card move, a reorder, X's visibility, settings or grants, a link or unlink — and nothing changes
  And R writing to Q gets «Проект не найден», not 403: BR-MX-24 retires the 403-before-404 of mod-016 api-contract § 0.1 rows 2-4
  And in the panel, X's addresses and `?workitem=X-1` on MARS's board show the «Проект не найден» frame

Scenario: A name held by an invisible project is refused as taken, and nothing more (mod-002 BR-M1-1)
  When P renames Y to X's name, and then to the name of a project P sees
  Then both are refused with the same answer, saying only that the name is taken, with no id, key or other trait of X

Scenario: The organization's project list (BR-MX-21)
  When U opens «Проекты» of North
  Then X is not listed, and the rows, the total and the pages match the twin

Scenario: The context switcher (BR-MX-28)
  When U opens the switcher, `feat-043-area-project-team-navigation`'s, which merges before this line (doc-013), and each of its groups
  Then X is not offered, and every group matches the twin
  And when U's remembered context is Y and Y turns «Ограниченный», U's next load falls back as for a project that no longer exists, with the same notice

Scenario: A team's and an area's project lists (BR-MX-7)
  When U opens the projects of MARS and of Logistics, and the pages of MARS and Logistics with their counts
  Then X is in neither list, and the lists and counts match the twin

Scenario: Team and project views and their counts (BR-MX-16, BR-MX-17)
  When U opens MARS's and Y's backlogs and boards at every level, with their row and column counts, and X's
  Then no item of X is shown or counted, MARS's and Y's views match the twin, and X's answer «Проект не найден»
  And U's reorder of MARS's stories backlog, naming Y's items only, takes effect as in the twin, is never stale because of X's items, and leaves their relative order (BR-MX-18)

Scenario: Area views and their counts (owed by `feat-042-area-board-and-overview`)
  Given «Включая дочерние области» is on, and X and Y are also linked to Warehouse, a child of Logistics
  When U opens Logistics' stories backlog and board
  Then the first page holds Y's 50 stories, once each, there is no second page, and the totals match the twin (BR-MX-16)

Scenario: The work item list and its total
  When U opens «Рабочие элементы» for Y and in the organization's «Рабочие элементы», which `feat-043-area-project-team-navigation` builds before this line (doc-013)
  Then no item of X is listed, and the rows, the total and the pages match the twin

Scenario: Parent candidates and their «ещё» flag
  When U looks for a parent by "dock" on a story of Y, where exactly 20 features of Y and 5 of X hold that word, and on a story of X
  Then Y's 20 features are offered with no «ещё», as in the twin, and the search on X's story answers «Проект не найден»

Scenario: Parent and child refs and the card's checklist counters
  When U reads Y's feature with its children, and opens its card on MARS's board
  Then its parent and child refs and the card's checklist counters match the twin, and no ref names an item of X

Scenario: The area overview's figures (owed by `feat-042-area-board-and-overview`)
  When U opens Logistics' overview
  Then its projects, teams, participants and counts by status match the twin, and (owed by `mod-007-iterations`) so do its active sprints, and no figure counts X or its items

Scenario: Sprint totals and a closed sprint's outcome (owed by `mod-007-iterations`; BR-MX-14)
  When U opens MARS's Active sprint, its sprint board and the Closed sprint's outcome
  Then their items, item counts and story-point totals match the twin, while the outcome's record still holds X's completed items unchanged, which K still sees

Scenario: Search and autocomplete, wherever they exist (brief § 10.4 item 4)
  When U searches or filters by X's name, X's key, an identifier of X's items or a word only X's titles hold — in the work item list, the view filters and the switcher
  Then nothing of X is found or offered, no project filter lists X, and every answer, counts included, matches the twin

Scenario: A Restricted project linked to two areas and a team stays protected (SC-06, BR-MX-7)
  Given X is also linked to area Finance, and U holds «Управление» on Finance too
  When U opens Logistics, Finance and MARS: their project lists, views and link pickers
  Then X appears nowhere, neither as a linked row nor as an option of an add multiselect, and no link or unlink U sends from these pages removes a link of X
  And K, holding no area or team grant, sees X's items on the views of both areas and of MARS, each once (BR-MX-16)
  And when the organization admin unlinks X from Finance, nothing changes for U or K on Logistics

Scenario: A team in two areas widens nothing (SC-05, BR-MX-7, BR-MX-22)
  Given MARS is linked to Logistics and Finance; PA («Участники областей») is linked to MARS and Logistics, PB («Участники областей») to MARS and Finance
  And G holds «Чтение» on Logistics, H «Чтение» on Finance, and both are members of MARS
  When each opens MARS's backlog and board, and the views of both areas
  Then G sees PA's items and none of PB's, H sees PB's and none of PA's, and each answer equals that of the same data without the other project

Scenario: The «Исполнитель» picker offers only members who can read the item's project (BR-MX-25)
  When M looks for each member of North in «Исполнитель» on an item of X, and on an item of Y
  Then on X only the organization admin, M and K are offered, and P, U, F and R are not
  And on Y every active member is offered, whatever their role, as today (mod-016 BR-M1-26 kept for «Организация»)

Scenario: An assignee who cannot read the project is refused (BR-MX-25)
  When M saves an item of X with P, U, F or R as its assignee
  Then the save is refused with «Этого пользователя нельзя назначить исполнителем.», as for a non-member, and the item is unchanged

Scenario: An assignee who loses access stays on the item, marked (BR-MX-25, BR-MX-26)
  Given an item of X is assigned to K
  When K's grant on X is removed
  Then the item keeps K as its assignee, and the item, the list row, the backlog row and the board card show K with «нет доступа к проекту»
  And M's save of its title, its state or any other field is kept with K unchanged, and clearing it to «Не назначен» is kept
  And K is no longer offered, and K's own request for the item answers «Проект не найден»
  And when K is granted «Чтение» on X again, the mark is gone at the next read
  And an assignee who left North keeps feat-017's «не в организации» instead of this mark
```
