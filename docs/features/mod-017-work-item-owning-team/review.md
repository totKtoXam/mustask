# Review: mod-017-work-item-owning-team

## Round 1 — requirements

Reviewer: requirements-reviewer, clean context, 2026-10-17. Input: `spec.md`, `stories/US-1..US-7`, against doc-012 (BR-MX-9..-12, -17, -18; US-4; US-7's owned-items, change-of-team and team-filter scenarios; US-2's default team), ADR-0028 (rules 2, 5, 8, 9, 12) and the code on this branch.

Verdict: **changes-requested**: three majors (R-1..R-3) go back to the analyst. Seven minors (R-4..R-10) are fixed in place in the spec and the stories.

### Rule → story mapping

| Rule | Covered by |
|---|---|
| BR-OT-1 owning team; BR-MX-9, -25, -26 | US-1 |
| BR-OT-2 setting it; BR-OT-16 tenant | US-1 (save), US-2 (create), US-3 (default naming), US-5 (filter value) |
| BR-OT-3 creation defaults; BR-MX-10 | US-2 |
| BR-OT-4 project default team; BR-MX-11 | US-3, US-2 |
| BR-OT-5, -6 view contents, places; BR-MX-17, -18; ADR-0028 rule 9 | US-4 |
| BR-OT-7 shown wherever the item is | US-1, US-4, US-5 |
| BR-OT-8, -9 filter, list | US-5 |
| BR-OT-10 areas own nothing | US-6, US-1 |
| BR-OT-11 no first team, last-team guard | US-6, US-7 |
| BR-OT-12 deletion; BR-MX-12 (one project) | US-7, US-3 |
| BR-OT-13 races | US-2, US-3, US-7 (gaps: R-2, R-3) |
| BR-OT-14 existing data | US-6 (gap: R-1) |
| BR-OT-15 access | every story's role scenarios |
| doc-012 US-4 unlink and sprint scenarios; US-7 «Проект» on rows | owed: feat-028, mod-007; feat-040 (OQ-3) |

Size: one L and one MR, as doc-012 § Decomposition fixes for a breaking line; no split was missed.

Checked and sound: 26 sampled fate-table test names exist. The codes relied on are registered. `organizations.team.write` is the administrator's alone. The list's columns match `WorkItemsPage.tsx`. The replay's 200 before the team check is mod-003's. The kit's `FilterBar` exists.

### R-1
Severity: major
Affects: BR-OT-14, US-6 (the upgrade), OQ-2
Problem: "Team places stay; … no row is dropped" cannot hold together with the key the line must create. Today `backlog_places` is keyed by the work item alone (`BacklogPlaceConfiguration`), and a place can name a team that no longer owns the item. The `BacklogPlace` summary says such a place "is simply not read", and `BacklogMembership` reads a place only where `place.TeamId == teamId`. That row is what `Get_ItemBackWithNoReorderMeanwhile_ReturnsToItsPlace` relies on. ADR-0028 rule 5 holds "a team's place names the item's current owning team" with a key (item, team) → item (id, owning team), and the key cannot be added over those rows. If they were kept without the key, BR-OT-6's "no memory of a former place" would break: an item given back to its old team would come back to its old place. The rows have to be dropped, and doc-012 § Existing data requires the MR to say what it drops.
Required resolution: BR-OT-14 and OQ-2 (A) should say: a team place that names the item's mapped owner stays, and every other team place is deleted (it was already unread), with the count logged and named in the MR as dropped data. US-6's upgrade scenario gains a case: an item placed on VENUS's backlog, then moved to MARS by an area edit with no reorder since, has no team place after the upgrade and still lists unplaced on MARS's backlog.
Status: resolved
Resolution: BR-OT-14 now says that a team place naming the item's mapped owner stays and every other team place is deleted, gives the reason (ADR-0028 rule 5's key, and BR-OT-6's "no memory of a former place"), names the stale places and the three markers as the data dropped in the MR, and has the migration log the count of stale places beside the items mapped and the markers dropped. "No row is dropped" is gone. OQ-2 (A) is reworded the same way (still decided A for the human), and the NFR "Existing data" bullet and the operation-completeness row for team places follow. US-6: the upgrade scenario now keeps only the places that named the item's owner, and a new scenario takes ALP-US-22, placed second on VENUS's backlog, moved to MARS by an area edit with no reorder since: after the upgrade it has no team place, lists unplaced on MARS's backlog, VENUS's other places are as they were, and set to VENUS later it stands unplaced, not second.

### R-2
Severity: major
Affects: BR-OT-13, US-2 "The parent's team is read when the child is saved", US-7 race outline row 3
Problem: The documents contradict each other on a child added while its parent's team is being deleted. US-2 says the child takes the project default team when the deletion wins, and BR-MX-10 ("its parent's owning team as stored when the child is saved") supports that. BR-OT-13 ("a creation (a child's included) … that meets the deletion of the team it names … comes after and is refused «Команда не найдена.»") and US-7's row "U adds a child beneath a story that MARS owns" say it may be refused. A child's creation names no team, so the refusal has nothing to refer to. QA cannot pass both.
Required resolution: BR-OT-13 and US-7's row should say that a child's creation is never refused for a team. If it commits first, the deletion reassigns it with the rest. If it comes after, it takes the parent's team as stored then (none), so the project default as it stands after the deletion, which has cleared a setting that named the team. Keep US-2's clause and remove "(a child's included)" from the refusal branch.
Status: resolved
Resolution: BR-OT-13 is split into four numbered cases. "(a child's included)" is gone from the refusal branch, now "a creation that sends a team"; case (2) says a child's creation sends no team and is never refused for one: if it commits first the deletion reassigns it with the rest, if it comes after it takes the parent's team as stored then (none, the parent having been reassigned), so the project default as it stands after the deletion, which has cleared a setting that named the team. BR-OT-3 states that a request naming a parent takes the parent's rule and a team it also carries is not read, so BR-OT-2's refusals never apply to a child (this makes "never refused" total for a direct request). The BPMN sends the child path straight to the store. US-2 keeps its clause and gains "A child is never refused for a team, whichever way the deletion of its parent's team falls" (A's setting names VENUS, so the two orders end differently: «Без команды» or VENUS). US-7's outline loses its "U adds a child" row, and a scenario of its own gives that row the not-refused outcome; the outline's Then now reads "takes it with the rest (an item is reassigned, a setting cleared)" so the default naming fits it too.

### R-3
Severity: major
Affects: BR-OT-13, BR-OT-6, US-4, NFR Concurrency
Problem: The race between a save that moves an item off a team and a reorder of that team's backlog is not specified. Today's reorder (`BacklogRepository`) locks `teams` `FOR SHARE` and `backlog_orders` `FOR UPDATE` and locks no item, while a save locks the item. Suppose a save moves ALP-US-12 from MARS to VENUS (and so drops its MARS place) and commits while a MARS reorder that read ALP-US-12 as a member is still writing its positions. The reorder then writes a place (ALP-US-12, MARS). Under R-1's key that is a 23503 violation, a server error unless mapped; without the key it leaves a place for a team that does not own the item. BR-OT-13 covers only the deletion races, and US-4 covers only the sequential stale reorder.
Required resolution: Add to BR-OT-13, with a US-4 scenario tested in both orders: a reorder of a team's backlog and a save that changes the owning team of an item it moves or names end in one order. If the reorder is first, the save drops the place it wrote. If the save is first, the reorder is refused with `boards.backlog.reorder_stale` and changes nothing. Neither ends in a key violation or a server error. The plan then puts the reorder's membership check under a lock that the save also takes (ADR-0028 rule 8's order: items (5) before order rows (6)).
Status: resolved
Resolution: BR-OT-13 case (3) states the race: a reorder of a team's backlog and a save that changes the owning team of an item the reorder reads as a member end in one order. The reorder writes a place for every member of the backlog, not only the item it moves, so the case covers any member, which is wider than "moves or names" and is the case of the reviewer's own example. Reorder first: the save drops the place the reorder wrote. Save first: the reorder, reading the members again under its lock, is refused with `boards.backlog.reorder_stale` and changes nothing when the item it moves or the one it follows has left the backlog (BR-M4-26, as today), and otherwise completes on the members that remain and writes no place for the one that left (today's handler already proceeds on the re-read members; refusing it would turn a harmless race into a retry). In neither order a key violation or a server error. BR-OT-6 gains the invariant; the error table's use of `reorder_stale` and the NFR Concurrency bullet follow, the bullet requiring the plan to put the reorder's membership check under a lock the save also takes, items before order rows (rule 8 steps 5 and 6). US-4 gains a scenario outline with the four combinations (reorder or save first; the moved item or another member), each with its result.

### R-4
Severity: minor
Affects: BR-OT-2, US-1
Problem: Three edges. (1) US-1's "for a deactivated project A it answers «Проект не найден.»" sits right after V's request, but V gets 403 there today: mod-016 § 0.1 puts the 403 before the nested 404, and BR-MX-24 is owed by feat-041. (2) BR-OT-2 did not say how a team of a deactivated project answers. Answering «не участвует» would reveal a live team, which ADR-0026 rule 5 forbids. (3) In "an id that … is no id", a JSON non-string is a 400 today (`Set_OwnerTeamIdIsANumber_Returns400BadRequest`), not «Команда не найдена.».
Required resolution: Name the sender; answer a deactivated project's team as not found; say "a string that is not an id".
Status: resolved
Resolution: BR-OT-2 now reads "another active project", and a deactivated project's team answers «Команда не найдена.». US-1 gains the example row "JUPITER, after B is deactivated", and its answer-order scenario cites mod-016 § 0.1 and has U send the deactivated-project save.

### R-5
Severity: minor
Affects: BR-OT-3, US-2
Problem: What a creation that leaves the field out gets was unstated, and so was which value a dialog submits when the default changes while it is open. The retired `Create_DefaultChangedSinceThePageWasRead_TakesTheCurrentDefault` encoded the opposite of the new dialog's behaviour.
Required resolution: State both.
Status: resolved
Resolution: BR-OT-3: a creation that sends the field gets it; one that leaves it out gets the rule's value as stored when it is saved. The dialog sends what it shows, and a row's add and «Добавить задачу» never send it. US-2 gains "The dialog creates with the team it shows".

### R-6
Severity: minor
Affects: BR-OT-14, US-6
Problem: The upgrade did not say whether «Изменён» moves, or that no project gets a named default. The second is a visible change on the stand: in a project of several teams a new item from the list now starts «Без команды», where it landed on the first team before. US-6's "an item in an area owned by no team below the root" also matched «Склад / Полки», which has no owner of its own and resolves to VENUS.
Required resolution: State both, and word the fallback case precisely.
Status: resolved
Resolution: BR-OT-14 and US-6 now say that no «Изменён» moves and no default is named, and the MR says so. The US-6 case is now an area that, like every area up to the root, has no owner.

### R-7
Severity: minor
Affects: § Existing rules (P-1)
Problem: Tests that encode retired rules were missing from the table: `OrganizationsSchemaTests`' first-team, default-area, root-owner and owner-key cases; `MoveAreaHandlerTests.Handle_ReResolvesInheritedOwners_KeepsExplicitOnes`; `RenameArea{Handler,Endpoint}Tests` "…KeepsChildrenAndOwner"; `RestoreProjectHandlerTests.Handle_DeactivatedProject_KeepsTeamStatusesOwnersAndDefaults`; `TeamsPage.test.tsx` «adds «Ядро» … and reads the tree again»; `ProjectListPage.create.test.tsx` «shows both helpers…»; `ProjectBacklogPage.test.tsx`'s empty-level case (feat-039 OQ-2); and `WorkItemsPage.test.tsx`'s two column cases. The blanket rule also missed arrangements that write `owner_team_id`, `default_of_team_id` or `is_first_team` directly, and fixtures' `isFirstTeam` and `defaultArea`.
Required resolution: Name them.
Status: resolved
Resolution: Added to the rows of mod-002 BR-M1-9, -11, US-6, -14 and -18 and of feat-039. A new row covers the list's table, and the blanket sentence is extended.

### R-8
Severity: minor
Affects: US-1, US-4, US-5
Problem: Some scenario data could not be tested as written. In US-4, "the last row of VENUS's backlog" is false when VENUS has unplaced items with a higher number, since the team tie-break is by number; doc-012 US-4 had "every item on VENUS's backlog is placed". In US-5, the reorder's Given left out ALP-US-30, which is on A's «Истории» backlog. "VENUS owns no item of A" contradicted the story's data. "A MARS story whose parent is VENUS's is a top-level row" is true on «Истории» whatever the filter (BR-T-5: a parent of a higher level). In US-1, "a checklist tick of its child" changes the child's state, not ALP-US-12's.
Required resolution: Make each one testable.
Status: resolved
Resolution: US-4 gains the placed-VENUS Given. In US-5, ALP-US-30 is added to the reorder, ALP-US-20 and ALP-TASK-15 are made MARS's for the empty case, and the tree case is now a MARS bug under a VENUS story. US-1's tick names ALP-TASK-13.

### R-9
Severity: minor
Affects: BR-OT-15, US-3
Problem: BR-OT-15 put every read under `workitems.workitem.read`, but the teams, the default mark and the picker's and filter's options come from the team read (`organizations.team.read`), and the deletion's count is a write-side read. US-3 did not say who is offered «Команда по умолчанию».
Required resolution: Name each code, and say which roles are offered the command.
Status: resolved
Resolution: BR-OT-15 now lists `organizations.team.read` for the teams and options and `organizations.team.write` for the count, and says that only an «Администратор» is offered «Команда по умолчанию» and «Удалить команду». US-3: U and V see the marks and get no command.

### R-10
Severity: minor
Affects: BR-OT-4, BR-OT-11, BR-OT-12, US-2, US-7, OQ-1
Problem: Five small inaccuracies. (1) US-2's hints «Встанет в конец бэклога команды…» and «Попадёт только в бэклог проекта.» are false for a «Задача», which is on no backlog level before feat-023. (2) mod-016 BR-M1-27 already uses "a project's default team" for the switcher's fallback, a different thing from BR-OT-4's. (3) BR-OT-11 dropped BR-M1-27's own-team-first, which US-7 tests. (4) BR-OT-12 did not say when the confirmation counts (doc-012 BR-MX-12: when it opens). (5) OQ-1 said feat-039 BR-PB-9 "assumes one" team, but BR-PB-9 handles a project with none, by hiding the team entries.
Required resolution: Correct each one.
Status: resolved
Resolution: (1) US-2's hints are limited to types of a backlog level. (2) BR-OT-4 now names BR-M1-27 as another rule. (3) BR-OT-11 restates the fallback: own team first in name order, else the first. (4) BR-OT-12 and US-7: the count is read when the dialog opens, and the operation covers what the team owns when it runs. (5) OQ-1's rationale is reworded.

### Open questions

OQ-1..OQ-9 remain decided under the night delegation, for the human. The reviewer contests none, but R-1 changes what OQ-2 (A) says about team places.
