#!/usr/bin/env bash
# Smoke check of a deployed MUSTASK (docs/deploy/vm.md, section 8). Run on
# the VM after `install.sh deploy`; reads the addresses from mustask.env.
#
# Usage:
#   smoke.sh [--insecure] [--resolve-local]
#
#   --insecure       accept a certificate the VM does not trust (rehearsals)
#   --resolve-local  send the site's host name to 127.0.0.1 (before DNS)
#
# Automated: everything in section 8 that needs no browser. The steps that
# need a person signing in are printed as a checklist at the end.

set -uo pipefail

env_file=${MUSTASK_ENV_FILE:-/etc/mustask/mustask.env}
insecure=0
resolve=0
for a in "$@"; do
    case $a in
        --insecure) insecure=1 ;;
        --resolve-local) resolve=1 ;;
        *) printf 'smoke: unknown argument %s\n' "$a" >&2; exit 2 ;;
    esac
done

[ -r "$env_file" ] || { printf 'smoke: cannot read %s (run with sudo)\n' "$env_file" >&2; exit 2; }
# Surrounding quotes are dropped, as systemd does.
value() { sed -n "s/^$1=//p" "$env_file" | tail -n 1 | sed 's/^"\(.*\)"$/\1/'; }
site=$(value Session__PanelUrl)
authority=$(value Keycloak__Authority)
host=${site#https://}

opts=(-sS --max-time 15)
[ "$insecure" = 1 ] && opts+=(-k)
[ "$resolve" = 1 ] && opts+=(--resolve "$host:443:127.0.0.1" --resolve "$host:80:127.0.0.1")

failed=0
check() {
    if [ "$2" = ok ]; then
        printf '  ok    %s\n' "$1"
    else
        printf '  FAIL  %s — %s\n' "$1" "$2"
        failed=1
    fi
}

printf 'MUSTASK smoke: %s\n' "$site"

body=$(curl -sS --max-time 15 http://127.0.0.1:5000/health 2>&1)
case $body in
    Healthy*) check "/health on the VM" ok ;;
    *) check "/health on the VM" "got '${body:0:120}' (database or realm: journalctl -u mustask-api)" ;;
esac

code=$(curl "${opts[@]}" -o /dev/null -w '%{http_code} %{redirect_url}' "http://$host/")
case $code in
    "301 https://$host/") check "http:// redirects to https://" ok ;;
    *) check "http:// redirects to https://" "got '$code'" ;;
esac

page=$(curl "${opts[@]}" "$site/" 2>&1)
case $page in
    *'id="root"'*) check "the panel is served at $site/" ok ;;
    *) check "the panel is served at $site/" "no panel index.html (${page:0:120})" ;;
esac

page=$(curl "${opts[@]}" "$site/some/client/route" 2>&1)
case $page in
    *'id="root"'*) check "a client route falls back to index.html" ok ;;
    *) check "a client route falls back to index.html" "no index.html (${page:0:120})" ;;
esac

code=$(curl "${opts[@]}" -o /dev/null -w '%{http_code}' "$site/api/dictionaries/languages")
[ "$code" = 401 ] && check "the API answers through the proxy (401 without a token)" ok \
    || check "the API answers through the proxy (401 without a token)" "got $code"

code=$(curl "${opts[@]}" -o /dev/null -w '%{http_code} %{redirect_url}' "$site/api/session/login")
case $code in
    "302 $authority/"*) check "sign-in redirects to the realm" ok ;;
    *) check "sign-in redirects to the realm" "got '${code:0:160}', expected 302 to $authority/…" ;;
esac

# Outside /api/ nginx serves the panel, so /health from outside is index.html.
if page=$(curl "${opts[@]}" "$site/health" 2>&1); then
    case $page in
        Healthy*|Unhealthy*|Degraded*) check "/health is not exposed through the proxy" "the API's /health answers from outside" ;;
        *) check "/health is not exposed through the proxy" ok ;;
    esac
else
    check "/health is not exposed through the proxy" "the site did not answer (${page:0:120})"
fi

cat <<EOF

By hand, in a browser (section 8):
  1. $site/ -> sign in -> the realm's login page -> back to the panel.
  2. A user with the mustask-api role 'user' lands in the panel; a user
     without it comes back with authError=no_access.
  3. Only-'user' opens the dictionaries and is refused creating an
     organization; the first administrator (all four roles) creates it.
  4. After their first sign-in, the creator finds a colleague in member search.
EOF

[ "$failed" = 0 ] && printf '\nautomated checks: passed\n' || printf '\nautomated checks: FAILED\n'
exit "$failed"
