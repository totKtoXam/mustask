# US-4: A team's views hold the items it owns, a project's views hold all of its items, and each keeps its own order

- story: US-4
- depends-on: US-1
- shippable: no — one of the seven stories of `mod-017-work-item-owning-team`; the field of US-1 decides what a view holds, and this story moves the team views off area ownership
- status: pending

As an **organization member who plans or delivers a team's or a project's work**, I want **a team's backlog and board to hold the items the team owns, and the project's to hold every item of the project, with the owning team shown on each**, so that **each team of a shared project works on its own items, and the project sees all of its work with no copies to keep in step** (BR-MX-16..-18; doc-012 US-4, US-7).

## Acceptance criteria

Data: «Альфа» (A) has MARS and VENUS. Its stories: ALP-US-12 and ALP-US-14 are MARS's, ALP-US-20 is VENUS's, ALP-US-21 has no owning team. MARS's «Истории» backlog lists ALP-US-12, ALP-US-14; A's lists ALP-US-20, ALP-US-21, ALP-US-12, ALP-US-14; all four are «Новая». The level is «Истории». U is an «Участник», V a «Читатель». Team views stay at `…/projects/:p/teams/:t/…` and project views at `…/projects/:p/…` (feat-039); both are project-nested until `feat-040-team-surfaces-across-projects`.

```gherkin
Scenario: Each team holds its own items of a shared project; the project holds all (BR-OT-5; BR-MX-17, SC-02)
  When U opens the backlogs and the boards of MARS, of VENUS and of A
  Then MARS's hold ALP-US-12 and ALP-US-14, and neither of the other two
  And VENUS's hold ALP-US-20 only, and A's hold all four, each once
  And ALP-US-21 is on no team's views
  And a team that owns nothing reads «Бэклог пуст» and «Канбан пуст»

Scenario: Membership no longer follows areas (BR-OT-5, BR-OT-10; BR-MX-9)
  Given ALP-US-12 is in the area «Склад»
  When Ann renames «Склад», moves it under another area, and U saves ALP-US-12's «Область» as «Альфа»
  Then ALP-US-12 stays on MARS's views and on A's, and no item changes team

Scenario: «Команда» is shown on every row and card (BR-OT-7; mod-006 BR-M4-14 and feat-027 BR-T-1 extended)
  When U opens the views of MARS and of A
  Then the backlog's columns read «Иерархия», «Порядок», «Тип», «Заголовок», «Статус», «Команда», «Трудоёмкость», «Бизнес-ценность», «Вид ценности»
  And ALP-US-12's row and card show «MARS», and ALP-US-21's show «Без команды» on A's views
  And V sees the same, and «Проект» is not yet a column or a card line (`feat-040-team-surfaces-across-projects`)

Scenario: A view holds its level's items in every state but Removed (BR-MX-17; the Removed half owed by `mod-005-work-item-delete-restore`)
  Given MARS also owns ALP-US-30 in «Закрыта» and the feature ALP-FEAT-2
  When U opens the «Истории» backlogs of MARS and of A
  Then each lists ALP-US-30 and not ALP-FEAT-2, which stands on the «Фичи» views of MARS and of A

Scenario: Children beneath a row follow the view (BR-OT-5; feat-027 BR-T-4, BR-T-5)
  Given ALP-US-12 has the task ALP-TASK-13, MARS's, and ALP-TASK-15, VENUS's
  When U opens MARS's «Истории» backlog and A's
  Then on MARS's only ALP-TASK-13 stands beneath ALP-US-12, and on A's both do
  And a MARS story whose parent feature is VENUS's is a top-level row of MARS's «Истории», and stands beneath that feature on A's «Фичи»
  And ALP-US-12's card checklist holds ALP-TASK-13 and ALP-TASK-15 whatever their team (feat-032 BR-C-1)

Scenario: Changing the team moves the item between team views and leaves the project's order (BR-OT-6; BR-MX-18)
  Given ALP-US-12 is placed first on MARS's backlog and third on A's, and every item on VENUS's «Истории» backlog is placed
  When U sets ALP-US-12's «Команда» to «VENUS» and saves
  Then it is gone from MARS's backlog and board, and is the last row of VENUS's «Истории» backlog, unplaced, below every placed item
  And it is still third on A's backlog
  And a reorder of MARS's backlog that moves or names ALP-US-12, sent with the order read before the change, is refused with `boards.backlog.reorder_stale` «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.», and nothing changes
  And with «Без команды» picked instead, ALP-US-12 is on no team's views and still third on A's

Scenario Outline: A reorder of a team's backlog and a change of an item's team end in one order (BR-OT-13, BR-OT-6; ADR-0028 rules 5, 8)
  Given ALP-US-12 and ALP-US-14 are MARS's and placed first and second on MARS's «Истории» backlog
  When U moves ALP-US-14 to the top of MARS's backlog while Ann saves <item>'s «Команда» as «VENUS»
  And <first> commits first
  Then <result>
  And no request answers with a server error, and when both have ended no place on MARS's order names <item>
  And with the save first, a move placed directly after the item the save took off the backlog is refused the same way as a move of that item

  Examples:
    | first       | item                           | result                                                                                                                                  |
    | the reorder | ALP-US-14, the item it moves   | the reorder is done, and the save then drops its MARS place: ALP-US-14 stands unplaced below every placed item on VENUS's backlog      |
    | the reorder | ALP-US-12, another member      | the reorder is done, and the save then drops ALP-US-12's MARS place: MARS's backlog lists ALP-US-14 alone                                |
    | the save    | ALP-US-14, the item it moves   | the reorder is refused with `boards.backlog.reorder_stale` «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.» and changes nothing |
    | the save    | ALP-US-12, another member      | the reorder is done on the members that remain, ALP-US-14 first, and writes no place for ALP-US-12                                      |

Scenario: An item that comes back to a team comes back unplaced (BR-OT-6; mod-006 § Model's former place ends)
  Given ALP-US-12 was placed first on MARS's backlog, then given to «VENUS»
  When U sets it back to «MARS» with no reorder of MARS's backlog meanwhile
  Then it stands unplaced on MARS's backlog, below every placed item, not in its former place

Scenario: A team's order and the project's order are independent (BR-OT-6; doc-012 US-7)
  Given MARS owns the stories S1, S2, S3 of A, and both backlogs list them in that order
  When U moves S3 to the top of MARS's backlog
  Then MARS's lists S3, S1, S2 and A's still lists S1, S2, S3
  And moving S1 to the bottom of A's then leaves MARS's order S3, S1, S2
  And each board lists the cards of a column in the order of its own view's backlog

Scenario: A card move writes only the state, and every view shows it once (BR-MX-16, BR-MX-18; doc-012 US-7)
  Given MARS owns ALP-US-12 in «Новая»
  When U moves its card to «Активна» on MARS's board
  Then ALP-US-12 is in «Активна», still A's and MARS's, with its identifier and both its places unchanged
  And the next read of MARS's and A's backlogs and boards shows it on each exactly once, in «Активна»

Scenario: A created item is unplaced on both orders (BR-OT-6; mod-006 BR-M4-24)
  When U creates a story from MARS's backlog
  Then it is the last row of MARS's backlog and of A's, and no place was written on either order

Scenario: Large views and their statements (spec NFR: volumes, query shape)
  Given MARS owns 5 000 stories of A, and A holds 5 000 stories across its teams
  When U opens MARS's and A's «Истории» backlog and board
  Then each answer arrives within 2 s and holds only the items of that view
  And the number of statements a read takes is the same for 50 stories and 50 teams as for 5 000 stories and 2 teams
```

## Owed by later lines

```gherkin
# `feat-040-team-surfaces-across-projects`: a team's views across its projects, the project filter, «Проект» on rows and cards,
#   a card opened in its own project, the creation-time then project-key tie-break, the 2 000 drawn rows (doc-012 US-7)
# `feat-041-project-visibility`, `mod-019-scoped-access`: hidden items leave every view; scoped codes decide a reorder and a move
# `feat-028-org-level-teams`: "of the project" becomes "linked"; an unlink moves the team's items (doc-012 US-3)
# `mod-007-iterations`: the sprint column and filter on the views
```
