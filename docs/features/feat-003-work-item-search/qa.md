# QA — Work item list filters (feat-003)

One QA pass over both layers (M with UI, `05-frontend.md` § 7), clean context: the plan below was written from `spec.md`, the stories, `api-contract.md` and `design.md` before any code under `src/` was read. Branch `feat/003-work-item-search`, started at `48a68f80`. **E2E: standing NO** (paused 2026-10-12, `status.md` Decisions): no Playwright spec was written, edited or run.

Short names. Backend: **SF/AF/KF** = `WorkItem{State,Assignee,Keyword}…EndpointTests`, **ND** = `…FilterNonDisclosureTests`, **VOL** = `…FilterVolumeTests`, **SC** = `FilterStatementCountTests`, **MX** = `DeletedItemSurfaceMatrixTests` + `DeletedStateParityTests`, **DK** = the domain `WorkItemKeywordTests`/`WorkItemFilterTests`, **QB** = `WorkItemFilterQaTests` (mine). Frontend: **PF/PA/PK** = `WorkItemsPage.{filters,assignee,keyword}.test`, **LD** = `WorkItemFormPage.landing.test`, **KW** = `KeywordFilter.test`, **HK** = `useWorkItemFilter.test`, **LIB** = `lib/*.test`, **PQ** = `WorkItemsPage.qa.test` (mine).

## Backend

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|---|---|---|---|
| US-1 options «Статус», order, once | process read lists «Удалена» on every type; options are its names | integration `GetProcessDefinitionEndpointTests`, PF | ✔ |
| US-1 one state, spans types, any chosen, order kept; held state of a deleted item does not count | `state=` single/many/cross-type | SF (1–4) | ✔ |
| US-1 «Удалена» alone / beside / with team; bin of this project only; all-deleted project | `state=Удалена` | SF, MX, QB | ✔ |
| US-1 restore leaves the bin; purge leaves no trace; each role reaches the bin | restore/purge then list; three roles | SF | ✔ |
| US-1 name that names nothing (`Готово`, `новая`, `Новая `, `%`, NUL, 5 000 chars) | 200, empty, beside a valid name adds nothing | SF, ND | ✔ |
| US-2 options: who, order (name, then id; unknown last), marks, `[]`, only this project, `state` decides the bin | `assignee-options` | AF (13 + 14) | ✔ |
| US-2 match: person, former member, disabled, `none`, any, with `Удалена`, Zoya case, combined | `assigneeId=` | AF | ✔ |
| US-2 values that name nobody (malformed, unknown, Oleg, Ann, `NONE`, upper-case id): same status/body | ND, AF | ✔ |
| US-2 containment: non-member, foreign/deactivated/unknown project, reader | options + list | AF, SF | ✔ |
| US-3 title (case, «ё» both ways, one piece of text), identifier (any case), number (whole, once), parts/other keys/other projects, `%_\` literal, trim, 255/256, NUL/line breaks, description not searched, first value, deleted only under «Удалена» | `search=` | KF (21), DK (all rows of the outline) | ✔ |
| BR-WS-2 all four filters, each clear widens | list | KF, AF, QB | ✔ |
| BR-WS-12 count/paging: 50 a page, page past last, one item once across the UNION | list | SF, KF | ✔ |
| BR-WS-15 / ND: other organization or project never reached | list, options | KF, AF, ND | ✔ |
| OQ-2 A: row `assignee.isActive`; card unchanged | list vs card shape | `GetWorkItemsEndpointTests`, AF | ✔ |
| NFR speed (100 000 / 5 000 / 500 / 2 000) | each dimension, all four, options | VOL | ✔ run in `verify.sh` |
| NFR query shape (list 7 statements, options 4 + ⌈people/500⌉) | statement counts for 50 and 5 000 items | SC | ✔ |
| ADR-0030 rows for the new reads | surface matrix + parity | MX | ✔ |
| 59 → 60 scoped routes, OpenAPI operation, `workitems.workitem.read` | inventory | `OrganizationAccessInventoryTests`, `WorkItemsOpenApiTests` | ✔ |
| Migrations | none in the diff (no table, column or index) | n/a | ✔ nothing to apply |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| B-1 | all four with «Удалена»; each clear widens | US-5 alone; clears as the intersection says | ✔ QB |
| B-2 | `teamId=none` and `assigneeId=none` beside states | two different `none`s; bin has neither | ✔ QB |
| B-3 | number and identifier narrowed by the chosen states | `5` finds US-5 only under «Удалена» | ✔ QB |
| B-4 | options ignore `search`, `assigneeId`, `teamId`, unknown parameters | byte-equal answer | ✔ QB |
| B-5 | both reads without a token | 401 | ✔ QB |
| B-6 | 120 `assigneeId` values in one request line | 200, correct rows | ✔ QB |
| B-7 | surrogate pair in a title and keyword | found, no error | ✔ QB |
| B-8 | invalid UTF-8 in `search`/`state`/`assigneeId` | 200, empty | ✔ QB |
| B-9 | «ё» as «е» + U+0308 (decomposed) | finds «ёлка» | ✘ D-B1 (test skipped with the id) |
| B-10 | concurrency: a restore/purge between two reads | no push; next read is right | ✔ SF (restore, purge), by design |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-10-19 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter FullyQualifiedName~WorkItemFilterQaTests` | 12 tests: 11 passed, 1 failed (B-9) → skipped with D-B1 |
| 2026-10-19 | `BASE=5de279f7 bash scripts/verify.sh` (once; whole branch, both layers) | backend: Shared 53, Architecture 86, Application 779, Contracts 248, Domain 801, Infrastructure 421, Api.IntegrationTests 2 998 passed, 0 failed, 18 skipped (the live-Keycloak switches and my one `Skip`); frontend and docs: see Frontend runs |

### Defects

#### D-B1: A decomposed «ё» is not folded to «е» in the keyword

- Severity: minor
- Steps: title «Новогодняя ёлка»; `GET …/workitems?search=е%CC%88лка` («е» + U+0308).
- Expected / Actual: BR-WS-9 reads «ё» equal to «е», and the decomposed letter is «ё» too; the item is listed. Actual: nothing, since the fold replaces only U+0451, and the title and keyword are compared as code points. Typed text is precomposed, so only pasted text meets it.
- Fix: normalise the keyword to NFC before the fold (`WorkItemKeyword.Of`); the titles are stored as sent, so a title typed decomposed stays out of reach, which needs the same fold on the column and is the developer's call.
- Status: open (test `Search_ADecomposedYo_IsStillYo` is `Skip`ped with this id).

### Verdict (Backend)

blockers: 0 · majors: 0 · minors: 1 · result: pass

## Frontend

### Coverage matrix

The page suites run on an MSW copy of the server (`testApi.ts`), whose meaning I compared with the contract (state by name, `none`, the keyword fold, the options' rule): no gap. Parity with the real server rests on the backend rows, which use the stories' data.

| Criterion / screen (spec, design) | Check | Test type | Status |
|---|---|---|---|
| Band § 2: order «Поиск», «Статус», «Исполнитель», «Команда»; texts at rest; widths; top-aligned | PF band, widths | component | ✔ |
| US-1 «Статус»: options once in order; one/many/cross-type; written to the address | PF, LIB `stateOptions` | component, unit | ✔ |
| US-1 «Удалена»: rows read like any row (mark, no action, no extra column), among live rows | PF | component | ✔ |
| US-1 deleted card, restore, reader; Back keeps the filter | PF, LD | component | ✔ |
| US-1 BR-WS-5 purge: back to the filtered list, page 1, notice, `replace`; other way → unfiltered; trail unfiltered | LD (6), LIB `listOrigin` | component, unit | ✔ |
| US-1 address: link, reload, one writer, `replace`, others kept, blank/duplicates left out; Back leaves the page | PF, HK (9), PQ | component, unit | ✔ |
| US-1 the main navigation opens the list unfiltered; «Очистить фильтры» removes all four | PF (keyword from a link), PQ | component | ✔ for a keyword from a link; ✘ for one typed in the field (D-F3) |
| US-1 page 1 whenever the content changes | PF "a change returns to page 1" | component | ✔ for a first change; ✘ coming back to an earlier content (D-F1) |
| US-1 BR-WS-10 unnameable values («Готово», `новая`, blank) → «Фильтр задан», «Очистить фильтры» | PF | component | ✔ |
| US-1 empty frames: filtered, bin, no live item, all deleted; «Сбросить фильтр» clears four, focus «Поиск» | PF | component | ✔; the way out of the frame flashes (D-F2) |
| US-1 BR-WS-14 creation notice judged by all four | PF (3), LIB `filterMismatch` | component, unit | ✔ |
| Design § 5 states 1–5, 8, 9; § 9 announcements (not on first read) | PF | component | ✔ |
| US-2 options: who, order as read (panel never sorts), marks, «Не назначен» last, never empty; re-read with «Удалена»; previous options stay; loading/failed texts | PA | component | ✔ |
| US-2 match, tags, remove button, Backspace; former/disabled/unknown marks in options, tags and column | PA | component | ✔ |
| US-2 D-2 alike options (e-mail, «№ n», mixed groups, found by e-mail), D-4 id that is no option | PA, LIB `assigneeFilterOptions` | component, unit | ✔ |
| US-2 keys: Down opens, type narrows, Enter ticks, Esc closes | PA, PF | component | ✔ |
| US-3 title/case/«ё», identifier, number, `%`, trim, blank, 255 in a link, first `search`, deleted only under «Удалена», reader | PK | component | ✔ |
| US-3 D-3 timing: 250 ms, Enter, leaving, trimmed, 255, address echo not overwriting typing | KW (10) | component | ✔; a typed keyword is not cleared (D-F3), nor text typed and cleared in one tick (D-F4) |
| BR-WS-9 reserved URL characters (`% & # +`) survive the address | PQ (2) | component | ✔ |
| § 8 narrow widths (640) | PF (style only: jsdom has no layout) | component | ◐ not measured in a browser |
| NFR option sizes: 2 000 people in the picker (design § 11 asks QA to time it) | PA (jsdom floor), a real browser, F-8 | component, manual | ✔ about 0.35 s to open or tick on a quiet machine |
| Contract conformance: parameter names, `none`, `assignee-options?state=`, blank keyword omitted, `isActive` on the row | `workItemsApi.test.ts`, read of `listFilterQuery` | unit | ✔ |
| Docs: mod-003 status note, mod-005 design § 6 amendment and status note | read | review | ✔; mod-017 note missing (D-D1) |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| F-1 | page 2, a filter set, then cleared | page 1 | ✘ D-F1 |
| F-2 | leave the filtered-empty frame, next read held | no «Рабочих элементов пока нет» | ✘ D-F2 |
| F-3 | keyword typed and applied, then «Очистить фильтры» / main navigation | cleared | ✘ D-F3 |
| F-4 | text typed, blur and clear in one tick | field and address agree | ✘ D-F4 |
| F-5 | keyword of `% & # +` written, sent and reloaded whole | exact | ✔ PQ (2) |
| F-6 | several changes, then Back | leaves the page in one step | ✔ PQ |
| F-7 | options read per state change | design § 2.3 asks a re-read when a state enters or leaves the bin; two live ticks made 2 extra reads (3 in all). Harmless, not a defect | note |
| F-8 | 2 000 people in the picker, production build, headless Chrome 155, 22 cores (software paint) | quiet machine, two runs: open 351 / 394 ms, first key 168 / 164, then 40 and 17 ms, tick 345 / 314, reopen 278 / 340. Loaded machine: open 772, first key 431, tick 1 184, reopen 1 048. The cost is the unvirtualised list (2 001 `li`, 10 038 DOM nodes) redrawn whole after each choice; a few hundred people cost a fraction. D-1 A stands (D-1 B would list the same options); a virtualised kit list is a separate task if a slow device complains | ✔ for the human: measured, acceptable |
| F-9 | Tab order of the band (design § 9); band at 640 px | order ✔ PQ; keys PF/PA/KW; widths asserted as styles | ◐ 640 px layout not seen in a browser |
| F-10 | E2E specs the change could break | none breaks (list-page specs use table-scoped or role-and-name locators); `27-organization-members.spec.ts:461` still passes, its intent (no mark in the list) is superseded by OQ-2 A | not run (NO) |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-10-19 | `npx vitest run src/features/workitems/pages/WorkItemsPage.qa.test.tsx` | 9 tests passed (5 are `it.fails` for D-F1..D-F4, 4 plain) |
| 2026-10-19 | the same `BASE=5de279f7 bash scripts/verify.sh` as above (frontend: affected files, guards) | docs checks, backend build and tests, lint, build, and the 13 source-reading guards (590 tests) ok; **affected frontend tests: RESULT failed**, 2 038 of 2 039 passed in 142 files, two failures, both caused by this pass (below) |
| 2026-10-19 | throw-away harnesses kept outside the repo: `AssigneeFilter` with 2 000 people (F-8); `KeywordFilter` + `useWorkItemFilter` + a router link (D-F3), production builds in headless Chrome 155 | F-8 numbers; D-F3 reproduced, and cleared without the focus move |
| 2026-10-19 | `WorkItemFormPage.readOnly`, `KeywordFilter`, `useWorkItemFilter`, `WorkItemFormPage.landing` (4 files), then `src/features/workitems/pages/WorkItemsPage` (7 files), alone, on the restored tree | 38 and 121 tests passed |
| | **E2E** | not asked, not run: standing NO (paused 2026-10-12); no `stand.json` exists to quote |

The two failures of the shared run: (1) `pages/qa-debug.test.tsx`, my scratch probe, deleted while Vitest had it queued ("Cannot find module"); (2) `WorkItemFormPage.readOnly.test.tsx` "a member turned reader after a refused save" timed out at 60 s (the file took 161 s) while the machine was saturated by this pass's own Vitest and Chrome runs and another build. Re-run alone it passes (row above), so a load timeout, not a defect. During the run I also put debug lines and, for about a minute, the D-F3 guard into `KeywordFilter.tsx` and `useWorkItemFilter.ts` to find the cause; both are back as the developer left them (`git diff` clean) and the re-runs above used them so.

### Defects

Each frontend defect below has a test in `WorkItemsPage.qa.test.tsx` written as `it.fails` (it passes while the defect stands). When one is fixed its test starts failing: drop `.fails` then.

#### D-F1: Clearing a filter brings back the page the earlier content was on

- Severity: major (BR-WS-11, design § 3: the page returns to 1 whenever a filter's content changes)
- Steps: 120 live items. Open the list, go to page 2 (`51–100 из 120`). Choose «Статус» «Новая»: page 1 of 120, as it should be. Untick «Новая» (or press «Очистить фильтры»).
- Expected / Actual: the unfiltered list on page 1 (`1–50 из 120`). Actual: `51–100 из 120`; the read is `?page=2`. `WorkItemsPage` keeps `paging = { content, page }` and shows `page` when `paging.content === content`; choosing «Новая» does not overwrite it, so the old pair matches again when the content returns to what it was.
- Fix: when the content differs from `paging.content`, store the new content with page 0 (derive it in render, or a `setPaging` on change).
- Status: open · test `D-F1` in PQ.

#### D-F2: «Сбросить фильтр» and «Очистить фильтры» flash «Рабочих элементов пока нет»

- Severity: minor (a pattern inherited from mod-017's page; this line makes it easy to reach)
- Steps: open `?state=Закрыта&teamId=<VENUS>` (the frame «Ничего не найдено»). Press «Сбросить фильтр» with the next read held.
- Expected / Actual: the frame stays (or the rows of the previous read) until the list arrives. Actual: at once «Рабочих элементов пока нет» with «Создайте первый рабочий элемент…», a false statement about a project that has items, until the read ends. The page chooses the frame from the new address (`filtered`) and the previous data (`count 0`, kept by `keepPreviousData`).- Fix: choose the empty frame only from data that belongs to the current key (`!query.isPlaceholderData`), else keep the previous frame or show the skeleton.
- Status: open · test `D-F2` in PQ.

#### D-F3: A keyword typed in «Поиск» comes back after «Очистить фильтры» and after the main navigation

- Severity: major (US-1 "The filter lives in the address": the main navigation's «Рабочие элементы» opens the list unfiltered; BR-WS-11, BR-WS-13: both clears remove every filter)
- Steps: open the list (`?state=Новая` or none). Type «оплат» in «Поиск» and press Enter (or wait the pause); the field blurs when the pointer goes to the next control. Press «Очистить фильтры», or click the main navigation's «Рабочие элементы».
- Expected / Actual: address without `search`, field empty, list unfiltered. Actual: the address loses the keyword for a moment, the field keeps «оплат», and 250 ms later the pause writes it back: the list is filtered again, `state` and `teamId` stay cleared. A keyword that arrived in a link clears fine, which is why the developer's clear tests pass. Reproduced in Vitest (`userEvent`) and in headless Chrome 155 on a production build with the real `KeywordFilter` and `useWorkItemFilter` beside a router link (address sampled every 10 ms: `?search=оплат` → `` → `?search=оплат`; with focus left in the field the same click clears).
- Cause: the wrapper's `onBlur` calls `setText(keyword)` with the text the field already has; that update is replayed over the render-phase reset (`setText(value)`) the field makes when the address changes (my reading of React's queue; the next fact is measured). Changing the handler to `if (keyword !== text) setText(keyword)` made both clears work in Vitest (tried in the tree, then put back).
- Fix: that guard, then re-run PQ's D-F3 and D-F4 (the second may go too).
- Status: open · tests `D-F3` (two) in PQ.

#### D-F4: Text typed and not applied survives «Очистить фильтры» pressed in the same tick

- Severity: minor (the blur and the click inside one router transition; one full run of PQ under load showed it with `userEvent`, `fireEvent` reproduces it every time)
- Steps: `?state=Новая`; type «abc» in «Поиск»; blur and click «Очистить фильтры» in one tick (a slow device, a busy page).
- Expected / Actual: field empty, address without `search`. Actual: the address is clean and the list unfiltered, but the field reads «abc». `KeywordFilter` keeps the keyword it wrote in `written` and waits for the address to echo it; the router renders only the last location, the echo never comes, `intended` stays «abc», and Enter or leaving the field write nothing because the text equals `intended`: the user must edit the text to filter by it again.
- Fix: drop `written` entries the address has moved past without echoing them (compare `location.key`), or have the clear tell the field.
- Status: open · test `D-F4` in PQ.

#### D-D1: No Log note in mod-017's status.md (and none in mod-016's)

- Severity: minor (docs)
- Steps: `spec.md` § Lines in flight: "this line's api-contract restates [the shared filter specification] with a note in mod-017's status.md (AGENTS.md)". `api-contract.md` § 7 lists mod-017 § 2.1, § 3 and mod-016 § 4.2 / BR-M1-26 as superseded in part. The branch changes only mod-003's and mod-005's `status.md`.
- Expected / Actual: one Log line in `mod-017-work-item-owning-team/status.md` (and, since the list's column leaves BR-M1-26's cut, in mod-016's). Actual: none.
- Status: open

### Verdict (Frontend)

blockers: 0 · majors: 2 (D-F1, D-F3) · minors: 3 (D-F2, D-F4, D-D1) · result: fail until the two majors are fixed; the re-check is PQ (drop the `.fails` of D-F1 and D-F3) and `WorkItemsPage.*`, `KeywordFilter`, `useWorkItemFilter` tests. The minors are carried to gate-3 as debts (the backend's D-B1 with them). No E2E: standing NO.
