#!/bin/sh
# Regression cases for queue numbering: which numbers `scripts/backlog.sh --ids`
# reports as taken, and so what `scripts/queue.sh add` and `scripts/new-task.sh
# hot` may issue.
#
# The defects (tech-034):
#   1. backlog.sh read docs/backlog.md and docs/backlog/withdrawn.md only from
#      origin/main. `queue.sh add` commits to LOCAL main, so two adds before a
#      push issued the same number (feat-039 and mod-017 were issued twice
#      that way). docs/features/numbering.md, source 3, says "across all
#      branches".
#   2. new-task.sh's hot path scanned withdrawn.md with a missing `|` before
#      `grep`, so a withdrawn hot number was never seen and was reissued.
#
# Each case builds its own throwaway repository with a BARE origin (the push
# is what makes "unpushed" mean something), copies the scripts under test into
# its scripts/ and runs them from there, the way they run in the real
# repository. Nothing here touches this checkout's branches or queue.
#
# Usage: sh scripts/tests/queue-numbering-cases.sh [--scripts <dir>] [--shell <sh>]
# (verify.sh runs it with no arguments when backlog.sh, queue.sh, new-task.sh or
# this file changes)
#
# Two options, for checking the cases themselves:
#   --scripts <dir>  take backlog.sh, queue.sh, new-task.sh from <dir> instead of
#                    scripts/ (e.g. an unfixed copy, to see the cases fail)
#   --shell <sh>     the shell that runs those three (default sh; e.g. dash)

set -eu

here=$(cd "$(dirname "$0")/.." && pwd)
src=$here
shell=sh
while [ $# -gt 0 ]; do
    case "$1" in
        --scripts) src=${2:?--scripts needs a directory}; shift 2 ;;
        --shell)   shell=${2:?--shell needs a shell}; shift 2 ;;
        *) printf 'Unknown option: %s\n' "$1" >&2; exit 2 ;;
    esac
done
src=$(cd "$src" 2>/dev/null && pwd) || { printf 'No such directory: %s\n' "$src" >&2; exit 2; }
for name in backlog.sh queue.sh new-task.sh; do
    [ -f "$src/$name" ] || { printf '%s not found in %s\n' "$name" "$src" >&2; exit 1; }
done

sandbox=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/queue-numbering-cases-$$"; mkdir -p "$d"; printf '%s' "$d"; })
trap 'rm -rf "$sandbox"' EXIT
trap 'exit 130' INT TERM

# The user's own git configuration (hooks, signing, autocrlf, branch.sort)
# stays out: a rejecting core.hooksPath would fail the queue commits, and the
# identity has to come from somewhere because the scripts commit on their own.
mkdir -p "$sandbox/home"
printf '[user]\n\tname = cases\n\temail = cases@example.invalid\n[commit]\n\tgpgsign = false\n' \
    > "$sandbox/gitconfig"
export HOME="$sandbox/home" GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="$sandbox/gitconfig"
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE XDG_CONFIG_HOME \
    GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT 2>/dev/null || true
repo="$sandbox/repo"
origin="$sandbox/origin.git"

g() { git -C "$repo" "$@"; }

# A pushed starting point: tech-010 is queued in main and on origin/main.
build() {
    rm -rf "$origin" "$repo"
    git init -q --bare "$origin"
    git -C "$origin" symbolic-ref HEAD refs/heads/main
    git init -q "$repo"
    g symbolic-ref HEAD refs/heads/main
    mkdir -p "$repo/scripts" "$repo/docs/features" "$repo/docs/backlog"
    cp "$src/backlog.sh" "$src/queue.sh" "$src/new-task.sh" "$repo/scripts/"
    printf '.claude/worktrees/\n' > "$repo/.gitignore"
    {
        printf '# Backlog\n\n## Next\n\n'
        printf -- '- [ ] tech-010-existing — Existing · epic:workflow · size:S · deps:- · note:-\n'
        printf '\n## Later\n\n'
    } > "$repo/docs/backlog.md"
    printf '# Withdrawn\n\n' > "$repo/docs/backlog/withdrawn.md"
    g add -A
    g commit -qm base
    g remote add origin "$origin"
    g push -q origin main
}

failures=0
rc=0
log=''

run() {  # <command...> — runs in the repo; sets rc and log
    rc=0
    log=$(cd "$repo" && "$@" 2>&1) || rc=$?
}

queue() { run "$shell" scripts/queue.sh "$@"; }

issued() {  # <slug> — the full id the last `queue.sh add` printed for this slug
    printf '%s\n' "$log" | grep -E "^[a-z]+-[0-9]{3}-$1\$" | tail -1 || true
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

# 1. The defect itself: two adds on local main before a push.
build
queue add tech first "First"
rc_first=$rc; id_first=$(issued first)
queue add tech second "Second"
id_second=$(issued second)
ahead=$(g rev-list --count origin/main..main)
ok=1
[ "$rc_first" = 0 ] && [ "$rc" = 0 ] && [ "$ahead" = 2 ] \
    && [ "$id_first" = tech-011-first ] && [ "$id_second" = tech-012-second ] && ok=0
log="local main is $ahead commit(s) ahead of origin/main (expected 2)
first add:  ${id_first:-none} (expected tech-011-first)
second add: ${id_second:-none} (expected tech-012-second)
$log"
check "two adds before a push issue two different numbers" "$ok"

# 2. A withdraw that is still unpushed: the number lives only in withdrawn.md on
#    local main, and must not be issued again.
build
queue add tech doomed "Doomed"
id_doomed=$(issued doomed)
queue withdraw tech-011 "changed my mind"
rc_withdraw=$rc
queue add tech after "After"
id_after=$(issued after)
ok=1
[ "$rc_withdraw" = 0 ] && [ "$rc" = 0 ] && [ "$id_doomed" = tech-011-doomed ] \
    && [ "$id_after" = tech-012-after ] && ok=0
log="added ${id_doomed:-none}, withdrew it (rc $rc_withdraw), then added ${id_after:-none} (expected tech-012-after)
$log"
check "an add after an unpushed withdraw does not reuse the withdrawn number" "$ok"

# 3. The hot path of new-task.sh: a withdrawn hot number is never reissued. The
#    queue is pushed first, so this is the missing pipe and nothing else.
build
rc_setup=0
queue add hot one "One"
[ "$rc" = 0 ] || rc_setup=$rc
g push -q origin main
queue add hot two "Two"
[ "$rc" = 0 ] || rc_setup=$rc
g push -q origin main
queue withdraw hot-002 "not needed"
[ "$rc" = 0 ] || rc_setup=$rc
g push -q origin main
run "$shell" scripts/new-task.sh hot fix-it "A fix"
rc_hot=$rc
ok=1
if [ "$rc_setup" = 0 ] && [ "$rc_hot" = 0 ] && g show-ref --verify --quiet refs/heads/hot/003-fix-it; then ok=0; fi
log="hot-001 is queued, hot-002 is withdrawn, all pushed (setup exit $rc_setup)
new-task.sh hot exit $rc_hot, branches: $(g branch --list 'hot/*' | tr -d ' *+' | tr '\n' ' ')(expected hot/003-fix-it)
$log"
check "new-task.sh hot skips a withdrawn hot number" "$ok"

# 4. The summary: numbers of unpushed queue commits are counted, their lines
#    are not listed, and one line says they exist.
build
queue add tech unpushed "Unpushed"
run "$shell" scripts/backlog.sh
ok=1
if printf '%s\n' "$log" | grep -qE '^  tech +taken up to 011, next 012$' \
    && ! printf '%s\n' "$log" | grep -q 'tech-011-unpushed' \
    && printf '%s\n' "$log" | grep -q 'tech-010-existing' \
    && [ "$(printf '%s\n' "$log" | grep -c 'unpushed queue commit')" = 1 ] \
    && printf '%s\n' "$log" | grep -qE '^  [(][+]1 unpushed queue commit'; then
    ok=0
fi
log="expected: 'tech taken up to 011, next 012', tech-011-unpushed not listed, one '(+1 unpushed queue commit...' line; got:
$log"
check "summary counts the unpushed number, lists only origin/main, says so in one line" "$ok"

# 5. No such line when there is nothing to say: all pushed, or the only unpushed
#    commit leaves the queue files alone.
build
run "$shell" scripts/backlog.sh
quiet_pushed=0
printf '%s\n' "$log" | grep -q 'unpushed queue commit' || quiet_pushed=1
printf 'notes\n' > "$repo/notes.txt"
g add notes.txt
g commit -qm 'unrelated commit on main'
run "$shell" scripts/backlog.sh
quiet_other=0
printf '%s\n' "$log" | grep -q 'unpushed queue commit' || quiet_other=1
ok=1
[ "$quiet_pushed" = 1 ] && [ "$quiet_other" = 1 ] && ok=0
log="nothing unpushed: $( [ "$quiet_pushed" = 1 ] && echo quiet || echo noisy ); unpushed non-queue commit: $( [ "$quiet_other" = 1 ] && echo quiet || echo noisy )
$log"
check "no unpushed-queue line when nothing queue-related is unpushed" "$ok"

# 6. numbering.md, source 3: the queue files over every branch, local and
#    remote-tracking, and nothing outside refs/heads and refs/remotes.
build
g checkout -q -b scratch/local
printf -- '- [ ] tech-040-branch-only — x · epic:workflow · size:S · deps:- · note:-\n' >> "$repo/docs/backlog.md"
g commit -qam 'queue line in a branch'
g checkout -q -b scratch/remote main
printf -- '- [x] tech-050-remote-only — withdrawn: x, 2026-01-01\n' >> "$repo/docs/backlog/withdrawn.md"
g commit -qam 'withdrawn line in another branch'
g push -q origin scratch/remote
g checkout -q main
g branch -q -D scratch/remote
printf -- '- [ ] tech-060-stashed — x · epic:workflow · size:S · deps:- · note:-\n' >> "$repo/docs/backlog.md"
g stash push -q -m cases
run "$shell" scripts/backlog.sh --ids
ok=1
if printf '%s\n' "$log" | grep -qx 'tech-040' \
    && printf '%s\n' "$log" | grep -qx 'tech-050' \
    && ! printf '%s\n' "$log" | grep -qx 'tech-060'; then
    ok=0
fi
log="expected tech-040 (local branch) and tech-050 (remote branch) present, tech-060 (stash) absent; got:
$log"
check "ids come from the queue files of every branch and remote branch, not from the stash" "$ok"

# 7. One pass: the number of git processes does not grow with the number of refs.
count_git() {  # <trace file> — git processes one `backlog.sh --ids` starts
    : > "$1"
    ( cd "$repo" && GIT_TRACE="$1" "$shell" scripts/backlog.sh --ids >/dev/null 2>&1 ) || true
    grep -c '' "$1" || true
}
build
trace="$(cd "$sandbox" && { pwd -W 2>/dev/null || pwd; })/trace.log"
few=$(count_git "$trace")
n=0
while [ "$n" -lt 12 ]; do
    n=$((n + 1))
    g branch "scratch/b$n"
done
many=$(count_git "$trace")
ok=1
[ "$few" -gt 0 ] && [ "$few" = "$many" ] && ok=0
log="git processes per backlog.sh --ids: $few with 1 branch, $many with 13 (they must be equal)"
check "reading the queue files is one pass, not a git process per ref" "$ok"

if [ "$failures" != 0 ]; then
    printf '\n%s queue numbering case(s) failed.\n' "$failures" >&2
    exit 1
fi
