# US-2 (MVP): An administrator chooses a team's iterations and its default

- story: US-2
- depends-on: US-1
- shippable: no — analysis story, delivered by `mod-007-iterations`
- status: pending

As an **organization administrator**, I want **to choose which nodes of the
project's iteration tree a team uses and which one is that team's default**,
so that **each of the project's teams plans in its own sprints and always has
a fallback for "current" (BR-M5-5, BR-M5-6, doc-001 D-11, E-5)**.

## Acceptance criteria

```gherkin
Scenario: Two teams are given different subsets
  Given a project with five iterations and two teams, A and B (E-5)
  When the administrator selects three of them for team A and the other two
    for team B
  Then team A's settings list exactly those three and team B's exactly those
    two, each shown with its path in tree order (BR-M5-5)
  And a work item owned by team A is offered only team A's three when its
    iteration is set (US-3)

Scenario: An iteration may be used by several teams at once
  Given a project with one iteration and two teams
  When the administrator selects it for team A and for team B
  Then both teams use the same iteration, and unselecting it for one leaves
    the other's selection unchanged

Scenario: Selecting a parent does not select its children
  Given «Релиз 1» with children «Спринт 1» and «Спринт 2»
  When the administrator selects only «Релиз 1» for a team
  Then the team's settings list «Релиз 1» and neither sprint
  And selecting «Спринт 1» alone later does not add «Релиз 1» (BR-M5-5)

Scenario: Only active iterations can be selected
  Given a project with a removed iteration (US-14)
  When the administrator opens the list of iterations to select for a team
  Then the removed iteration is not offered (BR-M5-5)

Scenario: The team's default is set
  Given a team with three selected iterations
  When the administrator marks one of them as the team's default
  Then the team's settings show it as «Итерация по умолчанию»
  And it is current whenever no selected iteration's dates cover today
    (doc-001 BR-9, US-4)

Scenario: The default is never assigned to new work items
  Given team A whose default iteration is «Спринт 3»
  When a member creates a work item in an area team A owns — from the work
    item list or from team A's backlog
  Then the new item's Итерация is empty (BR-M5-6, `doc-002` § Fields)

Scenario: A default outside the team's selection is refused
  Given a team with three selected iterations and a fourth iteration in the
    project that the team has not selected
  When the administrator tries to make that fourth one the team's default
  Then the action is refused (BR-M5-6)
  And the team's default is unchanged

Scenario: Unselecting the default clears it
  Given a team whose default is one of its selected iterations
  When the administrator unselects that iteration for the team
  Then the iteration leaves the team's list
  And the team has no default afterwards (BR-M5-6)
  And the team's current iteration is recomputed from what is left
    (doc-001 BR-9)

Scenario: Unselecting does not touch work items
  Given work items owned by team A pointing at an iteration team A uses
  When the administrator unselects that iteration for team A
  Then those work items still point at it and still show its name, marked as
    outside the team's selection (BR-M5-7)
  And the picker no longer offers it for a new assignment

Scenario: A deleted team's iteration settings are dropped
  Given team B, not the project's first team, with selected iterations, a
    default, and items pointing at «Спринт 4», which the first team has not
    selected
  When an administrator deletes team B (doc-006 BR-M1-18)
  Then team B's selection and default no longer exist (BR-M5-5)
  And its items, now owned by the first team (`doc-003` BR-M4-20), still
    point at «Спринт 4», marked as outside the team's selection (BR-M5-7)

Scenario: A team may have no default at all
  Given a team with selected iterations and no default
  When an administrator opens the team's settings
  Then the default is shown as empty, which is a valid state (BR-M5-8)

Scenario: Only an administrator changes a team's selection or default
  Given users whose roles in the organization are member and viewer
  When either tries to select an iteration for a team or change its default
  Then the action is refused (spec § Roles)
  And both can read which iterations the team uses and which is its default
```
