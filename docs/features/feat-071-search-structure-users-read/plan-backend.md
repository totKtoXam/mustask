# feat-071 — backend plan: the search read for structure and users

Stage 4a artifact (size L). Requirements: [feat-065](../feat-065-global-search/spec.md) (BR-GS-2..-6, -8..-10, -17, -19 for awareness; stories US-2, US-4; US-8 only so the read does not preclude it), [design D-7 and § 3.2-3.4](../feat-065-global-search/design.md). Sight: feat-041 (ADR-0040, doc-016 BR-TD-12..-16). Contract: [api-contract.md](api-contract.md). Decision record: [ADR-0044](../../adr/0044-trigram-quick-search-full-text-page-and-search-limiter.md) (proposed; gate-1 accepts it with this plan).

Scope of this task: departments, teams, projects, sub-areas, users. Work items (feat-065 `search-work-items-read`) are a later line; the read is built so that line adds one group, not a second read.

## 1. Where it lives

A new module `Search`, owned by no other module; it reads other modules' tables the way `Boards` already does (`AggregateViewReadModel` reads Organizations and WorkItems tables through the one `MustaskDbContext`), behind an Application abstraction.

| Layer | Path | Content |
|---|---|---|
| Domain | `Mustask.Domain/Search/` | `SearchQueryText` (trim, 2..100, the fold: lower-invariant, ё→е, the LIKE escape), `SearchGroupKind`, `MatchTier` (1..5), `SimilarityRule` (threshold, min length 4) and the "fill with similar" rule: pure, unit-tested |
| Application | `Features/Search/SearchStructure/` | `SearchStructureQuery`, handler, validator, `SearchStructureResponse` (one use case; first read and expansion are the same query with `Expand`) |
| Application | `Abstractions/Search/ISearchReadModel.cs` | the read the handler calls: `ReadAsync(SearchScope, folded query, limit, kinds)` |
| Infrastructure | `Search/SearchReadModel.cs` | the EF/SQL; `Search/SearchFold.cs` (`HasDbFunction` for `search_fold`) |
| Infrastructure | `Migrations/…_AddSearchTrigramIndexes` | § 4 |
| Api | `Features/Search/SearchEndpoints.cs`, `SearchReadLimiter.cs` (limiter + endpoint filter) | § 5 |

The read model takes a **list of organizations** (`SearchScope`: id, role, the caller's `AccessSet`, gate flags) and filters `organization_id = ANY(...)`. This task passes one; US-8's line passes many without a new read or more statements (BR-GS-19 statement-count rule). Ranking's "current organization first among equal tiers" is one more sort key supplied by the scope, null for now.

No lanes: one use case, so no write-zone split (04-backend § 4c).

## 2. The read (one per organization)

Order is fixed by BR-GS-4/-5: **sight first, then match, rank, limit**.

1. **Access.** The route's existing organization gate (non-member and instance administrator without membership: 404 `organizations.organization.not_found`, as every organization route). `organizations.membership.read` is read once from `IOrganizationAccess`: without it the users group is never queried and never present.
2. **Sight sets** (once per request). `IOrganizationAccess.SetAsync` yields the caller's `AccessSet` (feat-041's set form, ADR-0040 § 4: `SeesProject`, `SeesTeam`, `SeesArea`; hidden ≡ nonexistent, parity with the single form already pinned by `UsersSeeingProjectParityTests`). One statement reads the ids and kinds of the organization's **active** departments, teams, projects and sub-areas (deactivated and deleted ones excluded in SQL, BR-GS-6; sub-areas of a deactivated project too); the `AccessSet` filters them in memory into four visible-id arrays. For the organization administrator the set holds no rows and every object is visible: no ids are passed and no statement is spent (the arrays are `null`, "no restriction"). At the doc-012 volumes the arrays are at most about 1 400 ids.
3. **Match, rank, limit — one statement per group** (≤ 5, only the groups the query kinds and the gate allow). Each statement is `WHERE organization_id = … AND id = ANY(@visible) AND (match)`, computes the tier, orders by tier, then the group's tie-break (name in the organizations Russian collation, then id), and takes `limit + 1`. Users: the organization's **current** members joined to the user projection (former members are not found; disabled accounts are found and flagged `inactive`, BR-GS-3).
   - Fold: the query is folded once in the app (`lower`, ё→е, trimmed); the column side uses the same fold as the index expression `search_fold(text)` (§ 4). Users use their existing folded `search_name` / `search_email` columns, so no function there (the migration checks they fold ё→е; if not, the index and the predicate use `search_fold` on them).
   - Literal characters: the folded text is escaped for `LIKE` (`\`, `%`, `_`), `ESCAPE '\'`.
   - Tiers (BR-GS-8): 1 `= q`; 2 `LIKE q%`; 3 a word of the name starts with `q` (`~ '(^|[^[:alnum:]])' || escaped_q` — regex-escaped); 4 `LIKE %q%`; 5 `word_similarity(q, text) >= threshold` for `length(q) ≥ 4` (the `<%` operator with `pg_trgm.word_similarity_threshold` set by `set_config(..., true)` in the same transaction, so the GIN index serves it; the threshold constant lives in `SimilarityRule`, starts at 0.5 as the prototype's, fixed by the near-miss table of the tests, ADR-0044 § 2). Projects match on name and key (the better tier wins), users on display name and email, teams, departments and sub-areas on name.
   - The tier-5 rule (BR-GS-17) is applied by the handler from the `limit + 1` rows: if tiers 1-4 already give at least `limit` rows, similar rows are dropped; else they fill up to `limit`. `hasMore` is true only when more than `limit` visible rows exist after that rule (never from a hidden row: the statement saw only visible ids).
4. **Groups.** First read: `limit = 5` for every group. Expansion (`expand = <group>`): only that group is read, `limit = 20`, `hasMore = false` (no further paging, BR-GS-9). Group order in the answer: by the best row's tier, ties in the fixed order «Рабочие элементы», «Проекты», «Команды», «Департаменты», «Области», «Пользователи» (BR-GS-8); empty groups are absent.
5. **Statement count** is fixed: organization gate + the access set's own statements (as today) + 1 visible-ids statement (none for the administrator) + ≤ 5 group statements, whatever the number of rows, matches or objects. The work items line adds one group statement.
6. **Nothing from the query text is logged**: only length and duration go to the log; the endpoint writes no body, and the validation failure carries no echo of the text (BR-GS-10).

## 3. Disclosure rules the code keeps

No total anywhere in the response; no field depends on a hidden row; the answer for a hidden object and for a nonexistent name is byte-identical (same groups, same bodies); no early exit on a hidden hit (the statements never see hidden ids, so there is nothing to exit early from); the 2-character query takes the same path (no trigram index at 2 characters: the visible set is at most a few thousand rows, within the same targets, checked by the volume test).

## 4. Migration

One migration `AddSearchTrigramIndexes` (additive, reversible):

- `CREATE EXTENSION IF NOT EXISTS pg_trgm;` (Down: `DROP EXTENSION IF EXISTS pg_trgm`, after the indexes).
- `CREATE FUNCTION search_fold(text) RETURNS text IMMUTABLE PARALLEL SAFE LANGUAGE sql AS $$ SELECT replace(lower($1), 'ё', 'е') $$;` (Down: drop).
- GIN `gin_trgm_ops` indexes: `areas` on `search_fold(name)` (departments and sub-areas are area rows of the typed tree, ADR-0031; a partial index `WHERE status = active` is considered at implementation if the volume test needs it), `teams` on `search_fold(name)`, `projects` on `search_fold(name)` and on `search_fold(key)`, and on the user projection (`directory_users`) `search_name` and `search_email` (already folded).

Which modules' tables and why it is acceptable: Organizations (`areas`, `teams`, `projects`) and UserDirectory (the projected users). It is one database and one `DbContext`, the migration chain is single, and the change is additive: no column, constraint or write path of those modules changes, their handlers do not know the indexes exist. The cost is write amplification on tables that are small and rarely written (names change by hand; the projection is written at sign-in, a few thousand rows). Expression indexes are written as raw SQL in the migration (EF's `HasIndex` cannot declare an expression), so the model snapshot does not carry them: a test pins their existence and the function (`pg_indexes`), and a test pins that the read model's predicate text uses the same `search_fold` expression, since a predicate that differs by one character silently stops using the index. Run on a clean database and twice-applied (rollback idempotency, DoD). Needs the DB host to allow `pg_trgm` (a trusted extension for the database owner since PostgreSQL 13): DevOps are asked before the MR (Q-4).

## 5. Limiter

`SearchReadLimiter` + `SearchReadLimitFilter`, a structural copy of ADR-0036's `AggregateReadLimiter` with the numbers of design D-7: per user (Keycloak subject) 3 in flight, queue 3; per process 24 in flight, queue 12; wait at most 1 s by the injected `TimeProvider`; refusal is 429 `http.too_many_requests` (existing catalogue entry) with `Retry-After: 1`. Registered next to `AggregateReadLimiter` in `Program.cs`.

Composition with ADR-0036's: **independent instances, no shared permits.** A search never waits behind a group read, and a user opening six groups cannot starve the palette (and the other way round). The pool arithmetic of ADR-0036 § 5 (40 of 100 connections) becomes 40 + 24 = 64 of 100 at the extreme, leaving 36; the ADR states this. The filter runs after authentication and the organization gate (subject known), before the handler; the limiter is the endpoint's only filter beyond them.

## 6. Error catalogue

`contracts/errors/search.json` (new module `search`, origin `slice`) and the regenerated `docs/error-codes.md` (`MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests`); each entry carries ru and kk (`kkNeedsReview: true`) as the catalogue requires; user texts are Russian (AGENTS.md):

| Code | Status | ru |
|---|---|---|
| `search.validation_failed` | 400 | Проверьте правильность запроса. |
| `search.query_too_short` (field `query`) | 400 | Введите не менее 2 символов. |
| `search.query_too_long` (field `query`) | 400 | Запрос не должен быть длиннее 100 символов. |
| `search.invalid_group` (field `expand`) | 400 | Такой группы результатов нет. |

Reused, not added: `organizations.organization.not_found` (404, «Организация не найдена.»), `http.too_many_requests` (429). `SearchErrorCodes.cs` in Domain carries the four codes.

## 7. Tests

Written with the code, each with a comment linking the story criterion.

Domain/Application (unit): query rules (trim, 2..100, 1 and 101 characters, code points not UTF-16 units); fold (case, ё→е, mixed Latin/Russian); LIKE escape («%», «_», «\» find only themselves); tier assignment table; the tier-5 rule (needs ≥ 4 characters, only below `limit`, never displaces, `hasMore` unaffected); similarity near-miss table («логстика», «jupitr», «смирнва» match; four non-matches that must not); group order by best tier and the fixed tie order; limit+1 → `hasMore`; expansion returns one group, ≤ 20, `hasMore = false`; users group absent without `organizations.membership.read` (handler test with such a caller, US-2); validator.

Integration (Testcontainers, Api.IntegrationTests): endpoint for each US-2 scenario (grouped results, key match, case/ё, literal characters, users by name/email, former and disabled members, ranking, group order, five rows and «Показать ещё», sub-areas, deactivated/deleted not found, no match, typo tier, trim); 404 for a non-member and for an instance administrator without membership; 400s with the field and reason; 429 body and `Retry-After: 1`.

**Hidden-object tests, one per kind** (US-4, BR-GS-4/-5; Olga's cast from US-4): a hidden project (by name and by key), team, department and sub-area is absent from groups and from «more»; `hasMore` is false when the user sees 4 of 7 and true for the administrator; the response for a hidden name and for a name that exists nowhere is byte-identical; no total or count field in any response (a reflection test over the response type and the JSON); hiding changes neither length nor order of the visible rows; a project visible by team membership, by a grant, by the visible-to-organization flag (feat-041 cases) each found and a project flipped to hidden vanishes at once; the typo tier does not resurrect a hidden object («jupitr»).

**Statement-count test**: a counting `DbCommandInterceptor` runs the read against 10 and against 1 000 objects, an administrator and a restricted user, an expansion and a first read; the count is equal across the data sizes and ≤ the bound of § 2.5; the query text never appears in the captured log (BR-GS-10) — one test captures every `ILogger` record of a request with a search text and asserts its absence, also on the 400 and 429 paths.

**Index tests**: the migration applies on a clean database and twice; `pg_indexes` lists the six indexes and `search_fold`; the predicate-text test of § 4; one `EXPLAIN` test at volume asserts the planner picks the GIN index for a ≥ 3-character query (on the volume data set, not on a toy table).

**Volume test** (the doc-012 data set: 1 000 areas, 200 projects, 200 teams, 2 000 members, generated once per class): 300 reads of mixed queries (1-char rejected, 2-char, 3-char, a word, a typo, a no-match) for the administrator and for a user seeing a tenth; targets from the spec: **p95 ≤ 300 ms, p99 ≤ 800 ms** server side, the 2-character query inside the same targets. Marked as a performance category, run in CI on the Testcontainers database; the thresholds are asserted with the machine-variance margin documented in the test (the spec's numbers are the goal; the test fails above 2× so a noisy runner does not turn CI red, and the measured values are written to `qa.md`).

**Limiter tests** (as `AggregateReadLimiterTests`, fake clock): the fourth concurrent read of one user waits, the seventh refused; one user cannot take the process's permits; the 25th read of the process queues, the 37th refused; the wait ends at 1 s by the fake clock; cancellation frees the place; the filter's 429 has an empty body that becomes `http.too_many_requests` and `Retry-After: 1`; the two limiters are independent (a user holding 6 aggregate permits is not delayed in search).

Architecture tests (existing): the slice references no other slice; `Mustask.Architecture.Tests` stays green with the new module.

## 8. ADRs relied on

ADR-0005 (error codes), ADR-0007 (error envelope), ADR-0009/-0012 (EF Core reads, no retry), ADR-0015 (envelope; its "full-text search … a new ADR" note is the departure recorded in ADR-0044), ADR-0026 (deactivation), ADR-0029 § 6 (set form), ADR-0030 (recycle bin hiding, for the work items line), ADR-0031 (typed tree: departments and sub-areas are areas), ADR-0033/0029 (the read codes), ADR-0036 (limiter precedent; not amended), ADR-0040 § 4 (sight by participation, `AccessSet`). New: **ADR-0044** (proposed).

## 9. Delivery order (phase of one author)

Migration → Domain rules and tests → `ISearchReadModel` + read model with the statement-count and hidden-object tests → handler/validator → endpoint, limiter, catalogue → volume test → `qa.md` input. Small commits per step, green after each.

## Open questions for gate-1 (decided by the human 2026-11-17: all as recommended; Q-4 the human confirmed `pg_trgm` is available on the production host)

- **Q-1 Separate limiter instance or share ADR-0036's permits?** Recommendation: **separate** (§ 5). Shared would let group reads starve the palette; the cost is 24 more connections at the extreme (64 of 100).
- **Q-2 Sight sets as id arrays from the in-memory `AccessSet`, or re-express sight as SQL joins?** Recommendation: **arrays** (§ 2.2): ADR-0040's set form is the single source, parity-tested; a second SQL expression of the sight rule would be a second place to get it wrong. Revisit only if the volume test fails for the restricted user.
- **Q-3 Expression indexes as raw SQL in one migration owned by this slice, with pinning tests, rather than editing the entity configurations of Organizations and UserDirectory?** Recommendation: **raw SQL here** (§ 4): EF cannot declare expression indexes, and it leaves the other modules' files untouched (parallel branches).
- **Q-4 `pg_trgm` on the production database host.** Needs a DevOps answer that the database owner may `CREATE EXTENSION pg_trgm` (a trusted extension; Testcontainers and the Aspire image have it). Recommendation: **ask DevOps now**, in parallel with gate-1, and record the answer in `status.md` before the MR; if refused, the fallback is a one-time grant by a DBA, not a code change.
- **Q-5 Volume-test strictness in CI.** Recommendation: assert 2× the spec's p95/p99 in CI and record the measured values in `qa.md` for the human to compare to the spec's numbers; a strict assertion on shared runners will flake.

## Implementation notes (backend-developer, 2026-11-17)

Deviations from this plan, none of them a contract change:

- The users statement text lives in `Mustask.Infrastructure/UserDirectory/SearchUsersStatement.cs`, not in `Search/SearchReadModel.cs`: the architecture rule `UserDirectoryRawSqlQaRules` lets only the user-directory sub-slice name `directory_users`. The helpers it shares (`Wrap`, `TierOf`, `Matches`, `SimilarityOf`) are `internal` on `SearchReadModel`.
- The database-level tests (statement count, index pinning, EXPLAIN pins, similarity table, volume) are one class, `Mustask.Infrastructure.Tests/Search/SearchDatabaseTests.cs`, over a bulk `generate_series` seeder (`SearchDataSet`). The key index's EXPLAIN pin drops the btree `ix_projects_organization_id_key` inside its rolled-back transaction, because on a short key the planner prefers it to the GIN index.
- Migration apply-twice / Down-Up is covered by the existing generic `MigrationsTests` (model matches migrations, pending-model check) plus the pins; no search-specific Down/Up test was added.
