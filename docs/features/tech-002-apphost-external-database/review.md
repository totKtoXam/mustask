# tech-002-apphost-external-database — review

## QA

Stage 05-qa, S route (light form: no test matrix, per `s-route.md` step 3).
Verdict: **pass — zero blockers, two minor non-blocking findings** (QA-1,
QA-2 below).

### What the change is

When `ConnectionStrings:mustask` is set (user secret or
`ConnectionStrings__mustask`), AppHost declares `AddConnectionString("mustask")`
instead of the PostgreSQL container; otherwise it declares the container as
before. `api` references and waits for the single `database` variable of
either branch. Files: `AppHost.cs`, `src/backend/AGENTS.md`,
`docs/runbooks/local-migrations.md`, the guard test
`Mustask.Architecture.Tests/AppHostExternalDatabaseRules.cs`.

### Checklist

| Check | Result |
|---|---|
| Default path (no connection string) unchanged | Pass. The container branch is the old code moved into the `if` body: same `AddParameter("mustask-db-password", secret: true)`, same `AddPostgres("postgres", password:, port: Port("Db"))`, `WithDataVolume()`, `AddDatabase("mustask")`; the diff shows nothing but indentation, the variable name (`postgres` -> `database`) and its declared type. `.WithReference(database)` / `.WaitFor(database)` bind to the same overloads as before (`PostgresDatabaseResource` is an `IResourceWithConnectionString`), and the object passed is the same one. Nothing else in `AppHost.cs` referenced the old `postgres` variable. |
| External branch wires api -> connection string | Pass. `AddConnectionString("mustask")` reads the same key `ConnectionStrings:mustask` that gates the branch, and `WithReference` exposes it to `Mustask.Api` as `ConnectionStrings__mustask`, which is what the API's `AddNpgsqlDbContext` and `MustaskDbContextFactory` read. |
| Whitespace-only value | Falls into the container branch (`IsNullOrWhiteSpace`); consistent with "not set". |
| Build | `dotnet build Mustask.sln` -> 0 warnings, 0 errors. |
| Guard test | `AppHostExternalDatabaseRules` 2/2 green; it also fails on my own mutation M2 below. |
| Docs accurate | Pass with two remarks (QA-2, O-2). Checked against the code: the secret key and the env var alias; "`Ports:Db` / `mustask-db-password` are not used" (both live only in the container branch); "the other two secrets stay required" (`keycloak-admin-password`, `keycloak-panel-client-secret` are declared outside the switch); "`dotnet ef database update` creates the database" (`MustaskDbContextFactory` reads `ConnectionStrings__mustask`; EF's migrator creates a missing database); "the first profile in `launchSettings.json` is `https`" (it is); the pointer from the old step 2 of the container procedure. No real password in any changed file (grep of the diff). |
| E2E | **Not applicable**: no UI and no frontend change; the change is AppHost wiring and docs. Nothing under `src/frontend/` was touched, so no Playwright spec was written or run and no stand was started or stopped. |

### Live evidence (read-only, no stand started or stopped)

The orchestrator's stand runs this exact AppHost code. Observed by QA:

- `http://localhost:5000/health` -> `200 Healthy`. `/health` runs every
  registered check, including `MustaskDbContext` (`AddNpgsqlDbContext`; pinned
  by `DatabaseHealthCheckTests`), so a healthy answer means the API reached
  PostgreSQL through the string it was handed — not only that the process is up.
- `http://localhost:5010/realms/mustask` -> 200; `http://localhost:3000/` -> 200.
- `docker ps`: one PostgreSQL container, `postgres-5432` (`postgres:latest`,
  `0.0.0.0:5432->5432`) — the human's own server; no PostgreSQL container
  created by Aspire alongside it.
- Read-only `psql` on `localhost:5432`, database `mustask`: tables
  `__EFMigrationsHistory` and `languages`; `MigrationId` =
  `20260922073752_InitialCreate`, the only migration in
  `Mustask.Infrastructure/Migrations`; `pg_stat_activity` shows an idle
  session on `mustask` besides QA's own — the API's connection.
- AppHost log (`apphost.log`): starts, dashboard up, Keycloak provisioning
  finished. User secrets contain `ConnectionStrings:mustask` (values not
  printed).

What the live run does not prove: that `Parameters:mustask-db-password` is not
demanded in external mode — that secret exists in the user secrets of this
machine. That claim rests on the code (the `AddParameter` call is inside the
container branch) and on the guard test, not on the run.

### Commands run

- `dotnet build Mustask.sln` -> 0 warnings, 0 errors.
- `dotnet test src/backend/tests/Mustask.Architecture.Tests --no-build --filter
  FullyQualifiedName~AppHostExternalDatabaseRules` -> 2/2 pass.
- Mutations of `AppHost.cs` against the guard test (each restored with
  `git checkout --`; worktree clean afterwards):
  - M1 (the developer's, from the Log, not repeated): hoist the password
    parameter above the switch -> fails.
  - M2: delete `.WaitFor(database)` -> `ApiReferencesAndWaits...` FAILS (1 of 2
    red), as it should.
  - M3: invert the switch to `!string.IsNullOrWhiteSpace(externalDb)` -> both
    tests still PASS. See QA-1.
- `bash scripts/verify.sh` -> exit 0, `RESULT: green` (classification: prose,
  agents, dotnet; Architecture.Tests 68/68, Domain 38, Shared 53, Application
  39, Contracts 48, Infrastructure 74/74, Api.IntegrationTests 346/346).
  Run once.

### Defects

No blockers. No majors.

- **QA-1 (minor, non-blocking) — the guard does not pin which branch holds
  what.** `AppHostCs_DeclaresTheContainerAndItsPasswordOnlyBehindTheExternalDatabaseSwitch`
  checks that `AddParameter(...)`, `AddPostgres(` and `AddConnectionString(...)`
  each occur once and after the text `string.IsNullOrWhiteSpace(externalDb)`.
  Steps: change `if (string.IsNullOrWhiteSpace(externalDb))` to
  `if (!string.IsNullOrWhiteSpace(externalDb))` in `AppHost.cs` (or swap the
  `if` and `else` bodies) and run the test: 2/2 green. Impact is limited — the
  mutated AppHost fails loudly at the first start in the default mode
  (`AddConnectionString` with no value) or starts a container in the external
  one, so this is not the quiet failure the test exists for (the hoisted
  parameter) — hence minor. Suggested fix: also assert that
  `AddConnectionString("mustask")` sits after the `else` and the container
  calls before it, and that the condition carries no `!`.
- **QA-2 (minor, docs, non-blocking) — the docs omit that the secret is
  shared by every worktree on the machine.** The AppHost has a single
  `UserSecretsId`, so `dotnet user-secrets set ConnectionStrings:mustask`
  switches the stand of every worktree and every task on that machine, not
  only the one the human is looking at, and two stands running side by side
  (different worktrees, different `Ports:*`) then share one database
  `mustask` — migrations and data collide, which the container mode avoids
  by giving each stand its own container and volume. The docs warn only about
  the exported terminal variable. Suggested fix: one sentence in
  `src/backend/AGENTS.md` and in the runbook's last section.

### Observations (not defects of this change)

- **O-1.** Not checked: whether the Aspire dashboard masks the
  `ConnectionStrings__mustask` value, which contains the password. The
  dashboard was not opened (no login token to hand and the stand is not
  ours to drive). If it shows the value in clear, the exposure is the same
  as for any local `Parameters:*` secret on a loopback dashboard, but it is
  worth one look by the human.
- **O-2.** The docs say `--launch-profile http` must not be passed and that the
  stand is "run and verified with `https` only"; the reason is not given (the
  `http` profile lacks `ASPIRE_ALLOW_UNSECURED_TRANSPORT`, which is the
  likely cause). Not reproduced — it would need a second stand, which QA was
  told not to start. The instruction is safe as written.
- **O-3 (pre-existing, outside this diff).** In a worktree without
  `.e2e-stand/`, the start-up log prints `E2E stand: could not delete the
  stale stand file ... DirectoryNotFoundException`: `File.Delete` on a missing
  directory is an `IOException`, so the best-effort catch reports a harmless
  condition as a failure. Visible in `apphost.log`; the line is not touched
  by this branch.

## Verdict

Pass. Zero blockers; QA-1 and QA-2 are minor and may be taken now or logged
for later at the orchestrator's choice. E2E: not applicable.

## Code review — Round 1 — backend

Clean-context review of `git diff main...tech/002-apphost-external-database`
(`6d11bfd`, `3ff6d95`, `99bc54f`) against the `status.md` Plan (items 1-4):
the AppHost switch, the docs, the guard test, the live check.

**Limit of this round.** No stand was started or stopped. The evidence for the
external branch at run time is read-only: `docker ps` shows `postgres-5432`
(the human's server, `0.0.0.0:5432`) and `mustask-e2e-keycloak-5010`, with no
PostgreSQL container made by Aspire; `http://localhost:5000/health` -> 200. The
Aspire 13.5.1 behaviour cited below comes from the package itself
(`~/.nuget/packages/aspire.hosting/13.5.1/lib/net8.0/Aspire.Hosting.xml` and
the user strings in `Aspire.Hosting.dll`), not from a run. Commands run:
`dotnet build src/backend/Mustask.AppHost` -> 0 warnings, 0 errors;
`dotnet test src/backend/tests/Mustask.Architecture.Tests --filter`
`AppHostExternalDatabaseRules|KeycloakStandClientSecretPlacementRules` -> 3/3.

### Correctness (no finding)

- `AddConnectionString("mustask")` in 13.5.1 (`Aspire.Hosting.xml:19680-19689`,
  `:21009-21018`) reads `Configuration.GetConnectionString("mustask")`, i.e. the
  same `ConnectionStrings:mustask` key the switch reads at `AppHost.cs:101`. It
  wraps a `secret: true` parameter in a surrogate that implements
  `IResourceWithConnectionString`. The external branch is entered only when that
  value is non-blank (`:105`), so it cannot reach `AddConnectionString` with a
  missing value; a whitespace-only value falls into the container branch.
- Both branches name the resource `mustask` (`:115`, `:121`), so `WithReference`
  (`:127`) injects `ConnectionStrings__mustask` in either mode. That is the
  `connectionName: "mustask"` the API reads
  (`Mustask.Infrastructure/DependencyInjection.cs:39-40`).
- `WaitFor(database)` (`:128`) on the connection-string resource: the running
  stand is in this mode and the API is past `WaitFor` and healthy (above).
  AppHost does not health-check the external server, so the API starts even
  when the server is down and `/health` reports it. Nothing in the diff claims
  otherwise.
- `IResourceBuilder<IResourceWithConnectionString>` (`:103`) relies on the
  covariant `IResourceBuilder<out T>`, and the build is green.
  `Aspire.Hosting.PostgreSQL` 13.5.1 has no `WithReference` overload of its
  own (its XML has none), so the container branch binds the same overload as
  before. The connection properties come from the runtime resource, so they
  are unchanged.
- The password parameter is declared only in the container branch (`:107`).
  `Port("Db")` has no other call site (`:109`).
- Security: neither code nor docs contain a real password. `AGENTS.md:52` and
  `local-migrations.md:68` use a `<your password>` placeholder. One exception
  is in the task artifacts (CR5).

### Findings

**CR1 — major — the container procedure now walks into the switch without
saying so.** `docs/runbooks/local-migrations.md:13-14,31-43`. Step 1 says
`dotnet run --project src/backend/Mustask.AppHost` "brings up an empty
database". Step 3 has the reader export `ConnectionStrings__mustask` "into the
current session's/terminal's environment variables". Lines 38-43 add that the
variable is needed **always**, and that even a deliberately broken value works
for `migrations add`, which invites keeping it set. Before this branch that
variable was inert for AppHost. Now `AppHost.cs:101` reads it through the
default environment-variable provider. An AppHost started again from that
terminal (or with the value kept in the user's environment) starts no
PostgreSQL container. It hands the API a string aimed at a port where nothing
listens, and nothing on screen names the cause. The diff knows this trap and
documents it in three places: `AppHost.cs:98-100`, `src/backend/AGENTS.md:63-65`
and the runbook's external step 4 (`:86-91`). It does not document it at the
one instruction that produces it, the container procedure's export step. This
is the direction-2 case of item 6: behaviour the diff changes, described by
prose the diff left as it was, in a file the diff touches two lines away (step 2's
new pointer). Someone following step 3 and then step 1 walks into the
failure, so it is major. Fix: in step 3 (and in the "needed **always**" note), say
to export it in a terminal other than the one that starts AppHost, or to unset
it before starting AppHost again, because AppHost reads `ConnectionStrings:mustask` too and
with it set starts no container (pointer to the last section). Qualify step 1's
"brings up an empty database" the same way. Optional, and the stronger fix: one
start-up line from AppHost naming the mode it chose, for example in the `else`
branch, "Database: external (ConnectionStrings:mustask is set); no PostgreSQL
container". That is the file's own "one named cause" idiom (`AppHost.cs:20-21`), and it makes CR1 and
CR2 self-diagnosing.

**CR2 — minor — QA-2, agreed: the user secret switches every checkout on the
machine.** `src/backend/AGENTS.md:49-57`, `docs/runbooks/local-migrations.md:65-74`.
The secret belongs to the AppHost's single `UserSecretsId`
(`Mustask.AppHost.csproj`). Setting it puts the stand of every worktree into
this mode, and their stands then share one database `mustask`. Container mode
gives each checkout its own volume: Aspire's volume names carry a per-path
hash (`docker volume ls` shows `<app>.apphost-<hash>-postgres-data` per
checkout for other projects on this machine). On this machine the secret is
already set (QA), and the series has other tasks in flight. A branch's
`dotnet ef database update` (external step 2, "after every new migration")
would put unmerged migrations into the database every other stand uses. That
outcome is conditional and recoverable (`local-migrations.md:51-56`: recreate
from the baseline), so it stays minor. Fix: one sentence in each place. The
secret applies to every checkout and worktree, and they all share one
database. To scope the mode to one checkout, export `ConnectionStrings__mustask`
in the terminal that starts that AppHost instead.

**CR3 — minor — the guard test's rationale overstates the failure it guards.**
`src/backend/tests/Mustask.Architecture.Tests/AppHostExternalDatabaseRules.cs:7-12`
and the assertion message `:50-52` say that hoisting the password parameter
makes the external stand fail with "missing configuration value". In
13.5.1 a parameter with no value is handled by `ParameterProcessor`. It logs
"Parameter resource {ResourceName} could not be initialized. Waiting for user
input." and raises the dashboard's "Unresolved parameters" prompt. Its state is
`ValueMissing` (user strings in `Aspire.Hosting.dll`; `Aspire.Hosting.xml:20759-20785`).
A hoisted parameter that nothing references blocks no resource. So the external
stand would most likely come up and nag for a secret, not fail. This was not run
(it needs a stand). The check is still worth having, and nobody would act
differently on the sentence, so it is minor. Fix: "the external-database stand
then reports a missing value for, and the dashboard prompts for, a secret it
has no use for".

**CR4 — minor — QA-1, agreed: the guard does not pin which branch holds what.**
`AppHostExternalDatabaseRules.cs:102-112` checks only that each call sits after
the condition text. An inverted condition, or swapped `if`/`else` bodies,
passes 2/2 (QA's M3; confirmed by reading). Fix: assert the literal
`if (string.IsNullOrWhiteSpace(externalDb))`. Also assert that `AddParameter`
and `AddPostgres` sit between it and the `else`, and that
`AddConnectionString("mustask")` sits after the `else`.

**CR5 — minor — the human's database credentials are committed.**
`docs/features/tech-002-apphost-external-database/status.md:26` (Plan 4) and
`:31` (the `[route]` Log line) carry the login and password in clear (the
quote that stood here is redacted in round 2, see R2-1). These are the real
credentials of a server published on `0.0.0.0:5432`. The rules say "never
commit secrets" (`AGENTS.md`, Hard rules; `docs/conventions/git.md:66`), and the
runbook in this same diff says "the real password goes only into the secret,
never into a file or a commit" (`local-migrations.md:65-66`). QA's "No real
password in any changed file" (`## QA`, "Docs accurate" row) is therefore not
true of the diff. The value is the image default and the branch has no remote,
so the exposure is small and this stays minor. It still belongs in the same
edit pass as CR1. Fix: replace both with "(credentials in the AppHost user
secret)". The earlier commits on the branch keep the text, and the squash
carries only the final tree.

**CR6 — minor — the new code comments are Russian.** `AppHost.cs:92-100,119-120`.
The Log (`[dev] AppHost review`) records that English comments were rewritten
in Russian to match the surrounding block. Root `AGENTS.md`, "Language", makes
code English and names no exception for matching nearby comments. The same
file already mixes the two (`AppHost.cs:200-206` is English). Fix: English for
the lines this branch adds.

### Behavioural claims checked (item 6)

Direction 1 — prose the diff adds or changes:
- `AppHost.cs:92-94`, user secret or env var switches the mode → `AppHost.cs:101` (host configuration includes user secrets and environment variables); live stand in this mode.
- `AppHost.cs:93-95`, no container, string handed to `Mustask.Api` as is → `:105-122`, `:127`; `Aspire.Hosting.xml:19680-19689`.
- `AppHost.cs:95-97`, password parameter not declared, `Ports:Db` unused → `:107`, `:109` (sole `Port("Db")`).
- `AppHost.cs:97-98`, the database is created by `dotnet ef database update` (ADR-0002) → no `Migrate(`/`EnsureCreated` in non-test `src/backend` (grep), `DependencyInjection.cs:39`; EF Core 10 `Microsoft.EntityFrameworkCore.Relational.xml:10418-10422` ("Will create the database if it does not already exist").
- `AppHost.cs:98-100`, an exported variable switches AppHost → `:101`; the claim holds, and its absence from the container procedure is CR1.
- `AppHost.cs:119-120`, Aspire takes the string from `ConnectionStrings:mustask` and passes `ConnectionStrings__mustask` → `Aspire.Hosting.xml:19684`; `:127`.
- `AppHostExternalDatabaseRules.cs:7-12`, `:50-52`, failure mode of a hoisted parameter → CR3.
- `AppHostExternalDatabaseRules.cs:80-81`, a mention in a comment neither satisfies nor breaks the check → `:82-83` drops `//`-led lines; no guarded call in `AppHost.cs` carries a trailing comment.
- `AppHostExternalDatabaseRules.cs:54-56`, a container above the switch starts even in external mode → `AppHost.cs:109` is the only `AddPostgres`; pinned by `:54`.
- `src/backend/AGENTS.md:15-17`, a container or an external PostgreSQL → `AppHost.cs:105-122`.
- `src/backend/AGENTS.md:49-57`, secret key, env-var alias, no container, string as is, `Ports:Db`/`mustask-db-password` unused, other two secrets still required → `AppHost.cs:101,105-122,163,169`. Scope across worktrees unstated → CR2.
- `src/backend/AGENTS.md:57-60`, the stand does not create the database, `dotnet ef database update` does → as for `AppHost.cs:97-98`.
- `src/backend/AGENTS.md:60-63`, default profile is `https`, plain `dotnet run` picks it, the stand is run with it only → `Properties/launchSettings.json:4` (first `Project` profile); the `http` profile (`:17-27`) lacks `ASPIRE_ALLOW_UNSECURED_TRANSPORT`, so the prohibition is safe (not run). The advice is mode-agnostic but sits in the external bullet; no finding.
- `src/backend/AGENTS.md:63-65`, the variable switches the mode → `AppHost.cs:101`.
- `src/backend/AGENTS.md:72-73`, the database port is used only with the own container → `AppHost.cs:109`.
- `local-migrations.md:22-27`, pointer to the external mode → `:58-95` exists.
- `local-migrations.md:60-63`, the container procedure does not apply → `AppHost.cs:105-122`.
- `local-migrations.md:65-74` → as for `AGENTS.md:49-57`; scope → CR2.
- `local-migrations.md:75-82`, creates the database if missing (CREATEDB needed), repeat run "already up to date", the stand never does this → EF `Relational.xml:10418-10422`; no `Migrate(` in the API (grep). "already up to date" is EF's own message; not re-run.
- `local-migrations.md:83-85`, launch profile → as for `AGENTS.md:60-63`.
- `local-migrations.md:86-91`, going back: remove the secret and unset the variable → `AppHost.cs:101,105`.
- `local-migrations.md:93-95`, verified 2026-10-01 → `status.md` Log `[dev]`; QA's live evidence; this round's read-only `docker ps` and `/health`.
- `status.md` Plan 1-3 → met (`AppHost.cs:101-128`; the two docs; the guard test). Plan 4 → met (live). Its credential → CR5.
- `review.md` `## QA`, "No real password in any changed file" → CR5. QA's O-1 is answered by `Aspire.Hosting.xml:19682-19686`: the parameter is `secret: true`. The API's `ConnectionStrings__mustask` carries a password in container mode too, so this mode adds no exposure.

Direction 2 — prose outside the diff that describes the changed behaviour.
Behaviours: AppHost now reads `ConnectionStrings:mustask` and may start no
container; `mustask-db-password` and `Ports:Db` become conditional. I grepped
`docs/`, `.claude/`, `scripts/`, `src/frontend/e2e/`, the `AGENTS.md` files and
non-test `src/backend` for `ConnectionStrings__mustask`,
`ConnectionStrings:mustask`, `mustask-db-password`, `Ports:Db`/`Ports__Db`,
`5432`, `PostgreSQL container`/`Postgres container`, `AddPostgres`,
`WithDataVolume`, `three secrets`, `empty database`, and `postgres` in
`scripts/`, `.claude/` and `e2e/`. Hits:
- `local-migrations.md:13-14,31-43` → CR1.
- `src/backend/AGENTS.md:40-48`, three secrets on a fresh clone → true of the default mode; qualified by the new `:56-57`. No finding.
- `src/backend/AGENTS.md:77-79`, the variable is required for `dotnet ef` → accurate; the switch caveat is at `:63-65`. No finding.
- `AppHost.cs:82-90`, `:160-168`, the password is a required parameter and the panel secret is "the third" → true of the default mode; qualified at `:92-97`. No finding.
- `src/backend/Mustask.Infrastructure/MustaskDbContextFactory.cs:10-14,27-28` and `docs/adr/0002-explicit-database-migrations.md:82-88` speak of the Postgres container and a random port and password. They were stale before this branch (ports are fixed since 2026-09-07), and in external mode they name a container that does not exist; harmless. Pre-existing, so no finding.
- `docs/adr/0001-use-net-aspire-for-local-orchestration.md:53`, AppHost orchestrates the API and a PostgreSQL container → still the default. An opt-in local toggle does not reverse that decision and does not need an ADR. `docs/adr/0020-...:58` is history.
- `src/frontend/e2e/support/apiReadiness.ts:50,100`, `/health` hits Postgres → mode-agnostic, true in both.
- Test-suite `ConnectionStrings__mustask` hits (`MustaskApiFactory.cs`, `MigrationsTests.cs` and others) → the test processes set it themselves and never start AppHost. No effect.
- `scripts/`, `.claude/` → none.
Not swept: nothing outside the identifiers above.

### Observations (not findings of this diff)

- Pre-existing: `src/backend/AGENTS.md:47-48` says a missing secret makes the
  start fail with "missing configuration value". In 13.5.1 the same evidence
  as CR3 points to a "waiting for user input" stand with a dashboard prompt.
  This is worth a docs follow-up, together with the stale random-port wording
  in `MustaskDbContextFactory.cs` and ADR-0002.
- For the orchestrator: the consolidation worktree's `AppHost.cs` on disk no
  longer has the switch (read-only `diff` against this branch). The stand that
  is running was built before the switch moved here. Restarting it from there
  before this branch lands would bring it up in container mode on `Ports:Db`
  5432, which `postgres-5432` holds.

### Verdict

Needs changes: one major (CR1) and five minor (CR2-CR6). CR1 is two docs
sentences, or one AppHost start-up line plus the sentences. CR2 and CR5 belong
in the same edit pass. CR3, CR4 and CR6 are cheap. After the fix, QA re-runs
over what was touched: the runbook and `AGENTS.md`, plus the guard test and
`AppHost.cs` if CR3, CR4 or CR6 are taken. Round 2 is then a delta over this one.

## Code review — Round 2 — backend

This round covers only the delta over round 1: the fix commits `bd26c86` and
`60bd1b4` (`git diff 717927a..60bd1b4`), which touch `AppHost.cs`,
`AppHostExternalDatabaseRules.cs`, `src/backend/AGENTS.md`,
`docs/runbooks/local-migrations.md` and `status.md`. I read them against the
round-1 findings CR1-CR6. Code the fix did not touch was not reviewed again.

**Limit of this round.** No stand was started or stopped. The new `Database:`
line is anchored by reading `AppHost.cs`; nobody saw it printed. Commands run:

- `dotnet build src/backend/tests/Mustask.Architecture.Tests` and
  `dotnet build src/backend/Mustask.AppHost` -> 0 warnings, 0 errors.
- `dotnet test src/backend/tests/Mustask.Architecture.Tests --no-build` ->
  68/68 pass. The `AppHostExternalDatabaseRules` filter -> 2/2 pass.

I also ran three mutations of `AppHost.cs` against the guard. The test reads
the source, so `--no-build` sees each change. Each mutation was restored with
`git checkout --`, and the worktree was clean afterwards.

- R2-M1 (the developer's, repeated): `if (!string.IsNullOrWhiteSpace(externalDb))`.
  The switch test fails, 1 of 2 red. This is QA's M3, which passed in round 1.
- R2-M2 (my own): the password parameter moved *below* the switch, after the
  `else` block. The test fails with "... is not in the branch of `if
  (string.IsNullOrWhiteSpace(externalDb))` where it belongs". Round 1's
  `AssertOnceAfter` would have passed this mutation, because the call still
  sat after the condition text.
- R2-M3 (my own): an unbalanced `{` inside the `if` body's string literal. The
  test fails with `InvalidOperationException: Unbalanced braces in AppHost.cs`.
  That matches the `SplitSwitch` summary
  (`AppHostExternalDatabaseRules.cs:116-121`): drift makes the check fail, not
  pass.

### Round 1 findings

| Finding | Status | Evidence |
|---|---|---|
| CR1 (major) | Resolved | Runbook step 1 (`local-migrations.md:13-18`) now says the stand gets its own container only when `ConnectionStrings:mustask` is set nowhere. Step 3 (`:35-41`) says to export the variable in a different terminal, or to unset it (both the bash and the PowerShell command are given). The "needed always" note (`:47-55`) now applies to `dotnet ef` only and says what a leftover export does. The stronger fix was also taken: each branch prints one `Database:` line as its first statement (`AppHost.cs:110`, `:125-128`). The connection string is never printed. |
| CR2 | Resolved | `src/backend/AGENTS.md:66-71` and `local-migrations.md:86-93` say the secret switches every checkout and worktree, that they all share one database, and that a per-terminal export is the alternative. |
| CR3 | Resolved | The comment at `AppHostExternalDatabaseRules.cs:12-15` and the message at `:60-63` now describe the unresolved-parameter path of Aspire 13.5.1. The anchor is in the claims list below. Not run. |
| CR4 | Resolved | The exact header is pinned and must occur once (`:29`, `:124-130`). The `if` and `else` bodies are separated by brace matching (`:132-149`). Each guarded call must occur once in the file and once in its own branch (`:176-189`). R2-M1 to R2-M3 above confirm it. |
| CR5 | Resolved in `status.md` (`:26`, `:31`) | One copy was left in this file → R2-1. |
| CR6 | Resolved | The lines this branch adds to `AppHost.cs` (`:92-103`, `:110`, `:124-133`) are English. The older Russian comments around them are untouched. |

### Findings

**R2-1 — minor — closed in this round — round 1's CR5 text quoted the
credential it reported.** Round 1's CR5 paragraph repeated the login and
password from `status.md` word for word. After the fix, that quote was the
only copy left in the final tree (I grepped `git diff main...HEAD`).
`review.md` is the reviewer's own file, so I removed the quote in this
round's commit. The rest of CR5 is unchanged, and no code or other document
was edited. As CR5 already noted, the branch's earlier commits still contain
the text.

No other findings: no blocker, major or minor is open.

### Behavioural claims checked (item 6)

Direction 1 — prose the fix adds or changes:
- `AppHost.cs:92-102`, the round-1 comment translated to English with the same claims → the round-1 anchors at their new lines: `:104`, `:108-134`, `:112` (password in the `if` body only), `:114` (the only `Port("Db")`). The ADR-0002 claim is anchored as in round 1.
- `AppHost.cs:102-103`, the `Database:` line names the mode and the switch is "never silent" → `:110` and `:125-128`, the first statement of each branch, neither one conditional. Nothing in `scripts/`, `.claude/` or `src/frontend/e2e/` parses AppHost stdout (grep). The documented start is `dotnet run`, and `aspire run` is not used (`Mustask.AppHost.csproj:6`). Not seen printed (no stand).
- `AppHost.cs:124`, the value is never printed → `:125-128` are fixed strings. `externalDb` is used only at `:104` and `:108`.
- `AppHost.cs:130-132`, Aspire reads `ConnectionStrings:mustask` and passes it on as `ConnectionStrings__mustask` → `Aspire.Hosting.xml:19680-19689` (as in round 1); `:139`.
- `AppHostExternalDatabaseRules.cs:12-15` and `:60-63`, Aspire 13.5.1 reports an unreferenced parameter as unresolved and the dashboard prompts for it → `Aspire.Hosting.xml:20771` (`ParameterProcessor` initializes parameter resources and handles unresolved ones), `:26926` ("Unresolved parameters"), `:2690` (`ValueMissing`), and the "Waiting for user input" string in `Aspire.Hosting.dll`. Not run. This is the round's weakest anchor. The model overload's summary (`:20779-20780`, "collecting dependent parameters") does not say on its own whether a model parameter that nothing references is processed. If it is not, a hoisted parameter is simply unused and the sentence overstates the harm. Nobody would act differently because of it, so it would be minor at most, and I have not raised it.
- `AppHostExternalDatabaseRules.cs:16-18`, an inverted condition or swapped branches start a container or find no connection string → `AppHost.cs:108-134` read with the condition inverted; R2-M1.
- `AppHostExternalDatabaseRules.cs:26-28` and `:116-121`, the header is exact and a brace in a string literal makes the check fail, not pass → `AppHost.cs:110` and `:125-128` contain no braces; R2-M3.
- `AppHostExternalDatabaseRules.cs:176-177`, once in the file and once in the branch → `:180-188`; R2-M2.
- `local-migrations.md:13-18`, the own container only when the key is set nowhere, and the line names the mode → `AppHost.cs:104`, `:108`. Neither of AppHost's `appsettings*.json` files contains `ConnectionStrings` (grep). `:110`, `:125-128`.
- `local-migrations.md:35-41`, with the variable exported there is no container and the API gets this string → `AppHost.cs:104`, `:122-133`, `:139`.
- `local-migrations.md:47-55`, even a broken value left exported switches the stand → `AppHost.cs:108` (any non-blank value).
- `local-migrations.md:86-93` and `src/backend/AGENTS.md:66-71`: one user-secrets store for every checkout; container mode gives each checkout its own volume; a per-terminal export limits the mode to one checkout → `Mustask.AppHost.csproj:5` (a single `UserSecretsId`). On this machine `docker volume ls` shows `<app>.apphost-<hash>-postgres-data` with five different hashes for one app. The environment-variable provider is per process (`AppHost.cs:104`).
- `local-migrations.md:110-111` and `src/backend/AGENTS.md:65-66` → as for `AppHost.cs:102-103`.
- `status.md:39` (the `[dev]` fix line), the list of mutations → two of them were repeated here (the inverted condition, and the moved password parameter, tested as the "below the switch" variant in R2-M2). The removed-`WaitFor` case is checked by test 2 (`:83-84`), confirmed by reading.

Direction 2 — prose outside the fix that describes behaviour the fix changes.
Two behaviours changed: AppHost now writes a `Database:` line to stdout in both
modes, and the guard now pins the branch structure. I grepped `docs/`,
`.claude/`, `scripts/`, `src/frontend/e2e/`, the `AGENTS.md` files and
`src/backend` for `Database:`, `apphost.log`, `AppHost prints`,
`start-up log`, `startup output`, `Distributed application started`,
`AppHostExternalDatabaseRules` and `missing configuration value`. I also
grepped `scripts/` and `.claude/` for `Mustask.AppHost` and `dotnet run`.
Hits:
- `AGENTS.md:67` (take the port from the startup output) — one extra line does not affect it.
- `AppHost.cs:15`, `src/frontend/e2e/support/standFile.ts:75`, `docs/templates/examples/tech-159-e2e-without-password/qa.md:78,88` (Aspire's start banner) — not affected.
- `scripts/keycloak-seed.sh:47` — only a user-secrets hint; it does not parse stdout.
- `status.md:35` (round-1 description of the guard, "exactly once and behind the switch") — still true; the guard is now stricter.
- `src/backend/AGENTS.md:47-48`, "missing configuration value" — pre-existing. This was a round-1 observation, and the developer deferred it on purpose (see Observations below).
- `review.md` (the QA section and round 1) — history.
Not swept: anything beyond these identifiers. The round-1 sweep of the
original change is the baseline and was not repeated.

### Observations (not findings of this diff)

- **The QA re-run is not recorded.** The fix line (`status.md:39`) ends with
  "Ready for QA re-run over the runbook, `AGENTS.md`, `AppHost.cs` and the
  guard test", and no `[qa]` line follows it. This round's builds, the 68/68
  run and the three mutations cover the guard test and `AppHost.cs`. The two
  docs are covered only by the claims list above. Before gate-3, the
  orchestrator should decide whether that replaces the QA re-run the stage
  asks for ("QA is re-run over what was touched").
- **The deferred docs follow-up is not queued.** `status.md:39` defers two
  wordings to "a separate docs follow-up": "missing configuration value"
  (`src/backend/AGENTS.md:47-48`) and the random-port text
  (`MustaskDbContextFactory.cs`, ADR-0002). There is no line for it in
  `docs/backlog.md`, neither on this branch nor on `main`. After the squash,
  the deferral would exist only in the log of a closed task. It needs a queue
  line (`scripts/queue.sh add`).

### Verdict

Approved by AI. CR1-CR6 are resolved, R2-1 was closed in this round, and no
finding is open. The two observations are for the orchestrator to settle
before gate-3.
