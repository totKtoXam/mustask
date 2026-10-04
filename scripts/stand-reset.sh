#!/bin/sh
# Stand reset (mod-014, ADR-0025 § 7): wipes this stand's pair — Keycloak's
# persistent store and the stand's own `mustask` database — so the next start
# creates both from scratch: the realm from deploy/keycloak/realms, an empty
# database for the migrations.
#
# When you need it: the start's drift check (the keycloak resource stays
# unhealthy, the AppHost log names a difference from the realm import) after
# a realm-import edit, a switch to a branch whose import differs, or a changed
# Keycloak administrator password / mustask-panel secret / stand port you do
# not want to restore.
#
# What it wipes, decided by the same Mustask.AppHost code a start runs:
#   - container mode: this checkout's PostgreSQL volume and Keycloak volume;
#   - external-database mode (ConnectionStrings:mustask set, tech-002): the one
#     database that connection string names — dropped and created empty on
#     that server — and the Keycloak volume keyed to it. Every other database
#     on the server, and postgres/template0/template1 always, stay untouched.
#     Every checkout working on that database loses its stand data too.
#
# What it refuses: while any container (running or stopped) still uses one of
# the volumes — stop the AppHost first; while anyone is connected to the
# external database (PostgreSQL refuses a plain DROP DATABASE); without a
# terminal to confirm on, unless --yes is given.
#
# Next steps it prints: start the stand, apply the migrations
# (docs/runbooks/local-migrations.md), seed the Keycloak users
# (scripts/keycloak-seed.sh). The first reconciliation is the backfill.
#
#   scripts/stand-reset.sh          asks before wiping
#   scripts/stand-reset.sh --yes    wipes without asking
#
# Run it from the terminal that starts the stand: an exported
# ConnectionStrings__mustask switches it to external mode exactly as it
# switches the stand.
set -eu

cd "$(dirname "$0")/.."

exec dotnet run --project src/backend/Mustask.AppHost -- --stand-reset "$@"
