# US-8: The instance administrator and the realm inventory

- story: US-8
- depends-on: US-2
- shippable: no — removing the realm roles admin/member/viewer only makes sense once memberships decide (US-2)
- status: pending

As a **Keycloak realm administrator**, I want **the realm to hold only
instance-level roles — the instance administrator, the dictionaries
administrator and the organization creator — and the stand I already run to be
moved to it without a reset**, so that **no Keycloak role pretends to grant
organization rights and nobody loses what they could do (D-6, D-7, doc-006
§ Roles constraints 1–3, 8)**.

## Acceptance criteria

doc-006 US-1, scenarios 1–5 (the create right granted and revoked in the
Keycloak console, refused when missing), and its scenario 6 with doc-006 US-4
scenario 8 (the instance administrator sees no organization content without a
membership) — unchanged, with `instance-admin` as the instance administrator.

```gherkin
Scenario: The realm import holds exactly the named inventory
  Given the realm import in the repository
  When the realm contract tests run
  Then its realm roles are exactly instance-admin, dictionaries-admin,
    organization-creator and the realm's default roles
  And the default roles hold dictionaries.languages.read, declared in a form
    the contract tests and the stand's drift check both read (BR-M1-37)
  And its groups are exactly /Администраторы (instance-admin),
    /Администраторы справочников (dictionaries-admin) and /Создатели
    организаций (organization-creator)
  And no realm role is named admin, member or viewer
  And the mustask-api client's roles equal the instance-level codes of the
    catalog, and no organization-scoped code appears anywhere in the import
  And instance-admin holds every instance-level code and no other, and is
    granted to at least one group (doc-006 constraint 3)

Scenario: Every signed-in user reads the dictionaries
  Given a user in no group, a member of /Создатели организаций only, and a
    member of /Администраторы справочников
  When each opens «Языки»
  Then each reads the languages list (BR-M1-29)
  And only the dictionaries administrator and an instance administrator see
    «Добавить язык», and a write by anyone else is refused

Scenario: The live realm of the human's stand is migrated in place
  Given a task stand whose live realm was created from the old import, with
    a user holding admin through /Администраторы, a user given admin
    directly, a user in /Сотрудники, a user in /Сотрудники/Участники and a
    user in /Создатели организаций
  When the live-realm script runs against it
  Then both admin holders hold instance-admin and create organizations and
    write dictionaries as before
  And the users of /Сотрудники and /Сотрудники/Участники are in no group and
    still read dictionaries
  And the organization creator still creates organizations
  And no user is created or deleted and every user keeps their id, so
    memberships and the user projection still match (BR-M1-28)
  And the realm roles admin, member, viewer and the two groups are gone

Scenario: The live-realm script changes nothing else
  Given an export of the task stand's realm taken before the script runs,
    with a user given admin directly, a group the import does not declare
    that has members and maps admin and one other role that is not member
    or viewer (dictionaries-admin), and a user with a credential and
    attributes
  When the script has run
  Then an export taken after it differs from the first only by the changes
    BR-M1-28 lists
  And clients and their secrets, and users' ids, credentials and attributes,
    are unchanged
  And Keycloak's built-in roles are unchanged except the default roles,
    which gained dictionaries.languages.read
  And the undeclared group keeps its members and its other role, and maps
    instance-admin in place of admin
  And no other realm is touched

Scenario: The live-realm script is safe to repeat
  Given a live realm the script has already migrated
  When it runs again
  Then it reports nothing to change and changes nothing

Scenario: An interrupted run is completed by running the script again
  Given a run of the script on the task stand stopped after any one of its
    steps
  When the script runs again
  Then the realm ends exactly as after an uninterrupted run
  And every user who held admin, directly or through a group, holds
    instance-admin, because every grant comes before any removal (BR-M1-28)

Scenario: An unexpected structure stops the script before any removal
  Given a live realm whose /Сотрудники holds a subgroup other than
    /Участники
  When the script runs
  Then it removes nothing, names the subgroup in its report and ends with a
    failure

Scenario: The stand comes up ready after the script, with no reset
  Given a stand whose live realm is on the old import — the task stand
    first, then the human's — started from this task's code, its keycloak
    resource not ready because the drift check reports the old realm roles
    and the default roles' composites
  When the live-realm script has run
  Then the drift check reports nothing and the stand comes up ready
  And `scripts/stand-reset.sh` was never run, so the stand's database keeps
    its data (BR-M1-37, D-6)

Scenario: The realm scripts keep the administrator's secrets out of sight
  When the live-realm script or `scripts/keycloak-seed.sh` runs
  Then the Keycloak administrator password is read from the environment
  And neither the password nor the administrator's access token appears on
    any command line (the process list), in the script's output or in a log
  And the live-realm script's report names users by username or id only

Scenario: The stand seed follows the inventory
  Given an empty realm store
  When `scripts/keycloak-seed.sh` runs with MUSTASK_PASSWORD and
    MEMBER_PASSWORD
  Then the administrator is in /Администраторы and the user `member` is in
    no group
  And `member` reads dictionaries and sees no organization until an
    organization administrator adds them
```
