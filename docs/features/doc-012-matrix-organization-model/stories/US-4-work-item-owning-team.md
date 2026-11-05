# US-4: A work item has an optional owning team

- story: US-4
- depends-on: -
- shippable: no — specified for `mod-017-work-item-owning-team`, which ships it together with US-7's team part
- status: pending

As a **user who may change a project's work items**, I want **to see and set the one team responsible for
each item, or none**, so that **each team of a shared project works on its own items, while every item
keeps its project, identifier, assignee and estimates** (SC-02).

## Acceptance criteria

Data: organization «Компания». Project A (key PA) has MARS and VENUS linked and no default team named;
project B (key PB) has JUPITER only; project C has no team; organization «Другая» has the team SATURN.
PA-US-12 is a story of A owned by MARS, «Исполнитель» «Анна Смирнова» (a MARS member), «Оценка в пунктах»
5, third on A's «Истории» backlog, with the child PA-TASK-13 (MARS's). «Ольга Петрова» is a member of
VENUS only. The user is an organization «Участник» and A's visibility is «Организация», unless a scenario
says otherwise. Spec BR-MX-1, BR-MX-9..BR-MX-14, BR-MX-17, BR-MX-18, BR-MX-23, BR-MX-25, BR-MX-26; brief
§ 6 rules 1-10 and its last paragraph, § 11.6.3; SC-02, SC-04, SC-09 (the race only).
Until `feat-028-org-level-teams` merges, a project's linked teams are its own teams; a clause that unlinks
is owed by that line, and the sprint scenarios at the end by `mod-007-iterations` (spec § Stories).

```gherkin
Scenario: Each team holds its own items of a shared project (SC-02)
  Given PA-US-20 is owned by VENUS and PA-US-21 has no owning team
  Then MARS's «Истории» backlog and board hold PA-US-12 and neither of the other two
  And VENUS's hold PA-US-20 only, and A's project «Истории» backlog holds all three (BR-MX-17)

Scenario: «Команда» is shown wherever the item is
  Then PA-US-12's card shows «Команда» «MARS», in the dialog and on the full page
  And A's «Рабочие элементы» list has a «Команда» column reading «MARS» on PA-US-12's row
  And PA-US-12's backlog rows and board cards show «MARS» (their project as well: US-7)
  And PA-US-21 shows «Без команды» in each of those places (brief § 11.6.3)

Scenario: Only the teams linked to the item's project are offered
  When the user opens «Команда» on PA-US-12's card
  Then it offers «Без команды» first, then «MARS» and «VENUS» by name, and nothing else
  And JUPITER (linked to B only), a deleted team and SATURN are not offered

Scenario: A reader sees the team and cannot change it
  Given the user is an organization «Читатель»
  Then «Команда» on PA-US-12's card is a value with no picker, and a save that sets it, sent anyway, is refused with 403 and changes nothing (BR-MX-23)

Scenario: Changing the team moves the item between team views and changes nothing else
  Given every item on VENUS's «Истории» backlog is placed
  When the user sets «Команда» to «VENUS» on PA-US-12 and saves
  Then the card shows «VENUS», and «Изменён» shows the time of this save
  And PA-US-12 keeps its identifier, project A, state, «Исполнитель», «Оценка в пунктах» 5, parent and child (BR-MX-9)
  And it is gone from MARS's backlog and board, and is the last row of VENUS's «Истории» backlog, unplaced
  And it is still third on A's «Истории» backlog: the project's order is untouched (BR-MX-18)
  And a reorder of MARS's «Истории» backlog sent with the order read before the change is refused as stale (mod-006 BR-M4-26)
  And with «Без команды» picked instead, PA-US-12 is on no team's view, and still third on A's backlog

Scenario Outline: A team the item's project does not have is refused, and nothing is stored
  When a save of PA-US-12 that changes «Заголовок» and sets the owning team to <team> is sent
  Then it is refused with <text> under «Команда», the choice kept, and no frame replaces the card
  And PA-US-12 is unchanged: neither its title nor its team was stored (BR-MX-9)

  Examples:
    | team                                                                          | text                              |
    | JUPITER, linked to B only                                                     | «Команда не участвует в проекте.» |
    | VENUS, unlinked from A after the card was read (owed by `feat-028-org-level-teams`) | «Команда не участвует в проекте.» |
    | a team deleted after the card was read                                        | «Команда не найдена.»             |
    | an id that names no team                                                      | «Команда не найдена.»             |

Scenario: Another organization's team gets the answer of a team that does not exist
  When one save of PA-US-12 sets SATURN of «Другая» and another sets an id that names no team
  Then both answers are identical, status and body, and nothing changes in either organization (BR-MX-1)

Scenario Outline: Outside a team surface a new item starts with the project default team
  Given project <project> with <links>
  When the user opens «Новый рабочий элемент» on <project>'s «Рабочие элементы»
  Then the dialog's «Команда» reads <team>, and «Создать» creates the item with it (BR-MX-10, BR-MX-11)

  Examples:
    | project | links                                                   | team          |
    | A       | MARS and VENUS, VENUS named the default in its settings | «VENUS»       |
    | B       | JUPITER only, no default named                          | «JUPITER»     |
    | A       | MARS and VENUS, no default named                        | «Без команды» |
    | B       | JUPITER and NEPTUNE, no default named                   | «Без команды» |
    | C       | no team                                                 | «Без команды» |

Scenario: Created from a team's backlog or board, the item is that team's
  Given A's settings name VENUS the default team
  When the user opens «Новый рабочий элемент» on MARS's «Истории» backlog, for project A (the project choice on a team surface: US-7)
  Then the dialog's «Команда» reads «MARS» and offers «Без команды», «MARS» and «VENUS» only
  And the item created is MARS's and is the last row of MARS's «Истории» backlog; from the board, the same
  And with «VENUS» picked before «Создать» instead, the item is VENUS's and not on MARS's backlog; with
    «Без команды», it is on no team's backlog, only on A's project views

Scenario: A team gone while the dialog was open refuses the creation
  Given the dialog reads «Команда» «MARS», and MARS is deleted before «Создать»
  When the user creates the item
  Then «Команда не найдена.» shows under «Команда», the title is kept, and nothing is created
  And with MARS unlinked from A meanwhile instead (owed by `feat-028-org-level-teams`), «Команда не участвует в проекте.» shows there, and nothing is created

Scenario: A child starts with its parent's team, else the project default
  Given A's settings name VENUS the default team; PA-FEAT-6 is MARS's and PA-FEAT-7 has no owning team
  When a story is added to PA-FEAT-6 from its row (feat-027 US-4), and another to PA-FEAT-7
  Then the first is MARS's and the second VENUS's, both in project A (BR-MX-10)
  And neither a row's add nor a card checklist's add (feat-032 US-3) has a «Команда» field: the child takes this default, even on a card opened over another team's page, and its team is changed afterwards on its card
  And a story added to PA-FEAT-6 while MARS is being unlinked from A (owed by `feat-028-org-level-teams`) takes PA-FEAT-6's team as stored when the story is saved, and is moved with it

Scenario: A child may have another team than its parent, in the same project (brief § 6, last paragraph)
  When the user sets PA-TASK-13's «Команда» to «VENUS» and saves
  Then it is accepted, and PA-US-12 is still MARS's
  When PA-US-12's «Команда» is later set to «Без команды»
  Then PA-TASK-13 is still VENUS's: a team change passes to no child and to no parent
  And PA-TASK-13's «Родитель» is still PA-US-12, and a parent of project B is neither offered nor
    accepted, whatever team the two share (feat-001 BR-H-1)

Scenario: The assignee need not belong to the owning team (brief § 6 rule 5)
  When the user sets «Исполнитель» to «Ольга Петрова», of VENUS only, on PA-US-12 (MARS's) and saves
  Then it is accepted: the picker offered her beside the MARS members, whatever their teams (BR-MX-25)
  And setting «Команда» to «VENUS» later keeps «Исполнитель» «Ольга Петрова»

Scenario: A person moving between teams moves no item (SC-04)
  When «Анна Смирнова» is removed from MARS and added to VENUS
  Then PA-US-12 is still MARS's, with «Исполнитель» «Анна Смирнова» and «Изменён» unmoved (BR-MX-26)

Scenario: A save or a creation that sets a team while it is being unlinked (BR-MX-12; owed by `feat-028-org-level-teams`)
  Given MARS is being unlinked from A with «Без команды» as the destination (US-3)
  When a save setting PA-US-20's «Команда» to «MARS», or a creation with «MARS», overlaps the unlink, in either order
  Then it either commits first and the unlink reassigns it with the others, or it comes after the unlink and is refused
    with «Команда не участвует в проекте.», the item unchanged or not created
  And the unlink is never refused for it, no item of A ends owned by MARS while MARS is not linked to A, and no request answers with a server error

# From here on, owed by `mod-007-iterations` (US-9, US-12). MARS has the Active «Спринт 3», which holds PA-US-12, and
# the Closed «Спринт 2», whose outcome lists the completed PA-US-14 (MARS's, 3 story points); VENUS has the
# Planned «Спринт 5» and the Closed «Спринт 4».

Scenario: Changing the team clears the sprint, and the form says so before saving (BR-MX-13)
  When the user sets «Команда» to «VENUS» on PA-US-12
  Then, before any save, «Спринт» is empty with «Спринт снят: он принадлежит команде «MARS».» under it,
    and its picker offers only VENUS's Planned and Active sprints
  When the user saves
  Then PA-US-12 is VENUS's with no sprint, and «Спринт 3» no longer holds it
  And a save that sets «Спринт 5» together with «VENUS» stores both: PA-US-12 is VENUS's, in «Спринт 5»
  And a save that sets «Без команды» leaves no sprint, and «Спринт» offers nothing while «Команда» is empty

Scenario: A sprint that is not an open sprint of the new team is refused (BR-MX-13, BR-MX-14)
  When a save sets «Команда» «VENUS» together with MARS's «Спринт 3», or with VENUS's Closed «Спринт 4»
  Then it is refused with «Спринт принадлежит другой команде.» or «Спринт завершён: в него нельзя добавить элемент.»
    under «Спринт», and PA-US-12 is unchanged

Scenario: A closed sprint's outcome keeps an item that changed team
  When PA-US-14's «Команда» is set to «VENUS» and saved
  Then PA-US-14 has no sprint, and «Спринт 2»'s outcome still lists it with project A and 3 story points
```
