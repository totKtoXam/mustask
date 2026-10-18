# Review — mod-018-area-workspace

## Requirements review, round 1

Requirements reviewer, clean context, 2026-10-18. The light route's one round (status.md § Plan). Read: `spec.md` and
US-1..US-6 (`git diff origin/main...mod/018-area-workspace`); doc-012 `spec.md` (Roles, Glossary, BR-MX-1..-8, -20..-24,
-27, the fate table, Existing data, NFR, Decomposition), its US-1, the area part of its US-3 and the area clauses of its US-2;
doc-013 `spec.md` (BR-NE-1..-9, the lines table, mod-018's row); ADR-0028 (rules 1-8, 12, 13, the delivery table);
ADR-0029 (§§ 2, 4, 6-8, 12, the delivery lines); ADR-0024 (§§ 1, 2, 4, 5); `docs/adr/README.md` (Process); feat-028
`spec.md`, `status.md` § Decisions, `api-contract.md` §§ 0.1, 0.4, 0.5, 2.4, 2.6 and the owed blocks of its stories;
mod-017 `spec.md` (BR-OT-10, -11, the fate table) and `design.md` § 3; feat-029 `design.md` (the band); mod-016
`api-contract.md` § 0.2 and the inventory rows 15-26; mod-002 `api-contract.md` § 4 and its route table;
`docs/error-codes.md`. Code read: `app/navigation/model.ts`, `CreateWorkItemDialog.tsx`, `teamTexts.ts` (the quoted
texts exist as the stories quote them), `WorkItemEndpoints.cs`, `SaveWorkItemHandler.cs`, `WorkItemConfiguration.cs`,
`ProjectBacklogMembership.cs`. Every test class and file named in spec § Existing rules was searched for: all exist.

### Rule → story map

| Rule | Stories |
|---|---|
| BR-AW-1 tenant | US-6 (all), US-1 (a foreign parent), US-3 (the multiselects) |
| BR-AW-2 independent existence | US-1, US-2, US-3, US-4 |
| BR-AW-3 the tree | US-1, US-2 |
| BR-AW-4 names, descriptions | US-1, US-2, US-6 |
| BR-AW-5 «Наследовать доступ» stored | US-1 |
| BR-AW-6 links | US-3 |
| BR-AW-7 who links | US-3, US-6 |
| BR-AW-8 links change nothing | US-3 |
| BR-AW-9 deletion | US-2, US-4 |
| BR-AW-10 deactivated projects, deleted teams | US-2, US-3 |
| BR-AW-11 the item's «Область» retires | US-4, US-5 |
| BR-AW-12 pages | US-1, US-3, US-4, US-6 |
| BR-AW-13 paths | US-1, US-2, US-3 |
| BR-AW-14 upgrade | US-5 |
| BR-AW-15 answers | US-1, US-6 |
| § ADR-0028 amendment | none: a document edit written at the backend stage and confirmed at gate-3 (status.md § Decisions), which feat-028's gate-3 answer allows |

doc-012: BR-MX-1 → BR-AW-1, BR-MX-2 → -2, BR-MX-3 → -3 and -5, BR-MX-4 → -4, BR-MX-5 → -6, BR-MX-6 → -7, BR-MX-7 → -8,
BR-MX-8 → -9, BR-MX-27 → -12, -13. Every scenario of doc-012 US-1 maps to US-1 or US-2, or is owed by mod-019 (the
parent picker by grant, participants, «Наследовать доступ» shown and switched, «Управление» on an area) or feat-041 (a
project the actor may not see in the confirmation; BR-MX-24's order, which reverses US-6's "403, not 404"), as the
stories' blocks say. Every area scenario of doc-012 US-3 maps to US-3 or US-6, its «Управление» and visibility clauses
owed the same way. doc-012 US-2's area clauses (creation with an area, a team's deletion, a deactivated project's links)
map to US-1 and US-3. The clauses feat-028 left owed here (its US-1, US-2, US-3, US-5, US-6, US-7) and mod-017's (US-6,
US-7) map to US-3, US-4 and US-5.

### R-1
Severity: major
Affects: BR-AW-12, US-4, US-6, NFR Tenant isolation
Problem: "the API keeps no project-nested area route" and "`…/projects/{projectId}/areas` is gone" also forbid the
project's own end of an area link. That end (its linked areas, a link and an unlink from its «Общие») is checked with
`organizations.project.link`, scope P. Under ADR-0029 § 12 it must therefore carry the project's id in its path. feat-028
built its project–team end the same way, keeping `POST …/projects/{projectId}/teams` with a changed meaning. Left as
written, the api-contract would either contradict the spec or put the project's end at an address that mod-019's
inventory test refuses.
Required resolution: retire mod-002's project-nested tree routes by name. Say that the project's and the team's ends of
the area links are routes of the project and of the team.
Status: resolved
Resolution: fixed in place by the reviewer.
- BR-AW-12 now retires mod-002 § 4's tree routes: the tree's read and an area's creation, rename, move and deletion
  under `…/projects/{projectId}/areas`. An area is addressed under the organization alone.
- BR-AW-12 also places the two link ends under the project and the team. Whether the project's end reuses that path is
  left to the api-contract.
- US-4's API clause, US-6's route clause and NFR Tenant isolation follow.

### R-2
Severity: minor
Affects: US-6 (the inventory scenario), spec § Roles, BR-AW-7
Problem: US-6 declared "`area.link` with `project.link` or `team.link` for a link and an unlink". For an unlink that reads
as both codes, against BR-AW-7, BR-MX-6 and ADR-0029 § 4: either end's code is enough. Nothing can observe the gap today,
because only «Администратор» holds the codes. mod-019, though, re-runs this clause against the inventory.
Required resolution: say which code each route declares.
Status: resolved
Resolution: fixed in place by the reviewer.
- For a link, the route declares its own end's code and checks the other end's second.
- For an unlink, the route declares only the code of the end it is sent to.
- § Roles says the same.

### R-3
Severity: minor
Affects: US-6 (the structure contract scenario), § Decomposition Breaks
Problem: ADR-0028's delivery table gives this line "the area-to-projects resolution" in `IOrganizationStructure`. Rule 8
defines it: the distinct reachable projects linked to an area and, on request, to its descendants. The spec names it only
in the Breaks cell, and US-6 asks only for "its linked projects". Its first consumer is feat-042, which would build on
whatever this line chose to build. Distinctness, the deactivated project and the descendants option had no criterion.
Required resolution: a testable clause for the resolution.
Status: resolved
Resolution: fixed in place by the reviewer. US-6's first scenario now has «Логистика» linked to Alpha, and «Логистика /
Складские системы» linked to Alpha and to the deactivated Gamma. The resolution answers:
- for «Логистика» alone, Alpha;
- for «Логистика» with its descendants, Alpha once;
- in one statement, with Gamma left out.

### R-4
Severity: minor
Affects: US-2 (the depth-after-a-move scenario)
Problem: the scenario's last clause moved «Логистика» under «Ф8» while Ivan creates a child of «Логистика / Складские
системы», "each allowed alone". The clause before it had already applied that same move. Read in order, the last clause
was a move to the current parent, and Ivan's child, on level 11, was refused alone. The race it means to test did not
exist.
Required resolution: start that clause from the Given.
Status: resolved
Resolution: fixed in place by the reviewer ("And, from the Given again, when …").

### R-5
Severity: minor
Affects: US-1 (the create form), US-2 (the move dialog, the move), BR-AW-3
Problem: two gaps.
- The server answer to a move of an area to the parent it already has (the top level for a top-level area) is not stated.
- The pickers' offer is unclear. US-1's «Родительская область» offers "every area of the tree". US-2's dialog offers "the
  top level and the other areas", leaving out only the area and its subtree. So for «Логистика» the dialog offers the top
  level, where it already is, and both pickers offer destinations the depth rule or a sibling name will refuse («Ф10», or
  «Ф9» for a subtree of height 2). US-2's title, "offers what a move may reach", claims more than the criteria check.
Required resolution: state the server answer. The design settles the offer: leave out the current place and the
destinations that would be refused, or offer them and refuse on save with the quoted texts. The scenario then follows.
Status: open: the server part is resolved, and the offer goes to the design
Resolution: the server part is fixed in place by the reviewer. US-2's subtree scenario gains "a move of an area to the
parent it already has, the top level included, succeeds and changes nothing", as a link of a linked pair does
(BR-AW-6). The pickers' offer is stage 3's to fix, and the design records it as a decision.

### R-6
Severity: minor
Affects: BR-AW-14, NFR Existing data, US-4 (the list's statement count)
Problem: `ix_work_items_project_id_type_id_area_id` serves feat-039's project views on its (project, type) prefix
(`ProjectBacklogMembership`). Dropping the item's area drops it. The spec says "nothing else is dropped or changed" and
pins only the list's statement count. A migration that drops the column and adds no replacement index would pass every
stated criterion and leave the project backlog's 5 000-item budget unindexed.
Required resolution: say that the upgrade keeps project views indexed.
Status: resolved
Resolution: fixed in place by the reviewer. NFR Existing data says:
- the upgrade leaves an index led by (project, type) in place of the dropped ones (ADR-0028 rule 13);
- the project backlog's 5 000-item budget holds after the upgrade.

### R-7
Severity: minor
Affects: NFR Volumes, US-1 (the reading scenario)
Problem: "the whole tree is shown within 2 s" over 1 000 areas on 10 levels gives no measure. It does not say whether the
time covers the read or the drawing, nor whether "shown" means every node expanded. US-1's first scenario implies that
the children show beneath «Логистика», but the initial expansion is not stated.
Required resolution: name the measure, and leave the initial drawing to the design.
Status: resolved
Resolution: fixed in place by the reviewer. NFR Volumes measures "shown within 2 s" as the read behind the page returning
within 2 s in a volume test at the organization's address, as mod-002's tree test did at 500 nodes. Which rows the panel
draws first is the design's (see R-8).

### R-8
Severity: minor
Affects: US-1, US-2, US-3, NFR Volumes; the gate-1 + gate-2 package
Problem: the volumes leave the design these decisions, and the spec takes none of them:
- **The multiselects and pickers.** At 1 000 areas, they could filter the tree's read on the client or search the
  server, and they need a bound on what they draw. At feat-028's gate-3 the human chose server search for the
  team form's «Участники» over the recommended local filter (feat-028 status.md § Decisions), so this is a question to put
  to the human, not one to settle quietly.
- **The deletion confirmation.** It names up to 200 projects and 200 teams (BR-AW-9).
- **The tree.** Its initial expansion at 1 000 areas.
- **"A way to open its page".** For a project listed on an area's page, it is not said which page opens before feat-043:
  the project's «Общие», its «Рабочие элементы», or another.
Required resolution: the design fixes each and puts each one with alternatives to the human as one question in the
package (status.md § Plan: one question per design decision). The criteria hold either way.
Status: open: goes to stage 3 and the package

### R-9
Severity: minor
Affects: US-4, § Decomposition Breaks
Problem: the card's band comes from feat-029's design. It holds «Область» in block B, row 1, with mod-017's «Команда» in
row 2, under it (mod-017 design § 3). The list's eight columns include «Область» too. US-4 removes both fields, but the
Breaks cell named neither design. The light route designs "where there are new screens", which could read as leaving
these changed screens to the frontend to improvise.
Required resolution: name both designs in Breaks, and make the band's new layout and the list's columns part of stage 3.
Status: resolved in the spec; the layout goes to stage 3
Resolution: fixed in place by the reviewer. The Breaks cell names feat-029's band and mod-017 design § 3, "whose band and
the list's columns the design re-lays without «Область»".

### R-10
Severity: minor
Affects: OQ-2
Problem: OQ-2 B's case cited BR-NE-3 as if it covered this field. BR-NE-3 was written for the sections of the object
pages. B also moves scope that doc-012 § Decomposition gives this line ("the inheritance switch"), and the human should
see that move when answering. The option's facts were right. It only overstated what the human's doc-013 answer covers.
Required resolution: present B as carrying BR-NE-3's reasoning over to a control. Name the scope it moves to mod-019.
Status: resolved
Resolution: fixed in place by the reviewer. The OQ-2 B paragraph now says that B carries over BR-NE-3's reasoning, which
was written for sections. Under B, the switch's control, its rules and doc-012 US-1's switch clauses go to mod-019, whose
queue line gains them. ADR-0029's delivery bullet for this line is still met by the stored switch. The repeated
sentence about doc-012 US-1's switch clauses was removed.

### Checked and found sound

- **Order of answers.**
  - BR-AW-15 keeps today's order, 403 before a nested 404, until feat-041, as ADR-0029 § 8 allows.
  - US-6's "Vera … is answered 403, not 404" is that order. doc-012 US-1's reverse clause is feat-041's, and spec § Owed
    by names it.
  - The place of a body id's not-found between 400 and 409 follows feat-028 api-contract § 0.5, so no finding is raised.
- **Codes.**
  - The redefined `area.read` and `area.write`, the new `area.create` and `area.link`, and the move's three checks match
    ADR-0029 §§ 4, 7.
  - A child's creation is addressed by its parent (§ 7).
  - `area.write` is «Администратор»'s alone today (mod-016 § 0.2), so no role loses a right.
- **Deletion and deactivation.**
  - BR-AW-9 and BR-AW-10 follow BR-MX-8 and ADR-0026 rules 2-3 as ADR-0028 § 6 amends them.
  - A deactivated project leaves an area's lists, yet the deletion's confirmation names it «деактивирован», and its
    restore finds the link ended (US-2, US-3).
- **Concurrency.**
  - Every NFR race has a scenario: one name at one level; a cycle; depth by a creation and a move; deletion against a
    child or a move under it; a link against either end's deletion; two links of one pair.
  - Each has the lock or key ADR-0028 rules 5 and 8 name.
- **The upgrade (US-5).**
  - The counts add up: 8 areas (Alpha 4, Beta 1, Gamma 2, Zeta 1) and 24 items (18 + 3 + 1 + 2).
  - «Изменён» does not move, as feat-028's upgrade did not.
  - Only areas and the item's pointer are dropped, as ADR-0028's "may drop" allows.
- **Interim pages.**
  - `…/settings/areas` and `…/settings/areas/:a`, the project's «Общие» and the team's page match doc-013's row for this
    line and ADR-0028 § 7.
  - `…/projects/:p/settings/areas` answers «Страница не найдена» with no redirect.
- **Interface copy.**
  - The three texts US-4 quotes as today's are in `CreateWorkItemDialog.tsx` and `teamTexts.ts` as quoted.
  - The settings navigation holds «Области» in the project's group (`app/navigation/model.ts`), which US-1 and US-4 move.
- **Fate table.** Every test class and file it names exists. The project's root, the per-project tree, the delete guard
  and the item's pointer each carry their tests.
- **Size.** L in one MR. doc-012 § Decomposition says splitting a breaking line would not make it additive, and every
  story is `shippable: no`.
- **Naming.** The reference tracker is not named in any file of this feature.

### Open questions

- OQ-1 (the per-project areas in the upgrade; A, drop all, recommended) is honest. Its options, costs and the human's
  feat-028 precedent (OQ-1 C, drop) are stated.
- OQ-2 («Наследовать доступ» before grants; B, store only, recommended) is honest, with R-10's rewording.
- Both are the human's. The orchestrator puts them in the gate-1 + gate-2 package after the design, one question each.
- The reviewer added no question. R-5's offer and R-8's four decisions are design decisions that stage 3 puts to the
  human the same way.

### Verdict

`needs-human-decision`.
- R-1..R-4, R-6, R-7 and R-10 are fixed in place.
- R-5's offer, R-8 and R-9's layout go to stage 3.
- OQ-1 and OQ-2 are the human's.
- No finding goes back to the analyst. The design continues on OQ-1 A and OQ-2 B.
