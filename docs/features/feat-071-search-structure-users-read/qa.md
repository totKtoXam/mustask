# QA — feat-071 search: structure and users read

## Backend

Plan written from feat-065 spec (BR-GS-3..-10, -17), stories US-2 and US-4, this task's api-contract and ADR-0044, before the code was read. Scope: departments, teams, projects, sub-areas, users (work items are feat-072). E2E: no (standing answer of the human). Commit under test: `df2f0e8d`.

### Test plan (from the requirements)

Criteria: every Gherkin scenario of US-2 and US-4 that this read delivers (UI-only ones — debounce, stale answers, failure text, 429 retry in the panel, recent items, work-item scenarios — belong to later lines). Added scenarios: S1 hidden parent names never in a visible row; S2 hidden ≡ nonexistent body, no total/count/hidden field, no hidden-dependent work or order; S3 limiter 3+3 / 24+12 / 1 s wait / 429 + `Retry-After: 1` / cancel frees the place / separate from ADR-0036's; S4 log holds no query text on 200/400/429 paths; S5 validation 2..100 after trim, code points, missing/short/long/unknown `expand`, malformed body; S6 `%`, `_`, `\` literal in names, keys and emails; S7 case and ё/е fold on both sides (stored names and the projection's users); S8 typo tier only at ≥ 4 chars, only fills below the limit, never displaces, respects sight; S9 limit+1 «ещё» from visible rows only; expansion of each of the five groups alone, up to 20, `hasMore` false; S10 row shape per kind as the contract table; S11 non-member / instance admin / removed member → organization 404; 401; viewer allowed; users group absent without `organizations.membership.read`; S12 migration applies, six trigram indexes, `search_fold` immutable.

### Coverage matrix (criterion → test)

| Criterion | Test (class.method) | Status |
|---|---|---|
| US-2 Grouped results | SearchStructureEndpointTests.Grouped_… | pass |
| US-2 Match by project key | …MatchByProjectKey_IgnoresCase | pass |
| US-2 Case and ё | …CaseAndYo_AreIgnored; SearchRulesTests.Fold_*; QA: Users_EmailLiteralsAndYoFold | pass |
| US-2 Literal characters | …LiteralCharacters_FindOnlyThemselves; QA: Users_EmailLiteralsAndYoFold (`_`, `%` in email) | pass |
| US-2 Users by name and email | …Users_AreFoundByNameAndByEmail_WithTheirEmail | pass |
| US-2 Users need membership read | SearchStructureHandlerTests.WithoutTheMembershipRead_* (handler, as the story says) | pass |
| US-2 Former and disabled members | …FormerMembersAreNotFound_AndDisabledMembersAreFlagged | pass |
| US-2 Ranking; group order | …Ranking_*; …GroupOrder_FollowsTheBestRow_*; handler Groups_AreOrderedByTheirBestTier_* | pass |
| US-2 5 rows and «ещё» | …FiveRowsAGroup_AndAnExpansionOfTheOneGroup; AnExpansion_StopsAtTwentyRows; QA: EveryGroup_ShowsFiveAndExpandsAlone (teams, departments, areas, users) | pass |
| US-2 Sub-areas | …SubAreas_AreFoundWithTheirProjectAndDepartment | pass |
| US-2 Deactivated and deleted | …DeactivatedAndDeletedObjects_AreNotFound | pass |
| US-2 Too short / trimmed / bounded | …TooShort_*, TooLong_*, TheQuery_IsTrimmed; SearchRulesTests.Judge_*; QA: TheBoundary_IsJudgedAfterTrimming, AMalformedBody_Is400_WithoutEchoingIt | pass |
| US-2 No match | …NoMatch_IsAnEmptyListOfGroups | pass |
| US-2 Typo tier; ≥ 4 chars; short group; sight | …TypoTolerance_* (3), SightEndpoint.TheTypoTier_DoesNotResurrectAHiddenObject; SearchDatabaseTests.ASimilarName_* (threshold table); SearchRulesTests.Fill_*, AllowsSimilar_* | pass |
| US-4 Hidden project not found, same body | SightEndpoint.AHiddenProject_IsNotFoundByNameOrKey_AndTheBodyIsTheNowhereBody | pass |
| US-4 Hidden teams/departments, MARS via link | …AHiddenTeamAndAHiddenDepartment_AreNotFound_AndALinkedTeamIs | pass |
| US-4 «More» never reflects hidden | …More_ComesFromVisibleRowsOnly | pass |
| US-4 No totals | …NoTotalsAnywhere (types and JSON keys) | pass |
| US-4 Order independent of hidden rows | …AHiddenRow_ChangesNeitherTheRowsNorTheirOrder (byte-equal body) | pass |
| US-4 Users without code | handler tests | pass |
| US-4 Sight changes between requests | …SightChangesApplyOnTheNextRequest_ThroughTeamGrantAndFlag | pass |
| US-4 Foreign org; not a member; membership ends | …AnotherOrganizationsObjects_AreNotFound; ANonMember_AndAnInstanceAdministrator_*; AMemberWhoIsRemoved_* | pass |
| US-4 Entry-point per kind | department, team, project, sub-area: SightEndpoint tests; user: AUserOfAnotherOrganization_IsNotFound (a user has no sight rule, only org membership) | pass |
| Hidden parent not named (S1) | QA: NoVisibleRow_NamesAHiddenParent (sweep over 13 queries) | pass |
| BR-GS-10 log privacy | SearchPrivacyAndLimitEndpointTests.TheTypedText_IsInNoLogRecord_OnAnyPath | pass |
| BR-GS-15 429 + Retry-After | …ARefusedRead_Is429WithRetryAfterOne; SearchReadLimiterTests (9) | pass |
| Contract row shape (S10) | QA: EveryRowKind_CarriesExactlyTheFieldsOfTheContract | pass |
| Statement count, index pins, volume (S12) | SearchDatabaseTests | pass |

### Additional scenarios — added tests

`SearchQaEndpointTests` (13 cases): row shape per kind; expansion of teams/departments/areas/users; `_`/`%` in emails and ё on the users side; several words are one piece of text (the reverse order appears only as `similar`); trim-then-bound at 100/101 and 2; malformed JSON, non-string query, empty body → 400 without echo; hidden-parent sweep. All green on first correct form (one test of mine was wrong about the typo tier and was fixed, not the code).

Not tested, by judgement: response timing of hidden vs nonexistent. By construction the read does no query-dependent work on hidden rows: the candidate statement does not take the text, and the group statements receive only visible ids (or the whole organization for an administrator, who hides nothing); the spec itself does not ask for a timing test.

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-17 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter FullyQualifiedName~Features.Search` | 63 passed (50 developer's + 13 QA) |
| 2026-11-17 | `dotnet test src/backend/tests/Mustask.Infrastructure.Tests --filter FullyQualifiedName~Search.SearchDatabaseTests` | 14 passed |
| not re-run | Domain.Tests Search, Application.Tests Search, Contracts.Tests, Architecture.Tests | developer's green accepted, CI runs the full suite |

Migration on a clean database: applied by the fixtures of both projects (indexes and `search_fold` asserted by `TheSixTrigramIndexes_AndTheFoldFunction_Exist`). Repeat/rollback not exercised by me.

### Volume (BR-GS NFR: p95 ≤ 300 ms, p99 ≤ 800 ms; test asserts 2×)

| Run | p95 | p99 |
|---|---|---|
| developer 1 | 233 | 298 |
| developer 2 | 347 | 432 |
| developer 3 | 474 | 654 |
| QA (one run) | 311.4 | 411.3 (p50 106.7, max 495) |

Dataset: 1000 areas, 200 projects, 200 teams, 2000 members, 300 reads, half administrator, half a caller seeing a tenth.

View: p99 is inside the spec in all four runs. p95 is above 300 in three of four, so I do not call it runner noise: the spread 233..474 is noise on a laptop with Docker Desktop, but the median of the four (~330) sits over the target. The measured scope is also narrower than the NFR's: the read model only (no HTTP, authentication, `AccessSet` construction for a non-administrator, JSON), with a ~107 ms p50 already a third of the budget. So the p95 target is neither shown met nor shown missed. Recorded as D-1 (minor debt, for gate-3): measure end to end on the stand or CI and re-judge; the ADR's fallback (sight in SQL) is the remedy if it holds above 300.

EXPLAIN pin for the project-key index: honest for what it claims, no more. It proves the predicate text of the read equals the index expression and that the operators (`LIKE`, `%>`) can use the GIN index (seq scan off; a drift fails). Dropping the competing btree inside a rolled-back transaction is a disclosed, harmless device (if the btree is renamed the `IF EXISTS` becomes a no-op and the test would fail loudly, not pass). It does not prove the planner picks the trigram index for the real statement (organization filter, OR of name and key); at 200 projects a btree plus filter is as good, so the key index is probably unused in practice. Not a defect; the claim "indexes serve the read" should not be read from it.

### Defects

#### D-1: p95 target not demonstrated (volume)

- Severity: minor (carried to gate-3 as a debt)
- Steps: run `SearchDatabaseTests.VolumeReads_StayWithinTwiceTheSpecBudget` several times; p95 233–474 ms.
- Expected / Actual: spec p95 ≤ 300 ms; three of four runs above it, over the read model alone, which excludes the request pipeline and the sight set.
- Status: open

#### D-2: word-prefix tier depends on the database's character classification

- Severity: minor
- Steps: `WordPrefixPattern` is `(^|[^[:alnum:]])q` evaluated with the database default collation; Cyrillic letters count as alphanumeric only when the cluster's ctype knows them. The test suite runs on `postgres:18-alpine`; the production host's ctype was not checked (no test pins it).
- Expected / Actual: tier 3 (word starts with) vs tier 4 (contains) ordering is right on the test image; on a C-locale cluster every contains-hit could rank as tier 3. Ranking only, no disclosure. Unverified on such a host.
- Status: open (suggest DevOps confirm the production ctype, or the regex take an explicit collation)

#### D-3: a null `color` is omitted, not sent as null

- Severity: minor (contract wording)
- Steps: contract says absent fields are omitted "except `color`", which is null when the object has none; the DTO omits a null `color`. No object kind can have a null colour today (projects, teams, departments always carry one), so no client sees the difference.
- Status: open (fix the contract sentence or the DTO; no test needed today)

Observations, not defects: with 3 exact and 3 similar rows the first read shows 5 and «ещё» (similar rows count toward «more»), and the expansion then shows 6, consistent. The log-privacy test covers 200, 400 and 429 paths, not the 404/401 paths; neither handles the text.

### Verdict

blockers: 0 · majors: 0 · minors: 3 (D-1..D-3, debts for gate-3) · result: pass
