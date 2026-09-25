# US-4 (MVP): A team sees its current iteration

- story: US-4
- depends-on: US-2
- shippable: no — analysis story, delivered by `mod-007-iterations`
- status: pending

As an **organization member**, I want **the system to know which iteration my team is
in right now**, so that **my team's screens can tell what we are doing now
from what is planned for later (BR-M5-8, doc-001 BR-9)**. Here the result is
checked on the team's iteration settings, where the resolved iteration is
marked «Текущая»; filtering by it is US-13, the pane marker US-11.

All dates below are the server's date in UTC (E-1); the derivation and its
tie-breaks are doc-001 BR-9.

## Acceptance criteria

```gherkin
Scenario: The dated iteration covering today is current
  Given a team that has selected three iterations, one of which started
    yesterday and finishes next week while the others do not cover today
  When a member opens the team's iteration settings
  Then that iteration is marked «Текущая», by name and path (doc-001 BR-9)

Scenario: The boundary days are covered, in UTC
  Given a team whose only selected iteration runs 1–14 March
  When the current iteration is resolved at 00:00 UTC on 1 March and at
    23:59 UTC on 14 March
  Then it is current both times
  And at 00:00 UTC on 15 March it is no longer current — also for a member
    whose local clock (UTC+6) still reads 14 March before 06:00
    (doc-001 BR-9, E-1)

Scenario: Overlapping iterations resolve to the earliest finish
  Given a team whose two selected iterations both cover today, finishing on
    10 and on 20 March
  When the current iteration is resolved
  Then the one finishing on 10 March is current (doc-001 BR-9)

Scenario: A sprint beats the release that contains it
  Given a team that has selected «Релиз 1» (1 March – 31 March) and its child
    «Релиз 1 / Спринт 3» (17 March – 31 March), today being 20 March
  When the current iteration is resolved
  Then «Релиз 1 / Спринт 3» is current — same finish, the later start wins
    (doc-001 BR-9)

Scenario: A full tie prefers a sprint, then the lower number
  Given a team whose selected release «Релиз 1» and sprints numbered 8 and 5
    all run 1–14 March
  When the current iteration is resolved
  Then the sprint numbered 5 is current, for every member and on every team
    surface (doc-001 BR-9, BR-M5-2)

Scenario: A full tie between releases goes to the one created first
  Given a team whose only selected iterations are the releases «Релиз А» and,
    created after it, «Релиз Б», both running 1–31 March
  When the current iteration is resolved
  Then «Релиз А» is current (doc-001 BR-9)

Scenario: A selected parent alone can be current
  Given a team that has selected only «Релиз 1», whose dates cover today,
    and none of its children
  When the current iteration is resolved
  Then «Релиз 1» is current (doc-001 BR-9)

Scenario: An unselected parent is never current through its children
  Given a team that has selected «Релиз 1 / Спринт 1» only, whose dates do
    not cover today, while «Релиз 1» (not selected) does
  When the current iteration is resolved
  Then «Релиз 1» is not current (BR-M5-5, doc-001 BR-9)

Scenario: The team's default is current when no dates match
  Given a team whose selected iterations have no dates or no dates covering
    today, and that has set one of them as its default
  When the current iteration is resolved
  Then the team's default is current (doc-001 BR-9)

Scenario: A dated match wins over the default
  Given a team with a default iteration and another selected iteration whose
    dates cover today
  When the current iteration is resolved
  Then the dated one is current, not the default (doc-001 BR-9)

Scenario: No current iteration
  Given a team whose selected iterations do not cover today and that has no
    default
  When a member opens the team's iteration settings
  Then «Нет текущей итерации» is shown as an explicit state, not an error
    (doc-001 BR-9)

Scenario: Current is recomputed, never pinned
  Given a team whose current iteration finished yesterday and whose next
    selected iteration starts today
  When a member reopens the team's iteration settings on that day
  Then the new iteration is current without anyone editing a setting
    (BR-M5-8)

Scenario: Each team has its own current iteration
  Given teams A and B in one project with different selections (E-5)
  When the current iteration is resolved for each
  Then each result is computed from that team's selection and default only
    (BR-M5-8)
```
