# tech-037-queue-ids-from-all-queue-files

- type: tech
- size: S
- branch: tech/037-queue-ids-from-all-queue-files
- stage: 04-backend
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. `scripts/backlog.sh` `queue_file_numbers`: read `docs/backlog.md` and every
   file under `docs/backlog/` of every ref in a fixed number of git processes
   (for-each-ref → batch-check of `<commit>:docs/backlog` → `rev-list
   --objects --no-walk --stdin` over the distinct trees → one `cat-file
   --batch`), still failing loudly on a short read.
2. `scripts/new-task.sh` hot path: take the taken numbers from
   `scripts/backlog.sh --ids` instead of reading two hardcoded files ref by
   ref (CR-4 names it too); refuse when that read fails.
3. `scripts/queue.sh` header, `docs/features/numbering.md` source 3: the queue
   files are `docs/backlog.md` plus everything under `docs/backlog/`.
4. Check: new cases in `scripts/tests/queue-numbering-cases.sh` (a number held
   only by `docs/backlog/later.md` on another branch is taken for `--ids`,
   `queue.sh add` and `new-task.sh hot`; the read stays one pass);
   `bash scripts/verify.sh` green.

## Log

- 2026-11-03 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-037-queue-ids-from-all-queue-files
- 2026-11-03 route confirmed by the human: tech, S, light cycle (only `scripts/` and `docs/`; check `scripts/verify.sh`, review the human's PR diff look); standing light-cycle approval confirmed to apply. Source: tech-034 CR-4.
- 2026-11-03 cases: `sh scripts/tests/queue-numbering-cases.sh` 14/14 PASS (also `--shell dash`); the two new cases fail against the pre-change scripts (`--scripts` with the main copies).
- 2026-11-03 `bash scripts/verify.sh` — exit 0, `RESULT: green`.

## Notes

Summary (from the queue line or an argument): queue.sh and backlog.sh read the queue's numbers from every queue file (docs/backlog.md and everything under docs/backlog/), not only docs/backlog.md and docs/backlog/withdrawn.md, so a number held by another queue file is never issued twice
