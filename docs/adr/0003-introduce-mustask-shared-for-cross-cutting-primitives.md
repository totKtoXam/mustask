# ADR-0003: Introduce Mustask.Shared for cross-cutting primitives with no dependencies

- Status: accepted (2026-08-23, confirmed by the human in chat at gate-3)
- Date: 2026-08-21
- Context: the global error handling task — the unified error handling
  mechanism (stage 4, backend)

## Problem

The business exception hierarchy (`MustaskException` and its descendants, see
ADR-0004) is needed at the same time by the domain (entities throw it on
invariant violations), by Application (use case input validation), and by
Api (centralized mapping to ProblemDetails). At the same time the type
must stay compatible with the current layout of
`docs/conventions/backend.md`, where reusable code goes either into
`Mustask.Domain` or into `Mustask.Application/Abstractions` — the convention has
no dedicated project for "shared with no dependencies".

The requirement on the type is strict: it must not pull in anything,
including ASP.NET Core — otherwise `Mustask.Domain`, the only layer that the
Onion rules allow to reference nothing at all
(`Domain_has_no_nuget_dependencies` in `Mustask.Architecture.Tests`), would
either have to break this rule or would not be able to use the shared
exception type directly.

## Options considered

### Option 1: a new `Mustask.Shared` project

- Pros: zero dependencies by construction — the project is empty, an
  architecture test can formally check this (no `PackageReference`, no
  `ProjectReference`); one explicit layer for what **every** other layer
  needs, not only the domain; creates no illusion that `MustaskException` is
  part of the domain model (it is about the HTTP error contract, not
  business rules).
- Cons: one more project in the solution, a new line in the closed
  `OnionDependencyRules` list (diverges from the convention's current
  layout, where there are exactly four projects:
  Domain/Application/Infrastructure/Api).

### Option 2: put `MustaskException` in `Mustask.Domain`

- Pros: no new project at all; the convention is not touched.
- Cons: the domain is by definition the business rules of a specific
  domain area; a shared technical error contract in it mixes two
  different reasons to change. `Mustask.Application` and `Mustask.Api` would gain
  access to the entire domain model for the sake of one exception type —
  a broader dependency than needed.

### Option 3: `Mustask.Application/Abstractions`

- Pros: the place is already designated by the convention for
  "infrastructure interfaces", requires no new project.
- Cons: the domain **cannot** reference `Mustask.Application` — Onion
  dependencies flow strictly inward (Domain ← Application). And domain
  rules also need to throw `MustaskException` (for example, an invariant
  violation on a status transition) — the option physically does not
  work without reversing the dependency arrow, which breaks Onion
  entirely.

## Decision

Option 1: a new `src/backend/Mustask.Shared` project, namespace
`Mustask.Shared.Exceptions`, with not a single `PackageReference` and no
`ProjectReference` — including no ASP.NET Core. `Mustask.Domain` gets a
single `ProjectReference` to it; the other layers get access to it
transitively and it is also allowed directly in the closed
`OnionDependencyRules` list (`Mustask.Shared` is allowed for every layer,
`Mustask.Shared` itself has an empty list of allowed references).

This is a deviation from the layout of `docs/conventions/backend.md` (it
only lists `Mustask.Domain`/`Mustask.Application`/`Mustask.Infrastructure`/`Mustask.Api`) —
the convention is updated by this same change: the solution structure
block is extended with `Mustask.Shared/`, with a reference to this ADR.

## Consequences

- `Mustask.Architecture.Tests` is extended: `Mustask.Shared` is added to the
  closed `AllowedProjectReferences` list, and the same checks apply to it
  as to `Mustask.Domain` — "no NuGet dependencies" / "no infrastructure
  packages" — a future attempt to add a package to `Mustask.Shared` (for
  example, for validation attributes) breaks the build immediately,
  rather than surfacing at code review.
- `Mustask.Shared` is not a "shared dumping ground": its only current content
  is the `MustaskException` hierarchy (ADR-0004). If a second reason for a
  cross-cutting, dependency-free type appears (for example, a
  `Result`-like primitive — but see ADR-0004, this path was deliberately
  not chosen), the decision on what goes in there is made anew, not
  "since we set it up, throw everything in".
- The decision is worth revisiting if `Mustask.Shared` develops a real need
  for a dependency (for example, serialization attributes) — then the
  project's purity would break and it would have to either be split, or
  the rule in `OnionDependencyRules` changed deliberately, through a new
  ADR.
