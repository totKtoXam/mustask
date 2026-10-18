# US-7: The upgrade drops every existing team

- story: US-7
- depends-on: US-1, US-2, US-3
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories); doc-012 § Existing data
- status: pending

As the **owner of a running stand**, I want **the upgrade to clear every team the per-project model made, and leave the work items without a team**, so that **the organization's teams start from an empty slate that an administrator fills once, against the real structure, and no item is left owned by a team that is gone**.

The human waived preservation of existing data (doc-012 § Existing data) and at gate-1 chose to use the waiver in full (OQ-1 C, 2026-10-17): per-project teams that merely share a name are not known to be one team, so none is kept, renamed or merged. The upgrade is new migrations on top of the merged ones, editing none (BR-OL-13). It drops teams and what hangs on them and remaps nothing.

Data before the upgrade, in «Ромашка» (Ann «Администратор», Max «Участник», Vera «Читатель»): Alpha (ALP) has the teams Platform, Reports and the deleted Old; Beta (BET) has "platform" and Core; Gamma (GAM), deactivated, has Ops. Platform has the members Max and Vera and owns 12 stories of Alpha, with team places and an order; Reports owns 4 more items of Alpha; "platform" owns 3 items of Beta; Ops owns 1 item of Gamma; 2 further items of Alpha are owned by no team. Alpha's settings name Reports the default team, Beta's name none. «Другая» has project Zeta (ZET) with a team Platform that owns 2 items. New interface copy is the design's.

## Acceptance criteria

```gherkin
Scenario: Every team is dropped with what hangs on it (BR-OL-13)
  When the upgrade runs
  Then «Ромашка» and «Другая» have no team: Platform, Reports, "platform", Core, Ops and the deleted Old, and Zeta's Platform, are gone
  And no team membership remains: Max and Vera are no longer in Platform, and both are still members of «Ромашка» with the roles they had
  And no team backlog place and no team backlog order remains
  And Alpha's default-team setting, which named Reports, is empty, and so are Beta's and Zeta's
  And no link between a project and a team exists

Scenario: Every item that had an owning team becomes «Без команды», and nothing else about it moves (BR-OL-13; BR-MX-9, BR-MX-10)
  When the upgrade has run
  Then the 12 stories and the 4 other items of Alpha, the 3 items of Beta, Ops's item of Gamma and the 2 items of Zeta show «Без команды»
  And each keeps its project, identifier, assignee, state, estimates, parent and children, «Область» and its place on the project backlog
  And no item's «Изменён» moves: the upgrade is no actor's edit, as mod-017 BR-OT-14's was none
  And the 2 items of Alpha that were already «Без команды» are as before
  And no item is deleted or changes project, and when Gamma is restored its item is «Без команды» too, owned by no team that is gone

Scenario: Nothing but teams is dropped, and the counts are logged (BR-OL-13; spec § NFR Observability)
  When the upgrade has run on this data
  Then the database holds the same count as before of organizations, organization memberships, projects (Gamma included), areas, work items and project backlog places
  And the log names, per kind, how many it dropped: 7 teams, 2 team memberships, the team places, the team order rows, 1 default-team setting, and 22 items made «Без команды»
  And the MR states "data dropped: every team with its members, its backlog places and order, the projects' default-team settings, and the owning team of every item; remapped: none"

Scenario: What a user sees: no team anywhere until an administrator creates one (BR-OL-10; OQ-2 A; US-1, US-2)
  Given the upgrade has run
  When Ann opens the organization's «Команды»
  Then the page reads «В организации пока нет команд» and offers her «Новая команда»
  When Max opens it
  Then it reads «Команды создаёт администратор организации.» and offers no action
  When Max opens Alpha's «Команды»
  Then it reads «В проекте пока нет команд.» and offers «Бэклог проекта» and «Доска проекта», which open Alpha's backlog and board at `…/projects/<Alpha>/backlogs/stories` and `…/boards/stories`
  And those hold all 18 items of Alpha in the project's own order, as before the upgrade
  And the switcher holds no team in any project, «Бэклоги» and «Канбан» are hidden (feat-039 BR-PB-9), and a team a browser remembers falls back with the switcher's notice, as for a deleted team (mod-002 DC-7)
  And every card reads «Без команды», its team picker offers «Без команды» alone, and a new item starts there
  And the address of a dropped team's page, members, backlog or board answers «Команда не найдена.»
  And «Другая» and its project Zeta read the same way

Scenario: An administrator rebuilds the structure with this line's own tools (US-1, US-3; SC-01 on upgraded data)
  Given the upgrade has run
  When Ann creates the team "Platform" with the members Max and Vera and the projects Alpha and Beta
  Then the name is free, though a team of that name existed before, and "Platform" is linked to Alpha and Beta
  And it owns no item: the 12 stories of Alpha and the 3 items of Beta are still «Без команды», and its backlog at Alpha's address is empty
  When Max sets Platform as the owning team of one story
  Then the story stands unplaced on Platform's backlog, with no memory of a place from before the upgrade (mod-017 BR-OT-6)
  And creating and linking the team moved no other item

Scenario: Databases with little to drop migrate (BR-OL-13)
  Given an organization with no project, and so no team
  When the upgrade runs
  Then it is as before, the counts logged are 0, and its administrator can create the first team (US-1)
  And on a database with no organization at all the migrations apply and change no row
```

## Owed by later lines

```gherkin
# `mod-018-area-workspace`: the upgrade of the per-project areas, which are another line's rows
# `mod-019-scoped-access`: nothing to upgrade; grants start empty
# `mod-007-iterations`, `mod-009-dashboards-widgets`: no sprint or dashboard exists yet, so the upgrade drops none with the teams
# `feat-015-work-item-history`: an owning team cleared by the upgrade is no actor's change and writes no entry
```
