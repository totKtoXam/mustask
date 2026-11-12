#!/usr/bin/env bash
# The deploy runner's job-started hook (docs/deploy/vm.md, section 11.4).
# runner-setup.sh installs it as /usr/local/libexec/mustask-runner/job-started.sh,
# owned by root, and points ACTIONS_RUNNER_HOOK_JOB_STARTED at it from a
# root-owned systemd drop-in of the runner service. The runner runs it, as the
# runner user, before any step of every job; a non-zero exit fails the job
# before it starts.
#
# Why: a pull_request run takes its workflow files from the PR itself. Once a
# fork's PR is approved to run, a workflow it adds can target this runner's
# labels, and the runner user may hand mustask-cd a tarball whose install.sh
# runs as root. GitHub has no runner groups for a repository owned by a user,
# so the VM itself refuses every job but deploy.yml of this repository,
# started by a tag push or by hand from main or a v* tag.
#
# The repository is the one runner-setup.sh registered for, recorded in
# /etc/mustask/cd-repository. A variable the runner did not set refuses the
# job: the guard fails closed.

set -u

repository_file=/etc/mustask/cd-repository

# Whether a job may run: <expected owner/repo> <GITHUB_REPOSITORY>
# <GITHUB_EVENT_NAME> <GITHUB_WORKFLOW_REF>. Says why not on stderr.
job_allowed() {
    local want=$1 repo=$2 event=$3 ref=$4
    [ -n "$want" ] || { echo "no repository recorded in $repository_file" >&2; return 1; }
    # GitHub names are case-insensitive; the runner reports the canonical case.
    want=$(printf '%s' "$want" | tr '[:upper:]' '[:lower:]')
    if [ "$(printf '%s' "$repo" | tr '[:upper:]' '[:lower:]')" != "$want" ]; then
        echo "repository '$repo' is not $want" >&2
        return 1
    fi
    case $event in
        push|workflow_dispatch) ;;
        *) echo "event '$event' may not run on this runner (push or workflow_dispatch only)" >&2; return 1 ;;
    esac
    local path=${ref%%@*} at=${ref#*@}
    [ "$path" != "$ref" ] || { echo "workflow ref '$ref' has no @ref" >&2; return 1; }
    if [ "$(printf '%s' "$path" | tr '[:upper:]' '[:lower:]')" != "$want/.github/workflows/deploy.yml" ]; then
        echo "workflow '$path' is not deploy.yml of $want" >&2
        return 1
    fi
    case $at in
        refs/heads/main) return 0 ;;
        refs/tags/v*)
            printf '%s' "${at#refs/tags/}" | grep -Eq '^v[A-Za-z0-9._-]+$' && return 0 ;;
    esac
    echo "workflow ref '$at' is neither refs/heads/main nor a refs/tags/v* tag" >&2
    return 1
}

main() {
    local want=''
    [ -r "$repository_file" ] && want=$(head -n 1 "$repository_file" | tr -d '\r')
    if job_allowed "$want" "${GITHUB_REPOSITORY:-}" "${GITHUB_EVENT_NAME:-}" "${GITHUB_WORKFLOW_REF:-}"; then
        echo "mustask runner guard: $GITHUB_WORKFLOW_REF ($GITHUB_EVENT_NAME) allowed"
        return 0
    fi
    echo "mustask runner guard: this job may not run on the production runner (docs/deploy/vm.md, section 11.4)" >&2
    return 1
}

# Sourced by scripts/tests/cd-runner-cases.sh; run, it is the hook.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main
fi
