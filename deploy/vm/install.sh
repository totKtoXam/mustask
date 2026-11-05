#!/usr/bin/env bash
# Installs and updates MUSTASK on one VM (docs/deploy/vm.md). Run as root on
# the VM, from the unpacked release that deploy/vm/build-release.sh made.
#
# Usage:
#   install.sh prepare --host <host> --keycloak-host <host> --realm <realm>
#                      [--install-packages] [--create-db]
#   install.sh nginx   --host <host> --cert <fullchain.pem> --key <privkey.pem>
#   install.sh deploy  <release-dir>
#   install.sh rollback
#   install.sh status
#
# prepare   once per VM: the service user, the directories, /etc/mustask/
#           mustask.env from the template (kept when it exists). With
#           --install-packages it runs apt-get for the ASP.NET Core 10
#           runtime, PostgreSQL and nginx; with --create-db it creates the
#           role and the database `mustask` on the local PostgreSQL under a
#           generated password, written straight into mustask.env and never
#           printed. Both are explicit because both change the machine.
# nginx     renders the site from nginx-mustask.conf, enables it, nginx -t,
#           reload. The certificate is yours: point at its files.
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
env_value() { sed -n "s/^$1=//p" "$env_file" | tail -n 1; }
# Settings still holding the template placeholder (comments do not count).
placeholders() { grep -nE '^[^#]*CHANGE_ME' "$env_file"; }

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
    local host='' kc_host='' realm='' packages=0 create_db=0
    while [ $# -gt 0 ]; do
        case $1 in
            --host) host=$2; shift 2 ;;
            --keycloak-host) kc_host=$2; shift 2 ;;
            --realm) realm=$2; shift 2 ;;
            --install-packages) packages=1; shift ;;
            --create-db) create_db=1; shift ;;
            *) die "prepare: unknown argument '$1'" ;;
        esac
    done
    [ -n "$host" ] && [ -n "$kc_host" ] && [ -n "$realm" ] \
        || die "prepare: --host, --keycloak-host and --realm are required"

    if [ "$packages" = 1 ]; then
        say "apt-get install aspnetcore-runtime-10.0 postgresql nginx"
        apt-get update
        DEBIAN_FRONTEND=noninteractive apt-get install -y \
            aspnetcore-runtime-10.0 postgresql nginx curl
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
        sed -e "s|@HOST@|$host|g" -e "s|@KEYCLOAK_HOST@|$kc_host|g" -e "s|@REALM@|$realm|g" \
            "$kit/mustask.env.template" > "$env_file"
    fi
    chown root:mustask "$env_file"
    chmod 0640 "$env_file"

    if [ "$create_db" = 1 ]; then
        command -v psql >/dev/null || die "no psql: install postgresql (or pass --install-packages)"
        systemctl enable --now postgresql >/dev/null 2>&1 || true
        local has_role
        has_role=$(runuser -u postgres -- psql -tAc "SELECT 1 FROM pg_roles WHERE rolname = 'mustask'") \
            || die "cannot reach the local PostgreSQL as postgres (systemctl status postgresql)"
        if [ "$has_role" = 1 ]; then
            say "role mustask exists, database left as it is"
        else
            say "role and database mustask on the local PostgreSQL"
            local pw
            pw=$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')
            # Through stdin, not argv: the password never shows in ps.
            runuser -u postgres -- psql -v ON_ERROR_STOP=1 -q <<SQL
CREATE ROLE mustask LOGIN PASSWORD '$pw';
CREATE DATABASE mustask OWNER mustask;
SQL
            sed -i "s|^ConnectionStrings__mustask=.*|ConnectionStrings__mustask=Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=$pw|" "$env_file"
            say "the password went into $env_file"
        fi
    fi

    if placeholders >/dev/null; then
        printf '\nStill to fill in %s (edit it on the VM, never in chat):\n' "$env_file"
        placeholders | sed 's/=.*/=…/'
    fi
}

cmd_nginx() {
    local host='' cert='' key=''
    while [ $# -gt 0 ]; do
        case $1 in
            --host) host=$2; shift 2 ;;
            --cert) cert=$2; shift 2 ;;
            --key) key=$2; shift 2 ;;
            *) die "nginx: unknown argument '$1'" ;;
        esac
    done
    [ -n "$host" ] && [ -n "$cert" ] && [ -n "$key" ] \
        || die "nginx: --host, --cert and --key are required"
    [ -f "$cert" ] || die "no certificate file $cert"
    [ -f "$key" ] || die "no key file $key"

    say "/etc/nginx/sites-available/mustask"
    sed -e "s|@HOST@|$host|g" -e "s|@CERT@|$cert|g" -e "s|@KEY@|$key|g" \
        "$kit/nginx-mustask.conf" > /etc/nginx/sites-available/mustask
    ln -sfn /etc/nginx/sites-available/mustask /etc/nginx/sites-enabled/mustask
    nginx -t
    systemctl enable --now nginx >/dev/null 2>&1
    systemctl reload nginx
}

cmd_deploy() {
    local src=${1:-}
    [ -n "$src" ] && [ -f "$src/VERSION" ] || die "deploy: give the unpacked release directory (with VERSION)"
    src=$(cd "$src" && pwd)
    [ -f "$env_file" ] || die "no $env_file: run 'install.sh prepare' first"
    if placeholders >/dev/null; then
        die "$env_file still has CHANGE_ME on line(s) $(placeholders | cut -d: -f1 | paste -sd, -)"
    fi

    local tag target
    tag=$(sed -n 's/^tag=//p' "$src/VERSION")
    [ -n "$tag" ] || die "VERSION has no tag"
    target=$releases/$tag
    say "release $tag ($(sed -n 's/^commit=//p' "$src/VERSION"))"

    local current=''
    [ -L "$root/current" ] && current=$(readlink -f "$root/current")

    if [ "$src" != "$target" ]; then
        rm -rf "$target.new"
        cp -R "$src" "$target.new"
        rm -rf "$target"
        mv "$target.new" "$target"
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

    if [ -n "$current" ] && [ "$current" != "$target" ]; then
        ln -sfn "$current" "$root/previous"
    fi
    ln -sfn "$target" "$root/current.new"
    mv -T "$root/current.new" "$root/current"

    say "restart"
    systemctl restart mustask-api
    if wait_health; then
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
    *) sed -n '2,30p' "$0"; exit 2 ;;
esac
