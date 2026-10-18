# mod-005-work-item-delete-restore

- type: module
- size: L
- branch: mod/005-work-item-delete-restore
- stage: complete
- epic: core/work-items
- deps: mod-003,mod-016
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-10-18, human in chat: OQ-1..OQ-4 A — «⭐ Участник и админ», «⭐ Ничего не строить», «⭐ Принять до feat-015», «⭐ Обычный диалог»)
gate-2: approved (2026-10-18, human in chat: design D-1..D-6 «⭐ Оставить все A»; ADR-0030 «⭐ Принять, все A»)
gate-3: pending

## Plan

The light route (the human's choice of 2026-10-18): analysis → one requirements review round → UX design + the recycle-bin ADR → gate-1 and gate-2 go to the human as one package → backend → frontend → one QA pass with the code review and the security pass → one fix round → at most one delta read → gate-3 → PR to main.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Light route: the human's choice of 2026-10-18.
- 2026-10-18, the human in chat: spec OQ-1 A (Участник and Администратор delete and restore, only the administrator purges), OQ-2 A (no recycle-bin page and no list filter; feat-003 brings the «Удалена» filter), OQ-3 A (the item keeps who, when and the held state; history entries come with feat-015), OQ-4 A (a plain destructive dialog, no typing); design D-1..D-6 A; ADR-0030 accepted with D-1..D-3 A.
- feat-028 (teams of the organization) was in flight and not in main when this line was built; BR-DR-9's project–team unlink test was owed by whichever of the two merges second. feat-028 merged first (PR #17); this branch took it on 2026-10-18 and wrote the test (plan-backend § 14).
- 2026-10-18 tech-012 m1 is closed by this line (the stored state is judged by `WorkItem.EnsureLive` before any value is applied, `DeletedItemIsReadOnlyEndpointTests.Patch_ADeletedItem_LiveStateAndAParent_Is409Deleted_NoLinkNoChangedAtMoves`); its queue line keeps m4 only — the orchestrator amends `docs/backlog.md`.
- 2026-10-18 taken as the recommended option A, for the human to confirm at gate-3: H-1/QA D-2 (an unknown deleter shows no «(неактивен)») and S-1 (the purge log line names the deleter and the deletion's time).

## Log

- 2026-10-18 [task-executor] `BASE=origin/main bash scripts/verify.sh` at e6b04126: RESULT green (backend 2858 integration + 2313 other passed, 17 skipped by live-Keycloak switches; frontend 2269 affected in 165 files + 590 guards; docs checks pass)
- 2026-10-18 [task-executor] left open, none blocks: DR2-1 (no pin tells InAnyState from Live), DR2-2 (a)(c), DR2-4, DR2-5 (docs and contract prose), CR-6, H-1/D-2, S-1; for gate-3: ADR-0028 rule 8 amendment (project row, hierarchy tier); E2E 27.5, 27.9, :135 and feat-028's list break, not run (NO)
- 2026-10-18 [code-reviewer] fix re-check of 07a6d87f (reader commit ddfeb512): approve; QD-1 closed, no new lock cycle; DR2-2 (b), DR2-3 closed; new minors DR2-4 (stale plan prose), DR2-5 (LockHierarchiesAsync missing from contract § 7); the "Carried" deadlock is QD-1, no bug line owed
- 2026-10-18 [backend-developer] QD-1 fixed (merge QA, major): unlink and team deletion take the project hierarchy lock before the owned items; race theory pins 4 variants; statements 11 -> 13; no contract change; verify.sh green; plan-backend § 14.3
- 2026-10-18 [code-reviewer] delta read 2 over the merge (16ac90d3, reader commit 8e812b28): approve; 0 blocker, 0 major; minors DR2-1 (weak any-state pin at BacklogRepository.cs:141), DR2-2 (stale lock-order prose, ADR-0028 rule 8 gap), DR2-3 (unlink x purge-child shape)
- 2026-10-18 [qa-engineer] QA of the merge (plan fb106bbb, tests e0076300, report 02c21feb): defects; QD-1 major (a parent write x an unlink or team deletion: 40P01, the clean-up answers 500), 0 minor; 17 race and surface tests added; every other meeting point covered
- 2026-10-18 [frontend-developer] merge check of 83c68c69: tsc, eslint, all 305 files green, no defect; 6 pins added (deleted card and Restore keep the owning team as stored; unlink/team-deletion counts are the server's, any state); plan-frontend.md § Merge
- 2026-10-18 [backend-developer] found, not fixed, main's own: a save that moves a link × an unlink or a team deletion can deadlock (40P01), reproduced; plan-backend § 14.3 "Carried"; same bug as QD-1, fixed above, no bug line
- 2026-10-18 [backend-developer] merge of origin/main (feat-028): 23 codes, 59 routes; unlink/deletion clear the default before the items (40P01 against the project reorder); unlink test written; contract wording only (§ 3.2, 3.7, 5), no wire change; plan-backend § 14
- 2026-10-18 [task-executor] main (feat-028, PR #17) merged at 2aaed5b6: 8 textual conflicts (2 other tasks' status.md, 4 backend access tests, 2 frontend role fixtures), both intents kept; main's later commits are queue files only, merge-tree clean
- 2026-10-18 [task-executor] wrap: `BASE=ce7f055b bash scripts/verify.sh` RESULT green (backend 2491 passed, frontend 2149 + 590 guards). The earlier red was ProjectBacklogVolumeTests under load: green 4 of 4 alone, reorders ~245 ms of 1000.
- 2026-10-18 [task-executor] left open, none blocks: H-1/D-2 and S-1 (taken as A, for the human); H-2 E2E 27.5 and 27.9 break, not run or edited (standing NO); BR-DR-9 unlink test written at the feat-028 merge; minor CR-6 (docs wording)
- 2026-10-18 [code-reviewer] delta read 1 (b5d50744): approve; CR-1 resolved; 0 blocker, 0 major open; one new minor CR-6 (backend.md raw-read wording, a repository comment), a debt
- 2026-10-18 [backend-developer] fix round (37cb9e80 CR-1, 5003924f CR-2, b5d8c265 CR-3, 3cb68680 CR-4, 5051042d CR-5, 4069a67d S-1, 3a328529 QA D-1, 267ab14a resolutions): all fixed, contract notes in plan-backend.md § 13
- 2026-10-18 [code-reviewer] security pass (8067f009): approve; 0 blocker, 0 major; S-1 minor (the purge erases the only record of the deleter), option A recommended
- 2026-10-18 [code-reviewer] code review round 1 (143b2478): changes-requested; CR-1 major (backend.md overstates "no read skips the choice", prose only), CR-2..CR-5 minor
- 2026-10-18 [qa-engineer] one pass, both layers (plan 512889f6, tests f8b6183b, report 23d3da40): pass; 0 blocker, 0 major; minors D-1 (Restore binds its body first, contract wording), D-2 (unknown deleter, for the human)
- 2026-10-18 [frontend-developer] stage 6 (plan 8fba3000, texts d789d776, wire 0e141562, dialogs 7924c5db, page/host/list/boards 078537e3): «Удалить»/«Удалить навсегда» in «⋮», three dialogs, deleted card; contract unchanged; breaks E2E 27.5, 27.9 (not edited)
- 2026-10-18 [backend-developer] stage 4 (687f01c4..f5cd2719): migration `AddWorkItemDeletionMarks`, three POST routes, `deletion` on WorkItem, `WorkItemSets` entry points, project reorder member lock, purge step contract; deviations in plan-backend.md § 12
- 2026-10-18 [backend-developer] plan-backend.md + api-contract.md (614a9b15): three POST sub-routes, `deletion` on WorkItem, entry points with parity/matrix/inventory tests; contract fixed from here (a change needs a note here)
- 2026-10-18 [orchestrator] the human answered gate-1 and gate-2 in chat (0ebe23ef): every OQ and D-n A, ADR-0030 accepted; backend starts
- 2026-10-18 [orchestrator] the gate-1 + gate-2 package goes to the human; no code before the answer.
- 2026-10-18 [task-executor] `bash scripts/verify.sh` at 12e5e574: RESULT green (links, backlog ids, status budget, spec shape).
- 2026-10-18 [backend-developer] docs/adr/0030-work-item-recycle-bin.md, proposed (12e5e574); references ADR-0026, amends ADR-0029 § 4; D-1..D-3 open, A recommended, for the human at gate-2. No code under src/.
- 2026-10-18 [ux-designer] design.md (8e269de4): no prototype, no new kit part; D-1..D-6 open, A recommended, for the human at gate-2.
- 2026-10-18 [requirements-reviewer] round 1 (68954630): approved-by-ai; R-1..R-13 all minor (0 blocker, 0 major), resolved in place by the reviewer; no new question.
- 2026-10-18 [business-analyst] spec.md + US-1..US-6 (15c20a32): BR-DR-1..18; OQ-1..OQ-4 open, A recommended, for the human at gate-1.
- 2026-10-18 [orchestrator] route L, light — confirmed by the human in chat («⭐ L, облегчённо»); gate-1 and gate-2 go to the human as one package after the design; E2E: no.
- 2026-10-18 [new-task] task started (scripts/new-task.sh; its worktree was removed so a worktree-isolated agent could take the branch)

## Notes

Summary (from the queue line or an argument): MVP. Founds the soft-delete pattern: delete → «Удалена», restore to the previous or a chosen state, the organization administrator's purge (doc-010 D-13), doc-001 BR-14 for the surfaces and writes that exist when it ships, no cascade (BR-M3-9); re-runs feat-001's US-7 (deleted items and links, enforced today through the Removed category) through its own Delete (feat-001 D-8 OQ-1); owes the recycle-bin ADR, which references mod-002's soft-deactivation ADR
