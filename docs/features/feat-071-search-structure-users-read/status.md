# feat-071-search-structure-users-read

- type: feature
- size: L
- branch: feat/071-search-structure-users-read
- stage: 07-mr
- epic: platform/ui
- deps: feat-041
- gates: gate-1, gate-3

## Gates

gate-1: approved (by the human in chat, 2026-11-17)
gate-3: approved (by the human in chat, 2026-11-18)

## Plan

Backend plan: [plan-backend.md](plan-backend.md); contract: [api-contract.md](api-contract.md).
One new `Search` module, one `POST /api/organizations/{id}/search`, sight sets first, one statement per group; pg_trgm migration.
Decision record: ADR-0044, accepted with the plan at gate-1 (2026-11-17); Q-1..Q-5 decided as recommended.

## Log

- 2026-11-17 [new-task] task started; session bound to worktree .claude/worktrees/objective-torvalds-200108, branch cut from feat/041-project-visibility (PR #203 not merged yet; origin/main is merged in once it lands). Queue line taken by PR #210.
- 2026-11-17 [new-task] size mismatch: queue has size:M, human confirmed L
- 2026-11-17 [orchestrator] route confirmed by the human in chat: L (thresholds: an ADR for the match technique and the limiter; a new Search slice; pg_trgm and indexes on other modules' tables). Requirements and design are feat-065's (gate-1 and gate-2 approved there 2026-11-13). Route: plan-backend + api-contract + ADR (proposed) → gate-1 here (the human accepts the ADR and the plan) → backend dev → QA → code review → gate-3 → PR. gate-2 has no subject (no UI). Economy mode: one agent per stage, one Opus review; E2E: no (standing answer).
- 2026-11-17 [backend-developer] plan-backend.md, api-contract.md, ADR-0044 (proposed) written; questions Q-1..Q-5 (limiter separate, sight arrays, raw-SQL indexes, pg_trgm on prod host, CI volume margin)
- 2026-11-17 [orchestrator] gate-1 approved by the human in chat: ADR-0044 accepted; Q-1 a separate limiter, Q-2 sight as id arrays from AccessSet, Q-3 raw-SQL expression indexes in the Search migration, Q-4 the human confirmed pg_trgm is available on the production DB host, Q-5 the volume test asserts 2x the spec's p95/p99 and records measured values in qa.md. Stage 4 development started.
- 2026-11-17 [backend-developer] backend built per plan-backend.md, no contract change: Domain/Search rules, pg_trgm migration AddSearchTrigramIndexes (search_fold + six GIN indexes), ISearchReadModel + SearchReadModel, SearchStructure handler/validator, POST /api/organizations/{id}/search with its own SearchReadLimiter, error catalogue search.json. Deviations (in plan-backend.md): users SQL moved into Infrastructure/UserDirectory/SearchUsersStatement.cs (architecture rule); DB tests in one class SearchDatabaseTests. Tests: Domain.Tests Search 62 passed, Application.Tests Features.Search 13 passed, Api.IntegrationTests Features.Search 50 passed, Infrastructure.Tests Search.SearchDatabaseTests 14 passed (statement count, six index pins + EXPLAIN, similarity table, volume), Contracts.Tests 237 passed, Architecture.Tests 82 passed. Volume (doc-012 set, 300 reads): p95 233-474 ms, p99 298-654 ms across runs (asserted at 2x spec: 600/1600).
- 2026-11-17 [qa-engineer] backend QA done: qa.md written, 13 tests added (SearchQaEndpointTests), Api.IntegrationTests Search 63 passed, SearchDatabaseTests 14 passed; blockers 0, majors 0, minors 3 (D-1 p95 volume not demonstrated, D-2 word-prefix regex ctype, D-3 null color omitted) carried to gate-3; E2E: no (standing answer of the human).
- 2026-11-18 [code-reviewer] round 1 (stage 8) over origin/main..420d4a92: changes-requested; open majors 3 (F1 volume test omits 2/3-char and no-match queries, F2 DB ctype precondition supersedes D-2, F3 contract vs DTO on null color = D-3), minors 3, nits 3; D-1 left to gate-3 after F1 re-measure; findings in review.md
- 2026-11-18 [backend-developer] fix round 1 (review F1-F9): F1 volume test now sends 2-char, 3-char, typo and no-match texts and asserts a 1-char text is rejected, re-measured (p50 78.0, p95 225.3, p99 292.7, max 312.0 ms; database read only, no handler/AccessSet/HTTP) in qa.md; F2 search_fold lowers under ru-x-icu, the migration refuses a C/POSIX datctype (test DB: en_US.utf8 / en_US.utf8), DB tests pin the Cyrillic fold and similarity and the guard, vm.md and vm.ru.md state the precondition; F3 api-contract.md null color sentence corrected to the code (contract note: prose only, no behaviour change; the DTO never emits null, color is always present on projects, teams and departments); F4 EXPLAIN predicates built from SearchSql; F5 shared SQL moved to the neutral Search/SearchSql, users statement and backend.md reworded as an accepted exception to mod-014 A.8 (ADR-0044 s1); F6 Down no longer drops pg_trgm; F7 stale test names fixed; F8 SearchQueryText.Fold delegates to DirectorySearchText.Normalize (parity test); F9 undeclared 403 removed. Tests: Domain.Tests Search 62, Application.Tests Features.Search 13, Api.IntegrationTests Features.Search 63, Infrastructure.Tests SearchDatabaseTests + MigrationsTests 26 (16 + 10), Architecture.Tests 82, Contracts.Tests 254, all passed. Still open for the orchestrator: ask DevOps for the production datctype with Q-4.
- 2026-11-18 [code-reviewer] delta read (round 2) over 26915565..59173664: approve; F1-F9 closed, 0 blockers/majors/minors open, nits N1-N3 as debts; owed before MR: DevOps production datctype in status.md (F2); D-1 to the human at gate-3
- 2026-11-18 [orchestrator] gate-3 approved by the human in chat. D-1 accepted as a debt (database-read p95 225 ms after the fix round; the end-to-end p95 is measured later, with the palette on the stand). F2: the human confirmed the production database has a UTF-8 locale (not C/POSIX), so the ctype guard passes there. Debts carried: D-1 end-to-end p95, N1 REINDEX of the search_fold indexes after an ICU major upgrade (runbook line), N2 reset local/stand databases that applied the earlier migration body, N3 the ctype guard may refuse a builtin/ICU-provider database on PostgreSQL 17+.

## Notes

Summary (from the queue line): Global search read for departments, teams, projects, sub-areas and users (feat-065 US-2, US-4): one read per organization under sight, groups, ranking tiers with the typo tier, limit+1 more, group expansion, trigram indexes, POST transport, per-user and per-process limiter, the ADR for match technique and limiter, hidden-object, statement-count and volume tests.
