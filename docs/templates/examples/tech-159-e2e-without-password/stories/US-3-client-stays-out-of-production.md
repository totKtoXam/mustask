# US-3: The impersonation-capable client cannot reach production

- story: US-3
- depends-on: -
- shippable: no — a guard for a client that, absent US-2, does not exist yet to guard; it is the other half of US-2's provisioning, not an independent capability
- status: pending

As the **human**, I want a guarantee that the stand-only client with the
impersonation right can never appear in a production realm, so that a
convenience for automated testing does not become the right to sign in as
anybody in the live system.

The production import is exactly the set of files bind-mounted into
Keycloak's `--import-realm` today — `deploy/keycloak/realms/mustask-realm.json`
and `deploy/keycloak/realms/partner-realm.json` — the same files
`KeycloakRealmRolesTests.cs` already reads. Nothing in that directory is
marked "stand-only" today, and `--import-realm` skips a realm that already
exists, so a second file merged alongside `mustask-realm.json` cannot add a
client to realm `mustask` — whichever mechanism the ADR (stage 4) picks for
provisioning the stand-only client, it is either (a) added to the files
above, in which case the guard below reads those same files, or (b) created
at stand start by a runtime admin-API call from `Mustask.AppHost`, in which case
the guard below must instead cover that code path (scenario 3).

## Acceptance criteria

```gherkin
Scenario: The realm-import files carry a closed, named set of clients
  Given `deploy/keycloak/realms/mustask-realm.json` and
    `deploy/keycloak/realms/partner-realm.json` — the production import
  When a contract test (extending `KeycloakRealmRolesTests.cs`) reads the
    `clients` array of each file
  Then the set of `clientId`s found equals a closed allowlist named in the
    test (today: `mustask-panel`, `mustask-api`, `partner-ui`, `partner-api`)
  And adding any client not on that allowlist — impersonation-capable or
    not — fails the test until a human edits the allowlist alongside the
    realm-import change, the same review attention a new client should get

Scenario: No client in the production import holds a service account
  Given the closed allowlist from the previous scenario — today `mustask-panel`,
    `mustask-api`, `partner-ui`, `partner-api`, none of which sets
    `serviceAccountsEnabled: true`
  When the same test inspects each listed client's `serviceAccountsEnabled`
    flag
  Then none of them is `true`
  And this needs no Keycloak role vocabulary to hold: obtaining the
    impersonation right through a service account is exactly the shape
    BR-2 forbids in the production import, so the moment any client in the
    closed allowlist turns service-account-enabled — impersonation-capable
    or not, and whatever its roles are named or later renamed to — the
    test fails

Scenario: If the client is provisioned by code instead of by import, the guard follows it there
  Given the ADR (stage 4) picks a runtime admin-API call from
    `Mustask.AppHost` to create the stand-only client, rather than a
    realm-import entry
  When a test (architecture-level, alongside
    `src/backend/tests/Mustask.Architecture.Tests`) inspects which
    assemblies/projects call that Keycloak admin-API creation path
  Then the call exists only inside `Mustask.AppHost`'s own stand-provisioning
    code, never inside `Mustask.Api`, `Mustask.Infrastructure`, or any project that
    ships outside a developer's stand
  And scenario 1's closed-allowlist test still passes over the
    realm-import files, now proving they contain no such client at all — the
    two guards together cover both places the client could appear

Scenario: The guard runs on every commit, not only before a release
  Given the tests from the previous scenarios
  When `dotnet test Mustask.sln` (and therefore `scripts/verify.sh`) runs
  Then they run and are green on every commit that touches the realm
    import or `Mustask.AppHost`'s Keycloak provisioning code, the same way
    `KeycloakRealmRolesTests` already does today
```
