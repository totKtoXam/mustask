# feat-043-area-project-team-navigation

- type: feature
- size: L
- branch: feat/043-area-project-team-navigation
- stage: gate-3
- epic: platform/ui
- deps: feat-040,mod-018,mod-020
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved 2026-10-30 (the human, in chat): all recommendations, OQ-1..5, OQ-7..9, H-1..4 A
gate-2: approved 2026-10-30 (the human, in chat): design, prototype, ADR-0032 direction; rejected once earlier the same day, see Decisions
gate-3: pending

## Plan

<!-- L: see plan-backend.md / plan-frontend.md -->

## Decisions

- Gate-1, 2026-10-30: OQ-1, 2, 3, 4, 5, 7, 8, 9 and H-1, 2, 3, 4 are answered A; the spec is the recommended text. tech-019 is absorbed; a new ADR is owed at the design.

- Gate-2 rejection, 2026-10-30, the human on the navigation structure (their words, translated): (1) the header is a trail of four selectors, each showing «все» or my organization / my department / my team / my project, each choosable; (2) having to drop into «Проекты» and pick a project to reach Общие, Рабочие элементы, Канбан, Бэклог, Команды, Настройки is inconvenient: all of these belong in the left menu as before, its content changing with what is chosen (organization, area, department, project or team); (3) «Настройки» stay where they were. Reopens the spec's context and switcher rules and D-1, D-3; gate-1 answers stand otherwise.

- Q-2, Q-3, Q-4, Q-5 answered A by the human, 2026-10-30; Q-1 A taken by the orchestrator as technical (the most specific object takes the address prefix, the others ride in the query, the form mod-020 founded). The spec stands as written for A.

- The human, 2026-10-30, at gate-2 (their words, translated): «Канбан» and «Бэклог» must be on all levels, not only the team; «Рабочие элементы» too, not only the project. Asked how: the organization and department boards and backlogs are built IN feat-043 (scope grows: feat-042's area board and backlog part moves here, plus an organization level no line had; feat-042 keeps the overview figures, queue rewording owed). D-7, reset reachable: a selector lists fitting values first, then a group «Остальные»; choosing from it resets what no longer fits with the notice. This replaces Q-3 A for item views and Q-6 A/C: every level has the three sections.

- The human, 2026-10-30, on the all-level scope: OQ-9 split, two PRs in a row: this line carries the navigation, selectors, pages and retirement (US-1..5, 7..9), L; a second line, M, right after it, carries the views across levels (US-6, US-10), and their entries at the organization and department levels are absent until it lands. Q-7 B: an aggregate backlog shows each project's own order, the rows of different projects interleaved, against the analyst's recommendation (A, no order at all). Q-8 A: the four levels only, no views of a sub-area or a project's node; «Включая дочерние области» stays with feat-042's overview. Q-9 A: a narrowed project or team backlog keeps the rank order and offers no reorder.

- The human, 2026-10-30, answering Q-10 and Q-11 in their own words, outside the offered options: «группировать их в аккордионы по проектам» and «можно раздробить запрос на параллельное выполнение». An aggregate backlog is grouped into one accordion per project instead of the interleave, each keeping its project's order; the read may be split into parallel per-project reads. The orchestrator's reading, to confirm at gate-2: a move is allowed inside an accordion, since a group is one project's rank order and the objection to Q-10 B falls away, under the same code as the project's backlog and Q-9 A.

- The human, 2026-10-30: the in-group move is confirmed. Q-12 A: the aggregate board is one collapsible swimlane per project. Q-13 A taken by the orchestrator as technical, following the human's suggestion of a cursor: a cursor for the group reads and board cells only, the page envelope stays everywhere else, a short ADR or amendment note owed by the second line.

## Log

- 2026-10-30 [orchestrator] task started in the worktree .claude/worktrees/feat-043-area-project-team-navigation-b716b1 (app-made; branch renamed, based on origin/main cc73b2be; new-task.sh not used because the session is already bound here). Route confirmed by the human: feature, L (threshold: breaks ADR-0024 § 2, a new ADR; retired addresses and moved pages), full route, gates 1/2/3 separate; block 3.3: only the queue line. Economy mode; E2E no; no local VERIFY_FULL. Usage at start: weekly 2%, 5-hour 2%.

- 2026-10-30 [orchestrator] queue: PR #41 reworded the lines after mod-018 (merged by the human), PR #42 takes the line (`queue.sh take`). Stage 1 done (business-analyst): spec.md, US-1..US-9, OQ-1..OQ-9. feat-003 and mod-005 are already merged, so BR-NE-8 makes this line the second to merge and US-6 carries their work; US-6 resolves mod-020 CR-2; tech-011 m4 is recorded obsolete on the queue line after the merge.

- 2026-10-30 [orchestrator] Stage 2 (requirements-reviewer): needs-human-decision, 2 blockers, 14 majors, 9 minors. Fix round (business-analyst) closed them in the spec and stories, except R-16 (feat-038 unmerged at stage 1: no dependency) and part of R-25 (a project moved by someone else while the dialog is open). No delta read. Written for the recommended answers of OQ-1..5, 7..9, H-1..4; the 5 s keyword budget (H-4 A) is the analyst's, from no source. Owed after gate-1 if OQ-5 A: tech-019 withdrawn into this line, tech-020 takes feat-043 as a dep.

- 2026-10-30 [orchestrator] Stage 3 (ux-designer): design.md, prototype/index.html (syntax checked only, not opened in a browser), ADR-0032 proposed. Adds kit parts AppTopBar, NavList, SearchMenu. One additive read family: GET /api/organizations/{o}/workitems and …/workitems/assignees. Owed: tech-020 takes feat-043 as a dep (queue); US-9 marks ADR-0024 §§ 1, 2, 4, ADR-0028 rule 7 and ADR-0031 rule 5 with a pointer to ADR-0032; the plan pins statement counts for «Обзор» and the lists and keeps the retired-address and route inventories as one source a test reads.

- 2026-10-30 [orchestrator] Spec reworked to the human's navigation requirement (business-analyst): BR-NV-1, -10, -17, -18, BPMN, US-1, 3, 4, 5, 8; Q-1..Q-5 open. Unchecked by the analyst after the change: US-2, US-6, US-7 bodies and spec.md Operation completeness, OQ-1 wording. The design, prototype and ADR-0032 §§ 1, 2, 5, 6 are owed a rework.

- 2026-10-30 [orchestrator] Design, prototype and ADR-0032 reworked (ux-designer); spec conflicts closed (business-analyst), Q-6 open. Prototype opened by the orchestrator in a browser: selectors, project menu, settings navigation, 13 links, no console errors. Found: BR-NV-17's offers make US-8's reset unreachable from a selector (D-7).

- 2026-10-30 [orchestrator] Scope rework: spec (business-analyst) split per OQ-9, feat-048 queued for US-6 and US-10 (PR #44, merged); aggregates are per-project groups, Q-7..Q-13 closed. Design, prototype, ADR-0032 reworked (ux-designer); the human asked for drag and drop in place of move arrows, the mechanism is the native one in src/frontend. Prototype opened by the orchestrator: levels, selectors, «Остальные» reset, row and card drags, a cross-lane drop stays put (the refusal notice itself not seen), no console errors. Design owes feat-048 a server-side read limit: 4 per tab is a browser promise, Npgsql default pool 100, no limiter in the API. design.md has 35 bytes of headroom. Gate-2 approved.

- 2026-10-30 [orchestrator] The doc-012 session relayed the human's two asks after gate-2: reuse first, and the reference tracker's look. Design pass: AppTopBar and NavList dropped for the existing TopBar and MainNavigation, SearchMenu the one new part, shell-local; prototype moved to the tokens' look, no pixel comparison (screenshots are outside the repo). Stage 4 (backend-developer): two additive reads, GET …/overview, …/selector-offers, no migration. Orchestrator's calls: three existing list reads stay without CallerAccess, feat-041 moves them; organizations.organization.read stands until mod-019. Go for local tests from the doc-012 session; the app's sync tool refused (origin not pinned), main merged by hand (8f009670). Queue edits owed: PR #45 open.

- 2026-10-30 [orchestrator] Stage 5 (qa-engineer): backend pass, 0 blockers, 0 majors, 3 minors carried to gate-3 (D-1 unordered id sets, D-2 no timing test at 1 000 areas, D-3 plan text). Affected tests green. No E2E (standing NO).

- 2026-11-02 [orchestrator] Stage 6, plan steps 1-8 and 13 done (frontend-developer, two runs): selectors in the TopBar, menu from addressModel, routes per ADR-0032, switcher retired (eae3b635), Breaks notes in 10 owners' status.md (88236412). Whole-suite Vitest not re-run as one run after the last fixes; the next run owes it. Orchestrator's reading of ADR-0032 § 6, to name at gate-3: an address carrying an organization fully sets the selection and the remembered one applies only to pages with no organization, otherwise «все» could not be chosen on the organization's own pages. Queue PR #45 still open. Weekly 8%.

- 2026-11-02 [orchestrator] Stage 6 done (frontend-developer): steps 9-12 and 14, HEAD 912cb4fd. Whole Vitest suite as one run: 334 files, 5116 passed, 6 skipped, 0 failed; lint 0 errors; tsc clean. PR #45 merged. Not done, to name at gate-3: the kit-guard allow-list entries for AppLayout, MainNavigation and UserMenu stay, each needs a new kit part (a tech-019 remainder, absorbed here by OQ-5 A). The developer's deviations are in its commits; the second selector row scrolls sideways rather than wraps. Local test lane is shared with tech-024 through the doc-012 session: a full run or code push needs its go.

- 2026-11-02 [orchestrator] Stage 7 (qa-engineer), round 1: not ready, 1 blocker, 2 majors, 2 minors, 52 tests added, 3 `it.fails`. F-3 blocker: «Настройки» clears the selection, the object's «Параметры» reachable only by address. F-1: an address naming a gone object overwrites the remembered selection. F-2: narrowed project backlog has no hint or tag and still offers the move. Returned to frontend-developer.

- 2026-11-02 [orchestrator] F-1..F-4 fixed (frontend-developer, 2fb50a5c). Orchestrator's calls: «Настройки» carries the selection in the address; the new query `projectId` is written into ADR-0032 §§ 1 and consequences. mod-017's test of reordering a filtered project backlog is replaced: Q-9 A changes that behaviour, to name at gate-3. Open for round 2: tsc error in OrganizationOverviewPage.qa.test.tsx. From a project's «Параметры», «Команда → Параметры» drops the project per ADR-0032 § 5, left as is.

- 2026-11-02 [orchestrator] Stage 7, round 2: pass, 0 blockers, 0 majors; F-6, F-7, F-8 minors fixed at once (6c997513). F-5 stays open: second selector row at 640 and 1023 px not seen in a browser. Nothing of the frontend has been opened in a browser; E2E standing NO. Full Vitest after the fixes owed before the push.

- 2026-11-02 [orchestrator] Stage 8 (code-reviewer, Opus): blocked-on-decision, 0 blockers, 8 majors, 8 minors, no security defect (34b6cec9, review.md). review.md is at the size limit. Calls: C-3, asked, the human chose A: a project row in «Области» opens the project's «Общие», as OQ-1 A, the design is corrected. C-5: the orchestrator's reading in the 2026-11-02 log entry was wrong and is withdrawn: US-8 and the BPMN, approved at the gates, say an organization page with no object reads the selection from the address, else the remembered one; «все» stays choosable because a choice writes the remembered selection. Code follows the approved text. C-6: the code follows ADR-0032 §§ 1, 3 and design § 2, a gone or non-fitting query value answers not found. Neither C-5 nor C-6 changes an approved text, so neither went to the human. Returned to frontend-developer.

- 2026-11-02 [orchestrator] Review findings fixed (frontend-developer, 794930ea). api-contract.md § 2 prose corrected for C-15, no shape change. C-9 fixed by the orchestrator (06464fe9): the organization and project deactivation dialogs say «селекторов», and the project's dialog says its node stops opening; this replaces mod-018's confirmation text in design.md:238, whose page behaviour OQ-3 A changed. C-14: the feat-040 project filter on a team view stays, as US-5 and the code; US-7 corrected. Review round 2 on the delta.

- 2026-11-02 [orchestrator] Stage 8, delta read 1 of 2 (code-reviewer, Opus): blocked-on-decision, 1 blocker, 5 majors, 6 minors (f050e1d3); no new read or endpoint. D-1 blocker: «Без команды» (`?teamId=none`) answers «Команда не найдена», caused by the C-6 fix. D-3 asked, the human: choosing an organization in the selector opens its remembered selection, as US-8; BR-NV-10 is amended. Returned to frontend-developer.

- 2026-11-02 [orchestrator] D-1..D-12 fixed (frontend-developer, 0aa167cd); mockRegistry failure of the full run at c28080fc fixed (99dadd41). Open: `state` on PageHeader and MenuButton has no kit guide or showcase entry. Delta read 2 of 2.

- 2026-11-02 [orchestrator] Stage 8, delta read 2 of 2: approve, 0 blockers, 0 majors, 4 minors as debt, E-1..E-4 (44042575). Kit guide and showcase entries added (8c5e8647); TreeRegistry has no showcase entry, its section list is pinned until tech-016. Waiting for the shared lane for the last full Vitest, then gate-3.

- 2026-11-02 [orchestrator] Full Vitest at e527fce6: 342 files, 5265 passed, 6 skipped, 0 failed. Not run: E2E (standing NO), any browser check of the panel (F-5 and the header layout unseen; the human's stand is off limits). Weekly 16% (start 2%). At gate-3. The human, away from the PC, asked for screenshots before the verdict; a throwaway harness on test data takes them, the stand stays untouched.

## Notes

Summary (from the queue line): Top-level «Области», «Проекты» and «Команды»; the object pages with their sections, a section absent until the line that owns it adds it; the organization's «Обзор» as its home page, the user's organizations beside the content (feat-045 folded in); the «Рабочие элементы» list across projects; breadcrumbs with no false hierarchy; the switcher and the remembered context; empty states. A retired address answers «Страница не найдена», nothing redirects, and the interim pages of the earlier lines (the team and area pages in the organization settings, the project's teams, area links and default team in its settings) move onto the objects' own pages (doc-012 BR-MX-27, -28; US-10)
