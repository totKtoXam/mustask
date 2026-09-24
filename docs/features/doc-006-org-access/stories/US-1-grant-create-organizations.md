# US-1: An instance administrator grants the right to create organizations

- story: US-1
- depends-on: -
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`
- status: pending

As an **instance administrator**, I want **to grant and revoke the
instance-level "create organizations" right**, so that **the people who may
set up an organization can do it themselves and nobody else can (D-10)**.

## Acceptance criteria

```gherkin
Scenario: An instance administrator may always create organizations
  Given a signed-in user holding the instance administrator role
  When they open the organization list
  Then the create-organization action is offered

Scenario: The right is granted in the Keycloak console
  Given a signed-in user who holds neither the instance administrator role
    nor the "create organizations" permission code
  And the create-organization action is not offered to them
  When an instance administrator grants that user the "create
    organizations" code in the Keycloak console, through a role or group that
    carries it (BR-M1-2, ADR-0018)
  And the user signs in again
  Then the create-organization action is offered to them

Scenario: Revoking the right leaves earlier organizations untouched
  Given a user who holds "create organizations" and has already created an
    organization in which they are the administrator
  When an instance administrator revokes that permission code from them in
    the Keycloak console
  And the user signs in again
  Then the create-organization action is no longer offered
  And they are still the administrator of the organization they created
    earlier (BR-M1-2: the instance-level right and the membership role are
    independent)

Scenario: Without the right the attempt is refused, not merely hidden
  Given a signed-in user who holds neither the instance administrator role
    nor the "create organizations" permission code
  When they invoke the create-organization action directly, bypassing the
    interface
  Then the action is refused with «Недостаточно прав»
  And no organization is created

Scenario: The instance-level right grants no access to organization data
  Given an instance administrator who holds no membership in the
    organization «Пример»
  When they open their organization list
  Then «Пример» is not listed
  And opening its address directly answers «Организация не найдена»
    (BR-M1-8: a non-member is told nothing about an organization's content)
```
