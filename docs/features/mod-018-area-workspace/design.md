# Design: The organization's area workspace (mod-018)

Stage 3 artifact. Rules (BR-AW-n) and scenarios (US-1..US-6) are in `spec.md` and
`stories/`; this file is layout, MUI components, states and Russian words, and nowhere
else. Every string in «» is final; ◆ marks a string no earlier spec or design quotes.
The server's refusal texts are the registry's (spec § Error codes).

- Base: [feat-028](../feat-028-org-level-teams/design.md) §§ 2-6 (the team's page, the
  searchable multiselect, the link and unlink dialogs: every area end below is that
  pattern), [mod-017](../mod-017-work-item-owning-team/design.md) § 3 and
  [feat-029](../feat-029-work-item-card-reference/design.md) § 4.3 (the band),
  [mod-002](../mod-002-organizations-projects-teams/design.md) § 11 (the tree),
  [ui-kit.md](../../conventions/ui-kit.md), ADR-0024, ADR-0028 § 7.
- Prototype: `prototype/index.html` (the stage file asks for one on an L).
- **New in the kit: nothing** (one prop on `TreeRegistry`, D-7; one `NotFoundLevel`, § 2).

## 1. Screen map

| Surface | Address (below `/organizations/:o`) | Stories | Change |
|---|---|---|---|
| «Области» | `/settings/areas` | US-1, US-2, US-6 | **rebuilt** over the organization (§ 3) |
| An area's page | `/settings/areas/:a` | US-1..US-3 | **new** (§ 4) |
| Dialogs of an area | over both | US-1, US-2 | create, rename, describe, move, delete (§ 5) |
| Link and unlink dialogs | over the three ends | US-3 | **new** (§ 6) |
| The project's «Общие», the team's page | `/projects/:p/settings/general`, `/settings/teams/:t` | US-3 | + section «Области» (§ 7) |
| «Новый проект», «Новая команда» | dialogs | US-3 | + field «Области» (§ 7) |
| The card, the list, «Новый рабочий элемент» | project pages | US-4 | **without «Область»** (§ 8) |
| `…/projects/:p/settings/areas` | - | US-4 | **retired** (§ 9) |

An area's name opens its page wherever it stands; a project's or team's name on an area's
page opens that end (D-4). **Navigation** (`app/navigation/model.ts`): a new entry in
«Организация» after «Команды»: id `organization-areas`, «Области», scope organization, code
`organizations.area.read`; an area's page is beneath it. The «Проект» group loses its
«Области».

**Nothing is drawn for what later lines bring** (BR-AW-5, doc-013 BR-NE-3): no «Участники»,
«Наследовать доступ», board or backlog of an area. After the upgrade no organization has an area
(US-5): the empty states of §§ 3, 4, 7 are the first state, not a fault.

## 2. Parts

| Part | Built from | Where |
|---|---|---|
| `DataTable`, `Section`, `InlineEmpty`, `MoreActionsMenu`, `Tag`, `ActionButton`, `TruncatedText`, `InlineNotice`, `FieldsSkeleton`, `InlineTextField`, `InlineTextArea`, `InlinePickerField`, `InlineMultiPickerField` | kit | pages, dialogs |
| `TreeRegistry`, `PageHeader`, `FactsPanel`, `StateFrame`, `ErrorBanner`, `RegistrySkeleton`, `FormSkeleton`, `FormDialog`, `ConfirmDialog`, `NameDialog`, `DescriptionDialog`, `LinkDialog` | existing | as named below |
| **`TreeRegistry.labelTo?(node)`** | an optional prop returning a path: the label becomes a router `Link` outside the Tab order (the row is the tab stop), Enter on a row follows it, Space selects. Absent: nothing changes | the tree (D-7) |
| **`NotFoundLevel` `area`** | the heading «Область не найдена», used with `keepNavigation`, as the team's | an area's page |

`LinkDialog` grows from two ends to four (§ 6.1).

## 3. «Области» (US-1, US-2, US-6)

```
Области                                       [Развернуть всё] [+ Новая область]
| Область                    | Описание                     |     |
| ▾ Логистика                | Склады и перевозки           |  ⋮  |
|     Складские системы      |                              |  ⋮  |
| ▸ Финансы                  |                              |  ⋮  |
```

- `PageHeader` «Области», primary «Новая область» (`AddIcon`, `area.create`). The view command
  «Развернуть всё» ◆ (`UnfoldMoreIcon`) / «Свернуть всё» ◆ (`UnfoldLessIcon`) shows while some
  area has children, one command by the tree's state. The old header command «Новая дочерняя область» (it waited for a selected
  row) goes: the row menu and the area's page carry it.
- `TreeRegistry`, siblings by name ignoring case (the server's order). «Область»: the name as a
  link to the area's page; «Описание»: one line, cut, empty when none. The initial expansion is
  D-3; a create or a move expands the parent, selects and focuses the row.
- **Row menu** «Действия: Логистика», drawn only for a holder of a code: «Новая дочерняя
  область», «Переименовать», «Изменить описание» ◆, «Переместить» (all `area.write`; a top-level
  area's move also `area.create`), «Удалить область» (destructive). Each opens § 5's dialog; the
  server refuses what it must.

| State | The user sees |
|---|---|
| Loading | `RegistrySkeleton` with «Область», «Описание» (+ the actions column for a holder) |
| Error | `ErrorBanner` with «Повторить» |
| Empty | `StateFrame` `InboxOutlinedIcon`, «В организации пока нет областей»; hint «Область объединяет проекты и команды организации. Создайте первую кнопкой «Новая область».» ◆ and the action «Новая область» for a holder of `area.create`; «Области создаёт администратор организации.» otherwise, no action |

## 4. An area's page (US-1, US-2, US-3)

```
Область: Складские системы   [Переименовать] [Изменить описание] [Переместить] [Удалить область]
  Название                Складские системы
  Родительская область    Логистика
  Описание                Складское ПО и интеграции
Вложенные области (0)                                    [+ Новая дочерняя область]
Проекты (3)                                              [+ Добавить проект]
Команды (1)                                              [+ Добавить команду]
```

- `PageHeader` `Область: <имя>` (the h1), the four commands each by its code (§ 10), overflowing
  into «Ещё действия» on a narrow bar. `FactsPanel`: «Название»; «Родительская область», the
  parent's path as a `Link` to its page, «Верхний уровень» ◆ in `text.secondary` at the top level;
  «Описание» keeps its line breaks, none reads «Описание не задано» (as feat-028 § 4).
- **«Вложенные области»:** `Section` (h2, `count`), `DataTable` «Область» (a link in
  `TruncatedText`) and «Описание»; the direct children only, by name. Action «Новая дочерняя
  область» (`ThinAddIcon`, `area.write`). No row menu: a child is acted on from its own page.
- **«Проекты», «Команды»:** `Section` with `ActionButton` «Добавить проект» / «Добавить команду»
  (`ThinAddIcon`), then `DataTable`: «Проект» (link, D-4) and «Ключ»; «Команда» (link to the
  team's page). Row menu «Ещё действия: Alpha» with «Отключить от области» (destructive, § 6.2).
  Active projects only: a deactivated project's link stays stored and is not listed (BR-AW-10).
  The area's own links only, never its descendants'. Without the codes of § 10: no button, no
  menu column.

| State | The user sees |
|---|---|
| Loading | `FormSkeleton fields={3}` for the facts, `TableSkeleton` in each section; the title reads «Область» |
| Facts failed | `ErrorBanner` with «Повторить» in place of the facts; sections load on their own |
| A section failed | `InlineNotice` error with «Повторить» under its head |
| Children empty | `InlineEmpty` «Вложенных областей пока нет.» ◆ |
| Projects / teams empty | `InlineEmpty` «К области не привязаны проекты» / «К области не привязаны команды» (the button stays for a holder) |
| Deleted, foreign or dropped by the upgrade | `NotFound` «Область не найдена» with the settings navigation kept and «Области» current |

## 5. Dialogs of an area (US-1, US-2)

### 5.1 «Новая область»

`FormDialog`, submit «Создать»; kit fields, `frame="always"`, label above:

```
Новая область                                                          ✕
Название *            [                                     ]  «От 1 до 100 символов.»
Родительская область  [ Верхний уровень                    ⌄]  «Необязательно. По умолчанию область создаётся на верхнем уровне.»
Описание              [                                     ]  «Необязательно. Не более 2000 символов.»
Проекты, Команды      [ Начните вводить название           ⌄]  «Необязательно. Можно добавить позже.»
                                                       Отмена  [Создать]
```

- «Название» `InlineTextField`, required, `autoFocus`; «Описание» `InlineTextArea`. «Родительская
  область» is `InlinePickerField` (not clearable) over «Верхний уровень» (first; only with
  `area.create`) and every area by its path (D-1, D-5). It starts at «Верхний уровень» from the
  header, and at the area itself from a row menu or a page section (changeable). «Проекты» and
  «Команды» are two `InlineMultiPickerField`s (option: name, the key in `text.secondary`), each
  drawn only with `area.link` and the other end's link code; an empty list says «В организации нет
  активных проектов.» / «В организации пока нет команд.».
- Checked on «Создать», nothing sent while refused: «Название» trimmed 1-100 («Название должно
  содержать от 1 до 100 символов»), «Описание» at most 2 000 as typed («Описание должно содержать
  не более 2000 символов»).
- **Refusals** land on their field, input kept: `area.name_taken` «Область с таким названием уже
  есть на этом уровне» → «Название»; `area.depth_exceeded` «Дерево областей не может быть глубже
  10 уровней» and `area.not_found` «Область не найдена.» (the parent gone; the options are read
  again) → «Родительская область»; `project.not_found` → «Проекты»; `team.not_found` → «Команды»
  (a choice no longer offered drops); the length codes → their field; 403 and the rest → the
  dialog's alert. Busy: fields disabled, Escape ignored.
- Done: closes, the tree or the page is read again, notice «Область «Аналитика» создана.»; the new
  row is selected and focused.

### 5.2 Rename, description

- «Переименование области»: today's `NameDialog` (submit «Сохранить», helper «От 1 до 100
  символов»), refusal `name_taken` under the field, notice «Область переименована.».
- «Описание области» ◆: `DescriptionDialog` (feat-028 § 5.2), «Сохранить», one `InlineTextArea`
  «Описание» with the stored text, hint «Не более 2000 символов.»; an empty text clears it.
  Notice «Описание области сохранено.» ◆.

### 5.3 «Перемещение области»

`FormDialog`, submit «Переместить» (unavailable until a place is chosen). It names what moves:
«Область «Логистика» переместится вместе со всеми вложенными областями: 2.»; a leaf: «Область
«Складские системы» переместится.» ◆. Field «Новая родительская область», `InlinePickerField`,
nothing preselected, offering by D-5: «Верхний уровень» (unless the area is top-level, and only with
`area.create`), then every area by path **except the area, its descendants and its current parent**.

- **Refusals** under the field, the tree unchanged: `name_taken`, `depth_exceeded`,
  `move_into_own_subtree` («Нельзя переместить область внутрь неё самой или её вложенной
  области»), `area.not_found` for the destination (options read again); the area itself gone → the
  alert, the page read again; 403 → the alert.
- Done: notice «Область «Логистика» перемещена.»; the new parent is expanded, the row selected and
  focused.

### 5.4 «Удаление области» (BR-AW-9)

`ConfirmDialog` (destructive, focus on «Отмена», confirm «Удалить область»). The links are read
**when the dialog opens**:

| State | The user sees |
|---|---|
| Counting | `FieldsSkeleton count={2}`; «Удалить область» unavailable |
| Count failed | `InlineNotice` with the server's text and «Повторить»; deletion unavailable |
| With links | «Удалить область «Складские системы»? Её связи будут удалены, сами проекты и команды не изменятся.», then two `List`s, «Команды (1)» and «Проекты (2)» (`ul`, one `ListItem` per link by name, the name in `TruncatedText`, a `Tag` «деактивирован» after a deactivated project), then «Восстановить область будет нельзя.»; **every link named** (D-2), a long list scrolls inside the dialog body |
| No links | «Удалить область «Аналитика»? Восстановить область будет нельзя.» |
| Has child areas (as the tree knows) | `InlineNotice` warning «Нельзя удалить область, в которой есть вложенные области» in place of the question; «Удалить область» unavailable; the links are not read |
| Done | closes; notice «Область «Складские системы» удалена.»; on the area's page, navigate to «Области»; in the tree, focus goes to the parent's row |
| 409 | the alert with the server's text (a child appeared meanwhile); the tree is read again |
| 404 | the alert «Область не найдена.»; on close the page reads itself again |
| 403 | the alert «Недостаточно прав»; the codes are read again |

## 6. Links (US-3)

### 6.1 «Добавление … » (four ends, one component)

`FormDialog`, submit «Добавить», unavailable until one is chosen; one required
`InlineMultiPickerField` (`frame="always"`) with the sentence ◆ above it:

| End | Title | Label | Sentence |
|---|---|---|---|
| Area, projects | «Добавление проектов в область» | «Проекты» | «Выбранные проекты войдут в область «Логистика». Их рабочие элементы и доступ к ним это не меняет.» |
| Area, teams | «Добавление команд в область» | «Команды» | «Выбранные команды войдут в область «Логистика». Работу команд и доступ их участников это не меняет.» |
| Project | «Добавление областей в проект» | «Области» | «Проект «Alpha» войдёт в выбранные области. Его рабочие элементы и доступ к ним это не меняет.» |
| Team | «Добавление областей в команду» | «Области» | «Команда «MARS» войдёт в выбранные области. Её работу и доступ её участников это не меняет.» |

- **Options:** the organization's active projects (name, the key beneath), active teams, or areas
  (each by its path), read when the dialog opens, minus the ones the page lists as linked; typed
  text narrows them (D-1).
- All linked: «Все проекты уже входят в область.» ◆ / «Все команды уже входят в область.» ◆ /
  «Проект уже входит во все области.» ◆ / «Команда уже входит во все области.» ◆. Nothing matches:
  «Ничего не найдено». **None exist** replaces the field with an info `InlineNotice` and leaves
  «Добавить» unavailable: «В организации нет активных проектов.» / «В организации пока нет команд.
  Создайте первую в разделе «Организация» → «Команды».» / «В организации пока нет областей.
  Создайте первую в разделе «Организация» → «Области».» ◆, the section's name a `Link`.
- **States:** loading, «Загрузка…» in the list, the field works; failed, `InlineNotice` «Не удалось
  загрузить проекты. Попробуйте ещё раз.» (…«команды»…, …«области»…) with «Повторить», «Добавить»
  unavailable; busy, all disabled.
- **Done** (all chosen are linked in one operation, or none): closes, the page is read again; on an
  area's page «Проект «Alpha» добавлен.» ◆ / «Проекты добавлены: 3.» ◆ and «Команда «MARS»
  добавлена.» ◆ / «Команды добавлены: 3.» ◆; on a project's or team's page «Область «Логистика»
  добавлена.» ◆ / «Области добавлены: 3.» ◆.
- **Refused:** 404 for a chosen end («Проект не найден.», «Команда не найдена.», «Область не
  найдена.»; nothing was linked) under the field, the options read again, a choice no longer offered
  dropped; the page's own end gone → the alert, the page read again; 403 → the alert «Недостаточно
  прав».

### 6.2 «Отключение …» (unlink)

`ConfirmDialog` (destructive, focus on «Отмена», confirm «Отключить»); nothing is counted, an
unlink changes nothing else (BR-AW-8):

| Page, row | Title | Text ◆ |
|---|---|---|
| Area, a project | «Отключение проекта от области» | «Отключить проект «Alpha» от области «Логистика»? Сам проект и его рабочие элементы не изменятся.» |
| Area, a team | «Отключение команды от области» | «Отключить команду «MARS» от области «Логистика»? Сама команда и её рабочие элементы не изменятся.» |
| Project, an area | «Отключение области от проекта» | «Отключить область «Логистика» от проекта «Alpha»? Сама область и проект не изменятся.» |
| Team, an area | «Отключение области от команды» | «Отключить область «Логистика» от команды «MARS»? Сама область и команда не изменятся.» |

Done: closes, the page is read again; notice «Проект отключён от области.» ◆ / «Команда отключена
от области.» ◆ / «Область отключена от проекта.» ◆ / «Область отключена от команды.» ◆. Busy: both
buttons inert, Escape ignored. An unlinked pair is a success (BR-AW-6). 404 → the alert («Область
не найдена.», «Проект не найден.», «Команда не найдена.»), the page read again; 403 → the alert
«Недостаточно прав».

## 7. The other ends and the creation forms (US-3, US-4)

- **The project's «Общие»:** below the facts, `Section` «Области» (`count`) with `ActionButton`
  «Добавить область» (`ThinAddIcon`), `DataTable` «Область» (the path as a link in `TruncatedText`
  to the area's page) and a row menu «Ещё действия: Логистика» with «Отключить от проекта»
  («…от команды» on a team's page; § 6.2). Empty: `InlineEmpty` «Проект не входит ни в одну
  область»; failed: `InlineNotice` with «Повторить»; loading: `TableSkeleton`.
- **The team's page:** the same section between «Проекты» and «Участники команды»; empty «Команда
  не входит ни в одну область».
- **«Новый проект», «Новая команда»:** after the existing fields, `InlineMultiPickerField`
  «Области» over the areas by path (D-1), hint «Необязательно. Области можно добавить позже на
  странице проекта.» ◆ / «…на странице команды.» ◆, drawn only with the link codes of both ends;
  with no area its list says «В организации пока нет областей.». Refusal `area.not_found` →
  «Области» (the list is read again, a vanished choice drops); the rest as feat-028 § 8.
- **Words that stop being true** (US-4): «Новый проект»: the name's helper reads «От 1 до 100
  символов.». «Переименование проекта»: «Ключ, описание, команды и области проекта не изменятся.» ◆.
  «Деактивировать проект?»: «Проект «Alpha» (ALP) пропадёт из списков и переключателя, а его
  страницы перестанут открываться. Ничего не будет удалено, связи с командами и областями
  сохранятся: проект можно восстановить в списке деактивированных проектов организации.» ◆.
  «Удаление команды» (feat-028 § 5.3) gains a line before «Восстановить команду будет нельзя.»:
  «Связи команды с областями тоже будут удалены.» ◆ (BR-AW-10).

## 8. The work item without «Область» (US-4)

- **The card** (D-6): block B holds **«Команда» in row 1** (`CardTeamField`, label column 82, its
  label stands 303 from the card's left edge as «Область» did) and the place of «Итерация»
  (mod-007) in row 2; block A is «Статус» and the place of «Причина». The band keeps its 76 px.
  Below a card width of 720 the blocks stack: «Статус», «Команда». The draft, the band's area
  error and the area refusal retire: a save names no area; a read-only card shows none either.
- **The list «Рабочие элементы»:** seven columns, «ID», «Тип», «Заголовок», «Исполнитель»,
  «Статус», «Команда», «Изменён»; the skeleton has the same seven headers.
- **«Новый рабочий элемент»:** the line reads «Рабочий элемент будет создан в статусе «Новая».
  Остальные поля заполняются в его форме.» (the state's name as the dialog already reads it); the
  dialog reads no area tree. A child from a row or the card's «Добавить задачу» has no dialog.

## 9. Retired surfaces (US-4)

- **`…/projects/:p/settings/areas`:** no route; the catch-all answers «Страница не найдена» with
  no redirect, for any user (ADR-0024 § 4).
- **Gone in code:** the project-nested `AREAS_ROUTE`, `areasPath(o, p)` and area hooks; the card's
  `AreaField`, `draft.area`, `cardTexts.area`; the list's «Область» header; the create dialog's
  tree read.

## 10. Roles

| Control | «Администратор» | «Участник» | «Читатель» | Not a member |
|---|---|---|---|---|
| The tree, an area's page and its lists, the «Области» sections (`area.read`) | sees | sees | sees | «Организация не найдена» |
| «Новая область» at the top level (`area.create`) | offered | - | - | - |
| «Новая дочерняя область», «Переименовать», «Изменить описание», «Удалить область» (`area.write`) | offered | - | - | - |
| «Переместить» (`area.write`; `area.create` at the top level, to or from) | offered | - | - | - |
| «Добавить проект / команду / область», the forms' pickers (`area.link` and the other end's link code) | offered | - | - | - |
| «Отключить от …» (the code of the end whose page it is, alone) | offered | - | - | - |

A missing code hides the control; a direct write answers 403 «Недостаточно прав» (error notice, 6
s) and the codes are read again. Until `mod-019-scoped-access` only «Администратор» holds them.

## 11. Narrow widths (down to 640 CSS px, ADR-0024)

The tree and the tables scroll inside their card, the page never does; names are cut, the actions
column stays; page heads overflow into «Ещё действия». `FactsPanel` puts the label above the value,
sections stack. Dialogs are full width less their margins; tags and captions wrap, the delete lists
scroll inside, the picker's list is no wider than the window. Below 640 px nothing is claimed.

## 12. Accessible names and focus

The tree is a treegrid named «Области»; a toggle «Развернуть «Логистика»» / «Свернуть «Логистика»»;
a row menu «Действия: Логистика» in the tree, «Ещё действия: Alpha» in a table. The tables are named
«Вложенные области», «Проекты области», «Команды области», «Области проекта», «Области команды»; a
picker by its label only; a tag's remove «Убрать «Alpha»».

Focus: a form dialog opens on its first field, the delete and unlink dialogs on «Отмена»; a refusal
puts focus on the field it names; after a row's dialog closes, focus goes to that row (`focusRow`,
`useRowFocus`), or the next, or the section's button when the row is gone. Escape closes the
picker's list first, then the dialog.

## 13. For the developer

- **Contract needs (backend):** the tree `{id, parentId, name, description, path}` in one read; an
  area with its parent, direct children and `description`; its active projects `{id, name, key}` and
  teams; a project's and a team's areas `{id, name, path}`; the deletion's links, every linked
  project (deactivated too, `active`) and team; `parentId`, `description`, `projectIds`, `teamIds`
  on the creation, `areaIds` on a project's and a team's.
- **Refusals:** `RefusalField` gains `description`, `projects`, `teams`, `areas`; `name_taken` is
  «Название» in a create or rename and the parent field in a move. `notFoundLevel` maps
  `organizations.area.not_found` to `area` for a page's first read; in a dialog it stays the alert
  or the field.
- **Routes and keys:** `ORG_AREAS_ROUTE`, `ORG_AREA_ROUTE`, `organizationAreasPath`,
  `organizationAreaPath`; the route's skeleton columns are «Область», «Описание». Any area or link write refreshes the tree, the area, its lists, the
  project's and team's areas and the pickers' lists. **Constants:** `TREE_EXPAND_ALL_LIMIT` = 100
  (D-3), `PICKER_OPTION_LIMIT` = 50 (D-1).
- **Kit guard and tests:** new files use kit parts only; the rewritten `AreasPage` and
  `AreaDialogs` lower their allow-list entries; words go to `lib/areaTexts.ts`. New cases are the
  states of §§ 3-8 (the first state included), the expansion rule, the picker's cap, the refusals
  and the roles.

## 14. Decisions

- [ ] **D-1 The searchable pickers (R-8).** (A, recommended) A local filter: the areas come from
  the tree's one read, the projects and teams from the active lists a dialog reads when it opens;
  typing narrows by path (areas) or by name and key; the list draws at most 50 matches and ends with
  «Показаны первые 50 из 312. Уточните запрос.» ◆. No new route, no wait per keystroke; it holds the
  stated 1 000 / 200 / 200. (B) Server search per field, as the human chose for the team form's
  «Участники» at feat-028 gate-3: unbounded, but three search routes with a statement-count test
  each, a debounce and a «Поиск…» state; users are in no list another page reads, these are.
- [ ] **D-2 The deletion's links at 200 + 200 (R-8).** (A, recommended) Every link named in the
  dialog, two scrolling lists as feat-028 § 5.3 does for projects: BR-AW-9 is met on sight and one
  pattern serves both deletions. (B) Counts and the names behind «Показать все»: shorter, but
  the actor can confirm unseen.
- [ ] **D-3 The tree's initial expansion (R-8).** (A, recommended) Everything expanded while the
  tree has at most 100 areas, the top level only above that, with «Развернуть всё» / «Свернуть всё»
  in the header. (B) The top level only, always: one rule, but the example tree hides its children
  at first. (C) Always everything: up to 1 000 rows drawn at once.
- [ ] **D-4 Where a project's row on an area's page leads (R-8).** (A, recommended) The project's
  «Общие», the link's other end, where «Области» lists the area back (feat-028 D-3's choice). (B) Its
  «Рабочие элементы»: the work, but the link's other end is not shown.
- [ ] **D-5 The pickers' offer (R-5).** (A, recommended) Every place except the structurally
  impossible ones (the area, its descendants, its current parent); the depth and name rules are
  refused on saving with the server's text, so the client keeps no copy of rules that concurrent
  edits change anyway. (B) Also grey out the places the client sees would be refused (level 10, a
  sibling name, a subtree too tall): guidance, but two rules copied into the panel.
- [ ] **D-6 The card's band without «Область» (R-9).** (A, recommended) «Команда» moves up to row 1
  of block B and the place of «Итерация» takes row 2, as feat-029 drew it before mod-017. (B)
  «Команда» stays in row 2 and row 1 stands empty: nothing moves for a user, but the band keeps a
  gap above the team.
- [ ] **D-7 The tree component.** (A, recommended) The legacy `TreeRegistry` with one optional prop
  (§ 2): small, its keyboard model is kept, tech-017 restyles it. (B) A kit-grade tree table from
  `TableCard`, `BodyRow` and `TreeCell`: the kit look at once, but a new shared part with showcase,
  tests and guide, work tech-017 owns, inside a breaking L.

## 15. Stories to screens

US-1: §§ 3, 4, 5.1. US-2: §§ 3, 4, 5.2-5.4. US-3: §§ 4, 6, 7. US-4: §§ 7, 8, 9. **US-5 has no
screen:** its first state is the empty states of §§ 3, 4, 7; the bookmarked address is § 9. US-6 is
§ 10 (the tenant answers are the backend's).
