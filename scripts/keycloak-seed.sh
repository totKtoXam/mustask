#!/usr/bin/env bash
# Keycloak seed for the stand: creates users and gives them client roles.
# The company-wide Keycloak realm lets MUSTASK own only clients and client
# roles (ADR-0031), so a user holds the client roles of `mustask-api`
# DIRECTLY — no groups, no realm roles. The realm and the four roles
# (`user`, `organization-creator`, `dictionaries-admin`, `system-admin`)
# arrive via import from deploy/keycloak/realms when the container is created;
# only the people are added here.
#
# What a token carries is the role names (resource_access.mustask-api.roles);
# Mustask.Api maps them to rights from a table in code (Mustask.Domain
# InstanceRoles). The administrator gets `user` (sign-in, the dictionaries'
# read) and `system-admin` (every instance right: the dictionaries, creating
# organizations); every other user gets `user` only. Without `user` the panel
# refuses the sign-in. Rights INSIDE an organization are not Keycloak's: they
# come from the user's membership in that organization, which an organization
# administrator grants on «Участники».
#
# Why users aren't in the realm-import: their passwords are secrets, and
# realm-import lives in the repository (AGENTS.md: secrets only through
# environment variables / user-secrets).
#
# Keycloak's store persists (mod-014, ADR-0025 § 7): `start-dev`'s database
# lives in a named Docker volume paired with the stand's database, so users and
# their ids survive stand restarts. Run this after the store is created — the
# first start, or after scripts/stand-reset.sh — not after every start. It is
# still safe to run repeatedly: for an existing user it fills in the profile,
# resets the password, and gives them the client roles again.
#
# Environment variables:
#   KEYCLOAK_URI              Keycloak address (default http://localhost:5010)
#   KEYCLOAK_REALM           the realm to seed (default mustask; the stand's Keycloak:Realm)
#   KEYCLOAK_ADMIN_PASSWORD   admin's password in the `master` realm — the
#                             same one set in AppHost's user-secrets under
#                             `Parameters:keycloak-admin-password` (required)
#
#   Required user — the instance administrator:
#   MUSTASK_USER, MUSTASK_PASSWORD    roles `user` and `system-admin`
#                                     (default name `mustask`)
#
#   Optional — created only if a password is given; needed to look at the
#   access model through eyes other than an administrator's:
#   MEMBER_PASSWORD      user `member` with the role `user` only — it reads
#                        the dictionaries and
#                        sees no organization until an organization
#                        administrator adds it on «Участники» (US-8)
#
# The administrator's token never appears on a command line: the password goes
# to Keycloak over stdin, and the access token it returns is kept in a private
# temporary file that curl reads as its configuration (curl -K), so neither is
# visible in the process list. The file is removed when the script ends.
#
# Passwords must satisfy the realm's policy: minimum 12 characters, both
# letter cases, and a digit.
set -u

KEYCLOAK_URI="${KEYCLOAK_URI:-http://localhost:5010}"
MUSTASK_USER="${MUSTASK_USER:-mustask}"
KEYCLOAK_REALM="${KEYCLOAK_REALM:-mustask}"

die() {
    printf '\n%s\n\n' "$1" >&2
    exit 1
}

[ -n "${KEYCLOAK_ADMIN_PASSWORD:-}" ] \
    || die "KEYCLOAK_ADMIN_PASSWORD is not set — this is the password of the master realm's admin. Get it from AppHost's user-secrets: dotnet user-secrets list --project src/backend/Mustask.AppHost"
[ -n "${MUSTASK_PASSWORD:-}" ] \
    || die "MUSTASK_PASSWORD is not set — password of the MUSTASK administrator (>= 12 characters, both letter cases and a digit)."

printf 'Waiting for Keycloak (%s) ' "$KEYCLOAK_URI"
attempt=0
until curl -fsS -o /dev/null "$KEYCLOAK_URI/realms/$KEYCLOAK_REALM"; do
    attempt=$((attempt + 1))
    [ "$attempt" -lt 60 ] || die "Keycloak did not respond at $KEYCLOAK_URI/realms/$KEYCLOAK_REALM after 60 attempts."
    printf '.'
    sleep 2
done
printf '— ready\n'

# The password goes over stdin, not as an argument: command-line arguments
# are visible in the process list to any user on the machine.
token=$(
    printf 'client_id=admin-cli&username=admin&grant_type=password&password=%s' \
        "$KEYCLOAK_ADMIN_PASSWORD" \
    | curl -fsS -X POST "$KEYCLOAK_URI/realms/master/protocol/openid-connect/token" \
        --data-binary @- \
    | tr ',' '\n' | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
)

[ -n "$token" ] || die "Could not get a Keycloak admin token — check KEYCLOAK_ADMIN_PASSWORD. The store keeps the administrator password it was created with: if Parameters:keycloak-admin-password was changed since, restore the old value or reset the stand (scripts/stand-reset.sh)."

# Private files (mktemp makes them 0600; umask keeps anything else the same). The
# token is written with the `printf` builtin and a redirection — not an argument of
# any process — as a curl configuration line, and curl reads it with -K.
umask 077
body_file=$(mktemp)
auth_file=$(mktemp)
trap 'rm -f "$body_file" "$auth_file"' EXIT
printf 'header = "Authorization: Bearer %s"\n' "$token" > "$auth_file"

# Three non-obvious things, all found out on a live stand:
#
# 1. The body goes over stdin (`--data-binary @-`), not as a command-line
#    argument: in Git Bash on Windows an argument with Cyrillic arrives at
#    curl.exe mangled, and Keycloak answers `Cannot parse the JSON`.
# 2. Content-Type must include `charset=UTF-8`. Without it Keycloak's
#    admin API decodes the body with some other encoding: the request goes
#    through with 204, but "Сотрудник" is stored as "РЎРѕС‚СЂСѓРґРЅРёРє".
#    Silent data corruption, noticeable only when read back.
#
# (There is no third item: with no groups, no Cyrillic path travels in a URL.)
api() {
    method=$1
    path=$2
    body=${3-}

    if [ -n "$body" ]; then
        printf '%s' "$body" | curl -sS -o "$body_file" -w '%{http_code}' -X "$method" \
            -K "$auth_file" \
            -H "Content-Type: application/json;charset=UTF-8" \
            --data-binary @- \
            "$KEYCLOAK_URI/admin/realms/$path"
    else
        curl -sS -o "$body_file" -w '%{http_code}' -X "$method" \
            -K "$auth_file" \
            "$KEYCLOAK_URI/admin/realms/$path"
    fi
}

# First "id" field from the response. Splitting on commas before searching
# is not cosmetic: `.*` in sed is greedy, and on the whole JSON string it
# would take the LAST occurrence.
first_id() {
    tr ',' '\n' < "$body_file" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1
}

# Gives the user the named client roles of mustask-api (comma-separated) —
# directly: the company-wide realm lets MUSTASK own only clients and client
# roles, so there are no groups and no realm roles to go through (ADR-0031).
# The roles arrive with the realm import; a role the realm does not have is
# an error naming it, not a silent skip.
grant_client_roles() {
    realm=$1
    user_id=$2
    role_names=$3

    api GET "$realm/clients?clientId=mustask-api" >/dev/null
    api_client_id=$(first_id)
    [ -n "$api_client_id" ] || { printf '   [!] no client mustask-api in realm %s — did the realm import not happen?\n' "$realm"; return 1; }

    mappings=""
    IFS=',' read -r -a wanted <<< "$role_names"
    for role_name in "${wanted[@]}"; do
        code=$(api GET "$realm/clients/$api_client_id/roles/$role_name")
        [ "$code" = "200" ] || { printf '   [!] no client role %s on mustask-api in realm %s: %s\n' "$role_name" "$realm" "$code"; return 1; }
        role_id=$(first_id)
        mappings="$mappings${mappings:+,}{\"id\":\"$role_id\",\"name\":\"$role_name\"}"
    done

    code=$(api POST "$realm/users/$user_id/role-mappings/clients/$api_client_id" "[$mappings]")
    case "$code" in
        204) printf '   client roles: %s\n' "$role_names" ;;
        *)   printf '   [!] client roles: %s %s\n' "$code" "$(cat "$body_file")"; return 1 ;;
    esac
}

# Creates a user (or finds an existing one), fills in the profile, sets the
# password, and gives them the client roles.
#
# About email/first name/last name: this isn't decoration, it's a login
# precondition. In Keycloak the user profile is declarative, and by default
# all three fields are required for the `user` role. A user without them is
# created and looks fine, but on login gets `invalid_grant: Account is not
# fully set up` — verified on the stand. The addresses are fake (the
# `.local` domain), the stand doesn't send mail anywhere.
seed_user() {
    realm=$1
    username=$2
    password=$3
    first=$4
    last=$5
    client_roles=$6

    printf '
== realm %s: user %s -> client roles %s
' "$realm" "$username" "$client_roles"

    profile="{\"username\":\"$username\",\"enabled\":true,\"emailVerified\":true,\"email\":\"$username@stand.local\",\"firstName\":\"$first\",\"lastName\":\"$last\"}"

    code=$(api POST "$realm/users" "$profile")
    case "$code" in
        201) printf '   created\n' ;;
        409) printf '   already exists — updating\n' ;;
        *)   printf '   [!] %s %s\n' "$code" "$(cat "$body_file")"; return 1 ;;
    esac

    id=$(api GET "$realm/users?username=$username&exact=true" >/dev/null; first_id)
    [ -n "$id" ] || { printf '   [!] could not find the user id\n'; return 1; }

    # The POST above didn't touch an existing user (409) — fill in the
    # profile here, otherwise an account created by an older version of the
    # script stays without email/name and never lets the user log in.
    code=$(api PUT "$realm/users/$id" "$profile")
    case "$code" in
        204) printf '   profile filled in\n' ;;
        *)   printf '   [!] profile: %s %s\n' "$code" "$(cat "$body_file")"; return 1 ;;
    esac

    # The password goes inside JSON, so quotes and backslashes in it must be
    # escaped — otherwise a password like `a"b` produces a broken body and
    # an unhelpful 400 instead of a clear error.
    escaped_password=$(printf '%s' "$password" | sed 's/\\/\\\\/g; s/"/\\"/g')

    code=$(api PUT "$realm/users/$id/reset-password" \
        "{\"type\":\"password\",\"value\":\"$escaped_password\",\"temporary\":false}")
    case "$code" in
        204) printf '   password set\n' ;;
        *)   printf '   [!] password: %s %s\n' "$code" "$(cat "$body_file")"; return 1 ;;
    esac

    # A user with no group holds the realm's default roles and nothing else.
    # The roles are the user's own: directly, no group (ADR-0031). Without `user` the account cannot sign in to the panel.
    grant_client_roles "$realm" "$id" "$client_roles" || return 1
}

failed=0

seed_user "$KEYCLOAK_REALM" "$MUSTASK_USER" "$MUSTASK_PASSWORD" "Администратор" "Mustask" "user,system-admin" || failed=1

if [ -n "${MEMBER_PASSWORD:-}" ]; then
    seed_user "$KEYCLOAK_REALM" member "$MEMBER_PASSWORD" "Участник" "Mustask" "user" || failed=1
fi

if [ "$failed" -eq 0 ]; then
    printf '\nDone. Keycloak console: %s/admin (admin / KEYCLOAK_ADMIN_PASSWORD)\n' "$KEYCLOAK_URI"
else
    printf '\nFinished with errors — see [!] above.\n' >&2
    exit 1
fi
