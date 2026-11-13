# mod-024-import-constructor

- type: module
- size: L
- branch: mod/024-import-constructor
- stage: 04-backend
- epic: core/work-items
- deps: mod-019,doc-017
- gates: gate-2, gate-3

## Gates

gate-2: approved (by the human in chat, 2026-11-13: «сразу переходи к реализации, в процессе разработки пересмотрим, что да как»)
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

- 2026-11-13 [ux-designer] stage 3 done: design.md (S1 list, S2 editor with 4 tabs and chain dialog, S3 dry run; all states, RU copy) and prototype/index.html (opened in headless Chrome, scripted walk-through without runtime errors; the human still looks at it in a browser). gate-2 stays pending.
- 2026-11-13 [orchestrator] prototype clicked through in the built-in browser (list, editor's four tabs, chain dialog, recoding, dry run): no runtime errors. Found: the dry-run report shows «Будет обновлён 30» while the template has no identifier column.
- 2026-11-13 [orchestrator] gate-2 asked; the human did not approve or reject: «тут сложно сказать, пока не прогонишь реальный парсер». Decided at the same time: «Импорт» in settings is shown to every member (imports.template.read); the 50-row preview is the first 50 rows of the selected result. gate-2 stays pending.
- 2026-11-13 [orchestrator] gate-2 approved by the human with a caveat: go straight to implementation and revisit the design during development, once the real parser runs (quote in Gates). Stage 4 started.

## Notes

Summary (from the queue line): Founds the import module: import templates per organization (create, edit, copy, delete), .xlsx/.csv source reading, column mapping, recoding tables, the converter catalogue, the identifier setting, and the dry run with its per-row report; registers the template and import permission codes, managers' scope included (doc-017 US-1..-4, US-7; ADR-0042)
