# US-1: An item shows its owning team and keeps it apart from everything else on the item

- story: US-1
- depends-on: -
- shippable: no — one of the seven stories of `mod-017-work-item-owning-team`, which doc-012 § Decomposition fixes as one line and one MR; the field has no starting value without US-2 and no view to move between without US-4
- status: pending

As a **user who may change a project's work items**, I want **to see and set the one team responsible for each item, or none**, so that **each team of a shared project works on its own items while every item keeps its project, identifier, assignee and estimates** (doc-012 US-4, SC-02, SC-04).

## Acceptance criteria

Data: organization «Компания» (O). Project «Альфа» (key ALP, A) has the teams MARS and VENUS; MARS is the team created with the project, renamed. Project «Бета» (key BET, B) has the team JUPITER. Organization «Другая» has the team SATURN. ALP-US-12 is a story of A owned by MARS, «Исполнитель» «Анна Смирнова» (a member of MARS), «Оценка в пунктах» 5, third on A's «Истории» backlog, with the child ALP-TASK-13, MARS's; ALP-US-21 has no owning team. U is an organization «Участник», V a «Читатель», Ann an «Администратор». Rules BR-OT-n are the spec's; BR-MX-n are doc-012's. Until `feat-028-org-level-teams` merges a project's teams are its own; "linked" in doc-012 reads "of the project" here.

```gherkin
Scenario: «Команда» is shown wherever the item is (BR-OT-1, BR-OT-7)
  When U opens ALP-US-12 in the card dialog and on its full page
  Then «Команда» «MARS» stands in the band beside «Область»
  And ALP-US-21 shows «Без команды» there
  And the item's read carries its team's id and name, or none, in the card, the create and the save answers

Scenario: The picker offers «Без команды» first, then the project's teams by name (BR-OT-2)
  When U opens «Команда» on ALP-US-12's card
  Then it offers «Без команды», «MARS» and «VENUS», in that order, and nothing else
  And JUPITER (a team of B), a deleted team and SATURN (of «Другая») are not offered
  And a text that matches no team reads «Ничего не найдено»

Scenario: A reader sees the team and cannot change it (BR-OT-15)
  Given V is an organization «Читатель»
  Then «Команда» on ALP-US-12's card is a value with no picker
  And a save that sets it, sent directly, answers 403 «Недостаточно прав» and changes nothing

Scenario: Changing the team changes the team and nothing else (BR-OT-1; BR-MX-9)
  When U sets «Команда» to «VENUS» on ALP-US-12 and saves
  Then the card shows «VENUS», and «Изменён» shows the time of this save
  And ALP-US-12 keeps its identifier, project «Альфа», state, «Исполнитель», «Оценка в пунктах» 5, «Область», parent and child
  And it is still third on A's «Истории» backlog: the project's order is untouched (BR-OT-6)

Scenario: A field not sent keeps its value, and «Без команды» is a choice of its own (BR-OT-2; mod-003 BR-M3-25)
  When U saves ALP-US-12's «Заголовок» only, while Ann sets its «Команда» to «VENUS»
  Then the title is saved and «Команда» is «VENUS»: a field not sent keeps the value stored at the save
  When U sets «Команда» to «Без команды» and saves
  Then ALP-US-12 has no owning team, «Команда» reads «Без команды», and its project, assignee and estimates are as before
  When U saves «Без команды» again
  Then nothing changes and «Изменён» does not move

Scenario Outline: A team the project does not have is refused, and nothing is stored (BR-OT-2)
  When a save of ALP-US-12 that changes «Заголовок» and sets the owning team to <team> is sent
  Then it is refused with <text> under «Команда», the choice kept, and no frame replaces the card
  And ALP-US-12 is unchanged: neither its title nor its team was stored

  Examples:
    | team                                        | text                              |
    | JUPITER, a team of B                        | «Команда не участвует в проекте.» |
    | a team deleted after the card was read      | «Команда не найдена.»             |
    | JUPITER, after B is deactivated             | «Команда не найдена.»             |
    | an id that names no team, or a string that is not an id | «Команда не найдена.» |

Scenario: Another organization's team gets the answer of a team that does not exist (BR-MX-1)
  When one save of ALP-US-12 sets SATURN of «Другая» and another sets an id that names no team
  Then both answers are identical, status and body, and nothing changes in either organization

Scenario: The address is answered before the team (mod-016 api-contract § 0.1; BR-MX-24's order owed by `feat-041-project-visibility`)
  When U sends a save that sets JUPITER for an item of A that does not exist
  Then it answers «Рабочий элемент не найден.»
  When V sends that save for ALP-US-12
  Then it answers 403 «Недостаточно прав», not «Команда не участвует в проекте.»
  And U's save for ALP-US-12 while A is deactivated answers «Проект не найден.»

Scenario: A child may have another team than its parent, in the same project (doc-012 US-4; brief § 6)
  When U sets ALP-TASK-13's «Команда» to «VENUS» and saves
  Then it is accepted, and ALP-US-12 is still MARS's
  When ALP-US-12's «Команда» is later set to «Без команды»
  Then ALP-TASK-13 is still VENUS's: a team change passes to no child and to no parent
  And ALP-TASK-13's «Родитель» is still ALP-US-12, and a parent of B is neither offered nor accepted (feat-001 BR-H-1)

Scenario: The assignee need not belong to the owning team (BR-MX-9, BR-MX-25)
  Given «Ольга Петрова» is a member of VENUS only
  When U sets «Исполнитель» to «Ольга Петрова» on ALP-US-12 (MARS's) and saves
  Then it is accepted: the picker offered her beside the MARS members
  And setting «Команда» to «VENUS» later keeps «Исполнитель» «Ольга Петрова»

Scenario: A person moving between teams moves no item (BR-MX-26; SC-04)
  Given «Анна Смирнова» is the assignee of ALP-US-12
  When Ann removes «Анна Смирнова» from MARS and adds her to VENUS
  Then ALP-US-12 is still MARS's, with «Исполнитель» «Анна Смирнова» and «Изменён» unmoved

Scenario: Saving the item's «Область» changes no team (BR-OT-10)
  When U sets ALP-US-12's «Область» to another area of A and saves
  Then «Область» changes, «Команда» is still «MARS», and ALP-US-12 is on MARS's views as before

Scenario: The team survives every other write of the item (BR-OT-1)
  When U changes ALP-US-12's state by the form and by a card move, and ticks ALP-TASK-13 in ALP-US-12's card checklist
  Then «Команда» is still «MARS» on ALP-US-12 after each, and ALP-TASK-13's team is its own
```

## Owed by later lines

Not verified here; the line named re-runs the clause (spec § Owed by).

```gherkin
# `feat-028-org-level-teams`: "of the project" becomes "linked to the project"
#   - Examples row: VENUS unlinked from A after the card was read → «Команда не участвует в проекте.»
#   - a save or creation that sets MARS while it is being unlinked is reassigned with the rest or refused (BR-MX-12; doc-012 US-3)
# `mod-007-iterations`: doc-012 US-4's sprint scenarios
#   - changing the team clears «Спринт», and the form says so before saving (BR-MX-13)
#   - a sprint that is not an open sprint of the new team is refused
#   - a Closed sprint's outcome keeps an item that changed team
# `mod-019-scoped-access`: the picker and the save need the right to change the item on the item's project (BR-MX-23)
# `feat-015-work-item-history`: the history records each change of owning team (ADR-0028 rule 10)
```
