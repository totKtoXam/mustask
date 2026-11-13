# Design: Global search palette and the full search page (feat-065)

Stage 3 artifact. Rules (BR-GS-n) and scenarios are in `spec.md` and `stories/`; this file is layout, MUI components, states, keyboard and the final Russian copy. ◆ = copy new in this design (the spec quotes the rest).

- Base: [doc-016 design § 3](../doc-016-teams-under-departments/design.md) (the header, the context path panel: its look and its open/close behaviour), [feat-043](../feat-043-area-project-team-navigation/design.md) (`SearchMenu`, frames), [feat-003 § 4-5](../feat-003-work-item-search/design.md) (rows and list states), [ui-kit.md](../../conventions/ui-kit.md).
- Prototype: `prototype/index.html`, one file, no network, opens by double-click. A bar on top switches the user (Максим: sees everything, two organizations; Ольга: sees only «Альфа», one organization; Вера: one organization), the state (norma, loading, error, organization not found, empty result) and a narrow view. Data: «Ромашка», «Другая» (with a second project key ALP). § 9 lists the scenarios.

## 1. Reuse and what the kit lacks (R-23)

| Wanted | Verdict |
|---|---|
| Header field | **New kit part `SearchField`** (`shared/ui/fields`): a 32 px field in the header, between the context path and the user menu: leading `ThinSearchIcon`, `InputBase` (`role="combobox"`), a `kbd` hint chip «Ctrl K» / «⌘K» while empty, a clear ✕ (`IconAction`) with text; resting fill `action.hover`, on focus white with the accent frame (the kit's "focus is loud"). The kit's inline fields have no leading icon or hint, so the bare input is kit-internal. Max width 520 px, centred in the free header space |
| Header magnifier (narrow) | `IconAction size="header"`, `label` «Поиск (Ctrl+K)» (the tooltip and the name; «Поиск (⌘K)» on macOS), icon: a new thin `ThinSearchIcon` in `shared/ui/icons`; shown only below 700 px, in place of the field |
| The panel | **New kit part `SearchPanel`** (`shared/ui/dialog`): MUI `Popper` (not `Popover`, not `Dialog`) with the kit's `Surface` look (white, 4 px radius, the soft glow of the context panel, no backdrop), `ClickAwayListener`, anchored to the `SearchField`. Props: `anchorEl`, `open`, `onClose`, `scope` (slot), `fullSearch` (slot), `children` (the listbox), `keys` (footer hints), `busy`, `compact`. **Why `Popper`:** the context panel is a `Popover`, which is a `Modal` and takes the focus; here the user keeps typing in the header field, so the popup must be non-modal and leave focus in the input (the ARIA 1.2 combobox with a listbox popup). The look and the open/close behaviour (Esc or click outside closes, no dimmed page) are the context panel's |
| Rows | **New kit part `SearchOption`**: `ListItemButton` (`component="a"`, a real link, `role="option"`, `tabIndex=-1`) with icon, one primary and one secondary line, an optional 4 px colour stripe, a `↵` hint on the active row; plain variant (`more`) with no link |
| Match emphasis | **New kit part `HighlightedText`** (`shared/ui/text`): `text`, `ranges` or `query`; renders `<mark>` spans from text, never from markup. Bold plus a tint token, so colour is not the only carrier |
| Groups | `List` (`role="listbox"`), per group `ListSubheader` + a `role="group"` list; ◆ «Похожие» is a small italic caption row |
| Scope | `MenuButton` (selection `radio`): the user's organizations, a divider, «Все организации» (only with two or more active memberships, BR-GS-19); one organization: a read-only `Tag` |
| Messages | `InlineNotice` (error with «Повторить»), `InlineEmpty`, `FieldsSkeleton` rows, `LiveRegion` |
| Full page | `PageHeader`, `InlineTextField` (frame drawn, label hidden) + `ActionButton primary` «Найти», the scope `MenuButton`, `Surface` with a `List`, `TablePager` (20 a page), `StateFrame`, `ErrorBanner`, `NotFound`, `FieldsSkeleton` |

Theme additions, part of the kit line `feat-NNN-search-dialog-kit`, not of any screen: `layout.searchField.width` (520), `layout.searchPanel.width` (720) and `.maxHeight` (70 vh), `layout.searchOption.height` (48), and `palette` token `search.match` (the tint). Each new part gets its tests, a showcase section on `/ui-kit` and a guide block (the kit rule).

## 2. Screen map

| Screen | Where | Stories |
|---|---|---|
| Header search field (magnifier below 700 px) | every signed-in page, in the middle of the header between the context path and the user menu | US-1 |
| Search panel: idle (recent), hint, loading, results, no results, error, organization not found | dropdown under the field, over the page | US-1..US-6, US-8 |
| Full search page «Поиск» | `/organizations/:organizationId/search?q=<text>&page=<n>&org=<id or all>`; not in the main navigation, reached from the panel's last row, its «Полный поиск» button, or by address | US-7, US-8 |

```
Header  [▣ MUSTASK] [Ромашка › все ⌄]   [🔍 Поиск…            Ctrl K]            (МП)
                                          +--------------------------------------+
                                          | panel: scope row, rows, key hints    |
```

No organization at all: no field, no magnifier, no shortcut. The panel never covers the field: it opens below the header.

## 3. The search panel

### 3.1 Layout

```
 [🔍 оплата                          ✕ ]      the header field = the combobox (typing happens here)
 +------------------------------------------------------+
 | Искать в: [Ромашка ⌄]                  🔍 Полный поиск |  scope row (permanent)
 | Недавние                          Очистить недавние   |  recent heading row (idle only)
 | ▌ icon  ALP-US-7 Оплата картой  · Ромашка        ↵   |  active row: tint + 3 px accent edge
 |         В работе · Альфа · Ромашка                    |  ("· Ромашка" only in «Все организации»)
 |   ⌄ Показать ещё                                      |
 | ------------------------------------------------------|
 | 🔍 Искать везде, включая описания                     |
 |    «оплата»                                           |
 | ↑↓ выбрать  ↵ открыть  Ctrl+↵ в новой вкладке  Esc    |  key hints
 +------------------------------------------------------+
```

- **Position (D-1 superseded).** The human replaced the centred modal with a field in the header and a dropdown panel at gate-2, 2026-11-13. The panel is a `Popper` (§ 1), anchored under the field (`bottom-start`, 4 px gap, left edge on the field's left edge), width `max(field, min(720 px, 100vw − 32 px))`, kept inside the viewport (`preventOverflow`), no flip; at most 70 vh high: the body scrolls, the scope row and key hints do not. No backdrop; the page stays visible.
- **Narrow (< 700 px, breakpoint D-2).** The field collapses to the magnifier `IconAction`. Clicking it turns the header into the field: the field takes the header's full width (the product mark, path, user menu are hidden while it is open), with a «Закрыть поиск» ✕ at its right; the panel goes full width right under the header, no radius, height up to the viewport's bottom; the key-hint footer is hidden (no hardware keys assumed). Why: at 640 px the header has no room for a 520 px field; full width gives typing room and readable rows.
- **Open and close (as the context panel, § 3.0).** Opens on click or focus of the field and on Ctrl/Cmd+K (which focuses it). Closes on Esc (the text stays in the field — D-11, the human at gate-2; ✕ clears it — focus returns to the element that had it before, else leaves the field), on a click outside, on choosing a row, and when focus leaves the field and the panel (Tab out). Result rows are never Tab stops; the panel's own controls are (§ 3.6).
- **Row** (BR-GS-11): type icon (the tree's, ADR-0024 tokens), primary line with the match emphasised, grey secondary line, `↵` on the active row. Department, team and project rows carry the colour stripe of doc-016 § 3.0 (4 px, 60 % of the row height, at the left of the icon); other types keep an empty spacer. A long line is cut with an ellipsis (`TruncatedText`) and the row's `title` gives the whole text. In «Все организации» every secondary line ends with « · <organization>» (a department's line is the organization alone).

| Type | Primary | Secondary |
|---|---|---|
| Рабочий элемент | **MSK-42** Оплата картой | В работе · Альфа |
| Проект | Альфа | ALP · Логистика |
| Команда | VENUS | Логистика |
| Департамент | Логистика | - |
| Область | Приёмка | Альфа · Логистика |
| Пользователь | Максим Петров (неактивен) | max.petrov@romashka.example |

### 3.2 Groups, ranking and "more" (BR-GS-8, -9)

Group headings, in the fixed order of ties: «Рабочие элементы», «Проекты», «Команды», «Департаменты», «Области», «Пользователи»; the order follows the best row's tier, so a query that exactly names an area puts «Области» first (prototype: «оплата»). An empty group has no heading. No heading, row or live region ever carries a total (BR-GS-5).

- **«Показать ещё»**: the last row of a group with more than 5 visible matches, a plain option with a chevron, in the listbox, so ↓ reaches it. Enter or click expands that group in place to at most 20; while that read runs the row reads «Загрузка…» and the rest of the list stays; the active row then becomes the first new row. No further paging.
- **Last row «Искать везде, включая описания»** (BR-GS-9, -18): always last, separated by a hairline, search icon, the typed text quoted in the secondary line as plain text; a real link to the full search page; shown whenever the query is valid and the read did not fail, also when nothing matched (then it is the only row, and active).
- **Typo-tolerant rows (BR-GS-17) are marked (D-3).** After the tier 1-4 rows of a group a caption «Похожие» (11 px, grey, italic, `role="presentation"`) opens the similar rows; they carry no emphasis. When a group has only similar rows its heading is followed at once by that caption. Reason: without the cue «лагистика» → «Логистика» reads as an exact hit; the caption costs one 16 px line and is read as part of the group.

### 3.3 States (BR-GS-10, -15, -16)

| # | State | Body | Live region (polite) |
|---|---|---|---|
| 1 | Idle, nothing opened yet | «Здесь появятся недавно открытые объекты» | - |
| 2 | Idle, recent items | group «Недавние» (≤ 10, newest first; secondary line starts with the type word: «Проект · ALP · Логистика»); the heading row ends with the text button «Очистить недавние» (D-10: not an option, arrows skip it, Tab reaches it; clears at once, no confirmation, notice «Недавние очищены.» via `useNotice`) | «Недавние: 4» |
| 3 | Recent resolving | 3 `FieldsSkeleton` rows, `aria-busy` | - |
| 4 | 1 character | «Введите не менее 2 символов» (no read, no results, recent hidden) | the same text |
| 5 | First read pending | 3 skeleton rows | - |
| 6 | A later read pending | the previous rows stay, dimmed (`aria-busy`, opacity .55), the active row kept; **Enter and clicks on result rows are inert until the answer** (a stale row is never opened); Esc, the field and the scope work | - |
| 7 | Results | groups, first row active | «Найдено: 7» (rows shown, not «more» or last-row options; never a total) |
| 8 | Nothing found | «Ничего не найдено» + the last row «Искать везде…» | «Ничего не найдено» |
| 9 | Error, 429 after 3 tries, offline, failed recent resolution | `InlineNotice` error, `role="alert"`: «Не удалось выполнить поиск. Попробуйте ещё раз.» + «Повторить» (ActionButton); the field keeps its text; the last row is not shown; focus stays in the field | the alert |
| 10 | 404 (not a member any more) | `InlineNotice` «Организация не найдена.», no retry; the scope menu stays usable to pick another organization | the alert |
| 11 | 429 being retried | as state 5 (skeleton) until the third try fails, then state 9 | - |

Permissions: no per-role variants; every member sees the same shapes, filled by sight (BR-GS-4). The «Пользователи» group is read under `organizations.membership.read`; a caller without the code simply has no such group (no hint). There is no «нет доступа» state in the palette.

### 3.4 Query field (BR-GS-10)

Placeholder ◆ «Поиск…» with the hint chip «Ctrl K» («⌘K») inside the field while it is empty; `aria-label` «Поиск»; `maxLength` 100 (the field stops, no error text); a clear ✕ «Очистить поле» appears with text and returns focus to the field, showing the idle state. Debounce 200 ms, stale answers dropped, no read during an IME composition, Enter during a composition commits the composition and chooses nothing. Text is trimmed for reading; the field keeps what was typed.

**Transport (BR-GS-10; for the backend line and the ADR).** The text goes in a **POST body** (`POST …/organizations/{id}/search`, a read, no write), never in the query string, so the API's request-line log never holds it; the full page's address carries `q` (a panel address, not the API's) and the page reads by the same POST. **Limiter** (ADR-0036's shape, numbers are this design's): per user 3 in flight, wait queue 3; per process 24 in flight, queue 12; wait at most 1 s; then 429 `http.too_many_requests`, `Retry-After: 1`. The panel retries by `Retry-After`, 3 tries. Reason: a search is short and the debounce makes it one per pause, so the limits are tighter than the group reads' (6 / 40).

### 3.5 Scope row (BR-GS-2, -19) and the permanent full-search button

One row at the top of the panel, always visible (every state, the empty field included).

- **Left: «Искать в:» and the scope `MenuButton`**, «Ромашка ⌄», radio selection: the user's organizations by name; for a user with **two or more active memberships** a divider and a third kind of choice **«Все организации»** (Максим in the prototype has it; Ольга and Вера have one organization and see a read-only `Tag`). The default at every opening is the current organization (BR-GS-2); the choice lasts until the panel closes. Picking clears the results, re-reads the current text, resets expansions, keeps focus in the field, changes no header selector.
- **«Все организации» display (BR-GS-19):** groups stay by type; each row's secondary line ends with its organization's name; same key or identifier in two organizations are two rows told apart by that name (prototype: `ALP-US-7` in «Ромашка» and in «Другая»); ranking puts the current organization first among equal tiers; the 5-row group limit and «Показать ещё» (20) are shared by all organizations. **Idle in this mode:** «Недавние» of the current organization only, rows without the organization suffix (a recent list is not a search result). Choosing a row of another organization opens it there, switches the current organization and the header path (`Другая › Склад › Зет`), no «Выбор сброшен» notice; the object goes to that organization's recent list. Errors and 404 are the one-organization texts.
- **Right: the permanent button «Полный поиск»** (`ActionButton tone="subtle"`, thin search icon; tooltip and `aria-label` «Искать везде, включая описания»). It opens the full search page with the current text (or none) and the current scope, and closes the panel. **Why here (D-11):** the scope row is the panel's only fixed strip that exists in every state and on every width (the key-hint footer is hidden on narrow screens, the field row belongs to the header field with its ✕), and the button sits next to the scope it hands over to the page. The last row «Искать везде, включая описания» under results stays as the keyboard's one-key route; the button is for the empty field and for the mouse. **Shortcut: none (D-11).** Ctrl/Cmd+Enter is «new tab» (US-1); Shift+Enter is the line-break/IME convention and Alt+Enter is taken by the OS on Windows, so any chord would collide; keyboard users reach the page by ↓ to the last row or by Tab to the button.

### 3.6 Keyboard and focus (US-1, BR-GS-16)

The focus stays in the header field; options never take DOM focus. The popup is non-modal: no focus trap, no `aria-modal`.

| Key | Effect |
|---|---|
| Ctrl/Cmd+K | focuses the field and opens the panel; toggles it closed when open; browser binding cancelled. Not inside the rich-text editor (its link command stays; clicking the field or the magnifier still opens it); **nothing while another modal dialog is open (R-18 kept)**, and a modal that opens while the panel is open closes the panel. Works from a plain text field without changing its text |
| ↓ / ↑ | next / previous option across groups, skipping headings, the «Похожие» caption and the «Очистить недавние» button; wraps; the active option scrolls into view (`block: nearest`) |
| Enter | chooses the active option (§ 3.7); inert while a newer read is pending and during IME composition |
| Ctrl/Cmd+Enter | opens the active row in a new tab (its real address), the panel stays open, the palette records nothing |
| Esc | closes at any time, the text stays (D-11); with the scope menu open it closes the menu only. Focus returns to the element that had it before opening (the magnifier on a narrow screen), else the field is left |
| Tab / Shift+Tab | field → clear ✕ → scope → «Очистить недавние» (idle) → «Полный поиск» → (narrow: «Закрыть поиск») → leaves the panel, which closes it. Rows are not Tab stops |
| Home / End | stay in the field (caret) |

Mouse: moving over a row makes it active (one highlight only); click chooses; middle click and Ctrl/Shift click use the link; a mouse press inside the panel never takes the focus from the field (except on its own controls).

ARIA: field `role="combobox"`, `aria-label` «Поиск», `aria-keyshortcuts`, `aria-expanded` (true while the panel is open), `aria-controls` the listbox, `aria-autocomplete="list"`, `aria-activedescendant` the active option's id; listbox `aria-label` «Результаты поиска» (idle: labelled by the «Недавние» heading); each group `role="group"` labelled by its heading; options `aria-selected`; the busy body `aria-busy`; a polite live region (§ 3.3). The scope button and the two buttons are plain buttons beside the listbox, not inside it, which keeps the listbox's children options and groups only. Active row: tint plus the 3 px accent edge (not colour alone).

### 3.7 Choosing a row (BR-GS-12; the rules are US-5's)

Enter or click: record the opening, close the panel, navigate; focus goes to the opened page's `h1`. Department, team, project, sub-area: context and its notice as doc-016 § 3.3 and the object's own page; work item: its card; user: the organization's «Рабочие элементы» filtered by «Исполнитель» (an empty list is the list's own answer, R-10); the last row or «Полный поиск»: the full page. Recording is a route-level hook (BR-GS-13), not a click handler; users are never recorded.

## 4. The full search page (US-7; BR-GS-18)

### 4.1 Layout

```
Ромашка / Поиск                               (PageHeader trail)
Поиск                                          (h1)
Искать в: [Все организации ⌄]
[ оплата картой шаг                      ] [Найти]   (InlineTextField, frame drawn)
Найдено: 45
+---------------------------------------------------------------+
| ▤ ALP-US-7 Оплата картой                                      |  title is the link to the card
|   В работе · Альфа                                            |
|   … повторная <b>попытка</b> <b>списания</b> …               |  snippet (grey, 13 px, ≤ 2 lines)
| ...20 rows                                                    |
+---------------------------------------------------------------+
                                            1–20 из 45  ‹  ›    (TablePager)
```

- `PageHeader` with the title «Поиск» as the page's only `h1`; the trail «<организация> / Поиск» is the shell's. The page has no primary action in the header: «Найти» beside the field is the page's one primary `ActionButton`.
- **Field.** `InlineTextField` (label hidden, `aria-label` «Поиск», `maxLength` 100) with the text from the address; Enter or «Найти» replaces the results, writes `q` and page 1 into the address (a history step, so Back returns to the previous query), keeps focus in the field. Placeholder ◆ «Номер, название или текст описания». Under 2 characters: no read, text ◆ «Введите не менее 2 символов» (`role="status"`) in place of the results; an empty field: ◆ «Введите запрос: номер, слово из названия или описания.»
- **Scope** (US-8). Under the title «Искать в:» and the same scope `MenuButton` as the panel's (§ 3.5). Changing it rewrites `org` in the address (an id, or `all`) and goes to page 1; in «Все организации» every row's grey line ends with « · <organization>» and the count and pager cover all of them; the server reads the memberships itself and never takes organization ids from the client. A user with one organization sees a read-only `Tag`.
- **Count** (BR-GS-18): «Найдено: 45», `role="status"`, only the visible matches; hidden ones are never counted.
- **Row**: `Surface` + `List`; work item icon, **identifier** and title as one link to the card, grey line «Состояние · Проект» (BR-GS-11); for a description match the **snippet**: about 140 characters around the first match, `…` at a cut end, matching words in `HighlightedText`. The snippet and its matches come from the server as **plain text plus ranges** (`snippet: string`, `matches: [{start, length}]`, contract of the page's line); the client builds spans from them, never parses markup, so a description containing `<script>` shows those characters as text (prototype: «шаблон»). Titles and identifiers are not emphasised on this page (stems differ from the typed word; the server judges). A title or identifier match with a description that holds no query word shows no snippet.
- **Paging.** `TablePager`: 20 rows, «1–20 из 45», two arrows, the same order across pages (BR-GS-18). A page change keeps the rows, `aria-busy`, until the next arrive; address gets `page`; focus stays on the arrow. A page past the last (count > 0): ◆ «На этой странице результатов нет» with the link-button «К первой странице».
- **From the panel**: the panel closes, the page opens with the text in the field (or empty) and the results shown, in the panel's scope; Back returns to the page the user came from.
- **Narrow**: the field and «Найти» stay on one line down to 640 px, rows wrap; the pager stays right-aligned. Nothing scrolls sideways.

### 4.2 States

| # | State | The user sees |
|---|---|---|
| 1 | Pending | field at once; 4 `FieldsSkeleton` rows (title, line, snippet) |
| 2 | Results | count, rows, pager |
| 3 | A page change pending | previous rows stay, `aria-busy` |
| 4 | Nothing found | `StateFrame` ◆ «Ничего не найдено» / «Ни один рабочий элемент не подходит под запрос. Проверьте написание или упростите запрос.»; field stays |
| 5 | Error, 429 after 3 tries | `ErrorBanner` «Не удалось выполнить поиск. Попробуйте ещё раз.» + «Повторить» |
| 6 | Not a member | `NotFound`: «Организация не найдена.» |
| 7 | Less than 2 characters | the hint above |

Permissions: none beyond sight; Читатель, Участник and Администратор see the same page, filled by what each sees.

## 5. Interface texts (final)

| Where | Text |
|---|---|
| Header field ◆ | name «Поиск»; placeholder «Поиск…»; hint «Ctrl K» / «⌘K»; «Очистить поле» |
| Magnifier (narrow) | name and tooltip «Поиск (Ctrl+K)»; macOS «Поиск (⌘K)»; «Закрыть поиск» |
| Scope ◆ | «Искать в:»; «Все организации» |
| Buttons ◆ | «Полный поиск» (tooltip «Искать везде, включая описания»); «Очистить недавние» |
| Groups | «Недавние», «Рабочие элементы», «Проекты», «Команды», «Департаменты», «Области», «Пользователи»; ◆ «Похожие» |
| Rows | «Показать ещё»; «Загрузка…»; «Искать везде, включая описания»; «(неактивен)» |
| Idle / hint | «Здесь появятся недавно открытые объекты»; «Введите не менее 2 символов» |
| None / error / 404 | «Ничего не найдено»; «Не удалось выполнить поиск. Попробуйте ещё раз.» + «Повторить»; «Организация не найдена.» |
| Live ◆ | «Найдено: 7»; «Недавние: 4» |
| Key hints ◆ | «выбрать», «открыть», «в новой вкладке», «закрыть» (keys drawn as `kbd`; ⌘ on macOS) |
| Notice ◆ | «Недавние очищены.» |
| Full page | «Поиск»; «Найти»; «Найдено: 45»; «1–20 из 45»; «Предыдущая страница», «Следующая страница»; «На этой странице результатов нет»; «К первой странице»; the other lines of § 4 |

## 6. Narrow widths and accessibility

Floor 640 CSS px (ADR-0024). Below 700 px the field collapses to the magnifier and the panel is full width (§ 3.1); from 700 px the field sits in the header and the panel is up to 720 px wide. Contrast and focus follow the kit; the active option, the emphasis and the «Похожие» caption never rely on colour alone (edge + tint, bold + tint, text). Reduced motion: the skeleton is static. The live region speaks after the answer settles, not on each keystroke.

## 7. Decisions for gate-2

| # | Decision | Default taken |
|---|---|---|
| D-1 | Palette position | **Superseded by the human at gate-2, 2026-11-13:** no centred modal and no fixed top offset; a search field in the header and a `Popper` panel under it (§ 3.1) |
| D-2 | Narrow below 700 px | the field collapses to the magnifier, the panel goes full width under the header (§ 3.1); spec floor is 640 |
| D-3 | Similar rows marked by a «Похожие» caption | marked — accepted by the human at gate-2, 2026-11-13 |
| D-4 | Enter inert while a newer read is pending | inert, not "wait then open" |
| D-5 | The last row hidden on error and 404 | hidden (BR-GS-9 says "even when nothing matched", not "even on failure") |
| D-6 | Snippet from the server as text + ranges; no emphasis on the page's titles | as § 4.1 |
| D-7 | POST for the read; limiter 3/3 per user, 24/12 per process, 1 s | as § 3.4 — accepted by the human at gate-2, 2026-11-13 |
| D-8 | Kit parts split out | split: `feat-NNN-search-dialog-kit` (S), accepted at gate-2, 2026-11-13; parts now `SearchField`, `SearchPanel`, `SearchOption`, `HighlightedText`, `ThinSearchIcon` |
| D-9 | `Popper`, non-modal, focus stays in the field (not `Popover`/`Dialog`) | § 1; the context panel's look and close behaviour, not its modal mechanism |
| D-10 | «Очистить недавние» is a text button at the right end of the «Недавние» heading, not a listbox option | human at gate-2, 2026-11-13 |
| D-11 | Esc closes the panel and keeps the typed text; ✕ clears it. The panel opens on any focus of the field (click, Tab, Ctrl/Cmd+K) | human at gate-2, 2026-11-13 |
| D-11 | Permanent «Полный поиск» button in the scope row; no shortcut | § 3.5 |
| D-12 | «Все организации» scope (BR-GS-19): current organization's recents only when idle; organization name on every row | § 3.5 |

Left to the ADR and not this design: the `word_similarity` threshold (the prototype uses 0.5 trigram overlap for its fake «лагистика»), the plain-text derivation, snippet length on the server.

## 8. For the developer

- Slice `src/frontend/src/features/search`: `api/` (one search client, aborting stale requests, `Retry-After` retries), `hooks/` (`useSearchPalette` state machine with the states of § 3.3, `useRecentItems` with the key `mustask.recent.v1.<sub>.<organizationId>`, `useOpenedObjectRecorder` mounted in the shell), `components/` (`SearchPalette`, `SearchResults`, `SearchPage`), `index.ts`. The shortcut is one `keydown` listener in `app/shell`, checking whether the event target is inside `shared/ui/RichTextEditor` (the part marks its root with an attribute for it) and whether a modal is open. The header hosts `SearchField` in `app/shell/TopBar`; the panel's open state lives in the slice, the shell only passes the anchor.
- The kit parts of § 1 are not written in the slice; `kitGuard` will fail a `Popper`, `Dialog`, `Chip` or `Alert` import there.
- Tests: the keyboard table (including wrap, group skipping, IME, Ctrl+Enter, focus return, Tab out closes, Ctrl+K with a modal open), every state of § 3.3 and § 4.2, text-never-markup for snippets, highlight by `ranges`, no total anywhere (live region, rows, count of the palette), recent per user and organization, «Все организации» rows with the organization suffix.

## 9. Prototype scenarios (all in `prototype/index.html`)

1. The header field and the panel: click or Ctrl+K opens (focus in the field), Ctrl+K toggles, Esc closes keeping the text and returns focus, a click outside and Tab out close; Ctrl+K in the plain field opens, in the «редактор» does not, with the «Новый рабочий элемент» dialog open does nothing.
2. Empty field: «Недавние» (4 rows) with «Очистить недавние» at the right of the heading (not reachable by arrows, reachable by Tab), then «Здесь появятся…»; «Заполнить недавние» restores. «Полный поиск» is visible in every state and opens the page with the current text and scope.
3. «оплата»: groups by tier, emphasis, the last row; ↑↓ across groups, wrap; Enter opens a fake page and records it; Ctrl+Enter toasts «в новой вкладке» and stays.
4. «отчёт»: «Показать ещё»; «лагистика»: «Похожие»; «MSK-42», «alp-7»; «ол»; one character → the hint.
5. Scope (Максим): «Все организации» with «оплата» and «ALP-US-7» (two rows told apart by the organization), «зет» opens «Другая» and switches the header path; Ольга and Вера have no such choice.
6. States switch: loading, error with «Повторить», organization not found, empty; «Узкий вид»: the magnifier, the field over the full header width, the full-width panel.
7. The page «Поиск»: «списание», «payment», «шаблон» (a `<script>` shown as text), «оплата» (two pages), the scope control on the page, Ольга's smaller count, the states switch, Back (browser).
