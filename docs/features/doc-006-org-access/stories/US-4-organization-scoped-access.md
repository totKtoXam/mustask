# US-4: Access is scoped to the organization the resource belongs to

- story: US-4
- depends-on: US-3
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`
- status: pending

As a **user who belongs to several organizations**, I want **my rights to be
decided by my membership in the organization that owns the thing I am
touching**, so that **being an administrator in one organization gives me
nothing in another (BR-5, D-9)**.

## Acceptance criteria

```gherkin
Scenario: The same user is an administrator in one organization and a reader in another
  Given a user with the role admin in organization A and the role viewer in
    organization B
  When they open a project in A and a project in B
  Then in A the project, team and area write actions are offered
  And in B they see the same data, but creating, renaming, deactivating or
    restoring a project, changing team membership, and creating, renaming,
    moving, re-owning or deleting an area are neither offered nor accepted
    when invoked directly (BR-M1-7)

Scenario: The resource's organization decides, not the one last opened
  Given the same user, having just acted as an administrator in A
  When they act on an area, team or project of B
  Then the check uses their membership in B (BR-M1-7)
  And the write is refused with «Недостаточно прав»

Scenario: A non-member is not told the organization exists
  Given an organization the user holds no membership in
  When they open its address directly, or the address of one of its
    projects, teams or areas
  Then the answer is «Организация не найдена»
  And the answer is indistinguishable from the answer for an organization
    that never existed (BR-M1-8)

Scenario: No token-carried role grants an organization-scoped right
  Given a user holding the instance administrator role, and with it every
    instance-level code in their token
  And who holds a membership with the role viewer in organization A
  When they attempt a project, team or area write in A
  Then the action is refused with «Недостаточно прав» (BR-5: only the
    membership role is read for an organization-scoped resource)

Scenario: The realm import carries no organization role and no organization-scoped code
  Given the realm import in the repository after the ADR-0018 amendment
  When the realm-import contract tests run
  Then no realm role named admin, member or viewer is declared
  And no `mustask-api` client role is an organization-scoped code — the
    test tells them apart by the scope each catalogued code carries
  And the instance administrator role holds every instance-level code and
    is granted to at least one group (§ Roles, constraints 1–3)
  And every realm role and group in the import is one the amendment names
    (§ Roles, constraint 8)

Scenario: M0 access is unchanged by the amendment
  Given a user who could read the languages dictionary before the
    amendment, and a dictionaries administrator
  When each opens the languages dictionary after it
  Then the first still reads it, and the dictionaries administrator still
    changes it (§ Roles, constraint 4)

Scenario: A role change takes effect without a re-login
  Given a signed-in user with the role viewer in an organization
  When an administrator changes their role to member
  And the user retries the write they were refused, without signing out
  Then the write succeeds (BR-M1-6: organization-scoped codes are not
    carried in the token)
  And on their next page load the panel offers the write controls the role
    member allows, learned from Mustask.Api (§ Roles, constraint 7)

Scenario: The instance administrator sees no organization content by default
  Given an instance administrator holding no membership in organization A
  When they open the organization list and A's address
  Then A is not listed and its address answers «Организация не найдена»
  And within M1 the instance-level role lets them do only one thing —
    create an organization; granting that right is Keycloak realm
    administration (US-1); it gives them no way into an organization left
    with no usable administrator (a known MVP limitation, spec § MVP
    boundary); instance-level rights outside M1, such as the dictionaries
    administrator's (M0), are unaffected
```
