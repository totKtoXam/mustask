# QA — feat-033-orphaned-organization-rescue

## Backend

Pass 1, 2026-11-03, qa-engineer (clean session, deep tier for L). Test plan
written from `spec.md`, US-1..US-3 and `api-contract.md` before the
implementation was read. Checkout: the task worktree, HEAD 0de1cd5b
(backend commits df5346c0..6fb75f09). E2E: not asked (standing answer no;
frontend QA owns the question on this route).

Routes: R1 `GET /rescue`, R2 `GET /rescue/appointee-candidates`, R3 `POST
/rescue/{id}/appointments`, R4 `GET /rescue/{id}/appointments`, O1 `GET
/{id}/appointments` (all under `/api/organizations`). Test classes, short
names: AAE `AppointAdministratorEndpointTests`, AAC `…ConcurrencyTests`, ARA
`AppointmentRecordAtomicityTests`, GRO `GetRescueOrganizationsEndpointTests`,
GAE `GetAppointmentsEndpointTests`, SAC `SearchAppointeeCandidatesEndpointTests`,
ARM `AppointmentReadModelTests` (Infrastructure), QA `RescueQaGapTests` and
TP `AppointmentReasonTrimParityTests` (added by this pass).

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| BR-RS-1 | `organizations.organization.rescue` Instance scope, `system-admin` only; panel mirror | unit (`InstanceRolesTests`, `PermissionCatalogTests`, `tokenClaims.test.ts`) | ✅ |
| BR-RS-1, "Without the right" ×3 | R1..R4 → 403 `organizations.rescue.forbidden` «Недостаточно прав» for: no role, `user`, `user`+`organization-creator`, `user`+`dictionaries-admin`, both, `system-admin` without `user` (401/403), `system-admin` of a foreign client, `system-admin` as a realm role, the organization's own admin; nothing written | integration (QA, AAE, GRO, SAC, GAE) | ✅ |
| BR-RS-6a row 1 | no right + missing organization → 403 on R3 and R4 | integration (QA) | ✅ |
| BR-RS-1 | 401 on R1..R4 and O1 | integration | ✅ |
| US-1 list | active + deactivated, exactly id/name/status/lastAppointedAt, name order | integration (GRO), ARM | ✅ |
| US-1 filter | substring, case, ё=е both ways, trimmed, blank = all, `%` `_` `\` literal, >100 chars → count 0 | integration (GRO, QA), ARM | ✅ |
| US-1 paging | 25 a page, true count, page past last `[]`, `page` 0/-1/abc/1.5/overflow/empty → 400 `invalid_page` | integration (GRO, QA) | ✅ |
| US-1/US-2 content hidden | rescuer after appointing: 404 on the organization, `/access`, `/members`, `/projects`, `/teams`, `/overview`, O1, `/member-candidates` | integration (QA, AAE) | ✅ |
| BR-RS-6 search | active projected only, inactive hidden, <2 chars none, 3 fields, no membership, >100 → 400 | integration (SAC) | ✅ (20-cap and `moreMatched` belong to mod-014's own tests) |
| US-2 non-member / member / viewer in a team | add or raise, addedBy kept, roleChangedBy = caller, teams kept, others identical | integration (AAE) | ✅ |
| BR-RS-4 add/raise only | every other membership row identical, refusals included | integration (AAE, QA) | ✅ |
| US-2 deactivated / repeatable / next request | stays deactivated, appointee restores; second appointee leaves first; appointee reads O1 (membership.write) with the same token | integration (AAE, QA) | ✅ |
| US-2 refusals | already admin, never signed in, pre-tech-025 inactive, self (and another rescuer may), unknown/malformed/zero org | integration (AAE) | ✅ |
| BR-RS-5 reason | empty/spaces/tab, 500 and 501 chars and emoji, trimmed store, 502 → 500 after trim, NUL, noncharacter, Unicode spaces, lone surrogate → 400 | unit + integration (AAE, QA, TP) | ⚠ D-1 |
| BR-RS-6a order | all five rows of the outline + inactive-admin → unavailable, self-not-member-no-reason → self | integration (AAE, QA) | ✅ |
| BR-RS-10 record / atomic / refusal writes none | 201 body = row; FK failure in the same save → 500, nothing kept | integration (AAE, ARA) | ✅ |
| BR-RS-10 readers | R4 any org, deactivated incl., newest first, `[]`, 404; O1 admin 200 same bytes, member/viewer 403 `organizations.access.forbidden`, stranger / other org's admin / deactivated org's admin 404 | integration (GAE, QA) | ✅ |
| US-3 record outlives membership | role change and removal after appointment | integration (GAE, AAC) | ✅ |
| BR-RS-11 | same user ×2, different users, vs removal both orders, vs role change both orders, vs raise-to-admin first → `already_admin` | integration (AAC, QA) | ✅ |
| BR-M1-33 inventory | two rescue routes are exact instance exceptions; O1 in the list | integration (`OrganizationAccessInventoryTests`) | ✅ — but three neighbouring inventory tests are red: D-2 |
| Migration | applies on a clean database, rollback drops only the table, re-apply restores | integration (`MigrationsTests`) | ✅ |
| Error codes | 8 codes in catalog + `contracts/errors` | contracts (`OrganizationErrorCatalogTests`) | ✅ |
| NFR volumes / query budget | 1000 orgs: 2 statements < 2 s; 100 records: 2 statements | integration (ARM) | ✅ |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A-1 | malformed JSON from a caller without the right | 403, not 400 | ✅ QA |
| A-2 | unknown organization and an invalid body | 404 before 400 | ✅ by code (handler step 1–2 before 3) and AAE "unknown user + missing org" |
| A-3 | `userId` missing / blank / 256 chars | 400 `membership.user_invalid` | ✅ AAE, QA |
| A-4 | `userId` with spaces around a known id | not trimmed → `user_unavailable` | ✅ QA |
| A-5 | reason of NBSP / U+3000 / U+2003 only | `reason_required` | ✅ QA, TP; U+FEFF ✗ D-1 |
| A-6 | 502 code points trimming to 500 | 201 | ✅ QA |
| A-7 | lone surrogate `\ud800` | 400, no 500 | ✅ QA |
| A-8 | `lastAppointedAt` = newest record | max | ✅ ARM |
| A-9 | `page` 0, -1, abc, 1.5, overflow, empty | 400 `invalid_page` | ✅ GRO, QA |
| A-10 | O1 for a deactivated organization by its admin | 404 | ✅ QA |
| A-11 | system-admin who is also a member reads O1 by role, R4 regardless | as contract | by code only (O1 is the plain scoped check, R4 asks no membership); not tested |
| A-12 | pre-tech-025 inactive row already admin | `user_unavailable` | ✅ QA |

### Runs

Filtered only (shared lane). The two whole-assembly reruns were the
coordinator's, not this session's.

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-11-03 | coordinator: whole `Mustask.Infrastructure.Tests` (incl. `MigrationsTests`, ARM) | 459/459 green |
| 2026-11-03 | coordinator: whole `Mustask.Api.IntegrationTests`, compiling this pass's uncommitted QA file | 5 failed: 2 × QA `Appoint_AReasonOfUnicodeSpacesOnly_IsRequired` (U+FEFF → 201, D-1) and the three tests of D-2; every other feat-033 and QA test green |
| 2026-11-03 | `dotnet test Mustask.Domain.Tests --filter FullyQualifiedName~AppointmentReasonTrimParityTests` (before the skips) | 3 passed, 3 failed: the U+FEFF cases (D-1) |
| 2026-11-03 | same, after marking the U+FEFF cases `Skip = D-1` | 3 passed, 3 skipped |
| 2026-11-03 | `dotnet test Mustask.Api.IntegrationTests --filter FullyQualifiedName~RescueQaGapTests.Appoint_AReasonOfUnicodeSpacesOnly_IsRequired` (cases as code points, U+FEFF skipped) | 1 passed, 2 skipped |

### Defects

#### D-1: «Причина» keeps U+FEFF, which the panel trims

- Severity: minor
- Steps: as a system-admin, `POST /api/organizations/rescue/{id}/appointments`
  with `{"userId": "<known>", "reason": "\uFEFF"}` (the JSON escape) (or a reason pasted with a
  leading byte-order mark).
- Expected / Actual: BR-RS-5 says the dialog and the server count the same way;
  the panel trims with `String.prototype.trim`, which removes U+FEFF, so
  expected 400 `reason_required` and a stored reason without the mark. Actual:
  201 and a record whose reason is an invisible character; `.NET string.Trim()`
  does not treat U+FEFF as white space. Also off by one at the limit: U+FEFF +
  500 characters passes the dialog and gets `reason_too_long` from the server.
  Fix: trim U+FEFF too in `AppointmentReason.TryCreate` (Domain). Tests ready,
  skipped with this id: TP `OnlyCharactersThePanelTrims_IsRequired` (2 cases),
  `ALeadingByteOrderMark_IsNotKept`, QA `Appoint_AReasonOfUnicodeSpacesOnly_IsRequired` (2 cases).
- Status: open — carried to gate-3 as a debt

#### D-2: three existing Api integration tests were not updated for the new routes and code

- Severity: major (the branch's `Mustask.Api.IntegrationTests` is red; no MR
  with failing tests). The production behaviour is right; the fixes are in
  test code.
- Steps: run the three tests below (they failed in the coordinator's whole
  rerun of the assembly).
- Expected / Actual:
  1. `TeamViewContainmentTests.TheRouteInventory_ListsTheThreeTeamAddressedRoutes_AndNotTheProjectNestedOnes`
     asserts `OrganizationAddressedRoutes.All.Count == 73`; actual 74 (O1 added).
     The same count was raised to 74 in `OrganizationAccessInventoryTests` but
     not here.
  2. `OrganizationsOpenApiTests.OpenApiDocument_OrganizationOperations_AreExactlyTheContracts`
     asserts `ContractOperations.Length == 58`; actual 59 (O1 enters through
     `OrganizationAddressedRoutes`). The four rescue operations are tagged
     `Organizations` too and are not in `ContractOperations`, so after the
     count the set comparison is likely to fail next: the list needs the rescue
     operations.
  3. `CreateOrganizationEndpointTests.Create_EveryCodeButTheCreateRight_Returns403CreateForbidden_CreatesNothing`:
     `KeycloakTestRealm.RolesFor` finds no role set that gives every code but
     `organizations.organization.create`. The only role holding the new rescue
     code is `system-admin`, which also holds create, so "every other code
     together" cannot be expressed in roles any more. The test needs restating
     (for example, every role but `system-admin` and `organization-creator`,
     or dropping instance codes no role-set can carry without create).
- Status: open — back to backend-developer

#### D-3: api-contract § 3 gives `invalid_page` the wrong field

- Severity: minor
- Steps: read `api-contract.md` § 3, row `organizations.rescue.invalid_page`.
- Expected / Actual: "400 (field page)", as § 1.1 and the code have it; the
  table says "400 (field reason)". Documentation only; a change of
  `api-contract.md` goes with a note in `status.md` (not edited by QA).
- Status: open — carried to gate-3 as a debt

### Verdict

blockers: 0 · majors: 1 (D-2) · minors: 2 (D-1, D-3) · result: fail —
D-2 returns to the backend-developer; the re-check covers the three tests and
nothing else.
