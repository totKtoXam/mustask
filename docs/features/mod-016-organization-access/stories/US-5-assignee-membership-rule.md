# US-5: Only an active organization member can be made an assignee

- story: US-5
- depends-on: US-1
- shippable: no — the rule reads the memberships of US-1
- status: pending

As a **member of an organization**, I want **the «Исполнитель» picker to
offer only the organization's active members, and the system to refuse anyone
else**, so that **work is assigned only to people who can see it (doc-001
BR-13, D-4)**.

## Acceptance criteria

The marks «неактивен» and «не в организации» on the form and in the list are
feat-017-work-item-assignee-marks' (D-4); here an assignee shows with no mark.

```gherkin
Scenario: The picker offers only active members of the item's organization
  Given the organization A with members «Анна Смирнова» (active) and «Олег
    Иванов» (Keycloak account disabled), and «Мария Соколова», an active
    projected user who is a member only of B
  When a member of A types «ов» in «Исполнитель» on a work item of A
  Then «Анна Смирнова» is offered
  And neither «Олег Иванов» nor «Мария Соколова» is offered
  And typing «Мария» shows «Никого не найдено»

Scenario: A non-member, inactive or unknown assignee is refused
  Given a work item of A
  When a save sets its assignee to «Мария Соколова», to «Олег Иванов», or to
    an id the projection does not know
  Then the save is refused with «Этого пользователя нельзя назначить
    исполнителем.» and the assignee is unchanged

Scenario: An assignee who leaves the organization stays
  Given a work item of A assigned to «Анна Смирнова»
  When an administrator removes her from A
  Then the item still has «Анна Смирнова» as its assignee
  And saving its title, its state or any other field succeeds
  And the item's form shows «Анна Смирнова» with no mark until feat-017

Scenario: The reads say whether the assignee is still a member
  Given a work item of A assigned to «Анна Смирнова», who is no longer a
    member, and one assigned to an active member
  When the work-item list, the item, the team's backlog and its board are read
    from Mustask.Api
  Then each answer says of the first assignee that she is not a member and of
    the second that he is (BR-M1-26)

Scenario: Clearing the assignee is always allowed
  Given a work item of A assigned to someone who is no longer a member
  When a member sets «Исполнитель» to «Не назначен» and saves
  Then the item has no assignee

Scenario: A member re-added to the organization can be assigned again
  Given «Анна Смирнова», removed from A and then added back as viewer
  When a member of A searches «Смир» in «Исполнитель»
  Then «Анна Смирнова» is offered — any role may be an assignee
```
