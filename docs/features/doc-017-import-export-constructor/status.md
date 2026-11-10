# doc-017-import-export-constructor

- type: docs
- size: L
- branch: doc/017-import-export-constructor
- stage: 01-business-analysis
- epic: core/work-items
- deps: -
- gates: gate-1, gate-3

## Gates

gate-1: pending
gate-3: pending

## Plan

No backend/frontend plan: a docs task. Input: [input-brief.md](input-brief.md)
(the human's ask and the anonymized analysis of the team's spreadsheet).
1. Stage 1 (deep, one agent): `spec.md` + `stories/` + ADR drafts (Proposed)
   + the decomposition table: the constructor lines, the gap lines, which of
   them must land before the first DevEx transfer, feat-005 withdrawn or
   reworded.
2. Stage 2: requirements review in a clean context, findings in `review.md`.
3. gate-1: the spec, the ADR drafts and the decomposition.
4. After gate-1: the orchestrator enters the delivery lines and withdraws or
   rewrites the superseded ones; final diff, `scripts/verify.sh`, gate-3.

## Log

- 2026-11-10 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-017-import-export-constructor
- 2026-11-10 [orchestrator] route confirmed by the human in chat: docs, L (a new module, ADR-level decisions: external key, undoable import batch), BA → requirements review → gate-1 → queue lines → final diff/verify → gate-3. gate-2 has no subject: the constructor's screens are designed in the delivery tasks.
- 2026-11-10 [orchestrator] line taken by sync PR #127 (auto-merge); session bound natively (EnterWorktree) to this worktree, branch confirmed. Economy mode: one agent per stage.

## Notes

Summary (from the queue line or an argument): Analysis: an import/export constructor for work items from .xlsx and .csv: column-to-field mapping, value recoding tables (type, state, priority, project, people), chained converters (split, Excel date, bucket to hours, hyperlink URL, regex, constant), an external key for upsert, a dry run with per-row results, batched import with undo, saved per-organization templates and export by the same template; decides which gaps must land before the first transfer of the DevEx sheet (external key and links, custom fields, item participants, custom states); replaces feat-005
