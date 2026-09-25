# US-14 (MVP): An administrator removes an iteration

- story: US-14
- depends-on: US-1, US-3
- shippable: no — analysis story, delivered by `mod-007-iterations`
- status: pending

As an **organization administrator**, I want **to remove an iteration the
project no longer needs, knowing beforehand how many work items it will
unschedule**, so that **the tree stays clean without silently losing anyone's
planning (BR-M5-10, doc-001 BR-6)**.

## Acceptance criteria

```gherkin
Scenario: The confirmation states the impact, deleted items included
  Given an iteration with no active children, pointed at by 12 work items,
    2 of them in «Удалена»
  When the administrator asks to remove it
  Then a confirmation names the iteration and states that 12 work items will
    lose their iteration, before anything is changed (BR-M5-10)

Scenario: Cancelling the confirmation changes nothing
  Given the confirmation of the previous scenario
  When the administrator cancels it
  Then the iteration, every team selection and default, and all 12 pointers
    are unchanged

Scenario: A confirmed removal unschedules the work items in it
  Given an iteration selected by two teams, the default of one of them, and
    pointed at by work items, some of them in «Удалена»
  When the administrator confirms its removal
  Then it disappears from the project's tree, from both teams' selections
    and from every picker, pane and iteration filter
  And the team whose default it was has no default afterwards (BR-M5-6)
  And every work item that pointed at it — the deleted ones included — has
    an empty iteration, each clearing recorded in that work item's history
    with who and when (BR-M5-10, doc-001 BR-4)
  And a deleted item restored later comes back with an empty iteration

Scenario: The count changed before the confirmation
  Given the confirmation stating that 12 work items will lose their
    iteration
  When a member schedules a thirteenth work item into it and then the
    administrator confirms
  Then nothing is changed, and the confirmation is shown again stating 13
    (BR-M5-10)

Scenario: Removal is all or nothing
  Given an iteration pointed at by work items
  When its removal fails part-way
  Then the iteration is still active and no pointer to it has been cleared
    (BR-M5-10, NFR § Bulk writes)

Scenario: An iteration with active children cannot be removed
  Given «Релиз 1» with an active child «Спринт 1»
  When the administrator asks to remove «Релиз 1»
  Then the action is refused with «Сначала удалите или перенесите вложенные
    итерации»
  And nothing changes (BR-M5-10)

Scenario: A removed iteration cannot be brought back or purged
  Given a removed iteration
  When an administrator looks for it in the project's tree or any team
    setting
  Then it is not shown, and no action offers to reactivate or purge it
    (E-3, BR-M5-10)
  And, if it was a sprint, its number is never issued again (BR-M5-2)

Scenario: Someone is editing an affected work item meanwhile
  Given a member with a work item open that points at the iteration
  When the administrator confirms the removal and then the member saves the
    work item
  Then the member's save is rejected as a conflict to reload and reapply,
    and after reloading the item shows an empty iteration (doc-001 BR-11)

Scenario: Scheduling into an iteration removed meanwhile is refused
  Given a member with an iteration picker open that still lists the iteration
  When the administrator confirms its removal and then the member picks it
  Then the pointer write is refused and the pointer is unchanged (BR-M5-7)

Scenario: A member or a viewer cannot remove an iteration
  Given users whose roles in the organization are member and viewer
  When either tries to remove an iteration
  Then the action is refused (spec § Roles)
```
