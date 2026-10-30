# Design: Area, project and team navigation (feat-043)

Stage 3 artifact, reworked after the gate-2 rejection (2026-10-30): four header selectors, a left menu of the most specific chosen object, no tabs, «Настройки» as the shell's derived last entry. Rules (BR-NV-n) and scenarios are in `spec.md` and `stories/`; this file is layout, MUI components, states and Russian words. Every string in «» is final; ◆ marks a string no earlier design quotes. The reference tracker is never named.

- Base: [mod-018 design](../mod-018-area-workspace/design.md) (tree, node dialogs, department links: «as mod-018 § n»), [feat-028 design](../feat-028-org-level-teams/design.md) (team and project dialogs, link and unlink), [feat-040 design](../feat-040-team-surfaces-across-projects/design.md) (team boards), [ui-kit.md](../../conventions/ui-kit.md), [ADR-0032](../../adr/0032-object-pages-and-navigation-context.md) (proposed: addresses, menu table, selectors, remembered selection).
- Prototype: `prototype/index.html` — the four selectors, the menus by scope, narrowing and reset, the settings navigation, a role switch. Choose project A in the last selector: «Общие», «Структура», «Рабочие элементы», «Канбан», «Бэклог», «Команды» stand in the left menu at once.
- Everything is built from the kit (`shared/ui`); a page never draws a frame.

## 1. New kit parts (ui-kit.md § 7)

| Part | Built on | Why |
|---|---|---|
| `AppTopBar` | MUI `AppBar` | product mark, the selector trail, user menu (OQ-5 A) |
| `SearchMenu` | MUI `Popover` + `Autocomplete` listbox | one selector: a trigger (small caption + value), «все» first, search, options; used four times |
| `NavList` (+ `NavItem`, expanded and rail) | MUI `List`, `Drawer` | the left menu and the settings navigation |

Each comes with tests, a showcase entry and a guide block. A page has no tabs, so `PageHeader` is used without `views`.

## 2. Screen map

| Screen | Address (under `/organizations/:o/`) | Stories |
|---|---|---|
| Header selectors, left menu, settings navigation | all | US-1, US-8 |
| Organizations list | `/organizations` (as today) | US-2 |
| Nothing chosen: «Обзор», «Рабочие элементы», «Области», «Проекты», «Команды», «Участники» | `overview` `workitems` `areas` `projects` `teams` `members` | US-2, US-6 |
| Department chosen | `areas/:d/` `overview` `projects` `teams` | US-3 |
| Team chosen | `teams/:t/` `overview` `boards/:l` `backlogs/:l` `projects` `members` | US-5 |
| Project chosen | `projects/:p/` `general` `workitems` `boards/:l` `backlogs/:l` `teams`; «Структура» = `areas/:node/overview` | US-4, US-6 |
| A node or sub-area from «Области» | `areas/:a/overview` (project menu, «Структура» marked) | US-3 |
| «Параметры» of a department, team, project, area | `areas/:d/settings`, `teams/:t/settings`, `projects/:p/settings`, `areas/:a/settings` | US-3..US-5 |
| Dialogs: links, placement, unlink, node commands | over the pages | US-7 |
| «Страница не найдена», level not-found, «Доступ ограничен» | any | US-1 |

The prefix is the most specific chosen object; other selectors ride as `?teamId=` and `?departmentId=` (ADR-0032 § 1). A bare object address opens the first entry of its menu, replacing the history entry (D-4). Organization settings: `settings/general`, `settings/process`.

## 3. The shell (US-1, US-8)

```
┌ ▣ MUSTASK │ Организация Ромашка ▾ › Департамент все ▾ › Команда все ▾ › Проект A ▾ │ (АК) ┐
├ Общие ───────┬──────────────────────────────────────────────────────────────────────────────┤
│ Структура    │  A  [MSK]                                    [Создать …]  [⋮]                 │
│ Раб. элементы│  Логистика / A                (page path, areas only)                          │
│ Канбан       │  …content, narrowed by the other selectors…                                   │
│ Настройки    │                                                                               │
└──────────────┴───────────────────────────────────────────────────────────────────────────────┘
```

- **Header** (`AppTopBar`): after the product mark four `SearchMenu` triggers in order organization, department, team, project, each a caption («Организация», «Департамент», «Команда», «Проект») over its value («все» or the name, cut with `TruncatedText`); a chosen one is marked. «Главная» is the product mark's link (D-1). Below 1024 px the triggers wrap into a second bar row and show the value only; the left menu is the overlay (menu button «Открыть меню»).
- **Selector popup** (`SearchMenu`): the organization selector lists the user's active organizations (current marked) and ends with «Все организации»; the others hold «все» first, a search field «Найти…», then the options by name (team: «Мои команды», then «Другие команды»). Typing narrows by contains (local, ≤ 2 s at 1 000/200/200). Never offered: project nodes, sub-areas, deactivated, deleted or foreign objects. Arrow keys move, Enter chooses, Escape closes and returns focus to the trigger; after a choice focus lands on the page's `h1`.
- **Narrowing** (ADR-0032 § 5): project offers = in the chosen department and linked to the chosen team; team offers = linked to the chosen project and to the chosen department (directly or through a project placed in it); department offers = holds the chosen project and linked to the chosen team. A chosen value the others no longer fit resets to «все» with the shell notice «Выбор сброшен: проект A.» (kinds «департамент», «команда», «проект»; several joined by «, »).
- **Choosing a value** rebuilds the menu. The current entry stays when the new menu has it (same segment, same level, old filters dropped), else the first entry opens. «все» in a selector widens the menu to the next most specific object; «все» in all three gives the organization's menu. The organization selector opens the chosen organization's «Обзор» with «все» in the rest; «Все организации» clears all four and opens `/organizations`.
- **Opening an object from a row or link** makes it the most specific: more specific selectors reset to «все» silently, less specific ones that fit stay (D-3). Examples: A's «Команды» → MARS leaves MARS (and the department); a department's «Проекты» → A leaves department and A; a team's «Проекты» → A leaves MARS and A.

| Selector state | The user sees |
|---|---|
| Loading | `FieldsSkeleton count={4}` in the options part; the trigger keeps its last value |
| Error | `InlineNotice` «Не удалось загрузить список. Попробуйте ещё раз.» ◆ with «Повторить» |
| No match | `InlineEmpty` «Ничего не найдено» |
| Fallback | shell notice (mod-016 DC-11 corner): «Ранее выбранный контекст недоступен. Выбрано: Ромашка.»; no organization left: «Ранее выбранный контекст недоступен.» and «Главная» opens |

- **Left menu** (`NavList`; icons required): the table is ADR-0032 § 2; entry labels as in the spec's table. The current entry is `aria-current="page"`. An entry the user may not open is absent. With a node or sub-area opened from «Области» the menu is the project's with «Структура» marked. With no organization only «Главная» and «Настройки» show.
- **«Настройки»** (derived, last; mod-015 BR-19): opens the settings navigation, the main menu as a rail, titled «Настройки»: «Организация» («Общие», «Процесс»), instance sections («Справочники», «Интерфейс»), and for «Администратор» one group per chosen kind — «Департамент», «Команда», «Проект» (and «Область» while a node or sub-area page is open), each with «Параметры». «Участник» and «Читатель» see «Организация» and the instance sections only; the address of «Параметры» shows them «Доступ ограничен».
- **Page path** (`PageHeader` `ancestors`): only for areas and items — `Логистика / A / Склад` on a node or sub-area page, the item's project on a card. No page shows a link as a parent; the selectors are the context.
- The selection narrows content as ADR-0032 § 5 says; a page with no organization (`Главная`, «Справочники») shows the remembered selection and does not change it.

## 4. The anatomy of an object page

`PageHeader` (title = the only `h1`; no tabs), commands in the bar (one primary at most, «Ещё действия» overflow), then the content in kit parts (`Section`, `DataTable`, `ColumnLayout`, `InlineEmpty`). A narrowed list says so with a `Tag` in the title line: «сужено: команда MARS, департамент Логистика» ◆. Page-level states, one frame (`StateFrame`, `NotFound` by level, `AccessDenied`):

| State | The user sees |
|---|---|
| Loading | `FieldsSkeleton` and `TableSkeleton` (one `role="status"` «Загрузка данных»); title «Область», «Проект» or «Команда» |
| Error | `ErrorBanner` with «Повторить» |
| Object not found, deleted, deactivated, foreign (prefix or query selector) | `NotFound` `area`/`project`/`team` («Область не найдена», «Проект не найден», «Команда не найдена»), menu kept; the selection does not change, except that a remembered object drops to «все» with the fallback notice |
| Entry the menu lacks | `NotFound` `page` «Страница не найдена» |
| Entry without the user's code | `AccessDenied` «Доступ ограничен»; the entry is absent |
| Not a member | `NotFound` `organization` «Организация не найдена» |

Commands (create, link, place, delete, deactivate) exist for «Администратор» only; rules, codes and confirmations are those of the interim pages (BR-NV-14).

## 5. Nothing chosen: the organization's pages

### 5.1 «Обзор» (US-2)

`PageHeader` «Обзор»; `ColumnLayout` main + side. Main: `Section` «Мои команды» (the user's teams, each a link; absent when none), stat links «Департаменты», «Проекты», «Команды» (a `Surface` band, count over label, each opens its list), then `Section`s «Департаменты», «Проекты», «Команды» with the **first 5 rows** by name (D-2) and the links «Все департаменты (N)» ◆, «Все проекты (N)» ◆, «Все команды (N)» ◆. Counts: departments only (OQ-2 A), active projects, teams. Side: `Section` «Организации» (first 5, current marked) and «Все организации». A row link opens the object (§ 3). Empty: `InlineEmpty` «Департаментов пока нет», «Проектов пока нет», «Команд пока нет». Loading `TableSkeleton` per section; error `ErrorBanner` per section. Below 1024 px the side stacks under the main. These lists exist only in the nothing-chosen menu: with an object chosen, «все» in its selector returns here.

### 5.2 «Области» (US-2)

As mod-018 § 3 with changes: a menu entry, not a settings page; primary «Создать департамент» ◆ (the dialog is mod-018 § 5.1, submit «Создать»); `FilterBar` «Найти область» (matches keep their ancestors; none: «Ничего не найдено», «Сбросить фильтр»). Columns «Название», «Тип», «Описание», row menu per type as mod-018 § 3. A department row sets the department selector; a project node row opens the project's «Структура» (selectors: its department, project); a sub-area row opens the area page the same way. A deactivated project's node and sub-areas: `Tag` «деактивирован», plain text, no link, no menu (OQ-3 A). Loading `RegistrySkeleton`; error `ErrorBanner`.

| State | The user sees |
|---|---|
| Empty, no node | `StateFrame` «В организации пока нет областей»; admin hint «Создайте первый департамент: он объединяет проекты и команды.» ◆ + «Создать департамент»; others «Департаменты создаёт администратор организации.» |
| Projects, no department | the tree of top-level project nodes; the administrator keeps «Создать департамент» |

### 5.3 «Проекты» and «Команды» (US-2, US-4, US-5)

«Проекты»: primary «Создать проект» (admin); `FilterBar` «Найти проект»; admin `InlineSelectField` «Показывать» (`Активные` / `Деактивированные`, `?status=deactivated`, D-5). `DataTable` «Проекты»: «Название» (link: sets the project), «Ключ», «Расположение» (path; «—» at the top; a link), row menu (admin): «Деактивировать» (confirmation as mod-002) or «Восстановить». The deactivated view's names are not links. «Создать проект» is mod-018 § 7's dialog with «Расположение»; done: notice «Проект «Зета» создан.» and the project's «Общие» opens (a new organization: its «Проекты» opens).
«Команды»: primary «Создать команду» (feat-028 dialog); `FilterBar` «Найти команду»; `DataTable` «Команды»: «Название» (link) with `Tag` «Моя команда» ◆, «Описание». Done: «Команда «SATURN» создана.» and its «Обзор» opens.

| State | The user sees |
|---|---|
| Loading / Error | `TableSkeleton` / `ErrorBanner` «Повторить» |
| Empty | `StateFrame` «Проектов пока нет» / «Команд пока нет»; admin hint «Создайте первый проект.» ◆ / «Создайте первую команду.» ◆ + the command; others «Проекты создаёт администратор организации.» / «Команды создаёт администратор организации.» ◆ |
| Deactivated view empty | `InlineEmpty` «Деактивированных проектов нет» |
| After filter | «Ничего не найдено», «Сбросить фильтр» |

### 5.4 «Участники»

The merged members page (mod-016) at `…/members`, unchanged inside; absent from the menu and «Доступ ограничен» at its address without the read code.

### 5.5 «Рабочие элементы» (US-6)

The project list (kit table) over every active project; the project's own entry is the same list fixed to the project (§ 7.1).

| | Nothing chosen `workitems` | Project chosen `projects/:p/workitems` |
|---|---|---|
| Columns | «Номер», «Название», **«Проект»**, **«Команда»** («Без команды»), «Статус», «Исполнитель», «Изменён» | the same without «Проект» |
| `FilterBar` | «Поиск», «Статус», «Исполнитель» | the same |
| Team | the team selector is the one `teamId` state; with a team chosen the menu holds no organization list (§ 7.2) | the selector narrows to items the team owns; «Без команды» is an option of the filter bar only here ◆ |
| Primary | none | «Новый рабочий элемент» (starts with the default team, BR-MX-10) |
| Pager | `TablePager`, 50 | same |

The title opens the card at `…/projects/<its project>/workitems/<identifier>`; the project cell is a link. «Исполнитель»: people on the listed items, former members «(не в организации)», then «Не назначен». «Удалена» in «Статус» lists deleted items (read-only card of mod-005).

| State | The user sees |
|---|---|
| Loading / Error | `TableSkeleton` / `ErrorBanner` «Повторить» |
| Empty, no filter | `StateFrame` «Рабочих элементов пока нет»; organization hint «Рабочий элемент создаётся в проекте.» ◆ + link «Проекты»; project: as today |
| After a filter | «Ничего не найдено» + «Сбросить фильтр» |
| Foreign `?workitem=` | the list shows; the card answers «Рабочий элемент не найден» |

## 6. Department, node and sub-area (US-3)

Titles: «Департамент: Логистика», «Структура: A» (a project's node, project menu), «Область: Склад». Facts are read-only kit fields (`labelPlacement="start"`); type marks as mod-018 § 4.

**Department chosen** — menu «Обзор», «Проекты», «Команды». «Обзор»: «Описание» («Описание не задано»), «Расположение: Верхний уровень», stat links «Проекты: 2», «Команды: 2». «Проекты»: `DataTable` «Проекты департамента»: «Проект» (link), «Ключ», `Tag` «деактивирован» (not a link), under the head «1 деактивирован» ◆; admin «Добавить проект» (§ 8.2) and row menu «Ещё действия: A» with «Вынести на верхний уровень» ◆. «Команды»: `DataTable` «Команды департамента», admin «Добавить команды» (§ 8.1), row menu «Отключить от департамента» (mod-018 § 6.2). Empty: `InlineEmpty` «В департаменте нет проектов» / «К департаменту не привязаны команды». «Параметры» (admin): «Переименовать», «Изменить описание» (mod-018 § 5.3), «Удалить департамент» (mod-018 § 5.5; the refusal names a holder of projects). No move, no analytics, no «Участники».

**«Структура»** (the node; project menu, address `areas/:node/overview`): facts «Тип: Проект», «Расположение» (the department, or «Верхний уровень»), «Описание» (the node's own), `ActionButton` «Открыть проект» (to «Общие»), `Section` «Вложенные области» (`DataTable`: «Область» link, «Описание»; admin «Добавить область», mod-018 § 5.2 with the parent preselected). Empty: `InlineEmpty` «Вложенных областей пока нет». No team links on a node (hint «Команды проекта — в разделе «Команды».» ◆). «Параметры» (group «Область»): «Изменить описание» and «Название меняется вместе с проектом.»; no delete, no move.

**Sub-area** (project menu, «Структура» marked; the selectors name its project and department): path `Логистика / A / Склад / Стеллажи` in the page path; facts «Родительская область» (link), «Проект» (link), «Описание», «Вложенные области». «Параметры» (group «Область», admin): «Переименовать», «Изменить описание», «Переместить» (mod-018 § 5.4), «Удалить область» (mod-018 § 5.5). A taken name: «Область с таким названием уже есть на этом уровне» on the field.

A deactivated project's node, sub-areas and items answer «Проект не найден» (OQ-3 A).

## 7. Project and team (US-4, US-5)

### 7.1 Project chosen

Menu: «Общие», «Структура», «Рабочие элементы», «Канбан», «Бэклог», «Команды». Title: the name with `Tag` of the key.
- **«Общие»:** `Section` «Основное» («Название», «Ключ», «Описание»); `Section` «Расположение»: the department as a link, or «Проект не входит ни в один департамент»; admin «Переместить» (§ 8.3). Opening it with no department chosen leaves department «все».
- **«Рабочие элементы»:** § 5.5. **«Канбан», «Бэклог»:** the project's existing pages (level drop-down; `boards/:level`, `backlogs/:level`), narrowed by the team selector (the mod-020 filter, one state); the cards open over them.
- **«Команды»:** `DataTable` «Команды проекта»: «Команда» (link: sets the team), `Tag` «По умолчанию» on the default, «По умолчанию: единственная команда» on a sole team; admin «Добавить команды» (§ 8.1), row menu «Отключить от проекта» (feat-028 confirmation with the reassign choice). Empty: `InlineEmpty` «К проекту не привязана ни одна команда».
- **«Параметры»** (admin): «Переименовать» (renames the node; helper «Область проекта переименуется вместе с проектом.»; a taken name: «Область с таким названием уже есть на этом уровне»), «Изменить описание», `Section` «Команда по умолчанию» (current or «Не выбрана»; «Выбрать» opens `DefaultTeamDialog` over the linked teams), «Ключ нельзя изменить.» ◆. No visibility field. Deactivation stays in «Проекты».

### 7.2 Team chosen

Menu: «Обзор», «Канбан», «Бэклог», «Проекты», «Участники и доступ». Title: the team's name.
- **«Обзор»:** facts «Описание», «Участников», «Проектов»; `Section` «Департаменты» (`DataTable` «Департаменты команды»: links set the department; admin «Добавить департаменты» § 8.1, row menu «Отключить от департамента»). Empty: «Команда не привязана ни к одному департаменту».
- **«Канбан», «Бэклог»:** feat-040's team pages at `teams/:t/boards|backlogs/:level`; with a department chosen they carry `?departmentId` and the department narrows no item view (Q-3 A).
- **«Проекты»:** `DataTable` «Проекты команды»: «Проект» (link: sets the project), «Ключ», «Расположение»; active only, narrowed by a chosen department; admin «Добавить проекты» (§ 8.1), row menu «Отключить от проекта». Empty: «Команда не привязана ни к одному проекту».
- **«Участники и доступ»:** feat-028's members table (`Section` «Участники»), admin «Добавить участника» and «Удалить из команды»; no grants.
- **«Параметры»** (admin): «Переименовать», «Изменить описание», «Удалить команду» (feat-028's confirmation: links, memberships, department links, items go to «Без команды»).

Readers: the same menus, no command, no row-menu column, no «Параметры».

## 8. Link and placement dialogs (US-7)

All `FormDialog` (submit unavailable until a choice), kit `InlineMultiPickerField` / `InlinePickerField`, `frame="always"`, opening on the field. Offers are read when the dialog opens and filtered locally by typed text (≤ 2 s at 200 each); none: «Ничего не найдено». Done: the dialog closes, both ends' queries and the selectors' offers are invalidated (no reload), a notice names the result. 404 for a chosen object: the alert «Команда не найдена» / «Проект не найден» / «Область не найдена» in the dialog, nothing linked, the offer read again; 404 for the page's own end: the dialog closes onto the refreshed page.

### 8.1 Links

| Opened from | Title ◆ | Field | Offer (minus linked) | Done |
|---|---|---|---|---|
| Project's «Команды» | «Добавление команд в проект» | «Команды» | active teams | «Команда «VENUS» добавлена.» / «Команды добавлены: 3.» |
| Team's «Проекты» | «Добавление проектов в команду» | «Проекты» | active projects | «Проект «D» добавлен.» / «Проекты добавлены: 3.» |
| Department's «Команды», team's «Обзор» | mod-018 § 6.1 | | departments only / teams | mod-018 § 6.1 |

All chosen in one operation, all or none. Sentences: «Выбранные команды войдут в проект «B». Работу команд это не меняет.» ◆ / «Команда «JUPITER» войдёт в выбранные проекты. Её работу это не меняет.» ◆. All linked: «Все команды уже подключены к проекту.» ◆ / «Команда уже подключена ко всем проектам.» ◆. Unlink (`ConfirmDialog`, destructive, focus «Отмена»): department pairs as mod-018 § 6.2; project–team as feat-028 (counts the team's items, one destination: a linked team or «Без команды»), identical from either end (`…/unlink-count` read at open).

### 8.2 Placing a project in a department (one at a time)

«Добавить проект» on a department's «Проекты»: «Добавление проекта в департамент» ◆; `InlinePickerField` «Проект» (single, searchable) over active projects that stand elsewhere, each `A — сейчас: Логистика` (top level: «сейчас: верхний уровень»). Once chosen: «Проект «A» переместится из «Логистика» в «Финансы». Его области, команды и рабочие элементы не изменятся.» ◆ (from the top: «Проект «D» войдёт в департамент «Финансы». …» ◆). Submit «Переместить»; done «Проект «A» перемещён.»

### 8.3 Moving a project

From «Общие»: mod-018 § 5.4's «Перемещение проекта» (single-select «Новое расположение *»: «Верхний уровень», then departments but the current). «Вынести на верхний уровень»: `ConfirmDialog` «Вынести проект «B» на верхний уровень? Команды, области и рабочие элементы проекта не изменятся.» ◆, submit «Вынести». Refusals land on the field or the dialog's alert, nothing changes: `area.name_taken` «Область с таким названием уже есть на этом уровне», `area.depth_exceeded` «Дерево областей не может быть глубже 10 уровней». A deactivated or deleted target: «Проект не найден» / «Область не найдена», the dialog closes onto the refreshed page.

## 9. Narrow widths and accessibility

Down to 640 CSS px (ADR-0024 § 5): the left menu is the overlay below 1024 px; the selectors wrap to a second header row and each popup is full width under 640 px; tables scroll inside their card, the page never does; page heads overflow into «Ещё действия»; `ColumnLayout` stacks. Each selector is a combobox with a labelled, grouped listbox; the menu is a `nav` named by the chosen object («Меню: проект A»); the tree is a treegrid named «Области»; lists are tables named by their section. After a choice, a dialog's close or a row link focus lands on the page's `h1`, the trigger or the row that opened the dialog. Colour never alone: deactivated and narrowed are `Tag` plus text. Row controls are named by their row («Ещё действия: A»).

## 10. For the developer

- **API — one additive family:** `GET /api/organizations/{o}/workitems` (the project list's contract over every active project: filters `q`, `state`, `assignee`; page 50; total order change time desc, number desc, project key; each row with project key and name and owning team; deleted states; caller a required argument, BR-NV-15) and `…/workitems/assignees`. Everything else exists: `GET /areas` (departments, project nodes with `projectId`/`projectActive`, sub-areas with `path`), projects, teams with `isMember`, `/areas/{id}/teams`, `/teams/{id}/areas`, `/projects/{id}/team-links`, `/teams/{id}/projects`, `/areas/{id}/deletion`, `…/unlink-count`. The selectors' offers are computed client-side from these lists (a project's place from the tree, a team's departments from the links); «Обзор» is built from the same lists (≤ 3 reads, one statement each) and cut to 5 rows. No overview read is added unless the plan's statement-count test fails (D-2 C). No batch move. If the team-by-department offer proves too heavy client-side, an additive `GET …/selector-offers` is the plan's call, not this design's.
- **Frontend:** one menu table per ADR-0032 § 2 with a route-inventory test; a retired-address table read by a test; one `useWorkContext` on the router's matching (bug-003, tech-009 F4); the offer/reset algorithm as a pure function with a test table (spec US-8 scenarios); the remembered selection per ADR-0032 § 6; no link builder targets a retired address; every link carries the selection (`teamId`, `departmentId`).
- **Error texts owed to `docs/error-codes.md`:** none for existing codes (every error string above exists). The reset notice «Выбор сброшен: …» and the fallback notice are client copy, not catalogue entries. If the cross-project read adds validation fields, their texts are feat-003's; the plan lists any addition.

## 11. Decisions for gate 2

| # | Question | Options |
|---|---|---|
| D-1 (reopened) | «Главная» | **(A, recommended)** the product mark opens it; an entry only with no organization. (B) an entry above «Обзор» in every menu, a duplicate for most users |
| D-2 | «Обзор» lists | **(A, recommended)** first 5 rows from the existing lists. (B) 10 rows. (C) a new `overview` read with 5 rows |
| D-3 (reopened) | What a row link or «Открыть» does to the selectors | **(A, recommended)** the opened object becomes the most specific: more specific selectors reset to «все», less specific ones that fit stay (A's «Команды» → MARS opens MARS's menu). (B) it sets its selector and keeps all that fit: from A's «Команды», MARS only narrows A's pages, and MARS's own pages need a second click. (C) it clears every other selector: simple, loses the department a user was in |
| D-4 | A bare object address | **(A, recommended)** opens the first menu entry, replacing the entry. (B) «Страница не найдена» |
| D-5 | Deactivated projects | **(A, recommended)** the «Показывать» select (`?status=`). (B) a path `…/projects/deactivated`: collides with an id |
| D-6 | The settings group for a node or sub-area | **(A, recommended)** group «Область» while its page is open. (B) its commands live on the page («Структура») for the administrator: a second place for settings, against BR-NV-18 |
