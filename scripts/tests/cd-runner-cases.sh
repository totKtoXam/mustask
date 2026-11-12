#!/bin/sh
# Regression cases for CD on the production VM (tech-066).
#
# Two halves. The wrapper deploy/vm/mustask-cd.sh, the one thing the runner may
# run as root: what tarball it accepts, which smoke flags it passes, and that
# a deploy runs the release's own install.sh on the unpacked release. And the
# workflows: the repository is public, so a job on the self-hosted runner must
# never be reachable from a pull request — deploy.yml triggers only on v* tags
# and workflow_dispatch, only its deploy job targets the VM, and every other
# workflow stays on GitHub-hosted runners.
#
# The wrapper is sourced (its main only runs when it is executed), so the cases
# need no root and no VM. The workflow checks read the YAML as text: the files
# are plain enough, and the cases then need no python.
#
# Usage: sh scripts/tests/cd-runner-cases.sh
# (verify.sh runs it when the wrapper, runner-setup.sh, a workflow or this
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
got=$(w smoke_args_of "$sandbox/missing") && [ -z "$got" ] && ok "smoke flags: no file, none" || bad "smoke flags: no file"

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

# --- the command line ---------------------------------------------------------------
if [ "$(id -u)" != 0 ]; then
    if bash "$wrapper" deploy < /dev/null >/dev/null 2>&1; then bad "run as a user: accepted"; else ok "run as a user: refused"; fi
fi

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

printf '\n'
if [ "$failures" = 0 ]; then
    printf 'cd-runner cases: all passed\n'
else
    printf 'cd-runner cases: %s failed\n' "$failures"
    exit 1
fi
