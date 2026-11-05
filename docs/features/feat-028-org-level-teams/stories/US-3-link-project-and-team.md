# US-3: A manager links a project and a team from either page

- story: US-3
- depends-on: US-1
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories); doc-012 US-3's project–team part
- status: pending

As a **manager of both a project and a team** (until `mod-019-scoped-access`, «Администратор»), I want **to link and unlink them from the page of either end**, so that **one team works on several projects and one project has several teams, with no copy of either, and a team that is not linked can never own the project's items**.

Cast (spec § Stories): Alpha, Beta, Gamma are active; MARS, VENUS, JUPITER are active; SATURN is deleted; Orion belongs to «Другая». The project's end is `…/projects/:p/settings/teams`, the team's `…/settings/teams/:t` (BR-OL-12). Unlinking, which reassigns items, is US-4's. New copy is proposed and fixed by the design.

## Acceptance criteria

```gherkin
Scenario Outline: A link is made from the page of either end (BR-OL-4, BR-OL-5, BR-OL-12; brief § 11.6.5-6)
  Given Alpha and MARS are not linked
  When Ann opens <page>, presses <command>, types part of the other end's name into the searchable multiselect «<label>», picks it and presses «Добавить»
  Then Alpha and MARS are linked, the link records Ann and the time, and both pages list the other end, by name, with a way to open its page: a team's page in the organization's settings, and, until `feat-043-area-project-team-navigation` gives a project a page of its own, the project's «Команды», its end of the link
  Examples:
    | page                                  | command                | label    |
    | Alpha's «Команды»                     | «Добавить команду»     | Команды  |
    | MARS's page, its projects section     | «Добавить проект»      | Проекты  |

Scenario: Several ends are linked in one confirmation (BR-OL-4)
  When Ann picks Alpha, Beta and Gamma together in MARS's «Проекты» and confirms
  Then MARS is linked to all three; and if one of them was deactivated meanwhile, MARS is linked to none of them and the answer is «Проект не найден.»

Scenario: The multiselect offers only what may be linked (BR-OL-1, BR-OL-4)
  Given Alpha is linked to MARS, Beta is deactivated and linked to nothing
  When Ann searches the team multiselect of Alpha's page
  Then it offers VENUS and JUPITER, and never MARS (linked), SATURN (deleted) or Orion (another organization)
  When Ann searches the project multiselect of VENUS's page
  Then it offers Alpha and Gamma, and never Beta (deactivated) or Zeta (another organization)

Scenario: One object, no copies (BR-OL-2, BR-MX-16; SC-01 in miniature)
  Given Alpha has work items
  When Ann links MARS to Alpha, Beta and Gamma
  Then MARS's page lists all three and each of them lists MARS, and the organization's list of teams holds MARS once
  And Alpha has the same work items with the same identifiers, and no team, project or work item was created

Scenario: Linking a linked pair or unlinking an unlinked one changes nothing (BR-OL-4)
  Given Ivan linked Alpha and MARS at time T
  When Ann links them again from either page, and asks to unlink Alpha and VENUS, which are not linked, naming «Без команды» or any linked team as the destination
  Then both answer success, Alpha and MARS are linked once and the link still records Ivan and T, and nothing else changes
  And two requests that link the same unlinked pair at once leave exactly one link, and neither answers with a server error

Scenario: Only active ends are linked (BR-OL-4, BR-OL-9; mod-002 BR-M1-20)
  Given Beta is deactivated and linked to MARS, and SATURN is deleted
  When Ann asks to link Beta with VENUS, or Alpha with SATURN
  Then the first is answered «Проект не найден.», the second «Команда не найдена.», and nothing is linked
  And Beta's link to MARS is unchanged, since a deactivated project keeps its links
  And when a link to MARS runs at once with MARS's deletion, no link to the deleted team remains, whichever finishes first

Scenario: A deactivated project keeps its links and gets them back on restore (mod-002 BR-M1-20, ADR-0026 rules 2-3)
  Given Alpha is linked to MARS and VENUS, its settings name VENUS, and MARS owns two of its items
  When Ann deactivates Alpha
  Then Alpha leaves every list and multiselect, and its address and its items' addresses answer «Проект не найден.»
  And MARS and VENUS no longer list it and their views hold none of its items, yet no link is removed
  When Ann restores Alpha
  Then it is linked to MARS and VENUS again, VENUS is its default, and MARS still owns the two items

Scenario Outline: Only a manager of both ends links (BR-OL-5; spec § Roles)
  Given Alpha and MARS are not linked
  When <user> asks, by any request, to link Alpha and MARS
  Then <answer>

  Examples:
    | user                      | answer                                                           |
    | Ann, «Администратор»      | the link is made                                                 |
    | Max, «Участник»           | 403 «Недостаточно прав», and nothing is linked                   |
    | Vera, «Читатель»          | 403 «Недостаточно прав», and nothing is linked                   |
    | a non-member of «Ромашка» | 404 «Организация не найдена.», and nothing is linked             |

Scenario: Readers read the links and are offered no command (BR-OL-5, BR-OL-14)
  Given Alpha and MARS are linked
  When Max and Vera open Alpha's «Команды» and MARS's page
  Then they read both lists and open the other end's page, and the pages offer them neither «Добавить команду», «Добавить проект» nor «Отключить от проекта»
  And their requests to unlink are refused with 403 «Недостаточно прав», and the link stays
  And a «Читатель» who asks to link a project that does not exist is answered 403, not 404 (today's order)
  And (owed by `mod-019-scoped-access`) a holder of «Управление» on one end only is refused 403, on both ends is allowed

Scenario: A link grants nothing and discloses nothing (BR-OL-5, BR-MX-7)
  Given Vera, a «Читатель», is in MARS
  When Ann links MARS to Beta
  Then Vera still reads Beta's items and changes none, and Max, an «Участник», reads and changes exactly what he did before
  And (owed by `feat-041-project-visibility`) a project the user may not see is neither offered nor listed

Scenario Outline: An owning team must be linked to the project (BR-OL-6, BR-MX-9)
  Given Alpha is linked to MARS and VENUS, and JUPITER is an active team of «Ромашка» linked to no project
  When Max saves ALP-US-12 with a new title and «Команда» <team>
  Then <answer> under «Команда», the choice is kept, and the item keeps its old title and team
  Examples:
    | team                                               | answer                                        |
    | JUPITER                                            | 409 «Команда не участвует в проекте.»         |
    | VENUS, unlinked from Alpha after the card was read | 409 «Команда не участвует в проекте.»         |
    | a team deleted after the card was read             | 404 «Команда не найдена.»                     |
    | Orion of «Другая», or an id that names nothing     | 404 «Команда не найдена.», identical for both |

Scenario: Only linked teams are offered for an item (BR-OL-6)
  Given Alpha is linked to MARS and VENUS, and JUPITER is linked to no project
  When Max opens «Команда» on the card of ALP-US-12, or in «Новый рабочий элемент» on Alpha's list
  Then the picker offers «Без команды» first, then MARS and VENUS by name, and nothing else

Scenario: The default team must be linked, and a link ends the refusal (BR-OL-6, BR-OL-7)
  When Ann names JUPITER the default team of Alpha
  Then she is refused with «Команда не участвует в проекте.» and the setting is unchanged
  When Ann links JUPITER to Alpha and names it again
  Then JUPITER is marked «По умолчанию» on Alpha's «Команды», and a new item from Alpha's «Рабочие элементы» starts with it
  And a team the dialog «Команда по умолчанию» offers is only a linked one, after «Не выбрана»

Scenario: The mark follows the links (BR-OL-7; mod-017 BR-OT-4)
  Given Alpha is linked to no team
  When Ann links MARS
  Then MARS is marked «По умолчанию: единственная команда»
  When Ann links VENUS as well
  Then no team is marked, since two are linked and the settings name none
  When Ann names VENUS
  Then VENUS is marked «По умолчанию» and MARS is not marked
```

## Owed by later lines

```gherkin
# `mod-018-area-workspace`: the area–project and area–team links, and their multiselects on the same pages
# `mod-019-scoped-access`: «Управление» on one end only; the link codes checked per object
# `feat-041-project-visibility`: a hidden project leaves the multiselects, the team's project list and every count
```
