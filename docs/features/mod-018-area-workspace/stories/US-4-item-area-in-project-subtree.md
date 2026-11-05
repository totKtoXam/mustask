# US-4: The work item's «Область» stays, on the project's node or one of its sub-areas

- story: US-4
- depends-on: US-1
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); reworked 2026-10-27 (typed tree reverses doc-012 OQ-1 A). The file's slug is stale from «retires»
- status: pending

As a **member who works with work items**, I want **the item to keep its «Область», now a place inside its project's node of the organization's tree**, so that **the project's own classification survives and the item's path continues the organization's structure (BR-AW-11)**.

Nothing about the item changes for the user: mod-003's field, band and list column stay as they are (feat-022, feat-029, mod-017 design § 3). What changes is where the area lives: the project's root area is now the project's node («Проект»), under a department or the organization, and the sub-areas below it are as before. Cast and conventions: spec § Stories; Alpha is under «Логистика», its node has the sub-areas «Платформа» (with «API») and «Отчёты»; Alpha holds ALP-EP-1, ALP-US-12 (a child of ALP-EP-1, in «Платформа / API», owned by MARS) and ALP-TASK-30.

## Acceptance criteria

```gherkin
Scenario: The card, the list and creation keep «Область» as before (BR-AW-11)
  When Max opens ALP-US-12's card, Alpha's «Рабочие элементы» and «Новый рабочий элемент»
  Then the card shows «Область» «Alpha / Платформа / API» or its project-relative form (the design's), the list its column, and the creation text names the area as mod-003 does
  And a new item, a child from a backlog row and a task from «Добавить задачу» are created as before, the child taking its parent's area (feat-032 BR-C-9)

Scenario: An item's area is its project's node or a sub-area of it (BR-AW-11)
  When Max sets ALP-US-12's «Область» to «Отчёты», or to Alpha's node itself
  Then it is saved
  And the offer lists only Alpha's node and its sub-areas, never a department, the organization, another project's node or any area of «Другая»
  And a direct request to set an area outside Alpha's subtree is answered «Область не найдена.» and changes nothing

Scenario: The item's reads and writes are as before (BR-AW-11; mod-003 api-contract § 1.1)
  When the panel reads ALP-US-12, the list or a backlog
  Then each item carries its area as before, and identifiers, parents, owning teams, states, estimates, assignees and backlog places are as before
  And the statement counts mod-003 pinned for these reads hold

Scenario: A project's node stands where its root stood (BR-AW-11, BR-AW-2; OQ-5, OQ-10)
  When Ann creates project Delta (DEL) under «Финансы»
  Then its node «Delta» exists under «Финансы», and «Новый проект» says again that the project's root area is named like the project
  When she renames Alpha «Alpha 2»
  Then its node is renamed, every path shows «Alpha 2», and the rename dialog says so again
  And deactivating and restoring Alpha changes no node and no item's area (OQ-6 A)

Scenario: Moving a project moves its subtree, items follow with no change (BR-AW-3, BR-AW-8)
  When Ann moves Alpha from «Логистика» to «Финансы»
  Then ALP-US-12's area is still «Платформа / API» of Alpha's node, now at the path «Финансы / Alpha 2 / Платформа / API», and no item was saved

Scenario: A node that items point at is not deleted (BR-AW-9; mod-002 BR-M1-13)
  Given «Платформа / API» is a leaf with items, and «Отчёты» is a leaf with none
  When Ann deletes «Платформа / API»
  Then it is refused with `area.not_empty`'s text, and nothing changes
  When she deletes «Отчёты» after the confirmation
  Then it is gone with its team links, and no item changes

Scenario: The project's own tree stays under its settings (BR-AW-12)
  When Max opens Alpha's settings
  Then «Области» is there as mod-002 had it, showing Alpha's node and below, not departments, other projects or the organization
  And creating, renaming, moving or deleting a sub-area there follows US-1 and US-2 for sub-areas
```

## Owed by later lines

```gherkin
# `feat-042-area-board-and-overview`: a department reaches items through its child project nodes, a project node through its sub-areas, with «Включая дочерние области»
# `feat-015-work-item-history`: a project's move changes no item and writes no history entry
# `feat-003-work-item-search`, `mod-008-queries-dashboards`, `feat-013-board-backlog-filter`: the area filter and Under on «Область» (queue lines to be re-checked, status.md § Decisions)
```
