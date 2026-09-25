# US-5: An organization administrator turns on Reason with per-transition rules (release 3)

- story: US-5
- depends-on: US-4
- shippable: no — release 3, no delivery task in this cycle's backlog
- status: pending

As an **organization administrator**, I want **to require a Reason for
specific state transitions**, so that **members record why a work item
moved (e.g. why a bug was closed) — without losing the ability to move a
work item to any state**.

## Acceptance criteria

```gherkin
Scenario: A transition can require a Reason
  Given an org admin has set the transition «Разрешена»→«Закрыта» on «Баг»
    to require a Reason with the default «Исправлено»
  When a member performs that transition on a «Баг» without choosing a
    reason
  Then the transition is rejected until a reason is provided

Scenario: Turning on Reason never removes a transition
  Given any number of transitions have a required Reason configured
  When a member looks at which destination states are offered from the
    current one
  Then every non-Removed state is still offered (BR-M2-7) — Reason adds a
    required input, it does not remove a choice

Scenario: Reason is recorded in history
  Given a transition that required a reason
  When it completes
  Then the chosen reason is part of the work item's history revision for
    that change (doc-001 BR-4)
```
