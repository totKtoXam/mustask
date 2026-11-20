# Design: Cascading context selectors, one organization (feat-063)

Stage 3 artifact. Rules (BR-n) and scenarios are in `spec.md` and `stories/`; this file is layout, MUI components, states, keyboard and the final Russian copy. It is a **narrowing of the approved doc-016 design § 3** ([design.md](../doc-016-teams-under-departments/design.md), gate-2 passed there): same button, same panel, same offers, same search; this file records only what the one-organization cut removes or adds. Where it is silent, doc-016 § 3.x text stands. ◆ = copy or behaviour new in this design.

- Prototype: `prototype/index.html` (one file, no network, opens from disk). Users: Ann (administrator of «Ромашка»), Olga (member of VENUS and ORION, two organizations, sees only VENUS, MARS, A and «Логистика» in «Ромашка»), Pavel (sees nothing), Nina (no organization). The grey bar lists the repair scenarios and the loading/error switch. § 9 lists what to click.
- Out (stays as in the spec): «Создать …» lines and "«все» is first" guarantee (feat-052); organization-«все» columns with headings, cross-organization search, a choice that leaves «все» for another organization's object (feat-064).

## 1. Reuse: no new component

| Wanted | Verdict |
|---|---|
| Path button | MUI `Button` (variant text, no border) with `Tooltip`; type icons `CorporateFareOutlined`, `ApartmentOutlined`, `GroupsOutlined`, `FolderOutlined`; colour dot = `Avatar` 8-10 px or a `Box` circle (as the tree, doc-016 § 7) |
| Panel | `Popover` anchored to the button, `role="dialog"`; search `TextField size="small"`; columns `List` (`role="listbox"`, `ListItemButton` rows, `ListSubheader` captions) |
| Lists in the columns | existing `SearchMenu` rows are reused for the row look; `selectorFit.ts` is rewritten to the tree invariant (doc-016 § 1) |
| Loading, error, empty | `FieldsSkeleton count={4}`, `InlineNotice` (error, with «Повторить»), `InlineEmpty` / muted `ListItem` text |
| Context notice | `InlineNotice` (`role="status"`, closable), replacing `useNotice` for the three context notices (spec BR-12) |
| No-organization state | the existing link «Нет организаций», unchanged |

The `Avatar`/dot and the stripe are the doc-016 pattern; no new kit part.

## 2. Screen map

| Screen | Where | Stories |
|---|---|---|
| Context path button | header, right after the logo, every page | US-1 |
| Panel: search field + four columns, or grouped results | opens from the button | US-1, US-2, US-3 |
| Context notice | under the header (panel closed) or in the panel under the search field (panel open) ◆ | US-2 |

No page changes. The pages under the header show the selection through the existing `Tag` «сужено: …» and the narrowed lists «Проекты», «Команды» (BR-4, doc-016 § 3.4).

## 3. The path button (US-1)

```
+ MUSTASK | [🏢 Ромашка › ▦ ● Логистика › 👥 ● MARS › 📁 все ⌄] ................ (АК) +
```

- Reads «Организация › Департамент › Команда › Проект»; each segment has its type icon; department, team, project have the colour dot when chosen; «все» is muted with a muted icon; the last chosen level has medium weight; the `›` separators are `aria-hidden`. Organization «все»: «Все организации › все › все › все» (BR-10).
- A long path ellipsises segment by segment (max 130 px each); `Tooltip` and `title` carry the full path.
- Accessible name: «Контекст: Ромашка, Логистика, MARS, все». `aria-haspopup="dialog"`, `aria-expanded` follows the panel. Reachable by Tab.
- A user with no organization: the button is replaced by the link «Нет организаций» and no panel opens (unchanged).
- The four feat-043 selector triggers are removed.

## 4. The panel (US-1, US-2)

```
+-------------------------------------------------------------------------+
| [ Найти департамент, команду или проект                              ]  |
| (notice here when one arrives while the panel is open) ◆                |
| Организация   | Департамент  | Команда        | Проект                   |
| Все организ.  | все          | все            | все                      |
| ▌Ромашка      | ▌Логистика   | Мои команды    | ▌A                       |
| ▌Другая       | ▌Маркетинг   | ▌VENUS         | ▌B                       |
|               | ▌Финансы     | Другие команды | ▌G                       |
|               |              | ▌MARS          | ...                      |
+-------------------------------------------------------------------------+
```

- `Popover`, width `min(880px, 100vw − 32px)`; column minimums 180 / 240 / 220 / 220 px; below that the **panel** scrolls sideways, never the page (640 CSS px kept).
- Each column: caption (`ListSubheader`), «все» first, then the offers (doc-016 § 3.2 restricted to the chosen organization), sorted by name; the team column splits «Мои команды» / «Другие команды» (feat-043). Organization column: «Все организации», then the user's organizations.
- A department, team, project row has the 4 px colour stripe at its left edge and no type icon; «все», organizations and «Все организации» carry an empty spacer of the same width. A name wraps to 2 lines, then an ellipsis and a `Tooltip` with the full name. The chosen row is highlighted (`selected`, medium weight), its stripe stays visible.
- Never in a column: «Остальные», a project node, a sub-area, a deactivated or deleted object, anything the user does not see (BR-TD-12..-17).
- A column choice applies at once (doc-016 § 3.3 and spec BR-3 give the result and the notice), moves the address (BR-9), keeps the panel open, the columns to the right narrow and focus returns to the chosen row.
- Esc and a click outside close the panel without changing the selection; focus returns to the button. A search result closes the panel and moves focus to the page heading (BR-9).

### 4.1 Organization «все» (BR-10)

| Part | Shows |
|---|---|
| Button | «Все организации › все › все › все» |
| Departments, teams, projects | only «все» (chosen) and the muted line «Выберите организацию» |
| Search field | disabled, helper under it «Выберите организацию» |
| Organization column | choosing «Ромашка» follows US-2: the lower three read what was remembered there, repaired |

## 5. States (US-1, US-3)

| State | Panel shows |
|---|---|
| Normal | the field and the four columns |
| Loading | `FieldsSkeleton count={4}` under the field (`role="status"`, `aria-label="Загрузка"`); the field stays enabled |
| Read failed | `InlineNotice` error «Не удалось загрузить список. Попробуйте ещё раз.» with the button «Повторить»; retry re-reads and redraws |
| A column has nothing | «все» plus the muted line (not selectable): «Нет доступных организаций» / «Нет доступных департаментов» / «Нет доступных команд» / «Нет доступных проектов» |
| Chosen department holds nothing | team column «все» + «Нет доступных команд»; project column «все» + «Нет доступных проектов» (not doc-016's «Команд нет»: the narrowed spec scenario wants these lines) |
| Organization «все» | § 4.1 |
| No organization | no panel (§ 3) |
| Permissions | no role differences: offers and search are sight-scoped; an invisible object is in no column, result or notice. Nothing is greyed to hint at a hidden object |

The pages below the header, narrowed by the selection, show `Tag` «сужено: департамент Логистика, команда MARS», and when empty «Здесь нет проектов, доступных вам.» / «Здесь нет команд, доступных вам.» (doc-016 § 3.4).

## 6. Panel search (US-3)

- Field label and placeholder: «Найти департамент, команду или проект». Typing replaces the columns with grouped results; clearing the field brings the columns back.
- Matches from the first character, case-insensitive, over the name (a project also over its key: «fin» finds C by FIN). Searches **only the chosen organization** (BR-7).
- Groups «Департаменты», «Команды», «Проекты» in that order, an empty group absent, at most 5 rows each, by name. Row = type icon, colour dot, name, a project's key in grey, grey parent path on the right: team and project show the department («Финансы»); a department shows none (the organization does not lead the path, one organization).
- No match: «Ничего не найдено». Loading: skeleton rows. Failure: «Не удалось выполнить поиск. Попробуйте ещё раз.» with «Повторить».
- First row active on typing; ↑ ↓ move and wrap; Enter chooses through the US-2 rules, closes the panel, focus to the page heading. The active row is `aria-selected`; the field keeps focus and carries `aria-activedescendant` ◆ (the developer's choice if the Popover list makes it heavy; the visible behaviour is what is specified).
- Organization «все»: field disabled (§ 4.1).

## 7. Notices (US-2, BR-12)

One notice at a time, `InlineNotice` (info, `role="status"`, closable with an icon button «Закрыть уведомление» ×), never blocking.

| Text | When |
|---|---|
| «Выбор сброшен: команда MARS, проект A.» | a choice, a search result or a link resets levels (kind in lower case, joined by «, », team before project: «команда …, проект …»); also a live repair (opening the panel re-reads the offers) |
| «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика / VENUS.» | the remembered selection was repaired when the shell first read the offers, or when another organization is chosen and its remembered levels were repaired |
| «Ранее выбранный контекст недоступен.» | no organization left (as doc-016 § 3.5, «Главная» opens) |

Dismissal ◆ (the spec left it to this stage): the notice stays until the user closes it, a later notice replaces it, **or the next choice that produces no notice clears it**, because it describes the result of that earlier action. Never timed. Position ◆: with the panel closed it sits in a full-width strip under the header and pushes the page down; **while the panel is open it is shown inside the panel, under the search field**, because the panel covers the strip, and only one live region exists at a time. Closing the panel moves it to the strip. After closing the notice in the panel, focus returns to the search field.

## 8. Keyboard and focus (NFR)

| Where | Keys |
|---|---|
| Button | Tab reaches it; Enter / Space opens the panel; focus goes to the search field (enabled), or to the chosen organization row when the field is disabled |
| Search field | ↓ enters the first column's first row (the organization column); with a query ↓ ↑ move the active result |
| Inside a column | ↑ ↓ rows (↑ on the first row returns to the field), ← → neighbouring column at the same row index (clamped), Enter chooses; rows have `role="option"`, columns `role="listbox"` with `aria-label` = the caption |
| After a column choice | the panel stays open, focus is on the chosen row of the same column |
| Esc, click outside | closes the panel, focus to the button; selection unchanged |
| Search result | closes the panel, focus to the page heading |

Colour is never the only carrier: the name is always text; stripes and dots are decorative (`aria-hidden`).

## 9. Prototype scenarios (every one exists in `prototype/index.html`)

1. Cascade as Ann: choose MARS (parents fill in, panel open, focus on the row, no notice); B, then VENUS (notice «Выбор сброшен: проект B.»); «Финансы» (notice «… команда VENUS.»); a project, then «все» in the department column (notice names the project); team «все» with a project chosen resets nothing.
2. Scenario button «Открыть A по ссылке»: «Выбор сброшен: команда JUPITER.».
3. Empty department: choose «Маркетинг» as Ann (team, project columns show the empty lines). Pavel: all empty lines. Nina: «Нет организаций».
4. Olga: sees only VENUS, MARS, A, «Логистика»; «Мои команды» / «Другие команды»; typing «orion» gives «Ничего не найдено»; «Другая» in the organization column opens that organization.
5. Search: «jup» (JUPITER, path «Финансы»), «fin» (project C by key), «фин» (department), ↓ Enter, Esc.
6. Repair: «Запомнено «Финансы / VENUS»» and «… A скрыт» (remembered notices, no panel); «Перенос VENUS», then open the panel (live repair).
7. Organization «все»: scenario button; the lower columns say «Выберите организацию», the field is disabled; choose «Ромашка» to leave it.
8. Loading and error: the «Данные» switch, then open the panel; «Повторить».
9. Narrow the window to 640 px: the panel scrolls sideways, the page does not.

## 10. Decisions beyond the spec (for gate-2)

1. Notice dismissal: closable, replaced by a later notice, cleared by the next notice-less choice, never timed (§ 7).
2. While the panel is open the notice is shown inside it (§ 7).
3. The "chosen department holds nothing" lines are «Нет доступных команд» / «Нет доступных проектов» (§ 5), following the spec scenario over doc-016's «Команд нет».
4. In organization «все» a project or team cannot be chosen from the columns (feat-064 lifts this).

## 11. Open questions

None blocking. `aria-activedescendant` on the search field (§ 6) is advisory; the developer may use roving focus instead if the behaviour in § 8 is kept.
