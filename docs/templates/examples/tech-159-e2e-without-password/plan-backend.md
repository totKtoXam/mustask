# Plan — backend (tech-159-e2e-without-password)

Stories covered: **US-2** (the stand seeds its own fixture user and client),
**US-3** (the client cannot reach production). Frontend's US-1/US-4/US-6/US-7
consume what this plan fixes but are out of this stage's write zone.

No product API surface changes: this task adds no `Mustask.Api` endpoint, no
domain model, no migration. `api-contract.md` is therefore not created for
this task — noted here per `04-backend.md` §4a.4 instead of left silent.

ADR: `docs/adr/0023-stand-only-keycloak-client-impersonates-the-e2e-fixture-user.md`
(status `proposed`, answers the spec's three ADR questions). This plan
assumes its decision; read it first for the *why*, this file is the *what*
and the *order*. Phase-1 review (2026-09-12): mechanism, placement and the
two-halved guard approved as planned (see `status.md` Log); this revision
adds the three items the review required plus the container-name answer.

## 1. The identifiers this plan fixes (repeated verbatim by `plan-frontend.md`)

| Name | Value | Notes |
|---|---|---|
| Stand-only client's `clientId` | `mustask-e2e` | realm `mustask`, confidential, service account, never in `deploy/keycloak/realms/*.json` |
| Fixture user's `username` | `e2e-fixture` | realm `mustask`, group `/Администраторы` (same group `mustask` uses today — BR-8), no password credential ever |
| Stand file (BR-1) | `.e2e-stand/keycloak-e2e-client.json` | anchor rule and shape fixed in §6 — not repo-root-relative in the naive sense, see below |
| Keycloak container's Docker name | `mustask-e2e-keycloak-{Ports:Keycloak}` | assigned by AppHost, not discovered — §3a |
| Realm `mustask`'s stand-only `accessTokenLifespan` | `120` (seconds) | set by AppHost with the admin credentials, not `mustask-e2e` — D-8 of `status.md`, §11 |

`E2E_USER`'s default in `src/frontend/e2e/support/env.ts` is `'mustask'` today
(confirmed by reading the file) and changes to `'e2e-fixture'` — that edit
is stage 6's, named here so it is not discovered late, per BR-8's "`E2E_USER`
resolves to it."

## 2. Files touched

New:
- `src/backend/Mustask.AppHost/Provisioning/KeycloakStandProvisioner.cs` — the
  provisioning logic itself (§3).
- `src/backend/Mustask.AppHost/Provisioning/KeycloakStandProvisioningOptions.cs`
  (or a record in the same file — decided during coding) — the small set of
  values AppHost.cs passes in (base address, admin credentials, generated
  secret, container name, output file path), kept separate from the HTTP
  logic so the unit tests in §4 construct it without touching
  `AppHost.cs`'s top-level script.
- `src/backend/tests/Mustask.Contracts.Tests/KeycloakStandProvisioningTests.cs` —
  unit tests over `KeycloakStandProvisioner`, no Docker/HTTP host/DB (§4).
- `src/backend/tests/Mustask.Architecture.Tests/StandOnlyKeycloakClientRules.cs`
  — the US-3 scenario-3 guard (§4).
- `src/backend/Mustask.AppHost/Provisioning/KeycloakStandIdentifiers.cs` (QA
  round 1, D-4, §10) — the two identifiers named once, referenced by
  `AppHost.cs` and by the new disjointness test below instead of being
  typed twice.
- `src/backend/tests/Mustask.Contracts.Tests/KeycloakStandDisjointnessTests.cs`
  (QA round 1, D-4, §10) — US-2 scenario 5's regression guard.
- `src/backend/Mustask.AppHost/Provisioning/KeycloakStandClientSecret.cs` (§12)
  — one random value per AppHost process, stable across repeat
  `ResourceReadyEvent` firings.
- `src/backend/tests/Mustask.Contracts.Tests/KeycloakStandClientSecretTests.cs`
  (§12).

Changed:
- `src/backend/Mustask.AppHost/AppHost.cs` — `.WithContainerName(...)` on the
  `keycloak` resource (§3a); the stand file is deleted as the very first
  statement in the file, before any resource is declared (QA round 1, D-2,
  §10); the `ResourceReadyEvent` subscriber logs before/after/on-failure
  (QA round 1, D-3, §10) and calls `KeycloakStandProvisioner` (§3); the
  client secret now comes from one `KeycloakStandClientSecret` instance
  constructed once outside the handler, not generated inside it (§12).
- `src/backend/tests/Mustask.Contracts.Tests/KeycloakStandProvisioningTests.cs`
  — a new two-pass convergence test (§12).
- `src/backend/tests/Mustask.Contracts.Tests/Mustask.Contracts.Tests.csproj` — the
  provisioning source is compiled in via `<Compile Include ... LinkBase>`,
  **not** referenced via `<ProjectReference>` to `Mustask.AppHost.csproj` (QA
  round 1 judgment, §10) — keeps this project's own "no Docker, no HTTP
  host, no DB" charter true at the build level, not only at runtime.
- `src/backend/tests/Mustask.Contracts.Tests/KeycloakRealmRolesTests.cs` — two
  new facts for US-3 scenarios 1–2 (§4).
- `src/backend/tests/Mustask.Architecture.Tests/StandOnlyKeycloakClientRules.cs`
  — a new capability-based fact, `ShippingProject_DoesNotCallKeycloaksAdminApi`
  (QA round 1, D-1, §10); the namespace-based fact's docstring corrected to
  state what it actually catches.
- `.gitignore` (repo root) — a new, explicit pattern for `.e2e-stand/` (§6).
- `docs/adr/0023-...md` — Consequences gains the attributability
  credential-scope finding (QA round 1, §10); Decision item 3 updated for
  the second guard.
- `docs/templates/examples/tech-159-e2e-without-password/status.md` — `## Log` lines.

Not touched by this stage (named so stage 6 does not have to search for
them): `src/frontend/**`, `docs/runbooks/stand.md` (US-8, stage 6),
`scripts/keycloak-seed.sh` (D-4 — untouched by design).

## 3. Provisioning mechanism, and why

**Chosen: an Aspire `ResourceReadyEvent` subscription on the `keycloak`
resource, running in-process inside `Mustask.AppHost`, talking to Keycloak's
admin REST API over plain `HttpClient`.** Confirmed available in the
installed Aspire version (`Aspire.Hosting.dll`, 13.5.1, already restored in
this worktree — `ResourceReadyEvent`/`IDistributedApplicationEventing`
types are present, confirmed by loading the assembly and listing its public
members, not by assuming the sample code compiles).

```csharp
builder.Eventing.Subscribe<ResourceReadyEvent>(keycloak.Resource, async (evt, ct) =>
{
    var secret = RandomNumberGenerator.GetHexString(40);
    using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{keycloakPort}") };
    await KeycloakStandProvisioner.ProvisionAsync(
        http,
        adminUsername: "admin",
        adminPassword: await keycloakAdminPassword.Resource.GetValueAsync(ct),
        clientId: "mustask-e2e",
        clientSecret: secret,
        fixtureUsername: "e2e-fixture",
        keycloakContainerName: keycloakContainerName,     // §3a — literal, not read back
        issuer: $"http://localhost:{keycloakPort}/realms/mustask",
        standFilePath: standFilePath,                     // §6 — anchored via builder.AppHostDirectory
        ct);
});
```

(Illustrative — exact Aspire API surface, e.g. how a `ParameterResource`'s
value is read at this point in the lifecycle, gets nailed down against the
compiler in phase 2, not guessed here.)

Why this over the alternatives named in the spec's ADR question 2:

- **Not a second realm-import file.** `--import-realm` skips a realm that
  already exists (`AppHost.cs`'s own comment, confirmed again by the spec
  and R-1's resolution) — a file merged alongside `mustask-realm.json` cannot
  add a client to realm `mustask` at all. This option does not exist.
- **Not a shell script invoked as a post-start hook.** `scripts/keycloak-seed.sh`
  already is that shape for the human-facing accounts, and D-4/BR-6 keep the
  two paths separate on purpose (different failure mode, different secret
  source, no `MUSTASK_PASSWORD`/`PARTNER_PASSWORD` requirement for this path). A
  second bash script duplicates the curl/JSON plumbing `keycloak-seed.sh`
  already had to work around (UTF-8 charset, percent-encoding group paths,
  body over stdin) instead of reusing anything, and — the deciding point for
  US-3 scenario 3 — a bash script's "reachable only from AppHost" guard has
  no equivalent to an architecture test over C# project/namespace
  references; a `.sh` file is reachable from any shell, whether AppHost
  invokes it or a developer does by hand.
- **Not a separate executable resource in AppHost.** A `builder.AddExecutable(...)`
  resource would need its own health-checked lifecycle, its own way to
  signal "done" back to AppHost, and a place to live outside `Mustask.Api`/
  `Mustask.Infrastructure` anyway — strictly more moving parts than a callback
  on an event AppHost already raises, for no capability this task needs
  (the seeding has no ongoing process to supervise, it runs once per start
  and exits).
- **In-process C# in `Mustask.AppHost` is also what makes the US-3 scenario-3
  guard mechanical**, not aspirational (see the ADR's answer to question 3
  and §4 below): the guard is a source/reference scan over a project graph,
  which only exists for compiled C#, not for a shell script or a
  container's entrypoint.

## 3a. The Keycloak container's name — assigned, not discovered

**Answer to the coordinator's question.** I could not confirm a public,
callable way to read back DCP's own assigned Docker container id/name from
inside a `ResourceReadyEvent` handler: reflecting over the installed
`Aspire.Hosting.dll` (13.5.1) found no `GetContainerIdAsync`-shaped method on
`ContainerResource` or `ResourceReadyEvent` (their full public member lists
were dumped and inspected directly — `ResourceReadyEvent` exposes only
`Resource` and `Services`). The string table does carry a family of
`ContainerId*`/`get_ContainerId` symbols, which is almost certainly what
backs the Aspire dashboard's own "Container Id" property display — but that
is an internal snapshot-property path (the same one `ResourceNotificationService`
feeds the dashboard), not something I found a documented or reflectable way
to await synchronously at this point without more spelunking than this
stage's budget allows, and I would rather say "I didn't find it" than ship
a guess.

**What I found instead is better than reading a generated id back: `Aspire.Hosting.dll`
exposes `ContainerResourceBuilderExtensions.WithContainerName<T>(IResourceBuilder<T>, string)`.**
AppHost can simply *assign* the container's Docker name instead of
discovering it after the fact — which means it is known at the point the
resource is declared, not just at `ResourceReadyEvent` time, with zero
introspection:

```csharp
var keycloakContainerName = $"mustask-e2e-keycloak-{keycloakPort}";

var keycloak = builder.AddContainer("keycloak", keycloakImage, keycloakTag)
    .WithContainerName(keycloakContainerName)
    // ... unchanged from here
```

Using `keycloakPort` (`Ports:Keycloak`, already the per-stand disambiguator
tech-114/116 established for exactly this reason — two stands on one
machine, or two worktrees' stands at once, already have to use different
ports) makes the name both deterministic *and* already unique across
simultaneous stands, which is exactly R-13's unresolved half: today's
`docker ps -a --filter name=keycloak` matches by prefix and takes the first
result because nothing distinguishes one stand's container from another's.
An exact name handed to stage 6 through the stand file (§6) replaces that
prefix guess with an exact match — `docker ps --filter name=^mustask-e2e-keycloak-5010$`
resolves to exactly one container or none, never "the first one found."

**One new risk this introduces, named rather than hidden:** pinning a
container name trades DCP's own collision-avoidance (a fresh random name
every run) for a small chance of a *stale-name* collision — if a previous
`dotnet run --project Mustask.AppHost` was killed hard enough that its own
container cleanup never ran, the old `mustask-e2e-keycloak-{port}` container can
still hold that name on the Docker host, and the next `dotnet run` fails to
create a new one with the same name until the old one is removed
(`docker rm`). This is not a new failure mode in kind — an orphaned
container after an ungraceful AppHost kill is already possible today,
unnamed — only in how it surfaces (a loud name-collision error instead of a
second, differently-named orphan). D-6/US-9's own scope is the E2E suite
aborting mid-run while AppHost stays alive, not AppHost's own process being
killed, so I have not added self-healing for this narrower case (e.g.
`docker rm -f` the name before creating). Flagged for the human to accept or
to ask for explicitly — it is a few lines to add if wanted.

## 4. Tests

All in `src/backend/tests/Mustask.Contracts.Tests/KeycloakStandProvisioningTests.cs`
unless noted, against a fake `HttpMessageHandler` — no Docker, no HTTP host,
no DB:

| Test | Covers | What it would catch |
|---|---|---|
| `ProvisionAsync_FreshRealm_CreatesClientAndFixtureUser` | US-2 scenario 1 (client + user exist), scenario 3 (profile fields, group) | wrong endpoint/verb, a missing profile field, wrong group path |
| `ProvisionAsync_ClientAndUserAlreadyExist_UpdatesInPlace` | US-2 scenario 2 (re-seeding is harmless) | a provisioning pass that only knows how to create, breaking on the second `dotnet run` |
| `ProvisionAsync_NeverCallsResetPassword` | US-2 scenario 3 (no password credential at all), BR-4 | a future edit that adds a password "for convenience" |
| `ProvisionAsync_WritesTheStandFile_WithIssuerClientAndContainerName` | US-2 scenario 1 (secret + file), BR-1, and the two review additions (issuer, container name — §6) | wrong path, wrong JSON shape, a missing field the suite needs to detect staleness or resolve the container |
| `KeycloakRealmRolesTests.ProductionRealmImport_ClientAllowlistIsClosed` (new fact, extends the existing class) | US-3 scenario 1 | a new client added to either realm-import file without updating the allowlist — including `mustask-e2e` ending up there by mistake |
| `KeycloakRealmRolesTests.ProductionRealmImport_NoClientHasServiceAccountEnabled` (new fact) | US-3 scenario 2 | any production client turning on a service account, by any name, for any reason |
| `Mustask.Architecture.Tests.StandOnlyKeycloakClientRules.ShippingProject_DoesNotReferenceAppHost` | US-3 scenario 3, first half | a `ProjectReference` from `Mustask.Api`/`Mustask.Infrastructure`/`Mustask.Application`/`Mustask.Domain`/`Mustask.Shared` to `Mustask.AppHost` (already caught today by `OnionDependencyRules.Layer_references_only_allowed_projects`, since `Mustask.AppHost` is not in any layer's allowlist — this test restates it by name against BR-2/US-3 so the connection is not left implicit) |
| `Mustask.Architecture.Tests.StandOnlyKeycloakClientRules.ShippingProject_DoesNotUseTheStandOnlyProvisioningNamespace` | US-3 scenario 3, second half | the admin-API-call logic copy-pasted into a shipping project instead of referenced from `Mustask.AppHost` — a risk a project-reference check alone cannot see |
| `Mustask.Architecture.Tests.StandOnlyKeycloakClientRules.StandOnlyProvisioner_ExistsInAppHost_UnderTheExpectedNamespace` (not-vacuous guard, same idiom as `RulesAreNotVacuous`) | — | the previous test passing for the wrong reason (the class renamed or moved, the scan matching nothing) |
| `Mustask.Architecture.Tests.StandOnlyKeycloakClientRules.ShippingProject_DoesNotCallKeycloaksAdminApi` (QA round 1, D-1, §10) | US-3 scenario 3, closes the gap the namespace check left | an independent reimplementation of the client-creation call under any name/namespace, not just a reference to `Mustask.AppHost.Provisioning` — proven red against QA's own `SneakyKeycloakClientCreator` reproduction, §10 |
| `Mustask.Contracts.Tests.KeycloakStandDisjointnessTests.KeycloakSeedScript_NeverMentionsTheE2EStandIdentifiers` (QA round 1, D-4, §10) | US-2 scenario 5 | `scripts/keycloak-seed.sh` gaining a line that touches `mustask-e2e`/`e2e-fixture` |
| `Mustask.Contracts.Tests.KeycloakStandDisjointnessTests.E2EFixtureUsername_IsNotAnyHumanFacingUsername` (QA round 1, D-4, §10) | US-2 scenario 5 | `KeycloakStandIdentifiers.FixtureUsername` drifting onto `mustask`/`partneradmin`/`inspector`/`operator` |

Every test above was actually driven red before green (not just designed
that way) — see §8 for what broke each one and how it was fixed, including
one real bug the exercise caught in the guard itself
(`StandOnlyProvisioner_ExistsInAppHost...` originally matched
`KeycloakStandProvisioner` as a substring of a renamed class, passing
vacuously — fixed to a word-boundary match before being trusted). §10
records the same discipline applied to the QA-round fixes, including one
guard (`ShippingProject_DoesNotCallKeycloaksAdminApi`) proven red against
the specific evasion that got past its predecessor, not only against an
obvious break.

**Confirmed live, not simulated (`dotnet run --project src/backend/Mustask.AppHost`,
self-started and self-stopped per D-6, 2026-09-12) — because it needs a real
Keycloak container, which none of `Mustask.Contracts.Tests`'s existing tests
bring up, and adding that would mean either a new NuGet package
(`Directory.Packages.props` — orchestrator-only) or a new Testcontainers
image download cost for every `dotnet test` run.** This is the one-time
manual check §7 accepts as the trade for not automating it; the results
below are what backend QA's own pass should reproduce, not take on trust:

- a `client_credentials` token was obtained for `mustask-e2e` with the exact
  secret the stand file held (US-2 scenario 1's second `And`) — confirmed;
- the fixture user completed the profile/credentials/groups checks over the
  live admin API: `credentials: []` (BR-4, no password at all),
  `enabled`/`emailVerified`/`email`/`firstName`/`lastName` all present, no
  pending required actions, group membership exactly `/Администраторы`
  (US-2 scenario 3, BR-8) — confirmed;
- the stand file was untracked after the stand start (`git status --porcelain .e2e-stand`
  — empty output; `git status --ignored` shows it `!!`, i.e. matched by the
  new `.gitignore` pattern, not merely absent) — confirmed (US-2 scenario 4);
- an impersonation call against `e2e-fixture`, authenticated as `mustask-e2e`'s
  own service account, returned `200 OK` with `Set-Cookie: KEYCLOAK_IDENTITY=...`
  and `KEYCLOAK_SESSION=...` scoped to `/realms/mustask/` — the literal
  browser-facing SSO cookie pair ADR-0023 is betting the whole mechanism on
  — confirmed;
- **the attributable event is in the realm's `/events` log, not
  `/admin-events`** — `GET /admin/realms/mustask/events` returned
  `{"type":"IMPERSONATE", ..., "details":{"impersonator_realm":"mustask","impersonator":"service-account-mustask-e2e"}}`.
  Worth recording precisely because the NFR text says "event log" without
  naming which of Keycloak's two logs — `/admin-events` (administrative
  changes: the client/user/role-mapping/group-membership CREATE/UPDATE
  events from provisioning itself, all present too) does **not** carry the
  impersonation event; `/events` (authentication/session events) does. QA's
  check belongs on `/events?type=IMPERSONATE`, not `/admin-events` — this
  was not obvious before running it and would have cost a QA cycle to
  discover otherwise;
- the container name assigned in §3a resolved to exactly one real, running
  container (`docker ps --filter name=keycloak` showed `mustask-e2e-keycloak-5010`,
  `docker ps -a` empty again after AppHost's own shutdown removed it) —
  confirmed; AppHost's shutdown (`Stop-Process` on `Mustask.AppHost.exe`) tore
  down every container and DCP process without anything left orphaned.

## 5. Known limitation: provisioning has no repository-level regression test

Named explicitly rather than left implicit. §4's unit tests protect the
*shape* of the requests `KeycloakStandProvisioner` sends (right endpoint,
right verb, right JSON, no password ever set) against a fake handler, and
the architecture tests protect *where the code may live* — but nothing in
`scripts/verify.sh` or `dotnet test Mustask.sln` proves the provisioning still
*works against a real Keycloak* after some later change (a Keycloak image
upgrade changing an endpoint's shape, an admin-API response format change,
a typo in a URL that every fake-handler test still matches because the fake
was written against the same typo). The only net that would actually catch
that is the E2E suite itself — and `src/frontend/e2e` is deliberately not
part of `scripts/verify.sh` (tech-119's own decision, `src/frontend/AGENTS.md`
§ E2E: "needs a live, seeded stand ... it is **not** part of `npm test` or
`scripts/verify.sh`"). So: a future change can break provisioning with
`verify.sh` fully green, and the first evidence would be an E2E run failing
at global setup — which per this task's own point (US-1) an agent can now
run and read the report of, but only when someone actually runs it (D-2:
the E2E question is still asked per task, tech-119 D-32). This is the trade
named in §7 made concrete: closing it for real is `Testcontainers.Keycloak`
plus a `Directory.Packages.props` entry, which is the orchestrator's call,
not this stage's to take unilaterally.

## 6. The stand file, its anchor, and `.gitignore`

**The literal contract `plan-frontend.md` must repeat, in full — name,
anchor, and shape:**

- **Path**: `.e2e-stand/keycloak-e2e-client.json`, resolved as a **subpath of
  each side's own repository root**, computed from a location fixed at
  compile/module time — never from the process's current working directory,
  because `dotnet run --project src/backend/Mustask.AppHost` and `npm run e2e`
  (from `src/frontend`) start with different cwds, and a cwd-relative path
  would silently resolve to two different places.
  - **AppHost side** (already absolute, Aspire-provided):
    `Path.Combine(builder.AppHostDirectory, "..", "..", "..", ".e2e-stand", "keycloak-e2e-client.json")`
    — three levels up from `src/backend/Mustask.AppHost` reaches the repo root.
  - **Suite side** (stage 6, fixed here so it is not reinvented per file):
    resolve from the *module's own file location*, not `process.cwd()` —
    e.g. `path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '.e2e-stand', 'keycloak-e2e-client.json')`
    from a file at `e2e/support/<name>.ts` (four levels up from
    `src/frontend/e2e/support` reaches the repo root — one level deeper than
    AppHost's three, because the suite's file sits one directory deeper
    under `src/`).
  - Both sides therefore resolve to `<this checkout's own repo root>/.e2e-stand/keycloak-e2e-client.json`
    — **automatically per-worktree**, since each worktree is its own
    checkout with its own `builder.AppHostDirectory`/`import.meta.url`, with
    no shared machine-wide location and nothing to namespace by hand. This
    is the fix for "several worktrees run their own stands side by side"
    the review named: it falls out of the anchor rule, not a separate
    mechanism.
- **Shape** — every field earns its place by a concrete failure it exists to
  catch:

  ```json
  {
    "issuer": "http://localhost:5010/realms/mustask",
    "clientId": "mustask-e2e",
    "clientSecret": "<generated at this stand start>",
    "keycloakContainerName": "mustask-e2e-keycloak-5010"
  }
  ```

  - `clientId` — not itself secret, included so the suite has no second
    place to hard-code it.
  - `clientSecret` — the one value with no other channel (BR-1).
  - `issuer` — **the staleness guard the review asked for.** The container
    has no volume: a new stand means a new secret, but a leftover file from
    a previous stand (or, since the file lives inside the checkout, one
    accidentally left after switching branches without a fresh stand start)
    looks byte-for-byte like a valid one. The suite already knows the
    expected issuer from its own `E2E_KEYCLOAK_URL`/realm (`env.ts`:
    `${keycloakUrl}/realms/mustask`); comparing it against this field before
    ever attempting a token request turns a `unauthorized_client`/secret
    mismatch three specs in — the generic, hard-to-place failure US-5 is
    written to prevent — into "this stand file belongs to another stand
    (expected `.../mustask`, found `.../partner`)" at global setup, naming the
    one cause US-5 requires.
  - `keycloakContainerName` — §3a's answer to the container-resolution
    question, so US-7's resolver has an exact name instead of a prefix
    guess.

  The fixture username is **not** in this file: it is not secret, and its
  value (`e2e-fixture`) is fixed by §1, a literal stage 6 hard-codes as
  `E2E_USER`'s new default the same way `'mustask'` is today — one more field
  here would be one more thing that could silently drift from §1 instead of
  being read from it.

`.gitignore` (repo root) gains:

```
# tech-159: the stand-only Keycloak client's secret, generated by AppHost
# at every stand start (D-5) — never committed, never human-set. Written by
# Mustask.AppHost, read by src/frontend/e2e's global setup.
/.e2e-stand/
```

Anchored at the repo root (`/.e2e-stand/`), not a bare `.e2e-stand/`,
because nothing else in the tree is expected to create a same-named
directory elsewhere, and an anchored pattern is easier to verify by reading.
Backend QA additionally runs `git status --porcelain .e2e-stand` after a
stand start and records the (empty) output in `qa.md`, per US-2's own
acceptance criterion — an assumption is not the same thing as a checked
fact.

## 7. What I am not doing, and why (things worth a second opinion)

- **No new test project (`Mustask.AppHost.Tests`).** `Mustask.sln` is
  orchestrator-only (`docs/conventions/git.md` line 287, confirmed).
  `Mustask.Contracts.Tests` already exists, is already wired into the solution,
  and its own header comment already promises exactly the shape of test
  this plan adds (no Docker, no HTTP host, no DB). The provisioning source
  compiles into it via a `<Compile Include ... LinkBase>` item (§10, after
  backend QA measured what a `ProjectReference` to `Mustask.AppHost.csproj`
  actually cost) — a `.csproj` edit to an existing test project either way,
  not a write-zone conflict; adding a project to `Mustask.sln` would be.
- **No `Testcontainers.Keycloak` / new `Directory.Packages.props` entry.**
  Confirmed orchestrator-only and confirmed there is a queued task for test
  infrastructure changes (`tech-156`) that this stage does not fold itself
  into. The consequence is §5's named regression gap, not a silent
  omission.
- **No new AppHost resource, port, or container.** The provisioning runs
  in-process against the existing `keycloak` resource; nothing new appears
  in `docs/runbooks/stand.md`'s port/process tables. What stage 6
  (US-8) *will* need to add there: the stand file's path, anchor rule and
  `.gitignore` coverage (§6), the fixture username replacing `mustask` in the
  E2E section, the container-name field replacing the prefix-match
  resolver, and the removal of `E2E_PASSWORD`/`E2E_KEYCLOAK_ADMIN_PASSWORD`
  from the environment-variable table (D-7).

## 8. Order of work, and the red seen before each green

Recorded here as the work happens, not reconstructed afterward:

1. `KeycloakStandProvisioner` + its unit tests (§3, §4) — red, then green,
   against a fake `HttpMessageHandler`. No AppHost wiring yet.
2. Wire it into `AppHost.cs` via `.WithContainerName` (§3a) and the
   `ResourceReadyEvent` subscription (§3); manual smoke check with a live
   `dotnet run --project src/backend/Mustask.AppHost` (self-started,
   self-stopped per D-6) — confirms the Aspire eventing API used in §3
   compiles and actually fires against this Aspire version, and that the
   named container actually comes up.
3. `.gitignore` entry; confirm `git status --porcelain .e2e-stand` is empty
   after the smoke run in step 2.
4. `KeycloakRealmRolesTests.cs` additions (US-3 scenarios 1–2).
5. `StandOnlyKeycloakClientRules.cs` (US-3 scenario 3) — each fact
   temporarily inverted first to see it fail, per `RulesAreNotVacuous`'s own
   discipline, then restored.
6. `dotnet build Mustask.sln` + `dotnet test Mustask.sln` green; `status.md` Log
   entry; hand off to backend QA.

## 9. Open items for the human (gate-3, alongside the ADR)

- The identifiers in §1 (`mustask-e2e`, `e2e-fixture`, the container name
  pattern) and the stand file contract in §6 are this plan's own choices,
  not spec-mandated literals — approved once already (see `status.md` Log);
  this revision's additions (issuer, container name fields; the anchor
  rule) are new since that approval and worth a second look.
- ~~§3a's stale-container-name risk~~ — **superseded by backend QA's live
  reproduction (§10, D-2).** The predicted failure (a name collision on the
  next `dotnet run`) did not happen: DCP silently removes the orphan and
  recreates the container under the same name. What QA found instead was a
  timing gap, fixed in §10.
- §7's two "not doing this" calls trade a live, Docker-backed regression
  test for a QA-time manual check plus the named gap in §5. If that trade is
  unacceptable, the fix is an orchestrator-mediated
  `Directory.Packages.props`/`Mustask.sln` change (or pulling `tech-156`
  forward), not something this stage can route around on its own.

## 10. Backend QA round 1 (`qa.md`, commit `46c6422`) — defects and fixes

Verdict was `fail`: 2 major, 2 minor, 0 blockers. This is defect round 1 of
the two `04-backend.md` allows.

**D-1 (major) — the namespace guard could be walked around.** QA's own
reproduction: a class under an unrelated name/namespace in `Mustask.Api/Features/`
reimplementing the client-creation POST, never mentioning
`Mustask.AppHost.Provisioning`. All 11 architecture facts stayed green. Fixed by
guarding the *capability*, not the name, per the coordinator's own
suggestion — verified first, not assumed: grepping `Mustask.Api` and every inner
layer for `admin/realms`/`/admin/` returns nothing today (`Mustask.Api`'s only
Keycloak traffic is discovery/JWKS, the token endpoint, and
`end_session_endpoint`). New fact,
`StandOnlyKeycloakClientRules.ShippingProject_DoesNotCallKeycloaksAdminApi`,
scans the same five shipping projects for the literal `/admin/realms/` —
the fragment every one of Keycloak's admin endpoints starts with, canonical,
not a guess. **Proven red against the actual evasion**, not just the
obvious break: QA's exact reproduction (a `SneakyKeycloakClientCreator`
class under `Mustask.Api/Features/`, POSTing to `/admin/realms/{realm}/clients`
with `serviceAccountsEnabled: true`, no mention of the provisioning
namespace) was recreated, ran red on the new fact while the namespace fact
stayed green (confirming the two checks are independent, not one masking
the other), then deleted (`git status --porcelain src/backend/Mustask.Api`
empty afterward). The namespace check's docstring is also corrected — it
now says what it actually catches (a reference or a copy that still names
its origin), not the stronger claim QA found it couldn't back up.

**D-2 (major) — a stale stand file could look valid for up to ~90 s after a
hard kill.** QA reproduced live: force-killing AppHost leaves the
pinned-name Keycloak container orphaned; the next `dotnet run` recovers
silently (DCP removes and recreates it — §3a's own predicted failure, a name
collision, did **not** happen), but Aspire's "Distributed application
started" banner prints well before the fresh container's health check (and
therefore `ResourceReadyEvent`, and therefore provisioning) fires — cold
JVM start plus realm re-import took ~65 s in QA's run. During that whole
window the on-disk file held the *previous* run's secret: same shape, same
`issuer` (same stand, same port), rejected by the new instance. Fixed by
deleting the stand file as the very first statement in `AppHost.cs` —
before any resource is even declared, computed once from
`builder.AppHostDirectory` and reused (not recomputed) by the provisioning
subscriber, so the two can never name two different paths. **Verified
live**, reproducing QA's own steps: force-killed AppHost mid-run (all 7
containers, including the pinned-name Keycloak one, survived orphaned,
confirmed via `docker ps`), started AppHost again, and polled the file every
second — **absent** for the whole window (not merely stale), reappearing
only once Keycloak's health check passed, with a genuinely new secret
(confirmed by string comparison against the pre-kill value) and a fresh
container id. A suite that reads too early now sees "no stand file," which
US-5 already turns into one named cause, instead of a secret that looks
valid and fails silently. This is now stage 6's contract to design against:
`global-setup.ts` must poll/retry against the file's *content* (e.g., retry
a token request until it succeeds), not treat file-existence or AppHost's
own console banner as sufficient — flagged for the human at gate-3 as a
requirement on the frontend stage, not merely a note.

**D-3 (minor) — provisioning had no log line.** Fixed: an `ILogger` (from
`@event.Services`, the same DI container Aspire's own host uses) logs one
line before provisioning starts and one on success, both naming the client
id and fixture username; a failure is logged with the exception and
rethrown, never swallowed, and the generated secret is never part of any
log line. Verified live — both lines appear in the AppHost console output
on every run, including immediately before the D-2 fix's delete-then-refill
cycle, which is exactly what made the timing gap visible enough to record
precisely in this document.

**D-4 (minor) — the manual/automatic seed disjointness (US-2 scenario 5)
had no regression guard.** Fixed with two new facts,
`Mustask.Contracts.Tests/KeycloakStandDisjointnessTests.cs`: `scripts/keycloak-seed.sh`
never mentions the E2E identifiers (`mustask-e2e`/`e2e-fixture`), and the fixture
username is never equal to any of the four human-facing usernames. The
reverse direction — grepping the provisioning code for the human-facing
usernames — is not implemented as a blind substring scan: `"mustask"` is also
the first three letters of every namespace in this repository
(`Mustask.AppHost`, `Mustask.Domain`, ...), so that scan would fail on its own
`using`s, not on a real regression (confirmed by deliberately trying it as
part of proving the actual guard red — see below). Instead, both identifier
literals were promoted to one named place,
`Mustask.AppHost/Provisioning/KeycloakStandIdentifiers.cs`, and `AppHost.cs`
was changed to reference them instead of repeating the strings inline, so
the disjointness test compares the one authoritative value, not a
hand-typed copy that could drift from what `AppHost.cs` actually uses. Both
facts proven red first: temporarily appending `e2e-fixture mustask-e2e` to
`keycloak-seed.sh` failed the first fact (reverted, `git checkout --`
confirmed clean); temporarily setting `KeycloakStandIdentifiers.FixtureUsername`
to `"mustask"` failed both facts at once (the second, as intended; the first as
an honest, correctly-triggered side effect, since the seed script
legitimately mentions "mustask" everywhere once that literal becomes a real
overlap) — reverted, tests green again.

**Two non-defect findings, addressed:**

- **Attributability's readable-by-whom answer.** QA confirmed the plan's
  own finding (`IMPERSONATE` in `/events`, absent from `/admin-events`) and
  additionally found `mustask-e2e`'s own service-account token gets `403` on
  both endpoints (missing `view-events`) — only a realm-admin-level
  credential can read the trail today. Added to ADR-0023's Consequences as
  one paragraph: the NFR is still met (the admin API can retrieve the
  event), but the suite cannot self-verify it with its own credential.
- **The `Mustask.Contracts.Tests` → `Mustask.AppHost` `ProjectReference`.** QA
  measured 43 MB of build output against 5.9 MB for
  `Mustask.Architecture.Tests`, a comparable sibling with no such reference,
  because the reference pulled `Aspire.Hosting.AppHost`/`.PostgreSQL`/`.JavaScript`
  into a project whose own header promises "no Docker, no HTTP host, no DB"
  — true at runtime (only a fake handler ever ran) but not at the
  build/dependency-graph level. **Decision: switched to a
  `<Compile Include="..\..\Mustask.AppHost\Provisioning\*.cs" LinkBase="Provisioning" />`
  item, removing the `ProjectReference` entirely.** This was QA's own
  suggested alternative and I agree with the reasoning: the two files have
  zero Aspire dependency of their own, `Mustask.AppHost.csproj`'s default
  globbing still compiles the identical source into `Mustask.AppHost.exe`
  separately (a second, independent compilation, not a copy that can
  drift), and the fix needs no new package and no `Mustask.sln` change — it
  stays inside a test project's own `.csproj`, already this stage's write
  zone. Measured after the change: `Mustask.Contracts.Tests`'s build output
  dropped from 43 MB to 20 MB (the residual is `Mustask.Api`'s own,
  already-legitimate reference to Npgsql's Aspire integration, unrelated to
  this fix and present before it too) — `Aspire.Hosting.AppHost`,
  `.PostgreSQL` and `.JavaScript` are gone from the output entirely. All 48
  facts in `Mustask.Contracts.Tests` still pass unchanged.

**Where I disagree with nothing in the report.** Every defect and both
non-defect findings held up under my own re-verification; I have no
correction to offer QA.

## 11. D-8 (`status.md`) — AppHost sets the stand's `accessTokenLifespan`

Specs 6, 9 and 11 each need a short-lived access token to observe a renewal
without waiting out the real 900s default, and each used to shorten
`accessTokenLifespan` through `mustask-e2e`'s own token — which 403s, since that
write needs `manage-realm`/`view-realm`, roles `mustask-e2e` deliberately does
not hold. The human's decision (D-8): do not widen the client: **AppHost
sets the realm's `accessTokenLifespan` once, at every stand start, with the
Keycloak administrator credentials `KeycloakStandProvisioner` already holds
for creating the client and user** — the same admin token from
`GetAdminTokenAsync`, no new secret, no new credential.

**The value: 120 seconds**, in
`Mustask.AppHost/Provisioning/KeycloakStandIdentifiers.AccessTokenLifespanSeconds`
— not re-derived per call site. This is the value `plan-frontend.md`'s specs
already hardcoded before this change (`09-token-renewal-two-tabs.spec.ts`,
`11-session-ended-by-admin.spec.ts`: `setAccessTokenLifespan(..., 120)`), so
no calibration changes on the frontend side — only *who* sets it moves.
**Said loudly, since the suite's waits are calibrated to it**: 120 is long
enough that the panel's 60s renewal lead (`RENEW_LEAD_MS`,
`shared/api/session.ts`) still leaves a real window before expiry, and short
enough that a spec doesn't wait minutes for a renewal. Changing this number
is a contract change with the frontend stage, not a free tuning knob — if a
future change needs a different value, it goes through both plans, not one.

**Mechanism**: `KeycloakStandProvisioner.SetAccessTokenLifespanAsync`, called
first in `ProvisionAsync` (before the client/user steps, order does not
matter functionally but this reads as "the realm is configured, then the
identities in it are"). Get-modify-put on the full realm representation, not
a partial body — the same caution `e2e/support/keycloakAdmin.ts`'s own
header comment already recorded (a partial `PUT /admin/realms/{realm}`
replaces the whole object and would silently wipe clients, themes and
everything not named in the body). Idempotent, like the rest of
provisioning: safe to run on every stand start.

**The role-set guard — added, not skipped.** The human's condition was
explicit: `mustask-e2e` stays at exactly `impersonation` + `manage-users`, never
`view-realm`/`manage-realm`/`realm-admin`. This was already an *incidental*
guarantee of the existing fresh-realm unit test (it asserts the granted-role
set equals exactly those two names), but I added a second, dedicated fact,
`ProvisionAsync_NeverGrantsMoreThanImpersonationAndManageUsers`, naming the
three forbidden roles explicitly and standing on its own — cheap (a dozen
lines), and it means a future change that widens the grant fails a test
whose name states the human's decision, not one whose name is about
something else entirely. Proven red first: temporarily added `"view-realm"`
to `ImpersonationRoles` in `KeycloakStandProvisioner.cs` — failed (the fake
handler's own role list doesn't recognize it either, so the failure is an
`InvalidOperationException` naming the unknown role rather than a bare
assertion mismatch, which is at least as strong a signal); reverted,
green again.

**No realm `partner` change, no `keycloak-seed.sh` change** — this touches
only `KeycloakStandProvisioner.SetAccessTokenLifespanAsync`'s call against
realm `mustask`, using the `options.Realm` the rest of the class already scopes
to.

**Live-verified** on a self-started, self-stopped stand (D-6), 2026-09-14:

- fresh start → `GET /admin/realms/mustask` (master admin token) reports
  `accessTokenLifespan: 120`;
- `mustask-e2e`'s live role-mapping on `realm-management` is exactly
  `impersonation` + `manage-users` — confirmed via the admin API, not
  inferred from the code;
- every other realm field checked (`bruteForceProtected`,
  `ssoSessionMaxLifespan`, `registrationAllowed`, `revokeRefreshToken`)
  survived the get-modify-put unchanged;
- graceful stop, fresh start again (new container, realm re-imported from
  `deploy/keycloak/realms/mustask-realm.json` at its committed `900`) →
  `accessTokenLifespan` is `120` again after provisioning — it survives a
  restart because provisioning re-applies it every time, not because
  anything persists across the container's own recreation (no volume, as
  always).

**The deviation for the runbook** (`docs/runbooks/stand.md`,
frontend developer's US-8 edits — routed by the coordinator, not edited
here to avoid two sessions on one file): the stand's access token now lives
~2 minutes instead of the production 15, so the panel silently renews every
couple of minutes during manual exploration on the stand — harmless, just a
fact worth knowing before someone reads network traffic and wonders why a
token that should last 15 minutes doesn't. Suggested sentence: **"The stand's
Keycloak realm carries a 120-second `accessTokenLifespan` instead of the
production 900 (tech-159, D-8) — AppHost sets it at every start so E2E
specs 6/9/11 can observe a token renewal without widening the E2E client's
rights, and the side effect is that the panel renews its own access token
roughly every two minutes during manual work on this stand, not every
fourteen."**

## 12. The secret must not rotate under a running suite (live finding, 2026-09-15)

While re-running the Docker-restarting specs back to back, the frontend
developer found that a bare `docker stop`/`docker start` of the `keycloak`
container makes DCP re-fire `ResourceReadyEvent` on that resource repeatedly
for up to ~30s. `ClientSecret = RandomNumberGenerator.GetHexString(40)` sat
*inside* the event handler, so every firing minted a fresh secret and
re-provisioned `mustask-e2e` with it — rotating the live client's secret out
from under a stand file the suite had already read (`unauthorized_client`,
caught live by two separate specs). The frontend made the consumer
resilient (`standFile.ts`'s bounded re-read retry) but correctly declined to
patch this side, since the rotation itself was never a design decision.

**Fix: `KeycloakStandClientSecret`** (`Mustask.AppHost/Provisioning/`) — one
random value, generated lazily on first read via `Lazy<string>`, stable for
the lifetime of the instance. `AppHost.cs` now constructs exactly one
instance per process, next to `keycloakContainerName` (same "computed once,
reused by every firing" shape `standFilePath` already established for
D-2), and the event handler reads `.Value` instead of generating anew.
Provisioning stays idempotent in the sense that matters: the client
converges to the *same* state on every repeat firing within one run; only a
fresh AppHost *process* gets a fresh secret. D-5 is otherwise untouched —
generated, never human-chosen, never logged, written only to the stand
file — and the stand file is still deleted at AppHost start (D-2), so a new
run is never mistaken for an old one.

**The other provisioning steps, checked as asked, not assumed:**

- **`SetAccessTokenLifespanAsync` (D-8):** convergent. Get-modify-put with
  the same literal (`120`) every firing lands on the same value; there is no
  "duplicate" shape for a scalar field to accumulate.
- **The realm-management role grant:** convergent, confirmed live, not just
  reasoned about — after the repeat firing below, `mustask-e2e`'s service
  account still held *exactly* `impersonation` + `manage-users`, not four
  entries or an error. Keycloak's role-mappings POST is an add-to-a-set
  operation; granting an already-held role a second time is a no-op, not a
  duplicate or a conflict.
- **The fixture user (create/update) and its group membership:** convergent
  by construction (create-or-update by natural key, then an unconditional
  PUT of the same profile; group membership is a PUT to a specific
  membership resource, idempotent by HTTP semantics) and confirmed live: one
  group (`/Администраторы`, not two), `credentials: []` still empty after
  the repeat firing.

**Nothing else was found non-idempotent.** The single defect was the secret;
everything else in `ProvisionAsync` was already convergent by the shape it
was written in (US-2 scenario 2, "re-seeding on every start is harmless"),
which is exactly why only the secret rotated visibly and nothing else broke
during the frontend's own testing.

**Tests** (`Mustask.Contracts.Tests`, fake handler, no Docker):
- `KeycloakStandClientSecretTests.Value_IsStableAcrossRepeatedReads` /
  `Value_DiffersBetweenTwoInstances` — the unit that actually carries the
  fix. Proven red first: temporarily made `Value` regenerate on every read
  (bypassing the `Lazy<string>`) — failed with two different hex strings
  where one was expected; reverted.
- `KeycloakStandProvisioningTests.ProvisionAsync_CalledTwiceWithinOneRun_ConvergesWithoutChangingTheSecret` —
  calls `ProvisionAsync` twice with the same options and asserts the create
  (POST) and update (PUT) bodies carry the identical secret, plus that the
  role grant, group membership and realm-lifespan write are each attempted
  a second time without throwing. Proven red first by a targeted mutation:
  temporarily passed a *different* `ClientSecret` on the second call (the
  exact shape of the old bug) — failed on the expected assertion; reverted.
  Caveat stated plainly: this test proves the provisioner *class* handles a
  stable-secret repeat correctly (it always did — the class never generated
  its own secret); it does not by itself guard `AppHost.cs`'s wiring
  choice of *where* the secret is generated. That guard is
  `KeycloakStandClientSecretTests` plus the live check below; I did not
  find a proportionate way to unit-test top-level `Program` statements in
  this codebase without a disproportionate refactor, and said so rather
  than skip the question.

**Live-verified with the exact sequence that found the bug** (self-started/
stopped stand, D-6, 2026-09-14): started the stand, recorded the stand
file's secret, `docker stop mustask-e2e-keycloak-5010` then `docker start
mustask-e2e-keycloak-5010`, waited ~55s. `ResourceReadyEvent` fired a second
time (confirmed via the provisioning log lines), and the stand file's
secret was **byte-for-byte unchanged** across the restart. Confirmed the
unchanged secret still authenticates (`client_credentials` grant
succeeded), `accessTokenLifespan` was still `120`, the role mapping was
still exactly the two roles, and the fixture user still had exactly one
group and no credentials — the full convergence claim above, not only the
secret.

**For the coordinator, not a change I made:** the frontend's `standFile.ts`
retry (30s/2s bounded re-read) is worth keeping regardless of this fix — it
also covers the ordinary D-2 window (a fresh stand start, before the first
provisioning pass ever completes) that this change does not touch. Nothing
about it needs simplifying; if anything, its purpose narrows cleanly to
"the file does not exist *yet*" now that "the file exists but is about to
be replaced by a different secret" can no longer happen within one run.
