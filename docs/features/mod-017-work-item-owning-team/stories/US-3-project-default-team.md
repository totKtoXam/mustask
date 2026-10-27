# US-3: An administrator names a project's default team, or lets the only team be it

- story: US-3
- depends-on: US-1
- shippable: no — one of the seven stories of `mod-017-work-item-owning-team`; its effect is the starting value of US-2's dialogs, and doc-012 US-2 leaves it to this line over a project's own teams
- status: pending

As an **organization administrator**, I want **to name the team that new items of a project start with, or have the project's only team be it**, so that **people creating items outside a team's page start with the team that usually does the work** (BR-MX-11; doc-012 US-2).

## Acceptance criteria

Data as US-1: «Альфа» (A) has MARS and VENUS and names no default; «Бета» (B) has JUPITER; Ann is an «Администратор», U an «Участник». The setting is on the project's «Команды» page, `…/projects/:p/settings/teams`, until `feat-043-area-project-team-navigation`. A project's default team is its settings' named team (marked «По умолчанию»), else its only team (marked «По умолчанию: единственная команда»), else none (BR-OT-4).

```gherkin
Scenario: The administrator chooses the default among the project's teams (BR-OT-4)
  When Ann opens the command «Команда по умолчанию» on A's «Команды» page
  Then the dialog «Команда по умолчанию» has the field «Команда», offering «Не выбрана», «MARS» and «VENUS», and never SATURN, JUPITER or a deleted team
  When Ann chooses «VENUS» and presses «Сохранить»
  Then the notice reads «Команда по умолчанию изменена.», VENUS's row carries «По умолчанию», and MARS's carries nothing
  And a new item created on A's «Рабочие элементы» starts with «VENUS» (US-2)
  And VENUS's own page shows «По умолчанию» beside its name
  And U and V see the same marks, and neither is offered «Команда по умолчанию» (BR-OT-15)

Scenario Outline: A team that is not the project's is refused, and the role decides who sets it (BR-OT-4, BR-OT-15)
  When <who> asks to name <team> the default team of A
  Then <result>, and A's setting is as before

  Examples:
    | who | team                          | result                                              |
    | Ann | JUPITER, a team of B          | it is refused with «Команда не участвует в проекте.» |
    | Ann | an id of no team, SATURN      | it is refused with «Команда не найдена.»             |
    | U   | MARS                          | it is refused with 403 «Недостаточно прав»           |
    | Ann | MARS in a deactivated project | it is refused with «Проект не найден.»               |

Scenario: Two teams and no setting give no default; one team is the default by itself (BR-OT-4)
  Given A's settings name no team
  Then neither MARS nor VENUS is marked, and a new item of A starts with «Без команды»
  Given B has JUPITER only
  Then JUPITER is marked «По умолчанию: единственная команда», and a new item of B starts with it
  When Ann names JUPITER in B's settings
  Then the mark reads «По умолчанию», the named setting winning over the inference

Scenario: Adding a second team ends the inference (BR-OT-4)
  Given the project «Гамма», created a moment ago, has the team created with it, «Гамма», marked «По умолчанию: единственная команда»
  When Ann adds the team «Дельта»
  Then neither team is marked, and a new item of «Гамма» starts with «Без команды», where before it started with «Гамма»

Scenario Outline: Losing the named team clears the setting, and the inference applies again (BR-OT-4, BR-OT-12)
  Given A's settings name VENUS
  When <change>
  Then A's setting is <setting> at the end of that same operation, and A's default is <default>

  Examples:
    | change                               | setting | default                                           |
    | Ann deletes VENUS                    | empty   | MARS, marked «По умолчанию: единственная команда» |
    | Ann chooses «Не выбрана» and saves   | empty   | none, since A has two teams                       |
    | Ann deletes MARS                     | VENUS   | VENUS, marked «По умолчанию»                      |

# Amended by mod-020, 2026-10-18 (OQ-8 B): the default is also the team the switcher opens on when no team is chosen (BR-MX-11; BR-OT-11).
#   Here MARS is chosen, so the scenario holds as written.
Scenario: The default sets the starting value of a creation, and moves no chosen team and no item (BR-MX-11; BR-OT-4)
  Given A has MARS and VENUS, U's team in the switcher is MARS, and A's settings name VENUS
  Then the switcher still names MARS, MARS's and VENUS's backlogs and boards hold the items they held, and the filters offer the same options
  And an item created earlier keeps its team, and an item created from MARS's backlog is MARS's
  And no item is moved when the setting changes

Scenario: The setting survives a deactivated project (ADR-0026 rules 2-3)
  Given A's settings name VENUS
  When Ann deactivates A
  Then A's «Команды» page, and the dialog, answer «Проект не найден.»
  When Ann restores A
  Then VENUS is marked «По умолчанию» again

Scenario: A write racing the deletion never leaves a setting naming a deleted team (BR-OT-13; NFR Concurrency)
  When Ann names VENUS the default while another administrator deletes VENUS
  Then either the naming is refused with «Команда не найдена.», or it lands first and the deletion clears it
  And in neither order does A's setting name a deleted team, and no request answers with a server error
```

## Owed by later lines

```gherkin
# `feat-028-org-level-teams`: unlinking the named team from the project clears the setting in the same operation (doc-012 US-2, BR-MX-11),
#   and no team is created together with a project, so a new project has no default until a team is linked (doc-012 US-2)
# `mod-019-scoped-access`: project «Управление» names the default team, an administrator holding no grant included (BR-MX-23)
```
