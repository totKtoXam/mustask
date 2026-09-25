# US-4: Every work item carries a stable, user-visible identifier

- story: US-4
- depends-on: US-1
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As an **organization member**, I want **every work item to have a short identifier I
can say out loud, paste into a chat and type into a search box, and that
tells me the project and the type at a glance**, so that **we can refer to a
piece of work without quoting its title**.

## Acceptance criteria

The scheme is W-6 (spec BR-M3-5): `{ProjectKey}-{WorkItemKey}-{Sequence}` —
the project's key, the type's key and a number from one sequence per project
shared by all five types. The project key is set by the organization
administrator when the project is created — 2..10 Latin capitals and
digits, starting with a letter, unique within the organization (deactivated
projects included), never changed (W-10; doc-006 BR-M1-1). The type keys are
fixed in doc-007's seed: `EPIC` Эпик, `FEAT` Фича, `US` Пользовательская
история, `TASK` Задача, `BUG` Баг (W-11). The examples use project key `MSK`.
How the number is generated is `mod-003-work-item-core`'s decision.

```gherkin
Scenario: An identifier is issued at creation and shown everywhere
  Given a member creates a «Баг» in project «Мастаск» with key MSK
    (US-1)
  When the work item is saved
  Then its identifier has the form MSK-BUG-<number>, e.g. MSK-BUG-42
  And that identifier is shown on the form header and in the work item list
    (screenshots 05, 08) — and on the board card once `mod-006` builds the
    card (M4, `doc-003`)

Scenario: One sequence per project is shared by all types
  Given project MSK whose last issued identifier is MSK-BUG-41
  When a member creates a «Задача» and then a «Пользовательская история»
  Then they receive numbers distinct from each other and from every number
    issued before in MSK, e.g. MSK-TASK-42 and MSK-US-43
  And no two work items of MSK, whatever their types, ever share a number —
    the number alone identifies the item within its project (W-6)

Scenario: Each project has its own sequence
  Given two projects of one organization with keys MSK and OPS
  When work items are created in both
  Then each project numbers its own items independently, so MSK-TASK-7 and
    OPS-TASK-7 may both exist
  And they are two different work items, told apart by the project key

Scenario: The type key follows the item's type
  Given one work item of each of the five types in project MSK
  When their identifiers are shown
  Then they read MSK-EPIC-<n>, MSK-FEAT-<n>, MSK-US-<n>, MSK-TASK-<n> and
    MSK-BUG-<n> — the same key on every item of a type (W-11)

Scenario: The identifier never changes
  Given an existing work item
  When its title, state, area, assignee or other fields change, and when it
    is deleted and restored (US-10)
  Then its identifier is the same before and after every one of those
    operations (BR-M3-5)

Scenario: An identifier is never reused
  Given a work item that an organization administrator purged permanently
    (US-10)
  When further work items are created in the same project afterwards
  Then none of them receives the purged item's number, under any type key
    (BR-M3-5)
  And a gap in the sequence is normal and is not reported as an error

Scenario: A failed creation promises nothing about consecutive numbers
  Given a create attempt refused by validation (US-1)
  When the member corrects the title and saves successfully
  Then the item is created — and whether its number is consecutive is not
    a requirement: gaps are allowed by BR-M3-5

Scenario: The identifier finds the item inside its project
  Given a work item's identifier
  When a member types it into the work item list's keyword filter
    (US-11)
  Then that one work item is found

Scenario: An address names the organization, then the identifier
  Given organizations «Орг А» and «Орг Б», each with a project keyed MSK and
    an item MSK-BUG-42
  When a member of «Орг А» opens the address of «Орг А»'s MSK-BUG-42
  Then the address carries «Орг А»'s internal id, and the identifier is
    resolved inside «Орг А» only — it opens «Орг А»'s item, never «Орг Б»'s
    (doc-010 D-8)
  And a user who is not a member of «Орг А» opening that address is answered
    "not found", even though their own organization has an MSK-BUG-42
    (doc-001 BR-5)
```

Not decided here: what a retyped or moved item's identifier becomes — both
operations are release 2 (spec § Out of scope). Validating the project key
itself — its format, uniqueness and immutability — is tested with project
creation in `mod-002` (doc-006 US-5), not here.
