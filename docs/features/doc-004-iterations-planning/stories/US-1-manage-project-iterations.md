# US-1 (MVP): An administrator builds the project's iteration tree

- story: US-1
- depends-on: -
- shippable: no — analysis story, delivered by `mod-007-iterations`
- status: pending

As an **organization administrator**, I want **to keep the project's
iterations as a tree of releases and numbered sprints — add them, nest them,
name them, date them and move them**, so that **teams have real releases and
sprints to schedule work into (BR-M5-1..BR-M5-4)**. Removal is US-14.

## Acceptance criteria

```gherkin
Scenario: A sprint takes the next sprint number, a release takes none
  Given a project whose sprints so far were issued the numbers 1 and 2
  When the administrator adds a top-level release «Релиз 2», then under it a
    sprint named «Спринт 3» with a start and a finish date
  Then the tree shows «Релиз 2» with no number, and «Релиз 2 / Спринт 3»
    with both dates and the number 3 (BR-M5-1, BR-M5-2)

Scenario: An empty sprint name shows «Спринт N»
  Given a project whose next sprint number is 7
  When the administrator adds a sprint and leaves the name empty
  Then it is shown as «Спринт 7» with the number 7 (BR-M5-3)

Scenario: A release needs a name
  Given a project whose next sprint number is 7
  When the administrator adds a release with an empty or blank name, or
    renames an existing release to an empty or blank name
  Then it is refused with «Укажите название релиза» and nothing is saved
  And the next sprint added gets the number 7 (BR-M5-2, BR-M5-3)

Scenario: A number is never issued twice, and kind and number are fixed
  Given a project whose sprint numbered 4 was later removed (US-14)
  When the administrator adds another sprint
  Then it gets the number 5, never 4
  And no action lets an administrator edit a number or change an
    iteration's kind between release and sprint (BR-M5-2)

Scenario: A refused creation issues no number
  Given a project whose next sprint number is 7, with a top-level
    iteration already named «Спринт 7»
  When the administrator adds a top-level sprint and leaves the name empty
  Then it is refused with «Итерация с таким именем уже есть»
  And the next sprint with a free name gets the number 7 (BR-M5-2, BR-M5-3)

Scenario: Two sprints created at once get different numbers
  Given two administrators of the same project
  When both add a sprint at the same moment
  Then the two sprints get two different numbers (BR-M5-2)

Scenario: A sprint's name is changed, and a cleared name falls back
  Given the sprint numbered 7, shown as «Спринт 7»
  When the administrator renames it «Стабилизация», and later clears the name
  Then it is shown as «Стабилизация», and after the clearing as «Спринт 7»
  And its number stays 7 and its kind «Спринт» throughout (BR-M5-2, BR-M5-3)

Scenario: Either kind may sit at any level
  Given a top-level release «Релиз 1» and a top-level sprint «Спринт 2»
  When the administrator adds the sprint «Спринт 1» and the release «Этап А»
    under «Релиз 1», and the sprint «Неделя 1» under «Спринт 2»
  Then all three are saved and shown with their paths, such as
    «Релиз 1 / Спринт 1» and «Спринт 2 / Неделя 1» (BR-M5-1, BR-M5-3)

Scenario: Children are ordered by start date, then by creation
  Given a parent with children «Б» (sprint 5, starts 10 March), «А» (sprint
    6, starts 1 March), «В» (sprint 4, no dates) and «Г» (a release created
    after them, no dates)
  When the administrator opens the tree
  Then the children are listed in the order «А», «Б», «В», «Г» (BR-M5-1)

Scenario: A parent's dates do not constrain its children
  Given «Релиз 1» dated 1 March – 31 May
  When the administrator dates its child «Спринт 1» 20 May – 10 June
  Then the child is saved with those dates, and the parent's dates are
    unchanged (BR-M5-4)

Scenario: An iteration without dates is added
  Given the same project
  When the administrator adds an iteration with a name and no dates
  Then it appears in the tree with empty dates
  And it can be selected by a team and assigned to work items like a dated
    one (BR-M5-4)

Scenario: A duplicate name among siblings is refused
  Given a parent whose active children include «Спринт 1»
  When the administrator adds another child named « спринт 1 » (differing
    only by case and surrounding spaces), or renames another child of that
    parent to it
  Then the action is refused with «Итерация с таким именем уже есть»
  And the parent still has exactly one child with that name (BR-M5-3)

Scenario: The same name under different parents is allowed
  Given «Релиз 1 / Спринт 1»
  When the administrator adds «Спринт 1» under «Релиз 2»
  Then it is saved, and pickers tell the two apart by their paths (BR-M5-3)

Scenario: A removed iteration's name is free again
  Given a removed sprint «Спринт 5» (US-14)
  When the administrator adds a sibling sprint named «Спринт 5» where it was
  Then it is saved, with a new number (BR-M5-2, BR-M5-3)

Scenario: Invalid dates are refused
  Given a project
  When the administrator sets a finish date earlier than the start date, or
    only one date of the pair
  Then the action is refused — «Дата окончания раньше даты начала» for the
    first case, «Укажите обе даты или ни одной» for the second
  And nothing about the iteration changes (BR-M5-4)

Scenario: A one-day iteration is allowed
  Given a project
  When the administrator sets an iteration's start and finish to one day
  Then the iteration is saved (BR-M5-4)

Scenario: An iteration is moved in the tree
  Given «Спринт 2» under «Релиз 1», with a child «Неделя 1», selected by a
    team and pointed at by work items
  When the administrator moves it under «Релиз 2»
  Then its path becomes «Релиз 2 / Спринт 2» with the same number
  And its child moves with it, as «Релиз 2 / Спринт 2 / Неделя 1»
  And the team's selection and every work item pointer are unchanged
    (BR-M5-1)

Scenario: A move into its own subtree or onto a taken name is refused
  Given «Релиз 1» with a child «Спринт 1», and «Релиз 2» with a child
    «Спринт 1»
  When the administrator moves «Релиз 1» under «Спринт 1», or moves
    «Релиз 1 / Спринт 1» under «Релиз 2»
  Then the first is refused with «Нельзя перенести итерацию внутрь неё самой»
    and the second with «Итерация с таким именем уже есть»
  And the tree is unchanged (BR-M5-1, BR-M5-3)

Scenario: An iteration is renamed and redated
  Given an iteration selected by a team and pointed at by work items
  When the administrator renames it and changes its dates
  Then the new name and dates are shown in the tree, in every team's
    selection, in every picker, pane and filter and on every work item
    pointing at it
  And no work item's pointer, no kind and no number changes as a result

Scenario: A member or a viewer cannot manage iterations
  Given users whose roles in the organization are member and viewer
  When either tries to add, rename, redate or move an iteration
  Then the action is refused (spec § Roles)
  And both can still read the project's iteration tree
```
