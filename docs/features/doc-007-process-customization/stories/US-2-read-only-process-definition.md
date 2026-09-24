# US-2: Any organization member reads the process definition, read-only

- story: US-2
- depends-on: US-1
- shippable: no — delivered by mod-001-process-seed
- status: pending

As an **organization member (any role)**, I want **to see the process
definition without being able to change it**, so that **I understand how
work items are typed and stated in my organization**.

## Acceptance criteria

```gherkin
Scenario: Every role can read the same process definition
  Given a user with role admin, member or viewer in an organization
  When they open the process-definition screen from one of its projects
  Then they see all five types and, for each: its full state list in order,
    with the default state marked, each state's category, its fixed icon and
    colour, and its backlog level
  And the information shown is identical regardless of their role

Scenario: A user with no membership in the organization reads nothing
  Given a user who holds no ORG_MEMBERSHIP in that organization
  When they request that organization's process definition
  Then the request is refused
  And no type, state or backlog level of it is disclosed (doc-001 BR-5/D-9)

Scenario: No role can add, rename or remove anything
  Given a user with role admin, member or viewer
  When they attempt to create, rename or delete a type, a state or a backlog
    level
  Then no MVP surface exposes such an operation, and any request that would
    change the process definition is refused (MVP has no process editing at
    all, D-6)

Scenario: State categories are visible, not just names
  Given the project's built-in process
  When a user inspects the state of a work item, or any module reads it
    (e.g. a board)
  Then the state's category (Proposed/InProgress/Resolved/Completed/Removed)
    is derivable from its definition
  And the same five categories apply to every type even where state names
    differ per type (BR-2)
```
