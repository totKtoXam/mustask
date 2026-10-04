#!/bin/sh
# Warm a fresh worktree: src/frontend/node_modules from a shared cache
# instead of a full `npm ci`.
#
# Usage:
#   scripts/warm-worktree.sh            # in any worktree of the repository
#
# Every agent worktree starts empty (docs/conventions/git.md, "What you need
# to know about worktrees"), and the first thing each one does is `npm ci` —
# two to three minutes per session, twenty sessions per L task. The lock
# file rarely changes between those sessions, so the install is the same
# every time. This script keeps one installed node_modules per
# package-lock.json content hash under a cache directory and COPIES it into
# the worktree (a copy, not a link: Vite and Vitest write their own caches
# inside node_modules, and two sessions sharing one directory would race on
# them). A copy is seconds; `npm ci` is minutes.
#
# Cache location: $MUSTASK_NODE_CACHE, else %LOCALAPPDATA%/mustask/node_modules
# on Windows, else ~/.cache/mustask/node_modules. Each entry is named by
# `git hash-object package-lock.json`, so a lock change is a new entry and
# old entries can be deleted at any time.
#
# The worktree's node_modules is its own after this: `npm ci` there works
# as before and does not touch the cache.

set -eu

die() {
    printf '\n%s\n\n' "$1" >&2
    exit 1
}

root=$(git rev-parse --show-toplevel 2>/dev/null) || die "Not a git repository."
fe="$root/src/frontend"
lock="$fe/package-lock.json"
[ -f "$lock" ] || die "No $lock — nothing to warm."

if [ -e "$fe/node_modules" ]; then
    printf 'node_modules already present in %s — nothing to do.\n' "$fe"
    exit 0
fi

hash=$(git -C "$root" hash-object "$lock")
if [ -n "${MUSTASK_NODE_CACHE:-}" ]; then
    cache_base=$MUSTASK_NODE_CACHE
elif [ -n "${LOCALAPPDATA:-}" ]; then
    cache_base="$LOCALAPPDATA/mustask/node_modules"
else
    cache_base="${XDG_CACHE_HOME:-$HOME/.cache}/mustask/node_modules"
fi
entry="$cache_base/$hash/node_modules"

# --- Copy: robocopy on Windows (multi-threaded, by far the fastest), cp -a elsewhere
copy_tree() {
    src=$1; dst=$2
    case "$(uname -s)" in
        MINGW*|MSYS*|CYGWIN*)
            wsrc=$(cygpath -w "$src"); wdst=$(cygpath -w "$dst")
            # robocopy exit codes below 8 mean success
            robocopy "$wsrc" "$wdst" //E //MT:16 //NFL //NDL //NJH //NJS //NP >/dev/null || [ $? -lt 8 ]
            ;;
        *)
            cp -a "$src" "$dst"
            ;;
    esac
}

if [ -d "$entry" ]; then
    printf 'Copying node_modules from the cache (%s)...\n' "$hash"
    copy_tree "$entry" "$fe/node_modules"
    printf 'Done.\n'
    exit 0
fi

printf 'No cache entry for this package-lock.json (%s): running npm ci once, then caching.\n' "$hash"
( cd "$fe" && npm ci )

# Another session may have filled the entry while npm ci ran; theirs is as good as ours.
if [ ! -d "$entry" ]; then
    mkdir -p "$cache_base/$hash"
    tmp="$cache_base/$hash/node_modules.tmp.$$"
    copy_tree "$fe/node_modules" "$tmp"
    mv "$tmp" "$entry" 2>/dev/null || rm -rf "$tmp"
fi
printf 'Cached at %s\n' "$entry"
