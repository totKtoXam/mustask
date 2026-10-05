# US-6: Each area resolves to exactly one owning team

- story: US-6
- depends-on: US-4, US-5
- shippable: no — ownership is read by M4's boards and backlogs, which do not exist yet; one side of M1's structure
- status: pending

As a **signed-in user** (the organization administrator once mod-016 narrows
it, BR-M1-19), I want **to say which team owns each area**, so that **every
area — and later every work item in it — resolves to exactly one team
(doc-001 BR-3, BR-M1-11)**.

## Acceptance criteria

```gherkin
Scenario: A child area is created with or without an owner
  Given the project «Пример проекта» with the teams «Пример проекта» and
    «Платформа» (US-4)
  When a signed-in user creates a child area under the root
  Then the owner picker offers «Пример проекта», «Платформа» and no explicit
    owner, and never a deleted team
  And the area is created with the owner chosen, or with none

Scenario: An area with no explicit owner inherits from its nearest owned ancestor
  Given «Платформа», owned explicitly by the team «Платформа», with the child
    «API» and the grandchild «API / Шлюз», neither with an explicit owner
  When a signed-in user opens the area tree
  Then «API» and «API / Шлюз» show the team «Платформа», marked
    «унаследовано»
  And every node shows exactly one owning team; at the latest, a node
    inherits the root's owner, the first team (BR-M1-11)

Scenario: Other modules get the same answer as the tree
  Given the areas above
  When another module asks which team owns «Платформа / API / Шлюз»
  Then the answer is the team «Платформа», the same as the tree shows
  And when it asks which area a work item created with no team context
    takes, the answer is the root (BR-M1-14)

Scenario: An area is re-owned
  Given «Отчёты», owned explicitly by the first team and no team's default,
    with the child «Квартальные» that has no explicit owner
  When «Отчёты» is re-owned to «Платформа»
  Then «Отчёты» shows «Платформа», and «Квартальные» shows «Платформа»
    marked «унаследовано»

Scenario: An explicit owner is cleared
  Given «Отчёты», owned explicitly by «Платформа» and no team's default,
    directly under the root
  When its owner is cleared
  Then «Отчёты» and its children resolve to the first team, the root's
    owner, marked «унаследовано»

Scenario: A move re-resolves inherited owners and keeps explicit ones
  Given «Отчёты» with no explicit owner under the root, and its child
    «Квартальные» owned explicitly by «Пример проекта»
  When «Отчёты» is moved under «Платформа»
  Then «Отчёты» resolves to «Платформа», marked «унаследовано»
  And «Квартальные» keeps its explicit owner «Пример проекта»

Scenario: An area's owner is a team of the area's own project
  Given the team «Ядро» of another project, in this organization or another
  When a direct request makes «Ядро» the owner of «Отчёты» of «Пример
    проекта», or creates a child area there with «Ядро» as its owner
  Then it is refused, and nothing changes (BR-M1-22)

Scenario: A team's default area stays with its team
  Given the leaf «Платформа», the default area of the team «Платформа», and
    «Отчёты», which the team also owns explicitly
  When «Платформа» is re-owned to another team, its owner is cleared, or it
    is deleted
  Then the action is refused with «Область используется как область по
    умолчанию команды «Платформа»» (BR-M1-14)
  And once the team's default is changed to «Отчёты», the same actions are
    allowed

Scenario: The root's owner is fixed
  Given the root, owned by the first team
  When a signed-in user looks at the root's actions
  Then neither re-owning nor clearing the owner is offered
  And each one, requested directly, is refused, and the root stays owned by
    the first team (BR-M1-11)

Scenario: A re-own or a delete, and a default change, at the same moment
  Given «Платформа / API», a leaf owned explicitly by the team «Платформа»
    and not its default
  When, at the same moment, «Платформа / API» is set as the default area of
    «Платформа» and is either re-owned to «Пример проекта» or deleted
  Then one of the two is refused
  And the default area of «Платформа» is an existing area it owns
    explicitly (BR-M1-13, BR-M1-14, NFR Concurrency)
```
