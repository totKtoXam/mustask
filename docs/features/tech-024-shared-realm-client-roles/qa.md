# QA — tech-024-shared-realm-client-roles

## Backend

Acceptance source: the queue line (status.md Notes) + ADR-0033 (no spec). The
plan (criteria C1-C10, scenarios N1-N13) was written before the code was read;
the "Test found" column was filled afterwards. QA ran no tests (the test lane
was not granted); it built `Mustask.sln` (0 warnings, 0 errors).

### Coverage matrix

| # | Criterion | Test found | Status |
|---|-----------|-----------|--------|
| C1 | Exactly four client roles + descriptions in the import | `KeycloakRealmRolesTests.MustaskRealm_DeclaresExactlyTheFourClientRolesOfTheTable_WithTheirDescriptions` | covered |
| C2 | No realm roles / groups / defaultRole / composites / `groups` mapper | `..._DeclaresNoRealmRolesGroupsOrDefaultRole`, `..._ClientRoles_HaveNoComposites` | covered |
| C3 | No raw codes as client roles | `..._ClientRoleNames_AreNotPermissionCodes` | covered |
| C4 | Table in Domain; system-admin = all instance codes; every code reachable | `InstanceRolesTests` (9 facts) | covered |
| C5 | No `user` -> no `perm` claim | `KeycloakPermissionClaimsTests.WithoutUser_*`, `DictionariesByClientRoleTests.WithoutUser_NothingOpens` | covered |
| C6 | Callback refuses token without `user` | `SessionCallbackEndpointTests.Callback_AccessTokenWithoutUserRole_*`, `..WithoutResourceAccess_*` | covered |
| C7 | Renewal that lost `user` -> 403 `session.no_access`, cookie cleared, cache evicted | `SessionTokenEndpointTests.Token_RenewedAccessTokenWithoutUserRole_*` | covered |
| C8 | Realm name = configuration, default, validated, placeholder in import | `KeycloakStandProvisioningTests.RealmName_*`, `MustaskRealm_NameIsAPlaceholder...`, drift test with configured realm, `KeycloakSeedScriptTests`, opt-in `LiveRealmImportTests` | covered (see D-1) |
| C9 | ADR amends 0018/0027, index lines | read: ADR-0033, README index, headers of 0018/0027 consistent | ok |
| C10 | Frontend mirror = backend table | compared by hand: identical; no machine check (status D-6, accepted debt, mod-019) | manual |

### Additional scenarios

| # | Scenario | Test found | Status |
|---|----------|-----------|--------|
| N1 | Unknown names / raw codes / `instance-admin` / `offline_access` | `UnknownRoleNames_AreIgnored`, `RawPermissionCodesAsRoles_GrantNothing`, domain `CodesOf_Unknown*` | covered |
| N1b | Wrong case / whitespace (`User`, `user `) | none | **added** `RoleNames_AreMatchedExactly` |
| N2 | Roles in another audience | `ShapesWithoutOurRoles` (other-api only) | partly; **added** `UserHeldOnlyForAnotherAudience_UnlocksNothing`, `RolesOfOurAudience_AreReadEvenNextToOtherAudiences`, callback `foreign-user` |
| N3 | `realm_access` / `groups` ignored | `PermissionClaimsTests.RealmRolesAndForeignClientRoles_GrantNothing` (API); none for session gate | **added** `RealmAccessAndGroupsClaims_AreIgnored`, callback `realm-user`/`groups-user` |
| N4 | Only `system-admin`, no `user` | claims, dictionaries, callback, renewal | covered |
| N5 | Duplicated roles | none | **added** `DuplicatedRoles_YieldEachCodeOnce` |
| N6 | Odd `resource_access` shapes | `ShapesWithoutOurRoles` (5 shapes), `NoResourceAccessClaim`; non-JWT access token in the gate: none | **added** callback `not-a-jwt`. Malformed JSON text in the claim is not tested (see D-2) |
| N7 | Realm-name edge cases | `RealmName_*` (blank, trim, `a/b`, `../master`, cyrillic) | **added** `a\nb`, `a:b`, `a?b`, `a%2Fb`, `${MUSTASK_REALM}`; `.`/`..` accepted (D-1) |
| N8 | Renewal drops `user`, keeps `system-admin` | C7 test uses exactly that | covered |
| N9 | `user` + unknown extras opens a session | implicit via `CreateToken` helper only | covered enough |
| N10 | No `default-roles-` literal in import | `MustaskRealm_NameIsAPlaceholder...` | covered |
| N11 | Pre-tech-024 store fails drift, reset named | `KeycloakStandDriftCheckTests` with `mustask-realm.pre-tech-024.json` | covered |
| N12 | Frontend table drift | manual compare (C10); `tokenClaims.test.ts`, `session.test.ts` exist | manual |
| N13 | Prose leftovers | grep below | ok |

Prose check: `git grep` for `migrate-live-realm`, `KeycloakLiveRealm`,
`instance-admin`, `default-roles-mustask`, `Администраторы`, "realm role"
outside `docs/features`, `docs/analysis`, `docs/templates/examples`: only
ADR-0018/0025/0027 (historical, amended by 0033), ADR-0033 itself, and
negative assertions in tests/scripts. `src/backend/AGENTS.md`,
`docs/runbooks/local-migrations.md`, `scripts/keycloak-seed.sh` header (usage,
`KEYCLOAK_REALM`, roles) and `docs/error-codes.md` (`session.no_access` row)
match the code. One left-over: `SessionCookieContractTests.cs:78-80` still
spells `/Администраторы` group names as a fixture (harmless data, see D-3).

### Runs

| Date | What was run | Result |
|------|--------------|--------|
| 2026-11-02 | `dotnet build Mustask.sln` after the added tests | 0 warnings, 0 errors |
| 2026-11-02 | tests | not run by QA (lane not granted); the added tests are listed in the report for the orchestrator |

### Defects

#### D-1: realm name `.` and `..` accepted

- Severity: minor
- Steps: `KeycloakStandIdentifiers.RealmName("..")` returns `..`; pattern is `^[A-Za-z0-9._-]+$`.
- Expected / Actual: ADR says the name goes into URLs and a file name (`/realms/..`, `..-realm.json`); a dot-only name should be rejected. Actual: accepted. Operator-supplied configuration only, hence minor.
- Status: open (gate-3 debt)

#### D-2: malformed `resource_access` text throws

- Severity: minor
- Steps: `KeycloakPermissionClaims.Append` with a `resource_access` claim that is not JSON -> `JsonDocument.Parse` throws `JsonException` inside `OnTokenValidated`. Unreachable with a validly signed Keycloak token (the claim is always JSON), so no test was written.
- Status: open (gate-3 debt, optional hardening)

#### D-3: cosmetic

- Severity: minor
- `SessionAccessGate`/`SessionAuthenticationSetup.OnTokenValidated`: `context.HandleResponse();            RedirectToPanel(...)` on one line (lost line break); `SessionEndpoints.cs` using order (`Mustask.Api.Authentication` after `...Readiness`). Stale `/Администраторы` fixture strings in `SessionCookieContractTests`.
- Status: open (gate-3 debt)

### Verdict

blockers: 0 · majors: 0 · result: pass, conditional on the added tests being green (not run by QA).
