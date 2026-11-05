# US-3: One team order across projects

- story: US-3
- depends-on: US-1
- shippable: no — the order of the cross-project views US-1 brings, in the same MR
- status: pending

As a **member who plans a team's work**, I want **one order of the team's backlog over all its projects, kept apart from each project's order**, so that **the team ranks its work across projects without moving any project's own plan**.

## Acceptance criteria

The common cast of spec § Stories. Rules: BR-TS-6, BR-TS-7. Identifiers are `<project key>-<type key>-<number>` (mod-003 BR-M3-5): ALP-US-1 is item number 1 of Alpha, a story.

```gherkin
Scenario: Unplaced items sort by creation time, then project key, then number (BR-TS-6; doc-012 US-7)
  Given MARS's backlog holds placed items
  And MARS then comes to own unplaced stories created: BET-US-2 at 10:00, ALP-US-1 at 10:05, ALP-US-3 and BET-US-3 both at 10:10, ALP-US-10 and ALP-US-9 both at 10:15
  When Max reads MARS's backlog twice, and MARS's board
  Then both reads list the placed items first, then BET-US-2, ALP-US-1, ALP-US-3, BET-US-3, ALP-US-9, ALP-US-10
  And each board column lists its cards in that order

Scenario: Equal positions left by the per-project reorders sort the same way (BR-TS-6, -14; feat-028 OQ-3 A)
  Given before this line MARS's backlog was reordered in Alpha's view and in Beta's view, so ALP-US-4 and BET-US-7 hold the same position
  And BET-US-7 was created before ALP-US-4
  When Max reads MARS's backlog
  Then BET-US-7 stands directly before ALP-US-4, and every read gives the same order
  When Max then moves any item of MARS's backlog
  Then every item on it holds a position of its own, and the order shown before the move is kept but for the moved item

Scenario: A reorder ranks items of several projects and moves no project's order (BR-TS-7; BR-MX-18)
  Given MARS's backlog lists ALP-US-1, ALP-US-2, BET-US-1, GAM-US-1, and Beta's own backlog lists BET-US-1 first
  When Max moves GAM-US-1 directly after ALP-US-1
  Then MARS's backlog lists ALP-US-1, GAM-US-1, ALP-US-2, BET-US-1, and so does every read after
  And Alpha's, Beta's and Gamma's own backlogs keep their orders
  And MARS's board lists the cards of each column in the new order

Scenario: A reorder naming an item that is not on the backlog is stale and discloses nothing (BR-TS-7)
  When Max's reorder of MARS's backlog moves or places after an item owned by VENUS, an item of Gamma while Gamma is deactivated, an item of MARS in «Удалена», an item of Zeta, or an unknown id
  Then each is refused with «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.», the five alike in status and body
  And the backlog is read again and nothing has changed

Scenario: A deactivated project's places are kept by a reorder (BR-TS-7; ADR-0028 rule 9)
  Given MARS's backlog lists ALP-US-1, GAM-US-1, BET-US-1, and Ann deactivates Gamma
  When Max moves BET-US-1 to the top
  Then MARS's backlog lists BET-US-1, ALP-US-1
  When Ann restores Gamma
  Then GAM-US-1 is back on MARS's backlog with the stored position it had before the deactivation, unchanged by Max's move
  And it stands by that position among the positions Max's move wrote, a tie sorting by BR-TS-6, so every read gives the same order of the three
  And Gamma's own backlog has its former order

Scenario: Who may reorder a team's backlog (BR-TS-13; feat-039 BR-PB-5)
  When Ann, Max and Vera each move an item of MARS's backlog
  Then Ann's and Max's moves take effect
  And Vera's is refused with 403 «Недостаточно прав», the drag is not offered to her, and nothing changes

Scenario: A reorder against an unlink, a deletion or a team change ends in one order (BR-TS-7; mod-017 BR-OT-13 (3); feat-028 BR-OL-8, -9)
  Given Max reorders MARS's backlog of 30 items across Alpha, Beta and Gamma
  When at the same moment Ann unlinks Beta from MARS, choosing «Без команды»
  Then either the reorder commits first and the unlink then deletes the places of Beta's items, or the unlink commits first and the reorder is refused as stale or completes on Alpha's and Gamma's items
  When at the same moment Ann deletes MARS instead
  Then either the reorder commits first and the deletion removes every place, or it is refused with «Команда не найдена.»
  When at the same moment a save changes ALP-US-2's team to VENUS instead
  Then the reorder's place for ALP-US-2 does not survive, and ALP-US-2 is unplaced on VENUS's backlog
  When at the same moment Ann deactivates Gamma instead, and later restores it
  Then the reorder completes on the items it reads, or is refused as stale if the item it moves or the one it is placed after is Gamma's, and Gamma's items keep a place on MARS's backlog either way
  And in every case no request ends in a server error and no team place names a team that does not own its item

Scenario: A read that meets the team's deletion is never a server error (BR-TS-7; tech-011 m2)
  Given Max reads MARS's backlog and MARS's board while Ann deletes MARS
  Then each read answers either the view as it was before the deletion or «Команда не найдена.»
  And never a server error, whichever commits first
```
