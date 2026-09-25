# US-3: An organization administrator manages membership

- story: US-3
- depends-on: US-2
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`,
  on the user projection of `mod-NNN-keycloak-user-projection`
- status: pending

As an **organization administrator**, I want **to add existing users to my
organization with a role, change that role and remove them**, so that **the
right people can work in the organization and only they (D-9, D-10)**.

## Acceptance criteria

```gherkin
Scenario: An administrator adds a member found by name or e-mail
  Given an organization the user administers and a Keycloak user who is not
    yet a member of it
  When the administrator searches for that user by part of their name or
    e-mail and adds them with the role member
  Then a membership for that organization is created with the role member
    (D-10)
  And the organization appears in that user's organization list on their
    next request, with no re-login (BR-M1-6)

Scenario: The search never exposes an organization's membership to outsiders
  Given an administrator of organization A searching for users to add
  When the search returns candidates
  Then each candidate is shown by display name and e-mail only
  And nothing in the result says which other organizations they belong to

Scenario: A search with no matches says so
  Given an administrator on the add-member search
  When they search for text that matches no user's name or e-mail
  Then they see «Пользователи не найдены», not an empty list
  And nothing is added

Scenario: An inactive account is not offered in the search
  Given a Keycloak user whose account is disabled or deleted (BR-M1-17)
  When an administrator searches for them by name or e-mail to add
  Then they are not offered as a candidate
  And adding them directly is refused

Scenario: Member search reads the user projection, not Keycloak
  Given a user just created in Keycloak, whose creation the projection has
    received (§ Roles, constraint 5)
  And Keycloak itself is unreachable at the moment of the search
  When an administrator searches for that user by name or e-mail
  Then the user is offered as a candidate, from the projection
  And existing members' statuses stay as last received (NFR Identity
    refresh)

Scenario: A user already in the organization is not added twice
  Given a user who is already a member of the organization
  When the administrator picks them in the search and confirms
  Then the action is refused with «Пользователь уже состоит в организации»
  And the user still holds exactly one membership in that organization
    (BR-M1-5)

Scenario: The membership shows who added it and who changed its role
  Given a member added by administrator «Анна Смирнова», whose role was later
    changed by administrator «Иван Петров»
  When any member of the organization opens the members screen
  Then that membership shows «Добавлен» with «Анна Смирнова» and the date
  And «Роль изменена» with «Иван Петров» and the date (BR-M1-16)

Scenario: An administrator changes a member's role
  Given a member of the organization with the role member
  When the administrator changes their role to viewer
  Then the member's write actions in that organization stop being available
    on their next request, with no re-login (BR-M1-6)
  And their role in every other organization is unchanged (BR-M1-7)

Scenario: The last active administrator cannot be removed or downgraded
  Given an organization in which exactly one membership with the role admin
    belongs to an active account
  When that membership's role is changed to member or viewer, or the
    membership is removed — by that administrator themselves or by anyone
  Then the action is refused with «В организации должен остаться хотя бы
    один администратор»
  And the membership keeps the role admin (BR-M1-4)
  And an inactive administrator's membership does not count as the one that
    remains (BR-M1-17)

Scenario: An administrator steps down while another administrator remains
  Given an organization with two active administrators, A and B
  When A changes their own role to member
  Then the change is saved and B is the organization's administrator
  And A's administrator actions stop on their next request (BR-M1-4)

Scenario: Two administrators removing each other at once keep one
  Given an organization whose only active administrators are A and B
  When A removes B and B removes A at the same moment
  Then exactly one of the two removals is saved, and the other is refused
    and changes nothing — with «В организации должен остаться хотя бы один
    администратор», or, when the refused actor's own membership is the one
    already removed, as for a non-member (BR-M1-8)
  And the organization still has one active administrator (BR-M1-4, NFR
    Concurrency)

Scenario: An administrator removes a member
  Given a member of the organization who also belongs to a project team and
    is the assignee of work items
  When the administrator removes their membership
  Then the user no longer sees the organization or anything in it
  And they are removed from every team of that organization (BR-M1-10)
  And the work items already assigned to them keep that assignee and stay
    readable to the organization's members, «Исполнитель» marking them «не
    в организации» (BR-M1-17; doc-002 § Fields)

Scenario: A member whose Keycloak account is disabled is shown as inactive
  Given a member whose Keycloak account has been disabled or deleted, and
    the user projection has received that change (NFR Identity refresh)
  When anyone opens the organization's members screen, or a work item
    assigned to them
  Then the member is listed with the marking «неактивен», and «Исполнитель»
    shows the same marking
  And their memberships, team memberships and assignments are unchanged
  And they cannot sign in, because Keycloak refuses them
  And they stay until an administrator removes them — nothing removes them
    automatically (BR-M1-17)

Scenario: A re-enabled account loses the inactive marking
  Given the member from the previous scenario, whose disabled account is
    enabled again in Keycloak
  When the projection has received that change and they sign in
  Then the marking «неактивен» is gone and they work in the organization
    with the role they held before (BR-M1-17)

Scenario: Every member can read the membership list, only an admin changes it
  Given users with the roles member and viewer in the organization
  When they open the organization's members screen
  Then they see the list of members with their roles
  And the add, change-role and remove actions are not offered — including
    for their own membership: a member or reader leaves only by an
    administrator's action (BR-M1-4)
  And invoking them directly is refused with «Недостаточно прав»

Scenario: A removed member's access ends immediately
  Given a member who currently has a work item open
  When the administrator removes their membership
  And the member's next action reaches the system
  Then it is refused and the organization is no longer listed for them
```
