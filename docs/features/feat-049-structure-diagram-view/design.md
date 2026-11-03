# Design: «Структура» diagram view (feat-049)

Stage 3 of an M task, frontend only, inside `features/organizations`. Closes US-1 (switch), US-2 (hierarchy with teams, sections, levels, layouts, toolbar) and US-3 (relations, branch emphasis, display modes). Prototype: `prototype/index.html` (self-contained; scenarios switch on the page). Interface copy is Russian and final; prose is English. Baseline: feat-043 design §§ 5.2 and 9, ADR-0031 (only a department takes a team link), ADR-0032, `docs/conventions/ui-kit.md`. Reworks 1-3 (gate-1+2 remarks) are folded into the body; § 12 indexes them and lists the spec deltas.

## 1. Reuse first, and what the kit lacks

| Need | Taken from | Note |
|---|---|---|
| Page head, «Создать департамент» | `PageHeader` (`title`, `primaryAction`) | `commands` stays empty in the diagram |
| Tree view | `TreeRegistry`, `ListFilter` | untouched |
| Loading, tree failed | `RegistrySkeleton` (columns «Название», «Тип»), `ErrorBanner` «Повторить» | the page's own, the same in both views |
| Offers failed/loading; summary; notes | `InlineNotice` (`warning`, `info`; `action`, `ref`) | § 6 |
| Empty org, 404 | `StateFrame`, `NotFound` (`notFoundLevel`) | the tree's |
| Type text + icon, tag | `AreaTypeMark`, `Tag` «деактивирован» | a team card uses the team icon of the teams registry |
| Clipped names | `TruncatedText` | full name is the accessible name |
| Chevrons, section toggles | `IconAction` (`tooltip={false}`, `tabIndex={-1}`) | like the tree's chevron |
| Toolbar icon buttons (R3-2) | `IconAction` with `tooltip` = its `aria-label`; the three menus via `MenuButton` `selection="radio"` whose trigger is an `IconAction` | icons § 4.9 |
| Level chips (R3-3) | `ActionButton`-style round chip, `aria-pressed` | outline from the palette, § 4.4 |
| Search, «Развернуть всё» | kit search field, `ActionButton` | |
| Announcements | `LiveRegion` (`serial`) | |
| Links | `useObjectLink` + `rowLink` of `AreasPage` | extract `rowLink` so both views share it |
| Colours | `theme.palette` roles only | `colourLiterals.test.ts`: no hex in features, SVG strokes included |

**The kit has no same-address view switch.** `PageHeader views` are addresses (BR-SD-1 forbids a new one), `ToggleButtonGroup` is forbidden in features (KIT-2), and the `BoxedTabs` docblock says "Not for page views". The design takes **`BoxedTabs` + `BoxedTabPanel`** (values `tree`/`diagram`, name «Вид структуры», arrow keys, Enter/Space confirm). It passes `kitGuard`, needs no new part; the docblock names an intended use, not a guard, so this is a recorded deviation (the developer adds a sentence to the docblock).

## 2. Screen map

One screen, one address: «Структура», two views; navigation and routes unchanged; links are the tree's (BR-SD-6).

## 3. The page, the switch and what is remembered

Layout, top to bottom: h1 and «Создать департамент»; the switch and the legend; the diagram toolbar (search, «Развернуть всё», two icon menus, counters); the offers notice or the summary of the chosen node; the frame (scrolls both ways, max-height min(70vh,560px)) with the levels controls at its top; the keyboard hint.

- The tree view keeps `ListFilter` and «Развернуть всё» as today. The diagram has its own toolbar: search, «Развернуть всё»/«Свернуть всё» (its own expansion), two icon menus, counters. «Создать департамент» stays in both views for `area.create` (OQ-3 A).
- **Page state** (in `AreasPage`, kept across a view switch for the visit, never stored): tree filter, expansion and selection; the diagram's expansion, search text, section folds and level choice. Dropped on a switch: the chosen node.
- **Stored in `localStorage`** per user and organization, each under its own key, never inside `mustask.context.v2`; a value outside the allowed set or a refused storage means the default, no error; written on a user's choice only: `mustask.structure.view.v1.<sub>.<org>` (`tree` default / `diagram`), `...mode.v1...` (`dim` default / `only` / `hl`), `...layout.v1...` (`h` default / `v`). One hook `useStructureSettings(organizationId)`.
- Not stored, on purpose: the level choice, the section folds, the search text: a stored filter reopens the page missing nodes and the user cannot tell why.

## 4. The diagram

### 4.1 Sections and what stands in them (R3-1, R3-4)

The model: **a team is a child of every department it is linked to** (`teams[].departmentIds`; ADR-0031). The department stays optional (confirmed: no model change). Two sections, each header a disclosure (`treeitem`, `aria-expanded`, chevron, title, count of cards; click, `Enter`/`Space`, `←`/`→`):

1. **«Без департамента»** on **top**. Holds, in this order: top-level projects (with their sub-areas) as in the tree, then the teams with **no department link** (whether or not they work on projects), choosable. Shown only when something is there. It replaces the old group «Без проектов и департаментов».
2. **«Департаменты, проекты и области»**: departments, and under each department **its projects first (with sub-areas), then its teams**, both in the read's order. The department's own child order is therefore: projects, teams.

A team linked to several departments stands **under each of them, as a copy**, every copy marked **«ещё в: <остальные департаменты>»**. Copies are one team: choosing any emphasises all (§ 4.7), counters count the team once (§ 4.8), the keyboard moves through copies as through separate items (each has its own place in the order). Folds are page state; choosing unfolds a folded section and the branches that hide emphasised nodes, copies' departments included (BR-SD-11).

### 4.2 Horizontal layout (default «Горизонтально»)

```
 ▾ Без департамента · 4
   1 Проект Мобильное приложение MOB         2 команды ..........,
     2 Область Платежи                                            :
   1 Команда ATLAS            0 проектов                          :  gutter with
   1 Команда ORION            1 проект ...........................'  relation arcs
 ▾ Департаменты, проекты и области · 15
   1 Департамент Логистика                    3 команды
   | 2 Проект Складской портал WH             2 команды
   | 2 Проект Транспорт TR   2 Команда JUPITER ещё в: Финансы  ·-·-· copy join
   | 2 Команда MARS · 2 Команда VENUS
   1 Департамент Финансы ... 2 Команда JUPITER ещё в: Логистика
```

- **One lane:** one row per visible node, pre-order of the read (never re-sorted), a full-width card indented 22 px per true level (a team under a department is level 2, a team in «Без департамента» level 1), all ending at one right edge; level badge in each node; faint dotted level guides; nesting joins are thin elbows in the indent gutter.
- **Relation lines run inside the tree** (R3-1): from a team card to its projects' cards, dashed arcs in a 150 px gutter at the right edge of the cards. A pair uses the team copy under the project's own department when it exists, else the first copy. There is no teams lane any more.

### 4.3 Vertical layout («Вертикально»)

- **Rows are levels:** departments and top-level projects on the top row, projects and teams under a department on the second, sub-areas below. A **tidy top-down tree**: leaves take consecutive slots (156 px card + 12 px gap), a parent stands over the middle of its visible children (projects, then teams, left to right). The level chips repeat in the left gutter of **each section band**, one per row; they toggle the same state.
- **«Без департамента»** is the same band, on top, over the full frame width: top-level projects and loose teams on row 1.
- **Relation lines** are curves between the cards: a team and a project on one row dip below the cards, on different rows run vertical-tangent between the lower edge of the upper card and the upper edge of the lower. Copy joins dip the same way.
- **Nodes** 156 × 58 px: line 1 chevron, badge, type; line 2 name (link); line 3 key, «деактивирован», «ещё в: …», count.
- **Large organization:** a vertical tree is as wide as its leaf count; above **120 visible cards** a note «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» stands above the frame (`InlineNotice` info; nothing is blocked).
- **One small pure function** `layoutDiagram(model, expansion, options{orientation, levels, sections, related})`: fixes the visible set and nearest visible ancestors (shared), then assigns rows (horizontal) or runs the tidy placement (vertical), then routes lines by the nodes' positions. About 200 lines, unit-tested without a DOM; approach A stands.

### 4.4 Levels (R2-1) and their colours (R3-3)

- **Chips = ruler.** One round toggle chip per level of the whole tree (1 … N, so chips never jump): horizontal layout at the column's left edge above the first row; vertical, in the gutter of each section band at its row. Click toggles (`aria-pressed`, «Уровень 2»); multi-select.
- **Mode menu** icon button (§ 4.9), default **«Только выбранные уровни»**: shown are the pressed levels. **«Уровень и вложенные»**: the topmost pressed level and every deeper one (deeper chips show a light tint). No chip pressed means all levels. **«Все уровни»** icon button, disabled while nothing is pressed, unpresses every chip.
- **Rows of a hidden level drop.** A node whose parent is hidden attaches to its nearest visible ancestor by a **dotted** join; with none it stands at its true column or row. A team follows the same rule through its department. In the vertical layout a level with no shown node is a 30 px strip.
- **Relation lines** go to the nearest visible ancestor (dotted, «связь через скрытый узел»); with none the line is not drawn. Counts and summaries speak of data, not of what is drawn.
- **Outline colour per level.** Every card has a 1.5 px outline in its level colour, equal to its level chip (outlined; filled when pressed) and the badge fill. The number in the badge and the chip stays, so the level is never colour alone. All colours are ≥ 3:1 against white (WCAG 1.4.11) in the light theme; the badge and pressed-chip text on its fill is white ≥ 4.5:1 for every level (BR-SD-12).

| Level | Palette role (light) | Contrast on white |
|---|---|---|
| 1 | `primary.dark` | 7.6:1 |
| 2 | `success.main` | 5.1:1 |
| 3 | deep orange 900 (#BF360C) role | 5.6:1 |
| 4 | `secondary.main`/purple 700 role | 8:1 |
| 5 and deeper | `text.secondary` | 6:1 |

The developer maps the four roles that exist in the theme and adds one `levelColour(level)` helper in `lib/diagramLevels.ts`; a purple that is not in the theme is a stage-4 question to the orchestrator, not an inline literal. A chosen card keeps its level outline and gets an outer 2 px primary ring and a tint; a search hit gets an amber ring.
- **Keyboard/aria:** chips are real buttons in a `group` «Уровни вложенности», before the tree in the Tab order; announcements «Показаны уровни: 2, 3» (+ «и более глубокие») / «Показаны все уровни»; focus stays on the chip. Survives a view switch, dropped on organization change, **not stored**.

### 4.5 Node anatomy

| Node | Shows | Choosable | Opens |
|---|---|---|---|
| Department | chevron, badge, «Департамент», name (link), «N команд» | yes | department page |
| Active project | chevron if children, badge, «Проект», name, key, «N команд» | yes | project «Общие» |
| Sub-area | chevron if children, badge, «Область», name | no | its page |
| Deactivated project / its sub-areas | type, plain name, `Tag` «деактивирован»; no key, no count | no | nothing |
| Team (each copy) | badge, «Команда», name (link), «ещё в: …» if linked elsewhere, «N проектов» | yes | team page |
| Section header | chevron, title, count | no | folds/unfolds |

### 4.6 Lines (R3-1: a new line style)

| Line | Style |
|---|---|
| Nesting | 1 px solid grey elbow; **dotted** across hidden levels |
| Team works on a project | 1.5 px **dashed** primary arc, dot at the project end |
| **Same team in another department (new)** | 1.5 px **dash-dot** `text.secondary`, no dot; joins consecutive copies of one team |
| Relation through a hidden node | dotted primary, ends at the nearest visible ancestor |
| Emphasised line | 3 px; the others fade or turn grey (see modes) |

The legend (top right of the toolbar row) shows all four: «вложенность», «команда работает над проектом», «та же команда в другом департаменте», «связь через скрытый узел». Constants (named in `lib/diagramLayout.ts`): horizontal `ROW 40`, `INDENT 22`, `HIERARCHY_WIDTH 480`, `GUTTER 150`; vertical `NODE 156×58`, `SLOT_GAP 12`, `BAND 88`; `RELATION_LINE_LIMIT 150`, `VERTICAL_NOTE_LIMIT 120`. Rendering: absolutely positioned HTML nodes (`Box`), one `<svg aria-hidden>` line layer behind.

### 4.7 Choosing, branch emphasis and the display mode (R3-5)

Click on the body or Space chooses, again/`Esc`/«Сбросить» clears; the name opens; a chevron toggles and chooses nothing. **Choosing a parent emphasises its whole branch.**

| Chosen | Emphasised (besides itself) |
|---|---|
| Department | every project and sub-area under it, every team under it (all their other copies too), all relations of that branch, and the far ends of those relations |
| Project with sub-areas | its sub-areas (any depth), the teams working on it (all copies), the relations |
| Sub-area | not choosable |
| Team (any copy) | all its copies, its projects with their lines, its departments |

The summary (`InlineNotice` info with «Сбросить», `role="status"`) says: department «Ветка департамента: проектов N, областей N, команд N. Связано вне ветки: …»; project «Ветка проекта: областей N. Команды проекта: …»; team «Проекты команды: …. Департаменты команды: …». **«Показ при выборе»** (icon menu) decides what choosing does to the rest, and branch emphasis holds in all three:

| Mode | Effect | Layout rebuilt |
|---|---|---|
| **«Затемнение»** (default) | the branch, the related and their lines emphasised, everything else at 50 % | no |
| **«Только связанные»** | kept: the branch, what it relates to, their ancestors, and the departments of related teams (a copy shows only under a kept department); all else removed; one more line «Показаны только ветка выбранного узла и связанные с ней.» | yes, on choose and clear |
| **«Подсветка»** | nothing dimmed or hidden; the branch, the related and their lines get a stronger accent frame, other lines grey | no |

Choosing also unfolds branches and sections hiding emphasised cards.

### 4.8 Large organization (OQ-6) and convenience

- **Lines:** up to **150** (project-team pairs plus copy joins, independent of expansion) all drawn; above, none until a node is chosen, then only the lines of its branch, with the note «Линии связей показаны для выбранного узла». The prototype's large organization has 189.
- No virtualization planned (~1 000 elements, at most ~150 paths); if the 2 s measure fails at stage 6, virtualizing rows is a layout-only fix.
- «Найти узел» marks matches, Enter jumps to the next (unfolds section and branches, focuses, scrolls; a team jumps to its first copy), case and «ё/е» ignored, filters nothing (BR-SD-8). Counters «Департаментов · Проектов · Областей · Команд» count **each team once** whatever its copies. **Follow-ups:** hover preview, zoom/minimap, drag and drop (feat-050), people (feat-051); none designed here.

### 4.9 Toolbar icons (R3-2)

Four controls are icon buttons (`IconAction`, 32 px, `aria-label` = tooltip text, `aria-haspopup="menu"` for the three menus). The text lives in the label and tooltip, so the screen reader reads «Показ при выборе: Затемнение»; the menu rows keep their text and hints.

| Control | MUI icon (Outlined) | Label / tooltip |
|---|---|---|
| Display mode | `VisibilityOutlined` | «Показ при выборе: <значение>» |
| Layout | `AccountTreeOutlined` | «Раскладка: <значение>» |
| Level mode | `LayersOutlined` | «Режим уровней: <значение>» |
| All levels | `LayersClearOutlined` | «Все уровни» |

Search and «Развернуть всё»/«Свернуть всё» stay text (the words are the control). The two menus sit in the toolbar row, the level pair at the top of the frame, before the chips.

## 5. Keyboard and screen reader (BR-SD-10, OQ-5 A)

- **One Tab stop for the tree**: `role="tree"` «Диаграмма структуры», roving `tabIndex`; names, chevrons, links inside `tabIndex -1`. The toolbar controls and the levels group are separate stops before it.
- **Order:** section header, its cards in pre-order **in both layouts** (department, its projects with sub-areas, then its teams); team copies are separate items.
- **`aria-level`:** section headers 1; a card is true level + 1 (a team under a department 3, a loose team 2).
- Keys: `↑`/`↓`, `Home`/`End`; `→` unfolds or goes to the first child; `←` folds or goes to the parent (nearest visible ancestor, else the section header); `Space` chooses (header: toggles; non-choosable: «Этот узел выбрать нельзя»); `Enter` opens (header: toggles; deactivated: «Узел деактивированного проекта ничего не открывает»); `Esc` clears. After a rebuild the same item keeps focus by id.
- Roles: `treeitem`, `aria-expanded` on sections and branches, `aria-selected` on choosable items, `aria-label` as in § 7; focus ring 2 px primary. The tree view is the text alternative of the hierarchy.

## 6. States

| State | The user sees (diagram) |
|---|---|
| Tree loading / failed / 404 | `RegistrySkeleton` / `ErrorBanner` «Повторить» / `NotFound`, no tabs, no create button |
| Offers loading | hierarchy without teams, keys, counts; warning «Команды и ключи проектов не загружены» |
| Offers failed | the same with «Повторить» (focus back if it fails again); the hierarchy stays |
| Empty org | `StateFrame` «В организации пока нет областей», create only for `area.create` |
| Deactivated project | tagged, plain, no lines, not choosable, no count |
| Team with no department and no project | in «Без департамента»; choosing: «Проекты команды: нет. Департаменты команды: нет» |
| Reads disagree | project without offer: no key, no lines; offer without node ignored; a link to a missing department is ignored (the team lands under its other departments, else in «Без департамента») |
| Many lines, nothing chosen | the note of § 4.8 |
| Level choice hides everything | «Нет узлов на выбранных уровнях» |
| A section folded | header only; count stays |
| «Только связанные», team with no relations chosen | the team alone (and its departments) |
| Vertical, over 120 visible cards | the note of § 4.3 |
| Narrow / after a write | the frame scrolls both ways, toolbar wraps / both reads refetch, the diagram redraws |

## 7. Interface copy (final, Russian)

| Place | Text |
|---|---|
| Tablist / tabs / tree | «Вид структуры» / «Дерево», «Диаграмма» / «Диаграмма структуры» |
| Legend | «вложенность», «команда работает над проектом», «та же команда в другом департаменте», «связь через скрытый узел» |
| Sections | «Без департамента», «Департаменты, проекты и области»; count after the title |
| Team copy mark | «ещё в: Финансы, Склад» |
| Toolbar | «Найти узел»; «Найдено: N. Enter — к следующему» / «Ничего не найдено»; «Развернуть всё» / «Свернуть всё»; «Департаментов: N · Проектов: N · Областей: N · Команд: N» |
| Display mode (icon, `aria-label`/tooltip) | «Показ при выборе: <режим>»; «Затемнение» («Остальное приглушается»), «Только связанные» («Показаны ветка выбранного узла и связанные с ней»), «Подсветка» («Всё на месте, ветка и связанное выделены цветом») |
| Layout (icon) | «Раскладка: <значение>»; «Горизонтально» («Иерархия слева направо»), «Вертикально» («Департаменты сверху») |
| Levels | group «Уровни вложенности»; chips «Уровень N»; icon «Режим уровней: <значение>»; «Только выбранные уровни» («Показаны отмеченные уровни»), «Уровень и вложенные» («Самый верхний из отмеченных и все более глубокие»); icon «Все уровни»; «Показаны уровни: 2, 3», «… и более глубокие», «Показаны все уровни»; «Нет узлов на выбранных уровнях» |
| Counts | «N команда / команды / команд», «N проект / проекта / проектов» |
| Section | announced «<раздел>: развёрнуто» / «<раздел>: свёрнуто» |
| Summary | «Ветка департамента: проектов N, областей N, команд N. Связано вне ветки: A, B» (or «нет»); «Ветка проекта: областей N. Команды проекта: A, B» (or «нет»); «Проекты команды: A, B. Департаменты команды: C, D» (each «нет» when empty); «Показаны только ветка выбранного узла и связанные с ней.» |
| Clear | «Сбросить»; announced «Выбор сброшен» |
| Notes | «Линии связей показаны для выбранного узла»; «Команды и ключи проектов не загружены» + «Повторить»; «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» |
| Toasts | «Узел скрыт режимом «Только связанные»», «Узел скрыт выбором уровней» |
| Not choosable / not opening | «Этот узел выбрать нельзя» / «Узел деактивированного проекта ничего не открывает» |
| Hint under frame | «Tab входит в диаграмму один раз. Стрелки — переход, Пробел — выбрать, Enter — открыть, Esc — сбросить.» |
| Node names | «Департамент Логистика, команд: 3» · «Проект Складской портал (WH), в департаменте Логистика, команд: 2» (top: «без департамента»; deactivated: «, деактивирован», no count) · «Область Приёмка, в «Склад»» · «Команда JUPITER, проектов: 2, в департаменте Логистика, ещё в: Финансы» (loose: «без департамента») · «Раздел «<название>», развёрнут/свёрнут, узлов: N» |
| Reused | «Структура», «Создать департамент», «деактивирован», «В организации пока нет областей», «Повторить», `areaTexts.ts` |

New strings go into `areaTexts.ts`, not inline.

## 8. Diagram approach (OQ-7)

Compared at round 0 and decided: **A** (SVG + HTML nodes in `features/organizations`; no dependency, no kit work, native tree with roving focus, stays M). B (a `shared/ui` part) is a generic API before a second user and may become L; C (a layout library) needs a dependency and probably an ADR and draws its own canvas. **Recommendation (decided): A.** Files: `components/StructureDiagram.tsx` (frame, tree role, keys), `DiagramNode.tsx`, `DiagramToolbar.tsx`, `lib/diagramModel.ts` (team copies, sections, branch emphasis), `lib/diagramLayout.ts`, `lib/diagramLevels.ts`, `hooks/useStructureSettings.ts`. Tests: model (copies, loose teams, counters once, branch sets per chosen kind) and layouts (both orientations, level modes, nearest-visible-ancestor, line routing) as pure unit tests; page tests for the switch, three keys, states, the one Tab stop, "only two reads", "choosing does not rebuild the layout" in «Затемнение»/«Подсветка». Promote to `shared/ui` when a second page needs it.

## 9. Recommended answers (all decided A at gate-1+2; threshold 150)

OQ-1 A deactivated project tagged, no lines · OQ-2 A browser storage · OQ-3 A «Создать департамент» in both views · OQ-4 A the group, now «Без департамента» on top (with loose teams) · OQ-5 A name opens, body chooses, one Tab stop · OQ-6 A, 150 lines · OQ-7 A.

## 10. Flags for the orchestrator (no spec edit made)

1. **Kit deviation:** the switch uses `BoxedTabs` (decided); its docblock gets a sentence.
2. `src/app/navigation/*`, `routeTree.tsx`: not needed. No new read or endpoint: `selector-offers` already gives `teams[].departmentIds` and `projects[].teamIds`.
3. **Level colours:** the theme has no purple or deep-orange role named here; levels 4 and 3 need palette roles (e.g. `secondary`) or a one-line theme addition each: decide at stage 6, no literal in features. Level 3 moved off `warning.dark` (3.8:1) so white badge text reaches 4.5:1.
4. BR-SD-10: sections are level-1 items, hierarchy and team cards true level + 1.
5. «Только связанные» and the vertical layout rebuild the layout on a choice/switch; the "no rebuild" test covers «Затемнение» and «Подсветка».
6. Size: still M; the vertical layout and the level filter remain the two separable cuts.

## 11. Prototype

`prototype/index.html`: scenario strip (normal organization with JUPITER in two departments, large organization with 189 lines, offers loading/failed, loading, tree failed, empty, not found), role switch, 640 px window; in the diagram everything above is clickable in both layouts. MUI parts are drawn by hand (inline SVG icons); layout, constants and copy are the design's.

## 12. Reworks

**Rework 1:** group on top, level marks, display modes, convenience. **Rework 2:** clickable levels, collapsible sections, vertical layout. feat-050, feat-051: queued, not designed.

### Rework 3 (gate-1+2, third rejection): answers

| Remark | Answer |
|---|---|
| R3-1+4 teams inside the tree | § 4.1: a team is a child of each department it is linked to, a copy per department, marked «ещё в: …»; choosing any copy emphasises all. New legend line: dash-dot grey join «та же команда в другом департаменте» (§ 4.6). In a department **projects come first, then teams**. Team-project lines run inside the tree from the team card to the project cards (§§ 4.2, 4.3). Counters count each team once. «Без проектов и департаментов» becomes «Без департамента» (top-level projects, then teams with no department), still collapsible, on top. Levels: team under a department 2, in «Без департамента» 1; chips, badges and level hiding apply to team cards like any card |
| R3-2 icons | § 4.9: display mode, layout, level mode, all levels are `IconAction`s with the text as tooltip and `aria-label` |
| R3-3 level outline colours | § 4.4: one palette colour per level (5 and deeper share one), same as the chip and badge; number stays; ≥ 3:1 on white; chosen card keeps its outline and adds a ring |
| R3-5 branch emphasis | § 4.7: a chosen department or project emphasises its whole branch, teams under it and relations, in all three modes |
| Department optional | confirmed, no model change |

### Spec deltas for the analyst (rounds 1-3)

- **US-1:** the diagram has its own toolbar (search, expand/collapse all, display mode, layout, counters); three stored keys (view, mode, layout) with fallback and no error (BR-SD-9); what survives a switch: search, folds, level choice, expansion, settings, not the chosen node; level choice, folds and search are not stored.
- **US-2:** (a) **teams are cards inside the tree**: a copy under each linked department, after the department's projects, marked «ещё в: …»; a team without a department is in «Без департамента»; the old "teams lane" and its criteria go. (b) Sections: «Без департамента» (top-level projects, then teams with no department) on top, «Департаменты, проекты и области» below; both disclosures; the old group «Без проектов и департаментов» is replaced; choosing unfolds sections and copies' departments. (c) Levels: toggle chips, two modes, «Все уровни», hidden rows dropped, dotted joins; a team under a department is level 2, a loose team level 1; teams are hidden by levels like any card (this supersedes "teams never hidden"); not stored. (d) Per-level outline colours equal to the chips, number kept, contrast ≥ 3:1. (e) Layouts: horizontal, vertical (levels as rows, chips per section band, note above 120 cards), same keyboard order. (f) Four toolbar controls are icon buttons with text as tooltip and `aria-label`. (g) Search, expand/collapse all, counters count each team once. (h) Accessible levels: section 1, card true level + 1.
- **US-3:** (a) choosing a department emphasises its whole branch (projects, sub-areas, teams under it, all copies) and the branch's relations; a project emphasises its sub-areas and teams; a team, all its copies, projects and departments; (b) the summaries of § 7; (c) per display mode, the three criteria (branch emphasised and the rest dimmed / only branch plus related kept / branch accented and nothing dimmed); (d) the lines: team-project dashed, copy join dash-dot, through a hidden node dotted; (e) "Volume": line count = pairs + copy joins, above 150 only the chosen branch's lines; the no-rebuild check covers «Затемнение»/«Подсветка», «Только связанные» and layout switches get their own 200 ms criterion at 200 × 200.
- **BR-SD-7** (department-team and project-team lines look alike) is **superseded**: there is no department-team line; a department holds its teams as children. **BR-SD-8:** search never filters. **BR-SD-10:** sections are tree items. **BR-SD-11:** «Только связанные» forces ancestors open without changing the expansion; the level filter applies after expansion and after «Только связанные».
- **Out of scope** gains: drag and drop (feat-050), people (feat-051), hover preview, zoom/minimap. **Non-functional:** no new read; timings unchanged.
