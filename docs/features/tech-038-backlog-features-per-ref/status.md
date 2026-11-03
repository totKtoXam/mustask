# tech-038-backlog-features-per-ref

- type: tech
- size: S
- branch: tech/038-backlog-features-per-ref
- stage: 04-backend
- epic: workflow
- deps: tech-037
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. `scripts/backlog.sh` `feature_cards`: the `docs/features` folders and their
   `status.md` cards of every ref (refs/heads, refs/remotes) in a fixed
   number of git processes, the same passes as tech-037's queue read; fails
   loudly.
2. `taken_ids` counts every folder of every ref, plus this worktree's own
   (an uncommitted new folder).
3. "In flight" = a folder on some ref and `stage: complete` on none. Such an
   id with no local task branch gets an IN PROGRESS row and an EPICS
   "in flight" count; a stale copy of a finished task on another branch does not.
4. `new-task.sh` hot drops its own per-ref folder loop (`backlog.sh --ids`
   covers it); `numbering.md` source 2 and `queue.sh` header say so.
5. Check: new cases in `scripts/tests/queue-numbering-cases.sh`;
   `bash scripts/verify.sh` green.

## Log

- 2026-11-03 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-038-backlog-features-per-ref
- 2026-11-03 route confirmed by the human: tech, S, light cycle (only `scripts/` and `docs/`; check `scripts/verify.sh`, review the human's PR diff look); standing light-cycle approval confirmed to apply. Source: tech-034 CR-5.
- 2026-11-03 built on tech-037 (same `backlog.sh` read): the branch starts from its commit, the PR opens once tech-037 is merged.
- 2026-11-03 cases: `sh scripts/tests/queue-numbering-cases.sh` 16/16 PASS (also `--shell dash`); new cases 11-12 fail against the tech-037 scripts.
- 2026-11-03 `bash scripts/verify.sh` — exit 0, `RESULT: green`.
- 2026-11-03 tech-037 merged as #61 without its gate-3/complete status commit; merged main and carried that commit here (tech-037 `status.md` only). Cases 16/16, `verify.sh` green again.

## Notes

Summary (from the queue line or an argument): backlog.sh reads docs/features/ per ref (every local and remote-tracking branch), so an id whose folder exists only on another branch is still seen as taken and in flight
