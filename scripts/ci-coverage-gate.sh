#!/bin/sh
# The coverage floors over the reports of CI's parallel jobs (tech-074).
#
# Usage:
#   scripts/ci-coverage-gate.sh backend <dir>
#     <dir> holds one folder per backend job, each the VERIFY_COVERAGE_OUT/backend
#     of a run of scripts/verify.sh: one folder per test project inside.
#   scripts/ci-coverage-gate.sh frontend <dir>
#     <dir> holds the Vitest blob reports of every shard (any depth).
#
# A part of the suite covers a part of the code, so verify.sh keeps a part's
# raw coverage instead of checking it, and this script checks the floors of
# scripts/coverage-floors.json on all of it at once: the backend reports are
# regrouped by test project (coverage-gate.mjs tells the integration tests by
# the project folder) and merged line by line there; the frontend blobs are
# merged by `vitest --merge-reports`, which rebuilds the whole suite's coverage.
# The workflow calls it only after every job of that side went green.

set -eu

mode=${1:-}
input=${2:-}
[ -n "$mode" ] && [ -n "$input" ] && [ -d "$input" ] || {
    sed -n '4,9p' "$0" | sed 's/^# \{0,1\}//' >&2
    exit 2
}

repo=$(cd "$(dirname "$0")/.." && pwd)
input=$(cd "$input" && pwd)
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT

native_path() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf "%s" "$1"; fi; }

case "$mode" in
    backend)
        # <input>/<job>/<project>/... -> <scratch>/<project>/<job>/...
        found=0
        for job in "$input"/*/; do
            [ -d "$job" ] || continue
            for project in "$job"*/; do
                [ -d "$project" ] || continue
                target="$scratch/$(basename "$project")/$(basename "$job")"
                mkdir -p "$target"
                cp -R "$project". "$target/"
                found=1
            done
        done
        [ "$found" = 1 ] || { echo "ci-coverage-gate: no backend reports under $input" >&2; exit 1; }
        node "$repo/scripts/coverage-gate.mjs" backend "$scratch"
        ;;
    frontend)
        blobs="$scratch/blobs"
        mkdir -p "$blobs"
        find "$input" -type f -name 'blob-*.json' -exec cp {} "$blobs/" \;
        [ -n "$(ls "$blobs")" ] || { echo "ci-coverage-gate: no Vitest blob reports under $input" >&2; exit 1; }
        report="$scratch/coverage"
        # The merged run replays the shards' results; the reporter only has to
        # print the summary, the coverage is what this step is for. --silent
        # drops the replayed console output of the tests: the shards' own logs
        # have it, and here it buried the floor's verdict.
        (cd "$repo/src/frontend" && npx vitest --merge-reports="$(native_path "$blobs")" \
            --coverage --coverage.reportsDirectory="$(native_path "$report")" --reporter=dot --silent)
        [ -f "$report/coverage-summary.json" ] || { echo "ci-coverage-gate: the merge wrote no coverage-summary.json" >&2; exit 1; }
        node "$repo/scripts/coverage-gate.mjs" frontend "$report/coverage-summary.json"
        ;;
    *)
        echo "ci-coverage-gate: unknown side '$mode' (backend | frontend)" >&2
        exit 2
        ;;
esac
