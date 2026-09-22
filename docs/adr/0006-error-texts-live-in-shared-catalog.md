# ADR-0006: Error texts live in a shared catalog, not in server code

- Status: accepted (2026-08-24, confirmed by the human in chat at gate-3)
- Date: 2026-08-23
- Context: the localizable error contract task — the server stops being
  the source of the displayed error text (Тр-10,
  Тр-22); changes two provisions of ADR-0004.

## Problem

MUSTASK must speak to the user in Kazakh on par with Russian (a human
decision made before the task started). Today `MustaskException.Message` goes to the client
verbatim as `detail` (ADR-0004) — this is the only Russian text, no other
language versions exist, and there is nowhere to store them next to the
throw site without duplicating formatting logic on both server and
client.

The decision "the server knows the user's language and picks the text
itself" was already rejected at stage 1 (`## Notes`, answer 1): language
is a deliberate choice in the profile, needed only for server-side
document rendering, which this task does not have. So the text for the
user must be substituted by the **client** — and for that the server
needs to return not ready-made text but a **stable code** plus **raw
parameters**, while the text itself (in both languages) must live
somewhere as the single source — otherwise the Russian version on the
server and the translations on the client will inevitably drift apart.

An additional force: Тр-8 changes the shape of
`ValidationException.Errors` — a field's error reason becomes an ordinary
ADR-0005 registry code rather than text (`IReadOnlyDictionary<string,
string[]>` in ADR-0004). Both changes — `Message` as diagnostics instead
of display text, and the shape of `Errors` — affect the same class
(`MustaskException` and its descendants) and are resolved by one
architectural step, so they are recorded as one ADR.

## Options considered

### Option 1: server-side `.resx`, the server knows the user's language

- Pros: a familiar .NET mechanism, the compiler checks that the resource
  exists.
- Cons: requires a language field in the user profile and
  `Accept-Language`/culture selection on every request — both paths were
  rejected by the human before the task started (`## Notes`, answer 1,
  item 4). Splits translation into two processes — one for UI strings on
  the client, another for `.resx` on the server — with a risk of a
  different tone and drifting terminology.

### Option 2: a translation table in the DB

- Pros: texts are edited without a backend release.
- Cons: the error path must not depend on what failed — if Postgres goes
  down the server must still be able to say that it failed, not lose its
  own error text along with the database (`## Notes`, item 4; НФТ-1).
  Rejected by the human before the task started.

### Option 3: the text catalog lives only in the frontend

- Pros: one catalog for all interface strings, including errors; nothing
  is duplicated on the server.
- Cons: a backend developer, an analyst, or a support engineer with no
  access to the frontend repository cannot find out what an error code
  means — an objection from the human raised during stage 1 of the spec
  (see `spec.md`, "Human correction"). Rejected.

### Option 4: the catalog is a shared file resource outside both codebases, embedded into the server as an EmbeddedResource (chosen)

- Pros: a single source of text for both languages and for both sides
  (the server returns `ru` as the fallback `detail`, the reference in
  `docs/` reads the same catalog, a follow-up task connects the same
  catalog to the UI); the catalog is accessible to the human without
  knowing React (Тр-17); the server continues not to depend on the
  filesystem **at request-handling time** — the files are parsed once at
  startup into an immutable in-memory dictionary (НФТ-1, AC-28).
- Cons: two places must consistently reference files outside themselves
  (the server — via `EmbeddedResource`, the frontend — via a relative
  import, decided separately in that follow-up task); requires a blocking
  completeness check, otherwise a code with no translation reaches
  production unnoticed (the risk is removed by Тр-12/Тр-18 and the
  project implementing them, `src/backend/tests/Mustask.Contracts.Tests`).

## Decision

Option 4. The catalog is `contracts/errors/<module>.json`, one file per
module (ADR-0005 already rejected a single codes file as a shared edit
point, the catalog must not bring this problem back through the back
door). The files are linked into `Mustask.Api` as `EmbeddedResource` and
parsed into `ErrorCatalog` once at application startup — there is no
file, no DB, and no network on the request-handling path. Not a single
line of Russian text is left in code: the `detail` of a business error,
the `detail` of a 500, and the `detail` of `UseStatusCodePages` responses
are taken from the catalog by code.

**`Title` is subject to the same rule** (a human decision, `status.md` →
`## Notes`, "Human decisions before implementation", item 1, made after
the stage-4 plan was written, before the code): the `DefaultTitlesByStatus`
table in `ErrorHandlingExtensions` — Russian text in code — would
reproduce for the title the same defect that this decision fixes for
`detail`. The status title gets `ru` and `kk` entries in the catalog (in
the entry for the platform code assigned by status — `http.*`) and falls
under the same blocking completeness check as the error text.

**Changes relative to ADR-0004:**

1. `MustaskException.Message` stops being the "text to display" contract. It
   becomes a technical diagnostic trace (code + parameters, invariant
   culture) — printed everywhere the exception surfaces outside our
   handler, but never shown to the user. The security contract is not
   weakened: only the code and `params` end up in the trace, which
   already go to the client anyway (Тр-5). The displayed text (`detail`)
   is built by the handler from the catalog, using the exception's code
   and parameters.
2. `ValidationException.Errors` changes shape from
   `IReadOnlyDictionary<string, string[]>` (field → texts) to
   `IReadOnlyDictionary<string, IReadOnlyList<FieldError>>` (field → a
   list of reasons, each an ADR-0005 registry code plus its own
   parameters). A field's error reason is not a second family of
   identifiers but an ordinary code: the same format, the same
   requirement for both translations, the same line in the reference,
   the same blocking check (a human decision following requirements
   review, `status.md` → `## Notes`).

**Kazakh texts of platform codes** (a human decision, "Human decisions
before implementation", item 2): they are written by the backend
developer, every entry is marked with a machine-readable flag "requires
proofreading by a native speaker" — the auto-check guarantees the
translation exists and is not equal to the Russian one, but does not
guarantee quality, and for user-facing text that is not enough.
Proofreading is a separate task; the mechanism itself works
immediately, without waiting for a translator.

Recording the deviation from the accepted ADR-0004 follows the rule in
`docs/adr/README.md`, item 3: ADR-0004 is not edited substantively, it
gets a `superseded by ADR-0006` mark only for the two provisions above
(the mapping of business errors to ProblemDetails through three
`IExceptionHandler`s, and the choice of exceptions over `Result`, remain
in force and are not overturned by this ADR).

## Consequences

- **What gets simpler:** translating an error text (both languages) needs
  no backend release — the catalog file is edited; the discrepancy "the
  user saw one thing, the support reference shows another" is impossible
  by construction, because both take the text from the same file; a new
  business code with parameters gets `{name}` substitution with no manual
  formatting at the throw site.
- **What gets harder:** the throw site of a business exception must know
  the closed set of type hints for each parameter (Тр-3) and cannot just
  pass a text — a small but real habit change compared to ADR-0004, where
  `message` was a free-form string. Catalog completeness is a blocking
  CI check, which means a new code with no translation into both
  languages does not reach review, let alone production.
- **Kazakh texts of platform codes, written by a developer rather than a
  translator, require proofreading** — tracked by a flag in the catalog
  and a line in `docs/error-codes.md`; the closing task is a separate
  proofreading task.
- Revisit if a professional translation process with its own file format
  (TMS/XLIFF) appears — then the catalog could become derived from it
  rather than the primary source; moving the catalog to the frontend
  (back to option 3) would be worth considering if a proven need arises
  to keep translation inside the frontend release process rather than
  alongside it.
