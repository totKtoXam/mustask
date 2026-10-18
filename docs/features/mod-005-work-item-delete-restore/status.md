# mod-005-work-item-delete-restore

- type: module
- size: L
- branch: mod/005-work-item-delete-restore
- stage: 04-backend
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
- feat-028 (teams of the organization) is in flight and not in main: this line builds on main as it is; BR-DR-9's project–team unlink test is owed by whichever of the two merges second.
- 2026-10-18 tech-012 m1 is closed by this line (the stored state is judged by `WorkItem.EnsureLive` before any value is applied, `DeletedItemIsReadOnlyEndpointTests.Patch_ADeletedItem_LiveStateAndAParent_Is409Deleted_NoLinkNoChangedAtMoves`); its queue line keeps m4 only — the orchestrator amends `docs/backlog.md`.

## Log

- 2026-10-18 [code-reviewer] delta read 1 (review-code.md): approve; CR-1 resolved; 0 blocker, 0 major open; one new minor (CR-6, docs debt); H-1, S-1, H-2, BR-DR-9 for the human

- 2026-10-18 [backend-developer] fix round (review CR-1..CR-5, security S-1, QA D-1): contract notes in plan-backend.md § 13 — api-contract § 0.4/§ 0.2/§ 2.2 (Restore's body binds first), § 2.3 + BR-DR-15 (purge log names deleter and time); left for the human: H-1/D-2, S-1 taken as A

- 2026-10-18 [frontend-developer] stage 6 done (plan-frontend.md): «Удалить»/«Удалить навсегда» in «⋮», three dialogs, the deleted card, D-5; contract unchanged. Breaks e2e 27-work-item-card 27.5 and 27-organization-members 27.9 (not edited). For the human: an unknown deleter shows no «(неактивен)»

- 2026-10-18 [backend-developer] stage 4 done (commits on the branch; plan-backend.md § 12 "As built" lists the deviations): migration `AddWorkItemDeletionMarks`, three POST routes, `deletion` on `WorkItem`, `WorkItemSets` entry points, project reorder member lock, purge step contract; api-contract.md is unchanged; the `stage:` line is the orchestrator's

- 2026-10-18 [backend-developer] plan-backend.md + api-contract.md (stage 4a, deep): three POST sub-routes, `deletion` on WorkItem, entry points + parity/matrix/inventory tests, project-reorder member lock; no code yet; contract fixed from here (a change needs a note here)
- 2026-10-18 [orchestrator] the human answered gate-1 and gate-2 in chat: every OQ and D-n A, ADR-0030 accepted; backend starts
- 2026-10-18 [orchestrator] route L, light — confirmed by the human in chat («⭐ L, облегчённо»); gate-1 and gate-2 go to the human as one package after the design; E2E: no.
- 2026-10-18 [new-task] task started (scripts/new-task.sh; its worktree was removed so a worktree-isolated agent could take the branch)
- 2026-10-18 [business-analyst] spec.md + US-1..US-6 (15c20a32): BR-DR-1..18; OQ-1 (who deletes), OQ-2 (way into the bin), OQ-3 (history), OQ-4 (purge dialog) open, A recommended, for the human at gate-1.
- 2026-10-18 [requirements-reviewer] round 1 (68954630): approved-by-ai; R-1..R-13 all minor (0 blocker, 0 major), resolved in place by the reviewer; no new question.
- 2026-10-18 [ux-designer] design.md (8e269de4): no prototype, no new kit part; D-1..D-6 open, A recommended, for the human at gate-2.
- 2026-10-18 [backend-developer] docs/adr/0030-work-item-recycle-bin.md, proposed (12e5e574); references ADR-0026, amends ADR-0029 § 4; D-1..D-3 open, A recommended, for the human at gate-2. No code under src/.
- 2026-10-18 [task-executor] `bash scripts/verify.sh` at 12e5e574: RESULT green (links, backlog ids, status budget, spec shape).
- 2026-10-18 [orchestrator] the gate-1 + gate-2 package goes to the human; no code before the answer.

## Notes

Summary (from the queue line or an argument): MVP. Founds the soft-delete pattern: delete → «Удалена», restore to the previous or a chosen state, the organization administrator's purge (doc-010 D-13), doc-001 BR-14 for the surfaces and writes that exist when it ships, no cascade (BR-M3-9); re-runs feat-001's US-7 (deleted items and links, enforced today through the Removed category) through its own Delete (feat-001 D-8 OQ-1); owes the recycle-bin ADR, which references mod-002's soft-deactivation ADR
