# Review — feat-040-team-surfaces-across-projects

## Round 1 — requirements

Requirements reviewer, clean context, 2026-10-18. This is the light route's one round (status.md § Plan). I did not write the
spec.

**Read:**
- `spec.md` and US-1..US-7 (`git diff origin/main...feat/040-team-surfaces-across-projects`).
- doc-012: `spec.md` (BR-MX-9..-28, the fate table, NFR, § Decomposition row 4 and its Breaks cell) and US-7.
- doc-013: BR-NE-1..-9, OQ-2, OQ-3, and feat-040's row.
- ADR-0028: §§ 6-9, 11, 13, and the delivery lines.
- ADR-0029: § 7 and the delivery lines.
- ADR-0024: § 2.
- feat-028: BR-OL-1..-14, OQ-2, design §§ 1, 4, 7, 10.
- mod-017: BR-OT-1..-16, OQ-3, OQ-8, OQ-9, design §§ 4, 5.2, 6.3.
- feat-039: BR-PB-1..-9, NFR, OQ-3, OQ-4, OQ-6.
- mod-006: § Model and BR-M4-24..-28.
- feat-027: BR-T-1, -6, -7, -10, -12, -16.
- mod-002: BR-M1-1 and BR-M1-24.
- mod-016: BR-M1-27, and inventory rows 35-37.
- mod-015: BR-13 and BR-18.
- mod-003: § The identifier and its number.
- tech-011: m2 and m3, in mod-006 `review.md`.

**Code read:** `contracts/errors/*.json` (every text the stories quote matches), `CreateWorkItemDialog.tsx`,
`BacklogOrder.cs`, `BacklogPlace*.cs`, `ContextSwitcher.test.tsx` and `projectViews.test.ts`. I searched for every test
name in § Existing rules, and all of them exist.

### Rule → story map

| Rule | Stories |
|---|---|
| BR-TS-1 addresses, the retired ones | US-1, US-7 (inventory) |
| BR-TS-2 what a team view holds | US-1 |
| BR-TS-3 «Проект» and «Команда» | US-1, US-4 |
| BR-TS-4 a card in its own project | US-4 |
| BR-TS-5 moves and row actions | US-4 |
| BR-TS-6 one team order, the tie-break | US-3 |
| BR-TS-7 reorder, races, tech-011 m2 | US-2 (filtered), US-3, US-7 |
| BR-TS-8 project filter | US-2, US-7 (a foreign value) |
| BR-TS-9 creating from a team view | US-5 |
| BR-TS-10 the switcher's interim team level | US-6 |
| BR-TS-11 page heads | US-6 |
| BR-TS-12 the way into project views | US-6 |
| BR-TS-13 codes, answers, tech-011 m3 | US-3, US-4, US-5, US-7 |
| BR-TS-14 data | US-3 (equal positions) |

**doc-012 US-7, the clauses owed by this line:**
- The views across a team's projects: US-1 and US-2.
- The card in its own project: US-4.
- A missing, foreign or unlinked team: US-1.
- MARS's stories of B on the project views: US-1 and US-2.
- The tie-break: US-3.
- A move offers only the states of the card's own type: US-4.
- Creating from a team view: US-5.
- Large views:
  - The team half is in US-7.
  - The project half is feat-039's `ProjectBacklogVolumeTests`, after R-10.
  - The 2 000 drawing bound is OQ-3.

**doc-012 § Decomposition, feat-040's row, every clause covered:**
- The team address.
- The project filter.
- «Проект» and «Команда» on rows and cards.
- The card in its own project.
- The tie-break.
- The head's drop-down.
- The interim team level, and an address naming a team, which sets the organization and the team.
- The mod-020 merge-order clause.
- tech-011 m2 (BR-TS-7) and m3 (BR-TS-13).
- feat-028's card-dialog note (BR-TS-4).
- Every item of the row's Breaks cell, which the spec's own Breaks list carries.

### R-1
Severity: major
Affects: BR-TS-10, US-6, § Owed by
Problem:
- BR-TS-10 says "A project change keeps the team". mod-002 BR-M1-24 says "a fallback changes the selection".
- So when the level holds a team only because of the fallback, that team becomes the selection and should survive a
  project change.
- US-6 «The fallback once both this line and mod-020 are merged» expects the opposite. Ann holds VENUS as Alpha's default,
  she chooses Delta, and the switcher then holds JUPITER.
- The two cannot both pass. The design and the context-source tests would each pick a different reading. The case
  matters as soon as mod-020 is in, because its fallback (the project's default team, the human's mod-017 OQ-8 B) then
  depends on the project.
- The § Owed by row also gave the fallback to mod-020 outright. BR-TS-10 says "whichever of this line and mod-020 merges
  second".
Required resolution:
- Say what "chosen" means, and make the rule and the scenario agree.
- Put the scenario on whichever line merges second.
Status: resolved
Resolution: fixed in place.
- BR-TS-10 now says how a team gets chosen: by a choice in the switcher or the head's menu, or by a team address. A team
  that the level holds only by its fallback is not chosen.
- "A project change keeps a chosen team and applies the fallback again otherwise." This is the analyst's own scenario,
  made a rule.
- US-6:
  - «A project change keeps a chosen team» starts from an explicit choice.
  - The mod-020 scenario says why JUPITER is right, and adds a chosen MARS that survives a project change.
  - The mod-020 scenario is marked as verified by whichever line merges second.
- The § Owed by row reads "whichever of `mod-020` and this line merges second".

### R-2
Severity: major
Affects: BR-TS-10, § Existing rules, US-6
Problem:
- The fate table marks feat-028 BR-OL-1..-10 kept. BR-OL-10's shell clause says that a team-less project "holds no team
  and the team-scoped entries are hidden". feat-039 BR-PB-9 has the same clause: "(once feat-028 merges) it has none, the
  switcher holds no team".
- BR-TS-10 and US-6 replace both clauses: Delta has no linked team, and Max's switcher still holds MARS with «Бэклоги»
  shown. Neither clause is in the table.
- The table also misses mod-017 BR-OT-11: it orders the head's drop-down and the switcher by name, and US-6 now puts the
  user's own teams first.
- The team level's own states are missing:
  - an organization with teams and no project;
  - the organization's teams unread or failed.
  Today's shell shows no team level at all without a project. `ContextSwitcher.test.tsx` holds «shows «Нет проектов» at
  the project level and no team level» and «a project with no teams shows «Нет команд»».
- These tests would fail with no rule named for retiring them, which is P-1's gap:
  - those two;
  - `projectViews.test.ts` «is hidden with no team at the switcher…»;
  - the two no-project and no-team cases of `useOrganizationsContextSource.teamLevel.test.tsx`.
Required resolution:
- List the replaced clauses and their tests.
- Give the team level a state for no project and a state for an unread or failed team list.
Status: resolved
Resolution: fixed in place.
- BR-TS-10:
  - The level is shown whether or not the organization has a project or the chosen project has a linked team.
  - It reads «Нет команд» only when the organization has no team.
  - While the organization's teams are unread or failed, it holds no team and the team-scoped entries are hidden. This
    restates feat-039 BR-PB-9's last clause over the organization's teams.
- The fate row adds:
  - BR-PB-9's no-team clause;
  - BR-OL-10's shell clause;
  - BR-OT-11's order (→ BR-TS-10, -11);
  - the five tests above, each by name.
- The kept row now reads "BR-OL-1..-9, BR-OL-10 (its shell clause replaced, its way in extended)".
- The Breaks list gains BR-OT-11's team order.
- US-6 gains «The team level is the organization's, whatever its projects»: an organization with a team and no project,
  and a failed read of the teams.

### R-3
Severity: minor
Affects: BR-TS-8, US-2
Problem:
- A filter value can now arrive from the address. BR-TS-8 says what such a value selects, but not what the control shows
  when the value names no option (an unlinked project, a deactivated one, a foreign id, a malformed id).
  - If the control shows "nothing chosen", the page claims "all projects" over an empty view.
  - US-2's "nothing in the page says which of these it was" cannot tell the two apart.
- Mixed values (Beta and an unknown id) are not covered.
- The empty view of a filter that matches nothing is not told apart from the empty state of a team that owns nothing.
- BR-TS-8 says only that a level change keeps the filter. It is silent on:
  - «Показать канбан» and «Показать бэклог» (feat-027 BR-T-16), which switch views at the same level;
  - the «Бэклоги» and «Канбан» entries.
Required resolution: state these four behaviours.
Status: resolved
Resolution: fixed in place.
- BR-TS-8:
  - An invalid value next to a valid one adds nothing.
  - The control shows that a filter is on, and offers to clear it, whenever the address carries a value. It names no
    project and no reason.
  - A filter that matches nothing shows mod-017 design § 6.3's filtered-empty frame, with «Сбросить фильтр».
  - «Показать канбан» and «Показать бэклог» keep the filter.
  - The entries open their landing without it.
- US-2's two value scenarios and its address scenario test each of these.

### R-4
Severity: minor
Affects: BR-TS-8, § Owed by, § Decomposition
Problem:
- Under OQ-2 A, this line puts the project filter in the page's address.
- mod-020 puts the team filter there. It is in flight on its own branch, with no spec yet.
- The spec says the two dimensions "differ" only until mod-020 merges. It does not say which line fixes the address form
  of a filter: the parameter, repeated values, an invalid value.
- doc-012 NFR Query shape requires one meaning on every surface. Without that rule, two lines built in parallel would
  each invent a form.
Required resolution: a merge-order rule, and a note to mod-020.
Status: resolved
Resolution: fixed in place.
- BR-TS-8: whichever of the two lines merges first founds the form, and the second follows it.
- § Owed by has a row for it.
- § Decomposition asks the orchestrator for a note in mod-020's status.md, covering both merge-order clauses: this one,
  and the fallback of R-1.

### R-5
Severity: minor
Affects: BR-TS-2, US-1
Problem:
- A team whose linked projects are all deactivated has no rule.
- ADR-0028 § 6 rule 3 takes a deactivated project out of its teams' link lists. By BR-TS-9, such a team offers no create
  command and no filter option.
- The spec does not say which empty state the team shows. JUPITER's state says the team works on no project. Any other
  wording would reveal the deactivation, against ADR-0026 rule 5.
Required resolution: name the state.
Status: resolved
Resolution: fixed in place.
- BR-TS-2: such a team reads as JUPITER does, and nothing says why.
- US-1 deactivates Alpha, VENUS's only project, and checks the result.

### R-6
Severity: minor
Affects: BR-TS-9, US-5
Problem:
The dialog has three states that no rule describes:
- «Команда» before any project is chosen. It offers "the chosen project's linked teams", and there is none yet.
- Where «Проект не найден.» lands at the save.
  - Today's dialog reads a project 404 as a lost page context and closes (`CreateWorkItemDialog`
    `onClose(contextGone)`).
  - On a team view the project is only a field, so closing would throw away the user's input. BR-TS-9 promises the
    opposite: "the dialog keeps its values".
- The dialog's line naming where the item lands. It names the project's root area (mod-017 BR-OT-10, until mod-018).
  That project is now chosen in the dialog.
Required resolution: state each.
Status: resolved
Resolution: fixed in place.
- BR-TS-9:
  - Until a project is chosen, «Команда» holds T. Whether it opens before then is the design's.
  - The landing line follows the chosen project.
  - On a team view, none of the save refusals closes the dialog:
    - «Проект не найден.» sits under «Проект»;
    - the team's answers sit under «Команда»;
    - that field's options are read again (mod-017 design § 4).
- US-5's refusal scenario checks where each answer lands and that the options are read again.

### R-7
Severity: minor
Affects: BR-TS-7, US-3
Problem:
- BR-TS-7 lists the races of a reorder with an unlink, a deletion and a team change.
- It does not list a reorder that meets the deactivation or the restore of one of T's projects.
  - The reorder's member set changes under it.
  - BR-TS-7 keeps the places of a deactivated project's items.
  - A moved or named item of that project then counts as off the backlog.
- Nothing says how the race ends.
Required resolution: name the outcome, with no server error.
Status: resolved
Resolution: fixed in place.
- BR-TS-7: the reorder works on the members it reads under its lock.
  - The project's items keep their places either way.
  - If its moved or named item has just left, the reorder is stale.
- US-3's race scenario gains the case.

### R-8
Severity: minor
Affects: US-3 «A deactivated project's places are kept by a reorder»
Problem:
- "GAM-US-1 is back … at its kept place" cannot be checked as an order.
- The reorder made during the deactivation rewrote every member's position (mod-006 § Model, `backlog_places.position`
  ≥ 1). So the position GAM-US-1 kept can now equal ALP-US-1's.
- The restored item stands where that position falls among the new positions. That is not necessarily the place the user
  saw before.
- The rule is ADR-0028's "come back exactly", which concerns the stored value. The scenario reads as a promise about the
  visible order.
Required resolution: assert what the rule guarantees.
Status: resolved
Resolution: fixed in place. The scenario now asserts three things:
- the stored position, unchanged by Max's move;
- the item's standing by that position among the positions the move wrote;
- a stable order, with ties sorting by BR-TS-6.

### R-9
Severity: minor
Affects: US-6 «A team address sets the organization and the team», «Another organization on a team page»
Problem:
- "its project level shows its current selection for «Ромашка»" cannot be verified. mod-002 BR-M1-24 remembers one
  selection, not one per organization. After an address from «Другая», the project level falls back to Ромашка's first
  project.
- "with no team chosen there" implies a memory per organization. No rule gives one.
Required resolution: name the value. Drop the implied memory.
Status: resolved
Resolution: fixed in place.
- The scenario reads "its project level shows Alpha, the level's first entry after the organization change".
- BR-TS-10 says the same, and says "one selection, with no per-organization or per-kind memory".
- The other scenario drops "with no team chosen there". Orion is still the fallback.

### R-10
Severity: minor
Affects: § Non-functional requirements, OQ-3
Problem: The NFRs had three gaps.
- **List sizes.**
  - The switcher's team level and the head's menu list every team of the organization: 200 at doc-012's volumes.
  - The project filter and the dialog's «Проект» list every linked project of the team: up to 200.
  - The spec named none of these, so neither the design nor a test would size them.
- **Client load under OQ-3 A.**
  - The spec did not say what a team view draws until the spun-out line merges.
  - That view is now the largest one: up to 50 projects' share, 5 000 items at a level.
  - Its render cost has never been measured (tech-011 t3).
- **doc-012 US-7's large-views clause.** It is owed by this line, and it covers a project's views too. US-7 here measures
  only the team's.
Required resolution: state the sizes, the interim drawing with its risk, and where the project half is held.
Status: resolved
Resolution: fixed in place.
- A new NFR, **List sizes**: 200 teams, 200 projects, each list one read and reached by keyboard. The search is the
  design's.
- **Client load:**
  - until the spun-out line, a team view draws as a project view does today (feat-039 NFR);
  - the render cost is an accepted risk under A.
- **Speed and volumes**: the project half is feat-039's `ProjectBacklogVolumeTests`, kept.

### R-11
Severity: minor
Affects: BR-TS-13
Problem:
- ADR-0029 § 7 (accepted) names team views among the surfaces that return rows of several projects. It says "every such
  read model takes the caller's access as a required argument".
- doc-013 applied this to feat-043's cross-project reads, so that mod-019 and feat-041 change no signature.
- This line builds the first team-view read model over several projects, and the spec said nothing about the argument.
  mod-019 would then have to change the read model's signature.
Required resolution: follow ADR-0029 § 7 now.
Status: resolved
Resolution: fixed in place.
- BR-TS-13: the team-view read model takes the caller as a required argument. Today that is the membership and role that
  ADR-0027 rule 3 resolves.
- mod-019 changes what the argument carries, and feat-041 adds the set form. Neither adds a signature.

### R-12
Severity: minor
Affects: BR-TS-13, § Error codes
Problem:
- "No new permission code, error code or text" can be read against the stories' new interface copy:
  - JUPITER's empty state;
  - the refused drop's notice;
  - «Проект» and «Выберите проект»;
  - the head's menu «Команды».
- Under that reading, the design would think it may add none of them.
Required resolution: narrow the sentence to error texts.
Status: resolved
Resolution: fixed in place.
- "No new … error text (`docs/error-codes.md`, `contracts/errors/*.json`)".
- The new interface copy is proposed in the stories and fixed by the design.

### Checked, no finding

- **doc-012, doc-013 and the ADRs.**
  - BR-MX-17, -18, -19, -27 and -28 hold, as interim by doc-013 OQ-3 A.
  - BR-TS-10's level is that answer's minimal one: no grouping, no per-kind memory, no vanish notice.
    - R-1's "chosen" is not a per-kind memory.
    - It retires with feat-043 (BR-NE-9).
  - ADR-0028:
    - § 7: four prefixes, no redirect, «Страница не найдена».
    - § 9: separate orders, and the tie-break by creation time, project key and number.
    - Rule 3: a deactivated project leaves team views and keeps its places.
    - The delivery line drops nothing.
  - ADR-0029: no new code. The reorder keeps `boards.backlog.reorder` until mod-019.
- **What feat-028 and mod-017 built.**
  - Storage:
    - The team order is already one per (team, level): `backlog_orders` (team, level).
    - So BR-TS-14 reads it as it is.
    - The equal positions come from feat-028 OQ-3 A.
  - mod-017:
    - BR-OT-6 and BR-OT-13 (3) extend across projects in BR-TS-7.
    - BR-OT-8's filter meaning is BR-TS-8's.
    - BR-OT-3's creation defaults are BR-TS-9's.
  - feat-028 BR-OL-8's reorder race matches US-3.
- **Every role reaches the project views under OQ-1 A.** feat-028 design § 10 shows «Бэклог проекта» and «Доска проекта»
  to every role on the project's «Команды».
- **The card by address.**
  - mod-003's resolution refuses a type key that does not match.
  - So US-4's ALP-BUG-3 rightly shows the not-found frame.
  - Keys are unique in the organization, deactivated projects included (mod-002 BR-M1-1).
- **Answers.**
  - Every quoted text matches `contracts/errors/*.json`.
  - The answer order is mod-016 § 0.1's: 403 before a nested 404, the team before the level.
  - A filter value never answers an error, as mod-017 api-contract § 3 already says.
- **The route inventory.** mod-016 rows 35-37 are exactly the three team-view routes: three out, three in.
- **Races.**
  - A reorder against an unlink, a deletion or a team change (US-3).
  - A creation against an unlink (US-5).
  - A read against a deletion (tech-011 m2).
  - A reorder against a deactivation (R-7).
- **The reorder budget.** US-7 asserts the reorder of 5 000 items within 1 s, the same work as today's team reorder.
  tech-011 t1 and t4 already track that timing and its flake in the parallel suite. This line adds no new risk there.
- **Size.** L in one MR, every story `shippable: no` (doc-012 § Decomposition: splitting a breaking line does not make it
  additive). The spun-out drawing bound is M, behind OQ-3.
- **E2E** is the standing NO (Decisions). The reference tracker is not named anywhere in the feature folder.

### Open questions

OQ-1..OQ-3 stand as the analyst wrote them, each with its options and a recommendation. They go to the human in the
gate-1 + gate-2 package, one question each, and the design continues on A. I added no question.

R-1's reading is the analyst's own US-6 scenario, made a rule: a team that the level holds only by its fallback is not
chosen. It carries the human's mod-017 OQ-8 B (the project's default team) onto the organization-wide level. The design
should show it. It becomes a question only if the human reads OQ-8 B otherwise.

### Verdict

`approved-by-ai`: R-1 and R-2 (major) and R-3..R-12 (minor) are fixed in place in `spec.md` and in US-1, US-2, US-3,
US-5 and US-6. None is left for the analyst. OQ-1..OQ-3 go to the human with the gate package.

## Round 1 — fix round (business analyst)

Business analyst, 2026-10-18, the light route's one fix round. The reviewer fixed every finding in place. I checked each
resolution against its sources and today's code, and every one stands: none is wrong, so none is reverted. Three of them
needed a tighter wording, and two gaps that the resolutions left are fixed below as A-1 and A-2.

**Sources re-read for the check:** mod-002 BR-M1-24 and design § 3.6; mod-015 BR-11 and BR-18; mod-016 BR-M1-27;
mod-017 BR-OT-11 and design §§ 4, 5.2, 6.3; feat-039 BR-PB-9; feat-028 BR-OL-10; ADR-0026 rule 5; ADR-0028 §§ 6, 7, 9
and rule 13; ADR-0029 § 7; doc-013 OQ-3; doc-012 BR-MX-28 and US-10.

**Code read:** the frontend tests of the switcher, its context source and the team and project routes
(`routes.boards.test.tsx`, `routes.projectBoards.test.tsx`, `routes.organizations.test.tsx`, `ContextSwitcher.test.tsx`,
`projectViews.test.ts`, `workContext.test.ts`, `resolveSelection.test.ts` and the three
`useOrganizationsContextSource*.test.tsx`).

### The resolutions, checked

| Finding | Check | Changed in this round |
|---|---|---|
| R-1 | Stands. "Chosen" holds only if a reload keeps it. The selection is remembered across a reload (mod-002 BR-M1-24), and without the flag a reload would make a fallback team the stored one, and so the kept one. That reopens R-1 once mod-020's fallback depends on the project. A project's own fallback (mod-002 design § 3.6: "the levels below take their first entries") is a project change too. | BR-TS-10: whether the held team was chosen is remembered with it, and a project change includes the project's own fallback. US-6: the mod-020 scenario gains a reload both ways; «A project change keeps a chosen team» gains Delta's deactivation. |
| R-2 | Stands, but its test list was short and three clauses were unnamed. | A-1. |
| R-3 | Stands. "Naming no project and no reason" read as covering a valid value too, against US-2's «Beta» chosen. | BR-TS-8: the clause is for a value that selects nothing. |
| R-4 | Stands. | - |
| R-5 | Stands. ADR-0028 § 6 rule 3 and ADR-0026 rule 5, with ADR-0028's amendment, say what it cites. | - |
| R-6 | Stands. mod-017 design § 4's refusal row puts both team answers under «Команда» and reads the teams again. | - |
| R-7 | Stands, with one wording fix. "Keep their places" read as untouched. But a reorder that read the project's items before the deactivation committed rewrites their places, as BR-TS-7's first sentence says. US-3 already read "keep a place". | BR-TS-7: they keep a place, the one they had or the one the reorder wrote. |
| R-8 | Stands. ADR-0028 § 9 keeps both kinds of place on a deactivation. | - |
| R-9 | Stands. mod-015 BR-11's reset gives Alpha, the first of Alpha, Beta, Delta, Gamma. | - |
| R-10 | Stands. ADR-0028 rule 13 sizes 200 projects and 200 teams. | - |
| R-11 | Stands. ADR-0029 § 7 says it of every read that returns rows of several projects. | - |
| R-12 | Stands. | - |

### A-1
Severity: major
Affects: § Existing rules (the BR-TS-10 row and the project-nested routes row), § Decomposition (Breaks)
Problem:
- R-2's list missed tests that encode a replaced rule. They would fail with no rule retiring them by name (P-1):
  - `routes.boards.test.tsx`, block «mod-015 BR-18 on a team-level page: a project or organization choice keeps the
    entry»:
    - its project-choice tests open a landing, where BR-TS-10 now says a project choice on a team page moves nothing;
    - its organization-choice tests land on "its first project's first team", where they now land on the level's first
      entry.
  - `routes.projectBoards.test.tsx` «a project whose teams list comes back empty: … «Бэклоги» and «Канбан» are not
    shown». This is exactly BR-PB-9's no-team clause, which R-2 replaced.
  - `workContext.test.ts` «a project brings its first team…» and «an organization without projects leaves the lower
    levels empty».
  - `useOrganizationsContextSource.test.tsx` «a project choice selects the list's first team, «Платформа»».
- BR-TS-10 also replaces three clauses that no fate row named:
  - mod-015 BR-11's "choosing a level resets the levels below it" and "… hides the levels below it", for the team
    level;
  - mod-002 design § 3.6's "the levels below take their first entries", for the team;
  - mod-015 BR-18's team-level page under its project's prefix (D-17), which BR-TS-1 retires.
Required resolution: name the clauses and the tests, and add the clauses to the Breaks list.
Status: resolved
Resolution: fixed in place.
- The BR-TS-10 row names the clauses and the tests above. Each rewrite gives its new intent. The two project-choice
  tests that lose their rule are retired.
- The project-nested routes row names BR-18's D-17 prefix.
- The Breaks list gains mod-015 BR-11 and BR-18.

### A-2
Severity: minor
Affects: BR-TS-10, § Existing rules (the kept row), § Out of scope, § Owed by, US-6 «With no team chosen, the level's
first entry stands»
Problem:
- The scenario said a deleted chosen team falls back "with no notice", and BR-TS-10 said "no vanish notice".
- Today's switcher announces a gone team: mod-002 design § 3.6's «Ранее выбранный контекст недоступен. Выбрано: …», held by
  `resolveSelection.test.ts` and `routes.organizations.test.tsx` «on «Главная», a remembered deleted team falls back
  with the notice».
- No fate row retired that notice, and the final switcher keeps one (ADR-0028 § 7, doc-012 US-10). The line would
  silently drop a notice that feat-043 then brings back.
- doc-013 OQ-3 A says the interim level has "no grouping, memory or fallback notice; those are feat-043's". BR-TS-10
  already reads its "memory" as "none of feat-043's" and keeps mod-002's remembered selection. The notice reads the same
  way.
Required resolution: keep today's notice and say so.
Status: resolved
Resolution: fixed in place.
- BR-TS-10: a gone team falls back with mod-002's notice, as today. What does not come with this line is feat-043's
  fallback to the organization alone.
- The kept row names the notice.
- § Out of scope and § Owed by give feat-043 its fallback to the organization alone, with its notice.
- US-6 expects the notice.
- This becomes a question only if the human reads doc-013 OQ-3 A as dropping today's notice.

### Open questions after the fix round

OQ-1..OQ-3 are unchanged, each with its options and a recommendation. They go to the human in the gate-1 + gate-2
package, and the design continues on A. This round adds no question.
