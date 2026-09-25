# US-2: An organization administrator manages teams and areas

- story: US-2
- depends-on: US-1
- shippable: no — delivered by mod-002-organizations-projects-teams
- status: pending

As an **organization administrator** (MVP has no project-scoped admin role —
BR-5), I want **to manage the project's teams, their membership and the
project's area tree**, so that **the team that owns each part of the work is
well defined (BR-3)**.

## Acceptance criteria

```gherkin
Scenario: A project may have several teams
  Given a project with its first team (US-1)
  When the administrator creates a second team «Платформа» in it
  Then the project lists both teams
  And «Платформа» owns an area of its own, its default area, so the work
    items created from its board land on its backlog (doc-006 BR-M1-9,
    BR-M1-14)

Scenario: An administrator adds an organization member to a team
  Given a project (US-1) and a user who is already a member of the
    project's organization
  When the administrator adds that user to one of the project's teams
  Then the user appears in that team's member list

Scenario: An administrator removes a team member
  Given a team member added in the previous scenario
  When the administrator removes them from the team
  Then the user no longer appears in the team's member list
  And their existing work item assignments are unaffected (BR-13 governs
    who *may* be assigned, not who already is)

Scenario: Only members of the project's organization can join its teams
  Given a user who is not a member of the project's organization
  When an administrator attempts to add them to one of the project's teams
  Then the action is refused

Scenario: An administrator creates an area node and assigns its owning team
  Given a project with its root area (owned by the project's first team,
    doc-006 BR-M1-11)
  When the administrator creates a child area under the root and assigns
    one of the project's teams to own it
  Then the child area exists with that team as its explicit owner (BR-3)

Scenario: An area with no explicit owner inherits its nearest owned ancestor
  Given a child area created with no team assigned
  When any module asks which team owns it (e.g. to place a work item on a
    board, BR-3)
  Then the answer is the nearest ancestor area's owning team
  And since the root is always owned, every area resolves to exactly one
    owning team (BR-3)

Scenario: A reader can see teams and areas but not change them
  Given a user whose role in the project's organization is viewer
  When they open the project's teams or area screen
  Then they can see the teams, their membership and the tree
  And no action on these screens lets them create, add, remove, re-own or
    delete anything
```
