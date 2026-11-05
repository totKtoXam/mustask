# US-2: Projects and teams exist on their own inside the organization

- story: US-2
- depends-on: US-4
- shippable: no — an analysis story; `feat-028-org-level-teams` delivers it, and `mod-017-work-item-owning-team` already ships the default-team setting over a project's own teams (§ Decomposition)
- status: pending

As an **organization administrator**, I want **to create projects and teams that need nothing else to exist, and to staff and delete teams without touching any project**, so that **a project can start with no team or area, and a team that serves several projects exists once**.

Cast: in organization «Ромашка» Ann and Ivan are admins, Max a member and Vera a viewer. Oleg's membership of «Ромашка» was removed. Team "Orion" belongs to another organization. A clause that names the area Logistics is owed by `mod-018-area-workspace`, and one that names a grant or a sprint by `mod-019-scoped-access` or `mod-007-iterations`: that line verifies it. What a holder of «Управление» may do on a project or a team is § Roles'.

## Acceptance criteria

```gherkin
# --- Projects ---
Scenario: A project is created with a name and a key only (BR-MX-2, SC-07; mod-002 BR-M1-1)
  Given «Ромашка» has the teams MARS and VENUS and the area Logistics
  When Ann creates project "Alpha" with key "ALP" and nothing else
  Then "Alpha" is in the organization's project list, with visibility «Организация», no default team and no description
  And it is linked to no team and no area, no team membership was added (Ann's included), and the organization still has exactly MARS, VENUS and Logistics
  And the «Новый проект» dialog no longer says «Так же будут названы первая команда проекта и его корневая область»
  And Max is offered no create action, and a creation request from him is refused with 403

Scenario Outline: Project names and keys keep mod-002 BR-M1-1
  Given "Alpha" (key "ALP") is active and "Old" (key "OLD") is deactivated
  When Ann creates a project named <name> with key <key>
  Then it is refused with <answer>, and nothing is created or linked
  Examples:
    | name      | key   | answer                                            |
    | " alpha " | "BET" | «Проект с таким названием уже есть в организации» |
    | "Beta"    | "OLD" | «Ключ «OLD» уже используется в организации»       |

Scenario: Teams, areas and a description chosen at creation are saved in the same operation (BR-MX-4, BR-MX-5, BR-MX-20)
  When Ann creates project "Beta" (key "BET") with a description, the teams MARS and VENUS and the area Logistics
  Then "Beta" exists with that description, linked to exactly MARS, VENUS and Logistics, each link recording Ann and the time, and nothing else is created
  And a project or a team created with a description of 2 001 characters is refused with «Описание должно содержать не более 2000 символов», and nothing is created

Scenario: A link that cannot be made leaves no project behind (BR-MX-1, BR-MX-5)
  When Ann creates project "Gamma" (key "GAM") with VENUS, deleted while her dialog was open, or with "Orion" of the other organization
  Then it is refused with «Команда не найдена», "Gamma" does not exist, and its name and key stay free

Scenario: A project with no links works through organization roles (SC-07; BR-MX-10, BR-MX-17, BR-MX-23; brief § 9)
  Given "Alpha" is linked to no team and no area, and Max belongs to no team
  When Max creates a work item in "Alpha"
  Then it is saved with «Команда» «Без команды», and the project board and backlog of "Alpha" show it
  And Max can move its card and reorder the backlog, while Vera sees both and can change neither

# --- Teams and their members ---
Scenario: A team is created with a name only (BR-MX-2)
  When Ann creates team "SATURN" with nothing else
  Then "SATURN" is in the organization's team list with no member, project, area or description, and no project, area or team membership is created
  And Max is offered no create action, and a creation request from him is refused with 403

Scenario: A team is created with its optional parts (brief § 11.5; BR-MX-11; ADR-0027 rule 2)
  Given project "Alpha" is linked to no team
  When Ann creates team "JUPITER" with a description, the members Max and Vera, project "Alpha" and the area Logistics
  Then JUPITER has that description and exactly those members, is linked to "Alpha" and Logistics, and nothing else is created
  And, as the only team linked to "Alpha", it is the project's default team, marked «По умолчанию: единственная команда»
  And Vera still only reads the items of "Alpha": belonging to JUPITER adds nothing to her viewer role

Scenario: Only active organization members join a team (BR-MX-20, BR-MX-1)
  When Ann creates a team with member Oleg, or adds Oleg or a user of the other organization to MARS
  Then it is refused with «Добавить в команду можно только активного участника организации», and nothing is created or added

Scenario Outline: A team's name is unique among the organization's active teams (BR-MX-4)
  Given «Ромашка» has the active team MARS and the deleted team NEPTUNE
  When Ann creates a team named <name>
  Then <result>
  Examples:
    | name      | result                                                                |
    | "  mars " | it is refused with «Команда с таким названием уже есть в организации» |
    | "Neptune" | the team is created: a deleted team's name is free again              |
    | "Orion"   | the team is created: a name is unique inside one organization only    |
    | "   "     | it is refused with «Название должно содержать от 1 до 100 символов»   |

Scenario: An admin renames a team and edits its description (BR-MX-4)
  Given «Ромашка» also has the team VENUS
  When Ann renames MARS to "Mars", and sets and then clears its description
  Then each is saved: a name never conflicts with its own team's
  When Ann renames it to " venus"
  Then it is refused with «Команда с таким названием уже есть в организации», and the name stays "Mars"
  And Max is offered no rename, and his rename request is refused with 403

Scenario: People move between teams, and no work moves with them (BR-MX-20, BR-MX-26; SC-04; brief §§ 5.5, 9)
  Given Max is in MARS, in VENUS and in a team of another organization, and is the assignee of ALP-7, which MARS owns
  When Ann removes Max from MARS
  Then Max is still in VENUS, and ALP-7 keeps its project, owning team, assignee and estimates
  When Ann adds Max back to MARS
  Then he is in MARS again, and adding him once more is refused with «Пользователь уже состоит в команде»
  When Ann removes Max from «Ромашка»
  Then neither MARS nor VENUS lists him, and his team in the other organization still does

# --- The project default team ---
Scenario: An admin chooses the default among the linked teams (BR-MX-11, BR-MX-10)
  Given "Alpha" is linked to MARS and VENUS but not to SATURN
  When Ann opens the default-team choice in the settings of "Alpha"
  Then it offers MARS, VENUS and no team, and never SATURN
  When Ann chooses VENUS
  Then VENUS is marked «По умолчанию», and an item created outside a team's surface starts with VENUS
  And a request naming SATURN is refused with «Команда не участвует в проекте.» and changes nothing, and Max's request to set any default is refused with 403

Scenario: Two linked teams and no setting give no default; the one left is inferred (BR-MX-11)
  Given "Alpha" is linked to MARS and VENUS, and its settings name no team
  Then "Alpha" has no default team, neither team is marked, and an item created in its list starts «Без команды»
  When MARS is unlinked from "Alpha"
  Then VENUS is marked «По умолчанию: единственная команда», and a new item starts with VENUS

Scenario Outline: Losing the named team clears the setting, and the inference applies again (BR-MX-11, BR-MX-12)
  Given "Alpha" is linked to MARS and VENUS, and its settings name VENUS
  When <change>
  Then the setting is empty after that same operation, and the default is <default>
  Examples:
    | change                         | default                                           |
    | VENUS is unlinked from "Alpha" | MARS, marked «По умолчанию: единственная команда» |
    | Ann deletes VENUS              | MARS, marked «По умолчанию: единственная команда» |
    | Ann clears the setting         | none, since two teams are linked                  |

# --- Deleting a team, deactivating a project ---
Scenario: The confirmation counts, per linked project, the items that lose their team (BR-MX-12)
  Given MARS is linked to "Alpha", "Beta" and "Gamma", and owns 5 items in "Alpha", 10 in "Beta" (2 of them «Удалена») and none in "Gamma"
  When Ann chooses to delete MARS on its page
  Then «Удаление команды» names "Alpha" — 5, "Beta" — 10 and "Gamma" — 0 items that become «Без команды», and ends «Восстановить команду будет нельзя.»
  And cancelling it changes nothing, and Max is offered no delete action and is refused with 403

Scenario: A team is deleted in one operation (BR-MX-12, BR-MX-2, BR-MX-9, BR-MX-13)
  Given MARS as above has the members Max and Vera
  When Ann confirms the deletion
  Then MARS is linked to no project, and no project, area or work item is deleted
  And its 15 items show «Без команды», keeping their project, identifier, assignee, estimates and project backlog place
  And the memberships of Max and Vera end, MARS is in no list or picker, and its address answers «Команда не найдена»
  And its area links and the grants on it end (owed by `mod-018-area-workspace`, `mod-019-scoped-access`), and its items leave its sprints (owed by `mod-007-iterations`)

Scenario: The deletion reaches a deactivated project too (BR-MX-12, BR-MX-11; ADR-0026 rule 2)
  Given deactivated project "Delta" is linked to MARS, MARS owns 3 of its items, and its settings name MARS
  When Ann deletes MARS, and then restores "Delta"
  Then the «Удаление команды» she confirmed named "Delta" — 3, marked «деактивирован»
  And "Delta" comes back with no link to MARS, its 3 items «Без команды», and no default-team setting

Scenario: A write racing the deletion never leaves work on a deleted team (BR-MX-12; § NFR Concurrency)
  When Max saves ALP-9 with owning team MARS, or Ivan links MARS to another project, concurrently with Ann's deletion of MARS
  Then either that write is refused with «Команда не найдена.», or it lands wholly before the deletion, which then includes it: no item stays owned by MARS and no link names it

Scenario: A deactivated project keeps its links and gets them back on restore (ADR-0026 rules 2-3; BR-MX-5)
  Given "Alpha" is linked to MARS, VENUS and Logistics, and its settings name VENUS
  When Ann deactivates "Alpha"
  Then it leaves every list and picker, link pickers included, and its address, its items' addresses and a request linking it answer «Проект не найден»
  And MARS, VENUS and Logistics no longer list it and their views hold none of its items, yet no link is removed
  When Ann restores "Alpha"
  Then it is linked to MARS, VENUS and Logistics again, VENUS is its default, and its items keep their owning teams
```
