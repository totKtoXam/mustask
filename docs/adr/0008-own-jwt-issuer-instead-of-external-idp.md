# ADR-0008: Issue JWTs ourselves (RS256) instead of plugging in an external IdP

- Status: superseded by [ADR-0018](0018-keycloak-as-identity-provider.md)
  (2026-09-07); before that — accepted (by the human at gate-3, 2026-08-23)
- Date: 2026-08-22
- Context: the identity and JWT authentication feature — the identity
  contour (stage 4, backend); the decision was made by the human before the
  feature started (`status.md` → Notes, items 1–2) and is recorded here as
  an ADR

## Problem

The new system needs an authentication contour: an employee signs in with a
login and password, gets a token, and every protected endpoint admits only
those who hold the required permission. The fork is **who issues the
token**: an external identity provider or the system itself.

An external IdP (e.g. Keycloak) is a separate service that has to be
deployed, upgraded, backed up and administered, and that duplicates part of
the user data.

Forces acting on the decision:

- MUSTASK is deployed in the customer's own contour;
  every additional infrastructure component is a separate line in the
  operations manual and a separate point of failure;
- the role model is checked against **permission codes**, not role names
  (BR-5), and permissions are edited in the system itself (BR-4) — so the
  source of truth for permissions is ours either way;
- there may be several services down the line, and they will need to verify
  a token without calling the issuer on every request;
- SSO and digital-signature login are explicitly out of scope for the
  feature, but may appear one day — the decision must not close off that
  path.

## Options considered

### Option 1: external IdP (Keycloak)

- Pros: ready-made password policies, lockout, 2FA, password recovery, SSO;
  no need to store and verify passwords ourselves; user administration
  through a ready-made UI.
- Cons: a separate service in the contour (deployment, upgrades, backup,
  monitoring, support licenses); double storage of the user (Keycloak + our
  table with a `keycloak_user_id` reference) and a perpetual sync task;
  permissions and roles still live with us because they are checked by
  permission codes — the IdP only covers authentication; local development
  and integration tests require standing up yet another container.

### Option 2: our own JWT issuer on RS256 (chosen)

- Pros: one service and one database — the whole `User`/`Role`/`Permission`
  model in one place, no sync; full control over the token format (claim
  set, `kid`, lifetimes, size — BR-19, BR-28); integration tests only need
  to stand up Postgres; asymmetric signing means future consumer services
  verify the token with a public key without calling the issuer; the path
  to an external IdP stays open — consumers already work against the
  standard "JWT + verification key set by `kid`" scheme.
- Cons (honestly): we own storing passwords and everything around them —
  password policy, change, recovery, brute-force protection; none of that
  exists yet and the risk is explicitly accepted (BR-14,
  `tech-XXX-auth-brute-force-protection`); the signing private key becomes
  our operational responsibility (storage outside the repository,
  rotation); SSO will have to be built ourselves if it is ever needed.

### Option 3: server-side cookie sessions, no JWT at all

- Pros: session revocation is instant and complete (no access token
  "living out its expiry"); no signing keys; the simplest option in a
  single service.
- Cons: every request hits the session store — a direct contradiction of
  the NFR "verifying a token on a protected request needs no DB call";
  scaling to several services requires a shared session store; future
  consumer services have nothing to present except a call back to us.

### Option 4: our own issuer, but HS256 signing (symmetric secret)

- Pros: as simple as it gets — one secret, no PEM or `kid` work at all.
- Cons: any consumer that can verify the token also gets the power to
  **forge** it — the secret is shared by everyone; rotating the secret logs
  everyone out at once; rejected by the human's decision (`status.md` →
  Notes item 2).

## Decision

**Option 2** is chosen: the token issuer is the MUSTASK system itself, signing
is **RS256** (RSA ≥ 2048 bits), the private key belongs only to the issuer,
consumers verify with the public key.

Key consequences fixed together with the decision:

- the token header carries a `kid`; the system holds a **current key**
  (signing and verification) and **previous keys** (verification only) at
  the same time, so key rotation does not log users out (BR-2);
- the private key and the initial administrator password come from
  environment configuration and never enter the repository; a missing key
  at startup is a startup error;
- the algorithm is hard-restricted to RS256: `alg: none` and symmetric
  algorithms are rejected on validation (BR-19);
- publishing a JWKS externally and external consumers are out of scope for
  now, but the extension point is already in place: the token format will
  not need to change for that.

The decision contains no deviations from `docs/conventions/*` — the
conventions did not prescribe an authentication method.

## Consequences

- **Simpler:** one database and one permission model; local runs and tests
  need no extra service; the token format is under our control.
- **Harder:** we now own the password lifecycle and brute-force protection.
  The tasks this spawns are already in the backlog:
  `tech-XXX-auth-brute-force-protection` (BR-14, risk explicitly accepted —
  going to production is allowed only together with this task or inside a
  closed network contour), `feat-XXX-password-policy`,
  `feat-XXX-password-recovery`, `feat-XXX-users-admin`.
- **Operationally:** a new secret appears that must be stored and rotated
  (the RS256 private key). The rotation procedure — "new private key into
  `SigningKey`, old public key into `PreviousKeys`" — was described in
  that feature's backend plan, §4.2.
- **When to revisit:** if the customer gets a mandatory corporate SSO
  requirement, or a digital-signature/national-CA sign-in requirement. Then
  an external provider is added as one more authentication source, while
  our own issuer keeps issuing tokens for internal consumers — that will be
  a separate ADR, not a rollback of this one.
