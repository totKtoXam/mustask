# US-4: Unlinking a team reassigns the items it owns (SC-09)

- story: US-4
- depends-on: US-3
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories); doc-012 US-3's SC-09 scenarios
- status: pending

As a **manager of both a project and a team** (until `mod-019-scoped-access`, «Администратор»), I want **taking a team off a project to ask where its items there go, and to move them in one step**, so that **no item is ever left owned by a team that no longer works on its project, and no item is deleted**.

Cast (spec § Stories): MARS and VENUS are linked to Alpha. MARS owns 7 items of Alpha, 5 live and 2 «Удалена», and 3 items of Beta, to which it is also linked. Alpha's settings name MARS the default team. VENUS's «Истории» backlog holds 4 placed stories. ALP-US-12 is MARS's, assigned to Anna, 5 points, third on Alpha's project «Истории» backlog. A clause about sprints is owed by `mod-007-iterations`. The dialog is the same from either page: the row action «Отключить от проекта» on Alpha's «Команды» and on the project's row of MARS's page. New copy is proposed and fixed by the design.

## Acceptance criteria

```gherkin
Scenario: The dialog states the team's items in the project and asks for one destination (BR-OL-8, OQ-5 A)
  When Ann chooses «Отключить от проекта» for MARS on Alpha's «Команды»
  Then «Отключение команды от проекта» reads «Команда «MARS» владеет рабочими элементами проекта «Alpha»: 7. Выберите, кому их передать.», the 7 counting every state, «Удалена» included, and not Beta's 3
  And «Передать рабочие элементы» offers «Без команды» and VENUS, never MARS itself, never JUPITER or any team not linked to Alpha
  And no destination is preselected, and «Отключить» is unavailable until one is chosen
  And «Отмена», Escape and «Закрыть», with or without a destination chosen, leave the link, the items, their places and Alpha's default setting as they were

Scenario: Confirming with another team reassigns the items in one operation (BR-OL-8, BR-MX-9, BR-MX-13, BR-MX-18)
  When Ann chooses VENUS and presses «Отключить»
  Then the 7 items are owned by VENUS, and each shows «Команда» «VENUS»
  And each of the 5 live ones is unplaced, below the 4 placed stories on VENUS's backlog of its level, and the 2 «Удалена» are on no backlog
  And MARS's backlog and board at Alpha's address answer «Команда не найдена.» (BR-OL-11), and the 7 are still on Alpha's project backlog and board, where ALP-US-12 is still third
  And they keep project, identifier, assignee, state, estimates, parent and children, «Область» and the project backlog's place, and each shows the time of the unlink as «Изменён»
  And only what MARS owns changes: a child of one of the 7 that another team owns keeps its own owning team, and a parent of one of them keeps its own
  And MARS's 3 items of Beta and its link to Beta are untouched, and no work item of Alpha was deleted
  And Alpha's default setting is empty, and VENUS being the only linked team, Alpha's «Команды» marks it «По умолчанию: единственная команда»
  And Alpha's «Команды» no longer lists MARS, the card's picker offers «Без команды» and VENUS, and a notice reads «Команда отключена от проекта.»
  And (owed by `mod-007-iterations`) MARS's Planned sprints no longer hold the 7, a Closed sprint's outcome record is unchanged, and no item is left in a sprint of a team that does not own it

Scenario: Confirming with «Без команды» (BR-OL-8, BR-MX-10)
  When Ann chooses «Без команды» and presses «Отключить»
  Then the 7 show «Без команды», are on no team's backlog or board, and stay on Alpha's project backlog and board
  And VENUS's backlog is as before, and Alpha's default setting is empty

Scenario: With no other team linked, «Без команды» is the only destination (BR-OL-8, BR-OL-10)
  Given MARS is the only team linked to Alpha and owns 4 of its items
  When Ann chooses «Отключить от проекта» for MARS
  Then the dialog offers «Без команды» alone
  When she confirms
  Then the 4 items show «Без команды», Alpha has no linked team and no default team, and Alpha's views still hold the 4

Scenario: A team that owns nothing in the project needs no destination (BR-OL-8, BR-OL-7)
  Given MARS owns no item of Alpha in any state
  When Ann chooses «Отключить от проекта» for MARS
  Then the dialog reads «Отключить команду «MARS» от проекта «Alpha»?» and offers no destination
  When she confirms
  Then the link is removed and Alpha's default setting is empty
  And an item of Alpha that was given MARS after the dialog opened shows «Без команды», since the request named «Без команды»

Scenario Outline: A write that names the team while it is being unlinked is reassigned or refused, never left on MARS (BR-OL-8; BR-MX-12; mod-017 BR-OT-13)
  Given the dialog counted 7 items and Ann chose VENUS
  When <write> overlaps the confirmed unlink, in either order
  Then it either commits first and the unlink reassigns it with the others, so VENUS receives 8, or it comes after the unlink and <refusal>
  And the unlink is never refused because of it, no item of Alpha ends owned by MARS while MARS is not linked to Alpha, and no request answers with a server error
  Examples:
    | write                                           | refusal                                                                 |
    | Max saves ALP-US-20 with «Команда» MARS         | is refused with «Команда не участвует в проекте.» and changes nothing   |
    | Max creates an item of Alpha with «Команда» MARS | is refused with «Команда не участвует в проекте.» and creates nothing   |
    | Max adds a child beneath a story MARS owns      | is never refused for a team: it takes its parent's team as stored then, else Alpha's default as it stands |

Scenario: A reorder or a default naming that meets the unlink ends in one order (BR-OL-8; mod-017 BR-OT-13 (3); ADR-0028 rule 8)
  Given Alpha's settings name VENUS instead, and Ann's confirmed unlink of MARS naming VENUS runs
  When Max reorders MARS's «Истории» backlog at Alpha's address, or Ivan names MARS the default team of Alpha, in either order with the unlink
  Then a reorder that commits first has its places deleted by the unlink, and one that comes after is refused with «Команда не найдена.» or «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.» and writes no place
  And a naming that commits first is cleared by the unlink, and one that comes after is refused with «Команда не участвует в проекте.», leaving the setting on VENUS
  And no team place names MARS for an item of Alpha, no setting of Alpha names MARS, the unlink is never refused because of either, and no request answers with a server error

Scenario Outline: A destination that is not an active team linked to the project is refused (BR-OL-8)
  When Ann confirms the unlink of MARS naming <destination>
  Then it is refused with <answer>, and the link, the items, their places and Alpha's setting are unchanged
  Examples:
    | destination                                           | answer                                                   |
    | MARS itself                                           | 409 «Команда не участвует в проекте.»                    |
    | VENUS, unlinked from Alpha after the dialog opened    | 409 «Команда не участвует в проекте.»                    |
    | JUPITER, an active team linked to no project          | 409 «Команда не участвует в проекте.»                    |
    | SATURN, deleted after the dialog opened               | 404 «Команда не найдена.»                                |
    | Orion of «Другая», or an id that names nothing        | 404 «Команда не найдена.», identical for both            |
    | no destination at all                                 | 400 «Выберите команду или «Без команды»» under `destination` |

Scenario: Two unlinks that name each other end with one applied (BR-OL-8; spec § NFR Concurrency)
  When Ann unlinks MARS naming VENUS, and Ivan unlinks VENUS naming MARS, at the same moment
  Then one is applied, the other is refused with «Команда не участвует в проекте.», and no request answers with a server error
  And every item of Alpha owned by either team is owned by the team still linked, or by none

Scenario: Linking the team again moves nothing back (BR-OL-8, BR-MX-2; brief §§ 4.7, 6.9)
  Given MARS was unlinked from Alpha and its 7 items went to VENUS
  When Ann links MARS to Alpha again
  Then the 7 stay VENUS's, MARS owns no item of Alpha, and Alpha's default setting stays empty
  And no work item of Alpha was deleted at any point: its items have the same ids and identifiers as before the unlink

Scenario: Only a manager of both ends unlinks (BR-OL-5)
  When Max or Vera asks, by any request, to unlink MARS from Alpha
  Then it is refused with 403 «Недостаточно прав», the link stays, and the pages offer them no «Отключить от проекта»

Scenario: A deactivated project cannot be unlinked from (BR-OL-4; mod-002 BR-M1-20)
  Given Ann deactivated Alpha
  When Ann asks to unlink MARS from Alpha
  Then she is answered «Проект не найден.», and Alpha's link to MARS and the 7 items' owner are unchanged

Scenario: A big unlink is quick and all or nothing (BR-OL-8; spec § NFR)
  Given MARS owns 5 000 items of Alpha
  When Ann confirms the unlink naming VENUS
  Then it completes within 5 s and every one of the 5 000 is owned by VENUS
  And if it fails at any step, MARS is still linked and still owns all 5 000, with their places and Alpha's setting as before
```

## Owed by later lines

```gherkin
# `mod-007-iterations`: the items leave the unlinked team's sprints; a Closed sprint's outcome is unchanged
# `mod-019-scoped-access`: a manager of both ends who holds a grant on neither, and the destination offered whatever grants the manager holds
# `feat-015-work-item-history`: each reassignment is recorded in the item's history
# `feat-041-project-visibility`: the count and the destinations show only what the manager may see
```
