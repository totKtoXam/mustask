# Plan — frontend (tech-159-e2e-without-password)

Stories covered: **US-1, US-4, US-5, US-6, US-7, US-8, US-9**. Consumes the
literal contract `plan-backend.md` §1/§6 fixes (identifiers, stand file path
and shape) verbatim — not re-derived here. Backend QA round 2: pass,
0 blockers (`qa.md`). D-2's live-measured ~90s window and the guard-boundary
paragraph are both binding on this stage (see §2).

No screens change — this stage's whole surface is `src/frontend/e2e/**` plus
the documents US-8 names.

## 1. Files touched

New:
- `e2e/support/standFile.ts` — resolves/polls/validates the stand file
  (§2). Anchor: `path.resolve(dirname(fileURLToPath(import.meta.url)), '..',
  '..', '..', '..', '.e2e-stand', 'keycloak-e2e-client.json')` from
  `e2e/support/*.ts` — four levels up to the repo root, exactly
  `plan-backend.md` §6's suite-side formula.
- `e2e/support/impersonation.ts` — the Node-only client-credentials +
  impersonation calls that replace typing a password (§3).
- `e2e/support/reconcile.ts` — US-9 scenario 1 (§6).
- `src/test/e2eSessionGuard.test.ts` — the structural guard (US-4 scenario 2,
  §5), a Vitest test so it runs inside `npm test`/`scripts/verify.sh`
  (Playwright's own suite is excluded from both, unchanged).

Changed:
- `e2e/support/env.ts` — `password()`/`keycloakAdminPassword()` removed
  (D-7); `user` default becomes `'e2e-fixture'` (`plan-backend.md` §1).
- `e2e/support/auth.ts` — `signIn` obtains the SSO cookie via impersonation
  instead of typing a password (§3); same signature, so every call site is
  unchanged except spec 10 (§4).
- `e2e/support/keycloakAdmin.ts` — admin calls authorize through `mustask-e2e`'s
  own service-account token instead of `admin-cli` + a human password (§6a).
  Originally grew a `KeycloakAdminForbiddenError` for the realm-write gap
  §6b found; removed again once D-8 closed that gap from the backend side —
  the module now holds only `logoutUserSessions`, which is all it ever needs
  again (§6b).
- `e2e/support/docker.ts` — `resolveKeycloakContainer` uses the exact name
  from the stand file instead of `docker ps --filter name=keycloak`'s first
  match (§7); adds `inspectContainerState` for reconciliation.
- `e2e/support/cookies.ts` — `withoutKeycloakSsoCookies` for spec 10 (§4).
- `e2e/global-setup.ts` — drops the `password()` check, adds the stand-file
  fail-fast (§2) and the reconciliation call (§6).
- `e2e/specs/01-deep-link.spec.ts` — carries the one OIDC-round-trip
  observation US-4 scenario 1 asks for (§3b).
- `e2e/specs/02-login-page.spec.ts`, `03-wrong-password.spec.ts` — comments
  updated; spec 3 stops reading `env().user` (§4).
- `e2e/specs/05-logout-keycloak-down.spec.ts`,
  `06-renewal-keycloak-down.spec.ts` — comments only for 05; 06 dropped the
  admin-password skip in the first pass, then (D-8) the capability-detection
  skip and its own `setAccessTokenLifespan` call too — the realm is already
  short-lived by the time the spec runs, nothing left to set or restore
  (§6b).
- `e2e/specs/09-token-renewal-two-tabs.spec.ts`,
  `11-session-ended-by-admin.spec.ts` — same arc as 06: admin-password skip
  → capability-detection skip → (D-8) no skip, no realm write, no restore
  (§6b).
- `e2e/specs/10-persistent-cookie.spec.ts` — the BR-7 fix: strips the
  Keycloak SSO cookie from the second context's storage state, asserts
  `mustask.session` explicitly (§4).
- `src/frontend/playwright.config.ts`, `e2e/support/auth.ts`,
  `e2e/support/env.ts`, `e2e/global-setup.ts`, `vite.config.ts` — header
  comments rewritten, pointing at `src/frontend/AGENTS.md` § E2E instead of
  the two dangling documents (US-8, §8).
- `src/frontend/AGENTS.md`, `docs/runbooks/stand.md`,
  `docs/ai-workflow/stages/04-backend.md`, `05-frontend.md` (US-8, §8).
- `docs/templates/examples/tech-159-e2e-without-password/status.md` — `## Log`.

Not touched: `src/backend/**`, `deploy/keycloak/**`, `.gitignore` (already
covers `.e2e-stand/`, backend's stage).

## 2. The stand file: read, poll, verify — never read once

`e2e/support/standFile.ts` exports `loadStandFile()`, module-cached per
worker process (workers: 1, so effectively once per run):

1. **Poll for the file's existence**, not a single read — `plan-backend.md`
   §10/D-2, live-measured: up to ~90s between Aspire's "started" banner and
   the fresh file appearing after a cold/force-killed restart. Interval 2s,
   timeout 120s (D-2's own number plus margin). `ENOENT` retries; any other
   read/parse error is not retried — a malformed file is a real defect, not
   a timing artifact (the backend writes it in one step after provisioning
   succeeds, never partially).
2. **Timeout → US-5 scenario 1** ("client does not exist"): one message
   naming the path and pointing at `dotnet run --project src/backend/Mustask.AppHost`.
3. **Issuer mismatch → US-5's "wrong stand" case**, named precisely per
   `plan-backend.md` §6's own reasoning (a leftover file from a different
   stand/port/realm looks byte-identical otherwise).
4. **Secret rejected by Keycloak → US-5 scenario 2**: a `client_credentials`
   grant is attempted once against the file's own `clientId`/`clientSecret`;
   a non-2xx response fails naming that mismatch, before any spec runs.

Global setup calls `loadStandFile()` right after the realm reachability
check, so a genuinely dead stand still reports through the older, unrelated
error first. (Code review round 1, F1: the realm check itself now runs
*after* §8's reconciliation, not before it — see §8.)

## 3. Sign-in without a password (US-1, US-4)

### 3a. The mechanism (`e2e/support/impersonation.ts`)

Entirely on the Node side, per US-1 scenario 2 — no browser `fetch`, no page
script:

1. `client_credentials` grant for `mustask-e2e` (stand file's own secret) →
   service-account access token.
2. `GET /admin/realms/mustask/users?username=e2e-fixture&exact=true` (same
   token) → the fixture user's id.
3. `POST /admin/realms/mustask/users/{id}/impersonation` (same token) — the
   response's `Set-Cookie` headers (`response.headers.getSetCookie()`, Node
   23's native `fetch`/undici, confirmed available) are `KEYCLOAK_IDENTITY`
   and `KEYCLOAK_SESSION`.
4. Parsed into Playwright cookie objects with `domain: new
   URL(keycloakUrl).hostname`, `path: '/'` — handed to
   `page.context().addCookies([...])` before any navigation.

**Named limitation, not glossed over:** "only on Keycloak's own origin"
means the code never asks for a broader domain (no wildcard, no `.localhost`)
— it does not mean the browser enforces port isolation on `localhost`.
`env.ts`'s own comment and `cookies.ts`'s already document that Chromium's
cookie jar is not port-scoped on a shared hostname (tech-119 finding, the
`:5099` probe). This task does not change that platform fact; the guard in
§5 checks what the suite's own code does, not what the browser does with it
afterward.

`auth.ts`'s `signIn(page, options)` keeps its existing signature:

```ts
export async function signIn(page: Page, options: { returnPath?: string } = {}): Promise<void> {
  const { panelUrl } = env();
  const cookies = await obtainKeycloakSsoCookies();
  await page.context().addCookies(cookies);
  await page.goto(options.returnPath ?? '/dashboard');
  await page.waitForURL((url) => url.origin === new URL(panelUrl).origin, { timeout: KEYCLOAK_FORM_TIMEOUT });
}
```

No spec other than 10 needs to change to keep working — the whole point of
keeping the signature identical.

### 3b. The one recorded OIDC round trip (US-4 scenario 1)

Only spec 1 (deep link) carries the instrumentation — not all thirteen, per
the story's "for at least one spec": registering listeners on every `signIn`
call would duplicate the same assertion twelve times for no added coverage.
Spec 1 registers `page.on('request'/'response')` **before** calling
`signIn`, then asserts:
- a request whose URL starts with
  `{keycloakUrl}/realms/mustask/protocol/openid-connect/auth`;
- a response for `{apiUrl}/api/session/callback`, whose
  `headersArray()` contains a `set-cookie` entry starting with
  `mustask.session=`;
- `getSessionCookie(page.context())` is `undefined` immediately after cookie
  injection and before `page.goto`, `truthy` after.

### 3c. Spec 3 and the brute-force counter (US-4 scenario 3)

Spec 3 stops reading `env().user` (now `'e2e-fixture'`, needed by every
other spec on every run) and submits its deliberately-wrong password against
a literal, non-existent username (`e2e-wrong-password-probe`) local to the
spec file. Keycloak's login form gives the same generic rejection either
way (no username enumeration), and `bruteForceProtected: true` never
accumulates failed attempts against the account an unattended run
repeatedly needs to sign in as.

### 3d. Spec 10: `mustask.session` must be the only explanation (US-4 scenario 5)

The reviewer's finding this plan must not repeat: carrying the *whole*
`storageState` from context 1 into context 2 (as the pre-review version did)
proves nothing, because the Keycloak SSO cookie would still be present and
could explain the dashboard rendering via a silent re-login, not
`mustask.session`. Fix: `cookies.ts` gains
`withoutKeycloakSsoCookies(cookies)` (filters any cookie name starting with
`KEYCLOAK_`); spec 10 applies it to `storageState.cookies` before opening
context 2, then asserts `getSessionCookie(context2)` is truthy in addition
to the dashboard rendering.

## 4. The context rule (BR-7) — mostly free, two deliberate exceptions

Playwright gives every `test()` its own fresh, cookie-less browser context
by default. Since only `signIn`/`obtainKeycloakSsoCookies` ever injects a
cookie, specs 2 and 3 (which never call `signIn`) automatically start with
neither cookie — no code needed for that half of BR-7. Spec 4 already uses
one `page`/implicit `context` for its whole test body (sign-in → sign-out →
re-open) — nothing recreates it, so that guarantee already held before this
task and keeps holding after `signIn`'s internals change. The two places
that needed real work were §3b (recording the round trip) and §3d (spec
10's second context) — both above.

## 5. The structural guard (US-4 scenario 2)

`src/test/e2eSessionGuard.test.ts`, a plain Vitest test (not a Playwright
spec — it never runs the suite, only reads its source), scans every
`e2e/**/*.ts` file's text for two patterns:

1. `name:\s*['"]mustask\.session['"]` — an object-literal cookie *assignment*
   named `mustask.session` (e.g. inside an `addCookies` call). Deliberately
   narrower than a bare substring match: `cookies.ts`'s own
   `cookie.name === 'mustask.session'` (a *read*, comparing an already-observed
   cookie) must not trip it — a `===` comparison never matches this
   assignment-shaped regex.
2. `\.route\(\s*[\`'"][^\`'"]*\/api\/session\/(login|callback|token)` —
   Playwright's own mocking primitive (`page.route`/`context.route`) aimed
   at any of the three session endpoints.

**Guard boundary, stated up front the way `qa.md`'s backend section states
its own** (read before treating this as more than it is): a text scan over
source cannot stop a determined rewrite — string concatenation, a cookie
name built from a template literal, `page.route(new RegExp(...))` built at
runtime, or moving the pattern into a helper this scan doesn't cover all
defeat it with no real effort. It is not a security control against an
adversarial contributor — anyone editing `e2e/**` could equally delete this
test. Its real job is the one named in the story: catching the *innocent*
fix — a future author "repairing" a flaky login by fabricating the cookie or
stubbing the callback, leaving thirteen green specs that prove nothing —
and failing loudly enough that code review sees it. Proven red before green
(§9): a temporary violating line was added to a scratch file under `e2e/`,
the guard test failed naming it, then the line was removed.

Runs inside `npm test`, which `scripts/verify.sh`'s frontend section already
calls — satisfying "the same `scripts/verify.sh` pass as the rest of the
suite's own checks" without a new script or CI step.

## 6. The admin-API specs (US-6) — a rights gap opened, then closed by D-8

### 6a. What moved cleanly, from the start

`keycloakAdmin.ts`'s `logoutUserSessions` (spec 11's revocation) needs only
`manage-users` on `realm-management` — a role `mustask-e2e` already holds
(`KeycloakStandProvisioner.cs`: `ImpersonationRoles = ["impersonation",
"manage-users"]`). Routing it through a `client_credentials` token for
`mustask-e2e` instead of `admin-cli` + `E2E_KEYCLOAK_ADMIN_PASSWORD` was a
straight swap from the start, unconditional.

### 6b. `accessTokenLifespan` — the gap this stage escalated, closed by D-8

Specs 6, 9 and 11 also needed to shorten the realm's access-token lifetime
so a renewal is observable without waiting out the real 900s default. That
write is `GET`/`PUT /admin/realms/mustask` — gated on `view-realm`/`manage-realm`,
which `mustask-e2e` deliberately does not hold (only `impersonation` +
`manage-users`, confirmed by reading `KeycloakStandProvisioner.cs` directly).
The first pass through this stage routed the three specs through `mustask-e2e`
anyway, got a live 403, and made them skip naming the exact gap
(`KeycloakAdminForbiddenError`) rather than widen the client itself — per
this stage's own instructions, that decision belongs to the backend/security
side, not here.

**Escalated, not absorbed — and the human decided D-8** (`status.md`): do
not widen `mustask-e2e`; instead **AppHost sets the realm's
`accessTokenLifespan` itself, once, at every stand start**, with the admin
credentials `KeycloakStandProvisioner` already holds for the client/user
steps (`SetAccessTokenLifespanAsync`, `plan-backend.md` §11). The value —
120 seconds, matching what these specs already hardcoded before any of this
— now lives in exactly one place, `KeycloakStandIdentifiers.AccessTokenLifespanSeconds`,
named in the specs' own comments so the coupling is not silent.

**What this stage did once D-8 landed:** removed `setAccessTokenLifespan`/
`getAccessTokenLifespan`/`KeycloakAdminForbiddenError` from
`keycloakAdmin.ts` entirely — no caller is left, and no realm-write helper
stays behind for a future change to reach for out of convenience (that is
exactly the shortcut D-8 declined). Specs 6, 9 and 11 dropped their own
`setAccessTokenLifespan` calls, their `try`/`catch`/`test.skip` gap
detection, and their `afterEach`/`afterAll` restore logic — there is nothing
left to write and nothing left to restore. `reconcile.ts`'s own
`accessTokenLifespan` reconciliation (§8) is gone for the same reason: it
targeted the wrong, now-obsolete value (900) and nothing on the suite's side
can drift it from 120 anymore.

**Result, live-verified in §9:** all three specs run, none skip. US-6 is
now fully delivered — not the half this plan originally flagged.

## 7. Docker container resolution (US-7)

`docker.ts`'s `resolveKeycloakContainer()` reads the stand file's own
`keycloakContainerName` (`plan-backend.md` §3a: assigned by AppHost from
`Ports:Keycloak`, not discovered) instead of `docker ps --filter
name=keycloak`'s first match. This does not just narrow the ambiguity the
story describes — **it removes the possibility structurally**: since the
suite now targets one exact, known name (verified to exist via `docker
inspect`, which either finds that one container or fails), there is no
"first match among several" step left to go wrong. A `docker inspect`
failure fails with one message naming the missing container and the start
command, mirroring `global-setup.ts`'s existing precedent.
`E2E_ALLOW_DOCKER=1` stays the only gate for specs 5/6 actually acting on it
— unchanged, per D-6/US-7 scenario 3 (not the default).

## 8. Reconciliation (US-9) and process documents (US-8)

`e2e/support/reconcile.ts`, called from `global-setup.ts` after the
panel/`Mustask.Api` reachability checks but *before* the realm reachability
check and before §2's fail-fast (`loadStandFile()`) — best-effort, logging
rather than failing the run, since that is not what US-5's fail-fast is
about. (Code review round 1, F1: the container repair was originally placed
after the realm check, which is the one thing a stopped container makes
fail — so the repair could never run in the case it exists for. It now
reads the stand file with `standFile.ts`'s `peekStandFile()`, a plain read
with no secret verification, precisely because verification needs Keycloak
already up.)

- **The `keycloak` container's state**: `docker inspect
  --format {{.State.Status}}` on the stand file's own container name; if not
  `running`, started and logged. Not gated on `E2E_ALLOW_DOCKER` — this is
  housekeeping to make the *whole* stand usable (every spec needs Keycloak
  reachable, not just 5/6), not a spec controlling Docker as its own test
  behavior (that stays gated, §7).
- **`accessTokenLifespan` needs no reconciliation anymore (D-8, §6b).** It
  originally targeted "restore to 900 if a killed spec left it shortened" —
  once AppHost itself became the only thing that ever sets this value
  (always to 120, at every stand start, including after a bare container
  restart), there was no drift left for this module to find: the "expected"
  value this reconciliation held (900) was also simply wrong once the stand
  deliberately runs at 120. Removed rather than left to warn about a
  now-nonexistent problem.

US-8's document updates (`src/frontend/AGENTS.md` § E2E,
`docs/runbooks/stand.md` § E2E, `04-backend.md`/`05-frontend.md`'s
"green per the human's run" wording, and the five header comments naming
the two dangling documents) are done in the same commit series as the code,
not deferred — per BR-9/tech-119 D-32's own precedent, and because leaving
them stale is exactly what US-8 exists to prevent.

## 9. Order of work and live verification

1. `standFile.ts` + `impersonation.ts`, unit-free (no fake-handler tests are
   added here — this stage has no dedicated unit-test project the way
   `Mustask.Contracts.Tests` is for the backend; correctness is proven by the
   live run in step 5, per `docs/conventions/frontend.md`'s own "E2E happy
   path... at the QA stage" — brought forward here because this task's
   whole point is the agent running it itself).
2. `auth.ts`, `keycloakAdmin.ts`, `docker.ts`, `cookies.ts`, `reconcile.ts`,
   `global-setup.ts`.
3. Spec-by-spec updates (§3b/c/d, §6b).
4. `src/test/e2eSessionGuard.test.ts` — proven red (a deliberate violation
   added, test run, failure confirmed naming it), then the violation
   reverted and the test green.
5. **Live run** (self-started/self-stopped stand, per D-6/AGENTS.md hard
   rules): `dotnet run --project src/backend/Mustask.AppHost`, then `npm run e2e`
   from `src/frontend` with `E2E_ALLOW_DOCKER=1` and no `E2E_PASSWORD`/
   `E2E_KEYCLOAK_ADMIN_PASSWORD` anywhere — iterated until green or until
   every remaining red/skip is understood and named. First pass (pre-D-8):
   10 passed, 3 named skips (6/9/11, §6b), 0 failed. Post-D-8: two more live
   findings surfaced only by running the specs that restart Docker back to
   back — AppHost's `ResourceReadyEvent` can keep re-firing (and therefore
   keep rotating `mustask-e2e`'s secret) for up to ~30s *after* a restart
   finishes, not just once at restart time; a single post-restart refresh
   (`docker.ts`) caught it for one spec (`08-cookie-size.spec.ts`) but not
   the next run's (`06-renewal-keycloak-down.spec.ts`) — so `standFile.ts`'s
   `withStandFile` retries in a bounded loop (30s/2s), used by every
   credential-using call, not a one-shot refresh tied to a specific restart.
   **Final result: 13 passed, 0 skipped, 0 failed, 456s** (7.6 minutes,
   under the 15-minute budget). `qa.md`'s Frontend section is the next,
   separate QA session's to write, not this stage's — recording the raw
   numbers here and in `status.md` is what this stage can do without
   crossing that line.
6. `npm run build`, `npm run lint`, `npm test`, `bash scripts/verify.sh`
   (through `C:\Program Files\Git\bin\bash.exe`) — all green before
   handoff.
7. Stand torn down, `dotnet build-server shutdown`.
