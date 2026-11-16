#!/bin/sh
# Release notes from merged pull requests (tech-069, ADR-0043).
#
# Usage:
#   scripts/release-notes.sh <ref> [<previous-tag>]
#
#   <ref>           a release tag (v0.4.1) or any other ref (origin/main, HEAD):
#                   a ref that is not a tag is a release not yet made, and the
#                   notes are headed "Unreleased" -- the preview before tagging
#   <previous-tag>  where the range starts. Default: the nearest v* tag
#                   reachable from the ref (for a tag, from the tag's parent);
#                   none -> the whole history, and the notes say it is the
#                   first release
#
# The notes list the pull requests merged into the ref's first-parent line
# between the previous tag and the ref, one line per pull request, grouped.
# A pull request is a first-parent merge commit (two parents) whose subject is
#   "Merge pull request #N from <owner>/<prefix>/<rest>"   GitHub's own, or
#   "<anything> (#N)"   a merge whose subject was rewritten on merging, e.g.
#                       "mod-021: teams in departments (#188)" (tech-071)
# Queue pull requests are squash commits (one parent), so they drop out by
# themselves; a merge from a sync/ branch, or a rewritten subject that is a
# queue subject ("Queue ", "Take ", "Withdraw "), is skipped as well.
#
# The line's title:
#   GitHub subject  the first non-blank line of the body (the PR title); an
#                   empty body -> the branch made readable, the shape PR titles
#                   have here: "feat/065-global-search" -> "feat-065: global
#                   search"; a branch not shaped <prefix>/<NNN>-<slug> as is
#   rewritten       the subject without its trailing " (#N)"
# The group:
#   docs only       every file the merge brought in (git diff --name-only
#                   <merge>^1 <merge>) is under docs/ -> Documentation,
#                   whatever the branch: an analysis or a design merged from
#                   feat/ ships no feature yet (feat-065 in v0.6.0, tech-071)
#   otherwise       the branch prefix -- for a rewritten subject, the prefix
#                   of the first task id in it ("mod-021" -> mod): feat
#                   Features; bug, fix, hot Fixes; mod Modules; tech
#                   Technical; doc Documentation; anything else Other
# Markdown goes to stdout.
#
# Pure git: no network, no gh. Titles are data -- printed, never evaluated.
# The Deploy workflow (.github/workflows/deploy.yml) runs this for the
# release page; a human runs it to preview: sh scripts/release-notes.sh origin/main

set -eu

if [ $# -lt 1 ] || [ $# -gt 2 ]; then
    printf 'usage: release-notes.sh <ref> [<previous-tag>]\n' >&2
    exit 2
fi

ref=$1
git rev-parse --verify --quiet "$ref^{commit}" >/dev/null \
    || { printf 'release-notes.sh: unknown ref %s\n' "$ref" >&2; exit 1; }

if git rev-parse --verify --quiet "refs/tags/$ref" >/dev/null; then
    heading=$ref
    start=$ref^
else
    heading=Unreleased
    start=$ref
fi

if [ -n "${2:-}" ]; then
    prev=$2
    git rev-parse --verify --quiet "$prev^{commit}" >/dev/null \
        || { printf 'release-notes.sh: unknown previous tag %s\n' "$prev" >&2; exit 1; }
else
    prev=$(git describe --tags --abbrev=0 --match 'v*' "$start" 2>/dev/null) || prev=
fi

if [ -n "$prev" ]; then
    range="$prev..$ref"
else
    range=$ref
fi

printf '## %s\n\n' "$heading"
if [ -n "$prev" ]; then
    # An explicit previous tag that is the end point itself: nothing lies in
    # between, and "since" would mislead.
    if [ -z "${2:-}" ] || [ "$(git rev-parse "$prev^{commit}")" != "$(git rev-parse "$ref^{commit}")" ]; then
        printf 'Changes since %s\n' "$prev"
    fi
else
    printf 'First release: the whole history.\n'
fi

# One record per first-parent merge: a mark line carrying 1 when the merge
# brought in docs/ files only (0 otherwise, an empty diff included), then the
# merge's subject and body.
git log --first-parent --merges --format=%H "$range" | while read -r merge; do
    changed=$(git diff --name-only "$merge^1" "$merge")
    docs=0
    if [ -n "$changed" ] && ! printf '%s\n' "$changed" | grep -qv '^docs/'; then
        docs=1
    fi
    printf '\036%s\n' "$docs"
    git log -1 --format='%s%n%b' "$merge"
done | awk '
# readable(b): "feat/065-global-search" -> "feat-065: global search".
function readable(b,    p, rest, num, slug) {
    if (b !~ /^[a-z]+\/[0-9]+-[^\/]+$/) return b
    p = b; sub(/\/.*$/, "", p)
    rest = b; sub(/^[^\/]+\//, "", rest)
    num = rest; sub(/-.*$/, "", num)
    slug = rest; sub(/^[0-9]+-/, "", slug); gsub(/-/, " ", slug)
    return p "-" num ": " slug
}
function flush(    n, b, p, g) {
    if (!have) return
    have = 0
    if (subj ~ /^Merge pull request #[0-9]+ from [^\/ ]+\/[^ ]+$/) {
        n = subj; sub(/^Merge pull request #/, "", n); sub(/ from .*$/, "", n)
        b = subj; sub(/^Merge pull request #[0-9]+ from [^\/]+\//, "", b)
        p = b; sub(/\/.*$/, "", p)
        if (p == "sync") return
        if (title == "") title = readable(b)
    } else if (match(subj, / \(#[0-9]+\)$/)) {
        if (subj ~ /^(Queue|Take|Withdraw) /) return
        n = substr(subj, RSTART + 3, RLENGTH - 4)
        title = substr(subj, 1, RSTART - 1)
        p = ""
        if (match(title, /[a-z]+-[0-9]+/)) { p = substr(title, RSTART, RLENGTH); sub(/-.*$/, "", p) }
    } else return
    g = "Other"
    if (docs) g = "Documentation"
    else if (p == "feat") g = "Features"
    else if (p == "bug" || p == "fix" || p == "hot") g = "Fixes"
    else if (p == "mod") g = "Modules"
    else if (p == "tech") g = "Technical"
    else if (p == "doc") g = "Documentation"
    line[g] = line[g] "- " title " (#" n ")\n"
    count++
}
BEGIN { mark = sprintf("%c", 30) }
{ sub(/\r$/, "") }
index($0, mark) == 1 { flush(); docs = (substr($0, 2) == "1"); have = 1; insubj = 1; subj = ""; title = ""; next }
insubj { subj = $0; insubj = 0; next }
{
    if (title == "" && $0 !~ /^[ \t]*$/) { title = $0; sub(/^[ \t]+/, "", title); sub(/[ \t]+$/, "", title) }
}
END {
    flush()
    if (count == 0) { printf "\nNo pull requests in this range.\n"; exit }
    k = split("Features Fixes Modules Technical Documentation Other", order, " ")
    for (i = 1; i <= k; i++) {
        g = order[i]
        if (line[g] != "") printf "\n### %s\n\n%s", g, line[g]
    }
}'
