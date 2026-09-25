# US-4: An organization administrator adds custom types, fields and states (release 3)

- story: US-4
- depends-on: US-1, US-2, US-3
- shippable: no — release 3, no delivery task in this cycle's backlog
- status: pending

As an **organization administrator**, I want **to add custom work item
types, fields and states to my organization's process**, so that **it can
match work the organization does that the five built-in types don't cover —
without corrupting existing work items or the boards/backlogs that read
them**.

## Acceptance criteria

```gherkin
Scenario: A new custom type must have a backlog level, a key and mapped states
  Given an org admin is defining a new work item type
  When they try to save it without assigning it to a backlog level, without
    a key unique in the process, or with a state that has no category
  Then the save is rejected (BR-M2-4, BR-M2-9, doc-001 BR-2)

Scenario: A custom field cannot silently drop existing data
  Given a custom field that existing work items already hold values in
  When an org admin removes that field, or narrows it — changes its kind or
    its allowed values so that a stored value no longer fits
  Then the change is rejected until those values are migrated or explicitly
    retired (BR-M2-6)

Scenario: A state held by existing work items cannot be deleted out from
    under them
  Given a custom state currently held by at least one work item, or
    remembered as the restore target of a work item in «Удалена»
  When an org admin tries to remove that state from the type
  Then the removal is rejected until no work item holds or remembers it
    (BR-M2-6, doc-001 BR-14)

Scenario: The built-in five types and their categories stay untouched
  Given the organization's process after any number of customizations
  When any module reads a built-in type's state
  Then it still maps to the same category it did before any customization
    (doc-001 BR-2), and the type keeps its key (BR-M2-9)
```
