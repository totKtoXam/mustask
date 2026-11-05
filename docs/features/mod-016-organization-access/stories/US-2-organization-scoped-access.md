# US-2: Access is decided by the membership of the resource's organization

- story: US-2
- depends-on: US-1
- shippable: no — the retrofit needs the memberships of US-1 to decide anything
- status: pending

As a **user who belongs to some organizations and not to others**, I want
**every request to be decided by my role in the organization that owns what
I touch**, so that **an organization's content is invisible to outsiders and
each member can do exactly what the role allows (doc-001 BR-5, doc-006
BR-M1-7, BR-M1-8)**.

## Acceptance criteria

doc-006 US-4, scenarios 1 (admin in A, viewer in B), 2 (the resource's
organization decides), 3 (a non-member is not told the organization exists),
4 (no token-carried role grants an organization code), 7 (a granted role
works on the next request, with no re-login; its panel side, as D-9 amends
it, is US-4's refocus scenario) and 8 (the instance administrator sees no
organization content by default); doc-006 US-2 «A
member or reader cannot rename or deactivate the organization»; doc-006 US-5
«Only an organization administrator creates a project»; doc-006 US-7 «A
reader sees the tree but changes nothing» — unchanged, each now enforced on
the endpoints of the spec's § Retrofit inventory. Changed and new:

```gherkin
Scenario: The organization list holds only the user's organizations
  Given a user who is a member of A and not of B
  When they open the organization list or the switcher
  Then A is listed and B is not

Scenario: A user who belongs to no organization sees an explicit empty state
  Given a signed-in user with no membership, holding the create right
  When they open the organization list
  Then they see «Вы пока не состоите ни в одной организации» with «Создайте
    организацию кнопкой «Создать организацию» выше или попросите
    администратора организации добавить вас.» and «Создать организацию»
  And a user without the create right sees «Попросите администратора
    организации добавить вас.» and no create action

Scenario Outline: A non-member gets «Организация не найдена» at every depth
  Given an organization the user holds no membership in
  When they request <address>
  Then the answer is 404 «Организация не найдена»
  And it is the same answer as for a malformed or never-existing id

  Examples:
    | address                                                          |
    | the organization, its «Общие» or «Участники»                     |
    | one of its projects, teams or areas                              |
    | a work item, a backlog or a board of one of its projects        |
    | its process, its assignee candidates or its served access       |
    | the restore of the organization, when it is deactivated          |

Scenario Outline: A member whose role lacks the code is refused
  Given a user whose role in A is <role>
  When they <action> in A
  Then the request answers 403 «Недостаточно прав» and nothing changes

  Examples:
    | role   | action                                                       |
    | viewer | create or save a work item                                   |
    | viewer | set a work item's parent or reorder a backlog                |
    | viewer | search the assignee candidates                               |
    | member | rename or deactivate the organization                        |
    | member | create, rename, deactivate or restore a project              |
    | member | create, rename or delete a team or set its default area      |
    | member | add or remove a team member                                  |
    | member | create, rename, move, re-own or delete an area               |
    | member | add a member, change a role or remove a member               |
    | member | search users to add to the organization or to a team         |

Scenario: A member writes work items, a reader reads them
  Given users with the roles member and viewer in A
  When each opens a project's work items, a team's backlog and its board
  Then both read them
  And the member creates a work item, changes its fields and its parent,
    reorders the backlog and moves a card, each kept after a reload
  And the viewer's same requests answer «Недостаточно прав»

Scenario: The order of the answers
  Given a user whose role in A is viewer
  When they send a write to a project of A that does not exist
  Then the answer is 403 «Недостаточно прав», not «Проект не найден»
  And a member's read of that project answers «Проект не найден»
  And a request with no token answers 401 before anything else

Scenario: Deactivated lists and restore are the administrators'
  Given a user who is admin in A and member in B, both with a deactivated
    project, and organizations C (they are admin) and D (member) deactivated
  When they open the deactivated organizations list
  Then C is listed and D is not
  And B's deactivated projects list and its project restore answer
    «Недостаточно прав», while A's open and restore (BR-M1-35)

Scenario: A work item's body id outside the address is refused
  Given a member of A and of B
  When they save a work item of A with an area or a parent of B, or create
    one in A from a team of B
  Then the request is refused as for an id that does not exist, and nothing
    changes (mod-003 BR-M3-23, feat-001 BR-H-1, mod-006 BR-M4-21)

Scenario: An area's or a team's body id outside the address is refused
  Given an administrator of A and of B
  When they set the owner of an area of A to a team of B, or the default
    area of a team of A to an area of B
  Then the request is refused as for an id that does not exist, and nothing
    changes (mod-002 BR-M1-22)

Scenario: A parent write needs the link code as well
  Given a test role bundle holding workitems.workitem.write but not
    workitems.link.write — no role of D-8 has such a bundle
  When a save or a create of a work item carries a parent
  Then it answers 403 «Недостаточно прав» before any refusal of the parent,
    and nothing changes
  And the same save without a parent succeeds (§ Retrofit inventory, rows
    29 and 31)

Scenario: Every organization-scoped endpoint declares an organization code
  Given the endpoints mapped by Mustask.Api
  When the endpoint inventory test runs
  Then every route with an organizationId declares its organization code,
    and the scoped check that enforces that code (the authorization
    requirement `RequiresOrganizationPermission`, ADR-0027) runs on it
  And no route without an organizationId declares an organization code
  And no POST, PUT, PATCH or DELETE route declares a code ending in .read,
    unless the test lists it as an exception with its reason
  And adding a route under /api/organizations/{organizationId} without a
    code, or a write route with a read code, makes `scripts/verify.sh` fail
    (BR-M1-33)
```
