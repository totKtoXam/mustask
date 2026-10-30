#!/bin/sh
# Cases for scripts/verify.sh itself (tech-044): the comparison base, and a test
# run that aborts.
#
# 1. The base. Without BASE, verify.sh compares with origin/main when that ref
#    exists (a worktree's local `main` is stale), else with main; an explicit
#    BASE wins. The cases give the throwaway repository a local main that is one
#    commit behind its origin/main and look at which files verify.sh lists.
# 2. The abort. A test step passes on its exit code AND on what the run printed:
#    "The active test run was aborted" / "Test host process crashed" from dotnet
#    and a Vitest run with no summary fail the step even at exit code 0. The
#    cases put stubs of dotnet, docker and npm first on PATH, so the real
#    verify.sh runs its real code paths without a real build.
#
# Usage: sh scripts/tests/verify-base-and-abort-cases.sh
# (verify.sh runs it when verify.sh or this file changes)

set -eu

script=$(cd "$(dirname "$0")/.." && pwd)/verify.sh
[ -f "$script" ] || { printf 'verify.sh not found next to %s\n' "$0" >&2; exit 1; }

sandbox=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/verify-base-cases-$$"; mkdir -p "$d"; printf '%s' "$d"; })
trap 'rm -rf "$sandbox"' EXIT
trap 'exit 130' INT TERM

mkdir -p "$sandbox/home" "$sandbox/bin"
: > "$sandbox/gitconfig"
export HOME="$sandbox/home" GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="$sandbox/gitconfig"
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE XDG_CONFIG_HOME VERIFY_FULL VERIFY_FORCE BASE \
    GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT DOTNET_CLI_UI_LANGUAGE VSLANG \
    STUB_DOTNET_TEST STUB_DOCKER STUB_NPM_TEST 2>/dev/null || true
export VERIFY_PYTHON=''

pass=0
failed=0
# expect <name> <condition>: the condition is evaluated inside an `if`, so a red
# case is reported and the run goes on (under `set -e` a bare failing command
# would end the script silently).
expect() {
    if eval "$2"; then
        pass=$((pass + 1))
        printf '  ok: %s\n' "$1"
    else
        printf '  FAIL: %s\n' "$1" >&2
        failed=1
    fi
}
has() { printf '%s' "$1" | grep -q -- "$2"; }
hasnt() { if has "$1" "$2"; then return 1; else return 0; fi; }
code_of() { printf '%s' "$1" | sed -n 's/^EXIT=//p' | tail -n 1; }
green() { [ "$(code_of "$1")" = 0 ]; }
red() { [ "$(code_of "$1")" = 1 ]; }

# --- the throwaway repository ---------------------------------------------------------
repo="$sandbox/repo"
g() { git -C "$repo" -c user.name=cases -c user.email=cases@example.invalid -c commit.gpgsign=false "$@"; }
# run [verify.sh args]: the real script, from inside the repository, the stubs
# first on PATH; its output, then "EXIT=<code>" on the last line.
run() {
    (
        cd "$repo"
        rc=0
        PATH="$sandbox/bin:$PATH" sh "$script" "$@" 2>&1 || rc=$?
        printf '\nEXIT=%s\n' "$rc"
    )
}

mkdir -p "$repo/docs" "$repo/src/frontend/src"
git init -q -b main "$repo" 2>/dev/null || { git init -q "$repo"; g checkout -q -b main; }
printf '# base\n' > "$repo/docs/base.md"
printf 'node_modules/\n' > "$repo/.gitignore"
printf '{ "name": "fe", "scripts": { "test": "vitest" } }\n' > "$repo/src/frontend/package.json"
g add -A && g commit -qm base

printf 'verify-base-and-abort-cases:\n'

# --- 1. the base ----------------------------------------------------------------------
# origin/main is one commit ahead of the local main; the task branch grows from it.
g checkout -q -b up
printf '# upstream\n' > "$repo/docs/upstream.md"
g add -A && g commit -qm upstream
g update-ref refs/remotes/origin/main HEAD
g checkout -q -b task
printf '# task\n' > "$repo/docs/task.md"
g add -A && g commit -qm task

r=$(run --list)
expect 'no BASE, origin/main exists: compares with origin/main' 'has "$r" "Comparing with origin/main"'
expect 'and lists only what the branch changed' 'has "$r" "docs/task.md" && hasnt "$r" "docs/upstream.md"'

r=$(BASE=main run --list)
expect 'an explicit BASE=main wins, and sees the stale main as all history' \
    'has "$r" "Comparing with main (" && has "$r" "docs/task.md" && has "$r" "docs/upstream.md"'

r=$(BASE='' run --list)
expect 'an empty BASE counts as unset' 'has "$r" "Comparing with origin/main"'

g update-ref -d refs/remotes/origin/main
r=$(run --list)
expect 'no origin/main: falls back to main' 'has "$r" "Comparing with main ("'

# --- 2. an aborted test run -----------------------------------------------------------
# Stubs. dotnet prints its abort text in another language unless asked for English
# (DOTNET_CLI_UI_LANGUAGE=en), as the maintainer's machine does: the English markers
# can only match if verify.sh sets the language itself.
cat > "$sandbox/bin/dotnet" <<'STUB'
#!/bin/sh
case "$1" in
    build) exit 0 ;;
    test)
        if [ "${DOTNET_CLI_UI_LANGUAGE:-}" = en ]; then
            aborted='The active test run was aborted. Reason: Test host process crashed : Process terminated.'
            ended='Test Run Aborted.'
        else
            aborted='[localized] the run was cut short, the host process is gone'
            ended='[localized] end'
        fi
        case "${STUB_DOTNET_TEST:-ok}" in
            ok)     echo 'Passed!  - Failed: 0, Passed: 3'; exit 0 ;;
            fail1)  echo 'Failed!  - Failed: 1, Passed: 2'; exit 1 ;;
            abort1) echo "$aborted"; echo "$ended"; exit 1 ;;
            abort0) echo "$aborted"; echo "$ended"; exit 0 ;;
        esac ;;
esac
exit 0
STUB
cat > "$sandbox/bin/docker" <<'STUB'
#!/bin/sh
case "$1" in
    info)  [ "${STUB_DOCKER:-up}" = up ]; exit $? ;;
    build) echo 'Tests run: 1, Failures: 0, Errors: 0'; exit 0 ;;
esac
exit 0
STUB
cat > "$sandbox/bin/npm" <<'STUB'
#!/bin/sh
case "$1" in
    run) exit 0 ;;
    test)
        case "${STUB_NPM_TEST:-ok}" in
            ok)        printf ' \033[2m Test Files \033[22m \033[1m\033[32m1 passed\033[39m\033[22m\033[90m (1)\033[39m\n'; exit 0 ;;
            nofiles)   echo 'No test files found, exiting with code 0'; exit 0 ;;
            nosummary) echo ' RUN  v3.2.7 /x'; echo 'Error: Channel closed'; exit 0 ;;
            fail1)     printf ' Test Files  1 failed (1)\n'; exit 1 ;;
        esac ;;
esac
exit 0
STUB
# node: scripts/coverage-gate.mjs (tech-046) is answered by STUB_GATE; anything
# else goes to the real node.
real_node=$(command -v node || echo node)
cat > "$sandbox/bin/node" <<STUB
#!/bin/sh
case "\$1" in
    scripts/coverage-gate.mjs)
        if [ "\${STUB_GATE:-ok}" = ok ]; then echo '  ok   stub floor'; exit 0; fi
        echo '  FAIL stub floor'; exit 1 ;;
esac
exec "$real_node" "\$@"
STUB
chmod +x "$sandbox/bin/dotnet" "$sandbox/bin/docker" "$sandbox/bin/npm" "$sandbox/bin/node"

# One branch per bucket, each from main. BASE=main: the stubs' repository has no origin.
export BASE=main

g checkout -q main
g checkout -q -b scn-dotnet
mkdir -p "$repo/src/backend/Foo" "$repo/src/backend/tests/Foo.Tests"
printf 'class X {}\n' > "$repo/src/backend/Foo/X.cs"
printf '[Fact] public void T() {}\n' > "$repo/src/backend/tests/Foo.Tests/T.cs"
g add -A && g commit -qm dotnet

r=$(run)
expect 'dotnet, tests pass: green' 'green "$r" && has "$r" "ok: backend tests, including integration"'
expect 'and its coverage floors are checked' 'has "$r" "ok: backend coverage floors"'
r=$(STUB_GATE=fail run)
expect 'dotnet, a coverage floor missed: red' 'red "$r" && has "$r" "FAIL: backend coverage below a floor"'
r=$(STUB_DOTNET_TEST=fail1 run)
expect 'dotnet, a failing test: red' 'red "$r" && has "$r" "RESULT: failed"'
r=$(STUB_DOTNET_TEST=abort1 run)
expect 'dotnet, an abort with exit code 1: red' 'red "$r" && has "$r" "RESULT: failed"'
r=$(STUB_DOTNET_TEST=abort0 run)
expect 'dotnet, an abort whose exit code was lost (0): red' 'red "$r" && has "$r" "FAIL: backend tests"'
expect 'and the run says why' 'has "$r" "counting it as failed"'
r=$(STUB_DOCKER=down run)
expect 'dotnet without Docker, tests pass: green' 'green "$r" && has "$r" "ok: tests src/backend/tests/Foo.Tests"'
r=$(STUB_DOTNET_TEST=abort0 STUB_DOCKER=down run)
expect 'dotnet without Docker, an abort at exit code 0: red, project named' \
    'red "$r" && has "$r" "FAIL: tests src/backend/tests/Foo.Tests"'

g checkout -q main
g checkout -q -b scn-frontend
printf 'export const a = 1;\n' > "$repo/src/frontend/src/a.ts"
mkdir -p "$repo/src/frontend/node_modules"
g add -A && g commit -qm frontend

r=$(run)
expect 'vitest, a summary: green' 'green "$r" && has "$r" "ok: tests src/frontend (affected)"'
r=$(STUB_NPM_TEST=nofiles run)
expect 'vitest, no test reaches the change (--passWithNoTests): green' 'green "$r"'
r=$(STUB_NPM_TEST=fail1 run)
expect 'vitest, a failing test: red' 'red "$r"'
r=$(STUB_NPM_TEST=nosummary run)
expect 'vitest, no summary at exit code 0: red' 'red "$r" && has "$r" "no Vitest summary"'

g checkout -q main
g checkout -q -b scn-provider
mkdir -p "$repo/deploy/keycloak"
printf 'FROM scratch\n' > "$repo/deploy/keycloak/Dockerfile"
g add -A && g commit -qm provider

r=$(run)
expect 'provider tests pass: green' 'green "$r" && has "$r" "ok: .NET tests tagged KeycloakProvider"'
r=$(STUB_DOTNET_TEST=abort0 run)
expect 'provider tests, an abort at exit code 0: red' 'red "$r" && has "$r" "FAIL: .NET tests tagged KeycloakProvider"'

printf '%s passed\n' "$pass"
[ "$failed" = 0 ]
