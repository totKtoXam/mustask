#!/usr/bin/env bash
# Keycloak seed for the stand: creates users and puts them into groups.
# Users are NOT given rights and roles directly — those come from group
# membership: group -> realm role -> resource client permissions -> token.
# The realm, roles and groups themselves arrive via import from
# deploy/keycloak/realms when the container is created; only the people are
# added here.
#
# Why users aren't in the realm-import: their passwords are secrets, and
# realm-import lives in the repository (AGENTS.md: secrets only through
# environment variables / user-secrets).
#
# The Keycloak container comes up without a volume (`start-dev`, built-in
# H2), so users live only until the container is recreated. The script is
# meant to be run repeatedly: for an existing user it fills in the profile,
# resets the password, and re-adds them to the group.
#
# Environment variables:
#   KEYCLOAK_URI              Keycloak address (default http://localhost:5010)
#   KEYCLOAK_ADMIN_PASSWORD   admin's password in the `master` realm — the
#                             same one set in AppHost's user-secrets under
#                             `Parameters:keycloak-admin-password` (required)
#
#   Required user — the administrator:
#   MUSTASK_USER, MUSTASK_PASSWORD    realm mustask, group "Администраторы"
#                                     (default name `mustask`)
#
#   Optional — created only if a password is given; needed to look at the
#   access model through eyes other than an administrator's:
#   MEMBER_PASSWORD      realm mustask, user `member` in group
#                        "Сотрудники / Участники" — gets the member role
#                        from its own group and viewer from the parent
#
# Passwords must satisfy the realm's policy: minimum 12 characters, both
# letter cases, and a digit.
set -u

KEYCLOAK_URI="${KEYCLOAK_URI:-http://localhost:5010}"
MUSTASK_USER="${MUSTASK_USER:-mustask}"

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
until curl -fsS -o /dev/null "$KEYCLOAK_URI/realms/mustask"; do
    attempt=$((attempt + 1))
    [ "$attempt" -lt 60 ] || die "Keycloak did not respond at $KEYCLOAK_URI/realms/mustask after 60 attempts."
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

[ -n "$token" ] || die "Could not get a Keycloak admin token — check KEYCLOAK_ADMIN_PASSWORD."

body_file=$(mktemp)
trap 'rm -f "$body_file"' EXIT

# Three non-obvious things, all found out on a live stand:
#
# 1. The body goes over stdin (`--data-binary @-`), not as a command-line
#    argument: in Git Bash on Windows an argument with Cyrillic arrives at
#    curl.exe mangled, and Keycloak answers `Cannot parse the JSON`.
# 2. Content-Type must include `charset=UTF-8`. Without it Keycloak's
#    admin API decodes the body with some other encoding: the request goes
#    through with 204, but "Сотрудник" is stored as "РЎРѕС‚СЂСѓРґРЅРёРє".
#    Silent data corruption, noticeable only when read back.
# 3. Cyrillic in the URL (group paths) gets percent-encoded byte by byte
#    for the same reason — see urlencode below: only ASCII survives curl
#    as an argument.
api() {
    method=$1
    path=$2
    body=${3-}

    if [ -n "$body" ]; then
        printf '%s' "$body" | curl -sS -o "$body_file" -w '%{http_code}' -X "$method" \
            -H "Authorization: Bearer $token" \
            -H "Content-Type: application/json;charset=UTF-8" \
            --data-binary @- \
            "$KEYCLOAK_URI/admin/realms/$path"
    else
        curl -sS -o "$body_file" -w '%{http_code}' -X "$method" \
            -H "Authorization: Bearer $token" \
            "$KEYCLOAK_URI/admin/realms/$path"
    fi
}

# First "id" field from the response. Splitting on commas before searching
# is not cosmetic: `.*` in sed is greedy, and on the whole JSON string it
# would take the LAST occurrence.
first_id() {
    tr ',' '\n' < "$body_file" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1
}

# Percent-encoding per RFC 3986: unreserved bytes (letters, digits, `-._~`)
# stay as-is, everything else — including every byte of Cyrillic — goes out
# as `%XX`. Encoding "everything" isn't an option even though the standard
# allows it: Keycloak answers 400 on `%41dministrators` but 200 on
# `Administrators` and on encoded Cyrillic — verified on the stand.
urlencode() {
    printf '%s' "$1" | od -An -tx1 -v | tr -s ' \n' '\n' | grep . | while read -r hex; do
        case "$hex" in
            3[0-9]|4[1-9a-f]|5[0-9a]|6[1-9a-f]|7[0-9a]|2d|2e|5f|7e) printf "\\x$hex" ;;
            *) printf '%%%s' "$hex" ;;
        esac
    done
}

# Group path of the form "/Parent/Child" -> group id. Each segment is
# encoded separately, separators remain separators.
group_id_by_path() {
    realm=$1
    group_path=$2
    encoded=""
    IFS='/' read -r -a segments <<< "$group_path"
    for segment in "${segments[@]}"; do
        [ -n "$segment" ] || continue
        encoded="$encoded/$(urlencode "$segment")"
    done
    api GET "$realm/group-by-path$encoded" >/dev/null
    first_id
}

# Creates a user (or finds an existing one), fills in the profile, sets the
# password, and puts them in the group.
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
    group_path=$6

    printf '\n== realm %s: user %s -> group %s\n' "$realm" "$username" "$group_path"

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

    group_id=$(group_id_by_path "$realm" "$group_path")
    [ -n "$group_id" ] || { printf '   [!] no group %s in realm %s — did the realm import not happen?\n' "$group_path" "$realm"; return 1; }

    code=$(api PUT "$realm/users/$id/groups/$group_id" "{}")
    case "$code" in
        204) printf '   in the group\n' ;;
        *)   printf '   [!] group: %s %s\n' "$code" "$(cat "$body_file")"; return 1 ;;
    esac
}

failed=0

seed_user mustask "$MUSTASK_USER" "$MUSTASK_PASSWORD" "Администратор" "Mustask" "/Администраторы" || failed=1

if [ -n "${MEMBER_PASSWORD:-}" ]; then
    seed_user mustask member "$MEMBER_PASSWORD" "Участник" "Mustask" "/Сотрудники/Участники" || failed=1
fi

if [ "$failed" -eq 0 ]; then
    printf '\nDone. Keycloak console: %s/admin (admin / KEYCLOAK_ADMIN_PASSWORD)\n' "$KEYCLOAK_URI"
else
    printf '\nFinished with errors — see [!] above.\n' >&2
    exit 1
fi
