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

Fix round (business-analyst): verified, with one residual closed. US-4 answered every one of mod-002's tree routes "as a
route that does not exist", and US-6 said they "are gone" under `…/projects/{projectId}/areas`. BR-AW-12, though, leaves
to the api-contract whether the project's end keeps that path with the link meaning, as feat-028 kept `GET` and `POST
…/projects/{projectId}/teams` with a changed meaning (its api-contract § 4). A kept `GET` or `POST` would have failed
both clauses. They now retire the routes by meaning:
- no request addressed under a project reads the tree or creates, renames, moves or deletes an area;
- a route of mod-002 § 4 whose path the project's end does not keep is a route that does not exist;
- a path it keeps serves only that project's area links (US-4, US-6, NFR Tenant isolation).

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

Fix round (business-analyst): verified, no further change. The link half matches feat-028 api-contract § 0.1 (the route
declares its own end's code and the handler asks the other's first). The unlink half differs from feat-028's project–team
unlink on purpose: BR-MX-6 makes that one need both ends, since it reassigns items, and an area unlink either end.

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

Fix round (business-analyst): verified against ADR-0028 rule 8 ("the distinct reachable projects"), no further change.
"In one statement" stays: it is stricter than NFR Query shape, it is what the statement-count test pins, and nothing in
the ADRs needs more than one.

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

Fix round (business-analyst): verified, no further change. From the Given, the move alone puts «Складские системы» on
level 10 and Ivan's child alone lands on level 3, so each is allowed alone and together they reach level 11.

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
Status: resolved in the spec; the offer is a stage-3 decision for the package
Resolution: the server part is fixed in place by the reviewer. US-2's subtree scenario gains "a move of an area to the
parent it already has, the top level included, succeeds and changes nothing", as a link of a linked pair does
(BR-AW-6). The pickers' offer is stage 3's to fix, and the design records it as a decision.

Fix round (business-analyst): the criteria now hold under either offer, so the design can choose without editing a story.
- BR-AW-3 states the move to the current parent too. The rule → story map sends BR-AW-3 to US-2, and the rule did not say it.
- US-2's dialog scenario is retitled "never offers the area or its subtree". It states only destinations that every option
  offers: «Финансы» and «Инфраструктура» for «Логистика», and the top level as well for «Логистика / Складские системы».
  Whether the dialog also offers the current place, or destinations the depth or name rule refuses, is the design's. A
  destination it offers that the server refuses is refused on saving with that rule's text.
- US-1's «Родительская область» offers the top level and the areas by path, every area of the example tree among them.
  The example tree is two levels deep, so every option offers all of it.
- US-1's and US-2's preambles say that a creation or a move to a place the form does not offer is made by a direct
  request. The depth and name scenarios («Ф10», «Ф9», «складские СИСТЕМЫ») therefore run under either option.

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

Fix round (business-analyst): verified, no further change. The index is `WorkItemsConstraints.ProjectTypeArea` in
`WorkItemConfiguration.cs`, ADR-0028 rule 13 sizes the indexes, and doc-012 NFR Volumes holds the 5 000-item budget.

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

Fix round (business-analyst): US-1's first scenario showed the children "beneath «Логистика»", which presumed an expanded
tree. It now shows them there "at once or on expanding it". US-1's preamble leaves the initial expansion to the design.

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

Fix round (business-analyst): no spec change; the four decisions stay stage 3's. The analyst checked that the criteria
hold under each alternative, as the finding says, and closed the one place where they did not:
- *Multiselects and pickers.* "Narrowed by typed text" and NFR Query shape hold for a client filter and for a server
  search alike.
- *The deletion confirmation.* BR-AW-9 names every link, as BR-MX-8 requires. The design lays the 200 + 200 out. It may
  not leave a name out.
- *The tree's initial expansion.* US-1's first scenario presumed an expanded tree; fixed under R-7.
- *"A way to open its page".* US-3 holds for whichever page the design opens.

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

Fix round (business-analyst): US-4's card scenario now shows the other fields "in the band the design re-lays without
«Область» (feat-029's band, mod-017 design § 3)". The story names the redesign, so the frontend does not improvise it.
The layout itself stays stage 3's.

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

Fix round (business-analyst): verified, no further change. OQ-2 B and the Decomposition's last sentence agree on what
mod-019's queue line gains.

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

### Fix round (business-analyst, 2026-10-18)

The light route's one fix round. Each finding's Resolution above ends with what this round did.
- R-1's residual is closed. US-4, US-6 and NFR Tenant isolation retire the tree routes by meaning, so a path the
  project's end keeps no longer contradicts BR-AW-12.
- R-5's server half is now also in BR-AW-3. The criteria of US-1 and US-2 hold under either picker offer.
- R-7: the tree's initial expansion is the design's.
- R-9: US-4 names the band's redesign.
- R-2, R-3, R-4, R-6 and R-10 are verified with no change.
- R-8 stays open for stage 3. Its four decisions go to the package, one question each, beside R-5's offer and R-9's layout.

No finding was declined. OQ-1 and OQ-2 are unchanged and stay the human's. The verdict stays `needs-human-decision`, and
the design continues on OQ-1 A and OQ-2 B.

## Round 1 — code and security

Code reviewer, clean context, 2026-10-27, HEAD 73aca1da, diff `891d17a6..HEAD`. One session for the general review and the L
security checklist (economy mode). Code read in full: every new or rewritten Organizations handler, the area repositories and
reads, the configurations, the migration, the endpoints and role bundles, and the item-area retirement. On the frontend: the pages,
`EndAreasSection`, permissions and routes. Tests were skimmed (`AreaLink…`, `AreaRaces…`, `AreaQa…`): no assertion is weakened.
The suite was not re-run.

### Findings

**M-1 · major · an unlink from the area's end ends a deactivated project's link.**
`src/backend/Mustask.Application/Features/Organizations/UnlinkProjectFromArea/UnlinkProjectFromAreaHandler.cs:202-206` accepts any
stored project (`structure.FindProjectAsync(...) is null`), and its summary (`:172-173`) says so on purpose. US-3 ("Only active ends
are linked…": unlinking Gamma from «Логистика» is answered «Проект не найден.» and nothing changes), BR-AW-10 ("a link or an unlink
naming it answers «Проект не найден.», and its restore brings its links back exactly") and api-contract § 3 say the opposite. Today
`DELETE …/areas/{a}/projects/{deactivated}` returns 204 and deletes the stored link, so the restore loses it. The project end
(`UnlinkAreaFromProjectHandler.cs:33-37`, `IsReachableAsync`) is right, so the two ends disagree. No test covers this.
Fix: use `projects.IsReachableAsync` (or `is not { IsReachable: true }`), correct the summary, and add an API test that the
unlink answers 404 `project.not_found` and the link survives a restore. (`UnlinkTeamFromAreaHandler.cs:250-251` likewise accepts a
deleted team. That is harmless, because a deletion leaves no link, but it should use `IsReachable: true` to match the contract's
"a deleted team … is that 404".)

**M-2 · major (prose) · the ADR-0028 amendment does not place the project row in the one lock order.**
`docs/adr/0028-matrix-organization-model.md:157` (c) says the order "takes the project row and the project hierarchy tier in it"
without saying where, and the numbered order on `:144` still has no project row. The order that was decided (mod-005 plan-backend
§ 14.3: teams → links → **project row** → number/hierarchy rows → items → order rows) is written only in a feature plan, which is
what the human sent this amendment to fix (status.md § Decisions, mod-005 gate-3). A later line that reads the ADR alone can put the
project row before the links and deadlock against the unlink. Also, the *Unlink* bullet (`:146`) still reads `FOR UPDATE` twice,
"overridden" only by (a).
Fix: rewrite the numbered list in place: (1) tree lock, then areas by id; (2) teams; (3) project–team links; (3a) the project row;
(3b) the project's number and hierarchy rows; (4) sprints; (5) items; (6) order rows. Say `FOR NO KEY UPDATE` in the Unlink bullet.

**M-3 · major (prose) · no P-10 break note in the owners' status.md.**
spec § Decomposition (`spec.md:161`) requires "a note in each owner's status.md" for mod-002 (§ 4 routes, § 6 codes, § 7
`RootAreaId`), mod-003 (§ 1.1 the item's area), mod-016 (inventory rows 21-24, 26), mod-017 (BR-OT-10), feat-022, feat-032,
feat-029 and mod-017 design § 3 (the band), and feat-028 (BR-OL-2). feat-028 wrote its notes at the dev stage (mod-002
`status.md:56`). This diff adds none. Live prose still describes the old contract, for example mod-002 `api-contract.md:419-504`
(the project-nested routes and `root_is_fixed`) and `:580-601` (`RootAreaId`), and mod-003 `api-contract.md:290` (the «Область»
picker). An agent working from those contracts calls routes that no longer exist.
Fix: one dated `[dev] P-10 note from mod-018` Log line in each owner's status.md, worded like feat-028's.

**m-1 · minor · contract statement count (QA D-1).** `api-contract.md:45` says the tree read takes "2 statements", but it takes 1
(`AreaTreeReadModel.cs:13-20`, pinned by `AreaReadsTests`). Fix: write "1 statement".

**m-2 · minor · the current-parent code check reads the area unlocked.** `MoveAreaHandler.cs:238` decides `area.create` from
`addressed.IsTopLevel`, which is read before the tree lock. A move that commits in between changes the current parent. This is
invisible until mod-019 (one role holds every code), but it goes against amendment (b) ("a fresh read decides only the other fields
of a row already locked"). The contract's "second codes first of all, before any id is looked at" (`api-contract.md:15`) cannot hold
for this one code either. Fix: re-check on `area.ParentId is null` after `LockAreaAndDestinationAsync`, and narrow the contract
sentence ("…except the current parent's, after the area is read").

**m-3 · minor · DEF-4 (QA).** `AreasPage.tsx:238` (and `AreaPage.tsx:407-416`) wraps the create dialog in `areaWrite`, while
the top-level «Новая область» is offered by `areaCreate` (`AreasPage.tsx:188-199`). This is equal today and diverges under mod-019.
Fix: gate the create dialog on `areaCreate || areaWrite`, as the server does. Carry to mod-019 if it is not folded in.

**m-4 · minor · stale comments (QA DEF-3 and others).** `CardTeamField.tsx:7-8`, `StateField.tsx:12` and `ProjectField.tsx:12`
still describe «Область» in the band. `CreateWorkItemHandler.cs:17` has "description,," and `:70` a double space. Fix: reword.

### Security checklist

- **Authorization:** pass. All 20 area routes declare a code (`OrganizationEndpoints.cs:328-430`), and the handlers check the
  second codes before any read. Member and Viewer hold read codes only (`OrganizationRoleBundles.cs:23-34`).
- **Tenant / IDOR:** pass, except M-1, which is a rule defect inside one organization, not a cross-organization one.
  - Every statement carries `organization_id`.
  - Parent and link keys are composite `(organization_id, id)` (migration `:140-179, :247-253`).
  - A foreign, malformed or missing id gets one 404 per level.
  - An unlinked pair answers 204 and a foreign end 404. That is BR-AW-1, not a leak.
- **Info leaks:** pass. 403 comes before the nested 404, and `inheritsAccess` is in no response.
- **Injection:** pass. Every raw SQL statement is interpolated and parameterized.
- **Secrets, personal data, audit:** pass. None added; `linked_by`/`created_by` are recorded.
- **Migration:** pass. The drop is OQ-1 A, logged per kind; nothing else changes; the (project, type) index replaces the dropped
  one. Down is lossy by design, as in feat-028.
- **Concurrency:** pass, with m-2.
  - Order: tree lock → areas by id → teams `FOR SHARE`. CreateProject takes the areas before the teams.
  - Presence is taken from the lock statement (`AreaRepository.cs:14-81`).
  - Rename and describe take the row only, with the NULLS NOT DISTINCT index as backstop.
  - No pair waits in a cycle: a team deletion never waits on an area, a link never takes the tree lock, and the area
    deletion's cascade waits only on link rows.
- **Reference product:** pass. No hit in the added lines.

### Behavioural claims

- api-contract § 2 "tree read, 2 statements" → m-1.
- api-contract § 0 "second codes … before any id is looked at" → holds for every code but Move's current parent → m-2.
- api-contract § 3 "a deactivated project … of the address is that 404 too, so … an unlink naming it changes nothing" → project end
  `UnlinkAreaFromProjectHandler.cs:33-37`; the area end contradicts it → M-1.
- `UnlinkProjectFromAreaHandler.cs:172-173` "a deactivated project … its stored link can be ended" → contradicts BR-AW-10 → M-1.
- api-contract § 7 "ListLinkedProjectsOfAreaAsync: each project once, deactivated left out, one statement" →
  `OrganizationStructure.cs:170-200`.
- `IAreaLinkRepository.cs:8-11` "a bind takes the area FOR SHARE and the team FOR SHARE first" → `LinkAreasToTeamHandler.cs:140-150`,
  `LinkTeamsToAreaHandler.cs:178-199`, `CreateAreaHandler.cs:122-137`.
- ADR-0028 amendment (b) "presence judged by the lock statement's own answer" → `AreaRepository.cs:33-81` (except m-2).
- ADR-0028 amendment (c) "the one lock order takes the project row" → no position stated → M-2.
- ADR-0028 rule 8 "a link vs an area deletion: cascades / fails as not found" → `LinkProjectsToAreaHandler.cs:49-56`, migration `:145, :173`.
- Rename summary "a concurrent creation of the same name … caught by the sibling index" → `AreaConfiguration.cs` (NULLS NOT DISTINCT);
  `AreaRacesEndpointTests:73`.
- Migration "logs per kind" / "changed_at not written" → migration `:22-37`; no `UPDATE work_items` in Up.
- NFR "an index led by (project, type) in place of the dropped ones" → migration `:200-203`.
- `DeleteAreaHandler` "links end with it (the database cascades)" → migration `:145, :173`.
- api-contract § 0 "`inheritsAccess` in no response and no request" → the `AreaResponse` records and the request records carry none.
- spec § Decomposition "Breaks (P-10), a note in each owner's status.md" → none in the diff → M-3.
- **Direction 2.**
  - Grepped `RootAreaId|IAreaUsage|root_is_fixed|settings/areas|projects/{projectId}/areas|LockRootAsync` over `docs/`, `src/`,
    `.claude/`, `scripts/` and AGENTS.md.
  - Hits:
    - ADR-0028: it already states the retirement.
    - `error-codes.md:55`: deprecated, which is correct.
    - mod-002 contract, design, plans and qa; mod-003 contract `:290`: merged history, which needs the P-10 note → M-3.
  - AGENTS.md and the conventions: no hit.
  - Not swept: `src/frontend/e2e/` (E2E paused).

### Verdict

`request-changes`. 3 majors (M-1 code, with a test and a QA re-check over that handler; M-2 and M-3 prose only, delta read, no
QA), 4 minors left as debt (m-1..m-4). For the human at gate-3: confirm the ADR-0028 amendment as not substantive (spec § ADR-0028
amendment), on its M-2 text.

## Round 2 — delta

Code reviewer, clean context, 2026-10-27, diff `c18792ae..HEAD` (round-1 fixes M-1, M-2, M-3, m-1, m-4). Verdict: **approve**.

Tests run: `Mustask.Api.IntegrationTests` filtered to `AreaLinkEndpointTests` (13 passed, 0 failed) and
`Mustask.Application.Tests` filtered to `AreaLink` (30 passed, 0 failed). The full suite was not re-run.

- **M-1 · closed.** `UnlinkProjectFromAreaHandler` takes `IProjectRepository` and refuses on `!IsReachableAsync` with
  `project.not_found`; its summary is corrected. `UnlinkTeamFromAreaHandler` requires `is { IsReachable: true }`. The new API tests
  assert 404 `organizations.project.not_found` with the link surviving a deactivate/restore (the restore is done before the read),
  and 404 `organizations.team.not_found` for a deleted team. The inverted handler test now expects `NotFoundException` and
  `Assert.Single(AreaProjects)`; it is the fix of the contract's rule, and no other assertion in the diff is weakened.
- **M-2 · closed.** The ADR-0028 rule 8 order is renumbered in place: (1) tree lock then areas by id, (2) teams, (3) links,
  (4) project row, (5) number and hierarchy rows, (6) sprints, (7) items, (8) order rows. The Unlink bullet says
  `FOR NO KEY UPDATE`; amendment (c) cites steps 4 and 5. `UnlinkTeamHandler` (links `:133`, project row `:178`, hierarchy `:179`,
  owned items `:180`, places, reassign, link removal) and `DeleteTeamHandler` (team links, `ClearDefaultTeamAsync`, hierarchies,
  owned items, places, reassign, links) follow it. The sprint step (6) sits between hierarchy and items. No sprint code exists
  yet (mod-007 is unbuilt; `UnlinkTeamHandler:56` says the pointers are owed), so the Unlink bullet ("open sprints, then items")
  and the Close bullet (team, sprint and destination, then items) are consistent with the order; neither takes a project row, which
  is allowed by "takes only what it needs".
- **M-3 · closed.** All eight Log lines were checked against the owners' specs and BR-AW-11 / BR-AW-14: the quoted rule ids
  (BR-OL-2, BR-M1-11, BR-M3-23 with BR-M3-2 and -20, BR-OT-10, BR-C-9) exist and say what each note retires. The mod-002 § 4/§ 6/§ 7
  references match the retired routes, the `root_is_fixed` code and `RootAreaId`. Accurate.
- **m-1, m-4 · closed.** "1 statement"; the three stale «Область» comments and the two typos are reworded. Code unchanged
  in behaviour.

### New findings

**m-5 · minor (prose, for mod-007) · the *Item save* bullet and the new order.** ADR-0028 rule 8 *Item save* says "the link and
the sprint its body names are locked before the item row" without the hierarchy tier. A save that takes the project hierarchy
(step 5) after a sprint (step 6) would invert the numbered order. Nothing runs today (no sprint code), so nothing is broken.
mod-007 must state where the hierarchy lock stands in that save before it takes a sprint. Open, carried to mod-007.

m-2 and m-3 of round 1 stay open by decision (mod-019). Blocker and major open: 0. Minor open: 3 (m-2, m-3, m-5).

## Requirements review — typed tree (2026-10-28)

One round (light route), clean context, over the reworked `spec.md` and US-1..US-6 against the human's typed-tree answer
(status.md § Decisions, 2026-10-27), doc-012 BR-MX-2..-8, mod-002 BR-M1-1, -11..-14, -20, -22, feat-028, ADR-0028, ADR-0029 and
`docs/adr/README.md`. Finding ids are TR-n so as not to collide with R-n of round 1 or M-/m- of the code review.

### Rule → story map

| Rule | Stories | Note |
|---|---|---|
| BR-AW-0 typed tree | US-1, US-3, US-5 | US-1 scenarios still untyped (TR-1) |
| BR-AW-1 tenant | US-1, US-6 | US-6 table still names area–project rows (TR-1) |
| BR-AW-2 existence | US-1, US-2, US-4 | US-1 "creating an area creates nothing else" untyped (TR-1) |
| BR-AW-3 tree, types, moves | US-1, US-2, US-3 | US-2 move/cycle/depth scenarios contradict OQ-3 A (TR-1); cross-project sub-area move (TR-7, fixed) |
| BR-AW-4 names | US-1, US-2 | department vs project name space missing (TR-4) |
| BR-AW-5 inherit switch | US-1 | sound |
| BR-AW-6 links | US-3 | project node link vs feat-028 project–team link (TR-3) |
| BR-AW-7 who links | US-3, US-6 | US-6 inventory still names `project.link` (TR-1) |
| BR-AW-8 nothing disclosed | US-3, US-4 | sound |
| BR-AW-9 deletion | US-2, US-4 | US-2 deletion scenario still ends area–project links (TR-1) |
| BR-AW-10 deactivated / deleted | US-3, US-4, US-5 | writes on a deactivated node undefined (TR-5) |
| BR-AW-11 item area | US-4 | sound |
| BR-AW-12 pages | US-1, US-4, US-6 | US-1 "no project's settings hold «Области»" contradicts it (TR-1) |
| BR-AW-13 paths | US-1, US-3, US-4 | card path exception (TR-8, fixed) |
| BR-AW-14 upgrade | US-5 | depth boundary (TR-6) |
| BR-AW-15 answers | US-6 | sound once TR-1 is done |

### TR-1
Severity: blocker
Affects: US-1, US-2, US-6 (all scenarios); BR-AW-0, -3, -6, -9, -12
Problem: the "re-basing note" is not enough. It remaps the cast and says "the criteria otherwise stand", but the Gherkin is
what the developer and QA test, and many scenarios contradict the reworked rules as written, not merely by cast:
- US-1: «Проекты» on the create form (no area–project link exists); «Родительская область» offering "every area of the example
  tree" (the type rules forbid most); «Транспортные системы» created under «Логистика» (a sub-area under a department, refused
  under OQ-7 A); the depth chain hung from the department «Финансы» (sub-areas cannot sit under a department); "the five areas"
  in the read scenario; "a project ... created afterwards is linked to no area" and the empty organization's "no area asked for
  anywhere" (a project now takes a parent and has a node); "no project's settings hold «Области» any more (US-4)", which is the
  direct opposite of BR-AW-12 and US-4's last scenario.
- US-2: «Логистика» "linked to Alpha"; the subtree move "«Логистика» under «Финансы»" with a Then that it succeeds, which
  note (2) says is refused; the cycle scenario between two departments (impossible under OQ-3 A, so it tests nothing);
  «Логистика» moved under «Ф9»/«Ф8» (a department under a sub-area: the type rule answers before the depth rule, so the depth
  criterion is never reached); the deletion scenario's "projects Alpha and Gamma" in the confirmation and "when Gamma is
  restored it is linked to no area"; the move dialog's offer (departments offered for a department).
- US-6: the Outline rows "linking «Финансы» with Zeta", "linking Alpha with «Другая»'s «Логистика»", "unlinking Alpha from …",
  "creating an area with the project Zeta"; the role table's "link or unlink of an area and a project"; the inventory's
  `project.link` and "mod-002 § 4's tree routes are gone" (the note says they need not be); the structure contract's
  "linked projects" and "a deactivated project left out" (the note says "marked").
A note that overrides scenario text by prose makes every such criterion ambiguous: a test can be written to either.
Required resolution: the business-analyst rewrites the scenarios of US-1, US-2 and US-6 over the typed tree, as US-3..US-5 were,
and deletes the re-basing notes. Every cycle and depth scenario uses moves the type rules allow (sub-areas inside one project;
a project node under a department for depth); each refusal by type names `type_not_allowed`. The move dialog's offer is
stated per type. Not done here: it is a rewrite of three stories, not a wording fix.
Status: resolved (fixed by BA 2026-10-28; not re-read, light route one round)
Resolution: US-1, US-2, US-6 rewritten scenario by scenario over the typed tree, re-basing notes deleted; US-3 rewritten after the gate-1 answers.

### TR-2
Severity: major
Affects: § ADR-0028 amendment; BR-AW-11 ("ADR-0028 rules 2, 3 as reworked"); ADR-0028, ADR-0029
Problem: the typed tree reverses accepted ADR decisions: ADR-0028 rule 2 retires the per-project root area by name, its
delivery table lets this line drop "every per-project area and the item's area pointer", rule 8 carries the area–project
links and the area-to-projects resolution, rule 7 the retired project areas address; ADR-0029 § 6 and its delivery bullet
name "area–project links" as this line's input. The spec only knew the editorial amendment and cited the reversed rules
"as reworked" with no owner. `docs/adr/README.md` Process 3: an accepted ADR is not edited substantively; a new ADR is
written and the old one marked superseded. Gate-3's "editorial" confirmation cannot cover this.
Required resolution: the spec names the new ADR, what it partially supersedes, and that the human accepts it before merge.
Status: resolved (in place)
Resolution: § ADR-0028 amendment gains a paragraph "The typed tree is a substantive ADR change": the reversed rules, the
new-ADR process, the "as reworked" citation pointed at it, acceptance by the human before merge. The ADR text itself is owed
by the design or backend plan.

### TR-3
Severity: major
Affects: BR-AW-6, BR-AW-7, US-3; feat-028 project–team links
Problem: a team linked to a project's node (area–team link) and a team linked to the project (feat-028 project–team link,
which reassigns items on unlink and needs both codes) are two different links between the same pair. The spec never says
how they relate; Alpha's page would show two team lists, and a team can be "in Alpha's node" but not "in Alpha". This is
the same dead-weight argument the spec makes against OQ-7 B, and it decides US-3's whole link half.
Required resolution: a human decision.
Status: open (new OQ-11, recommended A: a project node's «Команды» is the project–team link; area–team links join departments)
Resolution: OQ-11 added with three options.

### TR-4
Severity: major
Affects: BR-AW-4, BR-AW-11, US-3, US-4; mod-002 BR-M1-1
Problem: a department and a project node are siblings at the top level, but project names are unique by project rule and node
names by sibling rule; nothing says whether a project «Финансы» may be created beside a department «Финансы», which error a
project creation or rename gets from a tree clash, or what the path «Финансы» names. A project rename (OQ-10 A renames the
node) can collide with a sibling; no concurrency pair covered it.
Required resolution: a human decision; the concurrency pair added.
Status: open (new OQ-12, recommended A: one name space per level); NFR Concurrency gained the pair (in place)
Resolution: OQ-12 added; NFR Concurrency names a project's creation or rename against a sibling's name and a project's
creation under a department against its deletion.

### TR-5
Severity: major
Affects: BR-AW-10, OQ-6, US-2, US-3; mod-002 BR-M1-20; BR-MX-5 (both ends active)
Problem: OQ-6 A says the deactivated project's node "stays as stored" but not what writes it accepts: moving it between
departments, renaming or describing it, creating or moving sub-areas below it, linking a new team (BR-MX-5 needs active
ends), and whether it blocks its department's deletion. Without a move, a department holding a deactivated project can
never be deleted short of restoring it.
Required resolution: the answer to OQ-6 states the writes.
Status: open (decision in OQ-6); OQ-6 now states what A implies (read-only per BR-M1-20, «Проект не найден.», existing links
end from the team's end, counts as a child, name kept) and a variant that allows the move.
Resolution: OQ-6 extended in place.

### TR-6
Severity: major
Affects: BR-AW-14, US-5, NFR Existing data; OQ-4
Problem: "deeper than 10 levels below its node" is off by one: with the node at level 1 (OQ-4 A), a tree 10 levels below it
is 11 deep and passes the check while breaking the limit; under OQ-4 B the stored root is level 1 and the threshold moves
again. The NFR's migration test "a tree 10 levels deep below its root" sits on the wrong side. The consequence that a tree
kept at the top level at 10 levels can never move into a department was unstated. And failing the whole upgrade is a
choice the analyst made, not the human (the old spec's OQ-1 B listed the cut as a cost).
Required resolution: a correct boundary per OQ-4, the consequence stated, and the fail/keep/cut choice to the human.
Status: resolved in wording (BR-AW-14, US-5's scenario, NFR); decision open as new OQ-13 (recommended A: fail, naming the
project)
Resolution: BR-AW-14 counts levels with the root, per OQ-4; US-5 tests 11 (fails) and 10 (kept, then a move into a
department refused); NFR's test sits at the limit and one past it.

### TR-7
Severity: minor
Affects: BR-AW-3, US-2 note (1); mod-002 BR-M1-22
Problem: BR-AW-3 let a sub-area's parent be "a project node or another sub-area" of any project, though BR-AW-11 needs an
item's area to stay below its own project. A move of «Платформа» under Beta's node would carry Alpha's items into Beta.
Required resolution: the rule states a sub-area stays inside its project's subtree.
Status: resolved (in place); the refusal's code and text are left to the design.
Resolution: BR-AW-3 amended.

### TR-8
Severity: minor
Affects: BR-AW-13, US-4
Problem: BR-AW-13 says every place outside the tree names a node by its full path from the top level, but US-4 shows the
card's «Область» as «Alpha / Платформа / API» or project-relative, as mod-003 has it.
Required resolution: name the exception.
Status: resolved (in place)
Resolution: BR-AW-13 names the item's «Область» as the exception, its form the design's.

### TR-9
Severity: minor
Affects: § Error codes; US-2, US-4
Problem: the registered `area.not_empty` text is «Нельзя удалить область, в которой есть вложенные области»: it says nothing of
items, yet the spec says "covering child nodes and items" and US-4 refuses a leaf with items by it. `root_is_fixed`
(«Корневая область меняется только вместе с проектом») is reused for a project's node and, under OQ-4 B, the organization
root, which it does not describe. `type_not_allowed` has no text at all, and the cross-project sub-area move (TR-7) no code.
Required resolution: the spec quotes the final text of `not_empty` (or a second code for items, as mod-003 had it) and states
whether `root_is_fixed` keeps its text; the design fixes `type_not_allowed`'s.
Status: resolved (fixed by BA 2026-10-28; not re-read, light route one round)
Resolution: spec § Error codes quotes `not_empty`'s new text (children or items, one code), `type_not_allowed`, the new `outside_project`, and keeps `root_is_fixed`'s text for a project's node; the design may refine the wording.

### TR-10
Severity: minor
Affects: NFR Existing data, US-5
Problem: the NFR said "the first build's index replacement is reverted by the rework migration", i.e. layered on top of the
first build's migration, while US-5 let the developer "rework or supersede" it. Layering upgrades from a database that has
already dropped the trees and the item's area.
Required resolution: the first build's migration does not ship.
Status: resolved (in place)
Resolution: NFR and US-5 say it is reworked in place or rewritten, never layered on; a database that applied it is no upgrade
source.

### TR-11
Severity: minor
Affects: status.md § Notes; story file names
Problem: status.md's Summary still describes the old spec (area–project links, the item's area retired, OQ-1 A); US-1..US-3's
and US-4..US-5's slugs are stale (the stories say so).
Required resolution: the orchestrator rewrites the Summary; slugs may stay.
Status: resolved (fixed by BA 2026-10-28; not re-read, light route one round)
Resolution: status.md Summary rewritten to the typed tree; slugs were renamed by the orchestrator.

### Open questions — check

- OQ-3, OQ-4, OQ-5, OQ-7, OQ-8, OQ-10: real decisions, options sound. OQ-5 B and C are weak (B breaks BR-AW-11), but say so.
- OQ-6: real; was missing the writes on a deactivated node (TR-5), now stated.
- OQ-9: barely a decision now: under OQ-2 B nothing reads the switch, and B only removes columns mod-019 may want. It can be
  answered A without a question, or dropped and left to mod-019; the reviewer recommends dropping it from the package.
- Added: OQ-11 (TR-3), OQ-12 (TR-4), OQ-13 (TR-6).

### Checked and found sound

Roles table and code split per ADR-0029 (`project.create` admin only, so placing at creation is consistent); BR-AW-5 under
OQ-2 B; BR-AW-8; BR-AW-11 against mod-002 BR-M1-11 and mod-003; US-3's link scenarios once OQ-11 is answered; US-4; US-5's
counts (4 project nodes, 4 sub-areas, 18/3/1/2 items); operation completeness; NFR volumes and query shape; Out of scope.

### Verdict

`needs-human-decision`: OQ-3..OQ-8, OQ-10..OQ-13 go to the human (OQ-9 recommended dropped). Before the package, TR-1 (blocker)
goes back to the business-analyst for the US-1, US-2 and US-6 rewrite, and TR-9 to the business-analyst and design.
Blocker open: 1 (TR-1). Major open: 3 (TR-3, TR-4, TR-5, all as questions); TR-2 and TR-6's wording fixed. Minor open: 2
(TR-9, TR-11).
