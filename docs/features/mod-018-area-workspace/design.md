# Design: The organization's typed area tree (mod-018)

Stage 3 artifact, reworked 2026-10-28 for the typed tree. Rules (BR-AW-n) and scenarios are in
`spec.md` and `stories/`; this file is layout, MUI components, states and Russian words. Every string in «» is final; ◆ marks a
string no earlier spec or design quotes. The server's refusal texts are the registry's (spec §
Error codes), refined in § 10.

- Base: [feat-028](../feat-028-org-level-teams/design.md) §§ 2-7 (the team's page, the searchable
  multiselect, the link and unlink dialogs, the project's «Команды»: every link below is that
  pattern), [mod-002](../mod-002-organizations-projects-teams/design.md) § 11 (the tree),
  [feat-029](../feat-029-work-item-card-reference/design.md) § 4.3 (the band),
  [ui-kit.md](../../conventions/ui-kit.md), ADR-0024.
- Prototype: `prototype/index.html`. **New in the kit: nothing** (§ 2).

## 1. Screen map

| Surface | Address | Stories | Change |
|---|---|---|---|
| «Области» | `/organizations/:o/settings/areas` | US-1, US-2, US-6 | the typed tree (§ 3) |
| A node's page | `…/settings/areas/:a` | US-1..US-3 | per type (§ 4) |
| Dialogs of a node | over both | US-1, US-2 | new department, new sub-area, rename, describe, move, delete (§ 5) |
| Team-link dialogs | over the department's and the team's pages | US-3 | § 6 |
| «Новый проект», «Общие»; the project's «Области» (`…/projects/:p/settings/areas`) | existing | US-3, US-4 | + «Расположение»; the subtree page stays (§ 7) |
| The card, forms, list: «Область» | project pages | US-4 | as merged; the offer is the project's subtree (§ 8) |

A node's name opens its page. **Navigation:** «Области» after «Команды» in «Организация» (id
`organization-areas`, code `organizations.area.read`); the «Проект» group keeps its «Области» (the
subtree). **Nothing is drawn for later lines** (BR-AW-5): no «Участники», «Наследовать доступ», board.
After the upgrade every project has its node at the top and no department: that is the first state
(US-5), not a fault.

## 2. Parts

| Part | Built from | Where |
|---|---|---|
| `DataTable`, `Section`, `InlineEmpty`, `MoreActionsMenu`, `Tag`, `ActionButton`, `TruncatedText`, `InlineNotice`, `FieldsSkeleton`, `Inline*Field` | kit | as named below |
| `TreeRegistry`, `PageHeader`, `FactsPanel`, `StateFrame`, `ErrorBanner`, `RegistrySkeleton`, `FormSkeleton`, `FormDialog`, `ConfirmDialog`, `NameDialog`, `DescriptionDialog`, `LinkDialog` | existing | as named below |
| **`TreeRegistry.labelTo?(node)`** | optional prop returning a path: the label becomes a router `Link` outside the Tab order (the row is the tab stop), Enter follows it, Space selects. Absent: nothing changes | the tree (D-7) |
| **`NotFoundLevel` `area`** | the heading «Область не найдена», with `keepNavigation` | a node's page |

**Type marks** (no new part): a decorative `SvgIcon` (`fontSize="small"`) and the label as text:
«Департамент» `ApartmentOutlinedIcon`, «Проект» `FolderOutlinedIcon`, «Область»
`AccountTreeOutlinedIcon`; used in the «Тип» column and the page's «Тип» fact. `LinkDialog` gains the
department's two ends (§ 6); a project node's team links use feat-028's dialogs unchanged.

## 3. «Области» (US-1, US-2, US-6)

```
Области                                  [Развернуть всё] [+ Новый департамент]
| Название               | Тип            | Описание            |     |
| ▾ Логистика            | ▣ Департамент  | Склады и перевозки  |  ⋮  |
|   ▾ Alpha              | ▢ Проект       |                     |  ⋮  |
|       Платформа        | ◇ Область      |                     |  ⋮  |
|   ▸ Beta               | ▢ Проект       |                     |  ⋮  |
| ▸ Финансы              | ▣ Департамент  |                     |  ⋮  |
```

- `PageHeader` «Области», primary **«Новый департамент»** (`AddIcon`, `area.create`; D-9). «Развернуть
  всё» ◆ / «Свернуть всё» ◆ (`UnfoldMoreIcon` / `UnfoldLessIcon`) shows while some node has children.
  The organization is **not a row** (OQ-4 A; D-8): the top level is level 1.
- `TreeRegistry`, siblings by name ignoring case, departments and project nodes mixed at the top as
  one name space. «Название»: the name as a link to the node's page; a deactivated project's node adds
  `Tag` «деактивирован» ◆. «Тип»: the type mark. «Описание»: one line, cut. Initial expansion D-3; a
  create or a move expands the parent, selects and focuses the row.
- **Row menu** «Действия: Логистика», drawn only for a holder of the code, **per type**:

| Type | Items (nothing else is drawn) |
|---|---|
| Департамент | «Переименовать», «Изменить описание» ◆, «Удалить департамент» (destructive); no move (it stands at the top), no sub-area (OQ-7 A) |
| Проект | «Новая вложенная область», «Изменить описание» ◆, «Переместить»; no rename or delete: they follow the project (OQ-10 A, OQ-5 A) |
| Область (a sub-area) | «Новая вложенная область», «Переименовать», «Изменить описание» ◆, «Переместить», «Удалить область» (destructive) |
| any node of a deactivated project | none (OQ-6 A) |

| State | The user sees |
|---|---|
| Loading | `RegistrySkeleton` with «Название», «Тип», «Описание» (+ the actions column for a holder) |
| Error | `ErrorBanner` with «Повторить» |
| Empty (no department, no project) | `StateFrame` `InboxOutlinedIcon`, «В организации пока нет департаментов и проектов»; hint «Департамент объединяет проекты и команды. Создайте первый кнопкой «Новый департамент»; проекты появляются в разделе «Проекты».» ◆ with the action «Новый департамент» for a holder of `area.create`; «Области создаёт администратор организации.» otherwise, no action |

## 4. A node's page (US-1, US-2, US-3)

```
Департамент: Логистика      [Переименовать] [Изменить описание] [Удалить департамент]
  Название  Логистика        Тип  ▣ Департамент        Расположение  Организация
  Описание  Склады и перевозки
Проекты (2)
Команды (1)                                                  [+ Добавить команду]
```

- `PageHeader` `<Тип>: <имя>` (h1; «Департамент», «Проект», «Область»); the commands are the row
  menu's of § 3 for the type, overflowing into «Ещё действия» on a narrow bar, plus «Открыть проект»
  (a `Link` button to the project's «Общие») on a project node of an active project.
- `FactsPanel`: «Название» (a project node adds «Название меняется вместе с проектом.» ◆),
  «Тип», «Расположение» (a department: «Организация»; a project node: its department's `Link` or
  «Организация»), «Родительская область» (a sub-area: the parent's path as a `Link`), «Проект» (project
  node, sub-area: «Alpha (ALP)» as a `Link` to its «Общие»), «Описание» with its line breaks,
  «Описание не задано» when none.
- **Sections by type** (`Section` h2 with `count`, `DataTable`):

| Type | Sections |
|---|---|
| Департамент | «Проекты»: its child project nodes, «Проект» (a `Link` to the node's page, `TruncatedText`), «Ключ», `Tag` «деактивирован»; read-only, no action. «Команды»: the area–team links, «Команда» (a `Link` to the team's page), `ActionButton` «Добавить команду» (§ 6), row menu «Ещё действия: MARS» with «Отключить от департамента» (§ 6.2) |
| Проект | «Вложенные области»: the direct children, «Область» and «Описание», action «Новая вложенная область» (`ThinAddIcon`). «Команды»: **feat-028's project–team links** (OQ-11 A), the same rows as the project's «Команды» (feat-028 § 7) under the sentence ◆ «Это те же связи, что в разделе «Команды» проекта.»; add and «Отключить от проекта» are feat-028's dialogs (§§ 6.1, 6.2 there), codes and texts |
| Область | «Вложенные области» as above; no team section (a sub-area takes no team link) |

Without a section's code: no button, no menu column. A node lists its own links and children only.

| State | The user sees |
|---|---|
| Loading | `FormSkeleton fields={4}`, `TableSkeleton` in each section; the title reads «Область» |
| Failed | facts: `ErrorBanner` with «Повторить»; a section: `InlineNotice` error with «Повторить» under its head |
| Children empty | `InlineEmpty` «Вложенных областей пока нет.» ◆ (a department: «В департаменте пока нет проектов.» ◆) |
| Teams empty | `InlineEmpty` «К области не привязаны команды» (a project node: feat-028's text); the button stays for a holder |
| **Deactivated project's node or sub-area** | `InlineNotice` info above the facts: «Проект «Gamma» деактивирован: область доступна только для чтения. Чтобы изменить её, восстановите проект в списке деактивированных проектов организации.» ◆; no commands, no buttons, no menus; the lists stay readable |
| Deleted, foreign, or not found | `NotFound` «Область не найдена» with the settings navigation kept and «Области» current |

## 5. Dialogs (US-1, US-2)

### 5.1 «Новый департамент»

`FormDialog`, submit «Создать»; kit fields, `frame="always"`, label above; no parent field (a
department stands at the top: OQ-3 A).

- «Название» (helper «От 1 до 100 символов.») `InlineTextField`, required, `autoFocus`; «Описание» `InlineTextArea` (helper «Необязательно. Не более 2000
  символов.»); «Команды» an `InlineMultiPickerField` (helper «Необязательно. Можно добавить позже.») (the active teams, D-1), drawn only with `area.link` and `team.link`; empty
  list «В организации пока нет команд.».
- Checked on «Создать»: «Название» trimmed 1-100 («Название должно содержать от 1 до 100 символов»),
  «Описание» ≤ 2 000 («Описание должно содержать не более 2000 символов»).
- **Refusals** land on their field, input kept: `area.name_taken` «Область с таким названием уже есть
  на этом уровне» (a sibling project node counts: OQ-12 A) → «Название»; `team.not_found` → «Команды»;
  403 and the rest → the alert. Busy: fields disabled. Done: notice «Департамент «Аналитика» создан.»,
  the row selected and focused.

### 5.2 «Новая вложенная область»

`FormDialog`, submit «Создать»: «Название *», «Родительская область *» and «Описание». «Родительская
область» is an `InlinePickerField` (not clearable) over **the project's own subtree only**: its node
and its sub-areas, by path («Логистика / Alpha», «Логистика / Alpha / Платформа»), narrowed by typed
text (D-1); preselected from a row menu or a page, changeable within that project. No department, no
other project's node is ever offered. Helper «Область создаётся внутри проекта «Alpha».» ◆.
Refusals: `name_taken` → «Название»; `depth_exceeded` «Дерево областей не может быть глубже 10
уровней», `area.not_found`, `type_not_allowed`, `outside_project` → «Родительская область»; the
rest as 5.1. Done: notice «Область «Отчёты» создана.».

### 5.3 Rename, description

- «Переименование области» (a department: «Переименование департамента» ◆): `NameDialog`, «Сохранить»,
  helper «От 1 до 100 символов»; refusal `name_taken` under the field; notice «Область переименована.»
  / «Департамент переименован.» ◆. A project node is renamed only in «Переименование проекта» (§ 7).
- «Описание области» ◆ (all types): `DescriptionDialog`, «Сохранить», one `InlineTextArea`, hint «Не более
  2000 символов.»; empty clears it; notice «Описание сохранено.» ◆.

### 5.4 Move, per type (BR-AW-3)

`FormDialog`, submit «Переместить», unavailable until a place is chosen. Nothing preselected. A
department has no move.

| Type | Title | Text ◆ | Field and offer (D-5) |
|---|---|---|---|
| Проект | «Перемещение проекта» | «Проект «Alpha» переместится вместе со всеми вложенными областями: 3. Рабочие элементы, команды и связи проекта не изменятся.» (without sub-areas: «…переместится.» and the rest) | «Новое расположение *»: «Организация» (when it is not the current place), then every department by name except the current one |
| Область | «Перемещение области» | «Область «Платформа» переместится вместе со всеми вложенными областями: 1.» | «Новая родительская область *»: the project's node and its sub-areas by path **except the area, its descendants and its current parent**; never another project's, never a department |

- **Refusals** under the field, the tree unchanged: `name_taken`, `depth_exceeded`,
  `move_into_own_subtree`, `type_not_allowed`, `outside_project`, `area.not_found` (options read
  again); the node gone, a deactivated project's node (`project.not_found`
  «Проект не найден.») and 403 → the alert.
- Done: notice «Проект «Alpha» перемещён.» ◆ / «Область «Платформа» перемещена.»; the new place is
  expanded, the row focused. A project's «Общие» opens the same dialog (§ 7).

### 5.5 Delete (BR-AW-9)

`ConfirmDialog` (destructive, focus on «Отмена»); never for a project node (it goes with its project).

| State | The user sees |
|---|---|
| Department, counting | `FieldsSkeleton count={1}`, «Удалить департамент» unavailable; the team links are read **when the dialog opens**; failed: `InlineNotice` with the server's text and «Повторить» |
| Department with teams | «Удалить департамент «Логистика»? Его связи с командами будут удалены, сами команды не изменятся.», a `List` «Команды (1)» (each team by name in `TruncatedText`; a long list scrolls inside the body: D-2), «Восстановить департамент будет нельзя.» |
| Department without teams | «Удалить департамент «Аналитика»? Восстановить департамент будет нельзя.» |
| Has children (as the tree knows) | `InlineNotice` warning with the `not_empty` text instead of the question; delete unavailable |
| Sub-area | «Удалить область «Отчёты»? Восстановить область будет нельзя.» (no team links to count) |
| Done | notice «Департамент «Логистика» удалён.» / «Область «Отчёты» удалена.»; from the node's page navigate to «Области»; in the tree focus goes to the next row |
| 409, 404, 403 | the alert with the server's text (a child or an item appeared; items point at the sub-area) / «Область не найдена.» / «Недостаточно прав»; the page or tree is read again |

## 6. Team links of a department (US-3)

Only a department takes an area–team link (OQ-8 A); the project node's are feat-028's (§ 4).

### 6.1 Add

`FormDialog` (`LinkDialog`), submit «Добавить», unavailable until one is chosen; one required
`InlineMultiPickerField` (`frame="always"`) with a sentence ◆ above it:

| End | Title | Label | Sentence |
|---|---|---|---|
| Department's page | «Добавление команд в департамент» | «Команды» | «Выбранные команды войдут в департамент «Логистика». Работу команд и доступ их участников это не меняет.» |
| Team's page | «Добавление департаментов в команду» | «Департаменты» | «Команда «MARS» войдёт в выбранные департаменты. Её работу и доступ её участников это не меняет.» |

- **Options:** the active teams or the departments by name, minus those the page lists; typed text
  narrows them (D-1); never a project node or a sub-area. All linked: «Все команды уже входят в департамент.» ◆ / «Команда уже входит во
  все департаменты.» ◆. Nothing matches: «Ничего не найдено». **None exist**: an info `InlineNotice` replaces the
  field, «Добавить» unavailable: «В организации пока нет команд. Создайте первую в
  разделе «Организация» → «Команды».» / «В организации пока нет департаментов. Создайте первый в
  разделе «Организация» → «Области».» ◆ (the section's name a `Link`).
- **States:** loading «Загрузка…»; failed `InlineNotice` «Не удалось загрузить команды. Попробуйте ещё
  раз.» (…«департаменты»…) with «Повторить»; busy, all disabled.
- **Done** (all chosen in one operation, or none): closes, the page is read again; «Команда «MARS»
  добавлена.» ◆ / «Команды добавлены: 3.» ◆ / «Департамент «Логистика» добавлен.» ◆ / «Департаменты
  добавлены: 3.» ◆.
- **Refused:** 404 for a chosen end (nothing linked) and `area.link_type_not_allowed` (§ 10) under the
  field; the page's own end gone, and 403 «Недостаточно прав» → the alert.

### 6.2 Unlink

`ConfirmDialog` (destructive, focus on «Отмена», confirm «Отключить»), nothing counted. Department's
page, a team: «Отключение команды от департамента», «Отключить команду «MARS» от департамента
«Логистика»? Сама команда и её рабочие элементы не изменятся.» ◆ Team's page, a department:
«Отключение департамента от команды», «Отключить департамент «Логистика» от команды «MARS»? Сам
департамент и команда не изменятся.» ◆ Done: «Команда отключена от департамента.» ◆ / «Департамент
отключён от команды.» ◆. An unlinked pair is a success (BR-AW-6); 404 and 403 → the alert.

## 7. The project's surfaces (US-3, US-4)

- **«Новый проект»:** after the existing fields, **«Расположение»** (`InlinePickerField`, not
  clearable) over «Организация» (first, the default) and every department by name; hint ◆
  «Необязательно. Без департамента проект появится на верхнем уровне.»; drawn for a holder of
  `area.write`, else absent (the project stands at the top).
  Refusals: `name_taken` («Область с таким названием уже есть на этом уровне») → «Название»,
  nothing created, the key stays free; `area.not_found` → «Расположение» (the list is read again).
- **«Общие»:** below the facts, **«Расположение»** (a department's name as a `Link`, or «Организация»)
  with the command «Переместить» ◆ (`area.write`) opening § 5.4's dialog for the project.
- **«Области» of the project** (`…/projects/:p/settings/areas`): `TreeRegistry` of the project's node
  and its sub-areas (no «Тип» column), header «Области» with «Новая вложенная область» (`area.write`);
  row menus as § 3. Loading and error as § 3; a node without sub-areas shows the node and under it `InlineEmpty` «У проекта пока нет вложенных областей. Создайте первую кнопкой «Новая
  вложенная область».» ◆ (a reader: «У проекта нет вложенных областей.» ◆). Names open the node's page.
- **Words that change:** «Новый проект»: the name's helper «От 1 до 100 символов. Так же будет называться
  корневая область проекта.»; «Переименование проекта»: «Корневая область переименуется вместе с
  проектом. Ключ, описание и команды проекта не изменятся.» ◆; «Деактивировать проект?»: as merged
  plus «Область проекта сохранится и станет доступной только для чтения.» ◆; «Удаление команды»
  (feat-028 § 5.3) gains before «Восстановить команду будет нельзя.»: «Связи команды с департаментами
  тоже будут удалены.» ◆.

## 8. The work item's «Область» (US-4)

Nothing is retired: the card's band, the list column, «Новый рабочий элемент» and the forms are as
merged (feat-029 § 4.3, mod-017 § 3, mod-003); the earlier D-6 rearrangement is withdrawn. What
changes is the offer: every «Область» picker (card, creation dialog, a list filter where one exists)
reads **the project's node and its sub-areas only**, by the project-relative path as before («Alpha /
Платформа / API»; the node itself «Alpha»), never a department, another project's node or the
organization. It is the same `InlinePickerField` with D-1's local filter and is never empty (the node
exists). An area outside the project is refused as `area.not_found` «Область не найдена.» on the field.
A project's move or rename changes no item and shows at once in the card's path.

## 9. Retired from the first build

The area–project links and dialogs, the area form's «Проекты» picker, the item without «Область», the
band's rearrangement.

## 10. Error copy (spec § Error codes; Russian final)

| Code | Status | Text | Shown |
|---|---|---|---|
| `organizations.area.type_not_allowed` | 409 | «Область этого типа нельзя разместить в выбранном месте. Департамент стоит на верхнем уровне, проект — на верхнем уровне или в департаменте, вложенная область — внутри своего проекта.» ◆ | the field of the place (§§ 5.2, 5.4); the alert otherwise |
| `organizations.area.outside_project` | 409 | «Вложенную область нельзя переместить за пределы её проекта» (as the spec) | «Новая родительская область» |
| `organizations.area.not_empty` (**changed**) | 409 | «Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы» ◆ | § 5.5's warning and alert (one code: a department with projects, a node with children, a sub-area with items) |
| **new** `organizations.area.link_type_not_allowed` | 409 | «Команду можно связать только с департаментом. Команды проекта подключаются на странице проекта.» ◆ | § 6.1's field or alert; a direct request on a project node or a sub-area |
| `organizations.area.root_is_fixed` (kept) | 409 | «Корневая область меняется только вместе с проектом» | the alert, on a direct rename or deletion of a project node (no control offers either) |
| `organizations.project.not_found` | 404 | «Проект не найден.» | a write on a deactivated project's node (the alert) |

## 11. Roles

| Control | «Администратор» | «Участник», «Читатель» | Not a member |
|---|---|---|---|
| The tree, a node's page, its lists (`area.read`) | sees | sees | «Организация не найдена» |
| «Новый департамент» (`area.create`); «Расположение» in «Новый проект» and «Общие» | offered | - | - |
| Rename, delete, «Новая вложенная область», description, move (`area.write`) | offered | - | - |
| «Добавить команду / департамент», «Отключить от …» of a department (`area.link` + `team.link`, or the code of the page's end) | offered | - | - |
| A project node's «Команды» commands | feat-028's codes | - | - |
| Any node of a deactivated project | read only | read only | - |

A missing code hides the control; a direct write answers 403 «Недостаточно прав» and the codes are read again. Until `mod-019-scoped-access` only «Администратор» holds them.

## 12. Narrow widths and accessibility

Down to 640 CSS px (ADR-0024): the tree and tables scroll inside their card, the page never does;
names are cut, page heads overflow into «Ещё действия»; `FactsPanel` stacks label over value. The tree is a treegrid named «Области» (toggle
«Развернуть «Логистика»», row menu «Действия: Логистика»; in a table «Ещё действия: MARS»); tables are
named «Проекты департамента», «Команды департамента», «Вложенные области», «Команды проекта»,
«Департаменты команды». A form dialog opens on its first field, delete and unlink on «Отмена»; a refusal
focuses the field it names; after a dialog focus returns to the row, the next, or the section's button.

## 13. For the developer

- **Contract needs:** the tree `{id, parentId, type, name, description, path, projectId,
  projectActive}` in one read; a node with its parent, children, `description`; a department's teams and
  a team's departments; a project node's teams (feat-028's read); `description`, `teamIds` on a
  department's creation; `areaIds` (departments) on a team's; `parentId` (a department or none) on a
  project's creation and move; a sub-area move.
- **Refusals:** `RefusalField` gains `description`, `teams`, `areas`, `location`; `name_taken` is
  «Название» in a create or rename and the place field in a move. `notFoundLevel` maps
  `area.not_found` to `area` for a page's first read. Texts in `lib/areaTexts.ts`.
- **Routes, constants:** `ORG_AREAS_ROUTE`, `ORG_AREA_ROUTE`, `organizationAreasPath`,
  `organizationAreaPath`, the project-nested `AREAS_ROUTE` kept; `TREE_EXPAND_ALL_LIMIT` = 100,
  `PICKER_OPTION_LIMIT` = 50. Any write refreshes the tree, the node, its lists and the pickers.
- **Tests:** states of §§ 3-8 per type, the deactivated node, expansion, the picker's cap, per-type
  offers and menus, refusals, roles. New files use kit parts only.

## 14. Decisions

D-1..D-7 were answered A at gate-2 on 2026-10-18 for the old spec and re-checked 2026-10-28 against the
typed tree (fate marked). D-8..D-10 are new and open.

- [x] **D-1 The searchable pickers — kept.** (A, recommended) A local filter over the tree's one read and
  the active teams; at most 50 matches, then «Показаны первые 50 из 312. Уточните запрос.» ◆. (B)
  Server search per field. Answered A; holds for every picker here.
- [x] **D-2 The deletion's links — amended.** (A, recommended) Every link named in a scrolling list;
  now only a department has links (its teams). (B) Counts and «Показать все». Answered A.
- [x] **D-3 The tree's initial expansion — kept.** (A, recommended) All expanded up to 100 nodes, the
  top level above, with «Развернуть всё» / «Свернуть всё». (B) Top level always. (C) Always all.
  Answered A.
- [x] **D-4 Where a project's row leads — retired.** The area page no longer lists projects as links;
  a department's «Проекты» are children opening the node's page, «Открыть проект» goes to «Общие».
- [x] **D-5 The pickers' offer — amended.** (A, recommended) Every place except the structurally
  impossible ones: the node, its descendants, its current place **and what the type rule forbids**;
  depth and name rules are refused on saving with the server's text. (B) Also grey out what the
  client sees would be refused. Answered A.
- [x] **D-6 The card's band without «Область» — retired:** the item's «Область» stays, § 8.
- [x] **D-7 The tree component — kept.** (A, recommended) `TreeRegistry` with `labelTo`, type marks in a
  column. (B) A kit-grade tree table: tech-017's work. Answered A.
- [ ] **D-8 Is the organization drawn in the tree? (OQ-4 A)** (A, recommended) No row: top-level rows are
  level 1, and «Организация» appears only as the «no department» choice in «Расположение»; it is not
  a node, so no actions, page or 404 to explain. (B) A fixed first row «Организация» over everything: the structure at a
  glance, but it looks like a node with a page and an empty menu.
- [ ] **D-9 The header command's name.** (A, recommended) «Новый департамент» (the only node created
  there: projects have «Новый проект», sub-areas their project's); it deviates from the stories'
  «Новая область» (US-1), whose copy the design fixes. (B) «Новая область» creating a department: the
  stories' words, but «area» for what is only a department.
- [ ] **D-10 The team page's section name.** (A, recommended) «Департаменты» (section, dialog, notices):
  only departments are linkable (OQ-8 A), so the name is true; the stories say «Области». (B) «Области»
  as written: one word for the tree and the page, but it promises nodes the section refuses
  (`link_type_not_allowed`).

## 15. Stories to screens

US-1: §§ 3, 4, 5.1, 5.2. US-2: §§ 3, 4, 5.3-5.5, 7. US-3: §§ 4, 6, 7. US-4: §§ 7, 8. **US-5 has no
screen:** its first state is § 3's tree with project nodes only. US-6 is § 11 (the tenant answers are
the backend's).
