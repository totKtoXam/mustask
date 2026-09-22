# QA — tech-159-e2e-without-password

## Backend

The test plan below was drawn up **before** reading the implementation —
sources: `spec.md` (index, BRs, NFRs), `stories/US-2-stand-seeds-fixture-and-client.md`,
`stories/US-3-client-stays-out-of-production.md`, and `status.md` §Decisions
(D-1..D-7). `plan-backend.md` and ADR-0023 were read only afterward, per
`04-backend.md`. US-1/US-4..US-9 belong to stages 6/7 and are out of this
section's scope except where a backend-observable fact bears directly on
them (noted inline).

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|---|---|---|---|
| US-2 / scenario 1 (fixture user + stand-only client exist after a fresh start; token obtainable with the secret AppHost wrote) | `KeycloakStandProvisioningTests.ProvisionAsync_FreshRealm_CreatesClientAndFixtureUser` (fake handler) + live: `client_credentials` token obtained from a fresh stand with the stand file's secret | unit + manual (live stand) | ☑ |
| US-2 / scenario 2 (re-seeding on every start is harmless) | `ProvisionAsync_ClientAndUserAlreadyExist_UpdatesInPlace` (fake handler, exercises the PUT/update branch) + live: stop/start cycle, secret rotates, old secret rejected (401) on the new instance | unit (full) + manual (**create branch only**, see note below) | ☑ (with a coverage gap noted below) |
| US-2 / scenario 3 (rights equivalent to `/Администраторы`, `E2E_USER` resolves to the fixture, no password credential, profile complete, no pending required actions) | `ProvisionAsync_FreshRealm_CreatesClientAndFixtureUser` (profile fields) + `ProvisionAsync_NeverCallsResetPassword` + live: `GET .../credentials` → `[]`, `GET .../groups` → exactly `/Администраторы`, `requiredActions: []`, `enabled`/`emailVerified`/`email`/`firstName`/`lastName` all present | unit + manual (live stand) | ☑ |
| US-2 / scenario 4 (stand file named once, `.gitignore` covers it, untracked after a start) | `ProvisionAsync_WritesTheStandFile_WithIssuerClientAndContainerName` (shape) + live: `git status --porcelain .e2e-stand` empty, `git status --ignored` shows `!! .e2e-stand/` | unit + manual (live stand) | ☑ |
| US-2 / scenario 5 (manual `keycloak-seed.sh` and the automatic seed touch disjoint accounts, don't fight) | none (see D-4 below) — verified by reading `scripts/keycloak-seed.sh`: `seed_user` is called only for `mustask`/`partner`/`inspector`/`operator`, never for `e2e-fixture`; group membership is only ever added (`PUT`), never replaced/cleared, so a manual run cannot strip `e2e-fixture`'s group even by accident (it never touches that user's id at all) | manual (code reading) | ☑ (no regression guard — D-4) |
| US-3 / scenario 1 (closed `clientId` allowlist over the production realm-import files) | `KeycloakRealmRolesTests.ProductionRealmImport_ClientAllowlistIsClosed` — broken deliberately (added `mustask-e2e` to `mustask-realm.json`) and confirmed red for the right reason, then reverted (`git status --porcelain` clean) | unit, verified non-vacuous live | ☑ |
| US-3 / scenario 2 (no production client has `serviceAccountsEnabled: true`) | `KeycloakRealmRolesTests.ProductionRealmImport_NoClientHasServiceAccountEnabled` — same experiment, confirmed red for the right reason, reverted | unit, verified non-vacuous live | ☑ |
| US-3 / scenario 3 (the guard follows the client to `Mustask.AppHost`'s own code) | `StandOnlyKeycloakClientRules` (3 facts) — **one fact does not catch the mistake it exists for**, see D-1 (blocker candidate, rated major below) | unit, **partially vacuous** | ☒ (D-1) |
| US-3 / scenario 4 (the guards run on every `dotnet test Mustask.sln`) | both test classes are registered project members of `Mustask.sln` (`Mustask.Contracts.Tests`, `Mustask.Architecture.Tests`, confirmed via `grep` on `Mustask.sln`); no new project was added | structural check | ☑ |
| NFR — Attributability (impersonation event retrievable, attributed to the client's service account) | live: `GET /admin/realms/mustask/events?type=IMPERSONATE` → one `IMPERSONATE` entry, `details.impersonator: "service-account-mustask-e2e"`; `GET /admin/realms/mustask/admin-events` over the same window carries the provisioning's own CRUD events but **no** `IMPERSONATE` entry — confirms the plan's own finding precisely | manual (live stand) | ☑ (see note on which credential can read it, below) |
| NFR — Secrets handling (AppHost-generated, no 4th mandatory secret, never logged) | `dotnet user-secrets list` unchanged (no new secret key); the secret differs between two consecutive starts (`271A82...` → `69BE43...` → `6FF99...` across three starts); grepped all captured AppHost console output for the secret value — never printed | manual (live stand) | ☑ |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A1 | Rename `KeycloakStandProvisioner` → `KeycloakStandProvisionerRenamed` throughout its own file, leave everything else untouched | `StandOnlyProvisioner_ExistsInAppHost_UnderTheExpectedNamespace` goes red, naming the rename | ☑ pass — went red for the right reason (see Runs) |
| A2 | A shipping project (`Mustask.Api`) gains a **new class under a different name and namespace** that reimplements the impersonation-capable-client creation (POST `.../clients` with `serviceAccountsEnabled: true`) without ever mentioning the string `Mustask.AppHost.Provisioning` | The architecture guard should catch it — this is exactly the "copy-pasted admin-API call" scenario `ShippingProject_DoesNotUseTheStandOnlyProvisioningNamespace`'s own docstring claims to cover | ☒ **fail — D-1** |
| A3 | Add a client to `deploy/keycloak/realms/mustask-realm.json` with `clientId: "mustask-e2e"`, `serviceAccountsEnabled: true` | Both `KeycloakRealmRolesTests` facts (US-3/1, US-3/2) go red, each naming the offending client | ☑ pass |
| A4 | Full stop (SIGTERM)/start cycle of AppHost, same stand, no volume | Old stand file's secret is rejected (401) by the new instance; new stand file's secret authenticates; fixture user recreated with the same correct shape | ☑ pass |
| A5 | Force-kill (`taskkill /F`) AppHost mid-run, leaving the pinned-name `mustask-e2e-keycloak-5010` container orphaned, then start AppHost again | `plan-backend.md` §3a predicts the second start **fails** to create a container with the same name | ☒ **did not reproduce as predicted** — DCP silently removed the orphan and created a fresh container; no visible startup failure. See D-2 for what was found instead |
| A6 | Same force-kill scenario, but check *when* the stand file becomes valid relative to Aspire's own "started" signal | The file should be valid (fresh secret, matches the live client) by the time an agent could reasonably act on "the stand is up" | ☒ **fail — D-2** |
| A7 | Query `/events` and `/admin-events` using the `mustask-e2e` client's own service-account token (the credential the suite itself will hold) instead of the master realm admin | — | Informational, not a defect: **403 Forbidden** on both — the service account has `impersonation`+`manage-users` but not `view-events`, so it cannot read its own attributability event. The NFR is still met (the admin API can retrieve the event), just not through the `mustask-e2e` client's own credential; worth knowing if a future story ever wants the suite to self-verify the event |

### Runs

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-09-13 | `dotnet test .../Mustask.Architecture.Tests.csproj --filter StandOnlyKeycloakClientRules` (baseline) | 11 passed / 0 failed |
| 2026-09-13 | Same, with a QA-experiment file added to `Mustask.Api/Features/` whose only content was the literal comment string `Mustask.AppHost.Provisioning` (a methodology mistake — the comment itself, not the code, tripped the guard) | 1 failed, for the wrong reason — discarded, redone |
| 2026-09-13 | Same file rewritten with no literal reference to the provisioning namespace, a real reimplementation of the client-creation POST under class `SneakyKeycloakClientCreator` | **11 passed / 0 failed — D-1** (the guard did not fire); file deleted immediately after, `git status --porcelain src/backend/Mustask.Api` empty |
| 2026-09-13 | Added `clientId: "mustask-e2e"` with `serviceAccountsEnabled: true` to `deploy/keycloak/realms/mustask-realm.json`; `dotnet test .../Mustask.Contracts.Tests.csproj --filter ProductionRealmImport` | 2 failed, both naming `mustask-e2e` correctly; reverted, `git status --porcelain`/`git diff --stat` on the file — clean |
| 2026-09-13 | Renamed `KeycloakStandProvisioner` → `KeycloakStandProvisionerRenamed` throughout its own file; `dotnet test .../Mustask.Architecture.Tests.csproj --filter StandOnlyKeycloakClientRules` | 1 failed (`StandOnlyProvisioner_ExistsInAppHost...`), 10 passed; `git checkout --` on the file, tree clean |
| 2026-09-13 | Live stand #1: `dotnet run --project src/backend/Mustask.AppHost` (fresh, no prior containers) | `mustask-e2e-keycloak-5010` up; `/realms/mustask` 200; stand file written; token obtained for `mustask-e2e`; fixture user profile/credentials/groups correct; impersonation call → 200 + real `KEYCLOAK_IDENTITY`/`KEYCLOAK_SESSION` cookies; `/events?type=IMPERSONATE` (master admin token) → one entry, `impersonator: service-account-mustask-e2e`; `/admin-events` → provisioning's own CRUD events, no `IMPERSONATE`; `mustask-e2e`'s own token → 403 on both `/events` and `/admin-events` (A7) |
| 2026-09-13 | Graceful stop (`kill`/`taskkill` without `/F`) — all containers torn down (`docker ps` empty) | Confirmed clean teardown |
| 2026-09-13 | Live stand #2: same command, same worktree | New container (different ID); new stand-file secret; **old secret → 401** on the new instance; fixture user recreated correctly (A4) |
| 2026-09-13 | Force-kill (`taskkill /F /T`) AppHost while stand #2's containers were up | All containers (`mustask-e2e-keycloak-5010` included) survived the kill — orphaned, still running |
| 2026-09-13 | Live stand #3: `dotnet run` again, orphaned containers still present | No startup error; DCP removed and recreated every container under the same names (confirmed by differing container IDs), `mustask-e2e-keycloak-5010`'s health check passed ~65 s after container creation; the client/user/roles/group were correctly (re)provisioned ~66 s after container creation (per `admin-events` timestamps) — but the on-disk stand file still held stand #2's secret for that whole window (checked at the +50s mark: stale; at +~140s mark: fresh) — **D-2** |
| 2026-09-13 | Final teardown: force-kill AppHost, explicit `docker rm -f` on this stand's own 10 named containers (by id, not `docker ps -q`, to avoid touching sibling worktrees' containers on the shared daemon) | `docker ps -a` empty |
| 2026-09-13 | `dotnet build-server shutdown` | MSBuild + VBCSCompiler servers stopped |

No full `dotnet test Mustask.sln` re-run — already green per the task brief (869 tests); only the touched/relevant test classes were re-run for the experiments above, per "green is not re-checked."

### Defects

#### D-1: The architecture guard `ShippingProject_DoesNotUseTheStandOnlyProvisioningNamespace` catches a reference, not a reimplementation

- Severity: **major**
- Steps: add a new file to any shipping project (reproduced in `Mustask.Api/Features/`) containing a class under an unrelated name/namespace that independently performs the same admin-API call the stand-only client relies on (`POST /admin/realms/{realm}/clients` with `serviceAccountsEnabled: true`) — as long as the file never contains the literal string `Mustask.AppHost.Provisioning`. Run `dotnet test Mustask.Architecture.Tests.csproj --filter StandOnlyKeycloakClientRules`.
- Expected: the guard fails, per its own docstring ("A `ProjectReference` scan cannot see a copy-pasted admin-API call... This scans source directly: no shipping project's code may mention the provisioning namespace at all, which is what 'reachable only from the stand's own provisioning' (US-3 scenario 3) actually requires").
- Actual: all 11 facts pass. The scan is a literal `Contains(ProvisioningNamespace, Ordinal)` over source text — it only catches a `using`/fully-qualified reference to `Mustask.AppHost.Provisioning`, not a semantically equivalent reimplementation under a different name. ADR-0023's Decision 3 and ADR text both describe the guard as covering "reachable only from the stand's own provisioning," which is a stronger claim than what the test delivers.
- Impact: exploiting this requires a deliberate act (or a very unusual accident) by a future contributor, and normal code review would likely catch a second Keycloak-client-creation routine appearing in a shipping project — so this is not "silently drifts on an innocent refactor" the way the already-fixed substring/word-boundary bug was. But it is a real gap in a security-relevant guard whose whole job is to enforce BR-2 ("never in production") once the client is created by code rather than by import, and the docstring overclaims what it verifies.
- Status: open. Two independent fixes are possible without escalation (both stay inside this stage's write zone): (a) soften the docstring/comment to state the actual, narrower guarantee ("catches a reference to this code, not an independent reimplementation of its behavior"), or (b) widen the scan to also flag shipping-project source that POSTs to a Keycloak `/clients` endpoint with `serviceAccountsEnabled` set to a literal `true` — a coarser but still mechanical signal. Recommend (a) at minimum; (b) is a judgment call for the backend-developer, since it trades false positives (a legitimate future client needing a service account, if one is ever added to a shipping project through a *different*, allowed path) for the wider net.

Verified byte-identical afterward: `git status --porcelain src/backend/Mustask.Api` was empty once the experiment file was deleted.

#### D-2: No signal distinguishes "the stand is up" from "the E2E fixture/client are actually provisioned and the stand file is fresh" — reproduced live, worst case ~90 s

- Severity: **major**
- Steps: with a stand already running (`mustask-e2e-keycloak-5010` healthy, stand file valid), force-kill AppHost (`taskkill /F /T`) — this leaves every container, including the pinned-name Keycloak one, orphaned and still running (contradicts `plan-backend.md` §3a's specific worry, see the note below). Start AppHost again (`dotnet run --project src/backend/Mustask.AppHost`) while the orphans are still up.
- Expected (US-2 scenario 1, "When the stand reports ready, Then the fixture user(s) ... already exist ... and a token can be obtained for [the client] with the secret AppHost ... wrote into the ... file"): whatever an agent reasonably treats as "the stand reports ready," the stand file at that point should already hold a secret that authenticates.
- Actual: Aspire prints "Distributed application started" (its own top-level readiness banner) well before the `keycloak` resource's own HTTP health check (`/realms/mustask`) passes — DCP silently removes the stale-named orphan and creates a fresh container, whose Keycloak process then takes real wall-clock time (~65 s observed, cold JVM start + realm re-import) before the health check passes and `ResourceReadyEvent` fires at all. Provisioning itself is fast once triggered (admin-events timestamps span under half a second), but the whole window between the "started" banner and the fresh stand file being written was on the order of 90 seconds in this run. During that window the on-disk stand file is the *previous* run's — same shape, same `issuer` (same stand/port, so the staleness field `plan-backend.md` §6 added specifically for this purpose does **not** distinguish it), but a secret that is rejected (401) by the now-fresh Keycloak instance.
- Impact: this is not, by itself, a violation of any single Gherkin line read as a steady-state fact ("once ready, ... exist") — but nothing in this task defines what "reports ready" means operationally, and the one field designed to catch a stale file (`issuer`) is blind to exactly this case, since it is the same stand. Whoever consumes the file (US-1's global setup, stage 6) must poll/retry against the file's *content* (e.g., retry a token request until it succeeds) rather than treat file-existence or AppHost's own console banner as sufficient — otherwise BR-5's "fail fast, one named cause" could misfire with a spurious "client absent/mismatched" report during a race that has nothing to do with a genuinely missing client. This risk is not unique to the force-kill path — the same race exists, just with a much narrower window, after any restart, since `ResourceReadyEvent` firing and the subscriber's several sequential admin-API round trips always happen strictly after the top-level "started" signal on a cold container.
- Correction to `plan-backend.md` §3a's own risk assessment: the specific failure mode named there ("the next `dotnet run` fails to create a new one with the same name until the old one is removed") was **not observed** — DCP recovers silently and successfully from a stale-named orphan. The real, reproduced risk is the timing one above, not a name collision.
- Status: open, flagged for stage 6 (frontend QA/dev must confirm `global-setup.ts` retries/validates the stand file rather than reading it once) and for the human at gate-3 (whether backend should also add an explicit "provisioning complete" log line — see D-3 — to make the window observable rather than inferred).

#### D-3: The provisioning subscriber has no success/failure log line

- Severity: **minor**
- Steps: read `AppHost.cs`'s `ResourceReadyEvent` subscription and `KeycloakStandProvisioner.ProvisionAsync` — no `try`/`catch`, no `ILogger`/`Console` call on success or failure anywhere in the path.
- Expected: an operator (human or agent) watching the console has a grep-able line marking "e2e stand provisioning done" (or a caught, rethrown failure naming the step), matching the file's own comment style.
- Actual: nothing is printed either way. This is exactly what made D-2 hard to notice — the "Distributed application started" banner gives no information about whether the subsequent, separate provisioning step has completed, succeeded, or failed. If a future Keycloak upgrade changes the admin API's shape (the exact risk `plan-backend.md` §5 already names as an untested regression), the failure would be silent at the console level too.
- Status: open, minor — a one-line addition, not a blocker.

#### D-4: US-2 scenario 5 (manual seed vs. automatic seed disjointness) has no regression guard

- Severity: **minor**
- Steps: none needed to reproduce — this is an absence. Confirmed by reading `scripts/keycloak-seed.sh` in full: it only ever calls `seed_user` for `mustask`, `partner`, `inspector`, `operator`.
- Expected/Actual: the criterion holds today, but nothing fails if a future edit to either seeding path adds `e2e-fixture` to `keycloak-seed.sh`, or has `KeycloakStandProvisioner` touch one of the four human-facing usernames. Both would violate BR-6/BR-8 silently.
- Status: open, minor — worth a one-line assertion (e.g., a contract test that greps `keycloak-seed.sh` for the literal username `e2e-fixture` and fails if found) or an explicit comment in both files cross-referencing the other; not a blocker for this task.

### Coverage notes (not defects)

- US-2 scenario 2's "already exists → update" branch is exercised **only** by the fake-handler unit test. Live, `keycloak` has no volume (`AppHost.cs`'s own comment: "Тома у контейнера намеренно нет"), so every normal stand restart recreates an empty realm from scratch and only the "create" branch was actually observed live in this pass (twice: stand #1 → #2, and #2 → #3). The "update" branch would only run live if a `dotnet run` restarts while the *same* Keycloak container instance survives (e.g., a very fast crash-restart racing DCP's own cleanup, or a human `docker restart`-ing the container without restarting AppHost) — not reproduced here, since deliberately provoking that race safely on a shared Docker daemon (other worktrees may be running) was judged not worth the risk for a code path already covered by a unit test with a fake handler.
- The container-name pin's uniqueness claim ("two stands from two worktrees use different ports, so the names differ") was verified structurally, not by literally running two worktrees at once: `Ports:Keycloak` defaults to `5010` for every worktree (`appsettings.json`), and `src/backend/AGENTS.md` already states "a port must be free — Aspire will not relocate a taken one" — so two simultaneous stands on default ports cannot both start regardless of this task, which means any two *simultaneously running* stands are already forced onto different ports by pre-existing behavior, and therefore onto different container names by this task's own naming rule. Not independently reproduced with two live worktrees.
- The NFR "Attributability" is met, but only through a credential with realm-admin-level `view-events` (the master-realm admin token used throughout this QA pass) — the `mustask-e2e` client's own service-account token, which only holds `impersonation`+`manage-users`, gets 403 on both `/events` and `/admin-events` (A7). The spec text does not require the suite itself to read the event, so this is not a defect against any acceptance criterion, just worth recording in case a future story wants the suite to self-verify attributability.

### Judgment: `Mustask.Contracts.Tests` → `Mustask.AppHost` `ProjectReference`

**Not sound, though it does not break anything today.** The reference achieves the letter of "no Docker, no HTTP host, no DB" at *runtime* — the four new tests genuinely only exercise a fake `HttpMessageHandler`, nothing live is started. But it violates the same charter at the *build/dependency-graph* level: `Mustask.AppHost.csproj` uses `Sdk="Aspire.AppHost.Sdk/13.5.1"` and references `Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`, and `Aspire.Hosting.JavaScript`. Once `Mustask.Contracts.Tests` takes a `ProjectReference` to it, those assemblies (`Aspire.Hosting.dll`, `Aspire.Hosting.PostgreSQL.dll`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL.dll`, `Aspire.Hosting.JavaScript.dll`, and more) land in `Mustask.Contracts.Tests`'s own build output — measured: **43 MB**, versus **5.9 MB** for `Mustask.Architecture.Tests`, a comparable sibling test project with no such reference. `KeycloakStandProvisioner`/`KeycloakStandProvisioningOptions` themselves have zero Aspire dependency (only `System.Net.Http.Json`/`System.Text.Json`), so none of this weight is needed by the code under test — it comes along purely because of *which project* the two files happen to live in.

The practical costs: `Mustask.Contracts.Tests` — a project whose own header comment promises to stay light — now rebuilds/restores against the entire orchestration stack, and is coupled to `Mustask.AppHost`'s health as a *compilable* project (a future Aspire SDK bump or a new hosting-integration package in `Mustask.AppHost` now has to build cleanly for `Mustask.Contracts.Tests` to build at all, even though its own tests would not notice the difference). This is a one-way coupling that did not need to exist: the plan's own §7 correctly ruled out a new test project and a new NuGet package as orchestrator-only, but a `ProjectReference` was not the only option left. A `<Compile Include="..\..\Mustask.AppHost\Provisioning\*.cs" Link="Provisioning\%(Filename)%(Extension)" />` item in `Mustask.Contracts.Tests.csproj` would compile the same two files into the test project directly, need no new package, no `Mustask.sln` change, and no `ProjectReference` — staying inside this stage's own write zone (editing a test project's own `.csproj`, which the plan already touches) while actually honoring "no Docker, no HTTP host, no DB" at both levels, not just at runtime.

This is offered as my own opinion with the evidence behind it, not a rewrite — the tests as written are correct and pass; the concern is architectural, and reasonable people could accept the current trade (one `ProjectReference`, heavier build output, no functional risk) as good enough for a task of this size.

### Verdict

blockers: 0 · majors: 2 (D-1, D-2) · minors: 2 (D-3, D-4) · result: **fail**

Per `04-backend.md`'s Definition of Done ("`qa.md`, Backend section: no blockers"), zero blockers alone would read as pass — but D-1 directly undercuts BR-2's "never reachable outside a developer's stand" guarantee (a guard that doesn't catch the mistake it exists for), and D-2 is a live-reproduced timing gap that stage 6 must design around, not discover by accident the way this pass did. Recommend returning both to the backend-developer before stage 6 starts consuming the stand file contract, even though neither blocks `dotnet test Mustask.sln`. D-3/D-4 can ride along with the same fix cycle. The E2E question (tech-119 D-32/`04-backend.md` step 4) is **not** asked at this stage — this task continues to stage 6 (frontend), so that question belongs to frontend QA (`05-frontend.md` step 3), per the routing rule.

## Backend QA round 2

Read per the round-1 discipline: `qa.md`'s own round 1 (above) first, then
`plan-backend.md` §10 (the author's account of each fix), then `spec.md`,
`stories/US-2-*`/`US-3-*`, and `status.md` §Decisions (D-1..D-7) — this
round's own re-read, not taken on trust from round 1. Fix commits examined:
`9a0f81c` (D-2, D-3), `b1c1bef` (D-1, D-4, the project reference),
`97d2607` (docs).

### D-1 — resolved

Reproduced round 1's evasion once, live: added
`Mustask.Api/Features/QaExperimentD1/SneakyKeycloakClientCreator.cs`, a class
under an unrelated name/namespace POSTing to `/admin/realms/{realm}/clients`
with `serviceAccountsEnabled: true`, mentioning `Mustask.AppHost.Provisioning`
nowhere. `dotnet test Mustask.Architecture.Tests.csproj --filter
StandOnlyKeycloakClientRules`: 15 passed / **1 failed** —
`ShippingProject_DoesNotCallKeycloaksAdminApi(project: "Mustask.Api")`, naming
`SneakyKeycloakClientCreator.cs` in the failure message —
while `ShippingProject_DoesNotUseTheStandOnlyProvisioningNamespace(project:
"Mustask.Api")` stayed green in the same run, confirming the two checks are
independent, not one masking the other. File deleted immediately after;
`git status --porcelain src/backend/Mustask.Api` empty; the suite re-run at
16/16 green to confirm no residue. The docstring correction (round 1's
recommendation (a)) is also in: both facts' XML comments now state what they
actually catch, not the stronger claim round 1 found unsupported.

**Guard boundary, for whoever is tempted to call this a security control:**
the two `StandOnlyKeycloakClientRules` facts together catch a copy-paste leak
(a stray `using Mustask.AppHost.Provisioning`, or a copy that still names its
origin in a comment), an honest mistake (a developer extending `Mustask.Api`
who reaches for the nearest working example of "how do I call Keycloak's
admin API" — this provisioning code — and copies the URL pattern verbatim
without renaming anything), and a refactor that drags the capability into a
shipping project without anyone deciding to (someone "simplifies" by moving
a helper out of `Mustask.AppHost` into `Mustask.Shared`, carrying the literal
`/admin/realms/` along for the ride). What it does **not** catch, and cannot
catch by construction: anyone who deliberately wants an admin-API call to
survive review and knows a literal-string scan is in the way. String
concatenation (`"/admin/" + "realms/"`), a char array, base64, a value read
from configuration, a `UriBuilder` built path-segment by path-segment, or
splitting the fragment across two adjacent literals all defeat
`Contains(text, literal)` with no effort — and no cleverer literal-scan
closes that door, because the weakness is a property of grepping source
text, not a bug in this particular grep. This is not a control against an
adversarial insider with commit access to the repository: anyone who can
edit `Mustask.Api` can equally add a `ProjectReference`, disable the test, or
edit its assertion. It earns its place anyway because its real audience is
not an adversary — it is the next contributor or agent who innocently
copies a nearby working example, and a reviewer skimming a diff who would
not necessarily notice one new outbound call to an unfamiliar Keycloak path
buried inside an unrelated change. Against that audience, which is most of
how a mistake like this actually reaches a shipping project, a mechanical,
always-on check that fails loudly and names the offending file is real value
for near-zero cost — it is just not, and should never be described as, the
thing standing between BR-2 and a determined attempt to defeat it.

### D-2 — resolved

Live, self-started/self-stopped stand, two full cycles (fresh start, force-
kill, restart), this session's own worktree/ports (Keycloak 5010):

- **Absence through the whole window, not staleness.** Force-killed
  `Mustask.AppHost.exe` (`taskkill /F /T`) after a healthy stand; started
  `dotnet run --project src/backend/Mustask.AppHost` again and polled
  `.e2e-stand/keycloak-e2e-client.json` every second from t=0. Result:
  **absent for the entire 41 s window**, reappearing only once, with a fresh
  secret. Confirmed both the old and new secret never touch any log line
  (`grep` for both literal values across both run logs — 0 matches).
- **Secret and container id both differ across the restart**, confirmed by
  direct comparison: secret `DC4613...C832` → `9DB663...79EA`; container id
  `ee04e4a7...` → `fd709561...` (`docker inspect --format '{{.Id}}'
  mustask-e2e-keycloak-5010`).
- **One deviation from round 1's own reproduction, recorded rather than
  smoothed over:** this round's `taskkill /F /T` on `Mustask.AppHost.exe` tore
  down every container cleanly (`docker ps -a` empty seconds later) instead
  of leaving them orphaned, unlike round 1's run. The fix does not depend on
  which of the two happens — the file is deleted as the first statement
  regardless of whether the next start reuses an orphaned container or
  creates an entirely fresh one — but the orphan-survival behaviour itself
  is evidently not deterministic across runs on this host, which is worth
  knowing if a future session tries to reproduce round 1's exact container-
  id-collision scenario and cannot.
- **The path's per-worktree derivation, verified rather than assumed:** the
  live log line ("stand file written to
  `...\clone-2f8bf0\src\backend\Mustask.AppHost\..\..\..\.e2e-stand\keycloak-e2e-client.json`")
  shows `builder.AppHostDirectory` resolving to *this worktree's own*
  physical path, and the three `..` normalizing to *this worktree's own*
  repository root — not a shared or machine-wide location. This was checked
  against the actual resolved path Aspire printed, not inferred from reading
  the source.
- **Two simultaneous stands:** two worktrees running at once are already
  forced onto different `Ports:Keycloak` values (Aspire will not relocate a
  taken port — confirmed as an existing, pre-tech-159 constraint,
  `src/backend/AGENTS.md`), so their `keycloakContainerName`s already
  differ (§3a). The stand-file path adds an independent, stronger guarantee
  than the container name's: it is anchored to `AppHostDirectory`, which is
  a property of *which physical checkout* the running `Mustask.AppHost.exe` was
  built from, not of the port it happens to use — so two simultaneous
  stands write to two different files even in the hypothetical where they
  shared a port (which they cannot, in practice, since the second process
  would fail to bind before ever reaching the provisioning subscriber).
  Within one single worktree, a second concurrent `dotnet run` cannot
  co-exist either, for the same pre-existing reason (Postgres/Keycloak ports
  already bound by the first). No dual-write race is possible in any
  combination actually reachable.

### D-3 — resolved

Confirmed live in both restart cycles above: a `tech-159: provisioning...`
line before, a `tech-159: ... provisioning finished...` line on success,
both naming `ClientId`/`FixtureUsername`/the stand file path only — grepped
both run logs for both generated secret values, zero matches in either. The
failure path (`catch (Exception ex) { logger.LogError(ex, ..., standFilePath); throw; }`)
was verified by reading the code, not by forcing a live failure: the
interpolated arguments are the exception and `standFilePath` only, the
`ClientSecret` local never appears in any `LogError`/`LogInformation` call
in `AppHost.cs`, confirmed by reading every logger call in the file.

### D-4 — judged, no real hole

The asymmetry is sound, not a shortcut. `KeycloakSeedScript_NeverMentionsTheE2EStandIdentifiers`
covers the direction that matters as a textual scan (the human-facing script
gaining a stray mention of the E2E identifiers). The "reverse" direction
the author declined to write as a substring scan — grepping
`KeycloakStandProvisioner.cs` for `mustask`/`partneradmin`/`inspector`/`operator` —
would indeed be vacuous or worse (false positives on `Mustask.AppHost`'s own
namespace, exactly as the author's own proof-of-red showed). What
`E2EFixtureUsername_IsNotAnyHumanFacingUsername` does instead — comparing
the one authoritative constant's *value* against the four human-facing
usernames directly — is not a weaker substitute for that scan, it is the
correct guard for what US-2 scenario 5 actually requires (the *value*
provisioning sends to Keycloak never equals a human-facing username), and it
has none of the false-positive problem a text scan would. Re-verified by
reading `KeycloakStandIdentifiers.cs` and confirming `AppHost.cs` references
the constants rather than re-typing the literals — the single-source-of-
truth property the fix claims. No residual gap found.

### The project reference — confirmed

`dotnet test Mustask.Contracts.Tests.csproj`: 48 passed. `Mustask.Contracts.Tests.csproj`
carries a `<Compile Include="..\..\Mustask.AppHost\Provisioning\*.cs"
LinkBase="Provisioning" />` item and **no** `ProjectReference` to
`Mustask.AppHost.csproj` — confirmed by reading the `.csproj`. Measured build
output: `src/backend/tests/Mustask.Contracts.Tests/bin/Debug/net10.0` = **20 MB**
(matches the plan's own figure exactly); the only Aspire-family assembly
left in it is `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL.dll`, which comes
from `Mustask.Api`'s own pre-existing, unrelated reference — `Aspire.Hosting.AppHost`/
`.PostgreSQL`/`.JavaScript` are confirmed absent. `Mustask.Architecture.Tests`'s
own output measures 5.9 MB, matching the plan's comparison baseline.
Independent compilation, not a copy that can drift: `Mustask.AppHost.csproj` has
no `<Compile Remove>` for `Provisioning/*.cs` and no exclusion of any kind —
confirmed by reading the file — so the SDK's default globbing still compiles
the identical two source files into `Mustask.AppHost.exe` on its own, a second,
independent compilation of the same source, exactly as claimed.

### Verdict — round 2

blockers: 0 · majors: 0 · minors: 0 · result: **pass**

All four round-1 defects resolved and independently re-verified this round
(D-1 by re-reproducing the evasion and watching it go red; D-2 live across
two full force-kill/restart cycles, including the per-worktree derivation
and the multi-stand question round 1 left unchecked; D-3 by log grep plus a
read of the failure path; D-4 by judgment, no test rewritten). The project-
reference judgment from round 1 is confirmed fixed and measured, not merely
re-asserted. `dotnet build-server shutdown` run after the last test/live
cycle; no container, process or file left behind that `git status
--porcelain` would show (the `.e2e-stand/` directory left on disk after the
live runs is expected, gitignored, developer-machine state — not build
residue). The E2E question is not asked here — this task continues to stage
6 (frontend), per the routing rule (`04-backend.md` step 4, tech-119 D-32).

## Frontend

### Phase 1 (no stand — the E2E question is being asked separately)

Read per the round-1 discipline, requirements before implementation:
`spec.md`, `stories/US-1`, `US-4`..`US-9` (US-2/US-3 are backend's, already
QA'd twice, not re-opened here), `status.md` §Decisions (D-1..D-8, D-8 is
binding and changed specs 6/9/11), `qa.md`'s own Backend section above
(both rounds), then `plan-frontend.md` and `plan-backend.md` §11/§12, then
the code under `src/frontend/e2e/**` and `src/frontend/src/test/e2eSessionGuard.test.ts`.

This section covers everything reachable without a live stand. **The live
run (US-1 scenario 3, and the artifact-grep half of US-1 scenario 2) is not
in this pass** — Phase 1 does not start a stand; it resumes only once the
human has answered the E2E question and authorized Phase 2.

### Coverage matrix

| Criterion (spec) | Check | Type | Status |
|---|---|---|---|
| US-1/1 (no `E2E_PASSWORD` anywhere, every spec still signs in) | `env.ts` has no `password()`/`keycloakAdminPassword()` at all (grep confirms no `E2E_PASSWORD`/`E2E_KEYCLOAK_ADMIN_PASSWORD` reader anywhere under `e2e/`); every spec calling `signIn` routes through `impersonation.ts` | code reading | ☑ (live confirmation is US-1/3, phase 2) |
| US-1/2a (Node-only: client-credentials/impersonation never runs in a browser context) | `impersonation.ts`/`keycloakAdmin.ts` use `node:http`/`fetch` from Node only, never `page.evaluate`/a page script; only `KEYCLOAK_IDENTITY`/`KEYCLOAK_SESSION` **cookie values** cross into `page.context()`, never the client secret | code reading | ☑ **but unguarded** — nothing in `e2eSessionGuard.test.ts` (or elsewhere) would fail if a future author moved this call into a page context; the property holds today by inspection only, see Coverage notes |
| US-1/2b (secret never appears in `results.json`/console/trace) | error paths in `impersonation.ts`/`standFile.ts`/`keycloakAdmin.ts` interpolate `response.status`/`response.body`/the file path into thrown messages, never `clientSecret`/`stand.clientSecret` (grepped every throw site) | code reading | ☑ structurally; empirical grep of a real run's artifacts is phase 2 |
| US-1/3 (13 passed, 0 failed, ≤15 min, recorded in `qa.md`) | not run this pass — needs a live stand | live | ☐ pending phase 2 |
| US-4/1 (OIDC round trip recorded: auth-endpoint nav, callback request, `Set-Cookie: mustask.session`, absent before) | `01-deep-link.spec.ts`: `waitForRequest`/`waitForResponse` registered before `signIn`, asserts `set-cookie` header and `getSessionCookie` undefined pre-injection | spec | ☑ |
| US-4/2 (structural guard) | `e2eSessionGuard.test.ts` — reproduced live (see below): catches a direct `{ name: 'mustask.session', ... }` assignment and a literal `.route('**/api/session/callback', ...)` stub, naming the file in both cases | vitest, reproduced red then green | ☒ **gap found — D-F1** (see Defects): a `page.route('**/api/session/*', ...)` glob, which stubs all three endpoints at once, is not caught |
| US-4/3 (spec 3 keeps the real form, non-fixture literal username) | `03-wrong-password.spec.ts` uses `NON_FIXTURE_USERNAME = 'e2e-wrong-password-probe'`, never `env().user` | spec | ☑ |
| US-4/4 (specs 2/3 never sign in) | neither spec imports/calls `signIn`; Playwright's per-test fresh context does the rest | spec + structural | ☑ |
| US-4/5 (spec 4: one context across sign-in/out/re-open) | `04-sign-out.spec.ts` uses one `page` throughout, no `browser.newContext()`/cookie-clearing call | spec | ☑ |
| US-4/6 (spec 10: `mustask.session` carried, Keycloak SSO cookie stripped, asserted truthy) | `10-persistent-cookie.spec.ts` + `cookies.ts`'s `withoutKeycloakSsoCookies` — filters `KEYCLOAK_*` before building context 2, asserts `getSessionCookie(context2)` truthy, not only the dashboard | spec | ☑ — the BR-7 inversion the reviewer caught earlier is not present; the fix matches the corrected rule |
| US-5/1 (missing client → one named cause) | `standFile.ts`'s `waitForStandFile` — ENOENT retried to a 120s deadline, then one error naming the path and the start command | code reading (live repro is phase 2) | ☑ |
| US-5/2 (credential mismatch → one named cause) | `verifyClientSecret` — a real `client_credentials` grant against the file's own values; non-2xx names the client and suggests a restart | code reading (live repro is phase 2) | ☑ |
| US-5 "wrong stand" (issuer mismatch, named in the task brief though not its own AC line) | `loadVerifiedStandFile` compares `stand.issuer` against `${keycloakUrl}/realms/${realm}` before ever attempting a token grant, names both values | code reading | ☑ |
| US-6/1 (spec 9 runs, no admin password) | `09-token-renewal-two-tabs.spec.ts` — no skip, no realm write, relies on AppHost's own 120s `accessTokenLifespan` (D-8) | spec | ☑ |
| US-6/2 (specs 6, 11 run, same condition) | `06-renewal-keycloak-down.spec.ts`/`11-session-ended-by-admin.spec.ts` — same shape; `logoutUserSessions` (spec 11) authorizes via `mustask-e2e`'s own token, needs only `manage-users`, which it holds | spec | ☑ |
| US-7/1 (resolver scoped to one stand, named failure on ambiguity) | `docker.ts`'s `resolveKeycloakContainer` uses the stand file's exact `keycloakContainerName`; `docker inspect` on an exact name cannot be ambiguous by construction — the "first match" class of bug is structurally removed, not merely narrowed | code reading | ☑ |
| US-7/2 (specs 5/6 control only the resolved container, `afterEach` restores) | both specs resolve via the container name, `afterEach` calls `startContainer` unconditionally when `containerName` was set | spec | ☑ |
| US-7/3 (Docker control stays declined without the flag) | `test.skip(!env().allowDocker, ...)` on both specs' `describe` blocks | spec | ☑ |
| US-8, all 4 scenarios | `src/frontend/AGENTS.md` § E2E, `docs/runbooks/stand.md` §§ "E2E test suite"/"Access from the local network", `04-backend.md` step 4 + DoD, `05-frontend.md` step 3 + DoD, `playwright.config.ts`/`auth.ts`/`env.ts`/`global-setup.ts`/`vite.config.ts` header comments — read in full; no dangling reference to `brief-e2e-playwright.md` or `e2e/README` survives anywhere in the repository (grepped); the 120s (not 900s) fact is stated in both `src/frontend/AGENTS.md` and the runbook | doc reading + repo-wide grep | ☑ — **one adjacent staleness found, not itself an AC line** — see D-F3 |
| US-9/1 (global setup reconciles container + `accessTokenLifespan`) | `reconcile.ts` only reconciles the container; the `accessTokenLifespan`-to-900 half of this **story's own Gherkin text** was deliberately dropped (D-8) | code reading | ☒ **the acceptance criterion itself is now wrong — D-F2** (see Defects) |
| US-9/2 (agent accounts for the stand after an unattended run) | procedural — this session started no stand, nothing to account for; verified structurally that `docker build-server shutdown`/stand-teardown are the frontend-developer's own log entries from the prior stage, not something to re-check here | procedural | ☑ (n/a this pass) |

### Defects

#### D-F1: The structural guard does not catch a wildcard stub over all three session endpoints

- Severity: **major**
- Steps: added a temporary file `e2e/support/qaExperimentGuard.ts` containing `page.route('**/api/session/*', () => {})`; ran `npx vitest run src/test/e2eSessionGuard.test.ts`. Also confirmed, in the same session, that the guard *does* catch the two patterns its own story names verbatim: a direct `{ name: 'mustask.session', value: ... }` assignment, and a literal `.route('**/api/session/callback', ...)` stub — both went red, naming the file. File deleted immediately after each step; `git status --porcelain` empty throughout and at the end.
- Expected: US-4 scenario 2 says the guard fails "if anything under `e2e/` ... stubs/mocks `/api/session/login`, `/api/session/callback`, or `/api/session/token`" — a single `page.route('**/api/session/*', ...)` stubs all three literally (Playwright's glob `*` matches any of the three path segments), so it should trip the guard.
- Actual: `STUBS_SESSION_ENDPOINT = /\.route\(\s*[`'"][^`'"]*\/api\/session\/(login|callback|token)/` requires one of the three literal words immediately after `/api/session/` — a bare `*` satisfies none of them, so the guard passes (green) on exactly this stub.
- Impact: unlike the guard's own documented boundary (string concatenation, a dynamically-built RegExp, a helper this scan doesn't cover — all named as *determined* evasions the guard was never meant to survive), this is not a determined evasion. `**/api/session/*` is the more natural way to write "intercept the session calls" for someone debugging a flaky sign-in or adding logging — arguably more natural than naming one endpoint — and it defeats the exact guarantee the story states, silently. This is the innocent-fix audience the guard's own docstring names as its real job, and it misses this member of that audience.
- Status: open. Recommend widening `STUBS_SESSION_ENDPOINT` to also flag `/api/session/*` (and `/api/session` with no trailing segment, and a bare `/api/session/`-prefixed pattern ending in any wildcard character), or restructuring the check to flag any `.route(...)` whose pattern's path starts with `/api/session/` regardless of what follows — the criterion is "touches this path prefix at all," not "touches these three literal names," so the broader check is not just a patch, it is the more correct reading of BR-7/US-4's own intent.

#### D-F2: US-9 scenario 1's own acceptance criterion contradicts D-8 and the shipped implementation

- Severity: **major**
- Steps: read `stories/US-9-stand-state-reconciled.md` scenario 1 (unchanged since the story was split out, before D-8 — confirmed via `git log --follow` on the file: one commit, predating D-8's 2026-09-14 decision) against `reconcile.ts` and `status.md`'s own D-8/log entries.
- Expected (per the story file, still): "it restores `accessTokenLifespan` to its import value (900) if it finds it different."
- Actual: `reconcile.ts` no longer touches `accessTokenLifespan` at all — correctly, per D-8: AppHost now sets it to `120` at every stand start (including a bare container restart), and restoring it to `900` would actively undo that and break specs 6/9/11's timing assumption. The code is right; the requirements artifact was never updated to match.
- Impact: a stale acceptance criterion is worse than a missing one — it reads as a still-binding contract the implementation fails, when the implementation is actually the correct, human-authorized behavior (D-8). A future reader (agent or human) diffing the story against the code would reasonably conclude `reconcile.ts` regressed, and a future "fix" that literally satisfies the stale text would reintroduce the 403/skip problem D-8 was written to close. This is exactly the class of drift `docs/ai-workflow`'s file-based handover is supposed to prevent.
- Status: open — not something QA rewrites (requirements text is not this stage's write zone in the way code is); flagging for the human/coordinator to correct the story file's scenario 1 to state the post-D-8 behavior (container reconciliation only, `accessTokenLifespan` reconciliation removed because AppHost itself is now the sole writer).

#### D-F3: `withStandFile`'s 30-second retry is broader than its now-narrower justification, and blind to why it retries

- Severity: **minor**
- Steps: read `standFile.ts`'s `withStandFile` against `plan-backend.md` §12 (the per-process-secret fix, `cfca81e`). `withStandFile` wraps every credential-using call (`obtainKeycloakSsoCookies`, `logoutUserSessions`) and, on **any** exception from the wrapped action, blind-retries for up to 30s (15 attempts) by re-reading and re-verifying the stand file each time — with no logging of the retry itself.
- Expected/actual gap: the scenario this retry was built for — `mustask-e2e`'s secret rotating more than once within a run, discovered live pre-D-12 — is now closed at the source (`KeycloakStandClientSecret` is a `Lazy<string>` stable for the process's lifetime; §12's own live check confirmed the secret survives a container stop/start byte-for-byte). What remains is a catch-all: any error from the wrapped action — a genuine bug in the impersonation call, a 500 from Keycloak's admin API, a fixture user that no longer exists — gets the identical 30-second, 15-attempt, silent treatment before finally surfacing, with no indication in the final error that a retry loop ran at all.
- Impact: correctness is not at risk (it cannot turn a real failure into a false pass, only delay and obscure it), but diagnosability is — a genuinely broken call now takes at least 30 extra seconds to fail and reads, in the reporter output, exactly like a slow success, not like "this retried 15 times for an unrelated reason." `plan-backend.md` §12's own recommendation ("keep it as is... its purpose narrows cleanly to 'the file does not exist yet'") does not quite match what the code does: "the file does not exist yet" is `waitForStandFile`'s job (a different function, already narrow and correctly scoped); `withStandFile` retries on *action* failure, not file-absence, and that half of the justification (repeated secret rotation) is the one D-12 closed.
- Status: open, minor — recommend either narrowing the retry to the specific signal that indicates a stale secret (e.g., a 401/`unauthorized_client` from the token endpoint) rather than any thrown error, or at minimum logging each retry attempt the way `reconcile.ts` already does for its own repairs, so a slow failure is legible instead of silent.

#### D-F4: `playwright.config.ts`'s header comment still describes specs writing realm settings

- Severity: **minor**
- Steps: read `playwright.config.ts`'s top comment: "some of them change realm settings (accessTokenLifespan) or stop/start the keycloak container." Confirmed via `git log -p --follow` that this sentence has not changed since tech-119 introduced it — tech-159's own header rewrite (US-8, confirmed present: the file now points at `src/frontend/AGENTS.md` § E2E instead of the two dangling documents) touched the paragraph above it but not this one.
- Expected/actual: since D-8, no spec writes `accessTokenLifespan` — AppHost is now the sole writer, at every stand start. This sentence is the one piece of this specific comment BR-9's "falsified by this task's own change" test should have caught, since D-8 is this same task's own decision, not a pre-existing fact left alone on purpose.
- Status: open, minor — a one-clause fix ("some of them stop/start the keycloak container" — drop the parenthetical).

### What the structural guard caught, precisely

Reproduced live, both directions, in this session (experiments deleted
immediately after each answered its question; `git status --porcelain`
confirmed empty at the end):

- **Caught:** `{ name: 'mustask.session', value: ... }` (an assignment-shaped
  literal, matching the guard's own stated target) — red, naming the file.
- **Caught:** `page.route('**/api/session/callback', () => {})` — red,
  naming the file.
- **Not caught (D-F1):** `page.route('**/api/session/*', () => {})` — green.
- Not attempted (already documented as out of scope by the guard's own
  header comment, and not worth re-litigating): variable-indirection
  evasions (`const n = 'mustask.session'; addCookies([{ name: n, ... }])`,
  `const p = '/api/session/callback'; page.route(p, ...)`) — the guard's
  own docstring already states these defeat it "with no effort," and D-F1
  is a materially different finding precisely because it needs none of that
  effort.

### Verdict — Phase 1

blockers: 0 · majors: 2 (D-F1, D-F2) · minors: 2 (D-F3, D-F4) · result:
**fail, pending phase 2**

Zero blockers, but D-F1 undercuts the exact guarantee US-4 scenario 2
states (a natural, non-adversarial stub defeats the guard silently) and
D-F2 leaves a binding acceptance criterion actively contradicting a human
decision (D-8) and the correct, shipped behavior — both worth fixing before
this task's review, on the same reasoning the backend round 1 verdict used
for its own two majors. D-F3/D-F4 can ride along with the same cycle. No
defect found in US-1/4–9's live-independent criteria beyond these four; the
BR-7 per-spec context rule holds for every spec checked, including the
scenario a reviewer previously caught being inverted for spec 10 (not
repeated here). **Not yet verified — needs phase 2 (a live, self-started/
self-stopped stand):** US-1 scenario 2's artifact-grep half, US-1 scenario 3
(13/0/0, ≤15 min, recorded here), and a live reproduction of US-5's two
fail-fast messages and US-9 scenario 1's container-repair path (currently
judged sound by code reading only). If phase 2 runs, worth specifically
watching: whether D-F1's gap is exploitable in practice (it isn't reachable
without someone writing it), whether D-F3's retry is ever actually
triggered by a genuine, non-secret-related failure during the run (it
should not be, if nothing else in the suite is broken), and reconfirming
the wall-clock time and pass/skip/fail counts fresh rather than trusting
the frontend-developer's own `status.md` log entry.

### Phase 2 — live run (human authorized E2E, "да, делай e2e")

Fix commit `f0e74cf` reviewed before running anything live — each of D-F1–
D-F4's diffs read in full (not taken on trust from the commit message).

#### D-F1–D-F4 re-verification

- **D-F1 — resolved, confirmed live-tooling.** `STUBS_SESSION_ENDPOINT` now
  matches the `/api/session` path prefix (`/\.route\(\s*[`'"][^`'"]*\/api\/session(\/|[`'"])/`)
  instead of the three literal names. Re-reproduced this session's own
  exact wildcard form: added `e2e/support/qaVerifyDF1.ts` with
  `page.route('**/api/session/*', () => {})`, ran
  `npx vitest run src/test/e2eSessionGuard.test.ts` — **red**, naming the
  file. Deleted immediately after; `git status --porcelain` empty. No
  legitimate `.route(` call exists anywhere under `e2e/` today (grepped),
  so the widened pattern has no false-positive surface to worry about yet.
- **D-F2 — resolved.** `stories/US-9-stand-state-reconciled.md` scenario 1
  no longer mentions restoring `accessTokenLifespan`; a comment names D-8
  and this QA's own D-F2 so a future reader sees why the text changed
  rather than wondering if it was silently dropped. Matches `reconcile.ts`
  (unchanged, still container-only).
- **D-F3 — resolved, confirmed live.** `getServiceAccountToken` in both
  `impersonation.ts` and `keycloakAdmin.ts` now throws
  `StaleStandCredentialError` only on a literal HTTP 401; `withStandFile`
  retries only on that type, logs every attempt, and rethrows anything else
  immediately. Across three full live runs (13 specs each, six total
  `keycloak` container stop/starts from specs 5 and 6) **the retry never
  fired once** — no `stand-only client credential rejected` line in any of
  the three run logs. This is the expected outcome now that the secret is
  stable per AppHost process (`cfca81e`); the retry stayed a true no-op
  safety net, not a crutch papering over a live problem.
- **D-F4 — resolved.** `playwright.config.ts`'s header comment no longer
  claims specs write `accessTokenLifespan`; says only that some specs
  stop/start the container.

#### The live runs

Stand: self-started this session (`dotnet run --project src/backend/Mustask.AppHost`,
this worktree, Keycloak on port 5010). Docker Desktop was **not** running
when this phase began — started it myself (`Start-Process`), ready after
~110s. All numbers below are read from `e2e-results/results.json` after
each run, not from console tallies or any earlier log entry.

| Run | passed | failed | skipped | duration | Notes |
|---|---|---|---|---|---|
| 1 | 13 | 0 | 0 | 317.7s (5.30 min) | `E2E_ALLOW_DOCKER=1`, no `E2E_PASSWORD`/`E2E_KEYCLOAK_ADMIN_PASSWORD` anywhere. Clean. |
| 2 | 12 | 1 | 0 | 345.0s (5.75 min) | Spec 13 (`13-token-size.spec.ts`) failed: `Test timeout of 60000ms exceeded` inside `request.allHeaders()`, after `signIn()` and the `/health` navigation had already succeeded — the retained trace's page snapshot shows the partner-monitoring page **fully rendered with data** ("Все партнёры доступны и синхронизированы."), meaning the browser-side work had already completed and the hang was in Playwright's own IPC step, not in a rejected request. No `StaleStandCredentialError`/retry line anywhere in this run's log — the failure was not the credential-rotation class of bug D-F3 addresses. |
| 3 | 13 | 0 | 0 | 347.4s (5.79 min) | Same suite, same stand, no code or config change in between. Spec 13 passed in 7.2s. |

All three runs are individually within the 15-minute budget (US-1 scenario
3). Stand-file secret (`52DB7B...D493`, recorded from the file after the
first stand-ready signal) was **byte-for-byte identical** every time it was
read — before run 1, after run 1, after run 2, and after run 3 — confirming
`cfca81e`'s per-process-secret fix holds across all six `keycloak`
stop/starts these three runs performed together, not just the one cycle
backend QA measured.

**Run 2's spec-13 failure — judged a transient flake, not a regression:**
reproduced zero times in two immediately-adjacent runs (1 and 3, same
stand, same code); no `withStandFile` retry fired for it (ruling out
D-F3's own area); the page snapshot shows the work the assertion depends on
had already finished when the timeout fired. The most likely explanation
is resource contention specific to this session, not this task's code:
Docker Desktop had just cold-started minutes earlier, and `tasklist`
throughout this session showed several unrelated `dotnet.exe` processes
from sibling worktrees competing for the same machine. Recorded here
rather than silently discarded, per "an empty defect list is a reason to
look harder" — but not filed as a defect against this task, since nothing
in `git diff` for tech-159 touches spec 13, `/health`, or the partner
healthcheck path, and the same spec passed cleanly immediately before and
after.

#### US-1 scenario 2 — the empirical half (credential leak grep)

Grepped the stand-only client's secret value (`52DB7B2783D886778D43AF943BBF71BD516DA493`)
and the literal strings `client_secret`/`clientSecret` across:
`e2e-results/results.json` (both the clean runs 1 and 3, and run 2's
saved copy with its one failure), all three runs' full console output, and
run 2's retained failure artifacts (`trace.zip`, screenshot) while they
still existed. **Zero occurrences anywhere, in any of the three runs.**

One process gap to disclose rather than hide: run 3 (a clean pass) cleared
Playwright's `test-results/` output directory as part of starting, which
deleted run 2's retained trace/screenshot *before* I ran the trace-specific
grep — I had already grepped run 2's console output and its saved
`results.json` (both real, both clean) by that point, but the literal
`trace.zip` binary itself was gone when I checked it directly, so that one
sub-check is inconclusive on the artifact rather than negative. This does
not weaken the conclusion structurally: `impersonation.ts`/`keycloakAdmin.ts`
make every credential-bearing call via `node:http`/`fetch` from the Node
test-runner process, never via `page.evaluate` or anything the browser
executes, and Playwright's trace only records browser-page-level network
activity — so the secret was never reachable by the trace mechanism in the
first place, independent of whether I still had a copy of that one file to
prove it a second way. Lesson for next time: copy a failing run's
`test-results/` aside before starting another run, not just its
`results.json`.

#### Verdict — Phase 2 / Frontend, final

blockers: 0 · majors: 0 · minors: 0 · result: **pass**

All four Phase 1 defects (D-F1–D-F4) independently re-verified this
session, not taken on trust from the fix commit's message: D-F1 by
re-reproducing the exact wildcard evasion and watching it go red under the
widened pattern; D-F2/D-F4 by reading the diffs against the stale text they
replace; D-F3 by three full live runs in which the narrowed retry never
had reason to fire. US-1 scenario 3 satisfied with fresh numbers (13/0/0,
13/0/0, and one isolated non-reproducing flake unrelated to this task's own
code) — all three runs under the 15-minute budget. US-1 scenario 2
satisfied: no credential of any kind found in any retained artifact, by
grep where an artifact still existed and by the calling code's own
Node-only structure where it did not. The stand-only secret's stability
across six container restarts was directly observed, not merely trusted
from the backend's own live-verification. No new defect is being filed for
run 2's spec-13 timeout — it did not reproduce, does not implicate any code
this task touched, and none of D-F1–D-F4's areas were involved.
