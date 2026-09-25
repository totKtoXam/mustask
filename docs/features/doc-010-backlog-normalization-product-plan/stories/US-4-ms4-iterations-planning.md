# US-4: MS-4 «Итерации и планирование» (MVP complete)

- story: US-4
- depends-on: US-3
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-4 «Итерации и планирование»**, so that
**a team can plan its work into sprints, see what is current and carry its
unfinished work forward — and the MVP is complete**.

Delivered by `mod-007-iterations`, `feat-008-iteration-filter`,
`feat-009-iteration-planning-pane` and `feat-010-move-unfinished-work`.

## Acceptance criteria

```gherkin
Scenario: Demo — plan a sprint and carry its unfinished rest
  Given MS-3's team «Платформа» with stories on its «Истории» backlog
  And the administrator has created «Релиз 1» with the sprints «Спринт 1»,
    dated to cover today, and «Спринт 2»
  And the administrator has selected all three for «Платформа»
  When «Анна Смирнова» opens «Планирование» on the «Истории» backlog and drags
    three stories onto «Релиз 1 / Спринт 1»
  Then each of the three has the «Итерация» «Релиз 1 / Спринт 1», and each
    has the change in its history
  When she filters the «Истории» board by «Текущая итерация»
  Then exactly the three stories are shown
  When one of them reaches «Закрыта»
  And she chooses «Перенести незавершённые» on «Спринт 1», picks «Спринт 2»
    and confirms a prompt stating that 2 items will be moved
  Then the two unfinished stories point at «Спринт 2»
  And the closed one still points at «Спринт 1»
  And the demos of MS-1..MS-3 still pass on the same stand

Scenario: Guard — a viewer plans nothing
  Given a user whose role in the organization is viewer
  When they open «Планирование» of «Платформа»
  Then they see the iterations and the items in them
  And they are offered neither a drop onto an iteration nor «Перенести
    незавершённые»
  And the server refuses such a request sent to it directly
```
