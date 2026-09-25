# doc-003-backlogs-boards

- type: docs
- size: M
- branch: doc/003-backlogs-boards
- stage: complete
- epic: product/analysis
- deps: doc-001
- gates: gate-1, gate-3

## Gates

gate-1: approved (2026-09-24, human in chat)
gate-3: approved (2026-09-24, human in chat, "принять, сделать squash")

## Plan

1. Input: the umbrella `docs/features/doc-001-domain-map/spec.md` (module M4
   boundary, MVP/release tables, ER model, BR-2/3/11/12, decomposition row
   (a) for doc-003), the binding human decisions D-1..D-12 in
   `docs/features/doc-001-domain-map/status.md`, the Azure DevOps reference
   `docs/features/doc-001-domain-map/research/ado-boards.md` and
   `screenshots/` (boards, backlogs, board settings).
2. Stage 1 (business-analyst): `spec.md` = the M4 deep dive — BOARD /
   BOARD_COLUMN / SWIMLANE / BACKLOG_ENTRY entities, backlog levels and their
   ordering (stack rank, BR-12), parent/child hierarchy on the backlog,
   Kanban columns mapped to state categories (BR-2), cards; MVP cut per
   D-3; WIP limits, custom columns/swimlanes, card styling as release-2
   ground only; stories with Gherkin criteria; decomposition refining
   mod-006-boards-backlogs (already queued) — no new queue lines unless the
   analysis shows M4 needs a split.
3. Stage 2 (requirements-reviewer, clean context) → gate-1.
4. Stages 3–8 skipped (docs task, no screens, no code); gate-3 → local
   squash via `scripts/finish-task.sh` (no remote).

## Decisions

- D-1..D-12 of doc-001 are binding input; this task does not reopen them.
- The queue line names WIP and swimlanes; doc-001 puts WIP limits and custom
  swimlanes in release 2. They are analysed as release-2 ground (the way
  doc-005 treats M6), not pulled into the MVP.
- DD-1 (R-1, human 2026-09-24): a board column is seeded one per non-Removed state category used at its level, and for each type at that level it maps to exactly one state — that type's first state in the category in M2's seed order; a card whose type has no state in a column's category cannot be dropped there. Matches doc-001's BOARD_COLUMN → STATE_DEFINITION edge; doc-007 owes the per-type state order. — superseded by doc-010 D-4 (2026-09-24)
- DD-2 (R-6, human 2026-09-24): the concurrency policy belongs to the operation, doc-001 BR-11 unchanged: a card drag writes only the state, last write wins, no version check; a form save keeps its xmin check, so a save landing after a drag gets the normal conflict and reloads. — amended in part by doc-010 D-20 (N-14, N-16, 2026-09-25): doc-001 BR-11 is now the operation → policy table; a drag of an item in «Удалена» is refused
- DD-3 (R-8, human 2026-09-24): mod-boards-backlogs (L) founds M4 — boards, columns, default swimlane, ordered backlog, conflict notice — without the hierarchy; a follow-up M task inside the slice (deps mod-boards-backlogs, feat-work-item-links) ships the backlog hierarchy display and the parent-mapping write, both in MVP. Queue lines are entered after gate-1.

## Log

- 2026-09-25 [doc-010] amended: context, § Roles (org-wide access, codes, board configuration = org admin), BPMN, rules renumbered BR-M4-n (BR-7/12/19 → doc-001), BR-M4-2 per-(type, state) columns and headers + § Board columns, BR-M4-3/5/6/8/10/11/16/17/18, BR-M4-13 rewritten (several teams, pre-M4 backfill), new BR-M4-20 (team deletion), BR-M4-21 (create from board/backlog), BR-M4-22 (filter bar), data model, operation completeness, NFR refresh model (notice text) and audit, out of scope, decomposition (queued ids, feat-NNN-board-backlog-filter, codes); stories US-1..US-3, new US-4 (cross-review N-2, N-4, N-9, N-10, N-11, N-14, N-16, N-17, N-18, N-21, N-25, N-26, N-29, N-30, N-31, N-32, N-34)
- 2026-09-24 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh
- 2026-09-24 [orchestrator] gate-3 requested; stages 3–8 skipped (docs task: no screens, no code)
- 2026-09-24 [orchestrator] queue in main: feat-004-backlog-hierarchy added (M, deps mod-006,feat-001; b5bee98); mod-006-boards-backlogs line refined to the split scope, deps +mod-005 per R-23 (6d70708)
- 2026-09-24 [orchestrator] gate-1: approved by the human in chat
- 2026-09-24 [orchestrator] gate-1 requested from the human
- 2026-09-24 [requirements-reviewer] round 2 delta check in review.md: approved-by-ai — all round-1 resolutions hold, DD-1..DD-3 reflected; 4 new majors fixed in place (R-20 card column by state category, R-21 rank kept only across delete/restore, R-22 hierarchy is expand-only in MVP with parent grouping rows moved to release 2, R-23 BR-13..17 and the mod-work-item-delete-restore dep added to mod-boards-backlogs) + 3 minors; no open findings, no human questions; verify.sh green (orchestrator run)
- 2026-09-24 [business-analyst] rework pass 2: R-1 (BR-2/BR-3 per-type column→state, BR-18 refused drop), R-5 (Data model section), R-6 (BR-19 policy per operation), R-8 (decomposition split: mod-boards-backlogs L + feat-NNN-backlog-hierarchy M) resolved per DD-1..DD-3; spec.md 152 lines, US-1 131, US-3 68; open questions: none; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] human decisions on round 1: R-1 per-type state per column, R-6 concurrency policy per operation, R-8 split into an L founder + an M hierarchy follow-up (hierarchy display and mapping write both MVP); recorded as DD-1..DD-3; rework pass 2 (R-1/R-5/R-6/R-8) delegated to business-analyst
- 2026-09-24 [orchestrator] session restored after a Claude Desktop reinstall: the task branch now lives in worktree nervous-napier-984cb3 (the doc-003-backlogs-boards worktree is gone); rework pass 1 copied in from the scratchpad; decisions on R-1/R-6/R-8 requested from the human again (the first request was interrupted, no answer recorded)
- 2026-09-22 [business-analyst] rework pass 1: R-2 (BR-13, one team per project in MVP, cross-team scenarios replaced by MVP-reachable variants), R-9 (BR-14 card fields and backlog row columns), R-10 (BR-15 in-column order = stack rank, vertical drag reorders), R-11 (BR-16/17 Removed items hidden, restore keeps position) resolved; spec.md 140 lines, stories 104/101/69; R-1/R-5/R-6/R-8 still open, waiting on the human; verify.sh green (orchestrator run)
- 2026-09-22 [requirements-reviewer] round 1 in review.md (deep tier, clean context): needs-human-decision — 19 findings: R-1 blocker (column↔state mapping undefined, ER edge BOARD_COLUMN→STATE_DEFINITION not reconciled), R-2/R-5/R-6/R-8/R-9/R-10/R-11 major open, 10 fixed in place (BPMN last-write-wins, board seeded at team creation, BR-8 drag-only, 6 scenarios added, 2 of 3 open questions answered from doc-001); human-only: R-1 mapping model, R-6 drag vs form-save race, R-8 + open question 1 (hierarchy write in MVP, one L or L+M split); verify.sh green (orchestrator run)
- 2026-09-22 [orchestrator] decisions requested from the human on R-1/R-6/R-8; business-analyst rework of R-2/R-9/R-10/R-11 started in parallel, R-1/R-5/R-6/R-8 follow the answers
- 2026-09-22 [business-analyst] stage 1 delegated (standard tier): spec.md 124 lines (context, roles, 3 stories, BPMN for the move flow, BR-1..BR-12 for M4, operation completeness, out of scope, decomposition refining mod-boards-backlogs), stories US-1..US-3 (all `shippable: no`, delivered by mod-boards-backlogs); 3 open questions for the human; written via the session scratchpad because subagents cannot write across worktrees, copied in by the orchestrator; `bash scripts/verify.sh` → RESULT: green (orchestrator run)

- 2026-09-22 [orchestrator] route confirmed by the human: docs, M, stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable (no screens). Size grounds: no L threshold fires (no migration, project, ADR, new slice, breaking change, external exchange or auth change); not S (more than 5 files, more than ~2 h); size matches the queue line. Queue line removed from main (465b7b9)
- 2026-09-22 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-003-backlogs-boards

## Notes

Summary (from the queue line or an argument): Backlogs and boards module analysis: backlog levels, ordering, hierarchy, Kanban columns, WIP, swimlanes, cards
