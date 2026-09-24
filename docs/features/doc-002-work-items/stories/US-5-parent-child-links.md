# US-5: A team member builds the hierarchy with Parent/Child links

- story: US-5
- depends-on: US-1
- shippable: no — analysis only; delivered by `feat-001-work-item-links`
- status: pending

As a **team member**, I want **to hang a work item under a bigger one**, so
that **an epic shows what it is made of and a task shows what it serves**.

## Acceptance criteria

Level order, highest first: Эпики → Фичи → Истории (Пользовательская история
and Баг) → Задачи (D-3). A parent sits on a higher **or the same** level as
its child (W-1, BR-8).

Consequence for `doc-003`: a backlog of one level can now hold a parent and
its child side by side (a story and its bug, a task under a task). The
reference product does not nest same-level items in its backlog tree;
`doc-003` owns how MUSTASK's backlog shows them, and M3's links stand either
way.

```gherkin
Scenario: A team member sets a parent
  Given a «Фича» and an «Эпик» in the same project
  When the team member opens the «Связи» tab of the «Фича» and picks the
    «Эпик» as its parent
  Then the link shows on the «Фича» as Parent, with the epic's identifier,
    title and current state (screenshot 07)
  And the same link shows on the «Эпик» as a child
  And both items record the link in their history (BR-10)

Scenario: A work item has at most one parent
  Given a «Задача» that already has a parent
  When the team member picks a different parent for it
  Then the action is refused, and the message names the existing parent
    (BR-8)
  And re-parenting is removing the old link and adding the new one — a link
    is never edited in place (doc-001 Operation completeness)

Scenario: A bug may be the child of a user story
  Given a «Пользовательская история» and a «Баг» in the same project
  When the team member makes the «Пользовательская история» the parent of
    the «Баг»
  Then the link is created — both sit on Истории, and a parent on the same
    level as its child is allowed (BR-8, W-1)

Scenario: Two items of the same type may be parent and child
  Given two «Задача» items in the same project
  When the team member makes one the parent of the other
  Then the link is created (BR-8, W-1)

Scenario: The parent may not sit on a lower level
  Given a «Задача» and a «Пользовательская история» in the same project
  When the team member tries to make the «Задача» the parent of the
    «Пользовательская история»
  Then the link is refused, and the message says the parent must be on the
    same or a higher level than the child (BR-8)

Scenario: A work item cannot be its own parent
  Given a «Фича»
  When the team member picks the same «Фича» as its parent
  Then the link is refused, and the item is not offered by the picker
    (BR-8)

Scenario: Skipping a level is allowed
  Given a «Задача» and an «Эпик» in the same project
  When the team member makes the «Эпик» the parent of the «Задача»
  Then the link is created — BR-8 requires a higher or equal level, not the
    next one up

Scenario: A child cannot be its own ancestor
  Given «Задача» A is the parent of «Задача» B, which is the parent of
    «Задача» C — all on one level, so only the cycle rule can refuse
  When the team member tries to make C the parent of A
  Then the link is refused as a cycle, naming the path it would close (BR-8)

Scenario: Linking across projects is refused
  Given two work items in different projects, of the same organization or of
    different ones
  When the team member tries to link them
  Then the link is refused, and the other project's items are not offered by
    the picker at all (BR-2, BR-8)

Scenario: Removing a link is its own action
  Given a «Задача» with a parent
  When the team member removes the link
  Then the link disappears from both items
  And the removal is recorded in both histories (BR-10)
  And there is no way to edit a link in place — re-parenting is remove plus
    add (doc-001 Operation completeness)

Scenario: A deleted parent leaves its children alone
  Given an «Эпик» with three children
  When the epic is deleted (US-10)
  Then the three children stay in their own states, each keeping its parent
    link (BR-9)
  And restoring the epic restores the hierarchy exactly as it was

Scenario: A reader can see links but not change them
  Given a user with only the "viewer" role
  When they open the «Связи» tab
  Then they see the parent and the children with their states
  And no «Добавить связь» or remove control is offered
```
