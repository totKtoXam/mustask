# US-1: Teams are the organization's, with pages in its settings

- story: US-1
- depends-on: -
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories)
- status: pending

As an **organization administrator**, I want **teams that belong to the organization and have pages of their own in its settings**, so that **a team exists once, whatever projects it works on, and nobody has to create a project to have one**.

Cast (spec § Stories): MARS is linked to Alpha and Beta, VENUS to no project, NEPTUNE is deleted, Orion belongs to «Другая». The pages are `…/settings/teams` and `…/settings/teams/:t` under «Организация» → «Команды» (BR-OL-12). Copy that no earlier spec quotes is proposed here and fixed by the design.

## Acceptance criteria

```gherkin
Scenario: The organization's teams are listed in its settings (BR-OL-3, BR-OL-12)
  When Ann, Max and Vera each open «Организация» → «Команды»
  Then each sees MARS and VENUS by name, ignoring letter case, and neither NEPTUNE nor Orion
  And each opens a team's page, which shows its name, its «Описание» and its projects
  And only Ann is offered «Новая команда»; Max and Vera are offered no command that changes anything
  And a user who is not a member of «Ромашка», an instance administrator included, is answered «Организация не найдена.»

Scenario: A team is created with a name only (BR-OL-2)
  When Ann creates team "SATURN" with nothing else
  Then "SATURN" is in the list with no description, no member and no project
  And no project, link or team membership was created, Ann's included
  And Max's and Vera's creation requests are refused with 403 «Недостаточно прав» and create nothing

Scenario: A team is created with its optional parts in one operation (BR-OL-3, BR-OL-4; brief § 11.5)
  Given Alpha is linked to no team and its settings name none
  When Ann creates "JUPITER" with a description, the members Max and Vera and the project Alpha
  Then JUPITER has that description and exactly those two members, and is linked to Alpha, the link recording Ann and the time
  And as Alpha's only linked team it is Alpha's default team, marked «По умолчанию: единственная команда»
  And Vera, a member of JUPITER, still only reads Alpha's items: belonging to a team grants nothing
  And an area chosen on the form is owed by `mod-018-area-workspace`

Scenario Outline: A choice that cannot be made leaves no team behind (BR-OL-1, BR-OL-3, BR-OL-4)
  When Ann creates team "ATLAS" with <choice>
  Then it is refused with <answer>, "ATLAS" does not exist and its name is still free
  Examples:
    | choice                                               | answer                                                            |
    | the member Oleg, or a user who is in «Другая» only   | «Добавить в команду можно только активного участника организации» |
    | the project Zeta of «Другая»                         | «Проект не найден.»                                               |
    | a project deactivated while the form was open        | «Проект не найден.»                                               |
    | a description of 2 001 characters                    | «Описание должно содержать не более 2000 символов»                |

Scenario Outline: A team's name is unique among the organization's active teams (BR-OL-3)
  Given «Ромашка» has the active team MARS and the deleted team NEPTUNE
  When Ann creates a team named <name>
  Then <result>
  Examples:
    | name        | result                                                                  |
    | "  mars "   | it is refused with «Команда с таким названием уже есть в организации»   |
    | "Neptune"   | the team is created: a deleted team's name is free again                |
    | "Orion"     | the team is created: a name is unique inside one organization only      |
    | "   "       | it is refused with «Название должно содержать от 1 до 100 символов»    |
    | 101 letters | it is refused with «Название должно содержать от 1 до 100 символов»    |

Scenario: Two creations of one name end with one team (BR-OL-3; spec § NFR Concurrency)
  When Ann and Ivan create "SATURN" at the same moment
  Then one team "SATURN" exists, the other request is refused with «Команда с таким названием уже есть в организации»
  And no request answers with a server error

Scenario: An administrator renames a team and edits its description (BR-OL-3)
  Given «Ромашка» also has the team VENUS
  When Ann renames MARS to "Mars", then sets its description to " Первая строка" + a line break + "Вторая", then clears it
  Then each is saved, the description kept exactly as typed, and a name never conflicts with its own team's
  And the new name shows wherever MARS is listed: both projects' «Команды», the card's picker, the page head's drop-down, the switcher
  When Ann renames it to " venus", or sets a description of 2 001 characters
  Then the first is refused with «Команда с таким названием уже есть в организации», the second with «Описание должно содержать не более 2000 символов», and nothing changes
  And Max's and Vera's rename and description requests are refused with 403 «Недостаточно прав»

Scenario: Members belong to the team, and one list serves every project (BR-OL-11; mod-016 BR-M1-10, BR-M1-36)
  Given Max is in MARS
  When Ann adds Vera to MARS from MARS's page
  Then Max and Vera are listed under «Участники команды» of that page
  And Alpha's and Beta's lists of teams mark MARS as the caller's team for Max and for Vera alike
  And adding Vera again is refused with «Пользователь уже состоит в команде»
  And adding Oleg, or a user who is in «Другая» only, is refused with «Добавить в команду можно только активного участника организации»
  When Ann removes Max from MARS
  Then Max is out of MARS and still in VENUS and in the organization
  And no item changed its project, owning team, assignee or estimates (BR-MX-26)
  When Ann removes Vera from «Ромашка»
  Then MARS no longer lists her, and a team of another organization that she is in still does
  And Max and Vera are offered no add or remove, and their requests are refused with 403

Scenario: A team page does not depend on a project (BR-OL-2)
  Given Ann deactivated Alpha
  When Ann opens MARS's page
  Then it still opens, renames, describes and staffs the team, and lists Beta alone
  And VENUS, linked to no project, has a page whose projects section reads «Команда пока не участвует ни в одном проекте.» and offers Ann «Добавить проект»

Scenario: The project-nested team page and routes are gone (BR-OL-12; ADR-0028 rule 7)
  When anyone opens `…/projects/<Alpha>/settings/teams/<MARS>`, a team Alpha links
  Then the panel answers «Страница не найдена», whether or not the user may see Alpha, and redirects nowhere
  And a read of MARS itself, a rename, a deletion, a member or an owned-items request sent to `…/projects/<Alpha>/teams/<MARS>…` is answered by the platform as a path that does not exist, and changes nothing; MARS's views there stay until `feat-040-team-surfaces-across-projects` (BR-OL-11)
  And `…/projects/<Alpha>/settings/teams` still opens, as Alpha's linked teams (US-3)
```

## Owed by later lines

```gherkin
# `mod-018-area-workspace`: the area chosen on the creation form, and an area's link to the team
# `mod-019-scoped-access`: a team's roles on its page; «Управление» on the team as the right to rename, describe and staff it
# `feat-043-area-project-team-navigation`: the top-level «Команды»; the page's other sections; the page moves out of the organization's settings
```
