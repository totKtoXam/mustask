#!/bin/sh
# Live-realm migration (mod-016, BR-M1-28, D-6, D-16; ADR-0027): moves the realm
# `mustask` of a stand whose Keycloak store was created BEFORE mod-016 to the realm
# the new import (deploy/keycloak/realms) declares — in place, with no reset. The
# store, the stand's database, every user and their id stay.
#
# What it does, in this order (every grant before any removal):
#   1. creates the realm role `instance-admin` (the three instance codes);
#   2. maps it onto /Администраторы and onto every other group, at any depth, that
#      maps `admin`;
#   3. maps it onto every user who holds `admin` directly;
#   4. adds `dictionaries.languages.read` to the realm's default roles, so that
#      every user reads the dictionaries;
#   5. only then deletes the realm roles `admin`, `member`, `viewer` and the groups
#      /Сотрудники/Участники and /Сотрудники (their users stay, in no group).
# It touches nothing else: no user is created or deleted; ids, credentials,
# attributes, every other group membership and role mapping, clients and their
# secrets stay as they are.
#
# It STOPS before any change — exit code 2, the report names what it found — when the
# structure is not the one it knows: a subgroup of /Сотрудники other than /Участники,
# a role mapped on either group other than `viewer` / `member`, or a realm role that
# has `admin` as a composite (it will not decide who then holds `instance-admin`).
# It is safe to repeat: a second run says "Nothing to change", and a run that was
# interrupted is completed by running it again (exit code 1 says so).
#
# When to run it: after merging mod-016, with the stand started and its `keycloak`
# resource not ready because the drift check reports the old realm roles `admin`,
# `member`, `viewer`, the new `instance-admin`, the composites of
# `default-roles-mustask` and the mappings of /Администраторы. Those findings are
# cleared by THIS command — NOT by scripts/stand-reset.sh, which the check's text names
# for them and which would delete the stand's data (src/backend/AGENTS.md, "Keycloak on
# the stand"). Run it on a task stand first; this script never starts a stand itself.
#
# Environment variables:
#   KEYCLOAK_ADMIN_PASSWORD   admin's password in the `master` realm — the same one
#                             set in AppHost's user-secrets under
#                             `Parameters:keycloak-admin-password` (required)
#   KEYCLOAK_URI              Keycloak address (default http://localhost:5010)
#
# The password is read from the environment and sent once in a form body; it is never
# an argument, so it does not show in the process list. The report names users by
# username only and holds no secret.
set -eu

cd "$(dirname "$0")/.."

[ -n "${KEYCLOAK_ADMIN_PASSWORD:-}" ] || {
    printf '\nKEYCLOAK_ADMIN_PASSWORD is not set — this is the password of the master realm'"'"'s admin. Get it from AppHost'"'"'s user-secrets: dotnet user-secrets list --project src/backend/Mustask.AppHost\n\n' >&2
    exit 2
}

exec dotnet run --project src/backend/Mustask.AppHost -- --migrate-live-realm
