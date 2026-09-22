#!/bin/sh
# Finish a task: squash the branch into main, once and only if it is allowed.
#
# Usage:
#   scripts/finish-task.sh [<branch>] [--message-file <path>] [--dry-run]
#
#   scripts/finish-task.sh                       # the current worktree's branch
#   scripts/finish-task.sh tech/004-some-slug --dry-run
#   scripts/finish-task.sh --message-file /tmp/msg.txt
#
# This repository has no remote, so the documented ending —
# `git push` and an MR through `glab`
# (docs/ai-workflow/stages/07-mr-complete.md) — cannot run here. Every task
# ends as a local squash into main, and that same six-step sequence has been
# repeated by hand 42 times: write the message, commit the branch, squash,
# commit in the main worktree, check the branch actually landed, report.
#
# WHEN A MERGE IS ALLOWED. Only a human accepts work (AGENTS.md, "Only a
# human presses Merge"). This script does not decide that — it refuses unless
# the decision is already recorded in the task's own status.md:
#
#   * `gate-3: approved`, or
#   * a standing approval written into `## Log` (AGENTS.md, "Standing
#     approval").
#
# Plus two facts it checks itself: `scripts/verify.sh` is green, and the
# branch is not already in main. It is deliberately NOT in the
# `permissions.allow` list of .claude/settings.json — everything there is
# read-only or a test, and this writes to main.

set -eu

die() {
    printf '\n%s\n\n' "$1" >&2
    exit 1
}

usage() {
    sed -n '2,29p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

branch=''
message_file=''
dry_run=0
while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help)      usage 0 ;;
        --dry-run)      dry_run=1; shift ;;
        --message-file) message_file=${2:?--message-file needs a path}; shift 2 ;;
        -*)             die "Unknown option: $1" ;;
        *)              [ -z "$branch" ] || die "Two branches given: '$branch' and '$1'."
                        branch=$1; shift ;;
    esac
done

git rev-parse --show-toplevel >/dev/null 2>&1 || die "Not a git repository."
[ -n "$branch" ] || branch=$(git symbolic-ref --short HEAD 2>/dev/null || echo '')
[ -n "$branch" ] || die "Detached HEAD and no branch given."

printf '%s' "$branch" | grep -qE '^(feat|bug|hot|tech|mod|doc)/[0-9]{3}-[a-z0-9]+(-[a-z0-9]+)*$' \
    || die "Branch '$branch' is not a task branch <prefix>/<NNN>-<slug> (conventions/git.md)."
id=$(printf '%s' "$branch" | sed 's|/|-|')

# --- the worktrees: main is where the squash lands, the task's is where the
# --- checks run ------------------------------------------------------------------

worktree_of() {
    wanted=$1
    found=''
    current=''
    while IFS= read -r line; do
        case "$line" in
            'worktree '*) current=${line#worktree } ;;
            "branch refs/heads/$wanted") found=$current; break ;;
        esac
    done <<LIST
$(git worktree list --porcelain)
LIST
    printf '%s' "$found"
}

main_worktree=$(worktree_of main)
[ -n "$main_worktree" ] || die "No worktree has main checked out — nowhere to squash into."

dirty=$(git -C "$main_worktree" status --porcelain)
[ -z "$dirty" ] || die "The main worktree is not clean: $main_worktree

$dirty

A squash commit must contain only the task. Deal with these changes first."

# --- 1. has this task already landed? --------------------------------------------
#
# A squash merge brings the task's folder into main with `stage: complete`
# inside it, and that is the only durable trace: the branch keeps its own
# commits forever, and the content comparison below stops being an answer as
# soon as a later task edits the same lines. Once `merge-tree` was the
# only check here, and it said an already-merged branch was mergeable again
# the moment main moved on — it caught the case in hand purely because that
# one happened to conflict.

landed=$(git -C "$main_worktree" show "main:docs/features/$id/status.md" 2>/dev/null \
    | grep -m1 '^- stage:' | sed 's/^- stage: *//' || true)
if [ "$landed" = 'complete' ]; then
    die "Already merged: main has docs/features/$id/ with \`stage: complete\`.
The branch keeps its own commits after a squash, so having commits above main
is normal and means nothing. Nothing to do."
fi

# --- 2. would the merge apply at all? --------------------------------------------

main_tree() {
    git -C "$main_worktree" rev-parse 'main^{tree}'
}

merged_tree() {
    git -C "$main_worktree" merge-tree --write-tree main "$branch" 2>/dev/null | head -1
}

if ! git -C "$main_worktree" merge-tree --write-tree main "$branch" >/dev/null 2>&1; then
    die "The merge would conflict, or git is older than 2.38.
Rebase '$branch' onto main and run this again."
fi

if [ "$(merged_tree)" = "$(main_tree)" ]; then
    die "Nothing to merge: merging '$branch' would change nothing in main."
fi
# --- 3. the human's decision, as recorded ----------------------------------------

card=$(git show "$branch:docs/features/$id/status.md" 2>/dev/null) \
    || die "No docs/features/$id/status.md on branch '$branch'.
A task without a state file is not a task (docs/features/README.md)."

printf '%s\n' "$card" | grep -qx -- '- stage: complete' \
    || die "status.md does not say \`- stage: complete\`.
Completion goes INTO the squash, not after it: set the line on the branch and
commit it, then run this again (docs/ai-workflow/stages/07-mr-complete.md)."

approval=''
if printf '%s\n' "$card" | grep -qE '^gate-3: approved'; then
    approval='gate-3: approved'
elif printf '%s\n' "$card" | grep -qi 'standing approval'; then
    approval='standing approval, recorded in the Log'
fi
[ -n "$approval" ] || die "Nobody has accepted this work.
docs/features/$id/status.md has neither \`gate-3: approved\` nor a standing
approval in \`## Log\`. Only a human accepts work (AGENTS.md, \"Only a human
presses Merge\"); this script will not do it for them."

if printf '%s\n' "$card" | grep -qE '^gate-3: rejected'; then
    die "gate-3 is rejected — the work is not finished."
fi

# --- 4. whose commits are in there? ----------------------------------------------

mine=$(git -C "$main_worktree" config user.email)
foreign=$(git -C "$main_worktree" log "main..$branch" --format='%ae' | grep -v -F -x -e "$mine" | sort -u || true)

# --- 5. the checks ---------------------------------------------------------------

printf 'Task:      %s (%s)\n' "$id" "$branch"
printf 'Accepted:  %s\n' "$approval"
printf 'Commits:   %s above main\n' "$(git -C "$main_worktree" rev-list --count "main..$branch")"
if [ -n "$foreign" ]; then
    printf 'Authors:   also %s — rebase onto main before merging, or the squash hauls their work in\n' "$(printf '%s' "$foreign" | tr '\n' ' ')"
fi

# Only the checks below need a working tree; everything above reads the
# branch itself, so an already-merged or not-checked-out branch gets a
# straight answer instead of a chore.
task_worktree=$(worktree_of "$branch")
[ -n "$task_worktree" ] || die "Branch '$branch' is not checked out in any worktree.
verify.sh runs against a working tree:
    git worktree add <path> $branch"

dirty=$(git -C "$task_worktree" status --porcelain)
[ -z "$dirty" ] || die "The task worktree is not clean: $task_worktree

$dirty

Uncommitted work does not reach the squash. Commit it or put it aside first."
printf '\nRunning scripts/verify.sh in %s\n\n' "$task_worktree"
( cd "$task_worktree" && sh scripts/verify.sh ) || die "verify.sh is red — not merging."

# --- 6. the message --------------------------------------------------------------

if [ -n "$message_file" ]; then
    [ -f "$message_file" ] || die "No such message file: $message_file"
    message=$(cat "$message_file")
else
    # With ONE commit on the branch its message was written for exactly this
    # squash, so it is the message. Git's own SQUASH_MSG is not usable: its
    # subject is "Squashed commit of the following:", which commit-msg rejects.
    #
    # With more than one, the last commit is the wrong guess. The last thing a
    # task does is record the gate verdict, so the subject came out as "record
    # gate-3 approval" for a commit that introduced a rule. Guessing at the first commit instead is no better: a branch
    # that starts with a plan or a rename would be mislabelled the other way.
    #
    # So the script refuses and asks for the message. This is stricter than it
    # looks elsewhere on purpose: main's history cannot be repaired afterwards.
    # `.githooks/pre-commit` recognises a merge only while `.git/SQUASH_MSG`
    # exists, and that file is gone the moment the squash commit is made, so a
    # later `--amend` is refused — and git gives a pre-commit hook no signal
    # for `--amend` that would let the hook tell the two apart.
    commits=$(git -C "$main_worktree" rev-list --count "main..$branch")
    if [ "$commits" -gt 1 ]; then
        die "The branch has $commits commits, so there is no message to reuse.
Pass the squash message with --message-file <path>.

What is being collapsed:

$(git -C "$main_worktree" log --reverse --format='  %h %s' "main..$branch")

The subject describes the task, not the last thing done in it. It cannot be
fixed after the merge: pre-commit refuses an --amend on main."
    fi
    message=$(git -C "$main_worktree" log -1 --format=%B "$branch")
fi
subject=$(printf '%s\n' "$message" | head -1)
[ -n "$subject" ] || die "Empty commit message."
printf '\nSubject:   %s\n' "$subject"

if [ "$dry_run" = 1 ]; then
    printf '\nDry run — nothing merged. Everything above passed.\n'
    exit 0
fi

# --- 7. the squash ---------------------------------------------------------------

tmp=$(mktemp 2>/dev/null || printf '%s/finish-task-%s' "${TMPDIR:-/tmp}" "$$")
trap 'rm -f "$tmp"' EXIT INT TERM
printf '%s\n' "$message" > "$tmp"

git -C "$main_worktree" merge --squash "$branch" >/dev/null \
    || die "The squash did not apply cleanly. Resolve it in $main_worktree, or
rebase the branch onto main and run this again."
git -C "$main_worktree" commit -q -F "$tmp"

# --- 8. did it actually land? ----------------------------------------------------

if [ "$(merged_tree)" != "$(main_tree)" ]; then
    die "The branch did NOT land in full — merging '$branch' into main would
still change something. Nothing was rolled back; look at $main_worktree
before doing anything else."
fi

printf '\n'
git -C "$main_worktree" log --oneline -1 | sed 's/^/  /'
printf '  main and %s are identical in content\n' "$branch"
printf '\nThe branch is kept: the squash collapses the task into one commit, and the\n'
printf 'fine-grained history survives only there (conventions/git.md).\n'
