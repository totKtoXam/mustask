# US-2: A project starts with no team, and has a description

- story: US-2
- depends-on: US-1, US-3
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories)
- status: pending

As an **organization administrator**, I want **to create a project that needs no team, and may carry a description and the teams I choose on the form**, so that **a project can start working before anyone decides who its teams are, and every project says what it is for**.

Cast (spec § Stories): the teams MARS and VENUS exist and are linked to no project; project Old (key OLD) is deactivated. A project's views and the shell's treatment of a team-less project are feat-039's (BR-PB-1..-9), kept; this story delivers BR-PB-9's clause "once feat-028 merges it has none". New copy is proposed and fixed by the design.

## Acceptance criteria

```gherkin
Scenario: A project is created with a name and a key only (BR-OL-2, SC-07; mod-002 BR-M1-1)
  When Ann creates project "Alpha" with key "ALP" and nothing else
  Then "Alpha" is in the organization's project list, with no description and no linked team
  And no team was created and no team membership was added, Ann's included: no list of teams marks a team as hers
  And the organization still has exactly MARS and VENUS
  And the «Новый проект» dialog's helper under «Название» reads «От 1 до 100 символов. Так же будет названа корневая область проекта.», no longer naming a team
  And Max and Vera are offered no «Новый проект», and their requests are refused with 403 «Недостаточно прав»

Scenario Outline: Project names and keys keep mod-002 BR-M1-1
  Given "Alpha" (key "ALP") is active and "Old" (key "OLD") is deactivated
  When Ann creates a project named <name> with key <key>
  Then it is refused with <answer>, and nothing is created or linked
  Examples:
    | name      | key   | answer                                            |
    | " alpha " | "BET" | «Проект с таким названием уже есть в организации» |
    | "Old"     | "BET" | «Проект с таким названием уже есть в организации: он деактивирован» |
    | "Beta"    | "OLD" | «Ключ «OLD» уже используется в организации»       |

Scenario: A description and teams chosen on the form are saved in the same operation (BR-OL-3, BR-OL-4)
  When Ann creates project "Beta" (key "BET") with a description and the teams MARS and VENUS from the multiselect «Команды»
  Then "Beta" has that description and is linked to exactly MARS and VENUS, each link recording Ann and the time
  And neither team is "Beta"'s default: two teams are linked and its settings name none
  And nothing else is created, and an area chosen on the form is owed by `mod-018-area-workspace`

Scenario Outline: A choice that cannot be made leaves no project behind (BR-OL-1, BR-OL-4)
  When Ann creates project "Gamma" (key "GAM") with <choice>
  Then it is refused with <answer>, "Gamma" does not exist, and its name and key stay free
  Examples:
    | choice                                         | answer                                             |
    | the team Orion of «Другая»                     | «Команда не найдена.»                              |
    | VENUS, deleted while the form was open         | «Команда не найдена.»                              |
    | a description of 2 001 characters              | «Описание должно содержать не более 2000 символов» |

Scenario: A project without a team works through the organization's roles (SC-07; BR-OL-10, BR-MX-10)
  Given "Alpha" is linked to no team, and Max is in no team
  When Max creates a work item in "Alpha" from its «Рабочие элементы»
  Then the dialog's «Команда» reads «Без команды», and the item is saved with it
  And the item is on "Alpha"'s project backlog and board, which open at their addresses
  And Max can move its card and reorder the project backlog, while Vera sees both and changes neither
  And the card's team picker offers «Без команды» alone

Scenario: The shell on a project with no team (feat-039 BR-PB-9; OQ-2 A)
  Given "Alpha" has no linked team
  When Max opens "Alpha"'s «Рабочие элементы»
  Then the switcher holds no team, the team-scoped entries «Бэклоги» and «Канбан» are hidden, none is current, and the page works
  And "Alpha"'s backlog at `…/projects/<Alpha>/backlogs/stories` and its board at `…/boards/stories` open and work
  When Max opens "Alpha"'s «Команды»
  Then the page reads «В проекте пока нет команд.» and offers Max «Бэклог проекта» and «Доска проекта», which open those pages
  And Ann is also offered «Добавить команду»; Max and Vera are not
  When Ann links MARS to "Alpha"
  Then «Бэклоги» and «Канбан» appear for the project, and the empty page gives way to the list

Scenario: An administrator edits a project's description (BR-OL-3)
  When Ann opens "Alpha"'s «Общие» and sets «Описание», changes it, then clears it
  Then each is saved, the text kept exactly as typed, and "Alpha"'s name, key and links are unchanged
  When Ann sets a description of 2 001 characters
  Then it is refused with «Описание должно содержать не более 2000 символов» and the stored description is unchanged
  And Max and Vera read the description, are offered no edit, and their requests are refused with 403 «Недостаточно прав»
  And the description of "Alpha" survives its deactivation and restoration

Scenario: Renaming a project changes neither its description nor its links (BR-OL-3; mod-002 BR-M1-11)
  Given "Alpha" has a description and is linked to MARS
  When Ann renames it to "Альфа"
  Then the description and the link are as before, and the root area takes the new name
```

## Owed by later lines

```gherkin
# `mod-018-area-workspace`: an area chosen on the creation form is linked in the same operation
# `feat-041-project-visibility`: the visibility chosen on the form; a project the user may not see is absent from the team pickers' counts
# `feat-043-area-project-team-navigation`: the way into a team-less project's views from the shell; the interim links of «Команды» retire
```
