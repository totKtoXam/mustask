# US-2: A project filter on a team's views

- story: US-2
- depends-on: US-1
- shippable: no — it filters the cross-project views US-1 brings, in the same MR
- status: pending

As a **member who plans a team's work**, I want **to narrow the team's backlog or board to some of its projects**, so that **I can look at one project's share of the team's work without leaving the team's order**.

## Acceptance criteria

The common cast of spec § Stories; MARS owns 5 stories in Alpha, 10 in Beta and 7 in Gamma. Rules: BR-TS-8, BR-TS-7 (a filtered reorder). The filter's place in the address is OQ-2 A.

```gherkin
Scenario: The filter offers the team's projects and narrows the view (BR-TS-8; doc-012 US-7)
  When Max opens MARS's backlog
  Then a «Проект» filter offers «Alpha», «Beta» and «Gamma», by name, with no counts, and nothing chosen
  And Delta, linked to no team, and Zeta, of «Другая», are not offered
  When Max chooses «Beta»
  Then the backlog shows the 10 of Beta, in MARS's order, and its total reads 10
  When Max also chooses «Alpha»
  Then it shows the 15 of Alpha and Beta, in MARS's order
  And MARS's board narrowed the same way shows the same cards in its columns

Scenario: VENUS's filter offers its one project (BR-TS-8)
  When Max opens VENUS's board
  Then «Проект» offers «Alpha» alone

Scenario: The filter narrows the view and changes nothing (BR-TS-8; mod-017 BR-OT-8)
  Given Max's MARS backlog is filtered to «Beta»
  When Ann opens MARS's backlog at the same time
  Then Ann sees all 22
  And no item, owning team, backlog place or project order has changed

Scenario: The tree is built over matching items, and a checklist is whole (BR-TS-8)
  Given story S of Beta, owned by MARS, has two MARS tasks, and its card's checklist lists them
  When Max filters MARS's backlog and board to «Beta»
  Then S stands with both tasks beneath it, and S's card lists both in its checklist
  When Max filters to «Alpha»
  Then S and its tasks are not shown

Scenario: A filtered backlog reorders against the full list (BR-TS-7, -8)
  Given MARS's backlog lists ALP-US-1, BET-US-1, ALP-US-2, BET-US-2, BET-US-3 in that order
  When Max, filtered to «Beta», moves BET-US-3 directly after BET-US-1
  Then the filtered view lists BET-US-1, BET-US-3, BET-US-2
  And unfiltered, MARS's backlog lists ALP-US-1, BET-US-1, BET-US-3, ALP-US-2, BET-US-2
  And Alpha's and Beta's own backlogs keep their orders

Scenario: A value that names no linked active project selects nothing (BR-TS-8)
  When the filter's value in the page's address is an unknown id, "not-a-guid", Zeta's id, Delta's id (linked to no team), or Alpha's id after Ann unlinked Alpha from MARS
  Then the view shows no item and a total of 0, every case alike, in the filtered-empty frame with «Сбросить фильтр», not the empty state of a team that owns nothing
  And the filter shows that it is on and offers to clear it, and nothing in the page says which of these it was or names a project
  When Max clears it
  Then the view shows all 22, and the address carries no filter
  When the address names Beta's id and an unknown id
  Then the view shows the 10 of Beta

Scenario: A deactivated project leaves the options and its value selects nothing (BR-TS-2, -8)
  Given Max's MARS backlog is filtered to «Gamma»
  When Ann deactivates Gamma and Max's view is read again
  Then «Gamma» is no longer offered, the view shows no item in the filtered-empty frame, and the filter still shows that it is on
  And when Ann restores Gamma, the same address shows Gamma's 7 again

Scenario: The filter lives in the page's address (BR-TS-8; OQ-2 A)
  Given Max filtered MARS's backlog to «Beta»
  When Max reloads the page, or Vera opens the same address
  Then the backlog shows the 10 of Beta with «Beta» chosen
  When Max changes the level to «Фичи»
  Then the filter still names «Beta» and the view shows MARS's features of Beta
  When Max presses «Показать канбан»
  Then MARS's board of «Фичи» opens with «Beta» still chosen
  When Max opens VENUS's backlog from the head's drop-down, or chooses VENUS in the switcher
  Then VENUS's view starts with no filter
  When Max, back on MARS's board filtered to «Beta», presses «Бэклоги»
  Then the entry's landing, MARS's «Истории» backlog, opens with no filter

Scenario: The filter works by keyboard and on a narrow screen (spec NFR accessibility)
  When Max reaches the filter by Tab, opens it, chooses and clears a value by keyboard
  Then each choice narrows the view as above, and focus stays on the filter
  And at 640 CSS px the filter and the view fit with no horizontal page scroll
```
