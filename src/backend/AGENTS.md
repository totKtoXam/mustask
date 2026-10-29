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
  through Testcontainers — Docker must be running. Each of the two starts one
  container per run and gives every fixture a database of its own on it
  (`tests/Shared/SharedPostgres.cs`, tech-047). Their classes run in
  parallel: `Mustask.Api.IntegrationTests` splits `MustaskApiCollection` into
  shards (`ApiTestCollectionFactory`, `MUSTASK_API_TEST_SHARDS` to override
  the count). A new class that captures the console, sets an environment
  variable, asserts on log entries of a host it builds, builds a host
  straight from `WebApplicationFactory<Program>`, shares a static flag with
  another class or holds a timing budget goes into `ApiTestCollectionFactory.SerialClasses` (a `*VolumeTests`
  name goes there by itself) — in `Mustask.Infrastructure.Tests`, a class
  that touches `ConnectionStrings__mustask` names
  `ProcessEnvironmentCollection`.
- `dotnet run --project src/backend/Mustask.AppHost` — local startup through
  .NET Aspire: brings up `Mustask.Api`, a PostgreSQL container (or uses an
  external PostgreSQL — see "External database" below), the frontend
  (Vite dev server, `AddViteApp`, `VITE_API_BASE_URL` is passed through from
  the `api` endpoint) and Keycloak with the `mustask` realm (ADR-0018; the
  instance-level access model is groups → roles → permissions, arriving in
  the token as `resource_access.mustask-api.roles`; rights inside an
  organization come from the membership, ADR-0027 — see "The access model"
  below), and opens the Aspire
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
  the details. **One exception to that remedy:** the drift findings of a store
  that predates mod-016 are cleared by `bash scripts/migrate-live-realm.sh`,
  never by the reset — see "A stand that predates mod-016" below. The provider pushes user changes to `Mustask.Api`'s
  `/api/user-directory/feed` signed with a secret AppHost generates per run
  (no fourth user secret). It reaches the api at
  `http://host.docker.internal:{Ports:Api}` — the api's own port on the Docker
  host, mapped with `--add-host …:host-gateway` so a Linux engine resolves it
  too — not through Aspire's container tunnel, which loses the api for good
  after a `keycloak` resource restart followed by an api restart. `Keycloak:UserProjection:ReconcilePeriodMinutes`
  shortens the reconciliation period for a QA run. `scripts/verify.sh` checks
  a provider change in its `provider` bucket — Docker required: `docker build
  --target provider deploy/keycloak`, then the .NET tests tagged
  `KeycloakProvider` with `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1`. A realm-import
  edit (`deploy/keycloak/realms/*`) is checked in both buckets: by the contract
  tests, and by `LiveRealmImportTests`, where a real Keycloak imports the
  repository's real file (an import Keycloak rejects only shows when it is
  started). `LiveRealmMigrationTests` — the live-realm command against a
  throwaway Keycloak — has a switch of its own,
  `MUSTASK_KEYCLOAK_LIVE_REALM_TESTS=1`, that no bucket sets: it runs on purpose.
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
- The access model (mod-016, ADR-0027) in three lines. **Instance rights come
  from the token**: the realm roles are `instance-admin` (group
  «Администраторы»: the dictionaries and creating organizations),
  `dictionaries-admin` and `organization-creator`; every signed-in user, in no
  group at all, reads the dictionaries through the realm's default roles.
  **Rights inside an organization come from the user's membership in it**
  (`admin`, `member` or `viewer`), read from the database on every request —
  nothing about them is in the token — so a role change applies on the next
  request. **Every route with `{organizationId}` declares an organization code**
  (`.RequiresOrganizationPermission(PermissionCatalog.X)`); the inventory test
  fails `scripts/verify.sh` for one that does not, and a non-member of the
  organization of the address gets 404 «Организация не найдена», never a 403.
- An organization on the stand: sign in to the panel as the administrator
  (group «Администраторы») or as a member of «Создатели организаций», and
  create one there (`POST /api/organizations`, which needs the right
  `organizations.organization.create`); its process is seeded in the same
  step, and the creator becomes **its administrator** in the same
  transaction. Everyone else is added on the organization's «Участники» page
  by an administrator and sees nothing of it before that — the seeded `member`
  user (`scripts/keycloak-seed.sh`, `MEMBER_PASSWORD`) is in no group and sees
  no organization until it is added. There is no seeded demonstration
  organization. A stand whose Keycloak was created before mod-016 is moved with
  `bash scripts/migrate-live-realm.sh`, below; a stand created before the group
  «Создатели организаций» existed picks the realm import up only after a stand
  reset — `bash scripts/stand-reset.sh`, see "Keycloak on the stand" above;
  until then its drift check keeps the stand unhealthy (mod-002 D-j).
- **A stand that predates mod-016 — the order of steps** (BR-M1-28, BR-M1-37;
  the stand's data stays, there is no reset). (1) Back up first if you want
  to: the database `mustask` and the Keycloak volume. (2) Apply the migration
  per the runbook (`docs/runbooks/local-migrations.md`): it seeds the creator
  of every existing organization as its administrator (nobody else — everyone
  else gets «Организация не найдена» until an administrator adds them).
  (3) Start the AppHost. The `keycloak` resource stays unhealthy and the stand
  not ready: the drift check reports the old realm roles `admin`, `member` and
  `viewer`, the new `instance-admin`, the composites of `default-roles-mustask`
  and the mappings of `/Администраторы` — its text names the reset, which is
  **not** the remedy. (4) Run `bash scripts/migrate-live-realm.sh` (PowerShell
  `scripts/migrate-live-realm.ps1`) with `KEYCLOAK_ADMIN_PASSWORD` in the
  environment (the password of `Parameters:keycloak-admin-password`; never an
  argument) and `KEYCLOAK_URI` if Keycloak is not on 5010. It creates
  `instance-admin`, gives it to `/Администраторы`, to every other group that
  mapped `admin` and to every user given `admin` directly, adds the read of the
  dictionaries to the default roles, and only then deletes the roles `admin`,
  `member`, `viewer` and the groups `/Сотрудники/Участники` and `/Сотрудники`;
  nobody is created or deleted and every user keeps their id. It is safe to
  repeat; exit code 2 means it stopped before any change and named a structure
  it does not know (read the report, fix it in the Keycloak console, run again);
  exit code 1 means a request failed — run it again. (5) The next probe of the
  drift check passes and the stand comes up ready. Run it on a task stand
  first; never `stand-reset.sh` for these findings.
