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
| QA-1 | minor | The project views' own tab drops the filter |
| QA-2 | minor | Pre-amendment text left in mod-017's spec and stories |

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
