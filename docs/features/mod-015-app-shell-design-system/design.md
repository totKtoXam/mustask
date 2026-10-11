# Design: Application shell, visual language and screen patterns (mod-015)

Stage 3 artifact for `spec.md` and `stories/US-1..US-12`. It fixes the
structure, the MUI components, every state and the final Russian copy of the
shell, the theme and the shared patterns; the clickable prototype shows them
(§ 14). Pixel values below are theme tokens (§ 2), not a pixel-perfect
mock-up: the developer puts them in `app/theme` once and reads them from
there (BR-1).

- Visual model: the reference tracker captured in
  `../doc-001-domain-map/screenshots/` (D-5). It is never named (D-3, BR-16).
- ADRs relied on: ADR-0007 (screen-state canon), ADR-0018 (permission codes
  in the token), ADR-0019 (MUI core, controlled forms), ADR-0015/ADR-0016
  (registry paging, unchanged), and
  [ADR-0024](../../adr/0024-app-shell-and-visual-language.md) (written by
  this task and accepted, `status.md` D-24: shell, visual language, context
  in the address, appearance tokens, reflow floor).
- One neighbouring `design.md` exists: mod-001's process screen (branch
  `mod/001-process-seed`, 6577518), drafted in parallel from this task's
  spec. § 15 answers its reconciliation table. Later modules build on this
  design (US-10).
- Copy rule: every Russian string here is final. A later module's design
  that reuses a pattern with its own words (an empty heading, a command)
  states them in its own `design.md`; it never changes the shell's copy.

## 0. Decisions D-15..D-23 in this design

| Decision | Where it lands |
|---|---|
| D-15 not found named by level | § 7.2 (frame copy), § 5.6 (switcher keeps its selection) |
| D-16 one appearance token per type, categories by code | § 2.3 (token set, values, lookups, fallback) |
| D-17 address prefixes, untyped ids | § 5.5 (address table, validation, not found) |
| D-18 interim BR-8 clause | § 4.2 (visibility), § 4.6 (re-evaluation) |
| D-19 mod-002 wires the switcher, first-team default, tree registry | § 5.7 (hand-over list), § 10.7 (tree registry) |
| D-20 «Все организации», «Нет организаций» | § 5.3 (menu), § 5.4 (states), § 10.9 (placeholder organization list) |
| D-21 mod-014 has no screen | § 11 (nothing of mod-014 is restyled or registered) |
| D-22 R-22 landing page; R-24 truncation and what yields; R-25 lookups; R-26 local prototype; R-27 no leave guard; R-28 locale | § 5.5; § 3.6 and § 5.4; § 2.3; § 14; § 9.7; § 2.6 |
| D-23 the owed ADR | ADR-0024, drafted with this design |

Earlier decisions with a visible effect: D-12 («Процесс» in the navigation
model, § 4.1), D-13 (unknown address, § 7.2), D-14 («Языки» under
«Настройки» → «Справочники», § 4.1), D-8 (640 CSS px floor, § 3.6, § 12.6).

## 1. Screen map

```mermaid
flowchart LR
    AUTH[Auth card, outside the shell] -->|signed in| HOME[«Главная»]
    HOME -->|«Настройки»| LANG[«Языки» registry]
    LANG -->|«Добавить язык»| NEW[«Добавить язык»]
    LANG -->|«Редактировать <код>»| EDIT[«Язык: <код>»]
    LANG -->|«Сделать языком по умолчанию <код>»| DLG[Dialog «Назначить языком по умолчанию»]
    NEW -->|created| EDIT
    ANY[Any shell page] -->|unknown address| NF[«Страница не найдена»]
    ANY -->|context link not found| NFL[«Организация / Проект / Команда не найден(а)»]
    ANY -->|no code| AD[«Доступ ограничен»]
    HOME -.prototype.-> WI[«Рабочие элементы» + preview pane] -.-> WIF[Work item form]
    HOME -.prototype.-> BRD[«Доски»] -.card.-> WIF
    HOME -.prototype.-> BKL[«Бэклоги» + «Планирование»]
    HOME -.prototype.-> SET[Settings: «Области», «Итерации» trees; organization pages]
```

| Screen | Route | Stories | Access | In this task |
|---|---|---|---|---|
| Frame: top bar, navigation, switcher, user menu | every signed-in route | US-2..US-5, US-12 | any signed-in user | built |
| «Главная» | `/home` (`/` redirects) | US-2, US-11 | any signed-in user | built |
| «Языки» | `/dictionaries/languages` | US-7, US-8, US-11 | `dictionaries.languages.read` | built (restyle) |
| «Добавить язык» | `/dictionaries/languages/new` | US-9, US-11 | `dictionaries.languages.write` | built (restyle) |
| «Язык: <код>» | `/dictionaries/languages/:code` | US-9, US-11 | `dictionaries.languages.write` | built (restyle) |
| Dialog «Назначить языком по умолчанию» | over «Языки» | US-9, US-11 | `dictionaries.languages.write` | built (restyle) |
| «Страница не найдена» | any unknown address | US-7 | any signed-in user | built |
| «Организация / Проект / Команда не найден(а)» | context addresses (§ 5.5) | US-7 | any signed-in user | frame built; routes by mod-002 and later |
| «Доступ ограничен» | any gated page | US-7 | user without the page's code | built |
| Auth card, all states | `/signed-out`, any route while the session is not authenticated | US-11 | anonymous | built (restyle) |
| Crash screen | any route that throws | US-11 | anyone | built (restyle) |
| «Рабочие элементы», preview pane, type picker | `…/projects/:projectId/workitems` | US-10 | per mod-003 | prototype; built by mod-003 |
| Work item form | `…/workitems/:workItemId` | US-10 | per mod-003 | prototype; built by mod-003 |
| «Доски» | `…/teams/:teamId/boards/:level` | US-10 | per mod-006 | prototype; built by mod-006 |
| «Бэклоги» + «Планирование» | `…/teams/:teamId/backlogs/:level` | US-10 | per mod-006 | prototype; built by mod-006 |
| «Области», «Итерации» (tree registry) | `…/projects/:projectId/settings/areas`, `…/settings/iterations` | US-10 | per mod-002, mod-007 | prototype; built by mod-002 |
| «Организации» (target of «Все организации») | `/organizations` | US-5 | per mod-002 | prototype placeholder; built by mod-002 |
| «Язык оформления» (token sheet) | prototype only | US-1, US-10 | — | prototype only, never in the panel |

### What each role sees

| Element | Anonymous | Signed in, no `dictionaries.*` | Dictionaries reader | Dictionaries administrator |
|---|---|---|---|---|
| Auth card, crash screen | yes | — | — | — |
| Top bar, switcher, user menu, «Главная», not-found frames | — | yes | yes | yes |
| «Настройки» in the main navigation | — | hidden | shown | shown |
| «Языки» registry | — | «Доступ ограничен» by address | read-only: no «Добавить язык», no row-action column | full |
| Language form, default-language dialog | — | «Доступ ограничен» | «Доступ ограничен» | full |
| Work-tracking hubs, organization and project settings | — | hidden in this build (BR-7, BR-8 interim, § 4.2) | same | same |

## 2. Visual language (US-1; BR-1..BR-5)

`app/theme` is the only source of appearance. The colour, layout and density
values below are its tokens; components read them through the theme
(`palette`, `typography`, `spacing`, `shape`, custom keys on the theme), never
as literals (BR-1).

### 2.1 Colour roles (BR-2)

| Role | Theme key | Value | Used by |
|---|---|---|---|
| Application background | `palette.background.default` | `#F8F8F8` | behind pages, the page header, auth card, crash screen |
| Navigation surface | `palette.shell.navigation` | `#EBEBEB` | main navigation, rail, settings navigation |
| Navigation hover | `palette.shell.navigationHover` | `#E2E2E2` | hovered navigation entry |
| Navigation selected | `palette.shell.navigationSelected` | `#DCDCDC` | current navigation entry, under the accent marker |
| Content surface | `palette.background.paper` | `#FFFFFF` | top bar (with a bottom divider), tables, filter bar, cards, forms, menus, dialogs |
| Neutral text | `palette.text.primary` | `#242424` | all text, secondary command labels |
| Secondary text | `palette.text.secondary` | `#616161` | hints, column headers, field labels, avatar background |
| Accent | `palette.primary.main` (dark `#0C5598`, contrast text `#FFFFFF`) | `#0F6CBD` | only: filled primary actions, links, selected navigation marker, selected tab underline, focus indicator, leading icons of secondary commands |
| Divider | `palette.divider` | `#E0E0E0` | top-bar bottom line, table row lines, section rules |
| Input border | `palette.shell.inputBorder` | `#8A8A8A` | text field and select outlines, outlined button outline |
| Table header | `palette.shell.tableHeader` | `#F3F3F3` | registry header row |
| Row hover | `palette.action.hover` | `#F0F0F0` | hovered table row, menu item |
| Row selected | `palette.shell.rowSelected` | `#E5F0FA` | selected list row (preview pane), selected tree row |

> **Changed by feat-027 (D-10, 2026-10-11):** the accent is `#0078D4`, the
> reference's (a white label on it is 4.53:1). As text it is 4.26:1 on the
> application background, under the 4.5:1 floor, so `MuiLink` draws a link in
> `palette.primary.dark` (`#0C5598`), not in the accent. The selected row's
> fill `#E5F0FA` and `accentDark` stay. See feat-027 design § 2.2.

Semantic colours carry meaning only and stay darker than MUI's defaults
(ADR-0007): `error` `#C42B1C`, `warning` `#9D5D00`, `success` `#107C10`,
`info` `#01579B`, each with white contrast text.

Measured ratios (WCAG 2.1, BR-5; the theme's unit test recomputes them):
text on content 15.5:1, on application background 14.6:1, on navigation
13.0:1, on navigation selected 11.3:1; secondary text 6.2 / 5.8 / 5.2 / 4.5:1;
accent on content 5.4:1, on navigation 4.5:1, on navigation selected 3.9:1;
white on accent 5.4:1; input border on content 3.5:1; error / warning /
success / info with white 5.7 / 5.3 / 5.4 / 7.4:1.

### 2.2 Secondary commands keep a neutral label

Theme overrides of `MuiButton` for `variant="text"` and `variant="outlined"`
with the primary colour: label `text.primary`, `startIcon` `primary.main`,
outlined border `shell.inputBorder`. `variant="contained"` is the one filled
primary action: accent background, white label. A button with
`color="error"` keeps the error colour (destructive confirmations, § 9.4).

Selection controls and progress indicators stay out of the accent, because
BR-2's list of accent uses is closed. `palette.secondary` is the neutral text
colour (`#242424`, contrast text white). `MuiCheckbox`, `MuiRadio`,
`MuiSwitch` (the language form's «Активен») and `MuiCircularProgress` default
to `color="secondary"`. A menu's current-entry `Check` is `text.primary`. A
focused field keeps its label in `text.secondary`, and its 2 px accent border
is the focus indicator.

### 2.3 Work-tracking appearance (BR-4; D-6, D-16, R-25)

The process seed stores per type exactly one token name; the theme holds its
icon and colour. Two lookups live in `app/theme`:

- `typeAppearance(token: string) → { icon, colour }` takes the stored token
  name only — never a type key, a type name or a label. A type `BUG` whose
  token is `task` renders the `task` icon and colour. A name outside the set
  renders the fallback, and nothing throws.
- `categoryColour(code: string) → colour` takes doc-007's category code only —
  never a state name or a Russian label. «Готово» in `InProgress` renders the
  `InProgress` colour. An unknown code renders `text.secondary`.

Slices read both lookups through the theme object (a custom theme key read
with `useTheme()`), never by importing `app/`: `app/` assembles the slices,
not the other way round. The theme holds colours and icons only. The
category labels in the table below are doc-007's words, shown here for
reference. Their one list in code is mod-001's (its design T-12..T-16), and
the theme keeps no second copy.

| Token | Icon (`@mui/icons-material`) | Colour | Contrast on content | Seeded for (mod-001) |
|---|---|---|---|---|
| `epic` | `Landscape` | `#C05600` | 4.6:1 | «Эпик» |
| `feature` | `EmojiEvents` | `#773B93` | 7.4:1 | «Фича» |
| `story` | `MenuBook` | `#00739E` | 5.3:1 | «Пользовательская история» |
| `task` | `Assignment` | `#8A6A00` | 5.1:1 | «Задача» |
| `bug` | `BugReport` | `#B4233A` | 6.5:1 | «Баг» |
| fallback | `HelpOutlineOutlined` | `text.primary` | 15.5:1 | an unknown token |

| Category code | Colour | Marker | Label (doc-007) |
|---|---|---|---|
| `Proposed` | `#707070` | filled dot | «Новые» |
| `InProgress` | `#005A9E` | filled dot | «В работе» |
| `Resolved` | `#00796B` | filled dot | «Решённые» |
| `Completed` | `#107C10` | filled dot | «Завершённые» |
| `Removed` | `#A4262C` | hollow ring | «Удалённые» |

> **Changed by feat-027 (D-10, 2026-10-11):** the type colours are the
> reference's — epic `#E06C00`, feature `#773B93`, story `#0098C7`, task
> `#A4880A`, bug `#CC293D` — and so are the `Proposed` dot `#B2B2B2` and the
> `Completed` dot `#339933`; the epic's icon is `CrownIcon`, the story's
> `StoryIcon`, the task's `AssignmentTurnedIn` (the table above lists the
> originals). The 3.98:1 floor stated below is lowered, and the contrast test
> exempts, the epic, story and task icons and the `Proposed` dot — graphics
> that text on the same row repeats — each with its reason; every graphic that is the
> only carrier keeps 3:1. See feat-027 design § 2.2, § 2.4.

No type or category colour equals the accent; every one is at least 3.98:1
on every surface it sits on (content, application background, row hover,
row selected). Colour is never alone: a type icon is followed by the type
name or the identifier, a category dot by the state name. The marker is an
8 px circle (`layout.categoryDot`), the `Removed` one a 2 px ring — a second,
non-colour cue. The shape follows the code alone: `Removed` is a ring, and
every other code, an unknown one included, is a filled dot. The marker is
drawn as a circle box with `borderRadius: '50%'`, not as an icon glyph,
because an icon scaled to 8 px loses the ring.

### 2.4 Typography

> **Superseded by feat-027 D-7 (Segoe UI, weight 600), 2026-10-09:** the
> panel's family is no longer Roboto but `"Segoe UI", -apple-system,
> BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif`
> (`@fontsource/roboto` is not imported any more; the dependency entry stays),
> and every weight 500 in the table below — and in this document's component
> notes (the selected tab label § 2.6, the product mark and the user menu
> § 3.2, the settings navigation's subheader § 4.4; wherever this document
> says 500) — is **600**, because Segoe UI has no 500 face. Sizes and line heights are
> unchanged. See feat-027 design § 2.1. The prototype under `prototype/`
> still names Roboto and 500, as a sketch of the original design.

Family: Roboto (already loaded by the panel through `@fontsource/roboto`),
then `"Segoe UI", system-ui, Arial, sans-serif` — *superseded by feat-027 D-7:
Segoe UI first, no Roboto, see the note above*. `typography.fontSize` 14.
Buttons are not upper-cased (`textTransform: none`).

| Variant | Size / line | Weight | Use |
|---|---|---|---|
| `h1` | 20 / 28 px | 500 | the page title, the only level-1 heading (BR-6); full-page frames |
| `h2` | 16 / 24 px | 500 | in-content section titles, the canon frame heading under a page header |
| `h3` | 14 / 20 px | 500 | sub-sections (form sections, settings dialog groups) |
| `body1`, `body2` | 14 / 20 px | 400 | base text, table cells, menus (BR-3: base 14 px) |
| `subtitle2` | 12 / 16 px | 500 | column headers, header-strip field labels |
| `caption` | 12 / 16 px | 400 | change stamps, counters |
| `button` | 14 / 20 px | 500 | all buttons and tabs |

### 2.5 Layout, density and breakpoints (BR-3)

| Token | Value | Notes |
|---|---|---|
| `layout.topBarHeight` | 48 px | BR-3; unit-tested |
| `layout.navigationWidth` | 240 px | expanded main navigation |
| `layout.railWidth` | 48 px | collapsed main navigation |
| `layout.settingsNavigationWidth` | 240 px | «Меню настроек» |
| `layout.sidePaneWidth` | 360 px | preview pane, «Планирование» |
| `layout.formColumnWidth` | 480 px | form pages, form dialogs |
| `layout.boardColumnWidth` / `boardColumnCollapsedWidth` | 280 / 40 px | board |
| `layout.authCardWidth` | 400 px | auth card |
| `layout.avatarSize` | 32 px | user menu |
| `layout.categoryDot` | 8 px | state marker |
| `layout.contextNameMaxWidth` | 200 / 160 / 96 px | switcher name at ≥ 1280 / 1024–1279 / < 1024 px |
| `layout.trailSegmentMaxWidth` | 200 px | one trail segment |
| `density.registryRowHeight` | 36 px | at most 40 px (BR-3); unit-tested |
| `density.controlHeight` | 32 px | buttons, small text fields' minimum target |
| `spacing` | 8 px | MUI unit; half steps (4 px) allowed |
| `shape.borderRadius` | 4 px | — |
| `breakpoints` | `xs` 0, `sm` 640, `md` 1024, `lg` 1280, `xl` 1536 | the widths of BR-10 and D-8 |

Content padding: 24 px left and right at ≥ 1024 px, 16 px below.

### 2.6 Component defaults (theme `components`)

- `MuiCssBaseline`: body on `background.default`.
- `MuiButtonBase`: `disableRipple`. Focus is shown by a 2 px accent outline,
  offset 2 px, on `:focus-visible` for every button, list item, tab, link,
  sort label and icon button (BR-5, US-12).
- `MuiButton`: `disableElevation`, minimum height `density.controlHeight`,
  padding 4 × 12 px; § 2.2 colours. `MuiIconButton` `size="small"`: padding
  4 px, icon 20 px.
- `MuiTextField` / `MuiOutlinedInput`: default `size="small"`, border
  `shell.inputBorder`, focused border 2 px accent; error colour from
  `error`.
- `MuiTableCell`: `size="small"` padding 4 × 12 px; head cells `subtitle2`,
  `text.secondary`, background `shell.tableHeader`; `MuiTableRow` height
  `density.registryRowHeight`, hover `action.hover`.
- `MuiTabs` / `MuiTab`: indicator 2 px accent; tab minimum height 40 px;
  selected label `text.primary` weight 500, others `text.secondary`.
- `MuiListItemButton` in navigation: selected → `shell.navigationSelected`
  plus a 3 px accent bar on the left edge; hover → `shell.navigationHover`.
- `MuiTooltip`: shown on hover and on keyboard focus (MUI default), delay
  500 ms on hover.
- `MuiChip` `size="small"` for tags (default colour) and statuses.
- `MuiPaper`: content panels use `variant="outlined"` at the call site (not
  a default, so menus keep their elevation). Cards on the board use
  elevation 1.
- `MuiDialog`: title `h2`, actions padding 16 × 24 px.
- Motion: width and slide transitions use `theme.transitions`; under
  `prefers-reduced-motion: reduce` they are `none` (CSS media query on the
  navigation width; `transitionDuration={0}` on temporary drawers from
  `useMediaQuery('(prefers-reduced-motion: reduce)')`).
- Locale: `createTheme(options, ruRU)` keeps MUI's Russian texts
  (`TablePagination` «1–10 из 25»; NFR Localization).

### 2.7 Machine checks

- Theme unit test: the density tokens (14 px, 48 px, row ≤ 40 px), the
  colour roles (navigation ≠ application background, top bar = content
  surface, secondary labels neutral), the contrast table of § 2.1 and § 2.3,
  the five tokens and five categories distinct and none equal to the accent,
  and the two lookups (§ 2.3).
- Colour-literal check, run by `npm test` (BR-1): scans `src/frontend/src/`
  except `app/theme/`, `*.test.*` and `src/test/` for a string literal that is
  a hex colour (`#` and 3, 4, 6 or 8 hex digits, the whole literal) or begins
  with `rgb(`, `rgba(`, `hsl(`, `hsla(`. «#12» in copy and `#` in an address
  do not match.

### 2.8 Iconography

Material icons from `@mui/icons-material`, outlined style in the shell,
20 px (`fontSize="small"`) in navigation, tables and command bars, 24 px
elsewhere, `fontSize="large"` in canon frames. Icons that sit beside a
visible label are `aria-hidden`. Every name below is one that
`@mui/icons-material` 9 exports. Version 9 dropped the unsuffixed `*Outline`
names (`ErrorOutline`, `HelpOutline`, `PersonOutline`, `DeleteOutline`), so
this design uses their `*OutlineOutlined` forms.

| Place | Icon |
|---|---|
| «Главная» | `HomeOutlined` |
| «Рабочие элементы» | `ListAltOutlined` |
| «Доски» | `ViewWeekOutlined` |
| «Бэклоги» | `FormatListNumbered` |
| «Спринты» | `DateRangeOutlined` |
| «Запросы» | `ManageSearch` |
| «Настройки» | `SettingsOutlined` |
| «Свернуть меню» / «Развернуть меню» | `KeyboardDoubleArrowLeft` / `KeyboardDoubleArrowRight` |
| «Меню» | `Menu` |
| switcher level, menus | `ExpandMore`; current entry `Check` |
| create commands | `Add` |
| «Ещё действия» | `MoreHoriz` |
| «Очистить фильтры» | `FilterListOff`; filter bar lead `FilterList` |
| «Закрыть» | `Close` |
| «Выйти» | `Logout` |
| no display name | `PersonOutlineOutlined` |
| canon frames | access denied `LockOutlined`; not found `SearchOff`; empty `InboxOutlined`; empty after filter `FilterListOff` |

## 3. Application frame (US-2; BR-6, BR-13)

### 3.1 Layout

```
≥ 1280 px, outside settings
+-------------------------------------------------------------------------------------+
| [skip link, on focus] Mustask | Org ▾ / Project ▾ / Team ▾ | Trail / Trail     (ИП) |  <- banner, 48 px, white, divider
+-------------+-----------------------------------------------------------------------+
| Главная     |  PageHeader: <h1 Title> ..................... [cmd] [cmd] [Primary]   |
| ─────────── |  [Tab | Tab | Tab]                                                  |
| Раб. элем.  |                                                                       |
| Доски       |  page content (white panels on the grey background)                   |
| Бэклоги     |                                                                       |
| ...         |                                                                       |
|             |                                                                       |
| Настройки   |                                                                       |
| « Свернуть  |                                                                       |
+-------------+-----------------------------------------------------------------------+
  nav 240 px, grey, fixed           main region, scrolls with the document
```

On a settings page the main navigation is a 48 px rail and «Меню настроек»
(240 px) stands to its right (§ 4.4). Below 1024 px there is no permanent
navigation, «Меню» opens it as an overlay (§ 4.5) and the trail moves into
the page header (§ 3.6).

### 3.2 Components

| Block | MUI | Notes |
|---|---|---|
| Frame | `Box` (flex) in `app/AppLayout` | replaces today's `AppLayout`; hosts `<Outlet />` |
| Skip link | `Link href="#main-content"` | first focusable element; visually hidden until focused, then shown at the top-left over the bar with the focus outline (no MUI equivalent; 10 lines of `sx` in the frame) |
| Top bar | `AppBar position="fixed" color="inherit" elevation={0}` + `Toolbar` (min height `layout.topBarHeight`) | content surface, `divider` bottom border, above the drawers (`zIndex.drawer + 1`) |
| «Меню» (< 1024 px) | `IconButton` + `Menu` icon | `aria-label="Меню"`, `aria-expanded`, `aria-controls` the overlay |
| Product mark | `Link component={RouterLink} to="/home" underline="none"` | text «Mustask», 16 px weight 500, `text.primary`; not a heading |
| Context switcher | § 5 | `role="group"`, `aria-label="Контекст работы"` |
| Divider switcher / trail | `Divider orientation="vertical" flexItem` | hidden when there is no trail |
| Page trail | `Breadcrumbs` (`aria-label="Путь к странице"`, separator «/») | ancestors only (BR-6); links are `Link component={RouterLink}`, plain ancestors `Typography`; each segment truncates at `layout.trailSegmentMaxWidth` with a `Tooltip` of its full text |
| User menu button | `IconButton` + `Avatar` (initials, `layout.avatarSize`, background `text.secondary`, white 13 px initials) | `aria-label="Профиль: <имя>"`, `aria-haspopup="menu"`, `aria-expanded` |
| User menu | `Menu` anchored right | first row: the display name as `Typography` (`body1`, weight 500, not focusable), `Divider`, `MenuItem` «Выйти» with `ListItemIcon` `Logout` |
| Main region | `Box component="main" id="main-content" tabIndex={-1}` | padding § 2.5; receives focus from the skip link and from an overlay entry |

The top bar and the navigation are fixed; the document scrolls the page,
page header included (US-6).

### 3.3 User menu and initials

- Initials: the first letters of the first two space-separated words of the
  display name, upper-cased; one word gives its first letter. «Иван Петров» →
  «ИП», «Иван Петрович Сидоров» → «ИП», «ivan.petrov» → «I», «e2e-fixture» →
  «E».
- No display name (neither name nor username in the token): the avatar shows
  `PersonOutlineOutlined`, the button is named «Профиль», and the menu shows only
  «Выйти».
- «Выйти» calls today's `signOut`; a failed sign-out lands on today's auth
  screen «Выход не завершён: сервис аутентификации недоступен.» (§ 11.3).

### 3.4 Titles and the trail

Every page passes its title to `PageHeader` (§ 6). The title is the page's
only `h1`; the browser tab reads «<title> — Mustask».

The trail's leading part comes from the shell, not from the page. The shell
derives it from the navigation model (§ 4.1) for the page on screen: on a
settings page, «Настройки» and the section, both plain text; on a page
beneath an entry's landing page, that entry as a link to its landing page.
A page passes to `PageHeader` only the ancestors below its own entry's page,
the pages between that landing page and itself, and the shell appends them.
None of this task's pages has such an ancestor. A page that also passed
«Настройки» or its section would show them twice. When the trail is empty
(`/home`), there is no trail and the vertical divider is hidden. The
mechanism is plan-frontend.md § 4.5 and § 9 D-7. The table below is the
result, and it does not change.

| Page | `h1` | Tab | Trail |
|---|---|---|---|
| `/home` | «Главная» | «Главная — Mustask» | none |
| `/dictionaries/languages` | «Языки» | «Языки — Mustask» | «Настройки / Справочники» (plain text) |
| `/dictionaries/languages/new` | «Добавить язык» | «Добавить язык — Mustask» | «Настройки / Справочники / Языки» («Языки» is a link) |
| `/dictionaries/languages/:code` | «Язык: <код>» | «Язык: <код> — Mustask» | as above |
| unknown address | «Страница не найдена» | «Страница не найдена — Mustask» | none |
| context not found | «Организация не найдена», «Проект не найден», «Команда не найдена» | «<heading> — Mustask» | none |
| refused by its code | «Доступ ограничен» | «Доступ ограничен — Mustask» | none |
| auth card, every state | «Mustask» (the card's mark) | «Mustask» | none |
| crash screen | «Что-то пошло не так» | «Mustask» | none |

The settings rule follows from these rows. A settings page's trail is
«Настройки / <section>», both plain text, followed by any ancestor page under
it («Языки» on the language form: the entry's link, which the shell derives).
So «Процесс» reads «Настройки / Организация», and «Области» reads
«Настройки / Проект». Prototype pages of later lines follow the same rule,
and their trails are shown in § 10.

### 3.5 States of the frame

| State | What the user sees |
|---|---|
| Session being restored, absent, lost or failed | no shell: the auth card (§ 11.3) |
| Authenticated | the frame; navigation filtered by the permission check (§ 4) |
| Page loading / empty / error / refused / not found | inside the main region, the frame unchanged (§ 7) |
| Render crash | no shell: the crash screen (§ 11.4) |

### 3.6 Narrow widths — what yields (D-22, R-24)

| Width | Top bar, left to right | Navigation |
|---|---|---|
| ≥ 1280 px | mark, switcher (names ≤ 200 px), trail, user menu | kept choice, else expanded |
| 1024–1279 px | mark, switcher (names ≤ 160 px), trail, user menu | kept choice, else rail |
| 640–1023 px | «Меню», mark, switcher (names ≤ 96 px), user menu | overlay from «Меню» |

Below 1024 px the trail leaves the top bar and stands above the title in the
page header, with the same `nav` name, segments and links, so it stays
operable. Nothing else hides: every control keeps its full accessible name,
and a truncated name keeps its full text in a tooltip (BR-11). Below 640 CSS
px no layout is designed (D-8).

## 4. Navigation (US-3, US-4; BR-7, BR-8, BR-10, BR-19)

### 4.1 The navigation model

One definition list in `app/` (it assembles the slices): each entry has a
label, an icon, a permission code, a scope (`instance`, `organization`,
`project`, `team`), a landing address built from the current context, and
whether its page exists in the build. Groups are separated by a `Divider`
and carry no heading. In code, registration is the "in the build" flag: the
model lists only built pages, and an entry is added by the line that builds
its page (plan-frontend.md § 9 D-19). The icon is required on main entries
by the entry's type.

| Place | Entry | Icon | Code | Scope | Landing address | In this build |
|---|---|---|---|---|---|---|
| Main, top | «Главная» | `HomeOutlined` | none | instance | `/home` | yes |
| Main, work | «Рабочие элементы» | `ListAltOutlined` | `workitems.workitem.read` | project | `/organizations/:o/projects/:p/workitems` | no (mod-003) |
| Main, work | «Доски» (superseded: «Канбан», feat-001 BR-H-16, built by feat-026) | `ViewWeekOutlined` | per mod-006 | team | `/organizations/:o/projects/:p/teams/:t/boards/stories` | no (mod-006) |
| Main, work | «Бэклоги» | `FormatListNumbered` | per mod-006 | team | `…/teams/:t/backlogs/stories` | no (mod-006) |
| Main, work | «Спринты» (release 2) | `DateRangeOutlined` | per mod-011 | team | `…/teams/:t/sprints` | no |
| Main, work | «Запросы» (release 2) | `ManageSearch` | per mod-008 | project | `…/projects/:p/queries` | no |
| Main, bottom | «Настройки» | `SettingsOutlined` | any settings entry visible | — | the first visible settings entry (BR-19) | yes |
| Settings «Организация» | «Общие» | — | `organizations.organization.read` | organization | `/organizations/:o/settings/general` | no (mod-002) |
| Settings «Организация» | «Участники» | — | `organizations.membership.read` | organization | `/organizations/:o/settings/members` | no (mod-016) |
| Settings «Организация» | «Процесс» | — | `process.definition.read` | organization | `/organizations/:o/settings/process` (mod-001 BR-M2-12) | no (mod-001 registers it, D-12) |
| Settings «Организация» | «Хранилище вложений» | — | `workitems.attachment-storage.manage` | organization | `/organizations/:o/settings/attachment-storage` | no (mod-004) |
| Settings «Проект» | «Команды» | — | `organizations.team.read` | project | `/organizations/:o/projects/:p/settings/teams` | no (mod-002) |
| Settings «Проект» | «Области» | — | `organizations.area.read` | project | `…/projects/:p/settings/areas` | no (mod-002) |
| Settings «Проект» | «Итерации» | — | `iterations.iteration.read` | project | `…/projects/:p/settings/iterations` | no (mod-007) |
| Settings «Справочники» | «Языки» | — | `dictionaries.languages.read` | instance | `/dictionaries/languages` | yes |

«Процесс» sits in the «Организация» section because its page is an
organization-level page; the model order (top to bottom of this table) is
the order BR-19's "first settings page" follows. The path segment after a
context prefix is the owning line's; the prefix is fixed (§ 5.5). Settings
entries carry no icon.

The «Организация» and «Проект» rows show where those sections sit and how
scope works. They are not mod-002's final entry list. mod-002's stage 3
decides which entries hold its pages (mod-002 BR-M1-23), and it may add some,
for instance the organization's project list or the project's own general
settings. Later lines add their entries the same way. BR-19's first page
follows the model order as it stands at that point.

### 4.2 Visibility (BR-7, BR-8 with its interim clause, D-18)

An entry is shown only when (1) its page exists in the build, (2) the shared
permission check passes for its code, and (3) for an organization-, project-
or team-scoped entry, the context it needs is real. Until mod-016, an
organization-scoped code is satisfied by any signed-in user; while the
switcher reads placeholder data no context is real, so every scoped entry
stays hidden. A group with no visible entry is hidden; «Настройки» is hidden
when no settings entry is visible. Nothing is ever shown disabled or as
"coming soon". Hidden is not protected: the page opened by address still
answers «Доступ ограничен».

In this task's build: «Основное меню» shows «Главная» and, to a holder of
`dictionaries.languages.read`, «Настройки»; «Меню настроек» shows
«Справочники» → «Языки».

### 4.3 Main navigation: expanded and rail

| Element | MUI | Behaviour and copy |
|---|---|---|
| Container | `Drawer variant="permanent"`, paper on `shell.navigation`, under the top bar | width `layout.navigationWidth` or `layout.railWidth`; width transition off under reduced motion |
| Region | `List component="nav" aria-label="Основное меню"` | — |
| Entry | `ListItemButton component={NavLink}` + `ListItemIcon` + `ListItemText` (built with react-router's `Link`, `selected` and `aria-current` from the model's marker: plan-frontend.md § 9 D-6) | selected marker § 2.6; `aria-current="page"` on its landing page, `"true"` on any page beneath it |
| Rail entry | the same `ListItemButton`, icon only, wrapped in `Tooltip placement="right"` | accessible name = the label; tooltip on hover and on focus |
| Group gap | `Divider` | — |
| Bottom block | «Настройки» entry, then the collapse toggle | pinned to the bottom (`mt: 'auto'`) |
| Collapse toggle | `ListItemButton` | expanded: `KeyboardDoubleArrowLeft` + «Свернуть меню», `aria-expanded="true"`; rail: `KeyboardDoubleArrowRight`, named «Развернуть меню», `aria-expanded="false"`, tooltip «Развернуть меню»; `aria-controls` the navigation |

Kept choice: the first toggle stores `expanded` or `rail` in
`localStorage` (key `mustask.navigation`); reload and new tabs of the browser
read it. Without a kept choice: expanded at ≥ 1280 px, rail at 1024–1279 px,
re-evaluated whenever the window crosses 1280 px. With a kept choice: it
applies at ≥ 1024 px whatever the width. Clearing browser storage returns to
the width default.

### 4.4 Settings navigation (BR-19)

Shown if and only if the address is a settings page (§ 4.1) and at least one
settings entry is visible.

| Element | MUI | Behaviour and copy |
|---|---|---|
| Main navigation | forced rail; collapse toggle hidden; «Настройки» marked current (`aria-current="true"`) | any rail entry leaves settings; no separate "back" |
| Container | `Drawer variant="permanent"` beside the rail, width `layout.settingsNavigationWidth`, `shell.navigation` with a `divider` left border | — |
| Region | `Box component="nav" aria-label="Меню настроек"` | — |
| Title | `Typography variant="h2" component="p"` «Настройки» | text, not a heading: the page's `h1` stays the first heading |
| Section | `List` + `ListSubheader` («Организация», «Проект», «Справочники») | subheader on `shell.navigation`, `body1` weight 500 |
| Entry | `ListItemButton component={NavLink}` + `ListItemText`, indented 16 px (built with react-router's `Link`: plan-frontend.md § 9 D-6) | `aria-current="page"` on its page, `"true"` below it (on `/dictionaries/languages/new` «Языки» keeps the marker with `aria-current="true"`) |

Activating «Настройки» opens the first visible settings entry in model order
(today `/dictionaries/languages`). Leaving settings returns the main
navigation to the kept choice or the width default. When a token renewal
removes the last visible settings entry, «Настройки» and «Меню настроек»
disappear without a reload and the page answers «Доступ ограничен».

### 4.5 Overlay below 1024 px (BR-10)

| Element | MUI | Behaviour |
|---|---|---|
| Trigger | «Меню» `IconButton` at the left of the top bar | present only below 1024 px, whatever is kept |
| Overlay | `Drawer variant="temporary" anchor="left"`, not kept mounted when closed (§ 12.2) | outside settings: the expanded main navigation (labels); on a settings page: the rail with «Настройки» current and «Меню настроек» beside it |
| Closing | Escape or a click on the backdrop | focus returns to «Меню» (MUI restores focus) |
| Choosing an entry | — | the overlay closes, the page opens, focus moves to `#main-content` |

The kept choice stays stored and applies again at ≥ 1024 px. No collapse
toggle is shown inside the overlay.

### 4.6 Re-evaluation (BR-8)

Navigation reads the same `can(code)` the pages use, plus the context
source's "is real" flag. A token renewal, a context switch or (from mod-016)
a refresh of served codes re-renders it without a page reload. The
navigation's own definitions do not change when mod-016 adds its source.

## 5. Context switcher (US-5; BR-11, BR-18; D-17..D-20, D-22)

### 5.1 Anatomy

```
[ Пример организации 1 ▾ ] / [ Пример проекта 1 ▾ ] / [ Пример команды 1 ▾ ]
```

| Element | MUI | Notes |
|---|---|---|
| Group | `Box role="group" aria-label="Контекст работы"` | in the banner, after the mark |
| Level | `Button variant="text" color="inherit" endIcon={<ExpandMore />}` | label = the name, one line, ellipsis at `layout.contextNameMaxWidth`; `aria-label` «Организация: <имя>», «Проект: <имя>», «Команда: <имя>»; `aria-haspopup="menu"`, `aria-expanded`; `Tooltip` with the full name |
| Separator | `Typography` «/», `aria-hidden` | `text.secondary` |
| Level menu | `Menu variant="selectedMenu"` + `ListSubheader` + `MenuItem role="menuitemradio"` | opens with focus on the current entry; current entry `aria-checked="true"` and a `Check` icon (`text.primary`, § 2.2); organizations and projects in alphabetical order, teams with the project's first team first and the rest alphabetical (mod-002 BR-M1-24 sorts) |

Keyboard: Enter, Space or ArrowDown on a level opens its menu; ArrowUp and
ArrowDown move; Enter chooses; Escape closes and returns focus to the level
(US-12).

### 5.2 Choosing

Choosing an entry closes the menu and resets the levels below it to their
first entry (the first team is the project's default until mod-016, D-19).
On a page with no context in its address («Главная», «Языки») nothing else
changes and no request is sent (§ 5.5 for context pages).

### 5.3 Menus and copy

| Level | Menu subheader | Entries | Ends with |
|---|---|---|---|
| Organization | «Организации» | the organizations | `Divider` + `MenuItem` «Все организации» (`ListItemIcon` `ListAltOutlined`), a link to the organization list (D-20). Hidden in this build (BR-7); shown from mod-002 |
| Project | «Проекты» | the current organization's projects | — |
| Team | «Команды» | the current project's teams | — |

### 5.4 States

| State | Shown in place of the levels | Copy | Owner |
|---|---|---|---|
| Ready | three levels | names | this task (placeholder data) |
| Loading | three `Skeleton variant="rounded"` 120 × 24 px inside `role="status"` | `aria-label` «Загрузка контекста» | designed here, wired by mod-002 |
| Failed | `ErrorOutlineOutlined` icon (error colour) + text + `Button size="small" variant="text"` | «Не удалось загрузить организации» + «Повторить»; below 1024 px the text is visually hidden (`visuallyHidden`), so screen readers still read it as the group's content, and it shows as the icon's tooltip | designed here, wired by mod-002 |
| No organizations | `Typography` (this build) or `Link component={RouterLink}` to the organization list (mod-002) | «Нет организаций»; no project or team level | this task / mod-002 |
| Organization without projects | the organization level + `Typography` `text.secondary` | «Нет проектов»; no team level | this task |
| Project without teams | organization and project levels + `Typography` | «Нет команд» | this task |
| Long name | the level truncated with an ellipsis | full name in `aria-label` and the tooltip; truncation is not "cut off" | this task |
| Projects loading (mod-002 D-n) | the organization level, then two `Skeleton variant="rounded"` 120 × 24 px in one `role="status"` | `aria-label` «Загрузка контекста» | designed by mod-002, rendered by the switcher from the source's level status |
| Teams loading (mod-002 D-n) | the organization and project levels, then one skeleton in the same `role="status"` | as above | as above |
| Projects failed (mod-002 D-n) | the organization level, then the failed block of the Failed row | «Не удалось загрузить проекты» + «Повторить», which retries the project level; below 1024 px as the Failed row | as above |
| Teams failed (mod-002 D-n) | the organization and project levels, then the failed block | «Не удалось загрузить команды» + «Повторить», which retries the team level; below 1024 px as the Failed row | as above |
| Remembered selection gone (mod-002 fallback) | the fallback context, levels as Ready | `Snackbar` with `Alert severity="info"`, 6 s, bottom left: «Ранее выбранный контекст недоступен. Выбрано: <организация> / <проект> / <команда>.» A level with nothing in it is left out («Выбрано: Бета.») | designed here, rendered by the switcher from the source (mod-002) |

The switcher draws the levels from the top and stops at the first level that
is not ready: a loading level and every level below it are the skeletons, a
failed level is the failed block with that level's text. When the
organization level is the one loading or failed, this gives the Loading and
Failed rows above as they stand (mod-002 D-n, DC-11).

The placeholder dataset is the one in US-5; it is fixed in the panel, sends
no request and keeps no selection across a reload.

### 5.5 Context in the address (BR-18; D-7, D-17, D-22)

| Level | Prefix | Example (prototype) |
|---|---|---|
| Organization | `/organizations/:organizationId/…` | `/organizations/org-1/settings/general` |
| Project | `/organizations/:organizationId/projects/:projectId/…` | `/organizations/org-1/projects/prj-1/workitems` |
| Team | `/organizations/:organizationId/projects/:projectId/teams/:teamId/…` | `/organizations/org-1/projects/prj-1/teams/team-1/boards/stories` |

- Route params are untyped strings; the page validates them, so a malformed
  and an unknown id get the same answer.
- On such a page the switcher shows the address's context; the levels below
  what the address carries show the current selection, and changing them
  does not change the page (an organization-level page keeps its address
  when the project or team changes).
- Choosing another entry at a level the address carries opens the landing
  page of the same navigation entry in the new context (§ 4.1), after the
  lower levels reset to their first entries. A page addressing one entity
  (a work item, one team, one area) goes to its entry's landing page. If
  that entry is not visible in the new context (for instance a project entry
  in an organization without projects), «Главная» opens. Navigation is
  re-evaluated for the new context in the same render.
- A link whose organization, project or team is not found answers, for the
  outermost level not found, «Организация не найдена», «Проект не найден» or
  «Команда не найдена» (§ 7.2), and the switcher keeps its selection.
- Pages with no context in the address never move on a switch.

### 5.6 Narrow window

Below 1024 px the three levels stay, names cut at 96 px, no leading icons
(there are none at any width); the menus are the same. «Меню» precedes the
mark. The switcher's tab stops do not change.

### 5.7 Hand-over to mod-002 (D-19, D-20)

mod-002 replaces the placeholder source with real lists behind the same
context-source interface (status `loading` / `failed` / `ready`, the tree,
`select(level, id)`, `isReal`), shows «Все организации» and links «Нет
организаций», remembers the selection with its fallback and the notice of
§ 5.4, makes context addresses set the selection, and builds the routes of
§ 5.5 and the tree registry of § 10.7.

> Amended 2026-10-04 by mod-002 (its D-n and D-o, the amendment its DC-11
> rests on). The interface (`shared/lib/workContext.ts`) gains, additively:
> `levelStatus` — the project and team levels' own status, each `loading`,
> `failed` or `ready` (absent: ready), the organization level's status
> staying `status`; `retry(level)` — «Повторить» of the level that failed
> (no level: the organization level, as before); `fallback` and
> `dismissFallback` — the notice of § 5.4, which the source raises and the
> switcher renders and words. `app/` hands the source a
> `SwitchTargetResolver` built from the navigation model, so a choice at a
> level the address carries opens the entry of § 5.5 without the slice
> importing `app/`. The four rows of § 5.4 marked mod-002 D-n are the
> switcher's copy from then on.

## 6. Page header and command bar (US-6)

### 6.1 Layout

```
[trail — only below 1024 px]
<h1 Title>                                  [Primary] [⊕ Secondary] [⊕ Secondary] [⋯]
[Tab] [Tab] [Tab]                                   (tabs only when the page has views)
```

| Element | MUI | Notes |
|---|---|---|
| Header | `shared/ui/PageHeader` on the application background, padding-top 16 px, bottom 8 px | scrolls with the page |
| Title | `Typography variant="h1" component="h1"` | wraps, never truncated; sets the browser tab title |
| Command bar | `Stack direction="row" spacing={1}` at the right, minimum height `density.controlHeight` | keeps its height with no command, so the content does not shift (US-6) |
| Primary action | `Button variant="contained"` with a leading icon | at most one per header; leftmost in the bar |
| Secondary command | `Button variant="text"` with a leading icon | neutral label, accent icon (§ 2.2) |
| Overflow | `IconButton` `MoreHoriz` `aria-label="Ещё действия"` + `Menu` | measured with a `ResizeObserver`: from the end, secondary commands move into the menu until the rest fit, in their original order; the primary action never moves |
| Views | `Tabs` + `Tab component={RouterLink}` | each view is an address; the selected tab is underlined in the accent |

### 6.2 States and permissions

A command the user lacks the code for is not rendered; one unavailable for
a reason of state is `disabled` (BR-9). A reader on «Языки» sees the title
and an empty command bar of the same height.

## 7. Screen-state canon (US-7; BR-12, BR-13; ADR-0007, ADR-0019)

### 7.1 The frame

`shared/ui/StateFrame`: icon (`fontSize="large"`, `text.secondary`),
heading, hint (`body1`, `text.secondary`), at most one action
(`Button variant="outlined"` with a leading icon where one fits), centred in
the area it replaces, 64 px top padding. Not an alert. Its heading is an
`h1` when it replaces the whole page (access denied, not found) and sets the
tab title; otherwise an `h2` under the page header.

Loading is not the frame: it is a skeleton of the content (`Skeleton`
inside `role="status" aria-label="Загрузка данных"`), with the page header,
the filter bar and the real column headers already shown. Error is the
shared `ErrorBanner` (`Alert severity="error"` with the server's text and
«Повторить»), in place of the content, header and filter bar kept.
`ErrorBanner` may take an optional title (`AlertTitle`) above the text, such
as mod-001's «Не удалось загрузить процесс». Without one it reads as it does
today.

### 7.2 Variants and copy

| Variant | Icon | Heading | Hint | Action |
|---|---|---|---|---|
| Empty (page supplies texts) | `InboxOutlined` | page's | page's | page's, only if permitted |
| Empty after a filter | `FilterListOff` | «Ничего не найдено» | page's | «Сбросить фильтр» |
| Access denied | `LockOutlined` | «Доступ ограничен» | «У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ.» | none |
| Unknown address | `SearchOff` | «Страница не найдена» | «Проверьте адрес или вернитесь на главную.» | «На главную» (`HomeOutlined`) |
| Organization not found | `SearchOff` | «Организация не найдена» | «Проверьте адрес или вернитесь на главную.» | «На главную» |
| Project not found | `SearchOff` | «Проект не найден» | same | «На главную» |
| Team not found | `SearchOff` | «Команда не найдена» | same | «На главную» |

An unknown address keeps the address (no redirect). A non-existent context
and one the user is not a member of (from mod-016) get the same frame.

### 7.3 Where each variant appears

| Area | Empty and empty after a filter replace | Kept above |
|---|---|---|
| Registry | the table with its header row and paging | page header, filter bar |
| Board | all columns | page header with tabs |
| Backlog | the table | page header, tabs; the planning pane if open |
| Work item list | the table; the preview pane closes | page header, filter bar |
| Whole page (denied, not found) | everything under the top bar | shell |

## 8. Registry with a filter bar — «Языки» (US-8, US-11)

### 8.1 Layout

```
Trail: Настройки / Справочники
Языки                                                           [+ Добавить язык]
+-----------------------------------------------------------------------------------+
| ⚲ [Код] [Эндоним] [По умолчанию ▾] [Активность ▾] [Порядок]   [⊘ Очистить фильтры] |  filter bar
+-----------------------------------------------------------------------------------+
| Код ↑ | Эндоним | По умолчанию | Активность | Порядок | Изменено |                 |  header row (grey)
| ru    | Русский | ★ да         | (Активен)  | 1       | 01.10.2026 10:00 — admin | ✎ |
| kk    | Қазақ тілі | нет       | (Активен)  | 2       | …        | ✎ ☆             |
|                                              1–10 из 25   ‹ ›                      |
+-----------------------------------------------------------------------------------+
```

### 8.2 Components

| Block | MUI | Notes |
|---|---|---|
| Page header | `PageHeader` title «Языки», primary «Добавить язык» (`Add`) | write code only |
| Filter bar | `shared/ui/FilterBar`: `Paper variant="outlined"` band, `Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}` (MUI 9's `Stack` takes no system props), `role="group" aria-label="Фильтры"`, leading `FilterList` (`aria-hidden`) | one band at 1440 px, wraps when narrower |
| «Код», «Эндоним», «Порядок» | `TextField size="small"` | as today |
| «По умолчанию» | `TextField select size="small"`: «Все», «Да», «Нет» | as today |
| «Активность» | `TextField select size="small"`: «Все», «Активные», «Неактивные» | as today |
| «Очистить фильтры» | `Button variant="text" startIcon={<FilterListOff />}` at the band's right end | shown only while a filter is set; clears every filter and shows page 1 |
| Table | `TableContainer component={Paper variant="outlined"}` (horizontal scroll inside), `Table size="small" aria-label="Реестр языков"` | the page never scrolls sideways |
| Sortable header | `TableCell sortDirection` + `TableSortLabel` | `aria-sort` follows the direction; first activation ascending, then descending |
| «По умолчанию» cell | `Star` (`warning`) + «да», or «нет» | as today |
| «Активность» cell | `Chip size="small"` «Активен» (`success`) / «Неактивен» (default) | as today |
| Row actions | `IconButton size="small"` + `Tooltip`: `Edit` «Редактировать», named «Редактировать <код>»; on an active non-default row `StarBorder` «Сделать языком по умолчанию», named «Сделать языком по умолчанию <код>» | always visible, reached by Tab; the actions column header stays empty, as today |
| Paging | `TablePagination` in the same paper, `rowsPerPageOptions={[]}`, 10 per page | «1–10 из 25» from ruRU |
| Notice | `Snackbar`, 6 s | «Язык по умолчанию изменён на «<эндоним>» (<код>).» or ««<эндоним>» уже является языком по умолчанию.» (unchanged) |

### 8.3 States

- **Loading:** header and filter bar shown; `RegistrySkeleton` (moved to
  `shared/ui`): real column headers, 5 skeleton rows, `role="status"`
  «Загрузка данных». No page spinner.
- **Error:** `ErrorBanner` with the server's text and «Повторить» in place of
  the table.
- **Empty, administrator:** frame «Справочник пуст», hint «Добавьте первый
  язык кнопкой «Добавить язык» выше.», action «Добавить язык» (`Add`).
- **Empty, reader:** frame «Справочник пуст», hint «Языки ещё не добавлены.»,
  no action.
- **Empty after a filter:** filter bar keeps its values and shows «Очистить
  фильтры»; frame «Ничего не найдено», hint «Ни один язык не подходит под
  заданные условия фильтра.», action «Сбросить фильтр». Both controls clear
  every filter and show page 1.
- **Permissions:** without `dictionaries.languages.read` the shared
  «Доступ ограничен» frame, no request; a reader has no «Добавить язык» and
  no actions column.

Behaviour is unchanged: `LanguagesRegistryPage.test.tsx` passes without an
edit.

## 9. Dialogs and form pages (US-9, US-11)

### 9.1 Dialog anatomy

| Part | MUI | Notes |
|---|---|---|
| Container | `Dialog` (`maxWidth="xs"` confirmation, `"sm"` form dialog, `"lg"` settings dialog), `fullWidth` | — |
| Title row | `DialogTitle` + `IconButton` `Close` `aria-label="Закрыть"` at its right | — |
| Body | `DialogContent` | error `Alert` at its top when a request fails |
| Footer | `DialogActions`: «Отмена» (`variant="text"`) left of the primary (`variant="contained"`) | the primary is the only filled button |
| Busy | primary shows `CircularProgress size={16}` as its start icon; «Отмена», «Закрыть», the primary and the fields are disabled; `onClose` ignores Escape and backdrop clicks | — |

Escape, «Закрыть» or «Отмена» close an idle dialog with nothing changed;
focus returns to the control that opened it (MUI default).

### 9.2 «Назначить языком по умолчанию» (restyled)

Title «Назначить языком по умолчанию»; text «Сделать «<эндоним>» (<код>)
языком по умолчанию?» and «Все активные записи должны быть переведены на этот
язык — иначе изменение будет отклонено.»; footer «Отмена», «Назначить». A
refusal shows the server's text in an error `Alert` and the dialog stays.

### 9.3 Form page layout

```
Trail: Настройки / Справочники / Языки
Добавить язык
+--------------------------------------+
| [error Alert, when not tied to field]|
| Код                                  |
| helper / error                       |
| Эндоним                              |
| Порядок                              |
|                    [Отмена] [Добавить]|
+--------------------------------------+   Paper variant="outlined", padding 24 px,
                                            max width layout.formColumnWidth
```

No «К списку»: the trail's «Языки» and «Отмена» lead back (C-12). The first
field has focus on a create page. Fields: `TextField` with label and helper
text; an error turns the helper into the error text in the error colour and
sets `aria-invalid="true"`. While a request runs, every field, «Отмена» and
the primary are disabled.

### 9.4 Destructive confirmation

`ConfirmDialog` gains the «Закрыть» control and a destructive flag: its
primary is `color="error" variant="contained"` and names the action. Pattern
example (prototype, for mod-003): title «Удалить рабочий элемент навсегда?»,
text «Рабочий элемент PRM-BUG-7 «Падение при пустом заголовке» будет удалён
без возможности восстановления вместе с историей, обсуждением и
вложениями.», footer «Отмена», «Удалить навсегда».

A reversible action is not destructive-coloured: «Удалить рабочий элемент?»,
«Рабочий элемент PRM-US-12 получит статус «Удалена». Его можно будет
восстановить.», «Отмена», «Удалить».

### 9.5 Form dialog (create in a dialog)

Pattern example: «Новый рабочий элемент», `maxWidth="sm"`. Fields: «Тип»
(`TextField select`, each option with its token icon in its colour, preset
by the type picker), «Заголовок» (`TextField`, focused, required). Footer
«Отмена», «Создать».

| Field | Required | Validation | Error text |
|---|---|---|---|
| «Заголовок» | yes | 1..255 characters after trimming (doc-002 BR-M3-3) | «Введите заголовок.» / «Не более 255 символов.» |

### 9.6 Large settings dialog

Pattern example: «Настройки доски», `maxWidth="lg"`, height 80 % of the
window. Left: a `List` of sections (`ListSubheader` groups, `ListItemButton`
entries, selected marker as in navigation) — «Общие», «Колонки»,
«Дорожки», «Карточки». Right: the selected section's form, scrolling on its
own. Footer «Отмена», «Сохранить». mod-006 fills the sections.

### 9.7 No leave guard (D-22, R-27)

No form page, dialog or the work item form guards leaving with unsaved input
in the MVP: activating a navigation entry, the product mark or a trail link
opens the new page at once and drops the input, as today.

### 9.8 Language form (restyled)

| Mode | Title | Fields | Footer |
|---|---|---|---|
| Create `/dictionaries/languages/new` | «Добавить язык» | «Код» (focused), «Эндоним», «Порядок» | «Отмена», «Добавить» |
| Edit `/dictionaries/languages/:code` | «Язык: <код>» | «Код: <код> (нередактируемо)», «Эндоним», «Порядок», «Активен» (`Switch`), «Изменено: <дата> — <кто>» (`caption`) | «Отмена», «Сохранить»; while a version conflict is compared, `VersionConflictPanel` takes their place |

| Field | Helper | Validation | Error text |
|---|---|---|---|
| «Код» | «от 2 до 8 символов, формат BCP-47 (например, ru, kk-KZ)» | 2–8 characters, BCP-47 | «Код должен содержать от 2 до 8 символов и соответствовать формату BCP-47 (например, ru, kk-KZ).» |
| «Эндоним» | «(необязательно)» | ≤ 200 characters | «Не более 200 символов.» |
| «Порядок» | «(необязательно, по умолчанию 0)» | number | the server's text |

Server texts (409, field errors, «Сначала назначьте языком по умолчанию
другой язык — кнопка «Сделать языком по умолчанию» в реестре языков.») and
the conflict panel's texts are unchanged. States: loading `FormSkeleton`
(moved to `shared/ui`), load error `ErrorBanner` with «Повторить», no access
«Доступ ограничен».

## 10. Work-tracking patterns (US-10; BR-14; D-9)

Designed and prototyped here, built by their first consumer. Placeholder
content: project key `PRM`, the US-5 dataset, Russian copy.

### 10.1 «Рабочие элементы» (mod-003)

```
Рабочие элементы          [+ Новый рабочий элемент ▾] [⇪ Импорт рабочих элементов] [⇩ Экспорт в CSV] [⋯]
[⚲ Фильтр по ключевому слову] [Тип ▾] [Исполнитель ▾] [Статус ▾] [Область ▾] [Теги ▾]  [⊘ Очистить фильтры]
| ID         | Заголовок                         | Исполнитель  | Статус      | Область      | Теги          |  | Preview pane
| PRM-US-12  | 📘 Вход по ссылке из письма        | (АС) Анна С. | ● Активна   | Пример пр… 1 | (интерфейс)   |  | 360 px
```

| Block | MUI | Notes |
|---|---|---|
| Primary | `Button variant="contained"` «Новый рабочий элемент», `startIcon` `Add`, `endIcon` `ExpandMore`, `aria-haspopup="menu"` | opens the type picker |
| Type picker | `Menu` + `MenuItem` + `ListItemIcon` (token icon in token colour) + `ListItemText` | «Эпик», «Фича», «Пользовательская история», «Задача», «Баг»; choosing one opens the form dialog (§ 9.5) preset to it |
| Secondary | «Импорт рабочих элементов» (`FileUpload`), «Экспорт в CSV» (`FileDownload`) | demonstrate overflow into «Ещё действия» |
| Filter bar | `FilterBar` with `TextField` «Фильтр по ключевому слову» (`Search` adornment) and `TextField select` with `slotProps={{ select: { multiple: true } }}` (MUI 9 has no `SelectProps`), `Checkbox` + `ListItemText` options: «Тип», «Исполнитель», «Статус», «Область», «Теги» | «Очистить фильтры» as in § 8 |
| List | `Table size="small" aria-label="Рабочие элементы"` | columns «ID», «Заголовок» (type icon + link to the form), «Исполнитель» (`Avatar` 24 px + name, or «Не назначен»), «Статус» (category dot + state name), «Область», «Теги» (`Chip size="small"`) |
| Row selection | click or Enter/Space on the row (`aria-selected`, background `shell.rowSelected`) | opens the preview pane; `aria-selected` on a row is exposed only inside `role="grid"`, so mod-003 gives the list that role with the WAI-ARIA grid keyboard model, or names the selection another way |

Empty: «Рабочих элементов пока нет», «Создайте первый рабочий элемент
кнопкой «Новый рабочий элемент» выше.», «Новый рабочий элемент». Empty after
a filter: «Ничего не найдено», «Ни один рабочий элемент не подходит под
заданные условия фильтра.», «Сбросить фильтр». Loading: skeleton rows under
the real headers. Error: `ErrorBanner`.

### 10.2 Preview side pane

`Box component="aside" aria-label="Просмотр рабочего элемента"` at the right,
`layout.sidePaneWidth`, content surface with a `divider` left border, its own
scroll. Header: «Открыть» (`OpenInNew`, opens the form) and «Закрыть»
(`Close`, `aria-label="Закрыть область просмотра"`). Body: the form's band,
header strip and «Детали» sections in one column (§ 10.3). At ≥ 1280 px the
pane stands beside the list and the list narrows; below 1280 px it opens over
the list as `Drawer variant="temporary" anchor="right"`, Escape closing it
and returning focus to the row. The planning pane (§ 10.5) follows the same
width rule.

### 10.3 Work item form (mod-003)

```
▌📘 Пользовательская история PRM-US-12                         [Сохранить] [⋯]
▌[ Вход по ссылке из письма__________________________ ]
 Исполнитель (АС) Анна Смирнова  Статус ● Активна  Область …  Итерация …  Теги (интерфейс) (+)
 [Детали] [История] [Связи] [Вложения]
 +------------------------------------------+  +-------------------------+
 | Описание                                 |  | Планирование            |
 | Критерии приёмки                         |  |  Приоритет  2           |
 | Обсуждение                               |  | Связанная работа        |
 +------------------------------------------+  |  Родитель  PRM-FEAT-3   |
                                               +-------------------------+
```

| Block | MUI | Copy and behaviour |
|---|---|---|
| Band | `Box` on content surface with a 4 px left stripe in the type colour | first line, the page's `h1`, styled as `subtitle2`: type icon + «Пользовательская история PRM-US-12»; second line, the editable title: `InputBase` 20 px, `aria-label="Заголовок"` |
| Band right end | `Button variant="contained"` «Сохранить» (disabled until something changes) + `IconButton` `MoreHoriz` «Ещё действия» → `Menu` «Удалить» (`DeleteOutlineOutlined`) | «Удалить» opens the reversible confirmation (§ 9.4) |
| Header strip | `Stack` of label/value pairs on the application background: «Исполнитель» (`Autocomplete` with avatars, «Не назначен»), «Статус» (`TextField select`, options with category dots; «Удалена» never offered), «Область», «Итерация» (`Autocomplete`), «Теги» (`Autocomplete multiple freeSolo` with `Chip`s, each chip's delete control named «Удалить тег «<тег>»», and «Добавить тег») | labels `subtitle2` `text.secondary` |
| Tabs | `Tabs` «Детали», «История», «Связи», «Вложения» | each tab is an address (`…/PRM-US-12`, `…/history`, `…/links`, `…/attachments`) |
| «Детали» | `Grid` two columns at ≥ 1280 px (8/4), one column below; sections are `h2` with a `divider` rule | wide: «Описание», «Критерии приёмки», «Обсуждение»; narrow: «Планирование» («Приоритет»), «Связанная работа» («Родитель») |
| «Обсуждение» | `TextField multiline` placeholder «Добавьте комментарий», with the same text as its accessible name (a placeholder alone does not name a field) + `Button variant="text"` «Добавить комментарий»; comments as `List` with `Avatar`, name, date | — |
| «История» | `List` of revisions: «<кто> изменил(а) «<поле>»: «<было>» → «<стало>»», date | — |
| «Связи» | groups «Родитель», «Дочерние» with type icon, id, title, state; «Добавить связь» | — |
| «Вложения» | `List`, empty frame «Вложений нет» with «Добавить вложение» | — |
| Deleted item | every field read-only; `Alert severity="info"` above the tabs: «Рабочий элемент удалён и доступен только для чтения. Восстановите его, чтобы вносить изменения.»; «Восстановить» (`Restore`, contained) in place of «Сохранить»; «Ещё действия» → «Удалить навсегда» (error colour, § 9.4) | — |

Browser tab: «Пользовательская история PRM-US-12 — Mustask», the `h1`'s text
(BR-6: the page's title is its only `h1`, and the tab repeats it). The
editable title is a field, not the heading. Trail: «Рабочие элементы»
(link). Loading: band and strip skeletons; error: `ErrorBanner`; not found:
mod-003's call within the canon.

Address: the prototype puts the form under the project prefix
(`…/projects/:projectId/workitems/PRM-US-12`). doc-002 US-4 asks only that
the address name the organization and then the identifier, so mod-003
chooses the prefix, organization or project, within BR-18.

### 10.4 Board (mod-006)

> **Superseded in part** by feat-001 BR-H-15 and BR-H-16 (its spec § Screens
> and copy), built by feat-026: the page and its navigation entry are
> «Канбан», not «Доски», and the level is chosen by the drop-down «Уровень:
> <уровень>» under the `h1` in place of the tabs «Эпики», «Фичи», «Истории» —
> on «Бэклоги» (§ 10.5) too. The address segment `boards` is unchanged. The
> rows below are left as written.

| Block | MUI | Copy and behaviour |
|---|---|---|
| Header | `PageHeader` «Доски», tabs «Эпики», «Фичи», «Истории» (addresses `…/boards/epics`, `…/features`, `…/stories`); secondary «Показать бэклог» (`FormatListBulleted`), «Настройки доски» (`SettingsOutlined`, opens § 9.6) | no primary on a board |
| Columns | `Stack direction="row"` scrolling horizontally inside its container; each column `layout.boardColumnWidth` | «Новые», «В работе», «Решённые», «Завершённые» (category labels as default headers) |
| Column header | `Typography` name + count (`caption`), `IconButton` `ChevronLeft` «Свернуть колонку «<имя>»» | collapsed: a `layout.boardColumnCollapsedWidth` strip with the vertical name and count, `IconButton` `ChevronRight` «Развернуть колонку «<имя>»» |
| «Новый элемент» | `Button variant="text" startIcon={<Add />}` at the top of the first column | opens the type picker of the level's types |
| Card | `Card` (elevation 1) with a 4 px left stripe in the type colour; line 1 type icon + id + title (`Link`); line 2 category dot + state name; line 3 tags as `Chip size="small"` | the title link opens the work item form |
| Keyboard path | each card has an `IconButton` `MoreHoriz` named «Ещё действия: <id>» → `Menu` with the subheader «Переместить в колонку» and the other columns' names | dragging is never the only way (mod-006) |

Empty: the frame replaces all columns: «На доске нет рабочих элементов»,
«Новые рабочие элементы этого уровня появятся в колонке «Новые».», «Новый
рабочий элемент».

### 10.5 Ordered backlog with the planning pane (mod-006, mod-007)

> **Superseded in part** by feat-001 BR-H-15 and BR-H-16, built by feat-026:
> the tabs are the drop-down «Уровень: <уровень>», as on «Канбан» (§ 10.4),
> and «Показать доску» reads «Показать канбан». The rows below are left as
> written.

> **Superseded in part** by feat-027 (its spec BR-T-1, BR-T-13, BR-T-14,
> BR-T-16 and `design.md` § 3, § 4), 2026-10-09..11: the page head is the
> team's name as the `h1` with a drop-down to another team, one tab «Бэклог»
> and the level drop-down at the tab row's right end, «Новый рабочий элемент»
> and «Показать канбан» at the top right (the same head is on «Канбан»,
> § 10.4); the table is the hierarchy column, «Порядок», «Тип», «Заголовок»
> (the tree, with «⋮»), «Статус», «Трудоёмкость», «Бизнес-ценность», «Вид
> ценности», with no drag-handle column — the whole row drags, and the
> handle's menu «Переместить <id>» is the top-level row's «Ещё действия»
> (feat-027 D-11). The rows below are left as written.

| Block | MUI | Copy and behaviour |
|---|---|---|
| Header | «Бэклоги», tabs «Эпики», «Фичи», «Истории»; primary «Новый рабочий элемент» (picker of the level's types); secondary «Показать доску» (`ViewWeekOutlined`), «Планирование» (`ViewSidebarOutlined`, `aria-pressed`) | — |
| Table | `Table size="small" aria-label="Бэклог"` | «Порядок», drag handle, «Тип» (icon + name), «Заголовок», «Статус», «Итерация» |
| Drag handle | `IconButton` `DragIndicator` named «Переместить <id>» → `Menu` «В начало», «Выше», «Ниже», «В конец» | keyboard path for reordering |
| Planning pane | `Box component="aside" aria-label="Планирование"`, `layout.sidePaneWidth`, title «Планирование» (`h2`), close `IconButton` «Закрыть панель «Планирование»», hint «Перетащите рабочий элемент на итерацию, чтобы запланировать его.» | iteration cards: name (link), dates, «Нет запланированной работы» or «Запланировано: <n>»; «Новая итерация» (`Add`, text) at the end |

Empty: «Бэклог пуст», «Добавьте рабочий элемент — он встанет в конец
бэклога.», «Новый рабочий элемент».

### 10.6 Type and category language

Type icons and dots follow § 2.3 everywhere: list, board card, backlog,
form band, type picker, header-strip status. The prototype's «Язык
оформления» page shows the five tokens and five categories with their labels.

### 10.7 Tree registry — «Области», «Итерации» (built by mod-002, D-19)

| Block | MUI | Copy and behaviour |
|---|---|---|
| Header | `PageHeader` «Области» / «Итерации», trail «Настройки / Проект»; secondary commands «Новый» (`Add`) and «Новый дочерний» (`Add`), the latter disabled until a row is selected (BR-9) | — |
| Table | `Table size="small"` (`aria-label` «Области» / «Итерации») | areas: «Область»; iterations: «Итерация», «Начало», «Окончание» |
| Row | `TableRow` with `aria-level`, selectable (`aria-selected`, `shell.rowSelected`); name indented 24 px per level | `aria-level`, `aria-expanded` and `aria-selected` on a row are exposed only inside `role="treegrid"`: the table takes that role, and mod-002's design fixes the WAI-ARIA treegrid keyboard model (arrows move and expand, Enter selects) |
| Expand | `IconButton` `ChevronRight` / `ExpandMore`, named «Развернуть «<имя>»» / «Свернуть «<имя>»», `aria-expanded` | — |
| Row actions | `IconButton` `MoreHoriz` «Действия: <имя>» → `Menu` «Новый дочерний», «Переименовать», «Удалить» | reached by Tab |

Empty («Итерации»): «Итераций пока нет», «Создайте первую итерацию кнопкой
«Новый» выше.», no action in the frame. mod-002 and mod-007 may name the
commands for their nodes in their own designs.

### 10.8 Empty work-tracking surfaces

All three (list, board, backlog) use the one `StateFrame` with their own
heading and «Новый рабочий элемент» (§ 10.1, § 10.4, § 10.5).

### 10.9 «Организации» placeholder (D-20)

The target of «Все организации» and «Нет организаций»: a registry page
«Организации» at `/organizations`, built by mod-002. The prototype shows a
placeholder table. With no organizations it shows the canon's empty frame
«Организаций пока нет» (mod-002 US-1, whose hint and action are mod-002's).
This task builds nothing there.

### 10.10 Vocabulary

Terms come from the doc-001 glossary; «Задача» names only the type, never
the generic entity («Рабочий элемент»). The reference tracker is never
named.

## 11. Restyled existing screens (US-11; BR-15)

### 11.1 «Главная»

`PageHeader` «Главная», no commands, no trail. Content: `Paper
variant="outlined"`, padding 24 px: «Добро пожаловать в Mustask»
(`Typography variant="h2" component="p"`) and «Mustask — трекер задач.
Разделы появятся в меню слева по мере их готовности.» (`body1`,
`text.secondary`). `/` redirects to `/home`.

### 11.2 Dictionaries

§ 8 and § 9.2, § 9.8. Behaviour unchanged except the change table C-1..C-15
of US-11, which this design realises: C-1..C-3 (§ 3.2, § 3.3), C-4 (§ 4.1),
C-5 (§ 7.2), C-6 and C-7 (§ 3.4), C-8 and C-9 (§ 8.3), C-10 and C-11 (§ 8.2),
C-12 (§ 9.3), C-13 (§ 9.1), C-14 (§ 7.2 `AccessDenied` moves to `shared/ui`),
C-15 (§ 11.3, § 11.4).

### 11.3 Auth card (all `AnonymousScreen` states)

Outside the shell, on the application background, a centred `Card
variant="outlined"` of `layout.authCardWidth`, padding 32 px, headed by
«Mustask» (`h1`). Texts, actions and their effects unchanged:

| State | Content | Action |
|---|---|---|
| restoring | `CircularProgress` + «Выполняется вход…» | none |
| signed out | `Alert severity="info"` «Вы вышли из системы.» | «Войти» |
| login failed | `Alert severity="error"` «Вход не выполнен.» + one of «Вход отменён на странице входа.», «Сервис аутентификации недоступен, повторите позже.», «Не удалось завершить вход, попробуйте ещё раз.», «Предыдущая попытка входа не была завершена.» | «Войти» |
| expired | `Alert severity="warning"` «Сессия истекла, войдите заново.» | «Войти» |
| auth unavailable | `Alert severity="warning"` «Сервис аутентификации недоступен, повторите позже.» | «Повторить» |
| sign-out failed | `Alert severity="error"` «Выход не завершён: сервис аутентификации недоступен.» | «Повторить выход» |

Actions are full-width `variant="contained"` buttons.

### 11.4 Crash screen

Outside the shell, on the application background, centred, `role="alert"`:
`ErrorOutlineOutlined` (`fontSize="large"`, `text.secondary`), «Что-то пошло не так»
(`h1`), «Обновить страницу» (`variant="contained"`). Effect unchanged.

### 11.5 Parallel modules

mod-001's slice is not touched; its «Процесс» entry is in the model (§ 4.1)
and is registered by whichever task merges second (D-12). mod-014 has no
screen (D-21). Keycloak's login theme is tech-003's.

## 12. Keyboard and screen reader (US-12)

### 12.1 Skip link

Tab from the top of the document focuses «Перейти к содержимому», shown at
the top-left; Enter moves focus to `#main-content`.

### 12.2 Landmarks

| Page | Landmarks |
|---|---|
| Outside settings | one `banner` (the `AppBar` as `header`), one `main`, `nav` «Основное меню»; «Путь к странице» when the page has a trail |
| Settings page | `banner`, `main`, `nav` «Путь к странице», `nav` «Основное меню», `nav` «Меню настроек» |
| Below 1024 px | the overlay's navigations exist only while it is open; the trail's `nav` moves into `main` with the page header |

The switcher is a `group`, the filter bar a `group`, the side panes `aside`
(`complementary`) with names.

### 12.3 Tab order

- «Главная», 1440 px: skip link → «Mustask» → organization, project, team
  levels → user menu → «Главная» → «Настройки» → «Свернуть меню» → the
  page's controls.
- «Языки», 1440 px: skip link → «Mustask» → three levels → user menu →
  rail «Главная» → rail «Настройки» → «Языки» in «Меню настроек» → the
  page's controls. Plain-text trail segments are not tab stops; a trail link
  sits between the switcher and the user menu.
- Below 1024 px «Меню» comes first after the skip link.

### 12.4 Menus

Switcher levels, the user menu, the type picker, «Ещё действия» and every
dropdown: Enter, Space or ArrowDown opens with focus on the current (or
first) item; ArrowUp/ArrowDown move; Home/End jump; Enter chooses; Escape
closes and returns focus to the trigger.

### 12.5 Focus and announcement

The 2 px accent outline (§ 2.6) on every focused control, ≥ 3:1 on every
surface. Skeletons are `role="status"` «Загрузка данных»; `ErrorBanner` is
an `Alert` (`role="alert"`); canon frames are not alerts. The collapsed rail
announces each entry's label and shows it as a tooltip on focus.

### 12.6 Zoom and reflow (D-8)

At 200 % of a 1280 px window (640 CSS px) the overlay layout of § 3.6
applies, every control stays operable and fully named, nothing overlaps, and
only a wide table scrolls inside its own container. Reflow to 320 CSS px is
not claimed (ADR-0024).

### 12.7 Reduced motion

Under `prefers-reduced-motion: reduce` the navigation changes width and the
overlay opens without animation.

## 13. Hand-over notes for the frontend developer

- `shared/ui` gains: `PageHeader` (title, trail, commands with overflow,
  tabs), `StateFrame` with `AccessDenied` and `NotFound` (`page`,
  `organization`, `project`, `team`), `FilterBar`, `RegistrySkeleton`,
  `FormSkeleton`, `FormDialog`, and `ConfirmDialog` with «Закрыть» and the
  destructive flag; `ErrorBanner` stays.
- `app/` gains the frame (top bar, skip link, switcher with its placeholder
  source, user menu, main and settings navigation, overlay), the navigation
  model and the theme (`app/theme`: tokens, theme, work-tracking lookups and
  their tests).
- The colour-literal check is a Vitest test outside `app/theme`.
- `docs/conventions/frontend.md` § MUI is brought in line with ADR-0019 and
  ADR-0024 in the implementation stage, once ADR-0024 is accepted.

## 14. Prototype

[`prototype/index.html`](prototype/index.html) — one self-contained file:
inline CSS, inline SVG copies of the Material icon glyphs the panel already
renders, vanilla JavaScript, the theme tokens of § 2 as CSS variables. It
loads nothing from any host (no fonts, scripts or images; Roboto is used if
installed, else the system sans-serif — *superseded by feat-027 D-7: the panel
draws Segoe UI at weight 600, which the prototype does not*) and adds no
dependency (BR-17, R-26).
Open it from disk or from any static server; addresses are hash routes
(`#/organizations/org-1/projects/prj-1/workitems`), so the browser's address
bar shows the context in the address.

A «Панель прототипа» (bottom right, dashed, not part of the design) switches:

- the view: «Целевое MVP» (every entry and mod-002's switcher) or «Сборка
  mod-015» (only what this task builds: «Главная», «Настройки» → «Языки»,
  no «Все организации», «Нет организаций» as text, later pages answer
  «Страница не найдена»);
- the role (dictionaries administrator, reader, no dictionaries code), the
  display name (with name, username only, none);
- the switcher's data (example, loading, failed, no organizations, an
  organization without projects, a 100-character name, a remembered
  selection that is gone);
- data states of «Языки», the work item list, board and backlog (data,
  loading, error, empty);
- links to every screen and dialog: the token sheet «Язык оформления», the
  work-tracking pages, a deleted work item, the organization settings, the
  organization list, an unknown address, the three not-found links, every
  auth card state, the crash screen, and the dialogs of US-9.

Narrow the window to see the rail (below 1280 px) and the overlay with the
trail in the page header (below 1024 px). A start state can also be put in
the address after `?`, e.g. `#/home?panel=0&view=build&role=reader`,
`&switcher=loading`, `&open=sw-org`, `&dialog=destroy:PRM-BUG-7`.

The «Спринты» and «Запросы» pages of the prototype only mark where release-2
hubs sit in the frame; their empty-state wording is an example, owned and
finalised by mod-011 and mod-008. The same holds for the bodies of the
organization settings pages (mod-002, mod-016, mod-001, mod-004).

## 15. Reconciliation with mod-001's design

mod-001's `design.md` (6577518, § "Adopting mod-015") derived its layout B
from this task's spec and asked this design to confirm six points. The
answers:

| mod-001 derived | Answer |
|---|---|
| «Процесс» is a settings entry of its own, beside «Организация», «Проект» and «Справочники»; section, order and icon to confirm | «Процесс» is the third entry of the «Организация» section, after «Общие» and «Участники» (§ 4.1). It is an organization-level page. Settings entries carry no icon. |
| The trail is «Настройки», plain text; whether a one-segment trail is drawn | The trail has two plain segments, «Настройки / Организация» (§ 3.4). mod-001's T-26 and its layout B sketch change to that. |
| Shared frames: icon, heading, hint, at most one action; the not-found icon; the error component's title slot | Yes (§ 7.1). Not found uses `SearchOff`, and its heading is the page's `h1` (§ 7.2). `ErrorBanner` takes an optional title, so «Не удалось загрузить процесс» fits (§ 7.1). |
| A page header with no command keeps its height | Yes (§ 6.1, § 6.2). No actions slot is needed. |
| The token lookup takes a token name, the category colour is looked up by code; whether the theme carries the category's shape | `typeAppearance(token)` and `categoryColour(code)` (§ 2.3). The shape follows the code (`Removed` a ring, every other code a dot), drawn as an 8 px circle, not as `FiberManualRecord` / `RadioButtonUnchecked` glyphs. Slices read the lookups through the theme. The theme holds no category labels: mod-001's T-12..T-16 stay the one list. |
| Both layouts go to one column below `lg`; whether two cards fit at `lg` | The theme's `lg` is 1280 px (§ 2.5), not MUI's 1200, so layout B's one-column rule applies below 1280 px. At 1280 px on a settings page the content is 1280 − 48 (rail) − 240 (settings navigation) − 48 (padding) = 944 px. That gives two cards of about 464 px, wide enough for the three status columns. |
