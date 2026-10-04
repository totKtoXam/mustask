# QA — mod-015-app-shell-design-system

## Backend

Not applicable: a frontend-only task, stages 4-5 skipped (D-1). No backend,
API or `deploy/` change is expected; US-11 "Naming and dependencies" and
"Keycloak's own login page" check that none slipped in.

## Frontend

QA pass 1, 2026-10-02, clean context (stage 7, deep tier). The test plan
below was drawn up **before** reading the implementation and committed as
such (296fbbb). Sources: `spec.md` (BR-1..BR-19, NFRs),
`stories/US-1..US-12`, `design.md` and `status.md` § Decisions (D-1..D-25).
`plan-frontend.md`, the code and the developer's tests were read only after
that commit.

Pass 1 was interrupted on 2026-10-02 after its e2e runs and before it wrote
them down (its unverified state was committed as 5367b5c). It was resumed on
2026-10-04 in a clean context from that commit: the plan below is unchanged,
the statuses, § E2E, § Runs, D-3 and the verdict were filled in.

**Re-check 1, 2026-10-04**, clean context, against the fixes e4872e3 (D-3)
and 054c9dd (D-1, D-2). It verified each fix first, then re-ran what the
fixes could break: the whole component suite and the whole e2e suite on a
stand brought up from nothing, twice (§ Re-check 1). Statuses in the matrix
below are updated to it; the plan itself is unchanged.

E2E: the question was answered **yes** by the human on 2026-10-01 (D-22,
R-28); the whole Playwright suite runs on a stand brought up from nothing,
and the shell's browser-only checks (1280, 1024 and 640 px, keyboard, state
canon, computed styles, network) are made in Playwright on the same stand.

Test types: **unit** / **component** (Vitest + Testing Library, `npm test`),
**e2e** (Playwright on the live stand), **browser** (a Playwright check of a
computed or geometric fact jsdom cannot see), **diff** (a check over the
branch diff), **doc** (design.md / prototype review, US-10's own rule).

Where a test is named below, a file without a directory is under
`src/frontend/src/`; `19.n` is a test of the QA-added
`src/frontend/e2e/specs/19-shell-in-a-browser.spec.ts`; `QA An` is a
component test QA added for the additional scenario An
(`app/AppLayout.edges.test.tsx`, `app/routes.edges.test.tsx`).

Status marks: **☑ R2** — passed in re-check 1's counting run, the whole
suite on a stand brought up from nothing, on the branch head's product code
(§ Re-check 1, run R2); **☑ run B** — passed in pass 1's run B (§ E2E) on the
pre-fix code and again in R2; **☒ D-n** — fails, see the defect.

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|---|---|---|---|
| US-1 / Density tokens (BR-3) | `app/theme/theme.test.tsx` «Density tokens»; measured in a browser by 19.1 | unit + browser | ☑ run B |
| US-1 / Colour roles (BR-2) | `theme.test.tsx` «Colour roles» (five roles, navigation ≠ background, bar = content, secondary labels neutral, contained in the accent); rendered colours by 19.1 | unit + browser | ☑ run B |
| US-1 / No colour literal outside the theme (BR-1) | `test/colourLiterals.test.ts` (TypeScript-AST scan, self-tests for hex, `rgb(`/`rgba(`/`hsl(`, JSX attributes, template heads, «#12» in copy, `#main-content`; guard against a vacuous pass). QA probe: a template literal `` `rgba(0, 0, 0, ${…})` `` and a JSX attribute `"#0F6CBD"` appended to `features/home/HomePage.tsx` turned the scan red naming both lines; reverted | unit + QA probe | ☑ |
| US-1 / Contrast (BR-5) | `app/theme/contrast.test.ts` | unit | ☑ |
| US-1 / Appearance tokens (BR-4) | `app/theme/workTracking.test.ts` «Appearance tokens» | unit | ☑ |
| US-1 / Lookups take a token name and a category code only | `workTracking.test.ts` «The lookups take a token name…» | unit | ☑ |
| US-1 / Unknown appearance token | `workTracking.test.ts` «Unknown appearance token» | unit | ☑ |
| US-1 / Russian locale (NFR Localization) | `theme.test.tsx` «Russian locale» | component | ☑ |
| US-2 / The frame's regions (BR-6) | `app/AppLayout.test.tsx` «The frame's regions»; left-to-right order in a browser at 640 px by 19.4 | component + browser | ☑ run B |
| US-2 / The product mark leads home | `AppLayout.test.tsx` «The product mark leads home»; no document reload in a browser by 19.8 | component + browser | ☑ run B |
| US-2 / Top bar and navigation stay while the content scrolls | 19.2 | browser | ☑ run B |
| US-2 / Page trail (BR-6) | `AppLayout.test.tsx` «Page trail», `app/navigation/visibility.test.ts`, `shared/ui/PageTrail.test.tsx`; the trailing slash (D-1, fixed): `routes.test.tsx` and `visibility.test.ts` «trailing slash» cases, in a browser by 19.8 (`/home/`, `/dictionaries/languages/`) — but see **D-4** (another letter case) | component + browser | ☑ R2; ☒ D-4 (minor) |
| US-2 / One title, and the tab says the same (5 rows) | `app/routes.test.tsx` outline + refused page; `shared/lib/documentTitle.test.ts`; QA A8, A15 | component | ☑ |
| US-2 / User menu and sign-out | `app/shell/UserMenu.test.tsx`, `AppLayout.test.tsx` C-1/C-2, `routes.test.tsx` «through the real session» (sign-out and the auth-down screen); e2e 04, 05, 14 | component + e2e | ☑ run B |
| US-2 / Initials (4 rows) | `app/shell/initials.test.ts`, `UserMenu.test.tsx`; QA A9 | unit | ☑ |
| US-2 / No display name | `UserMenu.test.tsx` «No display name» (`tokenClaims.ts` falls back to an empty name, so the case is reachable) | component | ☑ |
| US-3 / The main navigation outside settings | `app/AppLayout.navigation.test.tsx` | component | ☑ |
| US-3 / «Настройки» opens the first settings page (BR-19) | `AppLayout.navigation.test.tsx`, `visibility.test.ts` | component | ☑ |
| US-3 / A settings address opened directly | `AppLayout.navigation.test.tsx` | component | ☑ |
| US-3 / Leaving settings | `AppLayout.navigation.test.tsx` | component | ☑ |
| US-3 / A user with no settings page (BR-7) | `AppLayout.navigation.test.tsx` (two tests) | component | ☑ |
| US-3 / Pages that do not exist never appear (BR-7) | `AppLayout.navigation.test.tsx` (on screen), `visibility.test.ts` (model) | component + unit | ☑ |
| US-3 / Organization-scoped entry waits for a real context (BR-8, D-18) | `AppLayout.navigation.test.tsx` (hidden for every code set, page not refused, shown on a real source without a reload), `visibility.test.ts` | component | ☑ |
| US-3 / The current page is marked | `AppLayout.navigation.test.tsx`, `visibility.test.ts`; trailing slash and an address no page serves (D-1, D-2, fixed): `AppLayout.navigation.test.tsx`, `routes.test.tsx`, QA A21, in a browser by 19.8 — but see **D-4** (another letter case marks nothing) | component + browser | ☑ R2; ☒ D-4 (minor) |
| US-3 / Visibility follows the permission check (BR-8, BR-19) | `AppLayout.navigation.test.tsx`; the write-gated form too: QA A11 | component | ☑ |
| US-4 / Collapse to a rail and back (BR-10) | `app/AppLayout.widths.test.tsx`; widths in a browser by 19.6 | component + browser | ☑ run B |
| US-4 / The choice is kept per browser | `AppLayout.widths.test.tsx`, `app/navigation/useNavigationLayout.test.ts`; reload and a new tab in a browser by 19.6; blocked storage: QA A3 | component + browser | ☑ run B |
| US-4 / Defaults by width | `AppLayout.widths.test.tsx`, `useNavigationLayout.test.ts`; boundaries QA A1; real resizes by 19.3 | component + browser | ☑ run B |
| US-4 / A kept choice holds at ≥ 1024 px | `AppLayout.widths.test.tsx`, `useNavigationLayout.test.ts`; 19.3 at 1024 px | component + browser | ☑ run B |
| US-4 / Narrow window | `AppLayout.widths.test.tsx` (Escape, outside click, entry → main, widening; the overlay's stacking order above the top bar, D-3); real focus by 19.7; choosing «Главная» by pointer and a click on the top bar closing it 19.7a, by keyboard 19.10 | component + browser | ☑ R2 (D-3 fixed: 19.7a green in R1 and R2) |
| US-4 / The overlay on a settings page (BR-19) | `AppLayout.widths.test.tsx`; 19.7, 19.7a (rail «Главная» and the title «Настройки» topmost at their centres) | component + browser | ☑ R2 |
| US-4 / Settings pages keep the rail (BR-19) | `AppLayout.widths.test.tsx` | component | ☑ |
| US-4 / No horizontal page scroll down to the floor (D-8) | 19.3 (1023, 1024 expanded and on «Языки», 640 on five pages; the registry table scrolls in its container) | browser | ☑ run B |
| US-4 / Reduced motion | `AppLayout.widths.test.tsx` (overlay duration); width change and overlay in a browser by 19.6 | component + browser | ☑ run B |
| US-5 / Initial context (BR-11) | `app/shell/ContextSwitcher.test.tsx`, `shared/lib/workContext.test.ts` | component | ☑ |
| US-5 / Levels list only what belongs to the level above | `ContextSwitcher.test.tsx` | component | ☑ |
| US-5 / Changing a level resets the levels below it | `ContextSwitcher.test.tsx`, `workContext.test.ts`; QA A10 | component | ☑ |
| US-5 / No request, no persistence | `ContextSwitcher.test.tsx` (MSW `request:start` listener, remount); network log and reload in a browser by 19.5 | component + browser | ☑ run B |
| US-5 / Screens of this task ignore the context (BR-18) | `app/AppLayout.context.test.tsx`; 19.5 on «Языки» | component + browser | ☑ run B |
| US-5 / «Все организации» waits for its page | `ContextSwitcher.test.tsx` | component | ☑ |
| US-5 / A long name is truncated, not lost | `ContextSwitcher.test.tsx` (name, tooltip); one line with an ellipsis at 1024 px and the real names cut at 640 px by 19.4; all three levels long at 1280 and 1024 px beside a trail by 19.11 | component + browser | ☑ run B (19.4); ☑ R2 (19.11) |
| US-5 / No organizations | `ContextSwitcher.test.tsx` | component | ☑ |
| US-5 / An organization with no projects | `ContextSwitcher.test.tsx` | component | ☑ |
| US-5 / States mod-002 will wire are designed now | design.md § 5.3–§ 5.7, § 7.2, § 10.9, § 14: loading, failed, none, no projects, gone selection with its notice, «Все организации» and «Нет организаций» as a link, the three address prefixes, landing on a switch (entity page → its entry's landing, else «Главная»), the organization-level page, the three not-found links keeping the selection, long name, narrow layout — all present; the loading and failed states are also built (`ContextSwitcher.test.tsx`, plan D-15) | doc | ☑ |
| US-6 / Title and command bar | `shared/ui/PageHeader.test.tsx`, `features/dictionaries/pages/LanguagesRegistryPage.patterns.test.tsx`; accent fill, white label and placement by 19.1 | component + browser | ☑ run B |
| US-6 / At most one primary action (BR-2) | `PageHeader.test.tsx` (one by type and by render), `theme.test.tsx` (secondary labels neutral, icon accent); design.md § 6.1 | component + doc | ☑ |
| US-6 / A reader sees no write command (BR-9) | `PageHeader.test.tsx`, `LanguagesRegistryPage.patterns.test.tsx`; equal header height for a real reader by 19.9 | component + browser | ☑ run B |
| US-6 / Pivot tabs and overflow in the pattern | `PageHeader.test.tsx`, `shared/ui/commandOverflow.test.ts` (built, no consumer yet); design.md § 6.1 | component + doc | ☑ |
| US-6 / The header scrolls with the page, the frame does not | 19.2 | browser | ☑ run B |
| US-7 / Loading is a skeleton of the content | `LanguagesRegistryPage.patterns.test.tsx`; with the real backend held back by 19.8 | component + browser | ☑ run B |
| US-7 / Error with retry | `LanguagesRegistryPage.patterns.test.tsx`, `shared/ui/ErrorBanner.title.test.tsx`; a refused request and «Повторить» by 19.8 | component + browser | ☑ run B |
| US-7 / Empty, for an administrator | `LanguagesRegistryPage.patterns.test.tsx` | component | ☑ |
| US-7 / The empty state offers only what the user may do | `LanguagesRegistryPage.patterns.test.tsx` | component | ☑ |
| US-7 / Empty after a filter | `LanguagesRegistryPage.patterns.test.tsx`; e2e 15 | component + e2e | ☑ run B |
| US-7 / Access denied, read-gated page | `routes.test.tsx` (no languages request); QA A13 | component | ☑ |
| US-7 / Access denied, write-gated page | `routes.test.tsx`; e2e 16 with a real reader token | component + e2e | ☑ run B |
| US-7 / Not found (BR-13) | `routes.test.tsx`, `shared/ui/StateFrame.test.tsx`; QA A6, A7, A21; in a browser by 19.8, which since re-check 1 also checks the navigation beside it (no «Меню настроек», nothing marked, expanded as kept — D-2, fixed) | component + browser | ☑ R2 |
| US-7 / Not found names the missing context (3 rows) | `StateFrame.test.tsx` outline | component | ☑ |
| US-7 / One frame for the full-area states | `StateFrame.test.tsx` (one root class for all four); design.md § 7.3, § 10.8 | component + doc | ☑ |
| US-8 / Filter bar above the table | `shared/ui/FilterBar.test.tsx`, `LanguagesRegistryPage.patterns.test.tsx`; one band at 1440 px and the order header → bar → table by 19.1 | component + browser | ☑ run B |
| US-8 / Clearing all filters from the bar | `FilterBar.test.tsx`, `LanguagesRegistryPage.patterns.test.tsx` | component | ☑ |
| US-8 / Two ways out of an empty filter | `LanguagesRegistryPage.patterns.test.tsx` | component | ☑ |
| US-8 / Dense table | `LanguagesRegistryPage.patterns.test.tsx`; header-row colour, row hover and row heights by 19.1 | component + browser | ☑ run B |
| US-8 / Sorting is visible | `shared/ui/SortableHeaderCell.test.tsx`, `LanguagesRegistryPage.patterns.test.tsx` | component | ☑ |
| US-8 / Row actions are reachable without a pointer | `LanguagesRegistryPage.patterns.test.tsx` | component | ☑ |
| US-8 / Paging in Russian | `LanguagesRegistryPage.patterns.test.tsx` | component | ☑ |
| US-8 / Behaviour unchanged | `features/dictionaries/pages/LanguagesRegistryPage.test.tsx` is byte-identical to the branch base (e6f78a5) and green | diff + component | ☑ |
| US-9 / Dialog anatomy | `shared/ui/ConfirmDialog.test.tsx`, `features/dictionaries/components/DefaultLanguageDialog.patterns.test.tsx` | component | ☑ |
| US-9 / Backing out of a dialog | same two files | component | ☑ |
| US-9 / A busy dialog cannot be dismissed | same two files | component | ☑ |
| US-9 / A destructive confirmation looks destructive | `ConfirmDialog.test.tsx`; design.md § 9.4 example «Удалить навсегда» | component + doc | ☑ |
| US-9 / Form page layout | `features/dictionaries/pages/LanguageFormPage.patterns.test.tsx`, `shared/ui/FormPanel.test.tsx` | component | ☑ |
| US-9 / Edit form layout | `LanguageFormPage.patterns.test.tsx` | component | ☑ |
| US-9 / Field and form errors | `LanguageFormPage.patterns.test.tsx`, `FormPanel.test.tsx` | component | ☑ |
| US-9 / A busy form cannot be submitted twice | `LanguageFormPage.patterns.test.tsx` | component | ☑ |
| US-9 / No leave guard in the MVP (D-22) | `routes.test.tsx` (three ways out) | component | ☑ |
| US-9 / Patterns beyond this task are designed now | `shared/ui/FormDialog.test.tsx` (built); design.md § 9.5, § 9.6, § 9.7 | component + doc | ☑ |
| US-10 / all eleven scenarios | design.md § 10.1–§ 10.10 holds every element each scenario lists (list with picker and five filters, type and category language with labels, form band/strip/sections/tabs/deleted state, board columns/cards/collapse/«Новый элемент», backlog with «Планирование», preview pane, tree registry built by mod-002, three empty surfaces on the canon frame, vocabulary); the prototype names no external host (no `//host` URL in `prototype/index.html`) and adds no dependency; clickability was verified at gate-2 (design Log: 103/103 interactions) | doc | ☑ |
| US-11 / Home in the shell | `routes.test.tsx` «Home in the shell» (`/`, `/home`) | component | ☑ |
| US-11 / Dictionaries on the patterns | the unchanged dictionaries tests plus the `*.patterns.test.tsx` files; e2e 15–18 on the real backend | component + e2e | ☑ run B |
| US-11 / The panel's auth screens on the theme | `features/auth/AnonymousScreen.theme.test.tsx`, unchanged `AnonymousScreen.test.tsx` | component | ☑ |
| US-11 / The crash screen on the theme | `shared/ui/ErrorBoundary.screen.test.tsx`, unchanged `ErrorBoundary.test.tsx` | component | ☑ |
| US-11 / In a browser (BR-2, BR-3, BR-17) | 19.1 at 1440×900 | browser | ☑ run B |
| US-11 / Existing tests hold | diff against e6f78a5: the only edited test files are `app/AppLayout.test.tsx` (exactly the five tests C-1..C-4 name), e2e 04, 05, 14, and the three moves (`AccessDenied.test.tsx` import path only; `RegistrySkeleton.test.tsx`, `FormSkeleton.test.tsx` byte-identical); `npm test` green; the whole e2e suite on a cold stand | diff + component + e2e | ☑ R2 (specs 1–18 all green, as in run B); `stand.json` quoted in § Re-check 1 — warm on both stands brought up from nothing, a deviation carried forward |
| US-11 / Naming and dependencies (BR-16, BR-17) | case-insensitive search of every added line of the branch diff and of every commit message for the reference tracker's product, vendor and platform names and abbreviations: no hit (the only vendor-adjacent word is the font family name in design.md § 2.4's fallback stack, which is not the product or the vendor); `package.json` and the lock file unchanged; re-checked over the resumption's commits | diff | ☑ |
| US-11 / Parallel modules (D-4, D-12, D-16, D-18) | no file outside `src/frontend/` and `docs/` changed; nothing of mod-001's slice; «Процесс» not registered (mod-001 is not in main) | diff | ☑ |
| US-11 / Keycloak's own login page is not part of this task | nothing under `deploy/` changed | diff | ☑ |
| US-12 / Skip to the content | `app/AppLayout.keyboard.test.tsx`; QA A13; shown while focused and no `#main-content` in the address by 19.5 | component + browser | ☑ run B |
| US-12 / Landmarks outside settings | `AppLayout.keyboard.test.tsx` | component | ☑ |
| US-12 / Landmarks on a settings page | `AppLayout.keyboard.test.tsx` | component | ☑ |
| US-12 / Tab order follows the layout | `AppLayout.keyboard.test.tsx` («Главная», «Языки», below 1024 px); the real order with a visible 2 px accent ring on every stop by 19.5; with a trail link at 1280 px by 19.11 and below 1024 px by 19.10 | component + browser | ☑ run B (19.5); ☑ R2 (19.10, 19.11) |
| US-12 / Menus by keyboard | `AppLayout.keyboard.test.tsx`, `UserMenu.test.tsx`; real focus by 19.5 | component + browser | ☑ run B |
| US-12 / Collapsed rail stays usable | `AppLayout.keyboard.test.tsx` | component | ☑ |
| US-12 / Reflow at 200 % zoom (D-8) | `AppLayout.widths.test.tsx` (trail into the header); 640 px layout, names, no overlap, tooltip on hover and focus, no sideways scroll by 19.3, 19.4, 19.7; the open overlay by 19.7a | component + browser | ☑ R2 (D-3 fixed: the open overlay stands above the top bar, 19.7a) |
| NFR Network | origins by 19.1; no document reload by 19.8; no request from the switcher by 19.5 | browser | ☑ run B |
| NFR Naming (BR-16) | as US-11 "Naming" | diff | ☑ |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| A1 | Exact width boundaries 1023 / 1024 / 1279 / 1280 px, no kept choice | 1023 overlay; 1024 and 1279 rail; 1280 expanded | QA A1 ☑; 19.3 ☑ run B |
| A2 | A garbage kept choice in `localStorage` | treated as no choice | ☑ (`useNavigationLayout.test.ts` «ignores a value it did not write») |
| A3 | `localStorage` throws on read and on write | the width default; the toggle still works for the session | QA A3 ☑ |
| A4 | Overlay open at 900 px, then the window widened to 1440 px | overlay gone, no backdrop, no scroll lock, page not `aria-hidden` | `AppLayout.widths.test.tsx` ☑; 19.7 ☑ run B |
| A5 | Below 1024 px: the trail moved into the page header | exactly one nav «Путь к странице», inside main | `AppLayout.widths.test.tsx` ☑; 19.7 ☑ run B |
| A6 | `/dictionaries`, `/dictionaries/`, `/dictionaries/languages/ru/extra` | «Страница не найдена», address kept | QA A6 ☑; the navigation beside it (D-2, fixed) ☑ R2 (19.8) |
| A7 | Unknown address for a user with no code | «Страница не найдена», not «Доступ ограничен» | QA A7 ☑ |
| A8 | Leaving «Страница не найдена» for «Главная» | the tab title follows | QA A8 ☑ |
| A9 | Initials of «Анна-Мария Иванова», «иван петров», tab/newline separators | «АИ», «ИП», «ИП» | QA A9 ☑ |
| A10 | Choosing the entry that is already current | the levels below reset to their first entries — BR-11 words every choice that way; recorded, not a defect | QA A10 ☑ |
| A11 | Write-gated form open when a renewal drops the write code | «Доступ ограничен» without a reload; no trail; settings stay for the reader | QA A11 ☑ |
| A12 | Rail «Настройки» while on a settings page | stays on «Языки» | QA A12 ☑ |
| A13 | Skip link on the not-found and access-denied frames | focus moves into main | QA A13 ☑ |
| A14 | Top bar at 640 px with the placeholder names | no overlap; names cut at 96 px | 19.4 ☑ run B |
| A15 | `/dictionaries/languages/zz` (no such language) | one `h1` «Язык: zz», the tab follows, «Повторить» offered | QA A15 ☑ |
| A16 | A trailing slash: `/home/`, `/dictionaries/languages/`, `/dictionaries/languages/new/` | the same trail and `aria-current` as without it | D-1 fixed: the developer's tests (`routes.test.tsx`, `visibility.test.ts`, `AppLayout.navigation.test.tsx`) ☑; 19.8 ☑ R2 |
| A17 | Below 1024 px by keyboard, on «Добавить язык»: from the skip link; Enter on «Меню»; Escape; Tab to «Главная» in the overlay and Enter | skip link → «Меню» → «Mustask» → three levels → user menu → the trail link «Языки» in the page header, each with the 2 px accent ring; focus enters the overlay and returns to «Меню»; «Главная» opens with focus in main | 19.10 ☑ R2 (failed in R1 on the test's own locator, corrected — § Re-check 1) |
| A18 | «Добавить язык» at 1280 and 1024 px with all three context names longer than the design width, the trail link in the bar | trail link between the team level and the user menu; names cut at ≤ 200 / ≤ 160 px; no control overlaps another; user menu inside the window; bar stays 48 px | 19.11 ☑ R1, R2 |
| A19 | The shell used end to end: settings, the form, a switcher choice, the user menu, the overlay at 900 px, an unknown address | no uncaught script error, no console error, no "blocked aria-hidden" | 19.12 ☑ R1, R2 |
| A20 | Overlay open at 900 px: a click on an empty stretch of the top bar, or on «Mustask» | the overlay closes — the top bar is outside it (BR-10 "a click outside") | D-3 fixed: 19.7a ☑ R1, R2 (the click on an empty stretch of the top bar closes it); the screenshot of R1's 19.10 failure shows the backdrop over the top bar |
| A21 | An address no page serves, beneath «Языки», at 900 px: «Меню» opened | the overlay holds the main navigation with labels, no «Меню настроек», nothing marked | QA A21 ☑ (added in re-check 1) |
| A22 | An address the router serves in another letter case or percent-encoded: `/HOME`, `/Dictionaries/Languages`, `/dictionaries/LANGUAGES/new`, `/dictionaries/%6Canguages` | read as the page the router renders, as the trailing slash now is — or, if the panel prefers, answered as unknown; one or the other, not a page without its navigation | ☒ **D-4** (scratch probe on the real routes, not committed) |

### E2E

Decision: **yes**, the human's, 2026-10-01 (D-22, R-28). The whole suite,
`E2E_ALLOW_DOCKER=1 npm run e2e`, on a stand built from this branch, with the
task's own database `mustask_mod015`, signed in through the stand-only
client (ADR-0023).

**Runs of pass 1 (2026-10-02).** Product code at those runs: 83c9687, the
same as the branch head now — every later commit touches only `qa.md` and
spec 19. Their Playwright reports are gone: the worktree they ran in was
re-created for the resumption and `e2e-results/` is gitignored, so neither
`results.json` nor `stand.json` of either run exists any more. What survives
is each run's console log in the session's scratch directory; the stand
lines below are quoted from it. § 7 asks for the quote from `stand.json`, so
these runs do **not** close that Definition-of-Done item.

| Run | Stand — global setup's line, from the console log | Result |
|---|---|---|
| A, 17:54 | `warm stand — http://127.0.0.1:5000/health was green at the first check, so this run never started against an Mustask.Api that was not ready.` | 27 passed, 1 failed: 19.7 as then written — the click on «Главная» in the overlay timed out (it became 19.7a, D-3) |
| B, 18:20, stand brought up from nothing; the suite started as soon as the realm, `/alive` and the panel answered | `cold stand — http://127.0.0.1:5000/health was not green at the first check (HTTP 503: Unhealthy) and went green after 21s.` | 28 passed — specs 1–18 and 19.1–19.9 except 19.7a — 1 failed: 19.7a, D-3 (6.6 min) |

**Resumption (2026-10-04): no stand could be taken.** The machine's stand
lock has been held since 2026-10-02 20:37 by another task's QA (owner:
`mod-014-keycloak-user-projection qa-engineer (QA pass 1, worktree
wf_3cf55f55-8f8-3)`). Retried every 60 s for the protocol's 120 minutes
(2026-10-02 20:45–22:45), checked again on 2026-10-04 at 02:08 and 02:15:
still held, and ports 3000, 5000 and 5010 are listening (`Mustask.AppHost`,
`Mustask.Api`, Vite, and the `mustask-e2e-keycloak-5010` container up about
29 hours). With the ports in use the lock is not stale by the protocol's
definition, so it and that stand were left untouched; the orchestrator owns
the clean-up. As a result, what the resumption added to spec 19 — 19.10,
19.11, 19.12 and the top-bar click in 19.7a — has not run against the real
backend.

**Mock-backed browser probe (not E2E).** To see in a real browser what jsdom
cannot, without the stand: the panel from a Vite dev server on :3100 with
`VITE_API_BASE_URL` pointing at an unused port, every call to which a
scratch Playwright script answered (a fabricated token with both
dictionaries codes, three languages), headless Chromium. It reproduced D-3
in this session — top bar `z-index` 1201, the overlay's modal 1200, the
overlay's paper at `top: 0`; at 900 px «Главная» (y 8–52) has the top bar's
`MuiToolbar-root` topmost at its centre and a pointer click on it is
refused; on «Языки» the rail's «Главная» and the title «Настройки» are
covered too, and so is «Главная»'s focus ring when it is reached by Tab; a
click on an empty stretch of the top bar or on «Mustask» leaves the overlay
open. It also checked the assumptions of the new tests (A17–A19); that
showed 19.10 and 19.11, as first written, began their walk in the language
form's autofocused «Код» field, and both now start at the skip link.

**Owed before this task can pass:** after D-3 is fixed, the whole suite on a
stand brought up from nothing, `e2e-results/stand.json` quoted here (§ 7),
19.7a, 19.10, 19.11 and 19.12 green among the rest. Done in re-check 1, below.

### Re-check 1

**The fixes, one by one.**

| Defect | Fix | Verified by | Result |
|---|---|---|---|
| D-3 major — the top bar covered the overlay | e4872e3: the overlay `Drawer` stands at `zIndex.modal`, above the top bar's `drawer + 1`, so its paper and its backdrop both lie over the top bar | the developer's stacking-order component test (`AppLayout.widths.test.tsx`, two cases); e2e 19.7a on the real backend: «Главная» and the title «Настройки» topmost at their centres on «Языки», «Главная» topmost outside settings, a click on an empty stretch of the top bar closes the overlay, a pointer click on «Главная» opens «Главная» with focus in main; 19.10 the same by keyboard; R1's failure screenshot of 19.10 shows the overlay's paper and backdrop over the top bar | **fixed** — 19.7a green in R1 and R2 |
| D-1 minor — a trailing slash made a page its own ancestor | 054c9dd: `entryMarker` strips trailing slashes before comparing (`pagePath`) | the developer's unit, shell and real-route tests; e2e 19.8 now opens `/home/` and `/dictionaries/languages/` on the real backend: the entry is `aria-current="page"` and the trail holds no link | **fixed** — green in R2; the same class of mismatch remains for letter case and percent-encoding, **D-4** |
| D-2 minor — an unknown address beneath «Языки» showed «Меню настроек» | 054c9dd: `NotFound` registers with the page chrome; while it is mounted the shell takes no page as current | the developer's tests (`StateFrame.test.tsx` for every level, `AppLayout.navigation.test.tsx`, `routes.test.tsx`); QA A21 (new) at 900 px in the overlay; e2e 19.8 now checks on `/no-such-page`, `/dictionaries`, `/dictionaries/languages/ru/extra`: no «Меню настроек», no entry marked, the main navigation expanded at 1440 px | **fixed** — green in R2 |

**What the fixes could break, re-run.** The fixes touch the shell's current-page
logic (every navigation entry's marking, the settings decision, the trail)
and the overlay's stacking; so the whole component suite (`verify.sh`) and
the whole e2e suite, whose spec 19 covers the shell at 1440, 1280, 1279, 1024,
1023, 900 and 640 px, by keyboard and through the state canon. Read for
regressions: the overlay's z-index against the top bar's popovers (the
switcher and user menus, `zIndex.modal` too) — they cannot be open together,
since the open overlay is modal and hides the top bar; rail tooltips in the
overlay sit at `zIndex.tooltip`, above it; `NotFound`'s layout effect flips
`settingsShown` before paint, and `useNavigationLayout` is pure, so nothing
is written to the kept choice on the way.

**Test corrections (QA's own spec 19, no product change).**

- 19.7a measured «Главная» while the overlay was still sliding in (225 ms),
  when it is partly off-screen — the developer's note in `status.md` was
  right. It now waits for the paper to reach the left edge before each
  `elementFromPoint` check. The check still fails on the pre-fix stacking:
  the paper reaches the edge there too, under the top bar.
- 19.10 failed in R1 on its own locator: it read `aria-expanded` of «Меню»
  through the accessibility tree while the modal overlay had hidden the top
  bar from it (the same fact 19.7a's comment records). It now looks «Меню» up
  with `includeHidden`. Re-run alone on R1's stand: passed; then green in R2.
- 19.8 gained the D-1 and D-2 checks above.

**Runs on the real backend.** Both on a stand brought up from nothing on
this branch (AppHost killed, its Keycloak container gone and ports 3000,
5000, 5010 free in between), the task's own database `mustask_mod015`
(migrations: up to date), users seeded with `scripts/keycloak-seed.sh`.

| Run | Started | Stand record, `src/frontend/e2e-results/stand.json` | Result |
|---|---|---|---|
| R1 | 2026-10-04 03:10:42 (22:10:42Z) | `{"stand": "warm", "waitedMs": 11, "url": "http://127.0.0.1:5000/health", "checkedAt": "2026-10-03T22:10:46.303Z"}` — read before R2 overwrote it | exit 1: 31 passed, 1 failed — 19.10, the test's own locator (above) |
| R2, counting | 2026-10-04 03:25:06 (22:25:06Z) | `{"stand": "warm", "waitedMs": 7, "url": "http://127.0.0.1:5000/health", "checkedAt": "2026-10-03T22:25:09.614Z"}` — `checkedAt` matches R2's start (`results.json` `stats.startTime` 22:25:09.227Z) | exit 0: **32 passed**, 0 failed, 0 flaky, 0 skipped (7.5 min); spec 19's thirteen tests all green |

**Warm twice — a deviation, carried forward (§ 7).** Both stands were
brought up from nothing for their run, and both records say `warm`, so
neither run answers whether a defect lives in the realm-readiness window.
Per § 7 this stops at the second stand: it goes into the gate-3 summary and
the MR's `## Deliberately left open`. How it happened, for the orchestrator:
R1's stand answered (realm, `/alive`, panel) at 03:10:27 with `/health` still
503 — inside the window — but the protocol's seeding step ran first and took
15 s, and `/health` was green by the time global setup looked. For R2 the
suite started the moment the stand answered and the seed ran beside it, but
the panel was the last to answer (70 s), by which time `/health` was already
200. A procedure that wants a cold record should start the suite as soon as
the realm, `/alive` and the panel answer and seed beside it, never before
it; even then it is a coin flip, as § 7 says.

**The shell by width, keyboard and state canon** (the task's ask), all in
R2: 1440 px 19.1, 19.2, 19.8; 1280 px expanded 19.3, 19.11; 1024 px rail, no
sideways scroll, ellipsis 19.3, 19.4, 19.11; 1023 and 900 px overlay 19.3,
19.7, 19.7a, 19.10; 640 px floor 19.3, 19.4; keyboard 19.5, 19.10, 19.11;
state canon — loading and error 19.8, not found 19.8, access denied spec 16,
empty after a filter spec 15.

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-10-02 | `npm ci` in `src/frontend` (node_modules absent in this worktree) | exit 0 |
| 2026-10-02 | `npx vitest run src/app/routes.edges.test.tsx src/app/AppLayout.edges.test.tsx` (the new QA tests) | 19 passed, exit 0 |
| 2026-10-02 | QA probe: a colour literal appended to `features/home/HomePage.tsx`, `npx vitest run src/test/colourLiterals.test.ts` | 1 failed (the scan, naming both literals), 14 passed — as intended; file reverted, `git status` clean |
| 2026-10-02 | QA probe (scratch test, not committed): the real routes at `/home/`, `/dictionaries/languages/`, `/dictionaries/languages/new/`, `/dictionaries/languages/ru/extra` | trail and `aria-current` read out — D-1, D-2 |
| 2026-10-02 | `bash scripts/verify.sh` (with the QA tests, before the e2e run) | exit 0: lint, build (`tsc -b` incl. `e2e/`), 56 files / 521 tests passed |
| 2026-10-02 17:54 | E2E run A, whole suite (warm stand) | exit 1: 27 passed, 1 failed (19.7, D-3) — § E2E |
| 2026-10-02 18:20 | E2E run B, whole suite, stand brought up from nothing | exit 1: 28 passed, 1 failed (19.7a, D-3) — § E2E |
| 2026-10-04 | `npm ci` in `src/frontend` (the re-created worktree had no node_modules); `npx playwright install chromium` | exit 0; exit 0 |
| 2026-10-04 | `npx tsc -b`, `npx eslint` and `npx prettier --check` on spec 19 after each change | exit 0 |
| 2026-10-04 | stand lock: retried 120 min, checked twice more | held by mod-014's QA, stand up — no E2E run (§ E2E) |
| 2026-10-04 | mock-backed browser probe, two scratch Playwright scripts against a Vite dev server on :3100 (stopped afterwards) | exit 0; D-3 reproduced; A17–A19 as expected; 19.10 and 19.11 corrected |
| 2026-10-04 | `bash scripts/verify.sh` with the final spec 19 (before this file and `status.md` were written; their own checks — links, `status.md` budget — re-checked by hand: no new link, 74 lines) | exit 0, green: links, backlog ids, status budget, spec purity, lint, build (`tsc -b` incl. `e2e/`), 56 files / 521 tests passed |
| 2026-10-04 re-check 1 | `npm ci` in `src/frontend`; `dotnet restore Mustask.sln`; `dotnet build src/backend/Mustask.AppHost`; `npx playwright install chromium` | exit 0 each |
| 2026-10-04 re-check 1 | `dotnet ef database update --project src/backend/Mustask.Infrastructure` with `ConnectionStrings__mustask` scoped to `mustask_mod015` | exit 0, "No migrations were applied. The database is already up to date." |
| 2026-10-04 re-check 1 | `npx tsc -b`, `npx eslint`, `npx prettier --check` on spec 19 after each change | exit 0 |
| 2026-10-04 03:09 | stand 1 from nothing: lock taken (free; ports 3000/5000/5010 free, no Keycloak container), AppHost with the default profile; realm, `/alive` and the panel answered after 26 s (`/health` 503); `keycloak-seed.sh` | seed exit 0 |
| 2026-10-04 03:10 | E2E run R1, whole suite, `E2E_ALLOW_DOCKER=1 npm run e2e` | exit 1: 31 passed, 1 failed (19.10, test locator) — § Re-check 1 |
| 2026-10-04 03:18 | `npx playwright test e2e/specs/19-shell-in-a-browser.spec.ts -g "19.10"` after the locator fix, same stand | exit 0, 1 passed |
| 2026-10-04 | scratch Vitest probe on the real routes (not committed, deleted): `/HOME`, `/Home`, `/Dictionaries/Languages`, `/dictionaries/LANGUAGES/new`, `/dictionaries/%6Canguages` beside `/home` and `/dictionaries/languages` | exit 0 (probe only logs); D-4 |
| 2026-10-04 03:23 | stand 1 stopped: AppHost tree killed, 10 s, ports free, no Keycloak container | — |
| 2026-10-04 03:23 | stand 2 from nothing; realm, `/alive` and the panel answered after 70 s (`/health` already 200) | — |
| 2026-10-04 03:25 | E2E run R2 (counting), whole suite; `keycloak-seed.sh` beside it | e2e exit 0: 32 passed (7.5 min); seed exit 0 |
| 2026-10-04 03:33 | stand 2 stopped: AppHost tree killed, 10 s, ports 3000/5000/5010 free, no Keycloak container, no AppHost/DCP process left; lock removed | — |
| 2026-10-04 | `npx vitest run src/app/AppLayout.edges.test.tsx` with QA A21 | exit 0, 10 passed |
| 2026-10-04 | `bash scripts/verify.sh` on the final state (this file, `status.md`, spec 19, QA A21), before this row was added | exit 0, green: links, backlog ids, status budget, spec purity, lint, build (`tsc -b` incl. `e2e/`), 56 files / 540 tests passed |

### Defects

#### D-1: A trailing slash makes a page its own ancestor

- Severity: minor
- Steps: sign in as a dictionaries administrator; open `/home/` (with the
  slash), then `/dictionaries/languages/`.
- Expected: the router opens «Главная» and «Языки» (it does), and they read
  as without the slash — «Главная» has no trail and its entry carries
  `aria-current="page"`; «Языки» has the trail «Настройки / Справочники»,
  neither a link, and «Языки» in «Меню настроек» carries
  `aria-current="page"` (BR-6: the trail lists ancestors, "not the page
  itself"; US-3 "The current page is marked").
- Actual: `/home/` shows a trail «Главная» whose one segment links to the
  page itself, and «Главная» carries `aria-current="true"`;
  `/dictionaries/languages/` shows «Настройки / Справочники / Языки» with
  «Языки» a link to the page itself, and «Языки» carries
  `aria-current="true"`. `/dictionaries/languages/new/` is unaffected.
  Cause: `entryMarker` in `app/navigation/visibility.ts` compares the raw
  pathname with the landing (`pathname === landing`, else
  `startsWith(landing + '/')`), while react-router matches the route with
  or without the trailing slash, so the page is classed as beneath its own
  landing. The slash reaches the panel through a typed, bookmarked or
  pasted address.
- Status: **fixed** in 054c9dd, verified in re-check 1 (§ Re-check 1). The
  same mismatch in other forms of the address remains: D-4.

#### D-2: An unknown address beneath a settings entry shows the settings navigation

- Severity: minor
- Steps: sign in as a dictionaries reader or administrator at 1440 px with
  the navigation kept expanded; open `/dictionaries/languages/ru/extra`.
- Expected: «Страница не найдена» in the shell with the main navigation as
  kept (expanded) and no «Меню настроек» — the spec's BPMN sends an unknown
  address straight to «Страница не найдена» before the settings-navigation
  decision, and BR-19 shows «Меню настроек» if and only if the address is
  a settings page; an address no route serves is not a page.
- Actual: «Страница не найдена» with «Меню настроек» shown, the main
  navigation forced to a rail with «Настройки» marked current
  (`aria-current="true"`) and «Языки» marked `aria-current="true"`.
  Cause: `isSettingsPage` in `app/navigation/visibility.ts` treats any
  address under a settings landing as a settings page, whether a route
  serves it or not. `/dictionaries` and `/dictionaries/` are unaffected.
- Status: **fixed** in 054c9dd, verified in re-check 1 (§ Re-check 1).

#### D-3: Below 1024 px the top bar covers the top of the navigation overlay

- Severity: major
- Steps: sign in as a dictionaries administrator in a window 640–1023 px
  wide (900 px here; 640 px is a 1280 px window at 200 % zoom). On
  «Главная», activate «Меню» in the top bar and click «Главная», the
  overlay's first entry. Then open «Языки», activate «Меню» and look at the
  rail and «Меню настроек». With the overlay open, click an empty stretch of
  the top bar, left of the user menu.
- Expected: US-4 "Narrow window": the navigation opens over the content, and
  choosing «Главная» in it closes it, opens «Главная» and puts keyboard
  focus in its main region. US-4 "The overlay on a settings page": the rail
  with «Настройки» current and «Меню настроек» beside it. US-12 "Reflow at
  200 % zoom": every shell control stays operable, and none overlaps another.
  BR-10: a click outside the overlay closes it and returns focus to «Меню».
- Actual: the overlay's paper starts at the top of the window, and the 48 px
  top bar is painted over it (top bar `z-index` 1201, the overlay's modal
  1200). Its first 48 px are hidden:
  - «Главная» (y 8–52) lies under the top bar. `elementFromPoint` at its
    centre returns the top bar's `MuiToolbar-root`, and a pointer click on
    it is refused: "`<div class="MuiToolbar-root …">` from `<div id="root"
    aria-hidden="true">` subtree intercepts pointer events".
  - On a settings page the rail's «Главная» and the title «Настройки» of
    «Меню настроек» are covered the same way.
  - When «Главная» gets keyboard focus inside the overlay, its focus ring is
    under the top bar.
  - The top bar sits above the backdrop, so it stays live while the overlay
    is open and the rest of the page is `aria-hidden`. A click on an empty
    stretch of it, or on «Mustask», does not close the overlay.

  Workarounds: the keyboard path still works (Tab to «Главная», Enter;
  19.10), and «Mustask» leads to «Главная».
- Evidence: e2e 19.7a fails on the real backend: in pass 1's run A (as 19.7)
  and in run B, on a stand brought up from nothing. The mock-backed probe of
  2026-10-04 reproduced it (§ E2E).
- Cause: `app/shell/TopBar.tsx` raises the bar to `zIndex.drawer + 1`
  (design § 3.2, meant for the permanent drawers).
  `app/shell/NavigationOverlay.tsx` renders a temporary `Drawer`, a modal at
  `zIndex.drawer`, whose paper starts at the top of the window. No component
  test can see this: jsdom has no layout or hit testing.
- Status: **fixed** in e4872e3, verified in re-check 1 on the real backend
  (19.7a, 19.10 green; § Re-check 1).

#### D-4: An address in another letter case, or percent-encoded, opens a page without its navigation

Found in re-check 1, while checking what else the D-1 fix's string
comparison could miss.

- Severity: minor
- Steps: sign in as a dictionaries administrator at 1440 px, nothing kept;
  open `/Dictionaries/Languages`. Then `/dictionaries/LANGUAGES/new`, `/HOME`
  and `/dictionaries/%6Canguages`.
- Expected: one answer or the other, consistently. Either the shell reads the
  address as the page the router renders, as it now does for a trailing
  slash — on «Языки» «Меню настроек» with «Языки» `aria-current="page"`, the
  main navigation a rail with «Настройки» current, the trail «Настройки /
  Справочники» (BR-6, BR-19, US-3 "A settings address opened directly"); on
  «Добавить язык» the trail «Настройки / Справочники / Языки»; on «Главная»
  «Главная» `aria-current="page"`. Or the panel does not serve such an
  address and answers «Страница не найдена» (BR-13). Not a page without its
  navigation.
- Actual: the router renders the page — «Языки», «Добавить язык», «Главная»,
  «Языки» respectively, each with its title — but the shell takes none of
  them for that page: no «Меню настроек», the main navigation expanded with
  no entry marked current, and no trail at all.
- Evidence: a scratch Vitest probe on the real `AppRoutes` (not committed):
  `/dictionaries/languages` → settings navigation shown, trail
  «Настройки/Справочники», «Языки» `page`, «Настройки» `true`;
  `/Dictionaries/Languages` → heading «Языки», settings navigation absent,
  trail none, nothing marked; `/HOME`, `/Home` → heading «Главная», nothing
  marked; `/dictionaries/LANGUAGES/new` → heading «Добавить язык», no
  settings navigation, no trail; `/dictionaries/%6Canguages` → heading
  «Языки», as `/Dictionaries/Languages`.
- Cause: react-router matches routes case-insensitively by default
  (`caseSensitive` is off) and decodes the path before matching, while
  `entryMarker` (`app/navigation/visibility.ts`) compares the raw
  `location.pathname`, with only trailing slashes stripped since D-1, to the
  landing strings by exact equality. Asking the router whether the landing
  matches (for example `matchPath` with the landing as the pattern, `end`
  for the page itself) would cover all of these forms and the trailing slash
  in one place.
- Workaround: the navigation itself works, and any entry chosen leads to the
  canonical address.
- Status: open

### Verdict

Re-check 1: blockers: 0 · major: 0 · minor: 1 open (D-4) · D-1, D-2, D-3
fixed and verified · result: **pass**

The counting E2E run (R2) is green, 32 of 32, on a stand brought up from
nothing; its `stand.json` says `warm`, as did R1's on another stand brought
up from nothing. Per § 7 that stops here and is a **deviation to carry
forward** into the gate-3 summary and the MR's `## Deliberately left open`:
no run on the fixed code has started against a `Mustask.Api` that was not
yet ready. The only cold run of this task is pass 1's run B, on the pre-fix
code, where everything but 19.7a (D-3) passed. D-4 is minor and left to the
orchestrator: fix it in this task or file it.
