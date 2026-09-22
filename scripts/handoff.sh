#!/bin/sh
# Prepare the prompt that starts the NEXT task in a fresh session.
#
# Usage:
#   scripts/handoff.sh <short-id> [--vendor claude|codex] [--note "<text>"]
#                                 [--out <path>] [--command-only]
#
#   scripts/handoff.sh tech-004
#   scripts/handoff.sh feat-001 --vendor codex
#   scripts/handoff.sh tech-004 --note "the CI runner is the shared one"
#
# WHEN to hand a task over at all is the table in
# docs/ai-workflow/handoff.md — read it first; this script assumes the
# decision is already made. Briefly: an auto-compaction returns the context to
# a fresh session's floor exactly (42,236 against 42,113, measured), so the
# reason to hand over is not cost. It is that a compaction carries the
# finished task's summary into the next one, and that `status.md` is only ever
# tested by someone who starts from it.
#
# This script writes the prompt; the human delivers it — as a background task
# chip, or with the command printed below it as the fallback. It does NOT start
# the session itself: the prompt is read by a human first, and a human stays in
# the loop to answer the new session's gates (docs/ai-workflow/handoff.md).
#
# WHAT GOES IN. Only what the new session cannot read for itself: the id, the
# stage, where the work lives, what to read first. Everything else it reads
# from the artifacts — that is what the artifacts are for. `--note` is the
# exception, and it is a smell: if a run cannot start without a fact that is
# in no file, the missing file is the defect, not the prompt.

set -eu

die() {
    printf '\n%s\n\n' "$1" >&2
    exit 1
}

usage() {
    sed -n '2,29p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

case "${1:-}" in
    -h|--help|'') usage 0 ;;
esac

short=$1
shift
vendor=claude
note=''
out=''
command_only=0
while [ $# -gt 0 ]; do
    case "$1" in
        --vendor)       vendor=${2:?--vendor needs claude or codex}; shift 2 ;;
        --note)         note=${2:?--note needs text}; shift 2 ;;
        --out)          out=${2:?--out needs a path}; shift 2 ;;
        --command-only) command_only=1; shift ;;
        -*)             die "Unknown option: $1" ;;
        *)              die "Unexpected argument: $1" ;;
    esac
done

case "$vendor" in
    claude|codex) ;;
    *) die "Unknown vendor '$vendor'. Known: claude, codex." ;;
esac

printf '%s' "$short" | grep -qE '^(feat|bug|hot|tech|mod|doc)-[0-9]{3}$' \
    || die "'$short' is not a short id like tech-004."

git rev-parse --show-toplevel >/dev/null 2>&1 || die "Not a git repository."

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

# The main worktree holds the queue, and is where a task that has not started
# yet begins.
main_worktree=$(worktree_of main)
[ -n "$main_worktree" ] || die "No worktree has main checked out."

# --- which task, and in what state ------------------------------------------------

queue_line=$(git -C "$main_worktree" show main:docs/backlog.md 2>/dev/null \
    | grep -m1 -E "^- \[[ x]\] $short-" || true)

id=''
if [ -n "$queue_line" ]; then
    id=$(printf '%s' "$queue_line" \
        | grep -oE '^- \[[ x]\] [a-z]+-[0-9]{3}-[a-z0-9-]+' | sed 's/^- \[[ x]\] //' || true)
fi
if [ -z "$id" ]; then
    id=$(ls -1 "$main_worktree/docs/features" 2>/dev/null | grep -m1 -E "^$short-" || true)
fi
if [ -z "$id" ]; then
    id=$(git -C "$main_worktree" branch --format='%(refname:short)' 2>/dev/null \
        | grep -m1 -E "^[a-z]+/${short#*-}-" | sed 's|/|-|' || true)
fi
[ -n "$id" ] || die "No task '$short' — not in the queue of main, not in docs/features/,
no branch holds it. What exists:
    bash scripts/backlog.sh"

branch=$(printf '%s' "$id" | sed 's/^\([a-z]*\)-/\1\//')

landed=$(git -C "$main_worktree" show "main:docs/features/$id/status.md" 2>/dev/null \
    | grep -m1 '^- stage:' | sed 's/^- stage: *//' || true)
if [ "$landed" = 'complete' ]; then
    die "$id is already merged into main (stage: complete). Nothing to hand off."
fi

started=0
if git -C "$main_worktree" rev-parse --verify --quiet "refs/heads/$branch" >/dev/null 2>&1; then
    started=1
fi

card=''
if [ "$started" = 1 ]; then
    card=$(git -C "$main_worktree" show "$branch:docs/features/$id/status.md" 2>/dev/null || true)
fi

field() {
    printf '%s\n' "$card" | grep -m1 "^- $1:" | sed "s/^- $1: *//" || true
}
from_line() {
    printf '%s' "$queue_line" | grep -oE "$1:[^ ]+" | cut -d: -f2 || true
}

stage=$(field stage)
epic=$(field epic); [ -n "$epic" ] || epic=$(from_line epic)
# A queued task has no status.md yet, and its type is the id's prefix — the
# same mapping docs/features/README.md defines.
type=$(field type)
if [ -z "$type" ]; then
    case "${id%%-*}" in
        feat) type=feature ;;
        bug)  type=bugfix ;;
        hot)  type=hotfix ;;
        tech) type=tech ;;
        mod)  type=modification ;;
        doc)  type=docs ;;
    esac
fi
size=$(field size); [ -n "$size" ] || size=$(from_line size)
deps=$(field deps); [ -n "$deps" ] || deps=$(from_line deps)

# Where the new session must run. Both vendors group sessions by working
# directory — that is the whole of "the same project": no API, just the cwd.
worktree=''
if [ "$started" = 1 ]; then
    worktree=$(worktree_of "$branch")
fi
cwd=${worktree:-$main_worktree}

# --- the prompt --------------------------------------------------------------------

[ -n "$out" ] || out="${TMPDIR:-/tmp}/mustask-handoff-$id.md"

{
    if [ "$started" = 1 ]; then
        printf 'Continue task %s. It is already started: branch `%s`, stage `%s`.\n\n' \
            "$id" "$branch" "${stage:-unknown}"
        printf 'If this session opened in a fresh worktree of its own — a task chip does\n'
        printf 'that, on a `claude/*` branch — then switch onto the task branch first,\n'
        printf 'where the work and every commit of it are:\n\n'
        printf '    git switch %s\n\n' "$branch"
        printf 'Do NOT run `scripts/new-task.sh` or `scripts/queue.sh take`: the branch,\n'
        printf 'the folder and the queue line exist already.\n\n'
        printf 'Read `docs/features/%s/status.md` first and continue from the `stage:` in\n' "$id"
        printf 'it, then the artifacts that stage needs and the matching\n'
        printf '`docs/ai-workflow/stages/*.md`. The classification is already recorded —\n'
        printf 'do not re-derive it, and do not re-read `routing.md` unless the route\n'
        printf 'itself is in question.\n'
    else
        printf 'Start task %s. It is queued in main and not started: no branch, no\n' "$id"
        printf 'folder, nothing claimed yet.\n\n'
        printf 'Classify it per `docs/ai-workflow/routing.md`, show the `ROUTE` block and\n'
        printf 'wait for the human. Only then `scripts/new-task.sh %s` and\n' \
            "$(printf '%s' "$id" | sed 's/^\([a-z]*\)-[0-9][0-9]*-/\1 /')"
        printf '`scripts/queue.sh take %s`.\n' "$short"
        if [ -n "$queue_line" ]; then
            printf '\nThe queue line, verbatim:\n\n    %s\n' "$queue_line"
        fi
    fi

    printf '\nFacts, so you do not go looking for them:\n\n'
    [ -n "$type" ] && printf -- '- type: %s\n' "$type" || true
    [ -n "$size" ] && printf -- '- size: %s\n' "$size" || true
    [ -n "$epic" ] && printf -- '- epic: %s\n' "$epic" || true
    if [ -n "$deps" ] && [ "$deps" != '-' ]; then
        printf -- '- deps: %s — check they are merged before starting\n' "$deps"
    fi
    printf -- '- working directory: %s\n' "$cwd"
    if [ "$started" = 1 ] && [ -d "$cwd/docs/features/$id" ]; then
        printf -- '- artifacts already written: %s\n' \
            "$(ls -1 "$cwd/docs/features/$id" | tr '\n' ' ')"
    fi

    if [ -n "$note" ]; then
        printf '\nFrom the session that handed this over:\n\n%s\n' "$note"
    fi

    printf '\nFirst, name this session `%s` — it is how the work is found in the\n' "$id"
    printf 'sidebar and the resume picker. Then start on the above.\n'

    printf '\nThe rules are in `AGENTS.md` and the stage file; read them there rather\n'
    printf 'than expecting this prompt to repeat them.\n'
} > "$out"

# --- the command, and only the command ---------------------------------------------

case "$vendor" in
    claude) launch="claude -n '$id' \"\$(cat '$out')\"" ;;
    codex)  launch="codex \"\$(cat '$out')\"" ;;
esac
run="cd '$cwd' && $launch"

if [ "$command_only" = 1 ]; then
    printf '%s\n' "$run"
    exit 0
fi

printf '\n--- prompt (%s) ---\n\n' "$out"
cat "$out"

# A task chip spawns the session with its own fresh worktree on a claude/*
# branch, and a human clicks it — which is the only thing the printed command
# ever had over it. For a task that has not started the fresh worktree is
# exactly right: that session runs new-task.sh and makes the proper branch
# inside it. For a task already in flight the branch exists and only one
# worktree may hold it, so the task's worktree is removed first and the new
# session switches onto the branch (docs/ai-workflow/handoff.md).
if [ "$vendor" = claude ]; then
    printf '\n--- how to deliver it ---\n\n'
    printf 'In the desktop app, offer this prompt as a background task chip. The\n'
    printf 'command below is the fallback, for when there is no chip (another vendor,\n'
    printf 'a session without the tool) or when a terminal is preferred.\n'
    if [ "$started" = 1 ]; then
        printf '\nThe task is started, so free its branch before clicking the chip — the\n'
        printf 'chip makes a worktree of its own, and the new session switches onto the\n'
        printf 'branch there:\n\n'
        printf '    git worktree remove %s\n\n' "$cwd"
        printf 'The branch keeps every commit. If the remove fails, check `git worktree\n'
        printf 'list` and `git branch -v` rather than the exit code: on Windows it can\n'
        printf 'report Permission denied having already deregistered the worktree and\n'
        printf 'emptied the directory. Delete the leftover empty folder — it blocks a\n'
        printf 'later new-task.sh for the same slug. The fallback command runs in that\n'
        printf 'same worktree, so take that route instead if you would rather keep it.\n'
    fi
fi

printf '\n--- the command, to run after the human has seen the prompt ---\n\n'
printf '%s\n' "$run"

# A command naming a CLI that is not installed is worse than no command:
# it fails in the terminal, away from any explanation. Say it here. The
# script does not install anything — that is a change to the machine, and
# the human makes it.
if ! command -v "$vendor" >/dev/null 2>&1; then
    printf '\n%s is not on PATH, so that command will not run as it stands.\n' "$vendor"
    case "$vendor" in
        claude) printf 'Install it: npm install -g @anthropic-ai/claude-code\n' ;;
        codex)  printf 'Install it: npm install -g @openai/codex\n' ;;
    esac
    printf 'The prompt above is written and stays valid whichever way you start\n'
    printf 'the session.\n'
fi

printf '\nThe prompt above is the whole handoff.\n'
if [ "$vendor" = claude ]; then
    printf 'It is the same prompt either way — the chip is only how it is delivered,\n'
    printf 'and the command is the fallback.\n'
fi
printf 'What this script does not do is start the session itself: the human sees\n'
printf 'the prompt first (docs/ai-workflow/handoff.md) and stays in the loop to\n'
printf 'answer its gates.\n'
if [ -n "$note" ]; then
    printf '\n--note was used: what you had to say there is a fact that lives in no\n'
    if [ "$started" = 1 ]; then
        printf 'artifact. Put it in docs/features/%s/status.md before it is lost.\n' "$id"
    else
        # This task has no folder yet, so status.md is nowhere to put it.
        # The queue line is, and it already carries a note: field.
        printf 'artifact, and this task has no folder yet — its place is the\n'
        printf 'note: field of its queue line in main.\n'
    fi
fi
