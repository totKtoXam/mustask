# Design: Area, project and team navigation (feat-043)

Stage 3 artifact. Rules (BR-NV-n) and scenarios are in `spec.md` and `stories/`; this file is layout, MUI components, states and Russian words. Every string in «» is final. ◆ marks a string no earlier design quotes. The reference tracker is never named.

- Base: [mod-018 design](../mod-018-area-workspace/design.md) (tree, node dialogs, department links: §§ 3-6 are reused where this file says «as mod-018 § n»), [feat-028 design](../feat-028-org-level-teams/design.md) (team and project dialogs, link and unlink), [feat-040 design](../feat-040-team-surfaces-across-projects/design.md) (team boards), [ui-kit.md](../../conventions/ui-kit.md), [ADR-0032](../../adr/0032-object-pages-and-navigation-context.md) (proposed: addresses, section list, context).
- Prototype: `prototype/index.html` (shell, switcher, «Обзор», tree, project page with the link dialog, the cross-project list; the role is switched at the top).
- Everything is built from the kit (`shared/ui`); a page never draws a frame. Pages reuse the kit's parts named below; a legacy part on a moved page (`FactsPanel`, `TreeRegistry` styled as before) is replaced by kit parts only where this file says so.

## 1. New kit parts

| Part | Built on | Why |
|---|---|---|
| `AppTopBar`, `NavList` (+ `NavItem`, expanded and rail) | MUI `AppBar`, `List`, `Drawer` | the shell on the kit (OQ-5 A); replaces `app/shell` legacy look |
| `SearchMenu` | MUI `Popover` + `Autocomplete` listbox, `groupBy` | the switcher: a trigger button, a search field, grouped options |

Each new part comes with tests, a showcase entry and a guide block. Sections are `PageHeader` `views` (no new part).

## 2. Screen map

| Screen | Address (under `/organizations/:o/`) | Stories |
|---|---|---|
| Shell: top bar, navigation, switcher, trail | all | US-1, US-8 |
| Organizations list | `/organizations` (as today) | US-2 |
| «Обзор» | `overview` | US-2 |
| «Рабочие элементы» (all projects) | `workitems` | US-6 |
| «Области», «Проекты», «Команды» | `areas`, `projects`, `teams` | US-2 |
| «Участники» (moved page) | `members` | US-1, US-9 |
| Department | `areas/:a/` `overview` `projects` `teams` `settings` | US-3 |
| Project's node, sub-area | `areas/:a/` `overview` `settings` | US-3 |
| Project | `projects/:p/` `general` `workitems` `boards/:l` `backlogs/:l` `teams` `settings` | US-4, US-6 |
| Team | `teams/:t/` `overview` `boards/:l` `backlogs/:l` `projects` `members` `settings` | US-5 |

A bare object address (`…/projects/:p`) opens the object's first section by replacing the entry (Back is not trapped); it is not a retired address. Organization settings: `settings/general` and `settings/process` stay; «Настройки» in the main navigation opens the settings navigation as today.

## 3. The shell (US-1, US-8)

```
┌ ▣ MUSTASK │ [Ромашка / MARS ▾] │ Ромашка / Команды / MARS / Бэклог │          (АК) ┐
├ Обзор          ┬──────────────────────────────────────────────────────────────────┤
│ Раб. элементы  │  MARS                                   [Создать …]  [⋮]          │
│ Области        │  Обзор  Канбан  Бэклог  Проекты  Участники и доступ  Настройки   │
│ Проекты        │  …section content…                                               │
│ Настройки      │                                                                  │
└────────────────┴──────────────────────────────────────────────────────────────────┘
```

- **Main navigation** (`NavList`): «Обзор», «Рабочие элементы», «Области», «Проекты», «Команды», «Участники», «Настройки», in this order, each with an icon (`DashboardOutlined`, `ListAltOutlined`, `AccountTreeOutlined`, `FolderOutlined`, `GroupsOutlined`, `PeopleOutlined`, `SettingsOutlined`). The current entry is marked (`aria-current="page"`): the entry of the page, an area's pages marking «Области» (a project's node and sub-areas too), an object's pages marking its own entry. An entry the user may not open is absent, never disabled. With no organization in the context only «Главная» and «Настройки» show; «Главная» is otherwise reached by the product mark (D-1). Widths as ADR-0024: expanded at ≥ 1280 px, rail 1024-1279, overlay below 1024 (menu button in the top bar, «Открыть меню»).
- **Settings:** «Настройки» opens a second navigation with the main one as a rail (as today): «Организация» → «Общие», «Процесс»; instance sections («Справочники», «Интерфейс»). No page of a team, area or project, no project list, no members page is in it (BR-NV-5).
- **Trail** (`PageTrail`, from `usePageHead`): `Ромашка / <«Области» | «Проекты» | «Команды»> / <object> / <section>`; an area's trail carries its ancestors (`Ромашка / Области / Логистика / A / Склад / Обзор`), a project's or team's carries neither department nor project; the organization's pages `Ромашка / Обзор`. Every part but the last is a link. The trail never shows a link as a parent.
- **Switcher** (`SearchMenu`), in the top bar after the product mark. The trigger reads `<организация>` or `<организация> / <объект>` (cut with `TruncatedText`; no organization: «Выберите организацию»). Popup: «Организации» (radio list ending «Все организации»), a search field «Найти объект», then the grouped options.

  Groups «Мои команды», «Департаменты», «Проекты», «Другие команды», each by name (Russian collation); «Ромашка (вся организация)» first, the context marked. A project node, a sub-area, a deactivated or foreign object is never offered. Typing narrows by contains (local filter over the lists, ≤ 2 s at 1 000/200/200). Arrow keys move through the options across groups, Enter chooses, Escape closes and returns focus to the trigger; focus lands on the opened page's heading after a choice. Choosing: same kind as the current object → the same section and level, no query; another kind → the object's first section the user may open; «Ромашка (вся организация)» → «Обзор»; another organization → its «Обзор»; «Все организации» → `/organizations`.

| Switcher state | The user sees |
|---|---|
| Loading | `FieldsSkeleton count={4}` in the options part; the organizations part is already there |
| Error | `InlineNotice` error «Не удалось загрузить список. Попробуйте ещё раз.» ◆ with «Повторить» |
| No match | `InlineEmpty` «Ничего не найдено» |
| Fallback | the shell's notice (mod-016 DC-11 corner): «Ранее выбранный контекст недоступен. Выбрано: Ромашка.»; no organization left: «Ранее выбранный контекст недоступен.» and «Главная» opens |

The context narrows nothing: «Рабочие элементы» under the context MARS still lists every project (BR-NV-10). A page with no organization (`Главная`, «Справочники») shows the remembered context and does not change it.

## 4. The anatomy of an object page (US-3..US-5)

`PageHeader` (title = the only `h1`; kinds below), `views` = the sections the user may open, in the ADR-0032 order, the commands of the whole page in the bar (one primary at most) with «Ещё действия» overflow; under it the section's content in kit parts (`Section`, `DataTable`, `ColumnLayout`, `InlineEmpty`). The page title does not change with the section. Page-level states, one frame for all sections (`StateFrame`, `NotFound` by level, `AccessDenied`):

| State | The user sees |
|---|---|
| Loading | `FieldsSkeleton` in the header's place and `TableSkeleton` in the section (one `role="status"` «Загрузка данных»); the title reads the kind: «Область», «Проект», «Команда» |
| Error | `ErrorBanner` with «Повторить» |
| Object not found, deleted, deactivated, foreign | `NotFound` `area`/`project`/`team` («Область не найдена», «Проект не найден», «Команда не найдена») with the navigation kept; the context does not change (unless it was the remembered object: fallback with the notice) |
| Section the kind lacks | `NotFound` `page` «Страница не найдена» |
| Section without the user's code | `AccessDenied` «Доступ ограничен»; the tab is absent |
| Not a member | `NotFound` `organization` «Организация не найдена» |

Section lists by role: «Настройки» and every create, link, place, delete command exist for «Администратор» only; «Участник» and «Читатель» read (spec § Roles). Command rules, codes and confirmations are those of the interim pages (BR-NV-14); this file does not repeat their texts.

## 5. The organization's pages

### 5.1 «Обзор» (US-2)

`PageHeader` «Обзор». `ColumnLayout`: main + side.
- Main: `Section` «Мои команды» (the user's teams in this organization, by name, each a link; absent when she belongs to none). Then three stat links (a `Surface` band; count + label, each opens its list): «Департаменты», «Проекты», «Команды». Then `Section`s «Департаменты», «Проекты», «Команды» with the **first 5 rows** by name (D-2; each row a link, a `Tag` «деактивирован» never appears: counts and rows are active only) and under each the link «Все департаменты (N)» ◆ / «Все проекты (N)» ◆ / «Все команды (N)» ◆. Counts: departments only (OQ-2 A), active projects, teams.
- Side: `Section` «Организации» with the user's active organizations by name (first 5, current marked `aria-current`), then «Все организации».
- Empty sections: `InlineEmpty` «Департаментов пока нет», «Проектов пока нет», «Команд пока нет»; counts read 0.
- States: loading `TableSkeleton` in each section; error `ErrorBanner` «Повторить» per section (one failed list does not blank the page).
- At < 1024 px the side section stacks under the main.

### 5.2 «Области» (US-2)

As mod-018 § 3 with these changes: it is an entry, not a settings page; `PageHeader` «Области», primary «Создать департамент» (◆ the verb of the lists; the dialog is mod-018 § 5.1 «Новый департамент» whose submit stays «Создать»); a `FilterBar` field «Найти область» above the tree (typed text keeps matches and their ancestors; no match: `StateFrame` «Ничего не найдено» with «Сбросить фильтр»). Columns «Название», «Тип», «Описание», row menu per type as mod-018 § 3. A project's node row: «Название» opens the node's page; sub-areas and departments open their pages. A deactivated project's node and its sub-areas: `Tag` «деактивирован», plain text, no link, no menu (OQ-3 A). Loading `RegistrySkeleton`; error `ErrorBanner`.

| State | The user sees |
|---|---|
| Empty, no node | `StateFrame` «В организации пока нет областей»; hint for «Администратор»: «Создайте первый департамент: он объединяет проекты и команды.» ◆ with «Создать департамент»; others: «Департаменты создаёт администратор организации.» |
| Projects, no department | the tree of top-level project nodes; the administrator still has «Создать департамент» in the header |

### 5.3 «Проекты» (US-2, US-4)

`PageHeader` «Проекты», primary «Создать проект» (admin). `FilterBar`: «Найти проект»; for the administrator `InlineSelectField` «Показывать» (`Активные` / `Деактивированные`, value in `?status=deactivated`). `DataTable` named «Проекты»: «Название» (link to the project), «Ключ», «Расположение» (the place as a path from the tree, «—» at the top level; a link to the department), row menu (admin): «Деактивировать» (active view; confirmation as mod-002) or «Восстановить» (deactivated view). The deactivated view's names are not links. «Создать проект»: dialog of mod-018 § 7 with «Расположение»; done: notice «Проект «Зета» создан.» and the new project's «Общие» opens (organization-creation flow: the new organization's «Проекты» opens).

| State | The user sees |
|---|---|
| Loading / Error | `TableSkeleton` / `ErrorBanner` «Повторить» |
| Empty | `StateFrame` «Проектов пока нет»; admin: «Создайте первый проект.» ◆ + «Создать проект»; others: «Проекты создаёт администратор организации.» |
| Deactivated view empty | `InlineEmpty` «Деактивированных проектов нет» |
| After filter | «Ничего не найдено», «Сбросить фильтр» |

### 5.4 «Команды» (US-2, US-5)

`PageHeader` «Команды», primary «Создать команду» (admin; dialog feat-028). `FilterBar` «Найти команду». `DataTable` «Команды»: «Название» (link) with `Tag` «Моя команда» ◆ on the user's own, «Описание». Empty: `StateFrame` «Команд пока нет», admin hint «Создайте первую команду.» ◆. Others as 5.3.

### 5.5 «Участники»

The merged members page (mod-016) at `…/members`, unchanged inside; only its address and entry move (BR-NV-5). «Доступ ограничен» for a user without the read code; the entry is then absent.

### 5.6 «Рабочие элементы» (US-6)

The project's list (as today's `WorkItemsPage`, kit table) with the scope as the only difference.

| | Organization `workitems` | Project `projects/:p/workitems` |
|---|---|---|
| Columns | «Номер», «Название», **«Проект»**, **«Команда»** («Без команды»), «Статус», «Исполнитель», «Изменён» | the same without «Проект» |
| Filters (`FilterBar`) | «Поиск», «Статус», «Исполнитель», «Команда» (all teams of the organization) | the same; «Команда» offers the project's teams and «Без команды» |
| Primary | none | «Новый рабочий элемент» (as today; starts with the default team, BR-MX-10) |
| Pager | `TablePager`, 50 | same |

The title opens the card at `…/projects/<its project>/workitems/<identifier>`; the project name is a link to the project. Filters live in the address; the page returns to 1 on a change; the selected navigation entry opens the list without `teamId` (H-1 A). «Исполнитель» options: the people on the listed items, former members marked «(не в организации)», then «Не назначен». «Удалена» in «Статус» lists deleted items (read-only card of mod-005).

| State | The user sees |
|---|---|
| Loading / Error | `TableSkeleton` / `ErrorBanner` «Повторить» |
| Empty, no filter | `StateFrame` «Рабочих элементов пока нет»; organization: hint «Рабочий элемент создаётся в проекте.» ◆ with a link «Проекты» (no create command); project: as today |
| After a filter | «Ничего не найдено» + «Сбросить фильтр» |
| Foreign `?workitem=` | the list shows; the card answers «Рабочий элемент не найден» |

## 6. Area pages (US-3)

Titles: «Департамент: Логистика», «Область проекта: A» (a project's node), «Область: Склад». Type marks and fact layout as mod-018 § 4; facts are read-only kit fields (`labelPlacement="start"`). Sections by kind (ADR-0032).

**Department.** «Обзор»: «Описание» («Описание не задано» when empty), «Расположение: Верхний уровень», stat links «Проекты: 2», «Команды: 2» opening the sections. «Проекты»: `DataTable` «Проекты департамента»: «Проект» (link to the project node's page), «Ключ», `Tag` «деактивирован» (not a link) ; a line under the head counts apart: «1 деактивирован» ◆; admin: primary-less command «Добавить проект» (§ 8.2) and a row menu «Ещё действия: A» with «Вынести на верхний уровень» ◆. «Команды»: `DataTable` «Команды департамента» (mod-018 § 4), admin «Добавить команды» (§ 8.1), row menu «Отключить от департамента». «Настройки» (admin): `Section` «Основное» (name, description, with «Переименовать» and «Изменить описание», dialogs of mod-018 § 5.3), `Section` «Удаление» with «Удалить департамент» (mod-018 § 5.5; the refusal names the holder of projects). No move, no analytics, no «Участники».

| Department state | The user sees |
|---|---|
| «Проекты» empty | `InlineEmpty` «В департаменте нет проектов» ; admin button «Добавить проект» stays |
| «Команды» empty | `InlineEmpty` «К департаменту не привязаны команды»; admin «Добавить команды» |

**Project's node.** «Обзор»: facts «Тип: Проект», «Расположение» (the department as a link, or «Верхний уровень»), «Описание» (the node's own), button-link «Открыть проект» (`ActionButton to`), `Section` «Вложенные области» (`DataTable`: «Область» link, «Описание»; admin «Добавить область» opens mod-018 § 5.2 with the parent preselected). Empty: `InlineEmpty` «Вложенных областей пока нет». No «Команды»: «Команды проекта на странице проекта.» ◆ is the hint under the table (a link to the project's «Команды»). «Настройки» (admin): «Изменить описание» only, with «Название меняется вместе с проектом.»; no delete, no move.

**Sub-area.** «Обзор»: «Путь», «Родительская область» (link), «Проект» (link), «Описание», «Вложенные области» as above. «Настройки» (admin): «Переименовать», «Изменить описание», «Переместить» (mod-018 § 5.4), «Удалить область» (mod-018 § 5.5).

A deactivated project's node, sub-areas and items have no page (OQ-3 A): the pages answer «Проект не найден». The `NotFound` `project` comes from the node's address too. Creating a sub-area refused by a taken name shows «Область с таким названием уже есть на этом уровне» on the field.

## 7. Project and team pages (US-4, US-5)

### 7.1 Project

Title: the project's name, with `Tag` of the key (`A · MSK`). Sections: «Общие», «Рабочие элементы», «Канбан», «Бэклог», «Команды», «Настройки» (admin).

- **«Общие»:** `Section` «Основное»: «Название», «Ключ», «Описание»; `Section` «Расположение»: the path as a link (`Логистика`), or «Проект не входит ни в один департамент» (top level); a link «Структура проекта» to the node's `overview`; admin: «Переместить» (§ 8.3). Trail has no department.
- **«Рабочие элементы»:** § 5.6. **«Канбан», «Бэклог»:** the project's existing pages with their level drop-down and «Команда» filter (`boards/:level`, `backlogs/:level`), opened from the tabs; the cards open over them.
- **«Команды»:** `DataTable` «Команды проекта»: «Команда» (link), a `Tag` «По умолчанию» on the default, «По умолчанию: единственная команда» on the sole team; admin: «Добавить команды» (§ 8.1) and row menu «Отключить от проекта» (feat-028 confirmation with the reassign choice). Empty: `InlineEmpty` «К проекту не привязана ни одна команда».
- **«Настройки»** (admin): `Section` «Основное» («Переименовать», «Изменить описание»: renaming also renames the node, helper «Область проекта переименуется вместе с проектом.»; a taken name shows «Область с таким названием уже есть на этом уровне» on the form), `Section` «Команда по умолчанию» (the current team or «Не выбрана», «Выбрать» opens `DefaultTeamDialog` over the project's linked teams, as feat-028). The key is read-only text with «Ключ нельзя изменить.» ◆; no visibility field. Deactivation stays in «Проекты».

### 7.2 Team

Title: the team's name. Sections: «Обзор», «Канбан», «Бэклог», «Проекты», «Участники и доступ», «Настройки» (admin).

- **«Обзор»:** facts «Описание», «Участников», «Проектов»; `Section` «Департаменты» (links; `DataTable` «Департаменты команды»; admin «Добавить департаменты» § 8.1, row menu «Отключить от департамента»). Empty: «Команда не привязана ни к одному департаменту».
- **«Канбан», «Бэклог»:** feat-040's team pages unchanged, their address now `teams/:t/boards|backlogs/:level`.
- **«Проекты»:** `DataTable` «Проекты команды»: «Проект» (link), «Ключ», «Расположение» (path); active only (E is neither listed nor counted); admin «Добавить проекты» (§ 8.1), row menu «Отключить от проекта». Empty: «Команда не привязана ни к одному проекту».
- **«Участники и доступ»:** the members table of feat-028 (`Section` «Участники»), admin «Добавить участника» and a row action «Удалить из команды»; no grants. Empty as feat-028.
- **«Настройки»** (admin): «Основное» (rename, describe) and «Удаление» («Удалить команду»: feat-028's confirmation, which names links, memberships, department links, and where items go: «Без команды»).

Reader: tabs without «Настройки», no command, no row menu column. `Участник` in a team gets nothing from it (BR-MX-22).

## 8. Link and placement dialogs (US-7)

All `FormDialog` (submit unavailable until a choice), kit `InlineMultiPickerField` / `InlinePickerField` with `frame="always"`, the dialog opens on its field. The offers are read when the dialog opens and filtered locally by typed text (≤ 2 s at 200 each); none read «Ничего не найдено». Done = the dialog closes, both ends' queries are invalidated (no reload), a notice names the result. 404 for a chosen object: the alert «Команда не найдена» / «Проект не найден» / «Область не найдена» in the dialog, nothing linked, the dialog stays and the offer is read again; 404 for the page's own end: the dialog closes onto the refreshed page.

### 8.1 Links

| Opened from | Title ◆ | Field | Offer (minus linked) | Done |
|---|---|---|---|---|
| Project's «Команды» | «Добавление команд в проект» | «Команды» | active teams | «Команда «VENUS» добавлена.» / «Команды добавлены: 3.» |
| Team's «Проекты» | «Добавление проектов в команду» | «Проекты» | active projects (not deactivated) | «Проект «D» добавлен.» / «Проекты добавлены: 3.» |
| Department's «Команды» and team's «Обзор» | mod-018 § 6.1 | | departments only / teams | mod-018 § 6.1 |

All chosen in one operation, all or none. A project node's page and a sub-area never offer a team link. Sentence above the field: «Выбранные команды войдут в проект «B». Работу команд это не меняет.» ◆ / «Команда «JUPITER» войдёт в выбранные проекты. Её работу это не меняет.» ◆. Every list linked: «Все команды уже подключены к проекту.» ◆ / «Команда уже подключена ко всем проектам.» ◆. Unlink (all four pairs): `ConfirmDialog`, destructive, focus «Отмена»: department pairs as mod-018 § 6.2; project–team as feat-028 (counts the team's items in the project and offers one destination: a linked team or «Без команды»), the same from either end (`GET …/unlink-count` is read at open).

### 8.2 Placing a project in a department (one at a time)

«Добавить проект» on a department's «Проекты»: title «Добавление проекта в департамент» ◆; `InlinePickerField` «Проект» (single, searchable) offering active projects that stand elsewhere, each as `A — сейчас: Логистика` (top level: «сейчас: верхний уровень»); the department's own and deactivated ones are absent. Under the field once chosen: «Проект «A» переместится из «Логистика» в «Финансы». Его области, команды и рабочие элементы не изменятся.» ◆ (a top-level project: «Проект «D» войдёт в департамент «Финансы». Его области, команды и рабочие элементы не изменятся.» ◆). Submit «Переместить». Done: «Проект «A» перемещён.»

### 8.3 Moving a project (from «Общие», or «Вынести на верхний уровень»)

mod-018 § 5.4's «Перемещение проекта» (single-select «Новое расположение *»: «Верхний уровень», then departments but the current one). «Вынести на верхний уровень» is the same move with no dialog but a confirmation: «Вынести проект «B» на верхний уровень? Команды, области и рабочие элементы проекта не изменятся.» ◆, submit «Вынести». Refusals land on the field (or the confirmation's alert): `area.name_taken` «Область с таким названием уже есть на этом уровне», `area.depth_exceeded` «Дерево областей не может быть глубже 10 уровней»; nothing changes. A deactivated or deleted target: «Проект не найден» / «Область не найдена», the dialog closes onto the refreshed page.

## 9. Narrow widths and accessibility

Down to 640 CSS px (ADR-0024 § 5): the overlay navigation below 1024 px; the section tabs scroll inside their strip; tables scroll inside their card and the page never does; page heads overflow into «Ещё действия»; `ColumnLayout` stacks the side column; the switcher popup is full width under 640 px. The tabs are a `tablist` of links with the current tab `aria-current="page"`; the tree is a treegrid named «Области»; lists are tables named by their section (§§ 6-7); the switcher is a combobox with a labelled listbox grouped by `role="group"`; after a choice, a section change or a dialog's close focus lands on the page's `h1`, the trigger or the row that opened the dialog. Colour never alone: deactivated is a `Tag` plus plain text. Row controls are named by their row («Ещё действия: A»).

## 10. For the developer

- **API — one additive family:** `GET /api/organizations/{o}/workitems` (the project list's contract over every active project: filters `q`, `state`, `assignee`, `teamId`; page 50; total order change time desc, number desc, project key; each row with project key and name and owning team; `deleted` states; caller as a required argument, BR-NV-15) and its `…/workitems/assignees` options read. Every other read exists: the areas tree (`GET /areas`: departments, project nodes with `projectId`/`projectActive`, sub-areas with `path`; the place of a project in a list and its sub-area counts are joined client-side from this one statement), projects, teams with `isMember`, `GET /areas/{id}`, `/areas/{id}/teams`, `/teams/{id}/areas`, `/projects/{id}/team-links`, `/teams/{id}/projects`, `/areas/{id}/deletion`, `…/unlink-count`. «Обзор» is built from the same lists (≤ 3 reads, each one statement) and cuts to 5 rows; **no overview read is added** unless the plan's statement-count test fails (then an additive `GET …/overview` with counts and 5 rows, D-2). No batch move.
- **Frontend:** a route table per kind from ADR-0032 § 2 with one inventory test; a retired-address table read by a test; one `useWorkContext` source on the router's matching (bug-003, tech-009 F4); the remembered context per ADR-0032 § 6; no link builder targets a retired address.
- **Error texts owed to `docs/error-codes.md`:** none new for existing codes (all strings above that are errors exist). If the cross-project read adds validation fields, their texts are the project list's (feat-003); the plan lists any addition.

## 11. Decisions for gate 2

| # | Question | Options |
|---|---|---|
| D-1 | «Главная» when an organization is in the context | **(A, recommended)** the product mark opens it; the entry shows only with no organization. (B) a first entry above «Обзор»: eight entries, a duplicate of «Обзор» for most users |
| D-2 | «Обзор» lists | **(A, recommended)** first 5 rows from the existing lists. (B) 10 rows. (C) a new `overview` read with 5 rows: fewer bytes, one more route |
| D-3 | The switcher | **(A, recommended)** the new kit part `SearchMenu`. (B) keep the legacy `Menu` in `app/shell`: fewer parts now, the legacy look stays and tech-020 cannot delete it |
| D-4 | Deactivated projects in «Проекты» | **(A, recommended)** the «Показывать» select (`?status=`). (B) a path `…/projects/deactivated`: collides with an id |
| D-5 | A bare object address (`…/projects/:p`) | **(A, recommended)** opens the first section, replacing the entry. (B) «Страница не найдена»: a pasted address of an object is dead |


## 12. Stories to screens

US-1 §§ 2-3; US-2 §§ 5.1-5.4; US-3 §§ 6, 8; US-4 §§ 7.1, 8; US-5 §§ 7.2, 8; US-6 § 5.6; US-7 § 8; US-8 §§ 3, 4; US-9 § 10 (code and docs, no screen). Texts are inline; ◆ marks the new ones.
