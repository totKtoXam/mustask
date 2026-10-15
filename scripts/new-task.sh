#!/bin/sh
# Starts a task FROM A QUEUE LINE: the branch, the worktree, and the
# docs/features/<id>/ folder.
#
# Usage:
#   scripts/new-task.sh <prefix> <slug> ["<task summary>"] [size] [epic]
#
#   scripts/new-task.sh feat order-submission-form
#   scripts/new-task.sh hot order-pdf-500 "Order PDF returns 500"
#
# The queue assigns the task's number: the id is taken from the line
# `- [ ] <prefix>-NNN-<slug>` in docs/backlog.md on the MAIN branch (with a
# configured remote — origin/main after fetch) — character for character,
# together with size and epic. No line — the script refuses: the line must
# first be added to the queue by a commit to main (by the orchestrator or the
# human). The only exception is the `hot` prefix: a hotfix goes outside the
# queue, is always size S, and gets the next free number from three sources
# (docs/features/numbering.md).
#
# The summary/size/epic for queued tasks can be omitted — they are taken from
# the line. An explicitly passed size (confirmed by the human during route
# confirmation) overrides the line's size: a mismatch is printed by the
# script and written to the Log.
#
# The application to do the work is the branch created successfully (git
# won't create it twice). The script commits nothing and does NOT edit
# docs/backlog.md: the orchestrator removes the taken task's line from the
# queue with a separate commit to main that changes only docs/backlog.md.

set -eu

die() {
    printf '\n%s\n\n' "$1" >&2
    exit 1
}

usage() {
    sed -n '2,28p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

case "${1:-}" in
    ''|-h|--help) usage 0 ;;
esac

prefix=$1
slug=${2:-}
summary=${3:-}
size=${4:-}
epic=${5:-}

# --- Argument checks -----------------------------------------------------------
case "$prefix" in
    feat|bug|hot|tech|mod|doc) ;;
    *) die "Unknown prefix '$prefix'. Allowed: feat, bug, hot, tech, mod, doc." ;;
esac

[ -n "$slug" ] || die "No slug given. Example: scripts/new-task.sh bug amount-rounding"

printf '%s' "$slug" | grep -Eq '^[a-z0-9]+(-[a-z0-9]+)*$' \
    || die "Slug '$slug' is not kebab-case: lowercase Latin letters, digits and hyphens only."

if [ -n "$size" ]; then
    case "$size" in
        F|S|M|L) ;;
        *) die "Size '$size' is not one of F|S|M|L." ;;
    esac
fi

# hotfix is always S (routing.md, the type table): default size is S, any
# other size is a call error, not a reason for a different route.
if [ "$prefix" = 'hot' ]; then
    [ -n "$size" ] || size=S
    [ "$size" = 'S' ] || die "hotfix is always S (routing.md) — size '$size' is not allowed."
fi

# bugfix is never F: a bug has a test that failed before the fix, i.e. a
# behavior change by definition (stages/f-route.md).
if [ "${size:-}" = 'F' ] && [ "$prefix" = 'bug' ]; then
    die "Size F is not allowed for prefix 'bug' — minimum is S (f-route.md)."
fi

# --- Root of the main repository (the script can run from a worktree) ----------
common_dir=$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null) \
    || die "Not a git repository."
repo=$(dirname "$common_dir")

# --- Reference point: origin/main with a configured remote, else main ----------
# fetch --prune is required before checking branch occupancy, otherwise
# remote-tracking branches show the state as of the last fetch. A network
# failure doesn't block the work, but it is called out.
if [ -n "$(git -C "$repo" remote 2>/dev/null)" ]; then
    git -C "$repo" fetch --prune --quiet 2>/dev/null \
        || printf 'Warning: git fetch failed — branch occupancy and the queue are checked against local state.\n' >&2
fi
base_ref=main
if git -C "$repo" rev-parse --verify --quiet origin/main >/dev/null 2>&1; then
    base_ref=origin/main
fi

# The queue is read from base_ref, not from the current worktree's file: the
# queue lives in main, and the copy in the task's branch can be arbitrarily
# stale.
queue=$(git -C "$repo" show "$base_ref:docs/backlog.md" 2>/dev/null) \
    || die "Could not read $base_ref:docs/backlog.md.
Check that this is a clone of the MUSTASK repository and that the main (or
origin/main) branch exists; on a fresh clone run git fetch origin first."

# --- id: from the queue line, for hot — the next free one ----------------------
all_refs=$(git -C "$repo" for-each-ref --format='%(refname:short)' refs/heads refs/remotes 2>/dev/null)

# printf '%03d' on an argument with a leading zero is an octal trap (008/009
# break arithmetic), and the 10#008 notation is a bashism that fails under
# dash. So leading zeros are stripped as text before the arithmetic.
next_after() {
    _last=$(printf '%s' "$1" | sed 's/^0*//')
    printf '%03d' $(( ${_last:-0} + 1 ))
}

size_note=''
size_warn=''
if [ "$prefix" = 'hot' ]; then
    # Emergency path outside the queue (a closed list of exceptions — hot
    # only). Next free number from three sources, scanning all branches:
    # branches, docs/features/<id>/, the queue files. Limiting to
    # refs/heads refs/remotes is deliberate — otherwise refs/stash gets in.
    [ -n "$summary" ] || die "For hot the task summary is required (third argument): there is no queue line."
    numbers=$(
        {
            echo "$all_refs" | grep -oE "${prefix}/[0-9]{3}" | tr '/' '-' || true
            for ref in $all_refs; do
                git -C "$repo" ls-tree --name-only "$ref:docs/features" 2>/dev/null \
                    | grep -oE "^${prefix}-[0-9]{3}" || true
                git -C "$repo" show "$ref:docs/backlog.md" 2>/dev/null \
                    | grep -oE "${prefix}-[0-9]{3}" || true
                # withdrawn numbers are taken and never reused (the section
                # lives in its own file)
                git -C "$repo" show "$ref:docs/backlog/withdrawn.md" 2>/dev/null \
                    grep -oE "${prefix}-[0-9]{3}" || true
            done
            ls "$repo/docs/features" 2>/dev/null | grep -oE "^${prefix}-[0-9]{3}" || true
        } | grep -oE '[0-9]{3}$' | sort -n
    )
    next=$(next_after "$(printf '%s\n' "$numbers" | tail -n 1)")
else
    printf '%s\n' "$queue" | grep -qE '^- \[ \] ' \
        || die "Queue $base_ref:docs/backlog.md is empty — nothing to take.
The orchestrator or the human must first add a task line to the queue with a
commit to main."

    line=$(printf '%s\n' "$queue" \
        | grep -E "^- \[ \] ${prefix}-[0-9]{3}-${slug}( |$)" | head -1 || true)
    [ -n "$line" ] || die "Queue $base_ref:docs/backlog.md has no line '${prefix}-NNN-${slug}'.
The id is taken only from the queue: add the task line (with a number) with a
commit to main and run the script again. Changing the slug to get past the
refusal is forbidden — it's the same work under a different name
(docs/features/numbering.md)."

    next=$(printf '%s' "$line" | grep -oE "${prefix}-[0-9]{3}-" | head -1 | grep -oE '[0-9]{3}')

    queue_size=$(printf '%s' "$line" | grep -oE 'size:[FSML]' | head -1 | cut -d: -f2 || true)
    if [ -n "$size" ] && [ -n "$queue_size" ] && [ "$size" != "$queue_size" ]; then
        size_note="queue has size:$queue_size, human confirmed $size"
        printf 'Warning: %s — writing the confirmed one.\n' "$size_note"
    fi
    [ -n "$size" ] || size=$queue_size
    if [ -z "${size:-}" ]; then
        # A silent M would change the gate set of feat/mod/doc without a word.
        size=M
        size_warn="the queue line has no valid size:, defaulted to M — check this during route confirmation"
        printf 'Warning: %s.\n' "$size_warn"
    fi
    if [ -z "$epic" ]; then
        epic=$(printf '%s' "$line" | grep -oE 'epic:[^ ·]*' | head -1 | cut -d: -f2 || true)
    fi
    # deps: does not come as an argument (unlike size/epic) — a task is
    # started from the queue line, and its current dependency list lives
    # there too; the second source for a task that has already started is
    # described in docs/features/README.md, "status.md format".
    deps=$(printf '%s' "$line" | grep -oE 'deps:[^ ·]*' | head -1 | cut -d: -f2 || true)
    if [ -z "$summary" ]; then
        summary=$(printf '%s' "$line" | sed 's/^- \[ \] [a-z0-9-]* — //; s/ ·.*$//' || true)
    fi
fi

[ -n "${epic:-}" ] || epic="-"
[ -n "${deps:-}" ] || deps="-"

id="${prefix}-${next}-${slug}"
branch="${prefix}/${next}-${slug}"
# The worktree lives inside the repository, under .claude/worktrees/ (a
# directory in .gitignore): Claude's desktop app groups sessions from such
# worktrees under the repository's own project, and for Codex the location is
# neutral — its sessions are keyed simply by cwd.
worktree="$repo/.claude/worktrees/$id"

# --- Lock: all refusals BEFORE the first side effect ----------------------------
# 1. The task branch already exists (locally or on any remote) — the task has
#    already been taken. The remote name isn't hardcoded: (.+/)? covers
#    origin and any other remote.
if printf '%s\n' "$all_refs" | grep -qE "^(.+/)?${prefix}/${next}-${slug}$"; then
    taken_wt=$(git -C "$repo" worktree list 2>/dev/null | grep -F "[$branch]" | awk '{print $1}' || true)
    die "Task '$id' has already been taken: branch $branch exists${taken_wt:+ (worktree: $taken_wt)}.
Take the next queue line. If line '$id' is still sitting in the main queue,
ask the orchestrator to remove it with a separate commit to main."
fi

# 2. The same number is taken by a branch with a DIFFERENT slug — a
#    desync: the number went to two tasks (a migration glitch or a manual
#    queue edit). This is not "the task was already taken", the message
#    must distinguish the cases.
clash=$(printf '%s\n' "$all_refs" | grep -E "^(.+/)?${prefix}/${next}-" | head -1 || true)
if [ -n "$clash" ]; then
    die "Number ${prefix}-${next} is already taken by another task: branch $clash.
This is a desync between the queue and the branches (one number for two
tasks), not a taken task. Don't create anything: fix the queue per the
double-numbering procedure (docs/features/numbering.md), then come back."
fi

# 3. A docs/features/ folder with this number exists in some ref: same id —
#    the task has already been done (the local branch was deleted after
#    merge, but a trace remains); a different slug — a desync, as in point 2.
#    A completed or cancelled task cannot be started again and overwrite its
#    status.md.
for ref in $all_refs; do
    hit=$(git -C "$repo" ls-tree --name-only "$ref:docs/features" 2>/dev/null \
        | grep -E "^${prefix}-${next}-" | head -1 || true)
    if [ -n "$hit" ]; then
        if [ "$hit" = "$id" ]; then
            die "Task '$id' has already been done: docs/features/$id/ exists in '$ref'.
A completed or cancelled task is not started again, the number is not reused
(docs/features/numbering.md). If a new task is needed, add a new queue line."
        fi
        die "Number ${prefix}-${next} is already taken by task '$hit': docs/features/$hit/ exists in '$ref'.
This is a desync between the queue and the feature folders (one number for
two tasks). Don't create anything: fix the queue per the double-numbering
procedure (docs/features/numbering.md), then come back."
    fi
done
hit=$(ls "$repo/docs/features" 2>/dev/null | grep -E "^${prefix}-${next}-" | head -1 || true)
if [ -n "$hit" ]; then
    if [ "$hit" = "$id" ]; then
        die "Task '$id' has already been done: docs/features/$id/ exists in main's working copy."
    fi
    die "Number ${prefix}-${next} is already taken by task '$hit' (docs/features in main's working copy) — a desync, see docs/features/numbering.md."
fi

# 4. The worktree directory is taken.
[ -e "$worktree" ] && die "Directory $worktree already exists.
If this is an abandoned worktree, deal with it manually (git worktree list) — the script won't touch it."

# --- Type, starting stage and gates ---------------------------------------------
case "$prefix" in
    feat) type=feature; stage=01-business-analysis ;;
    mod)  type=module;  stage=01-business-analysis ;;
    doc)  type=docs;    stage=01-business-analysis ;;
    bug)  type=bugfix;  stage=04-backend ;;
    hot)  type=hotfix;  stage=04-backend ;;
    tech) type=tech;    stage=04-backend ;;
esac

# F and S routes: no spec or design is written, so the first two gates have
# nothing to gate — they don't appear in status.md at all. The agent
# declares applicability (the gates field), only the human sets a gate's
# value.
if [ "$size" = 'F' ] || [ "$size" = 'S' ] || [ "$prefix" = 'bug' ] || [ "$prefix" = 'hot' ] || [ "$prefix" = 'tech' ]; then
    gates='gate-3'
    gate_lines='gate-3: pending'
    stage=04-backend
else
    gates='gate-1, gate-2, gate-3'
    gate_lines='gate-1: pending
gate-2: pending
gate-3: pending'
fi

# --- Worktree from a fresh main --------------------------------------------------
# Creating the branch IS the application to do the work: git creates the ref
# under a lock, a second concurrent call gets "branch already exists" and
# stops.
printf 'Task:     %s\nBranch:   %s\nWorktree: %s\n\n' "$id" "$branch" "$worktree"

mkdir -p "$(dirname "$worktree")"
# --no-track: otherwise the branch gets origin/main as upstream and a bare
# git push fails; the MR flow sets the upstream itself via
# git push -u origin <branch>.
git -C "$repo" worktree add --no-track "$worktree" -b "$branch" "$base_ref"

feature_dir="$worktree/docs/features/$id"
mkdir -p "$feature_dir"

today=$(date +%Y-%m-%d)

cat > "$feature_dir/status.md" <<STATUS
# $id

- type: $type
- size: $size
- branch: $branch
- stage: $stage
- epic: $epic
- deps: $deps
- gates: $gates

## Gates

$gate_lines

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- $today [new-task] task started, worktree $worktree${size_note:+
- $today [new-task] size mismatch: $size_note}${size_warn:+
- $today [new-task] $size_warn}

## Notes

${summary:+Summary (from the queue line or an argument): $summary}
STATUS

printf '\nDone. Next:\n'
if [ "$prefix" = 'hot' ]; then
    printf '  1. The orchestrator records the confirmed route and size in status.md.\n'
else
    printf '  1. The orchestrator records the confirmed route and size in status.md,\n'
    printf '     then takes the queue line in its separate main-only commit.\n'
fi
printf '  2. Bind the actual implementation/orchestrator session to %s.\n' "$worktree"
printf '     This script cannot rebind its parent session.\n'
printf '  3. In that bound session, confirm its host/project root plus\n'
printf '     git rev-parse --show-toplevel and git branch --show-current.\n'
printf '     They must identify this worktree and %s.\n' "$branch"
printf '  4. A shell cd or command-specific workdir is insufficient. If native\n'
printf '     relocation is unavailable, prepare an interactive human handoff from\n'
printf '     this worktree and stop the original session before continuing.\n'
printf '  5. Review and commit docs/features/%s/status.md (on the task branch)\n' "$id"
if [ "$prefix" = 'hot' ]; then
    printf '  6. This task is outside the queue: ask the orchestrator to reflect it in\n'
    printf '     the main queue after the fact.\n'
else
    printf '  6. The queue line was taken in step 1; do not continue task work until\n'
    printf '     that separate main-only commit is complete.\n'
fi
printf '  7. When done: git worktree remove %s\n\n' "$worktree"
