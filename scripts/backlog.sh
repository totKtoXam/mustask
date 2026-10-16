#!/bin/sh
# Project summary: what's in progress, what's queued, what's taken, what's done.
#
# The backlog stores only the queue of tasks not yet started; everything else
# is derived from branches, feature folders, and history — where it already
# lives. Duplicating that in a shared file cost a merge conflict on every
# start and finish.
#
#   scripts/backlog.sh          summary
#   scripts/backlog.sh --ids    only the taken numbers (for checking numbering)
#   scripts/backlog.sh --done   finished tasks: merge date, id, epic, size
#
# Two different reads of the queue, on purpose:
#   - the QUEUED list is origin/main (else main): what everybody else can see;
#   - the TAKEN numbers (--ids, NUMBERS, and so `queue.sh add`) are the queue
#     files — docs/backlog.md and docs/backlog/withdrawn.md — over EVERY local
#     and remote-tracking branch (docs/features/numbering.md, source 3).
#     `queue.sh add` commits to local main, so a number issued before the push
#     is on no remote branch; reading origin/main alone issued it twice.

set -eu

# Root of the CURRENT worktree — for docs/features and branches. The queue
# itself is read from MAIN: that's where it lives (a number is assigned by a
# commit to main), and the copy of docs/backlog.md in the task
# branch's worktree can be arbitrarily stale.
cd "$(git rev-parse --show-toplevel)"

# Queue: origin/main with a configured remote (fresher than local), else
# main. Both unavailable — said out loud, not a silent empty queue. Read once
# (N-2), the source is remembered for the section header (N-3). This is the
# QUEUED list only; the taken numbers do not depend on it (taken_ids below).
queue_src=origin/main
queue_cache=$(git show origin/main:docs/backlog.md 2>/dev/null) \
    || { queue_src=main; queue_cache=$(git show main:docs/backlog.md 2>/dev/null); } \
    || { queue_src='?'; queue_cache=''
         printf 'Warning: docs/backlog.md could not be read from either origin/main or main — the QUEUED list is empty; numbers are still counted from the queue files of every other branch.\n' >&2; }

main_backlog() {
    printf '%s\n' "$queue_cache"
}

# The queue files' numbers over every ref in refs/heads and refs/remotes (not
# refs/stash), in ONE pass: for-each-ref names `<commit>:<file>` for each ref
# and a single cat-file --batch reads them all — a git process per ref is what
# this replaced. Two refs on one commit are read once (sort -u).
# docs/backlog/withdrawn.md counts too: a withdrawn number is taken forever
# and is never reused. A ref without the file answers "missing" — no match.
queue_file_numbers() {
    git for-each-ref --format='%(objectname):docs/backlog.md%0a%(objectname):docs/backlog/withdrawn.md' \
        refs/heads refs/remotes 2>/dev/null \
        | sort -u \
        | git cat-file --batch 2>/dev/null \
        | grep -oE "^- \[[ x]\] [a-z]+-[0-9]{3}" | grep -oE "[a-z]+-[0-9]{3}$" || true
}

taken_ids() {
    {
        ls docs/features 2>/dev/null | grep -oE "^[a-z]+-[0-9]{3}" || true
        git branch --all --format="%(refname:short)" | grep -oE "[a-z]+/[0-9]{3}" | tr "/" "-" || true
        queue_file_numbers
    } | sort -u
}

if [ "${1:-}" = "--ids" ]; then
    taken_ids
    exit 0
fi

if [ "${1:-}" = "--done" ]; then
    # One `git log` over main rather than one per task: a squash subject
    # carries the short id in parentheses (conventions/git.md), so the merge
    # date is a lookup in that list. A task merged before the convention, or
    # one whose folder reached main some other way, prints dashes instead of
    # a date — an honest gap, not a guess.
    merge_log=$(git log main --format='%ad %h %s' --date=short 2>/dev/null || true)
    {
        for dir in docs/features/*/; do
            [ -f "$dir/status.md" ] || continue
            grep -q "^- stage: complete" "$dir/status.md" || continue
            id=$(basename "$dir")
            short=$(printf '%s' "$id" | grep -oE "^[a-z]+-[0-9]{3}" || true)
            epic=$(grep -m1 "^- epic:" "$dir/status.md" | sed "s/^- epic: *//" || true)
            size=$(grep -m1 "^- size:" "$dir/status.md" | sed "s/^- size: *//" || true)
            [ -n "$epic" ] || epic="-"
            [ -n "$size" ] || size="?"
            hit=""
            if [ -n "$short" ]; then
                hit=$(printf '%s\n' "$merge_log" | grep -m1 -E "[(]$short[,)]" || true)
            fi
            when=$(printf '%s' "$hit" | cut -d' ' -f1)
            sha=$(printf '%s' "$hit" | cut -d' ' -f2)
            if [ -z "$when" ]; then when="----------"; sha="-------"; fi
            printf '%s  %-7s  %-44s %-22s %s\n' "$when" "$sha" "$id" "$epic" "$size"
        done
    } | sort -r
    exit 0
fi


printf '\n=== IN PROGRESS (task branches, stage not complete) ===\n\n'
found=0
merged_kept=0
for branch in $(git branch --format="%(refname:short)" | grep -E "^(feat|bug|hot|tech|mod|doc)/[0-9]{3}-"); do
    id=$(printf '%s' "$branch" | sed "s|/|-|")
    status="docs/features/$id/status.md"

    stage=""
    size=""
    epic=""
    if [ -f "$status" ]; then
        stage=$(grep -m1 "^- stage:" "$status" | sed "s/^- stage: *//")
        size=$(grep -m1 "^- size:" "$status" | sed "s/^- size: *//")
        epic=$(grep -m1 "^- epic:" "$status" | sed "s/^- epic: *//")
    else
        # status.md lives on its own branch — it isn't visible from main.
        # Read it once: three `git show` calls of the same blob cost three
        # object lookups and used to drop `epic:` on the floor.
        card=$(git show "$branch:docs/features/$id/status.md" 2>/dev/null || true)
        stage=$(printf '%s
' "$card" | grep -m1 "^- stage:" | sed "s/^- stage: *//" || echo "?")
        size=$(printf '%s
' "$card" | grep -m1 "^- size:" | sed "s/^- size: *//" || echo "?")
        epic=$(printf '%s
' "$card" | grep -m1 "^- epic:" | sed "s/^- epic: *//" || echo "-")
    fi

    [ -n "$stage" ] || stage="?"
    [ -n "$size" ] || size="?"
    [ -n "$epic" ] || epic="-"

    # A merged task keeps its branch (conventions/git.md): the branch is
    # not evidence of work in progress, the stage is. Counted, not listed.
    if [ "$stage" = 'complete' ]; then
        merged_kept=$((merged_kept + 1))
        continue
    fi
    found=1
    ahead=$(git rev-list --count main.."$branch" 2>/dev/null || echo "?")
    printf '  %-30s %-17s size:%-2s stage:%-19s +%s\n' "$branch" "$epic" "$size" "$stage" "$ahead"
done
[ "$found" = 0 ] && printf '  no active task branches\n'
[ "$merged_kept" -gt 0 ] && printf '  (+%s merged branches kept, not listed)\n' "$merged_kept"

printf '\n=== QUEUED (%s:docs/backlog.md) ===\n\n' "$queue_src"
queue_lines=$(main_backlog | grep -E "^- \[ \]" || true)
if [ -z "$queue_lines" ]; then
    printf '  queue is empty\n'
else
    epics=$(printf '%s\n' "$queue_lines" | grep -oE "epic:[^ ]*" | cut -d: -f2 | sort -u)
    [ -n "$epics" ] || epics="-"
    for epic in $epics; do
        printf '  [%s]\n' "$epic"
        printf '%s\n' "$queue_lines" | grep -F "epic:$epic " | while read -r line; do
            id=$(printf '%s' "$line" | grep -oE "[a-z]+-[0-9]{3}-[a-z0-9-]+" | head -1)
            size=$(printf '%s' "$line" | grep -oE "size:[FSML]" | cut -d: -f2)
            deps=$(printf '%s' "$line" | grep -oE "deps:[^·]*" | cut -d: -f2 | sed "s/ *$//")
            printf '    %-40s size:%-2s deps:%s\n' "$id" "$size" "$deps"
        done
    done
fi
# Queue commits on local main that origin/main does not have yet (`queue.sh add`
# commits locally, the push comes later). The list above is origin/main, so
# they are not in it; their numbers are counted (taken_ids), their lines are
# not shown.
if [ "$queue_src" = origin/main ] && git rev-parse --verify --quiet main >/dev/null 2>&1; then
    unpushed_queue=$(git rev-list --count origin/main..main -- docs/backlog.md docs/backlog 2>/dev/null || echo 0)
    if [ "${unpushed_queue:-0}" -gt 0 ]; then
        printf '  (+%s unpushed queue commit(s) on local main; their numbers are counted, their lines are not listed)\n' "$unpushed_queue"
    fi
fi

printf '\n=== EPICS ===\n\n'
# One line per epic across all three states. The state of a task is its
# `stage:`, never the existence of its branch: a merged task keeps its branch
# by convention (conventions/git.md), so branches outlive the work. A task in
# flight is read from its own branch — its folder does not reach main until
# the squash — and everything else from the folders here.
epic_rows=$(
    {
        for branch in $(git branch --format="%(refname:short)" | grep -E "^(feat|bug|hot|tech|mod|doc)/[0-9]{3}-" || true); do
            id=$(printf '%s' "$branch" | sed "s|/|-|")
            [ -f "docs/features/$id/status.md" ] && continue
            card=$(git show "$branch:docs/features/$id/status.md" 2>/dev/null || true)
            e=$(printf '%s\n' "$card" | grep -m1 "^- epic:" | sed "s/^- epic: *//" || true)
            [ -n "$e" ] || e="-"
            if printf '%s\n' "$card" | grep -q "^- stage: complete"; then
                printf '%s done\n' "$e"
            else
                printf '%s flight\n' "$e"
            fi
        done
        for dir in docs/features/*/; do
            [ -f "$dir/status.md" ] || continue
            e=$(grep -m1 "^- epic:" "$dir/status.md" | sed "s/^- epic: *//" || true)
            [ -n "$e" ] || e="-"
            if grep -q "^- stage: complete" "$dir/status.md"; then
                printf '%s done\n' "$e"
            else
                printf '%s flight\n' "$e"
            fi
        done
        printf '%s\n' "$queue_lines" | grep -oE "epic:[^ ]*" | cut -d: -f2 | sed "s/$/ queued/" || true
    } 2>/dev/null
)
for e in $(printf '%s\n' "$epic_rows" | cut -d" " -f1 | sort -u); do
    [ -n "$e" ] || continue
    d=$(printf '%s\n' "$epic_rows" | grep -c "^$e done$" || true)
    f=$(printf '%s\n' "$epic_rows" | grep -c "^$e flight$" || true)
    q=$(printf '%s\n' "$epic_rows" | grep -c "^$e queued$" || true)
    printf '  %-24s %3s done, %2s in flight, %2s queued\n' "$e" "$d" "$f" "$q"
done


printf '\n=== DONE (stage: complete) ===\n\n'
done_count=0
for dir in docs/features/*/; do
    [ -f "$dir/status.md" ] || continue
    grep -q "^- stage: complete" "$dir/status.md" || continue
    done_count=$((done_count + 1))
done
printf '  %s tasks; list: scripts/backlog.sh --done\n' "$done_count"

printf '\n=== NUMBERS ===\n\n'
for prefix in feat bug hot tech mod doc; do
    last=$(taken_ids | grep -oE "^$prefix-[0-9]{3}$" | grep -oE "[0-9]{3}$" | sort -n | tail -1)
    [ -n "${last:-}" ] || continue
    # Leading zeros are stripped as text: 008 in arithmetic is an octal
    # trap, and 10#008 is a bashism that fails under dash.
    bare=$(printf '%s' "$last" | sed 's/^0*//')
    next=$(printf '%03d' $(( ${bare:-0} + 1 )))
    printf '  %-5s taken up to %s, next %s\n' "$prefix" "$last" "$next"
done

printf '\n'
