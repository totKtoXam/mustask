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
     database.
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
     Ports:Db 5435`), or you risk applying migrations to your own server.
     If something doesn't add up, you can check yourself against the
     actual run: Aspire dashboard → `api` resource → Environment variables
     tab → `ConnectionStrings__mustask`.
  3. Export `ConnectionStrings__mustask` with this value into the current
     session's/terminal's environment variables and run:
     `dotnet ef database update --project src/backend/Mustask.Infrastructure`
     (without `--startup-project src/backend/Mustask.Api` — `Mustask.Api` does not
     reference `Microsoft.EntityFrameworkCore.Design`, and the command
     fails with that flag). New migration: `dotnet ef migrations add
     <Name> --project src/backend/Mustask.Infrastructure`.
     `ConnectionStrings__mustask` is needed **always** — `MustaskDbContextFactory`
     throws before attempting a connection if the variable is not set (see
     above, "no hardcoded fallback"); but the value can be any
     syntactically valid string, even a deliberately broken one (e.g. with
     an unreachable port) — `migrations add` never touches the real
     database.
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
