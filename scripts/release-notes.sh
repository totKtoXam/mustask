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
# The notes list the first-parent merge commits "Merge pull request #N from
# <owner>/<prefix>/<rest>" between the previous tag and the ref, one line per
# pull request: its title (the first line of the merge commit body) and (#N),
# grouped by the branch prefix. Queue pull requests are squash commits, not
# merges, so they drop out by themselves; a merge from a sync/ branch is
# skipped as well. Markdown goes to stdout.
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
    printf 'Changes since %s\n' "$prev"
else
    printf 'First release: the whole history.\n'
fi

git log --first-parent --merges --format='@@@%s%n%b' "$range" | awk '
function flush() {
    if (subj == "") return
    if (!match(subj, /^Merge pull request #[0-9]+ from [^\/ ]+\/[^ ]+$/)) { subj = ""; return }
    n = subj; sub(/^Merge pull request #/, "", n); sub(/ from .*$/, "", n)
    b = subj; sub(/^Merge pull request #[0-9]+ from [^\/]+\//, "", b)
    p = b; sub(/\/.*$/, "", p)
    if (p == "sync") { subj = ""; return }
    if (title == "") title = "Pull request #" n
    g = "Other"
    if (p == "feat") g = "Features"
    else if (p == "bug" || p == "fix" || p == "hot") g = "Fixes"
    else if (p == "mod") g = "Modules"
    else if (p == "tech") g = "Technical"
    else if (p == "doc") g = "Documentation"
    line[g] = line[g] "- " title " (#" n ")\n"
    count++
    subj = ""
}
/^@@@/ { flush(); subj = substr($0, 4); title = ""; next }
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
