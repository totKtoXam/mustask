# US-4: The work item's «Область» and the per-project tree retire

- story: US-4
- depends-on: -
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 OQ-1 A, ADR-0028 rule 3
- status: pending

As a **member who works with work items**, I want **the item to stop carrying an area of its project**, so that **«Область» names one thing, the organization's workspace area, and no item, card, list or project keeps a second tree to maintain (BR-AW-11)**.

The human chose at doc-012 gate-1 (OQ-1 A, 2026-10-17) to retire the item's pointer: team ownership already moved to the owning team (mod-017), and the classification path is dropped, departing from brief § 2. A workspace area reaches items only through its project links, which `feat-042-area-board-and-overview` reads. The data itself is dropped by the upgrade (US-5). Cast and conventions: spec § Stories; Alpha holds the items ALP-EP-1, ALP-US-12 (a child of ALP-EP-1, owned by MARS) and ALP-TASK-30 (a child of ALP-US-12).

## Acceptance criteria

```gherkin
Scenario: The card has no «Область» (BR-AW-11; feat-022's band, feat-029's card)
  When Max opens ALP-US-12's card, in the project's list or over a team's or the project's board
  Then it has no «Область» field and names no area, and its «Команда», «Исполнитель», «Статус» and every other field are shown,
    in the band the design re-lays without «Область» (feat-029's band, mod-017 design § 3), and saved as before
  And a save of any field succeeds with no area in it, and Vera's read-only card shows no «Область» either

Scenario: The list has no «Область» column (BR-AW-11)
  When Max opens Alpha's «Рабочие элементы»
  Then its columns are those it had, without «Область», and its loading skeleton has the same headers
  And the page's read takes no area path: its statement count is pinned without one (spec § NFR Query shape)
  And no list, board or backlog row of any project names an area

Scenario: A new item takes no area (BR-AW-11, BR-MX-10)
  When Max opens «Новый рабочий элемент» in Alpha
  Then its text reads «Рабочий элемент будет создан в статусе «Новая». Остальные поля заполняются в его форме.», naming no area
  When he creates a story there, a child of ALP-US-12 from its backlog row, and a task from the «Добавить задачу» of ALP-US-12's card
  Then each is created with its project, type, initial state and owning team as before (BR-MX-10), and none has an area
  And the child takes nothing from its parent but its parent and, as before, its parent's owning team

Scenario: The item's read and write carry no area (BR-AW-11; mod-003 api-contract § 1.1)
  When the panel reads ALP-US-12, the list or a backlog
  Then no item carries an area, and a create or a save stores none, whatever its body holds
  And the items' identifiers, parents, children, owning teams, states, estimates, assignees and backlog places are as before,
    and every team's and the project's backlog and board show the same items in the same order

Scenario: A project has no tree of its own (BR-AW-11, BR-AW-2)
  When Ann creates project Delta (DEL)
  Then no area is created, and «Новый проект» no longer says «Так же будет названа корневая область проекта»
  When she renames Alpha «Alpha 2»
  Then no area is renamed, and the rename dialog no longer says «Корневая область проекта получит то же название»
  And deactivating and restoring Alpha creates, renames or deletes no area; its area links are US-3's

Scenario: The project's settings hold no «Области», and its old address is retired (BR-AW-12; ADR-0028 § 7)
  When Max opens Alpha's settings
  Then their navigation has no «Области», and its areas are a section of «Общие» (US-3)
  When anyone opens `…/projects/<Alpha>/settings/areas`, a bookmark included
  Then the page reads «Страница не найдена», whether or not the user may see Alpha, and nothing redirects
  And no request addressed under a project reads the tree or creates, renames, moves or deletes an area (BR-AW-12):
    a route of mod-002 § 4 whose path the project's end of the links does not keep is answered as a route that does not exist,
    and a path it keeps reads or changes only Alpha's area links

Scenario: An area's deletion no longer looks at items (BR-AW-9; mod-003 BR-M3-24 retired)
  Given «Финансы / Аналитика» is a leaf linked to Alpha, whose items include some in «Удалена»
  When Ann deletes «Аналитика»
  Then it is deleted after US-2's confirmation, and no item of Alpha changes
```

## Owed by later lines

```gherkin
# `feat-042-area-board-and-overview`: an area reaches items through its project links, with its child areas' on request
# `feat-015-work-item-history`: the area dropped by the upgrade is no actor's change and writes no history entry
# `feat-003-work-item-search`, `mod-008-queries-dashboards`, `feat-013-board-backlog-filter`: no area filter and no Under on «Область» (their queue lines already say so)
```
