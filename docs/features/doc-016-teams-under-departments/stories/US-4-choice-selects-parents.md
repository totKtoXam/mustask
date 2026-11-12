# US-4: Choosing a value chooses its parents and resets what no longer fits

- story: US-4
- depends-on: US-3
- shippable: no — the offers of US-3 and these choices are one capability, shipped together
- status: pending

As an **organization member**, I want **choosing a team or a project to fill in the organization and department above it, and a change higher up to clear only what no longer fits**, so that **the four selectors never contradict each other and I never fill them top-down by hand**.

Cast: spec § Stories; Ann sees everything. Menu rebuilding, the remembered selection and the address follow ADR-0032 §§ 1, 2, 6, unchanged.

## Acceptance criteria

```gherkin
Scenario: Choosing a team chooses its department and organization (BR-TD-9)
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  When she chooses MARS
  Then the selection reads «Ромашка» / «Логистика» / MARS / «все», with no notice

Scenario: Choosing a project chooses its department, never a team
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  When she chooses C
  Then the selection reads «Ромашка» / «Финансы» / «все» / C, with no notice

Scenario: A project chosen under a team keeps the team and its department
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / «все»
  When she chooses B
  Then the selection reads «Ромашка» / «Логистика» / MARS / B

Scenario: From organization «все», a choice opens the value's organization
  Given Olga's selection is «Все организации»
  When she chooses ORION under the heading «Другая»
  Then the selection reads «Другая» / «Склад» / ORION / «все» and the page moves to ORION in «Другая»

Scenario: Choosing a department resets what no longer fits
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  When she chooses «Финансы»
  Then the selection reads «Ромашка» / «Финансы» / «все» / «все»
  And she sees «Выбор сброшен: команда MARS, проект A.»

Scenario: A team that does not fit the chosen project resets it
  Given Ann's selection is «Ромашка» / «Логистика» / «все» / B
  When she chooses VENUS, which is not linked to B
  Then the selection reads «Ромашка» / «Логистика» / VENUS / «все», and she sees «Выбор сброшен: проект B.»
  And choosing MARS instead, linked to B, resets nothing and shows no notice

Scenario: «все» resets every lower level (BR-TD-10)
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  When she sets the department selector to «все»
  Then the selection reads «Ромашка» / «все» / «все» / «все» and she sees «Выбор сброшен: команда MARS, проект A.»
  And setting only the team selector to «все» from the same selection reads «Ромашка» / «Логистика» / «все» / A, with no notice
  And «все» on a level with nothing chosen below shows no notice

Scenario: Choosing another organization clears the rest
  Given Olga's selection is «Ромашка» / «Логистика» / VENUS / A
  When she chooses «Другая» in the organization selector
  Then the lower selectors read what she last chose in «Другая», or «все», as ADR-0032 § 6 says

Scenario: Opening an object from a row or a link
  Given Ann's selection is «Ромашка» / «Финансы» / JUPITER / «все»
  When she opens A from a list row or a link
  # An address carrying a query value that does not fit answers not found for its level (ADR-0032 § 1), it does not reset
  Then the selection reads «Ромашка» / «Логистика» / «все» / A, and she sees «Выбор сброшен: команда JUPITER.»

Scenario: A change elsewhere repairs a selection that no longer fits (BR-TD-18)
  Given Olga's selection is «Ромашка» / «Логистика» / VENUS / «все»
  When Ann unlinks VENUS from A and moves it to «Финансы», and Olga's panel next reads the offers
  Then the selection reads «Ромашка» / «Финансы» / VENUS / «все», the department following its team, with no notice
  And had Olga's selection been «Ромашка» / «Логистика» / VENUS / A, the project would reset and she would see «Выбор сброшен: проект A.»
```
