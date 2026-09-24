# US-10 (release 3): A manager reads a delivery plan across teams

- story: US-10
- depends-on: US-3
- shippable: no — release-3 ground, not built by any MVP task; founded by `mod-NNN-delivery-plans`
- status: pending

As a **manager working across several teams**, I want **a timeline showing,
per team, what each of their iterations holds**, so that **I can see
dependencies and collisions between teams without opening every board
(research § Other product capabilities; not captured in the screenshots —
the source project has no plan)**.

## Acceptance criteria

```gherkin
Scenario: A plan shows chosen teams against iterations
  Given a plan configured with three teams and a backlog level
  When a member opens it
  Then there is one row per team and one column per iteration period, each
    cell holding that team's work items of that level scheduled into that
    iteration (BR-7, BR-17)

Scenario: The plan owns no scheduling data
  Given a work item shown on a plan
  When a member moves its card from one iteration column to another
  Then the work item's single iteration pointer is changed — exactly as in
    US-3, under the same write rule — and the plan itself stores nothing
    about that item (BR-7, BR-17)
  And the change is recorded in the work item's history (doc-001 BR-4)

Scenario: Teams with different iterations line up by date
  Given two teams that use different iterations with overlapping dates
  When both are on the same plan
  Then each team's row shows its own iterations positioned by their dates,
    and the rows are comparable in time (BR-4)

Scenario: A team with undated iterations
  Given a team whose iterations have no dates
  When it is added to a plan
  Then its row states that the iterations need dates to be placed on a
    timeline, and the rest of the plan still renders (BR-4)

Scenario: A plan crosses teams but never organizations
  Given teams in two different organizations
  When a member configures a plan
  Then only teams of projects inside one organization the member belongs to
    can be added (doc-001 BR-5, § Out of scope)

Scenario: Reading a plan is a read permission
  Given a user with only the "viewer" role
  When the user opens a plan
  Then the plan renders and every card is read-only (doc-001 D-9)
```
