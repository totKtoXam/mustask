#!/usr/bin/env bash
# Sets up the self-hosted GitHub Actions runner that deploys MUSTASK on this
# VM (docs/deploy/vm.md, section 11). Run as root on the VM, once; a re-run
# refreshes the wrapper, the job guard, the sudoers file and the smoke flags,
# installs the service if an earlier run stopped before it, and leaves the
# registration as it is.
#
# Usage:
#   runner-setup.sh --url https://github.com/<owner>/<repo>
#                   --runner-version <x.y.z> --runner-sha256 <hex>
#                   [--name <runner name>] [--smoke-args "<flags>"]
#
# --runner-version and --runner-sha256 come from the release page of
# github.com/actions/runner (the linux-x64 line). The download is refused
# unless it matches the checksum.
# --name defaults to mustask-prod-<hostname>.
# --smoke-args are the flags `mustask-cd smoke` gives smoke.sh: empty by
# default, or --insecure (a certificate the VM does not trust) and at most one
# of --resolve-local (the VM cannot reach its own public name) and
# --resolve-to=<IPv4> (TLS and the http redirect are a proxy's in front of the
# VM, and on the VM the name points at the VM itself). Kept in
# /etc/mustask/cd-smoke-args; a re-run without the flag keeps the recorded ones.
#
# The registration token (Settings > Actions > Runners > New self-hosted
# runner, valid for an hour) is read from a prompt when the runner is not
# registered yet. It never goes on a command line and is never written down.
#
# What it does:
#   - the system user mustask-runner (no password, no login shell, no sudo
#     beyond the one rule below), home /var/lib/mustask-runner;
#   - /usr/local/sbin/mustask-cd from mustask-cd.sh next to this script;
#   - the job guard: runner-job-guard.sh as
#     /usr/local/libexec/mustask-runner/job-started.sh, the repository in
#     /etc/mustask/cd-repository, and a root-owned systemd drop-in of the
#     runner service that sets ACTIONS_RUNNER_HOOK_JOB_STARTED to it. Every job
#     but deploy.yml of that repository, from main or a v* tag, fails before
#     its first step (section 11.4);
#   - /etc/sudoers.d/mustask-runner: mustask-runner may run exactly
#     `mustask-cd deploy` and `mustask-cd smoke` as root, checked by visudo;
#   - the runner under /var/lib/mustask-runner/actions-runner, registered with
#     the label mustask-prod, installed as a systemd service run as
#     mustask-runner. A re-run restarts the service only when the drop-in
#     changed: do not re-run it while a deploy is running.

set -euo pipefail

user=mustask-runner
home=/var/lib/mustask-runner
dir=$home/actions-runner
wrapper=/usr/local/sbin/mustask-cd
sudoers=/etc/sudoers.d/mustask-runner
smoke_args_file=/etc/mustask/cd-smoke-args
guard=/usr/local/libexec/mustask-runner/job-started.sh
repository_file=/etc/mustask/cd-repository
label=mustask-prod
kit=$(cd "$(dirname "$0")" && pwd)

die() { printf 'runner-setup: %s\n' "$*" >&2; exit 1; }
say() { printf '== %s\n' "$*"; }
val() { [ $# -ge 2 ] && [ -n "$2" ] && [ "${2#--}" = "$2" ] || die "$1 needs a value"; }

[ "$(id -u)" = 0 ] || die "run as root (sudo bash $0 ...)"

url='' version='' sha='' name='' smoke_args='' smoke_given=0
while [ $# -gt 0 ]; do
    case $1 in
        --url) val "$@"; url=$2; shift 2 ;;
        --runner-version) val "$@"; version=$2; shift 2 ;;
        --runner-sha256) val "$@"; sha=$2; shift 2 ;;
        --name) val "$@"; name=$2; shift 2 ;;
        # The flags start with --, so val would refuse them: an empty value is
        # allowed, it clears the recorded flags.
        --smoke-args) [ $# -ge 2 ] || die "--smoke-args needs a value (\"\" for none)"
            smoke_args=$2; smoke_given=1; shift 2 ;;
        *) die "unknown argument '$1'" ;;
    esac
done

printf '%s' "$url" | grep -Eq '^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' \
    || die "--url must be https://github.com/<owner>/<repo> (got '$url')"
name=${name:-mustask-prod-$(hostname -s)}
printf '%s' "$name" | grep -Eq '^[A-Za-z0-9._-]+$' || die "--name '$name' is not a plain name"
# The same rule as smoke_args_of in mustask-cd.sh, which refuses the file at
# every smoke otherwise.
resolve=''
for w in $smoke_args; do
    case $w in
        --insecure) ;;
        --resolve-local|--resolve-to=*)
            if [ "$w" != --resolve-local ]; then
                printf '%s' "${w#--resolve-to=}" | grep -Eq '^([0-9]{1,3}\.){3}[0-9]{1,3}$' \
                    || die "--smoke-args: '$w' is not --resolve-to=<IPv4 address>"
            fi
            [ -z "$resolve" ] || [ "$resolve" = "$w" ] || die "--smoke-args: $resolve and $w: give one address"
            resolve=$w ;;
        *) die "--smoke-args: '$w' is not --insecure, --resolve-local or --resolve-to=<IPv4>" ;;
    esac
done
for f in mustask-cd.sh runner-job-guard.sh; do
    [ -f "$kit/$f" ] || die "no $f next to $0"
done
command -v visudo >/dev/null || die "no visudo: install sudo"
repository=${url#https://github.com/}

registered=0
[ -f "$dir/.runner" ] && registered=1
if [ "$registered" = 0 ]; then
    printf '%s' "$version" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$' \
        || die "--runner-version must be x.y.z (github.com/actions/runner/releases)"
    printf '%s' "$sha" | grep -Eq '^[0-9a-f]{64}$' \
        || die "--runner-sha256 must be the 64 hex digits of the linux-x64 line"
fi

if ! id "$user" >/dev/null 2>&1; then
    say "user $user"
    useradd --system --create-home --home-dir "$home" --shell /usr/sbin/nologin "$user"
fi
[ "$(id -u "$user")" != 0 ] || die "$user is root"
# Never a member of a group that is root by another name.
for g in sudo admin wheel docker lxd; do
    id -nG "$user" | tr ' ' '\n' | grep -qx "$g" && die "$user is in the group $g: remove it first"
done
chmod 0750 "$home"

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

# LF whatever the checkout did, as build-release.sh does for the kit.
say "$wrapper"
tr -d '\r' < "$kit/mustask-cd.sh" > "$tmp"
install -o root -g root -m 0755 "$tmp" "$wrapper"

say "$guard (jobs of $repository only)"
mkdir -p "$(dirname "$guard")" "$(dirname "$repository_file")"
tr -d '\r' < "$kit/runner-job-guard.sh" > "$tmp"
install -o root -g root -m 0755 "$tmp" "$guard"
printf '%s\n' "$repository" > "$tmp"
install -o root -g root -m 0644 "$tmp" "$repository_file"

say "$sudoers"
cat > "$tmp" <<EOF
# tech-066: the deploy runner may run exactly these two commands as root.
# Written by deploy/vm/runner-setup.sh; docs/deploy/vm.md, section 11.
Defaults:$user !requiretty, env_reset
$user ALL=(root) NOPASSWD: $wrapper deploy, $wrapper smoke
EOF
visudo -cf "$tmp" >/dev/null || die "the sudoers rule failed visudo -c; nothing installed"
install -o root -g root -m 0440 "$tmp" "$sudoers"

mkdir -p "$(dirname "$smoke_args_file")"
if [ "$smoke_given" = 1 ] || [ ! -f "$smoke_args_file" ]; then
    printf '%s\n' "$smoke_args" > "$smoke_args_file"
fi
chown root:root "$smoke_args_file"
chmod 0644 "$smoke_args_file"
say "smoke flags: '$(tr -d '\n' < "$smoke_args_file")' ($smoke_args_file)"

if [ "$registered" = 1 ]; then
    say "the runner in $dir is registered already, the registration is left as it is"
else
    command -v curl >/dev/null || die "no curl"
    say "runner $version"
    mkdir -p "$dir"
    chown "$user:" "$dir"
    archive=$home/actions-runner-linux-x64-$version.tar.gz
    curl -fsSL -o "$archive" \
        "https://github.com/actions/runner/releases/download/v$version/actions-runner-linux-x64-$version.tar.gz"
    printf '%s  %s\n' "$sha" "$archive" | sha256sum -c --quiet - \
        || { rm -f "$archive"; die "the runner download does not match --runner-sha256; nothing installed"; }
    runuser -u "$user" -- tar -xzf "$archive" -C "$dir"
    rm -f "$archive"
    # The runner's own list of system packages it needs (libicu and the like).
    "$dir/bin/installdependencies.sh" >/dev/null

    token=''
    if [ -t 0 ]; then
        read -rsp 'Registration token (not echoed): ' token
        echo
    else
        IFS= read -r token || true
    fi
    [ -n "$token" ] || die "no registration token"
    say "registering $name with the label $label"
    # ACTIONS_RUNNER_INPUT_TOKEN is how config.sh takes --token from the
    # environment: the token never shows in ps or in a shell history.
    (
        cd "$dir"
        ACTIONS_RUNNER_INPUT_TOKEN=$token runuser -u "$user" -- ./config.sh --unattended \
            --url "$url" --name "$name" --labels "$label" --work _work --replace
    )
    token=''
fi

# The service, also when an earlier run registered the runner and stopped
# before this point. svc.sh records the unit's name in .service.
if [ ! -f "$dir/.service" ]; then
    say "systemd service"
    (
        cd "$dir"
        ./svc.sh install "$user"
    )
fi
unit=$(head -n 1 "$dir/.service" | tr -d '\r')
printf '%s' "$unit" | grep -Eq '^actions\.runner\.[A-Za-z0-9._-]+\.service$' \
    || die "$dir/.service names '$unit', not an actions.runner.*.service unit"

# The hook is set from a drop-in outside the runner's directory: the runner
# user owns that directory, root owns this file.
dropin=/etc/systemd/system/$unit.d/mustask-job-guard.conf
printf '# tech-066: every job starts with the guard (docs/deploy/vm.md, section 11.4).\n[Service]\nEnvironment=ACTIONS_RUNNER_HOOK_JOB_STARTED=%s\n' \
    "$guard" > "$tmp"
changed=1
cmp -s "$tmp" "$dropin" && changed=0
mkdir -p "$(dirname "$dropin")"
install -o root -g root -m 0644 "$tmp" "$dropin"
systemctl daemon-reload
if [ "$changed" = 1 ] || ! systemctl is-active --quiet "$unit"; then
    say "restart $unit (the job guard is set)"
    systemctl restart "$unit"
else
    say "$unit is running with the job guard, left as it is"
fi
say "done: the runner is online with the labels self-hosted, Linux, X64, $label"
