# US-2: A choice selects its parents, resets what no longer fits, and a stale selection is repaired

- story: US-2
- depends-on: US-1
- shippable: no — the offers of US-1 and these choices are one capability, shipped together
- status: pending

As an **organization member**, I want **choosing a team or a project to fill in the department above it, a change higher up to clear only what no longer fits, and a stale remembered selection to be mended**, so that **the four levels never contradict each other and I never fill them top-down by hand**.

Cast: as US-1. Rules: doc-016 BR-TD-9, -10, -18; design § 3.3, 3.5; ADR-0032 § 6 (the remembered selection and its notice). The notice is an `InlineNotice` under the header, `role="status"`, closable, replacing the previous one (spec BR-12). Page and focus on a choice: spec BR-9. BR-TD-10 governs «все» (spec BR-3). Organization «все» as a source of choices is feat-064.

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

Scenario: A column choice moves the page and keeps the panel open (BR-9)
  Given Ann's panel is open on «Ромашка» / «все» / «все» / «все»
  When she chooses MARS
  Then the address moves per ADR-0032 § 5 at once, the panel stays open and focus is on the MARS row
  And Esc or a click outside closes the panel and returns focus to the button
  And a search result choice closes the panel and focus goes to the page heading, as in feat-043

Scenario: Choosing another organization
  Given Olga's selection is «Ромашка» / «Логистика» / VENUS / A and she is also a member of «Другая»
  When she chooses «Другая» in the «Организация» column
  Then the page opens the «Обзор» of «Другая» and the panel stays open with the lower columns showing «Другая»'s offers
  And the lower three levels read what she last chose in «Другая», repaired per BR-TD-18 with «Ранее выбранный контекст недоступен. Выбрано: …» if that changed anything, else «все» when she chose nothing there
  And nothing is changed in what she remembered for «Ромашка»

Scenario: A remembered selection that no longer fits is repaired (BR-11)
  Given Olga's remembered selection is «Ромашка» / «Финансы» / VENUS / «все» and VENUS now stands in «Логистика»
  When the shell first reads the offers of «Ромашка» (no panel is opened)
  Then the selection reads «Ромашка» / «Логистика» / VENUS / «все» and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика / VENUS.»

Scenario: A remembered value that is gone or hidden resets to «все»
  Given Olga holds a grant on MARS
  And Olga's remembered selection is «Ромашка» / «Логистика» / MARS / A and A is now hidden from her
  When the shell first reads the offers of «Ромашка»
  Then the selection reads «Ромашка» / «Логистика» / MARS / «все» and she sees «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика / MARS.»

Scenario: A change seen while the panel is open repairs the selection (BR-TD-18, BR-11)
  Given Olga's selection is «Ромашка» / «Логистика» / VENUS / A
  When Ann unlinks VENUS from A and moves VENUS to «Финансы», and the offers are next re-read (opening the panel re-reads them)
  Then the selection reads «Ромашка» / «Финансы» / VENUS / «все» and she sees «Выбор сброшен: проект A.»
  And had her selection been «Ромашка» / «Логистика» / VENUS / «все», the department follows VENUS with no notice

Scenario: An address that does not fit is not a reset
  Given an address carries a `departmentId` that does not fit its object
  Then it answers not found for its level (ADR-0032 § 1) and the selection does not change
```
