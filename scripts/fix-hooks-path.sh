#!/usr/bin/env bash
# One-time repair of core.hooksPath on a clone (tech-060). Idempotent: a second
# run finds nothing to change.
#
# The value meant to be live is the relative `.githooks` in the shared config,
# which git resolves against the root of each worktree, so every worktree runs
# its own branch's hooks (docs/conventions/git.md, "Local checks"). The tool
# that creates worktrees can write an absolute path into a worktree's own
# config.worktree, pointing at the checkout it was created from; that override
# wins over the shared value, and the worktree then runs that other checkout's
# (often older) hooks. This script:
#
#   1. sets core.hooksPath = .githooks in the shared config;
#   2. removes core.hooksPath from every config.worktree of the clone (the
#      main worktree's and each linked one's).
#
# Run from any worktree of the clone:
#
#   bash scripts/fix-hooks-path.sh            # change and report
#   bash scripts/fix-hooks-path.sh --dry-run  # report only
#
# The hooks themselves also hand over to the worktree's own copy since
# tech-060, so a worktree whose override comes back still runs its own hooks
# once the pinned checkout has that change.

set -euo pipefail

dry_run=0
case ${1:-} in
    '') ;;
    --dry-run) dry_run=1 ;;
    *) printf 'usage: bash scripts/fix-hooks-path.sh [--dry-run]\n' >&2; exit 2 ;;
esac

common=$(git rev-parse --path-format=absolute --git-common-dir)

run() {
    if [ "$dry_run" = 1 ]; then
        printf '  would run: %s\n' "$*"
    else
        "$@"
    fi
}

shared=$(git config -f "$common/config" --get core.hooksPath || true)
if [ "$shared" = .githooks ]; then
    printf 'shared config: core.hooksPath = .githooks (already)\n'
else
    printf 'shared config: core.hooksPath = %s -> .githooks\n' "${shared:-<unset>}"
    run git config -f "$common/config" core.hooksPath .githooks
fi

changed=0
for file in "$common/config.worktree" "$common"/worktrees/*/config.worktree; do
    [ -f "$file" ] || continue
    value=$(git config -f "$file" --get-all core.hooksPath || true)
    [ -n "$value" ] || continue
    printf '%s: removing core.hooksPath = %s\n' "$file" "$value"
    run git config -f "$file" --unset-all core.hooksPath
    changed=$((changed + 1))
done
printf 'worktree overrides found: %s\n' "$changed"

if [ "$dry_run" = 0 ]; then
    printf '\nlive here now:\n'
    git config --show-origin --get-all core.hooksPath
fi
