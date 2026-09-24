# US-6 (release 2): A team works the sprint taskboard

- story: US-6
- depends-on: US-5
- shippable: no — release-2 ground, not built by any MVP task; founded by `mod-NNN-sprint-views`
- status: pending

As a **team member**, I want **a board of the current sprint's tasks in
columns by state, grouped by the story they belong to or by the person doing
them**, so that **the team can run its daily work without opening each item
(doc-001 D-3, screenshot 10)**.

## Acceptance criteria

```gherkin
Scenario: Tasks of one iteration in columns by state
  Given a team iteration holding requirement-level items with child Задача
    items
  When a member opens the taskboard for that iteration
  Then every Задача pointing at that iteration appears in the column of its
    state's category (doc-001 BR-2)
  And moving a card to another column changes that task's state and records
    it in history (doc-001 BR-4)

Scenario: Grouping by requirement or by person
  Given a taskboard with tasks belonging to several stories and assigned to
    several people
  When a member switches the grouping between «По элементам» and «По людям»
  Then the same tasks are regrouped into rows accordingly, and no work item
    is changed by switching

Scenario: A task inherits the iteration of the story it is added under
  Given a story in a sprint
  When a member adds a child task to it from the taskboard
  Then the new task is created pointing at the same iteration (BR-7)

Scenario: A task scheduled into another iteration than its parent
  Given a task whose iteration differs from its parent story's
  When the taskboard of either iteration is opened
  Then the task is shown on the board of its own iteration, and its parent
    row states that the parent is elsewhere — the mismatch is visible, never
    silently corrected

Scenario: An empty taskboard
  Given a sprint with no tasks
  When a member opens the taskboard
  Then an explicit empty state is shown with a way to add work, not an error
    (screenshot 10)

Scenario: A viewer cannot move cards
  Given a user with only the "viewer" role
  When the user drags a card on the taskboard
  Then the move is refused and the board returns to its previous state
    (doc-001 D-9)
```
