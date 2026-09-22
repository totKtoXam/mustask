# US-3: Work items follow the built-in process

- story: US-3
- depends-on: US-1
- shippable: no — delivered by mod-process-seed
- status: pending

As an **organization administrator**, I want **every new project to ship with the
five built-in work item types and their states already defined**, so that
**my team can start creating work items without first configuring a
process**.

## Acceptance criteria

```gherkin
Scenario: A new organization's project already has a usable process
  Given an organization administrator has just created an organization and a
    project inside it (US-1)
  When they open the project's work item type list
  Then exactly five types are present: «Эпик», «Фича», «Пользовательская
    история», «Задача», «Баг» (D-3, D-7, D-12)
  And each has its own set of states already defined
  And no process-editing screen is offered (M2 ships fixed, no editing UI)

Scenario: The built-in states map to the categories boards rely on
  Given the project's built-in process
  When any module reads the state of a «Задача»
  Then «Новая» resolves to category Proposed, «Выполняется» to InProgress,
    «Готово» to Completed, and «Удалена» to Removed (BR-2)
  And the same category set (Proposed/InProgress/Resolved/Completed/Removed)
    applies to every one of the five types, even where the state names
    differ per type

Scenario: A backward state transition is allowed with no reason
  Given a work item of any type in a non-Removed state
  When a team member moves it directly to any other non-Removed state,
    including "backward" (e.g. «Готово» → «Новая»)
  Then the transition succeeds with no reason required (BR-10, D-6)

Scenario: «Удалена» is not offered by the ordinary state control
  Given a work item in any non-Removed state
  When a team member opens the state field to change it directly
  Then «Удалена» is not among the offered choices
  And it is reachable only through the dedicated Delete action (US-6)

Scenario: A reader can see the process definition but not change it
  Given a user signed in with only the "viewer" role
  When they open the project's work item type list
  Then they can see all five types and their states
  And no action on this screen lets them add, rename, or remove a type or a
    state
```
