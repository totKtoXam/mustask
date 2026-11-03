# QA — tech-025 user projection without the provider

No spec.md: the acceptance source is the queue line (status.md Notes), ADR-0034
and status.md Decisions D-1..D-9. The plan below was written before the
implementation was read. Route M, backend only, single pass; E2E: standing
answer NO (not asked).

## Backend

### Coverage matrix

Test names are short; files: `SU` = Api.IntegrationTests/Features/Session/SessionUserProjectionTests.cs,
`SC` = .../SessionUserProjectionClaimsTests.cs, `H` = Application.Tests/Features/UserDirectory/RecordSignIn/RecordSignInHandlerTests.cs,
`V` = Domain.Tests/UserDirectory/SignInProfileValidationTests.cs, `P` = Domain.Tests/UserDirectory/ProjectedUserTests.cs.
"(added)" = written in this pass.

| # | Criterion | Covered by | Status |
|---|-----------|------------|--------|
| C1 | Sign-in upserts the user from claims | SU Callback_FirstSignIn_Creates...; H Handle_FirstSignIn | covered |
| C2 | Search finds only after sign-in | SU Callback_FirstSignIn (lookup), Search_FindsThePerson_OnlyAfterTheSignIn (SearchAsync, added) | covered |
| C3 | Changed claims at sign-in update the row | SU Callback_SecondSignInWithChangedNames; H, P RecordSignIn_Changed* | covered |
| C4 | Renewal with changed names updates the row | SU Renewal_WithChangedClaims_UpdatesTheRow | covered |
| C5 | Renewal with unchanged claims writes nothing | SU Renewal_WithUnchangedClaims_WritesNothing; Renewal_AfterAChangedRenewal_..._WritesNothing (fingerprint stored by a renewal, added) | covered |
| C6 | New user whose upsert fails fails the sign-in | SU Callback_NewUserWithoutAUsername / OversizedClaim (no cookie, authError, no row) | covered |
| C7 | Known user's failed refresh logs and continues | SU Callback_KnownUserWhoseUpsertFails_IsLetThrough | covered |
| C8 | Renewal never ends the session on a failed upsert | SU Renewal_WhoseUpsertFails_KeepsTheSession; Renewal_WithoutAUsername_... (added); Renewal_AfterAFailedUpsert_TriesAgain (no fingerprint stored, added) | covered |
| C9 | Failed "is known" check counts as unknown | none (needs a faulting IUserDirectory; code reads as specified: IsKnownAsync catch returns false) | gap, accepted: read-only verified |
| C10 | Concurrent first sign-ins leave one row | SU ConcurrentFirstSignIns (8 parallel, one Added); H TwoFirstSignInsAtOnce | covered |
| C11 | Legacy absent/deleted/disabled row revived | P RecordSignIn_Legacy*; H LegacyInactiveRow; SU Callback_LegacyDisabledRow_IsReactivatedByTheSignIn (added) | covered |
| C12 | No provider/listener/push channel | leftover grep (below); KeycloakRealmImportShapeTests; AppHostStandWiringRules; whole Architecture green | covered |
| C13 | Seven push codes deprecated; catalog and docs agree | contracts json, docs/error-codes.md "yes" column matches ErrorCodesReferenceGenerator format; UserDirectoryRetiredCodesTests (added) pins deprecated + no constant + live codes not deprecated | covered |
| C14 | verify.sh bucket/switch rename consistent | `bash -n scripts/verify.sh` ok; scripts/tests/verify-base-and-abort-cases.sh updated; LiveRealmFactAttribute.Switch = MUSTASK_KEYCLOAK_LIVE_TESTS; no old names left in scripts/.github | covered |

### Additional scenarios

| # | Scenario | Covered by | Status |
|---|----------|------------|--------|
| N1 | Missing/blank sub | V EnsureValid_MissingId; SC ProfileOf_Unreadable; H MissingRequiredClaim | covered |
| N2 | Missing/blank username | V, H, SU Callback_NewUserWithoutAUsername | covered |
| N3 | 255 vs 256 code points, astral pairs | V FieldsAtTheLimit / OneOverTheLimit / Astral | covered |
| N4 | U+0000 and noncharacters | V NulOrNoncharacter; H OddCharacters; P SignIn_AndRecordSignIn_WithANoncharacter | covered |
| N5 | Name from only preferred_username | P SignIn_NoNamesNoEmail; SU Callback_NewUserWithOnlyAUsername_IsStored (added) | covered |
| N6 | E-mail absent, later present | SU only-username (absent); P RecordSignIn_ChangedEmailOnly / EmailDropped | covered |
| N7 | Two subs, same username and e-mail | SU Callback_TwoDifferentPeopleWithTheSameUsernameAndEmail_GetTwoRows (added; no unique index exists) | covered |
| N8 | Padded/whitespace claims | V whitespace id; SU "   " username. Padded names are stored verbatim (no trim): by design, not a defect | covered |
| N9 | Disabled in Keycloak | ADR-0034 Consequences (stale row stays; no access effect) | prose only |
| N10 | Renewal token lost its claims | SU Renewal_WithoutAUsername_KeepsTheSession_AndTheStoredRow (added) | covered |
| N11 | Leftovers | grep below | covered |
| N12 | Prose | below | covered |

### Prose and leftovers

- Leftover grep (`PushSignature|PushChannel|UserPush|mustask-user-projection|MUSTASK_KEYCLOAK_PROVIDER_TESTS|KeycloakProvider|LiveProvider|providers/user-projection`): code, scripts, CI, deploy, frontend clean. Hits only in ADR-0025/0034, mod-014/mod-016/tech-047 history, `KeycloakRealmImportShapeTests` (asserts absence) and the pre-tech-024 realm fixture. One live hit: `docs/backlog.md` line 15 (tech-008) still speaks of `UserPushValidation`, a class this task deleted (D-3 below).
- ADR-0034 matches the code (rules 1-3, outcomes, limits, failure rule, fingerprint). Fixed one typo ("the drift check" sentence start).
- `src/backend/AGENTS.md`, `docs/runbooks/local-migrations.md`: consistent with the sign-in projection and the `KeycloakLive` / `MUSTASK_KEYCLOAK_LIVE_TESTS` names.
- `contracts/errors/user_directory.json` and `docs/error-codes.md`: the seven codes carry `"deprecated": true` / `yes`, in the format ErrorCodesReferenceGenerator writes; live codes untouched.
- `scripts/verify.sh`: `bash -n` ok; `keycloak` bucket for Dockerfile and realm imports; stub case in `scripts/tests/verify-base-and-abort-cases.sh` renamed with it. verify.sh itself not run (rule).

### Runs

| Date | What | Result |
|------|------|--------|
| 2026-11-02 | `dotnet build Mustask.sln` (with the added tests) | 0 warnings, 0 errors |
| 2026-11-02 | `dotnet test` | NOT run (the machine's test lane is another session's); added tests are unrun |

Already green on this tree before QA (dev/orchestrator): Domain UserDirectory, Application RecordSignIn, Architecture, Contracts, Infrastructure.Tests, Api.IntegrationTests.

### Defects

#### D-1: E2E members specs and support helpers expect a push-fed projection

- Severity: minor (debt for gate-3; E2E is paused, not rewritten here)
- Steps: read `src/frontend/e2e/support/membersApi.ts` (`pollUntil`, `awaitInDirectory`, `awaitNotOffered`, `awaitMemberState`, `createDirectoryUser`: "fed by Keycloak's event listener asynchronously (ADR-0025)") and `src/frontend/e2e/support/keycloakAdmin.ts` (`createEphemeralMember`, `setUserEnabled`: "the projection learns of it"; BR-UP-3 name rule); consumers: specs 27-organization-members, 28-team-members, 29-role-views, 30-assignee-membership.
- Expected / Actual: a Keycloak-created fixture user must now sign in once to be found; `awaitNotOffered` after `setUserEnabled(false)` can never turn true (a disabled user stays in the projection, ADR-0034). The specs time out when run.
- Status: open, carried to gate-3 (follow-up: E2E fixture users sign in once, drop the disabled-withdrawal case).

#### D-2: Obsolete wording in backlog line tech-008

- Severity: minor (docs)
- Steps: `docs/backlog.md` line 15 asks for a Domain test pinning "the noncharacter rule of UserPushValidation".
- Expected / Actual: the class is deleted; the rule now lives in `SignInProfileValidation` and is pinned by `SignInProfileValidationTests.EnsureValid_NulOrNoncharacter...`. The line is moot. `docs/backlog.md` is the orchestrator's file; not edited.
- Status: open, for the orchestrator.

#### D-3: C9 has no automated test

- Severity: minor
- Steps: a faulting `IUserDirectory` is not available in the integration host; the rule "failed is-known check counts as unknown" is in `SessionUserProjection.IsKnownAsync` (catch returns false) and was verified by reading only.
- Status: open, accepted as debt.

No blocker or major found.

### Tests added (unrun)

- `src/backend/tests/Mustask.Api.IntegrationTests/Features/Session/SessionUserProjectionTests.cs`: 7 tests added, `RenewAsync` gained an overload taking an explicit cookie.
- `src/backend/tests/Mustask.Contracts.Tests/UserDirectoryRetiredCodesTests.cs`: 8 cases (new file).

### Verdict

blockers: 0 · result: pass, conditional on the added tests being green when the test lane runs them
