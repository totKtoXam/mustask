#!/usr/bin/env bash
# The one thing the self-hosted runner may run as root (docs/deploy/vm.md,
# section 11). runner-setup.sh installs it as /usr/local/sbin/mustask-cd,
# owned by root, and the sudoers file lets the runner user run exactly
#
#   mustask-cd deploy < mustask-<tag>.tar.gz
#   mustask-cd smoke
#
# and nothing else: no other argument, no other command.
#
# deploy  reads the release tarball that deploy/vm/build-release.sh made from
#         stdin (stdin, not a path: the sudoers rule then needs no wildcard),
#         unpacks it into a fresh root-owned directory after checking its
#         listing, and runs that release's kit/install.sh deploy on it.
# smoke   runs the live release's kit/smoke.sh with the flags recorded in
#         /etc/mustask/cd-smoke-args (written by runner-setup.sh; empty, or
#         --resolve-local and/or --insecure).
#
# The release's own install.sh runs as root, as it does by hand: whoever can
# hand this script a tarball can run code as root. That is why only the
# deploy job of deploy.yml, behind the production environment, reaches the
# runner (section 11, "Trust model").

set -euo pipefail

smoke_args_file=/etc/mustask/cd-smoke-args
current=/opt/mustask/current
# Where the tarball is unpacked. Not from the environment (sudo resets it
# anyway); scripts/tests/cd-runner-cases.sh sets it after sourcing.
stage_parent=/var/tmp

die() { printf 'mustask-cd: %s\n' "$*" >&2; exit 1; }

# The release directory a tarball listing (`tar -tzf`) unpacks into, or a
# refusal. Every member must sit under one top directory mustask-<tag>, whose
# tag is a plain name; nothing absolute and no `..` segment anywhere.
release_of_listing() {
    local listing=$1 top='' line first
    [ -n "$listing" ] || { echo "the tarball is empty" >&2; return 1; }
    while IFS= read -r line; do
        line=${line#./}
        [ -n "$line" ] || continue
        case $line in
            /*) echo "absolute member '$line'" >&2; return 1 ;;
        esac
        case /$line/ in
            */../*) echo "member with '..': '$line'" >&2; return 1 ;;
        esac
        first=${line%%/*}
        if [ -z "$top" ]; then
            top=$first
        elif [ "$first" != "$top" ]; then
            echo "more than one top directory ('$top', '$first')" >&2
            return 1
        fi
    done <<EOF
$listing
EOF
    case $top in
        mustask-*) ;;
        *) echo "the top directory '$top' is not mustask-<tag>" >&2; return 1 ;;
    esac
    printf '%s' "${top#mustask-}" | grep -Eq '^[A-Za-z0-9][A-Za-z0-9._-]*$' \
        || { echo "the tag of '$top' is not a plain name" >&2; return 1; }
    case $top in
        *..*) echo "the tag of '$top' holds '..'" >&2; return 1 ;;
    esac
    printf '%s\n' "$top"
}

# The smoke flags from the recorded file: only the two smoke.sh knows, each at
# most once. A missing file means none.
smoke_args_of() {
    local file=$1 word seen=' '
    [ -f "$file" ] || return 0
    # shellcheck disable=SC2013 # words not lines
    for word in $(tr -d '\r' < "$file"); do
        case $word in
            --resolve-local|--insecure) ;;
            *) echo "unknown smoke flag '$word' in $file" >&2; return 1 ;;
        esac
        case $seen in
            *" $word "*) continue ;;
        esac
        seen="$seen$word "
        printf '%s\n' "$word"
    done
}

cmd_deploy() {
    [ -t 0 ] && die "deploy reads the release tarball from stdin: mustask-cd deploy < mustask-<tag>.tar.gz"
    local stage listing top tag
    # Root-owned and 0700 from its creation: the runner user, who wrote the
    # tarball, cannot swap the unpacked files between the check and the run.
    stage=$(mktemp -d "$stage_parent/mustask-cd.XXXXXX")
    # shellcheck disable=SC2064 # the path is fixed now
    trap "rm -rf '$stage'" EXIT
    cat > "$stage/release.tar.gz"
    listing=$(tar -tzf "$stage/release.tar.gz") || die "not a gzip tarball"
    top=$(release_of_listing "$listing") || die "refused: the tarball is not one release"
    tag=${top#mustask-}
    mkdir "$stage/unpacked"
    tar -xzf "$stage/release.tar.gz" -C "$stage/unpacked" --no-same-owner
    rm -f "$stage/release.tar.gz"
    local release=$stage/unpacked/$top
    [ -d "$release" ] && [ ! -L "$release" ] || die "refused: $top is not a directory"
    [ "$(sed -n 's/^tag=//p' "$release/VERSION" 2>/dev/null)" = "$tag" ] \
        || die "refused: VERSION in $top does not say tag=$tag"
    [ -f "$release/kit/install.sh" ] && [ ! -L "$release/kit/install.sh" ] \
        || die "refused: $top has no kit/install.sh"
    bash "$release/kit/install.sh" deploy "$release"
}

cmd_smoke() {
    [ -f "$current/kit/smoke.sh" ] || die "no live release with kit/smoke.sh under $current"
    local args
    args=$(smoke_args_of "$smoke_args_file") || die "refused: $smoke_args_file"
    # shellcheck disable=SC2086 # the flags were checked word by word above
    bash "$current/kit/smoke.sh" $args
}

main() {
    [ "$(id -u)" = 0 ] || die "run as root (the runner calls it through sudo -n)"
    [ $# = 1 ] || die "usage: mustask-cd deploy < mustask-<tag>.tar.gz | mustask-cd smoke"
    case $1 in
        deploy) cmd_deploy ;;
        smoke) cmd_smoke ;;
        *) die "usage: mustask-cd deploy < mustask-<tag>.tar.gz | mustask-cd smoke" ;;
    esac
}

# Sourced by scripts/tests/cd-runner-cases.sh for the two checks above; run,
# it is the command.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main "$@"
fi
