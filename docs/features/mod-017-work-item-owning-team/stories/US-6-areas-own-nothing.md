# US-6: Areas stop owning, teams stop having default areas

- story: US-6
- depends-on: US-4
- shippable: no — one of the seven stories of `mod-017-work-item-owning-team`; it retires what US-4 replaces, and the upgrade it specifies is the line's one data change
- status: pending

As an **organization administrator**, I want **an area to be only a place in the project's tree, and a team to be only a group of people with its own items**, so that **moving, renaming or deleting an area never moves anyone's work, and no team has an area it must keep** (BR-MX-9, BR-MX-17; mod-002 BR-M1-11, BR-M1-14 retired; ADR-0028 rule 2).

## Acceptance criteria

Data: «Альфа» (A, root area «Альфа») has MARS and VENUS and the areas «Склад» and «Склад / Полки» (paths below the root, mod-002 DC-9). Before this line MARS owned the root, VENUS owned «Склад» and its default area was «Склад / Полки»; ALP-US-12 sat in the root, ALP-US-20 in «Склад / Полки». Ann is an «Администратор», U an «Участник». The item's «Область» stays as a classification path until `mod-018-area-workspace` retires it (doc-012 OQ-1 A); this story takes away only what an area *did*.

```gherkin
Scenario: The area tree shows names and nothing owned (BR-OT-10)
  When Ann opens A's «Области»
  Then the tree has the column «Область» only: no «Владелец», no «унаследовано» and no «По умолчанию» chip
  And the root's menu holds «Новая дочерняя область» only, and another area's «Новая дочерняя область», «Переименовать», «Переместить» and «Удалить»
  And «Изменить владельца» and «Владелец области» are nowhere
  And U, an «Участник», is offered no action, as before

Scenario: A new area has a name and a place, and no owner (BR-OT-10)
  When Ann opens «Новая область» under the root
  Then the dialog asks for «Название» alone: no «Владелец» and no «Наследовать от родительской области»
  And the area created stores a name and a parent only, and the tree read carries no owner, no inherited mark and no default mark

Scenario: The owner and default-area writes are gone (BR-OT-10; Breaks cell)
  When a request sets an area's owner, or a team's default area, as mod-002 api-contract §§ 4.5 and 3.5 did
  Then it answers as a route that does not exist
  And the area, the team and every item are as before

Scenario: Moving or renaming an area moves no item (BR-OT-5, BR-OT-10; BR-MX-9)
  When Ann moves «Склад» under another area of A and renames «Полки»
  Then ALP-US-12 and ALP-US-20 stay on the views of the teams that own them and of A, with the same «Команда»
  And their «Область» shows the area's current path, and «Изменён» is unmoved

Scenario: Deleting an area is refused for children and items alone (BR-OT-10; mod-002 BR-M1-13 narrowed)
  When Ann deletes «Склад», which has «Полки»
  Then it is refused with «Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области»
  When Ann deletes «Склад / Полки» after ALP-US-20 left it
  Then it is deleted, though it was VENUS's default area
  And the root answers «Корневая область меняется только вместе с проектом» (kept)
  And no answer says «Область используется как область по умолчанию команды»

Scenario: The item's «Область» is a classification path and changes no team (BR-OT-10)
  Given a new top-level item is created in A from MARS's backlog, and another from VENUS's board
  Then both are in the root area «Альфа», whichever team they start with (US-2)
  And a child created beneath ALP-US-20 is in «Склад / Полки», its parent's area as stored (feat-027 BR-T-10 kept for the area)
  When U saves a child's «Область» as another area
  Then «Область» changes and «Команда» does not

Scenario: A team has no area (BR-OT-11)
  When Ann opens A's «Команды» and VENUS's own page
  Then the table has the column «Команда» only, with no «Область по умолчанию» and no «Изменить область по умолчанию»
  And VENUS's page has the fact «Название» only, with no «Область по умолчанию» and no «Изменить»
  And «Владеет корневой областью» is nowhere, and MARS's row has «Переименовать» and «Удалить команду», as VENUS's has

Scenario: Adding or renaming a team touches no area (BR-OT-11; BR-MX-2, the team half)
  When Ann adds the team «Склад», while the area «Склад» exists under the root
  Then the team is created: no «Область с таким названием уже есть на этом уровне» can come from a team, and no area is created
  And the dialog «Новая команда» reads «От 1 до 100 символов.» without the sentence about the root area
  When Ann renames «Склад» to «Хранение»
  Then «Переименование команды» does not say «Названия областей команды не изменятся.», and the area «Склад» is as it was

Scenario: A project is still created with a team and a root area, now plain (BR-OT-11; BR-MX-2 owed by `feat-028-org-level-teams` and `mod-018-area-workspace`)
  When Ann creates the project «Гамма» with the key GAM
  Then the dialog's helper reads «От 1 до 100 символов. Так же будут названы команда проекта и его корневая область.»
  And «Гамма» has the team «Гамма», with Ann its only member, and the root area «Гамма», owned by nobody
  And renaming the project renames the root area and not the team, as «Корневая область проекта получит то же название. Ключ и названия команд не изменятся.» says

Scenario: The upgrade keeps every item on the team its area gave it (BR-OT-14; doc-012 § Existing data, OQ-4 A)
  Given a database at the previous migration, with the data above, an item in each area, and MARS's backlog placed
  When the migration of this line runs
  Then ALP-US-12 is owned by MARS (the root's owner), ALP-US-20 by VENUS («Склад / Полки» inherits «Склад»), and an item in an area below the root that, like every area between it and the root, has no owner of its own, by the root's owner
  And no item's «Изменён» moved, and no project names a default team
  And every team place that names the item's owner is as it was, so every team's backlog lists what it listed, in its order
  And every area keeps its name, parent and items, and no area holds an owner or a default mark any more
  And the migration is an additive migration on top of the merged ones, edits none, and logs how many items it mapped, how many markers it dropped and how many stale team places it deleted (next scenario)

Scenario: A team place that names a former owner is dropped, and the item lists unplaced (BR-OT-14, BR-OT-6; ADR-0028 rule 5; doc-012 § Existing data)
  Given a database at the previous migration, where ALP-US-22 stood in «Склад», VENUS's, and was placed second on VENUS's «Истории» backlog
  And U then saved its «Область» as «Альфа», MARS's, and neither VENUS's nor MARS's backlog was reordered since, so VENUS's place named a team that no longer owned it and was not read
  When the migration of this line runs
  Then ALP-US-22 is owned by MARS and holds no team place, and VENUS's backlog does not list it
  And MARS's «Истории» backlog lists it unplaced, below every placed item
  And the places VENUS's backlog held for the items it still owns are as they were
  And the migration logged this place among the stale team places it deleted, and the MR names the stale places as the data dropped
  When U later sets ALP-US-22's «Команда» to «VENUS»
  Then it stands unplaced below every placed item on VENUS's backlog, not second: no former place is remembered (BR-OT-6)

Scenario: A deactivated project and a deleted team are upgraded like the rest (BR-OT-14)
  Given the deactivated project «Дельта», whose item sat in an area that passed to its first team when another team was deleted
  When the migration runs and Ann restores «Дельта»
  Then the item is owned by that first team, and no item of any project is owned by a deleted team
```

## Owed by later lines

```gherkin
# `mod-018-area-workspace`: the item's «Область», the list's column, the card field, the area delete guard (BR-M3-23, BR-M3-24)
#   and the per-project tree itself retire (doc-012 OQ-1 A, BR-MX-3..-8)
# `feat-028-org-level-teams`: no team is created with a project; `organizations.area.*` keeps its codes until `mod-018-area-workspace` redefines them
```
