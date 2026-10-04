# Review: mod-015-app-shell-design-system

## Round 1 — requirements

Reviewer: requirements-reviewer (deep tier, clean context), 2026-09-28.
Inputs: `spec.md`, `stories/US-1..US-12`, `status.md` (D-1..D-5 binding),
cross-checked against `docs/conventions/frontend.md`, ADR-0007, ADR-0018,
ADR-0019, doc-001 (glossary), doc-006 (constraints 1–8, BR-M1-6/8), doc-007
(M2 logical model), doc-010 (codes, plan), the current `src/frontend/src`
(`app/`, `features/auth`, `features/home`, `features/dictionaries`,
`shared/ui`, their tests) and the screenshots in
`docs/features/doc-001-domain-map/screenshots/` (01, 03, 05, 08, 17, 20).

Special checks:

- **Naming (D-3, BR-16).** A case-insensitive search of the task folder for
  the reference tracker's product and vendor names and their abbreviation
  finds nothing. No blocker.
- **No invented API.** The switcher is placeholder-only (BR-11, US-5); the
  component-test dataset in US-5 is a prop, not an API; organization-scoped
  codes are deferred to mod-016 (BR-8). Holds.
- **Scope vs D-2 / D-4.** Only panel screens are restyled; `deploy/keycloak/`
  is out (US-11 last scenario, follow-up row); mod-001/mod-014 files are not
  touched (US-11). Holds; see R-6 for the test-rewrite rule that interacts
  with it.
- **Size.** One L (D-1, confirmed by the human) plus one S follow-up. Consistent.
- **Budgets.** `spec.md` 192 lines, every story under 80. Within budget.

### Rule → story mapping

| Rule | Covered by | Note |
|---|---|---|
| BR-1 theme only source | US-1 | only colour literals are machine-checked (R-14) |
| BR-2 colour language | US-1 | accent exclusivity is under-specified (R-7) |
| BR-3 density | US-1, US-8 | layout heights need a real browser (R-14) |
| BR-4 type/category colours | US-1, US-10 | conflicts with doc-007 (R-1) |
| BR-5 contrast | US-1, US-12 | — |
| BR-6 frame, title, tab | US-2, US-6 | titles/trail for several pages missing (R-9) |
| BR-7 entry visibility | US-3 | — |
| BR-8 same permission check | US-3 | organization scope undefined (R-3) |
| BR-9 hide vs disable | US-6, US-7 | — |
| BR-10 collapse, widths | US-4 | breakpoints vs kept choice (R-16) |
| BR-11 switcher placeholder | US-5 | empty project level (R-16) |
| BR-12 state canon | US-7 | wording contradicts US-7 (R-12) |
| BR-13 not found | US-7 | — |
| BR-14 designed vs built | US-10 | — |
| BR-15 restyle scope | US-11 | change list incomplete (R-6) |
| BR-16 naming | US-10 (design only) | no criterion on code, tests, commits (R-15) |
| BR-17 no new library / host | US-1 | library part has no criterion (R-15); host list wrong (R-10) |
| NFR reflow / widths | US-4, US-12 | contradiction (R-4) |

### R-1
Severity: major
Affects: BR-4, BR-1, US-1 ("Type and category colour language"), US-10; doc-007 § Logical model; mod-001 (in flight)
Problem: BR-4 and US-1 make the icon and colour of each work item type a theme token keyed on `EPIC`/`FEAT`/`US`/`TASK`/`BUG`. The approved doc-007 models `WORK_ITEM_TYPE.icon` and `WORK_ITEM_TYPE.colour` as data set by the seed for the five built-in types (P-5), served by M2, and release 3 lets an organization administrator add custom types. That gives two sources of truth for the same fact, and mod-001, running in parallel, will seed values the panel then either ignores (theme wins, the seeded columns are dead) or renders (a server-supplied colour bypasses BR-1 "no colour literal outside `app/theme`" and BR-5's contrast test). US-1 also keys the category test on the Russian labels («Новые»…), while doc-007 keys categories on `Proposed`/`InProgress`/`Resolved`/`Completed`/`Removed`.
Required resolution: The human decides who owns type appearance (Question H-1). Then BR-4 states it explicitly, e.g. "the seed stores an icon key and a colour token name from a closed set defined in `app/theme`; the theme holds the values", and doc-007/mod-001 are told through the orchestrator. US-1 keys category colours on the category codes, not the labels.
Status: resolved
Resolution: BR-4, BR-2, BR-5 (D-6); US-1 "Appearance tokens", "Unknown appearance token"; US-10 type picker and "Type and category language". Round 2: resolved — ownership follows D-6 (seed stores a token name, `app/theme` holds the values), categories keyed on doc-007's codes, no type or category colour equals the accent, the hand-off to mod-001 is in § Decomposition. Residuals raised as R-20 (storage shape against doc-007's two attributes, sequencing with mod-001) and R-25 (the "not keyed on a type key" check).

### R-2
Severity: major
Affects: US-3 ("A dictionaries reader sees home and settings", "Back from settings", "The current page is marked"), US-12 ("Landmarks", "Tab order"), US-2 ("Page trail"), BR-6
Problem: The spec never says when the settings navigation is shown. It could be a toggle ("activate «Настройки»" swaps the menu, no address change) or a route rule (shown while the current address is a settings page). The stories need the route rule and read the toggle. A user who opens `/dictionaries/languages` from a bookmark: is «Языки» visible and marked `aria-current`, or only after activating «Настройки»? After "Back from settings" the user is still on «Языки» but no visible entry is current. If the last settings entry disappears on token renewal (US-3 BR-8 scenario) while the settings menu is open, what is shown? The reference tracker (screenshots 17, 20) navigates to a settings page and keeps the hub menu as an icon rail beside the settings menu, so there are two navigation regions at once. The spec swaps one menu for the other, which changes US-12's landmark count and tab order. The stories test different outcomes depending on the reading.
Required resolution: Add a BR. The settings navigation is shown if and only if the current address belongs to a settings page. Activating «Настройки» opens the first settings page the user may see (or state the alternative). Say whether the hub menu is replaced or kept as a rail beside the settings menu. Say what "back" does: navigate to «Главная», or to the last hub page. Say what happens when no settings entry remains. Align US-3, US-12 (landmarks, tab order) and the page trail with that rule.
Status: resolved
Resolution: BR-19; US-3 (all scenarios); US-12 "Landmarks outside settings", "Landmarks on a settings page", "Tab order"; US-2 page table. Round 2: resolved — the settings navigation is a route rule (iff a settings address with a visible entry), «Настройки» opens the first visible settings page, the main navigation stays beside it as a rail, there is no separate "back", and losing the last entry answers «Доступ ограничен».

### R-3
Severity: major
Affects: § Navigation model, BR-8, BR-11, US-5; doc-006 constraints 1, 6, 7, BR-M1-8
Problem: Every non-M0 code in the navigation model (`organizations.*`, `process.definition.read`, `workitems.*`, `iterations.*`) is organization-scoped (doc-006 constraint 1). It has no yes/no answer without an organization, and for the «Проект» rows without a project. The spec does not say that the navigation is evaluated against the organization and project selected in the switcher. BR-8's "No navigation entry changes when the source does" can be read as the opposite. This task founds exactly this coupling: what the menu shows after the user switches organization, and what happens to the open page (an org-scoped page of organization A after switching to B). Left open, the coupling is decided by whoever wires mod-002/mod-016, after gate-2 has approved a shell that did not account for it. A second, related gap: nothing says whether the context is part of the address (shareable links to a board or work item, as in the reference, and as US-6's "each view can be linked to" implies) or a remembered selection (as US-5's "remembered selection that is no longer available" implies). That choice shapes every later module's routes, and the ADR this task owes (D-1) is the natural place for it.
Required resolution: Add a BR. Organization-scoped entries are evaluated against the organization selected in the switcher, and «Проект» entries against the selected project. Switching the context re-evaluates the menu and moves the user off a page they can no longer see (to what?). BR-8's last sentence is reworded. The human answers Question H-2 (context in the address or remembered), and the answer is recorded as input to design.md and the ADR.
Status: resolved
Resolution: BR-8, BR-18, BR-11 (D-7, D-11); BPMN S4–S8; US-5 "States mod-002 will wire are designed now". Round 2: resolved — organization-scoped entries are evaluated against the current organization and «Проект» entries against the current project, a context switch re-evaluates without a reload and falls back to «Главная», and the context is part of the address (D-7; extended to organization-level pages, see Round 2 § Analyst calls (a)). Residuals raised as R-21 (not-found copy against mod-001) and R-22 ("the same page").

### R-4
Severity: major
Affects: NFR "Accessibility", NFR "Browsers and widths", § Out of scope ("Phone layouts below 768 px"), US-12 ("Reflow at 200 % zoom"), US-4
Problem: US-12 demands the narrow layout at 1280 px zoomed to 200 %, a 640 CSS px viewport. That is below the 768 px floor the NFR supports, in a range the out-of-scope list excludes. The NFR also claims WCAG 2.1 AA for the shell, the patterns and the restyled screens. AA includes 1.4.10 Reflow, which requires content to work at 320 CSS px (400 % zoom of 1280). The three statements cannot all hold, and QA cannot say which one is the requirement.
Required resolution: The human answers Question H-3. The spec then states one floor consistently. Either the shell reflows to 320 CSS px (1.4.10 met; the "phones" exclusion is reworded to "no phone-specific layout"), or the AA claim is qualified ("AA except 1.4.10 below N px"). The NFR, US-4, US-12 and out of scope use the same number.
Status: resolved
Resolution: NFR "Accessibility" and "Browsers and widths", § Out of scope, BR-10 (D-8); US-4 "Narrow window", "No horizontal page scroll down to the floor"; US-12 "Reflow at 200 % zoom". Round 2: resolved — one floor, 640 CSS px, everywhere; the 1.4.10 gap is stated and owed to the ADR.

### R-5
Severity: major
Affects: US-8 ("Clearing all filters", "Behaviour unchanged"), US-7 ("Empty and empty after a filter")
Problem: When a filter matches nothing, US-8 puts a clear control named «Сбросить фильтр» in the filter bar (a filter is set), and US-7 puts an action «Сбросить фильтр» in the empty-after-filter frame. The screen then has two buttons with the same accessible name. The existing test `LanguagesRegistryPage.test.tsx` ("фильтр без совпадений…") calls `getByRole('button', { name: 'Сбросить фильтр' })`, which throws on two matches. So US-8's "every scenario about … filters … passes unchanged" cannot hold together with US-7 + US-8 as written. Two identically named controls are also poor for screen-reader users (US-12).
Required resolution: Pick one. (a) The filter-bar control gets a distinct name (e.g. «Очистить фильтры»). (b) The empty-after-filter frame's action is dropped because the bar already offers it. (c) US-8's "unchanged" exception explicitly allows rewriting that test. State which.
Status: resolved
Resolution: The filter-bar control is «Очистить фильтры» (US-8 "Clearing all filters from the bar", "Two ways out of an empty filter…"); the empty frame keeps «Сбросить фильтр» (US-7 "Empty after a filter"). Round 2: resolved — `LanguagesRegistryPage.test.tsx` "фильтр без совпадений…" still finds exactly one «Сбросить фильтр».

### R-6
Severity: major
Affects: US-11 (change table, "Existing tests hold"), US-8 ("Behaviour unchanged"), US-2, US-9, BR-15
Problem: US-11 allows rewriting only tests that assert one of four listed changes. The restyle makes more observable changes than that, and today's tests would fail on them:
- The display name leaves the top bar for a closed menu with initials. `AppLayout.test.tsx` "показывает имя из токена в шапке" (`getByText('Иванов Иван')`) fails, and that change is not listed.
- The page trail adds a second «Добавить язык» text on the form page when the trail is rendered with the page. `LanguageFormPage.test.tsx` uses `findByText('Добавить язык')`.
- The empty and empty-after-filter states move from a table row to a centred frame.
- Whether the form's «К списку» button stays is unstated, now that the trail links back.
- R-5.

With the list incomplete, "every test passes, except …" is unsatisfiable and QA has no rule for which rewrites are legitimate.
Required resolution: Complete the change table. Add at least: the display name moves into the user menu, the page trail, the empty states' new frame, the fate of «К списку», and the filter-bar clear control. Or reword the rule: "a test may be rewritten when it asserts layout or placement changed by US-2..US-9; a test about requests, validation, permissions, conflict handling or copy may not".
Status: resolved
Resolution: US-11 closed change table C-1..C-15 naming the tests each rewrites; US-8 "Behaviour unchanged". Round 2: resolved — checked against `AppLayout.test.tsx` (all five tests accounted for by C-1..C-4), `LanguagesRegistryPage.test.tsx`, `LanguageFormPage.test.tsx`, `AccessDenied.test.tsx`, `RegistrySkeleton.test.tsx`, `ErrorBoundary.test.tsx`, `AnonymousScreen.test.tsx`, `HomePage.test.tsx` and e2e specs 01, 04, 05, 06, 09, 10, 14–18: no test outside the table asserts a behaviour the table changes. How the e2e half is run: R-28.

### R-7
Severity: minor
Affects: BR-2, US-1 ("Colour roles"), US-6 ("At most one primary action"), BR-4
Problem: US-1 says no element uses the accent colour except primary actions, the active navigation marker, focus, tabs and links. MUI's text and outlined buttons are accent-coloured by default. US-6 makes every secondary command a text button, «Отмена» in dialogs and forms is one, the new «Сбросить фильтр» control is one, and the empty-state «Добавить язык» is outlined today. The same rule catches type and category colours: in the reference tracker the user-story type and the in-progress category are blue. "A neutral light grey" and "a grey distinct from…" cannot be checked without a named token. The top bar's colour is not stated (white in the reference).
Required resolution: State that secondary, text and outlined buttons use the neutral text colour (icon included, or say the icon may be accent). State that type and category colours must differ from the accent value. Name the top bar's surface. Phrase the checks as "equals theme token X" rather than as colour adjectives.
Status: resolved
Resolution: BR-2, BR-4; US-1 "Colour roles"; US-6 "At most one primary action"; US-11 "In a browser". Round 2: resolved — secondary labels neutral, their leading icons accent, top bar on the content surface, type and category colours unequal to the accent, checks phrased as token equality.

### R-8
Severity: minor
Affects: US-3 ("The current page is marked"), US-2 ("Page trail"), US-7 ("Access denied"), BPMN node E
Problem: The language form (`/new`, `/:code`) is gated by `dictionaries.languages.write`, not read (`LanguageFormPage`). A "dictionaries reader" (Roles table: read-only) opening `/dictionaries/languages/new` gets «Доступ ограничен». So US-3's `aria-current` scenario and US-2's "form for a new language" trail scenario have the wrong actor. US-7 says a user "without `dictionaries.languages.read`" is refused on `/new`, but a user holding write without read would get in. The BPMN asks "Holds the page's read code?" for every page.
Required resolution: Use "a dictionaries administrator" in US-3 and US-2. In US-7, split the scenario: `/dictionaries/languages` refused without read, `/dictionaries/languages/new` refused without write. The BPMN node reads "Holds the page's code?".
Status: resolved
Resolution: US-2 and US-3 actors are dictionaries administrators; US-7 "Access denied, read-gated page" / "Access denied, write-gated page"; BPMN node E "Holds the page's code?". Round 2: resolved.

### R-9
Severity: minor
Affects: BR-6, US-2 ("Page trail", "One title"), US-9, US-11
Problem: Titles and trails are specified only for «Главная», «Языки» and the new-language form. Unspecified:
- the edit form (today «Язык: <код>»);
- the trail on «Главная»;
- the tab title and trail of «Страница не найдена» and «Доступ ограничен»;
- the tab title of the auth screens, where «Mustask» is the card's h1 today;
- whether «Настройки» and «Справочники», which have no page, are links in the trail;
- the trail landmark's accessible name. MUI `Breadcrumbs` defaults to the English `aria-label="breadcrumb"`, which conflicts with the Russian-copy rule and US-12's landmark list.
Required resolution: Add a short table (page → h1, tab title, trail) for the pages this task builds, including edit, not-found, access denied and auth. Say that non-page trail segments are plain text. Give the trail landmark a Russian name.
Status: resolved
Resolution: BR-6; US-2 page table (edit form, not found, access denied, auth states, crash screen) and "Page trail". Round 2: resolved — non-page segments are plain text, the trail is a navigation region «Путь к странице».

### R-10
Severity: minor
Affects: US-1 ("Russian locale and local assets"), US-5 ("No request, no persistence", "Screens of this task ignore the context"), NFR "Network"
Problem: The panel calls Mustask.Api on a different origin (`VITE_API_BASE_URL`, port 5000 on the stand; the panel is on 3000 and listed in the API's CORS). US-1's "no request to a host other than the panel's own and the Keycloak host" therefore fails on a correct build. US-5's "no request is sent" can be broken by the session module's proactive token renewal: every ~2 minutes on the stand, per `src/frontend/AGENTS.md`, which has nothing to do with the switcher.
Required resolution: US-1 allows the panel's origin, Mustask.Api's origin and Keycloak's. US-5 reads "the switcher sends no request; no request other than the session's own token renewal is sent", or the scenario is scoped to a component test with the network mocked.
Status: resolved
Resolution: BR-17, NFR "Network"; US-5 "No request, no persistence" (component test with a failing network mock, browser log allows the session's renewal); US-11 "In a browser". Round 2: resolved.

### R-11
Severity: minor
Affects: US-7 ("Empty and empty after a filter", "The empty state offers only what the user may do", "One frame")
Problem: A reader sees today's hint «Добавьте первый язык кнопкой «Добавить язык» выше.», which points to a button they do not have. The code shows it unconditionally, and US-7 keeps it silently. US-7 also does not say where the centred frame sits. For empty-after-filter the filter bar must stay visible above it, or «Сбросить фильтр» is the only way out. Whether the column headers stay is also open (today they do).
Required resolution: Give the reader a hint of their own (e.g. «Языки ещё не добавлены.») or none. State that the filter bar and page header stay, and whether the table header stays, for both empty states.
Status: resolved
Resolution: US-7 "Empty, for an administrator", "The empty state offers only what the user may do", "Empty after a filter" (page header and filter bar stay; table header row and paging give way; reader hint «Языки ещё не добавлены.»). Round 2: resolved.

### R-12
Severity: minor
Affects: BR-12, US-7
Problem: BR-12 says each of the six states "has one shared frame (icon, heading, hint, optional action)". US-7 makes loading a skeleton and error the shared error component, so only four states share the frame. As written, BR-12 would require a skeleton with an icon and a heading.
Required resolution: Reword BR-12: "empty, empty after a filter, access denied and not found share one frame…; loading is a content-shaped skeleton; error is the shared error component with «Повторить»".
Status: resolved
Resolution: BR-12 reworded (four states share the frame; loading is a skeleton; error is the shared error component). Round 2: resolved.

### R-13
Severity: minor
Affects: US-11 ("The panel's auth screens on the theme"), BR-15
Problem: The criterion lists six texts and says each screen's "single action … unchanged". «Выполняется вход…» has no action today. `login_failed` renders four different second lines (`loginFailedDetail`) that are not listed. The render-crash screen «Что-то пошло не так» / «Обновить страницу» (`shared/ui/ErrorBoundary`) is a panel screen, but it is neither in the restyle scope nor in the state canon.
Required resolution: Change "its single action" to "its action, if any". Cover the `login_failed` detail lines by reference ("all `AnonymousScreen` states"). Put the crash screen in scope (restyled on the theme, outside the shell) or in out of scope.
Status: resolved
Resolution: BR-15; US-11 "The panel's auth screens on the theme" (every `AnonymousScreen` state, each `login_failed` detail line, "its action if any"), "The crash screen on the theme"; US-2 page table. Round 2: resolved.

### R-14
Severity: minor
Affects: BR-1, BR-3, US-1 ("Base density", "The theme is the only source of appearance")
Problem: BR-1 names colours, type sizes, spacing, radii, shadows and icon sizes. The check in US-1 covers colour literals only. The slices hold literal sizes today (`width: 150`, `maxWidth: 480`, `width: 380`, `DRAWER_WIDTH = 240`), so the rest of BR-1 is unverified and the code already contradicts it. The scan's scope is open: are test files and fixtures included? A bare `#[0-9a-f]{3,8}` pattern also matches ordinary strings. "Body text 14 px, top bar 48 px, rows ≤ 40 px" need a layout engine, and `npm test` (jsdom) has none. They are browser or QA checks only, while the "check that runs with `npm test`" is the only automated gate.
Required resolution: Split BR-1: colour literals are machine-checked (state the scope and the excluded paths). Other dimensions come from theme tokens and are checked in review. Or list the allowed exceptions. For density, add a unit test on the theme's tokens (body font size, toolbar min-height, dense row height) as the `npm test` proxy, and leave pixel measurement to QA in a browser.
Status: resolved
Resolution: BR-1 split (colour literals machine-checked with a stated scope and exclusions; other dimensions reviewed, no exception list), BR-3; US-1 "Density tokens", "No colour literal outside the theme"; US-11 "In a browser". Round 2: resolved.

### R-15
Severity: minor
Affects: BR-16, BR-17, US-10 ("Vocabulary and naming")
Problem: BR-16 (D-3) covers code, identifiers, comments, tests, assets and commits. Its only criterion checks `design.md` and the prototype. BR-17's "no new UI library" has no criterion at all. The prototype's own technology is unstated too: a CDN-loaded prototype would fetch from a third-party host.
Required resolution: Add a US-11 scenario. The task's diff and commit messages contain none of the forbidden names (a case-insensitive search, as this review ran on the folder). `package.json` gains no UI dependency beyond MUI core and `@mui/icons-material`. State that the prototype loads nothing from a third-party host, or that BR-17 does not apply to it.
Status: resolved
Resolution: BR-17; US-11 "Naming and dependencies"; US-10 "Vocabulary and naming", "The prototype stays local". Round 2: resolved (BR-17's wording raised as R-26).

### R-16
Severity: minor
Affects: BR-10, BR-11, US-4, US-5, US-12 ("Tab order")
Problem: Several edge cases are open:
- A window resized across 1280 px or 1024 px, with or without a kept choice.
- A kept "expanded" choice below 1024 px: BR-10 implies the overlay wins, but it is not said.
- How the settings menu looks as a rail.
- Whether focus returns to «Меню» or moves to the new page's main region after choosing an overlay entry. The latter is the usual pattern; US-4 says «Меню».
- The tab order of «Настройки» and the collapse toggle, which share the bottom.
- The switcher's "nothing to choose" is tested only for organizations. An organization with no projects is the realistic case: doc-006 gives every project a first team, but a new organization has no project. The BPMN says each level can be empty.
Required resolution: State the resize rule and that below 1024 px the overlay ignores the kept choice. Add a US-5 scenario for an organization with no projects (project level says so, team level hidden). Fix the bottom-of-menu order in US-12. Confirm the focus target after overlay navigation.
Status: resolved
Resolution: BR-10, BR-19; US-4 (resize across 1280 px, overlay ignores the kept choice below 1024 px, focus to the main region after an overlay entry); US-5 "An organization with no projects"; US-12 "Tab order". Round 2: resolved.

### R-17
Severity: minor
Affects: US-10 (header, "Work item form")
Problem: US-10 says it is "verified by hand: by the requirements reviewer…". The requirements review is stage 2, before `design.md` and the prototype exist, and stage 3 has no reviewer of its own. The work item form pattern also leaves out what its consumers need most: where «Сохранить» and «Удалить» / «Восстановить» sit (mod-003, mod-005), and the attachments tab that mod-004 (MVP) adds beside «Детали», «История», «Связи». The prototype's settings pages for areas and iterations (§ Navigation model) are trees in the reference (screenshots 17, 18), and no tree-registry pattern is named, although mod-002 and mod-007 need one.
Required resolution: Change the header to "verified by the human at gate-2 in the prototype; QA checks at stage 7 that `design.md` covers every element listed". Add the form's command placement and an attachments tab to the form scenario. Either add a tree-registry pattern or state that the settings pages in the prototype reuse US-8 and the tree is designed by mod-002.
Status: resolved
Resolution: US-10 header (human at gate-2, QA at stage 7), "Work item form" (command placement, «Вложения» tab, «Восстановить»), "Tree registry"; BR-14. Round 2: resolved.

### R-18
Severity: minor
Affects: § Navigation model ("Labels are the doc-001 glossary's"), decomposition row of this task
Problem: «Главная», «Рабочие элементы», «Доски», «Бэклоги», «Спринты», «Запросы», «Настройки» and «Справочники» are not glossary entries. The glossary has the singular terms, and hub names are new copy. Separately, `docs/conventions/frontend.md` § MUI still mandates `@mui/x-data-grid` for registries and `react-hook-form` + `zod` for forms, which ADR-0019 lifted. This task founds the registry and form patterns and owes the shell ADR, so a stale convention would contradict the patterns it ships.
Required resolution: Say "derived from the doc-001 glossary terms; the hub labels are this spec's copy". Add to the decomposition row that the owed ADR brings `frontend.md` § MUI in line with ADR-0019 and this task's patterns.
Status: resolved
Resolution: § Navigation model (labels derived from glossary terms, hub labels are this spec's copy); decomposition row (the owed ADR brings `frontend.md` § MUI in line with ADR-0019). Round 2: resolved.

### R-19
Severity: minor
Affects: US-2 ("User menu and sign-out", "No display name"); story headers of US-1, US-5
Problem: The initials rule is shown only for a two-word name. `displayName` falls back to `preferred_username` (`tokenClaims.ts`), e.g. `e2e-fixture` or `ivan.petrov`, and single-word names exist. US-1's `depends-on: -` hides that its scenarios read the registry, filter bar and navigation marker of US-3/US-6/US-8. US-5's last scenario needs US-4's narrow layout but depends on US-2 only.
Required resolution: State the initials rule, e.g. "first letters of the first two words; one word → its first letter; upper-case". Set US-1 `depends-on` to the stories its scenarios read, or move those scenarios to US-11. Add US-4 to US-5's `depends-on`.
Status: resolved
Resolution: US-2 "Initials" (rule and four examples, including single-word usernames); US-1 `depends-on: -` with its browser checks moved to US-11 "In a browser"; US-5 `depends-on: US-2, US-4`. Round 2: resolved.

### Questions only the human can answer

- **H-1 (R-1).** Who owns the icon and colour of a work item type: the theme,
  keyed on the type key, or the process seed (doc-007 `WORK_ITEM_TYPE.icon`
  / `.colour`, set by mod-001)? Recommended: the seed stores a token name
  from a closed set; `app/theme` holds the values. That keeps BR-1 and BR-5
  enforceable and doc-007's model intact. mod-001 needs the answer before it
  fixes its seed format.
- **H-2 (R-3).** Is the organization/project/team context part of the page
  address (shareable links; a link into an organization the reader is not a
  member of answers "not found", BR-M1-8) or a remembered selection outside
  the address? Recommended: in the address for project- and team-level
  pages, since every later module's routes depend on it; the owed ADR
  records it.
- **H-3 (R-4).** Does the panel commit to WCAG 2.1 AA reflow (1.4.10, usable
  at 320 CSS px), or is the AA claim qualified with a stated floor (e.g.
  640 CSS px = 1280 px at 200 %)? Recommended: qualify to 640 CSS px for the
  MVP and record the gap, since phone use is out of scope.
- The spec's own OQ-1..OQ-6 remain open and are the human's. The reviewer
  sees no reason to disagree with their recommendations. OQ-5 is required
  for BR-13 as written, and OQ-6 for US-3.

### Edits made by the reviewer

None. Every finding changes a requirement, not a typo, and goes back to the
business-analyst.

### Verdict

`needs-human-decision`. There are 0 blockers, 6 majors (R-1..R-6) and 13
minors (R-7..R-19). H-1..H-3 and OQ-1..OQ-6 need the human. After the
answers, R-1..R-19 go back to the business-analyst for one revision, and
round 2 is a delta over them.

## Round 2 — requirements (delta)

Reviewer: requirements-reviewer (deep tier, clean context; author of neither
the spec nor round 1), 2026-09-28. Scope: R-1..R-19, the reworked
`spec.md` and `stories/` (commit 6eff77f against 3e0e0b2), binding decisions
D-1..D-14. Cross-checked only where the rework points: doc-007 § Logical model
and P-5, doc-006 BR-M1-8, ADR-0007 § canon, ADR-0019, the frontend tests and
e2e specs named in US-11's change table, and mod-001's spec draft (its own
worktree, stage 1, gate-1 pending) for BR-4 and BR-18.

Special checks:

- **Naming (D-3, BR-16).** A case-insensitive search of the task folder for
  the reference tracker's product and vendor names and their abbreviation as a
  word finds nothing. No blocker.
- **R-1..R-19.** 19 of 19 resolved (Resolution fields above). R-1 and R-3 leave
  residuals that are new defects of the chosen resolution, not gaps in it:
  R-20, R-21, R-22, R-25.
- **Change table C-1..C-15 against the code.** Every existing unit test and e2e
  spec was read against the table: the five `AppLayout.test.tsx` tests map to
  C-1..C-4; `LanguagesRegistryPage.test.tsx` (including the two-«Добавить язык»
  empty case and the single «Сбросить фильтр»), `LanguageFormPage.test.tsx`,
  `RegistrySkeleton.test.tsx`, `ErrorBoundary.test.tsx`, `AnonymousScreen.test.tsx`
  and e2e 01, 06, 09, 10, 15–18 hold without an edit as the stories are
  written; e2e 04, 05, 14 are named. The table is complete.
- **Budgets.** `spec.md` 197 lines after the housekeeping below, stories 47–104
  lines. Within budget.
- **Size.** Unchanged: one L plus one S follow-up.

### Rule → story mapping (delta)

| Rule | Covered by | Note |
|---|---|---|
| BR-4 appearance tokens | US-1, US-10 | shape against doc-007 (R-20); check wording (R-25) |
| BR-8 same check, current context | US-3; US-5 design scenario | — |
| BR-10 widths, focus | US-4, US-12 | top-bar content below 1024 px (R-24) |
| BR-17 no new UI dependency | US-10, US-11 | wording (R-26) |
| BR-18 context in the address | US-5 "Screens of this task ignore the context", "States mod-002 will wire…"; the owed ADR | not-found copy (R-21), "the same page" (R-22) |
| BR-19 settings navigation | US-3, US-4, US-12 | — |
| BR-15 / D-12 registration | US-11 "Parallel modules" | «Процесс» stays hidden (R-23) |

### R-20
Severity: major
Affects: BR-4, § Decomposition ("D-6: the orchestrator hands BR-4's token set to mod-001"), US-1 "Appearance tokens"; doc-007 § Logical model (`WORK_ITEM_TYPE.icon`, `WORK_ITEM_TYPE.colour`, P-5); mod-001 draft US-1 ("each type carries an icon and a colour set by the seed") and BR-M2-13 (its screen shows "the icon and colour")
Problem: BR-4 follows D-6 literally — one token name per type, whose icon and colour live in `app/theme`. doc-007's approved model has two attributes, `icon` and `colour`, and mod-001's current draft seeds both as values. BR-4 does not say what becomes of doc-007's two attributes. They could merge into one `appearance` attribute, or both could hold the same token name. It also does not say how mod-001's screen shows an icon and a colour if mod-001 merges first: the token → value map exists only once this task lands `app/theme`, and D-4 keeps this task out of mod-001's files. The hand-off line names the token set and nothing else. So mod-001 would fix its seed format against a half-specified contract. Round 1's H-1 recommendation assumed doc-007's model stays intact. D-6's single token does not keep it intact.
Required resolution: BR-4 (or the D-6 hand-off line) states the storage shape. Recommended: one attribute holding one token name, replacing doc-007's `icon` and `colour`, with the amendment to doc-007's logical model carried by mod-001 as M2's owner. The spec also states the sequencing rule if mod-001 merges first. Recommended, by analogy with D-12: whichever task merges second wires mod-001's screen to the theme's lookup, and until then mod-001 shows the token's type name without a theme colour. See H-5.
Status: resolved
Resolution: D-16. BR-4 states the shape: one token name per type from the closed set, replacing doc-007's `icon` and `colour`, the doc-007 amendment carried by mod-001; BR-15 states the sequencing (consolidation C-2 in place of the literal recommendation; a timing window it left open is R-29); § Decomposition's hand-off bullet carries both to mod-001; US-1 "Appearance tokens". Round 3: resolved.

### R-21
Severity: major
Affects: BR-18 ("A link into an organization or project the user may not see answers «Страница не найдена»"), BR-12, BR-13, US-5 "States mod-002 will wire…", US-2 page table; mod-001 draft BR-M2-11 («Организация не найдена»); doc-006 BR-M1-8
Problem: For the same situation — an organization identifier in the address that is unknown or, after mod-016, not the user's — mod-001's draft answers «Организация не найдена». BR-18 answers «Страница не найдена». Both specs are at gate-1 now, and this task's ADR is to record the rule for every later module. BR-12 allows a page to supply its own heading and hint only for the empty state, so under this spec as written mod-001's copy is outside the canon, and a user gets two different answers to one question depending on the page. doc-006 BR-M1-8 only says "not found". Either copy satisfies it.
Required resolution: Pick one. (a) BR-12/BR-18: the not-found frame may name what was not found («Организация не найдена», «Проект не найден») with the same hint and «На главную»; an unknown panel address keeps «Страница не найдена». (b) mod-001 adopts «Страница не найдена» (the orchestrator tells mod-001). Recommended: (a). It keeps both specs as they are and gives a better answer to a shared link. See H-4.
Status: resolved
Resolution: (a), D-15. BR-12 (the frame names the missing context, same hint and «На главную»; non-member and non-existent indistinguishable from mod-016), BR-13, BR-18 (the outermost level not found); BPMN D2–D3; US-2 page table; US-7 "Not found names the missing context". Round 3: resolved — agrees with mod-001 BR-M2-11 and mod-002 BR-M1-18, BR-M1-20.

### R-22
Severity: minor
Affects: BR-18, BPMN S6–S8, US-5 "States mod-002 will wire…"
Problem: "Choosing another context there opens the same page in the new context" is clear for a hub page (a list, a board) and undefined for a page that addresses one entity: work item 123 of project A has no counterpart in project B. BR-18 also leaves open what the project and team levels show on an organization-level page, whose address carries only the organization. The ADR will copy this rule into every later module's routing, so the gap will spread.
Required resolution: BR-18: "the same page" is the landing page of the same navigation entry; a page that addresses an entity goes to its entry's landing page in the new context. State that on an organization-level page the lower switcher levels show the current selection and changing them does not change the page (or say mod-002 decides it).
Status: resolved
Resolution: D-22. BR-18 (landing page of the same entry; an entity page goes to its entry's landing page; below the address's levels the switcher shows the current selection and does not move the page); BPMN S4–S8; US-5 "States mod-002 will wire…". Round 3: resolved — mod-002 US-8 builds exactly this.

### R-23
Severity: minor
Affects: BR-15, D-12, § Navigation model («Процесс» row), US-11 "Parallel modules"; mod-001 draft BR-M2-10, BR-M2-12
Problem: mod-001's `process.definition.read` is organization-scoped and "not carried in a token" until mod-016 (mod-001 draft BR-M2-10). BR-8 checks only the token until mod-016, so a «Процесс» entry registered under D-12 is hidden from every user, while mod-001's page is open to every signed-in user by address. Its address also needs a real organization identifier (mod-001 BR-M2-12), which the placeholder switcher does not have until mod-002. US-11's "its navigation entry is registered" can then only be checked in code, never on screen. The human should know that D-12 gives the process screen no visible entry before mod-002 and mod-016.
Required resolution: US-11 "Parallel modules": the entry is registered in the navigation model and, per BR-7/BR-8, stays hidden until its code is served (mod-016) and the context is real (mod-002). Carry that into the gate-1 summary.
Status: resolved
Resolution: superseded by D-18. BR-8's interim clause (an organization-scoped code is satisfied by any signed-in user; organization- and project-scoped entries stay hidden until the context is real); § Navigation model note; US-3 "An organization-scoped entry waits for a real context"; US-11 "Parallel modules" (the «Процесс» entry is checked in the model, not on screen). Round 3: resolved.

### R-24
Severity: minor
Affects: BR-6, BR-10, BR-11, US-12 "Reflow at 200 % zoom", US-4 "Narrow window"
Problem: At 640 CSS px the top bar is to hold «Меню», the product mark, three switcher levels, the page trail and the user menu. No rule says what yields: the trail, the team level, or long names. Organization and project names are user data of unbounded length. "No shell control is cut off or overlaps another" does not say whether an ellipsis counts as cut off, so the scenario passes or fails on the reader's reading.
Required resolution: State the rule: a long context name is truncated with an ellipsis, its full name stays in the accessible name and a tooltip, and truncation is not "cut off". Say which top-bar elements hide below 1024 px, e.g. the trail moves under the page header, or leave it to `design.md` with the scenario reworded to "every control stays operable and fully named".
Status: resolved
Resolution: D-22. BR-11 (ellipsis, full name in the accessible name and a tooltip, truncation is not "cut off", what else yields below 1024 px is `design.md`'s call); US-5 "A long name is truncated, not lost" (tightened in round 3, R-30); US-12 "Reflow at 200 % zoom" reworded. Round 3: resolved.

### R-25
Severity: minor
Affects: US-1 "Appearance tokens" ("no token is keyed on a type key, a state name or a Russian label")
Problem: The token names `epic`, `task` and `bug` equal the type keys `EPIC`, `TASK` and `BUG` up to case, so a test cannot tell "keyed on a token" from "keyed on a type key" by reading the map. The clause cannot be verified as written.
Required resolution: Phrase it as behaviour: "the theme's lookup takes a token name only; a type whose key is `BUG` and whose token is `task` renders the `task` icon and colour". Drop the "not keyed on" wording.
Status: resolved
Resolution: D-22. US-1 "The lookups take a token name and a category code only" (a `BUG` type storing `task` renders `task`; «Готово» in `InProgress` renders `InProgress`); BR-4 "its lookup takes a token name only". Round 3: resolved.

### R-26
Severity: minor
Affects: BR-17, § Out of scope (`@mui/x-data-grid`), decomposition row (frontend.md § MUI); ADR-0019 § Decision and "When to revisit"
Problem: "MUI core and `@mui/icons-material` are the set" reads as a standing rule. Written into `frontend.md` by the owed ADR, it would close ADR-0019's door: DataGrid is revisited at "the first large registry (the work item list)", mod-003's page that US-10 prototypes here. The set also omits the dependencies the panel already renders with, `@fontsource/roboto` and emotion.
Required resolution: BR-17 binds this task and its prototype: "adds no dependency that renders UI". ADR-0019's revisit at the work item list stands and belongs to mod-003. The owed ADR does not restate the set as closed.
Status: resolved
Resolution: D-22. BR-17 binds this task and its prototype ("adds no dependency that renders UI"), ADR-0019's revisit stays mod-003's, the owed ADR restates no closed set; § Out of scope; US-10 "The prototype stays local"; US-11 "Naming and dependencies". Round 3: resolved.

### R-27
Severity: minor
Affects: US-9, US-10 "Work item form", BR-18, BR-19, US-3 "Leaving settings"
Problem: The shell adds new ways to leave a page mid-edit: the product mark, the rail, the trail, and — once mod-002 wires it — a context switch that navigates (BR-18). Nothing says what happens to unsaved input in a form page or the work item form. Today the language form has no guard, and whether that stays the pattern decides mod-003's form.
Required resolution: State the rule in US-9 or US-10. Either "no leave guard in the MVP; the language form keeps today's behaviour", or a confirmation dialog pattern designed here (with its copy) and built by its first consumer.
Status: resolved
Resolution: D-22, first option. US-9 "No leave guard in the MVP" and the `design.md` statement in "Patterns beyond this task are designed now"; § Out of scope. Round 3: resolved.

### R-28
Severity: minor
Affects: US-11 "Existing tests hold"; US-1 "Russian locale (BR-17)"
Problem: "The frontend unit suite and the e2e specs … every test passes" relies on the e2e suite. That suite needs a live stand and the human's per-task consent (`src/frontend/AGENTS.md`), and `scripts/verify.sh` does not run it. If the human declines E2E, the criterion and the e2e rewrites of C-1/C-2 cannot be verified. Traceability nit: US-1's locale scenario is labelled BR-17, but the locale rule is the NFR "Localization".
Required resolution: State that the e2e half runs at stage 7 with the human's consent, and that when it is not run QA records it as not verified (the rewritten specs still type-check under `npm run build`). Relabel the US-1 scenario "(NFR Localization)".
Status: resolved
Resolution: D-22. US-11 "Existing tests hold" (the e2e specs run at stage 7 against the stand, consent given 2026-10-01); US-1 "Russian locale (NFR Localization)". Round 3: resolved.

### Analyst calls for gate-1

- **(a) BR-18 puts the organization in the address of organization-level
  pages; D-7 names project- and team-level pages.** Consistent with D-7:
  D-7 sets a minimum and does not exclude organization-level pages. It is
  also the only reading that agrees with doc-006 BR-M1-8, where a link into
  an organization must answer "not found" deterministically, and with
  mod-001's draft BR-M2-12, whose organization-level process screen already
  carries the organization identifier in its address. No separate question.
  The gate-1 summary names it so the human confirms or narrows it along with
  gate-1.
- **(b) BR-4's closed set `epic`, `feature`, `story`, `task`, `bug`.**
  Consistent with D-6: one token name per type from a closed set, values in
  `app/theme`, categories keyed on doc-007's codes. The set itself needs no
  decision. Two things around it do: how doc-007's two attributes `icon` and
  `colour` map onto one token, and what mod-001's screen shows if it merges
  first. D-6 does not answer either (R-20). The shape follows from D-6 and
  the analyst can write it. The sequencing extends D-4/D-12, so it goes to the
  human (H-5).

### Questions only the human can answer (round 2)

- **H-4 (R-21).** A link into an organization or project the user may not see:
  one «Страница не найдена» everywhere, or the not-found frame naming what
  was not found («Организация не найдена», as mod-001's draft has it)?
  Recommended: the frame may name it. An unknown panel address keeps
  «Страница не найдена». Neither spec has to retract anything.
- **H-5 (R-20).** Confirm that D-6 replaces doc-007's `icon` and `colour` with
  one appearance token (the amendment carried by mod-001), and choose the
  sequencing if mod-001 merges before this task. Recommended: as D-12 — the
  second to merge wires mod-001's screen to the theme's lookup, and until then
  mod-001 shows the type without theme colours. The orchestrator hands both to
  mod-001 with the token set.

### Edits made by the reviewer

Housekeeping only. The analyst's "Rework round 1" map was moved from
`spec.md` into the Resolution fields of R-1..R-19 above, and the section was
deleted from `spec.md` (the spec template carries no review history). No
normative text of `spec.md` or any story was changed.

### Verdict

`needs-human-decision`. R-1..R-19: 19 resolved. Round 2: 0 blockers,
2 majors (R-20, R-21), 7 minors (R-22..R-28). H-4 and H-5 need the human, and
(a) goes into the gate-1 summary for confirmation. This was the second and
last review iteration. After the answers, the business-analyst applies H-4,
H-5 and R-22..R-28 as text edits, and the orchestrator checks them against
this list before `gate-1` is set. No third round is needed.

## Round 3 — delta check (2026-10-01)

Reviewer: requirements-reviewer (deep tier, clean context; author of neither
the spec nor rounds 1–2), 2026-10-01. Scope: the decisions dated 2026-10-01
(D-15..D-23) against `spec.md` and `stories/` at 252fa60 (diff against
8483a98), the open findings R-20..R-28, and consistency with the sibling specs
as committed on their branches — mod/001-process-seed (65aa732),
mod/002-organizations-projects-teams (98153a2),
mod/014-keycloak-user-projection (8338fc2) — plus `main`'s queue (124181d) and
tech-001's doc-010 amendment (e482e78) for D-23.

Special checks:

- **Naming (D-3, BR-16).** A case-insensitive search of the task folder and of
  this branch's commit messages for the reference tracker's product and vendor
  names and their abbreviations finds nothing. No blocker.
- **R-20..R-28.** 9 of 9 resolved (Resolution fields above).
- **Budgets.** `spec.md` 213 lines, stories 47–115 lines. Within budget.
- **Size.** Unchanged: one L plus one S follow-up (now `tech-003`).

### Decision → text map

| Decision | Lands in | Check |
|---|---|---|
| D-15 not found by level | BR-12, BR-13, BR-18; BPMN D2–D3; US-2 page table; US-7 "Not found names the missing context" | complete; switcher on a not-found answer: R-31 |
| D-16 one appearance token, sequencing | BR-4 (shape), BR-15 (sequencing), § Decomposition (hand-off); US-1 "Appearance tokens", "The lookups…"; US-11 "Parallel modules" | complete; one merge order unowned: R-29 |
| D-17 address prefixes, untyped ids | BR-18; US-5 "States mod-002 will wire…" | complete |
| D-18 interim BR-8 clause | BR-8; § Navigation model note; US-3 "An organization-scoped entry waits for a real context"; US-11 "Parallel modules" | complete |
| D-19 mod-002 wires, first-team default, tree registry | Context, BR-11, BR-14, § Out of scope; US-5 design scenario; US-10 "Tree registry" | complete |
| D-20 «Все организации», «Нет организаций» | BR-11; US-5 "«Все организации» waits for its page", "No organizations", design scenario | complete; hiding the item and keeping «Нет организаций» as plain text in this build applies BR-7 to D-20's "placeholder until mod-002" |
| D-21 mod-014 has no screen | BR-15; US-11 "Parallel modules"; § Decomposition | complete (`status.md` D-4 and D-12 still name mod-014; `status.md` is the orchestrator's) |
| D-22 R-22, R-24..R-28 | see their Resolution fields | complete; R-24's scenario tightened: R-30 |
| D-23 the owed ADR | § Decomposition, this task's row | complete; contents aligned with tech-001's § ADR debt row: R-32 |

### Sibling consistency

- **mod-002 (98153a2).** BR-M1-24 and US-8 build what BR-11 and BR-18 leave
  to them: alphabetical lists, the first team as default, «Все организации»
  and «Нет организаций» leading to the organization list, the remembered
  selection with its fallback, the address setting the switcher, the
  landing-page rule, the organization-level page whose lower levels do not
  move it, and the failed state with «Повторить». BR-M1-18 and BR-M1-20
  (deleted team, deactivated organization or project → not found by level)
  fall under BR-18's "may not see". D-c's «Процесс» link inside the project
  settings, at `/organizations/:organizationId/settings/process`, fits D-17's
  organization prefix and sits beside this spec's «Процесс» settings entry
  (D-12) without conflict. Two gaps found against it: R-30 (name length),
  R-31 (the switcher on a not-found answer).
- **mod-014 (8338fc2).** No screen (its D-1); app/theme and AppLayout
  untouched (its D-2). D-21 holds.
- **mod-001 (65aa732).** The branch has not yet applied its own gate-1
  answers. As committed it still says: BR-M2-12 "adds no navigation entry …
  shell change or theme change"; BR-M2-13 and § Out of scope "the icon and
  colour … fixed by the seed (doc-007 P-5)"; NFR Layout "otherwise mod-015
  restyles this screen". Those contradict D-16 (one appearance token, the
  doc-007 amendment carried by mod-001, this task never edits mod-001's slice)
  and D-12 (mod-001 registers its own entry when it merges second), and its
  screen's address is not yet tied to D-17's prefix. This spec is right under
  its decisions, and the correction is mod-001's, already owed through
  § Decomposition's hand-off bullet. Not a finding against this spec; see the
  cross-spec notes below.

### R-29
Severity: minor
Affects: BR-15, § Decomposition (D-12/D-16 bullet), US-11 "Parallel modules"; D-12, D-16
Problem: D-16 gives the adoption of mod-001's screen (theme lookup, canon frames) to mod-001 when this task is in `main` before mod-001's frontend stage, and to the D-12 follow-up line when mod-001 merges first. D-12 files that line only if mod-001 is merged when this task merges. A third order is realistic with both tasks at gate-1 in parallel: this task merges after mod-001's frontend stage has begun but before mod-001 merges. Then nobody owns the adoption. mod-001's duty is tied to the earlier start, and the follow-up line is not filed.
Required resolution: Close the window without contradicting D-12 or D-16. mod-001 merges second, so it wires the screen on its rebase; D-12 already makes it register its own entry in that order. The follow-up line stays reserved for "mod-001 merged first".
Status: resolved
Resolution: BR-15 reads "if mod-001 merges first, the D-12 follow-up adoption line does it; otherwise mod-001 wires it itself — in its frontend stage when this task is in main before that stage, or on its rebase when this task merges after that stage has begun". § Decomposition's bullet adds "otherwise mod-001 adopts it itself (BR-15)", and US-11 "Parallel modules" names both owners. This is the reviewer's reading of D-12 with D-16 and contradicts neither text. The gate-1 summary names it for confirmation, and it goes to mod-001 with the D-16 hand-off.

### R-30
Severity: minor
Affects: US-5 "A long name is truncated, not lost"; BR-11; mod-002 BR-M1-1
Problem: The scenario uses a 120-character organization name, which mod-002's BR-M1-1 rules out (names are 1–100 characters). It also checks the ellipsis in a component test. The panel's `npm test` runs in jsdom, which has no layout, so a CSS ellipsis cannot be observed there, and the scenario passes or fails depending on how the test author reads "shows".
Required resolution: Use the longest real name (100 characters). Keep the accessible name and the tooltip in the component test, and move the ellipsis to a browser measurement by QA.
Status: resolved
Resolution: US-5 scenario reworded as required: a 100-character name per mod-002 BR-M1-1, with the ellipsis measured by QA in a browser 1024 px wide.

### R-31
Severity: minor
Affects: BR-18, US-5 "States mod-002 will wire are designed now"; mod-002 BR-M1-24, US-8 "The address sets the switcher"
Problem: BR-18 says that on a page whose address carries a context "the switcher shows the address's context", and mod-002 makes such an address set the remembered selection. A link whose context is not found has no context to show. Nothing says whether the switcher then shows the found outer levels, clears, or keeps its selection. The ADR copies BR-18 into every later module's routing.
Required resolution: State that a not-found answer leaves the switcher's selection unchanged, and show it that way in the prototype.
Status: resolved
Resolution: BR-18's not-found sentence ends "and leaves the switcher's selection as it was". US-5's design scenario shows the not-found links with "the switcher keeping its selection (BR-18)". This agrees with mod-002 US-8, whose address scenarios all name contexts that are found.

### R-32
Severity: minor
Affects: § Decomposition (this task's row, the follow-up row, the queue consequences), § Out of scope; doc-010 § ADR debt as tech-001 amends it (D-23); `main` 10f5364, 124181d
Problem: Three facts went stale after 2026-10-01. First, the follow-up line is queued as `tech-003-keycloak-login-theme` (main 124181d), but the spec still says `tech-NNN`. Second, the queue consequences still read as owed. mod-016's line was amended in `main` (10f5364). mod-002's line had been taken before D-10, so its new dep and scope live in its own `status.md` (D-b). Third, tech-001's § ADR debt row for this task lists the appearance tokens among the owed ADR's contents (D-23), and this spec's row did not, so the two descriptions of one ADR differed.
Required resolution: Update the id and the applied state, and add the appearance-token set to the owed ADR's contents.
Status: resolved
Resolution: § Out of scope and § Decomposition name `tech-003-keycloak-login-theme (queued, main 124181d)`. The queue-consequence bullets record where each consequence was applied. This task's row adds "the closed appearance-token set that mod-001's seed names (BR-4, D-6, D-16)".

### Cross-spec notes for the orchestrator

These are not findings against this spec. They go to the gate-1 summary or to
the owning task.

- **mod-001 has not yet taken D-12, D-16 and D-17.** At 65aa732 its spec still
  holds the icon-and-colour pair, rules out its own navigation entry and theme
  wiring, and expects this task to restyle its screen (see § Sibling
  consistency). The hand-off owed under § Decomposition should reach
  mod-001's round-2 delta before its seed format is fixed. It should carry the
  single token, the doc-007 amendment, R-29's sequencing, its own entry if it
  merges second, and its screen's address under
  `/organizations/:organizationId/` (mod-002 D-c names
  `/organizations/:organizationId/settings/process`).
- **tech-003's queue line** says "restyle … to mod-015's shell tokens and
  copy", while this spec's follow-up row says "Russian copy and flows
  unchanged". If "copy" means the interface text, the line changes what D-2
  keeps. The orchestrator rewords the line in `main` or tells tech-003's
  analyst that this row governs.
- **R-29** is the reviewer's reading of how D-12 and D-16 combine. The human
  confirms it at gate-1 together with the rest.

### Edits made by the reviewer

- `spec.md`: BR-15 (R-29), BR-18 (R-31), § Out of scope and § Decomposition
  (R-32, and R-29's bullet).
- `stories/US-5`: "A long name is truncated, not lost" (R-30), "States mod-002
  will wire are designed now" (R-31).
- `stories/US-11`: "Parallel modules" (R-29).
- `review.md`: the Status and Resolution fields of R-20..R-28 are filled in.

No other normative text was changed.

### Verdict

`approved-by-ai`. R-20..R-28: 9 resolved. Round 3 found 0 blockers, 0 majors
and 4 minors (R-29..R-32), all fixed in place. Every decision dated
2026-10-01 is reflected completely, and no new contradiction or untestable
criterion remains. `gate-1` is the human's. The summary carries R-29's
reading and the two cross-spec notes.

## Design check (2026-10-01)

Reviewer: an adversarial design reviewer standing in for the human at gate-2,
which the human delegated. It took the requirements-reviewer's stance and
had a clean context. It wrote none of `design.md`, the prototype or
ADR-0024. Scope: `design.md`, `prototype/index.html` and
[ADR-0024](../../adr/0024-app-shell-and-visual-language.md) at 4c771f5,
checked against `spec.md` and `stories/US-1..US-12` at the same commit. The
check also covered the sibling artifacts as committed on their branches:
mod/001-process-seed (6577518: `design.md`, the prototype, `spec.md`),
mod/002-organizations-projects-teams (35e55c0: `spec.md`, stories) and
mod/014-keycloak-user-projection (93fa86d: `spec.md`). The remaining sources
were `docs/conventions/frontend.md`, ADR-0007, ADR-0019 and doc-002, and the
code at this commit (`AppLayout`, `AnonymousScreen`, `ErrorBoundary`,
`ErrorBanner`, the dictionaries slice, `apiErrorText`). Component and icon
names were checked against MUI 9.4.0 as installed in the main checkout,
which `^9.3.1` resolves to.

Special checks:

- **Naming (BR-16).** A case-insensitive search of the task folder and of
  ADR-0024 for the reference tracker's product and vendor names and their
  abbreviations finds nothing. The commit message of 4c771f5 is clean.
- **Local prototype (BR-17).** The prototype contains no URL to any host. Its
  script compiles. After this check's edits, seven routes were rendered in
  jsdom: the work item form, the organization list with and without
  organizations, the failed switcher, the language edit form, the auth card
  and «Процесс». Titles, `h1`s and trails match the design, and no page
  error is raised.
- **Contrast (BR-5).** Every figure in § 2.1 and § 2.3 was recomputed with the
  WCAG 2.1 formula and confirmed. The tightest are a type colour on a
  selected row (`epic`, 3.98:1), secondary text on the selected navigation
  entry (4.52:1) and the input border on content (3.45:1). The prototype's
  long organization name is exactly 100 characters (US-5, mod-002
  BR-M1-1).
- **MUI only (frontend.md, ADR-0019).** Every component named is
  `@mui/material` core and every icon comes from `@mui/icons-material`, so no
  UI dependency is added. Four icon names and two props did not exist in
  MUI 9 (R-35, R-36). Every other name is present.
- **Copy.** Every Russian string of the restyled screens was compared with
  the code and with the stories' quoted copy, and all of them match exactly.
  New copy (switcher states, the fallback notice, pattern copy) is Russian
  and follows the doc-001 glossary. «Задача» names only the type, except in
  the home text «трекер задач», which US-11 requires verbatim. That is not a
  finding.
- **No invented API.** Nothing in the design calls or defines a backend
  operation. § 5.7's context source is a frontend seam that mod-002 fills.
  The context addresses of other lines are illustrative under BR-18's fixed
  prefixes (R-42, R-44).

### Scenario → design coverage

| Story | Scenarios | Where | Check |
|---|---|---|---|
| US-1 | 8 | § 2.1–2.7; prototype «Язык оформления» | complete |
| US-2 | 8 | § 3.1–3.4, § 12.2 | complete; the work item form's tab: R-38 |
| US-3 | 9 | § 4.1–4.4, § 4.6; prototype views «Сборка mod-015» / «Целевое MVP» | complete |
| US-4 | 9 | § 3.6, § 4.3, § 4.5, § 2.6 (motion) | complete |
| US-5 | 10 | § 5.1–5.7; prototype «Данные переключателя» | complete; R-39, R-40 |
| US-6 | 5 | § 6; prototype command bar with overflow and tabs | complete |
| US-7 | 10 | § 7, § 8.3, § 10.8 | complete |
| US-8 | 8 | § 8 | complete; R-36 |
| US-9 | 10 | § 9 | complete |
| US-10 | 11 | § 10; prototype | complete; R-41, R-42, R-43 |
| US-11 | 9 | § 11; C-1..C-15 mapped in § 11.2 | complete; R-34 |
| US-12 | 7 | § 12, § 2.6 | complete |

The canon is designed on every built screen and pattern. «Языки» has
loading, error, both empty states, empty after a filter and access denied.
The language form has loading, load error and access denied, for create and
for edit. The frame has «Страница не найдена», not found at each of the three
levels, and «Доступ ограничен». The switcher has ready, loading, failed, no
organizations, no projects, no teams, a long name and the fallback. The
list, board, backlog and tree each have loading, error and empty, and the
list also has empty after a filter. The auth card has six states, plus three
detail lines, and there is the crash screen.

### Sibling consistency

- **mod-001 (6577518).** Its design derived layout B from this spec and asked
  this design to confirm six points (its § "Adopting mod-015"). This design
  had said no neighbouring design existed (R-33). The six points are now
  answered in § 15. One of them changes mod-001's copy: its trail T-26
  becomes «Настройки / Организация» (R-37). Its one-column rule in layout B
  moves from 1200 to 1280 px (the theme's `lg`). The rest agree. The
  prototype's seeded statuses match doc-007 and mod-001: «Разрешена» is in
  progress on three types and resolved on «Баг», and «Задача» has no
  «Разрешена». The prototype's «Процесс» body is illustrative, and
  mod-001's cards govern it.
- **mod-002 (35e55c0).** BR-M1-24 and US-8 match § 5: the lists,
  «Все организации», «Нет организаций», the failed state with «Повторить»,
  the fallback notice, the landing-page rule and the organization-level page
  whose lower levels do not move it. The team order differed (R-40). The
  settings entries were read as final (R-44). The organization list's empty
  state was outside the canon (R-43). mod-002's stage 3 builds on this
  design after gate-2 (its NFR "Seam with mod-015").
- **mod-014 (93fa86d).** It has no screen and leaves `app/theme` and
  `AppLayout` alone (its D-2 (2)). The user menu reads the token's name. Its
  display-name rule (BR-UP-3: first and last name, else the username) gives
  the same initials as US-2's examples. Consistent.
- **doc-002.** The identifier form `PRM-US-12` follows BR-M3-5, and the title
  rule in § 9.5 follows BR-M3-3. The work item's address: R-42.

### R-33
Severity: minor
Affects: `design.md` header, new § 15; mod-001 `design.md` § "Adopting mod-015"
Problem: `design.md` said that no neighbouring `design.md` existed. But mod-001's design (6577518, committed nine minutes earlier) builds its layout B on this spec and asks this design to confirm six points: the section, order and icon of «Процесс», the trail, the frame's not-found icon and error title, the header height, the lookups and the marker shape, and two cards at `lg`. While they stay unanswered, mod-001's frontend builds its own reading of the shell.
Required resolution: Name mod-001's design and answer each point.
Status: resolved
Resolution: The header bullet is reworded. § 15 answers the six points, and the rules it cites are now stated in § 2.3, § 3.4 and § 7.1.

### R-34
Severity: minor
Affects: § 2.2, § 5.1, prototype CSS; BR-2, US-11 "In a browser"
Problem: BR-2's list of accent uses is closed. The design left several controls at MUI's default `primary`, which is the accent: `Switch` («Активен» on the restyled language form), `Checkbox` (work item filters), `CircularProgress` (the auth card, busy buttons), the menu's current-entry check and focused field labels. The prototype draws the checks, checkboxes, switch and auth spinner in the accent. A code review against BR-2 refuses the restyled form.
Required resolution: Keep these controls out of the accent, or amend BR-2.
Status: resolved
Resolution: § 2.2 makes `palette.secondary` the neutral text colour and the default colour of `Checkbox`, `Radio`, `Switch` and `CircularProgress`. The menu's `Check` is `text.primary`, and focused labels stay `text.secondary`. The prototype CSS follows. If the human prefers accent selection controls, BR-2 needs a one-line amendment. The design itself would not change.

### R-35
Severity: minor
Affects: § 2.3, § 2.8, § 3.3, § 5.4, § 10.3, § 11.4; `package.json` `@mui/icons-material ^9.3.1`
Problem: `HelpOutline`, `PersonOutline`, `ErrorOutline` and `DeleteOutline` do not exist in `@mui/icons-material` 9. Only the suffixed forms are exported, such as `HelpOutlineOutlined`. Imports by these names fail the build.
Required resolution: Use names that version 9 exports.
Status: resolved
Resolution: The four are renamed to `HelpOutlineOutlined`, `PersonOutlineOutlined`, `ErrorOutlineOutlined` and `DeleteOutlineOutlined`, and § 2.8 states the rule.

### R-36
Severity: minor
Affects: § 8.2 (`FilterBar`), § 10.1
Problem: Two of the props named are gone from MUI 9's types. `Stack` takes no system props, so `flexWrap="wrap"` fails, and `TextField` has no `SelectProps`. TypeScript strict refuses both.
Required resolution: Name the MUI 9 forms.
Status: resolved
Resolution: § 8.2 reads `Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}`, as today's registry does. § 10.1 reads `slotProps={{ select: { multiple: true } }}`.

### R-37
Severity: minor
Affects: § 3.4; BR-6, US-2; mod-001 T-26
Problem: The trails of «Языки» («Настройки / Справочники») and of the prototype's organization and project settings pages imply the rule «Настройки / <section>», but the design never states it. mod-001 derived a one-segment «Настройки» for «Процесс», so two designs read BR-6 two ways.
Required resolution: State the settings-trail rule.
Status: resolved
Resolution: § 3.4 states it. § 15 tells mod-001 that its T-26 becomes «Настройки / Организация» (see the notes below).

### R-38
Severity: minor
Affects: § 10.3, the prototype's work item page; BR-6
Problem: The work item form's `h1` reads «Пользовательская история PRM-US-12», while its tab reads «PRM-US-12 <заголовок> — Mustask». BR-6 makes the page's title its only `h1` and has the tab read «<title> — Mustask». mod-003 would copy a pattern that breaks the rule.
Required resolution: Make the tab and the `h1` agree.
Status: resolved
Resolution: The tab now repeats the `h1` («Пользовательская история PRM-US-12 — Mustask») in § 10.3 and in the prototype. The editable title is a field, not the heading. A tab carrying the item's title would need BR-6 amended, which mod-003 may propose in its own spec.

### R-39
Severity: minor
Affects: § 5.4, prototype CSS; US-12, BR-11
Problem: Below 1024 px the switcher's failure text moved into the tooltip of an icon that takes no focus, and the prototype sets it to `display:none`. A screen-reader or keyboard user hears only «Повторить» and never learns what failed.
Required resolution: Keep the text available to assistive technology.
Status: resolved
Resolution: The text is visually hidden (`visuallyHidden`), so screen readers still read it, and pointer users get the tooltip. The prototype clips the text instead of removing it.

### R-40
Severity: minor
Affects: § 5.1; mod-002 BR-M1-24
Problem: § 5.1 put all three switcher levels in alphabetical order. mod-002 lists a project's teams with its first team first.
Required resolution: Follow mod-002's order.
Status: resolved
Resolution: § 5.1 now reads: organizations and projects in alphabetical order, and teams with the project's first team first, then the rest in alphabetical order.

### R-41
Severity: minor
Affects: § 10.1, § 10.7; NFR Accessibility (the patterns are held to WCAG 2.1 AA)
Problem: The list rows carry `aria-selected`, and the tree rows carry `aria-level`, `aria-selected` and `aria-expanded`, inside a plain `Table`. Screen readers expose those states only inside `grid` or `treegrid`, so they would announce neither the selection nor the level, and automated checks flag the attributes.
Required resolution: Name the role, and give the keyboard model to the line that builds the pattern.
Status: resolved
Resolution: § 10.1 and § 10.7 give the list `role="grid"` and the tree `role="treegrid"`. Each building line takes the WAI-ARIA keyboard model: mod-003 for the list, mod-002 for the tree. The prototype keeps its simplified markup, because it is a prototype only.

### R-42
Severity: minor
Affects: § 10.3, the prototype route; doc-002 US-4, BR-M3-5; BR-18
Problem: § 1 and the prototype put the work item form under the project prefix. doc-002 US-4 reads "an address names the organization, then the identifier", and the identifier already carries the project key. The pattern can be read as fixing mod-003's route.
Required resolution: Say that the prefix is mod-003's choice.
Status: resolved
Resolution: § 10.3 says that mod-003 chooses the prefix, organization or project, within BR-18. The prototype's route is illustrative.

### R-43
Severity: minor
Affects: the prototype's `orgListPage`, § 10.9; BR-12; mod-002 US-1
Problem: «Нет организаций» led to a table with a header row and no rows. It was the only empty state in the prototype outside the canon, because § 7.3 has empty states replace the table.
Required resolution: Show the canon's frame.
Status: resolved
Resolution: The page shows the frame «Организаций пока нет», mod-002 US-1's heading, and mod-002 supplies the hint and the action. § 10.9 says so.

### R-44
Severity: minor
Affects: § 4.1; mod-002 BR-M1-23
Problem: mod-002 BR-M1-23 leaves to its stage 3 which entry holds which of its pages. It also has pages that the model lacks, the organization's project list and the project's general settings. § 4.1 listed fixed entries under «Организация» and «Проект» without saying that they show placement only.
Required resolution: Say what the rows fix and what they leave to the owning line.
Status: resolved
Resolution: A paragraph after § 4.1's table says that the rows show placement and scope, that mod-002's stage 3 decides its entries, and that BR-19's first page follows the model as it then stands.

### R-45
Severity: minor
Affects: § 2.3, § 4.5, § 7.1, § 10.3; ADR-0024 Decision 3; frontend.md § Slice rules
Problem: Five points were left unstated or slightly wrong:
- The design did not say how a slice reaches the lookups in `app/theme`. frontend.md has `app/` assemble the slices, not the reverse.
- It did not say that the theme holds no category labels (mod-001 keeps the one list), or how the `Removed` ring is drawn.
- `keepMounted` on the overlay contradicted § 12.2's "the overlay's navigations exist only while it is open".
- `ErrorBanner` had no title slot for mod-001's «Не удалось загрузить процесс».
- The «Обсуждение» field was named by its placeholder only.
Required resolution: State each point.
Status: resolved
Resolution: The edits:
- § 2.3: slices read the lookups through the theme object, the theme holds no labels, the marker shape follows the code, and the marker is drawn as an 8 px circle, not as an icon glyph. ADR-0024 Decision 3 gains the same clause on reading the lookups.
- § 4.5: `keepMounted` is dropped.
- § 7.1: `ErrorBanner` takes an optional `AlertTitle`.
- § 10.3: the field gets an accessible name.

### Notes for the orchestrator (not findings)

- **gate-1 first.** `status.md` still reads `gate-1: pending` at 4c771f5,
  and the design log records that stage 3 ran that way. Round 3 gave
  `approved-by-ai`. mod-001 and mod-014 have "gate-1 approved under
  delegation" commits, and this task has none. Record `gate-1` before
  `gate-2`. `status.md` D-4 and D-12 still name mod-014 (see round 3).
- **To mod-001** (its frontend stage, or a delta now):
  - T-26 and the layout B sketch's trail become «Настройки / Организация».
  - Layout B's one-column breakpoint is 1280 px.
  - The category marker is a drawn circle, a ring for «Удалённые», not an
    icon glyph.
  - The reconciliation table is answered in this design's § 15.
- **To mod-002's stage 3:**
  - § 4.1 (its settings entries are its own call).
  - § 5 (the switcher, team order per its BR-M1-24).
  - § 10.7 (the treegrid keyboard model).
  - § 10.9 (the organization list).
- **To mod-003's stage 3:**
  - R-38: the tab repeats the `h1`, unless mod-003 proposes amending BR-6.
  - R-41: the list's grid role and keyboard model.
  - R-42: the address prefix.
- **ADR-0024** stays `proposed`. The human accepts it together with gate-2.

### Edits made by the reviewer

- `design.md`:
  - The header bullet on neighbouring designs.
  - § 2.2, § 2.3, § 2.8, § 3.3, § 3.4, § 4.1, § 4.5, § 5.1, § 5.4, § 7.1,
    § 8.2, § 10.1, § 10.3, § 10.7, § 10.9 and § 11.4.
  - New § 15 (reconciliation with mod-001).
- `prototype/index.html`:
  - The switcher's failure text is visually hidden below 1024 px.
  - The menu check, the checked checkbox, the switch and the auth spinner
    are neutral.
  - The work item tab repeats the `h1`.
  - The empty organization list shows the canon frame.
- ADR-0024: Decision 3 says that slices read the lookups through the theme.

`spec.md` and the stories were not changed.

### Verdict

`approved-by-ai`. The check found 0 blockers, 0 majors and 13 minors
(R-33..R-45), all fixed in place. Each of the 104 story scenarios has its
screen or state in `design.md` and, where the scenario asks for one, in the
prototype. The canon is designed on every built screen and pattern. The copy
is Russian and matches the code and the stories exactly. The stack is MUI
core and `@mui/icons-material` only, with no new dependency, and nothing
defines an API. `gate-2` is the orchestrator's to record, after `gate-1`.

## Round 1 — frontend (code review, 2026-10-04)

Stage 8, round 1: a full pass over `git diff main...mod/015-app-shell-design-system`
at 71a0a90 (124 files, +15 984/−514), by a session that wrote none of it.
Read before the diff: `status.md` (D-1..D-25 taken as binding), `spec.md`,
US-1..US-12, `design.md` §§ 0–7 and 13, `plan-frontend.md` in full, ADR-0024,
`qa.md` (pass 1, re-check 1, D-1..D-4), `docs/conventions/frontend.md` and the
git convention on commits. There is no backend side and no `api-contract.md`
(stages 4–5 skipped, D-1).

Run in this worktree (`npm ci` first): `npx vitest run` — 56 files, 540 tests,
green; `npm run lint` — green; `tsc -b` — green; `vite build` — green with a
dummy `VITE_API_BASE_URL`, as `scripts/verify.sh` builds it. No stand was
started and the E2E suite was not re-run: QA's R2 (32/32) is the evidence
for the browser-only checks. The branch diff and its commit messages were
searched case-insensitively for the reference tracker's product and vendor
names and abbreviations: nothing.

Verdict: **needs-changes** — 0 blockers, 2 majors, 7 minors. Both majors
are prose, not product behaviour: two documents promise more than the code
next to them in the same diff delivers, and one of them is the catalogue
later modules are told to build on. The code itself holds up.

### What holds (checked, not assumed)

- **Navigation and visibility.** One model (`app/navigation/model.ts`), one
  check (`AppLayout.tsx:85-88`: `can(code)` for instance codes, any
  signed-in user for scoped ones per D-18), and scoped entries gated on
  `isReal` (`visibility.ts:46`). Token renewal and a context switch
  re-evaluate the navigation through the `useMemo` dependencies
  (`AppLayout.tsx:90-94`). The tests for this exercise the real path. A
  renewed `AuthContext` re-renders the mounted shell, and the location
  probe is proved not to remount (`AppLayout.navigation.test.tsx:195-209,
  307-320`).
- **The not-found seam.** `NotFound` registers through the page chrome in a
  layout effect (`StateFrame.tsx:133-134`). The shell then treats the
  address as no page (`AppLayout.tsx:98-107`), so the BPMN order (not found
  before the settings decision) holds. QA's D-1 and D-2 are fixed at their
  cause (`visibility.ts:92`, `:101`, `:124`).
- **The overlay.** It closes on any layout change (`AppLayout.tsx:133-137`),
  stands at `zIndex.modal` over a `drawer + 1` top bar
  (`NavigationOverlay.tsx:44`, `TopBar.tsx:35`), and moves focus to `main`
  only after an entry is chosen (`AppLayout.tsx:141-151`).
- **Theme.** The 14 px body, the 48 px bar and the 36 px row are all real
  (`theme.ts:74`, `tokens.ts:115`, `:144`), and they are measured on a
  rendered row and bar (`theme.test.tsx:50-65`, `:85-95`). The secondary
  button labels are neutral (`components.ts:37-56`). Selection controls are
  off the accent (`components.ts:212-215`). `focusVisible` and
  `motion.reducedMotion` are real options of the installed MUI 9.4
  (`node_modules/@mui/material/styles/createThemeNoVars.d.ts:26-60`).
- **The BR-1 colour check is sound for what BR-1 says.** It parses with the
  TypeScript compiler, guards against a vacuous pass
  (`colourLiterals.test.ts:107`) and self-tests both directions. The
  dimension half of BR-1 (code review's job) was swept. Every product file
  outside `app/theme` uses tokens or the spacing unit, with one exception:
  F-6.
- **Architecture.** `shared/` imports nothing from `app/` or `features/`.
  Slices import `app/` only in tests. `app/` reaches the dictionaries slice
  only through its barrel (`model.ts:2`, `routes.tsx:4`). There is no new
  dependency, and nothing under `deploy/`, `src/backend/` or mod-001's
  slice changed.
- **Restyle.** The only existing tests edited are those that US-11's change
  table names (`git diff --stat -M main...HEAD -- '*.test.*'`). That is
  `AppLayout.test.tsx` and three renames, two of them with no changes and
  `AccessDenied.test.tsx` with its import path. The behaviour and copy of
  the registry, the form, the dialog and the auth screen are unchanged
  outside C-1..C-15.
- **Security.** There is nothing server-side. User-controlled input reaches
  only text nodes and `document.title` (the `:code` param). `localStorage`
  is read as an allow-listed enum (`keptChoice.ts:5-23`). The placeholder
  source sends nothing (`ContextSwitcher.test.tsx:121-150`).

### F-1 (major) — the colour-literal check is described as wider than it is

`src/frontend/src/app/theme/tokens.ts:4-5`: "This is the only file under
`src/` that holds colour literals (BR-1, checked by
`src/test/colourLiterals.test.ts`)". `docs/conventions/frontend.md:55-56`:
"No colour literal outside it — `src/test/colourLiterals.test.ts` fails
`npm test` on one". Also, more softly, `docs/adr/0024-app-shell-and-visual-language.md:114-115`
("rejects colour literals outside `app/theme`").

Files in the same diff disprove both sentences. `contrast.test.ts:89-98`
(under `src/`, in `app/theme`) holds `'#000000'`, `'#FFFFFF'` and
`'#0F6CBD'`. `colourLiterals.test.ts:80-84` (outside `app/theme`) holds
`'#0F6CBD'`, `'#fff'` and `'#C42B1C'`. `npm test` is green with all of them,
correctly: BR-1 exempts `*.test.*` and `src/test/`, and the check skips
`app/theme/` and `src/test/` (`colourLiterals.test.ts:61-68`). The
convention is the sentence every later agent reads. It states a guarantee
over a set the mechanism does not cover, the same shape as F2 of the
example review the stage cites. Severity is major by the stage's rule for a
sentence its own diff contradicts. The fix is a rewording.

Fix: word both sentences as BR-1 does. For example, in frontend.md: "No
colour literal outside it, test files (`*.test.*`) and `src/test/` excepted:
`src/test/colourLiterals.test.ts` fails `npm test` on one". In tokens.ts:
"the only product file holding colour literals; the check scans `src/`
outside `app/theme/`, the tests and `src/test/`". The ADR line may take the
same clarification as a non-substantive edit, or stay as written. Drop or
narrow "Components never import it" (`tokens.ts:5`) too: the hooks
`useNavigationLayout.ts` and `useWidthClass.ts` import its `WidthClass`
type.

Status: open

### F-2 (major) — design.md tells later modules to pass ancestors the shell already derives

`docs/features/mod-015-app-shell-design-system/design.md:370` (§ 3.4):
"Every page passes its title and its ancestors to `PageHeader`". The code
in the same diff does something else. The shell derives «Настройки /
<section>» and the entry link from the navigation model, then appends
whatever the page passes (`AppLayout.tsx:116-120`), and `PageHeader`'s own
contract says its `ancestors` are only those "below the ones the shell
derives" (`PageHeader.tsx:48-49`). The plan records the change
(`plan-frontend.md:588`, § 9 D-7), but later lines do not read mod-015's
plan. `docs/conventions/frontend.md:39-42` and design.md's own header
(lines 20-21) name design.md as the catalogue a module's design fills.

A mod-002 developer who follows § 3.4 for «Общие» passes `[{ label:
'Настройки' }, { label: 'Организация' }]`. The trail then reads «Настройки /
Организация / Настройки / Организация». Acting on the sentence produces a
visible defect, so this is major, besides being a sentence its own diff
contradicts.

Fix: amend § 3.4 so that it states the implemented mechanism. The shell
derives the part the navigation model knows, and a page passes only
ancestors below its own entry's page. That is a note on the mechanism; the
trail table, which gate-2 approved, does not change. Add a matching clause
to the `PageHeader` bullet in frontend.md:63-65. Optional, in the same
edit: point § 4.3/§ 4.4's `NavLink` (design.md:478, :503) and § 4.1's
"whether its page exists in the build" at plan § 9 D-6 and D-19. Following
those does not produce a defect today, because modules register entries
and do not render them.

Status: open

### F-3 (minor) — a main navigation entry without an icon disappears silently

`src/frontend/src/app/shell/MainNavigation.tsx:73` returns `null` for an
entry with no `icon`, and `types.ts:17` makes `icon` optional on every
`NavigationEntry`. A later line that registers «Рабочие элементы» without
an icon type-checks and passes its visibility tests (`visibleModel` keeps
it), but the entry is never rendered. The work group's divider is drawn
over an empty list (`MainNavigation.tsx:155-164`).

Fix: give `top` and `work` a type whose `icon` is required (a
`MainNavigationEntry` beside a settings entry without one), or render a
fallback icon. Either way the compiler or the screen says so.

Status: open

### F-4 (minor) — comments added in Russian

AGENTS.md (Language): code and comments are English. This diff adds Russian
comments in `src/frontend/src/features/dictionaries/paths.ts:1-2` (a new
file), rewrites `features/auth/AnonymousScreen.tsx:14-16` in Russian, and
appends a Russian sentence to `features/home/HomePage.tsx:9`. The comments
it leaves untouched are not in scope.

Fix: write the added and rewritten comment text in English. The quoted UI
copy stays in Russian.

Status: open

### F-5 (minor) — e2e 14's header names a fallback that does not exist

`src/frontend/e2e/specs/14-header-name.spec.ts:5-6`, a docblock this diff
rewrote, keeps "(`name` → `preferred_username` → `"anonymous"`)". The
actual fallback is the empty string (`src/shared/api/tokenClaims.ts:52`).
With an empty name, `UserMenu` names the button «Профиль» with no colon
(`UserMenu.tsx:52`). The spec's own locator `/^Профиль: /` then fails,
which is the real protection. `not.toBe('anonymous')` (line 22) guards a
value that cannot occur.

Fix: say "→ empty string", and that an empty name fails the `«Профиль: »`
locator. Drop or keep the `anonymous` assertion, but do not present it as
the guard.

Status: open

### F-6 (minor) — two 1 px border literals outside the theme (BR-1, dimensions)

`src/frontend/src/app/AppLayout.tsx:51` (`` `1px solid ${divider}` ``) and
`:196` (`borderLeft: 1`) draw the settings column's divider with a literal
width. BR-1 has no exception list for dimensions, and frontend.md:56-57
leaves them to review. They are the only dimension literals in product code
outside `app/theme`.

Fix: move the rule into the theme. For example, add a navigation-column
class styled in `components.ts`, as `navigationEntryClass` already is, or
add a `layout` token for the divider width.

Status: open

### F-7 (minor) — ADR bookkeeping

- `docs/adr/0024-app-shell-and-visual-language.md:3` reads `Status:
  accepted` with no note on who accepted it, or when.
  `docs/adr/README.md:33-35` asks the agent to record an acceptance "with a
  note". ADR-0007 and ADR-0019 carry theirs. The acceptance itself is
  recorded in `status.md` D-24.
- `docs/adr/0007-frontend-ui-stack-and-screen-state-canon.md:3-7` still
  lists only ADR-0019 as narrowing it. ADR-0024 amends its canon: it adds
  not found, and the canon's frame is now `shared/ui/StateFrame`. The
  precedent ADR-0019 set is a note on that status line.
- `design.md:15-16` still calls ADR-0024 "proposed by this task".

Fix: on ADR-0024's status line, add "(delegated by the human 2026-10-01,
recorded 2026-10-02, mod-015 D-24)". On ADR-0007's status line, add "and
amended by ADR-0024 (not found added; one shared frame)". In design.md,
change "proposed" to "accepted".

Status: open

### F-8 (minor) — Firefox and Edge support is stated, Chromium alone is run

`spec.md:150-153` (NFR "Browsers and widths"): "current desktop Chrome, Edge
and Firefox; fully supported from 1024 px wide". The only Playwright project
is `chromium` (`src/frontend/playwright.config.ts:42-46`), and QA's browser
evidence is Chromium only. Edge shares Chromium's engine, but Firefox is
asserted and not checked.

Fix: run spec 19 once under Playwright's `firefox` device, without
committing a new project if that is unwanted, and record the run in qa.md.
Alternatively, mark the Firefox clause as unverified in this task, in the
spec or as a gate-3 "deliberately left open" line.

Status: open

Developer (2026-10-04): took the alternative. Playwright's Firefox build is
not installed on this machine, and spec 19 needs a stand; a frontend-stage
run of both for one minor did not seem worth the stand lock. The clause is
recorded as unverified in `plan-frontend.md` § 14, worded for the gate-3
summary and the MR's «Deliberately left open». spec.md is left as it is: it
is gate-1's text, not a verification record. If the human wants the run,
it is spec 19 under `devices['Desktop Firefox']` against a cold stand, and
QA records it in qa.md.

### F-9 (minor) — one branch commit does not build

`baa1303` ("wip … interrupted dev pass 1, unverified") contains a
`PageHeader` that fails `tsc`, per the 2026-10-02 `[dev]` Log line, and
5dd37c3 fixed it. `docs/conventions/git.md:63-64` allows "wip" commits "but
every commit must build". Nothing can be done without rewriting history,
which this stage does not do, and the squash removes the commit from
`main`. The branch history is kept on the server.

Fix: none in code. Carry the deviation into the gate-3 summary, next to
QA's § 7 one.

Status: open (record only)

### Weighed on request

- **D-25 / plan D-8, the Vitest alias of `@testing-library/react`.** It was
  weighed and is accepted. The alias is exact (`vite.config.ts:53-58`
  matches the bare specifier only). The wrapper composes with a caller's
  `wrapper` and themes `renderHook` as well. Its premise about the package
  index holds: `@testing-library/react/dist/index.js:23-44` registers
  cleanup only when runner globals exist, and `setup.ts:27-28` cleans up by
  hand. It is documented where the next agent looks (frontend.md:119-122).
  The failure mode is loud: a test that bypasses it renders under MUI's
  default theme and throws on `theme.layout`.

### Not re-raised (QA's)

- **QA D-4 (minor, open).** A letter-case variant or a percent-encoded
  address renders the page without its navigation. I concur on the
  severity and the cause: `entryMarker` compares strings, while the router
  matches case-insensitively and after decoding. QA's proposed fix, asking
  the router through `matchPath` with the landing as the pattern, is the
  right shape. It would also absorb the trailing-slash special case at
  `visibility.ts:92`.
- **QA § 7.** No E2E run on the fixed code has started against an API that
  was not ready yet (both R1 and R2 stands report `warm`). QA carries this
  to gate-3.

### Behavioural claims in prose

Direction 1, prose the diff adds or changes. Each claim is followed by its
anchor, or by the finding it became.

- spec BR-1, colour literals machine-checked → `colourLiterals.test.ts:61-68`, `:105-116` ✓; dimensions by review → F-6
- spec BR-2, top bar on the content surface, secondary labels neutral → `components.ts:37-56`, `:185-194`; `theme.test.tsx:85-111` ✓
- spec BR-3, 14 / 48 / ≤ 40 px → `tokens.ts:115`, `:144`; `theme.ts:74`; `theme.test.tsx:39-65` ✓ (browser: QA 19.1)
- spec BR-4, lookups take a token or code only; unknown → fallback → `workTracking.ts:62-71`; `workTracking.test.ts` ✓
- spec BR-5, contrast → `contrast.test.ts:43-84` ✓
- spec BR-6, one `h1`, tab title, trail of ancestors only → `PageHeader.tsx:84`, `:106-113`; `documentTitle.ts:5-7`; `visibility.ts:141`; `routes.test.tsx:62-88` ✓
- spec BR-7, shown only when built and permitted, nothing disabled → `model.ts:30`; `visibility.ts:46` ✓
- spec BR-8, one check, interim clause, re-evaluated without reload → `AppLayout.tsx:85-94`; `AppLayout.navigation.test.tsx:142-209`, `:307-320` ✓
- spec BR-10, layout rules, kept choice per browser, focus after the overlay → `useNavigationLayout.ts:15-30`; `keptChoice.ts:5`; `AppLayout.tsx:133-151` ✓
- spec BR-11, no request, nothing kept, «Все организации» hidden → `placeholderContext.ts:67`; `ContextSwitcher.tsx:218-254`; `ContextSwitcher.test.tsx:121-150` ✓
- spec BR-12/BR-13, one frame, not found by level, no redirect → `StateFrame.tsx:46-145`; `routes.tsx:29` ✓
- spec BR-16/BR-17 and US-11, no name, no UI dependency, no `deploy/` or mod-001 change → name search (nothing); `git diff --stat main...HEAD -- deploy src/backend src/frontend/package*.json` (empty) ✓
- spec BR-19, settings navigation iff a settings page with a visible entry; not found is no page → `AppLayout.tsx:98-107`; `visibility.ts:124` ✓
- spec NFR Network, the shell sends nothing of its own → `AppLayout.context.test.tsx:31-55`; `ContextSwitcher.test.tsx:121-150` ✓ (browser: QA 19.1)
- spec NFR Browsers, Chrome, Edge and Firefox → F-8
- US-8/US-11, only the named tests edited → `git diff --stat -M main...HEAD -- '*.test.*'` ✓
- ADR-0024:99-109, the frame and navigation rules → `TopBar.tsx:32-35`; `visibility.ts:46`; `useNavigationLayout.ts:15-30` ✓
- ADR-0024:114-115, the check rejects literals outside `app/theme` → F-1
- ADR-0024:150-152, slices never import `app/` → a grep of `features/` finds `app/` imports in tests only ✓
- ADR-0024:178-183, frontend.md rewritten, data-grid / forms / pickers lines dropped → `frontend.md:35-95` ✓
- ADR-0024:3, `accepted` → `status.md` D-24; the note is missing → F-7
- frontend.md:46-52, every signed-in page in `AppLayout`, entries only for built pages → `routes.tsx:16-30`; `model.ts:20-48` ✓
- frontend.md:55-56, colour mechanism → F-1; :56-57, dimensions by review → F-6
- frontend.md:78-81, one check (`can` / `useAccessCheck`) → `RequirePermission.tsx` (`can`); `AppLayout.tsx:85-88` ✓
- frontend.md:88, `@mui/x-data-grid` not in the stack → `package.json:17-28` ✓; :93, dates via `dayjs` in `format.ts` → `shared/lib/format.ts:1`, `:19` ✓
- frontend.md:119-122, alias and viewport stub → `vite.config.ts:53-58`; `setup.ts:11`; `viewport.ts:17` ✓
- design.md:370, pages pass their ancestors → F-2; design.md:15-16, ADR-0024 "proposed" → F-7
- `tokens.ts:4-5`, the only file with colour literals; components never import it → F-1
- `theme.ts:89-95`, MUI focus ring on every control; transitions honour reduced motion → MUI 9.4 `focusVisible`, `motion` (`createThemeNoVars.d.ts:26-60`); `NavigationOverlay.tsx:45`; `AppLayout.tsx:29-34` ✓ (browser: QA 19.5, 19.6)
- `NavigationOverlay.tsx:22-28`, at the modal level over the top bar; the backdrop covers the bar → `NavigationOverlay.tsx:44`; `TopBar.tsx:35`; QA 19.7a R2 ✓
- `AppLayout.tsx:130-131`, `:138-140`, the overlay closes on a layout change; focus goes to main after a choice → `AppLayout.tsx:133-151` ✓
- `PageHeader.tsx:121-127`, the bar keeps its height; `ResizeObserver` overflow; inline in jsdom → `PageHeader.tsx:146`, `:191`; `PageHeader.test.tsx` ✓
- `ConfirmDialog.tsx` / `FormDialog.tsx` docblocks, busy ignores Escape and outside clicks; the paper is the form → `ConfirmDialog.tsx:49`; `FormDialog.tsx:48`, `:53` ✓
- `UserMenu.tsx:15-18`, focus skips the name row → `UserMenu.tsx:30`; QA 19.5 ✓
- `ContextSwitcher.tsx:64-69`, Enter / Space / ArrowDown open on the current entry; Escape returns → `ContextSwitcher.tsx:105-110`, `:132`; `AppLayout.keyboard.test.tsx:127-173` ✓
- `SkipLink.tsx` docblock, no `#main-content` in the address → `SkipLink.tsx:14`; QA 19.5 ✓
- `testingLibrary.tsx:14-17`, the index adds only cleanup and act hooks under globals → `@testing-library/react/dist/index.js:23-44`; `setup.ts:27-28` ✓
- `features/dictionaries/index.ts:1-5`, `app/` imports the slice through the barrel only → `model.ts:2`; `routes.tsx:4` ✓
- `pageChrome.ts` docblock, a full-page frame registers no header, so there is no trail → `AppLayout.tsx:116-120`; `StateFrame.tsx` mounts no `PageHeader` ✓
- e2e 14:5-6, the fallback is "anonymous" → F-5; e2e 19 header, the session flow is untouched → `e2eSessionGuard.test.ts:49`, green in this round ✓
- status Log 2026-10-02 `[dev]`, "all others byte-identical" → as US-8/US-11 above ✓; 2026-10-04 `[dev]`, "56 files / 539 tests" → this round: 56 / 540 (QA A21 came later) ✓; baa1303's failing `tsc` → F-9

Direction 2, behaviour the diff changes, the identifier grepped and what the
hits were:

- An unknown address no longer redirects to «Главная». Grepped `redirect`,
  `unknown address`, `path="*"` and `Navigate to="/home"` over `docs/`,
  `.claude/`, the `AGENTS.md` files and `e2e/`. Hits only in this feature's
  docs, `routes.tsx` and spec 19. None stale.
- «Выйти» and the display name moved into the user menu. Grepped `Выйти`,
  `app bar`, `top bar`, `AppBar` and `drawer` (same tree). ADR-0022:211
  still holds. e2e 04, 05 and 14 were rewritten, and 14's stale fallback is
  F-5.
- `AccessDenied`, `RegistrySkeleton` and `FormSkeleton` moved to `shared/ui`;
  `ConfirmDialog` lost its fixed error colour; the inline `createTheme` left
  `App.tsx`. Grepped those identifiers in `docs/**/*.md`, `AGENTS.md` and
  `.claude/`. ADR-0007:78 (locales in one `createTheme`) still holds.
  ADR-0019:22 is a dated history of the old `shared/ui`. frontend.md was
  updated. None stale.
- The screen-state canon gains not found and one shared frame. ADR-0007's
  canon paragraph and status line do not record the amendment → F-7.
- Page titles moved from `h2` to `h1` and the tab title changed. Grepped
  `heading` with «Языки» or «Главная» in `e2e/`. Specs 09, 10 and 16 hold,
  confirmed by QA R2 at 32/32.
- Component tests render under the theme (alias), and `matchMedia` is
  stubbed. Grepped `testing-library/react`, `renderWithProviders` and
  `matchMedia` in `docs/`, `.claude/` and the `AGENTS.md` files. The only
  hit is frontend.md:119-122, which this diff added.
- Not swept: `prototype/index.html`; `design.md` §§ 8–12 and 14–15; QA's run
  records in `qa.md` (they need a stand); spec 19 beyond its header and the
  session guard.

## Round 2 — frontend (code review, 2026-10-04)

Stage 8, round 2, a delta over round 1. Scope: F-1..F-9 and the fix diff
`3be1663..8563850` (03dcaff, 6a318c2, 7fbbe27, a852d60, 8563850; 21 files,
+210/−58). The session wrote none of it and did not run round 1.

Run in this worktree (`npm ci` first): `npx vitest run` — 56 files, 544
tests, green; `npm run lint` — green; `tsc -b` — green; `vite build` — green
with a dummy `VITE_API_BASE_URL`. F-3's guard was tried by mutation: with
`icon` made optional on `MainNavigationEntry`, `tsc -b` fails with TS2578
(unused `@ts-expect-error`) at `visibility.test.ts:91`, and with TS2322 at
`MainNavigation.tsx:80`. The edit was reverted and the tree is clean. No
stand was started. e2e 14's edit (one assertion dropped, the docblock)
type-checks under `tsc -b` but was not run. The fix diff and its commit
messages were searched case-insensitively for the reference tracker's
product and vendor names and abbreviations: nothing.

Verdict: **approve** — 0 blockers, 0 majors open. F-1..F-7 are resolved.
F-8 is resolved by one of the two alternatives round 1 offered. F-9 stays a
record for gate-3. One new minor, F-10, sits in a test comment the fix
added. It does not hold the gate.

### Round 1 findings

| Finding | Status | Evidence |
|---|---|---|
| F-1 (major) colour-check scope | resolved | `tokens.ts:4-10` states the scan as `colourLiterals.test.ts:61-68` implements it. No other product file under `app/theme/` holds a colour literal (grep of `theme.ts`, `components.ts`, `contrast.ts`, `workTracking.ts`, `classNames.ts`), so "the only product file" holds. "Outside `app/theme/` only types are imported" holds: `useWidthClass.ts:3` and `useNavigationLayout.ts:2` are `import type`, and the barrel's importers outside the folder take `theme` only. `frontend.md:55-57` takes round 1's wording (see Notes). `ADR-0024:115-118` adds the exception as a clarification, which `adr/README.md:36-37` permits. |
| F-2 (major) trail mechanism | resolved | `design.md:373-383` and `frontend.md:66-69` state what `visibility.ts:147-170` and `AppLayout.tsx:121-125` do: on a settings page «Настройки / <section>» as plain text, beneath a landing the entry as a link, then the page's own `ancestors`. "None of this task's pages has such an ancestor" holds: no product file passes `ancestors`, only `PageHeader.test.tsx:285`, `:293`. The empty trail hides the divider at `TopBar.tsx:60`. § 4.3 and § 4.4 now point at plan D-6, which matches `MainNavigation.tsx:39-42` and `SettingsNavigation.tsx:65-68`. § 4.1 points at D-19, which matches `model.ts:20-48`. The gate-2 trail table is unchanged. |
| F-3 (minor) silent icon drop | resolved | `types.ts:32-34`, `:46-48`; the `!entry.icon` drop is gone (`MainNavigation.tsx:72-75`). The compile-time guard `visibility.test.ts:82-98` was proven by the mutation above. `build` is `tsc -b && vite build` (`package.json:8`), the app project includes `src` (`tsconfig.app.json:29`), and `verify.sh:471` runs `build`. |
| F-4 (minor) Russian comments | resolved | `paths.ts:1-3`, `AnonymousScreen.tsx:14-16` and `HomePage.tsx:6-11` are in English, and so is `FormSkeleton.tsx`. A scan of every comment line the branch adds under `src/frontend` for Cyrillic outside quoted copy finds one line, which was already on `main` (Notes). |
| F-5 (minor) e2e 14 fallback | resolved | The docblock at `14-header-name.spec.ts:4-15` matches `tokenClaims.ts:52` (empty string) and `UserMenu.tsx:52` («Профиль», no colon). The dead `anonymous` assertion is gone. Plan § 12 (`plan-frontend.md:709-715`) says the same. |
| F-6 (minor) 1 px literals | resolved | `layout.dividerWidth` (`tokens.ts:115-119`, `:139`) is used at `AppLayout.tsx:37-39`, `:56`, `:201` and `components.ts:192`. It renders the same 1 px `divider` rule, so nothing on screen moved. The test added with it is F-10. |
| F-7 (minor) ADR bookkeeping | resolved | `ADR-0024:3-4` matches `status.md` D-24. `ADR-0007:3-11` records the amendment, matching `ADR-0024:10-12` and `:163-170`. The index line `adr/README.md:62` and `design.md:15-17` agree. |
| F-8 (minor) Firefox | resolved (alternative) | Recorded as unverified at `plan-frontend.md:762-769`, for the gate-3 summary and the MR's «Deliberately left open», one of round 1's two alternatives. The developer's premise holds: the local Playwright browser cache holds Chromium builds only. The orchestrator carries the line to gate-3. |
| F-9 (minor) baa1303 does not build | open, record only | Nothing to do on the branch. Carry it into the gate-3 summary next to QA § 7. |

### F-10 (minor) — the F-6 test cannot see the literal it is labelled against

`src/frontend/src/app/AppLayout.widths.test.tsx:208-210`. The comment says
"the rule's width is the theme's `layout.dividerWidth`, not a literal at the
call site (BR-1)", and the describe reads "a divider rule from the theme".
The assertion (`:214`) compares the computed width with
`${theme.layout.dividerWidth}px`, which is `1px`. If `1px solid` went back
in at either call site, the test would still pass. It pins the rendered rule
(width, style and colour, in both placements), which is worth having. It
does not guard F-6's regression, and BR-1 leaves dimension literals to
review on purpose.

Fix: make the comment say what the test pins. For example: "Code review
F-6: the settings column's rule in both placements, as the theme defines it.
Whether the call site reads the token or a literal is review's job: BR-1
does not machine-check dimensions." No code change.

Status: open (non-blocking)

### Notes (not findings)

- `frontend.md:55-57` names the exceptions as `*.test.*` and `src/test/`
  but does not say the scan covers only `src/`. e2e spec 19 holds expected
  colours (`e2e/specs/19-shell-in-a-browser.spec.ts:32-38`). This is not
  re-raised, for three reasons: a Playwright spec is a test file in the
  ordinary sense, BR-1 and `tokens.ts:5` both say `src/`, and the sentence
  is round 1's own suggested wording. Adding "under `src/`" would make it
  exact if the line is touched again.
- `shared/ui/RegistrySkeleton.tsx:17` keeps the Russian sentence it had on
  `main`, inside a docblock the diff extended with English lines. F-4
  excluded untouched comment text. The matching case in `FormSkeleton.tsx`
  was translated and this one was not. Cosmetic.
- QA re-run over what the fix touched. The runtime edits render the same
  pixels (a 1 px `divider` rule), the type change is compile-time only, and
  the rest is comments and docs. e2e 14 lost one assertion and was not
  re-run. Whether QA re-runs is the orchestrator's call.
- QA D-4 (minor, QA's) is untouched by the fix diff and stays open, as
  round 1 left it.

### Behavioural claims in prose

Direction 1, prose the fix diff adds or changes:

- `tokens.ts:4-10`: the only product file with colour literals, the scan's scope, only types imported outside the folder → `colourLiterals.test.ts:61-68`; grep of the `app/theme/` product files; `useWidthClass.ts:3`, `useNavigationLayout.ts:2` ✓
- `tokens.ts:115-118`: `dividerWidth` under the top bar and left of «Меню настроек» → `components.ts:192`; `AppLayout.tsx:37-39`, `:56`, `:201` ✓
- `types.ts:8-13`, `:27-31`: a settings entry carries no icon; a main entry without one does not compile → `types.ts:14-34`; mutation run ✓
- `visibility.test.ts:82-85`: `npm run build` runs `tsc -b` over this file, and the directive fails the build once the error goes away → `package.json:8`; `tsconfig.app.json:29`; mutation run (TS2578) ✓
- `AppLayout.widths.test.tsx:208-210`: the width is the token, not a literal → about the code, `AppLayout.tsx:37-39` ✓; about what the test checks → F-10
- `AppLayout.tsx:36`: the rule left of «Меню настроек» → `:56`, `:201` ✓
- e2e `14-header-name.spec.ts:4-15`: empty-string fallback, «Профиль» with no colon, the locator is the guard → `tokenClaims.ts:52`; `UserMenu.tsx:52`; spec `:20-21` ✓
- `AnonymousScreen.tsx:14-16`: the tab reads «Mustask» in every state → `AnonymousScreen.tsx:19`; `documentTitle.ts:5-7` ✓
- `paths.ts:1-3`: the registry is the landing page of «Настройки» → «Справочники» → «Языки», and the form lives beneath it → `model.ts:34-45` ✓
- `frontend.md:49-52`: the type requires an icon on main entries → `types.ts:32-34` ✓
- `frontend.md:55-57`: the colour mechanism and its exceptions → `colourLiterals.test.ts:61-68` ✓ (the `src/` note above)
- `frontend.md:66-69`: the shell derives the model's part of the trail, and a page passes only deeper ancestors → `visibility.ts:147-170`; `AppLayout.tsx:121-125` ✓
- `design.md:15-17`: ADR-0024 is accepted → `ADR-0024:3-4`; `status.md` D-24 ✓
- `design.md:373-383`: the trail mechanism; no page of this task passes ancestors; an empty trail hides the divider → `visibility.ts:147-170`; grep for `ancestors=`; `TopBar.tsx:60` ✓
- `design.md:397-399`: «Языки» is the link the shell derives → `visibility.ts:157-158` ✓
- `design.md:435-438`: registration is the build flag; the type requires an icon on main entries → `model.ts:20-48`; `types.ts:32-34` ✓
- `design.md:493`, `:518`: entries are built with `Link`, with `selected` and `aria-current` from the marker → `MainNavigation.tsx:39-42`; `SettingsNavigation.tsx:65-68` ✓
- `ADR-0024:3-4`: accepted, with the note → `status.md` D-24 ✓. `:115-118`: the check's exceptions → `colourLiterals.test.ts:61-68` ✓
- `ADR-0007:8-11`: amended by ADR-0024 (not found added, one frame in `shared/ui/StateFrame`) → `ADR-0024:163-170`; `shared/ui/StateFrame.tsx`; `adr/README.md:62` ✓
- `plan-frontend.md:709-715`: the e2e 14 plan → spec `:20-25` ✓. `:757-758`: D-24 recorded 2026-10-02, and pass 5's rewrite is 83fd809 → `status.md` D-24; 83fd809 touches only `docs/conventions/frontend.md` ✓. `:762-769`: `chromium` is the only project and Firefox was not run → `playwright.config.ts:44` ✓
- review.md, F-8's developer note: Firefox is not installed → the local Playwright browser cache holds `chromium-1234` and `chromium_headless_shell-1234` and no Firefox build ✓
- `status.md` Log 2026-10-04 `[dev]`: each per-finding claim → the table above. "56 files / 544 tests, lint, build" → this round's run ✓. "Proved to fail the build" → reproduced ✓

Direction 2: for each behaviour the fix diff changes, the identifiers
grepped over `docs/`, `.claude/` and the `AGENTS.md` files, and the hits:

- Main navigation entries require an icon, and settings entries carry none.
  Grepped `NavigationEntry` and `icon?`. One hit: `plan-frontend.md:78`
  lists `types.ts`'s exports without `MainNavigationEntry`. That is a file
  inventory in the stage's own plan, not a claim later lines act on, so it
  is not raised. frontend.md and design.md § 4.1 were updated in the diff.
  The other hits are round 1's text.
- The divider width became a token. Grepped `dividerWidth`, `divider width`,
  `1 px` and `1px` in this feature's docs. `plan-frontend.md:584` (D-3, the
  49 px bar) still holds with a 1 px token. None stale.
- e2e 14 no longer asserts `anonymous`. Grepped `anonymous`. Plan § 12 was
  updated in the diff. The other hits (`error-codes.md:44`, US-11's
  anonymous visitor, `src/frontend/AGENTS.md:102`, the tech-159 example) are
  about something else. None stale.
- ADR-0007's status line now records ADR-0024. Grepped
  `narrowed by ADR-0019` and `ADR-0007.*canon`. The index line was updated.
  `ADR-0019:6`, `frontend.md:36` and doc-010 `spec.md:247` agree with the
  amendment. None stale.
- Not swept: nothing. The fix diff changes these four behaviours and no
  others.
