# US-3: A manager links and unlinks projects, teams and areas

- story: US-3
- depends-on: US-1, US-2, US-4
- shippable: no — an analysis story: `feat-028-org-level-teams` delivers its project–team part, `mod-NNN-area-workspace` its area part
- status: pending

As a **manager of a project, a team or an area** (a holder of «Управление» on it, or the organization admin), I want **to
link and unlink projects, teams and areas from the page of either end**, so that **one team works on several projects and
one project sits in several areas with no copies, and taking a team off a project never leaves its work with an unlinked owner**.

"A manager of both ends" is the organization admin; once `mod-NNN-scoped-access` merges it is also a user holding «Управление»
on both objects of the pair (BR-MX-6), and that line re-runs these scenarios for such a user; removing an area link needs
«Управление» on either end. A clause that needs a grant, a visibility or a sprint is marked with the line that owes it (spec § Stories).

## Acceptance criteria

```gherkin
Background:
  Given organization O with active projects A, B and C, teams MARS, VENUS and JUPITER, and areas Logistics and Finance
  And organization O2 with project Z, team PLUTO and area North

Scenario Outline: A link is made from the page of either end (BR-MX-5, BR-MX-27; brief § 11.6.5-6)
  Given user U is a manager of both <one> and <other>, and they are not linked
  When U opens the page of either of them, types part of the other's name into its searchable multiselect and picks it
  Then <one> and <other> are linked, and each page lists the other end with a way to open that object's own page
  Examples:
    | one            | other     |
    | project A      | team MARS |
    | area Logistics | project A |
    | area Logistics | team MARS |

Scenario: The multiselect offers only what the user may link (BR-MX-1, BR-MX-5, BR-MX-6, BR-MX-21)
  Given A is linked to MARS, and team SATURN is deleted
  When the organization admin searches the team multiselect on A's page
  Then it offers VENUS and JUPITER, and never MARS (linked), SATURN (deleted) or PLUTO (organization O2)
  And a project multiselect never offers a deactivated project or a project of O2
  And (owed by `mod-NNN-scoped-access`) a user holding «Управление» on A and VENUS only is offered VENUS and not JUPITER,
    and one holding «Изменение» but not «Управление» on A sees A's linked teams and areas, each with a way to open it, and no command to link or unlink
  And (owed by `feat-NNN-project-visibility`) a project multiselect never offers a project the user may not see

Scenario: «Управление» on one end creates no link and removes no project–team link (BR-MX-6, BR-MX-23; owed by `mod-NNN-scoped-access`)
  Given U, not an organization admin, holds «Управление» on project A only, and A is linked to MARS and Logistics, and Logistics to MARS
  When U asks, by any request, to link A with VENUS or with Finance, or to unlink A from MARS
  Then each answer is 403, and the links of A, VENUS, Finance, MARS and Logistics are as before
  And «Управление» on VENUS, Finance or MARS alone is refused the same way, from the other end
  But U unlinks A from Logistics, and a holder of «Управление» on Logistics alone unlinks Logistics from MARS: an area link ends on either end's «Управление»
  And the organization admin, holding no grant on any of them, links and unlinks each of these pairs

Scenario: An id of another organization is answered as an id that names nothing (BR-MX-1, BR-MX-24)
  Given user U is the organization admin of both O and O2
  When U asks, in O, to link project A with team PLUTO, area Logistics with project Z, or team MARS with area North,
    from either end, or the same with an id that names nothing
  Then the answers are «Команда не найдена», «Проект не найден» and «Область не найдена», identical for the foreign and the unknown id, and nothing changes in O or O2
  And a member who lacks «Управление» on the end the request is made from gets 403 for a foreign, an unknown and an
    existing other end alike, so no answer tells whether the other end exists

Scenario: Only active ends are linked (BR-MX-5, BR-MX-8, BR-MX-12, BR-MX-24)
  Given project B is deactivated while linked to MARS, and team SATURN is deleted
  When the organization admin asks to link B with VENUS or Finance, to unlink B from MARS, or to link SATURN with A
  Then B answers «Проект не найден» and SATURN «Команда не найдена», and nothing is linked or unlinked
  And B's link to MARS is unchanged (mod-002 BR-M1-20: a deactivated project keeps its links)
  And when linking A with MARS runs at once with MARS's deletion, or A with Logistics with Logistics's deletion, no link to a deleted team or area remains, whichever finishes first

Scenario: Linking a linked pair or unlinking an unlinked one changes nothing (BR-MX-5, BR-MX-20)
  Given user U1 linked project A with team MARS at time T
  When U2, a manager of both ends, links A with MARS again, from either page, and unlinks A from VENUS, or Logistics from C, which are not linked
  Then each answer is success, A and MARS are linked once and the link still records U1 and T, and nothing else changes
  And two requests that link the same unlinked pair at once leave exactly one link

Scenario: SC-01, SC-06 — a linked object stays one object (BR-MX-2, BR-MX-16)
  Given team MARS is linked to no project, and project A, with its work items, is linked to area Logistics
  When a manager of all the ends picks A, B and C together in MARS's project multiselect, and links A to Finance
  Then MARS's page lists A, B and C, each of them lists MARS, and the organization's team list holds MARS once
  And Logistics and Finance each list A, A's page lists both, and the organization's project list holds A once
  And A has the same work items with the same identifiers, and no team, project, area or work item was created

Scenario: Links disclose nothing (BR-MX-7, BR-MX-21; SC-05: no widening through a team; owed by `feat-NNN-project-visibility`)
  Given team MARS and area Logistics are each linked to projects A and R, and user V may see A but not R
  When V opens MARS's page or Logistics's page
  Then the only linked project shown is A, and every count of linked projects is what it would be had R never been linked
  And when V, a manager of MARS, asks to link a project V may not see, the answer is «Проект не найден», as for no project
  And a member of MARS with no grant on R still gets «Проект не найден» at R's address: the link grants nothing

Scenario: A link gives no power over its other end beyond ending the link (BR-MX-6; P-9; owed by `mod-NNN-scoped-access`)
  Given project A is linked to area Logistics and team MARS, and P holds «Управление» on A and nothing on Logistics or MARS
  When P opens A's page
  Then Logistics and MARS are listed with a way to open them, and nothing on A's page renames, moves, deletes or configures them
  And P is offered to unlink Logistics, since an area link ends on either end's «Управление», and is not offered to unlink MARS
  When P asks, by any request, to rename, move or delete Logistics, to switch its «Наследовать доступ», or to rename MARS or change its members or grants
  Then each answer is 403, and Logistics and MARS are unchanged
  And a holder of «Управление» on Logistics or MARS alone gets 403 renaming A or changing its visibility or default team
  And a manager of both A and MARS is offered, on A's page, to unlink MARS as well

Scenario: SC-06 — unlinking an area removes only that link (BR-MX-2, BR-MX-8)
  Given project A is linked to areas Logistics and Finance and to team MARS, MARS is linked to Logistics, and A has items
  When a holder of «Управление» on A or on Logistics unlinks them, from the page of an end they manage
  Then A–Finance, A–MARS and MARS–Logistics stay linked, and A, Logistics, MARS and A's items (their number, identifiers and owning teams) are unchanged
  When a holder of «Управление» on MARS or on Logistics then unlinks them
  Then MARS's project links and the items it owns are unchanged, and Logistics still exists

Scenario: Unlinking a team that owns no items in the project (BR-MX-11)
  Given MARS and VENUS are linked to project A, MARS owns no item of A in any state, and A's settings name MARS as default
  When a manager of A and MARS asks to unlink them, from either page
  Then a confirmation is asked, with no destination to choose; cancelling it changes nothing
  When it is confirmed
  Then the link is removed, A's default-team setting is empty, and A's default team is VENUS, its only linked team; an item of A given MARS after the dialog opened goes to «Без команды»

Scenario: SC-09 — the unlink dialog states the team's items in the project (BR-MX-12)
  Given MARS and VENUS are linked to project A, and MARS owns 3 items of B and 7 of A, in every state category, Removed included
  When a manager of A and MARS asks to unlink them, from either page
  Then the confirmation states that MARS owns 7 items in A and asks for one destination for all of them: VENUS or «Без команды»
  And no team that is not linked to A is offered, and (owed by `mod-NNN-scoped-access`) VENUS is offered whether or not the manager holds a grant on it
  And cancelling it, with or without a destination chosen, leaves the link, the items, their sprints, their backlog places and A's default-team setting as they were

Scenario: SC-09 — confirming reassigns the items in one operation (BR-MX-9, BR-MX-12, BR-MX-13, BR-MX-14, BR-MX-18)
  Given that dialog, A's settings naming MARS as default, and (owed by `mod-007-iterations`) 2 of the 7 items in MARS's
    Planned sprint and 1 in its Closed sprint
  When the manager chooses VENUS and confirms
  Then the 7 items are owned by VENUS and are in no sprint
  And each of them outside the Removed category stands unplaced on VENUS's backlog of its level, after every placed item
  And (owed by `mod-007-iterations`) MARS's Planned sprint no longer holds the 2, and its Closed sprint's outcome record is unchanged
  And A's default-team setting is empty, and MARS is no longer linked to A
  And the 7 keep their project, identifier, assignee, state and estimates, and show the unlink's time as «Изменён»; A has as many items as before, and A's own backlog order is unchanged
  And MARS's 3 items of B and its link to B are untouched, and if the operation fails at any step, none of the above has happened

Scenario: SC-09 — with no other team linked, «Без команды» is the only destination (BR-MX-11, BR-MX-12, BR-MX-17)
  Given MARS is the only team linked to project A and owns 4 items of A
  When a manager of A and MARS asks to unlink them
  Then the confirmation states 4 items and offers «Без команды» alone
  When it is confirmed
  Then the 4 items show «Без команды» as their «Команда», are in no sprint and on no team's views, and stay on A's views, and A has no default team

Scenario: A save that sets the team during an unlink is reassigned with the rest or refused, never left on MARS (BR-MX-9, BR-MX-12)
  Given the unlink dialog of MARS and project A counted 7 items, with VENUS chosen as the destination
  When, after the count and before or while the confirmed unlink runs, a user saves an item of A with «Команда» MARS, or creates an item of A owned by MARS
  Then either that save or creation commits first and the confirmed unlink reassigns it with the others, so VENUS receives 8,
    or it comes after the unlink and is refused because MARS is not linked to A, changing nothing
  And afterwards no item of A is owned by MARS unless MARS is linked to A, and the unlink is never refused because of a racing save
  And a confirmed unlink whose destination is MARS itself or was unlinked from A after the dialog opened is refused with «Команда не участвует в проекте.»,
    one whose destination was deleted, is foreign or is unknown with «Команда не найдена.», and nothing changes
  And two unlinks of MARS and VENUS from A that name each other end with one applied and the other refused, never with a server error

Scenario: Linking the team again does not move the items back (BR-MX-2, BR-MX-12; brief §§ 4.7, 6.9)
  Given MARS was unlinked from project A, its 7 items going to VENUS
  When MARS is linked to A again
  Then the 7 items stay owned by VENUS, MARS owns no item of A, and A's default-team setting stays empty
  And no work item of A was deleted at any point: A's items have the same ids and identifiers as before the unlink
```
