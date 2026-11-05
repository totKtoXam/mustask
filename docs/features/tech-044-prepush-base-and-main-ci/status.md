# tech-044-prepush-base-and-main-ci

- type: tech
- size: S
- branch: tech/044-prepush-base-and-main-ci
- stage: complete
- epic: workflow
- deps: tech-041
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. BASE: `scripts/verify.sh` is the one place that defaults it (`BASE=${BASE:-main}`; the pre-push hook only calls it). Default to `origin/main` when `git rev-parse --verify -q origin/main` succeeds, else `main`; an explicit BASE wins; print the base and merge-base in the run.
2. Aborted run: verify.sh judges a test step by the exit code of the command alone. Add a second signal (a function that tees the output): fail on an abort marker in the output even at exit 0, and for Vitest on a missing summary; force English dotnet output (the host here prints Russian).
3. CI: on `push` to main compare the pushed commit with `github.event.before` (fallback HEAD^1) in a small step before Verify; PR runs keep `origin/main`.
4. git.md "Protecting main" rewritten to the live rules (`gh api .../branches/main/protection`, read-only); "Local checks" says what `core.hooksPath` is.
5. Check: new `scripts/tests/verify-base-and-abort-cases.sh` (stubs for dotnet/npm in a throwaway repo, wired into verify.sh); `BASE=origin/main bash scripts/verify.sh`, command and exit code in the Log.

## Decisions

- Light cycle (S, only docs/scripts/hooks/.github); E2E = NO (standing answer since 2026-10-12).
- The BASE default lives in verify.sh, not in the hook: the hook calls the verify.sh of the pushing worktree, so the fix is live even where `core.hooksPath` points at another checkout's older hook.
- The abort check reads the output as a second signal beside the exit code; dotnet is run with `DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033` (the maintainer's host prints Russian, where the English markers never match).
- CI base on a push to main: `github.event.before` when the clone has it, else `HEAD^1`, else the step fails (never a silent "nothing to verify"); PR runs keep `origin/main`.

## Log

- 2026-10-18 [task-executor] stage complete, gate-3 pending: the orchestrator's auto-merge on green CI is the merge. Light cycle: no QA or review session, no E2E (no runtime change)
- 2026-10-18 [task-executor] `BASE=origin/main bash scripts/verify.sh` exit 0, RESULT: green (shell, ci, prose; 18 new cases, 24 stamp cases). ci.yml: no PyYAML here, so parsed with js-yaml instead; the base step run for pull_request, push and a zero `before`
- 2026-10-18 [task-executor] dev: verify.sh BASE default and run_tests, scripts/tests/verify-base-and-abort-cases.sh (main's verify.sh fails 10 of its 18 cases), ci.yml base step, git.md, pre-push comment; modes of pre-push and verify.sh stay 100755
- 2026-10-18 [orchestrator] route S, LIGHT CYCLE (s-route.md, "Light cycle"): no QA, no review session, check = verify.sh (command and exit code below); E2E not asked
- 2026-10-18 [orchestrator] standing approval of 2026-10-18 («Ты работай, я спать буду. Пр-ы сам мерджи»; «Включай auto-merge для всех пр-ов»): no route confirmation, PR auto-merged on green CI; the human chose this line and cycle at tech-041's and mod-020's gate-3
- 2026-10-18 [new-task] task started, worktree a workflow agent's worktree

## Notes

Summary (from the queue line or an argument): tech-041's findings in one pass: the pre-push hook defaults BASE to the local `main`, stale in a worktree, so a push can verify all of main's history — default it to origin/main when that ref exists; a test run that aborts ("test host crashed") must fail the hook, not let the push through (seen on mod-020's last push); CI's push trigger on main compares HEAD with origin/main and prints "No changes — nothing to verify" after every merge — verify the merged commit against its first parent; docs/conventions/git.md "Protecting main" still says "not configured" and "at least one approve", while the live protection is the required check verify.sh (change-proportionate) only, no reviews, enforce_admins off

Findings (task-executor, 2026-10-18):

- Abort: the old verify.sh judged a test step by the bare exit code. A probe project whose test calls `Environment.FailFast` makes `dotnet test` exit 1 ("The active test run was aborted", "Test host process crashed", "Test Run Aborted" - printed in Russian on this host, the English markers need `DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033`); a Vitest test that calls `process.abort()` makes `vitest run` exit 1 and print no "Test Files" summary. So the pass-through seen on mod-020's push was not reproduced here; the output is now a second signal, so an exit code lost on the way (a wrapper, a pipe, a runner that exits 0) no longer passes.
- Hooks path: the live `core.hooksPath` on this machine is absolute, the hub's `.githooks` (`git config --show-origin`: the shared `.git/config`), not the relative `.githooks` git.md documents. A hook edited on a task branch does not run in its worktree until the hub has it; `scripts/verify.sh` always comes from the pushing worktree. git.md now says both. The setting was not changed (not this task's paths).
- `origin/main` moves only on `git fetch`: the new default fixes the stale local `main`, not a stale remote-tracking ref.
