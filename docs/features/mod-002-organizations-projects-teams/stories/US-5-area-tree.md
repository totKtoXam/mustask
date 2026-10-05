# US-5: The project's area tree is built and pruned

- story: US-5
- depends-on: US-3
- shippable: no — the tree's nodes are what US-6 assigns to teams; one side of M1's structure
- status: pending

As a **signed-in user** (the organization administrator once mod-016 narrows
it, BR-M1-19), I want **to build the project's area tree — create, rename,
move and delete its nodes**, so that **every part of the product has a place
where its work items will live (doc-001 BR-3, BR-M1-12)**.

The area tree is the first consumer of mod-015's tree registry: mod-015
designs and prototypes that pattern, and this task builds it and shows the
tree through it (mod-015 BR-14, D-b, BR-M1-24).

## Acceptance criteria

```gherkin
Scenario: The area tree is shown as the shell's tree registry
  Given mod-015's design.md and prototype of the tree registry
  When a signed-in user opens a project's area tree
  Then it follows that pattern, each node under its parent with its name,
    its resolved owner (US-6) and the actions of this story
  And the pattern lives in the shared UI layer and names nothing of areas,
    owners or teams, so a later module's tree reuses it rather than copies
    it (mod-015 BR-14)

Scenario: The root comes with the project and follows it
  Given the project «Пример проекта» (US-3)
  When a signed-in user opens its area tree
  Then the root «Пример проекта» is shown
  And no rename, move or delete action is offered for it, and each one,
    requested directly, is refused and changes nothing (BR-M1-11)

Scenario: A tree with only the root says so
  Given a project whose tree is the root alone
  When a signed-in user opens the area tree
  Then they see the root and «Вложенных областей пока нет», with the
    create-child action

Scenario: A child area is created under any node
  Given the project's area tree
  When «Отчёты» is created under the root, and «Квартальные» under «Отчёты»
  Then the tree shows «Пример проекта / Отчёты / Квартальные»

Scenario: Sibling names are unique, compared ignoring case and spaces
  Given «Отчёты» has the child «Квартальные»
  When another child «квартальные » is created under «Отчёты», or a child of
    «Отчёты» is renamed to it
  Then the action is refused with «Область с таким названием уже есть на
    этом уровне» (BR-M1-12)
  And «Квартальные» is still free under another parent
  And an empty name, or one longer than 100 characters, is refused with
    «Название должно содержать от 1 до 100 символов»

Scenario: Two creations of one sibling name at the same moment keep one
  Given «Отчёты» has no child «Квартальные»
  When «Квартальные» is created under «Отчёты» twice at the same moment
  Then exactly one «Квартальные» exists under «Отчёты»

Scenario: A child area is renamed
  Given «Отчёты / Квартальные» with children of its own
  When it is renamed to «Годовые»
  Then the tree shows «Отчёты / Годовые», with its children and its owner
    unchanged

Scenario: A node moves with its whole subtree
  Given the root's children «Отчёты» and «Архив», and «Отчёты» with the
    children «Квартальные» and «Годовые»
  When «Отчёты» is moved under «Архив»
  Then the tree shows «Архив / Отчёты / Квартальные» and
    «Архив / Отчёты / Годовые», and no «Отчёты» remains under the root

Scenario: A move needs a free name at its destination
  Given «Архив» already has a child «Отчёты», and the root has another child
    «Отчёты»
  When the root's «Отчёты» is moved under «Архив»
  Then the action is refused with «Область с таким названием уже есть на
    этом уровне», and the tree is unchanged (BR-M1-12)

Scenario: A node is never moved under itself or its descendant
  Given «Отчёты» with the child «Квартальные»
  When «Отчёты» is moved under «Квартальные», or under itself
  Then the action is refused with «Нельзя переместить область внутрь неё
    самой или её вложенной области», and the tree is unchanged (BR-M1-12)
  And of two moves submitted at the same moment that would together form a
    cycle — «Отчёты» under «Архив» and «Архив» under «Отчёты» — one is
    refused

Scenario: A node never leaves its project
  Given the project «Второй проект» with its own area tree
  When a direct request moves «Отчёты» of «Пример проекта» under an area of
    «Второй проект», or creates an area of «Пример проекта» under one
  Then it is refused, and both trees are unchanged (BR-M1-22)

Scenario: Only an empty leaf is deleted
  Given «Отчёты» with the child «Квартальные», which has no children and is
    no team's default area
  When «Отчёты» is deleted
  Then the action is refused with «Нельзя удалить область, в которой есть
    рабочие элементы (включая удалённые) или вложенные области»
  When «Квартальные» is deleted
  Then it disappears from the tree, and no action restores it

Scenario: A leaf deleted while a child is put under it
  Given «Отчёты» with the leaf children «Квартальные» and «Годовые»
  When «Квартальные» is deleted at the same moment as a child is created
    under it, or as «Годовые» is moved under it
  Then one of the two is refused, and no area is left under a deleted
    parent (BR-M1-13, NFR Concurrency)
```
