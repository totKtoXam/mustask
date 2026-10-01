# bug-001-handoff-short-id-prefix

- type: bugfix
- size: S
- branch: bug/001-handoff-short-id-prefix
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved · delegated by human 2026-10-01 · merged 2026-10-01 (local squash, no remote)

## Plan

1. Reproduce: with branches `doc/001-domain-map` (stage complete in main) and
   `mod/001-process-seed`, `bash scripts/handoff.sh mod-001` prints
   "doc-001-domain-map is already merged into main (stage: complete)".
2. Fix the branch fallback in `scripts/handoff.sh` (the `grep -m1 -E "^[a-z]+/${short#*-}-"`
   line): match on the id's own prefix, `^${short%%-*}/${short#*-}-`. The queue
   lookup and the `docs/features` lookup already anchor on the full `$short`
   (prefix included), and `scripts/backlog.sh` keeps the prefix too — they stay.
3. `scripts/tests/handoff-cases.sh`: a throwaway repository with a main worktree;
   cases — a started task whose number a merged task of another type shares
   resolves to its own branch; a short id whose only same-number branch has a
   different prefix is "No task"; a merged task still reports "already merged".
   The cases fail before the fix.
4. `scripts/verify.sh` runs the cases when `scripts/handoff.sh` or the case file
   changes.

## Log

- 2026-09-28 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/bug-001-handoff-short-id-prefix

- 2026-09-28 [route] the human confirmed bug · S (route Dev → QA → Review → gate-3; gate-1/gate-2 have no subject); no standing approval, so the light cycle does not apply

- 2026-09-28 [worktree] the task worktree was removed (clean, only the committed status.md) and the session's worktree `.claude/worktrees/exciting-engelbart-12ccb2` switched onto the task branch: the session may only write inside its own worktree
- 2026-09-28 [dev] before the fix `sh scripts/tests/handoff-cases.sh` → exit 1: case 1 (mod-001 → "doc-001-domain-map is already merged") and case 2 (feat-002 handed off as tech-002-something) FAIL; cases 3–4 PASS
- 2026-09-28 [dev] fix in `scripts/handoff.sh` branch fallback: `^${short%%-*}/${short#*-}-`; the queue and docs/features lookups and `scripts/backlog.sh` already keep the prefix, unchanged; cases wired into `scripts/verify.sh`
- 2026-09-28 [dev] `bash scripts/verify.sh` → exit 0 (green, incl. "handoff.sh: short-id resolution cases" 4/4); `bash scripts/handoff.sh mod-001 --command-only` in the real repository → "Continue task mod-001-process-seed", exit 0

- 2026-09-28 [qa] verdict: pass, no defects (docs/features/bug-001-handoff-short-id-prefix/review.md, ## QA). Confirmed the cases fail without the fix: `main`'s `handoff.sh` + this branch's `handoff-cases.sh` copied into a scratch scripts/+scripts/tests/ layout, `sh scripts/tests/handoff-cases.sh` → 2/4 FAIL, exit 1 (case 1 and 2, matching the dev log). In-repo (fixed) `sh scripts/tests/handoff-cases.sh` → 4/4 PASS, exit 0. `bash scripts/verify.sh` → green, exit 0. Checked the other two id lookups in `handoff.sh`, `backlog.sh`, `queue.sh`, `new-task.sh`, `finish-task.sh` for the same prefix-stripping pattern — none found; reviewed the "branch also queued", "merged task with branch kept" (already case 3), "hot prefix" (not special-cased) and "three-prefix collision" (fix's anchor already prefix-specific) edge cases — no gaps, no new cases added. Decisions: E2E is not applicable — this task changes only a tooling shell script, no runtime code and no UI surface; the orchestrator confirms with the human.
- 2026-09-28 [review] round 1 (scripts): approve — 0 blocker, 0 major, 1 minor + 4 nit open, none blocking gate-3 (review.md, ## Code review)
- 2026-09-28 [dev] review round 1 fixes: R1-1 (GIT_CONFIG_GLOBAL to an empty sandbox file, XDG_CONFIG_HOME/GIT_CONFIG_PARAMETERS/GIT_CONFIG_COUNT unset), R1-2 (INT/TERM exit 130, cleanup on EXIT only), R1-4 (usage comment); R1-3 and R1-5 need no change. Under a global config with `branch.sort=-refname` and a rejecting `core.hooksPath`: main's handoff.sh → cases 1–2 FAIL, exit 1; fixed → 4/4 PASS, exit 0. `bash scripts/verify.sh` → exit 0
- 2026-09-28 [review] round 2 (scripts): approve — R1-1, R1-2, R1-4 closed; 0 blocker, 0 major, 0 minor, 2 nit left by design (R1-3, R1-5) (review.md, ### Round 2)
- 2026-10-01 [orchestrator] gate-3: approved under the human's delegation of 2026-10-01 (consolidation session: the human handed every gate decision and the local squash of the six in-flight tasks to the orchestrator); E2E confirmed not applicable (tooling script only, no runtime or UI); R1-3 and R1-5 accepted as informational; stage: complete, local squash via scripts/finish-task.sh

## Notes

Summary (from the queue line or an argument): scripts/handoff.sh resolves a short id to another type's branch with the same number: the branch fallback strips the prefix, so mod-001 hands off doc-001-domain-map; match the branch on its own prefix, with a regression case run by verify.sh
