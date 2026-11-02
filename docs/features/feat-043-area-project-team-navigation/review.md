# Review: feat-043-area-project-team-navigation

Requirements round: [review-requirements.md](review-requirements.md).

## Round 1 — code

Reviewed at b572d92b, from 5321cdc7; one pass, security included. `FE` = `src/frontend/src`, `org` = `FE/features/organizations`.

**Verdict: `blocked-on-decision`.** 0 blockers, 8 majors, 8 minors. C-3, C-5 and C-6 wait on a decision (below); the others can go to the developer now. Security: nothing found.

### Majors

- **C-1 A link change that unfits a chosen value resets nothing, and the success notice is lost.** Fit is judged only in `applyChoice`/`openObject`; `resolveAgainst` (`FE/app/navigation/useNavigationSource.ts:84-99`) checks existence only. `org/pages/TeamsPage.tsx:177-189`, `TeamPage.tsx:313-328` suppress their notice (`fallsBack`) for a shell reset that never fires. A and MARS chosen, unlink: no notice, `?teamId=MARS` stays. BR-NV-17, ADR § 5, US-7:73-74. Fix: judge fit on offers change; test it end to end.
- **C-2 A deactivated project's node opens read-only, the option OQ-3 rejected.** `org/pages/AreaPage.tsx:442-446` draws a notice for `projectActive === false`, and `AreaPage.test.tsx:386` pins it, against BR-NV-13, OQ-3 A, design:121, US-3:71-74. Rows stay links: `AreaPage.tsx:288`, `AreasPage.tsx:217` (design line 88, US-2 line 51). Fix: «Проект не найден» on the page, plain text in rows; mark mod-018 BR-AW-10 (OQ-6 A) in Breaks.
- **C-3 Row targets drop selectors.** `AreaPage.tsx:339`, `:87`, `:274`, `org/components/TeamDepartmentsSection.tsx:60` are plain paths; `useObjectLink` is unused there, so «Логистика» / MARS opening A loses MARS (US-3 line 79, BR-NV-19). `AreaPage.tsx:288` opens the node, not «Общие» (US-3 lines 20, 25). `AreasPage.tsx:217` follows design line 88 (project row: «Структура»), against US-2 line 50 and OQ-1 A: spec and design disagree. `FE/app/navigation/model.ts:75-82` drops the team silently.
- **C-4 QA F-1 is open on the area prefix.** `…/areas/<gone>/overview`: chosen department is null, `lostKey` empty, so `useNavigationSource.ts:255-257` writes all-null, against its comment at `:236-238`; a remembered gone department gets no notice (BR-NV-7, US-8 line 84). It also fires while a node loads. `:251` writes the address's objects, so other remembered values go (remembered MARS + gone A, address names A: MARS lost, US-8 line 81). Fix: compare the prefix id, wait for the node, drop only the gone kind.
- **C-5 Remembered selection: prose and code differ.** Code reads it only with no organization (`useNavigationSource.ts:177-183`) and any organization page writes, so «Обзор» wipes it. US-8 lines 69-76, spec BPMN node Kp and ADR § 6 say otherwise. status.md holds the reading "to name at gate-3"; the texts stand.
- **C-6 Query selector not found is not implemented.** ADR § 1 last sentence, § 3, design line 73 say a gone or non-fitting query value answers not found. Code drops a gone value quietly and keeps a non-fitting one.
- **C-7 BR-NV-15 overstated.** BR-NV-15 and US-2 lines 80-82 put the lists in the signature test; `NavigationReadSignatureTests.cs` covers `INavigationReadModel` only and its summary still says "the lists". The deferral to feat-041 is only in the status Log. Fix: amend both.
- **C-8 Owed document marks.** mod-017 `spec.md` BR-OT-8 (edited here) still says "a filtered backlog reorders against the full list", contradicting Q-9 A; mod-017 is absent from spec Breaks. mod-017 `api-contract.md` changed with no note in its `status.md` (hard rule). ADR-0031 rule 5 has no marker, while ADR-0032 Spawned and plan step 5 say US-9 writes it. Six lines are corrupt, a literal `n> ` and each doubled: mod-016 `design.md` (2), mod-017 `api-contract.md:332-333`, `design.md` (4), from d94fbab6.

### Decisions owed

1. C-3: project row in «Области», «Общие» (spec) or «Структура» (design).
2. C-5: keep the recorded reading and amend US-8, BPMN, ADR § 6, or implement the text.
3. C-6: not found for query values, or weaken the ADR to a quiet drop.

### Minors (debt)

- C-9 `org/pages/OrganizationGeneralPage.tsx:112`, live copy still says «переключателя».
- C-10 Stale "switcher" comments: `AppLayout.tsx:94`, `TeamHead.tsx:89`, `mutations.ts:46`, `MembersPage`, others by grep; `docs/conventions/frontend.md:47`, `ui-kit.md:245,410`; retired addresses in comments `TeamPage.tsx:45`, `AreaPage.tsx:192`.
- C-11 `FE/app/shell/ContextHost.tsx:62-72`: the link notice in router state refires on reload and remount (`TopBar.tsx:58,70`); `onOrganization` sets no focus (US-8 line 102).
- C-12 `fallbackSelection` (`selection.ts:271`) is dead outside its test.
- C-13 ADR-0024 § 4 marker cites ADR-0032 § 4; the levels are § 3.
- C-14 US-7 line 25 replaces a team view's project filter; US-5 line 35 and `useViewFilter.ts:15` keep it.
- C-15 api-contract § 2: offers serve «Команды»; `OrganizationTeamsPage` reads `useOrganizationTeams`.
- C-16 `useSelectorOffers` lacks `refetchOnWindowFocus` (default false, `App.tsx:15`): another user's change stays unseen.

### Claims pass

- Seven and five statements: `NavigationReadModelTests.cs:22-23,115`. Holds.
- Both reads guarded by the code and scoped by `caller.OrganizationId`: `OrganizationEndpoints.cs`, `Reachability.cs`. Holds.
- Storage holds ids only: `selection.ts:164-170`. Holds.
- `projectId` only on settings: `selection.ts:86-92`. Holds.
- Retired addresses redirect nowhere: `routeInventory.test.tsx`. Holds.
- Narrowed backlog: `narrowed.ts:15`, `BacklogTable.tsx`. Holds; mod-017 text: C-8.
- Switcher retired: `shellSources.test.ts`. Holds. No `it.fails` left.
- mod-017's reorder test replaced for Q-9: `ProjectBacklogPage.reorderFiltered.test.tsx:40`, title and dataset read only.
- Tests deleted in eae3b635 cover retired modules, except C-1's rule.
- 10 Breaks notes present; ADR-0024, ADR-0028 marks present; the rest C-8.
- Direction 2: grepped `ContextSwitcher`, `switchTarget`, `placeholderContext`, `settings/(teams|areas|projects|members)`, `switcher`. Code clean; `docs/conventions` C-10. Unswept: other feature folders' hits (first 40 listed, unread) and the reorder sentence beyond mod-017.

Not read: `design.md` and plans in full; most tests; `PlaceProjectDialog`, `LinkDialog`, `OrganizationListPage` (BR-NV-16). Run: `selection`, `selectorFit` tests, 61 green.

## Round 2 — code (delta)

Reviewed at 06464fe9, delta from 34b6cec9; `FE`, `org` as above. First delta read of the two an L may have.

**Verdict: `blocked-on-decision`.** 1 blocker, 5 majors, 6 minors. D-3 waits on a decision (below); the rest can go to the developer now. Security: the delta adds no endpoint and no new read. D-1 is new, caused by C-6's fix.

### C-1..C-16

| Id | State | Anchor or remainder |
|---|---|---|
| C-1 | closed; a flash remains: D-4 | `selectorFit.ts:227`, `ContextHost.tsx:110-137`; a link added resets nothing; one notice |
| C-2 | closed | `AreaPage.tsx:214`, `AreasPage.tsx:134`, rows plain text; spec Breaks, mod-018 status |
| C-3 | partly: D-6 | project, team and department rows carry the selection; sub-area rows do not |
| C-4 | closed; display: D-8 | `useNavigationSource.ts:188,244,298-317` |
| C-5 | closed as ruled, except D-3 | `useNavigationSource.ts:203-208,286-295`; a visit writes only the organization |
| C-6 | closed; regression D-1, prose D-2 | `useNavigationSource.ts:253-272`, `AppLayout.tsx:313` |
| C-7 | closed in spec and US-2; D-9 | test summary not amended |
| C-8 | closed | no `n> ` line left under `docs/`; BR-OT-8, Breaks, ADR-0031 mark, mod-017 status note |
| C-9 | closed | no «переключател» left in the panel's copy |
| C-10 | partly, debt: D-10 | the listed lines are done; about 30 hits of "switcher" remain |
| C-11 | closed | `ContextHost.tsx:75-83,177` |
| C-12 | closed | `fallbackSelection` gone |
| C-13, C-14, C-15 | closed | ADR-0024 § 4, US-7 line 25, api-contract § 2 |
| C-16 | closed | `useNavigationSource.ts:157`, `navigationQueries.ts` |

### Blocker

- **D-1 «Без команды» answers «Команда не найдена».** `FE/app/navigation/selection.ts:67-68` reads any `teamId` as the team selector, and mod-017's filter value `none` (`workItemsApi.ts:377`, `TeamFilter.tsx`, BR-NV-12) is no team. Ann picks «Без команды» on A's «Рабочие элементы», «Канбан» or «Бэклог»: the address is `?teamId=none`, `selectorNotFound` is `team` and `AppLayout.tsx:313` replaces the page. Confirmed by a throwaway hook test. `?teamId=none&teamId=<team>` fails the same; the reverse order passes. Worked before C-6. Fix: take the first `getAll('teamId')` value that is not `NO_TEAM_FILTER_VALUE`; test the filter value through the source.

### Majors

- **D-2 BR-OT-8 and the filter's comments contradict C-6.** mod-017 `spec.md:77` (a row this delta edits): "a value that is not a team of the project selects nothing, alike for an unknown, foreign or other-project id". `workitems/hooks/useTeamFilter.ts:18-20` and `TeamFilter.tsx:17-18` add that «Очистить фильтры» removes it. Now such a value answers not found and the filter row is off screen. Fix: mark BR-OT-8 (feat-043, ADR-0032 § 1), add it to spec Breaks with a mod-017 status note, correct both comments.
- **D-3 Choosing an organization erases its remembered selection.** `FE/app/shell/ContextHost.tsx:176` writes `NO_SELECTION` for the target. US-8 lines 70-72: MARS and A in «Ромашка», then «Другая», then back, a reload on «Участники» reads MARS / A, and «Другая» keeps what she left. With the selector both fail; through a row of the organizations list (`…/overview`, no write) both hold, so the result depends on the path. BR-NV-10 and US-8 line 57 say the choice opens «все»: the texts collide. No test covers the return.
- **D-4 A link change flashes not found and unmounts the page.** With A and MARS chosen on `…/projects/A/teams?teamId=MARS`, Ann unlinks MARS: the render with the new offers has `selectorNotFound: 'team'` (`useNavigationSource.ts:267`), one commit draws «Команда не найдена» in place of `Outlet`, and only then `ContextHost.tsx:123` moves the page. The same on `areas/L/teams?teamId=MARS`, `teams/MARS/…?departmentId=L` and a move of a project. `OrganizationsSection` unmounts with the page and its notice: that is the only reason `AreaPage.tsx:585` and `TeamDepartmentsSection.tsx:162`, which have no `fallsBack`, do not stand beside the reset notice. `ContextHost.test.tsx:165` uses a stub source, so C-1's end-to-end test is still owed. Fix: judge the reset in the source, so a pair that fitted the previous offers is never `selectorNotFound`; suppress those two notices as `TeamsPage.tsx:177` does.
- **D-5 A remembered pair that stopped fitting makes every menu entry not found.** Olga remembers A and MARS, Ann unlinks them while Olga's tab is closed, Olga reloads on «Участники». `ContextHost.tsx:113` skips a first read and `useNavigationSource.ts:254` judges addresses only, so the selectors read MARS / A. Each entry of A's menu is `…/projects/A/…?teamId=MARS`: «Команда не найдена», and nothing repairs the store (`:318`) until she touches the team selector. ADR § 5 "so does a link change"; US-7 line 73. Fix: judge a remembered selection's pairs at the first read of the offers, drop the more specific value, with one notice.
- **D-6 C-3 reopened: a sub-area link drops the team.** `org/pages/AreaPage.tsx:287`, `:156` (the parent) and `AreasPage.tsx:137` are plain paths. Selectors «Логистика» / MARS / A, «Структура», open «Склад»: `areas/<sub>/overview` has no `teamId`, MARS is gone with no notice and `useNavigationSource.ts:319` writes that down. `model.ts:84` fixed exactly this for «Структура»; design line 173: every link carries `teamId`. Fix: one helper that appends the chosen team to the node's address, used at the three places.

### Decision owed

- D-3: (a) the choice opens «все» and writes it: amend US-8 lines 70-72, and make the organizations-list row the same or say why not; (b) the choice writes nothing and opens the remembered selection: amend BR-NV-10 and US-8 line 57; (c) the choice carries «все» for that history entry only and writes nothing: both texts hold, but the next menu click brings the memory back. Both (a) and (b) amend approved text.

### Minors (debt)

- D-7 `ContextHost.tsx:122-133`: a reset under a remembered selection moves the page. Olga on «Участники» with a focus read that unfits A and MARS lands on MARS's «Обзор»; a `…/settings` form is left too. On a page with no organization nothing re-renders, so the selectors keep the reset value until the next render. Fix: write and notify only when the address does not name the pair, and re-read.
- D-8 `useNavigationSource.ts:208`: a prefix that names a gone object shows «все» and the organization's menu, while the store keeps MARS. BR-NV-7, US-3 line 64 and design line 73 say the selectors do not change; `.qa.test.tsx:110` checks the store only.
- D-9 `NavigationReadSignatureTests.cs:7` still says "the lists".
- D-10 Stale comments. False: `AreaPage.tsx:179` ("read-only notice"), `TeamDepartmentsSection.tsx:61` ("the team stays"; `openObject` resets it, rightly), `shared/lib/workContext.ts:7` (remembered only with no organization). "switcher": `OrganizationGeneralPage.tsx:115-117`, `TeamDialogs.tsx:18`, `useAccessWatcher.ts`, `noticeCorner.ts:7` and others by grep. `kitGuard.baseline.ts:19-20` lists the deleted `ContextSwitcher.tsx`.
- D-11 The two dialog texts changed by 06464fe9 are in no design: status.md says they replace mod-018 `design.md:238`, which still reads the old copy, as does `:114`.
- D-12 `AreaPage.tsx:261`: «Открыть проект» drops the link's state, so focus stays (design § 9). A tab on a page with no object reads `localStorage` in render, so another tab's choice flips it on the next re-render with no reload (`useNavigationSource.ts:205`); harmless, not "for a reload".

### Asked, and clean

- Render-time navigation: none; every `navigate` is in an effect or a handler. Loops: none; each `raise` drops the value that caused it, and deps go by keys.
- Not found while offers load: none (`:254`). No false not found from offers themselves: they are not filtered by user, and a failed read gives `offers` undefined, so the page stands. `…/projects/deactivated` reads as a gone prefix and answers nothing (`:256`).
- A link's address wins; a second tab's address wins in its tab; `remember` is sync before `navigate`, so «все» survives a reload (`.test.tsx:147`).
- Deactivated node: the read model carries `ProjectActive` on sub-areas too (`IAreaTreeReadModel.cs:25`); no hook follows `AreaPage.tsx:214`; `nodeUnoffered` writes nothing; a remembered E drops at the next page.
- `TreeRegistry`: `labelState` and an undefined `labelTo` are optional and additive, the two tests assert them, Enter selects a plain row. Same shape as `TruncatedText`'s `linkState`. `labelTo` has no guide or showcase block from mod-018; no new pattern, no finding.
- Back: `ContextHost.tsx:80` replaces the same entry, so the history is the same length, the notice does not refire and `focusHeading` is kept; `hash` is dropped and no link that carries a notice has one. `:130` replaces the entry of an unfit pair; an older entry with that pair answers not found, as ADR § 1 says, and writes nothing.

### Claims pass

- BR-NV-15, US-2: `NavigationReadSignatureTests.cs:14-20`, `INavigationReadModel` only. Holds; D-9.
- api-contract § 2 «Команды» keeps `GET …/teams`: `OrganizationTeamsPage` (round 1). Holds.
- mod-017 BR-OT-8, narrowed backlog: `narrowed.ts`; server reorder unchanged, no `.cs` in the delta. Holds; the same row D-2.
- ADR-0031 rule 5 mark: `routeInventory.test.tsx`. Holds.
- design § 5.2 project row «Общие»: `AreasPage.tsx:136`. Holds.
- mod-018 status, BR-AW-10; dialog text «не будет открываться»: `AreasPage.tsx:134`, `AreaPage.tsx:214`. Holds.
- `TreeRegistry` "Enter selects": `TreeRegistry.tsx:298-307`, `labelTo.test.tsx`. Holds.
- `useNavigationSource.ts:112-117` header: holds except D-1.
- US-7 line 25: `useViewFilter.ts:49`. Holds.
- Direction 2: not found for a query value: grepped `selects nothing`, `teamId`; mod-017 BR-OT-8 and two comments D-2; the feat-003, feat-028 and feat-040 hits are other dimensions or the project filter, which a prefix does not read. Deactivated node: grepped `только для чтения`; mod-018 `design.md:114,238` D-11. Remembered selection: grepped `remembered`; `workContext.ts:7` D-10. Unswept: US-4 for the project row, and mod-020's docs.

Not read: the delta's tests beyond those named; `ContextSelectors`. Run: one throwaway hook test (D-1), removed. Nothing opened in a browser.
