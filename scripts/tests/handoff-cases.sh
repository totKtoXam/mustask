#!/bin/sh
# Regression cases for scripts/handoff.sh: which task a short id resolves to.
#
# The defect (bug-001): the last fallback, a task that is neither queued in
# main nor has a folder there, looked the id up among the branches with the
# prefix stripped — `^[a-z]+/001-` for mod-001. Numbers are per prefix
# (docs/features/numbering.md), so doc/001-domain-map matched first and
# `handoff.sh mod-001` reported doc-001 as already merged.
#
# The cases build a throwaway repository — main plus a few task branches, the
# shape the real one had when the defect showed — and run the real script
# against it from outside, so they do not depend on this checkout's branches.
#
# Usage: sh scripts/tests/handoff-cases.sh
# (verify.sh runs it when handoff.sh or this file changes)

set -eu

script=$(cd "$(dirname "$0")/.." && pwd)/handoff.sh
[ -f "$script" ] || { printf 'handoff.sh not found next to %s\n' "$0" >&2; exit 1; }

sandbox=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/handoff-cases-$$"; mkdir -p "$d"; printf '%s' "$d"; })
trap 'rm -rf "$sandbox"' EXIT
trap 'exit 130' INT TERM

# The user's own git configuration (hooks, signing, branch.sort) stays out:
# a rejecting core.hooksPath would fail the setup, and a branch.sort that lists
# mod/ before doc/ would let case 1 pass against the unfixed script.
mkdir -p "$sandbox/home"
: > "$sandbox/gitconfig"
export HOME="$sandbox/home" GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="$sandbox/gitconfig"
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE XDG_CONFIG_HOME \
    GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT 2>/dev/null || true
repo="$sandbox/repo"

g() { git -C "$repo" -c user.name=cases -c user.email=cases@example.invalid -c commit.gpgsign=false "$@"; }

status_file() {  # <id> <stage>
    mkdir -p "$repo/docs/features/$1"
    printf '# %s\n\n- type: x\n- size: S\n- stage: %s\n' "$1" "$2" > "$repo/docs/features/$1/status.md"
}

# --- the repository --------------------------------------------------------------

git init -q "$repo"
g symbolic-ref HEAD refs/heads/main
mkdir -p "$repo/docs/features"
printf '# Backlog\n\n## Next\n\n' > "$repo/docs/backlog.md"
g add -A && g commit -qm base

# doc-001: merged — its folder is in main with stage complete, its branch kept.
g branch doc/001-domain-map
status_file doc-001-domain-map complete
g add -A && g commit -qm 'merge doc-001'

# mod-001: in flight — shares the number with doc-001, lives only on its branch.
g checkout -q -b mod/001-process-seed
status_file mod-001-process-seed 01-business-analysis
g add -A && g commit -qm 'start mod-001'
g checkout -q main

# tech/002: in flight; feat-002 exists nowhere.
g branch tech/002-something

# --- the cases -------------------------------------------------------------------

failures=0
out="$sandbox/prompt.md"

run() {  # <short-id> — sets rc and log
    rc=0
    log=$(cd "$repo" && sh "$script" "$1" --command-only --out "$out" 2>&1) || rc=$?
}

check() {  # <description> <condition-exit-code>
    if [ "$2" = 0 ]; then
        printf 'PASS  %s\n' "$1"
    else
        printf 'FAIL  %s\n' "$1"
        printf '%s\n' "$log" | sed 's/^/      | /'
        failures=$((failures + 1))
    fi
}

# 1. The defect itself: the number shared with a merged task of another type.
rm -f "$out"
run mod-001
ok=1
[ "$rc" = 0 ] && grep -q '^Continue task mod-001-process-seed\.' "$out" 2>/dev/null && ok=0
check "mod-001 resolves to its own branch, not to doc-001 with the same number" "$ok"

# 2. The only branch with the number has another prefix: no such task.
run feat-002
ok=1
[ "$rc" != 0 ] && printf '%s' "$log" | grep -q "No task 'feat-002'" && ok=0
check "feat-002 is not found when only tech/002-* exists" "$ok"

# 3. A merged task is still reported as merged — through the folder in main.
run doc-001
ok=1
[ "$rc" != 0 ] && printf '%s' "$log" | grep -q 'doc-001-domain-map is already merged' && ok=0
check "doc-001 is still reported as already merged" "$ok"

# 4. An in-flight task whose number no other task shares.
rm -f "$out"
run tech-002
ok=1
[ "$rc" = 0 ] && grep -q '^Continue task tech-002-something\.' "$out" 2>/dev/null && ok=0
check "tech-002 resolves to tech/002-something" "$ok"

if [ "$failures" != 0 ]; then
    printf '\n%s handoff.sh case(s) failed.\n' "$failures" >&2
    exit 1
fi
