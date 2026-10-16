# feat-032-board-card-checklists
- type: feature
- size: M
- branch: feat/032-board-card-checklists
- stage: complete
- epic: core/backlogs-boards
- deps: mod-006,feat-027
- gates: gate-1, gate-2, gate-3
## Gates
gate-1: approved 2026-10-12 (human, in chat; one package with gate-2)
gate-2: approved 2026-10-12 (human, in chat; with D-5)
gate-3: approved 2026-10-14 (human, in chat, via AskUserQuestion; no remote — local squash by finish-task.sh)
## Plan
- Backend, read side only: `GetBoard` slice + `IBacklogReadModel.ReadChecklistsAsync` — one statement for all cards (direct children in the project, not Removed, appearance task/bug/testcase, any Область, by number); assignees through the directory in batches of 500.
- Domain: `AppearanceToken.Testcase` ("testcase"), `CardChecklist.Kinds`. No migration (varchar(16) fits).
- Contract (api-contract.md): each board card gains `checklist` (always present, `[]` when empty) — not `children`, which feat-027 pins as absent on a card. No new routes or codes: tick/untick = mod-003 PATCH `{stateId}`, «Добавить задачу» = feat-027 create with `parentId`.
- Frontend: `CardChecklists` component in the boards slice (counters, lists, add field, drag guard), open-state in `BoardColumns`, `testcase` flask in the type marks, `AssigneeAvatar` exported from workitems — per design.md.
- Owed at gate-3: superseded-in-part notes on mod-006 § 2.3/§ 3.3, BR-M4-14 and the «Доски» card row, and on mod-001's token list.
## Decisions
- D-1 (human 2026-10-12, in chat, with a screenshot of the reference tracker's board card, `research/reference-card.png`): «В карточках work item-ов отображаются их таски и баги как чеклисты и их можно сворачивать/разворачивать по нажатию по ним; отобрази на кого назначено». The screenshot: a user story card (type icon, number, title, «…»; the state with its dot; the assignee's avatar and name; «Story Points») and below it (1) a row of counters — a clipboard «4/5» (tasks done/total), a bug «1/1», a flask «1» (test cases, a count only), each a toggle that expands its own checklist; (2) the expanded checklist: «+ Add Task», then one row per child — a checkbox (checked when done), the type icon, the assignee's avatar, the title as a link, «…» on hover; a collapse chevron at the bottom. Asked through AskUserQuestion, the human chose the full scope «как на скриншоте»: the counters and toggles, a row's checkbox closes or reopens the child (the existing state change), the assignee avatar on each row and on the card, «Добавить задачу» creating a task beneath the card's item (feat-027 create-beneath).
- D-2 (orchestrator 2026-10-12): the board answer gains the card's children (additive, no breaking change). Test cases come from feat-031 (a type on the «Задачи» level, a child of a story); the checklist groups children per type, so they appear once feat-031 is in main — this task does not wait for it. feat-029 (in flight) rewrites `src/frontend/src/features/boards/components/BoardColumns.tsx` (the card's title link opens a dialog): whichever merges second rebases.
- D-3 (human 2026-10-12, in chat through AskUserQuestion, on review round 1): OQ-1 — the lists start collapsed, only the counters show; OQ-3 — untick writes «Новая» (the analyst's option); R-20 — «Трудоёмкость» on the card is not in this cut, it is its own queue line feat-034-board-card-effort; R-1 — the test-case appearance token is fixed here as `testcase` (feat-032 adds the flask icon), and feat-031 must adopt the same spelling.
- D-4 (orchestrator 2026-10-12, per the reviewer's advice): OQ-2, OQ-4, OQ-5, OQ-6 take the spec's recommended option (a); OQ-2 reworded per R-5, OQ-4 states the divergence from feat-027 BR-T-4 per R-6.
- D-5 (human 2026-10-12, at gate-1+gate-2): a counter's accessible name starts with its visible text, e.g. «1/3 — MSK-US-2, Задача: выполнено 1 из 3» (WCAG 2.5.3, the designer's note).
## Log

- 2026-10-14 [frontend-developer] merge of main 734f316: BoardColumns re-applied on feat-029's structure; thin icons moved to shared/ui; main's kitGuard forced kit parts (ActionButton, IconAction, InlineTextField, UserAvatar) — the add field's metrics and the add glyph now follow the kit, slightly different from design § 4/5; names/aria unchanged; verify.sh green except the known BacklogTreeVolume flake (passes alone)
- 2026-10-14 [orchestrator] gate-3 approved by the human in chat; repository has no remote, so the ending is scripts/finish-task.sh (local squash). Debts for the queue (NNN): m-2 Firefox draggable fallback, m-3 200-child open time unmeasured, m-4 WorkItemField.Of(Testcase) throws (feat-031), m-8 CardChecklist.Holds unused, d-1/d-2 spec wording
- 2026-10-13 [orchestrator] full verify.sh on 450abb9 (no E2E): vitest 158 files/2013 tests green, lint and build ok, backend 1599 passed; 2 BacklogVolume timing tests (5000 items, 2 s budget; not feat-032 files) failed under load from other sessions and passed alone (2/2, 24 s) — RESULT failed formally. Flaky budget on shared machine, noted for the human at gate-3. Stage: gate-3
- 2026-10-13 [orchestrator] code review delta read: approved-with-minors (F-1, F-2 closed; open debts m-2, m-3, m-4, m-8, d-1, d-2). Final full verify.sh NOT completed: stopped at the request of the tech-028 session (machine at 100% CPU; full frontend runs queue until its all-clear). Last green: frontend developer's run before 76352c4, QA's run at 85632a0; the F-1 fix carries its own passing test. Owed: one full verify.sh after the all-clear, before gate-3
- 2026-10-12 [orchestrator] stopped at the human's word in the middle of review fixes: docs fixes abbfa7f done; F-1 fix 76352c4 committed, its verify.sh run killed before finishing. To resume: verify.sh, then one code-review delta read of F-1/F-2, then gate-3
- 2026-10-12 [orchestrator] code review round 1 (6475cd9): needs-fixes, 0 blockers, 2 majors (F-1 every card re-renders per drag event; F-2 BR-C-11 claims a feat-031 queue-line record), 8 minors. F-1, m-6, m-7 to the frontend developer; F-2, m-1, m-5 docs to a task-executor; ADR-0024 untouched (its § 3 lets the token set grow); QA D-4 sent to the feat-031 session
- 2026-10-12 [qa-engineer] pass, no E2E: 0 blockers, 0 majors, 5 minors carried to gate-3 (qa.md); tests 85632a0, report eea4420; verify.sh green. Next: 08-code-review
- 2026-10-12 [frontend-developer] cf0e7e9, d08194d, 35089cf, c4ea0e6, f99161f: flask token, AssigneeAvatar, checklist model, CardChecklists UI, one fix; verify.sh green (no E2E). Deviations listed in its report: checkbox disabled until the process loads; no Firefox fallback. Next: 07-qa, no E2E
- 2026-10-12 [orchestrator] the human, in chat, to speed up: QA runs without the full E2E run (unit + integration only); QA and code review stay sequential
- 2026-10-12 [backend-developer] f715ec4, c5b1763, 71994bf: board card `checklist` + `testcase` token; 23 integration tests incl. a volume test (5000×5 in ~0.9 s); verify.sh green. Known: `WorkItemField.Of(Testcase)` throws — feat-031's. Next: 06-frontend
- 2026-10-12 [orchestrator] at the human's word, sent the `testcase` spelling (D-3, R-22) to the feat-031 session
- 2026-10-12 [orchestrator] gate-1 + gate-2 approved by the human in chat, with D-5 (the counter's name starts with «n/m»); spec and design amended for D-5; next: 04-backend
- 2026-10-12 [ux-designer] design.md + prototype/card-checklists.html; 12 decisions the spec left open (design.md decisions table); one note: the counter's name does not contain its visible «n/m» (WCAG 2.5.3). gate-1+gate-2 package put to the human
- 2026-10-12 [orchestrator] requirements review round 2: R-1..R-20 resolved; R-21 (major) and R-23 fixed by the analyst → approved-by-ai; R-22 to the human; stale tick on a removed child deferred to mod-005 (BR-C-7). Detail in review.md

- 2026-10-12 [business-analyst] rework after review round 1: R-1..R-20 applied, none declined; open questions none

- 2026-10-12 [orchestrator] review round 1: needs-human-decision, 0 blockers, 6 majors, 14 minors; the human answered OQ-1/OQ-3/R-20/R-1 (D-3), the rest D-4; queued feat-034-board-card-effort in main (3be988b); the spec goes back to the analyst for R-1..R-19

- 2026-10-12 [business-analyst] spec.md + US-1..US-3 written; OQ-1..OQ-6 open for the human. Note for gate-3: mod-006's «Доски» card row and BR-M4-14 get a superseded-in-part note

- 2026-10-12 [orchestrator] route confirmed by the human in chat: feature M — an additive field in the board answer and the card in the existing boards slice, ≤ 3 stories; gate-1+gate-2 as one package, one QA pass, gate-3. D-1, D-2
- 2026-10-12 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-032-board-card-checklists

## Notes

Summary (from the queue line or an argument): Kanban card children as checklists like the reference: counters (tasks done/total, bugs done/total, test cases count) that each expand their list; a row's checkbox closes/reopens, type mark, assignee avatar, title link; «Добавить задачу»; the assignee on the card (the human, 2026-10-12)
