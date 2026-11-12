# doc-015 — Backend code-quality analysis

## Scope and method

- **Subject:** `src/backend` at commit `705ed258` (origin/main). The task branch `b83471b5` has no changes under `src/` relative to that commit (`git diff --stat 705ed258 HEAD -- src` is empty).
- **Criteria:** the seven agreed with the human on 2026-11-09:

  | Key | Criterion | How it was measured |
  |---|---|---|
  | ARC | Architecture and ADR conformance | Each canon rule in `docs/conventions/backend.md` and ADR-0002/0003/0021/0026 was read against the code. Slice layouts were compared with the Dictionaries reference slice. `using`/namespace greps covered the Onion and slice boundaries. The `Mustask.Architecture.Tests` rules were read to see what each one actually enforces. |
  | SEC | Security and authorization | Endpoint categories and policies were inventoried. JWT bearer and session (ADR-0022/0033) configuration was read. Outside-in id resolution was traced through handlers, repositories and read models. Raw SQL, secrets, CORS, error bodies, logging and the deploy/nginx layout were checked. |
  | TXN | Transactions and concurrency | `UnitOfWork` and every `ExecuteInTransactionAsync` site were reviewed. Lock statements were checked for order and for lock-then-load. Every read-then-write pre-check was checked for a backing unique index. Idempotency and counters were checked, plus ADR-0010/0011/0012/0013 conformance. |
  | EFC | EF Core query efficiency | Read models were checked for projection and tracking. Paging was checked against ADR-0014/0015/0036. N+1 and per-row loops were looked for. The index inventory came from the model snapshot. The statement-count tests were located. |
  | ERR | Error handling and the error contract | The exception-handler pipeline, the registry scanner and catalog tests, throw sites, `DbUpdateException` mapping, `ProducesProblem` counts per endpoint file, and response-body shapes were reviewed (ADR-0004/0005/0006). |
  | ASY | Async and cancellation | Every endpoint delegate and every EF/Npgsql/HTTP terminal was grepped for token forwarding. Sync-over-async and fire-and-forget patterns were searched. Background services, outbound HTTP clients, timeouts and the cancel path were reviewed. |
  | DEP | Dependency hygiene | `Directory.Packages.props`, `Directory.Build.props`, `global.json`, all 14 csproj files and the resolved `obj/project.assets.json` were read. The Onion and package rules in architecture tests and the analyzer/warning settings were checked. |

- **Mode:** read-only. No build, test, server, restore or EXPLAIN was run. Runtime behaviour is inferred from the code, package defaults and the existing tests.
- **Process:**
  1. Seven analysts, one per criterion.
  2. Consolidation: duplicates merged, every blocker and major spot-checked against the cited lines, ids assigned.
  3. A clean-context review of this document (done, round 1).
- **Review:** [review.md](review.md) corrected this analysis. Nearly every cited line was confirmed; the corrections are mostly severity, plus three canon-sanctioned patterns that had been reported as defects. Seven severities changed, ASY-4 went up, and three misses were added (ARC-11, the ADR-0031 inconsistency merged into ARC-3, the refresh-retry hazard in ASY-4). Each corrected finding closes with a `Review:` line.
- **mod-019 overlap:** at consolidation, `origin/mod/019-scoped-access` changes 13 files under `docs/features/mod-019-scoped-access/` and no code. "In flight" marks are therefore judged against its spec and ADR-0029, not against a diff.
- **Severity scale:**
  - blocker: a defect that must be fixed before further work.
  - major: a real risk or a canon breach with spreading impact.
  - minor: a local defect or a gap with bounded impact.
  - info: no defect; drift or a missing guardrail worth recording.

## Summary

| Criterion | blocker | major | minor | info |
|---|---|---|---|---|
| ARC | 0 | 0 | 6 | 5 |
| SEC | 0 | 2 | 2 | 2 |
| TXN | 0 | 0 | 3 | 2 |
| EFC | 0 | 0 | 2 | 4 |
| ERR | 0 | 0 | 1 | 5 |
| ASY | 0 | 0 | 2 | 4 |
| DEP | 0 | 0 | 2 | 4 |
| **Total** | **0** | **2** | **18** | **26** |

The backend is in good structural health:
- Onion references are enforced.
- No slice imports another.
- Every write locks before it loads.
- Every uniqueness pre-check is backed by an index mapped to the same 409.
- Cancellation tokens reach every EF terminal.
- Error codes come from a tested registry.

No blocker was found. Verdict after review: needs a human decision.

The two majors are security findings, and both widen who can obtain a working MUSTASK token:
- **SEC-1:** the bearer path accepts any realm token whose audience is `mustask-api`, with no `azp` check. The `azp` half is a defect. Whether the `user` role should also gate bearer requests is a policy question, because ADR-0033 rule 2 keeps organization rights.
- **SEC-2:** under the shared origin `portal.devexpert.kz/mustask`, any same-origin script can call `/api/session/token` and mint short-lived access tokens. The refresh token stays unreadable, so ADR-0022's core protection holds; what grows is the set of scripts able to mint access tokens.

Human decisions for gate-1:
1. SEC-2: record a risk acceptance for the shared origin, or give MUSTASK its own origin.
2. SEC-1: the user-gate policy on bearer requests. The `azp` allow-list needs no decision, but it must admit `mustask-e2e`.
3. ARC-11: accept or revise the proposed ADRs 0021, 0022, 0033, 0034 and 0036. They are implemented and cited as canon but still `Status: proposed`.

The architecture findings are bounded. ARC-1 (a link rule repeated in four handlers) and ARC-2 (two shared helpers at the module root) are minor. ARC-3 and ARC-5 conform to accepted ADR-0031 and to backend.md's bulk-SQL allowance.

The recurring theme across criteria is canon rules held by discipline rather than by tests. These include:
- `ProducesProblem` (ARC-6)
- reachability in one place (ARC-4)
- `CancellationToken` forwarding (ASY-1)
- `.editorconfig` severities (DEP-3)
- throw-site code rules (ERR-2)
- the xmin convention on the real model (TXN-4)

One likely real defect is EFC-1: a far page of the rescue list overflows to a negative OFFSET. ASY-4 is a latent one: a retried refresh POST can end the user's session.

## Findings

### ARC — Architecture and ADR conformance

#### ARC-1 · minor · Area-tree type rules are enforced in handlers, not in Domain; the link rule exists only in four handlers
- **Evidence:**
  - `src/backend/Mustask.Domain/Organizations/AreaLinks.cs:35-40`: `AreaTeam.Link` takes a bare `Guid areaId` and checks only well-formedness.
  - The "department only" link rule is repeated in four handlers:
    - `src/backend/Mustask.Application/Features/Organizations/AddTeam/AddTeamHandler.cs:131-135`
    - `src/backend/Mustask.Application/Features/Organizations/CreateArea/CreateAreaHandler.cs:134-137`
    - `src/backend/Mustask.Application/Features/Organizations/LinkTeamsToArea/LinkTeamsToAreaHandler.cs:58-62`
    - `src/backend/Mustask.Application/Features/Organizations/LinkAreasToTeam/LinkAreasToTeamHandler.cs:71-74`
  - `src/backend/Mustask.Domain/Organizations/Area.cs:109-125`: `CreateProjectNode(Guid? parentId)` never checks that the parent is a department.
  - `src/backend/Mustask.Application/Features/Organizations/CreateProject/CreateProjectHandler.cs:122-128`: the handler checks the department rule on creation. The same rule for a move lives in `Area.cs:190-195` (`MoveUnder`).
  - `CreateAreaHandler.cs:129-132` overlaps the check that `Area.CreateSubArea` already makes (`Area.cs:140-143`), but it also fixes the order of answers ahead of the link check at `:134`, so it is not a pure duplicate.
- **Rule:** `docs/conventions/backend.md:44-46` (domain rules live in Domain; the handler orchestrates, the domain decides) and `:50-52`.
- **Impact:**
  - A new write path that creates an `area_teams` row must remember to copy the rule. ADR-0031 rule 4 keeps area-team links for departments only, and neither the raw bulk insert (ARC-5) nor the check constraint covers the rule. ADR-0029 grants are a separate concept and do not create `area_teams` rows.
  - The project-node rule is split between the domain (move) and a handler (create), so the two can drift apart.
  - Bounded: one module, one rule, consistently placed today.
- **Proposed fix:**
  - A domain rule over `AreaType` (for example `AreaRules.EnsureLinkable(AreaType)`), called by every link path at the same step. `AreaTeam.Link(Area area, ...)` does not fit: the handlers lock and get back types only (`AddTeamHandler.cs:124-135` uses `lockedAreas.Values` as `AreaType`), so it would force an extra load.
  - `Area.CreateProjectNode` checks the parent's type the same way, throwing `AreaTypeNotAllowed` unless it is a department.
  - Handlers keep their order of answers (BR-AW-15) by calling the domain at the same step. Keep `CreateAreaHandler.cs:129-132` for that reason, with a comment saying the domain is authoritative.
  - Add Domain unit tests for both rules.
- **Effort:** S
- **In flight:** mod-019 (link codes) rewrites the access checks at the top of these same handlers. The type rule is not authorization, but the files collide.
- **Review:** partly confirmed, major → minor — see review.md.

#### ARC-2 · minor · Two shared helpers at the module root of `Application/Features` break the no-shared-services rule; the arch test cannot see the module root
- **Evidence:**
  - `src/backend/Mustask.Application/Features/Organizations/AreaRequests.cs:7-61`: a static helper shared by 11 slices (`ParseIds`, `TreeOf`, `TypeName`, `EnsureProjectActiveAsync`). Its docstring says it sits at the module root "so no slice references another".
  - `src/backend/Mustask.Application/Features/WorkItems/WorkItemAddress.cs:17`: "a slice takes shared code from Abstractions, the domain or here". This adds a third home the canon does not list.
  - Not a breach: four cross-module interfaces at the module root (`Features/Organizations/IOrganizationStructure.cs`, `Features/Process/IProcessSeed.cs`, `Features/WorkItems/IWorkItemTeamOwnership.cs`, `Features/Boards/ITeamBacklogPlaces.cs`). `backend.md:53-54` lets modules talk through explicit interfaces and does not say where those live.
  - `src/backend/tests/Mustask.Architecture.Tests/VerticalSliceRules.cs:15-17`: the regex requires `Features.<module>.<slice>`, so `using Mustask.Application.Features.Organizations;` never matches.
  - `VerticalSliceRules.cs:81-92`: `SliceOf` returns null for a module-root file, which the rule then skips (`:28-31`).
  - `src/backend/Mustask.Application/Abstractions/Boards/AggregateViews.cs:3`: an unused using. Abstractions imports `Mustask.Application.Features.Organizations`, but no type of it appears in the file.
- **Rule:** `docs/conventions/backend.md:50-52` (no shared services between slices; reuse goes to Domain or Abstractions).
- **Impact:**
  - Two modules already use this third home, and a code comment presents it as allowed.
  - Nothing limits what accumulates there, and the arch test cannot see it.
  - There is no runtime risk, and today it is two small pure helpers.
  - `EnsureProjectActiveAsync` is a reachability rule sitting outside "one place per module" (see ARC-4).
- **Proposed fix:** decide between two options and record the choice.
  - (a) Amend the canon: the module root holds only public contracts (interfaces plus their records), which also settles where the four interfaces live. Add an arch test that allows only those there and bans Abstractions → Features imports.
  - (b) Move the helpers:
    - `TypeName` becomes an `AreaType` wire-name extension in Domain.
    - `ParseIds` becomes `AddressedId.ParseSet`.
    - `TreeOf` moves to Domain.
    - `EnsureProjectActiveAsync` becomes an `IProjectRepository` method.
    - `WorkItemAddress` moves to `Abstractions/WorkItems`.
  - In both cases, delete the unused using at `AggregateViews.cs:3`.
- **Effort:** S
- **In flight:** mod-019 touches the area handlers that call `AreaRequests`.
- **Review:** partly confirmed, major → minor — see review.md.

#### ARC-3 · info · `AreaType` is stored as a number by accepted ADR-0031, which conflicts with `backend.md` while claiming no departures
- **Evidence:**
  - `docs/adr/0031-typed-area-tree.md:39` (accepted): "`areas` carries `type` (`Department` 1, `Project` 2, `Area` 3)", with the check constraint `ck_areas_type_shape`. The code conforms to it.
  - `docs/adr/0031-typed-area-tree.md:82`: "Departures from `docs/conventions/*`: none", while rule 1 prescribes a numeric closed set.
  - `src/backend/Mustask.Infrastructure/Organizations/Configurations/AreaConfiguration.cs:27`: `HasConversion<short>()`.
  - Literal type numbers in SQL:
    - `AreaConfiguration.cs:52-54`: index filter `"type = 2"`.
    - `AreaConfiguration.cs:61-65`: `ck_areas_type_shape` written with 1/2/3.
    - `src/backend/Mustask.Infrastructure/Organizations/AreaRepository.cs:106`: raw lock `AND type = 2`.
    - `src/backend/Mustask.Infrastructure/Migrations/20261028124825_OrganizationAreaWorkspace.cs:135`: `type = 2` / `type = 3`.
  - `src/backend/Mustask.Domain/Organizations/Area.cs:9-19`: the enum with explicit values 1..3.
  - Every other persisted enum uses `HasConversion<string>()` (for example `OrganizationConfiguration.cs:26`, `WorkItemConfiguration.cs:63-64`, `ProjectedUserConfiguration.cs:37`); only `ProjectedUserModelTests.cs:68-69` asserts it.
- **Rule:** `docs/conventions/backend.md:314-318` (a closed set is stored as a string) and ADR-0021 § 2, against accepted ADR-0031 rule 1.
- **Impact:**
  - Not a code defect: by the canon rule, code that conforms to an accepted ADR is not one. The real issue is the ADR-0031 vs `backend.md:314-318` inconsistency, which carries a false "no departures" line. The human settles it: amend ADR-0031 to record the departure, or amend the canon.
  - The magic numbers in SQL (`AreaConfiguration.cs:54,63-65`, `AreaRepository.cs:106`) are real but follow from the ADR. The explicit values 1..3 limit the reorder-corruption risk the canon names.
  - It is the only numeric persisted enum, a precedent for the next typed table (ADR-0029 scopes).
- **Proposed fix:** reconcile ADR-0031 and the canon in the canon-sync task (candidate #15), with a one-line departure note in the ADR. No migration. Converting the column would need an amending ADR first and has a cosmetic benefit only.
- **Effort:** F
- **In flight:** -
- **Review:** partly confirmed, minor → info; the "no departures" inconsistency was added from review M-2 — see review.md.

#### ARC-4 · minor · Reachability predicates are restated inline outside `Reachability.cs`, in Boards and in M1 itself
- **Evidence:**
  - Boards:
    - `src/backend/Mustask.Infrastructure/Boards/BacklogMembership.cs:59-60, 85-86`
    - `src/backend/Mustask.Infrastructure/Boards/AggregateViewReadModel.cs:60, 73, 88-91, 235`
    - `src/backend/Mustask.Infrastructure/Boards/BacklogRepository.cs:71`
  - `src/backend/Mustask.Infrastructure/Boards/ProjectBacklogRepository.cs` uses `Reachability`, so the Boards module mixes both styles.
  - M1's own seam: `src/backend/Mustask.Infrastructure/Organizations/OrganizationStructure.cs:114-115, 131, 205-206, 213`.
  - `src/backend/Mustask.Infrastructure/Organizations/Reachability.cs:5-13`: the documented single place.
- **Rule:** `docs/conventions/backend.md:253-264`; ADR-0026 rules 3, 4, 7.
- **Impact:** "Reachable project" has at least 10 textual copies. ADR-0029 adds a visibility condition, and each missed copy can leak a hidden or deactivated project into a board, backlog or aggregate count. Nothing finds the copies mechanically.
- **Proposed fix:**
  - Compose `ReachabilityRules.ProjectOf`/`ActiveTeamOf`, or `Reachability.ReachableProjects(orgId)`, in Boards and in `OrganizationStructure`. They stay single-statement because both are `IQueryable`.
  - Add a source test banning `ProjectStatus.Active`/`TeamStatus.Active` literals in `Mustask.Infrastructure` outside `Reachability.cs` and the configurations.
- **Effort:** S
- **In flight:** mod-019. ADR-0029 § 6/7 makes every list, counter and search join access and visibility (`AggregateViewReadModel`, `BacklogMembership`). Fix this with mod-019 or after it.

#### ARC-5 · info · Two write paths for project/area team links; the bulk path skips the domain factory's well-formedness checks
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/Organizations/AreaLinkRepository.cs:10-34`: raw `unnest ... ON CONFLICT DO NOTHING` insert, without `AreaTeam.Link`.
  - `AreaLinkRepository.cs:46`: the domain path `Add(AreaTeam)`, used by `AddTeamHandler.cs:175-178` and `CreateAreaHandler.cs:165-168`.
  - `src/backend/Mustask.Infrastructure/Organizations/ProjectTeamRepository.cs:78-106`: raw insert into `project_teams`, bypassing `ProjectTeam.Link`.
  - The checks the raw path skips (`AreaLinks.cs:45-58`, `ProjectTeam.cs:39-49`) are well-formedness only: non-empty ids and the length of `linkedBy`. The handlers guarantee both: ids are parsed and locked, and the subject comes from the token.
  - Callers of the raw area path: `LinkTeamsToAreaHandler.cs:78-79` and `LinkAreasToTeamHandler.cs:81`. Callers of `ProjectTeamRepository.LinkAsync`: `LinkTeamsToProjectHandler.cs:94` and `LinkProjectsToTeamHandler.cs:90`.
- **Rule:** `docs/conventions/backend.md:219-226` explicitly allows raw SQL for bulk operations, and `:55-57` asks for writes through the domain. The canon does not say that bulk SQL must not replace the factory.
- **Impact:** a latent drift risk, not a canon breach: a rule added to `Link` (for example the ARC-1 rule) would not apply to the bulk path unless the handler also calls it.
- **Proposed fix:** optional. The bulk methods could take collections built by `Link(...)` and unnest their fields into the same single idempotent statement. Calling the ARC-1 domain rule in the handlers already covers the type rule.
- **Effort:** S
- **In flight:** mod-019 adds `*.link` codes per link end (ADR-0029 § 4) in these handlers.
- **Review:** partly confirmed, minor → info — see review.md.

#### ARC-6 · minor · The reference slice (Dictionaries) and Session declare no `ProducesProblem`; nothing enforces it
- **Criteria:** ARC, ERR (merged).
- **Evidence:**
  - `src/backend/Mustask.Api/Features/Dictionaries/DictionaryEndpoints.cs:24-35`: four routes, none with `.ProducesProblem`.
  - `src/backend/Mustask.Api/Features/Session/SessionEndpoints.cs:28-63`: none, although `/token` answers `SessionErrorCodes`.
  - The other modules declare it, for example:
    - `src/backend/Mustask.Api/Features/Boards/AggregateViewEndpoints.cs:49-64`: a shared `Map()` with 400/401/403/404/429.
    - Counts per file: Organization 107, Membership 46, WorkItems 44, Board 22, Rescue 14.
  - No test references `ProducesProblem` or `ProducesResponseTypeMetadata`.
- **Rule:** `docs/conventions/backend.md:80-83`; ADR-0004.
- **Impact:** the slice AGENTS.md tells everyone to copy omits the metadata, so copies start without it. The OpenAPI document under-reports 400/401/403/404/409 for these routes.
- **Proposed fix:**
  - Add `.ProducesProblem(...)` to the four dictionary routes and to the 4xx answers of `/api/session/token`.
  - Add an endpoint-metadata test, reusing the `EndpointDataSource` enumeration of the category inventory, asserting that every non-Public `/api` route declares at least one problem response.
- **Effort:** F
- **In flight:** -

#### ARC-7 · minor · Slices that serve more than one use case or route
- **Evidence:**
  - `src/backend/Mustask.Application/Features/Organizations/SearchTeamMemberCandidates/`: two handlers, two queries and a shared helper `TeamMemberCandidateSearch.cs:14-33` in one folder.
  - `src/backend/Mustask.Api/Features/Boards/AggregateViewEndpoints.cs:34-36`: three routes to one `GetViewGroupsHandler`.
  - `AggregateViewEndpoints.cs:13-14, 42-44`: the Boards file maps two WorkItems slices.
- **Rule:** `docs/conventions/backend.md:50` (one use case = one folder = one endpoint) and `:61` (endpoints grouped by module).
- **Impact:** two use cases in one folder share a helper that `VerticalSliceRules` would otherwise refuse, and the pattern is easy to copy. WorkItems routes mapped from the Boards file are hard to find by module.
- **Proposed fix:**
  - Split `SearchNewTeamMemberCandidates` into its own folder and move the helper to `Abstractions/Organizations`.
  - Move the two WorkItems routes to the WorkItems endpoint files.
  - Amend the canon to allow one parameterized read serving several views (`GetViewGroups`), or split it.
- **Effort:** S
- **In flight:** -

#### ARC-8 · info · Canon route rule is stale: organization-scoped modules are not under `/api/<module>/`
- **Evidence:**
  - `docs/conventions/backend.md:61` says routes are `/api/<module>/...`, but:
    - `src/backend/Mustask.Api/Features/WorkItems/WorkItemEndpoints.cs:37`: `/api/organizations/{organizationId}/projects/{projectId}/workitems`.
    - `src/backend/Mustask.Api/Features/Boards/BoardEndpoints.cs:35, 66` and `AggregateViewEndpoints.cs:32`: also under `/api/organizations/{organizationId}`.
  - `docs/adr/0027-organization-access-from-membership.md:128`: the nesting that motivates this.
- **Rule:** `docs/conventions/backend.md:61`.
- **Impact:** every module except Dictionaries and Session departs from the literal canon text. The code follows ADR-0027, so the text is what is wrong, and it misleads whoever designs the next route.
- **Proposed fix:** amend `backend.md:61`. Instance-scoped modules use `/api/<module>/...`; organization-scoped resources nest under `/api/organizations/{organizationId}/...` per ADR-0027/0029.
- **Effort:** F
- **In flight:** mod-019 (ADR-0029 § 1 amends rule 3) defines the nested chain. Amend after it lands.
- **Review:** severity changed, minor → info (stale canon text; the code follows ADR-0027, same kind of drift as ERR-4) — see review.md.

#### ARC-9 · info · Slice drift vs the Dictionaries reference: two response-DTO schools and two file layouts
- **Evidence:**
  - Dictionaries: `src/backend/Mustask.Application/Features/Dictionaries/UpdateLanguage/LanguageResponse.cs:3-8`. Each slice has its own response copy and 4 files.
  - Organizations: `src/backend/Mustask.Application/Features/Organizations/CreateArea/CreateAreaHandler.cs:14-25`. 16 slices keep command, response, result and handler in one file, and `AreaResponse` is copied 4 times.
  - WorkItems: `src/backend/Mustask.Application/Features/WorkItems/GetWorkItem/GetWorkItemResult.cs:5`. Five slices return one shared `WorkItemView` from Abstractions.
  - Boards: `src/backend/Mustask.Application/Abstractions/Boards/AggregateViews.cs:67-111`. Wire DTOs and shared logic live in Abstractions.
  - Process and UserDirectory: endpoint-less in-process slices (`SeedProcessHandler`, `RecordSignInHandler`).
  - Session: Api-only (ADR-0022).
- **Rule:** `docs/conventions/backend.md:18-24, 192-198`.
- **Impact:** no defect. A new slice's shape depends on which module it was copied from.
- **Proposed fix:** record in `backend.md` when each school applies (a per-slice copy for small shapes, an Abstractions view for a wire shape shared by several routes) and whether a single-file slice is acceptable.
- **Effort:** F
- **In flight:** -

#### ARC-10 · info · Canon rules that no test enforces
- **Evidence:**
  - `src/backend/tests/Mustask.Architecture.Tests/VerticalSliceRules.cs:15-56` checks only `using` lines of `Mustask.Application`. It misses:
    - alias usings
    - fully-qualified names
    - module-root files
    - cross-slice use in Api and Infrastructure
  - "Writes only through the domain" is guarded for `work_items` and process states only (`WorkItemAccessRules.cs:91`, `ProcessStateWritesRules.cs:42`). `project_teams`, `area_teams` and `memberships` are unguarded.
  - `ProjectedUserModelTests.cs:68-69` asserts enum-as-string for one entity only.
  - No global `JsonStringEnumConverter` is configured; current wire DTOs map enums by hand.
- **Rule:** `docs/conventions/backend.md:44-57, 61, 80-83, 192-238, 253-264, 309-323`.
- **Impact:** these rules rely on review:
  1. domain rules in Domain (ARC-1)
  2. one use case per folder (ARC-7)
  3. no shared services, with module-root files invisible (ARC-2)
  4. writes through the domain outside work items and process (ARC-5)
  5. enums as strings (one accepted exception, ARC-3)
  6. `ProducesProblem` (ARC-6)
  7. routes (ARC-8)
  8. reachability in one place (ARC-4)
  9. time only through `IClock` (clean today)
  10. reads with `AsNoTracking` plus projection
  11. no `Constants.cs`
  12. Abstractions must not import Features
- **Proposed fix:** add cheap source and model tests for (3), (6), (8), (9) and (12), and for (5) once ARC-3 is settled. Leave (1), (2) and (10) to review.
- **Effort:** S
- **In flight:** tech-035 reworks the test kit. Agree with it where the new model tests live.

#### ARC-11 · minor · Implemented ADRs cited as canon are still `Status: proposed`; the session and realm security model runs on unaccepted decisions
- **Evidence:**
  - `docs/adr/0022-token-mediating-backend-for-the-panel.md:3`, `docs/adr/0033-shared-realm-client-roles.md:3`, `docs/adr/0021-localized-dictionaries-pattern.md:3`, `docs/adr/0034-user-projection-from-the-sign-in.md:3` and `docs/adr/0036-cursor-paged-group-reads-and-read-limiter.md:3`: each reads `Status: proposed`.
  - The code implements all five and this analysis, like the code comments, cites them as binding. SEC-1, SEC-2, TXN-3 and EFC-4 reason from their texts.
- **Rule:** `docs/adr/README.md` rule 2: only the human accepts an ADR.
- **Impact:** the session model (0022) and the realm role model (0033) run in production on texts the human never accepted. Their wording can still be fixed in place (SEC-1, SEC-2), but nobody has decided it. This is a governance gap, not a code defect.
- **Proposed fix:** a gate-1 question: accept or revise each of 0021, 0022, 0033, 0034 and 0036, after the SEC-1 and SEC-2 decisions, since both findings may amend 0022 or 0033. Carried by the canon-sync task (candidate #15).
- **Effort:** F
- **In flight:** -
- **Review:** added from review M-1.

### SEC — Security and authorization

#### SEC-1 · major · The bearer path accepts any realm token with `aud=mustask-api`: no `azp` check; whether `user` should gate bearer requests is a policy question
- **Evidence:**
  - `src/backend/Mustask.Api/Authentication/JwtBearerSetup.cs:50-82`: issuer, audience, lifetime and RS256 only. Nothing in `Mustask.Api` checks `azp`/`AuthorizedParty`.
  - `JwtBearerSetup.cs:92-100`: `OnTokenValidated` only appends `perm` claims.
  - `JwtBearerSetup.cs:110`: `AuthenticatedOnly` is `RequireAuthenticatedUser()` and nothing more.
  - `src/backend/Mustask.Api/Authorization/PermissionPolicies.cs:72-76`: `org:<code>` is authentication plus membership.
  - `src/backend/Mustask.Api/Authorization/OrganizationPermissionHandler.cs:39, 56`: checks `IsAuthenticated`, then reads the membership by `sub`.
  - The `user` gate exists only at panel sign-in and renewal (`SessionAuthenticationSetup.cs:185`, `SessionEndpoints.cs:205`).
  - `src/backend/Mustask.Api/Features/Organizations/OrganizationEndpoints.cs:87`: `GET /api/organizations` is AuthenticatedOnly.
  - ADR-0033 (`Status: proposed`, `:3`) reads two ways on the `user` half:
    - `docs/adr/0033-shared-realm-client-roles.md:57` ("without it there is no session and no right") and `:76-77` ("A bearer token without `user` authenticates and holds no right").
    - Rule 2 (`:70-72`) keeps organization rights ("ADR-0027's organization rights are untouched"). The most coherent reading of "holds no right" is no instance right, and the code matches rule 2.
  - `src/backend/tests/Mustask.Api.IntegrationTests/Features/Dictionaries/DictionariesByClientRoleTests.cs:67`: the only `user`-less bearer test covers instance codes only.
- **Rule:** ADR-0033 rules 1-3; ADR-0018 (resource server of the realm).
- **Impact:**
  - `azp` half (a defect): in the company-wide realm, the default audience mapper can put `aud=mustask-api` into any full-scope client's token for a user who holds a `mustask-api` role. A token issued to, leaked by or logged by another company application then reads and writes every organization the person belongs to.
  - `user` half (a policy question, not a defect): a person whose `user` role was revoked keeps organization access through a bearer token, which is what ADR-0033 rule 2 says today.
  - The realm's mappers are not visible from the repository.
- **Proposed fix:**
  - In `OnTokenValidated`, fail the token unless `azp` is in a configured allow-list. The list must admit `mustask-panel` and the stand-only `mustask-e2e` client (ADR-0023), or the E2E suite breaks.
  - Human decision: should a bearer token without `user` hold organization rights? If not, withhold every organization verdict when `resource_access.mustask-api.roles` lacks `user`, and amend ADR-0033 in place (it is still proposed). If yes, state that in rule 3.
  - Add integration tests for a foreign `azp`, for `mustask-e2e`, and (after the decision) for a member's token without `user`, against an organization route and against `GET /api/organizations`.
- **Effort:** S
- **In flight:** mod-019 rewrites `OrganizationPermissionHandler` and the `org:*` policies, so the user gate goes into that rewrite or just before it. The `azp` check sits in `JwtBearerSetup`, which mod-019 does not touch.
- **Review:** partly confirmed, stays major on the `azp` half — see review.md.

#### SEC-2 · major · The shared-origin deployment (ADR-0037) widens who can mint MUSTASK access tokens: any script on the portal origin can call `/api/session/token`
- **Evidence:**
  - `docs/adr/0022-token-mediating-backend-for-the-panel.md:48, 90`: XSS blast radius as the motivation; "the panel is a SPA on its own origin". ADR-0022 is still `Status: proposed` (`:3`). Its core protection, a refresh token never readable by script (`:47-51, 84-88`), is not affected.
  - `docs/adr/0037-serve-under-a-configured-path-base.md:5, 17, 51`: the target is `https://portal.devexpert.kz/mustask`, inside the portal's `server {}`, and "the web origin stays the bare `https://<host>`". There is no threat-model section.
  - `src/backend/Mustask.Api/Features/Session/SessionAuthenticationSetup.cs:107`: the cookie is scoped by `Path`, which does not isolate against same-origin `fetch`.
  - `src/backend/Mustask.Api/Features/Session/SessionEndpoints.cs:57-59`: `POST /api/session/token` is Public and authenticates by the cookie. `:253` returns the access token in the body.
  - `deploy/vm/nginx-mustask-locations.conf:1-5, 13`: MUSTASK is mounted as locations inside another server.
- **Rule:** ADR-0022 (token-mediating backend). ADR-0037:5 records that the human chose the sub-path over a subdomain, without a threat-model section.
- **Impact:** one XSS in any co-hosted portal application, or a compromised page, can `fetch('/mustask/api/session/token', {method:'POST', credentials:'include'})` from a signed-in user's browser and read short-lived access tokens for as long as the browser session lives. Neither SameSite=Lax nor CORS stops a same-origin request. What grows is the set of scripts able to mint access tokens, from MUSTASK's own code to every co-hosted application. The refresh token stays protected.
- **Proposed fix:** a human decision recorded in an ADR. Either:
  - give MUSTASK its own origin (a subdomain, as ADR-0022 assumes), or
  - record a risk acceptance for the shared origin, with compensating controls: an agreed CSP and XSS hygiene for every co-hosted application, short access-token lifetimes and a tight session lifetime.

  No server-side check on `/token` can tell the panel apart from other same-origin script.
- **Effort:** M
- **In flight:** -
- **Review:** partly confirmed, stays major; the "voids ADR-0022" framing was overstated — see review.md.

#### SEC-3 · minor · A `perm` claim already present in the access token is honoured as an instance right
- **Evidence:**
  - `src/backend/Mustask.Api/Authentication/KeycloakPermissionClaims.cs:14-17`: the docstring says rights from other sources are "closed by construction".
  - `KeycloakPermissionClaims.cs:30-50`: `Append` only adds claims and never removes the ones the token already carries.
  - `JwtBearerSetup.cs:38`: `MapInboundClaims = false`, so a top-level `perm` claim arrives unchanged.
  - `PermissionPolicies.cs:47`: instance policies use `RequireClaim("perm", code)`.
  - `KeycloakPermissionClaimsTests.cs:16-24`: no test starts with a pre-existing `perm` claim.
- **Rule:** ADR-0033 rule 2 (instance rights come only from the four client roles).
- **Impact:** a realm mapper administered by others that emits `perm` would grant instance codes directly, including `organizations.organization.rescue`. That is unlikely, but the impact is cross-organization.
- **Proposed fix:**
  - Strip existing `perm` claims before deriving new ones, or derive into a fresh identity.
  - Add a unit test with `perm=organizations.organization.rescue` and no roles that must end with no `perm`.
- **Effort:** F
- **In flight:** -

#### SEC-4 · minor · No request-body ceiling below Kestrel's 30 MB default; list-shaped inputs have no count cap
- **Evidence:**
  - `src/backend/Mustask.Api/Program.cs:40`: default host. No `MaxRequestBodySize`/`RequestSizeLimit` anywhere.
  - `src/backend/Mustask.Domain/WorkItems/WorkItemFieldRules.cs:108-133`: caps the length of each scenario but not their number.
  - `src/backend/Mustask.Api/Features/WorkItems/WorkItemEndpoints.cs:348`: `AcceptanceCriteria` in a PATCH that a `member` can send.
  - `OrganizationEndpoints.cs:758, 760, 778` and `AddTeamHandler.cs:113, 122, 147`: id lists with no cap.
  - `src/backend/Mustask.Domain/WorkItems/WorkItemFilter.cs:149-151`: repeated query values with no cap.
- **Rule:** canon "input validation at the boundary"; the criterion's input-size-limits item.
- **Impact:**
  - Any member can store tens of MB per work item per request, repeatedly, which bloats the DB and makes every later read of the card expensive.
  - Admin calls can push very large id sets into `FOR SHARE` locks and `ANY()` queries.
  - This is insider-only resource abuse, not a data leak.
- **Proposed fix:**
  - A global Kestrel `MaxRequestBodySize` of about 2 MB.
  - Domain count caps per scenario list, per id list and per repeated query parameter, each with its own error code in `contracts/errors`.
- **Effort:** S
- **In flight:** mod-019 touches the top of `AddTeamHandler`.

#### SEC-5 · info · The Data Protection key ring that encrypts the refresh token in the session cookie is not configured in code
- **Evidence:**
  - No `AddDataProtection`/`PersistKeysTo*`/`ProtectKeysWith*` in `src/backend` or `deploy/`.
  - `SessionEndpoints.cs:223-229`: the ticket carries the id and refresh tokens.
  - `deploy/vm/mustask-api.service:16, 25-26`: `HOME=/var/lib/mustask`, so the keys fall to the default `~/.aspnet/DataProtection-Keys`, unencrypted at rest.
  - `docs/adr/0022-token-mediating-backend-for-the-panel.md:269`: a second instance will need a shared key ring.
- **Rule:** ADR-0022 (ticket protection); secrets handling.
- **Impact:** it works on the single VM today. A container or second-instance deployment would silently lose or diverge the key ring, which signs everyone out or fails renewals.
- **Proposed fix:** `AddDataProtection().SetApplicationName("mustask").PersistKeysToFileSystem(<configured path>)`, with key protection where the platform offers it, and a fail-fast at start outside Development when the path is missing.
- **Effort:** S
- **In flight:** -

#### SEC-6 · info · `IOrganizationAccess` exposes the resolved role but not the organization it was resolved for
- **Evidence:**
  - `src/backend/Mustask.Api/Authorization/OrganizationPermissionHandler.cs:81`: the handler stores the organization id and the role.
  - `src/backend/Mustask.Api/Authorization/HttpContextOrganizationAccess.cs:16-22`: only `Role` and `Holds(code)` are exposed.
  - `src/backend/Mustask.Application/Features/Boards/GetViewGroups/GetViewGroupsHandler.cs:25, 32`: the handler re-parses `organizationId` and pairs it with `access.Role`. The same pattern appears in all ten `CallerAccess` constructions.
- **Rule:** ADR-0027 rule 3 (the role is the role in the organization of the address).
- **Impact:** correct today. A handler taking the organization from a body or query would apply one organization's role to another's data, and no test would notice.
- **Proposed fix:** expose `OrganizationId`, or a `CallerAccess` factory, on `IOrganizationAccess` and have handlers use it.
- **Effort:** S
- **In flight:** mod-019 replaces the resolved role with scoped effective permissions. Fold this into its design.

### TXN — Transactions and concurrency

#### TXN-1 · minor · `UnitOfWork` still carries the retry-era wrapper and a stale comment; ADR-0012's promised follow-up line was never filed
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/UnitOfWork.cs:17`: an execution-strategy wrapper around a strategy that cannot retry.
  - `UnitOfWork.cs:21-28`: a Russian comment that claims retries.
  - `UnitOfWork.cs:28`: `ChangeTracker.Clear()`, justified only by retries.
  - `src/backend/Mustask.Infrastructure/DependencyInjection.cs:55`: `DisableRetry = true`.
  - `src/backend/Mustask.Infrastructure/Dictionaries/Seeding/DictionarySeeder.cs:24-30`: the same pattern.
  - `docs/adr/0012-disable-ef-retry-on-failure.md:74-79`: says `tech-NNN-unitofwork-retry-comment` "has been filed". The queue has no such line.
- **Rule:** ADR-0012 Consequences; AGENTS.md language rule.
- **Impact:**
  - Readers are told retries exist.
  - `Clear()` does real work nobody wrote down. Lock-then-load (`OrganizationRepository.cs:7-10`, `WorkItemRepository.cs:12-18`) needs no entity tracked before the lock, otherwise the identity map returns a stale xmin.
  - Following the ADR's literal "remove the dead wrapper" would silently drop that guard, and no test pins it.
- **Proposed fix:**
  - Drop the wrapper and keep `Clear()`.
  - Rewrite the comment in English with its real reason.
  - Add a test pinning the clearing.
  - Mirror the change in `DictionarySeeder`.
  - File the missing line, or record it as covered by this task.
- **Effort:** S
- **In flight:** -

#### TXN-2 · minor · The unit of work's own unconditional flush bypasses the per-module constraint-to-error mapping
- **Evidence:**
  - `UnitOfWork.cs:33-35`: a raw `SaveChangesAsync` before commit.
  - Module mappings exist only on each repository's own `SaveAsync`:
    - `src/backend/Mustask.Infrastructure/Organizations/OrganizationsSaveChanges.cs:22-87`
    - `src/backend/Mustask.Infrastructure/WorkItems/WorkItemRepository.cs:195-207`
    - `src/backend/Mustask.Infrastructure/Dictionaries/LanguageRepository.cs:57-75`
  - `src/backend/Mustask.Application/Features/UserDirectory/RecordSignIn/RecordSignInHandler.cs:42` relies on the implicit flush. That is safe today because of the advisory lock (`UserDirectoryLocks.cs:22-39`).
- **Rule:** ADR-0011 echelon 1; ADR-0010 Consequences.
- **Impact:** a slice that forgets the repository `SaveAsync` still persists, but a lost uniqueness race then answers 500 instead of the registry 409. `FakeUnitOfWork` does not distinguish the two flush points, and the pattern is easy to copy from `RecordSignIn`.
- **Proposed fix:** either assert `!ChangeTracker.HasChanges()` before the final flush in Debug and tests, with an allow-list, or route the final flush through one composed mapper. State the rule under "EF Core" in `backend.md`.
- **Effort:** S
- **In flight:** -

#### TXN-3 · minor · The Languages reference slice exposes raw xmin as the public If-Match version, against ADR-0010
- **Evidence:**
  - `docs/adr/0010-optimistic-concurrency-via-xmin-convention.md:82-85`: "xmin is never exposed externally".
  - `src/backend/Mustask.Infrastructure/Dictionaries/ConcurrencyVersion.cs:6-19`: xmin rendered as the API version, with a comment misreading the ADR.
  - `src/backend/Mustask.Application/Features/Dictionaries/UpdateLanguage/UpdateLanguageHandler.cs:62`: compares If-Match against xmin.
  - `src/backend/Mustask.Api/Features/Dictionaries/IfMatchHeader.cs:6-9` and `DictionaryEndpoints.cs:103`: the If-Match plumbing.
- **Rule:** ADR-0010 Consequences.
- **Impact:** the next slice that needs If-Match will copy xmin-as-ETag from the reference slice. The mechanism itself works today: the version is compared, and a race maps to `dictionaries.version_conflict`.
- **Proposed fix:** amend ADR-0010, or add an ADR accepting the exception and saying whether new slices may copy it, or move Languages to an explicit version column. Fix the comment in either case.
- **Effort:** S
- **In flight:** -

#### TXN-4 · info · No model-wide test asserts the xmin token on every table entity of the real `MustaskDbContext`
- **Evidence:**
  - `src/backend/tests/Mustask.Infrastructure.Tests/Conventions/OptimisticConcurrencyConventionTests.cs:17`: tests a convention-only context.
  - `ProjectedUserModelTests.cs:40`: checks one real entity.
  - `CodeUniqueIndexModelTests.cs:26`: the precedent of a real-model walk.
  - `src/backend/Mustask.Infrastructure/MustaskDbContext.cs:77`: the registration that nothing pins.
- **Rule:** ADR-0010 ("impossible to forget", pinned by tests).
- **Impact:** dropping the registration would remove lost-update protection everywhere. `ConcurrencyVersion.Read` reads the real model's `xmin` property (`ConcurrencyVersion.cs:18-19`), so the Languages If-Match endpoint tests (`UpdateLanguageEndpointTests`, `DictionaryContractRulesEndpointTests`) would also fail. The gap is coverage of the other entities.
- **Proposed fix:** an `XminModelTests` walking the real model's table-mapped entities.
- **Effort:** F
- **In flight:** tech-035 may host it.
- **Review:** partly confirmed, "only one test would notice" was understated — see review.md.

#### TXN-5 · info · The area-link insert relies on in-memory order and unnest emission order; project links state ORDER BY in SQL
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/Organizations/AreaLinkRepository.cs:22`: sorted in C#.
  - `AreaLinkRepository.cs:26-33`: `INSERT ... SELECT FROM unnest(...)` with no `ORDER BY`.
  - `src/backend/Mustask.Infrastructure/Organizations/ProjectTeamRepository.cs:97-105`: `ORDER BY pair.project_id, pair.team_id`.
- **Rule:** ADR-0011, ADR-0028 rule 8 (multi-row locks in a fixed order stated in the statement).
- **Impact:** overlapping link requests hold only `FOR SHARE`, so what keeps them from deadlocking (a 40P01 answered as 500) is the order of their unique-key waits. That order is correct in practice but rests on executor behaviour SQL does not guarantee.
- **Proposed fix:** `ORDER BY p.area_id, p.team_id`.
- **Effort:** F
- **In flight:** - (same statement as ARC-5)

### EFC — EF Core query efficiency

#### EFC-1 · minor · Rescue organizations page: int overflow gives a negative OFFSET (likely a 500), and there is no short-circuit past the end
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/Organizations/AppointmentReadModel.cs:38`: `.Skip((page - 1) * pageSize)` computed in `int`.
  - `AppointmentReadModel.cs:33-35`: the count is read, but the page statement runs even when skip >= count.
  - `src/backend/Mustask.Application/Features/Organizations/GetRescueOrganizations/GetRescueOrganizationsHandler.cs:18-30`: accepts any int >= 1, with a page size of 25.
  - The reference pattern: `src/backend/Mustask.Infrastructure/WorkItems/WorkItemReadModel.cs:117-123` (a `long` skip and an empty answer past the end), also `AggregateViewReadModel.cs:124-129`.
- **Rule:** ADR-0014/0015 (an explicit answer instead of different behaviour); consistency with the other paged readers.
- **Impact:** a page >= 85 899 347 overflows, so PostgreSQL refuses the negative OFFSET and the user gets an unhandled 500 instead of an empty page. A deep page also runs a useless OFFSET scan. The route is instance-administrator only. The 500 is inferred, not executed.
- **Proposed fix:** copy the `WorkItemReadModel` pattern and add a far-page test.
- **Effort:** F
- **In flight:** -

#### EFC-2 · info · `FindProjectAsync` takes two round trips and three unused default-team subqueries on every project-scoped read
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/Organizations/OrganizationStructure.cs:30-70`, with the default-team subqueries at `:47-64`.
  - `OrganizationStructure.cs:77-78`: a second statement only for organization status.
  - `OrganizationStructure.cs:205-206`: the one-statement pattern already used for teams.
  - `src/backend/Mustask.Domain/Organizations/ReachabilityRules.cs:29-30`: the in-memory `IsReachable` already exists.
  - Callers on the hot path:
    - `GetWorkItemsHandler.cs:33`
    - `GetWorkItemHandler.cs:30`
    - `WorkItemAddress.cs:34`
    - `GetProjectBacklogHandler.cs:36`
    - `GetProjectBoardHandler.cs:36`
    - `GetAssigneeOptionsHandler.cs:29`
- **Rule:** `backend.md` EF Core (a fixed statement count and only the needed columns); ADR-0026 rule 3.
- **Impact:** every project-scoped request pays an extra round trip and three ignored subqueries. It is a primary-key lookup with small correlated subqueries over `project_teams`, and `IOrganizationStructure.cs:76-79` says the default team is resolved "in the statement that reads the project". No measured cost and no correctness issue: an optimisation note.
- **Proposed fix:**
  - Fold organization status into the first projection and compute reachability with `ReachabilityRules.IsReachable`.
  - Optionally offer a lighter by-id read without the default-team subqueries for read-only handlers.
- **Effort:** S
- **In flight:** mod-019 (US-3) may change how handlers resolve the addressed project. Related to ARC-4.
- **Review:** severity changed, minor → info — see review.md.

#### EFC-3 · minor · Redundant `work_items (project_id, type_id, area_id)` index left over after the area condition was dropped
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/WorkItems/Configurations/WorkItemConfiguration.cs:136-138`: the index, with a stale comment.
  - `src/backend/Mustask.Infrastructure/Boards/ProjectBacklogMembership.cs:13, 115`: claims the index serves it, and states "no area condition".
  - `src/backend/Mustask.Infrastructure/WorkItems/WorkItemAreaUsage.cs:14`: the only area-filtered query, served by another index (`WorkItemConfiguration.cs:135`).
  - `WorkItemConfiguration.cs:150-151` and migration `20261103095819_AggregateViewIndexes.cs:37-41`: an index covering the same (project, type) prefix.
  - `src/backend/Mustask.Infrastructure/Migrations/MustaskDbContextModelSnapshot.cs:1474-1534`: 15 secondary indexes on `work_items`.
- **Rule:** `backend.md` EF Core and ADR-0009 (index what reads use; write cost).
- **Impact:** every insert and save maintains an index no query needs, on the hottest table. The stale comments mislead the next slice about which index serves the backlog.
- **Proposed fix:** confirm the plans with EXPLAIN in the feat-048 volume test and in `ProjectBacklogStatementCountTests`, then drop the index in a new migration and fix the two comments.
- **Effort:** S
- **In flight:** -

#### EFC-4 · info · Whole-set reads with no server-side ceiling (by design, not recorded as a limit)
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/Boards/BacklogReadModel.cs:16-28, 242`: "up to 20 000 rows".
  - `GetProjectBacklogHandler.cs:59`
  - `MemberReadModel.cs:11-26`
  - `AppointmentReadModel.cs:58-70`
  - `ProjectReadModel.cs:183-189`
  - `TeamReadModel.cs:145-184`
- **Rule:** ADR-0015 covers registry lists only; ADR-0009 says to revisit when bulk reads grow.
- **Impact:** response size grows with the data. The statement count stays fixed, so this is a size risk, not N+1.
- **Proposed fix:** when a spec is next touched, record a ceiling or adopt the ADR-0036 cursor, with a volume test.
- **Effort:** M
- **In flight:** -

#### EFC-5 · info · Substring searches have no trigram index
- **Evidence:**
  - `src/backend/Mustask.Infrastructure/WorkItems/WorkItemFilterPredicate.cs:164-178`
  - `WorkItemReadModel.cs:77-79`
  - `src/backend/Mustask.Infrastructure/UserDirectory/UserDirectoryReadModel.cs:159-162`
  - `AppointmentReadModel.cs:28-30`
- **Rule:** ADR-0036 decision 1 and feat-048 OQ-2 (an accepted risk); ADR-0009 (measure first).
- **Impact:** inside one project the scan is bounded by the project index. The organization-wide keyword read and the directory search scan every candidate row.
- **Proposed fix:** none now. If profiling shows it, add `pg_trgm` GIN indexes through an ADR.
- **Effort:** M
- **In flight:** -

#### EFC-6 · info · Read rules are enforced only by per-slice statement counters, each a private copy
- **Evidence:**
  - `src/backend/tests/Mustask.Infrastructure.Tests/Boards/ProjectBacklogStatementCountTests.cs:194-221`
  - Identical copies in:
    - `OwningTeamStatementCountTests.cs:384`
    - `FilterStatementCountTests.cs:215`
    - `DeletionStatementCountTests.cs:223`
    - `AggregateViewStatementCountTests.cs:217`
    - `TeamViewStatementCountTests.cs:247`
  - Nothing checks for `AsEnumerable` bridges or entity-returning readers.
- **Rule:** `backend.md` EF Core (`AsNoTracking` plus projection; filter before materialization).
- **Impact:** no defect. A slice that skips its counter test can add a per-row cascade unnoticed, and the six copies will drift.
- **Proposed fix:** move `StatementCounter` into the shared test kit. Optionally add a text rule against `AsEnumerable()`/`ToList()` followed by `Where`.
- **Effort:** S
- **In flight:** tech-035 (the counter should land in its kit).

### ERR — Error handling and the error contract

#### ERR-1 · minor · A 5xx business exception is logged at Warning, so a real server failure never reaches error alerting
- **Evidence:**
  - `src/backend/Mustask.Api/ErrorHandling/BusinessExceptionHandler.cs:96`: unconditional `LogWarning`.
  - `BusinessExceptionHandler.cs:105`: the status comes from `ex.StatusCode`.
  - `src/backend/Mustask.Shared/Exceptions/MustaskException.cs:54`: accepts 400..599.
  - `src/backend/Mustask.Application/Features/Organizations/CreateOrganization/OrganizationCreateFailedException.cs:14-15`: a 500.
  - `CreateOrganizationHandler.cs:102-112`: wraps every seed failure in it.
  - `GlobalExceptionHandler.cs:49`: an unwrapped fault would be logged at Error.
- **Rule:** ADR-0004 (business exceptions are expected outcomes; a server fault is an error).
- **Impact:** a failed organization seed is missed by level-based alerting.
- **Proposed fix:** log at Error with the exception when the status is 500 or above, and pin the level with a unit test.
- **Effort:** F
- **In flight:** -

#### ERR-2 · info · ADR-0005 throw-site rules are not enforced by tests: the scanner reflects over constants only, comments are stale, and the planned enforcement item does not exist
- **Evidence:**
  - `src/backend/tests/Mustask.Contracts.Tests/AppErrorCodeScanner.cs:17-52`: reflects over constants only. Literal codes and status disagreement are undetected.
  - `CatalogFixture.cs:27-29`: the Infrastructure assembly is excluded deliberately and the exclusion is documented. No `[ErrorCodes]` class lives in Infrastructure (all 6 are in Domain or Api), and its throw sites use Domain constants that are already scanned, so adding the assembly would change nothing.
  - Stale comments: `CatalogFixture.cs:39` ("today zero"), `AppErrorCodeScanner.cs:12-15` ("today empty") and `RulesAreNotVacuous.cs:7-10`.
  - `docs/conventions/backend.md:127-134` refers to a backlog item that does not exist; the canon prescribes the by-hand attribute "until then".
  - Statuses are pinned by hand per module: `WorkItemErrorCatalogTests.cs:69`, `BoardErrorCatalogTests.cs:39`, `OrganizationErrorCatalogTests.cs:79`.
- **Rule:** ADR-0005 rules 5-6.
- **Impact:** clean today (102 constants, all referenced, no literals). A regression would pass CI and reach the wire and the generated `docs/error-codes.md`.
- **Proposed fix:**
  - Add an arch test or analyzer rejecting literal codes in `MustaskException` constructors.
  - Optionally check statuses per exception type.
  - Refresh the comments and queue or close the canon's item.
- **Effort:** S
- **In flight:** tech-035 may host a shared error-assertion helper.
- **Review:** partly confirmed, minor → info; the Infrastructure claim was refuted — see review.md.

#### ERR-3 · info · Typed query parameters turn a malformed value into a generic `http.bad_request`, while an out-of-range value gets a module field code
- **Evidence:**
  - Framework-bound typed parameters:
    - `src/backend/Mustask.Api/Features/WorkItems/WorkItemEndpoints.cs:159` (`int? page`) and `:298` (`bool? levelAbove`)
    - `src/backend/Mustask.Api/Features/Boards/AggregateViewEndpoints.cs:186`
    - `src/backend/Mustask.Api/Features/Dictionaries/DictionaryEndpoints.cs:52`
  - `src/backend/Mustask.Application/Features/WorkItems/GetWorkItems/GetWorkItemsHandler.cs:38-45`: `page < 1` gives `workitems.invalid_page` in the field shape.
  - `src/backend/Mustask.Api/ErrorHandling/MalformedRequestExceptionHandler.cs:63, 85-90`: a binding failure becomes a bare status with `http.bad_request`.
  - The consistent pattern: `src/backend/Mustask.Application/Features/Organizations/GetRescueOrganizations/GetRescueOrganizationsHandler.cs:20-26`.
- **Rule:** ADR-0004:111-128 (accepted) explicitly records that typed-parameter binding failures such as `?pageSize=abc` go through `MalformedRequestExceptionHandler` to their own status with the platform code. That is the canon-sanctioned answer, not a contract breach. ADR-0005 (validation answers a module code in the field shape) covers the out-of-range case.
- **Impact:**
  - `?page=0` and `?page=abc` get different bodies, and the frontend cannot point at the field for the second. This is a sanctioned difference.
  - No order of checks leaks: the org-scoped check runs in authorization before binding (`OrganizationPermissionHandler.cs:26-28`), so a foreign organization still gets 404 first, and for a project-level route the binding 400 is the same bytes whether or not the project exists.
  - What remains is a docstring mismatch: `AggregateViewEndpoints.cs:186` binds `int? page` against its own docstring at `:23-24` ("values reach the handler as the strings they are").
- **Proposed fix:** bring `AggregateViewEndpoints.cs:186` in line with its docstring (bind as `string?`), or fix the docstring. Optionally bind the other pages as `string?` and parse them in the handler with the module's field code.
- **Effort:** S
- **In flight:** -
- **Review:** partly confirmed, minor → info; ADR-0004 sanctions the behaviour and the order-leak claim was wrong — see review.md.

#### ERR-4 · info · Canon drift: `backend.md` describes three exception handlers, the code registers four
- **Evidence:**
  - `docs/conventions/backend.md:62-66`
  - `src/backend/Mustask.Api/ErrorHandling/ErrorHandlingExtensions.cs:115-118`: cancel, business, malformed, global.
- **Rule:** the canon must describe the actual pipeline.
- **Impact:** a reader misses the handler that shapes every binding-failure 400.
- **Proposed fix:** list four handlers and say what the malformed one maps.
- **Effort:** F
- **In flight:** -

#### ERR-5 · info · Bad query parameters get two body shapes across modules
- **Evidence:**
  - `src/backend/Mustask.Domain/Dictionaries/DictionaryQueryException.cs:17`: a top-level code with no field map.
  - `src/backend/Mustask.Application/Abstractions/Boards/AggregateParameters.cs:62, 99, 115, 157-158`: the field shape.
  - `GetWorkItemsHandler.cs:38-45`: the field shape.
- **Rule:** ADR-0004/0005.
- **Impact:** two frontend code paths. The Dictionaries shape predates the convention and is catalogued.
- **Proposed fix:** when Dictionaries is next touched, move it to the field shape and deprecate the old code per ADR-0005.
- **Effort:** S
- **In flight:** -

#### ERR-6 · info · Instance-permission routes without `WithForbiddenErrorCode` fall back to the generic `http.forbidden`
- **Evidence:**
  - `src/backend/Mustask.Api/Authorization/ForbiddenErrorCodeResultHandler.cs:32-43, 46-52`
  - `WithForbiddenErrorCode` is used only in `RescueEndpoints.cs:32, 40, 48, 58` and `OrganizationEndpoints.cs:98`. Rescue and organization create both carry a code, so the only instance-permission routes without one are the four Dictionaries routes (`DictionaryEndpoints.cs:25-35`).
- **Rule:** ADR-0005 (a module code the frontend can explain); ADR-0027 order.
- **Impact:** a user without a Dictionaries instance right sees the generic text. The organization-scoped path is correct.
- **Proposed fix:** give the four Dictionaries routes a forbidden code, or document `http.forbidden` as the intended answer.
- **Effort:** S
- **In flight:** none. mod-019 (ADR-0029) concerns organization-scoped rights, not the Dictionaries instance policy.
- **Review:** partly confirmed, reach smaller than stated; the "fold into mod-019" suggestion has no support — see review.md.

### ASY — Async and cancellation

#### ASY-1 · info · Forwarding the `CancellationToken` is a convention only: no analyzer, canon rule or test enforces it
- **Evidence:**
  - `Directory.Build.props:7-8`: `TreatWarningsAsErrors` and `AnalysisLevel=latest`, but no `AnalysisMode`, so CA2016 stays a suggestion.
  - `.editorconfig:14-22`: no `dotnet_diagnostic.CA2016` line.
  - `docs/conventions/backend.md:65`: no token rule.
  - `src/backend/tests/Mustask.Architecture.Tests/WorkItemAccessRules.cs:162`: the only `cancellationToken` text in the architecture tests.
  - `src/backend/Mustask.Infrastructure/Organizations/AreaRepository.cs:115`: EF overloads take an optional token.
- **Rule:** generic practice (CA2016). There is no canon rule.
- **Impact:** clean today. A dropped token on a `ToListAsync` or `SendAsync` compiles, so a disconnected client's query or outbound call would run to completion.
- **Proposed fix:**
  - `dotnet_diagnostic.CA2016.severity = warning` in `.editorconfig`.
  - A `backend.md` § API line: endpoints take a token and pass it down; abstractions take a required token; framework overrides are exempt.
  - Under `TreatWarningsAsErrors`, CA2016 as a warning turns any later miss into a build break, which is the intent. Check the solution builds first.
- **Effort:** F
- **In flight:** -
- **Review:** severity changed, minor → info (clean today; a missing guardrail) — see review.md.

#### ASY-2 · minor · The OIDC code-exchange backchannel is left at the framework default: a 60 s timeout and a plain `HttpClient` outside `IHttpClientFactory`
- **Evidence:**
  - `src/backend/Mustask.Api/Features/Session/SessionAuthenticationSetup.cs:127, 133-146`: no `Backchannel`/`BackchannelTimeout`.
  - `SessionAuthenticationSetup.cs:88-89`: the refresh and logout calls use a factory client with a 5 s timeout.
  - `src/backend/Mustask.Api/Authentication/Readiness/RealmMetadata.cs:19`: discovery uses 5 s.
- **Rule:** the project's own pattern: factory clients with explicit timeouts for every Keycloak call.
- **Impact:**
  - A stalled Keycloak holds the sign-in callback for up to 60 s, while renewal against the same endpoint gives up after 5 s.
  - The client gets no handler rotation and no ServiceDefaults resilience or telemetry.
- **Proposed fix:** `options.Backchannel = httpClientFactory.CreateClient(RefreshGrantClient.HttpClientName)`, or at least `BackchannelTimeout = 5 s`.
- **Effort:** F
- **In flight:** -

#### ASY-3 · info · Session endpoints treat a client abort as "Keycloak unreachable": a 503, and on `/logout` a misleading warning, instead of the cancel path
- **Evidence:**
  - `src/backend/Mustask.Api/Features/Session/SessionEndpoints.cs:193, 298`: both filters catch `TaskCanceledException` without checking `RequestAborted`. The `/token` branch (`:193-201`) logs nothing and only sets 503; only `/logout` writes the warning (`:300-301`).
  - `src/backend/Mustask.Api/ErrorHandling/OperationCanceledExceptionHandler.cs:33-45`
  - `docs/conventions/backend.md:65`
- **Rule:** `backend.md` § API, the cancel handler chain (ADR-0004).
- **Impact:** a tab closed during `/logout` writes one warning that reads like a Keycloak outage, which pollutes the signal operators use to tell an outage from a misconfiguration (`RefreshGrantClient.cs:186-198`). `/token` only returns 503 to a client that is gone. There is no user-visible effect.
- **Proposed fix:** add `&& !cancellationToken.IsCancellationRequested` to both filters.
- **Effort:** F
- **In flight:** -
- **Review:** partly confirmed, minor → info; only `/logout` logs — see review.md.

#### ASY-4 · minor · The standard resilience handler can retry the non-idempotent `refresh_token` POST, which under reuse detection ends the user's session
- **Evidence:**
  - `src/backend/Mustask.ServiceDefaults/Extensions.cs:33-36`: `ConfigureHttpClientDefaults` adds `AddStandardResilienceHandler` to every client, including `keycloak-token`. Its default retry does not exclude POST.
  - `src/backend/Mustask.Api/Features/Session/RefreshGrantClient.cs:55-71`: the refresh POST.
  - `src/backend/Mustask.Api/Program.cs:62`
  - `SessionAuthenticationSetup.cs:88-89`
  - `RealmMetadata.cs:31-32`
  - `deploy/keycloak/realms/mustask-realm.json:28-29`
- **Rule:** generic: one explicit policy per outbound dependency; ADR-0022 D-14 (`:60-71`, refresh-token rotation and reuse detection).
- **Impact:**
  - Take a `refresh_token` POST that Keycloak committed but whose answer came back as a 5xx or a reset. The retry presents the same, already rotated refresh token, and reuse detection then ends the whole client session (D-14; `docs/adr/0022-token-mediating-backend-for-the-panel.md:52-71`). A transient failure becomes a forced sign-out. The earlier conclusion "harmless against rotation" is not supported.
  - The handler's 10 s and 30 s timeouts never fire inside the 5 s `HttpClient.Timeout`.
  - Neither `RefreshGrantClient.cs:42-54` nor `RealmMetadata` documents the retries.
- **Proposed fix:** disable retries explicitly on the refresh POST (a per-client resilience pipeline without retry), and add a test that a 5xx on refresh is not retried. Configure the pipeline per Keycloak client and drop `HttpClient.Timeout`, or remove the handler for these clients. State the chosen policy in the `RefreshGrantClient` remarks.
- **Effort:** S
- **In flight:** -
- **Review:** severity changed, info → minor; the refresh-retry hazard was added from review M-3 — see review.md.

#### ASY-5 · info · Dev-only AppHost: `CancellationToken.None` on stand reset and a fresh `HttpClient` per drift probe
- **Evidence:**
  - `src/backend/Mustask.AppHost/AppHost.cs:404, 512`
  - `src/backend/Mustask.AppHost/Stand/KeycloakDriftHealthCheck.cs:70-72`
  - `StandReset.cs:195`
- **Rule:** generic.
- **Impact:** local only. A stuck docker call during `--reset` can only be stopped by killing the process.
- **Proposed fix:** optional. Wire `Console.CancelKeyPress` into the reset and set a short provisioning timeout.
- **Effort:** F
- **In flight:** -

#### ASY-6 · info · Minor async style inconsistencies
- **Evidence:**
  - `SessionEndpoints.cs:68`: a synchronous `LoginAsync`.
  - `src/backend/Mustask.Infrastructure/Dictionaries/GridifyValidation.cs:117`: the lone `ConfigureAwait(false)` in production code.
- **Rule:** generic naming and consistency.
- **Impact:** cosmetic.
- **Proposed fix:** rename and drop it when the files are next touched.
- **Effort:** F
- **In flight:** -

### DEP — Dependency hygiene

#### DEP-1 · info · One central Npgsql version serves two majors, and `Logging.Abstractions` sits below its family
- **Evidence:**
  - `Directory.Packages.props:44-46`: Npgsql 8.0.3, for the AppHost stand reset only.
  - `Directory.Packages.props:42`: the EF provider 10.0.3 pulls Npgsql 10.0.3.
  - `src/backend/Mustask.AppHost/Mustask.AppHost.csproj:16` and `Stand/StandReset.cs:3`: the consumer.
  - Infrastructure uses Npgsql types transitively (`MigrationsGuard.cs:2`, `BacklogRepository.cs:8`, `ProjectBacklogRepository.cs:8`). An explicit reference would fail restore with NU1605.
  - `Directory.Packages.props:72`: `Logging.Abstractions` 10.0.0, against 10.0.11 for the rest of the family.
  - `Directory.Packages.props:24-27`: these two entries block transitive pinning (NU1109).
- **Rule:** central package management, with one release line per family.
- **Impact:** no runtime defect. Both entries are deliberate and documented: Npgsql 8.0.3 is pinned to what `Aspire.Hosting.PostgreSQL` resolves in AppHost (`:44-46`), and the NU1109 trade-off is written at `:24-27`. The trap is loud, not silent: a downgrade fails restore with NU1605. `Logging.Abstractions` 10.0.0 is referenced only by Application (`Mustask.Application.csproj:11`), and the shared framework resolves it upward at runtime. The entries do block the tool that would have handled the tech-040 advisory without an override.
- **Proposed fix:** optional.
  - Bump `Logging.Abstractions` to 10.0.11 and Npgsql to 10.0.3. `HealthChecks.NpgSql` 9.0.0 only needs >= 8.0.3. The Npgsql bump changes the AppHost stand-reset runtime.
  - Check the AppHost health check and the stand reset, then retry transitive pinning.
  - Otherwise use a commented `VersionOverride` in AppHost.
- **Effort:** S
- **In flight:** -
- **Review:** severity changed, minor → info — see review.md.

#### DEP-2 · minor · Two central `PackageVersion` entries have no consumer
- **Evidence:**
  - `Directory.Packages.props:71`: `Microsoft.Extensions.Identity.Core`, left over from the bootstrap.
  - `Directory.Packages.props:12`: `Aspire.Hosting.Testing`, only planned by ADR-0001 (`:30, :57`).
- **Rule:** the catalog lists what the build uses.
- **Impact:** both are bumped and reviewed for nothing. Identity.Core also suggests to readers and to agents that ASP.NET Identity is part of the stack.
- **Proposed fix:** delete both, and re-add Testing with its first test.
- **Effort:** F
- **In flight:** -

#### DEP-3 · info · `.editorconfig` style rules marked as warnings are not enforced by the build or CI
- **Evidence:**
  - `.editorconfig:17, 22, 25`
  - `Directory.Build.props:5-8`: no `EnforceCodeStyleInBuild`.
  - `.github/workflows/ci.yml:89-96` and `scripts/verify.sh:826`: no `dotnet format --verify-no-changes`.
- **Rule:** `docs/conventions/backend.md:344` together with the repo's own severities.
- **Impact:** the rules hold only by discipline. "warning" plus warnings-as-errors reads as a gate that does not exist. No current defect: outside `Migrations/*.cs`, no file breaks `file_scoped_namespaces` today.
- **Proposed fix:** set `EnforceCodeStyleInBuild=true`, mark `Mustask.Infrastructure/Migrations/*.cs` as `generated_code = true`, or add `dotnet format` to `verify.sh`. Raising IDE rules under `TreatWarningsAsErrors` can break the build across the solution, so check the fan-out first.
- **Effort:** S
- **In flight:** -
- **Review:** severity changed, minor → info (a guardrail gap with no current defect) — see review.md.

#### DEP-4 · minor · Build and project files carry Russian comments
- **Evidence:**
  - `Directory.Packages.props:48, 56-57, 60-65`
  - `Directory.Build.props:20-24`
  - `src/backend/Mustask.Api/Mustask.Api.csproj:7-53`
  - `Mustask.Application.csproj:8-14`
  - `Mustask.Infrastructure.csproj:5-7, 25-26`
  - `Mustask.Api.IntegrationTests.csproj:44-46`
  - `Mustask.Architecture.Tests.csproj:24-29`
  - `Mustask.Contracts.Tests.csproj:28-32`
- **Rule:** AGENTS.md language rule.
- **Impact:** the rationale for a package, the text most read during an upgrade, is half in another language. The same pattern exists in `.cs` files, which is outside this criterion.
- **Proposed fix:** translate the comments in one sweep, wording only.
- **Effort:** S
- **In flight:** -

#### DEP-5 · info · Dependency rules cover five of seven production projects; package hygiene is not guarded
- **Evidence:**
  - `src/backend/tests/Mustask.Architecture.Tests/OnionDependencyRules.cs:13-20`: no rows for ServiceDefaults and AppHost.
  - `OnionDependencyRules.cs:36-47, 53-77`: inner layers only.
  - `RepositoryLayout.cs:46-56`: reads only `Include`. Nothing checks:
    - test packages in production projects
    - `VersionOverride`
    - unused `PackageVersion` entries
- **Rule:** `docs/conventions/backend.md:38-44`.
- **Impact:** all of these hold today. The unchecked projects are the composition ones, which are the most likely to accrete references. A consumer check would have caught DEP-2.
- **Proposed fix:**
  - Add rows for ServiceDefaults (no references) and AppHost (`Mustask.Api`).
  - A theory over production csproj files: no test framework package, no `VersionOverride`.
  - A fact that every `PackageVersion` has a consumer.
- **Effort:** S
- **In flight:** tech-035 reworks the kit. Check overlap.
- **Review:** severity changed, minor → info (a missing guardrail; all of it holds today) — see review.md.

#### DEP-6 · info · The test framework is on the xunit v2 line; the Aspire version is written in two places
- **Evidence:**
  - `Directory.Packages.props:91-94`
  - `src/backend/Mustask.AppHost/Mustask.AppHost.csproj:1` against `Directory.Packages.props:9-12, 43`
- **Rule:** version currency; a single source of version.
- **Impact:** none. xunit v2 gets maintenance fixes only. An Aspire mismatch would show at restore.
- **Proposed fix:** leave xunit to tech-035. Optionally move the Aspire SDK version to `global.json` `msbuild-sdks`.
- **Effort:** F
- **In flight:** tech-035 owns the test SDK line.

## Checked and clean

**ARC**
- Onion project references match the canon and are enforced (`OnionDependencyRules.cs:13-34`).
- Domain imports no outer layer and no `Microsoft.*`.
- Shared holds only Errors and Exceptions.
- No slice imports another slice namespace. Infrastructure imports no slice namespace.
- Api touches Infrastructure only in `Program.cs:19`. Endpoints are thin.
- ADR-0002:
  - no `Migrate`/`EnsureCreated` in production code
  - every migration has a `Down`
  - rollback to zero is tested (`MigrationsTests.cs:60`)
- ADR-0026: no query filters; statuses are stored as text; `Reachability` is used by the M1 read models.
- ADR-0021: Languages is the only dictionary, with the seed, the convention and its tests.
- `SystemClock.cs:7` is the only `UtcNow`.
- None of these exist: lazy proxies, AutoMapper, `*Raw` SQL, DbContext pooling, retry-on-failure.
- No `Constants.cs`; error-code classes sit next to their modules.
- The Session and UserDirectory boundaries are test-guarded.

**SEC**
- Every endpoint has exactly one category over a deny-all fallback (`JwtBearerSetup.cs:115-118`), with an inventory test.
- Every `{organizationId}` route declares an organization code. The two rescue exceptions are tested.
- The organization check runs before the body is read, with a uniform 404 (`OrganizationAccessReadModel.cs:14-24`).
- Nested ids are resolved outside-in with the organization inside the lookup (`OrganizationStructure.cs:34, 95, 106`).
- Body ids are kept inside their project or organization, and `OrganizationContainmentTests` covers every addressed route.
- Raw SQL is parameterized.
- JWT checks: issuer, audience, lifetime, RS256 only, strict `kid`.
- Session cookie and OIDC:
  - the cookie is HttpOnly, Secure and Lax, with no sliding renewal
  - PKCE and `form_post`
  - a strict return-URL validator
  - back-channel logout
- No committed production secret.
- CORS is Development-only.
- 500 bodies hide details.
- No token is logged.
- The E2E client is AppHost-only.
- There is no over-posting.
- `/health` is not proxied.

**TXN**
- `DisableRetry` is set for the whole context.
- `UnitOfWork` opens READ COMMITTED explicitly.
- Every locking write handler runs in a transaction. `AddMember` relies on the PK plus a mapped 23505.
- Lock-then-load is applied consistently.
- Lock statements return ids only.
- Multi-row locks are ordered in one statement. The DeleteTeam and UnlinkTeam orders follow rule 8.
- On-demand lock rows use `ON CONFLICT DO NOTHING` then `FOR UPDATE`.
- Backlog reorders serialize on the order row.
- Every uniqueness pre-check has an index mapped to the same 409.
- `CreateWorkItem` replays idempotently after the counter lock.
- The last-administrator invariant is serialized on the organization row.
- There is no external I/O inside a transaction (ADR-0013).
- `ProcessSeedStore` uses a savepoint.

**EFC**
- No `Include`; the one nested collection projection is a single statement.
- No `AsEnumerable` bridges before SQL operators.
- Read models use `AsNoTracking` with projections.
- Work-item paging counts and pages on one `IQueryable`, with a `long` skip and a short-circuit past the end. Languages follows ADR-0015.
- ADR-0036 cursor reads use a keyset and LATERAL, with `limit+1` and one dress-up statement.
- No N+1: directory lookups are batched, link reads are one UNION, flags are EXISTS.
- Bulk writes are set-based.
- Indexes exist for the filters and orders in use.
- Recursive CTEs are bounded and cycle-safe.

**ERR**
- Mapping is centralized, with the status taken from the exception.
- The handler order is correct and runs before routing.
- An uncatalogued code falls to an anonymous 500, logged at Error. Platform codes are checked at startup.
- Client aborts answer 499.
- Status-code pages give empty-bodied responses a platform code.
- Registry: 102 constants, all referenced; 114 catalog codes, 12 of them deprecated; no literals at throw sites.
- Validation uses the field-error shape.
- No inline user-facing text in C#.
- Logging is CWE-117 safe.
- `DbUpdateException` is mapped wherever the race is reachable.
- No swallowed exceptions.
- The 404-vs-403 order follows ADR-0027.
- `Contracts.Tests` enforces catalog completeness and generates `docs/error-codes.md`.

**ASY**
- Every I/O endpoint delegate takes and forwards a token.
- No optional tokens outside framework overrides.
- Every EF terminal and all 42 `ExecuteInTransactionAsync` sites pass the token.
- `CancellationToken.None` appears only where deliberate and documented.
- None of these exist: sync-over-async, `async void`, `Task.Run`, fire-and-forget, `WhenAll` over a shared context.
- Background services honour their stopping token.
- The renewal lock and the read limiter respect the request token.
- Outbound HTTP uses factory clients with 5 s timeouts.
- The cancel path is covered by tests.
- Blocking I/O happens only at startup or in dev tooling.

**DEP**
- CPM is on, with no `Version` or `VersionOverride` on any reference.
- No prerelease packages.
- The SDK is pinned in `global.json` and CI uses it.
- These families are aligned: EF Core, ASP.NET, Aspire, OpenTelemetry, Gridify (placed per ADR-0015).
- Every declared reference is used.
- The Onion package rules are enforced for the inner layers.
- No test packages in production projects.
- `EF.Design` is `PrivateAssets=all`.
- The MessagePack override is documented with a removal condition.
- Nullable and warnings-as-errors are global, with one commented suppression.
- All 14 projects are in the solution.

## Not covered

- Runtime behaviour, because nothing was built, tested, restored or EXPLAINed:
  - the 500 in EFC-1
  - index usage in EFC-3
  - binding-failure bodies in ERR-3
  - resilience and timeout composition in ASY-4
  - wire cookie flags
- The live company realm (client scopes, `fullScopeAllowed`, other clients' audience mappers). SEC-1's practical reach must be confirmed there.
- Not every handler was read in full: about 100 Application handlers and about 78 handler throws. Business authorization rules were not audited:
  - last-admin protection beyond its locking
  - self-demotion
  - other handler-level domain rules (`SaveWorkItemHandler`, `RenameProjectHandler`, `ReorderBacklogHandler`)
- Pairwise deadlock-freedom of the roughly 40 lock-taking handlers was not checked; the documented orders were spot-read. `ProjectBacklogRepository.cs:110-130` and the area move/delete handlers were read only to their lock headers.
- Per-feature `api-contract.md` error-code lists against what each endpoint can throw.
- The full JSON shape of every Abstractions view returned by Boards and WorkItems.
- Hand-written indexes and constraints in migrations other than `AggregateViewIndexes`.
- Online vulnerability, deprecation and outdated checks for packages. The resolved transitive versions came from `obj/project.assets.json` dated 2026-10-18.
- Authorization placement (`access.Holds`, `OrganizationPermissionHandler`, `CallerAccess`) beyond SEC-1 and SEC-6, deliberately, because mod-019 rewrites it.
- Test projects beyond Architecture.Tests, Contracts.Tests, the statement-count tests and a few model and migration tests. Whether fixtures pass real tokens.
- `src/frontend`, `deploy/keycloak`, the Kazakh catalog texts.
- Mid-session, worktree isolation refused the analysts' shell commands, so later evidence was gathered with Read/Grep against the checkout confirmed at `705ed258`. The consolidation pass re-read every cited major in this worktree, where `src/` is identical.

## Dropped at consolidation

No finding was dropped outright. Changes:
- **Merged:** ERR "Two slices declare no ProducesProblem" into ARC-6 (same routes, same fix). It is listed under both criteria.
- **Downgraded:** ARC-3 (AreaType stored as a number), major → minor. The enum has explicit values 1..3 (`Area.cs:12-18`), which removes the reorder-corruption risk the canon cites. What remains is magic numbers in SQL and the precedent. The review later moved it to info: accepted ADR-0031 prescribes the numbers.
- **Evidence corrected (ARC-2, ARC-10):** `VerticalSliceRules.cs` has 94 lines.
  - The regex is at `:15-17`, not `:139-141`.
  - `SliceOf` is at `:81-92`, not `:213-215`.
  - The rule body is at `:19-56`, not `:139-180`.

  The substance holds: a module-root `using` does not match the regex, and module-root files are skipped.
- **Evidence corrected (ERR-3):** the rescue handler lives at `Features/Organizations/GetRescueOrganizations/`, not `Features/Rescue/`.
- **Reframed (SEC-1):** kept major, on spot-check of `JwtBearerSetup.cs:50-110` and `OrganizationPermissionHandler.cs:39-82`.
  - The `azp` half stands on the code.
  - The `user`-gate half was read as a contradiction inside ADR-0033 (`:57`, `:76-77` against `:71-72`). The review corrected this: rule 2 keeps organization rights, so it is a policy question, and ADR-0033 is still proposed.
- **Confirmed on spot-check, unchanged:**
  - ARC-1: `AreaLinks.cs:35-40`, the four handler checks, `Area.cs:109-125` against `:190-195`, `CreateProjectHandler.cs:125-128`.
  - SEC-2: ADR-0022 `:90`, ADR-0037 `:5, 17, 51`, `SessionAuthenticationSetup.cs:107`, `SessionEndpoints.cs:57-59`, `nginx-mustask-locations.conf:1-13`.
  - EFC-1, a minor, also checked because it is queued as a bug: `AppointmentReadModel.cs:38`.
- **In-flight marks re-based:** analysts marked the mod-019 overlap "not verified". At consolidation, `git diff --stat origin/main...origin/mod/019-scoped-access` shows 13 files, all under `docs/features/mod-019-scoped-access/`. Overlap is judged against that spec and ADR-0029.

## Candidate follow-up tasks

| # | prefix | slug | size | covers | why |
|---|---|---|---|---|---|
| 1a | tech | bearer-azp-check | L | SEC-1 (azp half), SEC-3 | Changes authorization rules (L threshold): an `azp` allow-list in `OnTokenValidated` that admits `mustask-panel` and the stand-only `mustask-e2e` (ADR-0023), plus stripping inbound `perm` claims. Independent of mod-019 (`JwtBearerSetup`, not touched by it). |
| 1b | tech | bearer-user-gate | L | SEC-1 (user half) | Changes authorization rules (L threshold). Starts only after the human settles the user-gate policy; amends ADR-0033 in place (still proposed). deps mod-019, which owns `OrganizationPermissionHandler` and the `org:*` policies. |
| 2 | tech | session-origin-adr | L | SEC-2 | The human decision (own origin vs a recorded risk acceptance for the shared origin) written as an ADR; touches session and secret handling (L threshold). SEC-5 moves to #12. |
| 3 | tech | request-size-limits | L | SEC-4 | A global body cap and per-list count caps refuse inputs that existing routes accept today: a change of API semantics, which routing.md counts as breaking, and when torn between M and L it takes L. New error codes across WorkItems and Organizations. deps mod-019 (touches `AddTeamHandler`). |
| 4 | tech | area-rules-into-domain | M | ARC-1, TXN-5, ARC-5 (optional) | A domain rule over `AreaType` called at the same step in the four link handlers and in project-node creation; the `ORDER BY` for the area link statement. Domain + Application + Infrastructure in one module. deps mod-019 (the files collide). |
| 6 | tech | shared-code-placement | M | ARC-2, ARC-7 | Settles where module-shared code lives (canon amendment or move), splits the two-use-case folder, and moves the WorkItems routes. deps mod-019 (area handlers call `AreaRequests`). |
| 7 | tech | read-path-reachability | M | ARC-4, EFC-3, EFC-2 (optional) | One reachability composition in Boards and `OrganizationStructure`. The redundant index is dropped only after EXPLAIN against the feat-048 volume data. Parked until mod-019 lands, because ADR-0029 rewrites these reads. |
| 8 | bug | rescue-page-overflow | S | EFC-1 | Observable 500 on a far page of the rescue list. Reproduce with a test first, then copy the `WorkItemReadModel` paging pattern. |
| 9 | tech | unit-of-work-cleanup | M | TXN-1, TXN-2 | Removes the retry-era wrapper, keeps and documents `ChangeTracker.Clear()`, and closes the second flush point's error-mapping gap. Covers the follow-up that ADR-0012 says was filed. |
| 10 | tech | error-contract-metadata | M | ARC-6, ERR-1, ERR-6 | `ProducesProblem` on Dictionaries and Session, a forbidden code on the four Dictionaries routes, Error-level logging for 5xx business exceptions, and an endpoint-metadata test over every non-Public route. More than 5 files across Api and tests (routing.md:134). ASY-3 is info and not included. |
| 12 | tech | outbound-http-policy | S | ASY-2, ASY-4, SEC-5 | One explicit timeout/retry policy for the Keycloak clients. It must disable retries on the `refresh_token` POST, with a test that a 5xx on refresh is not retried; this changes session behaviour. SEC-5 (explicit Data Protection key ring) is a config-only addition. |
| 13a | tech | build-analyzer-gates | M | ASY-1, DEP-3 | CA2016 as a warning and `EnforceCodeStyleInBuild` with `Migrations` marked `generated_code`. Fan-out across the solution is unknown under `TreatWarningsAsErrors`, so check the build first (S if clean). Independent. |
| 13b | tech | architecture-and-model-tests | M | ARC-10, DEP-5, ERR-2, TXN-4, EFC-6 | Review-only rules become tests: arch rules, the catalog scanner's literal check, the xmin model test, the shared statement counter, dependency rows. deps tech-035, which owns the test kit. |
| 14a | tech | package-catalog-cleanup | M | DEP-2, DEP-6, DEP-1 (optional) | Unused entries removed and the Aspire SDK version in one place; the optional Npgsql 8 → 10 bump changes the AppHost stand-reset runtime. |
| 14b | tech | build-file-comments-english | S | DEP-4 | Translate the Russian comments in about 8 build files, wording only. Comment-only; fits the light cycle if the human counts build-file comments as docs. |
| 15 | tech | canon-sync | L | ARC-8, ARC-9, ERR-4, TXN-3, ARC-3, ARC-11 | Brings `backend.md` in line with the code (routes, slice schools, four handlers), records the Languages xmin-as-ETag exception in an ADR (L threshold), fixes ADR-0031's "no departures" line against `backend.md:314-318`, and carries the human's accept-or-revise question for the proposed ADRs 0021/0022/0033/0034/0036. Docs-only. deps mod-019 for the route text. |

Not queued: the human decides at gate-1.

Dropped or parked after the review (the numbers are kept, so the gaps are intentional):
- **#5 area-type-as-text: dropped.** ARC-3 is info: accepted ADR-0031 rule 1 prescribes the numeric values, a conversion would need an amending ADR first, and the benefit is cosmetic. The ADR-0031/canon reconciliation moved into #15.
- **#11 query-parameter-errors: parked.** ERR-3 and ERR-5 are info and ADR-0004:111-128 sanctions `http.bad_request` for binding failures; an L task with all three gates is poor value. If wanted, make it a canon question. The `AggregateViewEndpoints.cs:186` docstring mismatch can ride along in #10, and ERR-5 is handled when Dictionaries is next touched.

Not given a task:
- SEC-6 folds into mod-019's design. ERR-6 is in #10 (mod-019 does not cover the Dictionaries instance policy).
- EFC-4, EFC-5, ASY-3, ASY-5 and ASY-6 are handled when their files are next touched.