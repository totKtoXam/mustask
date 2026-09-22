# ADR-0005: Treat error codes as a stable registry — part of the API contract

- Status: accepted (2026-08-23, the human in chat: "approve both")
- Date: 2026-08-23
- Context: a discussion of the policy for named values (the human,
  2026-08-23); builds on ADR-0004, where the error code appeared as the
  `MustaskException.Code` field, but the rules of its lifecycle were not
  fixed.

## Problem

`MustaskException.Code` already goes to the client as `errorCode`, and the
frontend will branch on it: showing different screens, texts, actions.
This makes the error code a **contract**, and a contract must have
lifecycle rules — otherwise renaming a code on the backend silently
breaks the frontend, and reusing a freed-up code changes the meaning of
old logs and metrics.

An additional force — СТ-1 (bilingualism): an error message must be shown
in Russian or Kazakh, by the user's choice, which means the frontend
cannot rely on the `Message` text — only on the code.

## Options considered

### Option 1: a central code catalog as a single file in `Mustask.Shared`

- Pros: uniqueness is visible at a glance; a single diff shows all new
  codes.
- Cons: breaks vertical slice — every slice reaches into a shared file
  (the same ailment `docs/backlog.md` had: a shared edit point for every
  task — guaranteed conflicts); a slice's codes live far from its logic.

### Option 2: codes are declared in the slice, the rules are registry-wide (chosen)

The code is declared where the exception is thrown (as a constant of the
slice or a domain concept), but obeys the registry's rules:

- Pros: slices are independent; the rules are mechanically checkable;
  every slice's `api-contract.md` already lists its codes — a
  distributed registry effectively already exists.
- Cons: uniqueness across slices is not visible in one place — an
  automatic check is needed.

## Decision

Option 2. Registry rules:

1. **Format** — `<module>.<reason>`, Latin script, snake_case:
   `orders.not_found`, `documents.already_signed`. Module = the slice's
   module.
2. **The code is part of the API contract.** Every code is listed in its
   slice's `api-contract.md`; a change to the set of codes is a contract
   change, the marking rule in `status.md` applies.
3. **Stability like task numbers:** a published code is never renamed or
   reused for a different meaning. A retired code is marked as deprecated
   in `api-contract.md`, but stays taken.
4. **Code ≠ message.** The code is a stable English identifier for
   machines; `Message` is text for a human, localizable (СТ-1), free to
   change. The frontend branches only on the code.
5. **Declared as a constant next to the slice's exception**, not as a
   string at the throw site: a string in two places will drift apart
   unnoticed.
6. **Uniqueness is checked mechanically** — the task for an
   analyzer/test is set in the backlog (see Consequences).

## Consequences

- Simpler: the frontend and logs get a stable anchor; message
  bilingualism does not touch contracts; review sees new codes in the
  `api-contract.md` diff.
- Harder: a poorly named code cannot "just be fixed" — only deprecated +
  new. The cost is deliberate, the same as for task numbers.
- Creates an enforcement task (analyzers + uniqueness check) in the
  backlog.
- Revisit if client code generation from OpenAPI appears — then the
  registry could move into the spec as the single source.
