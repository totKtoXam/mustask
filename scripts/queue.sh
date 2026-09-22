#!/bin/sh
# The queue in main: put a task in, take it out, withdraw it.
#
# Usage:
#   scripts/queue.sh add <prefix> <slug> "<description>" [options]
#   scripts/queue.sh take <short-id>
#   scripts/queue.sh withdraw <short-id> "<reason>"
#   scripts/queue.sh next <prefix>
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
# `scripts/backlog.sh --ids` — the same three sources, so there is one
# implementation of "which numbers are taken", not two.
#
# The queue lives in main, so every subcommand commits in the MAIN worktree,
# whichever worktree you run it from. `.githooks/pre-commit` allows a commit
# to main whose every staged path is a queue file; this script stages exactly
# those and nothing else.

set -eu

# Scratch outside the repository, removed however the script ends: a temporary
# file next to docs/backlog.md survives a failure and is one `git add` away
# from the queue commit.
tmp=$(mktemp 2>/dev/null || printf '%s/queue-sh-%s' "${TMPDIR:-/tmp}" "$$")
trap 'rm -f "$tmp"' EXIT INT TERM

die() {
    printf '\n%s\n\n' "$1" >&2
    exit 1
}

usage() {
    sed -n '2,25p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

case "${1:-}" in
    -h|--help|'') usage 0 ;;
esac

command=$1
shift

repo=$(git rev-parse --show-toplevel) || die "Not a git repository."

# --- the main worktree: that is where the queue lives ---------------------------

main_worktree=''
current=''
while IFS= read -r line; do
    case "$line" in
        'worktree '*) current=${line#worktree } ;;
        'branch refs/heads/main') main_worktree=$current; break ;;
    esac
done <<LIST
$(git worktree list --porcelain)
LIST

[ -n "$main_worktree" ] || die "No worktree has main checked out — nowhere to put the queue line.
The queue lives in main (docs/features/numbering.md); check main out somewhere and repeat."

queue_file="$main_worktree/docs/backlog.md"
withdrawn_file="$main_worktree/docs/backlog/withdrawn.md"
[ -f "$queue_file" ] || die "Not found: $queue_file"

dirty=$(git -C "$main_worktree" status --porcelain)
[ -z "$dirty" ] || die "The main worktree is not clean:

$dirty

A queue commit must contain only the queue. Deal with these changes first."

# --- helpers --------------------------------------------------------------------

taken_ids() {
    sh "$repo/scripts/backlog.sh" --ids
}

# Next free number for a prefix. Leading zeros are stripped as text: 008 in
# arithmetic is an octal trap, and 10#008 is a bashism that dash rejects.
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
        git -C "$main_worktree" checkout -- $1 2>/dev/null || true
        die "Commit subject is ${#2} characters, the hook allows 72:
    $2
Nothing was changed. Shorten the slug, or the wording in queue.sh that
builds this subject."
    fi
    # Explicit paths only, never `git add -A`: the pre-commit exception for
    # main dies on one foreign path, and silently staging someone else's work
    # is how that happens.
    for path in $1; do
        git -C "$main_worktree" add -- "$path"
    done
    staged=$(git -C "$main_worktree" diff --cached --name-only)
    [ -n "$staged" ] || die "Nothing changed — the queue already looks like this."
    # Subject, then body. Building the subject out of a queue line's whole
    # description produced a 169-character subject, commit-msg rejected it,
    # and the queue file was left modified but uncommitted.
    if [ -n "${3:-}" ]; then
        git -C "$main_worktree" commit -q -m "$2" -m "$3"
    else
        git -C "$main_worktree" commit -q -m "$2"
    fi
    printf '%s\n' "$staged" | sed 's/^/  /'
    git -C "$main_worktree" log --oneline -1 | sed 's/^/  /'
}

# --- subcommands ----------------------------------------------------------------

case "$command" in

next)
    prefix=${1:-}
    [ -n "$prefix" ] || die "Usage: scripts/queue.sh next <prefix>"
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
        clash=$(ls "$main_worktree/docs/features" 2>/dev/null \
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
    number=$(next_number "$prefix")
    id="$prefix-$number-$slug"
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
    printf 'Next: scripts/new-task.sh %s %s\n' "$prefix" "$slug"
    ;;

take)
    short=${1:-}
    [ -n "$short" ] || die "Usage: scripts/queue.sh take <short-id>"
    line=$(line_of "$short")
    [ -n "$line" ] || die "No queue line for '$short' in docs/backlog.md.
Already taken, or never queued — scripts/backlog.sh shows the queue."
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
    if [ -d "$main_worktree/docs/features/$id" ]; then held=1; fi
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

# --- the invariant --------------------------------------------------------------
#
# Whatever the subcommand did, no number may have gone missing: `add` adds
# exactly one, `take` and `withdraw` keep every one of them. Checked out loud,
# because a lost number is the one failure here that is silent until two tasks
# answer to the same id.
after=$(taken_ids | wc -l | tr -d ' ')
printf 'Taken numbers now: %s\n' "$after"
