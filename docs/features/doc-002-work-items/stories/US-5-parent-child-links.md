# US-5: A member builds the hierarchy with Parent/Child links

- story: US-5
- depends-on: US-1
- shippable: no — analysis only; delivered by `feat-001-work-item-links`
- status: pending

As an **organization member**, I want **to hang a work item under a bigger one**, so
that **an epic shows what it is made of and a task shows what it serves**.

## Acceptance criteria

Level order, highest first: Эпики → Фичи → Истории (Пользовательская история
and Баг) → Задачи (D-3, doc-007 BR-M2-4). A parent sits on a higher **or the
same** level as its child (W-1, BR-M3-8).

The backlog (doc-003) nests only children exactly one level below, and its
mapping action offers only parents one level up, re-parenting through M3's
atomic replace parent (BR-M3-8, doc-003 BR-M4-10, BR-M4-11); same-level and
level-skipping parents are set on this form. M3's links stand either way.

```gherkin
Scenario: A member sets a parent
  Given a «Фича» and an «Эпик» in the same project
  When the member opens the «Связи» tab of the «Фича» and picks the
    «Эпик» as its parent
  Then the link shows on the «Фича» as Parent, with the epic's identifier,
    title and current state (screenshot 07)
  And the same link shows on the «Эпик» as a child
  And both items record the link in their history (doc-001 BR-4)

Scenario: A work item has at most one parent
  Given a «Задача» that already has a parent
  When the member picks a different parent for it on the form
  Then the action is refused, and the message names the existing parent
    (BR-M3-8)
  And on the form re-parenting is removing the old link and adding the new
    one — a link is never edited in place

Scenario: Replace parent is one atomic write
  Given a «Пользовательская история» whose parent is «Фича» A, and another
    «Фича» B in the same project
  When the replace-parent operation re-parents it to B — as the backlog
    mapping does (doc-003 US-3)
  Then the story has exactly one Parent link, to B
  And A's history records a removed child, B's an added child, and the
    story's history exactly one revision naming both (BR-M3-8)
  And a replace that would break a BR-M3-8 condition — a cycle, another
    project, a lower level — is refused and the link to A stays

Scenario: A bug may be the child of a user story
  Given a «Пользовательская история» and a «Баг» in the same project
  When the member makes the «Пользовательская история» the parent of
    the «Баг»
  Then the link is created — both sit on Истории, and a parent on the same
    level as its child is allowed (BR-M3-8, W-1)

Scenario: Two items of the same type may be parent and child
  Given two «Задача» items in the same project
  When the member makes one the parent of the other
  Then the link is created (BR-M3-8, W-1)

Scenario: The parent may not sit on a lower level
  Given a «Задача» and a «Пользовательская история» in the same project
  When the member tries to make the «Задача» the parent of the
    «Пользовательская история»
  Then the link is refused, and the message says the parent must be on the
    same or a higher level than the child (BR-M3-8)

Scenario: A work item cannot be its own parent
  Given a «Фича»
  When the member picks the same «Фича» as its parent
  Then the link is refused, and the item is not offered by the picker
    (BR-M3-8)

Scenario: Skipping a level is allowed
  Given a «Задача» and an «Эпик» in the same project
  When the member makes the «Эпик» the parent of the «Задача»
  Then the link is created — BR-M3-8 requires a higher or equal level, not
    the next one up

Scenario: A child cannot be its own ancestor
  Given «Задача» A is the parent of «Задача» B, which is the parent of
    «Задача» C — all on one level, so only the cycle rule can refuse
  When the member tries to make C the parent of A
  Then the link is refused as a cycle, naming the path it would close
    (BR-M3-8)

Scenario: Linking across projects is refused
  Given two work items in different projects, of the same organization or of
    different ones
  When the member tries to link them
  Then the link is refused, and the other project's items are not offered by
    the picker at all (BR-M3-2, BR-M3-8)

Scenario: Removing a link is its own action
  Given a «Задача» with a parent
  When the member removes the link
  Then the link disappears from both items
  And the removal is recorded in both histories (doc-001 BR-4)

Scenario: A deleted parent leaves its children alone
  Given an «Эпик» with three children
  When the epic is deleted (US-10)
  Then the three children stay in their own states, each keeping its parent
    link (BR-M3-9)
  And restoring the epic restores the hierarchy exactly as it was

Scenario: A child may let go of a deleted parent
  Given a «Фича» whose parent «Эпик» is in «Удалена»
  When the member removes the link on the «Фича»'s «Связи» tab
  Then the link is gone from both items, and both histories record the
    removal — the deleted epic changes in no other way (BR-M3-9)
  And the «Фича» can now take another parent

Scenario: A reader can see links but not change them
  Given a user whose role in the organization is viewer
  When they open the «Связи» tab
  Then they see the parent and the children with their states
  And no «Добавить связь» or remove control is offered
```
