# US-6: An organization administrator manages the project's team

- story: US-6
- depends-on: US-5
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`
- status: pending

As an **organization administrator**, I want **to say who is in the
project's team**, so that **the team's boards, backlogs and iterations show
the right people's work (BR-3)**.

## Acceptance criteria

```gherkin
Scenario: An administrator adds an organization member to the team
  Given a project (US-5) and a user who is already a member of the
    project's organization
  When the administrator adds that user to the project's team
  Then the user appears in the team's member list
  And nothing about their organization role changes — team membership
    carries no role of its own (BR-5)

Scenario: Only members of the project's organization can join its team
  Given a user who belongs to a different organization, or to none
  When the administrator searches for them in the add-to-team picker
  Then they are not offered as a candidate
  And adding them directly is refused (BR-M1-10)

Scenario: An inactive organization member is not offered in the picker
  Given a member of the project's organization whose Keycloak account is
    disabled or deleted (BR-M1-17)
  When the administrator searches for them in the add-to-team picker
  Then they are not offered as a candidate

Scenario: An administrator removes a team member
  Given a member of the project's team who is the assignee of work items
  When the administrator removes them from the team
  Then they no longer appear in the team's member list
  And they still see the project and its work items, because their
    organization membership is untouched (BR-5)
  And their existing work item assignments are unaffected (BR-13)

Scenario: MVP offers no second team and no team deletion
  Given a project with its one automatically created team (BR-M1-9)
  When an administrator opens the project's team screen
  Then the team is shown with its members
  And no action creates a second team or deletes this one — several teams
    per project are release 2 (BR-M1-9)

Scenario: A team with no members says so
  Given the project's team whose only member has lost their organization
    membership, so the team has no members left (BR-M1-10)
  When an administrator opens the project's team screen
  Then they see «В команде пока нет участников» with the add-member action
  And the team still owns its areas, and its project and work items are
    unchanged

Scenario: An administrator renames the team
  Given the project's team, still carrying the name it was created with
  When the administrator renames it
  Then the new name is shown wherever the team is named, and the team's
    membership and area ownership are unchanged

Scenario: A reader sees the team but changes nothing
  Given a user with the role viewer in the project's organization
  When they open the project's team screen
  Then they see the team and its members
  And no action on the screen adds, removes or renames anything, and
    invoking one directly is refused with «Недостаточно прав»
```
