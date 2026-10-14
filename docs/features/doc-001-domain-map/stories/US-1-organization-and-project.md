# US-1: An organization administrator opens an organization and a project

- story: US-1
- depends-on: -
- shippable: no — delivered by mod-002-organizations-projects-teams (organizations, projects, teams, areas) and mod-016-organization-access (memberships, team memberships and the access checks)
- status: pending

As an **instance administrator, or a user granted the "create organizations"
right**, I want **to create an organization and, inside it, a project with
its first team**, so that **my organization has a place to track its work
in MUSTASK**.

## Acceptance criteria

```gherkin
Scenario: An instance administrator creates an organization
  Given a signed-in instance administrator
  When they create an organization with a unique name
  Then the organization exists
  And the administrator becomes its first organization administrator
    automatically (`ORG_MEMBERSHIP.role = admin`, D-9)

Scenario: A user granted "create organizations" can also create one
  Given a signed-in user to whom a Keycloak realm administrator has
    assigned the "create organizations" right (D-10), who is not an
    instance administrator
  When they create an organization with a unique name
  Then the organization exists
  And they become its first organization administrator automatically

Scenario: A user without the right cannot create an organization
  Given a signed-in user who is neither an instance administrator nor
    granted "create organizations" (D-10)
  When they attempt to create an organization
  Then the action is refused with «Недостаточно прав»
  And no organization is created

Scenario: An organization administrator adds a member
  Given an organization the user administers (previous scenarios) and a
    Keycloak user who is not yet a member of it
  When the administrator searches for that user by name or e-mail and adds
    them with a role (admin/member/viewer)
  Then an `ORG_MEMBERSHIP` for that organization is created with the chosen
    role (D-10)
  And the organization now appears in that user's organization list

Scenario: A non-member cannot see the organization
  Given an organization a user has not been added to
  When that user looks at their list of organizations
  Then the organization is not shown
  And opening it directly by its address answers «Организация не найдена»
    (BR-5)

Scenario: An organization administrator creates a project
  Given an organization the user administers (previous scenario)
  When they create a project with a unique name and the key `PRM`
  Then the project exists with that key and is listed for every member of
    that organization
  And its first team exists automatically, named after the project, with
    the creating administrator as its first member, owning the project's
    root area (BR-3)
  And the project's process is its organization's one built-in process
    (Module map M1/M2, D-2)

Scenario: Only an organization administrator may create a project in it
  Given a user who is not a member of an organization, and a user whose
    role in it is member
  When either attempts to create a project inside it
  Then the non-member is answered «Организация не найдена» and the member
    «Недостаточно прав» (BR-5)
  And no project is created

Scenario: A project name and key must be unique within its organization
  Given a project named «Пример проекта» with the key `PRM` in an
    organization
  When an admin of that organization tries to create another project with
    the same name, or with the key `PRM`
  Then the action is refused with a message naming the conflict
  And the same name and key are still free in a different organization

Scenario: An organization administrator deactivates a project
  Given a project the administrator no longer needs
  When they deactivate it
  Then the project moves out of the normal list (BR-6: deactivated, not
    hard-deleted) and stays recoverable indefinitely — projects are never
    purged
  And members and readers of the organization no longer see it in their
    project list

Scenario: An organization administrator restores a deactivated project
  Given the project deactivated in the previous scenario
  When the administrator restores it
  Then it is listed again for every member of the organization, with its
    key, teams, areas and work items as they were (BR-6: nothing was
    hard-deleted)

Scenario: A user with no organizations sees an empty state
  Given a newly signed-in user who belongs to no organization yet
  When they open MUSTASK for the first time
  Then they see an explicit "no organizations yet" screen, not an empty list
    indistinguishable from a loading error
  And it offers the create action only if the user is an instance
    administrator or holds "create organizations" (D-10); otherwise it tells
    them to ask an administrator to add them to one
```
