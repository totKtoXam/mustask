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
#     files — docs/backlog.md and every .md under docs/backlog/ — over EVERY local
#     and remote-tracking branch (docs/features/numbering.md, source 3).
#     `queue.sh add` commits to local main, so a number issued before the push
#     is on no remote branch; reading origin/main alone issued it twice.
#
# The docs/features folders are read the same way, over every ref (source 2):
# a folder on any branch holds its number, and a task folder whose card is
# `complete` on no ref is in flight even with no local branch (tech-038).

set -eu

# Root of the CURRENT worktree — for docs/features and branches. The queue
# itself is read from MAIN: that's where it lives (a number is assigned by a
# commit to main), and the copy of docs/backlog.md in the task
# branch's worktree can be arbitrarily stale.
cd "$(git rev-parse --show-toplevel)"

# Queue: origin/main with a configured remote (fresher for others' pushes; own
# unpushed queue commits are noted under QUEUED), else main. Both unavailable — said out loud, not a silent empty queue. Read once
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
# refs/stash). The queue files are docs/backlog.md and every .md file under
# docs/backlog/ — withdrawn.md (a withdrawn number is taken forever and never
# reused) and whatever a split moves there, such as later.md (tech-037; until
# then only the two names were read, and a number in a third file looked free).
#
# A fixed number of git processes however many refs there are — a git process
# per ref or per commit is what this avoids:
#   1. for-each-ref: the distinct commits behind the refs;
#   2. cat-file --batch-check: `<commit>:docs/backlog.md` (a blob) and
#      `<commit>:docs/backlog` (a tree) for each of them;
#   3. rev-list --objects over the distinct docs/backlog trees: every file
#      under them, each distinct blob once;
#   4. cat-file --batch: the distinct blobs, read in one go.
# A ref without a file or the directory answers "missing" — no match.
#
# It fails loudly. A read that quietly returns nothing makes every number look
# free, and `queue.sh add` would issue a taken one: so a git that errors, or
# answers fewer requests than were asked, is an error on stderr and a
# non-zero return — never an empty list (tech-034, QA D1).
queue_read_failed() {  # <why> [<what: the queue files | the docs/features folders>]
    printf 'Error: %s could not be read from the git object store (%s) — the taken numbers they hold are unknown, so none is reported.\n' \
        "${2:-the queue files}" "$1" >&2
}

# <requests, one per line> [<what>] — `git cat-file --batch-check` over them,
# every request answered: `<oid> <type> <size>`, or `<request> missing`.
queue_batch_check() {
    _out=$(printf '%s\n' "$1" | git cat-file --batch-check) \
        || { queue_read_failed 'git cat-file --batch-check failed' "${2:-}"; return 1; }
    _asked=$(printf '%s\n' "$1" | grep -c '')
    _got=$(printf '%s\n' "$_out" \
        | grep -cE '^[0-9a-f]{40,64}( [a-z]+ [0-9]+|(:[^ ]+)? missing)$' || true)
    if [ "$_got" -lt "$_asked" ]; then
        queue_read_failed "git cat-file --batch-check answered $_got of $_asked requests" "${2:-}"
        return 1
    fi
    printf '%s\n' "$_out"
}

queue_file_numbers() {
    _commits=$(git for-each-ref --format='%(objectname)' refs/heads refs/remotes) \
        || { queue_read_failed 'git for-each-ref failed'; return 1; }
    [ -n "$_commits" ] || return 0
    _commits=$(printf '%s\n' "$_commits" | sort -u)
    _found=$(queue_batch_check "$(printf '%s\n' "$_commits" | sed 's|$|:docs/backlog.md|'
        printf '%s\n' "$_commits" | sed 's|$|:docs/backlog|')") || return 1
    _blobs=$(printf '%s\n' "$_found" | awk '$2 == "blob" { print $1 }')
    _trees=$(printf '%s\n' "$_found" | awk '$2 == "tree" { print $1 }' | sort -u)
    if [ -n "$_trees" ]; then
        # Each tree given is listed first as `<oid> ` with no path, then what
        # is under it as `<oid> <path>`. Only .md files are queue files.
        _listed=$(printf '%s\n' "$_trees" | git rev-list --objects --no-walk --stdin) \
            || { queue_read_failed 'git rev-list --objects failed'; return 1; }
        _asked=$(printf '%s\n' "$_trees" | grep -c '')
        _got=$(printf '%s\n' "$_listed" | grep -cE '^[0-9a-f]{40,64} ?$' || true)
        if [ "$_got" -lt "$_asked" ]; then
            queue_read_failed "git rev-list --objects listed $_got of $_asked docs/backlog trees"
            return 1
        fi
        _blobs="$_blobs
$(printf '%s\n' "$_listed" | grep -E '^[0-9a-f]{40,64} .+\.md$' | cut -d' ' -f1 || true)"
    fi
    _blobs=$(printf '%s\n' "$_blobs" | grep -E '^[0-9a-f]{40,64}$' | sort -u || true)
    [ -n "$_blobs" ] || return 0
    _answers=$(printf '%s\n' "$_blobs" | git cat-file --batch) \
        || { queue_read_failed 'git cat-file --batch failed'; return 1; }
    # Every blob is answered by one header line: `<blob> blob <size>`.
    _asked=$(printf '%s\n' "$_blobs" | grep -c '')
    _got=$(printf '%s\n' "$_answers" | grep -cE '^[0-9a-f]{40,64} blob [0-9]+$' || true)
    if [ "$_got" -lt "$_asked" ]; then
        queue_read_failed "git cat-file --batch answered $_got of $_asked requests"
        return 1
    fi
    printf '%s\n' "$_answers" \
        | grep -oE "^- \[[ x]\] [a-z]+-[0-9]{3}" | grep -oE "[a-z]+-[0-9]{3}$" || true
}

# The task folders of docs/features over every ref in refs/heads and
# refs/remotes, with their cards: one line `<id> <stage> <size> <epic>` per
# distinct status.md blob, and `<id> - - -` for every folder (so a folder
# without a card is still listed). Until tech-038 only this worktree's
# folders were read, so a folder merged since the worktree branched, or one
# that lives on another branch only, looked free (tech-034 CR-5). The same
# fixed number of git processes as queue_file_numbers, and it fails as loudly.
features_what='the docs/features folders'
feature_cards() {
    _commits=$(git for-each-ref --format='%(objectname)' refs/heads refs/remotes) \
        || { queue_read_failed 'git for-each-ref failed' "$features_what"; return 1; }
    [ -n "$_commits" ] || return 0
    _found=$(queue_batch_check "$(printf '%s\n' "$_commits" | sort -u | sed 's|$|:docs/features|')" \
        "$features_what") || return 1
    _trees=$(printf '%s\n' "$_found" | awk '$2 == "tree" { print $1 }' | sort -u)
    [ -n "$_trees" ] || return 0
    _listed=$(printf '%s\n' "$_trees" | git rev-list --objects --no-walk --stdin) \
        || { queue_read_failed 'git rev-list --objects failed' "$features_what"; return 1; }
    _asked=$(printf '%s\n' "$_trees" | grep -c '')
    _got=$(printf '%s\n' "$_listed" | grep -cE '^[0-9a-f]{40,64} ?$' || true)
    if [ "$_got" -lt "$_asked" ]; then
        queue_read_failed "git rev-list --objects listed $_got of $_asked docs/features trees" "$features_what"
        return 1
    fi
    # `<oid> <id>` is a folder, `<oid> <id>/status.md` its card.
    _task='[a-z]+-[0-9][0-9][0-9]-[a-z0-9-]+'
    _map=$(printf '%s\n' "$_listed" | grep -E "^[0-9a-f]+ $_task/status\\.md\$" \
        | sed 's|/status\.md$||' || true)
    printf '%s\n' "$_listed" | grep -oE " $_task\$" | sort -u | sed 's|^ ||; s|$| - - -|' || true
    [ -n "$_map" ] || return 0
    _blobs=$(printf '%s\n' "$_map" | cut -d' ' -f1 | sort -u)
    _answers=$(printf '%s\n' "$_blobs" | git cat-file --batch) \
        || { queue_read_failed 'git cat-file --batch failed' "$features_what"; return 1; }
    _asked=$(printf '%s\n' "$_blobs" | grep -c '')
    _got=$(printf '%s\n' "$_answers" | grep -cE '^[0-9a-f]{40,64} blob [0-9]+$' || true)
    if [ "$_got" -lt "$_asked" ]; then
        queue_read_failed "git cat-file --batch answered $_got of $_asked requests" "$features_what"
        return 1
    fi
    # The map first, then the blobs: a header line `<oid> blob <size>` of a
    # mapped oid starts a card, its `- stage:`/`- size:`/`- epic:` fill it.
    { printf '%s\n--\n' "$_map"; printf '%s\n' "$_answers"; } | awk '
        function flush() { if (cur != "") print id[cur], st, sz, ep; cur = "" }
        !sep && $0 == "--" { sep = 1; next }
        !sep { id[$1] = $2; next }
        NF == 3 && $2 == "blob" && ($1 in id) { flush(); cur = $1; st = sz = ep = "-"; next }
        cur != "" && st == "-" && /^- stage:/ { st = $3 }
        cur != "" && sz == "-" && /^- size:/  { sz = $3 }
        cur != "" && ep == "-" && /^- epic:/  { ep = $3 }
        END { flush() }'
}

# All taken numbers. Returns non-zero, having said why on stderr and printed
# nothing, when the queue files or the folders cannot be read. [<cards>] —
# feature_cards output the caller already has, so it is not read twice.
taken_ids() {
    _queue_numbers=$(queue_file_numbers) || return 1
    if [ $# -gt 0 ]; then _cards=$1; else _cards=$(feature_cards) || return 1; fi
    {
        # This worktree's own folders too: a new folder is not committed yet.
        ls docs/features 2>/dev/null | grep -oE "^[a-z]+-[0-9]{3}" || true
        [ -z "$_cards" ] || printf '%s\n' "$_cards" | grep -oE "^[a-z]+-[0-9]{3}" || true
        git branch --all --format="%(refname:short)" | grep -oE "[a-z]+/[0-9]{3}" | tr "/" "-" || true
        [ -z "$_queue_numbers" ] || printf '%s\n' "$_queue_numbers"
    } | sort -u
}

if [ "${1:-}" = "--ids" ]; then
    taken_ids || exit 1
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


# Every ref's folders and cards, read once for IN PROGRESS, EPICS and NUMBERS.
cards_failed=0
cards=$(feature_cards) || cards_failed=1
local_task_ids=$(git branch --format="%(refname:short)" \
    | grep -E "^(feat|bug|hot|tech|mod|doc)/[0-9]{3}-" | sed "s|/|-|" || true)
# In flight on another branch (tech-038): a task folder on some ref,
# `stage: complete` on none, and no local task branch to list it by — a
# session on another machine, or a folder on a branch with a non-task name.
# A stale copy of a finished task on an old branch is not in flight: the
# finished card is on main. One row per id, from a card that is not complete.
elsewhere=''
if [ "$cards_failed" = 0 ] && [ -n "$cards" ]; then
    elsewhere=$({ printf '%s\n--\n' "$local_task_ids"; printf '%s\n' "$cards"; } | awk '
        !sep && $0 == "--" { sep = 1; next }
        !sep { skipped[$1] = 1; next }
        NF != 4 || ($1 in skipped) { next }
        $2 == "complete" { done[$1] = 1; next }
        !($1 in row) || row[$1] ~ / - - -$/ { row[$1] = $0 }
        END { for (k in row) if (!(k in done)) print row[k] }' | sort)
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
while read -r id stage size epic; do
    [ -n "$id" ] || continue
    found=1
    printf '  %-30s %-17s size:%-2s stage:%-19s (no local branch: folder on another ref)\n' "$id" "$epic" "$size" "$stage"
done <<ELSEWHERE
$elsewhere
ELSEWHERE
[ "$cards_failed" = 0 ] || printf '  (tasks whose folder lives only on another branch are unknown: the folders could not be read, see the error above)\n'
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
# Queue commits on local main that origin/main does not have yet (`queue.sh add`,
# `take` and `withdraw` commit locally, the push comes later). The list above is
# origin/main, so an add among them is not in it and a take or withdraw is not
# reflected in it yet; their numbers are counted (taken_ids) either way.
if [ "$queue_src" = origin/main ] && git rev-parse --verify --quiet main >/dev/null 2>&1; then
    unpushed_queue=$(git rev-list --count origin/main..main -- docs/backlog.md docs/backlog 2>/dev/null || echo 0)
    if [ "${unpushed_queue:-0}" -gt 0 ]; then
        printf '  (+%s unpushed queue commit(s) on local main: add/take/withdraw not in origin/main yet, so this list may differ from main; their numbers are counted)\n' "$unpushed_queue"
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
        # In flight on another branch only; a folder here is counted above.
        printf '%s\n' "$elsewhere" | while read -r id stage size e; do
            [ -n "$id" ] && [ ! -d "docs/features/$id" ] && printf '%s flight\n' "${e:--}"
        done || true
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
# Read once. A failed read is said out loud (taken_ids wrote why on stderr) and
# ends the summary non-zero: a missing line must not look like "nothing taken".
numbers_failed=0
if [ "$cards_failed" = 0 ]; then taken=$(taken_ids "$cards"); else taken=$(taken_ids); fi \
    && numbers_ok=1 || numbers_ok=0
if [ "$numbers_ok" = 1 ]; then
    for prefix in feat bug hot tech mod doc; do
        last=$(printf '%s\n' "$taken" | grep -oE "^$prefix-[0-9]{3}$" | grep -oE "[0-9]{3}$" | sort -n | tail -1)
        [ -n "${last:-}" ] || continue
        # Leading zeros are stripped as text: 008 in arithmetic is an octal
        # trap, and 10#008 is a bashism that fails under dash.
        bare=$(printf '%s' "$last" | sed 's/^0*//')
        next=$(printf '%03d' $(( ${bare:-0} + 1 )))
        printf '  %-5s taken up to %s, next %s\n' "$prefix" "$last" "$next"
    done
else
    numbers_failed=1
    printf '  unavailable — the taken numbers could not be read (see the error above)\n'
fi

printf '\n'
[ "$numbers_failed" = 0 ] || exit 1
