# mod-005-work-item-delete-restore

- type: module
- size: L
- branch: mod/005-work-item-delete-restore
- stage: 01-business-analysis
- epic: core/work-items
- deps: mod-003,mod-016
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

The light route (the human's choice of 2026-10-18): analysis → one requirements review round → UX design + the recycle-bin ADR → gate-1 and gate-2 go to the human as one package → backend → frontend → one QA pass with the code review and the security pass → one fix round → at most one delta read → gate-3 → PR to main.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Light route: the human's choice of 2026-10-18.

## Log

- 2026-10-18 [orchestrator] route L, light — confirmed by the human in chat («⭐ L, облегчённо»); gate-1 and gate-2 go to the human as one package after the design; E2E: no.
- 2026-10-18 [new-task] task started (scripts/new-task.sh; its worktree was removed so a worktree-isolated agent could take the branch)

## Notes

Summary (from the queue line or an argument): MVP. Founds the soft-delete pattern: delete → «Удалена», restore to the previous or a chosen state, the organization administrator's purge (doc-010 D-13), doc-001 BR-14 for the surfaces and writes that exist when it ships, no cascade (BR-M3-9); re-runs feat-001's US-7 (deleted items and links, enforced today through the Removed category) through its own Delete (feat-001 D-8 OQ-1); owes the recycle-bin ADR, which references mod-002's soft-deactivation ADR
