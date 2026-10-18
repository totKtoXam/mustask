#!/bin/sh
# Cases for scripts/verify.sh itself. The green stamp (tech-030): when a full run
# leaves a stamp, when a later full run takes it, and when it must not. The tooling
# check of the executable bit (tech-039): the mode git holds decides, not the
# working tree; and of the syntax: the shebang's interpreter decides, not `sh`.
#
# The cases build a throwaway repository (main plus a task branch that changes
# documents only, so verify.sh runs just the cheap prose checks) and run the real
# script against it from outside.
#
# Usage: sh scripts/tests/verify-stamp-cases.sh
# (verify.sh runs it when verify.sh or this file changes)

set -eu

script=$(cd "$(dirname "$0")/.." && pwd)/verify.sh
[ -f "$script" ] || { printf 'verify.sh not found next to %s\n' "$0" >&2; exit 1; }

sandbox=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/verify-stamp-cases-$$"; mkdir -p "$d"; printf '%s' "$d"; })
trap 'rm -rf "$sandbox"' EXIT
trap 'exit 130' INT TERM

mkdir -p "$sandbox/home"
: > "$sandbox/gitconfig"
export HOME="$sandbox/home" GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="$sandbox/gitconfig"
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE XDG_CONFIG_HOME VERIFY_FULL VERIFY_FORCE BASE \
    GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT 2>/dev/null || true
repo="$sandbox/repo"

g() { git -C "$repo" -c user.name=cases -c user.email=cases@example.invalid -c commit.gpgsign=false "$@"; }
run() { (cd "$repo" && sh "$script" "$@" 2>&1); }
stamps() { ls "$repo/.git/verify-stamps" 2>/dev/null | wc -l | tr -d ' '; }

mkdir -p "$repo/docs"
git init -q -b main "$repo" 2>/dev/null || { git init -q "$repo"; g checkout -q -b main; }
printf '# base\n' > "$repo/docs/base.md"
g add -A && g commit -qm base
g checkout -q -b task
printf '# one\n' > "$repo/docs/one.md"
g add -A && g commit -qm one

pass=0
check() {  # <name> <condition result: 0 = holds>
    if [ "$2" = 0 ]; then pass=$((pass + 1)); printf '  ok: %s\n' "$1"
    else printf '  FAIL: %s\n' "$1" >&2; failed=1; fi
}
failed=0
has() { printf '%s' "$1" | grep -q "$2"; }

printf 'verify-stamp-cases:\n'

# 1. a green full run on a clean tree writes a stamp
out=$(VERIFY_FULL=1 run); has "$out" 'RESULT: green'; check 'a full run is green' $?
[ "$(stamps)" = 1 ]; check 'and leaves one stamp' $?

# 2. the same tree is not verified again
out=$(VERIFY_FULL=1 run); has "$out" 'Already verified green'; check 'the same tree takes the stamp' $?

# 3. VERIFY_FORCE ignores it
out=$(VERIFY_FULL=1 VERIFY_FORCE=1 run); has "$out" 'Already verified green' && r=1 || r=0
check 'VERIFY_FORCE=1 runs the checks' $r
[ "$(stamps)" = 1 ]; check 'and writes the stamp back' $?

# 4. an affected-only run never writes or takes a stamp
before=$(stamps); out=$(run); has "$out" 'Already verified green' && r=1 || r=0
check 'a run without VERIFY_FULL does not take a stamp' $r
[ "$(stamps)" = "$before" ]; check 'and writes none' $?

# 5. a new commit is a new tree
printf '# two\n' > "$repo/docs/two.md"; g add -A && g commit -qm two
out=$(VERIFY_FULL=1 run); has "$out" 'Already verified green' && r=1 || r=0
check 'a commit makes a miss' $r
[ "$(stamps)" = 2 ]; check 'and a second stamp' $?

# 6. a dirty tree neither takes nor writes a stamp
printf '\nedit\n' >> "$repo/docs/two.md"
before=$(stamps); out=$(VERIFY_FULL=1 run); has "$out" 'Already verified green' && r=1 || r=0
check 'a modified file: no hit' $r
[ "$(stamps)" = "$before" ]; check 'a modified file: no stamp written' $?
g checkout -q -- docs/two.md

# 7. an untracked file is a dirty tree too
printf '# new\n' > "$repo/docs/untracked.md"
before=$(stamps); out=$(VERIFY_FULL=1 run); has "$out" 'Already verified green' && r=1 || r=0
check 'an untracked file: no hit' $r
[ "$(stamps)" = "$before" ]; check 'an untracked file: no stamp written' $?
rm -f "$repo/docs/untracked.md"

# 8. another merge-base is another key
before=$(stamps); out=$(BASE=HEAD~1 VERIFY_FULL=1 run); has "$out" 'Already verified green' && r=1 || r=0
check 'another BASE: no hit' $r
[ "$(stamps)" = $((before + 1)) ]; check 'another BASE: its own stamp' $?

# 9. a skipped check leaves no stamp, and removes an older one (a forced, partial run)
mkdir -p "$repo/scripts"; printf 'x = 1\n' > "$repo/scripts/probe.py"
g add -A && g commit -qm probe
out=$(VERIFY_FULL=1 run); has "$out" 'RESULT: green'; check 'with python the probe is green' $?
if [ "$(stamps)" -gt 3 ]; then
    before=$(stamps)
    out=$(VERIFY_FULL=1 VERIFY_FORCE=1 VERIFY_PYTHON='' run); has "$out" 'NOT CHECKED'; check 'without python a check is skipped' $?
    [ "$(stamps)" = $((before - 1)) ]; check 'a partial forced run removes the old stamp' $?
    out=$(VERIFY_FULL=1 run); has "$out" 'Already verified green' && r=1 || r=0
    check 'so the next run is real' $r
else
    printf '  skipped: no python here, the partial-run case needs one\n'
fi

# 10. the executable bit is the mode in the index (tech-039). A script committed
# as 100644 fails the tooling check even where the working tree says "executable"
# (Windows answers yes to every *.sh), and the same script at 100755 passes even
# where the working tree is not executable (a checkout that never ran chmod). Each
# half fails against the `[ -x ]` check on one of the two platforms.
printf '#!/bin/sh\nexit 0\n' > "$repo/scripts/probe.sh"
g update-index --add --chmod=-x scripts/probe.sh && g commit -qm 'probe script, 100644'
out=$(run) && rc=0 || rc=$?
[ "$rc" != 0 ] && r=0 || r=1
check 'a script committed as 100644: the run fails' $r
has "$out" 'scripts/probe.sh(mode 100644' && r=0 || r=1
check 'and names the file and its mode' $r
g update-index --chmod=+x scripts/probe.sh && g commit -qm 'probe script, 100755'
out=$(run) && rc=0 || rc=$?
[ "$rc" = 0 ] && has "$out" 'RESULT: green' && r=0 || r=1
check 'the same script at 100755: green' $r

# 11. the syntax is read by the interpreter the shebang names (tech-039). A bash
# script with bash-only syntax (an array, `[[ ]]`) is green: `sh -n` would reject
# it where sh is dash (the Linux CI) though Git Bash's sh, which is bash, accepts
# it. The other half runs anywhere: a bash script that is broken is still red.
printf '#!/usr/bin/env bash\nf() { local a=(1 2); [[ ${a[0]} = 1 ]] && echo ok; }\nf\n' > "$repo/scripts/probe-bash.sh"
g update-index --add --chmod=+x scripts/probe-bash.sh && g commit -qm 'probe bash script'
out=$(run) && rc=0 || rc=$?
[ "$rc" = 0 ] && has "$out" 'RESULT: green' && r=0 || r=1
check 'a bash-shebang script with bash-only syntax: green' $r
printf '#!/usr/bin/env bash\nf() { local a=(1 2\n' > "$repo/scripts/probe-bash.sh"
g commit -qam 'probe bash script, broken'
out=$(run) && rc=0 || rc=$?
[ "$rc" != 0 ] && has "$out" 'scripts/probe-bash.sh(syntax)' && r=0 || r=1
check 'and a broken one is red, named by file' $r

printf '%s passed\n' "$pass"
[ "$failed" = 0 ]
