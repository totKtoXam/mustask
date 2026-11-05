#!/usr/bin/env bash
# Builds a MUSTASK release from a git tag into one tarball for the VM
# (docs/deploy/vm.md, section 4).
#
# Usage:
#   deploy/vm/build-release.sh <tag> <site-url> [out-dir]
#
#   deploy/vm/build-release.sh v0.1.0 https://mustask.devexpert.kz
#   deploy/vm/build-release.sh v0.1.0 https://portal.devexpert.kz/mustask
#
# <site-url> is the public address of the panel: https://<host> when MUSTASK
# owns the host name, https://<host>/<path> under a path of someone else's
# site (ADR-0037). The path is the path base: it goes to the panel build
# (VITE_BASE_PATH) and, via install.sh prepare --path, to Hosting__PathBase.
#
# The release is built from the tag, not from the working tree: the tag is
# checked out into a throwaway worktree, so local edits never reach the VM.
# The kit itself (this directory) is taken from the current checkout, since a
# tag older than the kit does not carry it.
#
# Output: <out-dir>/mustask-<tag>.tar.gz (default: out/ at the repository
# root, ignored by git) holding
#   api/        dotnet publish of Mustask.Api (framework-dependent)
#   panel/      the panel build, VITE_API_BASE_URL=<site-url> and the path base baked in
#   efbundle    EF Core migrations bundle, linux-x64, self-contained: the VM
#               needs no SDK and no checkout to migrate
#   kit/        the files of this directory (install.sh, smoke.sh, unit, ...)
#   VERSION     tag, commit, site URL, build time
#
# Needs on the build machine (any OS with bash): git, the .NET 10 SDK,
# dotnet-ef 10.x (`dotnet tool install --global dotnet-ef`), Node.js + npm.

set -euo pipefail

die() { printf 'build-release: %s\n' "$*" >&2; exit 1; }

[ $# -ge 2 ] || die "usage: deploy/vm/build-release.sh <tag> <site-url> [out-dir]"
tag=$1
site=${2%/}
out=${3:-}

case $site in
    https://*) ;;
    *) die "site-url must be https://<host>[/<path>] (got '$site'): the session cookie is Secure" ;;
esac
rest=${site#https://}
host=${rest%%/*}
base=''
case $rest in
    */*) base=/${rest#*/} ;;
esac
# No port: the nginx site listens on 443 and install.sh takes a host.
printf '%s' "$host" | grep -Eq '^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$' \
    || die "site-url must be https://<host name>[/<path>], no port (got '$site')"
# The path base: plain segments, no trailing slash (trimmed above), no '.' or
# '..' segments.
if [ -n "$base" ]; then
    printf '%s' "$base" | grep -Eq '^(/[A-Za-z0-9._-]+)+$' \
        || die "the path of site-url must be /segment[/segment] of letters, digits, . _ - (got '$base')"
    case $base/ in
        */./*|*/../*) die "the path of site-url must not hold '.' or '..' segments (got '$base')" ;;
    esac
fi
# The tag names the release directory on the VM and the tarball.
printf '%s' "$tag" | grep -Eq '^[A-Za-z0-9._-]+$' \
    || die "tag '$tag' must be one plain name (letters, digits, . _ -)"

kit=$(cd "$(dirname "$0")" && pwd)
repo=$(git -C "$kit" rev-parse --show-toplevel)
# The default lands in the repository's ignored /out/, wherever it runs from.
out=${out:-$repo/out}

git -C "$repo" rev-parse -q --verify "refs/tags/$tag" >/dev/null \
    || die "no tag '$tag' here; run 'git fetch --tags' first"
commit=$(git -C "$repo" rev-parse -q --verify "$tag^{commit}") \
    || die "tag '$tag' does not point at a commit"
kit_commit=$(git -C "$repo" rev-parse HEAD)
# The kit is copied from the working tree: say so when it is not HEAD's.
[ -z "$(git -C "$repo" status --porcelain -- deploy/vm)" ] || kit_commit="$kit_commit+dirty"

command -v dotnet >/dev/null || die "dotnet (the .NET 10 SDK) is not on PATH"
dotnet ef --version >/dev/null 2>&1 \
    || die "dotnet-ef is missing: dotnet tool install --global dotnet-ef"
command -v npm >/dev/null || die "npm is not on PATH"

mkdir -p "$out"
out=$(cd "$out" && pwd)
work=$(mktemp -d "${TMPDIR:-/tmp}/mustask-release.XXXXXX")
src="$work/src"
stage="$work/mustask-$tag"

cleanup() {
    git -C "$repo" worktree remove --force "$src" >/dev/null 2>&1 || true
    rm -rf "$work"
}
trap cleanup EXIT

printf '== %s = %s\n' "$tag" "$commit"
git -C "$repo" worktree add --detach "$src" "$commit" >/dev/null
mkdir -p "$stage"

printf '== API: dotnet publish\n'
dotnet publish "$src/src/backend/Mustask.Api" -c Release -o "$stage/api" --nologo

printf '== migrations bundle (linux-x64, self-contained)\n'
# The design-time factory refuses to build the context without a connection
# string; building the bundle never connects, so a placeholder with no
# password does. The real string is given on the VM when the bundle runs.
ConnectionStrings__mustask='Host=build.invalid;Database=mustask' \
dotnet ef migrations bundle \
    --project "$src/src/backend/Mustask.Infrastructure" \
    --startup-project "$src/src/backend/Mustask.Infrastructure" \
    --configuration Release \
    --target-runtime linux-x64 --self-contained \
    --output "$stage/efbundle" --force

printf '== panel: npm ci + build, VITE_API_BASE_URL=%s VITE_BASE_PATH=%s\n' "$site" "${base:-/}"
(
    cd "$src/src/frontend"
    npm ci --no-audit --no-fund
    VITE_BASE_PATH=${base:-/} VITE_API_BASE_URL=$site npm run build
)
cp -R "$src/src/frontend/dist" "$stage/panel"

mkdir -p "$stage/kit"
for f in install.sh smoke.sh mustask-api.service nginx-mustask.conf nginx-mustask-locations.conf mustask.env.template; do
    # LF on the VM whatever the build machine's checkout did (.gitattributes
    # says LF, but an old clone may predate it).
    tr -d '\r' < "$kit/$f" > "$stage/kit/$f"
done

cat > "$stage/VERSION" <<EOF
tag=$tag
commit=$commit
kit_commit=$kit_commit
site=$site
built=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF

tarball="$out/mustask-$tag.tar.gz"
tar -C "$work" -czf "$tarball" "mustask-$tag"
printf '\n== done: %s\n' "$tarball"
printf 'copy it to the VM, then: tar -xzf mustask-%s.tar.gz && sudo bash mustask-%s/kit/install.sh deploy mustask-%s\n' \
    "$tag" "$tag" "$tag"
