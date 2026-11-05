# US-4: An item on a team view is read and written in its own project

- story: US-4
- depends-on: US-1
- shippable: no — a team view across projects (US-1) is only safe to act on with it, in the same MR
- status: pending

As a **member who delivers a team's work**, I want **every card I open, move or extend on a team's view to act on the item in its own project**, so that **two items with the same number in different projects are never mixed up, and nothing is written to the wrong project**.

## Acceptance criteria

The common cast of spec § Stories. Rules: BR-TS-4, BR-TS-5. The card dialog's not-found frame is feat-029's: «Рабочий элемент не найден», «Проверьте адрес или закройте окно.», «Закрыть».

```gherkin
Scenario: A card opened on a team view opens in its own project (BR-TS-4; doc-012 US-7)
  Given MARS owns story SA of Alpha and story SB of Beta, ALP-US-3 and BET-US-3
  When Max opens SB's card from MARS's board and saves a new title
  Then the card shows BET-US-3 with «Проект» «Beta», the title is written to BET-US-3, and ALP-US-3 is unchanged
  And the address reads …/teams/<MARS>/boards/stories?workitem=BET-US-3
  When Max opens SA's row title on MARS's backlog
  Then the card shows ALP-US-3 with «Проект» «Alpha»

Scenario: A card opened by address is read in the project its key names (BR-TS-4)
  When Max opens …/teams/<MARS>/backlogs/stories?workitem=BET-US-3
  Then the card of BET-US-3 opens over MARS's backlog
  When Max opens …/teams/<MARS>/backlogs/stories?workitem=ALP-US-8, ALP-US-8 being owned by VENUS
  Then the card of ALP-US-8 opens too, though MARS's backlog does not hold it

Scenario: An identifier that names no readable item shows the not-found frame (BR-TS-4)
  When Max opens MARS's backlog with ?workitem= set to GAM-US-1 while Gamma is deactivated, ZET-US-1 of «Другая», ALP-US-999 that does not exist, ALP-BUG-3 where item 3 of Alpha is a story, XYZ-US-1 of no project, or "garbage"
  Then each shows the dialog's not-found frame over MARS's backlog, the six alike
  And nothing in it names Gamma, Zeta or the reason
  And «Закрыть» leaves MARS's backlog with no ?workitem= in the address

Scenario: Project views read the card in the page's project, as before (BR-TS-4; feat-028's re-check)
  When Max opens BET-US-3's card from Beta's board
  Then it shows BET-US-3 of Beta, read and written in Beta
  And Alpha's backlog with ?workitem=BET-US-3 shows the not-found frame, as today

Scenario: A card move writes the item's state in its own project (BR-TS-5; doc-012 US-7)
  Given MARS owns story SB of Beta in «Новая»
  When Max moves SB's card to «Активна» on MARS's board
  Then SB is in «Активна», still in Beta, owned by MARS, with its identifier and both backlog places unchanged
  And the next read of MARS's and Beta's backlogs and boards shows SB on each exactly once, in «Активна»

Scenario: A move offers only the states of the card's own type (BR-TS-5; BR-MX-19; mod-006 BR-M4-18)
  Given MARS's board shows stories and bugs of Alpha, Beta and Gamma
  When Max opens it and drags a card
  Then the columns are those the organization's process gives the level, and the read changes no item's state
  And only columns that map a state of the card's type are drop targets
  And a direct write of a state of another type is refused with «Этот статус недоступен для рабочего элемента этого типа.», and nothing changes

Scenario: A child added from a row or a card lands in the parent's project with its team (BR-TS-5; mod-017 BR-OT-3)
  Given MARS owns story SB of Beta
  When Max adds a task with the row's «+» on MARS's backlog, and another with «Добавить задачу» on SB's card on MARS's board
  Then both tasks are created in Beta, with Beta's next numbers, SB as parent and MARS as team
  And both stand beneath SB on MARS's backlog and in SB's checklist

Scenario: A row dropped onto a row of another project is a refused target (BR-TS-5; feat-027 BR-T-12)
  Given MARS's «Истории» backlog shows story SA of Alpha and bug BB of Beta
  When Max drags BB over SA
  Then SA is marked refused, releasing sends nothing, and the notice says the parent must be of the same project (proposed: «Родителем может быть только элемент того же проекта»)
  And a direct parent write naming SA as BB's parent answers «Родитель не найден в этом проекте.», and nothing changes
  When Max opens «Изменить родителя…» on BB
  Then it offers parents of Beta only

Scenario: «Читатель» reads cards and cannot change them (BR-TS-13)
  When Vera opens MARS's board and SB's card
  Then she sees the card, the drag is not offered, the row's «+» and «Добавить задачу» are not offered
  And her direct state write and her direct child creation are refused with 403 «Недостаточно прав», and nothing changes
```
