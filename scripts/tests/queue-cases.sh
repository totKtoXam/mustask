#!/bin/sh
# Regression cases for scripts/queue.sh: the queue lands through a PR (tech-049).
#
# Before tech-049 every subcommand committed on the local main of the root
# checkout; main takes changes only through PRs, so each commit was hand-moved
# to a sync/* branch and main reset, and a commit nobody moved sat unpushed on
# main for days. The cases pin the new shape: the commit goes to a pushed
# sync/queue-* branch built from origin/main, main is not touched, the
# pushed branch holds its number, and --local keeps the old commit on main.
#
# The cases build a throwaway clone with a bare origin and run the real
# scripts against it, with a stub in place of gh (QUEUE_GH).
#
# Usage: sh scripts/tests/queue-cases.sh
# (verify.sh runs it when queue.sh, backlog.sh or this file changes)

set -eu

scripts=$(cd "$(dirname "$0")/.." && pwd)
[ -f "$scripts/queue.sh" ] || { printf 'queue.sh not found next to %s\n' "$0" >&2; exit 1; }

sandbox=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/queue-cases-$$"; mkdir -p "$d"; printf '%s' "$d"; })
trap 'rm -rf "$sandbox"' EXIT
trap 'exit 130' INT TERM

# The user's own git configuration (hooks, signing) stays out: the repository's
# pre-push runs verify.sh, which has no business inside a sandbox.
mkdir -p "$sandbox/home"
printf '[user]\n\tname = cases\n\temail = cases@example.invalid\n[commit]\n\tgpgsign = false\n[init]\n\tdefaultBranch = main\n' \
    > "$sandbox/gitconfig"
export HOME="$sandbox/home" GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="$sandbox/gitconfig"
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE XDG_CONFIG_HOME \
    GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT 2>/dev/null || true

origin="$sandbox/origin.git"
repo="$sandbox/repo"
today=$(date +%Y-%m-%d)

g() { git -C "$repo" "$@"; }

# --- the stub gh -----------------------------------------------------------------
#
# Records every call; `pr create` answers with a URL, `pr merge` succeeds unless
# STUB_MERGE=fail, `pr list` finds nothing.
stub="$sandbox/gh-stub"
cat > "$stub" <<'STUB'
#!/bin/sh
printf '%s\n' "$*" >> "$STUB_LOG"
case "$1 $2" in
    'pr create') printf 'https://example.invalid/pull/1\n' ;;
    'pr merge') [ "${STUB_MERGE:-ok}" = ok ] || { printf 'auto-merge is not allowed\n' >&2; exit 1; } ;;
    'pr list') ;;
esac
STUB
chmod +x "$stub"
export STUB_LOG="$sandbox/gh.log"
: > "$STUB_LOG"

# --- the repository ----------------------------------------------------------------

git init -q --bare "$origin"
git init -q "$repo"
mkdir -p "$repo/scripts" "$repo/docs/backlog"
cp "$scripts/queue.sh" "$scripts/backlog.sh" "$repo/scripts/"
printf '# Backlog\n\n## Next\n\n## Later\n' > "$repo/docs/backlog.md"
printf '# Withdrawn\n\n' > "$repo/docs/backlog/withdrawn.md"
g add -A && g commit -qm base
g remote add origin "$origin"
g push -q origin main
g fetch -q origin

failures=0
case_no=0

check() {  # <description> <command...>
    case_no=$((case_no + 1))
    description=$1
    shift
    if "$@"; then
        printf 'ok   %s. %s\n' "$case_no" "$description"
    else
        printf 'FAIL %s. %s\n' "$case_no" "$description"
        failures=$((failures + 1))
    fi
}

run() {  # <output-file> <args...>: runs queue.sh from the clone, keeps its exit code
    out=$1
    shift
    set +e
    (cd "$repo" && sh scripts/queue.sh "$@") > "$out" 2>&1
    status=$?
    set -e
    return 0
}

contains() { grep -q -F -e "$2" "$1"; }
remote_has() { git --git-dir="$origin" show-ref --verify --quiet "refs/heads/$1"; }
remote_queue() { git --git-dir="$origin" show "$1:docs/backlog.md"; }

main_before=$(g rev-parse main)

# --- 1. add lands as a PR, main untouched ---------------------------------------------

QUEUE_GH=$stub run "$sandbox/out1" add tech foo "Foo work"
check "add exits 0" [ "$status" = 0 ]
check "add pushes sync/queue-$today-add-tech-001" remote_has "sync/queue-$today-add-tech-001"
check "the pushed branch carries the queue line" \
    sh -c "git --git-dir='$origin' show 'sync/queue-$today-add-tech-001:docs/backlog.md' | grep -q '^- \[ \] tech-001-foo — Foo work'"
check "the local main did not move" [ "$(g rev-parse main)" = "$main_before" ]
check "origin main did not move" [ "$(git --git-dir="$origin" rev-parse main)" = "$main_before" ]
check "no local sync branch is created" \
    sh -c "! git -C '$repo' show-ref --verify --quiet 'refs/heads/sync/queue-$today-add-tech-001'"
check "the throwaway worktree is removed" [ "$(g worktree list | grep -c '')" = 1 ]
check "a squash PR is opened into main" contains "$STUB_LOG" "pr create --base main --head sync/queue-$today-add-tech-001 --title Queue tech-001-foo"
check "auto-merge is asked for" contains "$STUB_LOG" "pr merge https://example.invalid/pull/1 --auto --squash"
check "the PR URL is printed" contains "$sandbox/out1" "PR: https://example.invalid/pull/1"

# --- 2. the unmerged sync branch holds its number; no gh ------------------------------

QUEUE_GH="$sandbox/no-such-gh" run "$sandbox/out2" add tech bar "Bar work"
check "second add exits 0 without gh" [ "$status" = 0 ]
check "second add issues tech-002, not tech-001 again" remote_has "sync/queue-$today-add-tech-002"
check "without gh, what is left for the human is printed" contains "$sandbox/out2" "Left for a human"

# --- 3. auto-merge refused ----------------------------------------------------------------

STUB_MERGE=fail QUEUE_GH=$stub run "$sandbox/out3" add tech baz "Baz work"
check "a refused auto-merge still exits 0" [ "$status" = 0 ]
check "a refused auto-merge is said out loud" contains "$sandbox/out3" "Auto-merge was refused"

# --- 4. take: refused while the add PR is open, lands once it merged ------------------

g branch tech/001-foo main
QUEUE_GH=$stub run "$sandbox/out4" take tech-001
check "take of a line only in an open add PR is refused" [ "$status" != 0 ]
check "the refusal points at the open add PR" contains "$sandbox/out4" "add PR that is still open"

# The merge of the add PR, as GitHub would do it.
git --git-dir="$origin" update-ref refs/heads/main "refs/heads/sync/queue-$today-add-tech-001"
QUEUE_GH=$stub run "$sandbox/out5" take tech-001
check "take exits 0 once the line is on origin/main" [ "$status" = 0 ]
check "take pushes sync/queue-$today-take-tech-001 without the line" \
    sh -c "git --git-dir='$origin' rev-parse -q --verify 'sync/queue-$today-take-tech-001' >/dev/null && ! git --git-dir='$origin' show 'sync/queue-$today-take-tech-001:docs/backlog.md' | grep -q 'tech-001-foo'"
check "take leaves the local main where it was" [ "$(g rev-parse main)" = "$main_before" ]

# --- 5. --local commits on main, as before --------------------------------------------

QUEUE_GH=$stub run "$sandbox/out6" --local add tech qux "Qux work"
check "--local add exits 0" [ "$status" = 0 ]
check "--local add commits on the local main" \
    sh -c "git -C '$repo' log -1 --format=%s main | grep -q '^Queue tech-'"
check "--local add pushes nothing" sh -c "! git --git-dir='$origin' for-each-ref --format='%(refname)' | grep -q 'qux'"

# --- 6. no origin: --local by itself ----------------------------------------------------

g remote remove origin
QUEUE_GH=$stub run "$sandbox/out7" add tech quux "Quux work"
check "with no origin, add falls back to the local main" \
    sh -c "[ $status = 0 ] && git -C '$repo' log -1 --format=%s main | grep -q 'quux'"

if [ "$failures" -gt 0 ]; then
    printf '\n%s of %s queue.sh cases failed. Outputs:\n' "$failures" "$case_no" >&2
    for f in "$sandbox"/out*; do printf '\n--- %s\n' "$f" >&2; cat "$f" >&2; done
    exit 1
fi
printf '\nAll %s queue.sh cases pass.\n' "$case_no"
