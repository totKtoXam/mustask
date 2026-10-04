# Plan — frontend (mod-015-app-shell-design-system)

Stage 6a (L task, deep pass) per `docs/ai-workflow/stages/05-frontend.md`.
No product code is written in this pass. Inputs, read at 0a9dd46:
`status.md` (D-1..D-23), `spec.md`, US-1..US-12, `design.md`, the prototype,
ADR-0024 (proposed), ADR-0007, ADR-0019, `docs/conventions/frontend.md`, and
the panel as it stands: `src/app` (`App`, `routes`, `AppLayout`), `shared/`,
`features/home`, `features/dictionaries`, `features/auth`, their tests, and
`e2e/specs`.

Stories built in code: **US-1..US-9, US-11, US-12**. **US-10** is a design
and prototype deliverable (BR-14, D-9). It has no code here, and QA checks
`design.md` § 10 against its scenarios. There is no backend work and no
`api-contract.md`, because stages 4–5 were skipped (D-1). The one
cross-module seam is the context-source interface that mod-002 fills
(design § 5.7), fixed in § 4.3 below.

Before reading on, here is the starting point. `app/theme` does not exist
yet: the theme is `createTheme({}, ruRU)` inline in `App.tsx`. `AppLayout`
is a blue app bar with a two-item drawer. `shared/ui` holds `ConfirmDialog`
(no consumer), `ErrorBanner`, `ErrorBoundary` and `LoadingBox`. The
dictionaries slice owns `AccessDenied`, `RegistrySkeleton`, `FormSkeleton` and
`RequirePermission`. An unknown address redirects to `/home`. No file under
`src/` holds a colour literal today, so the BR-1 check starts green.

## 1. Shape of the change

- **New slices: none.** The shell is `app/`'s, which frontend.md gives the
  router, theme, providers, layout and guards. The patterns go to
  `shared/ui`. The existing slices are restyled in place.
- **Routes:** the addresses do not change. `*` stops redirecting and renders
  «Страница не найдена» inside the shell (C-5). `/` still redirects to
  `/home`.
- **Dependency direction:** `app/` imports slices only through their
  barrels and `shared/`. `shared/` imports neither `app/` nor `features/`.
  Slices reach theme values only through the theme object (`useTheme()`,
  `sx` callbacks), never by importing `app/theme` (design § 2.3).
- **Server state:** nothing new. TanStack Query stays in the dictionaries
  slice, with the same keys. The shell, theme, navigation and switcher send
  no request (NFR Network, BR-11).
- **Client state:**
  - the navigation's kept choice, in `localStorage`, key `mustask.navigation`;
  - the switcher selection, in `useState` inside the shell's work-context
    provider (gone on reload, BR-11);
  - the overlay's open flag;
  - the page-header registration that drives the trail (§ 4.5).
  No global store.
- **Dependencies:** none added (BR-17). Everything is `@mui/material` 9.4
  core and `@mui/icons-material` 9.4; every icon design § 2.8 names is
  exported by the installed version. The colour-literal check uses
  `typescript`, already a devDependency.

## 2. Files

### 2.1 `src/app/theme/` (new): the only place for appearance

| File | Holds |
|---|---|
| `tokens.ts` | Every value of design § 2.1–§ 2.5: colour roles, semantic colours, `shell.*` greys, type and category colours, `layout.*`, `density.*`, breakpoints, type scale. The only file in `src/` with hex literals. |
| `muiAugmentation.d.ts` | MUI module augmentation: `Palette.shell` (`navigation`, `navigationHover`, `navigationSelected`, `inputBorder`, `tableHeader`, `rowSelected`), `Theme.layout`, `Theme.density`, `Theme.workTracking`. These are types only, so `shared/ui` and slices type-check against them without importing `app/`. |
| `workTracking.ts` | `APPEARANCE_TOKENS = ['epic','feature','story','task','bug'] as const`, `CATEGORY_CODES = ['Proposed','InProgress','Resolved','Completed','Removed'] as const`, `typeAppearance(token: string) → { icon, colour }` (the fallback is `HelpOutlineOutlined` + `text.primary`), `categoryColour(code: string) → string` (the fallback is `text.secondary`). There are no category labels: mod-001 keeps the one list (design § 2.3). |
| `components.ts` | The `components` block of design § 2.6, plus the overrides of § 3.2 below. |
| `theme.ts` | `createTheme(options, ruRU)`, exporting `theme`. The two lookups are attached as functions under `theme.workTracking` (§ 3.3). |
| `contrast.ts` | `contrastRatio(foreground, background)`, the WCAG 2.1 relative-luminance formula, used by the contrast test. |
| `index.ts` | Barrel: `theme`, plus the token tuples for tests. |
| `theme.test.ts`, `contrast.test.ts`, `workTracking.test.ts` | US-1 (§ 11). |

`App.tsx` imports `theme` from `./theme` and drops its inline `createTheme`.

### 2.2 `src/app/` shell and navigation

| File | Holds |
|---|---|
| `AppLayout.tsx` | Rewritten. The frame of design § 3.1: the skip link, `TopBar`, the main navigation, the settings navigation or the overlay, and `<main id="main-content">`. It provides the work context, the access check and the page chrome (§ 4). |
| `AppLayout.test.tsx` | The five tests named by C-1..C-4 are rewritten. New frame tests are added (§ 11). |
| `routes.tsx` | `*` → `<NotFound level="page" />` inside `AppLayout` (C-5). |
| `routes.test.tsx` (new) | The real routes under a stub `AuthContext`: US-2's title outline, US-7's not-found and refused pages, the no-leave-guard scenario. |
| `navigation/types.ts` | `NavigationScope`, `NavigationEntry`, `SettingsSection`, `NavigationModel`. |
| `navigation/model.ts` | **The navigation registry** (design § 4.1): «Главная» (top), an empty work group, and the settings sections «Организация», «Проект», «Справочники» → «Языки». Only built pages are registered, so registration stands for "its page exists in the build" (BR-7). mod-001's «Процесс» row is added here by whichever task merges second (D-12, § 10 pass 5). |
| `navigation/visibility.ts` | Pure functions: `visibleModel(model, access, context)`, `entryMarker(pathname, entry, context) → 'page' \| 'true' \| null`, `isSettingsPage`, `firstSettingsLanding`, `deriveTrail` (§ 4.5). |
| `navigation/useNavigationLayout.ts` | Resolves `{ main: 'expanded' \| 'rail' \| 'overlay', settingsShown, toggle }` from the width class, the kept choice and `isSettingsPage` (BR-10, BR-19). |
| `navigation/keptChoice.ts` | Reads and writes `localStorage['mustask.navigation']` (`expanded` \| `rail`). |
| `navigation/NavigationModelContext.ts` | Defaults to `model.ts`. Tests inject a model holding a test entry gated by an organization-scoped code (US-3). |
| `shell/TopBar.tsx` | `AppBar`, «Меню» (below 1024 px), the product mark, the switcher, the vertical divider, the trail slot and the user menu. |
| `shell/SkipLink.tsx` | «Перейти к содержимому» (§ 8). |
| `shell/UserMenu.tsx`, `shell/initials.ts` | Avatar initials, «Профиль: <имя>», the name row, «Выйти» (US-2, C-1, C-2). |
| `shell/ContextSwitcher.tsx` | The three levels, their menus and their states (§ 4.3). |
| `shell/placeholderContext.ts` | **The switcher data registry:** the US-5 dataset and `usePlaceholderContextSource()`, which keeps the selection in `useState` and resets the lower levels. |
| `shell/MainNavigation.tsx` | Expanded list or rail, the bottom block with «Настройки» and the collapse toggle. |
| `shell/SettingsNavigation.tsx` | «Меню настроек» with the title «Настройки» and its sections. |
| `shell/NavigationOverlay.tsx` | The temporary drawer below 1024 px. |
| `shell/*.test.ts(x)` | The unit and component tests of § 11. |

### 2.3 `src/shared/`

| File | Holds |
|---|---|
| `config/product.ts` | `PRODUCT_NAME = 'Mustask'`, used by the product mark, the auth card, the tab title and the crash screen. Today the literal sits in three places. |
| `lib/documentTitle.ts` | `formatDocumentTitle(title \| null)`, which gives «<title> — Mustask» or «Mustask», and `useDocumentTitle`. |
| `lib/workContext.ts` | The context-source interface (§ 4.3), `WorkContext` and `useWorkContext()`. Later slices read the current context from here. |
| `lib/access.ts` | `PermissionScope`, `AccessCheck = (code, scope) => boolean`, `AccessCheckContext` (fail closed by default) and `useAccessCheck()` (BR-8, § 4.2). |
| `ui/pageChrome.ts` | `PageChromeContext`: `registerHeader(ancestors?)`, `trail`, `trailPlacement` (§ 4.5). The default outside the shell is no trail and a no-op registration. |
| `ui/PageTrail.tsx` | `Breadcrumbs` «Путь к странице». Links are `Link component={RouterLink}`, plain segments are `Typography`, each segment is truncated at `layout.trailSegmentMaxWidth` and carries a `Tooltip`. The top bar uses it at 1024 px and wider, the page header below. |
| `ui/PageHeader.tsx`, `ui/commandOverflow.ts` | Title `h1` and the tab title, at most one primary action, secondary text commands, «Ещё действия» overflow, tabs as addresses (US-6, § 5.1). |
| `ui/StateFrame.tsx` | The canon frame, plus the `AccessDenied` and `NotFound` (`page` \| `organization` \| `project` \| `team`) variants (US-7, C-14). |
| `ui/AccessDenied.test.tsx` | Moved from the dictionaries slice, assertions unchanged (C-14). |
| `ui/FilterBar.tsx`, `ui/SortableHeaderCell.tsx` | The filter band with «Очистить фильтры», and the sortable header that emits `aria-sort` (US-8). |
| `ui/RegistrySkeleton.tsx`, `ui/FormSkeleton.tsx` + their tests | Moved from the dictionaries slice. The tests move unchanged, and the literal heights become spacing units (§ 9, D-12). |
| `ui/DialogTitleBar.tsx` | `DialogTitle` plus a **sibling** «Закрыть» `IconButton` (§ 9, D-5). |
| `ui/ConfirmDialog.tsx` | Rebuilt per design § 9.1 and § 9.4. Props: `title`, `children`, `confirmLabel`, `busy`, `error`, `destructive = false`, `onConfirm`, `onClose`. |
| `ui/FormDialog.tsx` | The form-dialog pattern of design § 9.5 (`maxWidth="sm"`, «Отмена» / submit, busy, error banner). |
| `ui/FormPanel.tsx` | The form-page column: `Paper variant="outlined"`, padding 24 px, `maxWidth: layout.formColumnWidth`, the error banner slot above the first field, and the footer below the last field (design § 9.3). |
| `ui/ErrorBanner.tsx` | Gains an optional `title` (`AlertTitle`). Without one it renders as today (design § 7.1). |
| `ui/ErrorBoundary.tsx` | The restyled crash screen (C-15). |
| `ui/LoadingBox.tsx` | Unchanged (`aria-label="Загрузка"`, which `AnonymousScreen.test` asserts). |

### 2.4 Slices restyled

| File | Change |
|---|---|
| `features/home/HomePage.tsx` | `PageHeader` «Главная» and the outlined paper of design § 11.1. |
| `features/dictionaries/index.ts` | Also exports `dictionariesPermissions` and `LANGUAGES_PATH`, so the navigation model stops deep-importing `permissions.ts`. |
| `features/dictionaries/pages/LanguagesRegistryPage.tsx` | `PageHeader`, `FilterBar`, `SortableHeaderCell`, the outlined table container, and the `StateFrame` empties (C-8..C-11). |
| `features/dictionaries/pages/LanguageFormPage.tsx` | `PageHeader` without «К списку», `FormPanel` (C-12). |
| `features/dictionaries/components/DefaultLanguageDialog.tsx` | A thin consumer of `ConfirmDialog`. It keeps its mutation (C-13). |
| `features/dictionaries/components/{AccessDenied,RegistrySkeleton,FormSkeleton}.tsx` | Removed: moved to `shared/ui`. |
| `features/auth/AnonymousScreen.tsx` | The auth card of design § 11.3, with the tab titled «Mustask» (C-15). |

`RequirePermission` and `VersionConflictPanel` stay where they are. The
theme defaults restyle them, and their code does not change.

### 2.5 Test infrastructure

| File | Change |
|---|---|
| `src/test/testingLibrary.tsx` (new) | Re-exports `@testing-library/react/pure` and wraps `render` in `ThemeProvider theme={theme}`, composing with a caller's `wrapper` (D-8). |
| `vite.config.ts` | `test.alias: [{ find: /^@testing-library\/react$/, replacement: <src/test/testingLibrary.tsx> }]`, with a comment saying why. |
| `src/test/viewport.ts` (new), `src/test/setup.ts` | A controllable `window.matchMedia` stub that evaluates `(min-width)`, `(max-width)`, `and` and `(prefers-reduced-motion: reduce)` against a set width. It defaults to 1440 px with motion allowed, is reset `beforeEach`, and has `setViewportWidth()` and `setReducedMotion()` (D-9). |
| `src/test/colourLiterals.test.ts` (new) | The BR-1 check (§ 7). |

No existing test file is edited except those the change table names
(§ 11.3). New scenarios go into new test files, so the untouched files stay
byte-identical and US-11's "only the named tests are edited" can be checked
with `git diff --stat -- '*.test.*'`.

### 2.6 E2E

`e2e/specs/14-header-name.spec.ts`, `04-sign-out.spec.ts` and
`05-logout-keycloak-down.spec.ts` are rewritten (C-1, C-2; § 12). Nothing
else under `e2e/` changes.

### 2.7 Docs

- `docs/conventions/frontend.md` § MUI is rewritten in pass 5, and only once
  ADR-0024 is recorded accepted (§ 14).
- `status.md` gets a `## Log` line per pass.

**Not touched:** `deploy/keycloak/**` (D-2), mod-001's slice (D-4, D-16),
`src/backend/**`, `package.json` dependencies, ADR files other than reading
them.

## 3. Theme (US-1; BR-1..BR-5)

### 3.1 Tokens

Values and key names exactly as design § 2.1 (colour roles, `palette.shell.*`,
semantic colours), § 2.3 (five tokens, five categories), § 2.4 (type scale),
§ 2.5 (`layout.*`, `density.*`, breakpoints `xs 0 / sm 640 / md 1024 / lg 1280
/ xl 1536`). Three points need care:

- **`palette.secondary`** is the neutral text colour (`#242424`, white
  contrast text). `MuiCheckbox`, `MuiRadio`, `MuiSwitch` and
  `MuiCircularProgress` default to `color="secondary"`, which keeps them off
  the accent (design § 2.2, R-34).
- **Type sizes are set per variant**, `h1`..`caption` per § 2.4.
  `typography.fontSize: 14` alone leaves `body1` at 16 px, because MUI builds
  `body1` from 16 (D-2).
- **`layout.contextNameMaxWidth`** is an object `{ wide: 200, medium: 160,
  narrow: 96 }` keyed by the width class (§ 4.4).

### 3.2 Component defaults (`components.ts`)

Everything in design § 2.6, plus these overrides that the design implies
but does not list. Each keeps BR-2's closed list of accent uses (D-4):

| Component | Override | Why |
|---|---|---|
| `MuiAppBar` | `backgroundColor: background.paper`, `color: text.primary`, the divider drawn **inside** the bar as an inset shadow built from `palette.divider` | The top bar uses the content surface (BR-2). An outer border would make it 49 px, not the 48 px QA measures (D-3). |
| `mixins.toolbar` | `{ minHeight: layout.topBarHeight }` | MUI's default toolbar is 56/64 px, responsive (D-3). |
| `MuiMenuItem`, `MuiListItemButton` (outside navigation) | `&.Mui-selected` background `action.selected`, not the primary tint | MUI tints selection with the accent. Navigation's selected marker (`shell.navigationSelected` + 3 px accent bar) is set by the navigation components through a `variant`-like class, per § 2.6. |
| `MuiInputLabel` | `&.Mui-focused` colour `text.secondary` | Design § 2.2. |
| `MuiTab` | selected label `text.primary`, weight 500 | Design § 2.6. MUI defaults the selected label to primary. |
| `MuiAvatar` | `width`/`height` `layout.avatarSize`, initials font size 13 px, background `text.secondary` | Keeps the avatar's dimensions out of call sites (BR-1). |
| `MuiButtonBase` | `disableRipple`; `:focus-visible` outline 2 px accent, offset 2 px | Design § 2.6, US-12. |
| `MuiTableRow` | `height: density.registryRowHeight`, hover `action.hover` | BR-3. |

### 3.3 The work-tracking lookups

`typeAppearance` and `categoryColour` are attached to the theme object after
`createTheme`, under `theme.workTracking`. The icon components therefore
never pass through MUI's deep merge of the options, which walks plain
objects, and nothing depends on how that merge treats a component object.
The token-to-icon map lives inside the closure. The lookups take a token name and a category code only (BR-4,
R-25), and an unknown name returns the fallback without throwing. No slice
in this task reads them. mod-001 is the first consumer (D-16).

### 3.4 Checks run by `npm test`

- `theme.test.ts`:
  - density: `body1`'s font size converted from rem through
    `typography.htmlFontSize` is 14 px; `layout.topBarHeight` and
    `mixins.toolbar.minHeight` are 48; `density.registryRowHeight` is at most
    40, and so is the `MuiTableRow` override;
  - colour roles: the five role tokens exist, `shell.navigation` ≠
    `background.default`, and the `MuiAppBar` override's background is
    `background.paper`;
  - secondary labels: a rendered `Button variant="text"` and
    `variant="outlined"` compute `color` equal to `text.primary`;
  - locale: a rendered `TablePagination` of 25 rows, 10 per page, reads
    «1–10 из 25».
- `contrast.test.ts`: every pair in design § 2.1 and § 2.3 meets its
  threshold, 4.5:1 for text and 3:1 for non-text. Each type and category
  colour is checked on content, the application background, row hover and
  row selected. The tightest pairs, recomputed for this plan, are `epic` on
  row selected at 3.98:1 and secondary text on navigation selected at
  4.52:1. The test asserts thresholds, not the design's rounded figures.
- `workTracking.test.ts`: there are exactly five tokens, distinct in both
  colour and icon, and five categories with distinct colours. None equals
  `primary.main`. `BUG` stored with token `task` answers `task`; «Готово» in
  `InProgress` answers `InProgress`'s colour; an unknown token answers the
  fallback icon and `text.primary`.

## 4. Shell (US-2..US-5, US-12)

### 4.1 Frame

The layout follows design § 3.1–§ 3.2.

- **Top bar:** `AppBar position="fixed" color="inherit" elevation={0}`,
  above the drawers at `zIndex.drawer + 1`.
- **Main navigation:** a `Drawer variant="permanent"` whose paper sits under
  the bar.
- **Main region:** `<Box component="main" id="main-content" tabIndex={-1}>`,
  offset by `mixins.toolbar`, with `minWidth: 0` so a wide table scrolls
  inside its own container rather than the page (US-4). Content padding is
  24 px at 1024 px and wider, 16 px below.
- **Scrolling:** the document scrolls, and the page header scrolls with it
  (US-6).
- **Top bar contents in DOM order** (the tab order of US-12): skip link,
  «Меню» (below 1024 px), the mark (`Link component={RouterLink} to="/home"`,
  not a heading, C-3), the switcher, the divider (only when there is a
  trail), the trail slot, and the user menu.

### 4.2 Navigation and visibility (US-3; BR-7, BR-8, BR-19)

- **Model:** `model.ts` lists the entries with their label, icon, `scope`,
  `permission` and `landing(context)`. «Настройки» is not an entry. It is
  derived: shown iff a settings entry is visible, and its landing is the
  first visible settings entry in model order (BR-19).
- **Access check:** `AppLayout` provides `AccessCheckContext` with
  `(code, scope) => scope === 'instance' ? can(code) : status ===
  'authenticated'`. The non-instance branch is BR-8's interim clause (D-18):
  an organization-scoped code is satisfied by any signed-in user until
  mod-016 swaps this one function. Pages gated by an organization-scoped code
  use the same `useAccessCheck()`, so the page and the navigation cannot
  disagree.
- **Visibility:** an entry is visible when it is registered, its access check
  passes, and, for a scope other than `instance`, `workContext.isReal` holds
  and the scope's level is selected. The placeholder source reports
  `isReal: false`, so every scoped entry stays hidden in this build.
- **Re-evaluation:** `can` changes identity on a token renewal and the work
  context on a switch, and both re-render the navigation without a reload.
- **Markers:** `ListItemButton component={Link}` (react-router `Link`, not
  `NavLink`, D-6), `selected` and `aria-current` from `entryMarker`. The
  entry carries `aria-current="page"` on its landing and `"true"` beneath it.
  The rail's «Настройки» always carries `"true"` on a settings page (US-3,
  design § 4.4).

### 4.3 Context switcher (US-5; BR-11, BR-18)

The context-source interface is the hand-over to mod-002 (design § 5.7). It
lives in `shared/lib/workContext.ts`:

```ts
type WorkContextLevel = 'organization' | 'project' | 'team';
interface WorkContextSource {
  status: 'loading' | 'failed' | 'ready';
  organizations: { id: string; name: string;
    projects: { id: string; name: string; teams: { id: string; name: string }[] }[] }[];
  selection: { organizationId?: string; projectId?: string; teamId?: string };
  isReal: boolean;                 // false for the placeholder dataset
  organizationListPath?: string;   // undefined: «Все организации» hidden, «Нет организаций» plain text
  select(level: WorkContextLevel, id: string): void; // resets the levels below to their first entries
  retry?(): void;                  // «Повторить» in the failed state
}
```

- **Order:** the source orders its lists. The switcher renders them as
  given. mod-002 applies BR-M1-24's order: alphabetical organizations and
  projects, and the project's first team first.
- **Level control:** a `Button variant="text" color="inherit"` with
  `endIcon={<ExpandMore />}`. The name sits in a `span` that ends in an
  ellipsis at `layout.contextNameMaxWidth[class]`. The accessible name is
  «Организация: <имя>», «Проект: <имя>» or «Команда: <имя>»;
  `aria-haspopup="menu"` and `aria-expanded` are set; a `Tooltip` shows the
  full name.
- **Group and separators:** the group is `role="group" aria-label="Контекст
  работы"`. Each separator is an `aria-hidden` «/».
- **Menu:** `Menu variant="selectedMenu"` with a `ListSubheader`
  («Организации», «Проекты», «Команды»). Each entry is a
  `MenuItem role="menuitemradio"` with `aria-checked`. The current entry has
  a `Check` in `text.primary`, and its selected background is neutral
  (§ 3.2).
- **States built here:**
  - ready;
  - «Нет организаций»;
  - «Нет проектов»;
  - «Нет команд»;
  - a long name;
  - loading: three `Skeleton variant="rounded"` in `role="status"`
    «Загрузка контекста»;
  - failed: `ErrorOutlineOutlined`, «Не удалось загрузить организации» and
    «Повторить», with the text visually hidden below 1024 px but kept in its
    tooltip.

  Loading and failed render from `status` now, so mod-002 only supplies the
  status (D-15). The fallback notice for a remembered selection that is gone
  stays mod-002's, because it needs remembering, which this task does not do.
- **Placeholder:** `usePlaceholderContextSource()` holds the US-5 dataset and
  keeps the selection in `useState` inside `AppLayout`, so a reload resets
  it. It sends no request and leaves `organizationListPath` undefined.

### 4.4 Collapse, widths and the overlay (US-4; BR-10, D-8)

- **Width class:** `useMediaQuery` on the theme breakpoints gives `wide`
  (≥ 1280 px), `medium` (1024–1279 px) or `narrow` (< 1024 px).
- **Layout:** `useNavigationLayout` resolves the layout as follows.

  | Situation | Main navigation | Settings navigation |
  |---|---|---|
  | `narrow`, any page | `overlay` | inside the overlay |
  | settings page, at least one settings entry visible | `rail` (toggle hidden) | `permanent` |
  | any other page, a choice kept | the kept choice | — |
  | any other page, no choice kept, `wide` | `expanded` | — |
  | any other page, no choice kept, `medium` | `rail` | — |

  The first row wins over the second.
- **Kept choice:** the toggle writes `expanded` or `rail` to `localStorage`.
  Clearing storage returns to the width default.
- **Overlay:** `Drawer variant="temporary" anchor="left"`, not kept mounted.
  Escape or the backdrop closes it, and MUI restores focus to «Меню».
  Choosing an entry closes it with `disableRestoreFocus`, and the
  transition's `onExited` focuses `#main-content` (BR-10).
- **Reduced motion:** the permanent drawers' width transition sits under an
  `@media (prefers-reduced-motion: reduce)` rule that sets it to `none`. The
  overlay gets `transitionDuration={0}` from
  `useMediaQuery('(prefers-reduced-motion: reduce)')` (design § 2.6).
- **Toggle:** a `ListItemButton` showing «Свернуть меню» when expanded and
  named «Развернуть меню» as a rail, with `aria-expanded` and
  `aria-controls` pointing at the navigation's id.
- **Rail entries:** each is wrapped in `Tooltip placement="right"` and
  carries an explicit `aria-label` equal to its label.

### 4.5 Page trail (US-2; BR-6) and titles

**The mechanism (D-7).** A page that renders `PageHeader` registers through
`PageChromeContext`. The shell then derives the trail from the navigation
model for the current address:

- for a visible settings entry: «Настройки» and the section's label, both
  plain text;
- for an address beneath an entry's landing (marker `true`): the entry's
  label as a link to the landing;
- after those: any `ancestors` the page passes to `PageHeader`.

The output matches US-2's table and design § 3.4. Pages that render a
full-page frame (refused, not found) mount no `PageHeader`, so they have no
trail. At 1024 px and wider the shell renders `PageTrail` in the top bar.
Below 1024 px it sets `trailPlacement: 'header'`, and `PageHeader` renders the
same `PageTrail` above its title. The `nav` name and links stay the same
(design § 3.6). Outside the shell, in a page test, there is no trail.

`PageHeader` keys its registration by the serialized ancestors, so a new
array identity on every render does not loop.

**Titles.** `PageHeader` and the full-page `StateFrame` variants call
`useDocumentTitle(title)`, which sets «<title> — Mustask».
`AnonymousScreen` sets «Mustask». The crash screen sets «Mustask» in
`componentDidCatch`.

### 4.6 User menu (US-2; C-1, C-2)

- **Button:** `IconButton` with `aria-label` «Профиль: <имя>» (or «Профиль»
  when there is no name), `aria-haspopup="menu"` and `aria-expanded`. Inside
  it, an `Avatar` holds the initials or `PersonOutlineOutlined`.
- **Initials:** `initials(displayName)` trims the name, splits it on
  whitespace, takes the first letter of each of the first two words and
  upper-cases them. One word gives one letter. An empty name gives `null`,
  which means the icon is shown instead.
- **Menu:** the name as non-focusable `Typography`, then a `Divider`, then
  `MenuItem` «Выйти» with `Logout`, which calls `useAuth().signOut`. A failed
  sign-out lands on the existing «Выход не завершён…» screen, unchanged.

## 5. `shared/ui` patterns

### 5.1 `PageHeader` (US-6)

**Props:** `title`, `ancestors?`, `primaryAction?: Command` (exactly one, so
"at most one filled command" holds by type), `commands?: Command[]`
(secondary, always `variant="text"` with a leading icon) and `views?: { label,
to }[]` (rendered as `Tabs` + `Tab component={RouterLink}`, the selected tab
taken from the pathname).

**Rendering:**

- The title is `Typography variant="h1" component="h1"`.
- The command bar has `minHeight: density.controlHeight`, so a reader's empty
  bar keeps the height (US-6).
- A `Command` with `onClick` renders a `button`. One with `to` renders a link.
  «Добавить язык» uses `onClick`, because two unchanged tests count it as a
  `button` (D-13).

**Overflow:** `commandOverflow.ts` exports `splitCommands(widths, available,
reserved)`. It is a pure function: from the end, secondary commands move into
«Ещё действия» until the rest fit, and the primary never moves.
`PageHeader` measures with `ResizeObserver` when it exists and keeps
everything inline when it does not, which is jsdom's case (D-32). No page in
this task has a secondary command. The overflow is built for later lines and
proven by its own tests.

### 5.2 `StateFrame` (US-7; BR-12, BR-13)

**Props:** `icon`, `heading`, `hint`, `action?` and `fullPage?`.

- **Layout:** centred, 64 px top padding (`spacing(8)`). The icon is
  `fontSize="large"` in `text.secondary`. The action is an outlined `Button`
  with a leading icon. The frame is not an alert.
- **Heading level:** with `fullPage` the heading is an `h1` and sets the tab
  title. Without it, the heading is an `h2` under the page header.

**Variants:**

| Component | Copy | Action |
|---|---|---|
| `AccessDenied` | «Доступ ограничен», hint unchanged (C-14) | none |
| `NotFound level="page"` | «Страница не найдена» | «На главную» (link to `/home`, `HomeOutlined`) |
| `NotFound level="organization"` | «Организация не найдена» | «На главную» |
| `NotFound level="project"` | «Проект не найден» | «На главную» |
| `NotFound level="team"` | «Команда не найдена» | «На главную» |

Every `NotFound` variant has the hint «Проверьте адрес или вернитесь на
главную.» The variant copy lives in the component (ADR-0007). A page supplies
only its own empty texts.

### 5.3 Registry pieces (US-8)

- **`FilterBar`:** a `Paper variant="outlined"` band holding
  `Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}`,
  with `role="group" aria-label="Фильтры"` and a leading `FilterList` that is
  `aria-hidden`. Props: `active` and `onClear`. «Очистить фильтры» (`text`,
  `FilterListOff`) shows only while `active`.
- **`SortableHeaderCell`:** a `TableCell sortDirection` wrapping a
  `TableSortLabel`. MUI emits `aria-sort` from `sortDirection`. There is
  **no** visually hidden "sorted" text, so the column header's accessible
  name stays its label (D-14).
- **Moved pieces:** `RegistrySkeleton` and `FormSkeleton` move unchanged
  except their literal sizes (D-12).

### 5.4 Dialogs and form pages (US-9)

- **`DialogTitleBar`:** the title row is `DialogTitle` (the `h2`) followed by
  an absolutely positioned **sibling** `IconButton` «Закрыть». Placed inside
  the `h2`, the button would turn the heading's name into «… Закрыть» (D-5).
- **`ConfirmDialog`:**
  - `maxWidth="xs"`;
  - a body slot, and an error `Alert` at its top;
  - a footer of «Отмена» (`text`) and the primary (`contained`, or with
    `destructive`, `color="error"`);
  - busy:
    - every control is disabled;
    - one `CircularProgress size={theme.spacing(2)}` is the primary's start
      icon;
    - `onClose` ignores Escape and backdrop clicks.

  MUI returns focus to the opener.
- **`FormDialog`:** `maxWidth="sm"`, a `form` with `onSubmit`, the same
  title bar and footer, and the same busy rules.
- **`FormPanel`:** see § 2.3. Field errors stay on `TextField` (`error`,
  `helperText`), which sets `aria-invalid="true"`.

## 6. Restyle of the existing screens (US-11; C-1..C-15)

| # | Where | What the code does |
|---|---|---|
| C-1, C-2 | `shell/UserMenu.tsx` | § 4.6 |
| C-3 | `shell/TopBar.tsx`, every `PageHeader` | The mark is a link. Titles are `h1`. |
| C-4 | `navigation/model.ts` | «Языки» under «Справочники». The main navigation shows «Настройки». |
| C-5 | `routes.tsx` | `*` → `NotFound level="page"`, address kept |
| C-6 | `deriveTrail`, `TopBar`, `PageHeader` | § 4.5 |
| C-7 | `useDocumentTitle` | § 4.5 |
| C-8 | `LanguagesRegistryPage` | `count === 0`: the `TableContainer` (header row and paging) is replaced by the frame. The page header and filter bar stay. |
| C-9 | `LanguagesRegistryPage` | A reader's empty hint is «Языки ещё не добавлены.», with no action. The administrator keeps today's hint and «Добавить язык» (`onClick`). |
| C-10 | `LanguagesRegistryPage` | `FilterBar active={isFilterActive} onClear={resetFilters}`. Both controls clear every filter and go to page 1. |
| C-11 | `LanguagesRegistryPage` | Headers through `SortableHeaderCell`. `toggleSort` is unchanged (ascending first). |
| C-12 | `LanguageFormPage` | «К списку» is gone. «Отмена» still navigates to `/dictionaries/languages`, and the trail's «Языки» links there. |
| C-13 | `DefaultLanguageDialog` → `ConfirmDialog` | «Закрыть» in the title row. Copy, mutation and the 409 text unchanged. |
| C-14 | `shared/ui/StateFrame.tsx` | `AccessDenied` moves with its test. Its texts are unchanged. |
| C-15 | `AnonymousScreen`, `ErrorBoundary` | See below. |

**C-15 in detail.** The auth card sits on `background.default`: a centred
`Card variant="outlined"` of `layout.authCardWidth`, padding `spacing(4)`,
with «Mustask» as `h1`. Actions are full-width `contained`. `LoadingBox`
stays for «Выполняется вход…». The crash screen keeps `role="alert"`, adds
`ErrorOutlineOutlined`, and makes «Что-то пошло не так» an `h1`. Texts,
actions and effects are unchanged.

Behaviour kept on purpose:

- the filter state stays local (`useState`), not in the address;
- no debounce;
- `keepPreviousData`;
- the Snackbar copy and its 6 s;
- the column order: «Код», «Эндоним», «По умолчанию», «Активность»,
  «Порядок», «Изменено». E2E 17 and 18 index cells by position.
- In edit mode the language form shows no helper texts, as today (D-11).

## 7. Colour-literal check (BR-1)

`src/test/colourLiterals.test.ts` sits in `src/test/` and so is outside its
own scope. It uses the `node` environment through a per-file
`@vitest-environment node` comment.

- **Scope:** every `.ts`/`.tsx` under `src/` except `app/theme/`, any
  `*.test.*` file and `src/test/`.
- **Parsing:** each file is parsed with the TypeScript compiler API
  (`ts.createSourceFile`), so comments and JSX text are never mistaken for
  literals.
- **What it visits:** `StringLiteral`, `NoSubstitutionTemplateLiteral`, and
  the head of a `TemplateExpression`.
- **Two rules:**
  - the whole literal matches `^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$`;
  - or its text begins with `rgb(`, `rgba(`, `hsl(` or `hsla(`, which also
    catches a template head such as `` `rgba(${…}` ``.
- **A self-test** runs on inline fixtures:
  - caught: `'#0F6CBD'`, `"#fff"`, `'rgba(0,0,0,.5)'`,
    `` `hsl(${h}, 50%, 50%)` ``;
  - not caught: `'#12'`, `'#'`, `'#main-content'`, JSX text «#12», a hex
    inside a comment.
- **The real scan:** asserts zero findings and prints `file:line` for each
  hit. Adding a literal to a slice fails the run (US-1).

One known edge: an address anchor made only of hex letters (for example
`'#add'`) would trip the rule as BR-1 words it. None exists. Such an anchor
would be renamed, not exempted, because BR-1 has no exception list.

Dimensions are not machine-checked (BR-1). Code review checks that every size
is a theme token, the spacing unit or a theme component override. § 9 D-12
lists the literals this task removes.

## 8. Accessibility (US-12; BR-5)

| Concern | Implementation |
|---|---|
| Skip link | First element in `AppLayout`. `Link href="#main-content"` is visually hidden until focused. Its click handler calls `preventDefault()` and focuses `#main-content`, so no hash enters the address (D-16). |
| Landmarks | One `banner` (`AppBar` renders `header`) and one `main`. `nav` «Основное меню» is the `List component="nav"`. `nav` «Меню настроек» appears only on settings pages. `nav` «Путь к странице» appears only with a trail. Below 1024 px the navigations exist only while the overlay is open. |
| Names | Switcher levels «Организация: …». User menu «Профиль: …». Rail entries by label. Toggle «Свернуть меню» / «Развернуть меню». «Меню». «Ещё действия». «Закрыть». Row actions «Редактировать <код>» and «Сделать языком по умолчанию <код>» (unchanged). |
| States announced | `aria-expanded` on the toggle, «Меню», the switcher levels and the user menu. `aria-current` (§ 4.2). `aria-sort`. `aria-checked` on switcher entries. `aria-invalid` on fields. |
| Live regions | Skeletons are `role="status"` «Загрузка данных». The switcher's loading state is `role="status"` «Загрузка контекста». `ErrorBanner` is an `Alert` (`role="alert"`). Canon frames are not alerts: E2E 15 asserts zero alerts on «Ничего не найдено». |
| Keyboard | MUI `Menu` gives Enter/Space/ArrowDown to open with focus on the current entry, arrows, Home/End, and Escape returning focus to the trigger. Row actions are always rendered, so Tab reaches them. Tooltips open on focus. |
| Focus indicator | The `:focus-visible` 2 px accent outline (§ 3.2) is at least 3:1 on every surface. The contrast test covers accent against each surface. |
| Motion | § 4.4 |
| Reflow | Below 1024 px: the overlay, the trail moving into the page header, and names cut at 96 px with tooltips. `minWidth: 0` on `main`. Tables scroll inside `TableContainer`. |

## 9. Where `design.md` and the code disagree, and the resolution

None of these changes a screen, a state or a string the design fixed. Each
is a mechanism the design names that would break an existing test or a rule,
or a gap the design leaves to the developer.

| # | Design says / the code has | Problem | Resolution |
|---|---|---|---|
| D-1 | § 2, § 13: tokens live in `app/theme` | It does not exist. The theme is inline in `App.tsx`. | Create `app/theme/` (§ 2.1). `App.tsx` imports it. |
| D-2 | § 2.4: `typography.fontSize` 14 | MUI builds `body1` from 16 px, so `body1` stays 16 px. | Set every variant's size and line height explicitly. The BR-3 test measures `body1`, not `fontSize`. |
| D-3 | § 2.5, § 3.2: a 48 px top bar with a bottom divider | `Toolbar`'s default `mixins.toolbar` is 56/64 px, and a 1 px border makes the bar 49 px. | Override `mixins.toolbar` and draw the divider inside the bar (§ 3.2). |
| D-4 | § 2.2 lists the controls kept off the accent | MUI's selected `MenuItem` and `ListItemButton` and the selected `Tab` label also default to the accent, outside BR-2's closed list. | Theme overrides (§ 3.2). |
| D-5 | § 9.1: "`DialogTitle` + `IconButton` «Закрыть»" | Inside the `h2`, the heading's name becomes «… Закрыть». Two unchanged tests query the dialog heading by its exact title. | The `IconButton` is a sibling of `DialogTitle`, absolutely positioned in the title row (`DialogTitleBar`). |
| D-6 | § 4.3, § 4.4: `ListItemButton component={NavLink}` | `NavLink` sets `aria-current="page"` on every descendant address, and cannot mark «Настройки» current on a settings page outside its own link. | react-router `Link`, with `selected` and `aria-current` from the model's `entryMarker`. «Настройки» is always `"true"` on settings pages, as US-3 and § 4.4 say. § 4.3's "page on its landing" rule applies to entries, and «Настройки» is derived, not an entry. |
| D-7 | § 3.4: "Every page passes its title and its ancestors to `PageHeader`" | Each settings page would copy «Настройки» and its section label from `app/`'s model into its slice. | The shell derives the trail from the model for a page that has a `PageHeader` mounted, and `ancestors` remains for deeper hierarchies (§ 4.5). The output equals US-2's table. mod-001's «Настройки / Организация» and mod-003's «Рабочие элементы» come free. |
| D-8 | § 2.3: slices read custom theme keys through `useTheme()` | Unchanged tests (`AnonymousScreen`, `ErrorBoundary`, `VersionConflictPanel`, `ErrorBanner`, and the moved `AccessDenied`, `RegistrySkeleton`, `FormSkeleton`) call RTL's `render` with no `ThemeProvider`. They get MUI's default theme, which has no `layout`, `density`, `shell` or `workTracking`, so the auth card's `layout.authCardWidth` would throw. | The Vitest alias renders every component test under the app theme (§ 2.5). No test file is edited. **Fallback** if the alias proves brittle in pass 1: `renderWithProviders` wraps the theme, and the components rendered by bare tests use only standard keys and the spacing unit (the auth card at `spacing(50)`, 400 px). That would be recorded as a deviation from § 2.5's `layout.authCardWidth`. |
| D-9 | § 3.6, § 4.3–§ 4.5: behaviour by width | jsdom has no `matchMedia`, so MUI's `useMediaQuery` answers `false` to every query, and every shell test would land in the narrow layout. | The `matchMedia` stub in setup (§ 2.5) defaults to 1440 px. Width scenarios set the width. |
| D-10 | § 9.4, § 13: "`ConfirmDialog` gains «Закрыть» and a destructive flag" | Today `ConfirmDialog` has no consumer and is always error-coloured. `DefaultLanguageDialog` builds its own `Dialog`. | Rebuild `ConfirmDialog` with `destructive` defaulting to `false` (accent primary), body content, busy and error slots. `DefaultLanguageDialog` consumes it. Its tests pass unchanged. |
| D-11 | § 9.8: the field table lists helpers «(необязательно)» and «(необязательно, по умолчанию 0)» without a mode | Today only the create form shows them, and C-1..C-15 is a closed list. | Helpers stay on the create form only. The edit form shows none, as today. |
| D-12 | § 9.1 `CircularProgress size={16}`. Code: `Skeleton` heights 24/20/40 and width 140, filter widths 150/100, `maxWidth: 480`, auth card `width: 380` | Literal dimensions, which BR-1 forbids at call sites. The auth card also differs from the design's 400 px. | Use the spacing unit (`theme.spacing(2)` for the progress, `spacing(3)`/`(2.5)`/`(5)`/`(17.5)` for the skeletons, `spacing(19)`/`(12)` for the filter widths) or a token (`layout.formColumnWidth`, `layout.authCardWidth` = 400). |
| D-13 | § 6.1, § 8.2: primary action as `Button` | A `RouterLink` would give it `role="link"`, and `LanguagesRegistryPage.test` counts and clicks «Добавить язык» as a `button`. | `onClick` + `navigate`. «На главную» in the not-found frame is a link, with no test constraint. |
| D-14 | § 8.2: sortable headers with `aria-sort` | MUI's sorting demo adds visually hidden "sorted …" text, which changes the column header's name. | `aria-sort` only, so the header's name stays its label. |
| D-15 | § 5.4: loading and failed switcher states are "designed here, wired by mod-002" | Rendering them only in mod-002 would make mod-002 edit the switcher UI. | Render them now from the source's `status`. mod-002 supplies the data. The fallback notice stays mod-002's (§ 4.3). |
| D-16 | § 3.2: skip link `href="#main-content"` | A fragment navigation puts `#main-content` into the address and does not reliably move focus. | The click handler focuses `main` and prevents the hash. `href` stays for semantics. |
| D-17 | ADR-0024 "When this ADR is accepted, frontend.md § MUI is rewritten in mod-015's implementation stage". Review notes: "The human accepts it together with gate-2". | `gate-2` is approved under delegation, but ADR-0024 still reads `Status: proposed`. | The frontend.md rewrite is held to pass 5 and runs only once the orchestrator records ADR-0024 accepted (§ 14). |
| D-18 | frontend.md: custom wrappers in `shared/ui` "if needed by ≥ 2 slices" | `PageHeader` tabs and overflow, `FormDialog` and the organization/project/team not-found variants have no second consumer in this task. | Follow BR-14 and ADR-0024, which make them the foundation later lines build on. Each ships with its own tests. frontend.md's rule is reconciled in the pass-5 rewrite. |
| D-19 | § 4.1: each entry records "whether its page exists in the build" | A flag that is always true for registered entries is dead data. | Registration is the flag: the model lists only built pages, and the prototype shows the rest. |
| D-20 | `AppLayout` deep-imports `features/dictionaries/permissions` | This deviates from frontend.md's barrel rule. | The dictionaries barrel exports the code and the path (§ 2.4). The auth slice has no barrel and stays deep-imported (out of scope, noted). |

## 10. Order of work: passes that each end green

"Green" means `npm run lint`, `npm test` and `npm run build` in
`src/frontend` plus `bash scripts/verify.sh`. Every commit inside a pass
builds (`docs/conventions/git.md`). Before each commit the diff and the
message are searched for the reference tracker's names (BR-16). The search
terms are never written into a file. Each pass ends with a `[dev]` Log line
naming its commits.

1. **Pass 1 — theme and test environment (US-1).**
   - `app/theme/*` and `App.tsx` on the new theme.
   - `src/test/testingLibrary.tsx` + the Vitest alias.
   - The `matchMedia` stub.
   - `colourLiterals.test.ts`.
   - The theme tests.

   Every existing test passes unedited under the new theme: this proves D-8's
   alias before anything depends on it.
2. **Pass 2 — `shared/ui` patterns (US-6, US-7, US-8, US-9 building
   blocks).**
   - `config/product.ts`, `lib/documentTitle.ts`, `ui/pageChrome.ts`,
     `PageTrail`, `PageHeader` + `commandOverflow`, `StateFrame` with
     `AccessDenied` and `NotFound`, `FilterBar`, `SortableHeaderCell`,
     `DialogTitleBar`, `ConfirmDialog`, `FormDialog`, `FormPanel`, and the
     `ErrorBanner` title.
   - Move `AccessDenied`, `RegistrySkeleton` and `FormSkeleton` with their
     tests (C-14), and repoint the dictionaries imports in the same commit.
3. **Pass 3 — shell, navigation, switcher (US-2, US-3, US-4, US-5, US-12).**
   - `lib/workContext.ts`, `lib/access.ts`, `app/navigation/*`,
     `app/shell/*`, `AppLayout`, `routes.tsx` (C-5), and the dictionaries
     barrel exports.
   - Rewrite the five `AppLayout.test.tsx` tests (C-1..C-4), add the shell
     tests, and rewrite E2E 14, 04 and 05 (C-1, C-2).
   - The pages still carry their own headers at this point. This is an
     intermediate state that passes the tests.
4. **Pass 4 — the existing screens on the patterns (US-11).** One commit
   each:
   1. «Главная»;
   2. «Языки» (C-8..C-11);
   3. the language form (C-12);
   4. the default-language dialog (C-13);
   5. the auth card and the crash screen (C-15);
   6. `routes.test.tsx` (US-2 title outline, US-7 not-found and refused,
      US-9 no leave guard).

   After it, `git diff --stat main -- '*.test.*'` shows edits only to the
   files § 11.3 names, plus new files.
5. **Pass 5 — closing.**
   - The frontend.md § MUI rewrite if ADR-0024 is accepted (D-17).
   - If mod-001 merged first and this branch was rebased onto it by the
     orchestrator, add the «Процесс» row to `model.ts` with an
     `organization` scope (D-12), and assert in `visibility.test.ts` that it
     stays hidden on placeholder data (US-11).
   - `npx playwright test --list` to confirm the rewritten specs compile.
   - A last BR-16 search over the whole branch diff and its commit messages.
   - Hand over to stage 7.

## 11. Test plan per story

### 11.1 Component and unit tests (Vitest), all in `npm test`

| Story | Test file | Scenarios covered |
|---|---|---|
| US-1 | `app/theme/theme.test.ts`, `contrast.test.ts`, `workTracking.test.ts`, `src/test/colourLiterals.test.ts` | All eight: density, colour roles, no colour literal, contrast, appearance tokens, lookups, unknown token, Russian locale |
| US-2 | `app/AppLayout.test.tsx`, `app/shell/UserMenu.test.tsx`, `app/shell/initials.test.ts`, `app/routes.test.tsx`, `app/navigation/visibility.test.ts` (`deriveTrail`) | Regions, the product mark leads home without reload, the trail (all rows), one `h1` and the tab per page (outline), user menu and sign-out, the initials outline, no display name. Pinned top bar and navigation while scrolling: QA. |
| US-3 | `app/AppLayout.test.tsx`, `app/navigation/visibility.test.ts` | Main navigation outside settings; «Настройки» opens the first settings page; direct settings address; leaving settings; no settings page; unbuilt pages never appear; the organization-scoped entry with a stub source going `isReal` (no reload, the page not refused through `useAccessCheck`); current markers (`page` / `true`, rail «Настройки» `true`); token renewal removes «Настройки» and «Меню настроек» and the page answers «Доступ ограничен» |
| US-4 | `app/navigation/useNavigationLayout.test.ts`, `app/AppLayout.test.tsx` | Collapse and expand with names, tooltips and `aria-expanded`; kept choice across a remount; defaults at 1440/1200/1440; kept choice holding at 1200; the narrow overlay (Escape and outside click return focus to «Меню», an entry moves focus to `main`, widening re-applies the choice); overlay on a settings page; settings pages keep the rail. Reduced motion: overlay `transitionDuration` 0 here, width transition in a browser by QA. No horizontal scroll: QA. |
| US-5 | `app/shell/ContextSwitcher.test.tsx` | Initial context and names; levels list only children; reset of lower levels; no request (MSW `request:start` listener asserts zero calls, not only `onUnhandledRequest`), and a remount shows the initial context; «Языки» ignores a switch (in `AppLayout.test.tsx`, with a request listener on the languages URL); no «Все организации»; 100-character name (accessible name and tooltip; the one-line ellipsis at 1024 px is QA's); no organizations; no projects; no teams; loading and failed with «Повторить» (D-15). The design-only scenario is QA's against `design.md`. |
| US-6 | `shared/ui/PageHeader.test.tsx`, `commandOverflow.test.ts`, `LanguagesRegistryPage.patterns.test.tsx` | Title `h1` and primary contained with a leading icon; at most one primary (by type and render); reader with no command and the bar's `min-height` kept; tabs as links with the selected one marked; overflow into «Ещё действия» in order (stubbed `ResizeObserver`); trail inline when placement is `header`. Header scrolling with the page: QA. |
| US-7 | `shared/ui/StateFrame.test.tsx`, `shared/ui/AccessDenied.test.tsx` (moved), `LanguagesRegistryPage.patterns.test.tsx`, `app/routes.test.tsx` | Skeleton under real headers; error with retry; empty for administrator and reader; empty after a filter with a single «Сбросить фильтр»; access denied, read- and write-gated, with no request; not found with the address kept and «На главную»; not found by level (outline); one frame component (the same `StateFrame` root renders each variant) |
| US-8 | `shared/ui/FilterBar.test.tsx`, `SortableHeaderCell.test.tsx`, `LanguagesRegistryPage.patterns.test.tsx`, `LanguagesRegistryPage.test.tsx` (unchanged) | Filter bar between header and table with five fields; «Очистить фильтры» only while set; two ways out with distinct names; header row and hover through theme (row height measured by QA); `aria-sort` ascending then descending; row actions reached by Tab; «1–10 из 25» → «11–20 из 25»; the unchanged file passes |
| US-9 | `shared/ui/ConfirmDialog.test.tsx`, `FormDialog.test.tsx`, `LanguageFormPage.patterns.test.tsx`, `DefaultLanguageDialog.patterns.test.tsx`, `app/routes.test.tsx` | Dialog anatomy; backing out by Escape, «Закрыть» or «Отмена» with focus returned; busy dialog not dismissible; destructive colour; form page layout (no «К списку», «Отмена» back, «Код» focused); edit layout with the conflict panel replacing the footer; field error `aria-invalid` and the banner above the first field; busy form; no leave guard. The patterns beyond this task are design-only. |
| US-11 | the unchanged files, `AnonymousScreen.theme.test.tsx`, `ErrorBoundary.screen.test.tsx`, `HomePage` covered in `routes.test.tsx`, `visibility.test.ts` | Home in the shell; dictionaries on the patterns (the unchanged page tests); auth states outside the shell on the card with «Mustask» `h1` and the tab «Mustask»; crash screen with `h1` and the tab; «Процесс» hidden on placeholder data, if registered. Browser checks, naming and Keycloak are QA's. |
| US-12 | `app/AppLayout.test.tsx`, `app/shell/ContextSwitcher.test.tsx`, `app/shell/UserMenu.test.tsx` | Skip link; landmarks outside and on settings; tab order on «Главная» and «Языки» at 1440 (`user.tab()` sequence); menus by keyboard; collapsed rail named and tooltipped on focus, Enter opens. Visible focus indicator and 200 % reflow: QA. |
| US-10 | none | QA checks `design.md` § 10 and the prototype against the eleven scenarios. |

### 11.2 Left to QA in a browser (stage 7)

- The computed sizes and colours of US-11's "In a browser" scenario: 14 px
  body, a 48 px bar, rows at most 40 px, the role colours.
- The network origins.
- The top bar and navigation pinned while scrolling (US-2, US-6).
- No horizontal page scroll at 1024 and 640 px (US-4).
- A truncated switcher name at 1024 px (US-5).
- Reduced-motion width changes (US-4).
- The visible focus indicator and reflow at 200 % (US-12).
- E2E (§ 12).

### 11.3 Existing tests that change, and only these

| File | Tests | Change |
|---|---|---|
| `app/AppLayout.test.tsx` | «показывает имя из токена в шапке», «"Выйти" вызывает signOut», «показывает название приложения в шапке», «с languagesRead — пункт "Языки" показан», «без languagesRead — пункт "Языки" скрыт…» | Rewritten (C-1..C-4): the name is in the menu, not the bar; «Выйти» is a menu item; «Mustask» is a link, not a heading; «Настройки» appears for a reader and is absent otherwise. |
| `features/dictionaries/components/AccessDenied.test.tsx` | the one test | Moves to `shared/ui/AccessDenied.test.tsx`. Only the import path changes (C-14). |
| `features/dictionaries/components/RegistrySkeleton.test.tsx`, `FormSkeleton.test.tsx` | all | Move to `shared/ui/`. Only the import path changes (US-11's move rule). |

Every other existing test, including `LanguagesRegistryPage.test.tsx`,
`LanguageFormPage.test.tsx`, `DefaultLanguageDialog.test.tsx`,
`AnonymousScreen.test.tsx`, `ErrorBoundary.test.tsx`, `HomePage.test.tsx` and
`App.test.tsx`, passes byte-identical. The constraints that keeps on the code
are D-5, D-8, D-13, D-14 and `LoadingBox`'s `aria-label`.

## 12. E2E

**Rewritten in pass 3** (they assert the old top bar, C-1 and C-2):

- `14-header-name.spec.ts`:
  - find the banner's `button` whose name starts with «Профиль: »;
  - assert the name after the prefix is non-empty. The panel's fallback is
    the empty string, which names the button «Профиль» with no colon, so the
    «Профиль: » locator itself fails on a missing name (code review F-5);
  - assert the bar shows no display name as text;
  - open the menu and assert it shows the same name.
- `04-sign-out.spec.ts`, `05-logout-keycloak-down.spec.ts`: open the user
  menu (`button` named `/^Профиль/`), then click the `menuitem` «Выйти». The
  rest is unchanged.

**Checked and holding unchanged** (their selectors against the new markup):

| Spec | Selector | Why it holds |
|---|---|---|
| 01 | URL | — |
| 06, 15, 17 | `getByText('Язык: kk' / 'Язык: ru')`, `getByLabel('Эндоним' / 'Порядок' / 'Код', exact)` | The `h1` is the only match. The trail holds «Языки», not «Язык: …». |
| 09, 10 | `getByRole('heading', { name: 'Главная' })` | The `h1`. «Добро пожаловать в Mustask» is `component="p"`. |
| 15 | `getByText('Ничего не найдено')`, `getByRole('alert')` count 0 | The frame is not an alert, and the shell renders no alert there. |
| 16 | `getByRole('heading', { name: 'Языки' })` (Playwright matches a substring) | The settings title is `component="p"` and «Справочники» is a subheader, so the `h1` is the only heading containing «Языки». `getByText('Доступ ограничен')` matches the frame `h1` only. |
| 17, 18 | `tbody tr td` by index; `getByRole('dialog')`; «Назначить» | Column order kept; one primary button. |

Playwright's Desktop Chrome viewport is exactly 1280 px wide, so the suite
runs in the expanded layout (`lg` is inclusive). The suite runs at stage 7
against a cold stand, consent given 2026-10-01 (D-22 R-28). It is QA's run,
per `05-frontend.md` § 7.

## 13. Hand-over seams

- **mod-002:**
  - replaces `usePlaceholderContextSource()` in `AppLayout` with its source
    behind § 4.3's interface, setting `isReal`, `organizationListPath`,
    `status` and `retry`;
  - adds the fallback notice;
  - registers its entries in `model.ts` with `scope` and `landing(context)`;
  - builds the context-in-address routes and the tree registry (D-19).
- **mod-016:** replaces the non-instance branch of the access check (§ 4.2)
  with the served codes. The model does not change (BR-8).
- **mod-001:** reads `theme.workTracking` and uses `StateFrame` /
  `ErrorBanner` with `title`. Its «Процесс» row is registered per D-12 and
  pass 5.

## 14. Open points for the orchestrator

- **ADR-0024 status.** It still reads `proposed`, though gate-2 is approved
  under delegation and the design check says the human accepts it with
  gate-2. The frontend.md § MUI rewrite waits for it (D-17). If the
  orchestrator records the acceptance, pass 5 does the rewrite. Otherwise it
  stays a remaining item at gate-3. Closed: the orchestrator recorded the
  acceptance as D-24 on 2026-10-02, and pass 5 did the rewrite (83fd809).
- **D-8.** The test-environment alias is the one structural change to how
  every component test renders. It is called out here so code review weighs
  it on purpose. The fallback is ready if review prefers it.
- **Firefox is not verified (code review F-8).** spec.md's NFR "Browsers and
  widths" names current desktop Chrome, Edge and Firefox. The only Playwright
  project is `chromium`, and every browser check of this task ran there.
  Edge shares Chromium's engine; Firefox has not been run. This task leaves
  the Firefox clause unverified: a line for the gate-3 summary and the MR's
  «Deliberately left open». Running spec 19 once under Playwright's
  `firefox` device needs that browser installed and a stand, which the
  frontend stage did not take.
