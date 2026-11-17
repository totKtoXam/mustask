# ADR-0044: Match the quick search by pg_trgm substring with a similarity tier, build the full search page on Postgres full-text, and limit searches per user and per process

- Status: proposed
- Date: 2026-11-17
- Context: `feat-071-search-structure-users-read` ([plan](../features/feat-071-search-structure-users-read/plan-backend.md)), owed by `feat-065-global-search` (OQ-2, decided at its gate-1 2026-11-13: "one ADR covers both"; [spec](../features/feat-065-global-search/spec.md) BR-GS-3, -7, -17, -18; [design](../features/feat-065-global-search/design.md) D-7). **Departs from ADR-0015's note** that "full-text search … a new ADR" is owed; **does not amend ADR-0036** (its limiter is kept, this one is added beside it). Accepted only by the human (`docs/adr/README.md`, rule 2).

## Problem

The palette must find a name, a key, an identifier or a title from a few typed characters, inside the caller's sight, in an organization with 100 000 work items, 1 000 areas, 200 projects, 200 teams and 2 000 members, at p95 ≤ 300 ms, and tolerate a near-miss («логстика»). The full search page must find work items by the words of their descriptions in their stemmed forms. ADR-0015 left both to a future ADR. A search is also the hottest read the API will have (one per typing pause per user), so the database pool needs a ceiling for it.

## Options considered

### Option 1: pg_trgm for the quick search, full-text for the page

- Pros: trigram GIN serves substring and prefix on partial words, on `MSK-BUG-42` and on mixed Russian/English text without a language configuration; the same extension gives a similarity tier; full-text stems words, which the page wants and the palette must not do (BR-GS-3).
- Cons: one extension to install; two index families.

### Option 2: tsvector everywhere

- Pros: one family; ranking by relevance.
- Cons: breaks on half-typed words and identifiers, needs a language per row, and its stemming is the wrong semantics for a type-ahead (a typed «опла» must match «оплата» by prefix, not by stem).

### Option 3: `LIKE` scan, no extension

- Pros: nothing to install.
- Cons: 100 000 titles scanned on every keystroke; fails the p95 target as organizations grow.

## Decision

Option 1, and the limiter.

1. **Quick search (palette): `pg_trgm`.** A migration creates the extension, an `IMMUTABLE` function `search_fold(text)` (`lower`, ё→е) and GIN `gin_trgm_ops` indexes on `search_fold(name)` of areas, teams and projects, `search_fold(key)` of projects, and on the user projection's folded `search_name` and `search_email`. The match is a substring `LIKE '%q%'` over the folded text with `\`, `%`, `_` escaped (every character literal, BR-GS-3), ranked in five tiers (exact, prefix, word prefix, contains, similar; BR-GS-8). The index predicate and the read model use the same `search_fold` expression; a test pins it, since a predicate that differs silently loses the index. The indexes are raw SQL in one migration owned by the search slice: they touch the tables of Organizations and UserDirectory additively (one database, one `DbContext`, no column or write path changes), and the cost is write amplification on small tables.
2. **Typo tier.** Tier 5 is `word_similarity(q, text) ≥ 0.5` through the `<%` operator (so the GIN index serves it), for queries of at least 4 characters, only to fill a group below its limit, never displacing a tier 1-4 row, never on identifiers (BR-GS-17). 0.5 is the starting value, the prototype's; it lives in one constant and is pinned by a table of near-misses and non-matches, so changing it is a code change with a test change, not an ADR amendment.
3. **Full search page: Postgres full-text (the plan for `feat-NNN-search-description-index` and `-full-page`; the technique is decided here, the details are those lines' plan).** The description's plain text (derived from the sanitized HTML of ADR-0039 in the description's own write, backfilled for existing items) is stored in a column of the work item; two generated `tsvector` columns, `russian` and `english` configurations, each with a GIN index; the query's words are all required in either configuration (BR-GS-18); relevance by `ts_rank_cd`; identifier forms and titles stay on the trigram path of § 1; snippets are built in the application from the plain text for the 20 rows of a page and sent as text plus ranges (design D-6), never `ts_headline` markup. Storage estimate, to be measured by the description-index line and reported in its plan: of the order of 0.3-0.5 GB per 100 000 items of 1 500 characters (text plus two indexes). Nothing about the full-text indexes is built by `feat-071`.
4. **Limiter.** A concurrency limiter on the search read, shaped as ADR-0036's: per user (Keycloak subject) **3** in flight, queue **3**; per API process **24** in flight, queue **12**; wait at most **1 s** by the injected `TimeProvider`; refusal is 429 `http.too_many_requests` with `Retry-After: 1`. **Independent of ADR-0036's limiter**: its own instance and permits, so neither read class starves the other. The two together reach 40 + 24 = 64 of Npgsql's default 100 connections at the extreme, leaving 36. The numbers are constants of `SearchReadLimiter`; changing them is a code change, changing the key or scope is an amendment (as ADR-0036). Reason for tighter numbers than the group reads' (6 / 40): a search is short and the 200 ms debounce makes it one per pause.
5. **Privacy.** The query travels in a POST body; the application logs and traces hold only its length and the timing (BR-GS-10); a test captures the log of a request, the validation and the 429 paths and asserts the text is absent.

## Consequences

- Easier: type-ahead on partial words, identifiers and mixed languages; a near-miss tier at no new infrastructure; one search table family per concern; a defined, retryable refusal for a hot read.
- Harder: the database must allow `pg_trgm` (a trusted extension for the owner on PostgreSQL 13+; DevOps confirm for the production host before the MR); expression indexes are outside the EF model snapshot and are guarded by tests, not by the model; two index families once the full page lands; the limiter's state is per process.
- Locked in: folding is `lower` + ё→е on both sides; the substring semantics of the palette (BR-GS-3); the sight sets are applied before matching (ADR-0040 § 4), never after the limit.
- Owed: error-catalogue module `search` (`contracts/errors/search.json`, `docs/error-codes.md`); a pointer in `docs/conventions/backend.md` to this ADR when accepted; the ADR-0015 note ("full-text search … a new ADR") is answered here, its envelope stays for lists.
- Revisit if: the volume test cannot hold p95 with the sight arrays (then re-express sight in SQL), if the production host refuses the extension (then a `LIKE` scan on the structure tables only, which stay in the low thousands), or if a second API replica needs cross-process limiting (an amendment on the limiter's scope).
