# ADR-0022: The panel's refresh token never reaches the browser — a token-mediating backend

- Status: proposed
- Date: 2026-09-10
- Context: the design stage of the task that turns `Mustask.Api` into a
  resource server of the `mustask` realm and moves the panel onto OIDC. Implements the login half of
  [ADR-0018](0018-keycloak-as-identity-provider.md); replaces the browser
  side of the contour ADR-0018 superseded together with
  [ADR-0008](0008-own-jwt-issuer-instead-of-external-idp.md)
- History: this file has carried four decisions in two days, and the
  reasons are worth keeping because they are the argument.
  1. 2026-09-09, first draft: tokens in tab memory, renewed through a
     hidden `prompt=none` iframe, no refresh token at all. **Rejected by
     the human at gate-2 the same day** — "implement the refresh token, the
     hidden iframe is a crutch".
  2. 2026-09-09, second draft: a refresh token in the browser, one shared
     record per origin, tabs coordinated by a Web Lock and a
     `BroadcastChannel`. Rewritten after a live-stand check (D-14 below)
     killed the recovery branch two review rounds had blessed.
  3. 2026-09-10, this text: the refresh token moves behind the origin's own
     backend and the browser never sees it. **Chosen by the human in chat**
     after the options were laid out with their costs.
  4. 2026-09-10, fix pass after code review: Decisions 1 and 7 as first
     written here promised a session cookie whenever "remember me" is off —
     that promise could never be kept, because Keycloak's token endpoint
     sends `refresh_expires_in` on every response regardless of the flag
     (review finding M-2). The human chose to keep the cookie's expiry rule
     exactly as built — a persistent cookie, knowingly, with its shared-
     computer risk accepted (D-23) — rather than rebuild it into a true
     session cookie. The same pass added id-token validation on every
     renewal (D-24, review finding M-4, closing the gap BR-2 already
     required) and a back-channel step at the front of logout (D-25, an
     orchestrator finding, not in the review).
  The number was never published outside this branch, and the file was
  never `accepted`, so nothing points at the older slugs.

## Problem

ADR-0018 handed authentication to Keycloak and fixed the flow —
Authorization Code + PKCE — but said nothing about what happens *after* the
code is exchanged. The answer becomes the pattern every MUSTASK frontend
repeats, including any frontend added later against its own realm.

1. **What renews an expired access token.** The realm issues 15-minute
   access tokens. A working day is eight hours, and the spec requires that
   a filled-in form is never lost to an expiry.
2. **Where the credential that renews it lives.** Any web storage is
   readable by every script on the origin, so one XSS turns into an
   exportable, self-sufficient credential carrying the user's full
   permission set. The panel is where users manage their teams' work, so a
   stolen session is a real breach, not an inconvenience.
3. **What happens when the same person has two tabs open.** The realm
   rotates refresh tokens — `revokeRefreshToken: true`,
   `refreshTokenMaxReuse: 0` (`deploy/keycloak/realms/mustask-realm.json`). Keycloak tracks *the* current refresh token per
   **client session**, and a client session belongs to the browser, not to
   the tab.

### Two measured facts this decision rests on

**D-14 (2026-09-09, live stand). A stale refresh token does not get
refused — it kills the session.** With a temporary password-grant client in
the `mustask` realm: the chain R1 → R2 → R3 works; presenting R1 afterwards
returns `invalid_grant` ("Maximum allowed refresh token reuse exceeded" one
generation back, "Stale token" two back); **and immediately after that the
still-current R3 returns `invalid_grant` "Session doesn't have required
client"**. Reuse detection ends the whole client session. So a recovery
branch of the form "on `invalid_grant`, ask the neighbours for a fresher
token" is meaningless — by then the session is dead for everyone — and a
second, stale copy of the refresh token is not an inconvenience but the
thing that ends the session. Whatever holds the token must make a second
copy impossible, not heal its consequences.

**Probe of a real login (2026-09-10, live stand, `probe_session.py`).**
Three numbers, measured against the real client `mustask-panel` with a real
code + PKCE exchange:

| Measured | Value |
|---|---|
| `KEYCLOAK_IDENTITY`, the SSO cookie | **session cookie** — no `Expires`, dies when the browser closes |
| refresh token lifetime | **28800 s (8 h)** |
| refresh token used by a client holding **no cookies at all** | **works** — a new access token comes back |

Read together: Keycloak's own default is that closing the browser ends
access, and a refresh token is a bearer credential that is fully
self-sufficient once copied. Putting that token into web storage therefore
does not merely add a risk — it *removes a protection the IdP gives us for
free* and extends the blast radius of one XSS to eight hours from any
machine.

Forces: the panel is a SPA on its own origin; **MUSTASK will not stay one
backend** — every protected call must keep going straight to the resource
server that owns it, validated against the realm's JWKS with no per-request
call to the issuer (ADR-0018); security headers are a later task, so
nothing here may depend on headers that do not exist yet.

## Options considered

### Option A: a hidden iframe with `prompt=none`, no refresh token

- Pros: no long-lived credential is held by the page at all.
- Cons: works only while panel and Keycloak are on the **same site**;
  forces a static `silent-renew.html` that must stay framable, colliding
  with the planned security headers; pins the stand to one hostname.
- **Rejected by the human at gate-2, 2026-09-09**: a crutch.

### Option B: the refresh token in the browser, tabs coordinated

One shared record per origin, a Web Lock (`mustask.auth.renew`) around every
exchange, a `BroadcastChannel` to publish the result — the second draft of
this ADR, reviewed twice.

- Pros: no backend work at all; the browser calls every service directly.
- Cons: it hands the browser an 8-hour, machine-portable credential that
  outlives the browser session Keycloak deliberately scoped to the window;
  correctness rests on `navigator.locks`, on storage event ordering, and on
  every future frontend repeating the protocol exactly; two review rounds
  found four blocking defects in it (a reloaded tab presenting a stale
  token, a `session-lost` broadcast killing healthy tabs, an unbounded lock
  wait, `sessionStorage` surviving tab duplication) — and by D-14 each of
  those is not a glitch but a killed session.
- Rejected. RFC 10017 ("OAuth 2.0 for Browser-Based Apps") ranks
  browser-held tokens last of the three viable architectures and
  discourages them; we have no reason to be the exception.

### Option C: turn rotation off in the realm

- Pros: the cross-tab race disappears outright.
- Cons: gives up reuse detection for every client of the realm to fix a
  problem created by one client's storage choice; contradicts the realm's
  rotation settings.
- Rejected.

### Option D: a token-mediating backend — the refresh token in an encrypted cookie (chosen)

The browser's origin backend is the OIDC client. It performs the code
exchange, keeps the refresh token, and hands the browser only a 15-minute
access token. The refresh token is carried **inside the cookie itself**,
encrypted with ASP.NET Core data protection — there is no session store.

- Pros: nothing renewable is reachable from script (`HttpOnly`, `Secure`,
  `SameSite`); the cookie's lifetime is still bounded by Keycloak's own
  refresh-token window rather than by anything the frontend invents
  (Decision 7) — nothing here outlives what Keycloak already grants;
  **the browser still calls each resource server directly with the
  bearer**, so adding services changes nothing; the client becomes
  confidential, which means a leaked refresh token is useless without the
  client secret; the whole cross-tab protocol of Option B — lock, channel,
  generation counter, storage — disappears from the frontend; RFC 10017
  ranks this second of three, above browser storage.
- Cons: a session module, cookie and CORS credential handling, a client
  secret to keep, and the renewal race moves from the browser into the
  backend (see Decision 5).
- **Chosen.**

### Option E: a full BFF that proxies every API call

The browser holds no access token either; the backend attaches it and
proxies all traffic.

- Pros: strictly the most protected — RFC 10017's first rank.
- Cons: every future service's traffic has to pass through the panel's
  backend, which is exactly the coupling we are trying not to build; it
  turns a token boundary into a network boundary.
- Rejected on the microservice ground the human raised.

### Option F: a server-side session store (Redis or a table)

The cookie carries an opaque id; the tokens live in shared storage.

- Pros: the renewal race is solved by one lock in one place.
- Cons: introduces shared runtime state, an operational dependency and
  sticky-session questions, to solve a problem we do not have yet.
- Rejected now, and named as the escape hatch in Decision 5.

## Decision

### 1. The refresh token lives in an encrypted cookie, never in the browser

The session cookie is issued by the origin's backend: `HttpOnly`, `Secure`,
`SameSite=Lax`, `Path=/api/session`. Its payload is the authentication
ticket, encrypted and signed by ASP.NET Core data protection, holding the
refresh token and the id token. **The access token is stripped from the
ticket** — the browser has it anyway. **The ticket's principal is cut
down to `sub` and `sid`** (D-30): left alone, the OpenID Connect handler
copies every id-token claim into it and duplicates what the id token already
carries. Stripping the access token alone was not enough: on the live stand
the cookie came to 5942 bytes in three chunks (defect S-2). With the
principal cut down, on test tokens calibrated to reproduce that live
measurement, the cookie is one chunk of 3836 bytes, which
`SessionCookieContractTests` guards. The margin to the ~4 KB chunk boundary
is about 200 bytes, so a larger id token — more groups, say — splits the
cookie again. That still works; the limit that matters is the reverse
proxy's header line, 8 KB by default in Nginx, and the cookie stays well
under it.

Consequences that follow from the cookie itself: script cannot read it, and
it does not travel with ordinary API calls because of the path.

**Correction, human decision D-23 (2026-09-10, review finding M-2).** This
decision first claimed the cookie dies with the browser unless "remember me"
is on — that was wrong. Keycloak's token endpoint returns
`refresh_expires_in` on every response whether or not "remember me" is
checked (8 h off, per the probe below), so a cookie whose expiry is taken
from that field was never going to be a session cookie — it was always
going to be persistent. The human chose to keep the mechanism exactly as
built — expiry from `refresh_expires_in`, unconditionally — rather than
rebuild it into a true session cookie (`IsPersistent = false`, the token's
own lifetime checked only server-side, never written to the cookie's
`Expires`). The accepted risk: on a shared computer, the next person who
opens the panel within that window continues under the previous person's
session unless that person pressed "Выйти". Code does not change for this;
see Decision 7 for what "remember me" does and does not add.

There is **no session store**. Any instance can serve any request, and the
only shared thing multiple instances need is the data-protection key ring —
one table or one mounted directory, which is a standard deployment detail
and not a session store.

### 2. The backend is the OIDC client; the panel has no OIDC library

The panel does not do PKCE, does not handle `state` or `nonce`, does not
talk to the token endpoint, and does not know the Keycloak address. It
navigates to `/api/session/login`, and afterwards asks its own origin for
an access token. The ASP.NET Core OpenID Connect handler does the flow —
discovery, PKCE, `state`, `nonce`, the code exchange — and the cookie
handler writes the ticket.

This deletes the entire library comparison the previous draft carried:
`keycloak-js` versus `oidc-client-ts` versus hand-rolled is no longer a
frontend question.

### 3. The browser keeps a 15-minute access token in memory and calls services directly

`POST /api/session/token` returns the current access token with its expiry
— `expiresAt`, and `expiresIn` in seconds counted by the server, from which
the panel schedules renewal — or 401 when there is no session. The panel holds it in a module variable — not in
`localStorage`, not in `sessionStorage` — and attaches it to calls against
`Mustask.Api` and, later, against any other resource server. **This is the
property that makes the decision microservice-safe:** the mediating backend
sits beside the browser, not between the browser and the services.

### 4. `mustask-panel` becomes a confidential client

The browser no longer talks to Keycloak's token endpoint, so the client no
longer has to be public. It gets a secret, held by the backend through the
usual secret channel (environment variables / user secrets, never in code).
`webOrigins` narrows to nothing, and `redirectUris` points at the backend's
callback rather than at a panel route.

The security gain, stated plainly: after this, a refresh token on its own
cannot be redeemed by anyone. The probe above showed a cookie-less client
redeeming one; with client authentication that stops being true.

### 5. Renewal is serialized in the backend, and a dead session is discovered, not broadcast

Renewal happens when the panel asks for a token and less than 60 seconds
remain: the backend redeems the refresh token, rewrites the cookie and
returns the new access token. Concurrent tabs are serialized by an
in-process lock keyed by the client session: the id token's `sid`,
which differs between browsers and is shared by the tabs of one, or — when
Keycloak issues no `sid`, which OIDC permits — an identifier generated at
sign-in and carried inside the ticket (review finding m-5). An
always-generated key was tried and rejected: the extra field pushed a
realistic ticket past one cookie chunk, while `sid` is in the ticket
anyway. This is correct while there is one backend process — the state
MUSTASK is in today.

Two triggers move this to shared state, and they arrive together: a second
instance of the origin backend needs both a shared data-protection key ring
and a shared renewal lock. At that point Option F becomes the answer, and
this ADR is superseded rather than patched.

When Keycloak answers `invalid_grant` the backend clears the cookie and
answers 401. Every tab discovers this on its own next call and shows the
sign-in screen. Nothing is broadcast, so a broadcast cannot kill a healthy
tab — defect B-4 of the previous draft cannot exist here.

**Correction, human decision D-24 (2026-09-10, review finding M-4).** The
first draft of this decision took the renewal response on trust. BR-2
requires the id token to be validated "the same way" at every renewal, not
only at the initial exchange, and that requirement fell out of the design
silently. It is restored: the id token that comes back from a renewal is
checked against the same `TokenValidationParameters` the OpenID Connect
scheme already uses for the exchange — signature against the realm's JWKS
through the shared `IConfigurationManager<OpenIdConnectConfiguration>`,
`iss`, `aud` = `mustask-panel`, lifetime — plus a check the initial exchange
cannot make: the same `sub` and the same `sid` as the ticket being renewed
(OIDC Core §12.2). A mismatch, or a token that fails validation outright, is
treated exactly like `invalid_grant`: the cookie is cleared, the caller gets
401 `session.expired`, and a log line names the reason and never the token
itself.

### 6. The session module is an isolated contour that can move to a gateway

`Mustask.Api/Features/Session/` depends on nothing in the domain and is
depended on by nothing in it. Today it lives inside the only backend
because there is only one; the day a second backend appears it
moves into a gateway **by moving the folder**, and the resource servers —
including today's `Mustask.Api` — keep validating bearer tokens exactly as they
do now. An architecture test guards the isolation.

### 7. "Remember me" is Keycloak's, and stays out of this task

The login form belongs to Keycloak. Enabling it is a realm change —
`rememberMe: true` plus `ssoSessionIdleTimeoutRememberMe` and
`ssoSessionMaxLifespanRememberMe` — after which the checkbox appears and
Keycloak makes its own SSO cookie persistent.

The backend needs no flag and no checkbox of its own — that stays true after
D-23, though for a different reason than first drafted. It sets the session
cookie's expiry from the `exp` of the refresh token it received, and
Keycloak sends `refresh_expires_in` on every token response whether or not
the box is checked (D-23 above): unchecked, that window is the realm's
plain SSO idle timeout (8 h, measured); checked, it becomes whatever
`ssoSessionMaxLifespanRememberMe` the realm carries. The backend copies
whichever number Keycloak gives it without asking which case it is, so
turning "remember me" on changes the number, not the mechanism, and the
sentence above is correct again once "unchecked" no longer means "session
cookie." Rotation keeps it sliding forward. The realm ships with
`rememberMe: false`; turning it on, and choosing how long "long" is for
this product, is a separate queue item.

### 8. Logout ends the Keycloak session before it ends ours (D-25)

The first draft of `POST /api/session/logout` cleared our cookie, then built
the front-channel logout URL from cached discovery metadata and returned
it — without ever confirming that Keycloak had ended anything. With
Keycloak down, the browser never reaches the front channel either, and
Keycloak's own SSO session (the `KEYCLOAK_IDENTITY` cookie measured above)
survives: the next "Войти" on that machine passes without a password once
Keycloak comes back. The cookie and the 200 were ours to give; the session
that actually mattered was Keycloak's, and it was only going away if the
browser cooperated.

Ordering fixes this, found by the orchestrator rather than the review
(finding F-1). When a cookie is present, the backend — a confidential
client — first calls the realm's `end_session_endpoint` itself, back-
channel, with `client_id`, `client_secret` and the ticket's `refresh_token`.
Only a Keycloak confirmation (2xx) or `invalid_grant` (the session there is
already gone) clears our cookie and returns 200 with the front-channel
`logoutUrl` — still needed, because it is the only way to clear
`KEYCLOAK_IDENTITY` from the browser. If Keycloak is unreachable, times out,
answers 5xx, or discovery itself cannot be fetched, the cookie is left
untouched and the endpoint answers 503: an unconfirmed logout must not look
like a signed-out session, and an unreachable Keycloak must not silently
leave its own session alive while ours looks gone. The panel surfaces this
as "Выход не завершён…" with a retry, not as a silent success. No cookie at
all → 200 without `id_token_hint`, as before — that call can only be a
repeat after an earlier failure. `post_logout_redirect_uri` moves to
`{Session:PanelUrl}/signed-out` so the front channel has somewhere to land
that is not straight back into the sign-in loop (review finding m-7).

## Consequences

**Removed from the frontend, compared with the previous draft:** the OIDC
library and its configuration, the PKCE and `state`/`nonce` handling, the
`mustask.auth.renew` Web Lock, the `mustask.auth` `BroadcastChannel`, the shared
storage record with its generation counter, the `sessionStorage` copies,
the reload-with-a-stale-token branch, and the requirement that
`navigator.locks` exist. The panel's authentication surface becomes three
calls to its own origin.

**Added to the backend:** `Mustask.Api/Features/Session/` — the cookie and
OpenID Connect schemes, four endpoints, the renewal lock, the
data-protection key ring configuration; validation of the id token on every
renewal against the shared JWKS manager (D-24); a back-channel call to
`end_session_endpoint` at the front of logout (D-25); `AllowCredentials()`
on the CORS policy for the panel origin; the client secret in
configuration; a section in `api-contract.md`. The resource-server half
already built in this task — JWT validation, permissions from
`resource_access`, the readiness gate — is untouched.

**Unchanged:** every service keeps validating bearer tokens against the
realm's JWKS with no per-request call to the issuer; rotation and reuse
detection stay on in the realm; the demolition of the in-house identity
contour (ADR-0018) stands.

**Deferred with a named trigger:** a shared renewal lock and key ring, at
the second backend instance (Decision 5); "remember me", as its own queue
item (Decision 7); security headers, a separate later task.

**How this gets verified:** the flow is exercised end to end in
`Mustask.Api.IntegrationTests` against the test realm — sign-in, token, renewal
across a rotation, id-token validation on renewal rejecting a foreign
`sub`/`sid` exactly like `invalid_grant` (D-24), sign-out completing the
back-channel call before the cookie is cleared and refusing to clear it when
Keycloak cannot be reached (D-25); the cookie's attributes and the absence
of the access token from the ticket are asserted directly; the panel is
tested against the session endpoints with MSW, with no Keycloak in the
loop. The cookie's real size on a real token is measured on the stand
before the task closes.
