# US-6: An organization administrator manages the project's teams

- story: US-6
- depends-on: US-5
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`
- status: pending

As an **organization administrator**, I want **to create the project's
teams, say who is in each and where their new work lands, and delete a team
that is no longer needed**, so that **the team's areas and default views
have the right people (BR-3; team membership carries no access, § Roles)**.

## Acceptance criteria

```gherkin
Scenario: An administrator adds a second team
  Given a project «Пример проекта» with its first team (US-5)
  When the administrator creates a team named «Платформа» in it
  Then the project has two teams, and «Платформа» has no members yet
  And a child area «Платформа» of the root exists, owned by the new team
    and set as its default area (BR-M1-9, BR-M1-14)

Scenario: A team's name is unique inside its project
  Given a project with a team named «Платформа»
  When the administrator creates or renames another team of the same
    project to «платформа », differing only in letter case or surrounding
    spaces
  Then the action is refused with «Команда с таким названием уже есть в
    проекте»
  And the name «Платформа» is still free in another project (BR-M1-9)

Scenario: A new team's area name must be free under the root
  Given a project whose root already has a child area named «Платформа»
  When the administrator creates a team named «Платформа»
  Then the action is refused with «Область с таким названием уже есть на
    этом уровне» (BR-M1-9, BR-M1-12)
  And no team is created

Scenario: An administrator sets a team's default area
  Given the team «Платформа», which owns the areas «Платформа» and
    «Платформа / API» explicitly
  When the administrator sets «Платформа / API» as its default area
  Then a work item created from that team's board or backlog gets Область
    «Платформа / API» (BR-M1-14)
  And the picker offered no area the team does not own explicitly
  And the first team's default stays the root area, with no action to
    change it

Scenario: An administrator adds an organization member to a team
  Given a project (US-5) and a user who is already a member of the
    project's organization
  When the administrator adds that user to one of the project's teams
  Then the user appears in that team's member list
  And nothing about their organization role changes — team membership
    carries no role of its own (BR-5)

Scenario: Only active members of the project's organization can join a team
  Given a user who belongs to a different organization, or to none, and a
    member of the organization whose Keycloak account is disabled or
    deleted (BR-M1-17)
  When the administrator searches for either in the add-to-team picker
  Then neither is offered as a candidate
  And adding them directly is refused (BR-M1-10)

Scenario: An administrator removes a team member
  Given a member of a team who is the assignee of work items
  When the administrator removes them from the team
  Then they no longer appear in the team's member list
  And they still see the project, its work items and every team's boards,
    because their organization membership is untouched (BR-5)
  And their existing work item assignments are unaffected (BR-13)

Scenario: An administrator deletes a team, and its work passes to the first team
  Given the team «Платформа», which owns the area «Платформа» with work
    items in it, has members and has selected iterations
  When the administrator deletes it and confirms the prompt naming the first
    team as the new owner of its areas
  Then «Платформа» is no longer listed, and its name is free in the project
  And the area «Платформа» is owned by the first team, so its work items
    appear on the first team's backlogs
  And its members are no longer in it, and its boards, its iteration
    selection and its default are gone (BR-M1-18)
  And no action restores it
  And a new team named «Платформа» is refused while the area «Платформа»
    keeps that name under the root, and allowed once it is renamed (BR-M1-9)

Scenario: The first team cannot be deleted
  Given a project's first team, which owns the root area (BR-M1-11)
  When the administrator attempts to delete it
  Then the action is refused with «Нельзя удалить команду, которой
    принадлежит корневая область проекта»
  And nothing changes (BR-M1-18)

Scenario: A team with no members says so
  Given a team whose only member has lost their organization membership,
    so the team has no members left (BR-M1-10)
  When an administrator opens the team's screen
  Then they see «В команде пока нет участников» with the add-member action
  And the team still owns its areas, and its project and work items are
    unchanged

Scenario: An administrator renames a team
  Given a team still carrying the name it was created with
  When the administrator renames it to a name free in the project
  Then the new name is shown wherever the team is named, and the team's
    membership, area ownership, default area and area names are unchanged

Scenario: A reader sees the teams but changes nothing
  Given a user with the role viewer in the project's organization
  When they open the project's teams screen
  Then they see every team and its members
  And no action on the screen creates, deletes, renames or sets anything,
    or adds or removes a member, and invoking one directly is refused with
    «Недостаточно прав»
```
