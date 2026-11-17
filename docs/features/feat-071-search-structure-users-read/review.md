# feat-071 — code review

## Round 1

Scope: `git diff origin/main..HEAD` at `420d4a92` (feat-041 now in main; the merge's only change to this task's diff is the `Mustask.Application/DependencyInjection.cs` resolution, both registrations kept, correct). 39 files, backend only. Read against feat-065 spec (BR-GS-3..-10, -17), US-2, US-4, plan-backend.md, api-contract.md, qa.md, status.md, ADR-0044, ADR-0040 (`AccessSet`), `docs/conventions/backend.md`. No tests run; no finding needed one.

Checked and sound: the order sight → match → rank → limit (group statements see only visible ids; the administrator passes none); no total/count field; hidden parent names cannot appear (a visible team/project makes its department visible, `AccessSet.SeesArea`); BR-GS-6 filters (projects/teams `status = 'Active'`, sub-areas joined to an active project, departments have no status, `ck_areas_color`); parameterised SQL (interpolated text is only aliases and a constant collation), `LIKE … ESCAPE '\'` and the regex escape set are complete; the threshold `set_config(…, true)` inside the read's own transaction; the limiter is ADR-0036's pattern (permits released by `using`, cancellation re-thrown, refusal only on the 1 s wait), its own instance, after the organization gate; no log line carries the text (Serilog `writeToProviders: true`, so the capture test sees production-level records); the migration is last in the chain, `CREATE EXTENSION IF NOT EXISTS`, Down in reverse order and exercised by `MigrationsTests.Migrator_RollbackToZeroThenReapply_*`; snapshot untouched.

### Findings

**F1 — major — the volume test omits the queries the plan says it measures.**
`src/backend/tests/Mustask.Infrastructure.Tests/Search/SearchDatabaseTests.cs:148`; claims in `plan-backend.md:41` ("the 2-character query … within the same targets, checked by the volume test") and `:86` ("1-char rejected, 2-char, 3-char, a word, a typo, a no-match"). The ten texts are 4..10 characters, all matching; no 2-character query (the one the trigram index cannot serve), no 3-character one, no no-match. D-1's numbers therefore exclude the case the plan singled out.
Fix: add e.g. `"пр"`, `"ко"`, `"про"`, `"юзз"`, a no-match (`"щщщщ"`) and a 1-character text asserted as rejected by `SearchQueryText.Judge`; re-run, put the new p50/p95/p99 in qa.md. If the 2-char case is the outlier, say so there.

**F2 — major — the fold and the trigram match depend on the database's ctype, an unstated deployment precondition (supersedes QA D-2).**
`src/backend/Mustask.Infrastructure/Migrations/20261117110034_AddSearchTrigramIndexes.cs:28` (`lower($1)` under the default collation), `src/backend/Mustask.Infrastructure/Search/SearchReadModel.cs:234` (`~ @word`, `[[:alnum:]]`), `src/backend/Mustask.Domain/Search/SearchQueryText.cs:52`. D-2 is wider than ranking: on a cluster whose `datctype` is `C`/`POSIX`, PostgreSQL's `lower()` folds ASCII only, so `search_fold('Альфа')` stays capitalised and «альфа» finds nothing (BR-GS-3 broken for every Cyrillic name), and pg_trgm does not treat Cyrillic as word characters, so the typo tier is dead. The codebase already avoids this elsewhere by folding under the explicit ICU collation (`Organizations/AppointmentReadModel.cs:29`, `WorkItems/WorkItemFilterPredicate.cs:95`). ADR-0044 names `pg_trgm` as the only DB precondition; `deploy/vm/install.sh:215` creates the database from the cluster's template, whatever its locale.
Fix (developer): make the migration fail loudly on such a cluster (`DO $$ BEGIN IF (SELECT datctype FROM pg_database WHERE datname = current_database()) IN ('C','POSIX') THEN RAISE EXCEPTION … END IF; END $$`) and add a DB test pinning `search_fold('ЁЛКА Альфа') = 'елка альфа'` and `word_similarity('логстика','логистика') >= 0.5`, so a CI image with a C ctype fails too; state the precondition in `docs/deploy/vm.md` and `vm.ru.md` in the same change and in the plan's implementation notes (ADR-0044 is accepted and not edited substantively, README rule 3). Orchestrator: ask DevOps for the production database's `datctype` alongside the Q-4 answer and record it in `status.md` before the MR.

**F3 — major (a sibling file contradicts the contract in the same diff) — null `color` (QA D-3).**
`docs/features/feat-071-search-structure-users-read/api-contract.md:40` ("absent fields are omitted, not null, except `color`") and `:50` ("or null") vs `src/backend/Mustask.Application/Features/Search/SearchStructure/SearchStructureResponse.cs:43` (`WhenWritingNull` on `Color`). Unreachable today — `ck_areas_color` and the non-null project/team columns guarantee a colour for the three kinds that carry one — but a frontend author reading the contract codes for a `null` that never comes, or for a key that may vanish.
Fix (prose only): contract — "absent fields are omitted, never null; `color` is always present on projects, teams and departments"; a contract-change note in `status.md` (AGENTS.md rule). No code or QA re-run.

**F4 — minor — the EXPLAIN pin proves less than the prose says.**
`SearchDatabaseTests.cs:90-116`; claims `plan-backend.md:51` ("a test pins that the read model's predicate text uses the same `search_fold` expression") and `:84` ("at volume asserts the planner picks the GIN index"), and `docs/conventions/backend.md` ("`SearchDatabaseTests` pins both"). The predicates are hand-written literals on 30 rows with `enable_seqscan = off`; only `search_fold(p.name)` is tied to the read model (line 115); the users columns from `SearchUsersStatement` and the `Matches`/`TierOf` text are not. It proves "the index can serve this literal shape", which qa.md already says honestly.
Fix: build each EXPLAIN predicate from `SearchReadModel.Matches(SearchReadModel.Folded(col), true)` (and the users statement's column text), or weaken the three sentences to "can use".

**F5 — minor — the users statement satisfies the boundary rule by location, not by intent; sub-slice cycle.**
`src/backend/Mustask.Infrastructure/UserDirectory/SearchUsersStatement.cs:5-13`, `SearchReadModel.cs:217`, `docs/conventions/backend.md` (new section). mod-014 `api-contract.md` A.8 says consumers "never read the projection's tables"; Search still does, through a string that lives in `UserDirectory/` and calls `SearchReadModel`'s internals, while `SearchReadModel` calls it back. The direct join was accepted with ADR-0044 § 1 / plan § 2.3, so the access itself is decided — the comment "so the boundary rule (api-contract A.8) holds" is what overstates.
Fix: move `TierOf`/`Matches`/`SimilarityOf`/`Wrap` to a neutral internal static class both use (breaks the cycle), and reword the comment and the backend.md sentence to "an accepted exception to mod-014 A.8 (ADR-0044 § 1): the search joins the projection in its own statement".

**F6 — minor — Down drops `pg_trgm` whoever created it.**
`AddSearchTrigramIndexes.cs:49`. If a DBA pre-created the extension (the Q-4 fallback) Down either removes it from under other users or fails for a non-owner. Fix: leave the extension in Down with a comment, or drop it only when this migration created it.

**F7 — nit — stale test names in comments:** `SearchIndexTests` (`SearchReadModel.cs:19`, migration `:14`) and `SimilarityThresholdTests` (`Mustask.Domain/Search/SimilarityRule.cs:6`); the class is `SearchDatabaseTests`.

**F8 — nit — two copies of the fold.** `SearchQueryText.Fold` (`SearchQueryText.cs:82-112`) re-implements `DirectorySearchText.Normalize`, which writes `search_name`/`search_email`; a drift silently breaks the users group. Call the one or add a parity test.

**F9 — nit — `SearchEndpoints.cs:33` declares 403**, which the contract does not list and no member can get (every role holds the organization read). Drop it or list it.

### QA minors, weighed

- **D-1 (p95 over 300 ms locally):** not a code defect and not for this loop: Q-5 gave the human the comparison at gate-3. But the numbers must first include F1's queries; and the measured scope (read model only, no handler, `AccessSet`, HTTP) should be stated beside them. Leave as debt: measure end to end on the stand/CI; ADR-0044's revisit trigger (sight in SQL) applies if it holds above 300.
- **D-2:** fix before gate-3, as F2 (major).
- **D-3:** fix before gate-3, as F3 (major, prose only).

### Behavioural claims checked

- plan § 2.2 candidates exclude deactivated/deleted, sub-areas of deactivated projects → `SearchReadModel.cs:37-57`.
- plan § 2.3 one statement per group, visible ids only → `SearchReadModel.cs:107-123,173-207`.
- plan § 2.3 tiers, similar ordered by similarity → `SearchReadModel.cs:233-247`.
- plan § 2.3 tier-5 rule → `SimilarityRule.cs:24-38`; handler `SearchStructureHandler.cs:56`.
- plan § 2.4 limits 5/20, `hasMore` false on expansion, group order → `SearchStructureHandler.cs:47-70`, `SearchGroupKind.cs:9-13`.
- plan § 2.5 statement count → `SearchDatabaseTests.cs:46-66` (read model level; access-set statements are feat-041's).
- plan § 2.6 / ADR-0044 § 5 no text in logs → no log call in the slice; `SearchPrivacyAndLimitEndpointTests.cs:70-110`. ("length and duration go to the log": nothing is logged at all — harmless.)
- plan § 3 2-char query within targets, checked by the volume test → F1.
- plan § 4 / backend.md predicate text pinned to the index → F4.
- plan § 4 reversible, applied twice → `MigrationsTests.cs:46,60`.
- plan § 5 / ADR-0044 § 4 limiter numbers, independence → `SearchReadLimiter.cs:16-24`; `SearchReadLimiterTests.cs:150`.
- plan impl. notes: users SQL under UserDirectory "so A.8 holds" → F5; Down/Up covered by generic tests → `MigrationsTests.cs:60` (the parenthetical names other tests; the claim holds).
- api-contract row fields, null `color` → F3; 404 for non-member/instance admin → `RequiresOrganizationPermission`, `SearchStructureEndpointTests`; body never echoes `query` → `SearchStructureValidator.cs`, privacy test.
- ADR-0044 § 1 sole DB precondition `pg_trgm` → F2.
- qa.md "no object kind can have a null colour" → `20261113081620_TeamsInDepartments.cs:362`, Project/Team non-null.
- status Log: test counts (14 DB tests = 1+1+6+5+1) and volume ranges match qa.md and the test file.
- Direction 2: behaviours changed — a new `POST …/search` route, a second read limiter on the pool, `pg_trgm`/`search_fold`, a consumer reading `directory_users`. Grepped `pg_trgm`, `search_fold`, `organizations/{organizationId}/search`, `AggregateReadLimiter`, "Consumers inject" over `docs/`, `.claude/`, the AGENTS.md files. Hits: ADR-0036 § 5 "40 of 100 leaves 60" — now 36, stated by ADR-0044 § 4; ADR-0036 not edited (README rule 3), no finding. doc-015 analysis "add pg_trgm through an ADR" — answered by ADR-0044, historical. mod-014 A.8 → F5. `docs/deploy/vm.md` has no database precondition → F2. No other hits; frontend docs not swept (no frontend change).

### Verdict

**changes-requested** — majors 3 (F1, F2, F3), minors 3 (F4-F6), nits 3 (F7-F9). F3 is prose only; F2 has an orchestrator part (the DevOps `datctype` answer) that the developer's fix does not wait for. Size L: one delta read follows the fix; minors stay debts unless folded into the same lines.
