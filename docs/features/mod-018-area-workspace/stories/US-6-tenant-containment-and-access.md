# US-6: Tenant containment and permissions on every area route

- story: US-6
- depends-on: US-1, US-2, US-3
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 BR-MX-1, BR-MX-6, ADR-0029 § 4
- status: pending

As the **owner of an organization**, I want **every read and write of an area and of its links to stay inside the organization in the address, and every route to answer by the caller's role**, so that **no tenant sees or changes another's areas, and no role does more than its codes allow (BR-AW-1, BR-AW-15)**.

Cast (spec § Stories): «Ромашка» has Alpha (ALP), MARS and the example tree of US-1. «Другая» has Zeta (ZET), Orion and its own «Логистика», linked to both. Ann is «Администратор» of both organizations. The codes this line registers and redefines are in spec § Roles; they are checked against the organization role alone until `mod-019-scoped-access`.

## Acceptance criteria

```gherkin
Scenario: The structure contract reads areas inside the organization (BR-AW-1; ADR-0028 §§ 4, 8)
  When a module asks the structure contract, in «Ромашка», for «Другая»'s «Логистика», for its linked projects and for Zeta's areas
  Then each reads as nothing: no area, an empty list
  And asked in «Ромашка» for its own «Логистика», for Alpha's areas and for MARS's, each reads as before
  And the contract's own test fails if a by-id read returns an area of another organization: no caller carries the check for it

Scenario Outline: A foreign id gets the answer of an id that names nothing (BR-AW-1, BR-AW-15)
  When Ann sends, in «Ромашка», <request>
  Then the answer is <answer>, identical in status and body to the same request with an id that names nothing
  And nothing changes in «Ромашка» or in «Другая»
  Examples:
    | request                                                                         | answer                    |
    | a read, a rename, a description, a move or a deletion of «Другая»'s «Логистика» | 404 «Область не найдена.» |
    | a creation of a child area under «Другая»'s «Логистика»                         | 404 «Область не найдена.» |
    | a move of «Финансы» under «Другая»'s «Логистика»                                | 404 «Область не найдена.» |
    | linking «Финансы» with Zeta, from the area's page                               | 404 «Проект не найден.»   |
    | linking «Финансы» with Orion, from the area's page                              | 404 «Команда не найдена.» |
    | linking Alpha with «Другая»'s «Логистика», from Alpha's «Общие»                 | 404 «Область не найдена.» |
    | linking MARS with «Другая»'s «Логистика», from MARS's page                      | 404 «Область не найдена.» |
    | unlinking Alpha from «Другая»'s «Логистика»                                     | 404 «Область не найдена.» |
    | creating an area with the project Zeta or the team Orion                        | 404 «Проект не найден.», «Команда не найдена.» |
    | creating a project or a team with «Другая»'s «Логистика»                        | 404 «Область не найдена.» |

Scenario: Names, lists and searches are the organization's own (BR-AW-1, BR-AW-4)
  Then «Другая»'s areas are in no tree, multiselect, picker or path of «Ромашка», and «Ромашка»'s in none of «Другая»
  And the multiselects of «Ромашка» offer neither Zeta nor Orion, whatever is typed
  And Ann can create a top-level «Логистика» in «Ромашка» while «Другая» has one, and a sibling name is unique in its own organization only

Scenario Outline: Every route answers by the caller's role (spec § Roles; BR-AW-7, BR-AW-15)
  Given Max is an «Участник», Vera a «Читатель», and Olga is in no organization
  When each sends <operation> in «Ромашка»
  Then Ann receives <ann>, Max and Vera <others>, and Olga, whatever her instance role, 404 «Организация не найдена.»
  Examples:
    | operation                                                              | ann      | others                  |
    | a read of the tree, of an area's page, of a project's or a team's areas | answered | answered                |
    | a creation of a top-level area, or of a child area                     | done     | 403 «Недостаточно прав» |
    | a rename or a description of an area                                   | done     | 403 «Недостаточно прав» |
    | a move of an area, under an area or to the top level                   | done     | 403 «Недостаточно прав» |
    | a deletion of an area                                                  | done     | 403 «Недостаточно прав» |
    | a link or an unlink of an area and a project, from either page         | done     | 403 «Недостаточно прав» |
    | a link or an unlink of an area and a team, from either page            | done     | 403 «Недостаточно прав» |
    | a creation of an area with projects or teams, or of a project or a team with areas | done | 403 «Недостаточно прав» |

Scenario: A refusal changes nothing, and today's order of answers holds (BR-AW-15; ADR-0029 § 8)
  When any request of this line is sent with no token
  Then it is answered 401 before anything else
  And Vera, who renames, moves or deletes an area that does not exist, or creates one under it, is answered 403, not 404
  And Ann, who sends the same, is answered «Область не найдена.»
  And Ann, who moves «Финансы» under an area that does not exist, or links it with a project that does not exist,
    is answered «Область не найдена.» or «Проект не найден.» after «Финансы» was found
  And a member whose organization membership was removed is answered «Организация не найдена.» on the next request

Scenario: Every route declares its code, and the project-nested routes are gone (BR-AW-12; mod-016 BR-M1-33)
  Then the route inventory lists each area route of this line at the organization's address with its code:
    `organizations.area.read` for the reads; `area.create` for a top-level area; `area.write` for a child area, a rename, a description,
    a move (with `area.create` to or from the top level) and a deletion; `area.link` with `project.link` or `team.link` for a link and an unlink
  And a creation of a project or a team with areas declares its own creation code and the link codes of the ends it names
  And the inventory test fails for a route under `/api/organizations/{organizationId}` that declares no code
  And no route names a project in its address to read, create, rename, move or delete an area: `…/projects/{projectId}/areas` is gone
  And `GET …/access` lists `area.read` for every role, and `area.create`, `area.write` and `area.link` for «Администратор» and for no one else
  And the panel shows each command only to a user who holds its code or codes
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: the same routes for a holder of «Управление» on the area or on one end; the codes checked per area (ADR-0029 § 4)
# `feat-041-project-visibility`: a project the user may not see answers «Проект не найден.» in every row above, and the order becomes BR-MX-24's
```
