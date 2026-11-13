# mod-024-import-constructor

- type: module
- size: L
- branch: mod/024-import-constructor
- stage: 03-design
- epic: core/work-items
- deps: mod-019,doc-017
- gates: gate-2, gate-3

## Gates

gate-2: pending
gate-3: pending

## Plan

Route L from stage 3. The requirements are doc-017's, approved at its gate-1
on 2026-11-10: `docs/features/doc-017-import-export-constructor/` (`spec.md`
with BR-IE-*, the converter catalogue, the NFRs incl. BR-IE-21 limits and the
file-safety rules; stories US-1, US-2, US-3, US-4, US-7) and
`docs/adr/0042-import-batch-through-work-item-writes-with-undo.md`
(accepted). Their gate-1 is not repeated here.

Scope (the queue line): import templates per organization (create, edit,
copy, delete, author); .xlsx/.csv source reading; header-based column
mapping; recoding tables (per-type state entries); the converter catalogue;
MUSTASK-identifier matching; the dry run with its per-row report; permission
codes `imports.template.read`, `imports.template.write`,
`imports.import.run`, managers' scope included; limits and file-safety NFRs.

Out of scope: the import write, the batch and its undo (feat-067), export
(feat-069), external links (feat-066), participants (feat-068).

Stages: design (screens of the constructor, clickable HTML prototype, opened
in a browser before the gate) → gate-2 → backend (plan-backend.md,
api-contract.md, code, tests) → backend QA → frontend → frontend QA → code
review (one Opus pass with the security pass) → gate-3 → PR on GitHub with
Auto-fix, merged by the human with a merge commit, branch kept.

Economy mode: one agent per stage, one Opus review. E2E: no (standing answer).
Verification: affected tests locally, the full verify.sh in CI. Examples and
fixtures use made-up data only.

## Log

- 2026-11-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/mod-024-import-constructor
- 2026-11-13 [orchestrator] session bound to the worktree (git rev-parse --show-toplevel and git branch --show-current confirmed), cut from origin/main 1dbcbfee.
- 2026-11-13 [orchestrator] queue line taken by scripts/queue.sh take mod-024, PR #171 with squash auto-merge.
- 2026-11-13 [orchestrator] route confirmed by the human in chat: L from stage 3 (L thresholds: a new module; new permission codes with managers' scope); spec and stories inherited from doc-017 (gate-1 approved 2026-11-10), gate-1 not repeated; gates gate-2 and gate-3. Question 3.2 not applicable: only the module's own new tables.

## Notes

Summary (from the queue line): Founds the import module: import templates per organization (create, edit, copy, delete), .xlsx/.csv source reading, column mapping, recoding tables, the converter catalogue, the identifier setting, and the dry run with its per-row report; registers the template and import permission codes, managers' scope included (doc-017 US-1..-4, US-7; ADR-0042)
