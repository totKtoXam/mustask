# US-2: An organization administrator manages teams and areas

- story: US-2
- depends-on: US-1
- shippable: no — delivered by mod-organizations-projects-teams
- status: pending

As an **organization administrator** (MVP has no project-scoped admin role —
BR-5), I want **to manage the default team's
membership and the project's area tree**, so that **the team that owns each
part of the work is well defined (BR-3)**.

## Acceptance criteria

```gherkin
Scenario: An administrator adds an organization member to the default team
  Given a project (US-1) and a user who is already a member of the
    project's organization
  When the administrator adds that user to the project's default team
  Then the user appears in the team's member list

Scenario: An administrator removes a team member
  Given a team member added in the previous scenario
  When the administrator removes them from the team
  Then the user no longer appears in the team's member list
  And their existing work item assignments are unaffected (BR-13 governs
    who *may* be assigned, not who already is)

Scenario: Only members of the project's organization can join its team
  Given a user who is not a member of the project's organization
  When an administrator attempts to add them to the project's team
  Then the action is refused

Scenario: An administrator creates an area node and assigns its owning team
  Given a project with its root area (owned by the default team, D-8)
  When the administrator creates a child area under the root and assigns a
    team to own it
  Then the child area exists with that team as its explicit owner (BR-3)

Scenario: An area with no explicit owner inherits its nearest owned ancestor
  Given a child area created with no team assigned
  When any module asks which team owns it (e.g. to place a work item on a
    board, BR-3)
  Then the answer is the nearest ancestor area's owning team
  And if no ancestor up to the root has an owner, the answer is undefined
    and the work item is not shown on any team's board until an owner is set

Scenario: A reader can see teams and areas but not change them
  Given a user signed in with only the "viewer" role
  When they open the project's team or area screen
  Then they can see the membership and the tree
  And no action on this screen lets them add, remove, or re-own anything
```
