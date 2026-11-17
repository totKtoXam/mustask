# feat-067-import-run-and-undo

- type: feature
- size: L
- branch: feat/067-import-run-and-undo
- stage: 01-business-analysis
- epic: core/work-items
- deps: mod-024
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-067-import-run-and-undo
- 2026-11-17 [orchestrator] route confirmed by the human in chat: feature, L, full route with three separate gates (gate-1, gate-2, gate-3), backend plan as a separate deep pass, two QA passes, one review round + security pass, E2E no (standing answer). Size grounds: two L thresholds fire: undo purges created items outside «Удалена», amending mod-005 BR-DR-12 (a change of an existing rule's semantics in another slice); M3 gains an in-process write contract for the import (its boundary changes). The batch tables are additive in the imports module; ADR-0042 is already accepted. Scope adds the mod-024 debts: QA-D-5 + R1-5 per-user upload quota into the worker; D-1 (rich-text reduction, hierarchy rules, concurrency) re-checked at write time; QA-FD-8 (stale dry-run report greyed, «Уже сопоставлено» marker), taken by the human's choice. Session bound to this worktree (EnterWorktree; toplevel and branch confirmed).

## Notes

Summary (from the queue line or an argument): The background import run through the work items module's own writes, one row per transaction: the import batch, its report and list, and the batch undo within 30 days, whose purge of untouched created items amends mod-005 BR-DR-12 (doc-017 US-5, US-6; ADR-0042)
