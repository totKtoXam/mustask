# Design: Global search palette and the full search page (feat-065)

Stage 3 artifact. Rules (BR-GS-n) and scenarios are in `spec.md` and `stories/`; this file is layout, MUI components, states, keyboard and the final Russian copy. ◆ = copy new in this design (the spec quotes the rest).

- Base: [doc-016 design § 3](../doc-016-teams-under-departments/design.md) (the header, the context path button, colour stripes), [feat-043](../feat-043-area-project-team-navigation/design.md) (`SearchMenu`, frames), [feat-003 § 4-5](../feat-003-work-item-search/design.md) (rows and list states), [ui-kit.md](../../conventions/ui-kit.md).
- Prototype: `prototype/index.html`, one file, no network, opens by double-click. A bar on top switches the user (Максим sees everything; Ольга sees only «Альфа», VENUS, MARS, «Логистика»; Вера has one organization), the state (norma, loading, error, organization not found, empty result) and a narrow view. Data: «Ромашка», «Другая». § 10 lists the scenarios.

## 1. Reuse and what the kit lacks (R-23)

| Wanted | Verdict |
|---|---|
| Header magnifier | `IconAction size="header"`, `label` «Поиск (Ctrl+K)» (the tooltip and the name; «Поиск (⌘K)» on macOS), icon: a new thin `ThinSearchIcon` in `shared/ui/icons` (the kit's header icons are hairlines) |
| The dialog | **New kit part `SearchDialog`** (`shared/ui/dialog`): MUI `Dialog` inside, because `DialogShell` is a titled form/card frame and the palette has no title bar. Props: `open`, `onClose`, `label` («Поиск»), `field` (the input slot), `scope` (slot), `children` (the listbox), `keys` (footer hints), `busy`, `fullScreenBelow` |
| Rows | **New kit part `SearchOption`**: `ListItemButton` (`component="a"`, a real link, `role="option"`) with icon, one primary and one secondary line, an optional 4 px colour stripe, a `↵` hint on the active row; plain variant (`more`, `clear`, `all`) with no link |
| Match emphasis | **New kit part `HighlightedText`** (`shared/ui/text`): `text`, `ranges` or `query`; renders `<mark>` spans from text, never from markup. Bold plus a tint token, so colour is not the only carrier |
| Field | `InputBase` inside `SearchDialog` (the kit's inline fields have no leading icon and clear glyph; the palette field is the one place a bare input is kit-internal), `role="combobox"` |
| Groups | `List` (`role="listbox"`), per group `ListSubheader` + a `role="group"` list; ◆ «Похожие» is a small italic caption row |
| Scope | `MenuButton` (selection `radio`) with the organizations; one organization: a read-only `Tag` |
| Messages | `InlineNotice` (error with «Повторить»), `InlineEmpty`, `FieldsSkeleton` rows, `LiveRegion` |
| Full page | `PageHeader`, `InlineTextField` (frame drawn, label hidden) + `ActionButton primary` «Найти», `Surface` with a `List`, `TablePager` (20 a page), `StateFrame`, `ErrorBanner`, `NotFound`, `FieldsSkeleton` |

Theme additions, part of the kit task and not of any screen: `layout.searchDialog.width` (640), `.offsetTop` (10 vh), `.maxHeight` (76 vh), `.optionHeight` (48), and `palette` token `search.match` (the tint). Each new part gets its tests, a showcase section on `/ui-kit` and a guide block (the kit rule). **This is the weight R-23 flagged** on `feat-NNN-search-palette`: the kit work is the first commit of that line; if it presses the M size, split it as its own S line.

## 2. Screen map

| Screen | Where | Stories |
|---|---|---|
| Header magnifier | every signed-in page, right end of the header, left of the user menu | US-1 |
| Palette: idle (recent), hint, loading, results, no results, error, organization not found | modal over any page | US-1..US-6 |
| Full search page «Поиск» | `/organizations/:organizationId/search?q=<text>&page=<n>`; not in the main navigation, reached only from the palette's last row (and by its address) | US-7 |

```
Header   [▣ MUSTASK] [Ромашка › все ⌄] ........................ [🔍] (МП)
```

The magnifier is the right-most control before the user menu; it stays visible at 640 px (the path button shortens first). No organization at all: no button, no shortcut.

## 3. The palette

### 3.1 Layout

```
        +----------------------------------------------------+
        | 🔍 [ Найти рабочий элемент, проект, ...  ] ✕  [Esc] |  field row, accent underline on focus
        | Искать в: [Ромашка ⌄]                               |  scope row
        +----------------------------------------------------+
        | Недавние / group heading                            |
        | ▌ icon  ALP-US-7 Оплата картой                 ↵    |  active row: tint + 3 px accent edge
        |         В работе · Альфа                            |
        |   ...                                               |  body scrolls, the field stays
        |   ⌄ Показать ещё                                    |
        | ----------------------------------------------------|
        | 🔍 Искать везде, включая описания                   |
        |    «оплата»                                         |
        | ↑↓ выбрать  ↵ открыть  Ctrl+↵ в новой вкладке  Esc  |  key hints
        +----------------------------------------------------+
```

- **Position (D-1).** Horizontally centred; the top edge is fixed at 10 vh (not vertically centred), so the field does not jump as the result height changes with every keystroke. Width 640 px, at most 76 vh high; the body scrolls, the field row, scope row and key hints do not.
- **Narrow (< 700 px, breakpoint D-2).** The dialog is full screen (`fullScreen`), no radius; the `Esc` hint and the key-hint footer are hidden (no hardware keys assumed), a «Закрыть» `IconAction` (✕) appears in the field row. Everything stays reachable by keyboard.
- **Backdrop.** Click on it closes. Focus is trapped (MUI `Dialog`): Tab cycles field → clear ✕ (when text) → scope → close ✕ (narrow only) → field. The listbox is not a Tab stop; rows are reached by arrows (`aria-activedescendant`).
- **Row** (BR-GS-11): type icon (the tree's, ADR-0024 tokens), primary line with the match emphasised, grey secondary line, `↵` on the active row. Department, team and project rows carry the colour stripe of doc-016 § 3.0 (4 px, 60 % of the row height, at the left of the icon); other types keep an empty spacer. A long line is cut with an ellipsis (`TruncatedText`) and the row's `title` gives the whole text.

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
- **Typo-tolerant rows (BR-GS-17) are marked (D-3).** After the tier 1-4 rows of a group a caption «Похожие» (11 px, grey, italic, `role="presentation"`) opens the similar rows; they carry no emphasis. When a group has only similar rows its heading is followed at once by that caption. Reason: without the cue a user who typed «лагистика» sees «Логистика» as if it were an exact hit and may doubt the search; the caption says «we guessed» at the price of one 16 px line. A screen reader reads the caption as part of the group (it is inside the group's list).
- A user who loses sight of a row between two reads sees the list as the server returned it; nothing is patched on the client.

### 3.3 States (BR-GS-10, -15, -16)

| # | State | Body | Live region (polite) |
|---|---|---|---|
| 1 | Idle, nothing opened yet | «Здесь появятся недавно открытые объекты» | - |
| 2 | Idle, recent items | group «Недавние» (≤ 10, newest first; secondary line starts with the type word: «Проект · ALP · Логистика»); last row «Очистить недавние» (trash icon, grey; an option, Enter or click clears at once, no confirmation, notice «Недавние очищены.» via `useNotice`) | «Недавние: 4» |
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

Placeholder ◆ «Найти рабочий элемент, проект, команду, человека…»; `aria-label` «Поиск»; `maxLength` 100 (the field stops, no error text); a clear ✕ «Очистить поле» appears with text and returns focus to the field, showing the idle state. Debounce 200 ms, stale answers dropped, no read during an IME composition, Enter during a composition commits the composition and chooses nothing. Text is trimmed for reading; the field keeps what was typed.

**Transport (BR-GS-10; for the backend line and the ADR).** The text goes in a **POST body** (`POST …/organizations/{id}/search`, a read, no write), never in the query string, so the API's request-line log never holds it; the full page's address carries `q` (a panel address, not the API's) and the page reads by the same POST. **Limiter** (ADR-0036's shape, numbers are this design's): per user 3 in flight, wait queue 3; per process 24 in flight, queue 12; wait at most 1 s; then 429 `http.too_many_requests`, `Retry-After: 1`. The panel retries by `Retry-After`, 3 tries. Reason: a search is short and the debounce makes it one per pause, so the limits are tighter than the group reads' (6 / 40).

### 3.5 Scope chip (BR-GS-2)

Row under the field: «Искать в:» and `MenuButton` «Ромашка ⌄» (radio selection, the user's organizations by name). One organization: a read-only `Tag` with the name. Picking another clears the results, re-reads the current text in the new organization, resets expansions, keeps focus in the field after the menu closes, and changes no header selector. It starts from the default of BR-GS-2 at every opening.

### 3.6 Keyboard and focus (US-1, BR-GS-16)

| Key | Effect |
|---|---|
| Ctrl/Cmd+K | opens; toggles closed when open; browser binding cancelled. Not inside the rich-text editor (its link command stays; the magnifier still opens); nothing while another modal is open. Works from a plain text field without changing its text |
| ↓ / ↑ | next / previous option across groups, skipping headings and the «Похожие» caption; wraps; the active option scrolls into view (`block: nearest`) |
| Enter | chooses the active option (§ 3.7); inert in states 6 and during IME composition |
| Ctrl/Cmd+Enter | opens the active row in a new tab (its real address), the palette stays open and the recent list is not changed by the palette (the new tab records the opening itself) |
| Esc | closes at any time, text discarded; with the scope menu open it closes the menu only. Focus returns to the opener (the magnifier or the element that had it) |
| Tab / Shift+Tab | cycles inside the dialog (§ 3.1) |
| Home / End | stay in the field (caret); no list jump |

Mouse: moving over a row makes it active (one highlight only, no separate hover tint); click chooses; middle click and Ctrl/Shift click use the link. Focus is in the field on open and after every state change; options never take DOM focus.

ARIA: dialog `aria-label="Поиск"`; field `role="combobox"`, `aria-expanded` (true while options exist), `aria-controls` the listbox, `aria-autocomplete="list"`, `aria-activedescendant` the active option's id; listbox `aria-label` «Результаты поиска»; each group `role="group"` labelled by its heading; options `aria-selected`; the busy body `aria-busy`; the live region of § 3.3. Active row: tint plus the 3 px accent edge (not colour alone).

### 3.7 Choosing a row (BR-GS-12; the rules are US-5's)

Enter or click: record the opening, close the palette, navigate; focus goes to the opened page's `h1`. Department, team, project, sub-area: context and its notice as doc-016 § 3.3 and the object's own page; work item: its card; user: the organization's «Рабочие элементы» filtered by «Исполнитель» (an empty list is the list's own answer, R-10); the last row: the full page. Recording is a route-level hook (BR-GS-13), not a click handler; users are never recorded.

## 4. The full search page (US-7; BR-GS-18)

### 4.1 Layout

```
Ромашка / Поиск                               (PageHeader trail)
Поиск                                          (h1)
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
- **Count** (BR-GS-18): «Найдено: 45», `role="status"`, only the visible matches; hidden ones are never counted.
- **Row**: `Surface` + `List`; work item icon, **identifier** and title as one link to the card, grey line «Состояние · Проект» (BR-GS-11); for a description match the **snippet**: about 140 characters around the first match, `…` at a cut end, matching words in `HighlightedText`. The snippet and its matches come from the server as **plain text plus ranges** (`snippet: string`, `matches: [{start, length}]`, contract of the page's line); the client builds spans from them, never parses markup, so a description containing `<script>` shows those characters as text (prototype: «шаблон»). Titles and identifiers are not emphasised on this page (stems differ from the typed word; the server judges). A title or identifier match with a description that holds no query word shows no snippet.
- **Paging.** `TablePager`: 20 rows, «1–20 из 45», two arrows, the same order across pages (BR-GS-18). A page change keeps the rows, `aria-busy`, until the next arrive; address gets `page`; focus stays on the arrow. A page past the last (count > 0): ◆ «На этой странице результатов нет» with the link-button «К первой странице».
- **From the palette**: the palette closes, the page opens with the text in the field and the results shown, in the palette's organization; Back returns to the page the user came from.
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
| Magnifier name and tooltip | «Поиск (Ctrl+K)»; macOS «Поиск (⌘K)» |
| Dialog | «Поиск» |
| Placeholder ◆ | «Найти рабочий элемент, проект, команду, человека…» |
| Clear field ◆ / close ◆ | «Очистить поле» / «Закрыть» |
| Scope ◆ | «Искать в:» |
| Groups | «Недавние», «Рабочие элементы», «Проекты», «Команды», «Департаменты», «Области», «Пользователи»; ◆ «Похожие» |
| Rows | «Показать ещё»; «Загрузка…»; «Искать везде, включая описания»; «Очистить недавние»; «(неактивен)» |
| Idle / hint | «Здесь появятся недавно открытые объекты»; «Введите не менее 2 символов» |
| None / error / 404 | «Ничего не найдено»; «Не удалось выполнить поиск. Попробуйте ещё раз.» + «Повторить»; «Организация не найдена.» |
| Live ◆ | «Найдено: 7»; «Недавние: 4» |
| Key hints ◆ | «выбрать», «открыть», «в новой вкладке», «закрыть» (keys drawn as `kbd`; ⌘ on macOS) |
| Notice ◆ | «Недавние очищены.» |
| Full page | «Поиск»; «Найти»; «Найдено: 45»; «1–20 из 45»; «Предыдущая страница», «Следующая страница»; «На этой странице результатов нет»; «К первой странице»; the other lines of § 4 |

## 6. Narrow widths and accessibility

Floor 640 CSS px (ADR-0024). Below 700 px the palette is full screen (§ 3.1); at 700 px and wider it is 640 wide with 8 px side margins. The header keeps the magnifier. Contrast and focus follow the kit; the active option, the emphasis and the «Похожие» caption never rely on colour alone (edge + tint, bold + tint, text). Reduced motion: the skeleton is static. The live region speaks after the answer settles, not on each keystroke.

## 7. Decisions for gate-2

| # | Decision | Default taken |
|---|---|---|
| D-1 | Palette sits at a fixed top offset, not vertically centred | fixed 10 vh |
| D-2 | Full screen below 700 px | yes; spec floor is 640 |
| D-3 | Similar rows marked by a «Похожие» caption | marked |
| D-4 | Enter inert while a newer read is pending | inert, not "wait then open" |
| D-5 | The last row hidden on error and 404 | hidden (BR-GS-9 says "even when nothing matched", not "even on failure") |
| D-6 | Snippet from the server as text + ranges; no emphasis on the page's titles | as § 4.1 |
| D-7 | POST for the read; limiter 3/3 per user, 24/12 per process, 1 s | as § 3.4 |
| D-8 | The kit part is the first commit of the palette line and may be split as an S line | flagged (R-23) |

Left to the ADR and not this design: the `word_similarity` threshold (the prototype uses 0.5 trigram overlap for its fake «лагистика»), the plain-text derivation, snippet length on the server.

## 8. For the developer

- Slice `src/frontend/src/features/search`: `api/` (one search client, aborting stale requests, `Retry-After` retries), `hooks/` (`useSearchPalette` state machine with the states of § 3.3, `useRecentItems` with the key `mustask.recent.v1.<sub>.<organizationId>`, `useOpenedObjectRecorder` mounted in the shell), `components/` (`SearchPalette`, `SearchResults`, `SearchPage`), `index.ts`. The shortcut is one `keydown` listener in `app/shell`, checking whether the event target is inside `shared/ui/RichTextEditor` (the part marks its root with an attribute for it) and whether a modal is open.
- The kit parts of § 1 are not written in the slice; `kitGuard` will fail a `Dialog`, `Chip` or `Alert` import there.
- Tests: the keyboard table (including wrap, group skipping, IME, Ctrl+Enter, focus return), every state of § 3.3 and § 4.2, text-never-markup for snippets, highlight by `ranges`, no total anywhere (live region, rows, count of the palette), recent per user and organization.

## 9. Prototype scenarios (all in `prototype/index.html`)

1. Ctrl+K and the magnifier open; Ctrl+K toggles; Esc returns focus to the magnifier; Ctrl+K in the plain field opens, in the «редактор» does not, with the «Новый рабочий элемент» dialog open does nothing.
2. Empty field: «Недавние» (4 rows), «Очистить недавние», then the «Здесь появятся…» state; «Заполнить недавние» restores.
3. «оплата»: groups by tier («Области» first), emphasis, the last row; ↑↓ across groups, wrap; Enter opens a fake page and records it; Ctrl+Enter toasts «в новой вкладке» and stays.
4. «отчёт»: 5 rows and «Показать ещё» → 14 rows in place; «лагистика»: «Похожие» → «Логистика»; «MSK-42» and «alp-7» identifier forms; «ол»; one character → the hint.
5. States switch: loading, error with «Повторить», organization not found, empty; scope chip (Максим, Ольга: two organizations; Вера: a tag); Ольга finds nothing of «Браво», «Чарли»; «Узкий вид» full screen.
6. Last row → the page «Поиск»: «списание», «payment» (word forms), «шаблон» (a `<script>` shown as text), «оплата» (33 hits, two pages, pager), Ольга's smaller count, the states switch on the page, Back (browser).
