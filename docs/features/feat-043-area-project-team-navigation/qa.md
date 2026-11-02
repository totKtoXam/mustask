# QA — Area, project and team navigation (feat-043)

## Backend

Scope: `GET /api/organizations/{o}/overview`, `GET …/selector-offers`, and the comment-only CR-3/CR-6 changes (US-2, US-8 data, US-9 CR-3/CR-6). The panel is stage 6; US-6 and US-10 are feat-048's. The plan was drawn from spec, US-2/8/9 and `api-contract.md` before the implementation was read.

### Coverage matrix

| Criterion | Check | Type | Status |
|---|---|---|---|
| US-2 counts once, active only, departments only (2/4/3, E and sub-areas excluded) | `Cast_OfUs2_CountsAndOffersMatchTheStory` (cast built by real writes: area move, link, deactivate); dev `Overview_CountsDepartmentsProjectsAndTeamsOnce…` | integration (API, Infra) | done |
| US-2 «Обзор» bounded: first 5 by name, statements constant at volume | dev `BothReads_StatementCountIsPinned_At…` (7 and 5 statements, 5 and 200 objects); QA `Overview_OrdersByRussianCollation…AndCutsToFive` | integration | done |
| US-2 team block: caller's own teams, all; none for Vera | dev `Overview_MyTeams…`; QA `EveryRoleReads_AndIsMemberIsPerCaller`, cast `myTeams=[VENUS]` | integration | done |
| US-2 «Проекты»/«Команды» rows: place, key, mark «Моя команда» | cast (departmentId/Name, null for D, isMember) | integration | done |
| US-2 empty states («Пустая», «Север») | `EmptyOrganization_…ZeroCountsAndEmptyArrays`, `OrganizationWithoutDepartments_…` | integration | done |
| US-2 BR-NV-15 caller required | `NavigationReadSignatureTests`, handler tests | unit | done |
| US-8 / BR-NV-17 offers data: place, `teamIds`, `departmentIds`, no nodes/sub-areas/deactivated/deleted | cast; `Offers_FollowAMoveAndATeamDelete_NothingStale`; `RestoredProject_ReturnsWithItsPlaceAndLinks` | integration | done |
| Reachability: other organization's rows, deactivated org | `OtherOrganizationsRows_NeverAppear`, `DeactivatedOrganization_IsNotFound` | integration | done |
| Access: 401, non-member 404, viewer 200; malformed id 404; upper-case id | dev `Reads_AreForEveryMember…`; QA `MalformedOrganizationId_…`; `OrganizationAccessInventoryTests` | integration | done |
| Route inventory 71 to 73, OpenAPI | `OrganizationAccessInventoryTests`, `TeamViewContainmentTests`, `OrganizationsOpenApiTests` (in the run below) | integration | done |
| US-9 CR-3/CR-6: comments only | diff of `fa9d068f`: two doc comments, no code or assertion change | review | done |
| US-2 "within 2 s" at 1 000 areas / 200 projects / 200 teams | statement pin at 200/200/20 only; no timing test | not covered | see D-2 |

### Additional scenarios

| # | Scenario | Result |
|---|---|---|
| 1 | Russian collation, case not deciding (Альфа, Арбуз, арбуз2, бета, Ёж, еж2, яблоко) | pass |
| 2 | Project moved to top level loses its place; deleted team gone from counts, `teamIds`, `departmentIds` | pass |
| 3 | Deactivate then restore a project keeps place and both team links | pass |
| 4 | `isMember` is per caller (viewer sees none) | pass |
| 5 | No migration in this line; the Testcontainers databases apply the existing ones from clean | n/a |

### Runs

| What | Result |
|---|---|
| Api.IntegrationTests, filter NavigationReads, OrganizationAccessInventory, TeamViewContainment, OrganizationsOpenApi | 262 pass (3 of my first-draft tests failed on my wrong expectation, 204 vs 200 of the area move; fixed in the test) |
| Api.IntegrationTests, NavigationReadsQaTests | 12 pass |
| Application.Tests, filter Navigation | 7 pass |
| Infrastructure.Tests, filter Navigation | 11 pass |
| Architecture.Tests (all) | 86 pass |

Not run: the full suite, E2E (standing answer NO).

### Defects

#### D-1: `teamIds` and `departmentIds` have no stated or enforced order

- Severity: minor
- Steps: read `selector-offers`; `NavigationReadModel` builds both from unordered link rows (`ToLookup` over a statement with no `ORDER BY`).
- Expected / Actual: the contract says lists come in the order the server gives; for these two arrays the order is whatever PostgreSQL returns, so it may vary between calls. The panel treats them as sets, so no user-visible effect is known; the contract should say "a set, no order", or the read should sort them.
- Status: open, carried to gate-3 as a debt

#### D-2: no test for the 2 s read budget at volume

- Severity: minor
- Steps: spec NFR Volumes and US-2 "page is read within 2 s"; only the statement counts are pinned (at 200 projects, 200 teams, 20 departments, not 1 000 areas).
- Expected / Actual: the constant statement count is the proven part. Timing is unmeasured, and the NFR's 1 000 areas are not seeded. Low risk (five/seven simple statements), but not demonstrated.
- Status: open, carried as a debt

#### D-3: `plan-backend.md` § 5 still says "Not run yet"

- Severity: minor (documentation)
- The tests ran green here; the line is stale. Also `status.md` still reads `stage: 04-backend` (the orchestrator's to move).
- Status: open

### Verdict

blockers: 0 · majors: 0 · minors: 3 · result: pass (backend). E2E asked by the frontend QA pass, not here; standing answer NO.

## Frontend

Scope: US-1..5 and US-7..9 (US-6 and US-10 are feat-048's). The plan was drawn from `spec.md` and the stories before the implementation was read; design, contract, ADR-0032 and `plan-frontend.md` came after. Reading of the selection, settled with the orchestrator: an address carrying an organization fully sets the selection; the remembered one applies to pages with no organization.

### Coverage matrix

| Story / criterion | Check | Type | Status |
|---|---|---|---|
| US-1 four selectors; the address names the most specific object, the others ride as `?teamId=` / `?departmentId=` | dev `selection`, `ContextSelectors`, `AppLayout.context`; QA `routes.node.qa` (capital and percent-encoded ids and letters) | unit, component, routes | done |
| US-1 / BR-NV-6 retired addresses answer «Страница не найдена», nothing redirects | QA `routes.retired.qa`: all 10 `RETIRED_ADDRESSES` plus 10 not-yet-built addresses through real `AppRoutes`: h1, address kept, no entry current; a non-member sees «Организация не найдена» | routes | done |
| US-1 no link targets a retired address | dev `addressModel.test` (no menu row matches a retired pattern); no crawl of every rendered link | unit | partial, see F-4 |
| US-2 «Обзор»: counts, first rows, «Все …» links, «Мои команды», empty states, other organizations | dev page test; QA `OrganizationOverviewPage.qa` (no team, two teams, empty, 200/1000 volumes, five organizations) | component | done |
| US-3 department / project node / sub-area resolved by `areas/:areaId`; the node page sets the project selector and the project menu | QA `routes.node.qa` (selectors, menu, only «Структура» current; unknown area) | routes | done |
| US-3 a node page lists no project teams | dev `AreaPage.test` (hint only; tabs `projects` and `teams` are NotFound for a node) | component | done |
| US-3/4/5 «Параметры» by role | QA `routes.node.qa`: member and viewer get «Доступ ограничен» for a department, a team, a project (6 cases) | routes | done |
| US-4 a backlog under a chosen team keeps its order and cannot be reordered | QA `ProjectViews.narrowed.qa` | component | fails: F-2 |
| US-7 selector fit, «Остальные», reset with notice, row link | dev `selectorFit`, `ContextSelectors`, `useObjectLink` tests (reviewed, no gap found) | unit, component | done |
| US-7 focus to `main h1` for `useObjectLink` links only | dev `ContextHost` tests | component | done |
| US-7 the second row scrolls under 1024 px | dev `AppLayout.widths` (style assertion); no real viewport | component | partial, see F-5 |
| US-8 remembered selection, fallback notice, repair of the store | dev `useNavigationSource.test`; QA `useNavigationSource.qa` | hook | fails in part: F-1 |
| US-8 settings navigation: groups by chosen objects, each with «Параметры» | QA `routes.node.qa`: the address-based case passes, the click path fails | routes | fails: F-3 |
| US-9 a section is absent until its owning line adds it; no dead link | dev `routeInventory`, `addressModel.test` | unit | done |

### Tests dropped in the commit that retired the switcher

The tests removed with `switchTarget`, `projectViews` and the switcher cases of the `AppLayout.*` files described the old three-level switcher. Each requirement that survives was found restated in the new `choose`, `selectorFit`, `useNavigationSource`, `routeInventory` and `AppLayout.*` tests. The old TC-4 and gate-3 node-team tests are replaced by the `AreaPage` hint test and the project «Команды» page tests. No requirement was found without a test, apart from the defects below.

### Tests added

Five files named `*.qa.test.tsx`, 52 tests: 49 pass, 3 are `it.fails`, one per defect F-1..F-3. They turn red when the defect is fixed, and the developer then drops `.fails`.

- `src/frontend/src/app/routes.retired.qa.test.tsx` (30)
- `src/frontend/src/app/routes.node.qa.test.tsx` (15, one `it.fails`)
- `src/frontend/src/app/navigation/useNavigationSource.qa.test.tsx` (2, one `it.fails`)
- `src/frontend/src/features/organizations/pages/OrganizationOverviewPage.qa.test.tsx` (4)
- `src/frontend/src/features/boards/pages/ProjectViews.narrowed.qa.test.tsx` (1, `it.fails`)

### Runs

| What | Result |
|---|---|
| The five files above | 5 files, 52 tests, 0 red |
| ESLint and Prettier on them | clean |
| Full Vitest | not run (shared lane; the last full run was 334 files, 5116 passed, on `912cb4fd`) |
| E2E | not run, standing answer NO; the old specs are knowingly broken by the address changes |

### Defects

#### F-3: «Настройки» clears the selection, and the object «Параметры» are unreachable from the menu

- Severity: blocker
- Steps: open `/organizations/<o>/projects/<p>/general?teamId=<t>`; the selectors read «Команда: Платформа», «Проект: Пример проекта». Press «Настройки» in «Основное меню».
- Expected: the settings navigation holds the groups «Проект», «Команда», «Организация», each with «Параметры» (ADR-0032 § 5), and the selectors are unchanged.
- Actual: «Настройки» lands on the organization's «Общие», an address that names no object; the selection is cleared, the selectors go to «все» and the project and team groups are gone. The only way to a project's, team's or department's «Параметры» is to type the address. Confirmed by logging the location, the selectors and `localStorage` after the click.
- Test: `routes.node.qa.test.tsx`, `it.fails` "choosing «Настройки» with a project and a team chosen keeps them and their groups". The address-based case passes, so the groups themselves are right.

#### F-1: an address that names a gone object overwrites the remembered selection

- Severity: major
- Steps: the remembered selection for the organization is `{team: MARS}`; open `/organizations/<o>/projects/<gone>/general`.
- Expected: nothing is written down for an object that does not exist; the remembered team stays and no notice appears.
- Actual: `useNavigationSource` writes the address's selection whenever the address carries an organization and the offers have arrived; the remembered `{team: MARS}` is replaced, so one bad link changes the user's context for every later visit.
- Test: `useNavigationSource.qa.test.tsx`, `it.fails` "names another object than the remembered one…". The case where the address names the remembered gone object itself passes (drops to «все», notice, store repaired).

#### F-2: a backlog under a chosen team has no hint and no «сужено» tag

- Severity: major
- Steps: open the project's backlog with `?teamId=<t>` (US-4, Q-9 A, design §§ 4, 7.1).
- Expected: «Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.», the tag «сужено: команда Платформа», and no reordering offered.
- Actual: neither text exists, and the row menu still offers the move.
- Test: `ProjectViews.narrowed.qa.test.tsx`, `it.fails`.

#### F-4: no check walks every rendered link for a retired target

- Severity: minor
- US-2's last scenario ("no link targets a retired address") is held by the unit test over the menu table and by the route inventory, but no test renders the pages and scans their links (page bodies, «Обзор» rows, row links). A page that builds a link by hand could still point at a retired address.
- Status: open, carried as a debt

#### F-5: the 1024 px second row is checked by style only

- Severity: minor
- `overflowX: 'auto'` is asserted; there is no real viewport (jsdom does not lay out), so wrapping is not excluded in a browser.
- Status: open, needs a human look at 640 and 1023 px

### What was not covered

- No visual check of the header at 640 px and below 1024 px.
- No real browser reload or Back/Forward (focus after a reload, the remembered selection after a real reload); the tests use `MemoryRouter` and `localStorage`.
- No full Vitest run (shared lane); no E2E (standing NO).
- The keyboard walk through the selectors is the developer's `AppLayout.keyboard` tests; not repeated.

### Verdict

blockers: 1 · majors: 2 · minors: 2 · result: not ready, returned to the frontend developer (F-3 first, then F-1 and F-2). The three `it.fails` tests are the acceptance for the fixes.

### Round 2 (commits `a8149357..8cc7bf74`)

Checked against `spec.md` (BR-NV-17, Q-9 A, US-4), `design.md` §§ 4, 7.1 and ADR-0032 §§ 1, 5, 6, with the new `projectId` query.

| Defect | Result | Evidence |
|---|---|---|
| F-3 «Настройки» clears the selection | fixed | the settings landings (`Общие`, `Процесс`) carry `teamId`, `departmentId`, `projectId`; the `it.fails` is a plain test and passes; QA probe: «Процесс» inside the settings navigation keeps the query and the four selectors |
| F-1 a gone object overwrites the remembered selection | fixed | the address that names another gone object writes nothing; the same-gone case still notices and repairs; the `it.fails` is a plain test and passes |
| F-2 narrowed backlog without hint, tag; move offered | fixed in part | hint and tag shown, «В начало»/«Ниже» absent, no drop line, no `/reorder` request, «все» gives the move back; the grip is still drawn (F-8), the tag for `none` reads wrongly (F-6) |
| F-4 no scan of rendered links | fixed | `routes.links.test.tsx`: 17 pages through real routes, none links to a retired address; it scans `a[href]` only, so a row that navigates by `onClick` is not seen |

`ProjectBacklogPage.reorderFiltered.test.tsx`: the mod-017 US-5 test (reorder against the full list under a filter) is replaced by the Q-9 A scenario, which is what Q-9 A requires: hint, tag, no menu move, no drop line, no write, order intact, «Очистить фильтры» restores the move. What the old test proved no longer happens in the panel (a filtered reorder and its request shape); the contract read of that case stays in the backend tests. The «команда удалена» test in the same file is untouched. No other coverage is lost; the drag handle is not asserted (F-8).

#### New defects (all minor)

- **F-6** The tag for the no-team filter alone reads «сужено: команда без команды» (`narrowedTag(['none'], [])`; the mixed case «команда Платформа, без команды» is fine). Test: `ProjectViews.narrowed.qa.test.tsx`, `it.fails`.
- **F-7** ADR-0032 § 1 says `projectId` is read on the organization's settings pages only. `readAddressSelection` reads it on every address with no object prefix: `…/overview?projectId=<p>` selects the project. Harm today: none a user reaches (nothing links there). Risk: `projectId` is also the name of feat-040's project filter (`PROJECT_FILTER_PARAM`); an organization-level list in feat-048 that filters by it would change the selection. Fix: read it under `/settings/` only, or state the wider rule in the ADR. Test: `routes.settings.qa.test.tsx`, `it.fails`.
- **F-8** Design § 7.1 and US-4: «no drag handle» on a narrowed backlog. For a user with the link code the rows stay draggable (a drop onto a row is a parent change) and the grip is drawn, so the handle suggests a reorder that is not there. Test: `ProjectViews.narrowed.qa.test.tsx`, `it.fails`. Not a reorder: no drop line, no request.

Checked and found right: a gone project in the settings query drops to «все» quietly, as a gone team does; the project backlog is not narrowed by `?projectId=` or `?departmentId=`; a team's backlog is not narrowed (Q-9 A, "in this line: a project's backlog under a chosen team").

#### Tests and runs

- Added: `src/frontend/src/app/routes.settings.qa.test.tsx` (3: 2 pass, 1 `it.fails` F-7); four tests in `ProjectViews.narrowed.qa.test.tsx` (3 pass, `it.fails` F-8 and F-6). With the three `it.fails` of round 1 turned into plain tests, the `.qa` files hold 3 `it.fails`, one per open defect F-6..F-8.
- Fixed in my predecessor's file: `OrganizationOverviewPage.qa.test.tsx` took `unknown` where msw wants `JsonBodyType`. `npx tsc -b` is clean; ESLint and Prettier clean on the touched files.
- Vitest, affected files only: the navigation folder, `routes.node.qa`, `routes.links`, `routes.retired.qa`, `routeInventory`, `OrganizationOverviewPage.qa`, the boards `lib` folder and the two backlog files (25 files, 416 pass); then all of `src/features/boards` plus four `AppLayout.*` files (65 files, 829 pass). Not run: the full suite (shared lane), E2E (standing NO).

#### Not covered

- No browser: the narrowed tag and hint placement, the grip, the second selector row at 640 and 1023 px (F-5) are unseen.
- A settings link built by hand elsewhere than the shell (a page body linking to `settings/…`) is not walked; the F-4 scan checks targets against the retired list, not that they carry the selection.
- «Команда → Параметры» from a project's «Параметры» drops the project (ADR-0032 § 5, left as is by the orchestrator); not probed further.
- A reparent by drop onto a row of a narrowed backlog still works; no check that the server keeps the rank rule for it.

#### Round 2 verdict

blockers: 0 · majors: 0 · minors: 5 (F-5 open from round 1; F-6, F-7, F-8 new; D-1..D-3 are the backend's) · result: pass (frontend), F-1..F-4 verified fixed. F-6..F-8 are small and may go to gate-3 as debts, or be fixed now at the human's choice; F-7 is worth fixing before feat-048 uses `projectId`.
