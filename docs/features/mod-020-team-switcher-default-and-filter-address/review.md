# mod-020-team-switcher-default-and-filter-address — review

## QA (light, S route)

Verdict: **pass with debts**. Open: 0 blocker, 0 major, 2 minor (QA-1, QA-2: debts for gate-3), 1 question for the human (Q-1).

Scope: `git diff origin/main...HEAD` at `da4fe52a` — the switcher (`workContext.ts`, `resolveSelection.ts`, `useOrganizationsContextSource.ts`), the filter in the address (`useTeamFilter.ts`, the list, the project backlog and board, `TeamHead`, `scope.ts`), the amended spec rows and this folder's `status.md`. Frontend only; no backend and no api-contract file touched.
E2E: not asked — the standing answer is NO since 2026-10-12 (recorded in `status.md`, Decisions). No Playwright spec was written or run. The existing specs 19, 23 and 28 never name a default team (no `defaultMark`/`setDefaultTeam` in `e2e/`), so none of them meets the new first step.

### Test plan (written before the implementation was read)

From the queue line, mod-017 OQ-8 B / OQ-9 B, BR-OT-8 with api-contract § 3, BR-OT-11, mod-016 BR-M1-27 and doc-012 BR-MX-11:

- **Switcher.** S1 a named default beats the user's own team; S2 the only team is the default; S3 no default: own first team, else first by name; S4 a remembered team that exists is kept; S5 a remembered team that is gone falls to the default; S6 a team in the address wins; S7 a list without marks does not crash; S8 another project or organization chosen re-resolves the default for the new project.
- **Filter.** F1 a choice is the repeated `teamId` (`none` for «Без команды»); F2 an address with it opens narrowed, every read carries it (list, backlog, board); F3 a change replaces the history entry; F4 other parameters kept, and the card's `?workitem=` comes and goes with the filter kept; F5 the level drop-down keeps it, the tab, «Показать канбан», the team menu and «Весь проект» start unfiltered; F6 a team's own views ignore it; F7 blank is no filter, an unknown or malformed value selects nothing and can be reset; F8 the list returns to page 1 when the filter changes; F9 duplicates, order, encoding, clearing removes the parameter; F10 another project re-reads the address; F11 the wire carries the same values; F12 no leftover `useState` filter.

### Plan coverage (status.md, Plan 1-4)

1. **OQ-8.** S1-S6 and S8 have their tests in `workContext.test.ts` and `useOrganizationsContextSource.defaultTeam.test.tsx` (hook, in-memory API with the real `defaultMark`). S7 is the `workContext.test.ts` case "no entry is the default or the list does not say" and the `Boolean(team.defaultMark)` line. The backend serves the mark (`TeamResponse.DefaultMark`), the type is the list's own, and a default naming invalidates `keys.teams`, which the switcher reads.
2. **OQ-9.** F1-F4, F7-F9, F11 in `useTeamFilter.test.tsx`, `WorkItemsPage.test.tsx` and `ProjectViews.teamFilterAddress.test.tsx`; F5 for the level drop-down and «Показать канбан». F10: the pages are keyed by project, and no route redirect drops the query (checked in `app/routes.tsx`); a sign-in round trip keeps it too (`AuthContext.currentPath()` returns pathname plus search). F12: no `useState` of team ids remains in the three pages.
3. **Docs.** doc-012 BR-MX-11, mod-017 BR-OT-4, -8, -11 and design § 6.3 agree with each other, with the code and with doc-013 BR-NE-9. Leftovers: QA-2.
4. **Checks.** See "Commands run".

### Mutation check of the new tests

Each change to a production file below was applied by hand, the named tests run, the file restored (`git status` clean after each).

| Mutation | Result |
|---|---|
| `defaultEntry` without the `isDefault` step | killed: 2 `workContext` tests and 6 hook tests fail |
| the level drop-down's links without the filter (`TeamHead`) | killed: 2 tests |
| `replace: true` → push in `useTeamFilter` | killed: 1 test |
| the list's page no longer tied to its filter | killed: 1 test (mod-017's "a change returns to page 1") |
| a blank `teamId` counts as a value | killed: 2 tests |
| `filterTeamIds` returns the address's ids for a team's views too | **survived** the developer's tests (a team's rows come from a different read, and the `filtered` flag was not asserted) |

The survivor mattered: with a stray `?teamId=` on a team's view, an empty team backlog or board would read «Ничего не найдено» with a «Сбросить фильтр» that changes nothing a team's view shows (and, by the code, `createdNotice` would add "не подходит под текущий фильтр" to a creation there). Closed by two tests on the empty states (below); with the mutation they fail.

### Tests added

`src/frontend/src/features/boards/pages/ProjectViews.teamFilterAddress.qa.test.tsx` (7 tests, all green):

- «Без команды» beside a team is `?teamId=none&teamId=<id>` in the address, in the chosen order, and in every backlog read.
- The team menu («Аналитика») and «Весь проект» leave the filter behind: the new address has no `teamId` and the team's view has no filter row.
- Board: opening a card adds `workitem` beside the filter, closing it leaves the filter and the field.
- Board: a `teamId` that names no team gives «Ничего не найдено»; «Сбросить фильтр» empties the address and brings the cards back.
- Board: a level change keeps several values, `none` among them, in the address's order.
- A team's empty backlog and empty board with a stray `teamId` read «Бэклог пуст» / «Канбан пуст», without «Сбросить фильтр» (kills the survivor).

### Defects

| Id | Severity | What |
|---|---|---|
| QA-1 | minor | The project views' own tab drops the filter — **fixed** (CR-2, `## Round 1 — fixes`) |
| QA-2 | minor | Pre-amendment text left in mod-017's spec and stories — **fixed** (CR-1, CR-3), except one backend comment (debt) |

**QA-1 — the selected tab «Бэклог» / «Канбан» of the project's views links to the page without its filter.**
Steps: open `/organizations/<o>/projects/<p>/backlogs/stories?teamId=<team>` (or choose a team in «Команда»), click the tab «Бэклог» (the only, selected tab). Result: the address becomes `…/backlogs/stories`, the field reads «Все команды», every row is back. The tab's `href` is the address without `?teamId=`, so «Copy link» on it also loses the filter. Before mod-020 the same click kept the filter, by reading the old code (not run on main): the filter was component state, the page is keyed by project, so a navigation to its own address kept it. `TeamHead` gives the level drop-down the filter (`levelSearch`) but not the tab (`views={[{ to: pagePath(...) }]}`). An exploratory test asserting `href` = the page's address with its filter failed on that line (received: the path alone); it is not committed, a failing test cannot be. The developer chose this on purpose (Decisions: «the tab … start unfiltered», design § 6.3), reading "leaving the page drops it"; but the tab is the page itself, not another view, so the reading costs a behaviour the page had. Why only minor: the spec names re-reads and level changes, not the tab; the click is rare and the filter is one choice away. Note for a fix: `PageHeader` picks the selected tab by `view.to === pathname` (`shared/ui/PageHeader.tsx:442`), so putting the search into `to` also needs that comparison to ignore it, and `shared/ui` runs the whole suite.

**QA-2 — text the amendment left behind.**
- `mod-017/stories/US-3-project-default-team.md:59`: «Scenario: The default affects the starting value of a creation, and nothing else (BR-MX-11; BR-OT-4)» now contradicts the amended BR-OT-4 and BR-MX-11. Its body ("Given … U's team in the switcher is MARS … the switcher still names MARS") still holds, because MARS is chosen there, not defaulted.
- `mod-017/spec.md:132` (the access/lifecycle table): «Team filter … Read: the page's state» — it is the page's address now.
- `mod-017/stories/US-5-team-filter.md:68`: «The choice lives with the page's visit … leaves the page and opens it again → no filter is set» holds for a link without `teamId`, but a link or Back to a filtered address does bring it back; the scenario does not say so.
- `mod-016/spec.md` BR-M1-27 (switcher default = the user's own first team) carries no pointer to the amendment, though mod-017 BR-OT-11 now sits between them; `TeamResponse` (backend XML comment) and feat-028 api-contract § 1.3 `isMember` still describe the member team as what the panel opens on. Comments and contracts, no behaviour.

### Question for the human (not a defect; already in `status.md`, Decisions)

**Q-1 — the fallback below the default.** The queue line says "the project's default team, else the first team by name". The developer kept BR-OT-11's step between them: the user's own first team, else the first by name, so that mod-016 US-7 (a member lands on their own team) stands. It differs from the literal line only for a project with no default team and a user whose own team is not first by name. The developer recorded it and noted that dropping the membership step is one line in `defaultEntry`. The reading is defensible: mod-017 OQ-8 B is "the project's default team first", with A's fallback (BR-OT-11) behind it. It is worth one sentence at gate-3.

### Commands run

- Vitest on the new QA file: 7 passed. Six mutation runs (above) plus a re-run of the survivor against the new tests, each followed by a restore; `git status` clean.
- `BASE=origin/main bash scripts/verify.sh` (with this file and the QA test in the tree): exit 0, RESULT green — docs checks, lint, build, Vitest affected 144 files / 1969 passed, source-reading guards 13 files / 590 passed (1 skipped, as before).

## Round 1

Scope: `git diff origin/main...mod/020-team-switcher-default-and-filter-address` at `c1f30c40` (merge base `7567d49e`). Frontend and docs, S: one round over the whole diff. Measured against the queue line, mod-017 OQ-8 B / OQ-9 B, BR-OT-8, doc-012 BR-MX-11 and doc-013 BR-NE-9.

Verdict: **changes-requested**. Open: 0 blocker, 1 major (CR-1), 3 minor (CR-2..CR-4). CR-1 is prose only, so it gets a delta read and no QA. CR-1 takes QA-2's US-3 bullet and raises it to major. The stage's rule makes a sentence that the diff's own amendments contradict a major. CR-2 is QA-1 with a wider scope. CR-3 is the rest of QA-2, with a correction. Q-1 stays for gate-3.

The code does what the line asks:
- OQ-8. `defaultEntry` checks `isDefault`, then `isMember`, then takes the first (`shared/lib/workContext.ts:140-146`). The mark comes from the list's `defaultMark` (`resolveSelection.ts:35-43`) at the switcher's team level (`useOrganizationsContextSource.ts:196`) and in a move's first entries (`:112`). A chosen or remembered team the list still holds is kept (`resolveSelection.ts:61`). Naming a default invalidates the key the switcher reads (`organizations/hooks/mutations.ts:198`).
- OQ-9. The filter is the repeated `teamId`, with blanks dropped and other values kept (`useTeamFilter.ts:18-22`). A change is a replace that keeps the rest of the address, the hash and the state (`:55-66`). The list and both project views read it, and a team's views ignore it (`BacklogPage.tsx:99-100`, `BoardPage.tsx:101-102`, `scope.ts:34-39`). The level links carry it (`TeamHead.tsx:128, 181`). The wire is unchanged (mod-017 api-contract § 3; a malformed value selects nothing, `:396`).
- Tests. They check what they say, and the fixtures make each choice discriminating: the default team is neither the user's own nor the first by name. QA's mutation table and its two added tests close the one gap.
- Conventions. Boards imports only workitems' `index.ts`. `shared/` imports nothing from `features/`. The commits are within 72 characters, and no ADR-worthy decision was made silently.

### Findings

**CR-1 — major — prose the diff's own amendments contradict, left unamended.** The diff amends BR-OT-4, BR-OT-11 (`mod-017/spec.md:73, 80`) and BR-MX-11: the project's default team is now the team the switcher opens on. These four sentences still say the opposite:
- `mod-017/api-contract.md:341-343` (§ 2.8): "It decides the starting value of a creation and nothing else: … the switcher, the views, the filters and the orders do not read it (BR-OT-4)". QA-2 does not list it.
- `mod-017/stories/US-3-project-default-team.md:59`: «The default affects the starting value of a creation, and nothing else». This is QA-2's bullet; the scenario's body still holds, because MARS is chosen there.
- `mod-017/design.md:322-324` (§ 8): "The switcher's fallback is the first team in that order, own teams first (BR-OT-11)". This is the file the diff edits at § 6.3.
- `mod-017/spec.md:100` (fate row for mod-016 BR-M1-27, US-7): "the switcher's fallback is the user's first team in that order, else the list's first". This is the file the diff edits.

How it fails: an agent writing a contract or story test from § 2.8 or US-3 asserts that naming a default leaves the switcher alone, and the test fails for a user who has no chosen team. feat-040 must carry this fallback onto its team level (doc-013 BR-NE-9). Its developer reading § 2.8 or design § 8 drops the default.

Fix, in prose: add a "(Amended by mod-020, 2026-10-18, OQ-8 B: …)" note to each place, as BR-OT-4 and BR-OT-11 have, and give US-3's title the same narrowing ("…and moves no chosen team"). Because of § 2.8, add a note line in mod-017 `status.md` (AGENTS.md: an api-contract changes only with a status note). Optionally reword BR-OT-4's own "nothing else: not the switcher", which now sits right before its amendment.

**CR-2 — minor — QA-1, with a wider scope: every link to the page's own pathname clears the filter.** There are three such links. The selected tab (`TeamHead.tsx:175`). «Весь проект» on the project page, where it is the selected entry (`TeamHead.tsx:161`). The nav entry «Рабочие элементы» on the list (`app/navigation/model.ts:102-106`). Each links to the pathname without `?teamId=`. Before mod-020 the same click kept the filter, because each page is keyed by project and stays mounted (`WorkItemsPage.tsx:70`, `ProjectBacklogPage.tsx:28`, `ProjectBoardPage.tsx:27`). Two pieces of prose name only the case seen from a team page. design § 6.3 (`mod-017/design.md:206-209`) says «Весь проект» is an "other address". The status Decisions call the tab and «Весь проект» "other views". Neither is true on the project page.

Fix: either carry `teamFilterSearch` on the links that resolve to the current pathname, or write the rule into BR-OT-8 and design § 6.3: any link to the page without `teamId` clears the filter, the selected tab and the nav entry included. For the tab, `PageHeader`'s `view.to === pathname` match (`shared/ui/PageHeader.tsx:440-442`) would have to ignore the search.

**CR-3 — minor — the rest of the pre-amendment prose (QA-2), and one correction.** Nobody acting on BR-MX-11 or BR-OT-8 would do anything differently because of these:
- `mod-017/spec.md:132` «Read: the page's state».
- `mod-017/stories/US-5-team-filter.md:68`. "Leaves the page and opens it again → no filter" holds for the nav entry, but Back, or a link to a filtered address, brings the filter back.
- `mod-017/api-contract.md:399` "Life: for the visit". The server half holds.
- `mod-016/spec.md:145` (BR-M1-27), `mod-016/stories/US-7-default-team-by-membership.md:8-11`, `mod-016/api-contract.md:349, 492-494` and `src/backend/…/GetTeams/TeamResponse.cs:8-9`. All still describe the member's team as the team the panel opens on.

Fix: amendment notes, and for mod-016 one Log note in its `status.md`, like mod-017's P-10 note (`mod-016/status.md:59`). Correction to QA-2: `feat-028/api-contract.md:168` (`isMember`) says nothing about where the panel opens. Nothing needs fixing there.

**CR-4 — minor — the list's page reset rests on `useMemo` identity.** `WorkItemsPage.tsx:98-99` compares the filter array by identity. That identity comes from `useTeamFilter.ts:53`, and the docblock at `:43-45` promises it. React documents `useMemo` as a performance hint whose cache it may drop: today on a dev hot reload, and possibly more in future versions. A dropped cache would send the user to page 1 with no change of filter. Fix: compare the filter's content (expose `content`, or compare `teamIds.join('\n')`) and soften the docblock's sentence.

### Q-1 (QA's question for the human)

I agree it is a question, not a defect. The code, the amended BR-MX-11 and BR-OT-11 follow the developer's reading. "One line in `defaultEntry`" understates the literal reading's cost, though. Dropping the membership step also retires mod-016 US-7 and its tests (`workContext.test.ts:107-159`, the mod-016 US-7 blocks of `useOrganizationsContextSource.defaultTeam.test.tsx`), and rewrites both amendments.

### Behavioural claims in prose

Direction 1 (prose the diff adds or changes):
- doc-012 BR-MX-11 amendment: default, then own first, then first by name; a remembered team is kept → `workContext.ts:140-146`, `resolveSelection.ts:61`; hook tests in the "mod-020 Scenario" block. Its feat-043 clause is marked future (doc-013 BR-NE-9).
- BR-OT-4 and BR-OT-11 amendments → the same anchors; BR-OT-4's leftover clause → CR-1 (optional).
- BR-OT-8 amendment: in the address, a link carries it, a level change keeps it, a team's views have none → `useTeamFilter.ts:18-22, 49-69`; `TeamHead.tsx:128, 181`; `scope.ts:34-39`; tests in `ProjectViews.teamFilterAddress*.test.tsx`.
- design § 6.3 amendment: the level links keep it → `TeamHead.tsx:181`; «Весь проект» and the other page are other addresses → true only from a team page → CR-2; the page returns to 1 "by the address too" → `WorkItemsPage.tsx:98-99` → CR-4.
- `useTeamFilter` docblock: a replace that keeps the parameters, the hash and the state → `:63` and its tests; "the same object while its content is" → `:53` → CR-4.
- `teamFilterOf`: a blank is no value, other values are kept → `:18-22` and its tests. `teamFilterSearch` gives only its own part → `:28-35` and its test.
- `scope.ts:29-33`: a team view's `teamId` is not sent → `:34-39`; the dev tests check the read paths, and QA's empty-state tests kill the survivor.
- `resolveSelection.ts` (`LevelList`, `teamLevelItems`), `workContext.ts` (`isDefault`, `WorkContextProject.teams`), the `withFirstEntries` comment → `:35-43`, `:140-146`, `useOrganizationsContextSource.ts:112` and the test "on a team-level page, moving to another project…".
- `useOrganizationsContextSource.ts:195`: the pages read the same key → `api/queryKeys.ts:19-24`; `select` applies per observer.
- Docblocks of `WorkItemsPage`, `ProjectFilterRow`, `BacklogPage`, `BoardPage` and `workitems/index.ts` → `WorkItemsPage.tsx:93`, `BacklogPage.tsx:99`, `BoardPage.tsx:101`, `index.ts:42`.
- status Decisions: the tab, «Показать канбан», the team menu and «Весь проект» start unfiltered → `TeamHead.tsx:161, 166-170, 175`, as built; "other views" → CR-2.
- status [dev] and the commit body, "frontend only, no api-contract change" → the diff stat: nothing under `src/backend`, no `api-contract.md`.
- QA claims: a default naming refreshes the switcher → `mutations.ts:198`; no redirect drops the query → `app/routes.tsx:68` is the only `Navigate` (index → «Главная»); feat-028 § 1.3's `isMember` → overstated → CR-3.

Direction 2 (behaviour the diff changes, prose outside it), over `docs/`, `.claude/`, the AGENTS.md files and the comments in `src/backend` and `src/frontend`:
- Which team the switcher opens on. Grepped `BR-M1-27`, "first team the user", "own first", "switcher's fallback", "nothing else", "creation only", "affects BR-MX-10", "do not read it" and `isMember`. Unamended hits → CR-1 (mod-017 api-contract § 2.8, design § 8, spec fate row, US-3:59) and CR-3 (mod-016 spec, US-7, api-contract; `TeamResponse.cs`). Hits that hold: mod-017 US-7:25 (its data names no default), US-2:33 (the switcher's team is chosen), feat-028 US-6:55 (points to mod-020), and the `DefaultTeamDialog` copy (it describes creation and claims no "only").
- Where the filter lives. Grepped "page's state", "for the visit", `OQ-9`, "filter … address" and `teamId` in the docs. Hits → CR-3 (mod-017 spec:132, US-5:68, api-contract:399). Hits that hold: feat-028 spec:166 and US-6:77 (owed-by markers naming mod-020), mod-015 plan-frontend:519 (another surface's filter), doc-013 BR-NE-6/-9 (agree).
- Not swept: `doc-012/audit/*.md` (dated audit snapshots); the plan, qa and review files of earlier features (records of past runs, mod-017 `plan-backend.md:424` among them); the E2E specs (standing NO; QA reports that none names a default).

### Commands run

`git diff`, `git log` and greps only. I ran no tests, because this checkout has no `node_modules`. The developer's and QA's `BASE=origin/main bash scripts/verify.sh` runs were green on this tree (status Log).

## Round 1 — fixes (developer)

Open after the fixes: 0 blocker, 0 major, 2 minor (CR-2 in part, CR-3 in part: debts for gate-3), Q-1 for the human. CR-1 is prose only: one delta read, no QA re-run. The two runtime changes below (CR-2, CR-4) are minors folded into the fix round; each has tests, and a delta reader who wants to read code reads only these lines: `TeamHead.tsx` (`filterSearch`), `PageHeader.tsx` (`pathOfView`, `PageViews`), `WorkItemsPage.tsx:99-102`, `useTeamFilter.ts:43-48`.

- **CR-1 (major) — fixed, prose.** Amendment notes added to mod-017 `api-contract.md` § 2.8 (and its note line in mod-017 `status.md`), `stories/US-3` (scenario retitled "…and moves no chosen team and no item"), `design.md` § 8 and `spec.md` fate row. BR-OT-4's own clause reworded: "since mod-020 … the team the team views' switcher opens … and nothing else: not a view's contents, a filter or an order".
- **CR-2 / QA-1 (minor) — fixed in part.** `TeamHead` gives the project page's own tab, and «Весь проект» (its selected entry), the page's filter, as the level drop-down already had; `PageHeader` finds the current view by the path of its `to`, so a query on it does not unselect the tab (`PageView` doc and `pathOfView`). Tests: `PageHeader.views.test.tsx` (3), `ProjectViews.teamFilterAddress.test.tsx` (5: both tabs' `href`, selection and click, `none` among several values, no filter no query, a team's own tab and «Весь проект» carry none); with `pathOfView` and the «Весь проект» link reverted, five of these eight fail (checked by hand, then restored). **Left as debt: the nav entry «Рабочие элементы» on the list** still opens the list unfiltered. `app/navigation/model.ts` is a static model whose `landing` is a function of the selection alone; carrying the page's filter would make the app shell read a slice's address. The rule is written down instead (BR-OT-8's amendment, design § 6.3): any link without `teamId` opens the page unfiltered.
- **CR-3 (minor) — fixed in part, prose.** Notes in mod-017 `spec.md` access table (:132), `US-5` (the scenario now ends with Back / a link with `teamId` bringing the filter back), `api-contract.md` § 3 "Life"; mod-016 `spec.md` BR-M1-27, `US-7`, `api-contract.md` §§ 3.3 and 6, and a note line in mod-016 `status.md`. **Left as debt: the `TeamResponse` XML comment** (`src/backend/…/GetTeams/TeamResponse.cs:8-9`). A comment-only edit, but a `.cs` file is backend code to `verify.sh`, and a frontend-only task does not take a backend build and test run for a comment; it goes with the next backend change to that record.
- **CR-4 (minor) — fixed.** The list's page is tied to the filter's content (`JSON.stringify(teamIds)`), not to the array's identity (`WorkItemsPage.tsx`); the `useTeamFilter` docblock no longer promises the identity. No new test: React keeps the memo today, so no test can make identity differ while the content holds; the behaviour (a change returns to page 1, the same filter keeps the page) stays covered by "a change returns to page 1" and "the next page keeps the filter in the address".
- **QA-2 — fixed**, by CR-1 and CR-3 above, except the backend comment.
- **Q-1** is unchanged: the human's call at gate-3.

## Round 2 (delta)

Scope: the fix diff `b5ee424c..6f626770` (one commit) and the findings open after round 1. S: the one delta read the cap allows (routing.md, "Ceremony caps by size").

Verdict: **changes-requested**. Open: 0 blocker, 1 major (CR-5, new, prose only), 3 minor debts (CR-2 rest, CR-3 rest, CR-6 new). CR-1 and CR-4 closed. The cap is spent, so the open list goes to the human, not to another read. Q-1 stays for gate-3.

Status after the merge (2026-10-18, PR #25 merged by the human at `0bc5311a` before this read landed): CR-5 resolved by `tech-045-mod020-default-team-prose`; CR-2's rest, CR-3's rest and CR-6 moved to `feat-043` (the human, 2026-10-18); Q-1 answered A (the user's own team stays before the first team by name), recorded in `status.md`.

### Previously open

- **CR-1 — closed** for its four places: mod-017 `api-contract.md:341-345` (§ 2.8), `US-3:59-61`, `design.md:326-329` (§ 8), `spec.md:100`; BR-OT-4 reworded (`spec.md:73`); the api-contract note line is in mod-017 `status.md`. Each agrees with `workContext.ts:140-146` and `resolveSelection.ts:61`. Two places round 1's sweep missed say the same as before → CR-5.
- **CR-2 — closed in part, as reported.** The project page's own tab and «Весь проект» carry the filter, a team page's carry none (`TeamHead.tsx:130, 163, 180`). `PageViews` selects by path (`PageHeader.tsx:77-79, 450-454`) while `Tabs`' `value` stays the full `to`, so the two agree. The other `views` callers (`OrganizationListPage`, `ProjectListPage`, `PageHeadDemo`) select as before. Rest: the nav entry «Рабочие элементы» (`app/navigation/model.ts:103-106`), now written into BR-OT-8 and design § 6.3. Minor, a debt. **Moved to feat-043 (the human, 2026-10-18).**
- **CR-3 — closed in part, as reported.** Notes at mod-017 `spec.md:132`, `US-5:72-76`, `api-contract.md:401`; mod-016 `spec.md:145`, `US-7:18-20`, `api-contract.md:349, 494-496`, `status.md`. Rest: `TeamResponse.cs:8-9`. Minor, a debt. **Moved to feat-043 (the human, 2026-10-18).**
- **CR-4 — closed.** The page is keyed by the filter's content (`WorkItemsPage.tsx:99-102`), and the docblock no longer promises identity (`useTeamFilter.ts:43-49`). No test is owed: `WorkItemsPage.test.tsx:420, 618` pin the behaviour.

### New findings

**CR-5 — major — the "creation only" claim, in two more places.**
- `mod-017/spec.md:57`, the spec's own flowchart, node N3: «Setting stored; it only starts new items (C2, C5)». BR-OT-4 in the same file (`:73`) now says the default also opens the switcher, so the diff carries its own disproof.
- `feat-028/spec.md:73` (BR-OL-7, merged): "The default starts new items and nothing else (mod-017 BR-OT-4)". This is the default-team rule in force for linked teams, the one feat-040 reads when it carries the fallback onto its team level (doc-013 BR-NE-9). Acting on it drops the default, as CR-1's places would have.

Fix, prose: N3 → "…it starts new items (C2, C5) and, since mod-020, is the team the switcher opens when none is chosen (BR-OT-11)". BR-OL-7 gets the "(Amended by mod-020, 2026-10-18, OQ-8 B: …)" note BR-OT-4 has, and feat-028 `status.md` a note line, as mod-016 and mod-017 got.

**CR-5 — resolved by `tech-045-mod020-default-team-prose` (2026-10-18).** Node N3 is amended in place (mod-017 `spec.md:57`, «Amended by mod-020, 2026-10-18»). BR-OL-7 keeps its sentence and gains an «Amended by mod-020 (2026-10-18)» note (feat-028 `spec.md:73`). Each of mod-017 and feat-028 has a note line in its `status.md`. Prose only; `BASE=origin/main bash scripts/verify.sh` is the check (light cycle).

**CR-6 — minor — two more pre-amendment texts; nobody would act differently on them.**
- `mod-016/design.md:772-776` (§ 8): "The team level of the switcher shows the user's own team after…". It is already a record: it still says "the project's first team, then the rest alphabetically", which mod-017 retired. BR-M1-27 carries the note.
- `SetDefaultTeamEndpointTests.cs:171`: the comment quotes US-3's old title, "the default affects the creation only". The test's own claim still holds: no item, view, filter or order moves.

Fix: a pointer to BR-M1-27's note in § 8. The comment goes with `TeamResponse.cs`, at the next backend change.

**CR-6 — moved to feat-043 (the human, 2026-10-18).**

### Runtime changes

- Against BR-OT-8 as amended: these keep the filter: the level links (`TeamHead.tsx:188`), the own tab (`:180`) and «Весь проект» on the project page (`:163`). These drop it: the other page (`:173`), another team (`:162`), «Весь проект» from a team (`:130`) and the nav entry. All match.
- «Весь проект» on a project page now links to the current address with `TEAM_CHOSEN_STATE`. The arrival effect re-navigates with `location.search` (`TeamHead.tsx:237-240`), so the filter survives the focus hand-off. The tab and the entry drop a card's `?workitem=`, as they did before.
- The tests check what they claim. The tab and entry tests assert `href` and the address after the click. `PageHeader.views` test 3 needs `pathOfView`. Test 2 passes on the pre-fix code: it pins the sort by path length, a mutation guard. "Five of eight fail when reverted" is consistent by reading; I did not re-run it.

### Behavioural claims in prose

Direction 1 (prose the fix adds or changes):
- Default first, then own team, then first by name; a remembered team is kept. Stated in mod-017 BR-OT-4, § 2.8, design § 8, the fate row and US-3, and in mod-016 BR-M1-27, the US-7 note and api-contract §§ 3.3, 6 → `workContext.ts:140-146`, `resolveSelection.ts:61`.
- US-7 note, "the scenarios below are for a project with none: three teams" → `US-7:23-80`: every scenario chooses «Пример проекта», three teams, none named.
- BR-OT-8 amendment and design § 6.3, the links that keep the filter and the links that drop it → `TeamHead.tsx:130, 162-163, 173, 180, 188`; `model.ts:103-106`.
- US-5: a link without `teamId` opens unfiltered; Back, or a link with it, filters again → `useTeamFilter.ts:51-54` reads the address alone; `ProjectViews.teamFilterAddress.test.tsx:71`.
- api-contract § 3 "Life" and the access table (`:132`) → `useTeamFilter.ts:10, 18-22`.
- The `PageView` docblock and `pathOfView` → `PageHeader.tsx:66-79, 450-454` and the mod-020 block of `PageHeader.views.test.tsx`.
- The `TeamHead` comment and the `TeamMenu` docblock → `TeamHead.tsx:130, 163`.
- The `useTeamFilter` and paging docblocks → `useTeamFilter.ts:53-54`, `WorkItemsPage.tsx:99-102`.
- The mod-016 and mod-017 status notes say "no shape or endpoint change" → the diff stat shows nothing under `src/backend`.
- The status Decisions line → the same anchors as BR-OT-8.
- The retitled QA test → its body, `…qa.test.tsx:73-91`.

Direction 2 (behaviour the fix changes; prose outside it, over `docs/`, `.claude/`, the AGENTS.md files and the comments in `src/`):
- The switcher's default, swept again because CR-1 covers every place. Grepped "nothing else", "only starts", "starts new items", "creation only", "affects BR-MX-10", `BR-M1-27`, `BR-OL-7`, "own first team" and "panel opens".
  - Unamended: CR-5, CR-6, and the CR-3 rest (`TeamResponse.cs`).
  - Hold: doc-012 BR-MX-11 (amended in its own row) and mod-017 `spec.md:189` (OQ-8, a record). Also feat-028 US-6:55 (points to mod-020), its US-3:111 and US-6:35 (marks and creation, no "only"), and the `isMember` comments in `ITeamReadModel.cs:21`, `GetTeamsHandler.cs:29` and `TeamReadModel.cs:53`.
- The tab and «Весь проект» keep the filter. Grepped "unfiltered", "other views" and «Весь проект». The hits hold: feat-039 US-1:61-73 and design § 3.4 (seen from a team page), mod-017 `design.md:205` (followed by its amendment).
- `PageHeader` selects by path. Grepped `PageView`, "current view" and "selected tab" in ui-kit.md, frontend.md and the mod-015 design. The hits hold: mod-015 design:711.
- The list's page is keyed by content. The "returns to 1" hits (design § 6.3) hold.
- Not swept: the audit snapshots, the plan, qa and review files of earlier features, and the E2E specs (standing NO). Pre-existing and not this task's: flowchart nodes D1 and D1x in mod-017 `spec.md:58-59` still show the last-team guard that feat-028 retired.

### Commands run

`git diff` and greps; `sh scripts/warm-worktree.sh`; `npx vitest run` on 14 files: `PageHeader.views`, `PageHeader`, `.team`, `.kit`, `.menu`, `patternProps.mod002`, `ProjectViews.teamFilterAddress` and `.qa`, `ProjectViews.team`, `BacklogPage.head`, `ProjectBacklogPage`, `ProjectBoardPage`, `WorkItemsPage`, `useTeamFilter`. 201 passed. No `verify.sh` and no full suite.
