# ADR-0023: A stand-only Keycloak client impersonates the E2E fixture user

- Status: accepted
- Date: 2026-09-12
- Context: the backend stage of the "E2E without a password" task (kept as
  an illustrative filled example in
  [`docs/templates/examples/tech-159-e2e-without-password/`](../templates/examples/tech-159-e2e-without-password/plan-backend.md)),
  continuing decision D-33 of the task that moved the panel onto Keycloak
  (this project's agents may not type a Keycloak password, so the
  Playwright suite needs another way to reach a signed-in panel). Realm and client shapes it touches are from
  ADR-0018 (Keycloak as IdP) and ADR-0022 (the panel's token-mediating
  backend); this decision does not change either.

## Problem

Decision D-33 of the Keycloak task fixed the approach in one sentence: "a stand-only confidential
Keycloak client with the impersonation right obtains an SSO cookie for a
fixture user, after which the panel walks its own ordinary login flow."
Three things that sentence leaves open, and which the spec (§ "ADR
expected") requires this record to answer:

1. **Which Keycloak mechanism actually produces that SSO cookie** — the
   legacy admin impersonation REST endpoint, or an RFC 8693 token-exchange
   grant.
2. **How the client is kept out of the production realm-import mechanically**,
   given `--import-realm` cannot add a client to a realm that already
   exists, so "a second file merged alongside `mustask-realm.json`" is not an
   available option.
3. **How a test tells "this client can impersonate" apart from every other
   confidential client**, without depending on a Keycloak role vocabulary
   that does not exist in the files a contract test can read.

Forces: BR-3 requires the suite to keep driving the panel's *real* OIDC
round trip (redirect to Keycloak's authorization endpoint, callback,
`Set-Cookie: mustask.session`) — only the credential-typing step is replaced,
not the flow around it (R-3's resolution makes this an executable
criterion, not an intention). BR-2 requires the client to never be able to
reach a production realm. The NFR "Attributability" requires the
impersonation to leave a retrievable entry in realm `mustask`'s own event log,
attributed to the client's service account — a mechanism that bypasses the
Events log fails this NFR outright, not as a footnote.

## Options considered

### Question 1 — the mechanism

#### Option A: Keycloak's admin impersonation REST endpoint (`POST /admin/realms/{realm}/users/{id}/impersonation`)

- Pros: designed for exactly this shape — a caller with the
  `realm-management` client roles `impersonation` + `manage-users`
  authenticates as itself, then asks Keycloak to open a session for another
  user. The response carries `Set-Cookie` for Keycloak's own realm cookies
  (`KEYCLOAK_IDENTITY`/`KEYCLOAK_SESSION`), which a Node HTTP client can
  read and hand to Playwright's browser context (`context.addCookies`,
  scoped to Keycloak's own origin — the same origin restriction R-3 already
  requires for the injected cookie). Once that cookie sits in the browser,
  navigating to the panel and letting it redirect to Keycloak's
  authorization endpoint reproduces the *real* silent-SSO path: Keycloak
  sees an existing session and redirects straight back with a code, and the
  panel's own `/api/session/callback` runs unmodified — this is what makes
  BR-3's criterion (a recorded navigation to the authorization endpoint, a
  `/api/session/callback` request, `Set-Cookie: mustask.session` on that
  response) achievable at all. Keycloak logs the call as an `IMPERSONATE`
  admin event, attributed to the calling client's service-account user —
  satisfying the Attributability NFR directly, by design, not as a side
  effect. Stable since early Keycloak versions, present unchanged in 26.4
  (the version this stand runs, `AppHost.cs`).
- Cons: it is the older of the two mechanisms and is generally discussed as
  a candidate for eventual replacement by standardized token exchange; a
  future Keycloak major could deprecate it, at which point this ADR would
  need revisiting (named below under "Consequences").

#### Option B: RFC 8693 token exchange (`urn:ietf:params:oauth:grant-type:token-exchange`, `requested_subject`)

- Pros: a standardized OAuth mechanism, not Keycloak-specific; the direction
  Keycloak's own roadmap favors long-term.
- Cons, decisive for this task: it returns tokens (access/refresh) directly
  from a backend-to-backend grant — it does not, by itself, create a
  browser-facing Keycloak session cookie. Using it here would mean either
  (a) skipping the panel's actual redirect/callback round trip and
  fabricating `mustask.session` from the exchanged tokens — exactly the
  shortcut R-3 was written to forbid, since it leaves the suite unable to
  catch a real regression in the OIDC wiring — or (b) somehow converting an
  exchanged token back into a Keycloak session cookie, for which token
  exchange has no built-in path. In Keycloak 26.x the richer "V2" token
  exchange is a preview feature gated behind `--features=token-exchange`
  and Keycloak's newer fine-grained admin permissions, both extra
  moving parts with no counterpart already running on this stand.
- Rejected: it cannot produce what BR-3 needs (a real, browser-visible
  Keycloak session that the panel discovers by redirecting to it), and
  adopting it would trade a stable, already-sufficient mechanism for a
  preview feature to solve a problem it does not have.

**Decision: Option A**, the admin impersonation REST endpoint.

### Question 2 — keeping the client out of production

#### Option A: add it to the realm-import files, gated by an environment condition

- Cons: `--import-realm` is all-or-nothing per realm at container-creation
  time; there is no "import this client only in dev" switch inside the
  JSON, and `deploy/keycloak/realms/*.json` is exactly the file set a
  production deployment would use once a deployment pipeline exists. Any entry there is
  permanent exposure, checked only by hoping nobody removes the gate later.
- Rejected outright — this is also why the spec's own "second file merged
  alongside `mustask-realm.json`" phrasing in ADR question 2 is not an option:
  a second realm-import file cannot add anything to a realm Keycloak
  already imported.

#### Option B: a runtime admin-API call from `Mustask.AppHost`'s own provisioning code (chosen)

- Pros: the client simply never exists as a JSON artifact — there is
  nothing in `deploy/keycloak/realms/*.json` to accidentally ship, review,
  or forget to strip before a deployment pipeline exists. What guarantees
  "never in production" is a fact about the source tree instead: the
  project that creates this client (`Mustask.AppHost`) is never deployed
  anywhere — it is `dotnet run`-only local orchestration (ADR-0001) — and
  the Onion dependency rules already forbid any shipping project
  (`Mustask.Api`, `Mustask.Infrastructure`, `Mustask.Application`, `Mustask.Domain`,
  `Mustask.Shared`) from referencing it at all (`OnionDependencyRules`'s
  allowlist does not include `Mustask.AppHost` for any layer).
- Cons: the guard now has to follow the client to where it actually lives —
  a JSON-only contract test (`KeycloakRealmRolesTests.cs`) proves nothing
  about a client that was never going to be in the JSON in the first place
  (R-1's own point). An architecture-level test is needed in addition (see
  Question 3 and `plan-backend.md` §4).
- **Chosen**, for the reason above: it makes "never in production" a
  property of the dependency graph plus one runtime fact (nobody deploys
  `Mustask.AppHost`), rather than a promise about a JSON file's future edits.

## Question 3 — how the guard recognizes "this client can impersonate"

Two guards, covering the two places this client could appear (R-1's
required resolution, "(b)" and "(c)"; R-18's fix to the first draft of (c)):

1. **Over the realm-import files** (`KeycloakRealmRolesTests.cs`, extended):
   a closed, named allowlist of `clientId`s — today `mustask-panel` and
   `mustask-api` — so *adding any client* to an import file fails the test
   until a human edits the allowlist; and a property check that **no**
   listed client has `serviceAccountsEnabled: true` (true of both today). This needs no Keycloak role vocabulary at all — R-18
   rejected keying it on `realm-management` client roles because that
   client is not declared in any import file (it is created by Keycloak
   itself at realm-creation time), so a test that looked for it there would
   either never run or pass vacuously. A service account is the *only*
   shape an impersonating caller can take (a human admin's own session is
   not a client), so "no production client has one" is exactly BR-2's
   guarantee, stated as a property that breaks the moment it stops holding,
   whatever a future client is named.
2. **Over the code path**, since this client is created by `Mustask.AppHost` at
   runtime, not by import (`Mustask.Architecture.Tests`, new
   `StandOnlyKeycloakClientRules`): three checks, not two — backend QA round
   1 (D-1) walked around the first draft of this pair, which is why a third
   was added and why this section now names all three the way § Decision
   does.
   1. No shipping project references `Mustask.AppHost` (already true today,
      restated by name against this decision — `OnionDependencyRules`
      already enforces it as a side effect).
   2. No shipping project's source uses the provisioning namespace/type
      (`Mustask.AppHost.Provisioning`) — catches an accidental `using` or a copy
      that still names where it came from, but nothing more: QA round 1
      reproduced a class under an unrelated name and namespace that
      reimplements the client-creation call without ever mentioning the
      namespace, and this check passed against it (D-1). A `ProjectReference`
      scan and a namespace-literal scan both see only what a reference or a
      careless copy would leave behind, not a deliberate reimplementation.
   3. **Added after D-1**: no shipping project's source calls Keycloak's
      admin REST surface at all (`/admin/realms/...`), a capability rather
      than a name — verified to have no legitimate user in any shipping
      project today (`Mustask.Api`'s own Keycloak traffic is realm
      discovery/JWKS, the token endpoint and `end_session_endpoint`, never
      the admin API). This is what D-1's reproduction cannot dodge: an
      independent reimplementation, under any name, still has to call an
      endpoint under `/admin/realms/...`, because that is the only place the
      capability lives.

   A companion "not vacuous" test asserts the provisioning type actually
   exists where checks 1–2 expect it, so a rename cannot make the guard pass
   for the wrong reason.

The sharper, role-specific check the spec's ADR question 3 also names
(checking Keycloak's own `realm-management` roles — `impersonation`,
`manage-users` — against the *live* realm) is not a repository-level test:
it needs a running Keycloak, which is exactly what a contract test over the
import files is built to avoid needing. It happens once, at backend QA
(`plan-backend.md` §4), against the stand this task's own D-6 lets the
agent bring up and tear down: the client's service account is confirmed to
actually hold both roles, and an impersonation call against it produces the
retrievable admin-event entry the Attributability NFR requires.

## Decision

1. **Mechanism**: the admin impersonation REST endpoint
   (`/admin/realms/mustask/users/{id}/impersonation`), called by the stand-only
   client's own service account (which therefore needs `serviceAccountsEnabled: true`
   and the realm-management client roles `impersonation` + `manage-users`
   inside realm `mustask`), not RFC 8693 token exchange.
2. **Placement**: a runtime admin-API call from `Mustask.AppHost`'s own
   provisioning code (`Provisioning/KeycloakStandProvisioner.cs`), triggered
   once per stand start via Aspire's `ResourceReadyEvent` on the `keycloak`
   resource — never a realm-import entry.
3. **Guard**: a closed `clientId` allowlist plus a no-service-account
   property over the realm-import files (covers "never by import" — F2,
   code review round 1, made this enumerate the directory
   `--import-realm` actually reads, not a hardcoded pair), and three
   architecture-level checks over `Mustask.AppHost`'s provisioning code: a
   reference scan, a namespace-literal scan, and — added after backend QA
   round 1 (D-1) showed the namespace scan alone passes against an
   independent reimplementation under an unrelated name — a scan for any
   shipping-project source calling Keycloak's admin REST surface
   (`/admin/realms/...`) at all, verified today to have no legitimate user
   in any shipping project. All run on every `dotnet test Mustask.sln`; the
   role-specific live check runs once at QA, not as a repository-level
   test. **What actually carries this guarantee is not the scans**: the
   decisive argument is that `Mustask.AppHost` is never deployed anywhere
   (`dotnet run`-only local orchestration, ADR-0001) and the Onion
   dependency rules already forbid any shipping project from referencing
   it, so the capability this ADR grants cannot reach anything that ships,
   independent of any text scan. The three scans are a second line — a net
   for the innocent copy-paste, the accidental `using`, the well-meaning
   reimplementation that doesn't realize it is reproducing this decision —
   not a claim that no literal-string scan can be walked around by an
   author who wants to (`qa.md`'s own boundary paragraph: string
   concatenation, a char array, base64 or a value read from configuration
   all defeat a `Contains` scan with no effort, and no cleverer scan closes
   that door). This is not a control against a determined, adversarial
   author with commit access — it does not need to be, because the
   dependency-graph argument above already covers that case.

## Consequences

**What this locks in:** any future stand-only Keycloak automation that
needs elevated rights follows the same shape — created by `Mustask.AppHost` at
runtime, never imported, guarded by the same two-sided check — rather than
each task inventing its own. The admin impersonation endpoint becomes a
dependency this stand takes on a Keycloak REST surface that is not part of
the standardized OAuth spec surface; if a future Keycloak upgrade removes or
restricts it, this ADR is superseded, not patched, by whatever replaces it
(token exchange, by then presumably out of preview, is the likely
successor).

**What gets easier:** no human ever sets a fourth mandatory secret (D-5);
`scripts/keycloak-seed.sh` stays exactly as it is (D-4); the suite's
admin-API calls (specs 6/9/11) move onto the same client instead of a
separate `E2E_KEYCLOAK_ADMIN_PASSWORD`.

**What gets harder:** the stand-only client's secret sits in plaintext on
the developer's disk (`.e2e-stand/keycloak-e2e-client.json`), readable by
an agent that chooses to read the file — accepted knowingly by D-5, because
the client only ever has power over the stand's own realm `mustask` (this
decision's whole point), never over anything a production Keycloak would
serve.

**Who can actually read the attributability trail:** the NFR promises the
impersonation event is retrievable after the fact, and backend QA round 1
measured precisely by whom — `mustask-e2e`'s own service-account token (the
credential the suite itself will hold) gets `403 Forbidden` on both
`/admin/realms/mustask/events` and `/admin-events`, because `impersonation` +
`manage-users` do not include `view-events`; only a realm-admin-level
credential (the master-realm admin used throughout this task's own
verification) can read it. This meets the NFR as written — the admin API can
retrieve the event — but the suite cannot self-verify its own attributability
today; a future story wanting that would need to add `view-events` to
`mustask-e2e`'s role grant, which is a new decision, not implied by this one.

**How this gets verified:** `plan-backend.md` §4 — unit tests over
`KeycloakStandProvisioner` against a fake HTTP handler (create, idempotent
update, no password ever set, the secret written to the named file), two
new facts on `KeycloakRealmRolesTests.cs` (the closed allowlist, the
no-service-account property), a new `Mustask.Architecture.Tests` class (the
reachability guard), and — once, at backend QA against a live stand — the
role-specific check and the attributability event.
