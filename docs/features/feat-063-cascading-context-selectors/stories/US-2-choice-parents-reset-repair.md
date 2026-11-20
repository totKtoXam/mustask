# US-2: A choice selects its parents, resets what no longer fits, and a stale selection is repaired

- story: US-2
- depends-on: US-1
- shippable: no — the offers of US-1 and these choices are one capability, shipped together
- status: pending

As an **organization member**, I want **choosing a team or a project to fill in the department above it, a change higher up to clear only what no longer fits, and a stale remembered selection to be mended**, so that **the four levels never contradict each other and I never fill them top-down by hand**.

Cast: as US-1. Rules: doc-016 BR-TD-9, -10, -18; design § 3.3, 3.5; ADR-0032 § 6 (the remembered selection and its notice). The notice is an `InlineNotice` under the header, `role="status"`, closable, replacing the previous one. Choosing another organization is covered by ADR-0032 § 6 unchanged; organization «все» is feat-064.

## Acceptance criteria

```gherkin
Scenario: Choosing a team chooses its department (BR-TD-9)
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  When she chooses MARS in the team column
  Then the button reads «Ромашка › Логистика › MARS › все», with no notice
  And the panel stays open and the project column narrows to MARS's projects

Scenario: Choosing a project chooses its department, never a team
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  When she chooses C
  Then the selection reads «Ромашка» / «Финансы» / «все» / C, with no notice

Scenario: A project chosen under a team that links it keeps the team
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / «все»
  When she chooses B
  Then the selection reads «Ромашка» / «Логистика» / MARS / B

Scenario: A team not linked to the chosen project resets the project
  Given Ann's selection is «Ромашка» / «Логистика» / «все» / B
  When she chooses VENUS, which is not linked to B
  Then the selection reads «Ромашка» / «Логистика» / VENUS / «все» and she sees «Выбор сброшен: проект B.»
  And choosing MARS instead, linked to B, resets nothing and shows no notice

Scenario: Choosing a department resets what no longer fits
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  When she chooses «Финансы»
  Then the selection reads «Ромашка» / «Финансы» / «все» / «все»
  And she sees «Выбор сброшен: команда MARS, проект A.»
  And the team and project columns narrow to «Финансы»

Scenario: «все» at the department level resets every lower level (BR-TD-10)
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  When she chooses «все» in the department column
  Then the selection reads «Ромашка» / «все» / «все» / «все» and she sees «Выбор сброшен: команда MARS, проект A.»

Scenario: «все» at the team or project level resets nothing else
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  When she chooses «все» in the team column
  Then the selection reads «Ромашка» / «Логистика» / «все» / A, with no notice
  And «все» on a level with nothing chosen below shows no notice

Scenario: Opening an object from a row or a link
  Given Ann's selection is «Ромашка» / «Финансы» / JUPITER / «все»
  When she opens A from a list row or a link
  Then the selection reads «Ромашка» / «Логистика» / «все» / A and she sees «Выбор сброшен: команда JUPITER.»

Scenario: A remembered selection that no longer fits is repaired
  Given Olga's remembered selection is «Ромашка» / «Финансы» / VENUS / «все» and VENUS now stands in «Логистика»
  When the panel loads
  Then the selection reads «Ромашка» / «Логистика» / VENUS / «все» and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика / VENUS.»

Scenario: A remembered value that is gone or hidden resets to «все»
  Given Olga's remembered project A is now hidden from her
  When the panel loads
  Then the project level reads «все» and the notice names the repaired selection (ADR-0032 § 6)

Scenario: A change seen while the panel is open repairs the selection (BR-TD-18)
  Given Olga's selection is «Ромашка» / «Логистика» / VENUS / A
  When Ann unlinks VENUS from A and moves VENUS to «Финансы», and Olga's panel next reads the offers
  Then the selection reads «Ромашка» / «Финансы» / VENUS / «все» and she sees «Выбор сброшен: проект A.»
  And had her selection been «Ромашка» / «Логистика» / VENUS / «все», the department follows VENUS with no notice

Scenario: An address that does not fit is not a reset
  Given an address carries a `departmentId` that does not fit its object
  Then it answers not found for its level (ADR-0032 § 1) and the selection does not change
```
