# Requirements review — feat-039-project-board

## Round 1 — requirements

Reviewed `spec.md` and `stories/US-1..US-3` against doc-012 (§ Roles, § Decomposition row 1, BR-MX-17/-18, § NFR, US-7 project part), ADR-0028 rule 9, ADR-0029 § delivery lines, mod-006, feat-027, feat-029, feat-032, mod-016 api-contract § 0.1/0.2, and the code on the branch (`Boards/*` backend, `features/boards`, `app/navigation`, `organizations/context`).

Checked and holding, no finding: the answer order (mod-016 § 0.1 and `ReorderBacklogHandler`: 401, organization 404, 403, project/level 404, 400, 409); the error texts quoted against `docs/error-codes.md`; the team place is keyed by the item alone (`backlog_places`, `ON CONFLICT (work_item_id)`), so the project order needs storage of its own, which ADR-0028 rule 9 gives it and BR-PB-4 relies on; `work_items.created_at` exists for the creation-time tie-break; a reorder places every same-level member, nested ones included, on team and project backlogs alike; E2E 26 and the unit tests find the drop-down's entries by name, as the Regression NFR says; size M holds (new order storage, three routes, two pages generalised from the team ones, one head entry; no code, no error code, no realm change).

### Rule → story mapping

| Rule | Covered by |
|---|---|
| BR-PB-1 views and addresses | US-1 (missing address), US-3 (missing address) |
| BR-PB-2 contents | US-1 (every item, states), US-3 (board holds the backlog's items) |
| BR-PB-3 tree | US-1 (children, rows drawn as the team's) |
| BR-PB-4 own order | US-2 (independent orders, change of team, unplaced, stale, concurrency, deactivation), US-3 (column order) |
| BR-PB-5 codes | US-1/US-2/US-3 «Who …» scenarios, US-2 answer-order outline |
| BR-PB-6 no create command | US-1 (head, empty), US-3 (head, empty, checklist) |
| BR-PB-7 interim entry | US-1 (drop-down) — shell behaviour open, R-1 |
| BR-PB-8 additive | US-1 (team views unchanged) |
| BR-MX-1, -16, -19; BR-M4-2, -3, -15, -18, -28 | US-1, US-3 |
| BR-MX-17/-18 owed halves, -21..-24 | owed scenarios in US-1..US-3 (mod-017, feat-040, feat-041, mod-019) |
| feat-029, feat-032 | US-3 (card over the board, checklist) — R-3 |
| NFR volumes, query shape | US-1, US-2, US-3 large scenarios |
| NFR tenant isolation, observability | inherited: every new route declares an organization code and goes through mod-016's scoped check |

### R-1
Severity: major
Affects: BR-PB-7, BR-PB-8, US-1 (The team drop-down reaches the project's views), US-3
Problem: A project page's address carries the organization and the project but no team, and the spec says nothing about what the shell does there. In today's code (`organizations/context/resolveSelection.ts` `addressSync`, `app/navigation/visibility.ts` `entryMarker`, `app/navigation/switchTarget.ts`): (a) the switcher keeps the team chosen last, so it names a team while the page shows the whole project; (b) the «Бэклоги» and «Канбан» entries match only team addresses, so no navigation entry is current on a project page; (c) any choice in the switcher there, another project or another team, matches no entry and opens «Главная». BR-PB-8's "no other existing test changes" also rules out the navigation-model change a reasonable answer needs. Left as it is, the design either guesses, or ships a page where switching projects drops the user on «Главная».
Required resolution: BR-PB-7 states (a) which navigation entry is current on a project page, (b) what the switcher shows at its team level there, (c) where a choice of another project, and of another team, in the switcher leads from a project page. BR-PB-8 is reworded to admit the navigation tests that answer changes. US-1 (and US-3 for «Канбан») gains a scenario for it. Recommended: the page's own entry («Бэклоги» / «Канбан») is current; the switcher shows the project and its last team as today; another project opens the same project view of that project at the same level; another team opens that team's page at the same level, as the drop-down does.
Status: resolved
Resolution: new BR-PB-9 states the shell's treatment, BR-PB-7 points to it. (a) «Бэклоги» is current on the project's backlog at each level and «Канбан» on its board, `aria-current="page"` with no trail, as on team pages; the entry keeps its team landing. (b) The switcher's team level is left as on any project-level page: the team last chosen in the project (so «Весь проект» from MARS's page still names MARS), else the default. (c) Another project opens the same project view at the same level; another organization the same view of its first project («Главная» with none); another team moves nothing. BR-PB-8 is reworded: the navigation tests gain project-address cases, and no existing case has such an address, so none changes. Two departures from the recommended text, both for the human as OQ-6 (decided: A (recommended) under the night delegation): (1) a team choice moves nothing, because mod-015 BR-18 says a level below the address's does not change the page, and in the code `select` returns before moving, so a team choice never reached «Главная», only a project or organization choice did; the recommended "opens that team's page" is OQ-6 B and would amend BR-18; (2) the project and organization moves keep the whole-project view and the level, an exception to BR-18's "landing of the same entry" that team pages do not share (mod-006 US-1 stays). US-1 gains three scenarios (the current entry and the switcher, a choice in the switcher, no team at the switcher) and US-3 one for «Канбан»; Rules relied on, Owed by (`feat-043-area-project-team-navigation`; "no team in the project" by `feat-028-org-level-teams`), the Regression NFR and the decomposition row (size M holds: a current-entry rule and one branch in the switch rule, no request, no entry) name it, and the note for the orchestrator adds OQ-6 for `feat-040-team-surfaces-across-projects` and `feat-043-area-project-team-navigation`.

### R-2
Severity: minor
Affects: US-1 (A large project backlog), NFR client load, OQ-4
Problem: The scenario had 5 000 stories, all of them top-level rows, and asserted that "the first state draws at most 2 000 rows". That cannot pass: BR-T-7, and `firstExpansion` in `lib/tree.ts`, always draw every top-level row and expand nothing when those rows alone pass 2 000. The criterion contradicted the spec's own OQ-4 (A).
Required resolution: assert BR-T-7's actual first state and leave the bound to feat-040.
Status: resolved
Resolution: the scenario now asserts every top-level row drawn and none expanded, with the bound owed by `feat-040-team-surfaces-across-projects` (OQ-4).

### R-3
Severity: minor
Affects: BR-PB-3, BR-PB-6, § Rules relied on, § Operation completeness, US-3
Problem: The team board carries feat-032's card checklists. Their counters and lists, a tick that writes a child's state, and «Добавить задачу», which creates a task under the card, all live on it. Both pages also open the item's card over themselves (feat-029). The spec named neither. BR-PB-6 bans create commands on project views and exempts only "a row's own add-child". A design that reads it literally drops «Добавить задачу» from the project board; one that keeps it does so untested.
Required resolution: say what the checklist and the card dialog do on the project views.
Status: resolved
Resolution: BR-PB-6 keeps «Добавить задачу» beside the row's add-child (a named parent, the parent's team) and the checklist as the team board's (feat-032 BR-C-1..-12). BR-PB-3 names feat-029's card over the page. Two rows join § Rules relied on, and the completeness table counts the tick and the add. US-3 gains "A card's checklist is the team board's".

### R-4
Severity: minor
Affects: § Roles, US-1 (Who reads), US-2 (Who may reorder), US-3 (Who moves cards)
Problem: § Roles had no column for the row actions OQ-5 keeps (add a child, drop onto a row, «Изменить родителя…», «Убрать родителя») or for a checklist tick, although BR-PB-5 names their codes. US-2 said a viewer's «Ещё действия» "offers none of" the moves, which presumes the menu exists. Today a reader's team backlog row has no «+», no «Ещё действия» and no grip (mod-016 design § 6.2, `BacklogPage.gating.test.tsx`), and a reader's checklist controls are disabled (`CardChecklists.tsx`). A test written from the old wording would demand a menu a reader must not get.
Required resolution: complete § Roles and assert the reader's controls as the team pages draw them.
Status: resolved
Resolution: § Roles gains the column and a paragraph on what the panel draws per role; the three scenarios assert today's reader controls.

### R-5
Severity: minor
Affects: BR-PB-2, § Operation completeness (place: delete)
Problem: The spec did not say whether an item can leave its project's view of a level. That decides whether the project order needs any way to drop a place other than a reorder. An item cannot change project (BR-MX-9) or type, and so level (mod-003 BR-M3-1); only the Removed category takes it off (mod-005).
Required resolution: state it.
Status: resolved
Resolution: BR-PB-2 states it.

### R-6
Severity: minor
Affects: BR-PB-3
Problem: The spec said children stand "by level, then in the project's order of that level", but tasks have no project order: the «Задачи» level gets no project view before feat-023. Their order beneath a story was undefined.
Required resolution: name the order.
Status: resolved
Resolution: tasks stand by number, as feat-027 BR-T-6 orders them on team backlogs, until `feat-023-tasks-backlog`.

### R-7
Severity: minor
Affects: NFR Volumes and speed
Problem: "One level with up to 5 000 items within 2 s" left out the tree. The backlog answer carries every descendant at full depth (feat-027 BR-T-17), and feat-027's budget is 5 000 items on each of four linked levels, tree included. doc-012's accepted risk above 5 000 items was also missing.
Required resolution: restate the tree read's volume and the risk.
Status: resolved
Resolution: the NFR states feat-027's volume for the project tree read, the board's 5 000, and doc-012's risk above 5 000 items.

### R-8
Severity: minor
Affects: § Business process (BPMN)
Problem: The flow skipped the 400 for a malformed position. US-2's answer-order outline asserts that 400 between the not-founds and the stale check.
Required resolution: add the step.
Status: resolved
Resolution: a position node with its 400 branch sits between the not-found and the stale checks.

### R-9
Severity: minor
Affects: US-1 (The team drop-down reaches the project's views)
Problem: "lists MARS (selected), VENUS and «Весь проект»" fixed an order, but BR-PB-7 leaves the entry's place to the design. A test written from it would pin an order nobody has chosen.
Required resolution: assert only what BR-PB-7 fixes.
Status: resolved
Resolution: the scenario asserts that the teams keep their order and that the entry is present where the design puts it.

### R-10
Severity: minor
Affects: § Decomposition (note for the orchestrator)
Problem: OQ-4 moves the 2 000 bound for the project views into feat-040, just as OQ-3 moves the entry's retirement there. The note for feat-040's status log named only OQ-3.
Required resolution: name both.
Status: resolved
Resolution: the note names OQ-3 and OQ-4.

### Verdict

changes-requested — R-1 (major) is open for the analyst; R-2..R-10 are fixed in place. OQ-1..OQ-5 stay as the analyst decided them under the night delegation. None is contradicted by the sources, and all five go to the human at gate-1. This is an M with UI, so `gate-1` stays `pending` for the package after stage 3.

## Round 1 — code (backend + frontend)

Stage 8, the one round of an M, over `git diff a653917f..HEAD` (189ba86a..c34ecc10, 81 files; the branch stacks on
doc-012's a653917f). Read: spec, US-1..US-3, design, api-contract, qa.md, status.md; doc-012 § Roles, § Decomposition row 1,
BR-MX-17/-18, § NFR; `docs/conventions/backend.md`, `frontend.md`, `git.md`. No stand, no suite run: QA's re-check 1
(`VERIFY_FULL=1` green on f8ea04ed; c34ecc10 changes qa.md only) stands, and the stamp cannot match while three paths are
untracked. Nothing modified but this file.

Verdict: **changes-requested** · blockers 0 · majors 1 · minors 3.

Checked and holding: containment (each handler compares `project.OrganizationId`, `GetProjectBacklogHandler.cs:34-36`; the
reorder's lock names the organization, `ProjectBacklogRepository.cs:20-24`; a body id outside the project's level is stale
through `StackRank.Move`, nothing learned); authorization with today's bundle (`BoardEndpoints.cs:67-91`, inventory 47); no
N+1 (fixed statements, pinned 9/8/11/12 at `ProjectBacklogStatementCountTests.cs:88-91`); the migration is additive,
reversible and drift-free (`AddProjectBacklogOrderMigrationTests`); the locks mirror the team's (project `FOR SHARE` against
the deactivation's `FOR NO KEY UPDATE`, `ProjectRepository.cs:29`; no cycle); the team order is untouched (`CreatedAt` is
null on team entries, `BacklogReadModel.cs:33,41,204`, so `StackRank.cs:81` changes nothing there); the 2 000-row cap is
BR-T-7's unchanged `firstExpansion`, and the board's bound is deferred to feat-040 (OQ-4, design § 7 "Size").

### CR-1 (major, docs: orchestrator; prose/git only) — the spec the branch is judged against is not on it; status.md is behind (QA D-3, escalated)

- `git status`: `spec.md` and `stories/US-1..US-3` are untracked (`review.md` was too, until this commit). The code cites
  BR-PB-n, US-n and OQ-n 131 times in 44 source files; design.md (`:3-4`) and the api-contract defer every rule to them;
  BR-PB-9 is the only requirement text for the exception to ADR-0024 § 2's switch rule that `switchTarget.ts:52-61` ships.
  An MR opened from this branch, or `finish-task.sh`, merges code whose rules exist nowhere in main. `status.md:6` still
  says `stage: 01-business-analysis`, and `api-contract.md:5-6` sends the reader to `status.md` § Plan for the plan, which
  `status.md:22` says "goes here after the design" and is not there. A pre-condition of gate-3, not a debt to queue: major.
- Fix (orchestrator): commit `spec.md` and `stories/US-1..US-3` by name; bring `status.md` up: stage, the developers' plan
  (or repoint the api-contract header), Log lines for the stages run, E2E = no (paused 2026-10-12), and the "decided … for
  the human" list (spec OQ-1..-6, design D-1..-5, QA D-2/D-4, this round's minors). A delta read, no QA.

### Minors (debts, listed at gate-3; not sent back)

- **m-1 (frontend + docs; answers QA D-2).** `BoardPage.tsx:123`, `:137-147` change a team board: a failed re-read now keeps
  the columns with the banner above (a653917f `BoardPage.tsx:113` replaced them). That contradicts BR-PB-8 ("the team
  backlogs and boards … stay"; "no other existing test changes" — `OrganizationAccessInventoryTests.cs:235` moved too, which
  api-contract § 5 does record). But it is what feat-032's merged design already promised, "a failed re-read keeps the board
  and its open lists, with the banner above" (`feat-032-board-card-checklists/design.md:218`), and the old code missed: keep
  the behaviour. Fix: cite feat-032 design § 7.1, not "feat-039 design § 7, state 9 … for both pages", in
  `BoardPage.tsx:42-45` and `BoardPage.reread.test.tsx:11-13`; give BR-PB-8 the clause while committing the spec (CR-1), the
  same pass; close QA D-2.
- **m-2 (docs, direction 2).** feat-027's merged prose describes the old menu:
  `feat-027-backlog-tree-editing/stories/US-1-backlog-layout.md:145` ("lists «Мастаск» alone") and its `design.md:367-370`
  ("one `MenuItem` … per team"); the unit test was updated, the prose not. Fix: a one-line "amended by feat-039 BR-PB-7" note
  at both, or leave them as merged history (tech-001 D-1's practice): the human's call.
- **m-3 (repository; with QA D-4).** `Mustask.AppHost.csproj:17-21`: the MessagePack `VersionOverride` is an incidental fix
  on a feature branch (`docs/conventions/git.md:25`), and QA D-1 says main fails the same NU1902, so every other branch stays
  red until this one merges. Fix: a queue line (fix NU1902 on main, the pin in `Directory.Packages.props`, dropped when Aspire
  resolves 2.5.303+), or accept it riding here and name it in the MR.

### Behavioural claims, direction 1 (one line a claim)

- spec BR-PB-1 (three levels, else «Страница не найдена»; a level the process lacks 404) → `ProjectBacklogPage.tsx:24`,
  `GetProjectBacklogHandler.cs:41-46`.
- BR-PB-2 (every item of the project, no team or area condition) → `ProjectBacklogMembership.cs:34-36`, `:57-59`.
- BR-PB-3 (tasks beneath a row by number) → `BacklogReadModel.cs:204`.
- BR-PB-4 (own order; unplaced by creation time, then number; a change of team keeps the place) → `StackRank.cs:81`, the
  place keyed by item (`ProjectBacklogPlaceConfiguration.cs`), RPB `Reorder_AnItemThatChangesTeam_KeepsItsProjectPlace`.
- BR-PB-4 "team views keep their number tie-break" → `BacklogReadModel.cs:33,41` (`ordersByCreation: false`).
- BR-PB-5 (codes) → `BoardEndpoints.cs:67-91`, `OrganizationAddressedRoutes.cs:86-88`.
- BR-PB-6 (no create command; add-child and «Добавить задачу» stay, naming no team) → `BacklogPage.tsx:97,139`,
  `BoardPage.tsx:101,124`, `BoardColumns.tsx:340`, `useChecklists.ts:323-328`.
- BR-PB-7 (the entry last, after a divider, selected on a project page) → `TeamHead.tsx:296-297`.
- BR-PB-8 (team views unchanged; no other existing test changes) → m-1.
- BR-PB-9 (a) current entry, no trail → `visibility.ts:139`; (c) a project or organization switch keeps page and level →
  `switchTarget.ts:52-61`; a team choice moves nothing → `useOrganizationsContextSource.ts:297-305` (`carriesLevel`).
- NFR query shape → CNT `:88-91`, 2 000 per level standing for 5 000 (structural: no per-row statement; the assignee
  batches are api-contract § 8's stated exception).
- NFR volumes (2 s, 1 s) → `ProjectBacklogVolumeTests` (four linked levels of 5 000).
- NFR concurrency (one at a time; team and project reorders fail neither) → `ProjectBacklogRepository.cs:19-36`; RPB gate
  tests.
- NFR tenant isolation → `GetProjectBacklogHandler.cs:34-36`, `ProjectBacklogRepository.cs:20`; inventory 47.
- NFR client load (BR-T-7; the 2 000 bound owed by feat-040) → unchanged `firstExpansion`, `ProjectBacklogPage.large.test.tsx`;
  the deferral is marked (spec OQ-4, design § 7 "Size").
- api-contract § 0.1 "the scoped check runs before the body is read" → `EndpointCategories.cs:33-38` (`RequireAuthorization`).
- api-contract § 3.2 step 1 (project `FOR SHARE`, deactivation `FOR NO KEY UPDATE`) → `ProjectBacklogRepository.cs:20-24`,
  `ProjectRepository.cs:29`; RPB `Reorder_ProjectDeactivatedWhileWaiting_Returns404ProjectNotFound`.
- api-contract § 6 (additive; `Down` drops the two tables) → migration `:79-86`; MIG.
- api-contract header "the plan is status.md § Plan" → CR-1.
- `queryKeys.ts:8` "a team's are read when they open" → `App.tsx:10-17` (default `staleTime` 0, refetch on mount).
- `BoardPage.tsx:42-45` "(feat-039 design § 7, state 9 … for both pages)" → m-1.
- qa.md "verify green" → not re-run here; QA's re-check 1.

### Behavioural claims, direction 2

- The team drop-down's entries (`Команды проекта`, `TeamMenu`): feat-027 design :367 and US-1 story :145 → m-2; e2e
  `26-backlog-tree-editing.spec.ts:627,878` find entries by name → unaffected.
- The switch rule on project addresses (`switchTarget`, BR-18): mod-015 `spec.md:131` and ADR-0024 § 2 (`:136-139`) state
  the landing rule; the exception is written only in BR-PB-9 → CR-1. ADR-0028 § 7 (accepted, `:164`) already keeps the
  section on a switch, so no ADR is owed.
- The team board's failed re-read (`ScopedBoard`): feat-032 design :218 agrees with the new code → m-1; mod-006
  api-contract :314 (level not-found as a banner) still true.
- The stack-rank tie-break (`StackRank.Order`): team entries carry no `CreatedAt`, so mod-006 BR-M4-13 still holds → none.
- The route count 44 → 47: mod-016 `api-contract.md:432` and `history.md:47` still say 44; merged artifacts, and the
  inventory test enforces the list → no finding.
- Not swept: doc-012's `audit/4-boards-backlogs.md` and `audit/6-frontend.md`, which describe the pre-doc-012 code.

## Round 2 (delta read)

Delta read, the one an M allows: `git diff cebe31b3..2261f41b` (27d3843a, 2261f41b; 7 files: `spec.md` and
`stories/US-1..US-3` added, qa.md D-2/D-3 status lines, the doc comment in `BoardPage.tsx`, the header comment in
`BoardPage.reread.test.tsx`), read against CR-1 and m-1 and the lines next to them. The only source change is in comments, so
nothing was run. The four new documents were put through verify.sh's prose checks by hand: their links resolve and none
carries a review section.

Verdict: **approve** · blockers 0 · majors 0 · minors 3 open (m-2, m-3, m-4).

- **CR-1: resolved on the branch.** `spec.md` and the three stories are committed by name (27d3843a), carrying BR-PB-9 and
  OQ-6 as round 1 read them. The code's BR-PB/US/OQ citations now land on tracked text. **The rest becomes m-4.**
- **m-4 (minor, docs: orchestrator; what is left of CR-1).** `status.md` has not changed. It still says
  `stage: 01-business-analysis`, has no Log lines for stages 2-8, and its § Plan holds no developers' plan, which
  `api-contract.md:5-6` points to. It also has no E2E = no decision and no "decided … for the human" list. This run does not
  allow a developer or the reviewer to edit the file. The major half of CR-1 is gone: code no longer merges without its rules.
  This half cannot slip through either. `finish-task.sh:135-147` refuses to merge unless status.md says `- stage: complete`
  and `gate-3: approved`. Each decided item is in its own artifact (spec OQ-1..-6, design D-1..-5, qa.md D-2/D-4).
  Fix (orchestrator, before gate-3 is presented): set the stage and add the Log lines. Add the plan, or repoint
  `api-contract.md:5-6` with a status note. Add E2E = no and the decided list (OQ-1..-6, D-1..-5, QA D-4, m-2..m-4).
- **m-1: closed.** `spec.md:67` (BR-PB-8) admits the one exception and cites feat-032 design § 7.1, which says it
  (`feat-032-board-card-checklists/design.md:218`). `US-1-project-backlog.md:97` asserts it. `BoardPage.tsx:44-46` and
  `BoardPage.reread.test.tsx:12-13` now cite § 7.1. qa.md D-2 is closed.
- **m-2, m-3:** unchanged. They remain debts for gate-3.

Claims, direction 1 (prose the delta adds):
- BR-PB-8's exception (a failed team-board re-read keeps the columns, the error above) → `BoardPage.tsx:137-147`,
  `BoardPage.reread.test.tsx:22`.
- BR-PB-8 "no other existing test changes" → `git diff --numstat --diff-filter=M a653917f..HEAD` on tests. It shows the
  route inventory (`OrganizationAccessInventoryTests.cs`, `OrganizationAddressedRoutes.cs`) and the drop-down list
  (`BacklogPage.head.test.tsx`), both named by the rule. Three backend files only gain cases (`BacklogRowViewTests`,
  `BacklogTreeTests`, `StackRankTests`: 0 deletions). → holds.
- US-1:97 → as BR-PB-8's exception, above.
- qa.md D-3 "half closed … open, the orchestrator's file" → m-4.
- `BoardPage.tsx:122-123` "(design § 7, states 6 and 9)" is unchanged and unqualified. It is true of both pages, now that
  the header names both sources. → no finding.

Claims, direction 2: the delta changes no behaviour (comments and documents only). → none swept, none owed.
