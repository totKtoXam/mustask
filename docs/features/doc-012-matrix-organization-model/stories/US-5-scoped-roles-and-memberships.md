# US-5: A manager grants scoped roles, and areas pass them down

- story: US-5
- depends-on: US-1, US-2, US-3, US-7
- shippable: no — analysis only; delivered by `mod-NNN-scoped-access`
- status: pending

As a **holder of «Управление» on an area, a project or a team**, I want **to give organization members a role of
that object's scope, and to have an area's grants reach the areas below it**, so that **each person can do what
their work needs on the objects it concerns, and nothing more (BR-MX-20, BR-MX-22, BR-MX-23)**.

## Acceptance criteria

Every project here has the visibility «Организация», as every project has until `feat-NNN-project-visibility`
merges (ADR-0029 § 8), so U reads every project through the organization role, and a grant shows in what U
may change. What grants decide about seeing a project is US-6's. Sprint management by a team grant is US-9's.

```gherkin
Background:
  Given an organization with the area «Логистика» and its children «Складские системы» and
    «Транспортные системы»; «Наследовать доступ» is on for the first child and off for the second
  And project WMS linked to «Складские системы», project TMS linked to «Транспортные системы», and project P linked to no area
  And teams MARS and VENUS, both linked to WMS and to P
  And U is an active member whose organization role is «Читатель», holding no grant: U reads every project's items and changes none

Scenario Outline: A manager gives a member a role of the object's scope
  Given M holds «Управление» on <object>, or is an organization admin holding no grant
  When M opens the object's grants and picks U
  Then exactly <roles> are offered, and none of them denies anything
  And the grant M gives is listed with U, the role, M as the one who gave it, and the date and time
  And U's next request is decided with that grant (BR-MX-20, BR-MX-23)

  Examples:
    | object      | roles                               |
    | «Логистика» | «Чтение», «Изменение», «Управление» |
    | WMS         | «Чтение», «Изменение», «Управление» |
    | MARS        | «Планирование», «Управление»        |

Scenario: Without «Управление» there is no grant to give (brief § 10.4 items 1-2, BR-MX-24)
  Given U holds «Изменение» on «Логистика», and member N holds no grant
  When U opens the grants of «Логистика», and of WMS, whose items the grant lets U change
  Then no control to give, change or remove a role is shown on either
  And U's direct request to give N a role on either answers 403 «Недостаточно прав» and creates no grant

Scenario: Only an active member of this organization can be given a role
  Given E is a member of another organization only, and D's membership here was removed
  When M, holding «Управление» on «Логистика», looks for E and D in its grant picker
  Then neither is offered; a direct request naming either is refused, creating no grant (BR-MX-1, BR-MX-20)

Scenario: A manager changes a role, then removes the grant
  Given U holds «Изменение» on «Логистика», given by M, and U's change to a WMS item is kept
  When M changes it to «Чтение»
  Then U's next change to a WMS item answers 403 «Недостаточно прав», and U still reads the item
  When M changes it back to «Изменение», U changes the item, and M then removes the grant
  Then U's change is kept, U is no longer listed, and U's next change answers 403 again (BR-MX-20)

Scenario: Each «Управление» manages its own object and nothing else (brief §§ 10.2, 11.5; BR-MX-4, BR-MX-6, BR-MX-11)
  Given K, an organization «Участник», holds «Управление» on project WMS and on team MARS, and no other grant
  When K renames WMS, sets and clears its description, and names MARS its default team
  And K renames MARS, sets and clears its description, and adds and removes its members
  Then each is saved
  And the same on P or on VENUS, deleting MARS, and creating a project or a team each answer 403 «Недостаточно прав»

Scenario: An area's participants show direct and inherited grants
  Given U holds «Чтение» on «Логистика», and V holds «Изменение» on «Складские системы»
  When a member opens the participants of «Складские системы»
  Then V is listed as a direct grant, and U as inherited, marked with «Логистика» as its source
  And U's line offers no change or removal there: it is changed only on «Логистика»
  And the participants of «Транспортные системы» do not list U (BR-MX-22)

Scenario: The head of an area changes what inherits from it, as the switches say (SC-03, SC-08)
  Given U holds «Изменение» on «Логистика» only
  When U changes an item of WMS, moves its card on WMS's board, and changes an item of TMS
  Then the WMS change and move are kept, and the TMS change answers 403 «Недостаточно прав»
  When an organization admin switches «Наследовать доступ» on for «Транспортные системы»
  Then U's next change to a TMS item is kept, with no sign-out
  When the admin switches it off again
  Then U's next change to a TMS item answers 403 (BR-MX-22, BR-MX-23)

Scenario: Inheritance switched off stops what comes from above, not what is held there (SC-08)
  Given U holds «Управление» on «Логистика», and V, also a «Читатель», holds «Изменение» on «Транспортные системы»
  When U and V each change an item of TMS
  Then U's change answers 403, and V's is kept
  And U manages the grants of «Складские системы» but is shown no grant control on «Транспортные системы»
  And a child of «Транспортные системы» whose inheritance is on receives V's grant and not U's (BR-MX-22)

Scenario: A project in two areas is changed by one area's grant, and the other gives nothing (SC-06, brief § 10.4)
  Given areas A and B; project X linked to both and project Y to B only
  And U holds «Изменение» on A only
  When U changes an item of X and an item of Y
  Then the change to X is kept, and the change to Y answers 403 «Недостаточно прав» (BR-MX-22)

Scenario: A team linked to two areas carries nothing between them (SC-05)
  Given areas A and B, both linked to MARS; project X linked to A and project Q to B, both linked to MARS, and MARS owns items of each
  And U holds «Изменение» on A and «Управление» on MARS
  When U opens MARS's board and moves a card of X and a card of Q
  Then the board offers U moves on the cards of X only; the move of X's card is kept, and a direct move of Q's answers 403 «Недостаточно прав» (BR-MX-7, BR-MX-22)

Scenario: A team grant reaches the team and none of its projects' items, a sprint close aside (US-12)
  Given U holds «Планирование» on MARS, and MARS owns items of WMS and P
  When U reorders MARS's backlog, changes an item of P and of WMS, and reorders P's own backlog
  Then the reorder of MARS's backlog takes effect, and each of the others answers 403 «Недостаточно прав»
  And so does putting an item of WMS into a sprint of MARS (owed by `mod-007-iterations`)
  And a «Читатель» who is a member of MARS with no grant is shown no reorder control on MARS's backlog,
    and the same reorder answers 403: belonging to a team grants nothing (§ Roles)

Scenario: Codes are only added (BR-MX-23; SC-07)
  Given project R; U holds «Изменение» on P
  When U saves a change to an item of P and to an item of R
  Then the first is kept, and the second answers 403 «Недостаточно прав»
  And an organization «Участник» holding «Чтение» on P still changes P's items: no grant takes a code away
  And a grant on project Z, linked to no area and no team, works the same: a grant reaches a project nothing links

Scenario: The order of the grants does not change the result (brief § 10.4 item 6)
  Given two copies of this organization, where U receives «Чтение» on «Логистика», «Изменение» on WMS
    and «Планирование» on MARS in one order in the first copy and in the reverse order in the second
  When U's effective permissions are read on every area, project, team and item of both copies
  Then they are identical, and stay identical after one grant is removed and given again (BR-MX-23)

Scenario: Every surface agrees, and the server decides (brief § 10.4 items 1-3)
  Given U holds «Чтение» on «Складские системы», and MARS owns item W of WMS
  When U opens WMS's list, its board, MARS's backlog and W, and sends a change of W past the panel
  Then W is on every surface, no surface offers a change control, and the direct change answers 403 and changes nothing
  When U is given «Изменение» there instead
  Then the next load of each page offers the change controls, and the direct change is kept
  And after the grant is removed, the controls are gone at the next load, and the direct change answers 403 again

Scenario: A person moves from MARS to VENUS (SC-04)
  Given U is a member of MARS holding «Планирование» on it, and is the assignee of MARS items of P
  When U is removed from MARS and its grant, and added to VENUS with «Планирование» on VENUS
  Then those items keep their project, owning team, assignee and estimates, and no project, area or link changes
  And on U's next request U reorders VENUS's backlog, and a reorder of MARS's answers 403 (BR-MX-26)

Scenario: Removing the organization membership ends every grant and team membership (BR-MX-20)
  Given U holds grants on «Логистика», WMS and MARS, and is a member of MARS
  When an organization admin removes U from the organization
  Then U is listed on none of those objects and in no team, and adding U again gives back none of them

Scenario: A grant removal applies from the next check (§ Non-functional, Concurrency)
  Given U holds «Изменение» on WMS only
  When the grant is removed while U's save of a WMS item is in flight
  Then a save whose check ran before the removal committed is kept, a save checked after it answers 403
    «Недостаточно прав» and leaves the item unchanged, and no save is half-applied

Scenario: The order of the answers (BR-MX-24)
  When a change to an item of P is requested with no sign-in, by a signed-in user who is not a member,
    and by a member whose organization role is «Читатель» and who holds no grant
  Then they answer 401, «Организация не найдена» and 403 «Недостаточно прав», and nothing changes
  And that «Читатель» changing an item id that names nothing in P answers «Рабочий элемент не найден.», not 403
```
