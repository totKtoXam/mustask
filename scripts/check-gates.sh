#!/bin/sh
# Checks gate-3 in docs/features/*/status.md against main's actual state.
#
# Usage:
#   scripts/check-gates.sh [<ref>]
#
#   scripts/check-gates.sh              # origin/main if present, else main
#   scripts/check-gates.sh origin/main
#
# Why: the merge happens in GitLab, and at that moment the branch still has
# `gate-3: pending` — and that's what rides into main as is. The file lies:
# the task has been accepted, but the record says "pending". The script
# finds such mismatches.
#
# What is looked for:
#   1. `pending` on a merged task — there is a verdict, the file doesn't have
#      it.
#   2. `approved` without an MR link — there is a verdict, but nothing to
#      check it against. Only for tasks merged THROUGH an MR: pre-GitLab
#      ones (local squash, before 2026-08-28) could not have had a link, and
#      requiring one from them is noise, not a finding. The marker of the
#      MR era is a GitLab merge commit next to the squash.
#
# The script fixes NOTHING and always exits with code 0: it's a report, not
# a gate. Fixes go through the F-route, batched
# (docs/ai-workflow/stages/07-mr-complete.md, "The gate-3 verdict and
# the MR link").
#
# Being merged is determined locally, without calling the API: the squash
# commit carries the task's short id in its subject (`... (tech-004)`), so
# `git log <ref> --grep` is enough. The MR link already lives in status.md
# itself.

set -eu

case "${1:-}" in
    -h|--help) sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
esac

# --show-toplevel, and NOT --git-common-dir: in a worktree the shared .git
# directory points at the hub, and the script would check someone else's
# files instead of the ones the caller is working on. The CURRENT tree is
# checked; git log sees all refs the same way from any worktree.
repo=$(git rev-parse --show-toplevel 2>/dev/null) \
    || { printf 'Not a git repository.\n' >&2; exit 1; }
features="$repo/docs/features"

[ -d "$features" ] || { printf '%s not found.\n' "$features" >&2; exit 1; }

# Default ref: the server's main matters more than the local one — it's what
# everyone else sees. The local one is used only if there's no remote.
ref=${1:-}
if [ -z "$ref" ]; then
    if git -C "$repo" rev-parse --verify --quiet origin/main >/dev/null; then
        ref=origin/main
    else
        ref=main
    fi
fi
git -C "$repo" rev-parse --verify --quiet "$ref" >/dev/null \
    || { printf "Ref '%s' not found.\n" "$ref" >&2; exit 1; }

printf 'Checking gate-3 against %s (%s)\n\n' "$ref" "$(git -C "$repo" rev-parse --short "$ref")"

stale=0
linkless=0
legacy=0
checked=0

for status in "$features"/*/status.md; do
    [ -f "$status" ] || continue
    checked=$((checked + 1))

    id=$(basename "$(dirname "$status")")
    # Short id — the first two segments: tech-004-some-slug... ->
    # tech-004. That's exactly what's in the squash commit's subject.
    short=$(printf '%s' "$id" | cut -d- -f1,2)
    # Branch name = id with the first hyphen replaced by a slash
    # (conventions/git.md).
    branch=$(printf '%s' "$id" | sed 's|-|/|')

    line=$(grep -m1 '^gate-3:' "$status" 2>/dev/null || true)
    [ -n "$line" ] || continue

    verdict=$(printf '%s' "$line" | sed 's/^gate-3:[[:space:]]*//' | cut -d' ' -f1)
    merged=$(git -C "$repo" log "$ref" --oneline -F --grep "($short)" | head -n 1)
    # Merged through an MR or by a local squash? The former leaves a GitLab
    # merge commit, the latter doesn't. We don't require a link from the
    # latter.
    via_mr=$(git -C "$repo" log "$ref" --oneline -F \
        --grep "Merge branch '$branch' into 'main'" | head -n 1)

    case "$verdict" in
        pending)
            if [ -n "$merged" ]; then
                stale=$((stale + 1))
                printf 'STALE      %s\n' "$id"
                printf '           file says pending, in %s: %s\n' "$ref" "$merged"
                printf '           %s\n\n' "$status"
            fi
            ;;
        approved)
            if [ -z "$via_mr" ]; then
                legacy=$((legacy + 1))
            else
                case "$line" in
                    *http*) ;;
                    *)
                        linkless=$((linkless + 1))
                        printf 'NO LINK    %s\n' "$id"
                        printf '           merged through an MR, no link in the record: %s\n\n' "$line"
                        ;;
                esac
            fi
            ;;
    esac
done

printf 'Tasks checked: %s · stale pending: %s · approved without a link: %s\n' \
    "$checked" "$stale" "$linkless"
# Skipped ones are called out: a silent skip reads as "all clean".
printf 'Skipped as pre-GitLab (merged by local squash, no MR): %s\n' "$legacy"

if [ "$stale" -gt 0 ] || [ "$linkless" -gt 0 ]; then
    printf '\nFix in one pass via the F-route: one branch, one edit to the\nrecords, one MR. The script changes nothing itself.\n'
fi
