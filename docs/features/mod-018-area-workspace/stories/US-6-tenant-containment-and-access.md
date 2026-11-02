# US-6: Tenant containment and permissions on every area route

- story: US-6
- depends-on: US-1, US-2, US-3
- shippable: no — one of the six stories of `mod-018-area-workspace`, an L and one MR (spec § Stories); doc-012 BR-MX-1, BR-MX-6, ADR-0029 § 4; reworked for the typed tree (2026-10-27)
- status: pending

As the **owner of an organization**, I want **every read and write of a node of the tree and of its team links to stay inside the organization in the address, and every route to answer by the caller's role**, so that **no tenant sees or changes another's nodes, and no role does more than its codes allow (BR-AW-1, BR-AW-15)**.

Cast (spec § Stories): «Ромашка» has the typed tree of US-1: the departments «Логистика» (linked to MARS), «Финансы», «Инфраструктура»; Alpha's node (ALP) under «Логистика» with «Платформа»; MARS and VENUS. «Другая» has Zeta's node (ZET) under its own department «Логистика», and Orion linked to that department. Ann is «Администратор» of both organizations. Team links join a department (OQ-8 A, OQ-11 A). The codes this line registers and redefines are in spec § Roles; they are checked against the organization role alone until `mod-019-scoped-access`.

## Acceptance criteria

```gherkin
Scenario: The structure contract reads nodes inside the organization (BR-AW-1; ADR-0028 §§ 4, 8)
  When a module asks the structure contract, in «Ромашка», for «Другая»'s «Логистика», for the project nodes below it and for Zeta's node
  Then each reads as nothing: no node, an empty list
  And asked in «Ромашка» for its own «Логистика», for the project nodes below it and for Alpha's node, each reads as before
  And, with Alpha's and Beta's nodes under «Логистика», and Gamma's, deactivated, under «Финансы»,
    the descendants read answers Alpha and Beta for «Логистика», and Alpha for «Логистика / Alpha» and its sub-areas:
    each project once however many nodes lead to it, a deactivated project marked as such, in one statement
  And the contract's own test fails if a by-id read returns a node of another organization: no caller carries the check for it

Scenario Outline: A foreign id gets the answer of an id that names nothing (BR-AW-1, BR-AW-15)
  When Ann sends, in «Ромашка», <request>
  Then the answer is <answer>, identical in status and body to the same request with an id that names nothing
  And nothing changes in «Ромашка» or in «Другая»
  Examples:
    | request                                                                           | answer                    |
    | a read, a rename, a description, a move or a deletion of «Другая»'s «Логистика»   | 404 «Область не найдена.» |
    | a creation of a sub-area under Zeta's node                                        | 404 «Область не найдена.» |
    | a move of Alpha's node under «Другая»'s «Логистика»                               | 404 «Область не найдена.» |
    | a move of «Платформа» under Zeta's node                                           | 404 «Область не найдена.» |
    | creating a project with «Другая»'s «Логистика» as its department                  | 404 «Область не найдена.» |
    | linking «Финансы» with Orion, from the node's page                                | 404 «Команда не найдена.» |
    | linking MARS with «Другая»'s «Логистика», from MARS's page                        | 404 «Область не найдена.» |
    | unlinking MARS from «Другая»'s «Логистика»                                        | 404 «Область не найдена.» |
    | creating a department with the team Orion                                         | 404 «Команда не найдена.» |
    | creating a team with «Другая»'s «Логистика»                                       | 404 «Область не найдена.» |
    | setting an item of Alpha's «Область» to Zeta's node or a sub-area of Zeta's node  | 404 «Область не найдена.» |

Scenario: A foreign project's node, and another project's node, are no item's area (BR-AW-11, BR-AW-1)
  When Ann sets an item of Alpha's «Область» to Beta's node, to «Логистика» or to Zeta's node, by a direct request
  Then each is answered «Область не найдена.», identically to an id that names nothing, and no item changes
  And to Alpha's node and to «Платформа» it is saved

Scenario: Names, lists and searches are the organization's own (BR-AW-1, BR-AW-4)
  Then «Другая»'s nodes are in no tree, picker or path of «Ромашка», and «Ромашка»'s in none of «Другая»
  And the pickers of «Ромашка» offer neither Zeta's node nor Orion, whatever is typed
  And Ann can create a department «Логистика» in «Ромашка» while «Другая» has one, and a sibling name is unique in its own organization only

Scenario Outline: Every route answers by the caller's role (spec § Roles; BR-AW-7, BR-AW-15)
  Given Max is an «Участник», Vera a «Читатель», and Olga is in no organization
  When each sends <operation> in «Ромашка»
  Then Ann receives <ann>, Max and Vera <others>, and Olga, whatever her instance role, 404 «Организация не найдена.»
  Examples:
    | operation                                                                         | ann      | others                  |
    | a read of the tree, of a node's page, of a team's nodes                           | answered | answered                |
    | a creation of a department, or of a sub-area                                      | done     | 403 «Недостаточно прав» |
    | a rename or a description of a node                                               | done     | 403 «Недостаточно прав» |
    | a move of a node, a project's placement included                                  | done     | 403 «Недостаточно прав» |
    | a deletion of a node                                                              | done     | 403 «Недостаточно прав» |
    | a link or an unlink of a department and a team, from either page                  | done     | 403 «Недостаточно прав» |
    | a creation of a department with teams, or of a team with departments              | done     | 403 «Недостаточно прав» |

Scenario: A refusal changes nothing, and today's order of answers holds (BR-AW-15; ADR-0029 § 8)
  When any request of this line is sent with no token
  Then it is answered 401 before anything else
  And Vera, who renames, moves or deletes a node that does not exist, or creates one under it, is answered 403, not 404
  And Ann, who sends the same, is answered «Область не найдена.»
  And Ann, who moves Alpha's node under a department that does not exist, or links «Финансы» with a team that does not exist,
    is answered «Область не найдена.» or «Команда не найдена.» after Alpha's node, or «Финансы», was found
  And a request that is refused by its type or its place, `type_not_allowed`, `outside_project` or `depth_exceeded`, is answered after the address and the role (409), never instead of a 404 or a 403
  And a member whose organization membership was removed is answered «Организация не найдена.» on the next request

Scenario: Every route declares its code, and the project's subtree stays under the tenant rule (BR-AW-12; mod-016 BR-M1-33)
  Then the route inventory lists each node route of this line at the organization's address with its code:
    `organizations.area.read` for the reads; `area.create` for a department; `area.write` for a sub-area, a rename, a description,
    a move (with `area.create` to or from the top level, a project's placement included) and a deletion; for a team link, the code of the end whose route it is
    (`area.link` or `team.link`) with the other end's checked second; for an unlink, the code of the end whose route it is, alone (BR-AW-7)
  And a creation of a team with departments declares its own creation code and `area.link`; a project's creation with a department declares `project.create` and `area.write` on the department (or `area.create` at the top);
    no area route uses `project.link`
  And the inventory test fails for a route under `/api/organizations/{organizationId}` that declares no code
  And a route under a project that reads the project's subtree or writes a sub-area (BR-AW-12) is in the inventory with its code and answers a project or a node of another organization as an id that names nothing
  And `GET …/access` lists `area.read` for every role, and `area.create`, `area.write` and `area.link` for «Администратор» and for no one else
  And the panel shows each command only to a user who holds its code or codes
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: the same routes for a holder of «Управление» on the node or on one end; the codes checked per node (ADR-0029 § 4)
# `feat-041-project-visibility`: a project the user may not see answers «Проект не найден.» in every row above, and the order becomes BR-MX-24's
```
