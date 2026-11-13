#!/bin/sh
# Regression cases for scripts/release-notes.sh (tech-069).
#
# The cases build a throwaway repository: merge commits in the shape GitHub
# writes ("Merge pull request #N from owner/prefix/slug", the PR title as the
# body), a squash "Queue ..." commit, a merge from a sync/ branch, and two tags.
# They pin the grouping, the exclusion of queue PRs, the first-release wording,
# an explicit previous tag, the preview of an untagged ref and an empty range.
#
# Usage: sh scripts/tests/release-notes-cases.sh
# (verify.sh runs it when release-notes.sh or this file changes)

set -eu

scripts=$(cd "$(dirname "$0")/.." && pwd)
[ -f "$scripts/release-notes.sh" ] || { printf 'release-notes.sh not found next to %s\n' "$0" >&2; exit 1; }

sandbox=$(mktemp -d 2>/dev/null || { d="${TMPDIR:-/tmp}/release-notes-cases-$$"; mkdir -p "$d"; printf '%s' "$d"; })
trap 'rm -rf "$sandbox"' EXIT
trap 'exit 130' INT TERM

mkdir -p "$sandbox/home"
printf '[user]\n\tname = cases\n\temail = cases@example.invalid\n[commit]\n\tgpgsign = false\n[tag]\n\tgpgsign = false\n[init]\n\tdefaultBranch = main\n' \
    > "$sandbox/gitconfig"
export HOME="$sandbox/home" GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="$sandbox/gitconfig"
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE XDG_CONFIG_HOME \
    GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT

repo="$sandbox/repo"
mkdir -p "$repo"
g() { git -C "$repo" "$@"; }
notes() { (cd "$repo" && sh "$scripts/release-notes.sh" "$@"); }

passed=0
failed=0
ok() { passed=$((passed + 1)); printf 'ok   %s\n' "$1"; }
bad() { failed=$((failed + 1)); printf 'FAIL %s\n' "$1"; }

# has <name> <text> <needle>: a fixed line must be in the text.
has() {
    if printf '%s\n' "$2" | grep -Fqx -- "$3"; then ok "$1"; else bad "$1 (no line: $3)"; printf '%s\n' "$2" | sed 's/^/     | /'; fi
}
lacks() {
    if printf '%s\n' "$2" | grep -Fq -- "$3"; then bad "$1 (found: $3)"; printf '%s\n' "$2" | sed 's/^/     | /'; else ok "$1"; fi
}

n=0
# merge <branch> <number> <title>: a GitHub-style merge commit on main.
merge() {
    n=$((n + 1))
    g checkout -q -b "$1" main
    printf '%s\n' "$n" > "$repo/f$n"
    g add "f$n"
    g commit -q -m "work on $1"
    g checkout -q main
    g merge -q --no-ff -m "Merge pull request #$2 from owner/$1" -m "$3" "$1"
}

# merge_raw <branch> <number> <title> <into>: like merge, but the message is
# written with commit-tree -F, so git does not clean it up (a trailing CR
# survives), and both parents are explicit. <into> is the branch the PR lands
# on; a CR is put after the title.
merge_raw() {
    n=$((n + 1))
    g checkout -q -b "$1" "$4"
    printf '%s\n' "$n" > "$repo/f$n"
    g add "f$n"
    g commit -q -m "work on $1"
    g checkout -q "$4"
    printf 'Merge pull request #%s from owner/%s\r\n\r\n%s\r\n' "$2" "$1" "$3" > "$sandbox/msg"
    sha=$(g commit-tree -p "$(g rev-parse "$4")" -p "$(g rev-parse "$1")" -F "$sandbox/msg" "$(g rev-parse "$1^{tree}")")
    g reset -q --hard "$sha"
}

g init -q
printf 'seed\n' > "$repo/seed"
g add seed
g commit -q -m "seed"

merge feat/001-old 1 "Old feature before the first tag"
g tag v0.1.0

merge feat/002-search 2 "Search work items by text"
merge bug/003-crash 3 "Fix the crash on an empty board"
merge hot/004-urgent 4 "Hotfix for the login loop"
merge tech/005-ci 5 "Speed up the CI"
merge doc/006-analysis 6 "Analysis of the import"
merge mod/007-iterations 7 "Iterations module"
merge chore/008-misc 8 'Tidy $(echo pwned) `id` & more'
merge sync/queue-2026-01-01-add-x 9 "Queue: add x"
n=$((n + 1))
printf '%s\n' "$n" > "$repo/f$n"
g add "f$n"
g commit -q -m "Queue tech-099-x (#10)"
g tag v0.2.0

merge feat/011-next 11 "Next feature"
g tag v0.2.1

out=$(notes v0.2.0)
has "heading is the tag" "$out" "## v0.2.0"
has "previous tag found by itself" "$out" "Changes since v0.1.0"
has "features group" "$out" "### Features"
has "feature line" "$out" "- Search work items by text (#2)"
has "fixes group" "$out" "### Fixes"
has "bug goes to fixes" "$out" "- Fix the crash on an empty board (#3)"
has "hot goes to fixes" "$out" "- Hotfix for the login loop (#4)"
has "technical group" "$out" "### Technical"
has "tech line" "$out" "- Speed up the CI (#5)"
has "documentation group" "$out" "### Documentation"
has "doc line" "$out" "- Analysis of the import (#6)"
has "modules group" "$out" "### Modules"
has "mod line" "$out" "- Iterations module (#7)"
has "other group" "$out" "### Other"
has "title is data, not evaluated" "$out" '- Tidy $(echo pwned) `id` & more (#8)'
lacks "sync merge skipped" "$out" "Queue: add x"
lacks "squash queue commit skipped" "$out" "tech-099"
lacks "range starts after the previous tag" "$out" "Old feature"
lacks "next release not included" "$out" "Next feature"

# Group order: Features before Fixes before Modules before Technical.
order=$(printf '%s\n' "$out" | grep '^### ' | tr '\n' ' ')
if [ "$order" = "### Features ### Fixes ### Modules ### Technical ### Documentation ### Other " ]; then
    ok "groups in fixed order"
else
    bad "groups in fixed order (got: $order)"
fi

out=$(notes v0.2.1)
has "next release heading" "$out" "## v0.2.1"
has "next release previous" "$out" "Changes since v0.2.0"
has "next release line" "$out" "- Next feature (#11)"
lacks "empty groups omitted" "$out" "### Fixes"

out=$(notes v0.1.0)
has "first release wording" "$out" "First release: the whole history."
has "first release line" "$out" "- Old feature before the first tag (#1)"
lacks "first release has no 'since'" "$out" "Changes since"

out=$(notes v0.2.1 v0.1.0)
has "explicit previous tag" "$out" "Changes since v0.1.0"
has "explicit previous keeps the older PRs" "$out" "- Search work items by text (#2)"
has "explicit previous keeps the newer PRs" "$out" "- Next feature (#11)"

out=$(notes main)
has "untagged ref is Unreleased" "$out" "## Unreleased"
has "untagged ref starts at the latest tag" "$out" "Changes since v0.2.1"
has "empty range says so" "$out" "No pull requests in this range."

merge feat/012-after 12 "After the last tag"
out=$(notes main)
has "preview lists what is not tagged yet" "$out" "- After the last tag (#12)"
lacks "preview leaves tagged PRs out" "$out" "Next feature"

# Edge titles (QA): printf-special characters, an empty body, a CRLF body, a
# branch without a prefix.
merge feat/013-pct 13 'Pct 100% %s %n \n \\ done'
merge bug/014-empty 14 ''
merge_raw tech/015-crlf 15 "CRLF title" main
merge plainbranch 16 "No prefix title"
out=$(notes main)
has "percent and backslashes printed verbatim" "$out" '- Pct 100% %s %n \n \\ done (#13)'
has "empty body falls back to the branch name" "$out" "- bug/014-empty (#14)"
has "CRLF stripped from the title" "$out" "- CRLF title (#15)"
has "branch without a prefix goes to other" "$out" "- No prefix title (#16)"
if printf '%s\n' "$out" | grep -q "$(printf '\r')"; then bad "no carriage return in the notes"; else ok "no carriage return in the notes"; fi

# A PR merged into a side branch, which is then merged to main: only the
# merge to main belongs to the release (--first-parent).
g checkout -q -b feat/020-base main
printf 'base\n' > "$repo/base"
g add base
g commit -q -m "work on feat/020-base"
merge_raw feat/021-stacked 21 "Stacked PR into the side branch" feat/020-base
g checkout -q main
g merge -q --no-ff -m "Merge pull request #20 from owner/feat/020-base" -m "Base PR with a stack" feat/020-base

# Annotated tags: as the end point and as the previous tag.
g tag -a v0.3.0 -m "release 0.3.0"
merge feat/022-after-annotated 22 "After the annotated tag"
g tag -a v0.4.0 -m "release 0.4.0"

out=$(notes v0.3.0)
has "annotated tag as the end point: heading" "$out" "## v0.3.0"
has "annotated end point: previous found" "$out" "Changes since v0.2.1"
has "side-branch merge to main is listed" "$out" "- Base PR with a stack (#20)"
lacks "PR merged into a side branch is left out" "$out" "Stacked PR"
out=$(notes v0.4.0)
has "annotated tag as the previous tag (found by itself)" "$out" "Changes since v0.3.0"
has "annotated previous tag keeps the newer PR" "$out" "- After the annotated tag (#22)"
lacks "annotated previous tag leaves the older PRs" "$out" "Base PR"
out=$(notes v0.4.0 v0.3.0)
has "annotated previous tag given explicitly" "$out" "Changes since v0.3.0"
out=$(notes v0.4.0 v0.4.0)
has "previous tag equal to the end keeps the heading" "$out" "## v0.4.0"
has "previous tag equal to the end: no PRs" "$out" "No pull requests in this range."
lacks "previous tag equal to the end: no 'since'" "$out" "Changes since"

if (cd "$repo" && sh "$scripts/release-notes.sh" v9.9.9 >/dev/null 2>&1); then
    bad "unknown ref fails"
else
    ok "unknown ref fails"
fi

printf '\n%s passed, %s failed\n' "$passed" "$failed"
[ "$failed" = 0 ]
