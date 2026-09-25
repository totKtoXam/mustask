# US-1: An organization's process is seeded automatically at creation

- story: US-1
- depends-on: -
- shippable: no — delivered by mod-001-process-seed
- status: pending

As a **user permitted to create organizations** (doc-006 US-2), I want
**every organization I create to receive a complete, ready-to-use process
automatically**, so that **its members can start creating work items without
configuring anything first**.

## Acceptance criteria

```gherkin
Scenario: A newly created organization already has a process
  Given a user permitted to create organizations creates a new organization
  When the organization is created
  Then exactly one Process exists for it
  And the process defines all five built-in types «Эпик», «Фича»,
    «Пользовательская история», «Задача», «Баг» (D-3, D-7, D-12), with the
    keys `EPIC`, `FEAT`, `US`, `TASK`, `BUG` respectively (BR-M2-9)
  And each type has exactly the states listed for it in spec.md § "MVP: the
    seeded process", in the order stated there, each mapped to the category
    stated there, and its first state is its initial state (BR-M2-2)
  And each type is assigned to exactly one of the backlog levels «Эпики»,
    «Фичи», «Истории», «Задачи» as that section's table states, and the
    levels carry the ranks and kinds stated there (BR-M2-4)
  And no operation for creating, renaming or deleting a type, a state or a
    backlog level is exposed by any MVP surface (D-6)

Scenario: A failed seed fails the whole organization creation
  Given the seed capability fails while creating the process for a new
    organization (e.g. an infrastructure error)
  When the organization-creation flow observes that failure
  Then the organization is not created
  And it is never visible to any module without its process
  And no partial set of its types, states or backlog levels remains
    (BR-M2-8)

Scenario: The seed never runs twice for the same organization
  Given an organization identifier the seed has been, or is being, called for
  When an internal retry, a duplicate call, or a second call racing the first
    at the same time targets the same organization identifier
  Then the organization still has exactly one Process
  And none of its types, keys, states or backlog levels are duplicated by
    their stable identity, independent of display name (BR-M2-9), even when
    the calls happened concurrently

Scenario: Every project of the organization uses the same process
  Given an organization with two or more projects
  When an organization member opens the process-definition screen of any of
    those projects
  Then the same five types with the same keys and states are shown in every
    project
  And there is no per-project process setting to change
```
