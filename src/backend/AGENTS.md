# Backend commands — .NET 10 + PostgreSQL

Scoped instructions: this file is loaded when the work happens under
`src/backend/`. The project's rules — the pipeline, the hard rules, the
language policy — stay in the root `AGENTS.md`; here are the commands only.
Conventions for the code itself: `docs/conventions/backend.md`.

The solution is `Mustask.sln`, in the repository root — every command below is
run from there, not from this directory.

- `dotnet build Mustask.sln` — build (nullable + warnings-as-errors).
- `dotnet test Mustask.sln` — all tests; `Mustask.Api.IntegrationTests` and the
  `RequiresDocker` tests of `Mustask.Infrastructure.Tests` bring up PostgreSQL
  through Testcontainers — Docker must be running.
- `dotnet run --project src/backend/Mustask.AppHost` — local startup through
  .NET Aspire: brings up `Mustask.Api`, a PostgreSQL container (or uses an
  external PostgreSQL — see "External database" below), the frontend
  (Vite dev server, `AddViteApp`, `VITE_API_BASE_URL` is passed through from
  the `api` endpoint) and Keycloak with the `mustask` realm (ADR-0018; the
  access model is groups → roles → permissions, and permissions arrive in
  the token as `resource_access.mustask-api.roles`), and opens the Aspire
  dashboard with links, logs and traces. `Mustask.Api` is a resource server
  of realm `mustask` (ADR-0018): it checks a Bearer access token's
  signature, `iss` and `aud` against the realm's JWKS, configured by
  `Keycloak:Authority`/`Keycloak:Audience`; there is no login/password loop
  of our own, and accounts are created by `scripts/keycloak-seed.sh`, not
  seeded into our own database. `Mustask.Api` is also the panel's OIDC
  client (ADR-0022): `Features/Session/` performs the Authorization Code +
  PKCE exchange with Keycloak on the panel's behalf, keeps the refresh token
  in its own encrypted cookie (`mustask.session`), and hands the browser only
  a short-lived access token — the panel itself never talks to Keycloak.
  Everything listens on `0.0.0.0` and is reachable from the local network.
  The realm, its roles, groups and login theme arrive by import from
  `deploy/keycloak/` when Keycloak's store is created (see "Keycloak on the
  stand" below); after that first start the Keycloak users are created by
  `bash scripts/keycloak-seed.sh` (see the script's header for its
  variables). AppHost also provisions the E2E
  stand-only client `mustask-e2e` and writes its secret to
  `.e2e-stand/keycloak-e2e-client.json` (ADR-0023). A separate docker compose
  is not needed (see
  `docs/adr/0001-use-net-aspire-for-local-orchestration.md`). On a fresh
  clone, three secrets are required before the first start — the PostgreSQL
  password, the Keycloak administrator password, and the client secret of
  the confidential OIDC client `mustask-panel` (ADR-0022): `AppHost.cs`
  takes them from user secrets instead of hardcoding them (`dotnet
  user-secrets set Parameters:mustask-db-password <password> --project
  src/backend/Mustask.AppHost`, likewise for `Parameters:keycloak-admin-password`
  and `Parameters:keycloak-panel-client-secret`); without this step the
  start fails with "missing configuration value".
- External database (a PostgreSQL server that already runs on the machine, for
  example on 5432): set the user secret `ConnectionStrings:mustask` — `dotnet
  user-secrets set ConnectionStrings:mustask
  "Host=localhost;Port=5432;Database=mustask;Username=postgres;Password=<your
  password>" --project src/backend/Mustask.AppHost` (the environment variable
  `ConnectionStrings__mustask` works too). AppHost then starts no PostgreSQL
  container and hands that string to `Mustask.Api` as is; `Ports:Db` and
  `Parameters:mustask-db-password` are **not used** in that mode (the other two
  secrets stay required). The stand does not create the database:
  `dotnet ef database update --project src/backend/Mustask.Infrastructure`
  does, and applies the migrations — run it before the first start (the
  runbook: `docs/runbooks/local-migrations.md`, "External database"). Start
  AppHost with the default launch profile (`https`, the first one in
  `Properties/launchSettings.json` — plain `dotnet run` picks it); the stand is
  run and verified with that profile only. Do not export
  `ConnectionStrings__mustask` in the terminal that starts AppHost unless you
  mean this mode — the variable switches it; AppHost prints a `Database:` line
  at startup naming the mode it chose. The user secret belongs to AppHost's
  single user-secrets store, so it switches every checkout and worktree on the
  machine to the same database `mustask` (container mode gives each checkout
  its own volume); to scope the mode to one checkout, export
  `ConnectionStrings__mustask` in the terminal that starts that checkout's
  AppHost instead of setting the secret.
- Keycloak on the stand (mod-014, ADR-0025): AppHost **builds** the Keycloak
  image from `deploy/keycloak/Dockerfile` — the image of `Keycloak:Image`/
  `Keycloak:Tag` plus the provider `mustask-user-projection`, whose Java
  sources (`deploy/keycloak/providers/user-projection`) are compiled and
  unit-tested inside that build; the first build on a machine, or after a
  Keycloak tag change, needs the network. Keycloak's store **persists** in a
  named volume paired with the stand's database (container mode: one pair per
  checkout; external mode: one store per external database), so users and
  their ids survive restarts; the `Keycloak store:` line at startup names it.
  Because `--import-realm` never overwrites an existing realm, the `keycloak`
  resource carries a drift check (`keycloak-realm-drift`): when the live realm
  differs from the realm import — a realm-import edit, another branch's import,
  a changed admin password, `mustask-panel` secret or stand port — the
  resource stays unhealthy, the stand does not come up ready, and the AppHost
  log names the difference and the remedy. The remedy is either restoring the
  old value or `bash scripts/stand-reset.sh` (PowerShell
  `scripts/stand-reset.ps1`): it wipes exactly this stand's pair — in
  external mode it drops and recreates only the database the connection
  string names — and prints the next steps (migrations, seed); its header has
  the details. The provider pushes user changes to `Mustask.Api`'s
  `/api/user-directory/feed` signed with a secret AppHost generates per run
  (no fourth user secret). It reaches the api at
  `http://host.docker.internal:{Ports:Api}` — the api's own port on the Docker
  host, mapped with `--add-host …:host-gateway` so a Linux engine resolves it
  too — not through Aspire's container tunnel, which loses the api for good
  after a `keycloak` resource restart followed by an api restart. `Keycloak:UserProjection:ReconcilePeriodMinutes`
  shortens the reconciliation period for a QA run. `scripts/verify.sh` checks
  a provider change in its `provider` bucket — Docker required: `docker build
  --target provider deploy/keycloak`, then the .NET tests tagged
  `KeycloakProvider` with `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1`.
- The stand's ports come from configuration: the defaults are in
  `appsettings.json` of the `Mustask.AppHost` project (the `Ports` section), and
  are overridden by the environment variables `Ports__Api`, `Ports__Frontend`,
  `Ports__Db`, `Ports__Keycloak`, or by user secrets
  (`dotnet user-secrets set Ports:Api 5100`). AppHost has no `.env` file —
  Aspire does not read one. A port must be free — Aspire will not relocate a
  taken one. The values: panel 3000, API 5000, Keycloak 5010, database 5432
  (the database port is only used when AppHost starts its own container).
- Migrations are applied explicitly, not at `Mustask.Api` startup (ADR-0002). How
  to apply them locally with the stand's fixed ports, and how to create a new
  one — a step-by-step runbook: `docs/runbooks/local-migrations.md`. In
  short: the `ConnectionStrings__mustask` variable is required (there is
  deliberately no fallback to localhost:5432), then
  `dotnet ef database update --project src/backend/Mustask.Infrastructure`.
- The interactive API explorer (Scalar) is at `/scalar`, the OpenAPI document
  at `/openapi/v1.json`; in Development it is on by default, outside it — by
  the `ApiExplorer:Enabled` configuration flag (`true`/`false` overrides the
  environment in both directions). The base address of a running
  `Mustask.Api` — from the Aspire dashboard or from the Information line in
  the log at startup.
- The Development stand's demonstration organization: at start `Mustask.Api`
  seeds the organization's process (the five work item types and their
  states, mod-001) for the fixed identifier
  `d0000000-0000-4000-8000-000000000001`, so the read-only process screen has
  something to show before organizations can be created. The panel opens it at
  `/organizations/d0000000-0000-4000-8000-000000000001/settings/process`, and
  the API answers
  `GET /api/organizations/d0000000-0000-4000-8000-000000000001/process`. The
  seed is a hosted service registered **only in Development** (never AppHost,
  never another environment). The API log carries its outcome as one of three
  events: `DemonstrationSeedReady`, `DemonstrationSeedSkipped` (the database
  lacks a migration of this build — apply the migrations, see the migrations
  bullet above, and restart) or `DemonstrationSeedFailed` (nothing remains,
  the next start tries again). It never stops the start. `mod-002` removes it
  when real organization creation lands.
