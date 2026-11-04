# Design: «Структура» diagram view (feat-049)

Stage 3 of an M task, frontend only, inside `features/organizations`. Closes US-1 (switch), US-2 (hierarchy with teams, sections, levels, layouts, toolbar) and US-3 (relations, branch emphasis, display modes). Prototype: `prototype/index.html` (self-contained; scenarios switch on the page). Interface copy is Russian and final; prose is English. Baseline: feat-043 design §§ 5.2 and 9, ADR-0031 (only a department takes a team link), ADR-0032, `docs/conventions/ui-kit.md`. Reworks 1-4 (gate-1+2 remarks) are folded into the body; § 12 answers round 4 and lists the spec deltas.

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
| Segmented toggles (R4-1,3,6) | **new kit part `IconToggleGroup`** over MUI `ToggleButtonGroup` (`size="small"`, `exclusive`) + `ToggleButton` + `Tooltip` | the raw MUI part is barred (KIT-2); icons § 4.9 |
| Level chips; «Все уровни» | `ActionButton`-style round chip, `aria-pressed` (colour § 4.4); `ActionButton` `subtle` | tooltips § 4.4 |
| Zoom (R4-5) | `IconAction` −/+ round an `ActionButton` `subtle` «100%» | § 4.9 |
| Search, «Развернуть всё» | kit search field, `ActionButton` | |
| Announcements | `LiveRegion` (`serial`) | |
| Links | `useObjectLink` + `rowLink` of `AreasPage` | extract `rowLink` so both views share it |
| Colours | `theme.palette` roles only | `colourLiterals.test.ts`: no hex in features, SVG strokes included |

**The kit has no same-address view switch.** `PageHeader views` are addresses (BR-SD-1 forbids a new one), `ToggleButtonGroup` is forbidden in features (KIT-2), and the `BoxedTabs` docblock says "Not for page views". The design takes **`BoxedTabs` + `BoxedTabPanel`** (values `tree`/`diagram`, name «Вид структуры», arrow keys, Enter/Space confirm). It passes `kitGuard`, needs no new part; the docblock names an intended use, not a guard: a recorded deviation, the developer adds a sentence to it.

**Round 4 adds one small kit part, `IconToggleGroup`** (`items[{value, label, icon, hint}]`, `value`, `onChange`, `label` for the group): the one-of-N icon switch of the toolbar, with a showcase entry and a test (KIT-19/20). It wraps the MUI parts, so the kit hands none on (KIT-24).

## 2. Screen map

One screen, one address: «Структура», two views; navigation and routes unchanged; links are the tree's (BR-SD-6).

## 3. The page, the switch and what is remembered

Layout, top to bottom: h1 and «Создать департамент»; the switch and the legend; the diagram toolbar (search, «Развернуть всё», three segmented toggles, «Все уровни», zoom, counters); the offers notice or the summary of the chosen node; the frame (scrolls both ways, max-height min(70vh,560px)) with the level chips at its top; the hint line.

- The tree view keeps `ListFilter` and «Развернуть всё» as today. The diagram has its own toolbar: search, «Развернуть всё»/«Свернуть всё» (its own expansion), the toggles, zoom, counters. «Создать департамент» stays in both views for `area.create` (OQ-3 A).
- **Page state** (in `AreasPage`, kept across a view switch for the visit, never stored): tree filter, expansion and selection; the diagram's expansion, search text, section folds, level choice and zoom. Dropped on a switch: the chosen node.
- **Stored in `localStorage`** per user and organization, each under its own key, never inside `mustask.context.v2`; a value outside the allowed set or a refused storage means the default, no error; written on a user's choice only: `mustask.structure.view.v1.<sub>.<org>` (`tree` default / `diagram`), `...mode.v1...` (`dim` default / `only` / `hl`), `...layout.v1...` (`h` default / `v`). One hook `useStructureSettings(organizationId)`.
- Not stored, on purpose: the level choice, the section folds, the search text, **the zoom** (reset on reload, like the level choice): a stored filter reopens the page missing nodes and the user cannot tell why.

## 4. The diagram

### 4.1 Sections and what stands in them (R3-1, R3-4)

The model: **a team is a child of every department it is linked to** (`teams[].departmentIds`; ADR-0031). The department stays optional (confirmed: no model change). Two sections, each header a disclosure (`treeitem`, `aria-expanded`, chevron, title, count of cards; click, `Enter`/`Space`, `←`/`→`):

1. **«Без департамента»** on **top**. Holds, in this order: top-level projects (with their sub-areas) as in the tree, then the teams with **no department link** (whether or not they work on projects), choosable. Shown only when something is there. It replaces the old group «Без проектов и департаментов».
2. **«Департаменты, проекты и области»**: departments, and under each department **its projects first (with sub-areas), then its teams**, both in the read's order. The department's own child order is therefore: projects, teams.

A team linked to several departments stands **under each of them, as a copy**, every copy marked **«ещё в: <остальные департаменты>»**. Copies are one team: choosing any emphasises all (§ 4.7), counters count the team once (§ 4.8), the keyboard moves through copies as through separate items (each has its own place in the order). Folds are page state; choosing unfolds a folded section and the branches that hide emphasised nodes, copies' departments included (BR-SD-11).

### 4.2 Horizontal layout (default «Горизонтально»)

Sketch: a section header, then per node one row, indented by level (`▾ Без департамента · 4` / `1 Проект …` / `2 Область …` / `1 Команда ATLAS`), a gutter of relation arcs at the right edge.

- **One lane:** one row per visible node, pre-order of the read (never re-sorted), a full-width card indented 22 px per true level (a team under a department is level 2, a team in «Без департамента» level 1), all ending at one right edge; level badge in each node; faint dotted level guides; nesting joins are thin elbows in the indent gutter.
- **Relation lines run inside the tree** (R3-1): from a team card to its projects' cards, dashed arcs in a 150 px gutter at the right edge of the cards. A pair uses the team copy under the project's own department when it exists, else the first copy. There is no teams lane any more.

### 4.3 Vertical layout («Вертикально»)

- **Rows are levels:** departments and top-level projects on the top row, projects and teams under a department on the second, sub-areas below. A **tidy top-down tree**: leaves take consecutive slots (156 px card + 12 px gap), a parent stands over the middle of its visible children (projects, then teams, left to right). The level chips repeat in the left gutter of **each section band**, one per row; they toggle the same state.
- **«Без департамента»** is the same band, on top, over the full frame width: top-level projects and loose teams on row 1.
- **Relation lines** are curves between the cards: a team and a project on one row dip below the cards, on different rows run vertical-tangent between the lower edge of the upper card and the upper edge of the lower. Copy joins dip the same way.
- **Nodes** 156 × 58 px: line 1 chevron, badge, type; line 2 name (link); line 3 key, «деактивирован», «ещё в: …», count.
- **Large organization:** a vertical tree is as wide as its leaf count; above **120 visible cards** a note «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» stands above the frame (`InlineNotice` info; nothing is blocked).
- **One small pure function** `layoutDiagram(model, expansion, options{orientation, levels, sections, related})` fixes the visible set and nearest visible ancestors, assigns rows (horizontal) or runs the tidy placement (vertical), then routes lines; about 200 lines, unit-tested without a DOM.

### 4.4 Levels (R2-1) and their colours (R3-3, R4-2)

- **Chips = ruler.** One round toggle chip per level of the whole tree (1 … N, so chips never jump): horizontal layout at the column's left edge above the first row; vertical, in the gutter of each section band at its row. Click toggles (`aria-pressed`, «Уровень 2»); multi-select. **Each chip has a hover/focus tooltip** saying what the level holds and what a click does (§ 7; BR-SD-10, BR-SD-16); the chip stays a real button, so the tooltip is also its description.
- **«Режим уровней»** is a two-button segmented toggle **in the toolbar beside «Раскладка»** (§ 4.9), default **«Только выбранные уровни»**: shown are the pressed levels. **«Уровень и вложенные»**: the topmost pressed level and every deeper one (deeper chips show a light tint). No chip pressed means all levels. **«Все уровни»** is a text button (`ActionButton` `subtle`) right after the toggle, disabled while nothing is pressed, unpresses every chip and puts focus on chip 1. The old round icon button at the frame's top is gone: its 32 px disabled halo was the stray faint circle (R4-7); the frame's top row is now the chips only.
- **Rows of a hidden level drop.** A node whose parent is hidden attaches to its nearest visible ancestor by a **dotted** join; with none it stands at its true column or row. A team follows the same rule through its department. In the vertical layout a level with no shown node is a 30 px strip.
- **Relation lines** go to the nearest visible ancestor (dotted, «связь через скрытый узел»); with none the line is not drawn. Counts and summaries speak of data, not of what is drawn.
- **Level colour on the card (R4-2).** Every card (department, project, area, team) has a **5 px left edge** and a 1.5 px outline in its level colour, and a **light tint of that colour as its fill** (8 %; hover 14 %), equal to its chip (outlined; filled when pressed) and badge fill. The badge number and the chip number stay, so colour is never the only signal. Outlines are ≥ 3:1 against white (WCAG 1.4.11); badge and pressed-chip text is white ≥ 4.5:1; **card text (#242424, secondary #4A4A4A, link primary.dark) is ≥ 5.5:1 on every tint, measured at the worst level and the 18 % emphasis tint** (BR-SD-12). A deactivated card keeps its outline with a neutral grey fill. Emphasis (§ 4.7) adds a ring and a stronger tint (14 % related, 18 % chosen or accented) and never replaces the level colour.

| Level | Palette role (light) | Contrast on white |
|---|---|---|
| 1 | `primary.dark` | 7.6:1 |
| 2 | `success.main` | 5.1:1 |
| 3 | deep orange 900 (#BF360C) role | 5.6:1 |
| 4 | `secondary.main`/purple 700 role | 8:1 |
| 5 and deeper | `text.secondary` | 6:1 |

The developer maps the four roles that exist in the theme and adds one `levelColour(level)` helper in `lib/diagramLevels.ts`; a purple that is not in the theme is a stage-4 question to the orchestrator, not an inline literal. A chosen card keeps its level colours and gets an outer 3 px primary ring; a related card a 2 px ring; a search hit an amber ring.
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

### 4.6 Lines (R3-1; contrast R4-4)

| Line | Style (the legend sample and the canvas use the same stroke) | On white |
|---|---|---|
| Nesting | **1.5 px solid** #616161 elbow; dotted across hidden levels | 6.2:1 |
| Team works on a project | **2.5 px dashed (7 4)** primary.dark, 4 px dot at the project end | 7.6:1 |
| Same team in another department | **2.5 px dash-dot (9 4 2.5 4)** #424242, no dot; joins consecutive copies of one team | 10:1 |
| Relation through a hidden node | **2.5 px dotted (2.5 4)** primary.dark, ends at the nearest visible ancestor | 7.6:1 |
| Emphasised line | 4 px; the others fade or turn grey #9E9E9E (see modes) | |

The legend (top right of the toolbar row) draws the four samples 34 px wide at the canvas stroke widths: «вложенность», «команда работает над проектом», «та же команда в другом департаменте», «связь через скрытый узел». Line kinds differ by pattern, never by colour alone. Constants (named in `lib/diagramLayout.ts`): horizontal `ROW 40`, `INDENT 22`, `HIERARCHY_WIDTH 480`, `GUTTER 150`; vertical `NODE 156×58`, `SLOT_GAP 12`, `BAND 88`; `RELATION_LINE_LIMIT 150`, `VERTICAL_NOTE_LIMIT 120`. Rendering: absolutely positioned HTML nodes (`Box`), one `<svg aria-hidden>` line layer behind.

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
- «Найти узел» marks matches, Enter jumps to the next (unfolds section and branches, focuses, scrolls; a team jumps to its first copy), case and «ё/е» ignored, filters nothing (BR-SD-8). Counters «Департаментов · Проектов · Областей · Команд» count **each team once** whatever its copies. Follow-ups not designed here: hover preview, minimap, drag and drop (feat-050), people (feat-051).

### 4.9 Toolbar toggles (R4-1, R4-3, R4-6) and zoom (R4-5)

Row: «Найти узел», «Развернуть всё», then three `IconToggleGroup`s with hand-drawn `SvgIcon`s in the feature (R5) — **display mode** (three nodes, one link: the rest dimmed / the unrelated one struck out / the related one filled), **layout** (tree left-to-right / top-down), **level mode**, beside layout (three level bars: one filled / it and the lower ones filled) — then «Все уровни», zoom, counters; wraps when narrow. A group is adjoined 32 px buttons; the pressed one has the selection tint and a 3 px underline. Group `aria-label` = control, button name = value, tooltip per § 7; each group is one Tab stop with the arrow keys between its buttons (MUI's roving focus; stage-6 decision, status § Decisions). The drifting halo (R4-1) came from a round icon button in an inline menu wrapper; a segment fills its button and the focus ring is drawn inside it.

**Zoom:** 50-200 %, ×1.1 per wheel notch or button press; Ctrl + wheel over the frame zooms about the pointer (a non-passive handler keeps the page from zooming), the buttons zoom about the frame centre. Holding the middle button drags the canvas (`grabbing`, browser autoscroll suppressed); scrollbars stay. «− 100% +» per § 7, the value resets to 100 %. Not stored: reset on reload or organization change, kept across view and layout switches. A CSS transform on the canvas, no layout rebuild.

## 5. Keyboard and screen reader (BR-SD-10, OQ-5 A)

- **One Tab stop for the tree**: `role="tree"` «Диаграмма структуры», roving `tabIndex`; names, chevrons, links inside `tabIndex -1`. The toolbar controls (each toggle button, «Все уровни», the zoom buttons) and the chips group are separate stops before it.
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
| «Все уровни», nothing pressed | disabled text button, no halo |
| Zoom at 50 / 100 / 200 % | «−» inactive / value inactive / «+» inactive |
| Narrow / after a write | the frame scrolls both ways, toolbar wraps / both reads refetch, the diagram redraws |

## 7. Interface copy (final, Russian)

| Place | Text |
|---|---|
| Tablist / tabs / tree | «Вид структуры» / «Дерево», «Диаграмма» / «Диаграмма структуры» |
| Legend | «вложенность», «команда работает над проектом», «та же команда в другом департаменте», «связь через скрытый узел» |
| Sections | «Без департамента», «Департаменты, проекты и области»; count after the title |
| Team copy mark | «ещё в: Финансы, Склад» |
| Toolbar | «Найти узел»; «Найдено: N. Enter — к следующему» / «Ничего не найдено»; «Развернуть всё» / «Свернуть всё»; «Департаментов: N · Проектов: N · Областей: N · Команд: N» |
| Toggles (group name / button name / tooltip «<группа>: <значение>. <подсказка>») | «Показ при выборе»: «Затемнение» («Остальное приглушается»), «Только связанные» («Показаны ветка выбранного узла и связанные с ней»), «Подсветка» («Всё на месте, ветка и связанное выделены цветом»); «Раскладка»: «Горизонтально» («Иерархия слева направо»), «Вертикально» («Департаменты сверху»); «Режим уровней»: «Только выбранные уровни» («Показаны отмеченные уровни»), «Уровень и вложенные» («Самый верхний из отмеченных и все более глубокие») |
| Levels | group «Уровни вложенности»; chips «Уровень N»; text button «Все уровни» (tooltip «Показать все уровни»); «Показаны уровни: 2, 3», «… и более глубокие», «Показаны все уровни»; «Нет узлов на выбранных уровнях» |
| Level chip tooltip | 1: «Уровень 1 — департаменты, проекты верхнего уровня и команды без департамента.»; 2: «Уровень 2 — проекты и команды под департаментом, области под проектом верхнего уровня.»; N ≥ 3: «Уровень N — области N-го уровня вложенности.»; then «Нажмите, чтобы показать только этот уровень» (mode «Уровень и вложенные»: «…показать этот и более глубокие уровни»; pressed: «Нажмите, чтобы убрать уровень из выбранных») |
| Zoom | group «Масштаб»; «Уменьшить масштаб (Ctrl + колесо мыши)», «Увеличить масштаб (Ctrl + колесо мыши)»; value «100%», name «Масштаб: 120%. Вернуть 100%», tooltip «Масштаб: 120%. Нажмите, чтобы вернуть 100%» (at 100 %: «Масштаб: 100%»); announced «Масштаб: 120%» |
| Counts | «N команда / команды / команд», «N проект / проекта / проектов» |
| Section | announced «<раздел>: развёрнуто» / «<раздел>: свёрнуто» |
| Summary | «Ветка департамента: проектов N, областей N, команд N. Связано вне ветки: A, B» (or «нет»); «Ветка проекта: областей N. Команды проекта: A, B» (or «нет»); «Проекты команды: A, B. Департаменты команды: C, D» (each «нет» when empty); «Показаны только ветка выбранного узла и связанные с ней.» |
| Clear | «Сбросить»; announced «Выбор сброшен» |
| Notes | «Линии связей показаны для выбранного узла»; «Команды и ключи проектов не загружены» + «Повторить»; «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» |
| Toasts | «Узел скрыт режимом «Только связанные»», «Узел скрыт выбором уровней» |
| Not choosable / not opening | «Этот узел выбрать нельзя» / «Узел деактивированного проекта ничего не открывает» |
| Hint under frame | «Tab входит в диаграмму один раз. Стрелки — переход, Пробел — выбрать, Enter — открыть, Esc — сбросить. Ctrl + колесо мыши — масштаб, средняя кнопка мыши (зажать и двигать) — перемещение.» |
| Node names | «Департамент Логистика, команд: 3» · «Проект Складской портал (WH), в департаменте Логистика, команд: 2» (top: «без департамента»; deactivated: «, деактивирован», no count) · «Область Приёмка, в «Склад»» · «Команда JUPITER, проектов: 2, в департаменте Логистика, ещё в: Финансы» (loose: «без департамента») · «Раздел «<название>», развёрнут/свёрнут, узлов: N» |
| Reused | «Структура», «Создать департамент», «деактивирован», «В организации пока нет областей», «Повторить», `areaTexts.ts` |

New strings go into `areaTexts.ts`, not inline.

## 8. Diagram approach (OQ-7, decided A)

SVG + HTML nodes in `features/organizations`: no dependency, one small kit part (`IconToggleGroup`), native tree with roving focus, stays M. Files: `components/StructureDiagram.tsx` (frame, tree role, keys), `DiagramNode.tsx`, `DiagramToolbar.tsx`, `shared/ui/.../IconToggleGroup.tsx`, `lib/diagramModel.ts` (team copies, sections, branch emphasis), `lib/diagramLayout.ts`, `lib/diagramLevels.ts`, `lib/diagramZoom.ts` (clamp, step, zoom about a point; pure), `hooks/useStructureSettings.ts`. Tests: model, layouts and zoom as pure unit tests; page tests for the switch, the three keys, the toggles, the zoom buttons and limits, states, the one Tab stop, "only two reads", "choosing does not rebuild the layout" in «Затемнение»/«Подсветка». Promote to `shared/ui` when a second page needs it.

OQ-1..OQ-7 were all decided A at gate-1+2 (OQ-6 threshold 150); see the spec.

## 10. Flags for the orchestrator (no spec edit made)

1. **Kit:** the view switch uses `BoxedTabs` (decided; docblock gets a sentence). **New: `IconToggleGroup`** in `shared/ui` for the toolbar toggles, because KIT-2 bars the raw MUI `ToggleButtonGroup` in features. Accepted by the human at gate-1+2 round 5 (OQ-7).
2. No navigation, route, read or endpoint change: `selector-offers` already gives `teams[].departmentIds` and `projects[].teamIds`.
3. **Level colours:** the theme has no purple or deep-orange role named here; levels 4 and 3 need palette roles (e.g. `secondary`) or a one-line theme addition each: decide at stage 6, no literal in features. Level 3 moved off `warning.dark` (3.8:1) so white badge text reaches 4.5:1.
4. «Только связанные» and the vertical layout rebuild the layout on a choice/switch; the "no rebuild" test covers «Затемнение» and «Подсветка».
5. Size: still M with the small kit part; the vertical layout and the level filter remain the two separable cuts.

## 11. Prototype

`prototype/index.html`: scenario strip (normal organization, large with 189 lines, offers loading/failed, loading, tree failed, empty, not found), role switch, 640 px window; everything above is clickable in both layouts. MUI parts are hand-drawn; layout, constants and copy are the design's.

## 12. Reworks

Rounds 1-3 are folded into the body and the spec. feat-050, feat-051: queued, not designed.

### Rework 4 (gate-1+2, fourth rejection): answers

| Remark | Where |
|---|---|
| R4-1, R4-3, R4-6 toggles; level mode beside layout; the drifting halo | § 4.9 (cause and fix stated) |
| R4-2 level colour on the cards | § 4.4 (text ≥ 5.5:1 on the tints, outlines ≥ 5:1) |
| R4-4 legend and line contrast | § 4.6 |
| R4-5 pan and zoom | § 4.9 |
| R4-7 stray circle; R4-8 chip tooltips | § 4.4, § 7 |

Round-4 spec deltas are in the spec (02fc1973). OQ-7: `IconToggleGroup` accepted by the human; icons redrawn (R5, § 4.9).
