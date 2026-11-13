# doc-017-import-export-constructor

- type: docs
- size: L
- branch: doc/017-import-export-constructor
- stage: gate-3
- epic: core/work-items
- deps: -
- gates: gate-1, gate-3

## Gates

gate-1: approved (by the human in chat, 2026-11-10)
gate-3: pending

## Plan

No backend/frontend plan: a docs task. Input: [input-brief.md](input-brief.md)
(the human's ask and the anonymized analysis of the team's spreadsheet).
1. Stage 1 (deep, one agent): `spec.md` + `stories/` + ADR drafts (Proposed)
   + the decomposition table: the constructor lines, the gap lines, which of
   them the one DevEx transfer waits for, feat-005 reworded.
2. Stage 2: requirements review in a clean context, findings in `review.md`.
3. gate-1: the spec, the ADR drafts and the decomposition.
4. After gate-1: the orchestrator enters the delivery lines and withdraws or
   rewrites the superseded ones; final diff, `scripts/verify.sh`, gate-3.

## Log

- 2026-11-10 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-017-import-export-constructor
- 2026-11-10 [orchestrator] route confirmed by the human in chat: docs, L (a new module, ADR-level decisions), BA → requirements review → gate-1 → queue lines → verify → gate-3. No gate-2: screens are designed in the delivery tasks.
- 2026-11-10 [orchestrator] line taken by sync PR #127 (auto-merge); session bound natively (EnterWorktree) to this worktree, branch confirmed. Economy mode: one agent per stage.
- 2026-11-10 [business-analyst] spec.md (BR-IE-1..-25, OQ-1..-10, decomposition with first-transfer column, feat-005 reworded), stories US-1..US-10, ADR-0039 and ADR-0040 proposed; ADR numbers from this branch only, origin/main not checked (no shell).
- 2026-11-10 [requirements-reviewer] round 1: needs-human-decision. 7 major, 11 minor, no blocker; all fixed in place but R-7 (custom fields vs doc-010 D-21), now OQ-11; OQ-1..OQ-11 open (review.md).
- 2026-11-10 [orchestrator] human answers in chat (gate-1 not yet asked): OQ-1 B (managers import into their projects too), OQ-2 «убираем это поле» → drop the external key, keep external links; OQ-3 A (undo purges), OQ-4 A (recode states).
- 2026-11-10 [orchestrator] human answers in chat: OQ-5 «игнорим» (Срок, Вес D on stories not moved), OQ-6 A, OQ-7 B (one DevEx transfer when its lines land), OQ-8 A, OQ-9 «отдельная задача для уведомлений в телеграмм», OQ-10 A, OQ-11 deferred as a recorded debt.
- 2026-11-10 [orchestrator] the human asked why the constructor is built for this one file; chose: split the spec into a generic constructor and a DevEx transfer example, and check the converter catalogue on the ТОС sheet and a generic tracker CSV export.
- 2026-11-10 [business-analyst] rework: spec split into a generic constructor and § DevEx transfer, catalogue by class (+«Дата из ISO 8601»), key dropped, ADR-0039 withdrawn (stub, git rm owed), US-9 links only, ADR-0040 updated; OQ-1..-11 resolved, OQ-12, OQ-13 new.
- 2026-11-10 [orchestrator] stubs removed; never-merged ADR-0040 renumbered to ADR-0039 (the key ADR it replaced was withdrawn), references updated; review.md round 1 keeps the old numbers as history.
- 2026-11-10 [requirements-reviewer] round 2 (delta): needs-human-decision. R-19..R-27: 2 major (no-identifier round trip, per-type state recoding), 7 minor, fixed in place but R-27 (orchestrator); OQ-12, OQ-13 open; verify.sh not run (no shell).
- 2026-11-10 [orchestrator] human answers in chat: OQ-12 B (a «Менеджер» also creates templates, edits and deletes their own), OQ-13 A; applied to § Roles, BR-IE-18, US-1, US-7, ADR-0039 decision 5.
- 2026-11-10 [orchestrator] gate-1 approved by the human in chat; ADR-0039 accepted with it (index updated). Next: the delivery lines into the queue, feat-005 and the amended lines reworded, then the final diff and gate-3.
- 2026-11-13 [orchestrator] delivery lines queued by scripts/queue.sh add (#160, #162, #164; #163, #165 conflicted and were folded into #161): feat-066, mod-024, feat-067, feat-068, feat-069, mod-025; NNN in spec, stories and ADR replaced by these ids.
- 2026-11-13 [orchestrator] sync PR #166: feat-005 reworded (OQ-8 A, deps feat-069); feat-015, feat-016, feat-002, mod-004, feat-047 amended per § Decomposition.
- 2026-11-13 [orchestrator] origin/main merged in: ADR-0039/-0040 were taken there meanwhile, -0041 by another branch; this ADR is now ADR-0042 (file, index, spec, stories). Next: gate-3.

## Notes

Summary (from the queue line or an argument): Analysis: an import/export constructor for work items from .xlsx and .csv: column-to-field mapping, value recoding tables (type, state, priority, project, people), chained converters (split, Excel date, bucket to hours, hyperlink URL, regex, constant), an external key for upsert, a dry run with per-row results, batched import with undo, saved per-organization templates and export by the same template; decides which gaps must land before the first transfer of the DevEx sheet (external key and links, custom fields, item participants, custom states); replaces feat-005

The summary above is the queue line as taken; the external key and the first-transfer plan in it were dropped by the human (OQ-2, OQ-7 B). Delivery lines are worded from spec.md § Decomposition, not from this summary (review.md R-27).
