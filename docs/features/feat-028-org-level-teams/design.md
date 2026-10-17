# Design: Organization-level teams (feat-028)

Stage 3 artifact. Rules (BR-OL-n) and scenarios (US-1..US-8) are in `spec.md` and
`stories/`; this file is layout, MUI components, states and Russian words, and nowhere
else. Every string in «» is final; ◆ marks a string no earlier spec or design quotes.
The server's refusal texts are the registry's (spec § Error codes).

- Base: [mod-017](../mod-017-work-item-owning-team/design.md) § 7 (teams page, mark,
  default and delete dialogs), [mod-016](../mod-016-organization-access/design.md)
  (members), [ui-kit.md](../../conventions/ui-kit.md), ADR-0024 (640 px), BR-OL-12.
- Prototype: **none** (D-4). Every screen is a registry page, a facts page with
  sections, or a form / confirmation dialog, all of which the panel has. The one new
  field (§ 2) is shown in the kit's showcase before a page uses it.
- **New in the kit: one field** (`InlineMultiPickerField`) and one optional prop on
  `Tag` (§ 2). No token, colour, icon, dependency or error code of mine.

## 1. Screen map

| Surface | Address (below `/organizations/:o`) | Stories | Change |
|---|---|---|---|
| «Команды» of the organization | `/settings/teams` | US-1, US-3, US-5, US-7 | **new** (§ 3) |
| A team's page | `/settings/teams/:t` | US-1, US-3..US-6 | **moved** from the project, + description, projects (§ 4) |
| Dialogs of a team | over those pages | US-1, US-5 | create, rename, describe, delete (§ 5) |
| Link and unlink dialogs | over either end | US-3, US-4 | **new** (§ 6) |
| The project's «Команды» | `/projects/:p/settings/teams` | US-2..US-4, US-6 | its linked teams (§ 7) |
| The project's «Общие», «Проекты» | `/projects/:p/settings/general`, `/settings/projects` | US-2 | description, creation form (§ 8) |
| `…/projects/:p/settings/teams/:t` | - | US-1 | **retired**: «Страница не найдена» (§ 9) |

A team's name opens its page; a project's name on that page opens the project's
«Команды», which links back; either end makes and removes a link (§ 6).
**Navigation** (`app/navigation/model.ts`): a new entry in «Организация» after
«Проекты»: id `organization-teams`, «Команды», scope organization, code
`organizations.team.read`; a team's page is beneath it. The project group's «Команды»
stays; the group headings and the trail tell the two apart, and the project page has
an intro line (§ 7).

Designed on the recommended options of the spec's OQ-1..OQ-5. OQ-4 B would add
«Создать и добавить» to § 6.1; OQ-2 B or C removes the two header links of § 7; OQ-5 B
preselects «Без команды» in § 6.2.

## 2. Parts

| Part | Built from | Where |
|---|---|---|
| `DataTable`, `Section`, `InlineEmpty`, `MoreActionsMenu`, `Tag`, `ActionButton`, `TruncatedText`; `InlineTextField`, `InlineTextArea`, `InlineSelectField` (`frame="always"`) | kit | the new list, sections and dialogs |
| `FormDialog`, `ConfirmDialog`, `NameDialog`, `FactsPanel`, `RowActionsMenu`, `TeamMembersSection`, `PageHeader`, `StateFrame`, `ErrorBanner`, `RegistrySkeleton`, `FieldsSkeleton`, `InlineNotice` | existing | as named below |
| **New** `InlineMultiPickerField<O>` | MUI `Autocomplete multiple` on the kit's `FieldFrame`, with `InlinePickerField`'s props but `value: O[]`, `onChange(O[])` and `removeLabel(label)`; a host that searches the server passes `inputValue`, `onInputChange` and `filterOptions` returning its answer whole. The chosen are kit `Tag`s with a remove button before the input; the list stays open while picking, each option has `aria-selected` and a decorative `Checkbox` (`aria-hidden`); Backspace in an empty input removes the last; the box grows by rows, not in width. Accessible name = the label only | «Команды», «Проекты», «Участники» |
| `Tag.removeLabel?` | optional prop; the default stays «Удалить тег «<label>»» | the picker passes «Убрать «Alpha»» ◆ |

Why a kit part: BR-MX-27 and US-3 want a *searchable* multiselect (200 teams, a user
search); `InlineMultiSelectField` is not. Per ui-kit.md § 7 it ships with its test, a
showcase entry (every state) and a guide block before a page uses it (D-1).

## 3. «Команды» of the organization (US-1, US-5, US-7)

```
Команды                                                       [+ Новая команда]
| Команда    | Проекты        | Описание                         |     |
| JUPITER    | Alpha          | Платформа и инструменты          |  ⋮  |
| MARS       | Alpha, Beta    |                                  |  ⋮  |
| VENUS      | Нет проектов   |                                  |  ⋮  |
```

- `PageHeader` «Команды» with the primary «Новая команда» (`AddIcon`) for the holder
  of `team.create`. `DataTable` `aria-label="Команды организации"`, rows by name
  ignoring case (the server's order), no paging, no search.
- **«Команда»:** the name as a link to the team's page in `TruncatedText`.
  **«Проекты»:** the active projects' names joined by «, », cut; «Нет проектов»
  ◆ in `text.secondary` when none (a team whose only projects are deactivated reads
  so too). **«Описание»:** one line, cut; empty when none.
- **Row menu** (`MoreActionsMenu` «Ещё действия: MARS», shown to a holder of any of
  the three codes): «Переименовать», «Изменить описание» ◆ (`team.write`),
  «Удалить команду» (destructive, `team.delete`); each opens § 5's dialog.

| State | The user sees |
|---|---|
| Loading | `DataTable loading`: `TableSkeleton` with «Команда», «Проекты», «Описание» (+ the actions column) |
| Error | the table's error: `InlineNotice` with the server's text and «Повторить» |
| Empty | `StateFrame` `InboxOutlinedIcon`, «В организации пока нет команд» ◆, hint: «Создайте команду кнопкой «Новая команда» и подключите её к проектам.» ◆ for the holder with its action «Новая команда»; «Команды создаёт администратор организации.» ◆ otherwise, no action |

## 4. A team's page (US-1, US-3, US-4, US-6)

```
Команда: MARS            [Переименовать] [Изменить описание] [Удалить команду]
  Название   MARS
  Описание   Платформа и инструменты
Проекты (2)                                       [+ Добавить проект]
| Проект    | Ключ |                                          |
| Alpha     | ALP  |                                       ⋮  |
Участники команды                                 [+ Добавить в команду]
| Имя | Почта |                                             ⋮ |
```

- `PageHeader` `Команда: <имя>` (the title is the h1) with the three commands, each by
  its code (§ 10); they overflow into «Ещё действия» on a narrow bar.
- `FactsPanel`: «Название», «Описание». The description keeps its line breaks
  (`white-space: pre-wrap`, `overflowWrap: anywhere`, never cut here); none reads
  «Описание не задано» ◆ in `text.secondary`.
- **«Проекты»:** `Section` (h2, `count`) with `ActionButton` «Добавить проект»
  (`ThinAddIcon`) in its head, then a `DataTable` `aria-label="Проекты команды"`:
  «Проект» (a link in `TruncatedText` to that project's **«Команды»**, its end of the
  link, until feat-043 gives a project a page: D-3), «Ключ», and a row menu «Ещё
  действия: Alpha» with «Отключить от проекта» (destructive). Active projects only,
  by name. Without both link codes: no button, no menu column.
- **«Участники команды»:** the existing `TeamMembersSection`, unchanged but for taking
  no project (its reads and writes are at the team's address; its search covers the
  organization's active members).

| State | The user sees |
|---|---|
| Loading | `FormSkeleton fields={2}` for the facts, `TableSkeleton` in «Проекты», the members' own skeleton; the title reads «Команда» |
| Facts failed | `ErrorBanner` with «Повторить» in place of the facts; sections load on their own |
| «Проекты» failed | `InlineNotice` error with «Повторить» under the section head |
| «Проекты» empty | `InlineEmpty` «Команда пока не участвует ни в одном проекте.» (the button stays for a holder) |
| Team deleted or foreign | `NotFound` «Команда не найдена» with the settings navigation kept and «Команды» current |

## 5. Dialogs of a team (US-1, US-5)

### 5.1 «Новая команда»

`FormDialog`, submit «Создать». New component (the legacy `NameDialog` stays for
renaming); fields are kit fields, `frame="always"` (as mod-017 D-3):

```
Новая команда                                                          ✕
Название *   [                                       ]   hint «От 1 до 100 символов.»
Описание     [                                       ]   hint «Необязательно. Не более 2000 символов.»
Участники    [ Начните вводить имя или почту        ⌄]   hint «Среди активных участников организации.»
Проекты      [ Начните вводить название             ⌄]   hint «Необязательно. Можно добавить позже.»
                                                       Отмена  [Создать]
```

- «Название» `InlineTextField`, required, `autoFocus`. «Описание» `InlineTextArea`.
  «Участники» is `InlineMultiPickerField` over the server's user search (two
  characters, the option lines and hint rows of `userSearchTexts`); «Проекты» the
  same over the active projects (option: name, key in `text.secondary`). «Проекты»
  is drawn only for a user who holds both link codes.
- Checked on «Создать», nothing sent while refused: «Название» trimmed 1-100
  («Название должно содержать от 1 до 100 символов»), «Описание» at most 2 000
  characters (as typed, never trimmed; «Описание должно содержать не более 2000
  символов»).
- **Refusals** land on their field, input kept: `team.name_taken` «Команда с таким
  названием уже есть в организации» → «Название»; `member_not_in_organization`
  «Добавить в команду можно только активного участника организации» → «Участники»;
  `project.not_found` «Проект не найден.» → «Проекты» (the lists are read again, a
  choice no longer offered drops); `description_length` → «Описание»; 403 and the
  rest → the dialog's alert. Busy: fields disabled, Escape ignored.
- Done: closes, the list is read again, notice «Команда «Ядро» создана.» ◆ (replaces
  mod-002's «добавлена»); focus returns to «Новая команда».

### 5.2 Rename, description

- «Переименование команды»: today's `NameDialog` (`Сохранить`, no helper); the
  refusal text is the new `name_taken`. Notice «Команда переименована.»
- «Описание команды» ◆ (and «Описание проекта», § 8): `FormDialog` «Сохранить»
  (unavailable until the text differs), one `InlineTextArea` «Описание» with the
  stored text, hint «Не более 2000 символов.» ◆; an empty text is allowed and clears.
  Sent as typed. Over 2 000: «Описание должно содержать не более 2000 символов» under
  the field. Notice «Описание команды сохранено.» ◆ / «Описание проекта сохранено.» ◆.
  One component, the title and the notice are props.

### 5.3 «Удаление команды» (BR-OL-9)

`ConfirmDialog` (destructive, focus on «Отмена», confirm «Удалить команду»). The
per-project counts are read **when the dialog opens**:

```
Удалить команду «MARS»? Рабочие элементы станут «Без команды»:
  Alpha 5 · Beta 10 · Gamma 0 · Delta [деактивирован] 3     (one line each)
Восстановить команду будет нельзя.              Отмена  [Удалить команду]
```

| State | The user sees |
|---|---|
| Counting | `FieldsSkeleton count={2}` for the text; «Удалить команду» unavailable |
| Count failed | `InlineNotice` with the server's text and «Повторить»; deletion unavailable |
| Linked to projects | the sentence above; a `List` (`ul`), one `ListItem` per project by name, **all counted, zeros and deactivated ones included**: the name in `TruncatedText`, a `Tag` «деактивирован» ◆ after a deactivated one, the count right, never wrapping; a long list scrolls inside the dialog's body |
| Linked to none | «Удалить команду «ATLAS»? Восстановить команду будет нельзя.» |
| Done | closes, notice «Команда «MARS» удалена.» (the switcher's notice alone when it falls back: DC-7 as today); on the team's page, navigate to «Команды» of the organization; on the list, re-read |
| 404 | alert «Команда не найдена.»; on close the page reads itself again |
| 403 | alert «Недостаточно прав»; the codes are read again |

There is no 409: the last-team guard is retired. An item given to the team while
the dialog is open is not in the counts and is reassigned all the same.

## 6. Links (US-3, US-4, US-6)

### 6.1 «Добавление команд в проект» / «Добавление проектов в команду»

One component, two ends. `FormDialog`, submit «Добавить», unavailable until one is
chosen.

```
Выбранные команды смогут владеть рабочими элементами проекта «Alpha». Доступ их
участников к проекту это не меняет.
Команды *   [ MARS ×  VENUS ×  Начните вводить название ⌄]    Отмена  [Добавить]
```

- Project end: title «Добавление команд в проект», label «Команды», text ◆ as drawn.
  Team end: «Добавление проектов в команду», label «Проекты», text ◆ «Команда «MARS»
  сможет владеть рабочими элементами выбранных проектов. Доступ её участников к
  ним это не меняет.»
- **Options:** the organization's active teams (projects) by name, read when the
  dialog opens, minus the ones the page lists as linked (no route of its own); a
  project option shows its key under the name.
- No options: all linked — «Все команды организации уже работают над проектом.» ◆ /
  «Команда уже работает над всеми проектами организации.» ◆; none exist — «В
  организации пока нет команд.» ◆ / «В организации нет активных проектов.» ◆; typed
  text matches nothing — «Ничего не найдено».
- **States:** loading — the list says «Загрузка…», the field works; failed —
  `InlineNotice` «Не удалось загрузить команды. Попробуйте ещё раз.» (…«проекты»…)
  with «Повторить» in the field's place, «Добавить» unavailable; busy — all disabled.
- **Done:** closes, the page is read again; notice «Команда «MARS» добавлена в
  проект.» ◆ for one, «Команды добавлены в проект: 3.» ◆ for several; «Проект
  «Alpha» добавлен в команду.» ◆ / «Проекты добавлены в команду: 3.» ◆. The shell's
  team lists are read again (a project that got its first team shows «Бэклоги» and
  «Канбан»).
- **Refused:** 404 «Команда не найдена.» / «Проект не найден.» (a chosen end gone:
  nothing was linked) under the field, the options read again, a choice no longer
  offered dropped; the page's own end gone — the alert, the page read again and
  its frame; 403 — the alert «Недостаточно прав».

### 6.2 «Отключение команды от проекта» (BR-OL-8)

`ConfirmDialog` (destructive, focus on «Отмена», confirm «Отключить»), the same from
a project's row on the team's page and from a team's row on the project's page.
The count of what the team owns *in that project* is read when it opens.

```
Команда «MARS» владеет рабочими элементами проекта «Alpha»: 7. Выберите, кому их передать.
Передать рабочие элементы *  [ Выберите команду или «Без команды»  ⌄]
Рабочие элементы встанут в конец бэклогов команды «VENUS».            <- after a choice
Рабочие элементы не удаляются. Если «MARS» — команда проекта по умолчанию, настройка
сбросится. Повторное подключение команды рабочие элементы не вернёт.   <- caption
                                                  Отмена  [Отключить]
```

- Field: `InlineSelectField`, `frame="always"`, required, **nothing preselected**
  (OQ-5 A): «Без команды» first, then the project's other linked teams by name; never
  the unlinked team or an unlinked one (the project's team list as read). The choice
  line: for a team «Рабочие элементы встанут в конец бэклогов команды «VENUS».» ◆;
  for «Без команды» «Рабочие элементы останутся в бэклоге и на доске проекта, но не
  попадут в бэклог ни одной команды.» ◆.
- With no other team linked the field still stands with «Без команды» alone and no
  preselection (D-2).
- Owns none: no field, «Отключить команду «MARS» от проекта «Alpha»?» and the caption
  «Если «MARS» — команда проекта по умолчанию, настройка сбросится.» ◆; «Отключить»
  is available at once, and the request names «Без команды».

| State | The user sees |
|---|---|
| Counting | `FieldsSkeleton count={2}`; «Отключить» unavailable |
| Count failed | `InlineNotice` with the server's text and «Повторить»; unavailable |
| Owns items, no choice | text and field; «Отключить» unavailable (aria-disabled, focusable) |
| Busy | field disabled, both buttons inert, Escape and a click outside ignored |
| Done | closes; notice «Команда отключена от проекта.»; the page is read again (the row goes; the mark may change); the shell's team lists are read again (the switcher's fallback notice alone when it falls back) |
| 409 | «Команда не участвует в проекте.» under the field, the choice cleared, the project's teams read again |
| 404 team | «Команда не найдена.» under the field, the same |
| 404 project | the alert «Проект не найден.»; the page read again shows its frame |
| 400 | «Выберите команду или «Без команды»» under the field |
| 403 | the alert «Недостаточно прав»; the codes are read again |

## 7. The project's «Команды» (US-2, US-3, US-4, US-6)

```
Команды       [Бэклог проекта] [Доска проекта] [☆ Команда по умолчанию] [+ Добавить команду]
Команды организации, работающие над проектом. Создать, переименовать или удалить
команду можно в разделе «Организация» → «Команды».
| Команда                                                               |
| JUPITER                                                            ⋮  |
| MARS   (По умолчанию)                                              ⋮  |
```

- The page is today's (legacy table, `RowActionsMenu`, the `Tag` mark from
  `DEFAULT_MARK_LABEL`), rows by name. The name links to the team's page in the
  organization's settings. **The row menu holds «Отключить от проекта»** (destructive)
  alone: «Переименовать» and «Удалить команду» are gone (BR-OL-5). The intro line is
  `Typography body2` in `text.secondary`; «Организация» → «Команды» is a `Link` to § 3.
- Header: primary «Добавить команду» (`AddIcon`) for a user with both link codes;
  «Команда по умолчанию» (`StarBorderIcon`, `project.write`) while at least one team
  is linked. «Бэклог проекта» (`FormatListNumberedIcon`) and «Доска проекта»
  (`ViewWeekOutlinedIcon`) are links (`to`, level «Истории») **shown only while no
  team is linked**, to every role (OQ-2 A, retired by feat-043); they overflow into
  «Ещё действия» on a narrow bar.
- «Команда по умолчанию» is mod-017 § 7.1 unchanged (its options are the linked
  teams); «Добавить команду» is § 6.1; «Отключить от проекта» is § 6.2.

| State | The user sees |
|---|---|
| Loading | `RegistrySkeleton` `['Команда', '']` / `['Команда']` |
| Error | `ErrorBanner` with «Повторить» |
| Empty | `StateFrame` `InboxOutlinedIcon`, «В проекте пока нет команд.»; hint «Добавьте команду организации — она сможет владеть рабочими элементами проекта. Без команд проект тоже работает: у его рабочих элементов будет «Без команды».» ◆ with the action «Добавить команду» for the holder, «Команды подключает администратор организации. Проект работает и без команд.» ◆ otherwise |
| Project gone or deactivated | `NotFound` «Проект не найден» |

## 8. The project: description and creation (US-2)

- **«Общие»:** facts «Название», «Описание» (as § 4), «Ключ», «Типы рабочих
  элементов». Commands «Переименовать», «Изменить описание» ◆ (`project.write`),
  «Деактивировать». The description dialog is § 5.2. A deactivated project keeps its
  description.
- **«Новый проект»:** «Название» (helper now «От 1 до 100 символов. Так же будет
  названа корневая область проекта.»), «Ключ» (unchanged), then `InlineTextArea`
  «Описание» (hint «Необязательно. Не более 2000 символов.») and
  `InlineMultiPickerField` «Команды» over the active teams (hint «Необязательно.
  Команды можно добавить позже на странице «Команды» проекта.» ◆), drawn only with
  both link codes. A project is created with no team when it is left empty. Refusals:
  `team.not_found` «Команда не найдена.» → «Команды»; `description_length` →
  «Описание»; the rest as today. Notice «Проект «Beta» создан.» as today.
- **«Переименование проекта»:** the text becomes «Корневая область проекта получит то
  же название. Ключ, описание и команды проекта не изменятся.» ◆
- **«Деактивировать проект?»:** «Проект «Alpha» (ALP) пропадёт из списков и
  переключателя, а его страницы и области перестанут открываться. Ничего не будет
  удалено, связи с командами сохранятся: проект можно восстановить в списке
  деактивированных проектов организации.» ◆

## 9. Retired surfaces (US-1, US-2)

- **`…/projects/:p/settings/teams/:t`:** no route; the catch-all answers «Страница не
  найдена» with no redirect, for any user (ADR-0024 § 4).
- **On the project's «Команды»:** «Переименовать», «Удалить команду», the dialog
  «Новая команда» with its notice «…добавлена.», the last-team rule and its note
  «Единственную команду проекта удалить нельзя.».
- **Gone in code:** `TEAM_ROUTE`, the project-nested `teamPath`, the team hooks keyed
  by project, `LAST_TEAM_NOTE`, the project-scoped `DeleteTeamDialog`.

## 10. Roles

| Control | «Администратор» | «Участник» | «Читатель» | Not a member |
|---|---|---|---|---|
| Lists, pages, marks, project lists, descriptions | sees | sees | sees | «Организация не найдена» |
| «Новая команда» (`team.create`) | offered | - | - | - |
| «Переименовать», «Изменить описание» of a team; «Добавить в команду», «Исключить из команды» (`team.write`) | offered | - | - | - |
| «Удалить команду» (`team.delete`) | offered | - | - | - |
| «Добавить команду», «Добавить проект», «Отключить от проекта» (both `team.link` and `project.link`) | offered | - | - | - |
| «Команда по умолчанию»; «Изменить описание» of a project (`project.write`) | offered | - | - | - |
| «Бэклог проекта», «Доска проекта» (team-less project) | links | links | links | - |

A missing code hides the control; a direct write answers 403 «Недостаточно прав»
(error notice, 6 s) and the codes are read again. The «Проекты» and «Команды» fields
of the two creation forms need both link codes.

## 11. Narrow widths (down to 640 CSS px, ADR-0024)

- **Tables** scroll inside their card, the page never does; names are cut by
  `TruncatedText`, the actions column stays. Page heads overflow into «Ещё действия».
- **Team page:** `FactsPanel` puts the label above the value (today's); sections
  stack; a section head with its button wraps.
- **Dialogs** are full width less their margins; fields, captions and tags wrap by
  rows; the picker's list is no wider than the window; the delete list scrolls inside.
- Below 640 px nothing is claimed.

## 12. Accessible names and focus

| Control | Role | Name |
|---|---|---|
| the tables | table | «Команды организации» / «Проекты команды» / «Команды» |
| row menu | button | «Ещё действия: MARS» (the row's name) |
| pickers | combobox, multiselectable listbox | «Команды» / «Проекты» / «Участники» (label only) |
| a tag's remove | button | «Убрать «Alpha»» ◆ |
| destination | combobox | «Передать рабочие элементы» |
| the delete list | list | «Рабочие элементы по проектам» ◆ |

Focus: a form dialog opens on its first field, the delete and unlink dialogs on
«Отмена»; a refusal puts focus on the field it names; after a row's dialog closes,
focus goes to that row's menu, or the next row's, or the primary button when the row
is gone (`useRowFocus`); a created team returns it to «Новая команда». Escape closes
the picker's list first, then the dialog.

## 13. For the developer

- **Contract needs (backend):** the organization's teams with `description` and the
  active `projects: [{id, name}]`; a team's `description` and active projects
  `{id, name, key}`; a project's linked teams with `defaultMark`, `isMember` and its
  `description`; the unlink's count (team, project) and the deletion's counts, one
  list `{projectId, name, active, count}` over every linked project, deactivated
  too; the user search and member routes at the team's address; link and unlink
  routes; `description` with `teamIds` / `projectIds` / `memberIds` on both creations.
- **Refusals:** `RefusalField` gains `description`, `destination`, `teams`, `projects`
  and `members`; `placeRefusal` maps `description_length`, `destination_required`,
  `team.name_taken` (team), `project.not_found` (projects), `team.not_found` (teams
  or destination), `team.not_in_project` (destination), `member_not_in_organization`.
- **Routes and keys:** `ORG_TEAMS_ROUTE`, `ORG_TEAM_ROUTE`, `organizationTeamsPath`,
  `organizationTeamPath`; `TEAMS_ROUTE` stays. A link, unlink or deletion refreshes
  the organization's teams, the team's projects, the project's teams, the work item
  pickers and the shell's team lists.
- **Kit guard:** new files use kit parts only; `TeamsPage`, `TeamPage` (moved),
  `TeamMembersSection`, `FactsPanel` keep their allow-list entries, lowered where a
  site goes; `DeleteTeamDialog` is rewritten. The team page shows kit sections beside
  the legacy members section until tech-017 (D-5).
- **Tests:** the fate table names what retires; new cases are the states of §§ 3-8,
  the dialog refusals, the picker part and the roles of § 10.

## 14. Decisions

For the human at gate-2; the design went on with A.

- [ ] **D-1 The searchable multiselect.** (A, recommended) A new kit field
  `InlineMultiPickerField` (§ 2). (B) The kit's `InlineMultiSelectField`: no new part,
  but US-3's "types part of the name" and a user search cannot be built on it.
- [ ] **D-2 The unlink with «Без команды» as the only destination.** (A, recommended)
  The field stands with one option and nothing preselected: one code path, and the
  choice that moves every item stays deliberate (OQ-5 A). (B) No field, a sentence
  «Рабочие элементы станут «Без команды».» and «Отключить» available at once.
- [ ] **D-3 Where a project row on a team's page leads.** (A, recommended) The
  project's «Команды» (the spec's default, R-7): the other end of the link. (B) The
  project's «Общие»: its name, key and description; the link is then one more click.
- [ ] **D-4 A prototype.** (A, recommended) None: every screen is made of existing
  patterns and the new field is built in the showcase first. (B) A clickable
  prototype of the unlink dialog and the picker (stage 3 calls it mandatory for L).
- [ ] **D-5 Mixed looks on the team's page.** (A, recommended) Kit sections beside the
  legacy facts and members until tech-017 moves them. (B) Move `FactsPanel` and
  `TeamMembersSection` to the kit here: a uniform page, a bigger line.

## 15. Stories to screens

US-1 §§ 3, 4, 5.1-5.2, 9 · US-2 §§ 7, 8 · US-3 §§ 4, 6.1, 7 · US-4 §§ 6.2, 7 · US-5
§ 5.3 · US-6 §§ 4, 7 · US-7 nothing of its own (renamed teams and «Нет проектов» show in
§ 3) · US-8 § 10 (the tenant answers are the backend's).
