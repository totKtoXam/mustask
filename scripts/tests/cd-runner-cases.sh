#!/bin/sh
# Regression cases for CD on the production VM (tech-066).
#
# Three parts. The wrapper deploy/vm/mustask-cd.sh, the one thing the runner may
# run as root: what tarball it accepts, which smoke flags it passes, and that
# a deploy runs the release's own install.sh on the unpacked release. And the
# workflows: the repository is public, so a job on the self-hosted runner must
# never be reachable from a pull request — deploy.yml triggers only on v* tags
# and workflow_dispatch, only its deploy job targets the VM, and every other
# workflow stays on GitHub-hosted runners. And smoke.sh itself, run against a
# stub curl that models a VM behind an edge proxy (bug-006).
#
# The wrapper is sourced (its main only runs when it is executed), so the cases
# need no root and no VM. The workflow checks read the YAML as text: the files
# are plain enough, and the cases then need no python.
#
# Usage: sh scripts/tests/cd-runner-cases.sh
# (verify.sh runs it when the wrapper, runner-setup.sh, smoke.sh, a workflow or this
# file changes)

set -eu

root=$(cd "$(dirname "$0")/../.." && pwd)
wrapper=$root/deploy/vm/mustask-cd.sh
deploy=$root/.github/workflows/deploy.yml
[ -f "$wrapper" ] || { printf 'mustask-cd.sh not found under %s\n' "$root" >&2; exit 1; }
[ -f "$deploy" ] || { printf 'deploy.yml not found under %s\n' "$root" >&2; exit 1; }
command -v bash >/dev/null || { printf 'cd-runner-cases: bash is required\n' >&2; exit 1; }

sandbox=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/cd-runner-cases-$$"; mkdir -p "$d"; printf '%s' "$d"; })
trap 'rm -rf "$sandbox"' EXIT
trap 'exit 130' INT TERM

failures=0
ok() { printf '  ok    %s\n' "$1"; }
bad() { printf '  FAIL  %s\n' "$1"; failures=$((failures + 1)); }

# Runs a function of the sourced wrapper in bash; stdout and the exit code.
w() { bash -c '. "$1"; shift; "$@"' cd-cases "$wrapper" "$@" 2>/dev/null; }

# --- the tarball listing ---------------------------------------------------------
accepts() { # <name> <expected tag dir> <listing>
    got=$(w release_of_listing "$3") && [ "$got" = "$2" ] && ok "listing: $1" || bad "listing: $1 (got '${got:-refused}')"
}
refuses() { # <name> <listing>
    if w release_of_listing "$2" >/dev/null; then bad "listing: $1 was accepted"; else ok "listing: $1 refused"; fi
}

nl='
'
accepts "one release" mustask-v0.2.0 "mustask-v0.2.0/${nl}mustask-v0.2.0/VERSION${nl}mustask-v0.2.0/kit/install.sh"
accepts "members with ./" mustask-v1 "./${nl}./mustask-v1/${nl}./mustask-v1/VERSION"
refuses "empty" ""
refuses "two top directories" "mustask-v1/VERSION${nl}other/x"
refuses "an absolute member" "mustask-v1/VERSION${nl}/etc/sudoers.d/x"
refuses "a .. member" "mustask-v1/VERSION${nl}mustask-v1/../../etc/x"
refuses "a top .. member" "mustask-v1/VERSION${nl}../x"
refuses "not mustask-<tag>" "release/VERSION"
refuses "an empty tag" "mustask-/VERSION"
refuses "a tag with .." "mustask-v1..2/VERSION"
refuses "a tag with a space" "mustask-v1 x/VERSION"
refuses "a tag starting with a dash" "mustask--rf/VERSION"
refuses "a trailing .. member" "mustask-v1/VERSION${nl}mustask-v1/.."

# --- the smoke flags -------------------------------------------------------------
flags() { # <name> <file content> <expected, words joined by spaces | REFUSED>
    printf '%b' "$2" > "$sandbox/args"
    if got=$(w smoke_args_of "$sandbox/args"); then
        got=$(printf '%s' "$got" | tr '\n' ' ' | sed 's/ $//')
    else
        got=REFUSED
    fi
    [ "$got" = "$3" ] && ok "smoke flags: $1" || bad "smoke flags: $1 (got '$got', expected '$3')"
}
flags "empty file" "" ""
flags "resolve-local" "--resolve-local${nl}" "--resolve-local"
flags "both, CRLF" "--resolve-local --insecure\r${nl}" "--resolve-local --insecure"
flags "a repeat is dropped" "--insecure --insecure" "--insecure"
flags "an unknown flag" "--resolve-local; rm -rf /" REFUSED
flags "a stray word" "resolve-local" REFUSED
flags "resolve-to an IPv4 address" "--resolve-to=91.244.102.30 --insecure${nl}" "--resolve-to=91.244.102.30 --insecure"
flags "resolve-to repeated as is" "--resolve-to=10.0.0.1 --resolve-to=10.0.0.1" "--resolve-to=10.0.0.1"
flags "resolve-to with no address" "--resolve-to=" REFUSED
flags "resolve-to a host name" "--resolve-to=edge.example.com" REFUSED
flags "resolve-to with a shell tail" "--resolve-to=10.0.0.1;id" REFUSED
flags "resolve-to as two words" "--resolve-to 10.0.0.1" REFUSED
flags "two addresses" "--resolve-to=10.0.0.1 --resolve-to=10.0.0.2" REFUSED
flags "resolve-local and resolve-to" "--resolve-local --resolve-to=10.0.0.1" REFUSED
got=$(w smoke_args_of "$sandbox/missing") && [ -z "$got" ] && ok "smoke flags: no file, none" || bad "smoke flags: no file"

# --- smoke.sh against a VM behind an edge proxy (bug-006) -------------------------
# A stub curl models the production layout: on the VM the site's name resolves
# to the VM itself, whose nginx serves a self-signed certificate and answers
# http with 200; the edge (192.0.2.10 here) ends TLS, redirects http and passes
# everything through. A request reaches the edge only when curl was given
# --resolve <host>:<port>:192.0.2.10.
smoke=$root/deploy/vm/smoke.sh
mkdir -p "$sandbox/stub"
cat > "$sandbox/stub/curl" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$STUB_LOG"
fmt='' url='' edge=0
while [ $# -gt 0 ]; do
    case $1 in
        -w) fmt=$2; shift ;;
        -o|--max-time) shift ;;
        --resolve) case $2 in portal.example.com:*:192.0.2.10) edge=1 ;; esac; shift ;;
        http*) url=$1 ;;
    esac
    shift
done
index='<div id="root"></div><script type="module" src="/mustask/assets/index-1.js"></script>'
say() { if [ -n "$fmt" ]; then printf '%s' "$1"; else printf '%s' "$2"; fi; }
case $url in
    http://127.0.0.1:5000/health) printf 'Healthy'; exit 0 ;;
esac
if [ "$edge" = 0 ]; then
    case $url in
        http://*) say '200 ' "$index"; exit 0 ;;
        *) echo 'curl: (60) SSL certificate problem: self-signed certificate' >&2
           [ -n "$fmt" ] && printf '000 '; exit 60 ;;
    esac
fi
case $url in
    http://portal.example.com/mustask/) say '301 https://portal.example.com/mustask/' '' ;;
    https://portal.example.com/mustask/assets/index-1.js) say '200 text/javascript' 'x' ;;
    https://portal.example.com/mustask/api/dictionaries/languages) say '401' '' ;;
    https://portal.example.com/mustask/api/session/login) say '302 https://kc.example.com/realms/R/protocol/openid-connect/auth?x' '' ;;
    https://portal.example.com/mustask/*) say '200 ' "$index" ;;
    *) say '404 ' 'not found' ;;
esac
STUB
chmod +x "$sandbox/stub/curl"
printf 'Session__PanelUrl=https://portal.example.com/mustask\nKeycloak__Authority="https://kc.example.com/realms/R"\n' > "$sandbox/mustask.env"
run_smoke() { # <flags...>; prints the exit code, the curl calls go to $sandbox/curls
    : > "$sandbox/curls"
    set +e
    STUB_LOG=$sandbox/curls MUSTASK_ENV_FILE=$sandbox/mustask.env PATH="$sandbox/stub:$PATH" \
        bash "$smoke" "$@" > "$sandbox/smoke.out" 2>&1
    printf '%s' "$?"
    set -e
}
smoke_case() { # <name> <expected exit code> <flags...>
    name=$1 want=$2; shift 2
    got=$(run_smoke "$@")
    [ "$got" = "$want" ] && ok "smoke.sh: $name" || { bad "smoke.sh: $name (exit $got, expected $want)"; sed 's/^/        /' "$sandbox/smoke.out"; }
}
smoke_case "the VM's own hop fails, as on production" 1
grep -q -- "--resolve" "$sandbox/curls" && bad "smoke.sh: the default run passes --resolve" || ok "smoke.sh: no flags, no --resolve (default unchanged)"
smoke_case "--resolve-local still checks the VM's own hop" 1 --resolve-local
smoke_case "--resolve-to the edge passes every check" 0 --resolve-to=192.0.2.10
grep -q 'automated checks: passed' "$sandbox/smoke.out" && ! grep -q FAIL "$sandbox/smoke.out" \
    && ok "smoke.sh: --resolve-to reports no FAIL" || bad "smoke.sh: --resolve-to output has a FAIL"
# Every request but the loopback /health went to the edge on its own port.
if grep -v '127.0.0.1:5000' "$sandbox/curls" | grep -qv -- '--resolve portal.example.com:443:192.0.2.10 --resolve portal.example.com:80:192.0.2.10'; then
    bad "smoke.sh: a request went out without --resolve to the edge"
else ok "smoke.sh: every request through the proxy goes to the edge"; fi
smoke_case "--insecure with --resolve-to passes too" 0 --insecure --resolve-to=192.0.2.10
smoke_case "--resolve-to another address fails" 1 --resolve-to=192.0.2.99
for f in --resolve-to= --resolve-to=edge.example.com --resolve-to=1.2.3 "--resolve-to=1.2.3.4;id"; do
    smoke_case "refuses $f before any request" 2 "$f"
    [ -s "$sandbox/curls" ] && bad "smoke.sh: $f made a request" || true
done
smoke_case "refuses --resolve-local with --resolve-to" 2 --resolve-local --resolve-to=192.0.2.10
smoke_case "refuses two --resolve-to" 2 --resolve-to=192.0.2.10 --resolve-to=192.0.2.10

# --- a deploy, with a stub install.sh ---------------------------------------------
# Builds mustask-<tag>.tar.gz whose kit/install.sh records how it was called.
release() { # <tag> <VERSION tag>
    rm -rf "$sandbox/build" && mkdir -p "$sandbox/build/mustask-$1/kit"
    printf 'tag=%s\ncommit=0000000\n' "$2" > "$sandbox/build/mustask-$1/VERSION"
    # shellcheck disable=SC2016 # the stub expands its own arguments
    printf '#!/usr/bin/env bash\nprintf "%%s|%%s\\n" "$1" "$(basename "$2")" > "%s/called"\n' "$sandbox" \
        > "$sandbox/build/mustask-$1/kit/install.sh"
    tar -C "$sandbox/build" -czf "$sandbox/mustask-$1.tar.gz" "mustask-$1"
}
deploys() { # <tarball>; the wrapper's cmd_deploy with its stage under the sandbox
    rm -f "$sandbox/called"
    mkdir -p "$sandbox/stage"
    bash -c '. "$1"; stage_parent=$2; cmd_deploy' cd-cases "$wrapper" "$sandbox/stage" < "$1" >/dev/null 2>&1
}

release v0.3.0 v0.3.0
if deploys "$sandbox/mustask-v0.3.0.tar.gz" && [ "$(cat "$sandbox/called" 2>/dev/null)" = "deploy|mustask-v0.3.0" ]; then
    ok "deploy: runs the release's kit/install.sh deploy <release>"
else
    bad "deploy: the release's install.sh was not run as 'deploy <release>'"
fi
[ -z "$(ls -A "$sandbox/stage")" ] && ok "deploy: the stage is removed" || bad "deploy: the stage is left behind"

release v0.3.1 v0.3.0
if deploys "$sandbox/mustask-v0.3.1.tar.gz" || [ -f "$sandbox/called" ]; then
    bad "deploy: a VERSION for another tag was deployed"
else
    ok "deploy: a VERSION for another tag is refused"
fi

printf 'not a tarball' > "$sandbox/junk.tar.gz"
if deploys "$sandbox/junk.tar.gz"; then bad "deploy: junk was accepted"; else ok "deploy: junk is refused"; fi

rm -rf "$sandbox/build" && mkdir -p "$sandbox/build/mustask-v1" "$sandbox/build/extra"
printf 'tag=v1\n' > "$sandbox/build/mustask-v1/VERSION"
tar -C "$sandbox/build" -czf "$sandbox/two.tar.gz" mustask-v1 extra
if deploys "$sandbox/two.tar.gz" || [ -f "$sandbox/called" ]; then bad "deploy: two top directories were accepted"; else ok "deploy: two top directories are refused"; fi

# The release is a symlink, or has no installer, or its installer fails.
rm -rf "$sandbox/build" && mkdir -p "$sandbox/build/real" && printf 'tag=v2\n' > "$sandbox/build/real/VERSION"
if ln -s real "$sandbox/build/mustask-v2" 2>/dev/null; then
    tar -C "$sandbox/build" -czf "$sandbox/link.tar.gz" mustask-v2
    if deploys "$sandbox/link.tar.gz" || [ -f "$sandbox/called" ]; then bad "deploy: a symlink as the release was accepted"; else ok "deploy: a symlink as the release is refused"; fi
fi
rm -rf "$sandbox/build" && mkdir -p "$sandbox/build/mustask-v3" && printf 'tag=v3\n' > "$sandbox/build/mustask-v3/VERSION"
tar -C "$sandbox/build" -czf "$sandbox/noinstall.tar.gz" mustask-v3
if deploys "$sandbox/noinstall.tar.gz"; then bad "deploy: a release without kit/install.sh was accepted"; else ok "deploy: a release without kit/install.sh is refused"; fi
rm -rf "$sandbox/build" && mkdir -p "$sandbox/build/mustask-v4/kit" && printf 'tag=v4\n' > "$sandbox/build/mustask-v4/VERSION"
printf '#!/usr/bin/env bash\nexit 7\n' > "$sandbox/build/mustask-v4/kit/install.sh"
tar -C "$sandbox/build" -czf "$sandbox/failing.tar.gz" mustask-v4
if deploys "$sandbox/failing.tar.gz"; then bad "deploy: a failing install.sh exited 0"; else ok "deploy: a failing install.sh fails the deploy"; fi
[ -z "$(ls -A "$sandbox/stage")" ] && ok "deploy: the stage is removed after a refusal too" || bad "deploy: the stage is left behind after a refusal"

# --- smoke runs the live release's smoke.sh with the recorded flags ------------------
mkdir -p "$sandbox/live/kit"
# shellcheck disable=SC2016 # the stub expands its own arguments
printf '#!/usr/bin/env bash\nprintf "%%s\\n" "$*" > "%s/smoked"\n' "$sandbox" > "$sandbox/live/kit/smoke.sh"
cmd_smoke_in() { # <live dir> <flags file>; the wrapper's cmd_smoke pointed into the sandbox
    bash -c '. "$1"; current=$2; smoke_args_file=$3; cmd_smoke' cd-cases "$wrapper" "$1" "$2" >/dev/null 2>&1
}
printf '%s\n' '--resolve-local --insecure' > "$sandbox/args"
rm -f "$sandbox/smoked"
if cmd_smoke_in "$sandbox/live" "$sandbox/args" && [ "$(cat "$sandbox/smoked" 2>/dev/null)" = "--resolve-local --insecure" ]; then
    ok "smoke: runs smoke.sh with the recorded flags"
else bad "smoke: the recorded flags did not reach smoke.sh"; fi
printf '%s\n' '--resolve-local; id' > "$sandbox/args"
rm -f "$sandbox/smoked"
if cmd_smoke_in "$sandbox/live" "$sandbox/args" || [ -f "$sandbox/smoked" ]; then bad "smoke: ran with a bad flags file"; else ok "smoke: a bad flags file is refused"; fi
if cmd_smoke_in "$sandbox/nolive" "$sandbox/args"; then bad "smoke: ran without a live release"; else ok "smoke: refused without a live release"; fi

# --- the command line ---------------------------------------------------------------
if [ "$(id -u)" != 0 ]; then
    if bash "$wrapper" deploy < /dev/null >/dev/null 2>&1; then bad "run as a user: accepted"; else ok "run as a user: refused"; fi
fi

# --- the job guard (the runner's job-started hook) ----------------------------------
# A pull_request run takes its workflows from the PR, so the workflow checks
# below cannot stop a fork's PR once it is approved to run; the guard on the VM
# does. It is sourced like the wrapper.
guard=$root/deploy/vm/runner-job-guard.sh
want=totKtoXam/mustask
wf=$want/.github/workflows/deploy.yml
job() { # <name> <0 allowed | 1 refused> <want> <repository> <event> <workflow ref>
    if bash -c '. "$1"; shift; job_allowed "$@"' cd-cases "$guard" "$3" "$4" "$5" "$6" >/dev/null 2>&1; then got=0; else got=1; fi
    [ "$got" = "$2" ] && ok "guard: $1" || bad "guard: $1 (expected $([ "$2" = 0 ] && echo allowed || echo refused))"
}
job "a tag push of deploy.yml" 0 $want $want push "$wf@refs/tags/v0.2.0"
job "a dispatch from main" 0 $want $want workflow_dispatch "$wf@refs/heads/main"
job "a dispatch from a tag" 0 $want $want workflow_dispatch "$wf@refs/tags/v0.1.0"
job "the repository in another case" 0 totktoxam/MUSTASK $want push "$wf@refs/tags/v1"
job "a pull_request" 1 $want $want pull_request "$wf@refs/pull/7/merge"
job "a pull_request_target" 1 $want $want pull_request_target "$wf@refs/heads/main"
job "a fork PR's own workflow" 1 $want $want pull_request "$want/.github/workflows/evil.yml@refs/pull/7/merge"
job "another workflow on push" 1 $want $want push "$want/.github/workflows/ci.yml@refs/heads/main"
job "deploy.yml of another repository" 1 $want $want push "evil/mustask/.github/workflows/deploy.yml@refs/tags/v1"
job "another repository" 1 $want evil/mustask push "evil/mustask/.github/workflows/deploy.yml@refs/tags/v1"
job "a dispatch from a branch" 1 $want $want workflow_dispatch "$wf@refs/heads/feature"
job "a tag not v*" 1 $want $want push "$wf@refs/tags/release-1"
job "a v tag with odd characters" 1 $want $want push "$wf@refs/tags/v1;id"
job "a schedule" 1 $want $want schedule "$wf@refs/heads/main"
job "no variables (fails closed)" 1 $want "" "" ""
job "no recorded repository" 1 "" $want push "$wf@refs/tags/v1"
job "a ref with no @" 1 $want $want push "$wf"
# Run as the hook, it reads the recorded repository and the runner's variables.
hook() { GITHUB_REPOSITORY=$1 GITHUB_EVENT_NAME=$2 GITHUB_WORKFLOW_REF=$3 bash -c \
    '. "$1"; repository_file=$2; main' cd-cases "$guard" "$sandbox/cd-repository" >/dev/null 2>&1; }
printf '%s\n' $want > "$sandbox/cd-repository"
hook $want push "$wf@refs/tags/v1" && ok "guard hook: exits 0 for a tag push" || bad "guard hook: refused a tag push"
if hook $want pull_request "$wf@refs/pull/1/merge"; then bad "guard hook: allowed a pull_request"; else ok "guard hook: exits non-zero for a pull_request"; fi
rm -f "$sandbox/cd-repository"
if hook $want push "$wf@refs/tags/v1"; then bad "guard hook: allowed with no recorded repository"; else ok "guard hook: refuses with no recorded repository"; fi

# --- the workflows ---------------------------------------------------------------
# The lines of a file without comments and blank lines.
code() { sed -e 's/[[:space:]]#.*$//' -e '/^[[:space:]]*#/d' -e '/^[[:space:]]*$/d' "$1"; }
# The keys one level under a top-level key (two spaces in).
children() { code "$1" | awk -v k="$2:" '$0 == k {on=1; next} /^[^ ]/ {on=0} on && /^  [A-Za-z_-]+:/ {sub(/^  /, ""); sub(/:.*/, ""); print}'; }

triggers=$(children "$deploy" on | tr '\n' ' ')
[ "$triggers" = "push workflow_dispatch " ] && ok "deploy.yml: triggers are push and workflow_dispatch only" \
    || bad "deploy.yml: triggers are '$triggers'"
push=$(code "$deploy" | awk '/^on:/ {on=1; next} /^[^ ]/ {on=0} on && /^  push:/ {p=1; next} on && /^  [^ ]/ {p=0} p && /^    [A-Za-z_-]+:/ {sub(/^    /, ""); sub(/:.*/, ""); print}' | tr '\n' ' ')
[ "$push" = "tags " ] && ok "deploy.yml: push is tags only" || bad "deploy.yml: push has '$push'"
code "$deploy" | grep -q "^    tags: \['v\*'\]$" && ok "deploy.yml: the tags are v*" || bad "deploy.yml: the push tags are not ['v*']"
code "$deploy" | grep -Eq 'pull_request|workflow_run|issue_comment' \
    && bad "deploy.yml: names a PR-driven event" || ok "deploy.yml: no PR-driven event"

# Every job that targets the VM: only deploy.yml's deploy job, behind production.
selfhosted=''
for f in "$root"/.github/workflows/*.yml "$root"/.github/workflows/*.yaml; do
    [ -f "$f" ] || continue
    code "$f" | grep -q 'self-hosted' && selfhosted="$selfhosted $(basename "$f")"
    if [ "$f" != "$deploy" ]; then
        other=$(code "$f" | grep -E '^[[:space:]]*runs-on:' | grep -v 'runs-on: ubuntu-latest$' || true)
        [ -z "$other" ] && ok "$(basename "$f"): every job runs on ubuntu-latest" \
            || bad "$(basename "$f"): a job not on ubuntu-latest: $other"
    fi
done
[ "$selfhosted" = " deploy.yml" ] && ok "only deploy.yml names self-hosted" || bad "self-hosted named in:$selfhosted"
job_of_selfhosted=$(code "$deploy" | awk '/^jobs:/ {on=1; next} on && /^  [A-Za-z_-]+:/ {job=$1} on && /self-hosted/ {print job}')
[ "$job_of_selfhosted" = "deploy:" ] && ok "deploy.yml: only the deploy job targets self-hosted" \
    || bad "deploy.yml: self-hosted in '$job_of_selfhosted'"
code "$deploy" | grep -q '^    runs-on: \[self-hosted, linux, mustask-prod\]$' \
    && ok "deploy.yml: runs-on is [self-hosted, linux, mustask-prod]" || bad "deploy.yml: runs-on of the deploy job"
deploy_job=$(code "$deploy" | awk '/^  deploy:/ {on=1; next} /^  [A-Za-z_-]+:/ {on=0} on')
printf '%s\n' "$deploy_job" | grep -q '^      name: production$' \
    && ok "deploy.yml: the deploy job uses the production environment" || bad "deploy.yml: the deploy job has no production environment"
printf '%s\n' "$deploy_job" | grep -q 'actions/checkout' \
    && bad "deploy.yml: the deploy job checks the repository out on the VM" || ok "deploy.yml: no checkout on the VM"
code "$deploy" | grep -q '^  cancel-in-progress: false$' && code "$deploy" | grep -q '^  group: ' \
    && ok "deploy.yml: one concurrency group, never cancelled" || bad "deploy.yml: concurrency"
unpinned=$(code "$deploy" | grep -E 'uses: ' | grep -Ev 'uses: [^@]+@[0-9a-f]{40}$' || true)
[ -z "$unpinned" ] && ok "deploy.yml: every action is pinned by commit" || bad "deploy.yml: unpinned: $unpinned"

# The rest of the guard: nothing but the deploy job may reach the VM, whatever
# the label is spelled like, and the deploy job's root commands are exactly the
# sudoers rule's two.
runs_on=$(code "$deploy" | grep -E '^[[:space:]]*runs-on:' | sed 's/^[[:space:]]*//' | tr '\n' '|')
[ "$runs_on" = "runs-on: ubuntu-latest|runs-on: ubuntu-latest|runs-on: [self-hosted, linux, mustask-prod]|" ] \
    && ok "deploy.yml: ubuntu-latest for build and notes, the VM for deploy, nothing else" || bad "deploy.yml: runs-on lines: $runs_on"
code "$deploy" | grep -qFx '    needs: [build, notes]' && ok "deploy.yml: the deploy job needs build and notes" || bad "deploy.yml: the deploy job does not need build and notes"
code "$deploy" | grep -q "^    if: github.ref == 'refs/heads/main' || startsWith(github.ref, 'refs/tags/v')$" \
    && ok "deploy.yml: a run starts only from main or a v* tag" || bad "deploy.yml: the main / v* ref guard is gone"
code "$deploy" | grep -q '^  group: deploy-production$' \
    && ok "deploy.yml: the concurrency group is deploy-production" || bad "deploy.yml: the concurrency group changed"
perms=$(code "$deploy" | awk '/^permissions:/ {on=1; next} /^[^ ]/ {on=0} on' | tr -d ' \n')
[ "$perms" = "contents:read" ] && ok "deploy.yml: workflow permissions are contents: read" || bad "deploy.yml: workflow permissions are '$perms'"
printf '%s\n' "$deploy_job" | grep -q 'sha256sum -c' \
    && ok "deploy.yml: the VM job checks the tarball's sha256" || bad "deploy.yml: no sha256 check on the VM"
sudo_lines=$(code "$deploy" | grep -E '(^|[[:space:];&|])sudo[[:space:]]' | sed 's/^[[:space:]]*//; s/^run: //' | tr '\n' '|')
# shellcheck disable=SC2016 # literal workflow text
want='sudo -n /usr/local/sbin/mustask-cd deploy < "$RUNNER_TEMP/release/mustask-$TAG.tar.gz"|sudo -n /usr/local/sbin/mustask-cd smoke|'
[ "$sudo_lines" = "$want" ] && ok "deploy.yml: sudo runs only mustask-cd deploy and smoke" || bad "deploy.yml: sudo lines: $sudo_lines"
# The dispatch input reaches the shell through an env var, never inline.
# shellcheck disable=SC2016 # literal workflow text
inline=$(code "$deploy" | grep -F '${{ inputs.' | grep -v '^ *INPUT_TAG: ' | grep -v '^run-name: ' || true)
[ -z "$inline" ] && ok "deploy.yml: inputs.tag only goes through env" || bad "deploy.yml: inputs used inline: $inline"

printf '\n'
if [ "$failures" = 0 ]; then
    printf 'cd-runner cases: all passed\n'
else
    printf 'cd-runner cases: %s failed\n' "$failures"
    exit 1
fi
