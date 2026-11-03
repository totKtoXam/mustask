# feat-048-aggregate-level-views

- type: feature
- size: L
- branch: feat/048-aggregate-level-views
- stage: 04-backend
- epic: platform/ui
- deps: feat-043
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved 2026-11-03 (the human, in chat): OQ-1..OQ-6 A, the spec index and its Delta as written
gate-2: approved 2026-11-03 (the human, in chat): the design with groupings, the prototype, ADR-0036's direction, D-1..D-6 A, with one change: the group of rows with no department or no team goes first (BR-AV-4 amended)
gate-3: pending

## Plan

<!-- L: see plan-backend.md / plan-frontend.md -->

## Decisions

- Gate-1 questions, 2026-11-03, the human in chat, one by one: OQ-1 A (auto-retry by Retry-After, then «Повторить»), OQ-2 A (limiter on group and cell reads only, keyword read an accepted risk), OQ-3 A (4 per tab, 6 per user with a short wait queue, keyed by the Keycloak subject per API process), OQ-4 A (cursor 400 reloads, refusal keeps rows, deactivated project drops its group), OQ-5 A (filter parameters on the reads now, no filter bar on aggregate board/backlog until feat-013), OQ-6 A («В конец» is the project backlog's end, «Ниже» on the last loaded row disabled). Gate-1 approved right after.

- Gate-2 questions, 2026-11-03, the human in chat after the prototype was sent to them: D-1 A (the Q-9 hint once under the page header), D-2 A (per user 6 + queue 6, global 40 + queue 20, 3 s wait, 429 Retry-After 2), D-3 A (a card dropped into a part-loaded cell re-reads that cell from its first page), D-4 A (the «Команда» column stays at team level). Gate-2 verdict itself asked separately.

- The human, 2026-11-03, before the gate-2 verdict (their words, translated): «let us make several display views: 1. no grouping; 2. grouping by organization, if possible; 3. by department, if possible; 4. by project, if possible; 5. by team, if possible». Asked how, the human chose: the switch is on «Бэклог», «Канбан» and «Рабочие элементы»; outside the by-project grouping a backlog's rows go by change time with no reorder (the Q-9 hint), the by-project grouping keeps each project's rank and the in-group move; a level offers only the groupings that give more than one group (organization: none, department, project, team; department: none, project, team; team: none, project; project: none, team; «by organization» never, since every view is inside one organization); done inside feat-048 as a spec amendment and design rework, then gate-2 again. Gate-2 is not approved; D-1..D-4 stand unless the rework changes them.

- Grouping amendment, 2026-11-03: spec.md BR-AV-1..12 and US-11 (business-analyst, ade3821a); P-10 note added to feat-043's status.md Log. The human answered OQ-7 A (team-level default: none without a department, by project with one), OQ-8 A (no grouping at project and team level keeps today's order: the project's rank and move, feat-040's team order), OQ-9 A (a grouped «Рабочие элементы» has no sorting; the flat list keeps it).

- Gate-2 questions after the rework, 2026-11-03, the human in chat: D-5 A (four new work_items indexes and one replaced, in one migration, proved by the 1 000 000-item test; a miss returns the task to gate-2, the budget is not relaxed), D-6 A after an explanation (flat rows with no indent in every grouping but by project). Gate-2 verdict (their words, translated): «option 1, but let the grouping of items without an organization/department/team/project be on top». Applied: «Без департамента» and «Без команды» go first, not last (spec BR-AV-4, US-11's two scenarios, design § 2's table, the prototype's sort and the board hint); every item has an organization and a project, so only those two groups exist. ADR-0034 → ADR-0036 renumber (0034 is tech-025's, 0035 feat-033's; the orchestrator's request, fa1112e8) told to the human with this gate.

## Log

- 2026-11-03 [orchestrator] task started in the worktree .claude/worktrees/mod-018-build-9e3722 (app-made; branch feat/048-aggregate-level-views cut from origin/main 41d784ee; new-task.sh not used because the session is already bound here; the app's sync tool refused, origin not pinned). Route confirmed by the human: feature, L, not the queue's M (threshold: an ADR is owed for the group reads' cursor, which deviates from ADR-0014/0015's page envelope, and for the server-side read limiter of feat-043 design § 12). Full route, gates 1/2/3 the human's; the spec is reused: spec.md indexes feat-043's US-6, US-10, BR-NV-12, BR-NV-20, approved at feat-043's gate-1 and gate-2, and gate-1 covers only what this line adds. Block 3.3: only the queue line. Economy mode; E2E no. Queue: `queue.sh take` PR #52 (auto-merge on). Usage at start: weekly 17%, 5-hour 5%.

- 2026-11-03 [orchestrator] Stage 1 done (business-analyst): spec.md as an index into feat-043's folder plus a Delta of OQ-1..OQ-5 (429 handling, limiter scope, per-tab cap against the per-user limit, failed cursor reads, the project list's «Команда» control left by feat-043 against BR-NV-12); no new story. Deps checked merged on origin/main by the orchestrator: feat-003 #32, mod-006, mod-017 #11, mod-020, feat-040 #31, feat-043 #49. verify.sh green.

- 2026-11-03 [orchestrator] Stage 2 (requirements-reviewer): needs-human-decision, 0 blockers, 5 majors, 6 minors (review-requirements.md, ce891fad); faithful to feat-043's approved text. Fix round (business-analyst, 8594e016): R-1..R-11 closed in spec.md; the old OQ-5 (project list's «Команда») became a stated requirement per BR-NV-12/Q-6 A; open for gate-1: OQ-1..OQ-6 (refusal UX, limiter coverage, per-user limit and queue, recovery after page 1, filters while feat-013 is unmerged, moves in a part-loaded group). Owed: a 429 entry in docs/error-codes.md and contracts/errors. At gate-1.

- 2026-11-03 [orchestrator] Gate-1 approved by the human. Stage 3 (ux-designer) started: design delta, the cursor and limiter ADR, the 429 catalogue entry.

- 2026-11-03 [orchestrator] Stage 3 done (ux-designer, b77b9503): design.md delta, prototype/index.html (9 scenarios), ADR-0036 proposed (cursor-paged group reads, per-user 6 + queue 6 / global 40 + queue 20, 3 s wait, 429 Retry-After 2). New parts: ProjectGroup and useGroupRead in features/boards, no kit part. Correction: GET …/organizations/{o}/workitems does not exist yet, design § 6 adds it. Prototype opened by the orchestrator in the browser pane: all 9 scenarios, part-loaded menu shows «Ниже» disabled with «Сначала нажмите «Показать ещё»», retry and refusal copy, deactivated group toast, no console errors; the cursor reload is silent by design. Open for gate-2: D-1..D-4. At gate-2.

- 2026-11-03 [orchestrator] Grouping rework: spec amended (BR-AV-1..12, US-11, OQ-7..9 A, see Decisions); design reworked (ux-designer, 964914e4): «Группировка» is the kit's MenuButton in the PageHeader bar, ProjectGroup renamed ViewGroup, reads take `grouping` and `group`, the cursor has two order keys (rank, change time), ADR-0036 updated. Budget check reasoned, not measured: four new work_items indexes and one replaced, one migration, proved by the 1 000 000-item test (D-5). Prototype opened by the orchestrator: backlog switch (four options at organization level, by-team shows the change-time hint and no move menu), board lanes, flat list with pager vs grouped, team default none without a department and by project with one, project + team shows no switch; no console errors. Seen: the project + team backlog shows no Q-9 hint in the prototype; the shipped feat-043 behaviour stands for it. Open for gate-2: D-5, D-6. At gate-2 again.

- 2026-11-03 [orchestrator] Gate-2 approved by the human with one change (the no-department and no-team groups go first), applied to spec BR-AV-4, US-11, design § 2 and the prototype; D-5 A, D-6 A. Stage 4 (backend-developer) started.

- 2026-11-03 [backend-developer] Stage 4 in progress: api-contract.md and plan-backend.md written; reads, cursor, limiter, migration and tests committed; contract departures (flat by-project rows, `column` key, no level on workitems routes) listed in api-contract section 4. The 1M-item budget test misses 2 s on the flat list deep page and some first-page reads (see report); not relaxed.

## Notes

Summary (from the queue line): «Рабочие элементы», «Канбан» and «Бэклог» at the organization and department level and «Рабочие элементы» at the team level: the organization-wide work-item list with feat-003's filters and keyword rules, and the aggregate backlogs and boards of every active project (department: the projects placed in it) as one collapsible group (swimlane on the board) per project in the project's own rank order, with a move inside a group and rows loaded per project as a group opens (reads capped at 4 in flight per tab, paged by a cursor), with the additive reads and the budgets of BR-NV-12 and BR-NV-20. Spec and stories US-6, US-10 live in feat-043's folder (its OQ-9); takes feat-042's board and backlog part, BR-NE-8's cross-project list and mod-020 CR-2's test.
