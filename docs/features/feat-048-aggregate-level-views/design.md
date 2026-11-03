# Design: Aggregate level views (feat-048)

Stage 3 artifact, a **delta** on [feat-043's design § 12](../feat-043-area-project-team-navigation/design.md) (approved at feat-043's gate-2), reworked on 2026-11-03 for the grouping switch (spec BR-AV-1..12, US-11). § 12 fixed and is **not reopened here**: MUI `Accordion` groups with a summary (name, `Tag`, count «40» / «3 из 40»); first group open on first visit; «Показать ещё» (next 50); «Свернуть все» and no «Развернуть все»; the board's lanes and 50-card cells; moves through the existing native drag and drop of `BacklogRow` and `BoardColumns` and the keyboard path «Ещё действия», «Переместить» / «Переместить в колонку»; a drop in another group refused; the 4-in-flight cap per tab; open-state memory (10 at most). What changes: **the group is no longer always a project** (§ 3), the user picks the grouping (§ 2). Spec: [`spec.md`](spec.md) (gate-1 approved, OQ-1..OQ-9 all A). Every string in «» is final. The reference tracker is never named.

- Prototype: [`prototype/index.html`](prototype/index.html) (§ 9).
- ADRs relied on: [ADR-0014](../../adr/0014-server-side-registry-paging-envelope.md)/[0015](../../adr/0015-gridify-for-list-queries.md) (the page envelope stays on the flat list), **[ADR-0036](../../adr/0036-cursor-paged-group-reads-and-read-limiter.md)** (proposed: the cursor with two order keys, the grouping parameter, the limiter), [ADR-0032](../../adr/0032-object-pages-and-navigation-context.md), [ADR-0005](../../adr/0005-stable-error-code-registry.md).

## 1. Reuse first; one new part, renamed

| Wanted | Verdict |
|---|---|
| **The «Группировка» switch** | **Not new.** The kit's `MenuButton` (`selection="radio"`, menu name «Группировка») with an `ActionButton` text trigger «Группировка: По проекту» (the shipped `menuOpen` trailing `ExpandMore`). It is the panel's one menu, already used for the level drop-down; a `Select` or `ToggleButtonGroup` was rejected: four long Russian options do not fit a button row at 640 px, and a second selector style would sit beside `MenuButton`s |
| Organization / department / team / project «Рабочие элементы» | **Not new.** The project's `WorkItemsPage` (feat-003) takes a scope; grouped form in § 4 |
| A group (backlog, list) and a lane (board) | **One new feature part, `ViewGroup`** in `features/boards/components` (**renamed from `ProjectGroup`**: a group is a department, a project or a team now). MUI `Accordion` (`slotProps.heading` an `h2`) + `AccordionSummary` (name, optional `Tag`, count) + `AccordionDetails`; it owns the group's read states (§ 3). With no header (grouping «Без группировки») it draws the same states in a plain `Box` (`role="region"`, named by the page), so the stream of rows has one implementation. Not the kit: one feature uses it three times |
| Read queue, retry, cursor, 429 backoff | **A hook, `useGroupRead`** (name kept: it reads one group of any grouping): the 4-per-tab queue, the `Retry-After` retry (slot released while waiting), the cursor, the held-row set by id. A group is named `{grouping, id}` (`id` absent for «Без команды», «Без департамента» and the stream) |
| Group's loading / empty / error | Kit: `TableSkeleton` / `BoardSkeleton`, `InlineEmpty`, `InlineNotice`, `ErrorBanner` (compact), `StatusRegion` |
| «Показать ещё», «Повторить», «Свернуть все» | `ActionButton` (text variant); «Свернуть все» in the `PageHeader` bar |
| Move menu | The shipped `MoreActionsMenu` with `BacklogRow`'s «Переместить» items |
| Notices | `useNotice` (deactivated project, refused move); `InlineNotice` for the two hints (§ 5) |
| «Без команды» (project list) | `FormControlLabel` + `Checkbox` in the `FilterBar` slot of `TeamFilter` |

## 2. The switch: place, options, default, memory

- **Place:** the first command in the `PageHeader` bar of «Бэклог», «Канбан» and «Рабочие элементы», left of «Свернуть все». The title line (title and the narrowing `Tag`) is unchanged. It is not a filter: «Сбросить фильтры» and the entry do not touch it (BR-AV-1).
- **Options (BR-AV-2),** always in this order, the current one checked: «Без группировки», «По департаменту», «По проекту», «По команде»; the level offers:

| Level (most specific selector) | Offered |
|---|---|
| Organization | all four |
| Department | «Без группировки», «По проекту», «По команде» |
| Team | «Без группировки», «По проекту» |
| Project | «Без группировки», «По команде» |
| Project and team both chosen | the control is **absent** (one option) |

- **Default and memory (BR-AV-3).** Defaults: «Бэклог», «Канбан»: «По проекту» at organization and department level, and at team level **with a department chosen**; «Без группировки» at team level with no department (feat-040's views as today, OQ-7 A) and at project level; «Рабочие элементы»: «Без группировки» everywhere. The choice is kept per user, organization, view and level in the browser; a remembered option the level no longer offers falls back to the default silently. Choosing re-reads the view from its first page, keeps filters and selectors, and is announced through `StatusRegion` («Группировка: По команде»). Not in the address.
- **Narrow widths:** the switch stays visible and wraps with the bar; «Свернуть все» moves into «Ещё действия».
- **States:** enabled while the headers read is pending (a second choice replaces the first read); all roles read and switch.

## 3. Groups, lanes and the stream: headers, forms, all states

**Headers per grouping** (name `h2`, count right; narrowed «3 из 40»; the headers come from one grouped count read):

| Grouping | Group header | Order | First |
|---|---|---|---|
| По проекту | name + key `Tag` | name, key | — |
| По департаменту | department name | name | «Без департамента» (projects at the top level) |
| По команде | team name | name | «Без команды» |

«Без команды» and «Без департамента» are ordinary groups with that name and no `Tag`. A group with no row has no header (and its open-state entry is ignored).

**«Без группировки» (BR-AV-5, -9):** no header and no lane. «Бэклог»: one flat list of rows; «Канбан»: one board without lanes (columns of the states, cells of 50 cards, «Показать ещё» per column); the count is shown once as a line in the page bar, «Элементов: 40» / «Элементов: 3 из 40»; «Свернуть все» is absent. The list is read as one group: the same cursor, limiter, cap and states below, with the neutral copy of the table (stream column).

Rows inside non-project groups are flat (no nesting, no indent): change-time order cannot keep a parent next to its children (D-6).

The group's own states live in `AccordionDetails` (or the plain `Box`); the **header keeps its name, key and count in every state**.

| State | The user sees | Rules |
|---|---|---|
| Closed | Summary only | No read |
| Waiting for a slot (a restored set of 10, 4 at a time) | Skeleton rows, `role="status"`: «Загрузка элементов группы» (stream: «Загрузка элементов») | Starts when one of the tab's 4 is free |
| Loading, first page | The same (5 rows; a board cell 3 cards) | — |
| Loaded | Rows or cards, then «Показать ещё» while `nextCursor` exists | 50 a page; a held row is dropped by id |
| Loading more | «Показать ещё» disabled, `CircularProgress`, rows stay | — |
| Empty (the group emptied meanwhile) | `InlineEmpty` «В группе больше нет элементов этого уровня» (stream: «Элементов этого уровня больше нет») | Headers re-read |
| **Refused, retrying** (429) | The loading state plus «Сервис занят, пробуем ещё раз…» (`aria-live="polite"`) | `Retry-After` (else 1 s, doubling), 3 tries; **the slot is released while waiting** (OQ-1) |
| **Refused, last try failed** | Compact `ErrorBanner` «Не удалось загрузить элементы группы. Сервис занят, попробуйте ещё раз.» + «Повторить» (stream: «…загрузить элементы. Сервис занят…») | First page only; other groups unaffected |
| **Refused on «Показать ещё»** | Rows kept, the button again and an `InlineNotice` (error) with the same message | (OQ-4.2) |
| Other read error | Compact `ErrorBanner` «Не удалось загрузить элементы группы.» (stream: «…загрузить элементы.») + «Повторить»; held rows stay | A 400 naming a level, filter or `grouping` is this state too |
| **Cursor refused** (400 naming `cursor`) | Loading state: rows dropped, first page read again, headers re-read. **No message** | One automatic reload per «Показать ещё»; a second 400 in a row is the error state |
| **Project deactivated meanwhile** (only by project: a page, a row or a card move answers «Проект не найден») | Group or lane disappears; headers re-read; `useNotice` «Проект больше недоступен: группа скрыта.» | Under other groupings the project's rows just leave the next read |
| **Item changed group meanwhile** (BR-AV-10) | Nothing at once. At the next read the item is in its new group, counts adjust | No message; a held row's commands work by its own project |
| Whole view: no group at all | Backlog «В бэклоге пока нет элементов»; board «На доске пока нет элементов»; list «Рабочих элементов пока нет»; narrowed or filtered «Ничего не найдено» (+ «Сбросить фильтры» when a filter is set) | `InlineEmpty` |
| Switching grouping / headers read pending | Three `Skeleton` header bars, one `role="status"` «Загрузка данных» | The switch stays enabled |
| Headers read fails | `ErrorBanner` «Повторить» in place of the groups | One read, no 429 (OQ-2) |

**Permissions:** all roles read, group, switch and «Показать ещё». «Читатель» has no grip, no «Переместить», no «Переместить в колонку» (none left: no «Ещё действия»). «Администратор» and «Участник»: moves where the item's project allows, 403 shows the catalogue text through `useNotice`; the row or card stays.

**Accessibility:** the summary is the accordion button (`aria-expanded`, `aria-controls`); the count is read as shown. A «Показать ещё» page is announced («Загружено ещё элементов: 50»); focus stays on the button, or goes to the first new row when it leaves. Colour never alone.

**Narrow widths:** the summary wraps (key `Tag` and count under the name below 640 px); lanes scroll horizontally inside the lane with its header fixed left.

## 4. «Рабочие элементы»: the flat list and the grouped form

Entry and address as feat-043's menu table. One page serves the four levels, the same filters (keyword, state, «Исполнитель», type, level), the same columns (shipped, plus «Проект» as a link and «Команда» as plain text or «Без команды», D-4 A); no create command; no «Команда» filter at levels above the project; title `Tag` «сужено: команда MARS, департамент Логистика» (feat-043 § 4: at the organization any of «команда …», «департамент …»; a department, «команда …»; a team, «департамент …»).

| | «Без группировки» (default) | Grouped (BR-AV-8) |
|---|---|---|
| Frame | `DataTable` «Рабочие элементы» | `ViewGroup` accordions of § 3, each with a `DataTable` inside |
| Rows | 50 a page, order: change time, number, project key | Same order, 50 a group then «Показать ещё» |
| Paging | `TablePager` with the total (ADR-0015 envelope) | **No pager, no page numbers** |
| Sorting | Sortable columns as feat-003 | **Columns not sortable** (`SortableHeaderCell` not used, plain headers); no sort control appears |
| Read | One page read; keyword read up to 5 s, table keeps its skeleton | Grouped count + group reads under the limiter |

Switching keeps filters and selectors; from the flat list to a grouped one the page number and sort are dropped, back to the flat list it starts at page 1 in the default order. The project's list keeps one control: «Без команды» takes `TeamFilter`'s place (exclusive with the selector; choosing it while a team is selected resets the selector with «Выбор сброшен: команда MARS.»). The project's backlog and board keep `ViewFilterRow`'s «Команда» until feat-013.

States: the flat list's are feat-003's frame (`StateFrame`: «Загрузка данных», `ErrorBanner` «Повторить», «Рабочих элементов пока нет», «Ничего не найдено» + «Сбросить фильтры», refused level: the catalogue's). Grouped: § 3. A non-member: «Организация не найдена». Narrow widths: the table scrolls in its card, «Проект» stays visible.

## 5. Hints and what can be moved, per grouping

**One `InlineNotice` (info) under the page header, never one per group, never two at once** (D-1 A):

| Case | Text |
|---|---|
| «Бэклог», by project, a team or a filter narrows | «Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.» |
| «Бэклог», any other grouping or «Без группировки» at organization, department, and **by team at project level** | «При этой группировке строки идут по времени изменения, порядок не меняется. Выберите «По проекту», чтобы менять его.» |
| Board, the list, project and team level «Без группировки» (as shipped) | No hint |

**What moves** (OQ-8 A: the project's and the team's own «Без группировки» views are as today):

| View and grouping | Rows / cards in a group | Move menu / drag |
|---|---|---|
| Backlog, by project, organization or department, nothing narrows | The project's rank | «Переместить» (§ 6) and drag, inside the group only |
| Backlog, by project, narrowed (team, department at team level, filter) | The project's rank | None; the narrowing hint |
| Backlog, by department / team, stream | Change time, then number, then project key | **None**: no grip, no «Переместить», the change-time hint |
| Backlog, project level «Без группировки» | The project's rank (shipped) | Shipped move, no hint |
| Backlog, team level «Без группировки» | feat-040's order | As shipped |
| Board, any grouping | By project: the project's rank; otherwise change time | **Only «Переместить в колонку» and the drag between columns, inside the lane** (a drop in another lane: not-allowed cursor, no line); the whole board without lanes: any column |
| List (flat or grouped) | Change time | None |

A move updates its group locally; a card moved into a part-loaded cell re-reads that cell from its first page (D-3 A). When the item's team changes, nothing jumps (BR-AV-10).

## 6. Moves in a part-loaded by-project group (OQ-6 A)

Items «В начало», «Выше», «Ниже», «В конец» of the shipped «Переместить».

| Item / target | Fully loaded | Part loaded (`nextCursor` present) |
|---|---|---|
| «В начало», «Выше» | enabled except on the first row | same |
| «Ниже» | enabled except on the last row | **disabled on the last loaded row**, secondary line «Сначала нажмите «Показать ещё»» |
| «В конец» | end of the group | **end of the project's backlog**: the row leaves the loaded set, the count is unchanged, `useNotice` «Элемент перенесён в конец бэклога проекта. Он появится после «Показать ещё».» |
| Drop below the last loaded row | a target | **not a target** until `nextCursor` is absent |

A 409 `boards.backlog.reorder_stale` (shipped text) reloads that group from the first page. 403 or «Проект не найден»: § 3.

## 7. For the developer: the server side owed

**Reads (additive; the api-contract is the backend developer's).** Checked in `src/backend`: only the project-scoped `GET …/projects/{p}/backlogs/{level}`, `…/boards/{level}` and the page-enveloped `…/projects/{p}/workitems` exist; the aggregate reads below are all new, scoped to the organization. `grouping` is `none | department | project | team`; a grouping the level does not offer (BR-AV-2) is the validation 400 naming `grouping`. `scope` = optional `departmentId`, `teamId`, `projectId` and feat-003's filters (OQ-5).

| Read | Shape |
|---|---|
| Grouped count `GET /api/organizations/{o}/{view}/{level}/groups?grouping=…` (`view` = `backlogs`, `boards`, `workitems`) | `{groups:[{kind,id|null,key?,name,count,total}]}` in the order of § 3, the empty absent; `none` gives one group; the board adds counts per state; one statement, a `GROUP BY` of the grouping's key (project: `project_id`; department: `project_id` rolled up by the project node's parent area; team: `owning_team_id`) |
| Group read `GET …/backlogs/{level}/rows?grouping=&group=&limit=50&cursor=` | `group` = the id, or `none` for «Без команды», «Без департамента» (with `grouping=none` absent). `{rows:[…], nextCursor}`, no total. By project: rank order with depth/parent as shipped; else change time, flat |
| Cell read `GET …/boards/{level}/cells?grouping=&group=&stateId=&limit=&cursor=` | `{cards, nextCursor}` |
| Flat list `GET /api/organizations/{o}/workitems` | New; the project list's parameters plus repeated `teamId`, `departmentId`; the ADR-0015 envelope; rows add `project {id,key,name}` and `team`; with `…/workitems/assignee-options`. Used for «Без группировки» only |

Every read takes the caller (BR-NV-15); `limit` 1..50 (default 50).

**The cursor** (ADR-0036): opaque, forward only, names a **position in the group's order**, which has **two keys**: `rank` (project rank key and id) for by project, `changed` (change time, number, project key) for everything else, the stream included. It fingerprints project/level/view/filters **and the grouping and the group**; a malformed or foreign cursor is the validation 400 `boards.validation_failed`, field `cursor`, reason `boards.invalid_cursor`. A row moved above the cursor is not seen until the group is reopened (approved stale-row rule).

**The limiter** (ADR-0036) is on the group read and the cell read of **every grouping**, not on the grouped count, the flat list or the keyword read (OQ-2):

| | Value |
|---|---|
| Key | the Keycloak subject (`sub`), across organizations, tabs, devices; per API process |
| Per user | **6** in flight, **queue 6**, wait at most **3 s** |
| Global (per process) | **40** in flight, **queue 20**, wait 3 s (pool 100; 60 stay for the rest) |
| Full | **429** `http.too_many_requests`, `Retry-After: 2` |

Two tabs restoring 10 groups each are 8 reads: 6 run, 2 wait, none refused (tested with a fake clock). The 4-per-tab cap is unchanged; the BFF (ADR-0022) must pass `Retry-After`.

| Code | Status | Russian | Kazakh (needs review) |
|---|---|---|---|
| `http.too_many_requests` (`http.json`) | 429 | Сервис сейчас занят. Повторите запрос через несколько секунд. | Қызмет қазір бос емес. Сұранысты бірнеше секундтан кейін қайталаңыз. |
| `boards.invalid_cursor` (`boards.json`, field reason) | 400 | Некорректный курсор страницы. | Бет курсоры дұрыс емес. |

The panel shows the group copy of § 3 for a 429, never the catalogue text.

### Budget check, change-time order (BR-AV-11)

The table `work_items` has `project_id`, `type_id`, `owning_team_id`, `changed_at`, `number` and no department or organization column. Each group read is **one statement** (constant count): a `LATERAL` per (project, level type) that takes the first 50 after the cursor in index order, an outer top-50 on (`changed_at` desc, `number` desc, project key), then the heap for those 50 rows only. Reasoned from the schema, not measured; the 1 000 000-item test (owed by the NFR) decides.

| Group | Plan | Index |
|---|---|---|
| Department; the stream | Probes: projects (≤200) × level types (≤4) = ≤800, each ≤50 entries: ≤40 000 entries, ≈ 0.1–0.3 s warm | **new** `ix_work_items_project_id_type_id_changed_at_number` `(project_id, type_id, changed_at DESC, number DESC)` `INCLUDE (id)` |
| Team | Equality on the team and a type: ≤4 probes of ≤50 entries | **new** `ix_work_items_owning_team_id_type_id_changed_at_number` `(owning_team_id, type_id, changed_at DESC, number DESC)`; **replaces** `ix_work_items_owning_team_id_type_id` (its prefix) |
| «Без команды» | As department, but `owning_team_id IS NULL` | **new** partial `ix_work_items_no_team_changed_at` `(project_id, type_id, changed_at DESC, number DESC) WHERE owning_team_id IS NULL`; without it a rare no-team share is found only by scanning most of the range |
| Project (by team at project level) | The project's items of one team, ≤ a few thousand, sorted | The existing `(project_id, owning_team_id, type_id)` is enough |
| Assignee filter | The assignee's items first, sorted | **new** partial `ix_work_items_assignee_id_changed_at` `(assignee_id, changed_at DESC, number DESC) WHERE assignee_id IS NOT NULL` |
| State filter, keyword | Post-filter on the range; fine for a state holding ≥ 5% of the items, not for a very rare one | none (D-5) |

By project (rank) keeps the shipped rank index. The grouped count is the same single pass as by project; the department roll-up adds a join of ≤200 projects. **The cost:** four new indexes carry `changed_at`, which every edit writes, and one is dropped, so each edit writes about three more index entries; a rare state filter is not covered. Raised, not relaxed: D-5.

## 8. Decisions for gate 2

D-1..D-4 stand as the human answered them (D-1 now covers both hints; D-3 any lane or column; D-4 holds under every grouping; D-2's numbers are unchanged, the group reads of every grouping count).

| # | Question | Options |
|---|---|---|
| D-1 | Hint place | **(A, rec., answered)** once under the header (§ 5). (B) in every group |
| D-2 | Limiter numbers | **(A, rec., answered)** user 6 + queue 6 + 3 s; global 40 + queue 20; `Retry-After: 2` |
| D-3 | A card moved into a part-loaded cell | **(A, rec., answered)** read the target cell again from its first page |
| D-4 | «Команда» column at team level | **(A, rec., answered)** same columns at every level but the project's |
| D-5 | The indexes BR-AV-11 needs for change-time order at 1 000 000 items (§ 7) | **(A, recommended)** the four new indexes above (one old index dropped), one migration with the slice, proved by the 1 000 000-item test; if that fails the task returns to gate 2 and the budget is not relaxed. Costs three more index entries per edit. A rare-state filter is not covered and is reported by the test. (B) A plus a state index `(project_id, state_id, changed_at DESC, number DESC)`: covers a rare state, one more entry per edit |
| D-6 | Flat rows in non-project groups (no nesting) | **(A, recommended)** flat, no indent: change-time order cannot hold a parent beside its children, and nothing is hidden. (B) a row keeps a «в {parent}» line and the parent is pulled in: another read per page, duplicates a row |

## 9. Prototype

`prototype/index.html`: one static file, no server. The nine delta scenarios of the first design stay (part-loaded move menu, waiting, retrying, refused, cursor reload, deactivated project, narrowing hint, «Без команды», lane). Four new scenarios share one data set (the US-11 items, 3 rows stand for 50): the backlog, the board and «Рабочие элементы» with the switch, and the team level, with level buttons to see the offer (organization, department, team without a department, team with one, project), the default and the memory per level, the two hints, the move menu by project only, lanes with «Без команды», and the flat list with the pager and sorting against the grouped one with «Показать ещё».
