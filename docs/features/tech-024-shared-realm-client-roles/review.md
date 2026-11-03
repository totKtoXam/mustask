# Review — tech-024-shared-realm-client-roles

## Round 1

Scope: `git diff $(git merge-base origin/main tech/024-shared-realm-client-roles)..tech/024-shared-realm-client-roles`
(74 files), reviewed together with tech-025 in one session (economy mode, route M). Acceptance source: the queue
line (status.md Notes) + ADR-0033 + status.md D-1..D-8. No tests run (lane belongs to another session).

Verdict: **approve** — 0 blockers, 0 majors, 3 minors (debt). QA's D-1..D-3 stand as recorded and are not repeated.

### Checked, no finding

- `InstanceRoles.CodesOf` (InstanceRoles.cs:52): empty without `user`, unknown names ignored, ordinal match;
  `system-admin` = `PermissionCatalog.InstanceCodes`. `KeycloakPermissionClaims` reads only
  `resource_access.<audience>.roles`; a role of another audience or `realm_access` grants nothing.
- Callback gate (SessionAuthenticationSetup.cs:181-190): runs in `OnTokenValidated` after the OIDC handler validated
  the id token, `HandleResponse()` before a ticket exists, so no cookie and no cache entry; redirect through the same
  `ReturnUrlValidator` path. Renewal (SessionEndpoints.cs:203-211): the refreshed id token is validated first
  (`RenewedIdTokenValidator`), then cache evicted, cookie cleared via `OnStarting`, 403 `session.no_access`.
- Realm import: four client roles with descriptions, no realm roles/groups/`defaultRole`/composites/`groups` mapper;
  `mustask-panel` keeps `roles` in `defaultClientScopes` and the `audience-mustask-api` mapper, so
  `resource_access.mustask-api` is still emitted (live test is opt-in, not run here).
- Realm name: validated (`KeycloakStandIdentifiers.RealmName`), passed to the import placeholder, the bind-mount file
  name, the health check, drift check, provisioning, `Keycloak__Authority` and `keycloak-seed.sh` (`KEYCLOAK_REALM`);
  no `default-roles-mustask` / `/realms/mustask` literal left in AppHost, Api code or the seed script.
- Retired live-realm migration: no reference left in code, scripts or `docs/runbooks`; AGENTS.md and the runbook name
  the stand reset instead.
- Frontend mirror table (tokenClaims.ts) equals `InstanceRoles`; `session.ts` maps 403 `session.no_access` to an ended
  session and any other 403 to `AuthUnavailableError`; `AnonymousScreen` text equals the catalog's `ru` string.
- `contracts/errors/session.json` and `docs/error-codes.md` agree on `session.no_access` (403).

### Findings

- **m1 (minor)** — SessionAuthenticationSetup.cs:184-190 and SessionEndpoints.cs:203-211: a refused sign-in or a
  renewal refused for lack of `user` drops our cookie but leaves the Keycloak SSO session (and the refresh token)
  alive, and `/api/session/logout` without a cookie cannot end it. A person refused with `no_access` who clicks
  "Войти" again is silently signed in as the same account and refused again; switching accounts needs a Keycloak
  logout elsewhere. Fix: on refusal revoke the refresh token / end the IdP session (the logout path's back-channel
  call), or redirect through `end_session_endpoint` with `id_token_hint`; at least say so in ADR-0033 Consequences.
- **m2 (minor, prose)** — stale text the diff left: `AppHost.cs:322` lists "declared groups" among what the drift
  check compares (D-4 removed it); `src/frontend/e2e/support/keycloakAdmin.ts:81-83` names the provisioner's
  `GroupPath`, `/Администраторы`, and `:91-93` says a user reads dictionaries "through the realm's default roles"
  with no group (now: client role `user`, on the stand via the default role's composite). Fix: rewrite the comments.
- **m3 (minor)** — KeycloakPermissionClaims.cs:58: `JsonDocument.Parse` on a non-JSON `resource_access` throws inside
  `OnTokenValidated` (QA D-2, same lines); `SessionAccessGate` already treats an unreadable token as "no `user`".
  Fix: catch `JsonException` in `ReadRoles(string, …)` and return `[]`.

### Behavioural claims (item 6)

- ADR-0033 D1 "exactly these four, no realm roles/groups/defaultRole/composites" → `mustask-realm.json:35-56`; Contracts
  `KeycloakRealmRolesTests`.
- D2 "unknown names ignored; without `user` no `perm`" → InstanceRoles.cs:52-69, KeycloakPermissionClaims.cs:46.
- D2 "a test fails for an instance code no role reaches" → InstanceRolesTests (Domain).
- D3 callback `no_access` / renewal 403 → SessionAuthenticationSetup.cs:184-190, SessionEndpoints.cs:203-211.
- D3 "a bearer token without `user` authenticates and holds no right" → KeycloakPermissionClaims (JwtBearer
  `OnTokenValidated`), `DictionariesByClientRoleTests`.
- D4 "health, drift, provisioning, seed use the same value; nothing derived spelled out" → AppHost.cs (keycloakRealm
  uses), KeycloakStandDriftCheck.cs (`"default-roles-" + expected.Realm`), keycloak-seed.sh:59. Anchored.
- D4 "Keycloak rejects any other file name" → status D-5, opt-in live test; not re-run (named gap).
- D6 "`user` as a composite of the default role, added by provisioning, never by the import" →
  KeycloakStandProvisioner.cs `AddUserRoleToDefaultRolesAsync`; import has no `defaultRole`.
- `src/backend/AGENTS.md` access-model and realm-name paragraphs → same anchors as D2/D4. Runbook "older realm import
  → stand reset" → KeycloakStandDriftCheck findings carry `Reset`.
- Direction 2: grepped `migrate-live-realm`, `instance-admin`, `Администраторы`, `default-roles-mustask`, `GroupPath`,
  "realm's default roles" over `docs/` (minus feature history/analyses/examples), `.claude/`, AGENTS files, `scripts/`,
  `src/frontend/e2e`, `src/frontend/src`: hits = AGENTS.md:182 (correct, describes the old import), keycloakAdmin.ts
  (m2), tokenClaims.test.ts (negative fixture), AppHost.cs:322 (m2). E2E specs' behaviour against client roles was not
  swept (E2E paused).
