# US-7: An organization administrator manages the project's area tree

- story: US-7
- depends-on: US-5, US-6
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`
- status: pending

As an **organization administrator**, I want **to build the project's area
tree and say which team owns each node**, so that **every work item has a
place and the team responsible for it is derivable (BR-3, D-8)**.

## Acceptance criteria

```gherkin
Scenario: The root area comes with the project and follows it
  Given a project created in US-5, with its root area owned by the
    project's first team
  When an administrator opens the area tree
  Then the root area is shown, named after the project, owned by the first
    team
  And the rename, move, re-own and delete actions are not offered for it,
    and invoking them directly is refused (BR-M1-11: the root is renamed
    only with the project, and its owner is always the first team)

Scenario: A tree with only the root says so
  Given a project whose area tree is the root alone
  When an administrator opens the area tree
  Then they see the root and «Вложенных областей пока нет» with the
    create-child action

Scenario: An administrator creates a child area with or without an owner
  Given a project with two teams, «Пример проекта» and «Платформа» (US-6)
  When the administrator creates a child area under the root
  Then the owner picker offers every team of the project, or no explicit
    owner
  And the child exists with the owner chosen, or with none (BR-3)

Scenario: An administrator re-owns a child area from one team to another
  Given a child area «Отчёты» owned explicitly by the first team, holding
    work items, and not a team's default area
  When the administrator re-owns it to the team «Платформа»
  Then «Отчёты» and every descendant without an explicit owner resolve to
    «Платформа»
  And their work items leave the first team's backlogs and appear on
    «Платформа»'s (BR-3; doc-003 decides their position)

Scenario: A team's default area stays with its team
  Given the area «Платформа», the default area of the team «Платформа»
  When the administrator re-owns it to another team, clears its owner or
    deletes it
  Then the action is refused with «Область используется как область по
    умолчанию команды «Платформа»» (BR-M1-14)
  And after the team's default is changed to another of its areas, the
    same action is allowed

Scenario: Sibling area names are unique
  Given an area with a child named «Платформа»
  When the administrator creates another child with the same name under the
    same parent
  Then the action is refused with «Область с таким названием уже есть на
    этом уровне»
  And «Платформа» is still free under a different parent (BR-M1-12)

Scenario: An area with no explicit owner inherits the nearest owned ancestor
  Given a child area created with no team assigned
  When any module asks which team owns it — to place a work item on a board
    (BR-3), for instance
  Then the answer is the owning team of the nearest ancestor that has one,
    which at the latest is the root (BR-M1-11), so every area resolves to
    exactly one owning team

Scenario: Moving a node moves its subtree and re-resolves ownership
  Given an area with children, some of them without an explicit owner
  When the administrator moves that area under a different parent
  Then its whole subtree moves with it
  And every node that had an explicit owner keeps it
  And every node without one now inherits from its new nearest owned
    ancestor
  And the work items in those areas keep pointing at the same area nodes

Scenario: A node is not moved under its own descendant
  Given an area with a subtree beneath it
  When the administrator tries to move it under one of its own descendants
  Then the action is refused, and the tree is unchanged (BR-M1-12: the
    areas of a project form a tree, never a cycle)
  And two moves submitted at the same moment that would together form a
    cycle are not both saved (NFR Concurrency)

Scenario: Only an empty leaf is deleted, deleted work items counting
  Given a leaf area whose only work item is in «Удалена»
  When the administrator deletes the area
  Then the action is refused with «Нельзя удалить область, в которой есть
    рабочие элементы (включая удалённые) или вложенные области»
    (BR-M1-13, doc-001 BR-14)
  And once that item is restored and moved, or purged, deleting the empty
    leaf succeeds and it disappears from the tree

Scenario: A work item created with no team context lands on the root
  Given a project with several teams
  When any module asks the project for the default area of a work item
    created from M3's project-wide form
  Then the answer is the project's root area, the first team's default
    (BR-M1-14; the rule that applies it is doc-002's)

Scenario: A reader sees the tree but changes nothing
  Given a user with the role viewer in the project's organization
  When they open the area tree
  Then they see every node with its owning team
  And no action creates, renames, moves, re-owns or deletes a node, and
    invoking one directly is refused with «Недостаточно прав»
```
