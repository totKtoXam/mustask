# tech-060-worktree-own-hooks

- type: tech
- size: S
- branch: tech/060-worktree-own-hooks
- stage: gate-3
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

Cause: no script in the repo sets an absolute `core.hooksPath`. With `extensions.worktreeConfig` on, the tool that creates worktrees writes the absolute `.githooks` of the checkout it starts from (the hub, or doc-012's worktree) into the new worktree's `config.worktree`. That override beats the shared relative `.githooks`, so the worktree runs another checkout's older hooks. On this machine 16 worktrees had it; the doc-012 pin ran the pre-tech-055 pre-push, which runs tests. Fix:

1. `.githooks/{commit-msg,pre-commit,pre-push}`: hand over (`exec`) to `$(git rev-parse --show-toplevel)/.githooks/<hook>` when started from another checkout's copy. `MUSTASK_OWN_HOOK` stops a second hand-over.
2. `scripts/fix-hooks-path.sh`: a one-time, idempotent repair the human runs. It sets the shared value to `.githooks` and unsets `core.hooksPath` in every `config.worktree`; `--dry-run` reports only.
3. `docs/conventions/git.md` "Local checks": where the absolute path comes from, and both remedies.

Check: `bash scripts/verify.sh`. The hand-over was tested: a copy under a pinned scratch `core.hooksPath` ran the worktree's own `commit-msg` and never its own body. The fix script's `--dry-run` listed all 16 overrides.

## Log

- 2026-11-06 [orchestrator] queued via PR #89 (take: PR #91) by the doc-012 session on the human's choice («⭐ Hook в каждом worktree»). Route: tech, S, light cycle (hooks, scripts and docs only); the human chose «⭐ S, лёгкий цикл».
- 2026-11-06 [orchestrator] binding: worktree `.claude/worktrees/unruffled-banach-65f877`, branch `tech/060-worktree-own-hooks` from origin/main. The config repair is shared state that auto mode blocks; the human runs the script.
