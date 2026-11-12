# Design: Teams and projects under departments, cascading selectors, sight (doc-016)

Stage 3 artifact. Rules (BR-TD-n) and scenarios are in `spec.md` and `stories/`; this file is layout, MUI components, states and final Russian copy. ◆ = copy new in this design (the spec quotes the rest).

- Base: [feat-043 design](../feat-043-area-project-team-navigation/design.md) (the header, `SearchMenu`, page frames), [mod-018](../mod-018-area-workspace/design.md) (department pages, move dialogs), [feat-028](../feat-028-org-level-teams/design.md) (team and link dialogs), [mod-019](../mod-019-scoped-access/design.md), [ui-kit.md](../../conventions/ui-kit.md).
- Prototype: `prototype/index.html` (one file, no network). Seed: «Ромашка» and «Другая»; six departments (one empty, two from the upgrade); teams MARS, VENUS, SATURN, JUPITER, PLUTO, ORION; projects A, B, G, C, D, E, Z. A user switch (Ann the administrator; Olga a member of VENUS and ORION; Max «Менеджер» of «Логистика»; Pavel with nothing) shows sight scoping. § 10 lists the clickable scenarios.

## 1. Reuse: no new component

| Wanted | Verdict |
|---|---|
| Header context control | **Rebuilt, no new kit part.** `app/shell/ContextSelectors` becomes the path `Button` + `Popover` panel (`TextField`, `List`, `ListSubheader`, `FieldsSkeleton`, `InlineNotice`, `InlineEmpty`) and the palette `Dialog`; `SearchMenu` is reused for the lists of the columns. The `OTHERS_HEADING` / `OTHERS_NOTE` group retires; `selectorFit.ts` is rewritten to the tree invariant (BR-TD-7, -9, -18); offers are sight-scoped (§ 3.2) |
| Team and project forms | `FormDialog` + `InlineTextField` + `InlinePickerField` (single, searchable; MUI `Autocomplete` underneath), as feat-028 and mod-018 |
| Move dialogs, refusals | `FormDialog` + `InlinePickerField`; the refusal is an `ErrorBanner`-style `Alert severity="error"` inside the dialog (inline, not a toast), the dialog stays open |
| Link picker | feat-043 § 8.1 `FormDialog` + `InlineMultiPickerField`, offering the department's teams only |
| «Видим всей организации» | MUI `Switch` with label in a `Section`, change confirmed by `ConfirmDialog` when turning on |
| Department card, tree, colour | MUI `Card` (outlined, coloured left border), `Chip`, `Avatar` 10 px as the dot, outlined `Button` + `Menu`; the tree is `shared/ui/TreeRegistry` (§ 7.2) |
| Lists, empty/loading/error | `DataTable`, `TableSkeleton`, `StateFrame`, `NotFound`, `ErrorBanner`, `InlineNotice`, `Tag`, `useNotice` (feat-043 § 4 frames), unchanged |

Removed (BR-TD-2): a team's «Области» multiselect and a department's team link picker.

## 2. Screen map

| Screen | Where | Stories |
|---|---|---|
| Context button, panel, Ctrl+K palette, notices | every page | US-3, US-4 |
| Team form «Новая команда»; team move dialog; team page's «Департамент» | «Команды», department page, selector «Создать команду»; team «Обзор» | US-1 |
| Project form «Новый проект» with «Расположение»; project move dialog; link picker | «Проекты», department page, selector «Создать проект»; project «Общие», «Команды» | US-5 |
| «Видим всей организации» | project «Параметры» (the shell's derived last entry) | US-6 |
| «Структура», «Проекты», «Команды», «Обзор» scoped by sight | organization menu | US-6, US-3 |
| Not-found frames for hidden objects | any address | US-6 |
| Upgrade departments | «Структура», department «Обзор» | US-2 |

## 3. Header: context path button, panel and palette (US-3, US-4; BR-TD-21, -22)

### 3.0 The context path button and its panel

```
+ MUSTASK | [Ромашка › ● Логистика › MARS › все ⌄] ........... (⌕) (АК) +
   panel: [search] / Организация | Департамент | Команда | Проект (rows: ＋ Создать, все, values)
```

**Button** (borderless text `Button`, logo height, right after it): the path «Организация › Департамент › Команда › Проект», each segment with its tree type icon (`CorporateFareOutlined`, `ApartmentOutlined`, `GroupsOutlined`, `FolderOutlined`) and, for department, team and project, the colour dot as in the tree; «все» / «Все организации» muted with a muted icon; the last chosen level medium weight; thin muted «›»; a small muted ⌄. Hover, focus: soft `action.hover` tint and ring, stronger while open. A long path shortens segments with an ellipsis; `Tooltip` and `aria-label` give the full path («Контекст: Ромашка, Логистика, MARS, все»); `aria-haspopup="dialog"`. It replaces feat-043's four triggers.

**Panel** (`Popover`, `min(880px, 100vw − 32px)`; column minimum widths 180 / 240 / 220 / 220 px; narrower: the columns scroll sideways inside the panel, never the page): at the top a `TextField size="small"` «Найти департамент, команду или проект»; below, four `List` columns (`role="listbox"`, caption `ListSubheader`) «Организация», «Департамент», «Команда», «Проект». A name wraps to 2 lines at most, then an ellipsis and a `Tooltip` with the full name; «＋ Создать …» stays on one line. Each column: feat-052's «＋ Создать …» line, then «все» (the organization column: «Все организации»), then the offers of § 3.2 by name with colour dots (department, team, project; the team column under «Мои команды» / «Другие команды»; with organization «все» each organization is a `ListSubheader`), the chosen value marked. Choosing in a column is § 3.3 and keeps the panel open; the columns to its right narrow and focus returns to the chosen row. Typing in the search replaces the columns with the grouped results (§ 3.1). Esc or a click outside closes the panel. Keyboard: ↓ from the field enters the first column; ↑ ↓ move inside a column, ← → move to the neighbouring column at the same row, Enter chooses.

### 3.1 Search results and the Ctrl+K palette (BR-TD-21, -22)

One sight-scoped search serves the panel's field and the palette. Rows: groups «Департаменты», «Команды», «Проекты» (an empty group is absent), at most 5 each, by name; a row = colour dot, name, a project's key in grey, parent path in grey on the right (team and project — «Логистика»; department — none; with several organizations «Другая / Склад»). Only what the user sees is found (BR-TD-12..-15): Olga typing «jup» or «фин» gets «Ничего не найдено»; «orion» gets ORION with «Другая / Склад». Choosing a row chooses the value as in § 3.3 (parents follow, levels below reset to «все» with «Выбор сброшен: …») and closes the panel or palette.

**Palette:** a search `IconButton` (tooltip «Поиск (Ctrl+K)», `aria-label` the same) before the avatar, and Ctrl+K anywhere, open a `Dialog` about 480 px, centred high over a dimmed page; field «Перейти к департаменту, команде или проекту»; footer «↑↓ выбрать · Enter перейти · Esc закрыть».

| State | Panel field / palette show |
|---|---|
| Empty field | panel: the columns. Palette: «Недавние» — the last 5 contexts the user chose, each its path with the department dot, newest first, Enter or a click sets it; none: «Недавних контекстов пока нет» ◆; one no longer visible is not listed |
| Typing | grouped results, first row active; ↑ ↓ wrap, Enter chooses |
| No results | «Ничего не найдено» |
| Loading / error | `FieldsSkeleton` rows / «Не удалось выполнить поиск. Попробуйте ещё раз.» with «Повторить» |

«Структура» has no search filter of its own: typed text is gone once a result is chosen, so a filter following it would be awkward; the selection narrows the page as before.

### 3.2 What each selector offers (BR-TD-8; sight BR-TD-12..-15)

| Chosen above | Организация | Департамент | Команда | Проект |
|---|---|---|---|---|
| organization «все» | the user's organizations | visible departments of all, by organization heading | visible teams of all, by heading | visible projects of all, by heading |
| organization X | as above | X's visible departments | X's visible teams | X's visible projects |
| + department Д | | not narrowed by lower levels | Д's teams | Д's projects |
| + team Т | | | not narrowed by the project | Т's linked projects (visible ones) |

Never offered: a project node, a sub-area, a deactivated, deleted or foreign object, «Остальные». A project narrows nothing above it. A department's projects linked to no team stay in its offer and leave when a team is chosen.

### 3.3 Choosing (BR-TD-9, -10)

| Action | Result | Notice |
|---|---|---|
| Team / project / department chosen | its organization, and for a team or project its department, are chosen; a project never chooses a team | none |
| A chosen value no longer fits (other department; project not linked to the new team; team not linked to the new project) | resets to «все» | «Выбор сброшен: команда MARS, проект A.» (kind in lower case, joined by «, », team before project) |
| Department replaced by the value's own | silent | none |
| Department «все» | team and project reset | the notice, naming what was chosen; none if nothing |
| Team «все» | nothing else changes; a chosen project that fits stays (decided at gate-2, BR-TD-10) | none |
| Project «все» | nothing else changes | none |
| Organization «все» («Все организации») | all four clear | the notice, none if nothing chosen below |
| Another organization | the lower three read what was last chosen there (ADR-0032 § 6), repaired by § 3.5, else «все» | per § 3.5 |
| A value from an organization «все» column | opens its organization; the page moves to the value | none |
| A row or link opens an object | as choosing it, its notice included («Выбор сброшен: команда JUPITER.» for A opened under «Финансы» / JUPITER) | per rule |

The notice is an `InlineNotice` (`role="status"`, closable) under the header, replacing the previous one; it never blocks.

### 3.4 Empty, loading, error

| State | A column shows |
|---|---|
| Nothing to offer | «все» and a muted line: «Нет доступных организаций» / «Нет доступных департаментов» / «Нет доступных команд» / «Нет доступных проектов» |
| Search without match | «Ничего не найдено» (the «все» line stays) |
| Loading / error | `FieldsSkeleton count={4}` under the search field; `InlineNotice` «Не удалось загрузить список. Попробуйте ещё раз.» with «Повторить» |
| «Создать …» with organization «все» (decided at gate-2, BR-TD-6) | uses the one organization the user may create in; with several it is shown disabled, «Создать команду — Выберите организацию»; with none, absent |
| «Создать команду» / «Создать проект» | only with the right in some department (administrator: all; «Менеджер»: his); the form's department is pre-set per BR-TD-6 |
| «Создать департамент» | administrator only; the dialog asks the name; the colour is automatic (§ 7.1) and shown as a helper «Цвет назначается автоматически» ◆ |

Narrowed lists carry the `Tag` «сужено: департамент Логистика, команда MARS» and, when empty, «Здесь нет проектов, доступных вам.» / «Здесь нет команд, доступных вам.»; «Структура» empty: «Здесь нет департаментов, доступных вам.» ◆. A department holding no visible team (or project) shows «Команд нет» ◆ («Проектов нет» ◆) in its column.

### 3.5 Repair of a selection (BR-TD-18)

Read against current offers in three occasions; the order is fixed: a chosen team, else a chosen project, gives the department its own; then, least specific first, each chosen value that is gone, hidden or no longer fits resets to «все».

| Occasion | Example | Notice |
|---|---|---|
| Panel opens with a remembered selection | stored «Ромашка / Финансы / VENUS», VENUS now in «Логистика» | «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика / VENUS.» |
| A move, link, membership or sight change seen while open | «Логистика / VENUS / A», VENUS unlinked from A and moved to «Финансы» | «Выбор сброшен: проект A.» (the department follows VENUS silently) |
| No organization left | | «Ранее выбранный контекст недоступен.» and «Главная» opens (feat-043 § 3) |

### 3.6 Deep link to a hidden object (BR-TD-12..-14)

The address of a hidden project, team or department answers the not-found frame of its kind, the same as for an address that names nothing: «Проект не найден.», «Команда не найдена.», «Область не найдена.» (`NotFound`, «К структуре»). The selection does not change; a hidden `?teamId=` or `?departmentId=` answers the same.

## 4. Teams (US-1)

### 4.1 «Новая команда» (`FormDialog`)

| Field | Type | Required | Validation | Error text |
|---|---|---|---|---|
| «Название» | `InlineTextField` | yes | not empty; unique in the organization (feat-028 BR-OL-3) | «Введите название» ◆; «Команда с таким названием уже есть в организации» ◆ |
| «Департамент» | `InlinePickerField` (single, search) | yes | one of the offered; the server answers 400 too | «Выберите департамент» |

Helper under «Департамент»: «Команда принадлежит одному департаменту.» ◆. The picker offers the organization's departments the user may create in (administrator: all; «Менеджер»: only those he manages; if one, it is pre-set), never «Верхний уровень», a project node or a sub-area. Submit «Создать»; done: «Команда «NEPTUNE» создана.» and its page opens (feat-043 § 5.3). Opened from a department's «Команды» or «Структура» the department is pre-set. Server: `Недостаточно прав` shows as `ErrorBanner` in the dialog; «Область не найдена.» (department deleted meanwhile) shows under the field.

### 4.2 Team page and move

Team «Обзор»: the line «Департамент: Логистика» (a link: sets the department) replaces the «Области» section; `ActionButton` «Переместить» next to it, shown to the administrator and to a «Менеджер» of the team's department. The department's «Команды» lists the teams standing in it with no link picker and no «Отключить» (BR-TD-2).

«Перемещение команды «SATURN»» (`FormDialog`): `InlinePickerField` «Новый департамент» (required, «Выберите департамент»); offers the organization's other departments (administrator: all; «Менеджер»: those he manages, and only when he manages the team's own department too). Helper: «Участники, доступы и спринты остаются на месте.» ◆ Submit «Переместить»; done: «Команда «SATURN» перемещена в «Финансы».» ◆. No other department to offer: text «Нет другого департамента, в который вы можете переместить команду.» ◆ and «Отмена» only.

**Refusal (BR-TD-4, OQ-9 A):** the dialog stays open with an inline `Alert` under the field: «Команда связана с проектами. Отключите её от них перед перемещением.» and, below, «Проекты: A [Отключить], B [Отключить]» (the team's linked projects; the actor sees all of them, being administrator or manager of the department). «Отключить» opens `ConfirmDialog` «Отключить команду «VENUS» от проекта «A»?» with «Рабочие элементы команды в проекте станут «Без команды».» — in the product it is feat-028's unlink dialog with its choice of destination; the prototype shows the default. After it the move dialog returns with the chosen department kept and the project gone from the list; with none left, «Переместить» succeeds. A team moved to the department it stands in is not offered (the server accepts it as a no-op).

States: loading — `FieldsSkeleton` in the dialog while offers load; error — `ErrorBanner` «Повторить»; permissions — a reader or organization «Участник» without a grant sees no «Создать команду», no «Переместить» (403 «Недостаточно прав» if reached by address).

## 5. Projects (US-5)

### 5.1 «Новый проект» (`FormDialog`)

Fields: «Название» (required; «Введите название»; unique «Проект с таким названием уже есть в организации» ◆), «Ключ» (required; «Ключ: от 2 до 10 латинских букв или цифр» ◆; as today), **«Расположение»** (`InlinePickerField`, required, «Выберите департамент»). «Расположение» offers the departments the user may create in and never «Верхний уровень», a project, a sub-area or a team; it is pre-set per § 3.4 (BR-TD-6). Helper: «Проект всегда стоит в департаменте.» ◆ Entry points: «Новый проект» in «Проекты» and in a department's «Проекты» / «Структура» column, and the selector's «＋ Создать проект». A team chosen in the header does not link the new project (it appears in the team's «Проекты» only after a link). Done: «Проект «Epsilon» создан.»; a department «Менеджер» sees it at once, through his grant.

### 5.2 Project page: place, move, links

«Общие»: «Расположение» section reads «Департамент: Логистика» (a link) with «Переместить» (users with `area.write` on the department). **Move dialog** «Перемещение проекта «A»»: «Новое расположение» (required, «Выберите департамент»), offers departments only, current excluded, «Верхний уровень» gone; for a «Менеджер»: the ones he manages. **Refusal:** inline `Alert` «Проект связан с командами. Отключите их перед перемещением.» and «Команды: MARS [Отключить], VENUS [Отключить]», the same unlink flow as § 4.2. A request to the top level (API only) answers «Проект должен находиться в департаменте.», shown as the banner.

«Команды» (project): `DataTable` «Команды проекта», row action «Отключить от проекта», `ActionButton` «Добавить команду». **Link picker** «Добавление команд в проект «A»» (`InlineMultiPickerField`): offers only the teams of A's department not yet linked; helper «Предлагаются команды департамента «Логистика».» ◆ Submit «Добавить»; none chosen: «Выберите хотя бы одну команду» ◆. Empty offers: «Все команды департамента уже подключены к проекту.» (feat-043 text) or, when the department has no team, «В департаменте «Маркетинг» нет команд. Создайте команду в этом департаменте.» ◆. Stale picker (a move or a link raced): `ErrorBanner` «Команду можно связать только с проектом своего департамента.», nothing changes, the picker is re-read. The team page's «Проекты» picker mirrors this with the team's department's projects.

States as feat-043 § 4. Permissions: the commands are absent without the code; the page itself needs sight.

## 6. «Видим всей организации» and what each role sees (US-6)

### 6.1 The setting

Project «Параметры», `Section` «Видимость»: MUI `Switch` labelled «Видим всей организации», helper «Проект и его команды увидят все участники организации.» ◆ Off at creation and after the upgrade. Turning on opens `ConfirmDialog` «Сделать проект «D» видимым всей организации?» with «Проект, его команды и департамент увидят все участники организации, но не другие проекты департамента.» ◆ (button «Сделать видимым»); done «Проект «D» виден всей организации.» ◆; turning off needs no confirmation: «Проект «D» виден только участвующим.» ◆ The control exists only for a holder of `organizations.visibility.write` on the project: the administrator, the project «Менеджер» and a department «Менеджер» (by inheritance; decided at gate-2); for anyone else the section is absent.

### 6.2 Surfaces by role (BR-TD-12..-17), seeded in the prototype

| Surface | Ann (administrator) | Olga (VENUS, ORION) | Max («Менеджер» «Логистика») | Pavel (no grant, no team) |
|---|---|---|---|---|
| Department selector («Ромашка») | all five | «Логистика» | «Логистика» | «все», «Нет доступных департаментов» |
| Team selector | all | VENUS (Мои команды), MARS | MARS, VENUS, SATURN | «Нет доступных команд» |
| Project selector | A, B, G, C, D, E | A | A, B, G | «Нет доступных проектов» |
| «Структура» | every department with its teams and projects | «Логистика»: VENUS, MARS, A only | «Логистика» in full | «Здесь нет департаментов, доступных вам.» |
| «Проекты» / «Команды» | all | A / VENUS, MARS | A, B, G / MARS, VENUS, SATURN | «Здесь нет проектов, доступных вам.» / «Здесь нет команд, доступных вам.» |
| Commands | all | none | create and move in «Логистика»; links of its teams | none |
| «Видим всей организации» switch | yes | absent | yes, on «Логистика» projects | absent |

Seeing a team shows none of its other projects; seeing a department shows only its visible children. «Видим всей организации» on D makes D, JUPITER and «Финансы» (showing D and JUPITER only) appear for everyone on their next request. With organization «все» lists carry an «Организация» column; counts are computed after the sight filter, so a hidden object changes none.

## 7. Department colour and the calmer «Структура» (BR-TD-20; gate-2 feedback)

### 7.1 The colour

Departments, teams and projects each have a colour: stored with the object, given automatically at creation (the upgrade colours every existing one, the fallback departments and what stands in them included), not editable in this cut — no colour picker anywhere. The API returns a palette key; the panel owns the hex and never shows a palette name. Palette of 12 as in spec BR-TD-20 (blue, orange, green, purple, red, teal, amber, pink, indigo, lime, magenta, cyan). One rule (spec BR-TD-20): the unused entry with the largest smallest hue-circle distance to those in use, ties by palette order; all in use: the least used. Scope: a department — its organization's active departments; a team or a project — the active ones of its kind in its department, with the department's own colour counted as used, so a child never repeats it while another hue is free. A move keeps the colour; a deletion frees it. «Логистика» (blue) thus has MARS orange, VENUS magenta, SATURN green; the theme supplies the dark variants of the hues.

Where it shows, always beside the name (colour is never the only signal): **department** — a 10 px dot (`Avatar`) before each option and the selected value of the department selector, in the «Департамент» column of «Проекты» and «Команды», in the «Департамент: …» line of a team or project page, and as the stripe and 9 % header tint of its card in «Структура»; **team and project** — a smaller 8 px dot before the name on its chip in «Структура», before each option and the selected value of the team and project selectors, in the name column of «Проекты» and «Команды», and in the page title of the team or project. The department stripe stays the dominant colour; children are small accents only (no filled chips, no coloured text).

### 7.2 «Структура»: two views

A page-title row holds, right to left: «＋ Создать департамент» (outlined, administrator), **one outlined `Button` with an icon that names the view it switches to** — «Дерево» while the list shows, «Список» while the tree shows (no `Tabs`, no `ToggleButtonGroup`) —. The default is «Список»; the real panel keeps the last choice per user (a user preference, restored on the next visit; the prototype uses `localStorage` and survives a reload). The upgrade `InlineNotice` (§ 8) sits above both views. Neither view has a text filter (§ 3.1).

**«Список» (cards).** One `Card` per department, white, hairline border, no shadow, 4 px left stripe in the department colour and a 9 % header tint; the name is the card's `h2`; small outlined `Chip`s «Команды 3», «Проекты 2» and, on the two upgrade departments, «создан при обновлении» (amber). Body: columns «КОМАНДЫ» / «ПРОЕКТЫ», each object an outlined `Chip` with its dot that opens it (team chip «моя» for the user's own; project chip with its key); empty column «Команд нет» / «Проектов нет»; stacked under 640 px. One outlined `Button` «＋ Создать» per card opens a `Menu`: «Команду», «Проект» (department pre-set). With organization «все» each organization is an `h4` heading.

**«Дерево» (today's area tree, `shared/ui/TreeRegistry`, changed only by doc-016).** Columns «Название», «Тип», «Описание»; toggle chevrons; row menu «Действия: …». In the header cell of the last (actions) column, right-aligned, a small text-less `IconButton` with `Tooltip` and `aria-label`: «−» «Свернуть всё» while anything is expanded, «+» «Развернуть всё» when all is collapsed. Every row type has its icon before the colour dot, the same icons in search results and in the panel columns: department `ApartmentOutlined`, project `FolderOutlined`, sub-area `PlaceOutlined`, team `GroupsOutlined`. Rows: the department (type «Департамент», 4 px stripe and tint in its colour, a colour dot, the «создан при обновлении» chip on the two upgrade departments); under it its projects (dot, key tag; below each its sub-areas as today) and its **teams** (type «Команда», team icon, dot, «Моя команда» tag) — a team is a row of its department, never of a project. No «Верхний уровень» rows, no department–team link rows. With organization «все» the organization is a plain heading row. Row menus by right and sight (`area.write` on the department): department — «Создать команду», «Создать проект», «Переименовать», «Удалить департамент» (refused with the BR-TD-5 texts: «Нельзя удалить департамент, в котором есть команды»; with a project «Нельзя удалить департамент, в котором есть проекты» ◆); team — «Переместить» (§ 4.2 refusal); project — «Открыть проект», «Переместить» (§ 5.2), «Новая вложенная область»; sub-area — «Переименовать». No menu for readers.

**States (both):** loading — two `Skeleton` blocks; empty — «Здесь нет департаментов, доступных вам.»; error — `ErrorBanner` «Повторить»; the content is sight-scoped (§ 6.2), so Olga's tree holds «Логистика» with A (and its sub-areas), VENUS and MARS only.

## 8. The upgrade's visible outcome (US-2)

Three things are visible; none keeps a flag in the data:

1. The departments «Команды без департамента» and «Проекты без департамента» are ordinary departments in every list and selector (with the «создан при обновлении» chip, § 7.2) and an automatic colour.
2. Administrators (and a «Менеджер» of that department) see on «Структура» an `InlineNotice` while either department still holds a team or a project: «После обновления созданы департаменты: «Команды без департамента», «Проекты без департамента». Переместите их команды и проекты в нужные департаменты.» ◆ It disappears by itself when both are empty; others never see it.
3. A remembered selection moved by the upgrade is repaired with the § 3.5 notice «Ранее выбранный контекст недоступен. Выбрано: …».

The cut links, the made-«Без команды» items and the log are in the MR only (BR-TD-19).

## 9. Narrow widths and accessibility

Down to 640 CSS px as feat-043 § 9: dialogs are full width. Columns are labelled listboxes; Escape closes the palette, panel or a dialog and returns focus to its trigger; a notice is `role="status"`, a refusal `role="alert"`.

## 10. Prototype scenarios (every one exists in `prototype/index.html`)

1. Cascade: choose MARS (parents fill in); «Финансы» (notice «Выбор сброшен: команда MARS.»); a project, then an unlinked team; department «все»; team «все» keeps the project.
2. Olga with «Все организации»: headings per organization; ORION opens «Другая» / «Склад». Pavel: empty offers; loading and error.
3. «＋ Создать …» in a column; team, project, department creation with validation and the next colour.
4. Move VENUS and A: refusal, «Отключить», retry; SATURN at once; Max only from «Логистика»; «Гонка» banner.
5. «Видим всей организации» on D (Ann), then Olga sees D, JUPITER, «Финансы»; user switch rescopes with «Выбор сброшен: …»; «Запомнено …», «Ann переносит VENUS», the address buttons.
6. «Структура»: card list; the «Дерево» / «Список» button (survives a reload); tree stripes, type icons, team rows, row menus, «−»/«+» header button, deletion refusal; upgrade notice and chips; colour dots everywhere.
7. Header: path button and panel (columns, create lines, narrowing, arrows, Esc, click outside); panel search and Ctrl+K palette («Недавние», «jup», «фин», «fin», «orion» as Olga, Enter, Esc).
