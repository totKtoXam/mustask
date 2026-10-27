# Frontend commands — React + TypeScript + Vite

Scoped instructions: this file is loaded when the work happens under
`src/frontend/`. The project's rules — the pipeline, the hard rules, the
language policy — stay in the root `AGENTS.md`; here are the commands only.
Conventions for the code itself: `docs/conventions/frontend.md`.

Node 23. The dev server is pinned to port 3000 — that port is in the
`Mustask.Api` CORS list — and listens on all interfaces.

- `npm ci` (in `src/frontend/`) — install dependencies. In a fresh
  worktree run `bash scripts/warm-worktree.sh` from the repository root
  instead: it copies an installed `node_modules` for this lock file from a
  machine-wide cache (seconds) and falls back to `npm ci` only when the
  lock file is new to the machine.
- `npm run dev` — the Vite dev server (needs a `.env` with
  `VITE_API_BASE_URL`, see `src/frontend/.env.example`; when started through
  AppHost the variable is substituted automatically).
- `npm run build` — type check (`tsc -b`, covers `src/` and `e2e/`) +
  production build.
- `npm run lint` — ESLint; `npm run format` / `format:check` — Prettier.
- `npm test` — Vitest (Testing Library + MSW) in single-run mode. Never picks
  up `e2e/**` (`vite.config.ts` excludes it explicitly).
- While you work, run Vitest on your own files (`npx vitest run
  src/features/<x>`), not the whole suite; `npm run test:fast` is the whole
  suite without the heavy page tests listed in `src/test/slowFiles.json` (about
  a third of the full run; regenerate the list with `node scripts/slow-files.mjs
  <vitest-json-report>`). `bash scripts/verify.sh` runs the
  frontend tests that reach what the branch changed
  (`vitest run --changed <merge-base>`); a change to `src/test/**`,
  `src/app/theme/**` or `src/shared/ui/**` runs everything on its own, and the
  tests that read sources off disk (kit guard, colour scan, e2e guards) always
  run next to the affected ones. Force the whole suite with `VERIFY_FULL=1 bash
  scripts/verify.sh` — `finish-task.sh` and CI (the required check on every
  pull request) do, so a merge is always gated by a full run; the pre-push hook
  runs the quick one, and `VERIFY_FULL=1 git push` makes it full — instant
  when a green full run of the same tree and merge-base, on a clean working
  tree, already left its stamp in the git directory (`VERIFY_FORCE=1` repeats
  it; use it after changing `node_modules` or the environment). A full run holds a lock
  in the shared git directory (`vitest-full.lock`): only one runs at a time
  across worktrees, the next one waits (a lock older than 60 min or with a dead
  owner is taken over).

## UI kit — build from it, add to it first

A screen is built from the panel's UI kit (`src/shared/ui`, tokens in
`src/app/theme`) and from nothing the kit already provides: do not import a MUI
part the kit wraps (`TextField`, `Button`, `Menu`, `Snackbar`, `Alert`, `Paper`,
the `Table*` family…), do not draw a surface, icon button, menu, notice or page
head of your own (no `<input>`, `<button>`, `<h1>`, no `component="h2"`), do not
style MUI's insides (`.Mui…`). A pattern the kit lacks goes into
the kit first — component, test, showcase entry, guide block — and is then used.
Read `docs/conventions/ui-kit.md` before you write a screen; the panel's
«Витрина компонентов» (`/ui-kit`, administrators) shows every component in every
state (the table family from tech-016). `src/test/kitGuard.test.ts` enforces the rule under `npm test`, with an
allow-list of today's violations (an exact count per file and rule) that only
shrinks: when it fails, use the kit part its message names, lower or remove an
entry when you fix its file, and never raise one or add one.

## E2E test suite (Playwright)

`playwright.config.ts`, specs in `e2e/specs/`, shared helpers in
`e2e/support/`. Chromium only, `workers: 1`, `fullyParallel: false` — the
specs share one Keycloak realm and one user, and some stop/start the
`keycloak` container. Needs a live, seeded stand (`dotnet run --project
src/backend/Mustask.AppHost`) — it is **not** part of `npm test` or
`scripts/verify.sh`.

**An AI agent may run this suite unattended.** The suite once signed in
through Keycloak's real form with a typed password (`E2E_PASSWORD`), which an
agent could not do; that was replaced with the stand-only Keycloak client
`mustask-e2e`'s own impersonation right (ADR-0023) — the suite obtains a real
Keycloak SSO session for the fixture user `e2e-fixture` (no password
credential exists for it at all) through that client, injects the cookie on
Keycloak's own origin, and lets the panel walk its own ordinary OIDC flow
from there (`e2e/support/impersonation.ts`, `auth.ts`). `E2E_PASSWORD` is
gone — there is nothing left to type. This does not change the E2E
question itself: the human is still asked, per task, whether to run E2E at
all — this only changes how a "yes" is executed.

The client's own secret is never chosen by a human: AppHost generates it at
every stand start and writes it to `.e2e-stand/keycloak-e2e-client.json`
(gitignored, per-worktree — `plan-backend.md` §6 in
`docs/templates/examples/tech-159-e2e-without-password/`), which the suite reads and
never prints, logs, or lets a browser context see (the client-credentials
and impersonation calls run on the Node side only, never inside a page).
`mustask-e2e` only ever holds power over this stand's own realm `mustask` — never a
production Keycloak, guarded by an architecture test over
`Mustask.AppHost`'s own code (ADR-0023).

- `npm run e2e` — runs the whole suite (`playwright test`). Start the stand
  first (`dotnet run --project src/backend/Mustask.AppHost`) and stop it afterward if you started it
  yourself (root `AGENTS.md`, "Hard rules").
- `npx playwright test --list` — lists specs without running them; safe for
  anyone, needs no environment variables and no live stand.
- `npx playwright test e2e/specs/07-cookie-attributes.spec.ts` — a single spec.

Environment variables (`E2E_*`, read only under `e2e/`, never a default for
a secret):

| Variable | Default | Needed by |
|---|---|---|
| `E2E_PANEL_URL` | `http://localhost:3000` | all specs |
| `E2E_API_URL` | `http://localhost:5000` | all specs |
| `E2E_KEYCLOAK_URL` | `http://localhost:5010` | all specs |
| `E2E_USER` | `e2e-fixture` | all specs |
| `E2E_ALLOW_DOCKER=1` | unset | specs 5, 6 (stop/start the `keycloak` container, resolved by the exact name in the stand file, `docker.ts`) — skip without it (Docker control is deliberately not the default) |

A global setup checks the panel and `Mustask.Api` (`/alive`) are reachable, then
repairs what an aborted prior run may have left behind — a stopped
`keycloak` container — *before* checking the realm (`/realms/mustask`) is
reachable, since a stopped container is exactly what makes that check fail;
then it waits until `Mustask.Api` *itself* reports the realm ready (`/health`,
below); only then does it load and verify the stand-only client's
credentials from the stand file (polling up to ~2 minutes — a freshly
(re)started stand can take up to ~90s to finish provisioning it) — failing
with one message naming the cause (a missing client, a stand file from
another stand, or a secret Keycloak rejects) before any spec runs. Realm
settings need no repair of their own — `accessTokenLifespan` is AppHost's own
to set (below), never the suite's, so there is nothing a killed spec could
have left mis-set.

**The suite may start the moment the stand answers — but only because the
setup waits for `Mustask.Api`, not for Keycloak.** (The moment it *answers*: the
three reachability checks above ask once each and fail fast, so the setup
never waits for a stand that is still coming up — only for this.) These are
two different facts and they disagree for up to 30 seconds on a cold stand.
`Mustask.Api` learns the realm is up only from `RealmReadinessMonitor`, whose
`CheckInterval` is 30s, and its readiness *latches*: once ready, a Keycloak
outage never takes it back (the `readiness.Current.IsReady` guard in
`RealmReadinessMonitor.cs`). So on a warm stand this is invisible — the API
latched ready long ago and `docker stop`/`start` cannot unlatch it — while
on a cold one the API's first check fails during Keycloak's startup and,
until the next tick, `/api/session/login` answers `authError=idp_unavailable`
*without* redirecting to Keycloak: the panel renders «Сервис
аутентификации недоступен» and no sign-in begins. That window is 30s and
`auth.ts`'s own sign-in timeout is also 30s, so it was a coin flip, not a
rare race — it cost specs 1 and 2 on a human's cold-stand run. `/alive` cannot cover this: it is
deliberately limited to the `live` tag and documented as not seeing realm
readiness (`RealmReadinessHealthCheck.cs`). `/health` carries the
`keycloak-realm` check and is anonymous, so the setup polls it for up to
120s.

**Every run records which stand it got** — as a line in the run's
output, `cold stand — … was not green at the first check` or `warm stand — …
was green at the first check`, and as the same fact in
`e2e-results/stand.json` beside Playwright's report. Both, because nothing
global setup prints ever reaches that report: the JSON reporter serialises
stdout per test, and global setup runs before the first test exists. A QA
session that reads the report from disk quotes the file — it is gitignored and
per-worktree, so it never travels to another one, and it is not cleared
between runs, so its `checkedAt` is what says which run it belongs to.

The cold record says `Mustask.Api` was not ready when the suite started. It does
**not** say which of `/health`'s checks was red — the endpoint aggregates the
realm and the database — so it is a superset of the window above, not a proof
of it. The warm record is the exact negation and has no such slack. Neither is
a verdict on how the stand was started. A run that is meant to count starts
against a stand brought up from nothing — the rule and its reason are in
`docs/ai-workflow/stages/05-frontend.md`, § 7.

**`mustask-e2e` stays at exactly `impersonation`+`manage-users`,
deliberately, not a gap to route around.** That is enough to impersonate
the fixture user and end its sessions (spec 11's revocation via
`logoutUserSessions`); it does not include `manage-realm`/`view-realm`, so
specs 6, 9 and 11 no longer ask the client to shorten
`accessTokenLifespan` themselves — AppHost does that once, for every stand,
with the admin credentials it already holds for provisioning
(`KeycloakStandProvisioner.SetAccessTokenLifespanAsync`,
`docs/templates/examples/tech-159-e2e-without-password/plan-backend.md` §11). The
realm therefore already carries a 120-second `accessTokenLifespan` (named
once, `KeycloakStandIdentifiers.AccessTokenLifespanSeconds`) by the time any
spec runs — specs 9/11's own 120-second wait comments name that same
constant, so a future change to one is not free to forget the other. One
side effect worth knowing before reading unrelated network traffic: the
panel renews its own access token roughly every two minutes on this stand,
not every fourteen.

**The structural guard.** `src/test/e2eSessionGuard.test.ts` (run by `npm
test`/`scripts/verify.sh`, not Playwright) fails if anything under `e2e/`
sets an `mustask.session` cookie directly or stubs `/api/session/login`,
`/api/session/callback` or `/api/session/token` — the guard against a
future "fix" for a flaky sign-in that fabricates the cookie instead of
exercising the real flow. It is a source-level text scan, not a security
control (see its own header comment for the boundary).

The case "sign-in attempt while Keycloak is down at stand startup, before `Mustask.Api` has ever obtained realm metadata" is not in this
suite — the harness cannot restart `Mustask.Api` under Aspire, and stopping
Keycloak mid-life does not reproduce it (readiness latches ready once
reached, `RealmReadinessMonitor.cs` ~line 129). It is covered instead by
`Login_RealmNotReady_RedirectsToPanelWithIdpUnavailable_WithoutIssuingChallenge`
in `src/backend/tests/Mustask.Api.IntegrationTests/Features/Session/SessionLoginEndpointTests.cs`.

Reports: `e2e-results/results.json` (JSON, read by the orchestrator),
`e2e-results/html` (human-readable, not opened automatically). Both are
gitignored. Traces and screenshots are kept only for failed tests
(`trace: 'retain-on-failure'`, `screenshot: 'only-on-failure'`).
