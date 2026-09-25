# US-13 (MVP): A member filters work items by iteration

- story: US-13
- depends-on: US-3, US-4
- shippable: no — analysis story, delivered by `feat-008-iteration-filter`
- status: pending

As an **organization member**, I want **to narrow the team's board and backlog to the
current or to a chosen iteration, and the project's work item list to a
chosen iteration**, so that **I see one iteration's work apart from the rest
(BR-M5-9, E-2, E-4)**. The filter joins the list's filter bar
(`feat-003-work-item-search`) and the team's board and backlog filter bar
(`doc-003` BR-M4-22, `feat-NNN-board-backlog-filter`); with their other
conditions it combines as AND.

## Acceptance criteria

```gherkin
Scenario: The team board filtered by the current iteration
  Given team A whose current iteration resolves to «Спринт 3» (US-4)
  When a member filters team A's board by «Текущая итерация»
  Then only team A's work items pointing at «Спринт 3» or at any of its
    descendants are shown (BR-M5-9)
  And the resolved iteration's name is visible, not just the word "current"

Scenario: No current iteration on a team surface
  Given a team with no current iteration (US-4)
  When a member filters its backlog by «Текущая итерация»
  Then «Нет текущей итерации» is shown as an explicit empty state — not an
    error and not every work item (doc-001 BR-9)

Scenario: The team surfaces offer only the team's iterations
  Given team A that has selected three of the project's five iterations
  When a member opens the iteration filter on team A's board or backlog
  Then it offers «Текущая итерация» and exactly those three, in tree order
    with their paths (BR-M5-5, BR-M5-9)

Scenario: A parent iteration includes its descendants
  Given team A's items pointing at «Релиз 1», at «Релиз 1 / Спринт 1» and at
    «Релиз 2»
  When a member filters team A's backlog by «Релиз 1»
  Then the first two are shown and the third is not (BR-M5-9)

Scenario: The team filter respects team ownership
  Given teams A and B in one project that both use the same iteration
  When a member opens team A's board filtered by that iteration
  Then only work items whose area is owned by team A are shown (doc-001
    BR-3)

Scenario: The project list filters by a specific iteration only
  Given a project with several teams and iterations
  When a member opens the iteration filter on the project's work item list
  Then it offers every active iteration of the project, in tree order with
    paths, and no «Текущая итерация» option (BR-M5-9, E-2)
  And choosing one shows every work item of the project pointing at it or at
    its descendants, whichever team owns it, items in «Удалена» excepted

Scenario: Filtering never reorders
  Given a team backlog in a known order
  When a member filters it by an iteration
  Then the shown items keep their relative backlog order (doc-001 BR-12)

Scenario: The iteration filter combines with the other filters
  Given a board filtered by an assignee (`feat-NNN-board-backlog-filter`)
  When a member adds an iteration to the filter
  Then only items matching both conditions are shown

Scenario: A viewer can filter
  Given a user whose role in the organization is viewer
  When the user filters a team board or the project list by an iteration
  Then the filtered view is shown like for a member (spec § Roles)
```
