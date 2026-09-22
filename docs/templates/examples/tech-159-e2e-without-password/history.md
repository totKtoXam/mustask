# History: tech-159-e2e-without-password

Rolled off `status.md` when it outgrew its budget (`docs/features/README.md`,
"Size budget"). Newest first within the block; the text is verbatim.

## Log entries rolled off 2026-09-12

- 2026-09-12 [orchestrator] route confirmed by the human (form). Checklist:
  block 0 — observable behaviour changes (realm contents, stand startup);
  block 1 — the authentication/authorization threshold fires, so the
  remaining questions were not asked; 3.1 — no regulatory basis, internal
  tooling; 3.2 — no migrations, the database is untouched; 3.3 — "only what
  is in the line". Type tech, size L, gates gate-1 and gate-3
- 2026-09-12 [orchestrator] task opened by hand rather than with
  `scripts/new-task.sh`: the script adds a worktree of its own, while this
  session already works inside worktree `clone-2f8bf0` — a
  second working copy would split the session's paths. Everything else the
  script does was done: branch off `main` (f2cebb7), folder, `status.md` by
  the format of `docs/features/README.md`. The script's guards were checked
  by hand: no `tech/159-*` branch and no `docs/features/tech-159-*` folder in
  any ref
- 2026-09-12 [orchestrator] queue: the `tech-159` line is in
  `docs/backlog.md` of main, removed at start (see the next commit there)
- 2026-09-12 [business-analyst] `spec.md` written: 7 user stories (US-1..7,
  password-free run, AppHost seed, the production-import guard, the real
  login flow kept, fail-fast on a missing client, specs 6/9/11 without
  `E2E_KEYCLOAK_ADMIN_PASSWORD`, specs 5/6 unattended under
  `E2E_ALLOW_DOCKER=1`), 6 business rules, BPMN, not split further in the
  decomposition table (single row, same reasoning as tech-119's). 5 open
  questions left for gate-1 (OQ-1..5: fixture identity, how far the
  AppHost seed reaches, the Docker-specs condition, the pre-existing
  container-name collision, whether the human fallback path stays) plus
  the ADR's three questions. `stage: 02-requirements-review`

- 2026-09-12 [requirements-reviewer] round 1: **needs-human-decision**.
  `review.md` — 4 blockers (the production-import guard is vacuous as
  written; no channel for the client's own secret; US-4's "not mocked" is
  not executable; the impersonated SSO cookie breaks/hollows specs 2, 3, 4,
  10), 9 majors, 4 minors. OQ-4 is a gap the spec should close itself; two
  decisions were taken silently (editing the process docs / the runbook,
  a fourth mandatory secret on a fresh clone)

- 2026-09-12 [business-analyst] defect round 1: all 17 `review.md` findings
  marked `resolved` (rewrote US-3/BR-2 as a closed-allowlist + code-path
  guard, US-4 as observable OIDC-flow checks, added BR-7/US-4 scenario 4 for
  the four clean-context specs, NFRs for secrets/network/attributability per
  D-5, new US-8 for the process-doc edits, new US-9 for reconciliation and
  teardown, US-7 scoped the container resolver, US-1 gained a measured
  unattended run); `spec.md` closed OQ-1..OQ-5 against D-3..D-7 and dropped
  the ADR's now-settled provisioning question. No finding left open.

- 2026-09-12 [requirements-reviewer] round 2 (delta): **needs-fixes**. 15 of
  17 resolutions hold; R-1 holds except one unexecutable sub-clause. R-4
  re-opened as **R-23** (blocker): BR-7/US-4 sc.4 tells spec 10's second
  context to drop `mustask.session` — the cookie it exists to prove — and invents
  a "post-sign-out context" spec 4 does not have. New: R-18 (the guard keys on
  a `realm-management` role list absent from the import files), R-19 (US-7's
  "restored even if aborted" is unachievable in-run; US-9 already covers it),
  R-20 (the gitignored stand file is unnamed and not required to be ignored —
  the only interface between the two stages), R-21 (fixture user needs
  email/first/last or login fails "Account is not fully set up"), R-22 (minor:
  `e2e/**` comments cite a `brief-e2e-playwright.md` that does not exist).
  Nothing new for the human; 593 lines is criteria density, not scope

- 2026-09-12 [business-analyst] defect round 2: R-18..R-23 all marked
  `resolved` in `review.md`. R-23 (the re-opened blocker) fixed by pairing
  each spec correctly — 2/3 get neither cookie, 4 keeps one context across
  sign-out, 10's second context keeps `mustask.session` and drops the Keycloak
  cookie. R-18's guard now keys on `serviceAccountsEnabled` (no role
  vocabulary needed). R-19 stopped claiming the run survives a hard abort,
  cross-referencing US-9 instead. R-20 named the stand file as one path
  `plan-backend.md`/`plan-frontend.md` must agree on, gitignored and
  checked untracked (new US-2 criterion). R-21 added the profile fields
  `keycloak-seed.sh` already proved necessary. R-22 fixed, and its own file
  list corrected against a grep — `keycloakAdmin.ts` doesn't cite the
  dangling doc, `global-setup.ts` and `vite.config.ts` do. No finding left
  open; nothing for the human beyond D-1..D-7


## Log entries rolled off 2026-09-13

- 2026-09-12 [orchestrator] the re-shaped index came back 262 lines against
  the ~250 guidance `stages/01-business-analysis.md` sets. Accepted as is, not
  split and not trimmed: the overage is 5% of a number written with a tilde,
  every line of it is requirement text carried over verbatim, and the only way
  to hit 250 would be rewording BR-1/BR-9 — a substantive edit, which would
  reset `gate-1`. The move itself was verified, not taken on trust: 31
  `Scenario:` blocks and 31 `Given` steps before, the same 31 of each after
- 2026-09-12 [human → orchestrator] **gate-1: approved**, and the task is
  paused right after it: the human asked for specs to be split into
  per-user-story files with declared dependencies, so that a stage session
  reads one story instead of the whole spec and independent stories can run in
  separate sessions. That is a separate docs task, to be done **before**
  tech-159's stage 4; this spec is then re-shaped as its first application.
  The story stays a unit of reading — the task keeps one number, one branch and
  one merge, as today
- 2026-09-12 [human → orchestrator] four gate-1 decisions taken by form after
  review round 1 (D-3..D-6). They close blocker R-2 and open questions
  OQ-1/OQ-2/OQ-3; OQ-4 stops being a question (D-6 makes the narrowing a
  requirement). Decided by the orchestrator, not the human, and recorded as
  requirements for the fix round: the process documents this task falsifies
  are edited in it (R-10, precedent tech-119 D-32), and the agent brings down
  the stand it started (R-12, `AGENTS.md` hard rule)
- 2026-09-12 [business-analyst] re-shaped into the tech-160 per-story format:
  `spec.md` cut from 652 to 262 lines (index only — context, roles, story
  table, BPMN, business rules, operation completeness, NFRs, out of scope,
  decomposition, open questions); the 9 stories moved verbatim to
  `stories/US-<N>-*.md` (29–74 lines each). A pure move — no criterion
  added, dropped or reworded; `gate-1: approved` stands. `depends-on`:
  US-7→US-9 and US-9→US-1,US-7 (the only cross-story text references);
  every story `shippable: no` — one capability, same shape as the
  US-2/US-3 example in the new rule. Index is 262 lines against the ~250
  guidance — reported, not trimmed to fit
- 2026-09-12 [backend-developer] phase 1 of stage 4 (plan before code, per
  the task instructions): `plan-backend.md` and ADR-0023 (proposed) written
  for US-2/US-3, no code yet. Fixes two identifiers `plan-frontend.md` must
  reuse verbatim — client `mustask-e2e`, fixture user `e2e-fixture` — and the
  stand file BR-1 names, `.e2e-stand/keycloak-e2e-client.json`. Chose the
  admin impersonation REST endpoint over RFC 8693 token exchange (the
  latter cannot produce a browser-facing Keycloak session, which BR-3's
  real-OIDC-flow criterion needs) and a runtime admin-API call from
  `Mustask.AppHost` over a realm-import entry (`--import-realm` cannot add a
  client to an existing realm). Flagged two scope calls for the human at
  gate-3: no new test project/`Testcontainers.Keycloak` package (both touch
  orchestrator-only files, `Mustask.sln`/`Directory.Packages.props`) — US-2's
  live-behaviour scenarios move to a QA-time check on a real stand instead
  of an automated Docker-backed test.
- 2026-09-13 [backend-developer] phase 2: code + tests for US-2/US-3, per the
  approved plan plus three additions the coordinator required first (the
  regression-gap statement, the stand-file anchor rule, a staleness field) and
  one question answered (the Keycloak container's Docker name is assigned by
  AppHost via `.WithContainerName`, not discovered — no public way to read
  DCP's own generated id back was found in `Aspire.Hosting` 13.5.1). Built:
  `Mustask.AppHost/Provisioning/KeycloakStandProvisioner(Options).cs`, wired into
  `AppHost.cs` via `ResourceReadyEvent`; 4 new unit tests
  (`Mustask.Contracts.Tests/KeycloakStandProvisioningTests.cs`, fake HTTP handler,
  each driven red before green, one real bug caught this way — a substring
  match in the architecture guard passed vacuously against a renamed class,
  fixed to a word-boundary regex); 2 new facts on `KeycloakRealmRolesTests.cs`
  (US-3 scenarios 1–2, each confirmed red against a deliberately broken
  allowlist/`serviceAccountsEnabled` before being restored); 3 new facts in
  `Mustask.Architecture.Tests/StandOnlyKeycloakClientRules.cs` (US-3 scenario 3).
  `dotnet test Mustask.sln` green (all projects, Docker-backed integration tests
  included). Live-verified on a self-started, self-stopped stand (D-6): the
  client/user/roles/group come up correctly, the stand file is untracked, an
  impersonation call returns the real `KEYCLOAK_IDENTITY`/`KEYCLOAK_SESSION`
  cookies, and the attributable event is in `/events` (`type: IMPERSONATE`),
  not `/admin-events` — recorded in `plan-backend.md` §4 since it wasn't
  obvious beforehand. No US-2/US-3 criterion turned out unimplementable.
  Open for the human at gate-3: the stale-container-name risk `plan-backend.md`
  §3a names (accept, or add a `docker rm -f` pre-step), and whether the
  no-live-regression-test trade in §5/§7 is acceptable as scoped.
- 2026-09-13 [qa] Backend QA (`qa.md`): **fail, 0 blockers / 2 major / 2
  minor** — no re-request of the E2E question here (this task continues to
  stage 6, so that question is frontend QA's per the routing rule). Guard
  experiments (each reverted, tree confirmed clean): the closed-allowlist
  and no-service-account facts and the not-vacuous rename guard all went red
  for the right reason; the fourth architecture fact does not — a
  reimplementation of the client-creation call under an unrelated
  name/namespace in a shipping project passes all 11 facts (D-1). Live,
  self-started/self-stopped stand (three start/stop cycles, one forced kill)
  reproduced the plan's own §3a risk differently than predicted: DCP
  recovers silently from a stale container name (no start failure), but the
  stand file can hold the *previous* run's rejected secret for up to ~90s
  after AppHost's own "started" banner while Keycloak/provisioning catch up
  — the `issuer` staleness field doesn't catch this, since it's the same
  stand (D-2, flagged for stage 6's global setup to poll/retry, not
  read-once). Also recorded: no success/failure log line around
  provisioning (D-3, minor); no regression guard for US-2 scenario 5's
  seed-disjointness (D-4, minor); the `Mustask.Contracts.Tests` →
  `Mustask.AppHost` `ProjectReference` judged unsound at the build-dependency
  level even though runtime-safe (43MB build output vs. 5.9MB for a
  comparable sibling test project) — opinion recorded in `qa.md`, not
  rewritten.

## Log entries rolled off 2026-09-15

- 2026-09-13 [orchestrator] a stalled QA session left its own experiment
  behind — a class under `Mustask.Api/Features/` that creates an
  impersonation-capable Keycloak client, written to prove a guard red and
  never deleted because the agent died first. Untracked, so no commit was at
  risk, but it sat in the worktree where the next agent would read it as
  code. Removed; the tree matched `HEAD` afterwards. The lesson is the
  orchestrator's: check `git status --porcelain` after every agent, not only
  after the ones that finish

- 2026-09-13 [backend-developer] QA round 1 fixes (`plan-backend.md` §10 has
  the full account). D-1: guard the capability, not the name — verified live
  that no shipping project has a legitimate reason to call `/admin/realms/`
  today, added `ShippingProject_DoesNotCallKeycloaksAdminApi`, proved it red
  against QA's own `SneakyKeycloakClientCreator` reproduction (the namespace
  fact stayed green throughout, confirming the two checks are independent).
  D-2: the stand file is now deleted as the very first statement in
  `AppHost.cs`, before any resource is declared — live-verified with the
  same force-kill QA used: the file was absent for the whole window, not
  merely stale, reappearing only with a genuinely new secret once Keycloak
  was healthy again. D-3: `ILogger` lines before/after/on-failure around
  provisioning, never logging the secret. D-4: two new facts
  (`KeycloakStandDisjointnessTests`) plus the two identifiers promoted to
  `KeycloakStandIdentifiers` so the guard compares one authoritative value,
  not a hand-typed copy. Non-defects: ADR-0023 Consequences gained the
  attributability credential-scope finding (`mustask-e2e`'s own token gets 403,
  missing `view-events`); the `Mustask.Contracts.Tests` → `Mustask.AppHost`
  `ProjectReference` is gone, replaced by a `<Compile>`/`LinkBase` item —
  build output measured 43 MB → 20 MB, `Aspire.Hosting.AppHost`/`.PostgreSQL`/`.JavaScript`
  gone from it entirely. `dotnet test Mustask.sln` green (all projects). No
  disagreement with anything in `qa.md`.
- 2026-09-13 [qa] Backend QA round 2 (`qa.md`): **pass, 0/0/0**. D-1
  re-reproduced live (the new capability fact goes red naming the offender,
  the namespace fact stays green) then reverted; guard-boundary paragraph
  added to `qa.md` for future readers. D-2 re-verified live across two
  force-kill/restart cycles: the stand file is absent for the whole window,
  not stale, secret and container id both differ afterward; the per-worktree
  path derivation and the two-simultaneous-stands question are now checked,
  not merely claimed. D-3 confirmed by log grep (no secret in either run's
  output) plus a read of the failure path. D-4 judged sound as an asymmetric
  guard, no hole found. Project-reference fix confirmed: 20 MB measured,
  both files compile independently into both assemblies. Stand self-started/
  self-stopped, `dotnet build-server shutdown` run after.
- 2026-09-13 [frontend-developer] `plan-frontend.md` written, then `e2e/**`
  wired onto impersonation (US-1/US-4/US-5/US-7/US-9) and docs updated
  (US-8). Two live-only bugs found and fixed, neither visible from reading
  the code: Keycloak binds a token's/cookie's `iss` to the request's `Host`,
  and `fetch()` silently overwrites a caller-set `Host` — impersonation now
  uses `node:http` with the browser's own `Host` preserved
  (`impersonation.ts`); and `signIn`'s old `waitForURL(origin===panel)`
  race-passed before the redirect-to-Keycloak-and-back round trip even
  started — fixed to wait for `/api/session/callback` first. Also found:
  Aspire re-fires `ResourceReadyEvent` when specs 5/6 stop/start the
  container directly, rotating `mustask-e2e`'s secret mid-run — `docker.ts` now
  refreshes the cached stand file after every restart it performs.
  Confirmed live: specs 6/9/11 all need `manage-realm`/`view-realm`, which
  `mustask-e2e` does not hold (only `impersonation`+`manage-users`) — they skip,
  naming the gap, not silently patched; not this stage's to widen. Full
  suite, self-started/self-stopped stand, `E2E_ALLOW_DOCKER=1`, no
  `E2E_PASSWORD`/`E2E_KEYCLOAK_ADMIN_PASSWORD`: **10 passed, 3 skipped
  (6/9/11, named), 0 failed, 63s** — well under the 15-minute budget.
  `scripts/verify.sh` green including the structural guard (proven red
  first). `dotnet build-server shutdown` run after.
- 2026-09-14 [backend-developer] D-8: `KeycloakStandProvisioner` now sets
  realm `mustask`'s `accessTokenLifespan` to `120` (get-modify-put, the master
  admin token it already holds — never `mustask-e2e`), value named once in
  `KeycloakStandIdentifiers.AccessTokenLifespanSeconds` (`plan-backend.md`
  §11 has the full account). Added a dedicated role-set guard
  (`ProvisionAsync_NeverGrantsMoreThanImpersonationAndManageUsers`, proven
  red first) naming the human's own condition — `mustask-e2e` stays at exactly
  `impersonation`+`manage-users`. Live-verified on a self-started/stopped
  stand: realm reports `120` after a fresh start and again after a graceful
  restart (re-applied, not persisted); `mustask-e2e`'s live role-mapping
  unchanged; every other realm field survived the write. `dotnet test
  Mustask.sln` green. The runbook sentence is in `plan-backend.md` §11 for the
  coordinator to route to the frontend developer's US-8 edit.
- 2026-09-14 [frontend-developer] Consumed D-8: specs 6/9/11 dropped
  `setAccessTokenLifespan`/the skip/the restore logic entirely;
  `keycloakAdmin.ts` lost the realm-write helpers and
  `KeycloakAdminForbiddenError` (no caller left); `reconcile.ts` lost its
  `accessTokenLifespan` reconciliation (targeted the now-wrong value, 900,
  and nothing on this side can drift 120 anymore). Two more live findings
  surfaced only by re-running the Docker-restarting specs back to back:
  AppHost's `ResourceReadyEvent` can keep re-firing (rotating `mustask-e2e`'s
  secret) for up to ~30s *after* a restart, past `docker.ts`'s one-shot
  post-restart refresh — `standFile.ts`'s `withStandFile` now retries in a
  bounded loop, used by every credential-using call. Runbook sentence
  placed verbatim; `src/frontend/AGENTS.md` § E2E carries the same
  two-minute-renewal fact. **Full suite, self-started/self-stopped stand:
  13 passed, 0 skipped, 0 failed, 456s.** `scripts/verify.sh`/build/lint/
  test green; `dotnet build-server shutdown` run after.

- 2026-09-15 [backend-developer] fixed the secret-rotation finding from the
  round above at its source (`plan-backend.md` §12): the client secret is
  now generated once per AppHost process (`KeycloakStandClientSecret`, a
  `Lazy<string>`), not once per `ResourceReadyEvent` firing — DCP's repeat
  firings after a bare `docker stop`/`start` now re-provision `mustask-e2e` with
  the *same* value every time. Checked the other steps as asked, not
  assumed: `accessTokenLifespan`, the role grant, the fixture user and its
  group membership were all already convergent (confirmed live, not only
  reasoned about); the secret was the one non-idempotent piece. New tests
  (`KeycloakStandClientSecretTests`, a two-pass convergence test on
  `KeycloakStandProvisioner`), each proven red first. Live-verified with
  the exact sequence that found the bug: stand up, `docker stop`/`start`
  the keycloak container, ~55s wait — `ResourceReadyEvent` fired again, the
  stand file's secret was byte-for-byte unchanged and still authenticated.
  `dotnet test Mustask.sln` green. Recommendation for the frontend's
  `standFile.ts` retry: keep it as is — it still covers the ordinary D-2
  window (a fresh stand start, before the first provisioning pass
  completes), which this fix does not touch.
- 2026-09-15 [qa] Frontend QA phase 1 (`qa.md`), no stand started: **fail,
  pending phase 2** — 0 blockers, 2 majors. D-F1: the structural guard
  misses a `page.route('**/api/session/*', ...)` wildcard stub (confirmed
  live: caught a direct `mustask.session` assignment and a named-endpoint stub,
  missed the wildcard one), reproduced then deleted. D-F2: US-9 scenario 1's
  own text still says "restore `accessTokenLifespan` to 900," unchanged
  since before D-8 — the shipped `reconcile.ts` (correctly) doesn't, D-8
  made 900 the wrong value. Two minors (D-F3: `withStandFile`'s 30s retry
  is now broader than its post-D-12 justification; D-F4: `playwright.config.ts`'s
  header still claims specs write `accessTokenLifespan`). Phase 2 (live run)
  awaits the human's E2E-question answer.
