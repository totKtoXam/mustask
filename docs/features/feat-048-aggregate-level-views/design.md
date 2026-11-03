# Design: Aggregate level views (feat-048)

Stage 3 artifact, a **delta** on [feat-043's design § 12](../feat-043-area-project-team-navigation/design.md) (approved at feat-043's gate-2). § 12 fixed and is **not reopened here**: one MUI `Accordion` per project ordered by name then key; the summary (name, `Tag` key, count «40» / «3 из 40»); first group open on first visit; «Показать ещё» (next 50); «Свернуть все» and no «Развернуть все»; the board's swimlanes and 50-card cells; moves through the existing native drag and drop of `BacklogRow` and `BoardColumns` (whole row the source, grip an `aria-hidden` hint, drop line) and the keyboard path «Ещё действия», «Переместить» / «Переместить в колонку»; a drop in another group refused; the 4-in-flight cap per tab; open-state memory (10 at most). This file adds what is left. Spec: [`spec.md`](spec.md) (gate-1 approved, OQ-1..OQ-6 all A); rules BR-NV-12, BR-NV-17, BR-NV-20 are in feat-043's `spec.md`. Every string in «» is final. The reference tracker is never named.

- Prototype: [`prototype/index.html`](prototype/index.html), the delta states only (§ 8). feat-043's prototype keeps its «feat-048» placeholders.
- ADRs relied on: [ADR-0014](../../adr/0014-server-side-registry-paging-envelope.md)/[0015](../../adr/0015-gridify-for-list-queries.md) (the page envelope stays on lists), **[ADR-0036](../../adr/0036-cursor-paged-group-reads-and-read-limiter.md)** (proposed: the cursor and the limiter), [ADR-0032](../../adr/0032-object-pages-and-navigation-context.md) (selectors, menu table), [ADR-0005](../../adr/0005-stable-error-code-registry.md).

## 1. Reuse first; one new part

| Wanted | Verdict |
|---|---|
| Organization / department / team «Рабочие элементы» | **Not new.** The project's `WorkItemsPage` (feat-003: `PageHeader`, `FilterBar`, `DataTable`, `TablePager`, `StateFrame`) takes a scope (organization, department, team, project) and two more columns (§ 2) |
| Group (backlog) and lane (board) | **One new feature part, `ProjectGroup`** in `features/boards/components` (not the kit: one feature uses it twice). MUI `Accordion` (`slotProps.heading` an `h2`) + `AccordionSummary` (name, `Tag`, count) + `AccordionDetails`. It owns the group's read states (§ 3) so the backlog's `BacklogTable` and the board's lane draw only rows and cells. Justified: § 12 names `Accordion` but no part owns the states four screens (backlog, board; organization, department) would otherwise copy |
| Read queue, retry, cursor, 429 backoff | **A hook, `useGroupRead`**, not a part: the 4-per-tab queue, the `Retry-After` retry (slot released while waiting), the cursor, the held-row set by id |
| Group's loading / empty / error | Kit: `TableSkeleton` (in `RegistrySkeleton`'s family) / `BoardSkeleton`, `InlineEmpty`, `InlineNotice`, `ErrorBanner` (compact), `StatusRegion` (the boards feature's live region) |
| «Показать ещё», «Повторить», «Свернуть все» | `ActionButton` (text variant); «Свернуть все» in the `PageHeader` bar |
| Move menu | The shipped `MoreActionsMenu` with `BacklogRow`'s «Переместить» items |
| Notices | `useNotice` for the deactivated project and a refused move; `InlineNotice` for the Q-9 hint |
| «Без команды» | MUI `FormControlLabel` + `Checkbox` in the `FilterBar` slot where `TeamFilter` stood; no new kit part |

## 2. «Рабочие элементы»: organization, department, team

Menu entry and address as feat-043's menu table (`workitems`, `areas/:a/workitems`, `teams/:t/workitems`). The project's list page serves all four levels; the **project level keeps today's page** except for one control (below).

- **Layout:** `PageHeader` (title = the level's object: the organization, the department, the team; no create command here, BR-NV-12), `FilterBar` (feat-003's filters: keyword, state, «Исполнитель», type, level), `DataTable` «Рабочие элементы», `TablePager` (50 a page, total). Columns: the shipped ones, plus «Проект» (link: sets the project, ADR-0032) and «Команда» (the owning team or «Без команды» as plain text). Order and total: change time, then number, then project key (BR-NV-12). The «Команда» filter is **absent** on the bar at every level of this line: the selector is the one state (Q-6 A).
- **Title `Tag`** (§ 4 of feat-043): «сужено: команда MARS, департамент Логистика». It names the selectors that narrow and are not the page's own object: at the organization, any of «команда …», «департамент …» (that order); at a department, «команда …» only (the title already names the department); at a team, «департамент …» only. Nothing narrowing, no `Tag`.
- **The project list's control** (stated requirement of `spec.md`): `TeamFilter` goes, **«Без команды»** takes its place (`Checkbox`, label «Без команды»; sent as `teamId=none`). Exclusive with the selector: switching it on while a team is chosen resets the selector to «все» with the shell's notice «Выбор сброшен: команда MARS.» (the shipped format, `resetNotice`); choosing a team while it is on switches it off without a notice (the control visibly clears). The project's backlog and board keep `ViewFilterRow`'s «Команда» until feat-013; recorded.
- **States** (one frame, `StateFrame`): loading `TableSkeleton`, one `role="status"` «Загрузка данных»; error `ErrorBanner` «Повторить»; empty with nothing chosen `InlineEmpty` «Рабочих элементов пока нет»; empty after a filter or selector «Ничего не найдено» with «Сбросить фильтры» when a filter is set; refused level: the catalogue's. The keyword read may take up to 5 s (budget): the table keeps its skeleton, no spinner text. These lists are single reads: no 429 here (OQ-2).
- **Permissions:** all roles read; there are no row commands. A non-member: «Организация не найдена» (§ 4 of feat-043).
- **Narrow widths:** the table scrolls inside its card; «Проект» is the second column, so it stays visible; filters wrap.

## 3. A group of «Бэклог» and a lane of «Канбан»: all states

One header read gives the groups (count «40»; narrowed «3 из 40» when a selector narrows; a filter is not built here, OQ-5). The group's own states live in `AccordionDetails`; the **header keeps its name, key and count in every state**, so the page never jumps.

| State | The user sees | Rules |
|---|---|---|
| Closed | Summary only | No read |
| Waiting for a slot (a restored set of 10 opens, 4 at a time) | Skeleton rows, `role="status"` «Загрузка элементов проекта» | Starts when one of the tab's 4 is free |
| Loading, first page | The same skeleton (5 rows; a board cell 3 cards) | — |
| Loaded | Rows or cards in the project's rank order, then «Показать ещё» while `nextCursor` exists; nothing at the end | 50 a page; a row already held is dropped by id (BR-NV-20) |
| Loading more | «Показать ещё» disabled, `CircularProgress` inside, rows stay | — |
| Empty (the group emptied meanwhile: its last item moved or deleted) | `InlineEmpty` «В проекте больше нет элементов этого уровня» | Headers re-read |
| **Refused, retrying** (429) | The loading state plus a caption «Сервис занят, пробуем ещё раз…» (`aria-live="polite"`) | Wait `Retry-After` (else 1 s, doubling), at most 3 tries; **the group releases its slot of the tab's 4 while it waits** (OQ-1) |
| **Refused, last try failed** | Inline `ErrorBanner` (compact) «Не удалось загрузить элементы проекта. Сервис занят, попробуйте ещё раз.» with «Повторить» (starts a fresh run of tries); other groups unaffected | First page only |
| **Refused on «Показать ещё»** | Rows kept; «Показать ещё» shown again and an `InlineNotice` (error) under the group with the same message | (OQ-4.2) |
| Other read error (not 429, not a cursor 400) | `ErrorBanner` (compact) «Не удалось загрузить элементы проекта.» with «Повторить»; rows held stay | A 400 naming a level or filter is this state too |
| **Cursor refused** (400 naming `cursor`) | The loading state: rows dropped, first page read again, headers re-read. **No message** | One automatic reload per «Показать ещё»; a second 400 in a row is the error state above |
| **Project deactivated meanwhile** («Проект не найден» on a page, a row move or a card move) | The group or lane disappears; headers re-read; `useNotice` «Проект больше недоступен: группа скрыта.» | A header read that then fails is the page's `ErrorBanner` «Повторить» |
| Whole page: no group at all | Backlog `InlineEmpty` «В бэклоге пока нет элементов»; board «На доске пока нет элементов»; narrowed by a selector «Ничего не найдено» | — |
| Headers read fails | `ErrorBanner` «Повторить» in place of the groups | One read, no 429 (OQ-2) |

**Permissions:** «Читатель» sees groups and «Показать ещё» but no grip, no «Переместить», no «Переместить в колонку» (the menu has no such items; with none left the row has no «Ещё действия»). «Администратор» and «Участник»: moves where the project's own board or backlog allows. A move refused by 403 (the project's code, until mod-019) shows the catalogue's text through `useNotice`; the row or card stays.

**Accessibility:** the summary is the accordion button (`aria-expanded`, `aria-controls`); the group is an `h2` named by the project; the count is read as shown («3 из 40»). A page of «Показать ещё» is announced through `StatusRegion` («Загружено ещё элементов: 50»); focus stays on the button, or on the first new row when the button leaves. Colour never alone.

**Narrow widths:** the summary wraps (key `Tag` and count under the name below 640 px); lanes scroll horizontally **inside the lane** with the lane header fixed on the left; «Свернуть все» moves into «Ещё действия».

## 4. Narrowed groups and the Q-9 hint

When a selector narrows (a team with a department, or a team with the organization/department level), the backlog groups show their rows in the project's rank order with **no grip and no move items**; the hint is **one** `InlineNotice` (info) under the page header, not one per group: «Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.» The title `Tag` is «сужено: …» (§ 2). The board is not reordered, so its card moves between columns stay available while narrowed, without the hint. This also applies to feat-040's team backlog narrowed by a department (BR-NV-17 (2)): same groups, same hint.

## 5. Moves in a group that is only part loaded (OQ-6 A)

The menu is the shipped «Переместить» (items «В начало», «Выше», «Ниже», «В конец»); a group holds the pages read so far (`loaded` rows of `count`).

| Item / target | Group fully loaded | Group part loaded (`nextCursor` present) |
|---|---|---|
| «В начало» | enabled except on the first row | same |
| «Выше» | enabled except on the first row | same |
| «Ниже» | enabled except on the last row | enabled except on the **last loaded row**, where it is **disabled**, with the secondary line «Сначала нажмите «Показать ещё»» in the item |
| «В конец» | the end of the group | **the end of the project's backlog**: the row leaves the loaded set, the count is unchanged, and `useNotice` says «Элемент перенесён в конец бэклога проекта. Он появится после «Показать ещё».» The row appears once, when the pages reach it, never twice (dropped by id) and never lost |
| Drop between loaded rows | drop line | same |
| Drop below the last loaded row | the end is a target | **not a target** (not-allowed cursor, no line) until `nextCursor` is absent |

After a move the group updates locally, with no re-read of the held pages. A 409 `boards.backlog.reorder_stale` (the shipped text «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.») reloads that group from the first page. A 403 or «Проект не найден»: § 3.

**Board:** a card dragged or moved to another column inside its lane leaves its cell (counts of both columns adjust by one) and the **target cell is read again from its first page** (one cell read, under the limiter), so the card is shown where its rank puts it, or not yet, when the cell is part loaded. No reorder on the board.

## 6. For the developer: the server side owed by this design

**Reads (additive; the api-contract is the backend developer's).** Checked in `src/backend`: the project's `GET …/projects/{p}/backlogs/{level}` and `…/boards/{level}` return the whole view; `GET …/projects/{p}/workitems` is the page-enveloped list; there is **no** `GET /api/organizations/{o}/workitems` (feat-043 added only `overview` and `selector-offers`), so the organization-wide list is new here.

| Read | Shape (input) |
|---|---|
| Organization-wide list `GET /api/organizations/{o}/workitems` | The project list's parameters (feat-003) plus repeated `teamId` (incl. `none` only on the project's list), `departmentId`; the page envelope of ADR-0015 (`page`, `pageSize`, `total`); each row adds `project {id,key,name}` and `team`. Also `…/workitems/assignee-options` for the «Исполнитель» filter (2 s). The team level is the same read with `teamId` |
| Grouped count, backlog `GET …/backlogs/{level}/groups` | `?departmentId=&teamId=` + feat-003's filter parameters (OQ-5); `{groups:[{projectId,key,name,count,total}]}` ordered by name then key; a project with `count` 0 is absent; one statement |
| Grouped count, board `GET …/boards/{level}/groups` | Same, `counts` per state per group and per column |
| Group read `GET …/projects/{p}/backlogs/{level}/rows` | `?limit=50&cursor=` + the same filters; `{rows:[…display order, depth/parent as the shipped backlog…], nextCursor}` (absent at the end); no `total` |
| Cell read `GET …/projects/{p}/boards/{level}/cells` | `?stateId=&limit=50&cursor=` + filters; `{cards:[…], nextCursor}` |

Every read takes the caller (BR-NV-15). `limit` is 1..50 (default 50), otherwise the validation 400 naming `limit`.

**The cursor** (ADR-0036): an opaque string, forward only; it names a **position in the rank order** (the last row's rank key and id), so a row moved or deleted behind it never breaks it. It also holds a fingerprint of the project, level, view and filter parameters: a malformed or foreign cursor (another project, level or filter set) is the existing validation 400 `boards.validation_failed` with the field `cursor` and reason `boards.invalid_cursor`.

**The limiter** (ADR-0036), on the group read and the cell read only (and so their «Показать ещё», OQ-2); not on the grouped count, the lists or the keyword read (accepted risk, OQ-2):

| | Value |
|---|---|
| Key | the Keycloak subject (`sub`), across organizations, tabs and devices; per API process |
| Per user | **6** in flight, **queue 6**, waiting at most **3 s** |
| Global (per process) | **40** in flight, **queue 20**, waiting at most 3 s (Npgsql's default pool is 100; 60 stay for everything else) |
| Full (queue full or the wait ran out) | **429** `http.too_many_requests`, header `Retry-After: 2` |
| Held | from the start of the read until its connection is released |

Two tabs restoring 10 groups each are 8 reads: 6 run, 2 wait a moment, none refused (the NFR of `spec.md`, tested with a fake clock: the 8 pass, a 13th concurrent read of one user is refused, 41 global in flight with a 21st queued are refused). The panel's 4-per-tab cap is unchanged. The BFF (ADR-0022) must pass `Retry-After` through.

**Error catalogue (the entries are the backend developer's):**

| Code | Status | Russian | Kazakh (needs review) |
|---|---|---|---|
| `http.too_many_requests` (file `http.json`, no params) | 429 | Сервис сейчас занят. Повторите запрос через несколько секунд. | Қызмет қазір бос емес. Сұранысты бірнеше секундтан кейін қайталаңыз. |
| `boards.invalid_cursor` (file `boards.json`, a field reason, no params) | 400 | Некорректный курсор страницы. | Бет курсоры дұрыс емес. |

The panel shows the group copy of § 3, never the catalogue text, for a 429; the catalogue text serves other clients.

## 7. Decisions for gate 2

| # | Question | Options |
|---|---|---|
| D-1 | Where the Q-9 hint sits on narrowed aggregate backlogs | **(A, recommended)** once under the header (§ 4). (B) in every group: ten identical notices on a restored page |
| D-2 | The limiter's numbers (the human fixed the shape; the numbers are the design's) | **(A, recommended)** per user 6 + queue 6 + 3 s; global 40 + queue 20; `Retry-After: 2`. (B) per user 6, queue 2: 429 sooner, less memory held; a third tab is refused at once. (C) global 60: less headroom under the pool of 100 |
| D-3 | A card moved into a part-loaded cell | **(A, recommended)** read the target cell again from its first page (§ 5). (B) put the card on top of the cell locally: cheaper, but the card shows in a wrong place until reload |
| D-4 | «Рабочие элементы» at team level shows «Команда» | **(A, recommended)** same columns at every level but the project's (one variant). (B) hide the constant «Команда» at team level: one more variant to test |

## 8. Prototype

`prototype/index.html`: a single static file, no server. A scenario list drives one mock «Бэклог» (and one «Канбан» lane, one list): part-loaded group with the move menu (disabled «Ниже», «В конец» notice), drop targets, waiting and retrying, refused after the last try, refused on «Показать ещё», cursor reload, deactivated project, narrowed groups with the hint, «Без команды» with the exclusive reset. Counts are scaled (5 rows stand for 50).
