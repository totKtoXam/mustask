#!/bin/sh
# The queue in main: put a task in, take it out, withdraw it.
#
# Usage:
#   scripts/queue.sh [--local] add <prefix> <slug> "<description>" [options]
#   scripts/queue.sh [--local] take <short-id>
#   scripts/queue.sh [--local] withdraw <short-id> "<reason>"
#   scripts/queue.sh next <prefix>
#
#   With a remote, the change lands as a PR: a commit on a fresh
#   sync/queue-<date>-<op>-<short-id> branch from origin/main, pushed, a squash
#   PR opened with gh and auto-merge asked for. The local main is not touched.
#   --local  commit in the worktree that has main checked out instead (the
#            no-remote case; also chosen by itself when there is no origin)
#
#   options for add:
#     --epic <epic>    default: workflow
#     --size <F|S|M|L> default: S
#     --deps <ids>     comma-separated short ids, default: -
#     --note <text>    default: -
#     --later          file under "Later" instead of "Next"
#
#   scripts/queue.sh add tech gitlab-ci "CI in GitLab" --epic platform/ci --size S
#   scripts/queue.sh take tech-004
#   scripts/queue.sh withdraw tech-004 "CI moved to the platform team"
#
# Why a script: 66 of the first 108 commits on main changed nothing but the
# queue. Every one was a hand edit — find the section, write the line by the
# format, invent a message, stage the right files. Mechanical work with three
# ways to get it wrong: a number that is already taken, a line in the wrong
# section, a foreign file caught in a commit to main.
#
# The number is issued HERE and only here, by the commit that puts the line
# in (docs/features/numbering.md). `add` computes it from
# `scripts/backlog.sh --ids` — one implementation of "which numbers are taken",
# not two: branch names, this worktree's docs/features folders and the queue
# files. The queue files are read there over every local and remote-tracking
# branch, so a number issued by an earlier `add` or `withdraw` that is not
# pushed yet is already taken. If that read fails, `add` and `next` refuse
# rather than issue a number from a partial list.
#
# The queue lives in main, and main takes changes only through PRs. Until
# tech-049 every subcommand committed on the local main of the root checkout,
# so each queue commit was then hand-moved to a sync/* branch, PR'd, and main
# reset (#33, #34/#35, #36 on 2026-10-28/29) — and a commit nobody moved sat
# unpushed on main for days. Now the commit is built in a throwaway worktree
# detached at origin/main and pushed straight to its sync branch: no local
# branch, nothing on main, and it runs from any worktree. A pushed but
# unmerged sync branch already holds its number — backlog.sh reads the queue
# files of every remote-tracking branch. `.githooks/pre-commit` lets a commit
# through whose every staged path is a queue file; this script stages exactly
# those and nothing else.

set -eu

# Scratch outside the repository, removed however the script ends: a temporary
# file next to docs/backlog.md survives a failure and is one `git add` away
# from the queue commit.
tmp=$(mktemp 2>/dev/null || printf '%s/queue-sh-%s' "${TMPDIR:-/tmp}" "$$")
# The throwaway worktree of the PR mode, when there is one. Removed on every
# exit: the commit in it is either pushed by then or was never meant to be.
scratch=''
cleanup() {
    rm -f "$tmp"
    if [ -n "$scratch" ]; then
        git -C "$repo" worktree remove --force "$scratch/wt" >/dev/null 2>&1 || true
        rm -rf "$scratch"
    fi
}
trap cleanup EXIT
trap 'exit 130' INT TERM

die() {
    printf '\n%s\n\n' "$1" >&2
    exit 1
}

usage() {
    sed -n '2,25p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

mode=pr
if [ "${1:-}" = --local ]; then
    mode=local
    shift
fi

case "${1:-}" in
    -h|--help|'') usage 0 ;;
esac

command=$1
shift
case "$command" in
    next|add|take|withdraw) ;;
    *) die "Unknown command '$command'. See scripts/queue.sh --help." ;;
esac

repo=$(git rev-parse --show-toplevel) || die "Not a git repository."

# QUEUE_GH points the PR step at another gh; scripts/tests/queue-cases.sh uses
# it for a stub and for "gh is not installed".
gh_command=${QUEUE_GH:-gh}

if [ "$mode" = pr ] && ! git remote get-url origin >/dev/null 2>&1; then
    printf 'No remote "origin" — committing on the local main (--local).\n'
    mode=local
fi

# Fresh remote-tracking refs before anything is counted: an add PR pushed from
# another session holds its number only once this clone has fetched it
# (numbering.md, source 1).
if [ "$mode" = pr ]; then
    if ! git fetch --prune --quiet origin; then
        [ "$command" = next ] || die "git fetch origin failed, so the queue on origin/main and the numbers held by
other sessions' sync branches are unknown. Nothing was changed."
        printf 'Warning: git fetch origin failed — the number below may be stale.\n' >&2
    fi
fi

# --- where the queue is edited ----------------------------------------------------
#
# PR mode: a throwaway worktree detached at origin/main. Local mode: the
# worktree that has main checked out, which must be clean.

work=''
if [ "$command" != next ]; then
    if [ "$mode" = pr ]; then
        git rev-parse --verify --quiet origin/main >/dev/null \
            || die "origin/main not found after the fetch — nothing to build the queue commit on."
        scratch=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/queue-sh-wt-$$"; mkdir -p "$d"; printf '%s' "$d"; })
        git -C "$repo" worktree add --quiet --detach "$scratch/wt" origin/main \
            || die "Could not create a temporary worktree at origin/main. Nothing was changed."
        work="$scratch/wt"
    else
        current=''
        while IFS= read -r line; do
            case "$line" in
                'worktree '*) current=${line#worktree } ;;
                'branch refs/heads/main') work=$current; break ;;
            esac
        done <<LIST
$(git worktree list --porcelain)
LIST
        [ -n "$work" ] || die "No worktree has main checked out — nowhere to put the queue line.
The queue lives in main (docs/features/numbering.md); check main out somewhere and repeat."
        dirty=$(git -C "$work" status --porcelain)
        [ -z "$dirty" ] || die "The main worktree is not clean:

$dirty

A queue commit must contain only the queue. Deal with these changes first."
    fi

    queue_file="$work/docs/backlog.md"
    withdrawn_file="$work/docs/backlog/withdrawn.md"
    [ -f "$queue_file" ] || die "Not found: $queue_file"
fi

# --- helpers --------------------------------------------------------------------

# The taken numbers are read into $taken_cache by `read_taken`, at top level
# (never inside a $(...), where `die` would end only the substitution). When
# backlog.sh cannot read the queue files it says why on stderr and exits
# non-zero, and this refuses: a number issued from a partial list is a number
# issued twice (tech-034, QA D1).
taken_cache=''
read_taken() {
    taken_cache=$(sh "$repo/scripts/backlog.sh" --ids) \
        || die "${1:-Could not read the taken numbers (scripts/backlog.sh --ids failed, see above), so no number is issued from a partial list.
Nothing was changed.}"
}

taken_ids() {
    [ -z "$taken_cache" ] || printf '%s\n' "$taken_cache"
}

# Next free number for a prefix, from $taken_cache. Leading zeros are stripped
# as text: 008 in arithmetic is an octal trap, and 10#008 is a bashism that dash
# rejects.
next_number() {
    last=$(taken_ids | grep -oE "^$1-[0-9]{3}$" | grep -oE '[0-9]{3}$' | sort -n | tail -1 || true)
    bare=$(printf '%s' "${last:-000}" | sed 's/^0*//')
    printf '%03d' $(( ${bare:-0} + 1 ))
}

# The full id of a queued task, from its short id. Also proves the line exists.
line_of() {
    grep -m1 -E "^- \[[ x]\] $1-" "$queue_file" || true
}

id_of_line() {
    printf '%s' "$1" | grep -oE '^- \[[ x]\] [a-z]+-[0-9]{3}-[a-z0-9-]+' | sed 's/^- \[[ x]\] //'
}

commit_queue() {
    # The subject is built from an id whose slug has no length limit, and
    # commit-msg rejects anything longer than 72 characters. It rejects it
    # AFTER the file is staged, so the queue is left modified in main with no
    # commit — a 47-character id once hit exactly that. Measure the
    # subject before anything is staged.
    if [ "${#2}" -gt 72 ]; then
        # The queue file is already rewritten by the time we get here, and the
        # subcommand refused to run on a dirty main — so putting these paths
        # back is exactly undoing our own edit, and it leaves main as it was
        # instead of half-changed.
        git -C "$work" checkout -- $1 2>/dev/null || true
        die "Commit subject is ${#2} characters, the hook allows 72:
    $2
Nothing was changed. Shorten the slug, or the wording in queue.sh that
builds this subject."
    fi
    # Explicit paths only, never `git add -A`: the pre-commit exception for
    # main dies on one foreign path, and silently staging someone else's work
    # is how that happens.
    for path in $1; do
        git -C "$work" add -- "$path"
    done
    staged=$(git -C "$work" diff --cached --name-only)
    [ -n "$staged" ] || die "Nothing changed — the queue already looks like this."
    # Subject, then body. Building the subject out of a queue line's whole
    # description produced a 169-character subject, commit-msg rejected it,
    # and the queue file was left modified but uncommitted.
    commit_subject=$2
    commit_body=${3:-}
    if [ -n "${3:-}" ]; then
        git -C "$work" commit -q -m "$2" -m "$3"
    else
        git -C "$work" commit -q -m "$2"
    fi
    printf '%s\n' "$staged" | sed 's/^/  /'
    git -C "$work" log --oneline -1 | sed 's/^/  /'
}

# --- subcommands ----------------------------------------------------------------

case "$command" in

next)
    prefix=${1:-}
    [ -n "$prefix" ] || die "Usage: scripts/queue.sh next <prefix>"
    read_taken
    printf '%s-%s\n' "$prefix" "$(next_number "$prefix")"
    # A read-only query: no invariant footer, so the output can be used as is.
    exit 0
    ;;

add)
    prefix=${1:-}; slug=${2:-}; description=${3:-}
    [ -n "$prefix" ] && [ -n "$slug" ] && [ -n "$description" ] \
        || die "Usage: scripts/queue.sh add <prefix> <slug> \"<description>\" [options]"
    shift 3

    epic=workflow
    size=S
    deps='-'
    note='-'
    section='## Next'
    while [ $# -gt 0 ]; do
        case "$1" in
            --epic)  epic=${2:?--epic needs a value}; shift 2 ;;
            --size)  size=${2:?--size needs a value}; shift 2 ;;
            --deps)  deps=${2:?--deps needs a value}; shift 2 ;;
            --note)  note=${2:?--note needs a value}; shift 2 ;;
            --later) section='## Later'; shift ;;
            *) die "Unknown option: $1" ;;
        esac
    done

    case "$prefix" in
        feat|bug|hot|tech|mod|doc) ;;
        *) die "Prefix '$prefix' is not one of feat, bug, hot, tech, mod, doc (routing.md)." ;;
    esac
    case "$size" in
        F|S|M|L) ;;
        *) die "Size '$size' is not one of F, S, M, L (routing.md)." ;;
    esac
    printf '%s' "$slug" | grep -qE '^[a-z0-9]+(-[a-z0-9]+)*$' \
        || die "Slug '$slug' is not kebab-case Latin (docs/features/README.md)."

    # The order is queue line first, branch second (docs/features/numbering.md).
    # Reversed, the two allocate different numbers: the branch takes one, and
    # `add` then issues the next free one — which is how one piece of work
    # once got a branch under one number and a queue line under the next. Nothing enforced the order, so
    # nothing caught it; this does.
    clash=$(git branch --all --format='%(refname:short)' \
        | grep -E "^(remotes/[^/]+/)?$prefix/[0-9]{3}-$slug$" | head -1 || true)
    if [ -z "$clash" ]; then
        clash=$(ls "$work/docs/features" 2>/dev/null \
            | grep -E "^$prefix-[0-9]{3}-$slug$" | head -1 || true)
    fi
    if [ -n "$clash" ]; then
        die "'$clash' already exists, so this work already holds a number.

The queue line comes first and the branch second; done the other way round the
branch takes one number and this command issues another for the same task.

If '$clash' is real work — it is already started, and there is nothing to
queue. If it was created by mistake and holds no commits and no folder, delete
it and run this again."
    fi
    read_taken
    number=$(next_number "$prefix")
    id="$prefix-$number-$slug"
    # Read again, as late as possible: the check is against the queue as it is
    # now, not against the list the number was computed from.
    read_taken
    taken_ids | grep -qx "$prefix-$number" && die "Number $prefix-$number is already taken — the count is stale, run scripts/backlog.sh --ids."

    entry="- [ ] $id — $description · epic:$epic · size:$size · deps:$deps · note:$note"

    # Insert as the last entry of the section: find the header, then the next
    # header, then step back over the blank lines between them.
    start=$(grep -n "^$section" "$queue_file" | head -1 | cut -d: -f1)
    [ -n "$start" ] || die "Section '$section' not found in docs/backlog.md."
    offset=$(sed -n "$((start + 1)),\$p" "$queue_file" | grep -n '^## ' | head -1 | cut -d: -f1)
    if [ -n "$offset" ]; then
        insert_at=$((start + offset - 1))
    else
        insert_at=$(grep -c '' "$queue_file")
    fi
    while [ "$insert_at" -gt "$start" ] && [ -z "$(sed -n "${insert_at}p" "$queue_file")" ]; do
        insert_at=$((insert_at - 1))
    done

    {
        sed -n "1,${insert_at}p" "$queue_file"
        printf '%s\n' "$entry"
        sed -n "$((insert_at + 1)),\$p" "$queue_file"
    } > "$tmp"
    mv "$tmp" "$queue_file"

    commit_queue 'docs/backlog.md' "Queue $id" "$description"
    printf '\n%s\n' "$id"
    if [ "$mode" = pr ]; then
        printf 'Next, once the PR below is merged: scripts/new-task.sh %s %s\n' "$prefix" "$slug"
    else
        printf 'Next: scripts/new-task.sh %s %s\n' "$prefix" "$slug"
    fi
    ;;

take)
    short=${1:-}
    [ -n "$short" ] || die "Usage: scripts/queue.sh take <short-id>"
    line=$(line_of "$short")
    [ -n "$line" ] || die "No queue line for '$short' in docs/backlog.md.
Already taken, or never queued — scripts/backlog.sh shows the queue. Queued by
an add PR that is still open? The line counts once it is merged into main."
    id=$(id_of_line "$line")

    # The branch is the claim on the work, and the line may only leave the
    # queue once it exists: a number held by no queue line, no branch and no
    # folder is invisible to all three sources and looks free again
    # (docs/features/numbering.md). Changed your mind before starting? That is
    # `withdraw` — it keeps the number.
    branch=$(printf '%s' "$id" | sed "s|-|/|")
    held=0
    if git show-ref --verify --quiet "refs/heads/$branch"; then held=1; fi
    if git show-ref --verify --quiet "refs/remotes/origin/$branch"; then held=1; fi
    if [ -d "$work/docs/features/$id" ]; then held=1; fi
    if [ "$held" = 0 ]; then
        die "Branch '$branch' does not exist, so nothing holds the id ${id} yet.
Create it first — scripts/new-task.sh takes the id from this very line — and
only then remove the line. If the task is not going to be done at all:
    scripts/queue.sh withdraw $short \"<reason>\""
    fi

    grep -v -F -x -e "$line" "$queue_file" > "$tmp"
    mv "$tmp" "$queue_file"

    commit_queue 'docs/backlog.md' "Take $id"
    printf '\nThe number stays taken: the branch and docs/features/%s/ hold it now.\n' "$id"
    ;;

withdraw)
    short=${1:-}; reason=${2:-}
    [ -n "$short" ] && [ -n "$reason" ] \
        || die "Usage: scripts/queue.sh withdraw <short-id> \"<reason>\""
    line=$(line_of "$short")
    [ -n "$line" ] || die "No queue line for '$short' in docs/backlog.md.
A task withdrawn AFTER it starts is not written to the withdrawn list at all —
its number is held by the branch and its folder (docs/features/numbering.md)."
    id=$(id_of_line "$line")

    grep -v -F -x -e "$line" "$queue_file" > "$tmp"
    mv "$tmp" "$queue_file"

    [ -f "$withdrawn_file" ] || die "Not found: $withdrawn_file"
    printf -- '- [x] %s — withdrawn: %s, %s\n' "$id" "$reason" "$(date +%Y-%m-%d)" >> "$withdrawn_file"

    commit_queue 'docs/backlog.md docs/backlog/withdrawn.md' "Withdraw $id" "$reason"
    printf '\nThe number stays taken and is never reused (docs/features/numbering.md).\n'
    ;;

*)
    die "Unknown command '$command'. See scripts/queue.sh --help."
    ;;
esac

# --- landing: PR mode only --------------------------------------------------------
#
# The commit goes straight to a new remote branch; this clone never gets a
# local one, so there is nothing to delete or to leave behind. A failed push
# leaves nothing at all — the commit dies with the throwaway worktree, and the
# number it would have issued is free again.
if [ "$mode" = pr ]; then
    short=$(printf '%s' "$id" | grep -oE '^[a-z]+-[0-9]{3}')
    sync="sync/queue-$(date +%Y-%m-%d)-$command-$short"
    n=1
    candidate=$sync
    while git show-ref --verify --quiet "refs/remotes/origin/$candidate" \
        || git show-ref --verify --quiet "refs/heads/$candidate" \
        || git ls-remote --exit-code --heads origin "$candidate" >/dev/null 2>&1; do
        n=$((n + 1))
        candidate="$sync-$n"
    done
    sync=$candidate

    git -C "$work" push --quiet origin "HEAD:refs/heads/$sync" \
        || die "The push of $sync failed (see above). Nothing reached origin, and the
local main was never touched; run the same command again."
    printf '\nPushed %s\n' "$sync"

    pr_body="${commit_body:+$commit_body

}Opened by \`scripts/queue.sh $command\`. Until this merges, the branch holds the
number: \`scripts/backlog.sh --ids\` reads the queue files of every
remote-tracking branch (docs/features/numbering.md)."

    left_for_human() {
        printf '\nLeft for a human: open a squash PR from %s into main and merge it.\n' "$sync"
        printf '  gh pr create --base main --head %s --title "%s"\n' "$sync" "$commit_subject"
        printf '  gh pr merge <url> --auto --squash\n'
    }

    if ! command -v "$gh_command" >/dev/null 2>&1; then
        printf '\ngh not found (%s).\n' "$gh_command"
        left_for_human
    elif ! pr_url=$("$gh_command" pr create --base main --head "$sync" \
            --title "$commit_subject" --body "$pr_body"); then
        printf '\ngh pr create failed (see above).\n'
        left_for_human
    else
        # Two queue PRs open at once both edit docs/backlog.md; the second can
        # conflict once the first merges. Said, not solved: auto-merge keeps
        # the window to one CI run.
        others=$("$gh_command" pr list --state open --json url,headRefName \
            --jq '.[] | select(.headRefName | startswith("sync/queue-")) | .url' 2>/dev/null \
            | grep -v -x -F "$pr_url" || true)
        if [ -n "$others" ]; then
            printf '\nOther queue PRs are open; if this one conflicts after they merge, rebase it on origin/main:\n'
            printf '%s\n' "$others" | sed 's/^/  /'
        fi
        if "$gh_command" pr merge "$pr_url" --auto --squash; then
            printf '\nAuto-merge (squash) is on: it merges when CI is green.\n'
        else
            printf '\nAuto-merge was refused (see above). Left for a human: merge it (squash) when CI is green.\n'
        fi
        printf 'PR: %s\n' "$pr_url"
    fi
fi

# --- the invariant --------------------------------------------------------------
#
# Whatever the subcommand did, no number may have gone missing: `add` adds
# exactly one, `take` and `withdraw` keep every one of them. Checked out loud,
# because a lost number is the one failure here that is silent until two tasks
# answer to the same id.
read_taken "The queue commit above was made, but the taken numbers could not be read
afterwards (scripts/backlog.sh --ids failed, see above), so the check that no
number went missing did not run. Run scripts/backlog.sh --ids to see why."
after=$(taken_ids | wc -l | tr -d ' ')
printf 'Taken numbers now: %s\n' "$after"
