# QA — Area, project and team navigation (feat-043)

## Backend

Scope: `GET /api/organizations/{o}/overview`, `GET …/selector-offers`, and the comment-only CR-3/CR-6 changes (US-2, US-8 data, US-9 CR-3/CR-6). The panel is stage 6; US-6 and US-10 are feat-048's. The plan was drawn from spec, US-2/8/9 and `api-contract.md` before the implementation was read.

### Coverage matrix

| Criterion | Check | Type | Status |
|---|---|---|---|
| US-2 counts once, active only, departments only (2/4/3, E and sub-areas excluded) | `Cast_OfUs2_CountsAndOffersMatchTheStory` (cast built by real writes: area move, link, deactivate); dev `Overview_CountsDepartmentsProjectsAndTeamsOnce…` | integration (API, Infra) | done |
| US-2 «Обзор» bounded: first 5 by name, statements constant at volume | dev `BothReads_StatementCountIsPinned_At…` (7 and 5 statements, 5 and 200 objects); QA `Overview_OrdersByRussianCollation…AndCutsToFive` | integration | done |
| US-2 team block: caller's own teams, all; none for Vera | dev `Overview_MyTeams…`; QA `EveryRoleReads_AndIsMemberIsPerCaller`, cast `myTeams=[VENUS]` | integration | done |
| US-2 «Проекты»/«Команды» rows: place, key, mark «Моя команда» | cast (departmentId/Name, null for D, isMember) | integration | done |
| US-2 empty states («Пустая», «Север») | `EmptyOrganization_…ZeroCountsAndEmptyArrays`, `OrganizationWithoutDepartments_…` | integration | done |
| US-2 BR-NV-15 caller required | `NavigationReadSignatureTests`, handler tests | unit | done |
| US-8 / BR-NV-17 offers data: place, `teamIds`, `departmentIds`, no nodes/sub-areas/deactivated/deleted | cast; `Offers_FollowAMoveAndATeamDelete_NothingStale`; `RestoredProject_ReturnsWithItsPlaceAndLinks` | integration | done |
| Reachability: other organization's rows, deactivated org | `OtherOrganizationsRows_NeverAppear`, `DeactivatedOrganization_IsNotFound` | integration | done |
| Access: 401, non-member 404, viewer 200; malformed id 404; upper-case id | dev `Reads_AreForEveryMember…`; QA `MalformedOrganizationId_…`; `OrganizationAccessInventoryTests` | integration | done |
| Route inventory 71 to 73, OpenAPI | `OrganizationAccessInventoryTests`, `TeamViewContainmentTests`, `OrganizationsOpenApiTests` (in the run below) | integration | done |
| US-9 CR-3/CR-6: comments only | diff of `fa9d068f`: two doc comments, no code or assertion change | review | done |
| US-2 "within 2 s" at 1 000 areas / 200 projects / 200 teams | statement pin at 200/200/20 only; no timing test | not covered | see D-2 |

### Additional scenarios

| # | Scenario | Result |
|---|---|---|
| 1 | Russian collation, case not deciding (Альфа, Арбуз, арбуз2, бета, Ёж, еж2, яблоко) | pass |
| 2 | Project moved to top level loses its place; deleted team gone from counts, `teamIds`, `departmentIds` | pass |
| 3 | Deactivate then restore a project keeps place and both team links | pass |
| 4 | `isMember` is per caller (viewer sees none) | pass |
| 5 | No migration in this line; the Testcontainers databases apply the existing ones from clean | n/a |

### Runs

| What | Result |
|---|---|
| Api.IntegrationTests, filter NavigationReads, OrganizationAccessInventory, TeamViewContainment, OrganizationsOpenApi | 262 pass (3 of my first-draft tests failed on my wrong expectation, 204 vs 200 of the area move; fixed in the test) |
| Api.IntegrationTests, NavigationReadsQaTests | 12 pass |
| Application.Tests, filter Navigation | 7 pass |
| Infrastructure.Tests, filter Navigation | 11 pass |
| Architecture.Tests (all) | 86 pass |

Not run: the full suite, E2E (standing answer NO).

### Defects

#### D-1: `teamIds` and `departmentIds` have no stated or enforced order

- Severity: minor
- Steps: read `selector-offers`; `NavigationReadModel` builds both from unordered link rows (`ToLookup` over a statement with no `ORDER BY`).
- Expected / Actual: the contract says lists come in the order the server gives; for these two arrays the order is whatever PostgreSQL returns, so it may vary between calls. The panel treats them as sets, so no user-visible effect is known; the contract should say "a set, no order", or the read should sort them.
- Status: open, carried to gate-3 as a debt

#### D-2: no test for the 2 s read budget at volume

- Severity: minor
- Steps: spec NFR Volumes and US-2 "page is read within 2 s"; only the statement counts are pinned (at 200 projects, 200 teams, 20 departments, not 1 000 areas).
- Expected / Actual: the constant statement count is the proven part. Timing is unmeasured, and the NFR's 1 000 areas are not seeded. Low risk (five/seven simple statements), but not demonstrated.
- Status: open, carried as a debt

#### D-3: `plan-backend.md` § 5 still says "Not run yet"

- Severity: minor (documentation)
- The tests ran green here; the line is stale. Also `status.md` still reads `stage: 04-backend` (the orchestrator's to move).
- Status: open

### Verdict

blockers: 0 · majors: 0 · minors: 3 · result: pass (backend). E2E asked by the frontend QA pass, not here; standing answer NO.
