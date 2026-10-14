# ADR-0018: Hand authentication to Keycloak with separate realms instead of our own issuer

- Status: accepted (by the human in chat, 2026-09-08); amended by
  [ADR-0027](0027-organization-access-from-membership.md) — organization
  access comes from the membership, Keycloak keeps instance roles only
- Date: 2026-09-07
- Context: the architecture stage of the series that moved the system onto
  Keycloak. Supersedes ADR-0008 (our own JWT issuer on RS256)

## Problem

ADR-0008 chose our own issuer, and in its own context the choice was
right: one system, one service, one database, permissions are checked by
codes and live with us regardless. The context has changed.

**More than one application needs the same login.** Once a second frontend
or a second backend appears — a separately shipped client, an integration
service, a gateway — an issuer embedded in `Mustask.Api` becomes a
dependency every other application has to trust and call. Each new
application would either reuse our issuer (coupling it to MUSTASK) or build
its own second one.

**What we would have to build ourselves is large.** Password storage and
policy, lockout, second factors, recovery, SSO and user administration are
all ours to write and to carry with an in-house issuer — and none of it is
the product.

## Options considered

### Option 1: keep our own issuer, let every new application build its own

- Pros: MUSTASK is not touched at all; the existing identity feature stays
  as is.
- Cons: login logic, password storage, key rotation and brute-force
  protection get written again for every application. The downsides of
  ADR-0008 ("we carry the password lifecycle") multiply.

### Option 2: Keycloak as the single authentication provider (chosen)

- Pros: password policy, lockout, 2FA, recovery, SSO and user
  administration come ready-made; every application verifies a standard
  OIDC token and has no ties to our code; all frontends see the same login
  mechanism.
- Cons (honestly, this is exactly what ADR-0008 was wary of): an
  additional service appears in the deployment — upgrades, backup,
  monitoring, a separate point of failure; local development and
  integration tests need one more container; realm configuration becomes an
  artifact that must be versioned and rolled out.

### Option 3: a proxy with a shared service password

- Pros: nothing to integrate; doable in a day.
- Cons: all users share one common password, there is no identity, no
  audit. Rejected by the human in the brief: "the proxy option does not
  work for us, we need full authorization."

## Decision

**Keycloak** issues authentication, one realm per system:

| realm | Who logs in | Clients | Login theme |
|---|---|---|---|
| `mustask` | MUSTASK users | the MUSTASK panel (see ADR-0022), `Mustask.Api` (audience) | MUSTASK branded |

A future system that must not share users with MUSTASK gets its own realm
with its own clients and theme, rather than a second set of clients in
`mustask`.

- the login flow for every frontend is OIDC Authorization Code + PKCE;
- `Mustask.Api` (and any later backend) is a resource server: it verifies
  the signature against its realm's JWKS, `iss` and `aud`; it does not call
  the issuer on every request — the ADR-0008 requirement of "verification
  without a call to the database" is preserved;
- machine-to-machine access, when it appears, uses client credentials of a
  dedicated client, not a shared user;
- MUSTASK permissions stay `<module>.<resource>.<action>` codes. The
  access model is three-layered, like Azure RBAC (clarified 2026-09-08 by
  the human's decision; the first draft flattened it to "realm role =
  permission code" and lost the "role as a set of permissions" layer the
  system had before Keycloak — `Role.ReplacePermissions`):
  a **permission** is a role of the resource client (`mustask-api`), its name
  matches the code from `PermissionCatalog`, and it changes only with a
  release; a **role** is a composite realm role, a named set of permissions,
  edited by an administrator; a **group** is the org structure with a
  hierarchy, a role is mapped onto it, and a user gets the union of the
  roles of all their groups. All of this collapses in the token into
  `resource_access.mustask-api.roles` — a flat list of permission codes without
  Keycloak's service roles; the code checks only that, it never sees role or
  group names. `PermissionCatalog` stays the code registry, but **loses all
  its former validator consumers** (`UpdateRolePermissionsHandler`,
  `IdentitySeeder.SyncPermissionCatalogAsync`, `RolePermissionPolicy`) — they
  leave together with identity. So that a typo does not turn into a
  permanent silent 403, contract tests against the realm import are
  introduced: the `mustask-api` client's roles equal `PermissionCatalog.AllCodes`,
  composites reference only declared permissions, groups reference only
  declared roles, the `admin` role holds every permission and is granted to
  at least one group. Without these tests, the chain "permission ↔ code ↔
  role ↔ group" is guaranteed by nothing. What the model does not have is
  Azure's scope ("a role, but only for one project"): a role assignment
  applies to the whole realm; if needed, a group attribute in the token and
  a check in the code, not Keycloak Authorization Services;
- **two former identity rules are repealed:** that the permission catalog
  cannot be edited from the interface, and that system roles cannot be
  deleted. An operator of the Keycloak console can do both; the replacement
  is a realm sourced only from import plus the contract tests mentioned
  above — they guard the import file, not the live realm.

The in-house identity contour is dismantled (decision by the human,
2026-09-07): the user/role/refresh-session tables and slices, token issuance
and `SigningKeyRing`, the dev RSA key in AppHost, refresh and anti-CSRF
cookies, the initial administrator seed and the related migrations and
tests. The named inventory is done at the design stage of the Keycloak
task, not here.

## Consequences

- **Simpler:** passwords, second factors, recovery and SSO are not our
  code; user administration is done via a ready-made console, our own user
  admin panel is never written.
- **Revocation is NOT instant.** A role removed in Keycloak stays in effect
  until the issued access token expires — this is a property of JWT, and it
  does not go away because the issuer is external. Token lifetime and
  back-channel logout are chosen deliberately; the former token lifetimes
  (15 min access, 8 h refresh, 12 h chain limit) are replaced by realm
  settings, and the replacement numbers must be written down, not inherited
  by silence.
- **Audit does not come for free.** A resource server only authenticates
  the caller; recording who did what stays our job, and an audit log
  remains a separate task.
- **Harder:** +1 infrastructure component in the deployment, with the full
  operational load; `Mustask.Api` integration tests must either bring up
  Keycloak (Testcontainers) or verify tokens signed with a test key — the
  choice is fixed at the design stage of the Keycloak task; a side effect —
  the stand gets heavier by one more container (and at the same time
  lighter by one, per ADR-0020).
- **Configuration appears that is easy to break silently:** the `webOrigins`
  and redirect URIs of the Keycloak clients depend on application
  addresses, and login themes depend on the server version and are cached.
  A Keycloak upgrade can break a custom theme; the realm import and the
  themes must live in the repository and be rolled out at startup, or the
  login appearance is not reproducible.
- **What disappears together with identity:** the "change password",
  "reset password", "grant permission" scenarios stop existing in our UI
  and move to the Keycloak console; user, role and password-recovery
  screens and in-house brute-force protection lose their subject. Tying
  `ICurrentUser` to the token's `sub`, `RequirePermission` with
  permission-based navigation filtering, and the real user in the header
  with a working logout **keep** their subject — only the source of the
  claims changes.
- **The database empties out completely** — all four `DbSet`s in
  `MustaskDbContext` belonged to identity. What to do with it is decided
  separately:
  [ADR-0020](0020-drop-storage-until-first-data-module.md) — storage leaves
  the system until the first module that actually stores data.
- **Configuration artifacts:** the realm import JSON and the login themes
  live in the repository and are rolled out at stand startup — otherwise
  "works on my machine" becomes unrepeatable.
- **User migration is cheap:** at the time of the decision there was
  exactly one user — `admin` from the seed.
- **When to revisit:** if a deployment forbids an additional service, or
  requires a login method Keycloak cannot provide. Then an in-house issuer
  comes back, but as an OIDC provider for every frontend — that will be a
  new ADR, not a rollback to ADR-0008.
