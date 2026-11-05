# US-7: Tenant containment, access and volumes

- story: US-7
- depends-on: US-1, US-3, US-4, US-5
- shippable: no — the guard rails of the routes US-1..US-5 bring, in the same MR
- status: pending

As an **organization**, I want **every team view to stay inside my organization, to answer by role in a fixed order and to stay fast at our volumes**, so that **nothing of another tenant leaks through a team's address, and a team across fifty projects is still usable**.

## Acceptance criteria

The common cast of spec § Stories. Rules: BR-TS-13 (with tech-011 m3), BR-TS-7 (tech-011 m2, in US-3), the spec's NFR. "A team-view route" is each of the backlog read, the board read and the reorder at the team's address; the card's reads and writes are the work item routes in the item's project.

```gherkin
Scenario: A non-member learns nothing (BR-TS-13)
  Given Oleg is no member of «Ромашка», and Ira is an instance administrator who is no member either
  When each calls a team-view route of MARS, of an unknown team and of Orion under «Ромашка»
  Then each answer is «Организация не найдена.», identical in status and body whatever the team

Scenario: The answer order (BR-TS-13; mod-016 § 0.1)
  When Vera reorders the backlog of an unknown team under «Ромашка»
  Then the answer is 403 «Недостаточно прав», before any not-found of the team
  When Max reads MARS's backlog of a level the process lacks
  Then the answer is «Уровень бэклога не найден.»
  When Max reads that level of an unknown team
  Then the answer is «Команда не найдена.», the team before the level
  When Max reorders MARS's backlog with a malformed body
  Then the answer is 400 and nothing changes
  When Max reorders it naming an item that left it meanwhile
  Then the answer is 409 «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.»

Scenario: Another organization's ids name nothing on every team-view route (BR-TS-13; tech-011 m3)
  When Max calls each team-view route under «Ромашка» with Orion's id as the team
  Then each answers «Команда не найдена.», identical in status and body to an unknown id
  When Max reads MARS's backlog and board filtered to Zeta's id
  Then each answers like a filter value that names nothing: no item, total 0
  When Max reorders MARS's backlog naming an item of Zeta as the item moved, and then as the one it is placed after
  Then each answers like an unknown item id, as stale, and nothing changes anywhere
  And Orion's backlog in «Другая» is unchanged, and no answer says whether Orion, Zeta or the item exist

Scenario: The project views keep their boundary (BR-TS-13; feat-039 kept)
  When Max calls Alpha's backlog, board and reorder routes with Zeta's id as the project
  Then each answers «Проект не найден.», identical to an unknown project id
  And a team filter of Orion's id on Alpha's backlog selects nothing

Scenario: The roles on a team's views (BR-TS-13; spec § Roles)
  When Ann, Max and Vera each read MARS's backlog and board
  Then each read succeeds and shows the same items
  When Vera reorders MARS's backlog, moves a card on MARS's board, or creates an item in Beta from it
  Then each is refused with 403 «Недостаточно прав», and nothing changes
  And none of these commands is offered to her on the page

Scenario: The route inventory holds the new routes and not the old ones (BR-TS-1, -13; mod-016 BR-M1-33)
  When the route inventory test runs
  Then the three team-addressed routes are listed, each with its code: the two reads `workitems.workitem.read`, the reorder `boards.backlog.reorder`
  And the three project-nested team-view routes are not served and not listed, and the count is unchanged

Scenario: A large team's views stay fast (spec NFR; doc-012 US-7 large views)
  Given an organization at doc-012's NFR volumes, where MARS is linked to 50 projects and owns 5 000 stories across them, and the team's order has places for 4 000 of them
  When Max opens MARS's backlog and MARS's board, unfiltered and filtered to 10 projects
  Then each answer returns within 2 s and holds only MARS's items of the chosen projects
  When Max moves one story to the top of MARS's backlog
  Then the reorder completes within 1 s

Scenario: A team-view read does not grow its statements with its data (spec NFR query shape)
  Given MARS's backlog and board read over 1 project with 10 items, and then over 50 projects with 5 000 items, children 5 levels deep and 10 values in the project filter
  When the number of database statements of each read is counted
  Then it is the same for the small and the large case
```
