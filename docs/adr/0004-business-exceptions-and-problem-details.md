# ADR-0004: Business errors are exceptions, mapping to ProblemDetails is centralized

- Status: accepted (2026-08-23, the human in chat: "approve both" —
  together with ADR-0005; before that, from 2026-08-21 it stood as
  proposed while the code was already merged); **partially superseded by
  ADR-0006** (the localizable error contract task) — only
  in the part where "`MustaskException.Message` goes to the client verbatim as
  the text to display" and the shape of `ValidationException.Errors`
  (`IReadOnlyDictionary<string, string[]>`). The choice of exceptions over
  `Result` and the mapping through three `IExceptionHandler`s are not
  overturned by this; the document below has not been substantively
  edited (`docs/adr/README.md`, item 3).
- Date: 2026-08-21
- Context: the global error handling task — the unified error handling
  mechanism (stage 4, backend); the task is set before the
  first slice, so that every subsequent slice is written to an already
  fixed pattern.

## Problem

There is no error handling at all in the backend code: no base exception
type, no `IExceptionHandler`, no `AddProblemDetails()`. The only handling
is `UseDeveloperExceptionPage()`, and only in Development; in prod any
exception today returns an empty 500 with no body and no `traceId`.

At the same time, the current wording of `docs/conventions/backend.md`
(`## API`) requires the exact opposite of what needs to be decided here:
"business errors are a typed Result from the handler, not exceptions for
control flow." The human, in setting the task, explicitly decided:
business errors are thrown as exceptions, the `Result` type is not
introduced at all. This decision is a direct deviation from the
convention, so an ADR is mandatory (per the rules of
`docs/adr/README.md`), and the convention itself is rewritten by this
same change, with a reference to it.

## Options considered

### Option 1: `MustaskException` (a hierarchy) + `IExceptionHandler` → ProblemDetails

- Pros: the slice's handler is not required to explicitly propagate the
  error type through the entire call chain (Domain → Application → Api) —
  the exception finds its own way up without intermediate
  `if (result.IsFailure) return …` at every level; domain methods stay
  simple: `order.Submit(clock.UtcNow)` either successfully changes state
  or throws — there's no need to wrap every domain method in
  `Result<T>`; mapping to an HTTP response lives in exactly one place
  (`Mustask.Api/ErrorHandling`), a new kind of error requires no changes in
  every endpoint — a new `MustaskException` descendant is picked up
  automatically via `StatusCode`, with no switch by type.
- Cons (honestly, not glossing over them):
  - **hidden control flow** — looking at the signature
    `Task<OrderResponse> Handle(...)`, it's impossible to tell which
    errors the method can return without reading its body and the bodies
    of everything it calls;
  - **the cost of throw on hot paths** — exceptions in .NET are more
    expensive than a plain `return` (collecting a stack trace, unwinding
    the stack); for operations where a business error is expected and
    frequent (for example, "limit exhausted" in a high-load scenario),
    this can be noticeable;
  - **failure modes are not visible in the method signature** — the
    compiler will not force the caller to handle the error (unlike
    `Result<T>`, where `IsFailure`/`IsSuccess` are explicit and often
    enforced by analyzers); a missed `catch` is not a compile error but a
    silent bug that surfaces in prod as a 500 via `GlobalExceptionHandler`
    instead of a meaningful response.

### Option 2: a typed `Result` (what the current convention requires)

- Pros: failure modes are part of the method signature, the compiler
  helps avoid forgetting to check `IsFailure`; an error is an ordinary
  value, throw is not used at all, there's no exception overhead even on
  hot paths; easier to test — `Assert.True(result.IsFailure)` with no
  `Assert.Throws`.
- Cons: every level (Domain → Application → Api) must explicitly
  propagate the `Result` further — a domain method calling two other
  domain methods, each of which can return an error, accumulates early
  `return`s/combinators (`Bind`/`Map`) at every step; there is no
  `Result` library of our own in the project and it would have to be
  written and maintained (or a NuGet package like `FluentResults` pulled
  in — a new dependency with long-term consequences across every layer);
  two ways to report an error are worse than one, if somewhere new code
  starts by mistake to both throw and return `Result` interchangeably —
  the boundary of "this method doesn't do that" stops being obvious
  without review discipline.

### Option 3: a hybrid (Result inside Domain/Application, exceptions only at the Api boundary)

- Pros: combines the strengths of both approaches — the internal logic is
  explicit and without the cost of throw, while Api gets a single catch
  point.
- Cons: requires converting `Result` → exception at the slice boundary
  (one more step that's easy to forget or do differently across slices);
  effectively two different error-reporting mechanisms coexist in the
  codebase at the same time (Result inside, exception outside) — harder
  to explain to a new developer where to use which than a single
  consistent approach in either option 1 or 2.

## Decision

Option 1 — a decision of the human, fixed before work began. The `Result`
type is not introduced in the project at all.

`Mustask.Shared.Exceptions.MustaskException` (abstract, `Mustask.Shared` — see
ADR-0003) and its descendants: `NotFoundException` (404),
`ConflictException` (409), `ValidationException` (400,
`IReadOnlyDictionary<string, string[]> Errors`), `ForbiddenException`
(403), `UnauthorizedException` (401). Domain and Application throw them
directly at the moment a violation is detected — no intermediate wrapping
in `Result` is needed.

`Mustask.Api/ErrorHandling` — four `IExceptionHandler`s, registered in the
order `cancel → business → malformed → global`:

| Handler | What it does |
|---|---|
| `OperationCanceledExceptionHandler` | dropped client → 499, `LogInformation` |
| `BusinessExceptionHandler` | `MustaskException` → `ProblemDetails`/`ValidationProblemDetails`, status from `ex.StatusCode` (not a switch by type), `LogWarning` with no stack trace |
| `MalformedRequestExceptionHandler` | `BadHttpRequestException` (a `[AsParameters]`/route/query binding failure — not an `MustaskException`, the framework throws it itself) → its own `StatusCode` (usually 400, sometimes 413/415), `LogWarning` with no client-supplied message in the log template (CWE-117) |
| `GlobalExceptionHandler` | everything else → 500, `LogError` with the full stack trace, exception details — Development only |

**[CHANGE vs. the original decision]** `MalformedRequestExceptionHandler`
was added later: without it, a `BadHttpRequestException` from a failed
query-parameter binding (for example `?pageSize=abc`) fell into
`GlobalExceptionHandler`, which unconditionally forces a 500 for anything
that is not an `MustaskException`/`OperationCanceledException` — overriding
the exception's own correct status. The fix is platform-wide (it concerns
any endpoint with `[AsParameters]`), so it is recorded here, not only in a
class comment.

`docs/conventions/backend.md`, the `## API` section: the line about "a
typed Result … not exceptions for control flow" is replaced with the
rule "business errors are `MustaskException` descendants, centralized mapping
in `Mustask.Api/ErrorHandling`", with a reference to this ADR, plus a table of
subclass → HTTP status and the code format `<module>.<reason>`.

## Consequences

- **What gets simpler:** domain methods return either a successful result
  or nothing (void/entity) and simply throw on an invariant violation —
  there's no need to design and carry a `Result<T>` type through every
  layer for every use case; adding a new kind of error requires no
  changes in `Mustask.Api` — only a new class in `Mustask.Shared.Exceptions`.
- **What gets harder:** the handler's/domain method's signature no longer
  says which errors it can return — this is compensated for by
  documentation discipline: **every slice lists its error codes
  (`<module>.<reason>`) in its own `api-contract.md`** and puts
  `.ProducesProblem(...)` (or the equivalent) on the endpoint in
  `Mustask.Api`, so that failure modes are visible at least in the API
  contract, if not in the C# method signature. Slice review must check
  that the list of error codes in `api-contract.md` has not drifted from
  what the handler actually throws.
- The cost of throw on hot paths is accepted deliberately: none of MUSTASK's
  current use cases is a high-load real-time scenario where a throw on
  every other request would be noticeable; if one appears, a local
  exception to the general rule can be considered for that specific hot
  path — without changing the rule for the whole project.
- `docs/conventions/backend.md` is changed by this ADR — new slices are
  written to the updated rule already, no parallel support for `Result`
  is intended.
- The decision is worth revisiting if a proven hot path appears where a
  throw on a business error measurably hurts performance — then a
  targeted exception or a partial move to option 3 for a specific module
  is discussed, not a rollback of the whole decision.
