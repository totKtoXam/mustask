# US-10: A work item has participants besides its assignee

- story: US-10
- depends-on: -
- shippable: no — an analysis story; delivered by `feat-NNN-work-item-participants`
- status: pending

As a **member**, I want **to name the other people who work on an item, besides its one assignee**, so that **a task done by several people says so, as the sheet's «Team» column did**.

## Acceptance criteria

```gherkin
Scenario: A member adds participants
  Given an item assigned to «Пользователь А» that the member may change
  When they add «Пользователь Б» and «Пользователь В» to «Участники»
  Then the card shows the assignee «Пользователь А» and the participants «Пользователь Б», «Пользователь В» (BR-IE-25)

Scenario: The picker offers only members who can read the project
  Given «Пользователь Г», a member who cannot read the item's project, and a person outside the organization
  When the member searches the «Участники» picker
  Then neither is offered, and a request naming either is refused (BR-IE-25)

Scenario: A participant appears once
  Given «Пользователь Б» is a participant
  When they are added again
  Then they are listed once (BR-IE-25)

Scenario: The assignee may also be a participant
  Given «Пользователь А» is the assignee
  When they are added to «Участники»
  Then they are listed there too; neither field changes the other (BR-IE-25)

Scenario: At most 50 participants
  Given an item with 50 participants
  When a 51st is added
  Then it is refused (BR-IE-25)

Scenario: A participant who loses access stays, marked
  Given «Пользователь Б» is a participant and later loses access to the project
  When the card is shown
  Then «Пользователь Б» stays, marked «нет доступа к проекту», and saving other fields is not refused (BR-IE-25, doc-012 BR-MX-25)

Scenario: Participants grant nothing
  Given «Пользователь Б» is a participant with no right to change the project's items
  When they try to change the item
  Then it is refused, as before they were added (BR-IE-25)

Scenario: A member removes a participant
  Given two participants
  When a member who may change the item removes one
  Then only the other stays (BR-IE-25)

Scenario: An import fills participants from a list column
  Given «Team» split by «,» and recoded to members, mapped to «Участники»
  When a row holds «Пользователь Б, Пользователь В»
  Then the item's participants are those two (BR-IE-5, BR-IE-9)
```
