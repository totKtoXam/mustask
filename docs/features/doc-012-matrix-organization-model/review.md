# Review: doc-012-matrix-organization-model

## Round 1 — requirements

Seven lenses read the spec, the eleven stories and the ADR-0028/0029 drafts, each in its own clean context: coverage, structure, access, views and sprints, navigation and decomposition, ADR consistency, cross-story consistency. Independent verifiers then tried to refute lens 1's findings. One consolidator, who wrote none of the artifacts, judged every finding against the cited lines in spec.md, the stories, the ADRs, status.md and the brief, and merged the results. 70 lens findings came in. 2 were dropped outright, and the other 68 were merged into 34 entries; several were narrowed on the way (see "Parts narrowed away"). The result is 5 blockers, 19 majors and 10 minors. By fix kind: 27 edit, 3 rework, 4 human.

### Completeness: brief → where covered

Condensed from lens 1. Rows that the lens found covered with no gap are folded together. Every partial or not-covered row is kept and points at its entry.

| Brief | Requirement | Covered by | Verdict |
|---|---|---|---|
| §§ 1, 3.1, 4, 5, 21.1-21.6 | Organization as root; independent areas, projects and teams; M:N links inside one organization; no Department | BR-MX-1, -2, -5; US-1..US-3; ADR-0028 rules 1, 4 | covered |
| § 2 | The audit covers "automations, notifications" | audits 1-7 say nothing of them; the out-of-scope "notifications" gives no reason | partial → R-33 |
| § 2 | Split an entity's several duties into separate mechanisms | OQ-1 and ADR-0028 rule 3 drop the classification duty without saying so | partial → R-24 |
| § 3.2 | Name, parent, links, roles, inheritance, aggregated views, no cycles, deletion keeps everything | BR-MX-3..-8, -17, -22; US-1, US-3, US-5, US-8 | covered |
| § 3.2, § 9, § 14 | Participants; area membership and project participation as relations | Glossary, BR-MX-20, -26 (the mapping to grants is not stated) | partial → R-30 |
| § 3.2 | Area "visibility settings" | § Out of scope, with no human decision | partial → R-8 |
| § 3.2, § 11.2 | Area "shared analytics and reporting"; an «Analytics» section | § Out of scope points at mod-008/mod-009, which do not cover areas (doc-005 D-15, BR-M6-1) | NOT COVERED → R-7 |
| § 6 rules 1-10 | Owning team, assignee, sprint consistency | BR-MX-9..-13; US-4, US-9 | covered; "never changes" goes past rule 1 → R-24 |
| § 7.1 | Filters: project, team, assignee, type, status, priority, sprint | US-11 | covered; where options come from → R-16 |
| § 7.1 | Filter: date | US-11 «Целевая дата» only, no reason given | partial → R-16 |
| §§ 7.1, 11.2 | Filter and overview figure: blockers | § Out of scope (reason true, owner unnamed) | partial → R-33 |
| §§ 7.2-7.3, § 7 common | Project and team boards; no copies; statuses; moves; paging; permissions | BR-MX-16..-19, -21; US-7, US-8 | covered |
| §§ 8.1-8.4, 8.6-8.9 | Team sprints across projects; sprint, project and area boards | BR-MX-13..-15; US-9 | covered; area figure → R-4; sprint board → R-12 |
| § 8.5 | Do not lose the history of completed sprints | BR-MX-14 outcome record; a deleted team's sprints become unreachable (ADR-0028 only) | partial → R-34 |
| §§ 10, 10.1-10.4 | Scoped RBAC, inheritance, visibility, one deterministic algorithm | § Roles; BR-MX-20..-24; ADR-0029; US-5, US-6 | covered; wording against the matrix → R-10 |
| §§ 11.1, 11.3-11.6 | Navigation, object pages, switcher, multiselects, empty states, no false hierarchy | BR-MX-27, -28; US-10 | covered |
| § 12 | Entities, keys, constraints | ADR-0028 rules 1, 4, 5 | covered |
| § 13, § 20 (data), § 21.12 | Data migration and preservation | § Existing data | waived (status.md) |
| § 14 | One reusable filtering logic for area, project and team | NFR Query shape binds the views only; the list's filters come from three other lines | partial → R-18 |
| § 16 | Domain, authorization, data tests | stories; NFRs | covered (data within the waiver) |
| § 16 UI/E2E, § 18 step 6 | Nine UI/E2E checks; a final run of the mandatory scenarios | nothing traces them to the pipeline's mechanism | NOT COVERED (trace) → R-33 |
| §§ 17, 19, 20, 21.7-21.15 | Non-regression, deliverables, DoD, anti-patterns | NFRs; § Decomposition; BR-MX-1..-28 | covered |
| SC-01..SC-09 | Mandatory scenarios | US-1..US-11 | covered; SC-10 is OQ-2 |
| status.md 3.3 | "The scope is the whole brief" | not cited; three items cut with no decision behind them | → R-7, R-8; blockers are settled by doc-010 D-18 → R-33 |

### Dropped lens findings

- **L3-8** (minor, three inheritance edge-case scenarios). BR-MX-22 and ADR-0029 settle all three cases unambiguously, as the lens agrees, and ADR-0029 § 12 already owes a test for each brief § 10.4 item 7 case. Adding them would grow two stories that are already at 149-150 lines and add no behaviour.
- **L5-10** (minor, trim deps and split line 11). This optimizes the delivery order; it is not a defect. Lens 5's own table finds the serial chain sound, and shortening it would need the owed-by markers that L7-4 lists. The orchestrator can reorder when it enters the lines after gate-1.

### Parts narrowed away

- L1-1: the claim that the visibility rule is missing is wrong, because BR-MX-21 covers every aggregate. The proposed edit is also wrong: it would add a third dashboard level against doc-005 D-15 and BR-M6-1. The entry became a human question (R-7).
- L1-3: dropped the § 13 leg, because § 13 is waived and its classification clause was about mapping data.
- L1-4: the pipeline already covers § 16's UI checks and § 18.6: per-line QA and component tests, owed-by re-runs, and `verify.sh` at every merge. Option B would decide each task's E2E question in advance, which routing.md does not allow. What remains is a minor trace edit (R-33).
- L1-5: OQ-1 already defines the list's area filter. Making feat-013 the founder would reverse doc-010 D-14 (R-18 keeps D-14's direction).
- L1-6: the human question is settled by doc-002 § Out of scope and doc-010 D-18 (other link types are release 2). It became an edit.
- L1-7 and L1-8: their proposed wordings were inaccurate. Project participation is not always a grant, and «Дата начала» is a second date the user sets. Both were corrected in R-30 and R-16.
- L3-5: US-9's removal outline does cover the hidden-item refusal, so the human option fell away. What remains is the unnamed exception (R-9).
- L4-1 and L4-10: each became an edit. Brief § 8.8 and US-9 decide L4-1, and stating the assumption behind a budget is the analyst's job (L4-10).
- L6-3: the fate table already lists mod-016 BR-M1-34.

### R-1
Severity: blocker
Affects: BR-MX-12; US-2, US-3, US-4; ADR-0028 § 6 *Rule 8*
Problem: The race between an unlink and a save that sets the team has three prescribed outcomes. BR-MX-12, US-3 and US-4 refuse one of the two requests. US-2, for a deletion that is "this unlink applied to every project", lets the save land and includes it. ADR-0028 *Rule 8*'s locks produce US-2's outcome: the unlink waits behind the save's `FOR SHARE`, then re-reads and reassigns 8 items, not 7, so the race tests in US-3 and US-4 cannot pass. The unlink also locks only its own link. Two unlinks in one project that name each other deadlock into a 500. No answer is given for a destination that is the team itself, is not linked, is deleted, is foreign or is missing, and US-2's "naming SATURN is refused" quotes no text. Nothing says whether a reassignment moves «Изменён», or whether «Удаление команды» names a linked deactivated project.
Required resolution:
- spec.md BR-MX-12: "If an item save or creation sets the unlinked team concurrently, either that save or the unlink is refused." → "A save or creation that sets the unlinked team concurrently either commits first and is reassigned with the rest, or comes after the unlink and is refused with «Команда не участвует в проекте.»; the confirmation counts what the team owned when it opened, and the request always names its destination. A destination that is the unlinked team, or is not linked to the project when the unlink runs, answers «Команда не участвует в проекте.», a deleted, foreign or unknown one «Команда не найдена.», and nothing changes. Each reassigned item's «Изменён» becomes the operation's time."
- US-3, "An unlink and a save that sets the team cannot both pass": Then → "Then either that save or creation commits first and the confirmed unlink reassigns it with the others, so VENUS receives 8, or it comes after the unlink and is refused because MARS is not linked to A, changing nothing". Add to that scenario: "And a confirmed unlink whose destination was unlinked from A or deleted after the dialog opened is refused with «Команда не участвует в проекте.» or «Команда не найдена.», and two unlinks of MARS and VENUS from A that name each other end with one applied and the other refused, never with a server error". In "Unlinking a team that owns no items", add to the confirmed Then: "; an item of A given MARS after the dialog opened goes to «Без команды»". In "SC-09 — confirming reassigns…", change "keep their project, identifier, assignee, state and estimates" → "keep their project, identifier, assignee, state and estimates, and show the unlink's time as «Изменён»". Budget: fold the two Thens of "Linking a linked pair" into one line, so the file stays at or under 150.
- US-4, "A save or a creation that sets a team while it is being unlinked": Then → the same two branches as US-3. The unlink is never refused because of a racing save.
- US-2: "a request naming SATURN is refused" → "a request naming SATURN is refused with «Команда не участвует в проекте.»". In "The deletion reaches a deactivated project too", add "And «Удаление команды» named "Delta" — 3, marked «деактивирован»".
- ADR-0028 § 6 *Rule 8*:
  - Lock order: "(3) project–team links, by project id" → "(3) project–team links, by project id, then team id".
  - *Unlink*: "link `FOR UPDATE`, then …" → "the unlinked link `FOR UPDATE` and the destination's link `FOR SHARE`, in team-id order, re-checking that the destination is still linked, then …".
  - Race "*Unlink vs a save that sets that team:* whichever locks the link second is refused, and the key backs it." → "*Unlink vs a save that sets that team:* a save that locks the link first commits, and the unlink, re-reading under its lock, reassigns it too; a save that locks it second finds no link and is refused, and the key backs it. *Two unlinks in one project, or an unlink vs its destination's unlink or deletion:* the team-id order serializes them; the later one re-reads and is refused if its destination is no longer linked."
Fix: edit
Source: L2-1, L2-2, L2-9, L2-10 (US-2 part)
Status: open
Resolution:

### R-2
Severity: blocker
Affects: NFR Concurrency; US-5 "A grant removal and a request that uses it…"; ADR-0029 § 7 (Concurrency)
Problem: The NFR lists "a grant removal and a request that uses the grant" among the races where one request is refused. ADR-0029 § 7 lets both pass: a request whose check ran before the removal committed is ordered before it, as ADR-0026 rule 8 argues, and nothing re-checks at commit. US-5's Then accepts every observable outcome, so its test cannot fail. ADR-0029 also claims that access-governing writes "re-evaluate the actor's code … after the locks" of ADR-0028, but no lock covers grants. Two managers who remove each other's grants at the same moment both succeed (write skew). A line that follows the NFR needs a per-request grant lock, which ADR-0029 rejects.
Required resolution:
- spec.md NFR Concurrency: "a link and the deletion of either end; a grant removal and a request that uses the grant. In each case one of them is refused (doc-001 BR-11)." → "a link and the deletion of either end. In each case one of them is refused (doc-001 BR-11). A grant, role, link or visibility change is not such a race: a request is decided by the access committed when its check runs, and one checked before the change commits completes as if it ran first (ADR-0029 § 7, as ADR-0026 rule 8)."
- US-5: the scenario title → "A grant removal applies from the next check (§ Non-functional, Concurrency)". Then → "Then a save whose check ran before the removal committed is kept, a save checked after it answers 403 «Недостаточно прав» and leaves the item unchanged, and no save is half-applied".
- ADR-0029 § 7 Concurrency: "either the save completes before the removal or it is refused (US-5). A write that governs access … re-evaluates the actor's code inside its transaction, after the locks ADR-0028 rule 6 orders, as `ChangeMemberRoleHandler` re-reads the actor's role today (BE-SEC-1)." → "a save checked before the removal committed is kept, and one checked after it is refused (US-5). A write that governs access is checked like any other request. Two managers who remove each other's grants at the same moment may both succeed, which leaves the object to the organization admin, who manages every object (§ 9)."
Fix: edit
Source: L3-1
Status: open
Resolution:

### R-3
Severity: blocker
Affects: BR-MX-24; ADR-0029 §§ 1, 6, 7, 8; ADR-0028 § 7 *Addresses*, Consequences "Amends"; mod-007's sprint routes
Problem: BR-MX-24 puts only the project, team and area not-found answers before the 403. A work item or sprint id in the address therefore answers after the 403. ADR-0029 § 8 item 4 answers it before. A «Читатель» saving a missing item of a visible project gets 403 under the spec and 404 under the ADR, and the generated matrix test needs one answer. BR-MX-24 also answers «Проект не найден» for every deactivated project, which drops the restore exception that ADR-0029 § 8 keeps. ADR-0029 § 6 step 2 jumps the admin past the project gate. The policy reads only the organization, project, team and area ids, yet § 8 orders the path's item and sprint not-found before the 403. ADR-0028 gives a sprint no address, and both ADRs put "a sprint" directly in the organization, while § 2 checks a sprint on its team. Grantees and new team members are body ids with no named answer.
Required resolution:
- spec.md BR-MX-24: "«Проект не найден» when the project the request is made in is missing, deactivated or not visible to them; «Команда не найдена» or «Область не найдена» when the team or area the request acts on (for a new child area, its parent) does not exist; 403 when they see that object but lack its code" → "«Проект не найден» when the project the request is made in is missing, deactivated (its restore excepted) or not visible to them; then the not-found of every other id in the address, outside in: «Команда не найдена» or «Область не найдена» (for a new child area, its parent), then the sprint's or the work item's; 403 when they see the object the request acts on but lack its code". In the list of body ids: "a sprint, a parent, an assignee" → "a sprint, a parent, an assignee, a grantee or a new team member (a user who is not a member answers «Участник не найден.»)".
- ADR-0029 § 6 step 2: append "The not-found answers of § 8 items 3-4 still come first."
- ADR-0029 § 7, the route-policy bullet: "reads the route values `organizationId`, `projectId`, `teamId` and `areaId`" → "reads the route values `organizationId`, `projectId`, `teamId`, `areaId`, `sprintId` and the work item's identifier, each resolved inside its container". Add a sub-bullet: "A filtered read addressed by a team or an area checks that object's read code of scope O (`organizations.team.read`, `organizations.area.read`) and filters its rows by its row code."
- ADR-0029 § 8 item 4: "then the work item's not-found" → "then the sprint's («Спринт не найден.») or the work item's not-found".
- ADR-0029 § 1 row 3 and ADR-0028 Consequences "Amends": "organization, then a project, team, area or sprint in it, then a work item in its project" → "organization, then a project, team or area in it, then a work item in its project or a sprint in its team".
- ADR-0028 § 7 *Addresses*, after "no prefix nests one object under another.": add "Owned objects stay under their owner: a work item under its project, a sprint under its team (`…/teams/:teamId/sprints/:sprintId`, in the panel and the API). A sprint id of another team answers «Спринт не найден.», as one that names nothing does."
Fix: edit
Source: L3-2, L6-2
Status: open
Resolution:

### R-4
Severity: blocker
Affects: BR-MX-17; US-8 "A team linked to two areas adds no item to either", "The overview shows…"; US-9 last scenario
Problem: US-9 has the area overview show a linked team's Active sprint "whatever their project". US-8 says no figure of «Логистика» holds PD-US-1, MARS's item in a project linked only to «Финансы», and that every figure equals a view's count. Put PD-US-1 in MARS's Active sprint, and the two stories demand opposite counts for the same figure, so `feat-NNN-area-board-and-overview` and `mod-007-iterations` would each write a test that fails the other's. Brief § 8.8 ("an area may aggregate the active sprints of its linked teams") and US-9 decide it: the figure is the team's sprint, over the items the user may see.
Required resolution:
- spec.md BR-MX-17: append "An area's overview also shows the Active sprint of each team linked to the area, counted as that team's sprint board counts it for the user, whatever the items' projects (brief § 8.8)."
- US-8: "Then no view or figure of «Логистика» holds PD-US-1" → "Then no view of «Логистика», and no figure of its overview but MARS's active sprint (US-9), holds PD-US-1". "And each figure equals the total" → "And each figure but the active sprints equals the total".
Fix: edit
Source: L4-1
Status: open
Resolution:

### R-5
Severity: blocker
Affects: spec § Existing rules (mod-002 BR-M1-20); BR-MX-14; US-9; ADR-0028 § 6 *Rules 2, 3*
Problem: The spec keeps mod-002 BR-M1-20 unchanged: under a deactivated project nothing changes until restore, and a restore brings it back exactly. ADR-0028's amendments say the opposite. A sprint close moves and records a deactivated project's items. A team deletion reassigns them, clears the default naming the team and removes the link. An area deletion removes its link. The spec and the stories state none of these exceptions. No sprint scenario has a deactivated project, and whether its outcome rows count while it is deactivated is open. `mod-007-iterations` would get a kept rule and an ADR that contradict each other.
Required resolution:
- spec.md fate row → "| mod-002 BR-M1-20 | kept for organizations and projects, except that deleting a team (BR-MX-12) or an area (BR-MX-8) and closing a sprint (BR-MX-14) reach a deactivated project: its links to the deleted object end, its items lose that owning team, a default naming the team is cleared, and a close moves and records its items like any others; extended by BR-MX-24 | a deactivated project keeps its other links and gets them back on restore (ADR-0026 rules 2-3, amended by ADR-0028) |".
- spec.md BR-MX-14: after "…or to no sprint." add "A deactivated project's items are moved and recorded like any others, and count on no sprint board, outcome or overview figure until the project is restored."
- US-12 (split from US-9 by R-12): a new scenario: "Given B is deactivated, and the Active «Спринт 12» holds integration in «Активна» / When Pavel closes «Спринт 12» towards «Спринт 13» / Then neither the sprint board nor the close confirmation shows integration or B / And once B is restored, integration is in «Спринт 13»".
Fix: edit
Source: L4-2, L2-8
Status: open
Resolution:

### R-6
Severity: major
Affects: BR-MX-3, BR-MX-6, BR-MX-8; § Roles (area «Управление»); § Operation completeness (Area, Area–project link, Area–team link); US-1, US-3; ADR-0029 § 4 (Link management), § 6 (Consequence writes)
Problem: BR-MX-6 asks for «Управление» on both ends of an area link, because the link decides what reaches a project. A move needs only the area and its new parent, and «Наследовать доступ» needs only the area. Yet both change what reaches every linked project and the parent's managers, and neither asks them. A manager below can cut the parent's managers off with no way back, an inherited manager can lock themselves out, and the one-operation create does not say where its grant is checked. Removing an area link needs both ends, yet any area manager can delete a leaf area and so end all of its links. US-1's confirmation would then name links to projects the deleter cannot see. Brief §§ 10.1 and 10.4 do not say who controls inheritance or unlinking.
Required resolution, depending on the human's answer:
- Under every option:
  - BR-MX-3: append "A move or a change of «Наследовать доступ» takes effect for every project linked to the area or its subtree, and needs nothing from those projects; a project that must not widen is «Ограниченный»."
  - US-1: "after a confirmation that names its links and grants and says it cannot be undone" → "after a confirmation that names its team links, its links to projects the user may see (a deactivated one marked «деактивирован»), and its grants, and says it cannot be undone; a link to a project the user may not see ends too, unnamed and uncounted (BR-MX-21)".
  - ADR-0029 § 6 "Consequence writes": add the area deletion and the team deletion beside the sprint close.
- (A) BR-MX-3: append "Switching «Наследовать доступ», or moving an area away from its parent, also needs «Управление» on its current parent (the organization admin at the top level); in a one-operation create the grant is checked on the parent and the switch is applied last." BR-MX-6: append "Removing an area–project or area–team link needs «Управление» on either end." The Area row's Update cell and the Delete cells of both area-link rows follow. US-1's switch and move scenario names the parent requirement, and US-3's area-link refusal lines follow. ADR-0029 § 4: "A link needs the code on both of its ends (BR-MX-6)" → "Creating a link needs the code on both ends; removing an area link needs it on either end (BR-MX-6)". R-10 writes the § Roles row.
- (B) BR-MX-6: append "Deleting an area (BR-MX-8) or a team (BR-MX-12) ends its links without «Управление» on their other ends." BR-MX-3: append "The area's own «Управление» decides its switch; turning it off can end the actor's inherited «Управление», which the dialog says." US-3 states that a project's manager cannot remove the project from an area they do not manage.
- (C, not recommended) A move, or switching inheritance on, that would widen what reaches a linked project whose visibility is not «Организация» also needs «Управление» on that project.
Fix: human
Decision: the human's, asked before the fixes
Source: L2-4, L3-4, L2-5, L3-6, L2-10 (US-1 part)
Status: open
Resolution:

### R-7
Severity: major
Affects: § Out of scope (area analytics); § Decomposition; US-10 "An area's page"; brief §§ 3.2, 11.2; status.md 3.3
Problem: The brief asks for "shared analytics and reporting" on an area and an unconditional «Analytics» section on its page, and the human set the scope to the whole brief. The spec defers this to "mod-008, mod-009", but neither line can own it. mod-009 is specified for team and project dashboards only (doc-005 D-15), mod-008's queries are single-project (BR-M6-1), and the decomposition rewords neither one for areas. The deferral lands on nobody, and no open question puts the cut to the human.
Required resolution, depending on the human's answer:
- (A) § Decomposition: add the row `feat-NNN-area-analytics` (M; deps mod-009, feat-NNN-area-board-and-overview): "Area dashboards over the area's visible projects and, on request, its descendants', each figure after BR-MX-21; amends doc-005 D-15 and BR-M6-1". § Out of scope: "Area analytics beyond the overview's figures (mod-008, mod-009)." → "Area analytics beyond the overview's figures: `feat-NNN-area-analytics`." US-10: "And there is no analytics section: the area's figures are those of its «Обзор» (US-8)" → "And until `feat-NNN-area-analytics` there is no analytics section: the area's figures are those of its «Обзор» (US-8)".
- (B) § Out of scope: → "Area analytics beyond the overview's figures, waived by the human at gate-1: no queued line covers areas (doc-005 D-15, BR-M6-1)."
Fix: human
Decision: the human's, asked before the fixes
Source: L1-1
Status: open
Resolution:

### R-8
Severity: major
Affects: § Out of scope (area visibility); BR-MX-21; brief § 3.2; status.md 3.3
Problem: The brief lists "visibility settings" among an area's properties. The spec rules out a per-area setting with an argument (two settings could disagree) that covers what an area shows of projects, not the area itself. Every member, a «Читатель» included, reads every area's name, description, links and participants, so an area for a confidential program cannot be hidden. This narrows a brief item under "the scope is the whole brief", and no open question puts it to the human.
Required resolution, depending on the human's answer:
- (A) § Out of scope bullet: "A visibility setting on areas (brief § 3.2): every member reads…" → "A visibility setting on areas (brief § 3.2), declined by the human at gate-1: every member reads…", with the rest of the bullet kept.
- (B) § Decomposition: add `feat-NNN-area-visibility` after `feat-NNN-project-visibility`: an area visibility, «Организация» or «Участники области», that hides the area's node, pages, links and participants from users no grant reaches, including how the tree shows a hidden parent. The out-of-scope bullet is removed.
Fix: human
Decision: the human's, asked before the fixes
Source: L1-2
Status: open
Resolution:

### R-9
Severity: major
Affects: Glossary "Link"; BR-MX-7; BR-MX-21; NFR Non-disclosure; § Operation completeness (link rows)
Problem: The Glossary and BR-MX-7 say that a link grants nothing and never makes a project visible. Under BR-MX-21 and BR-MX-22, an area–project link is exactly what makes a «Участники областей» project and its items reachable for area grantees, and ADR-0029 locks this in ("only area–project links carry access"). A test written from BR-MX-7 fails once visibility ships. "A link has no fields" also contradicts BR-MX-20, which records who made the link and when. BR-MX-21 says a hidden project "does not exist", yet the stories accept two refusals that depend on hidden data: a rename to a hidden project's name (US-6) and the removal of a Planned sprint that holds hidden items (US-9). The NFR's twin test does not name them as exceptions.
Required resolution:
- Glossary "Link": "A link has no fields, grants nothing and discloses no project" → "A link has no fields beyond who made it and when (BR-MX-20); only an area–project link carries access, and only for holders of grants on the area (BR-MX-7)".
- BR-MX-7 → "**Links disclose nothing by themselves.** A project–team or area–team link grants no code and never makes a project visible. An area–project link is the one path by which grants on an area reach a project and its items (BR-MX-21 «Участники областей», BR-MX-22); it never reaches a «Ограниченный» project and gives nothing to a user whom no area grant reaches."
- § Operation completeness, the Update cell of the three link rows: "no: a link has no fields" → "no: a link has nothing to edit".
- BR-MX-21: append "Two refusals are accepted exceptions and name nothing more: a rename to a hidden project's name is refused as taken (project names stay unique in the organization, mod-002 BR-M1-1), and a Planned sprint that holds hidden items is not removable (BR-MX-14)."
- NFR Non-disclosure: after "«ещё» flags included" add ", BR-MX-21's two named exceptions aside".
Fix: edit
Source: L2-3, L3-5
Status: open
Resolution:

### R-10
Severity: major
Affects: § Roles; BR-MX-22; BR-MX-23; ADR-0029 § 4; US-5
Problem: BR-MX-23 gives the organization role's bundle on "the organization's structure or a project with visibility «Организация»". A project is part of the structure, so a literal reading gives a «Участник» who sees a «Участники областей» project through a «Чтение» area grant the role's change codes on it. ADR-0029 § 6 step 4 says "only when p is «Организация»", but US-5 and US-6 cite BR-MX-23. § Roles says less than the matrix in four places:
- Project «Чтение» seems to order the backlog.
- Area «Управление» claims every child area, against US-5's non-inheriting child.
- Team «Планирование» omits rename, dates and removal.
- «Участник»'s "today's behaviour (mod-006 BR-M4-27)" is wrong, because no project backlog exists today.

BR-MX-22 says a team grant never reaches items, yet closing a sprint moves them under the team code (ADR-0029 § 6). Filling a sprint needs the item code, so a «Читатель» planner cannot fill one. No code is named for moving an area to the top level, or for reading an object's grants.
Required resolution:
- BR-MX-23: "The first is the bundle of their organization role, when the object is part of the organization's structure or belongs to a project with visibility «Организация»." → "The first is the bundle of their organization role: always on the organization, its areas, teams and sprints, and on a project and everything in it only when the project's visibility is «Организация» (ADR-0029 § 6 step 4)."
- § Roles:
  - «Участник»: "Reads the organization's structure." → "Reads the organization's members, areas, teams, links and sprints, and the projects whose visibility is «Организация»." Also "All of this is today's behaviour (mod-006 BR-M4-27)" → "On «Организация» projects and on teams this is today's bundle (mod-016 api-contract § 0.2); the project backlog's order is new (`feat-NNN-project-board`)".
  - Project row: "Reads, or also changes, the project's work items and orders its backlog, a Restricted project included" → "«Чтение» reads the project's work items; «Изменение» also changes them and orders the project's backlog; a Restricted project included".
  - Area «Управление»: "its child areas" → "creating child areas under it, and the areas below it that inherit from it", plus R-6's answer.
  - Team «Планирование»: "creates, starts and closes the team's sprints" → "creates, renames, dates, starts, closes and removes the team's sprints; putting an item into one needs the right to change the item".
- BR-MX-22: "and never the items of the team's projects." → "and never the items of the team's projects, except that closing the team's sprint moves its unfinished items (BR-MX-14)."
- ADR-0029 § 4: the `organizations.area.create` Covers cell, "a top-level area" → "a top-level area, and a move to the top level (BR-MX-3)". Add a bullet: "An object's grants and participants are read with its read code: `organizations.area.read`, `organizations.project.read`, `organizations.team.read`."
- US-5, "A team grant reaches the team and none of its projects' items": title → "…, a sprint close aside (US-9)". Append to its Then: "and U's setting an item's sprint answers 403 «Недостаточно прав»".
Fix: edit
Source: L3-3, L3-7, L7-2
Status: open
Resolution:

### R-11
Severity: major
Affects: ADR-0028 § 6, § 8; ADR-0026 rule 7; BR-MX-12
Problem: Accepted ADR-0026 rule 7 makes the status the only cross-module contract: a module acts on a removal in its own write path, and mod-006 hides a deleted team's entries at read time. ADR-0028 *Rule 2* and BR-MX-12 instead make the team deletion and the SC-09 unlink write work items, board places and, later, sprint pointers in one transaction. `IOrganizationStructure` is read-only, and § 8 forbids writes through it, so no write path is named. ADR-0028 does not list rule 7 among its amendments. `feat-028-org-level-teams` would invent the path, `mod-007-iterations` would extend it, and review has nothing to check the lock order against.
Required resolution:
- ADR-0028 § 6: add, before *Rule 8*: "*Rule 7:* the status stays the cross-module contract for every removal but two. The team deletion (rule 2) and the unlink with reassignment (BR-MX-12) change other modules' rows in their own transaction, and the Organizations handler owns both. It calls one in-process write contract per owning module, kept at that module's root beside its read contract. Work items reassign a project's items from one team to another, or to none, and clear their sprint pointers. Boards delete those items' team places. `mod-007-iterations` adds its open-sprint step. Each call locks its own rows in *Rule 8*'s order and joins the caller's transaction; no module writes another module's tables."
- ADR-0028 § 8, the last bullet of "What the slices read instead": append "A write that changes another module's rows goes through that module's write contract (§ 6 *Rule 7*)." R-23 extends the amends lists.
Fix: edit
Source: L6-1
Status: open
Resolution:

### R-12
Severity: major
Affects: BR-MX-14; § Operation completeness (Sprint); § Decomposition (`mod-007-iterations`); US-9; § Stories
Problem: `mod-007-iterations` is to build "the team sprint board", and no artifact defines it. Nothing says which sprint it shows, or what it shows when none is Active. Nothing says which levels it shows, though a sprint spans levels and boards are per level, or which columns, order and moves it has (a team order exists per level). Nothing says what "3 items and 16 SP" sums, or where the board lives, since US-10's team sections have no sprint board. QA cannot write a test for it. US-9 is at 150 lines, and R-5, R-13, R-14 and R-34 add about 20 more.
Required resolution: The analyst defines the board in BR-MX-14, within its line, and in the stories. Recommended content:
- the Active sprint; with none, «У команды нет активного спринта» and a way to the team's «Спринты»;
- one level at a time, with mod-006's level drop-down;
- the organization process's columns for the level (BR-MX-19);
- the team order of that level (ADR-0028 § 9);
- state moves as on the team board (BR-MX-18), with no reorder;
- totals of items and story points over the shown level, after visibility;
- a view inside the team's «Спринты» section, so US-10's sections stay as they are.

Split US-9 into US-9 "A team's sprint lifecycle" (numbering, names, dates, start, removal, who manages) and US-12 "Items in a team's sprint" (joining, team change, close, outcome, the sprint board, project boards, the area overview's figure). `mod-007-iterations` delivers both. Add the story table row for US-12, with depends-on per R-20. The Sprint row's Surface cell becomes "the team's «Спринты», with its sprint board".
Fix: rework
Source: L4-5
Status: open
Resolution:

### R-13
Severity: major
Affects: BR-MX-14, BR-MX-15; spec § Existing rules (doc-004 BR-M5-4, -11); ADR-0028 § 6 *Rule 9*, § 10; US-9
Problem: A close moves "every item whose state is outside the Completed category", and «Удалена» is outside it. A deleted item therefore rides from sprint to sprint unseen. Such an item also makes its Planned sprint unremovable under the Restrict key, which the planner sees as a refusal on an empty board. "The items completed in the sprint" can mean completed during the sprint's dates or Completed at close, and these are different sets (doc-004 BR-M5-11 lets a Completed item join). Completed items keep the Closed sprint, so it has a frozen outcome and a mutable set of current items, and nothing says which one its page and the «Спринт» filter show, or what happens to a reopened item. No rule says whether an Active sprint may be re-dated, or whether a Closed one may be re-dated, which would rewrite burndown history. The sprint name's length and comparison are not stated either.
Required resolution:
- BR-MX-14:
  - "Closing moves, in the same operation, every item whose state is outside the Completed category to a Planned sprint of the team, or to no sprint. Closing also records the outcome: the items completed in the sprint, with their project and story points." → "Closing moves, in the same operation, every item whose state is in the Proposed, In Progress or Resolved category to a Planned sprint of the team, or to no sprint. Closing also records the outcome: the sprint's items in the Completed category at that moment, with their project and story points then. Completed items and items in «Удалена» keep the closed sprint as their sprint until a save changes it; an item reopened later keeps it too, and no close moves it. A Closed sprint's page and figures show its outcome; the «Спринт» filter matches the items whose sprint it currently is."
  - "A Planned sprint with no items may be removed." → "A Planned sprint that holds no item outside «Удалена» may be removed; the removal clears its items in «Удалена». An Active sprint's dates may change but not be cleared; a Closed sprint may be renamed, and its dates never change."
- BR-MX-15: append "A sprint's name is 1–100 characters after trimming and is compared ignoring letter case and surrounding spaces; a removed sprint frees its name, never its number."
- Fate row "doc-004 BR-M5-4, -11; doc-001 BR-7": "kept, except that a Closed sprint takes no item (BR-MX-14)" → "kept for Planned sprints; a started sprint always has both dates, and a Closed one takes no item and keeps its dates (BR-MX-14)".
- ADR-0028 § 6 *Rule 9*: "a Planned sprint with no items is deleted outright (the item key, Restrict, refuses one that has items)" → "a Planned sprint that holds no item outside «Удалена» is deleted outright after clearing those items' pointers (the item key, Restrict, refuses one that still has items)". § 10 *The outcome record*: "one row per item completed in the sprint" → "one row per sprint item in the Completed category at close".
- Stories (US-9 and US-12 after R-12):
  - The removal outline gains the row "| Planned | only an item in «Удалена» | it is gone, and that item has no sprint |".
  - "Closing moves the unfinished items…": add a story in «Удалена» to the Given, and "and it stays in «Спринт 12», outside the outcome" to the Then.
  - "The outcome never changes afterwards": add "When API is later reopened / Then its «Спринт» still reads «Спринт 12», a filter by «Спринт 12» holds it, and the outcome still lists it at 5 SP".
  - The dates scenario: an Active sprint's dates changed are accepted and cleared are refused with «У активного спринта должны быть обе даты.»; a Closed sprint's dates changed are refused.
Fix: edit
Source: L4-3, L4-4, L4-6
Status: open
Resolution:

### R-14
Severity: major
Affects: BR-MX-14; ADR-0028 § 10 (and § 6 *Rule 8*'s sprint binder); US-9
Problem: Only one sprint race is named, a close against a save into the sprint. Four more can break BR-MX-13/-14 or end in a 500:
- a close and the removal of its destination;
- a removal and a save that sets the sprint;
- a close and an unlink or deletion of its team;
- a close and a save that moves one of its items to another team.

No scenario pins any of them. ADR-0028 also contradicts itself on the sprint binder. *Rule 8* lists "a sprint's creation" among the writes that lock the team `FOR SHARE`, as ADR-0026 rule 8 requires. § 10 says creation locks "only that row `FOR UPDATE`, never the team row", which lets a sprint be created for a team whose deletion just committed. The counter row has no place in the lock order.
Required resolution:
- BR-MX-14: append "Each of these races has one loser and no server error: a close and a save into the sprint, the removal of its destination, an unlink or deletion of its team, or a save that changes the owning team of one of its items; a removal and a save into the sprint."
- ADR-0028 § 10 *The stored sprint*: "Sprint creation locks only that row `FOR UPDATE`, never the team row." → "Sprint creation locks the team row `FOR SHARE` (§ 6 *Rule 8*), so it queues behind a deletion, and then that counter row `FOR UPDATE`, after the sprints of step (4)." Add a bullet *Sprint races* to § 10 that names, for each pair above, the lock that serializes it (the sprint rows `FOR UPDATE` by id; the link before the sprint for a save), the request that re-reads, and the loser.
- US-12: turn "A save into a closing sprint is refused or becomes part of the close" into an outline over the five pairs, with the loser and the unchanged state in each row.
Fix: edit
Source: L4-7 (races), L2-7
Status: open
Resolution:

### R-15
Severity: major
Affects: BR-MX-17; US-8; US-11 "The level, the filters and «Включая дочерние области» are remembered…"
Problem: US-8 has one «Включая дочерние области» switch per area, which narrows "its views", and the overview's figures match the view "under the same switch". US-8's data paragraph and US-11 remember the switch per view, so a user with the board off and the backlog on gets an overview that matches neither. BR-MX-17 does not state the default. US-8 does not say whether an area that links nothing itself, but whose child does, shows the no-projects state with the switch off. Nothing says whether area views offer a create command, and BR-MX-10 would give such an item the project default.
Required resolution:
- BR-MX-17: "…and, when «Включая дочерние области» is on, to its descendants." → "…and, when «Включая дочерние области» is on, to its descendants. The switch is one choice per user and area, shared by the area's backlog, board and overview, and is on until the user turns it off (doc-001 BR-15). Area views offer no create command."
- US-8 data paragraph: "it is then remembered per view (US-11)" → "it is then remembered per user and area, for the backlog, the board and the overview alike (US-11)". Empty-states scenario: add "And an area that links no project itself, but whose child does, shows the child's items with the switch on and the no-projects state with it off". Paging scenario: add "And neither the backlog nor the board of an area offers a create command".
- US-11: title "The level, the filters and «Включая дочерние области» are remembered per view and per user" → "The level and the filters are remembered per view and per user, and «Включая дочерние области» per area and per user"; "off on Logistics' board" → "off on Logistics"; "and Logistics' board has the switch off" → "and Logistics' board, backlog and overview have the switch off".
Fix: edit
Source: L4-8, L4-14
Status: open
Resolution:

### R-16
Severity: major
Affects: US-11; brief § 7.1
Problem: US-11 says options "come after visibility" but never says where they come from. For «Команда» on an area there are three candidate sources with different lists, so the non-disclosure twin test cannot be written, and two lines build these filters at different times. «Спринт» has no «Без спринта» (a planner's way to find unscheduled work), and nothing says whether Closed sprints are listed. doc-004 BR-M5-9's «Текущая итерация» is "replaced by US-11's sprint filter", which has no such option. The brief's "date" became «Целевая дата» with no stated reason, although «Дата начала» and the creation and change times exist.
Required resolution: US-11, after the data table, add: "A filter's options are:
- «Проект»: the projects the view reaches that the user may see;
- «Команда»: the teams linked to those projects, then «Без команды»;
- «Исполнитель»: the people assigned to the view's items that the user may see, then «Не назначен»;
- «Спринт»: on a team's views «Текущий спринт» (its Active sprint) and the team's sprints; elsewhere the sprints of the teams that own the view's visible items; newest first, each naming its team, Closed ones included, then «Без спринта»;
- «Тип» and «Статус»: those the process gives the level.

No option shows a count. The brief's "date" (§ 7.1) is «Целевая дата», the date that plans an item's delivery; «Дата начала» and the creation and change times are not view filters."

Add an Examples row: "| «Спринт» | «Без спринта» | B1 | A3 | A3, B1 |".
Fix: edit
Source: L4-9, L1-8
Status: open
Resolution:

### R-17
Severity: major
Affects: NFR Client load, NFR Volumes; US-7 "Large team and project views"
Problem: Client load says the 5 000-item budget "bounds" whole-level team and project reads, but the spec's own volumes (100 000 items, area views over 50 000 items) let one project level exceed it. The new project view holds every team's items of the project, and nothing says what a 20 000-story level does. US-7's 2 s check runs on three «Организация» projects with no grant, so the visibility and grant path, where the new cost sits (P-4), is never measured. doc-004's bulk-write budget (5 s for up to 5 000 affected items, all or nothing) has no successor, although BR-M5-12 is "replaced → BR-MX-14", and BR-MX-12's reassignment has no budget.
Required resolution:
- NFR Client load: "and the 5 000-item budget bounds them" → "within the budget a team's or a project's level holds at most 5 000 items; above that the read stays whole and has no time budget, an accepted risk revisited when a project nears it".
- NFR Volumes: "including the first page of an area view over 50 000 items" → "including the first page of an area view over 50 000 items of at least 10 projects". Append "Closing a sprint, and an unlink or a team deletion that reassigns items, complete within 5 s for up to 5 000 affected items, all or nothing (doc-004's bulk-write NFR, carried over)."
- US-7: "Given MARS owns 5 000 stories across A, B and C, and A holds 5 000 stories" → "Given an organization at the spec's NFR volumes, where MARS is linked to 50 projects of all three visibilities and owns 5 000 stories across them, A holds 5 000 stories, and U holds 50 area and project grants".
Fix: edit
Source: L4-10, L4-11 (US-7 part), L4-7 (budget part)
Status: open
Resolution:

### R-18
Severity: major
Affects: NFR Query shape; § Decomposition (rows `mod-NNN-work-item-owning-team`, `feat-NNN-team-surfaces-across-projects`, `feat-013-board-backlog-filter`); spec § Existing rules (doc-003 US-2 row); doc-010 D-14; doc-003 BR-M4-22
Problem: Brief § 14 forbids duplicating filtering logic. The NFR binds only the views to one specification, while the «Рабочие элементы» list gets its team filter from the owning-team line, its sprint filter from feat-008 and its state, assignee and keyword filters from feat-003. feat-013 "drops its feat-003 dependency", which cuts the human's D-14 tie between the views' filter bar and the list's (doc-003 BR-M4-22) without a fate row. feat-013 then "takes over" filters that lines 2 and 4 already published: that is either a breaking change or the wrong word. The statement-count rule grows only with "rows or projects", so a walk per area level or per ancestor grant passes it.
Required resolution:
- NFR Query shape:
  - "in a number of statements that does not grow with the rows or the projects" → "in a number of statements that does not grow with the rows, projects, teams, areas, tree depth or grants".
  - "The views take their filters from one shared specification, so a filter means the same on every view (US-11)." → "The views and the «Рабочие элементы» list take their filters from one shared specification, which feat-003 founds for the list and feat-013 extends to the views (doc-010 D-14), so a filter means the same on every surface (US-10, US-11); each line that adds a filter adds it there."
- § Decomposition, `feat-013-board-backlog-filter`:
  - Task: "taking over the team views' project filter and the project views' team filter" → "extending the shared filter specification that the project views' team filter and the team views' project filter began, their request parameters kept".
  - Rewrites: "it drops its feat-003 dependency" → "it keeps building on feat-003's filters (D-14)". Append to the feat-016 clause "with feat-013 in its deps".
- Rows `mod-NNN-work-item-owning-team` and `feat-NNN-team-surfaces-across-projects`, Task: append "(a dimension of the shared filter specification)".
- Fate row "doc-003 US-2 (a filtered backlog reorders against the full list)" → "doc-003 US-2 (a filtered backlog reorders against the full list) and BR-M4-22 (the views' filter bar is the list's)", with the Fate cell becoming "kept on team, project and area views (US-11, NFR Query shape)".
Fix: edit
Source: L1-5, L5-9, L4-11 (Query shape part)
Status: open
Resolution:

### R-19
Severity: major
Affects: § Decomposition (lines 1, 3, 4, 6, 8, 9, 11; Notes for the queue); § Operation completeness (Surface header); ADR-0028 § 7 *Old addresses*, Consequences (delivery table); US-10
Problem: Each line "ships on its own", but only the team and area management pages get an interim home. Six new surfaces have none:
- line 1's project views, which no entry reaches;
- line 4's team-alone addresses, which the three-level switcher cannot represent, and whose Breaks cell omits the context model;
- the project's end of a link and the «По умолчанию» mark, once line 3 retires `…/projects/:p/settings/teams`;
- line 6's project grants;
- line 8's area prefix;
- line 9's «Спринты».

Line 11 also moves the organization settings' project list and members page, and the project's general settings, with no stated fate for the old addresses. US-10's retired kinds omit the settings pages that line 11 retires itself. The note "feat-038 is unaffected" is wrong, because feat-038 lands on the project list, whose address moves.
Required resolution: The analyst picks (A) interim-surface clauses or (B) moving the shell part of line 11 to right after line 4. A is recommended because it keeps the order. Minimum content under A:
- Surface header: "(US-10; the team and area ones sit in the organization settings until the navigation line)" → "(US-10; until the navigation line: team and area pages in the organization settings; the project's teams, area links, default team, visibility and grants in the project's settings; each view reached from its object's page)".
- The Task cells of lines 1, 3, 4, 6, 8 and 9 each name the entry and the address that reach their new pages. Line 4: "until the navigation line, the switcher's team level lists the organization's teams the user may see, own teams first, whatever project is chosen, and an address naming a team alone sets organization + team". Line 4's Breaks cell adds the switcher's context model.
- ADR-0028 § 7 *Old addresses*: "`…/projects/:p/settings/teams[/:t]`" → "`…/projects/:p/settings/teams/:t`". Add "`…/projects/:p/settings/teams` stays as the project's linked teams (link, unlink, default mark) until the navigation line moves it; `/organizations/:o/settings/projects[/deactivated]`, `…/settings/members` and `…/projects/:p/settings/general` move to `…/projects`, `…/members` and the project's «Общие» and «Настройки», and the navigation line retires the old ones the same way." The Consequences table's `feat-028-org-level-teams` row follows.
- US-10, the retired-kind clause: "or a project's settings of teams or of areas —" → "a project's settings of teams, of areas or its general settings, a team's or an area's page in the organization's settings, or the organization settings' project list or members page —".
- Notes for the queue: "feat-038 (onboarding) is unaffected, but must not assume that a project comes with a team" → "feat-038 (onboarding) must not assume that a project comes with a team, and its landing on the organization's project list moves to `…/projects` with the navigation line; whichever merges second adjusts it, noted in feat-038's status.md".
Fix: rework
Source: L5-1, L5-5
Status: open
Resolution:

### R-20
Severity: major
Affects: spec § Stories (paragraph and depends-on column); § Decomposition (rows 2 and 4); the depends-on headers of US-2, US-3, US-4, US-9; US-6; US-7
Problem: The depends-on column contradicts the delivery order. US-4 depends on US-3, yet US-4's line ships before both of US-3's lines. US-3 and US-2 count and reassign owned items, which only US-4 brings, but neither lists US-4. US-9 lists only US-4 and US-7, though it uses grants, Restricted projects, the overview, the unlink and the deletion. The owed-by rule names "an area, a grant, a visibility, a sprint" but not the two things that lines 1-3 lack: an owning team or «Без команды», and a team linked to more than one project. So US-7's Background cannot run on line 1. Rows 2 and 4 both claim "US-7, team part". US-6's list clause names nothing that exists at line 7, and its overview clause asserts active sprints at line 8, before sprints exist.
Required resolution:
- § Stories paragraph: "— an area, a grant, a visibility other than «Организация», a sprint —" → "— an owning team or «Без команды», a team linked to more than one project, an area, a grant, a visibility other than «Организация», a sprint —".
- Depends-on, both in the story table and in each story's header:
  - US-2: "-" → "US-4";
  - US-3: "US-1, US-2" → "US-1, US-2, US-4";
  - US-4: "US-3" → "-";
  - US-9: "US-4, US-7" → "US-2, US-3, US-5, US-6, US-7, US-8";
  - US-12 (R-12): "US-9, US-2, US-3, US-5, US-6, US-7, US-8".
- § Decomposition: row 2, "US-7, team part" → "US-7: owned items, a change of team, the project views' team filter"; row 4, "US-7, team part" → "US-7: views across a team's projects, the project filter, the card in its own project, the tie-break, creation from a team view".
- US-6: "and in every context whose list spans projects" → "and (owed by `feat-NNN-area-project-team-navigation`) in the organization's «Рабочие элементы»". Also "its projects, teams, participants, counts by status and active sprints match the twin" → "its projects, teams, participants and counts by status match the twin, and (owed by `mod-007-iterations`) so do its active sprints".
- US-7: mark inline, with no new lines, the clauses that need «Без команды» or a default team "(owed by `mod-NNN-work-item-owning-team`)", and those that need MARS on two projects "(owed by `feat-NNN-team-surfaces-across-projects`)".
- The story-file lines above are applied by the batch that owns each file. This entry's batch owns the spec's paragraph, the table rows of every story but US-9 and US-12, and the decomposition.
Fix: edit
Source: L5-2, L7-4
Status: open
Resolution:

### R-21
Severity: major
Affects: spec § Existing rules (mod-002 BR-M1-23, -24 row); US-10; feat-001 BR-H-16 (the human, D-7); mod-015 BR-6, -11, -18, -19; ADR-0024 §§ 1, 4
Problem: US-10 overturns shell rules that the fate table never names (P-1):
- mod-015 BR-6: the trail now shows the context and the page itself;
- BR-11: three levels;
- BR-18: the switch falls back to «Главная»;
- ADR-0024 § 1 (scoped entries) and § 4 (not-found levels).

The row replaces BR-M1-24 wholesale, its organization level included, yet nothing says how a user changes organization, which US-10 relies on. US-10 also names the board section «Доска». That silently reverses the human's feat-001 BR-H-16 ("The board page and its navigation entry are named «Канбан»"), which has no fate row.
Required resolution:
- Fate table: the row "mod-002 BR-M1-23, -24; mod-016 BR-M1-27 | replaced → BR-MX-27, BR-MX-28 | …" becomes two rows:
  - "| mod-002 BR-M1-23, BR-M1-24's project and team levels; mod-016 BR-M1-27; mod-015 BR-6 (trail), BR-11, BR-18; ADR-0024 § 1 (scoped entries), § 4 (not-found levels) | replaced → BR-MX-27, BR-MX-28, ADR-0028 rule 7 | objects get pages of their own; the trail names the context and the section; choosing an object opens its page |"
  - "| mod-002 BR-M1-24's organization level; mod-015 BR-19; the product mark's «Главная»; feat-001 BR-H-15, -16 | kept: changing organization is untouched, and the board section of an area, a project and a team is «Канбан» | the human, 2026-10-06 (feat-001 D-7) |"
- US-10: every «Доска» → «Канбан» (the trail reads «Ромашка / Команды / MARS / Канбан»). In the switcher scenario, add "And its organization part lists her active organizations by name and ends with «Все организации», as today".
Fix: edit
Source: L5-3, L5-4
Status: open
Resolution:

### R-22
Severity: major
Affects: § Decomposition (Task, Rewrites and Breaks cells); the queue lines mod-011, feat-003, mod-008, feat-023, feat-024, feat-008, bug-003, tech-019
Problem: The orchestrator cannot enter several rewords as written:
- mod-011 is "reworded for team sprints" while mod-007 now ships the team sprint board, and its deps cite the withdrawn feat-004 and feat-014.
- The feat-003 (area filter) and mod-008 (Under on «Область») rewords give no direction.
- feat-023 and feat-024 extend to project views without `feat-NNN-project-board` in their deps, and feat-023 is no longer S.
- feat-008's «Текущий спринт» on the list names no team.
- The nine new lines carry no epic.
- bug-003 and tech-019 overlap line 11 unnamed.
- Line 7's Breaks cell names "mod-003 assignee candidates", but the route is mod-016 api-contract § 3.4, inventory row 34, so the break would be noted in the wrong status.md.
Required resolution: Widen the cells, with no new sub-table because of the line budget, so that each reword states its new scope, deps and size:
- mod-011 (row 9): "keeps the sprint backlog and the «Задачи» taskboard on team sprints, deps `mod-007-iterations, feat-009`".
- feat-003 and mod-008 (row 5): "their area filter and Under on «Область» are dropped (OQ-1 A; under B they apply to the classification tree)".
- feat-023 and feat-024 (row 1): "both gain `feat-NNN-project-board` in deps; feat-023 becomes M".
- feat-008 (row 9): "«Текущий спринт» on the list is the Active sprint of the context's team, and is absent without a team context".
- Epics, in each row's Task cell: `core/backlogs-boards` (rows 1, 4, 8, 10), `core/work-items` (2), `core/org-access` (3, 5, 6, 7), `core/iterations-planning` (9), `platform/ui` (11).
- Row 11 Rewrites: "takes over tech-009 F4 (the switcher's address ids); makes tech-011 m4 obsolete" → "takes over tech-009 F4 and bug-003 (route matching for the marker and the trail); makes tech-011 m4 obsolete; runs after tech-019, or absorbs it".
- Row 7 Breaks: "mod-003 assignee candidates (gain a project)" → "mod-016 api-contract § 3.4 and inventory row 34 (`…/workitems/assignee-candidates` moves under the project)".
Fix: rework
Source: L5-7, L5-12, L6-3 (Breaks-cell part)
Status: open
Resolution:

### R-23
Severity: major
Affects: ADR-0028 header Context line, § 6, Consequences ("Amends", "Retires or replaces"); ADR-0029 Consequences; spec § Existing rules (doc-006 BR-M1-7 row, ADR-0024/ADR-0026 row)
Problem: Each ADR keeps its own list of retired rules and per-line duties, and both lists have drifted from the spec's fate table, which the lines follow (P-1, P-10).
- ADR-0028 omits rows that the spec retires: mod-006 BR-M4-5, -20, -21, -24; feat-027 BR-T-6, -10, -16; doc-001 D-2, D-8, BR-3, D-11, BR-9; and others.
- ADR-0029 omits doc-006 BR-M1-7, doc-001 BR-13 and mod-003 BR-M3-22. It names doc-004 § Roles, which the spec lacks.
- ADR-0029 gives `mod-007-iterations` no set-form surfaces and the navigation line no duty. Its § 12 matrix "crossed with each visibility" is assigned to a line that merges before visibility exists.
- ADR-0028 also changes ADR-0026 rule 5 (not-found levels) and rule 7 (R-11) but lists neither, and it gives no index-line instruction for its acceptance.
Required resolution:
- ADR-0028 Consequences "Retires or replaces": replace the mod-002, mod-016, mod-003 and doc-004 bullets with "every rule that the spec's fate table marks retired or replaced with BR-MX-1..-19, -27, -28 or this ADR as its successor; that table is the authority, and this list does not repeat it". Keep the api-contract § 7 bullet.
- ADR-0028 header Context line and Consequences "Amends": "ADR-0026 rules 1, 2, 3, 6, 8 and 9" → "ADR-0026 rules 1, 2, 3, 5, 6, 7, 8 and 9". Append to "Amends": "When this ADR is accepted, the index lines of ADR-0024 and ADR-0026 get 'amended by ADR-0028'."
- ADR-0028 § 6: add "*Rule 5:* the not-found levels are the organization, then the one object the address names (rule 7), then the ids that object owns; 'deactivated' and 'deleted' are still never revealed."
- ADR-0029 Consequences:
  - "Retires or replaces": prefix "The spec's fate table is the authority for rules."
  - "What the delivery lines implement": the `mod-007-iterations` bullet → "`iterations.sprint.read` and `.write`, and the set form on the sprint list's totals, the sprint board and the area overview's sprint figures (US-6, US-9)". Add "`feat-NNN-area-project-team-navigation`: the set form on the organization's, an area's and a team's project lists, the switcher and the cross-project «Рабочие элементы» list (US-10)". In the `mod-NNN-scoped-access` bullet, the § 12 matrix becomes "crossed with visibility from `feat-NNN-project-visibility` on".
- Spec fate rows:
  - "doc-006 BR-M1-7; doc-001 BR-5, D-1 (scope) | replaced → BR-MX-23" → "doc-006 BR-M1-7; doc-001 BR-5, D-1 (scope); doc-004 § Roles (any member manages any team's iterations) | replaced → BR-MX-22, BR-MX-23".
  - "ADR-0024 § 2; ADR-0026 rules 1, 2, 3, 6, 8, 9" → "… rules 1, 2, 3, 5, 6, 7, 8, 9", with "not-found levels (5); consequence writes across modules (7)" added to its Why.
Fix: edit
Source: L6-3, L6-4
Status: open
Resolution:

### R-24
Severity: major
Affects: OQ-1, OQ-4, OQ-5; BR-MX-9; ADR-0028 § Problem, § 1, § 3
Problem: Three open questions frame the human's choice incompletely.
- OQ-1 recommends retiring the item's «Область» because "its one real duty" is team ownership. Audits 1 and 3, and ADR-0028's own Problem, count the classification path as a second, user-visible duty. Brief § 2 asks for duties to be split, not dropped. The queued feat-003 and mod-008 plan to use the path. The obvious third option, a pointer to a workspace area, is not shown.
- OQ-5 lists only doc-011's access and board rows. doc-011's `work-item-move`/`copy` rows meet BR-MX-9's absolute "never changes", although merged doc-002 limited that to the MVP. Option A works only if doc-011's gate-1 waits.
- OQ-4's option B (a regenerated baseline) breaks `docs/conventions/backend.md`'s never-edit rule, which ADR-0028 attributes to the human's waiver instead, and neither says so.
Required resolution:
- OQ-1: "Its one real duty, team ownership, moves to the owning team; nothing filters, queries or reports by it today; and keeping it would leave two trees named «Область»." → "It has two duties today, team ownership and the classification path shown on cards and lists. A moves the first to the owning team and drops the second, which departs from brief § 2 (split an entity's duties, do not drop them); the queued feat-003 and mod-008 plan to filter and report by the path, though nothing does today; and keeping it would leave two trees named «Область»." Append "(C, rejected) An optional pointer to one workspace area linked to the item's project: an item would then reach an area twice, by its project's link and by the pointer, and area views would have to choose."
- ADR-0028 § 3: "The pointer's only real duty, team ownership, moves to the owning team." → "Of the pointer's two duties, team ownership moves to the owning team and the classification path is dropped, departing from brief § 2 (spec OQ-1)."
- BR-MX-9: "A work item belongs to exactly one project, which never changes." → "A work item belongs to exactly one project, which no team, area, link, board or sprint operation changes; moving an item to another project is out of scope." ADR-0028 § 1: "which never changes (BR-MX-9)" → "which no structure operation changes (BR-MX-9)".
- OQ-5:
  - After "assume per-team boards inside one project.", insert "Its `work-item-move`/`copy` rows must also state what a move does to the owning team, the sprint and both places (BR-MX-9, -13, -18)."
  - Append to (A): "doc-011's gate-1 waits for this one; the restatement is made on doc-011's branch and logged in its status.md."
- OQ-4 (B): append "It departs from `docs/conventions/backend.md` (a merged migration is never edited), and § Existing data changes with it."
- ADR-0028 § Problem: "and the human's waiver, under which rows may be dropped but merged migrations are never edited" → "the human's waiver, under which rows may be dropped (status.md); and `docs/conventions/backend.md`, under which a merged migration is never edited". Add before § Consequences: "**Departures from `docs/conventions/*`:** none under OQ-4 (A); option B would depart from backend.md's never-edit rule and record it here."
Fix: edit
Source: L1-3, L1-10, L5-13, L6-5
Status: open
Resolution:

### R-25
Severity: minor
Affects: US-6 "A Restricted project linked to two areas and a team stays protected"; US-3; US-10
Problem: In US-3 and US-10 the link multiselect only adds: a linked end is never offered, and a link is removed by its row's unlink command, with SC-09's destination choice. US-6 instead treats the picker as a set editor ("not even as an existing link", "saving a link picker never unlinks X"). That model needs a different API, has to merge hidden links back in, and would let an unlink skip SC-09's destination choice.
Required resolution: US-6: "Then X appears nowhere, not even as an existing link, and saving a link picker never unlinks X" → "Then X appears nowhere, neither as a linked row nor as an option of an add multiselect, and no link or unlink U sends from these pages removes a link of X".
Fix: edit
Source: L7-1
Status: open
Resolution:

### R-26
Severity: minor
Affects: BR-MX-10; US-4 "A child starts with its parent's team, else the project default"
Problem: BR-MX-10 lets the creator change the team "before saving", but US-4 says a row's add and a checklist's add have no «Команда» field. Its child clause, "if that team is still linked", can bite only while the parent's team is being unlinked, and then sends the child to the project default while the unlink moves the parent elsewhere. Precedence puts "created from a team's surface" before "created as a child", so a checklist add on a card of another team, opened over MARS's page, has two answers.
Required resolution:
- BR-MX-10 → "**Creation defaults.** A new item's owning team starts as follows. Created as a child, on any surface: its parent's owning team as stored when the child is saved, or the project default team when the parent has none. Otherwise, created from a team's surface: that team. Otherwise: the project default team (BR-MX-11), or «Без команды» when the project has none. In a creation dialog the creator may change it, before saving, to any linked team or to «Без команды»; a row's or a checklist's add has no «Команда» field."
- US-4, the child scenario: append "And a story added to PA-FEAT-6 while MARS is being unlinked from A takes PA-FEAT-6's team as stored when the story is saved, and is moved with it".
Fix: edit
Source: L2-6
Status: open
Resolution:

### R-27
Severity: minor
Affects: BR-MX-18; spec § Existing rules (the two mod-006 order rows); US-7 "An item that changes team…"
Problem: The fate table keeps mod-006 BR-M4-13 ("listed by identifier number") per view, while BR-MX-18 replaces that order with creation time. The BR-M4-24 row claims "inside one project that is the same order", which concurrent creations can break. "Then by identifier" reads as a text compare (PA-10 before PA-9), against ADR-0028 § 9's project key then number. mod-006's "return to a former place" ends with the owner change, and no row records that.
Required resolution:
- Fate table: remove "-13" from "mod-006 BR-M4-2, -3, -18, -23; BR-M4-6, -8, -13, -25; doc-001 BR-12". Then "mod-006 BR-M4-24; doc-001 BR-12's tie-break; feat-027 BR-T-6" → "mod-006 BR-M4-13, -24; doc-001 BR-12's tie-break; feat-027 BR-T-6", with its Why "…; inside one project that is the same order" → "…; mod-006 § Model's return to a former place ends too, because a team place ends with the owning team (ADR-0028 rule 9)".
- BR-MX-18: "then by identifier" → "then by project key and number".
- US-7 "An item that changes team…": append to its last Then "; set back to MARS, S stands unplaced on MARS's backlog".
Fix: edit
Source: L4-12, L5-8
Status: open
Resolution:

### R-28
Severity: minor
Affects: BR-MX-19; ADR-0028 § 11; OQ-2
Problem: Under OQ-2 (A), no line can make processes differ, so BR-MX-19's category-column rule can be neither built nor tested. It also disagrees with ADR-0028 § 11 on where columns come from: the items a view shows, or the projects it can show, regardless of filter.
Required resolution:
- BR-MX-19 → "**Statuses on shared views.** A view's columns are those the organization's process gives the level, whatever items it shows (mod-002 BR-M1-3, OQ-2). A move offers only the states of the card's own type in its own project's process. No status is ever rewritten to make columns match. The line that lets projects' processes differ owes the shared-view columns: state categories over the projects the view can show and the user may see, whatever the filter or page (ADR-0028 rule 11), with a drop target per state of the card's own type."
- ADR-0028 § 11: "BR-MX-19 then applies to the set of projects a view *can* show" → "The shared-view columns that BR-MX-19 leaves to that line then come from the set of projects a view *can* show and the user may see".
Fix: edit
Source: L4-13, L2-12
Status: open
Resolution:

### R-29
Severity: minor
Affects: spec § Business process (BPMN)
Problem: The cycle check follows "Create areas", where it cannot fire. The move, where it belongs, is not drawn, and neither are the depth and sibling-name refusals. Only the link step shows a permission gate, so a reader of the diagram would take item creation, sprint planning, granting and unlinking as open to any member.
Required resolution: Change node texts only (no new line):
- A1 → "Create or move areas: top level by the org admin, under a parent with «Управление» on it".
- A2 → "New parent is the area itself or a descendant, deeper than 10 levels, or the name taken among its siblings?"
- A2x → "Refused, tree unchanged".
- G1 → "«Управление» holders grant scoped roles and set each project's visibility and default team".
- W1 → "A user with the item code creates a work item in a project".
- SP1 → "A team planner plans a sprint with items of several projects".
- U1 → "A manager of both ends unlinks a team from a project".
Fix: edit
Source: L2-11, L3-9
Status: open
Resolution:

### R-30
Severity: minor
Affects: Glossary "Grant / participant"; BR-MX-26; § Operation completeness (Grant, Team membership); US-10 "A team's page spans its projects"
Problem: Brief § 9 lists area membership and project participation as relations of their own. The spec folds them into grants and the organization role without saying so: only the area half is defined, and BR-MX-26 still lists "a team, an area or a grant" as if area membership were separate. One label also covers different relations. A team's «Участники» holds both its members, who get nothing, and the grants on it, while a project calls the same thing «Участники и доступ». No story says how the section shows a member with no grant.
Required resolution:
- Glossary "Grant / participant": append "Brief § 9's area membership and project participation are these grants, or for a «Организация» project the organization role (BR-MX-23); a role-less member would hold nothing, so there is none. Team membership stays a relation of its own and grants nothing."
- BR-MX-26: "Adding a user to, or removing them from, a team, an area or a grant" → "Adding a user to, or removing them from, a team, or giving or removing a grant".
- § Operation completeness: the Grant row's Surface "the object's «Участники» («Участники и доступ» on a project)" → "the area's «Участники»; the «Участники и доступ» of a project or a team". The Team membership row's Surface "the team's «Участники»" → "the members list of the team's «Участники и доступ»".
- US-10: the team's sections "and «Участники»" → "and «Участники и доступ»". Append to that scenario's last line: ", and «Участники и доступ» lists the team's members, who get no role from it, apart from the grants on MARS with their roles".
Fix: edit
Source: L1-7, L7-3
Status: open
Resolution:

### R-31
Severity: minor
Affects: BR-MX-27, BR-MX-28; ADR-0028 § 7 (*The switcher*, *Breadcrumbs*); US-10
Problem: Area names are unique only among siblings, yet the switcher, the multiselects and the trail show an area by its bare name, so «Логистика / Склад» and «Финансы / Склад» look identical. ADR-0028 says that an area's trail "may" show its ancestors, while US-10 fixes one without them. Several US-10 criteria have no checkable value:
- the «Обзор» counts;
- an area «Участники» that the Background leaves empty;
- the switcher's group order;
- where "remembered per user" lives;
- the order of the 50-row pages;
- whether D's «Общие» offers the area link.

US-10's area «Проекты» (direct links) and US-8's overview list (the subtree) are two sets that the data cannot tell apart.
Required resolution:
- BR-MX-27: append "Outside the area tree an area is named by its path from the top level («Логистика / Склад»), since area names are unique only among siblings (BR-MX-4)."
- BR-MX-28: "The choice is remembered per user" → "The choice is remembered per user in the browser".
- ADR-0028 § 7:
  - *Breadcrumbs*: "An area's trail may show its ancestor areas" → "An area's trail shows its ancestor areas".
  - *The switcher*: "with the user's own teams first" → "the organization alone first, then the user's own teams, then the areas, the projects and the other teams, each group by name".
- US-10:
  - «Ромашка / Области / Склад / Обзор» → «Ромашка / Области / Логистика / Склад / Обзор».
  - "A's areas «Логистика» and «Склад»" → "A's areas «Логистика» and «Логистика / Склад»".
  - Switcher: "with VENUS before every other object" → "«Ромашка» alone first, then her own teams (VENUS), then the areas, the projects and the other teams, each group by name, an area with its path".
  - Background: "and «Склад» to A" → "and «Склад» to A and C". Add "And Irina holds «Чтение» on «Логистика»".
  - The area's page: "«Проекты» lists A and B" → "«Проекты» lists A and B, its own links, and not C, which «Склад»'s «Проекты» lists and «Логистика»'s «Обзор» counts (US-8)". Add "and «Участники» lists Irina, direct, and «Склад»'s lists her, inherited".
  - «Обзор»: the counts stated as numbers, recounted from the Background.
  - The cross-project list's pages: "newest first, then by identifier".
  - D's «Общие»: "and the command to link an area only for a holder of «Управление» on D".
  - Keep the file at or under 150 lines.
Fix: edit
Source: L5-11, L7-5, L5-6
Status: open
Resolution:

### R-32
Severity: minor
Affects: spec § Stories; BR-MX-21, BR-MX-24; the not-found and 403 copy in US-1..US-10; docs/error-codes.md
Problem: The registry gives «Проект не найден.», «Команда не найдена.» and «Область не найдена.» with a full stop, and US-4 quotes them that way. The BRs and the other stories omit the full stop, so the same deleted team answers two strings. US-6's "same code and body" check needs one exact string. A refusal is written sometimes as a bare 403 and sometimes as "403 «Недостаточно прав»".
Required resolution: spec.md § Stories, at the end of the paragraph after the table, append: "A quoted «… не найден(а)» or a 403 in a story is the docs/error-codes.md entry with that text (organizations.project.not_found, organizations.team.not_found, organizations.area.not_found, organizations.access.forbidden «Недостаточно прав»), its full stop included."
Fix: edit
Source: L7-6
Status: open
Resolution:

### R-33
Severity: minor
Affects: § Out of scope (blockers; the last bullet); NFR Regression; brief §§ 2, 7.1, 11.2, 16, 18.6
Problem: Three brief items are disposed of without their reason or owner.
- The blockers cut is true and is settled by doc-002 § Out of scope and doc-010 D-18 (other link types and per-type fields are release 2), but "the line that adds one" names no line.
- "Notifications" is out of scope with no reason, and automations are not mentioned. None exists in `src/backend`.
- NFR Regression's "every test listed … stays green" literally takes in E2E specs that `verify.sh` never runs. Nothing traces brief § 16's UI/E2E list and § 18.6's final run to the pipeline's mechanism: per-task E2E question, stories' owed-by re-runs, `verify.sh` at every merge.
Required resolution:
- § Out of scope, blockers: "…no queued line adds one; the line that adds one owes the filter and the figure." → "…other link types and per-type fields are release 2, owned by doc-011, the release-2 scope analysis (doc-002 § Out of scope, doc-010 D-18), and the line it names owes the filter and the figure."
- § Out of scope, last bullet: "; notifications." → "; notifications and automations: none exist in `src/backend`, so none depends on the hierarchy (brief § 2)."
- NFR Regression: append "E2E specs, outside `scripts/verify.sh`, are not part of "green": each line answers the E2E question per task (routing.md) and names in its MR the specs its address changes break, and the SC-01..SC-10 clauses run through their stories' owed-by re-runs (brief §§ 16, 18.6)."
Fix: edit
Source: L1-6, L1-11, L1-4
Status: open
Resolution:

### R-34
Severity: minor
Affects: BR-MX-14; ADR-0028 § 6 *Rule 2*; US-9; brief § 8.5
Problem: Brief § 8.5 says changes must not lose the history of completed sprints. Deleting a team strips its items' sprint pointers, Closed sprints included. ADR-0028 *Rule 2* then makes the team's sprints and outcome records "unreachable with the team", with no restore. The spec states this nowhere, so a product exception to § 8.5 sits only in an ADR storage clause.
Required resolution, depending on the human's answer:
- (A) BR-MX-14: append "A deleted team's sprints and outcome records are kept and read by no surface; an item's history keeps its sprint acts once feat-015 ships."
- (B) BR-MX-14: append "A deleted team's Closed sprints and their outcomes stay listed, read-only, on the «Спринты» of the projects whose items they record." US-12 gains a scenario, and ADR-0028 *Rule 2*'s "become unreachable with the team" changes to match.
Fix: human
Decision: the human's, asked before the fixes
Source: L1-9
Status: open
Resolution:
