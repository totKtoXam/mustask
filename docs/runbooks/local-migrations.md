# Runbook: EF migrations locally (Aspire, fixed ports)

Rule — `src/backend/AGENTS.md` and ADR-0002: migrations are applied
explicitly, not at `Mustask.Api` startup. This is the verified step-by-step
procedure; moved out of AGENTS.md (2026-08-23).

- Migrations are applied explicitly, not at `Mustask.Api` startup
  (see `docs/adr/0002-explicit-database-migrations.md`). The design-time
  factory (`MustaskDbContextFactory`) has no hardcoded fallback to
  `localhost:5432` — the `ConnectionStrings__mustask` variable is always
  required; without it the command fails with a clear error instead of
  quietly connecting to the wrong place:
  1. `dotnet run --project src/backend/Mustask.AppHost` — brings up an empty
     database in the stand's own PostgreSQL container, provided
     `ConnectionStrings:mustask` is set nowhere: neither as a user secret nor
     as `ConnectionStrings__mustask` in this terminal's environment. With
     either set, AppHost starts no container (see "External database" below);
     the `Database:` line it prints at startup says which mode it chose.
  2. Assemble the connection string. The port and password
     are fixed, not reissued on every run, so the string is stable:
     `Host=localhost;Port=5432;Database=mustask;Username=postgres;Password=<the
     one stored in AppHost's user-secrets under
     `Parameters:mustask-db-password`>`. The port is the `Ports:Db` value from
     `appsettings.json` of the `Mustask.AppHost` project (overridden by
     `Ports__Db`); the password — `dotnet user-secrets list --project
     src/backend/Mustask.AppHost`. **If the machine has its own PostgreSQL
     installed** — it has taken 5432, and this string leads to it, not to
     the stand: shift the stand's port first (`dotnet user-secrets set
     Ports:Db 5435`), or you risk applying migrations to your own server —
     or, if that server is exactly what the stand should use, switch to the
     external-database mode (the last section of this runbook).
     If something doesn't add up, you can check yourself against the
     actual run: Aspire dashboard → `api` resource → Environment variables
     tab → `ConnectionStrings__mustask`.
  3. Export `ConnectionStrings__mustask` with this value into the current
     session's/terminal's environment variables — **in a terminal other than
     the one that starts AppHost**, or unset it before starting AppHost again
     (`unset ConnectionStrings__mustask`; in PowerShell `Remove-Item
     Env:ConnectionStrings__mustask`). AppHost reads `ConnectionStrings:mustask`
     too: with the variable set it starts no PostgreSQL container and hands the
     API this string instead (see "External database" below). Then run:
     `dotnet ef database update --project src/backend/Mustask.Infrastructure`
     (without `--startup-project src/backend/Mustask.Api` — `Mustask.Api` does not
     reference `Microsoft.EntityFrameworkCore.Design`, and the command
     fails with that flag). New migration: `dotnet ef migrations add
     <Name> --project src/backend/Mustask.Infrastructure`.
     `ConnectionStrings__mustask` is needed **always** for `dotnet ef` —
     `MustaskDbContextFactory` throws before attempting a connection if the
     variable is not set (see above, "no hardcoded fallback"); but the value
     can be any syntactically valid string, even a deliberately broken one
     (e.g. with an unreachable port) — `migrations add` never touches the real
     database. It is for the `dotnet ef` terminal only: left exported in the
     terminal that starts AppHost, even a deliberately broken value switches
     the stand to the external-database mode, with no container and an API
     aimed at a port where nothing listens.
  All of this was verified manually (2026-08-22, 2026-08-23): the
  container came up, the port and password were extracted, `dotnet ef
  database update` applied `InitialCreate`, a repeat run reported "already
  up to date"; `dotnet ef migrations add` was verified separately with
  both a working and a deliberately broken connection string. Re-verified
  2026-09-07 with the port and password fixed; the DB port is 5432, string
  `Host=localhost;Port=5432;...`.
- The migration history starts from a single baseline,
  `20260922073752_InitialCreate` (the languages table only), in
  `src/backend/Mustask.Infrastructure/Migrations/`. A database created from
  an earlier history does not match it — recreate the stand's database
  rather than updating it, and let `dotnet ef database update` build it
  from the baseline.

## External database (PostgreSQL that already runs on the machine)

For a machine where a PostgreSQL server already holds 5432 and the stand
should use it instead of its own container (tech-002). The container
procedure above does not apply: there is no stand container to bring up first,
and the string points at your own server on purpose.

1. Set the connection string as an AppHost user secret (the real password
   goes only into the secret, never into a file or a commit):
   `dotnet user-secrets set ConnectionStrings:mustask
   "Host=localhost;Port=5432;Database=mustask;Username=postgres;Password=<your
   password>" --project src/backend/Mustask.AppHost`. When this value is
   set, AppHost declares a connection-string resource instead of the
   PostgreSQL container and hands the string to `Mustask.Api` unchanged.
   `Ports:Db` and `Parameters:mustask-db-password` are **not used** in this
   mode; `Parameters:keycloak-admin-password` and
   `Parameters:keycloak-panel-client-secret` are still required. The secret
   belongs to AppHost's single user-secrets store, so it switches **every
   checkout and worktree** of this repository on the machine to the same
   database `mustask` (container mode gives each checkout its own volume), and
   a `dotnet ef database update` run from a branch with an unmerged migration
   changes the database every other stand uses. To scope the mode to one
   checkout, do not set the secret: export `ConnectionStrings__mustask` in the
   terminal that starts that checkout's AppHost instead.
2. Create the database and apply the migrations. Export the same string as
   `ConnectionStrings__mustask` in a terminal and run
   `dotnet ef database update --project src/backend/Mustask.Infrastructure`
   — it creates the database `mustask` if it does not exist (the role needs
   the CREATEDB privilege, or create the empty database yourself) and applies
   every migration; a repeat run reports "already up to date". The stand never
   does this itself (ADR-0002), so do it before the first start and after
   every new migration.
3. Start the stand: `dotnet run --project src/backend/Mustask.AppHost` with
   the default launch profile (`https`, the first in
   `Properties/launchSettings.json`; do not pass `--launch-profile http`).
4. To go back to the stand's own container, remove the secret (`dotnet
   user-secrets remove ConnectionStrings:mustask --project
   src/backend/Mustask.AppHost`) and make sure `ConnectionStrings__mustask` is
   not exported in the terminal that starts AppHost: AppHost reads the
   environment variable exactly like the secret, so a terminal that still has
   it exported from step 2 keeps the stand in this mode. AppHost prints a
   `Database:` line at startup naming the mode it chose.

Verified 2026-10-01 (tech-002): the stand came up against a PostgreSQL on
`localhost:5432` with the migrations applied to the database `mustask` —
Keycloak, the API `/health` and the panel were up.

## Migrations that report

`dotnet ef database update` prints "Applying migration … Done." for each
migration and nothing a migration says itself: a PostgreSQL `NOTICE` or
`WARNING` raised inside it lands in the **PostgreSQL server log** (container
mode: `docker logs` of the stand's database container; external mode: the
server's own log), never in the terminal, with or without `--verbose`. A
migration whose result needs a person's decision therefore comes with a query
to run **after** the update, and an empty answer means nothing needs deciding.
Run it with any SQL client against the same database (`psql`, an IDE's database
console).

### `MoveResolvedStatesToResolvedCategory` (bug-004 D-3)

It moves «Разрешена» on Эпик, Фича and Пользовательская история from
InProgress to Resolved, for the rows still as the old seed wrote them (type key
EPIC, FEAT or US, third state, InProgress), and touches nothing else. After
`database update`, run the check — the `SELECT` of the migration's own report,
pinned equal to it by `MoveResolvedStatesToResolvedCategoryRunbookTests`:

```sql
SELECT process.organization_id AS organization_id,
       state.name AS state_name,
       string_agg(DISTINCT state.category, ', ' ORDER BY state.category) AS categories
FROM state_definitions AS state
JOIN work_item_types AS kind ON kind.id = state.work_item_type_id
JOIN processes AS process ON process.id = kind.process_id
GROUP BY process.id, process.organization_id, state.name
HAVING count(DISTINCT state.category) > 1
ORDER BY process.organization_id, state.name;
```

- **No rows:** no process has a state name in two categories.
- **A row:** that organization's process still has the name in more than one
  category, because someone changed a row by hand; the migration did not guess.
  Its board shows the name in two columns until the categories agree. There is
  no screen to change a state yet (doc-007's customization is not built), so
  decide per row and set `state_definitions.category` by hand.

The check names only the breach of the one-category rule. A state renamed or
repositioned by hand that breaks no rule is not named anywhere: nothing needs
deciding there.

**Running the move again.** `database update` applies the migration once per
database. A checkout built before it that shares the database (external mode)
keeps writing the old seed's shape for every organization it creates, and
`database update` is then a no-op. The move is idempotent — it selects only rows
still as the old seed wrote them — so run it by hand once more, then the check:

```sql
UPDATE state_definitions AS state
SET category = 'Resolved'
FROM work_item_types AS kind
WHERE kind.id = state.work_item_type_id
  AND kind.key IN ('EPIC', 'FEAT', 'US')
  AND state.name = 'Разрешена'
  AND state.position = 3
  AND state.category = 'InProgress';
```

## After a stand reset

`scripts/stand-reset.sh` (PowerShell: `scripts/stand-reset.ps1`; mod-014,
ADR-0025 § 7) wipes the stand's pair — Keycloak's persistent store and the
stand's own database — when the start's drift check reports that the live realm
differs from `deploy/keycloak/realms`. It is decided by the same
`Mustask.AppHost` code a start runs, so run it from the terminal that starts
the stand (an exported `ConnectionStrings__mustask` switches it exactly as it
switches the stand). Afterwards the database is empty:

- container mode: the PostgreSQL volume is gone; start the stand (it creates an
  empty `mustask` database) and apply the migrations with the container
  procedure above (steps 2–3);
- external mode: the reset dropped the one database the connection string
  names and created it empty — no other database on that server is touched,
  and `postgres`/`template0`/`template1` never are; apply the migrations
  (step 2 of "External database") before starting the stand. Every checkout
  that worked on that database lost its stand data with it.

Then seed the Keycloak users (`bash scripts/keycloak-seed.sh`); the first
reconciliation of the user projection loads them into the new database.

## A stand with data that moves to organization access (mod-016) — no reset

The migration `AddOrganizationAccess` is not only schema: it **seeds** the
memberships. In the same migration, each existing organization — active or
deactivated — gets exactly one member, an `admin`: the creator the organization
already records (whatever the user projection says of them), added by
themselves at the organization's creation time; and a project's creator joins
the project's first team when they are then a member of its organization.
Nobody else is seeded: everyone else gets «Организация не найдена» until an
administrator adds them on «Участники». `Down` drops the two membership tables
(the organizations, projects and teams stay).

For a stand that holds data you want to keep, this is the order (the full text,
with what each step does to Keycloak, is in `src/backend/AGENTS.md`, "A stand
that predates mod-016"): back up if you want to; apply the migrations as above
(the migration seeds the creators); start the stand — the `keycloak` resource
stays unhealthy, because the drift check reports the old realm roles; run
`bash scripts/migrate-live-realm.sh` (PowerShell
`scripts/migrate-live-realm.ps1`) with `KEYCLOAK_ADMIN_PASSWORD` in the
environment; the next probe passes. **Do not run `scripts/stand-reset.sh`** for
these findings, although the check's text names it: it would delete the data
this order keeps.
