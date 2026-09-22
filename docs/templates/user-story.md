# US-<N>: <title>

<!-- Stage 1 template for one story file, docs/features/<id>/stories/US-<N>-<slug>.md.
Delete the comments when filling it in. Keep the header block below as the
first thing in the file — a downstream stage reads it before anything else to
decide whether it needs this story at all. -->

- story: US-<N>
- depends-on: US-1, US-3 | -
- shippable: yes | no — <one line: why not>
- status: pending

As a **<role>**, I want **<action>**, so that **<value>**.

## Acceptance criteria

```gherkin
Scenario: <name>
  Given <precondition>
  When <action>
  Then <verifiable result>
```

<!-- Several scenarios per story is normal: happy path + failures. If the
list keeps growing past what one story can hold, it is a sign this is really
two stories — see the per-file line budget in
stages/01-business-analysis.md. -->
