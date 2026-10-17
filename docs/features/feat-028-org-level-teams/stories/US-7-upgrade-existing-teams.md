# US-7: Existing teams are upgraded, name collisions included

- story: US-7
- depends-on: US-1, US-3
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories); doc-012 § Existing data
- status: pending

As the **owner of a running stand**, I want **every existing team to become the organization's, linked to the project it was created in, with its people, its items and its order kept**, so that **the upgrade needs no reset and nobody finds their work moved**.

The human waived preservation of existing data (doc-012 § Existing data), so the upgrade may drop rows; this story drops none (BR-OL-13) and says what it remaps. The upgrade is new migrations on top of the merged ones, editing none.

Data before the upgrade, in «Ромашка»: Alpha (ALP) has the teams Platform (created first), Reports and the deleted Old; Beta (BET) has "platform" (created later) and Core; Gamma (GAM), deactivated, has Ops. Platform has the members Max and Vera and owns 12 stories of Alpha, with team places and an order; "platform" owns 3 items of Beta; Alpha's settings name Reports the default team, Beta's name none. «Другая» has project Zeta (ZET) with a team Platform.

## Acceptance criteria

```gherkin
Scenario: Every team is kept and linked to the project it was created in (BR-OL-13)
  When the upgrade runs
  Then every team of «Ромашка» keeps its id, its members, the items it owns, its team places and its order
  And it belongs to «Ромашка», and is linked to its project: Platform and Reports to Alpha, "platform" and Core to Beta, Ops to Gamma
  And each of those links records the upgrade as its maker and the time of the upgrade
  And Old stays deleted and linked to nothing, and its name is not counted in any collision
  And Alpha still marks Reports «По умолчанию», Beta marks no team, and Gamma's link to Ops stays out of every list while Gamma is deactivated and comes back on its restoration
  And no item's «Изменён» moves

Scenario: Nothing visible moves for the people who use the stand (BR-OL-13; mod-017 BR-OT-14's standard)
  When the upgrade has run
  Then Platform's «Истории» backlog at Alpha's address holds the same 12 stories in the same order, and "platform"'s holds the same 3 items of Beta
  And every card shows the same «Команда», every picker offers the teams of its project, and Max and Vera are still in Platform
  And the switcher, the project backlogs and boards and the work item lists answer as before
  And what does change is named: the renamed teams' names (below), and Ops, whose only project Gamma is deactivated, which is now listed in the organization's «Команды» as one of its teams, its page listing no project until Gamma is restored (ADR-0028 rule 3)

Scenario Outline: Teams of one organization that share a name are told apart by their project (BR-OL-13, OQ-1 A)
  Given <teams>
  When the upgrade runs
  Then <after>, and each renamed team keeps its id, members, items, places and link
  Examples:
    | teams                                                                        | after                                                                                  |
    | Platform (Alpha, created first: the smaller id) and "platform" (Beta, created later) | Platform keeps its name and "platform" becomes «platform (BET)»                  |
    | a third team "PLATFORM" in Gamma, deactivated                                | it becomes «PLATFORM (GAM)»                                                            |
    | the organization already has a team named «platform (BET)»                   | the renamed team becomes «platform (BET) 2», and a further one «platform (BET) 3»      |
    | two teams whose name is 100 characters long                                  | the later one's name part is cut so that the whole is at most 100 characters, ending « (BET)» |
    | that cut name is itself taken                                                | the name part is cut further, so that the whole, ending « (BET) 2», is at most 100 characters |
    | Platform in Zeta of «Другая» against Platform of «Ромашка»                   | neither is renamed: a name is unique inside one organization                           |
    | the deleted Old of Alpha and an active "Old" of Beta                         | neither is renamed: a deleted team's name is free                                      |

Scenario: Nothing is dropped, and the counts are logged (BR-OL-13; spec § NFR Observability)
  When the upgrade has run on this data
  Then no team, membership, place, order row, link, item or setting is missing, and the database holds the same count of each as before plus one link per team that is not deleted
  And the log names the number of teams upgraded, the number of links created and the number of teams renamed
  And the MR states "data dropped: none; remapped: the names of teams that collided"

Scenario: An organization with no project upgrades to no team, and a database with no organization migrates (BR-OL-13)
  Given an organization with no project, and so no team
  When the upgrade runs
  Then it is as before, and its administrator can create the first team and link it (US-1, US-3)
  And on a database with no organization at all the migrations apply and change no row

Scenario: An administrator unifies two upgraded teams with this line's own tools (BR-OL-8, BR-OL-9; SC-01 on upgraded data)
  Given Platform of Alpha and «platform (BET)» of Beta are the same real team
  When Ann links Platform to Beta
  And Ann unlinks «platform (BET)» from Beta, naming Platform as the destination
  And Ann deletes «platform (BET)» and adds its members to Platform
  Then Platform is linked to Alpha and Beta and owns the 3 items of Beta, unplaced on its backlog at Beta's address
  And «platform (BET)» is gone, its name is free, and no item was deleted or changed project
```

## Owed by later lines

```gherkin
# `mod-018-area-workspace`: the upgrade of the per-project areas, which are another line's rows
# `mod-019-scoped-access`: nothing to upgrade; grants start empty
```
