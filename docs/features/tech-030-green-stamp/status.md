# tech-030-green-stamp

- type: tech
- size: S
- branch: tech/030-green-stamp
- stage: complete
- epic: workflow
- deps: tech-028
- gates: gate-3

## Gates

gate-3: approved

## Plan

1. `scripts/verify.sh`: a green `VERIFY_FULL=1` run on a clean tree (no unchecked parts) writes a stamp `<git-common-dir>/verify-stamps/<tree-hash>-<merge-base>`; a later `VERIFY_FULL=1` run on the same tree and merge-base finds it and returns green at once (`VERIFY_FORCE=1` ignores it). finish-task.sh and pre-push already call `VERIFY_FULL=1`, so the same commit is tested once.
2. A stamp is written only when nothing was NOT CHECKED / skipped, and only for a clean working tree.
3. Docs: header of verify.sh, `docs/ai-workflow/README.md`, `07-mr-complete.md` step 1, and the QA/review stage texts say: need a full guarantee — `VERIFY_FULL=1 bash scripts/verify.sh`, instant when the stamp exists; do not run the suite by hand otherwise.
4. Check: `scripts/tests/verify-stamp-cases.sh` (write / hit / miss after a commit / another BASE / force / dirty / untracked / skipped check / a partial forced run), `bash scripts/verify.sh`. One review round. No E2E.

## Log

- 2026-10-13 [new-task] task started, branched from tech/028-frontend-tests-speedup (deps; not yet merged)
- 2026-10-13 route: tech, S, short route under the human's standing approval ("do it yourself"); no E2E

- 2026-10-13 review round 1 (review.md in the session, not a file): 4 major, 4 minor — all handled, see Notes
- 2026-10-13 `VERIFY_FULL=1 bash scripts/verify.sh` on this branch: RED once, one test of 3643 — `BacklogPage.add` "the same two header buttons" hit the 20 s timeout while other sessions loaded the machine (983 s run); the file alone is green in 131 s, the test takes 7.9 s idle. Lint, build and the 19 stamp cases were green. Not claimed green until a full run on the rebased branch (tech-029 raises the timeout to 60 s).

- 2026-10-13 gate-3: approved by the human in chat ("approve"); tech-028, tech-029 and tech-030 land in ONE squash from branch tech/030-green-stamp (the human asked for a single verified merge instead of three full runs); the branches tech/028 and tech/029 are kept

## Notes

Summary: A green VERIFY_FULL run leaves a stamp keyed by the tree hash; finish-task, pre-push, QA and review accept it instead of re-running the suite.

### Review round 1 — handled

F1 the stamp is written only if HEAD, its tree and a clean working tree are still what the run started with; F2 a run that goes on (forced, or a miss) deletes the old stamp first, so a red or partial run leaves none; F3 the QA step of `s-route.md` and a line in `06-code-review.md` say `VERIFY_FULL=1 bash scripts/verify.sh`, never a hand-run suite; F4 `scripts/tests/verify-stamp-cases.sh` — 19 checks, run from verify.sh's shell bucket when verify.sh or the cases change (the mid-run change of F1 is not covered by a case: it needs a hook into a running verify.sh); F5 the two silent skips (`*.py` without python, `*.ps1` without PowerShell) call `skipped`; F6 cleanliness by the exit code of `git status` and refusing assume-unchanged / skip-worktree files; F7 the header says the stamp is about the code on this machine, `VERIFY_FORCE=1` after changing the environment; F8 the conditions (same tree and merge-base, clean working tree) are spelled out. `VERIFY_PYTHON` (empty = no python) was added to verify.sh for the cases.
