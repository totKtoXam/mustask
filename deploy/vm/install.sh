#!/usr/bin/env bash
# Installs and updates MUSTASK on one VM (docs/deploy/vm.md). Run as root on
# the VM, from the unpacked release that deploy/vm/build-release.sh made.
#
# Usage:
#   install.sh prepare --host <host> [--path /<path>]
#                      --keycloak-host <host> --realm <realm>
#                      [--install-packages] [--create-db]
#   install.sh nginx   --host <host> --cert <fullchain.pem> --key <privkey.pem>
#   install.sh nginx   --path /<path>
#   install.sh deploy  <release-dir>
#   install.sh rollback
#   install.sh status
#
# prepare   once per VM: the service user, the directories, /etc/mustask/
#           mustask.env from the template (kept when it exists). --path is
#           the path of the public address when MUSTASK lives under a path of
#           a shared host (ADR-0037): it goes into Session__PanelUrl and
#           Hosting__PathBase; leave it out at a host root. With
#           --install-packages it runs apt-get for the ASP.NET Core 10
#           runtime, PostgreSQL and nginx; with --create-db it creates the
#           role and the database `mustask` on the local PostgreSQL under a
#           generated password, written straight into mustask.env and never
#           printed. Both are explicit because both change the machine.
# nginx     --host: the whole site from nginx-mustask.conf, enabled, nginx -t,
#           reload. The certificate is yours: point at its files.
#           --path: MUSTASK lives under that path of a server that is not
#           ours. It renders nginx-mustask-locations.conf into
#           /etc/nginx/snippets/mustask-locations.conf, checks it with
#           nginx -t, and prints the one `include` line the owner of that
#           server adds. It does not touch the server itself.
# deploy    copies the release to /opt/mustask/releases/<tag>, applies the
#           migrations with the bundle (ADR-0002: explicit, before the start),
#           installs the unit, switches /opt/mustask/current, restarts and
#           waits for /health. The previous release stays for rollback.
# rollback  points /opt/mustask/current back at the previous release and
#           restarts. Migrations are NOT reverted.
# status    the active release and the service state.

set -euo pipefail

env_file=/etc/mustask/mustask.env
root=/opt/mustask
releases=$root/releases
state=/var/lib/mustask
kit=$(cd "$(dirname "$0")" && pwd)

die() { printf 'install: %s\n' "$*" >&2; exit 1; }
say() { printf '== %s\n' "$*"; }

[ "$(id -u)" = 0 ] || die "run as root (sudo bash $0 ...)"

# The value of one key of the env file. The file is in systemd's format, not
# shell: values hold ';' and must not be sourced.
env_value() { sed -n "s/^$1=//p" "$env_file" | tail -n 1 | sed 's/^"\(.*\)"$/\1/'; }
# Settings still holding the template placeholder (comments do not count).
placeholders() { grep -nE '^[^#]*CHANGE_ME' "$env_file"; }
# A host name with no port, path or characters sed would read.
host_name() { printf '%s' "$1" | grep -Eq '^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$'; }
# The path of the public address: empty (host root) or /segment[/segment], no
# trailing slash, no . or .. segments; safe in sed and in nginx.
base_path() {
    [ -z "$1" ] && return 0
    printf '%s' "$1" | grep -Eq '^(/[A-Za-z0-9._-]+)+$' || return 1
    case $1/ in */./*|*/../*) return 1 ;; esac
}
# A flag that takes a value must have one.
val() { [ $# -ge 2 ] && [ -n "$2" ] && [ "${2#--}" = "$2" ] || die "$1 needs a value"; }

wait_health() {
    local i
    for i in $(seq 1 30); do
        if curl -fs -o /dev/null --max-time 5 http://127.0.0.1:5000/health; then
            return 0
        fi
        sleep 2
    done
    return 1
}

cmd_prepare() {
    local host='' path='' kc_host='' realm='' packages=0 create_db=0
    while [ $# -gt 0 ]; do
        case $1 in
            --host) val "$@"; host=$2; shift 2 ;;
            --path) val "$@"; path=$2; shift 2 ;;
            --keycloak-host) val "$@"; kc_host=$2; shift 2 ;;
            --realm) val "$@"; realm=$2; shift 2 ;;
            --install-packages) packages=1; shift ;;
            --create-db) create_db=1; shift ;;
            *) die "prepare: unknown argument '$1'" ;;
        esac
    done
    [ -n "$host" ] && [ -n "$kc_host" ] && [ -n "$realm" ] \
        || die "prepare: --host, --keycloak-host and --realm are required"
    # They go into sed and into the env file: plain names only.
    host_name "$host" || die "--host '$host' is not a plain host name"
    base_path "$path" || die "--path '$path' must be /segment[/segment] of letters, digits, . _ - (no trailing slash)"
    printf '%s' "$kc_host" | grep -Eq '^[A-Za-z0-9.-]+(:[0-9]+)?(/[A-Za-z0-9._-]+)*$' \
        || die "--keycloak-host '$kc_host' is not host[:port][/path]"
    printf '%s' "$realm" | grep -Eq '^[A-Za-z0-9._-]+$' || die "--realm '$realm' is not a plain name"

    if [ "$packages" = 1 ]; then
        # Only what is missing, and never an upgrade: on a shared VM an
        # upgrade of PostgreSQL or nginx restarts what others rely on.
        local missing=''
        dpkg-query -W -f '${Status}' aspnetcore-runtime-10.0 2>/dev/null | grep -q 'ok installed' \
            || missing="$missing aspnetcore-runtime-10.0"
        command -v psql >/dev/null || missing="$missing postgresql"
        command -v nginx >/dev/null || missing="$missing nginx"
        command -v curl >/dev/null || missing="$missing curl"
        if [ -n "$missing" ]; then
            say "apt-get install --no-upgrade$missing"
            apt-get update
            # shellcheck disable=SC2086
            DEBIAN_FRONTEND=noninteractive apt-get install -y --no-upgrade $missing
        else
            say "packages already installed, nothing to do"
        fi
    fi
    command -v dotnet >/dev/null || die "no dotnet: install aspnetcore-runtime-10.0 (or pass --install-packages)"

    if ! id mustask >/dev/null 2>&1; then
        say "service user mustask"
        useradd --system --create-home --home-dir "$state" --shell /usr/sbin/nologin mustask
    fi
    mkdir -p "$releases" /etc/mustask
    chown mustask:mustask "$state"
    chmod 0750 "$state"

    if [ -f "$env_file" ]; then
        say "$env_file exists, left as it is"
    else
        say "$env_file from the template"
        sed -e "s|@HOST@|$host|g" -e "s|@BASE_PATH@|$path|g" \
            -e "s|@KEYCLOAK_HOST@|$kc_host|g" -e "s|@REALM@|$realm|g" \
            "$kit/mustask.env.template" > "$env_file"
    fi
    chown root:mustask "$env_file"
    chmod 0640 "$env_file"

    if [ "$create_db" = 1 ]; then
        command -v psql >/dev/null || die "no psql: install postgresql (or pass --install-packages)"
        systemctl enable --now postgresql >/dev/null 2>&1 || true
        local has_role has_db
        has_role=$(runuser -u postgres -- psql -tAc "SELECT 1 FROM pg_roles WHERE rolname = 'mustask'") \
            || die "cannot reach the local PostgreSQL as postgres (systemctl status postgresql)"
        has_db=$(runuser -u postgres -- psql -tAc "SELECT 1 FROM pg_database WHERE datname = 'mustask'")
        if [ "$has_role" = 1 ]; then
            # The password exists only in the env file: a role without it
            # there is one nobody can log in as any more.
            env_value ConnectionStrings__mustask | grep -q 'Password=CHANGE_ME' \
                && die "role mustask exists but $env_file has no password for it: put it in by hand, or DROP ROLE mustask and re-run"
            say "role mustask exists, left as it is"
        else
            say "role mustask on the local PostgreSQL"
            local pw
            pw=$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')
            # The env file first, so a failure below never loses the password.
            # Through the environment and stdin, never argv: it never shows
            # in ps. The temp file is 0600 from its creation; `cat >` keeps
            # the env file's owner and mode.
            install -m 0600 /dev/null "$env_file.tmp"
            trap 'rm -f "$env_file.tmp"' EXIT
            PW=$pw awk '/^ConnectionStrings__mustask=/ {
                    print "ConnectionStrings__mustask=Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=" ENVIRON["PW"]; next
                } { print }' "$env_file" > "$env_file.tmp"
            cat "$env_file.tmp" > "$env_file"
            rm -f "$env_file.tmp"
            # A failing statement is logged with its text by default: keep
            # the password out of the server log.
            runuser -u postgres -- psql -v ON_ERROR_STOP=1 -q <<SQL
SET log_min_error_statement = panic;
SET log_statement = 'none';
CREATE ROLE mustask LOGIN PASSWORD '$pw';
SQL
            say "the password went into $env_file"
        fi
        if [ "$has_db" = 1 ]; then
            say "database mustask exists, left as it is"
        else
            say "database mustask"
            runuser -u postgres -- psql -v ON_ERROR_STOP=1 -q -c 'CREATE DATABASE mustask OWNER mustask'
        fi
    fi

    if placeholders >/dev/null; then
        printf '\nStill to fill in %s (edit it on the VM, never in chat):\n' "$env_file"
        placeholders | sed 's/=.*/=…/'
    fi
}

# MUSTASK under a path of a server that is not ours: the locations go into a
# snippet file, and the owner of that server includes it. The server itself is
# never written or enabled here.
cmd_nginx_locations() {
    local path=$1
    base_path "$path" && [ -n "$path" ] \
        || die "--path '$path' must be /segment[/segment] of letters, digits, . _ - (no trailing slash)"
    command -v nginx >/dev/null || die "no nginx: install it (or run prepare --install-packages)"

    local snippet=/etc/nginx/snippets/mustask-locations.conf probe
    mkdir -p /etc/nginx/snippets
    say "$snippet"
    sed -e "s|@BASE_PATH@|$path|g" "$kit/nginx-mustask-locations.conf" > "$snippet.new"

    # Nothing includes the snippet yet, so a plain `nginx -t` would not read
    # it. Test it inside a throwaway server of its own.
    probe=$(mktemp "${TMPDIR:-/tmp}/mustask-nginx.XXXXXX")
    printf 'events {}\nhttp {\n    server {\n        listen 127.0.0.1:65000;\n        include %s.new;\n    }\n}\n' \
        "$snippet" > "$probe"
    if ! nginx -t -c "$probe"; then
        rm -f "$probe" "$snippet.new"
        die "the snippet failed nginx -t; the previous snippet (if any) is left as it was"
    fi
    rm -f "$probe"
    # Keep the previous snippet: once the portal includes it, the whole-config
    # test below reads the new one, and a failure must not leave it live.
    rm -f "$snippet.bak"
    [ -f "$snippet" ] && cp -p "$snippet" "$snippet.bak"
    mv "$snippet.new" "$snippet"
    # The server's own configuration must still pass as a whole.
    if ! nginx -t; then
        if [ -f "$snippet.bak" ]; then
            mv "$snippet.bak" "$snippet"
            die "nginx -t fails on the current configuration; the previous snippet is restored"
        fi
        rm -f "$snippet"
        die "nginx -t fails on the current configuration; the new snippet is removed"
    fi
    rm -f "$snippet.bak"

    if grep -rqF "include $snippet;" /etc/nginx 2>/dev/null; then
        systemctl reload nginx
        say "the snippet is already included; nginx reloaded"
    else
        printf '\nThe snippet is installed but not active. The owner of the server that serves\n'
        printf 'the public host name adds this line inside its server { ... } block, then\n'
        printf "runs 'nginx -t && systemctl reload nginx':\n\n    include %s;\n\n" "$snippet"
    fi
}

cmd_nginx() {
    local host='' cert='' key='' path=''
    while [ $# -gt 0 ]; do
        case $1 in
            --host) val "$@"; host=$2; shift 2 ;;
            --cert) val "$@"; cert=$2; shift 2 ;;
            --key) val "$@"; key=$2; shift 2 ;;
            --path) val "$@"; path=$2; shift 2 ;;
            *) die "nginx: unknown argument '$1'" ;;
        esac
    done
    if [ -n "$path" ]; then
        [ -z "$host$cert$key" ] || die "nginx: --path installs locations for a server that is not ours; it takes no --host, --cert or --key"
        cmd_nginx_locations "$path"
        return
    fi
    [ -n "$host" ] && [ -n "$cert" ] && [ -n "$key" ] \
        || die "nginx: give --host, --cert and --key (own host name) or --path (shared server)"
    host_name "$host" || die "--host '$host' is not a plain host name"
    [ -f "$cert" ] || die "no certificate file $cert"
    [ -f "$key" ] || die "no key file $key"
    # nginx resolves relative paths against its own prefix, not this cwd.
    cert=$(readlink -f "$cert")
    key=$(readlink -f "$key")
    case "$cert$key" in
        *[\|\&\;\ ]*) die "certificate and key paths must not hold | & ; or spaces" ;;
    esac

    local site=/etc/nginx/sites-available/mustask enabled=/etc/nginx/sites-enabled/mustask
    local was_enabled=0
    [ -L "$enabled" ] && was_enabled=1
    [ -f "$site" ] && cp -p "$site" "$site.bak"
    say "$site"
    sed -e "s|@HOST@|$host|g" -e "s|@CERT@|$cert|g" -e "s|@KEY@|$key|g" \
        "$kit/nginx-mustask.conf" > "$site"
    ln -sfn "$site" "$enabled"
    # A site that fails the test must not stay enabled: the next reload or
    # reboot would take down every other site of this nginx with it. A
    # working site from an earlier run is put back as it was.
    if ! nginx -t; then
        if [ -f "$site.bak" ]; then
            mv "$site.bak" "$site"
            [ "$was_enabled" = 1 ] || rm -f "$enabled"
            die "nginx -t failed; the previous site is restored"
        fi
        rm -f "$enabled"
        die "nginx -t failed; the site is written but not enabled"
    fi
    rm -f "$site.bak"
    systemctl enable --now nginx >/dev/null 2>&1 || die "cannot start nginx (systemctl status nginx)"
    systemctl reload nginx
}

cmd_deploy() {
    local src=${1:-}
    [ -n "$src" ] && [ -f "$src/VERSION" ] || die "deploy: give the unpacked release directory (with VERSION)"
    # -P: through symlinks, so `deploy /opt/mustask/current` is the release
    # itself and never a link copied over its own target.
    src=$(cd "$src" && pwd -P)
    [ -f "$env_file" ] || die "no $env_file: run 'install.sh prepare' first"
    if placeholders >/dev/null; then
        die "$env_file still has CHANGE_ME on line(s) $(placeholders | cut -d: -f1 | paste -sd, -)"
    fi

    local tag target
    tag=$(sed -n 's/^tag=//p' "$src/VERSION")
    [ -n "$tag" ] || die "VERSION has no tag"
    # It becomes a path that rm -rf and mv work on.
    case $tag in
        *..*|.*) die "bad tag '$tag' in VERSION" ;;
    esac
    printf '%s' "$tag" | grep -Eq '^[A-Za-z0-9._-]+$' || die "bad tag '$tag' in VERSION"
    target=$releases/$tag
    say "release $tag ($(sed -n 's/^commit=//p' "$src/VERSION"))"
    local site
    site=$(sed -n 's/^site=//p' "$src/VERSION")
    [ "$site" = "$(env_value Session__PanelUrl)" ] \
        || die "the release is built for $site, but Session__PanelUrl is $(env_value Session__PanelUrl): rebuild for the right address"

    local current=''
    [ -L "$root/current" ] && current=$(readlink -f "$root/current")

    if [ "$src" != "$target" ]; then
        rm -rf "$target.new" "$target.old"
        cp -R "$src" "$target.new"
        # Two renames, so a live release directory is gone only between them.
        [ -e "$target" ] && mv "$target" "$target.old"
        mv "$target.new" "$target"
        rm -rf "$target.old"
    fi
    chown -R root:root "$target"
    chmod -R u=rwX,go=rX "$target"
    chmod 0755 "$target/efbundle"

    say "migrations (bundle)"
    ConnectionStrings__mustask=$(env_value ConnectionStrings__mustask) "$target/efbundle"

    say "systemd unit"
    install -m 0644 "$target/kit/mustask-api.service" /etc/systemd/system/mustask-api.service
    systemctl daemon-reload
    systemctl enable mustask-api >/dev/null 2>&1

    # Only a release that once turned healthy becomes the rollback target:
    # deploying a fix over a failed release keeps the last good one.
    if [ -n "$current" ] && [ "$current" != "$target" ] && [ -f "$current/.healthy" ]; then
        ln -sfn "$current" "$root/previous"
    fi
    rm -f "$target/.healthy"
    ln -sfn "$target" "$root/current.new"
    mv -T "$root/current.new" "$root/current"

    say "restart"
    systemctl restart mustask-api
    if wait_health; then
        touch "$target/.healthy"
        say "healthy: $tag is live"
    else
        journalctl -u mustask-api -n 30 --no-pager || true
        die "/health did not turn healthy in 60 s (database or Keycloak, see the log above); 'install.sh rollback' returns the previous release"
    fi
}

cmd_rollback() {
    [ -L "$root/previous" ] || die "no previous release to roll back to"
    local prev cur
    prev=$(readlink -f "$root/previous")
    cur=$(readlink -f "$root/current")
    [ -f "$prev/VERSION" ] || die "the previous release $prev is gone"
    # After a failed release and a fix of the same tag the two can coincide.
    [ "$prev" != "$cur" ] || die "previous is the current release ($(basename "$cur")): nothing to roll back to"
    say "rollback: $(basename "$cur") -> $(basename "$prev"); migrations are not reverted"
    ln -sfn "$prev" "$root/current.new"
    mv -T "$root/current.new" "$root/current"
    ln -sfn "$cur" "$root/previous"
    systemctl restart mustask-api
    wait_health && say "healthy" || die "/health is not healthy after the rollback"
}

cmd_status() {
    if [ -L "$root/current" ]; then
        cat "$root/current/VERSION"
    else
        echo "no release deployed"
    fi
    [ -L "$root/previous" ] && echo "previous=$(basename "$(readlink -f "$root/previous")")"
    systemctl is-active mustask-api || true
}

command=${1:-}
[ $# -gt 0 ] && shift
case $command in
    prepare) cmd_prepare "$@" ;;
    nginx) cmd_nginx "$@" ;;
    deploy) cmd_deploy "$@" ;;
    rollback) cmd_rollback ;;
    status) cmd_status ;;
    *) sed -n '2,40p' "$0"; exit 2 ;;
esac
