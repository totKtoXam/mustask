# US-6: Existing organizations get their first administrator

- story: US-6
- depends-on: US-1, US-3
- shippable: no — the seeding fills the memberships US-1 and US-3 introduce
- status: pending

As **the creator of an organization that exists before this task ships**, I
want **to become its administrator when access narrows**, so that **someone
can add everyone else and nothing already built is lost (D-3, mod-002
BR-M1-21)**.

## Acceptance criteria

doc-006 US-2 scenario 1 (the creator of a new organization becomes its
administrator) and scenario 5 (a failed process seed leaves nothing behind —
no membership either) — unchanged.

```gherkin
Scenario: Each existing organization's creator becomes its administrator
  Given organizations A (active) and B (deactivated) created before this task
    by «Анна Смирнова» and «Иван Петров», and other users who worked in them
  When the database migration of this task runs
  Then A has exactly one membership, «Анна Смирнова» as admin, and B exactly
    one, «Иван Петров» as admin
  And each is recorded as added by the creator at the organization's
    creation time, with no role change
  And no other user holds a membership in A or B (BR-M1-25)

Scenario: Everyone else is a non-member until added
  Given A after the migration and a user who worked in A before it
  When they open A or any of its projects
  Then they see «Организация не найдена» until an administrator adds them on
    «Участники»

Scenario: A creator who cannot sign in still gets it
  Given an organization whose recorded creator's Keycloak account is
    disabled, or was deleted before the projection first received users
  When the migration runs
  Then that creator holds the admin membership anyway (D-3, D-10)
  And since they cannot sign in, nobody can change the organization's
    members, projects, teams or areas — doc-006 H-1's known limitation, no
    recovery path in MVP

Scenario: A creator the projection does not know yet administers as usual
  Given an organization whose recorded creator is enabled in Keycloak but
    not yet received by the projection (mod-014 BR-UP-17)
  When the migration runs and the creator signs in
  Then they hold the admin membership (D-3, D-10)
  And they add members, change roles and manage projects, teams and areas
    like any administrator — access is read from the membership, never from
    the projection (BR-M1-32, mod-014 BR-UP-12)
  And changing their own role or removing themselves is refused with «В
    организации должен остаться хотя бы один администратор» while no known
    active administrator remains (BR-M1-4, BR-M1-32)
  And once the projection receives them, they count as a known active
    administrator

Scenario: An existing project's creator joins its first team when a member
  Given in A a project «Пример проекта» created by «Анна Смирнова» and a
    project «Отчёты» created by «Олег Иванов», A's creator being «Анна
    Смирнова»
  When the migration runs
  Then «Анна Смирнова» is the only member of the first team «Пример проекта»
  And the first team «Отчёты» has no members, because «Олег Иванов» is not a
    member of A (doc-006 BR-M1-10)
  And no team other than a project's first team gains a member

Scenario: Running the migration on a database with nothing to seed
  Given a database with no organizations
  When the migration runs
  Then it succeeds and creates no membership

Scenario: The migration can be reverted
  Given a database migrated by this task
  When the migration is reverted
  Then the membership data is gone and organizations, projects, teams and
    areas are exactly as before it ran
```
