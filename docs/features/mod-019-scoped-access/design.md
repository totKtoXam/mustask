# Design: Scoped access (mod-019)

Stage 3 artifact. Rules (BR-SA-n) and scenarios are in `spec.md` and `stories/`; this file is layout, MUI components, states and Russian words. Every string in «» is final; ◆ marks a string no earlier design or the error catalogue quotes. The reference tracker is never named.

- Base: [feat-043 design](../feat-043-area-project-team-navigation/design.md) («as feat-043 § n»: menus, object pages, «Настройки»), [mod-018 design](../mod-018-area-workspace/design.md), [mod-016 design](../mod-016-organization-access/design.md) (members, role dialogs, user search), [ui-kit.md](../../conventions/ui-kit.md).
- Prototype: `prototype/index.html` (open it in a browser) — the three grant sections, the grant, role and revoke dialogs, the node page with the switch, the switch-off confirmation, the create-project dialog, and a «Вы — …» user switch that shows each user's controls and the codes re-read.
- Nothing is invented where the kit has a part. The kit lacks a switch: plain MUI `Switch` + `FormControlLabel` is allowed (ui-kit § 10).

## 1. Screen map

| Screen | Address (under `/organizations/:o/`) | Stories |
|---|---|---|
| Department «Участники» (new menu entry; feat-043 § 6 said «no Участники») | `areas/:d/members` | US-1, US-5 |
| Project «Участники и доступ» (new menu entry) | `projects/:p/access` | US-1, US-2, US-5 |
| Team «Участники и доступ» (exists: members; the grants are added) | `teams/:t/members` | US-1, US-5 |
| Node's «Структура» and sub-area page: «Наследовать доступ» | `areas/:node/overview`, `areas/:a/overview` | US-2, US-5, US-8 |
| Dialogs: grant, role, revoke, switch-off | over the pages | US-1, US-2 |
| «Создать проект» for a department manager | org «Проекты», department «Проекты» | US-4, US-8 |
| Per-object controls, re-read and notices | all | US-4 |

Menus after this task: department «Обзор», «Проекты», «Команды», «Участники»; project «Общие», «Структура», «Рабочие элементы», «Канбан», «Бэклог», «Команды», «Участники и доступ»; team unchanged. A project node and a sub-area have no grant section (OQ-1 A). Every member reads the lists; the entries are present for everyone.

## 2. The grants section (one component, three scopes)

`GrantsSection` in the organizations slice, parameterised by scope (department, project, team); the page passes the object and its served codes. `PageHeader` (title: «Участники» / «Участники и доступ»), a notice, `FilterBar` «Найти участника», `TableCard` + `DataTable` «Доступ»: no pager (the whole list is read once; the 200-row component test narrows it by typed text).

```
Участники и доступ                                   [ Выдать доступ ]
(i) Здесь показан только выданный доступ. Роль в организации ...
[ Найти участника ]
 Участник              Роль         Откуда                    Выдан               ⋮
 Ирина К.              Участник     В проекте                 Анна С., 06.11.2026 ⋮
 Ольга П.              Читатель     От департамента «Логис…»  Анна С., 05.11.2026
 Вы  Кирилл Т.         Менеджер     В проекте                 Анна С., 06.11.2026 ⋮
```

| Part | Component | Words |
|---|---|---|
| Header | `PageHeader`; primary `ActionButton` with `Add` | «Выдать доступ», only with the object's grant code |
| Notice (project, department) | `InlineNotice` info | «Здесь показан только выданный доступ. Роль в организации действует везде и задаётся на странице «Участники» организации.» ◆ |
| Notice (project, node switched off, in a department) | `InlineNotice` muted | «Доступ департамента «Логистика» в этом проекте отключён.» ◆ |
| Columns | `HeadCell`, `Cell` | «Участник» (`UserLine`: name, e-mail below; `Tag` «Вы» on own row; «неактивен» as mod-016; «Неизвестный пользователь»), «Роль», «Откуда» (project only: `Tag` «В проекте» or the link «От департамента «Логистика»» to its «Участники»), «Выдан» (giver and `dayjs` date-time, same marks) |
| Row menu | `MoreActionsMenu` «Ещё действия: <имя>» | «Изменить роль», «Отозвать доступ». The column is absent without the grant code |
| Inherited row (project) | the same row, muted | no menu: it is changed on the department. A user holding both a project grant and an inherited one has two rows |
| Own row | | «Изменить роль» is absent (BR-SA-19); «Отозвать доступ» stays |

Roles by scope (the select and the column use the same labels; by decision OQ-10, 2026-11-06): department «Читатель», «Участник», «Менеджер»; project the same; team «Читатель», «Участник», «Менеджер». The select opens on the lowest role, «Читатель», on every scope including the team (D-7). Where a text could mix an organization role with a scoped one it is qualified. Rows are ordered by name.

| State | The user sees |
|---|---|
| Loading | `TableSkeleton` in the card, one `role="status"` «Загрузка данных» |
| Error | `ErrorBanner` «Не удалось загрузить доступ.» ◆ with «Повторить» |
| Empty | `InlineEmpty`: «В департаменте пока никому не выдан доступ» ◆ / «В проекте пока никому не выдан доступ» ◆ / «К команде пока никому не выдан доступ» ◆; managers add «Выдайте роль, чтобы человек работал с объектом.» ◆ under it, and the header command stays |
| After filter | «Ничего не найдено», «Сбросить фильтр» |
| Object not found | `NotFound` by level (feat-043 § 4), menu kept |
| 403 on a write | see § 3; the page re-reads the object's codes and drops the controls |
| Reader (no grant code) | the list, no «Выдать доступ», no column of menus; no disabled control anywhere |

Team page: two `Section`s on one page, «Участники команды» (feat-028's table with one added column «Роль в команде»: the role, or «—» when the member holds none; «Добавить участника» stays the page's primary) and «Доступ к команде» (this table, «Выдать доступ» as a neutral `ActionButton`). The grant list may hold people who are not members, and members may hold no grant: belonging to a team grants nothing. Empty: «К команде пока никому не выдан доступ».

## 3. Dialogs: give, change, revoke

**«Выдача доступа»** (`FormDialog`, footer «Отмена», «Выдать», disabled until both fields are filled; opens on the first field).

| Field | Component | Words and rules |
|---|---|---|
| «Участник организации» * | the slice's `UserSearchField` (mod-016 § 3.5), scope = this object | Candidates are active members of this organization who hold no grant on the object, never the caller (BR-SA-2, -19); the server leaves them out. Hint lines as mod-016, and for "nobody": «Пользователи не найдены. Себе доступ выдать нельзя; у кого он уже есть, тому роль меняется в списке.» ◆ |
| «Роль» * | kit `InlineSelectField`, `frame="always"`, preselected with the lowest role, «Читатель», on every scope including the team | helper by the chosen role (below) |

| Role | Helper under «Роль» ◆ |
|---|---|
| Департамент «Читатель» | Просматривает проекты департамента и их рабочие элементы. |
| Департамент «Участник» | Всё, что у «Читателя», а также создание и изменение рабочих элементов проектов департамента. |
| Департамент «Менеджер» | Всё, что у «Участника», а также управление департаментом, его проектами и областями: названия, доступ, связи, создание проектов. |
| Проект «Читатель» / «Участник» | Просматривает проект. / Всё, что у «Читателя», а также рабочие элементы проекта и порядок его бэклога. |
| Проект «Менеджер» | Всё, что у «Участника», а также настройки проекта, доступ, связи и области проекта. |
| Команда «Читатель» | Только числится в списке «Участники и доступ» команды; бэклог и спринты команды видят все, порядок и спринты не меняет. |
| Команда «Участник» | Упорядочивает бэклог команды и ведёт её спринты. |
| Команда «Менеджер» | Всё, что у «Участника», а также участники, название, связи команды и доступ к ней. |

Refusals: `404 organizations.membership.not_found` — «Участник не найден.» under the picker, the chosen user cleared, the list re-read; the self-grant validation — «Нельзя выдать доступ самому себе.» ◆ under the picker (reachable only past the panel); a wrong scope or node type — the dialog's `InlineNotice` «Эту роль нельзя выдать на этом объекте.» ◆; `403 organizations.access.forbidden` — the dialog body becomes `InlineNotice` «Недостаточно прав» with only «Закрыть» (the page behind has dropped its controls); the object gone — the dialog closes onto `NotFound`. Success: the dialog closes, the list and the object's codes are re-read, notice «Пользователю «Олег Иванов» выдан доступ.» ◆, focus returns to «Выдать доступ».

**«Изменение роли»** (`FormDialog`, «Сохранить» disabled until the role differs): «Пользователь: <имя>», the same select. Success: «Роль пользователя «Олег Иванов» изменена.» (mod-016). A grant gone meanwhile: `InlineNotice` «Доступ уже изменён или отозван. Список обновлён.» ◆.

**Revoke** (`ConfirmDialog`, destructive, focus «Отмена», «Отозвать»):

| Row | Title | Text ◆ |
|---|---|---|
| Someone else | «Отозвать доступ?» | «Пользователь <имя> потеряет роль «Участник» на объекте «A». Доступ, который он получает по роли в организации или от департамента, сохранится.» |
| Oneself | «Отозвать свой доступ?» | «Вы потеряете роль «Менеджер» на объекте «A». Вернуть её сможет другой менеджер или администратор организации.» |

Success: «Доступ пользователя «Олег Иванов» отозван.» ◆ / «Ваш доступ отозван.» ◆; focus on the next row's menu, else «Выдать доступ» (or the `h1` when the caller's own controls are gone).

## 4. «Наследовать доступ» (US-2, BR-SA-6, -7)

A `Section` «Доступ» on the project node's «Структура» (after the facts) and on a sub-area's page. A department's page shows none.

| Viewer | The user sees |
|---|---|
| Holds `organizations.area.write` on the node and on its parent (top level: `.area.create`) | `FormControlLabel` + `Switch` «Наследовать доступ» with the state text below |
| Anyone else | a read-only fact (`labelPlacement="start"`) «Наследовать доступ: Включено» / «Выключено», the same state text; no switch is drawn (absent, not disabled) |

| Node | State text ◆ |
|---|---|
| Project node in a department, on | Доступ департамента «Логистика» действует в проекте «A», его рабочих элементах и областях. |
| ... off | Доступ департамента «Логистика» в проекте «A» не действует. Действует только доступ, выданный в самом проекте. |
| Top-level project node | Над проектом нет департамента, поэтому выбор пока ни на что не влияет. Он начнёт действовать, когда проект переместят в департамент. |
| Sub-area, on | Права на управление областями, действующие в «A», действуют и здесь. На рабочие элементы это не влияет. |
| ... off | Права на управление областями из «A» здесь не действуют. На рабочие элементы это не влияет: их определяет проект. |

On → immediate request, `Switch` busy while it runs, notice «Наследование доступа включено.» ◆. Off → the confirmation of § 5 first; the switch stays on until it is confirmed. A 403 (the caller lost the code): notice «Недостаточно прав», codes re-read, the switch becomes the read-only fact. Loading: `FieldsSkeleton`; error: `ErrorBanner`.

## 5. The switch-off confirmation (OQ-6, BR-SA-7, -19, R-27)

`ConfirmDialog` (destructive, `cancelAutoFocus`, `maxWidth="sm"`), title «Отключить наследование доступа?», «Отмена» / «Отключить». It reads the impact when it opens.

```
Отключить наследование доступа?
Доступ департамента «Логистика» перестанет действовать в проекте «A», ...
Потеряют доступ через департамент: 2 пользователя.
  Не учитываются администраторы и те, у кого есть доступ в самом проекте.
(!) Вы входите в их число. После отключения вы потеряете управление этим ...
Доступ в проекте, выданный этими пользователями (1):
  Кому          Роль         Кто выдал
  Павел Р.      Менеджер     Ирина К.
  Он не прекратится при отключении. Отозвать его можно вручную ...
                                              [ Отмена ] [ Отключить ]
```

| Part | Component | Words ◆ |
|---|---|---|
| Lead (node) | `Typography` | «Доступ департамента «Логистика» перестанет действовать в проекте «A», его рабочих элементах и областях. Доступ, выданный в самом проекте, сохранится.» |
| Lead (sub-area) | | «Права на управление областями из «A» перестанут действовать в области «Платформа» и её вложенных областях. На рабочие элементы это не влияет.» |
| Count | bold `Typography` + muted line | «Потеряют доступ через департамент: 2 пользователя.» (1 пользователь, 2-4 пользователя, 0 and 5+ пользователей; sub-area: «Потеряют право управлять областью: …»). Muted: «Не учитываются администраторы организации и те, у кого есть доступ в самом проекте.» Zero: «Это не отнимет доступ ни у кого.» |
| Self-lockout | `InlineNotice` warning, only when the caller is counted | Node: «Вы входите в их число. После отключения вы потеряете управление этим узлом и не сможете включить наследование обратно: это сможет сделать только администратор организации.» Sub-area: «…: это сможет сделать менеджер проекта или администратор организации.» |
| Given grants | heading + compact `DataTable` «Выданный доступ» (columns «Кому», «Роль», «Кто выдал»), body scrolls over 6 rows; list only | Heading «Доступ в проекте, выданный этими пользователями (N)»; note «Он не прекратится при отключении. Отозвать его можно вручную в «Участники и доступ» проекта.» The block is absent when N = 0 |

| State | The user sees |
|---|---|
| Loading | `FieldsSkeleton` for count and list; «Отключить» disabled |
| Error | `InlineNotice` «Не удалось узнать, кого это затронет. Попробуйте ещё раз.» with «Повторить»; «Отключить» disabled |
| Confirmed | closes; notice «Наследование доступа отключено.»; if the caller was counted, their codes on the node are re-read and the switch becomes the read-only fact, focus on the `h1` |
| 403 on confirm | dialog alert «Недостаточно прав»; the page re-reads |

The count is read when the dialog opens and is not re-judged at confirm; the server applies the switch on the locked row (BR-SA-13).

## 6. What each served code shows (US-4, BR-SA-11, -12, -18)

Every cell reads the object's served codes (never the organization answer); a missing code makes the control absent. `useAccessCheck()` answers for the object of the page; list rows get a flag from their read (§ 8).

| Object (codes served on it) | Present with the code | Absent without it |
|---|---|---|
| Department `area.grant` | «Выдать доступ», row menus on «Участники» | both; the list stays |
| Department `area.write` | «Параметры» group «Департамент» in «Настройки»: «Переименовать», «Изменить описание»; «Создать проект» on its «Проекты» | the group (address: «Доступ ограничен»), the command |
| Department `area.link` | «Добавить команды», «Отключить от департамента» | both |
| Project `project.grant` | «Выдать доступ», row menus (a department manager holds it through reach; a switched-off or top-level project gives none) | both |
| Project `project.write` / `.link` | «Параметры» group «Проект» (rename, description, default team) / «Добавить команды», «Отключить от проекта» | the group / both |
| Node or sub-area `area.write` | «Добавить область», «Параметры» group «Область», «Наследовать доступ» as a switch; on a sub-area also «Переименовать», «Переместить», «Удалить область». A project manager holds it on the node and every sub-area, not on the department | the commands; the switch becomes the read-only fact. «Название меняется вместе с проектом.» stays the node's only name text |
| Team `team.grant` / team write / `team.link` | «Выдать доступ», row menus / «Добавить участника», «Удалить из команды», «Параметры» / «Добавить проекты», «Отключить от проекта» | each |
| Tree read: ids with `area.write` | row commands and move destinations in org «Структура» only on those ids | the row menu column for other rows |
| Organization `area.create` | «Создать департамент» | the command |

«Настройки» is always present; its groups follow the codes (feat-043 § 3): «Организация» for everyone, «Департамент», «Команда», «Проект», «Область» for those who hold the object's code (no longer «Администратор» only).

**«Создать проект» (BR-SA-12, -17).** The command is on org «Проекты» and on a department's «Проекты» (the place preselected). It is absent when the caller's places are empty. The dialog «Новый проект» is mod-018 § 7's with «Расположение *» offering «Верхний уровень» only with `area.create` and each department with `area.write`; with one place the field shows it, never empty. Races: `403` — dialog alert «Недостаточно прав», the places re-read, a cleared choice if it is gone, the dialog closes if none is left; the admin's `404` — «Область не найдена.»; `409 type_not_allowed` — mod-018's text. Done: «Проект «Зета» создан.» and the project's «Общие» opens (reached through the department grant). A department manager's «Добавить проект» (placing an existing project) is offered only when the caller holds the area code on the project's node, its current parent and the destination (BR-MX-3); otherwise absent.

**Direct address.** A page whose entry the caller may not use answers «Доступ ограничен»; a node's rename through the area route answers `409 area.root_is_fixed` «Корневая область меняется только вместе с проектом» (alert in a dialog, none is drawn by the panel).

## 7. Re-read and notice flows (BR-SA-11, ADR-0029 § 10)

| Trigger | Panel does |
|---|---|
| The caller writes a grant on the object (their own included), or the switch | re-reads that object's codes and the lists |
| Any call under the object answers 403 or 404 | re-reads that object's codes; a «Проект не найден» also re-reads the project list and the selectors' offers |
| Window refocus, page load | re-reads the codes of the object on screen |
| Someone else's change | seen at the next trigger above; no polling |

When a re-read shows different codes, one notice per object, with no role named: «Ваш доступ в проекте «A» изменился.» ◆ / «…в департаменте «Логистика»…» / «…в команде «MARS»…». The organization notice keeps its wording («Ваша роль в организации «…» изменилась: «…».»). The same difference is announced once. A control that held focus and vanished sends focus to the page's `h1`; an entry lost under the user becomes «Доступ ограничен»; the controls follow the new codes without a reload.

## 8. For the developer

- **Reuse:** `PageHeader`, `Section`, `TableCard`, `DataTable`, `FilterBar`, `MoreActionsMenu`, `ActionButton`, `Tag`, `InlineEmpty`, `InlineNotice`, `ErrorBanner`, `TableSkeleton`, `FieldsSkeleton`, `FormDialog`, `ConfirmDialog`, `InlineSelectField`, `NotFound`, `useNotice`; from the slice `UserSearchField`, `UserLine`, `memberTexts` (extend). One new slice part: `GrantsSection`; plus `GrantDialog`, `InheritAccessControl`, `SwitchOffDialog`. No `shared/ui` part.
- **Reads the screens need (the contract is the plan's):** the grants of an object (project's includes inherited rows with the department, giver, time); a candidate search aware of the object (excludes holders and the caller); the switch impact (count, whether the caller is counted, the given grants, up to 50 rows); the node's switch state and whether the project has a department (the project and node reads); a per-row flag for "the caller may unlink this team" on link tables; the link pickers' offers filtered by the other end's code (US-7). None of them is a matrix of every object.
- **Owed to `docs/error-codes.md` (◆ strings):** the self-grant validation «Нельзя выдать доступ самому себе.», the wrong scope or type «Эту роль нельзя выдать на этом объекте.», the grant gone «Доступ уже изменён или отозван. Список обновлён.»; the rest exists.
- **Tests:** the 200-row component test and a test per surface of BR-SA-18; plural forms of the count as a unit table.
- **Widths (640 px):** tables scroll inside their card, dialogs go full width below 640 px, the header command overflows into «Ещё действия». **Accessibility:** tables named by their section; row menus «Ещё действия: <имя>»; the switch labelled «Наследовать доступ» with its state text as `aria-describedby`; «Вы», «неактивен», «От департамента» are text, never colour alone; dialogs return focus to their opener.

## 9. Decisions for gate 2

| # | Question | Options |
|---|---|---|
| D-1 | A department's grants | **(A, recommended)** a menu entry «Участники» (reverses feat-043's "no Участники"). (B) a section of «Обзор»: a long page, the same states twice. (C) in «Параметры»: readers would not see it, against US-5 |
| D-2 | A project's grants | **(A, recommended)** a menu entry «Участники и доступ». (B) a section of «Общие»: one fewer entry, but «Общие» gets a table with its own states |
| D-3 | Inherited grants on a project | **(A, recommended)** one table with the column «Откуда» and a muted, menu-less row. (B) two sections «В проекте» and «От департамента»: two filters, two empty states |
| D-4 | A team's two lists | **(A, recommended)** two sections on one page. (B) `BoxedTabs`: a reader sees one list at a time and misses the other |
| D-5 | The picker and those who may not be given a role (holders, oneself) | **(A, recommended)** never offered, with the hint line on "nobody". (B) shown with a reason, disabled: against "unavailable is absent" |
| D-6 | The user picker | **(A, recommended)** the slice's `UserSearchField` (server search, mod-016). (B) an async kit picker: a kit task of its own |
| D-7 | The role in the grant dialog | **(A, recommended)** preselect the lowest role; its helper shows. (B) none: one more click, one more error text |
| D-8 | One's own row | **(A, recommended)** no «Изменить роль», «Отозвать доступ» with its own confirmation. (B) both shown, the server refuses: a known refusal offered |
| D-9 | The switch control | **(A, recommended)** `Switch`, on at once, off after the confirmation. (B) `Switch` + «Сохранить»: a form for one bit. (C) `InlineSelectField` «Включено/Выключено»: reads as a setting, not as an on/off |
| D-10 | Whoever may not switch | **(A, recommended)** the read-only fact with the state text (US-2). (B) nothing: the state is lost for readers |
| D-11 | Why a project shows no inherited rows | **(A, recommended)** the muted line «Доступ департамента … отключён.» (B) nothing: an empty-looking list with no reason |
| D-12 | The switch-off dialog's list | **(A, recommended)** the count, the self-lockout line and the given-grants table up to 6 rows visible, scrolling. (B) also the names of every user losing access: a second list nobody asked for. (C) a typed confirmation for self-lockout: weight the spec rejected (OQ-9 C) |
| D-13 | «Создать проект» for a department manager | **(A, recommended)** on org «Проекты» and on the department's «Проекты» (place preselected). (B) org «Проекты» only |

Buttons keep feat-043's «Создать проект» / «Создать департамент» (the stories' «Новый …» is the dialog title).

## 10. New open questions

- **OQ-D1.** BR-SA-19 lists "the project grants on this project". On a **sub-area's** switch-off the project grants are not cut by it. Shown in the design: the same list for the sub-area's owning project (A); or only on project nodes (B). Recommended A, needs the human's word.
- **OQ-D2.** US-7 puts the unlink command on a row only for who holds the link code on both ends, and the pickers offer only linkable objects, but BR-SA-11 serves no matrix of objects. Design assumes a per-row flag and filtered offers (§ 8); the contract must say so.
