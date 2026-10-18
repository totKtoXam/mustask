# US-8: Tenant containment and permissions on every step

- story: US-8
- depends-on: US-1, US-3, US-4, US-5
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories); doc-012 BR-MX-1 and mod-017 finding S-2, which the human put into this line on 2026-10-17
- status: pending

As the **owner of an organization**, I want **every read and write of a team to stay inside the organization in the address, and every step to answer by the caller's role**, so that **no tenant sees or changes another's teams, links or projects, and no role does more than its codes allow**.

Cast (spec § Stories): «Ромашка» has Alpha (ALP), MARS and VENUS. «Другая» has Zeta (ZET), linked to its team Orion. Ann is «Администратор» of both organizations. The four codes this line registers and the narrowed `organizations.team.write` are in spec § Roles; they are checked against the organization role alone until `mod-019-scoped-access`.

## Acceptance criteria

```gherkin
Scenario: The structure contract reads inside the organization (BR-OL-1; mod-017 S-2; ADR-0028 §§ 4, 8)
  When a module asks the structure contract, in «Ромашка», for project Zeta, for team Orion, for Zeta's linked teams and for Orion's linked projects
  Then each reads as nothing: no project, no team, an empty list
  And asked in «Другая» for Alpha or MARS, each reads as nothing as well
  And asked in «Ромашка» for Alpha and MARS, and in «Другая» for Zeta and Orion, each reads as before, with its status
  And the contract's own test fails if a by-id read returns an object of another organization, whatever the callers compare: no caller carries the check for it

Scenario Outline: A foreign id gets the answer of an id that names nothing (BR-OL-1, BR-OL-14)
  When Ann sends, in «Ромашка», <request>
  Then the answer is <answer>, identical in status and body to the same request with an id that names nothing
  And nothing changes in «Ромашка» or in «Другая»
  Examples:
    | request                                                                     | answer                        |
    | a rename, a description, a deletion or an owned-items count of team Orion     | 404 «Команда не найдена.»     |
    | adding a member to Orion, or reading Orion's members                          | 404 «Команда не найдена.»     |
    | linking Alpha with Orion, from Alpha's page                                   | 404 «Команда не найдена.»     |
    | linking Zeta with MARS, from MARS's page                                      | 404 «Проект не найден.»       |
    | unlinking MARS from Alpha, naming Orion as the destination                    | 404 «Команда не найдена.»     |
    | creating a team with the project Zeta                                         | 404 «Проект не найден.»       |
    | creating a project with the team Orion                                        | 404 «Команда не найдена.»     |
    | saving ALP-US-12 with the owning team Orion                                   | 404 «Команда не найдена.»     |
    | naming Orion the default team of Alpha                                        | 404 «Команда не найдена.»     |
    | opening Orion's backlog at Alpha's address                                    | 404 «Команда не найдена.»     |

Scenario: Names and lists are the organization's own (BR-OL-1, BR-OL-3)
  Then Orion is in no list, multiselect or picker of «Ромашка», and MARS is in none of «Другая»
  And Ann can create a team "Orion" in «Ромашка», or rename MARS to "Orion", although «Другая» has one
  And a project's key and name, and a team's name, are unique in their own organization only

Scenario Outline: Every step answers by the caller's role (spec § Roles; BR-OL-5, BR-OL-14)
  Given Max is an «Участник», Vera a «Читатель», and Olga is in no organization
  When each sends <operation> in «Ромашка»
  Then Ann receives <ann>, Max <max>, Vera <vera>, and Olga, whatever her instance role, 404 «Организация не найдена.»
  Examples:
    | operation                                                     | ann      | max                     | vera                    |
    | a read of the teams, of a team, of a project's linked teams   | answered | answered                | answered                |
    | a creation of a team                                          | done     | 403 «Недостаточно прав» | 403 «Недостаточно прав» |
    | a rename or a description of a team                           | done     | 403 «Недостаточно прав» | 403 «Недостаточно прав» |
    | an addition or a removal of a team member                     | done     | 403 «Недостаточно прав» | 403 «Недостаточно прав» |
    | a deletion of a team, and the count it announces              | done     | 403 «Недостаточно прав» | 403 «Недостаточно прав» |
    | a link or an unlink of a project and a team, and the count the unlink announces | done | 403 «Недостаточно прав» | 403 «Недостаточно прав» |
    | a creation of a team with projects, or of a project with teams | done    | 403 «Недостаточно прав» | 403 «Недостаточно прав» |
    | a naming of a project's default team                          | done     | 403 «Недостаточно прав» | 403 «Недостаточно прав» |
    | a description of a project                                    | done     | 403 «Недостаточно прав» | 403 «Недостаточно прав» |

Scenario: A refusal changes nothing, and the order of today's answers holds (BR-OL-14)
  When any request of this line is sent with no token
  Then it is answered 401 before anything else
  And a «Читатель» who sends a team write naming a team that does not exist is answered 403, not 404
  And Ann, who asks to link Alpha with a team that does not exist, is answered «Команда не найдена.» after Alpha's address was found
  And a member whose organization membership was removed is answered «Организация не найдена.» on the next request

Scenario: Every route declares its code, and the old routes are gone (BR-OL-1, BR-OL-12; mod-016 BR-M1-33)
  Then the route inventory lists each route of this line with its code: `organizations.team.read` for the reads, `team.create`, `team.write`, `team.delete` (the deletion and its counts), `team.link` together with `project.link` (a link, an unlink and the unlink's count), and `project.write` for the default team and a project's description
  And the inventory test fails for a route under `/api/organizations/{organizationId}` that declares no code
  And no route that reads, renames, describes, deletes or staffs a team, or counts what its deletion reassigns, names a project in its address; what stays at a project's address is the project's linked teams, the project's end of a link and an unlink with the unlink's count, and the team views and their reorder until `feat-040-team-surfaces-across-projects` (BR-OL-11)
  And `GET …/access` lists the four new codes for «Администратор» and for no one else, and the panel shows each command only to a user who holds its code or codes
```

## Owed by later lines

```gherkin
# `mod-019-scoped-access`: the same steps for a holder of «Управление» on one end or on the team; the codes checked per object
# `feat-041-project-visibility`: a project the user may not see answers «Проект не найден.» in every row above, before the 403
```
