# ADR-0021: Store a dictionary as an entry plus a typed translation table per dictionary, and resolve the language on the server

- Status: proposed
- Date: 2026-09-09
- Context: the dictionaries feature — the first MUSTASK module that owns
  data. Supersedes
  [ADR-0020](0020-drop-storage-until-first-data-module.md); deviates
  knowingly from one option rejected in
  [ADR-0006](0006-error-texts-live-in-shared-catalog.md)

## Current state (2026-09-22)

This ADR is the pattern for **future** localized dictionaries. Today no
dictionary implements it: the only dictionary is **languages**, which by
design has no translation table (an endonym is not translated, Decision 1).
The dictionary whose translations first exercised the pattern was removed
together with its translation table, its completeness check and its seed
texts. Concretely, nothing in the code currently implements:

- a per-dictionary `<entries>_translations` table (Decision 1);
- server-side resolution of a translated name with fallback to the default
  language (Decision 3) — what remains is the request-language negotiation
  itself: `Accept-Language` in, `Content-Language` out, via
  `IRequestLanguageResolver`;
- the rule that blocks changing the default language until every active
  entry is translated into it. Changing the default language today only
  requires the language to be active
  (`dictionaries.languages.default_must_be_active`).

The seeding rule of Decision 5 still applies to the languages seed
(`contracts/dictionaries/languages.json`). The first dictionary with
translatable fields brings the rest back as described below.

## Problem

MUSTASK has to speak more than one language (Russian and Kazakh on par,
English seeded), and its reference data — languages today, work item types,
priorities and statuses next — has to carry a name in every language of
the system. Without a dictionary such a value is a bare integer or a
free-typed string: the registries show `1`, and a typo silently becomes
data.

Three decisions have to be made once, for every dictionary that will ever
exist, and they are hard to reverse later:

1. **How translations are stored.** Two languages are live today (`ru`,
   `kk`), a third (`en`) is seeded, and adding a fourth must not be a
   release of the data schema.
2. **Who picks the text** — the server by `Accept-Language`, or the client
   from all translations. ADR-0006 already answered this question for error
   texts, and answered it the other way.
3. **Whether MUSTASK keeps a database at all.** ADR-0020 removed the store
   "until the first module with data appears". This is that module, and it
   merges before the identity teardown of ADR-0018 (D-4).

Forces: volumes are tiny and will stay tiny (≤ 10 languages, ≤ 20 entries
in a typical dictionary, ≤ 300 translations); a dictionary is read by
almost every screen, so a read must be one or two indexed queries; some
dictionaries are closed (their composition changes only with a release —
e.g. a work item type the code branches on), others are open (languages —
edited by an administrator).

## Options considered

### Option 1: a column per language (`name_ru`, `name_kk`, …)

- Pros: the simplest read — one row, no join; trivially projected.
- Cons: a new language is a migration of every dictionary table and an edit
  to every projection; the number of columns grows as fields × languages
  (name, short designation, description × 3 = nine columns for one
  dictionary); "which languages exist" lives in the DDL instead of the data.
  Rejected by the human before the task started (2026-09-08).

### Option 2: a JSONB column with all translations on the entry row

- Pros: no join, a new language changes nothing structurally.
- Cons: no foreign key to the languages dictionary — a typo in a language
  tag is data, not an error; per-field constraints (lengths, "the name in
  the default language is not empty") become application-only; filtering and
  ordering by the resolved name turn into expression indexes over JSON. A
  registry that has to prove "every active entry is translated" should be
  able to prove it with SQL.

### Option 3: one shared translations table for every dictionary (EAV)

- Pros: one table, one set of conventions, one repository.
- Cons: the row loses its type — `entity_type` + `entity_id` + `field_name`
  + `value` cannot be constrained by a foreign key to the entry, so nothing
  stops an orphan or a misspelled field name; every read needs a pivot from
  rows to fields; a "name / short designation / description" triple of one
  dictionary and a "name / genitive case" of another become the same
  shapeless bag. Rejected by the human (same decision).

### Option 4: entry + a typed translation table per dictionary (chosen)

Each dictionary is two tables: `<entries>` and `<entries>_translations`, the
latter with a real column per translatable field, a composite primary key
(entry, language) and foreign keys to both sides.

- Pros: the pair "entry + language" is unique by the primary key, not by
  discipline; an orphan translation and an unknown language are impossible
  by foreign key; lengths are ordinary column constraints, while "not empty"
  is an application-level invariant the aggregate enforces (its
  `SetActivityAndOrder`/`SetTranslation` methods), not a column one — whether
  a field may be empty depends on the entry's activity and on which language
  is currently the default, neither of which a `CHECK` can see; the read is one join, and both filtering and ordering by the
  resolved name are ordinary SQL; adding a language inserts rows, it changes
  no DDL.
- Cons: a new dictionary is two tables and two entity configurations rather
  than one row in a registry — the shape has to be repeated, and repetition
  drifts unless something checks it; a read of a dictionary always joins.

## Decision

**Option 4**, plus the four rulings below. Together they are the pattern
every later dictionary (work item types, priorities, statuses and the like)
follows without a new ADR.

### 1. The shape

An entry has a surrogate `id uuid` primary key, a business `code`, an
`is_active` flag, an explicit `sort_order`, and the moment and author of the
last change. A translation row is `(entry_id, language_id)` as a composite
primary key plus one column per translatable field. Both tables live under
the dictionary's own module; there is no shared base entity and no
inheritance — EF Core inheritance mapping buys nothing here and costs a
mapping surprise per dictionary. The pattern is a **shape that is copied**,
guarded by a model test rather than by a base class.

The languages dictionary is the one entry type with no translation table:
a language carries an **endonym** — its own name in itself (`Қазақша`) —
which is by definition not translated.

**The `code` is unique, by a model convention.** A model-finalizing
convention creates the unique index on every entity's `Code` property, the
same way `xmin` is added by ADR-0010's convention; a per-entity
`HasIndex(...).IsUnique()` is not written and cannot be forgotten. The human
explicitly declined a separate ADR for this ("if you implement variant 3,
no ADR is needed", 2026-09-08): it is recorded as a paragraph
in `docs/conventions/backend.md` plus a model test.

### 2. Closed dictionaries have an enum code, open ones a string

A **closed** dictionary has its composition fixed in code: the code is an
`enum` in `Mustask.Domain`, stored in the database and shown in the API **as
a string** (a `CHECK` constraint listing the values), following the "named
values" rule of `docs/conventions/backend.md` — a number would turn
reordering the enum's members into silent data corruption.

If the numeric value of a closed dictionary ever becomes an external
contract (another system receives the integer), it is kept as an explicit,
separately indexed column with a business meaning of its own — never the
enum's ordinal — and it is append-only: never changed, never reused after a
deactivation. A snapshot test of the code ↔ number pairs then fails on any
edit. Reordering the enum therefore corrupts nothing; the number is data,
not an ordinal.

An **open** dictionary (languages) has a `string` code entered by an
administrator, with the values known at seed time listed in a constants
class next to the entity. Creating an entry of a closed dictionary through
the API is not merely unauthorized — the route does not exist.

The enum, the entity and the seed reference each other with XML `cref`
documentation, so that whoever finds one of the three finds the other two.

### 3. The server resolves the language, and this deviates from ADR-0006

The client sends `Accept-Language`; the server returns **one resolved text
per record**, and returns all translations at once only to the
administration operations that exist to edit them. Resolution is
**whole-translation, not per-field** (D-17): if a translation into the
requested language exists it is returned as it is, an empty optional field
included; only the complete absence of a translation falls back to the
default language, and the absence of any translation at all yields the
entry's code in place of the name.

ADR-0006 rejected exactly this ("the server knows the user's language and
picks the text itself") for error texts, on the grounds that the language is
a deliberate profile choice and that a `.resx` catalog would split
translation into two processes. **The deviation is knowing** (D-2) and
narrow: error texts are a fixed catalog shipped with the release and are
displayed by a client that already holds every string; dictionary
translations are *data* edited by an administrator without a release, with
an arbitrary number of languages. Sending every translation of every row so
the client can pick one would multiply a registry's payload by the number of
languages to use one of them, and would push the fallback rule — a business
rule with a spec criterion of its own — into every consumer, including
every future frontend. ADR-0006 is **not** repealed: error
texts keep coming from `contracts/errors/*.json` by code, unchanged.

### 4. The store stays; ADR-0020 is superseded

ADR-0020 removed `MustaskDbContext`, the migrations, the EF harness, the
`postgres` resource in AppHost and Testcontainers "until the first module
with data appears" and named its own revisit condition: "any module that
stores data". This is that module, and it lands first (D-4): it merges
before the identity teardown, which then removes identity **only**, leaving
the store, the context and the migrations in place. ADR-0020 is marked `superseded by ADR-0021`; nothing it left in force
(ADR-0002, 0009, 0010, 0011, 0012, 0013) is touched — those decisions simply
regain their subject.

### 5. Seed text lives in `contracts/`, not in C#

The composition (and any external numbers) of a closed dictionary are the
enum. The **texts** of the initial entries — for example the endonyms of
`ru`, `kk`, `en` — live in
`contracts/dictionaries/*.json`, next to `contracts/errors/*.json` and for
the same reason (ADR-0006, option 4): they are readable and editable by an
analyst who cannot build the backend, and a completeness check can be
blocking. The seeder inserts what is missing at the **entry** level: it
creates an entry (with its shipped translations) only if the entry does not
exist yet, and never revisits one that does — so an administrator's edit or
deletion survives every restart, the same discipline as `IdentitySeeder`. One
consequence: a language added after an entry was already seeded never
receives that entry's shipped translation into it; an administrator types
it.

This departs from the line "reference data — seeded in migrations" in
`docs/conventions/backend.md`. The reason is that these rows are not fixed
reference data but the initial state of data the administrator owns from the
first day: a migration that inserted them would either be a one-off (and
then a fresh environment created after a text is corrected gets the old
text) or would have to update rows a human may have already edited.

## Consequences

- **What gets simpler:** a new language is an insert, not a migration; a new
  dictionary is a copy of a documented two-table shape; "is every active
  entry translated into the language we are about to make default" is a
  single SQL question, which is what makes a "no default language without
  full translation" refusal implementable at all; a list read stays one join and one query.
- **What gets harder:** every list read of a dictionary must resolve the
  request language before it projects, and the resolved-name expression is
  what ordering and filtering are built on — a slice that forgets it will
  order by the code and nobody will notice in a Russian-only environment.
  The mitigation is that resolution lives in one place per module, not per
  slice.
- **An external number, once published, is a contract**, and the snapshot
  test is the only thing that says so at edit time.
- **Two catalogs now live in `contracts/`** — errors and dictionary seeds.
  They must not grow into a general-purpose configuration directory: what
  belongs there is text that has to be readable without building the system
  and checkable by a blocking test.
- **Server-side caching is deliberately absent.** Volumes are tiny and the
  client caches per language (a non-functional requirement of the spec); a server cache with
  invalidation is a backlog line to be opened only if measurements ask for
  it. Caching before measuring would also make "an administrator's edit is
  visible on the next screen load" untrue in a way nobody would notice.
- **When to revisit:** if a dictionary appears whose composition is measured
  in thousands of rows (settlements, customers), the per-dictionary
  table shape holds but the "read the whole dictionary into a select" habit
  does not; and if translation ever moves into a professional TMS process,
  the seed files become derived from it rather than primary — the same
  condition ADR-0006 records for error texts.
