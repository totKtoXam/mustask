# US-1: An organization administrator opens an organization and a project

- story: US-1
- depends-on: -
- shippable: no — delivered by mod-organizations-projects-teams
- status: pending

As an **instance administrator, or a user granted the "create organizations"
permission**, I want **to create an organization and, inside it, a project
with its default team**, so that **my organization has a place to track its
work in MUSTASK**.

## Acceptance criteria

```gherkin
Scenario: An instance administrator creates an organization
  Given a signed-in instance administrator
  When they create an organization with a unique name
  Then the organization exists
  And the administrator becomes its first organization administrator
    automatically (`ORG_MEMBERSHIP.role = admin`, D-9)

Scenario: A user granted "create organizations" can also create one
  Given a signed-in user who holds the instance-level "create organizations"
    permission (D-10) but is not an instance administrator
  When they create an organization with a unique name
  Then the organization exists
  And they become its first organization administrator automatically

Scenario: A user without the permission cannot create an organization
  Given a signed-in user who is neither an instance administrator nor
    granted "create organizations" (D-10)
  When they attempt to create an organization
  Then the action is refused
  And no organization is created

Scenario: An organization administrator adds a member
  Given an organization the user administers (previous scenarios) and a
    realm user who is not yet a member of it
  When the administrator searches for that user by name or e-mail and adds
    them with a role (admin/member/viewer)
  Then an `ORG_MEMBERSHIP` for that organization is created with the chosen
    role (D-10)
  And the organization now appears in that user's organization list

Scenario: A non-member cannot see the organization
  Given an organization a user has not been added to
  When that user looks at their list of organizations
  Then the organization is not shown
  And they cannot open it directly by guessing its identifier

Scenario: An organization administrator creates a project
  Given an organization the user administers (previous scenario)
  When they create a project with a unique name inside it
  Then the project exists and is listed for every member of that
    organization
  And a default team for that project exists automatically, with the
    creating administrator as its first member
  And the project's process is seeded from M2's one built-in process,
    scoped to this organization (Module map M1/M2, D-2)

Scenario: Only an organization administrator may create a project in it
  Given a user who is not a member of an organization, and a user who is a
    member of it with the "member" role
  When either attempts to create a project inside it
  Then the action is refused (Operation completeness: Project — create is
    the organization administrator's operation)
  And no project is created

Scenario: A project name must be unique within its organization
  Given a project named "Пример проекта" already exists in an organization
  When an admin of that organization tries to create another project with
    the same name in it
  Then the action is refused with a message naming the conflict
  And the same name is still free in a different organization

Scenario: An organization administrator archives (soft-deletes) a project
  Given a project with no work in progress the administrator no longer needs
  When they delete the project
  Then the project moves out of the normal list (BR-6: deactivate, not a
    hard delete) and stays recoverable indefinitely until an administrator
    purges it by hand
  And members and readers of the organization no longer see it in their
    project list

Scenario: An organization administrator restores an archived project
  Given the project archived in the previous scenario
  When the administrator restores it
  Then it is listed again for every member of the organization, with its
    teams, areas and work items as they were (BR-6: nothing was hard-deleted)

Scenario: A user with no organizations sees an empty state
  Given a newly signed-in user who belongs to no organization yet
  When they open MUSTASK for the first time
  Then they see an explicit "no organizations yet" screen, not an empty list
    indistinguishable from a loading error
  And it offers the create action only if the user is an instance
    administrator or holds "create organizations" (D-10); otherwise it tells
    them to ask an administrator to add them to one
```
