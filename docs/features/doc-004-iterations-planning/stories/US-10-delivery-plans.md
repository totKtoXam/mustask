# US-10 (release 3): An organization member reads a delivery plan across teams

- story: US-10
- depends-on: US-3
- shippable: no — release-3 ground, not built by any MVP task; founded by `mod-013-delivery-plans`
- status: pending

As an **organization member working across several teams**, I want **a
timeline showing, per team, what each of their iterations holds**, so that
**I can see dependencies and collisions between teams without opening every
board (research § Other product capabilities; not captured in the
screenshots — the source project has no plan)**.

## Acceptance criteria

```gherkin
Scenario: A plan shows chosen teams against iterations
  Given a plan configured with three teams and a backlog level
  When a member opens it
  Then there is one row per team and one column per iteration period, each
    cell holding that team's work items of that level scheduled into that
    iteration (BR-M5-7, BR-M5-17)

Scenario: The plan owns no scheduling data
  Given a work item shown on a plan
  When a member moves its card from one iteration column to another
  Then the work item's single iteration pointer is changed — exactly as in
    US-3, under the same write rule — and the plan itself stores nothing
    about that item (BR-M5-7, BR-M5-17)
  And the change is recorded in the work item's history (doc-001 BR-4)

Scenario: Teams with different iterations line up by date
  Given two teams that use different iterations with overlapping dates
  When both are on the same plan
  Then each team's row shows its own iterations positioned by their dates,
    and the rows are comparable in time (BR-M5-4)

Scenario: A team with undated iterations
  Given a team whose iterations have no dates
  When it is added to a plan
  Then its row states that the iterations need dates to be placed on a
    timeline, and the rest of the plan still renders (BR-M5-4)

Scenario: A plan belongs to the organization, never crosses it
  Given teams of two projects of one organization, and a team of another
    organization
  When a member configures a plan
  Then teams of both projects can be added, and the other organization's
    team is never offered (BR-M5-17, doc-001 BR-5)

Scenario: Members and administrators edit a plan, everyone reads it
  Given a plan created by one member
  When another member changes its teams, and a user whose role in the
    organization is viewer opens it
  Then the member's change is saved (BR-M5-17)
  And the viewer sees the plan rendered, with every card read-only and no
    control to change the plan
```
