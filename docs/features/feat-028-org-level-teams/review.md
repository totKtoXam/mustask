# Review — feat-028-org-level-teams

## Requirements review, round 1

Requirements reviewer, clean context, 2026-10-17. The light route's one round (status.md § Plan). Read: `spec.md` and
US-1..US-8; doc-012 `spec.md` (Roles, Glossary, BR-MX-4..-6, -12, the fate table, Existing data, Decomposition, NFR), its
US-2 and the project–team part of its US-3; ADR-0028 (rules 3-9, Consequences); ADR-0029 (§§ 4, 6, 8, the delivery lines);
mod-017 `spec.md`, `api-contract.md` §§ 0, 2.5-2.11, 7, `plan-backend.md` §§ 11-12, `review-security.md` S-2,
`review-code.md` CR-7; feat-039 BR-PB-7..-9; mod-016 api-contract § 4.1. Code read: `OrganizationEndpoints.cs`,
`MembershipEndpoints.cs`, `Team.cs`, `TeamMembership.cs`, `TeamConfiguration.cs`, `OrganizationRoleBundles.cs`,
`contracts/errors/organizations.json`. Every test name in spec § Existing rules was searched for: all exist.

### Rule → story map

| Rule | Stories |
|---|---|
| BR-OL-1 tenant | US-8 (all), US-1, US-2, US-3 (foreign rows) |
| BR-OL-2 independent existence | US-1, US-2, US-3, US-5 |
| BR-OL-3 names, descriptions | US-1, US-2, US-6, US-8 |
| BR-OL-4 links | US-3, US-4 |
| BR-OL-5 who links | US-3, US-4, US-8 |
| BR-OL-6 linked means bindable | US-3, US-6 |
| BR-OL-7 default team | US-3, US-4, US-5, US-6 |
| BR-OL-8 unlink (SC-09) | US-4, US-6, US-7 |
| BR-OL-9 deletion | US-5 |
| BR-OL-10 team-less project | US-2, US-4, US-5 |
| BR-OL-11 team across projects | US-6 |
| BR-OL-12 pages | US-1, US-3 |
| BR-OL-13 upgrade | US-7 |
| BR-OL-14 answers | US-3, US-6, US-8 |

doc-012: BR-MX-4 → BR-OL-3, BR-MX-5 → -4, BR-MX-6 → -5, BR-MX-12 → -8, -9. Every scenario of doc-012 US-2 maps to a story
(the deactivated project's setting after R-8; "Ann clears the setting" stays mod-017 US-3's). Every project–team scenario of
doc-012 US-3 maps to US-3, US-4 or US-8; its «Управление» and visibility clauses are owed by mod-019 and feat-041, as the
stories' blocks say.

### R-1
Severity: minor
Affects: BR-OL-13, US-7, OQ-1
Problem: "the earliest created keeps it" cannot be computed: `teams` has no creation time (`TeamConfiguration.cs`), so the
migration and its test would each pick their own meaning of "earliest".
Required resolution: name the measure.
Status: resolved; superseded by OQ-1 C (human, 2026-10-17)
Resolution: fixed in place by the reviewer. BR-OL-13 reads "earliest" as the smallest id, team ids being time-ordered
version-7 UUIDs (`Team.Create`, and no migration inserts teams); US-7's first collision row says "the smaller id". The
backend plan checks that assumption against the stand's data.
Superseded: the upgrade drops every team and compares no names, so "earliest" is not needed. BR-OL-13 and US-7 no longer
carry the clause.

### R-2
Severity: minor
Affects: BR-OL-13, US-7
Problem: the collision test read "ignoring case and spaces", wider than BR-OL-3's "surrounding spaces", so "Plat form" could
pass as a collision with "Platform". The 100-character cut also did not leave room for « 2», so a cut name that was already
taken could break the length check.
Required resolution: use BR-OL-3's comparison; make the cut include the suffix.
Status: resolved; superseded by OQ-1 C (human, 2026-10-17)
Resolution: fixed in place by the reviewer. BR-OL-13 now cites BR-OL-3's comparison and cuts so that the whole name,
suffixes included, is at most 100 characters. US-7 gains a row for a cut name that is already taken.
Superseded: no team is renamed in the upgrade, so neither the comparison nor the cut applies there. BR-OL-3's comparison
still governs the names administrators give afterwards.

### R-3
Severity: minor
Affects: § Roles, US-4, US-5, US-8; ADR-0029 §§ 4, 6
Problem: three steps had no code.
- The deletion's per-project counts. Today the count route is `team.write`'s, which this line narrows.
- The unlink's count.
- A one-operation creation that links: a team created with its projects, a project created with its teams.
Until mod-019 only «Администратор» acts, so nothing leaks today. But mod-019 would inherit three paths that nobody
specified, and creating with links would get round BR-OL-5's two link codes.
Required resolution: name the codes.
Status: resolved
Resolution: fixed in place by the reviewer. § Roles now says:
- the deletion's counts take `team.delete`, and the deletion needs no code on the projects it reaches (ADR-0029 § 6,
  consequence writes);
- the unlink's count takes the two link codes;
- a creation that links also needs both link codes.
The table headers name these. US-8's role table gains the unlink's count and creation with links, and its inventory clause
names the code of each.

### R-4
Severity: minor
Affects: BR-OL-8, US-4, US-5, § NFR Concurrency; ADR-0028 rule 8
Problem: BR-OL-8 covered only a save or a creation racing an unlink. Three races ADR-0028 rule 8 names had no criterion:
- **A reorder of the team's backlog at the project's address.** The place key (item, team) → item (id, owning team) turns a
  late place into a `23503`, a server error unless the plan catches it. mod-017's reorder re-checks only that the team is
  active, not that it is still linked.
- **A default naming of the team.**
- **An unlink against the deletion of the unlinked team or of its destination.**
Required resolution: state each race's outcome and add a criterion for each.
Status: resolved
Resolution: fixed in place by the reviewer.
- BR-OL-8 now covers the default naming (cleared with the rest, or refused `not_in_project`) and the reorder (its places
  deleted, or refused `team.not_found` / `boards.backlog.reorder_stale`, writing no place). An unlink that meets either
  team's deletion ends as BR-OL-9 says.
- US-4 gains a scenario for the reorder and the naming.
- US-5's race outline gains two unlink rows (of MARS, and with MARS as the destination), and its Then reads "works on what
  it left".
- § NFR Concurrency lists all of these.

### R-5
Severity: minor
Affects: BR-OL-4, BR-OL-8, US-3
Problem: "unlinking an unlinked pair succeeds and changes nothing" clashed with two other rules: "none named, 400", and a
destination "not linked when the unlink runs" answers 409. Take a stale dialog that confirms an unlink after another
manager has unlinked both the team and its chosen destination. Read one way, that is success; read the other, a 409.
Required resolution: fix the order.
Status: resolved
Resolution: fixed in place by the reviewer. BR-OL-8: an unlink of a pair that is not linked moves nothing. After the 400 and
the destination's not-found it succeeds, whatever team it names. US-3's idempotence scenario names a destination.

### R-6
Severity: minor
Affects: US-8, BR-OL-11
Problem: US-8 said the inventory "lists no route that names a project in the address of a team write". BR-OL-11 keeps the
team views and their reorder (`boards.backlog.reorder`, a team write) at the project's address until feat-040. The
criterion contradicted the spec, and a link route from the project's page could not have been written.
Required resolution: name what leaves the project's address and what stays.
Status: resolved
Resolution: fixed in place by the reviewer. Every route that reads, renames, describes, deletes or staffs a team, or
counts what its deletion reassigns, leaves the project's address. What stays: the project's linked teams, the project's end
of a link and of an unlink with the unlink's count, and the team views with their reorder.

### R-7
Severity: minor
Affects: US-3, US-4
Problem: two criteria could not be tested as written.
- US-3 asks for "a way to open that object's own page". A project has no page of its own until feat-043.
- US-4 says the 7 items "are gone from MARS's backlog and board at Alpha's address". After the unlink that address answers
  «Команда не найдена.» (BR-OL-11), so there is nothing to look at.
Required resolution: make both concrete.
Status: resolved
Resolution: fixed in place by the reviewer.
- US-3: a team opens its page in the organization's settings. Until feat-043, a project opens its «Команды», which is its
  end of the link. The design may name another project page, with a note.
- US-4: the address answers «Команда не найдена.».

### R-8
Severity: minor
Affects: US-5; doc-012 US-2 "The deletion reaches a deactivated project too"
Problem: doc-012 asks that deleting a team clears a deactivated project's default naming it. US-5's cast named no default
for Delta, so the clause was lost.
Required resolution: restore it.
Status: resolved
Resolution: fixed in place by the reviewer. In US-5's cast Delta's settings name MARS, and VENUS is linked to Beta. The
confirmation scenario empties Alpha's and Delta's settings, and Delta's restore scenario checks that its setting is empty.

### R-9
Severity: minor
Affects: US-7, BR-OL-13; ADR-0028 rule 3
Problem: "Nothing visible moves" was not true. A team whose only project is deactivated (Ops) was hidden along with
Gamma. Once teams are the organization's, it is listed in the organization's «Команды» and its page opens (US-1, "A team
page does not depend on a project"). Renamed teams are visible too. An MR that says nothing visible moved would be wrong.
Required resolution: name the visible changes.
Status: resolved; superseded by OQ-1 C (human, 2026-10-17)
Resolution: fixed in place by the reviewer. US-7's second scenario names both changes: the renamed teams, and Ops, listed
with no project until Gamma is restored.
Superseded: nothing is renamed and no team survives, so Ops is gone. The point of the finding holds in a larger form:
"nothing visible moves" is false, and US-7's fourth scenario states what a user sees instead (no team anywhere, every item
«Без команды»).

### R-10
Severity: minor
Affects: § Decomposition; status.md § Plan
Problem: the task cell said "two QA passes, one review round". status.md's light route has one QA pass over both layers,
beside the code review and the security pass, and one fix round.
Required resolution: align the cell with status.md.
Status: resolved
Resolution: fixed in place by the reviewer.

### R-11
Severity: minor
Affects: § Decomposition, Breaks; mod-017 api-contract §§ 0.1, 0.2, 0.4, BR-OT-15
Problem: the Breaks note for mod-017 named BR-OT-2, -4, -11, -12 and §§ 2.7-2.9. It left out four pieces of mod-017 that
this line changes:
- BR-OT-15 and § 0.1, the codes of the default naming, the deletion and its count;
- § 0.2, whose row "a team of a deactivated project → 404" goes, since such a team is now an active team of the
  organization, judged "not linked";
- § 0.4, the deletion's 409.
Required resolution: name them in the P-10 note.
Status: resolved
Resolution: fixed in place by the reviewer.

### R-12
Severity: minor
Affects: § Error codes, US-1
Problem: two inventories were incomplete.
- The error table left out `organizations.team.member_not_found` «Участник команды не найден.», which the team page's
  member removal answers (`RemoveTeamMemberHandler`).
- US-1's list of retired project-nested requests left out the single team read, `GET …/projects/{p}/teams/{t}`, although
  § Existing rules retires that read's tests.
Required resolution: complete both.
Status: resolved
Resolution: fixed in place by the reviewer. The error table has the row. US-1 names "a read of MARS itself" and notes that
MARS's views stay at that prefix until feat-040.

### R-13
Severity: minor
Affects: BR-OL-3
Problem: "kept as typed" did not say what clearing a description means, so US-1's "then clears it" and US-2's "then clears
it" had no stored value to check.
Required resolution: define the empty description.
Status: resolved
Resolution: fixed in place by the reviewer. BR-OL-3: an empty description is no description. Spaces are otherwise kept as
typed (BR-MX-4).

### R-14
Severity: minor
Affects: § Out of scope; bug-005 (mod-017 CR-7)
Problem: bug-005 fixes a deadlock in mod-017's team locks: the creation's default lock, and the deletion that locks every
active team of the project. This line rewrites both onto links (ADR-0028 rule 8), and the deletion no longer needs the
project's teams once the last-team guard goes. The two tasks change the same code, and nobody said which goes first.
Required resolution: say who decides.
Status: resolved
Resolution: fixed in place by the reviewer. § Out of scope: the backend plan says whether this line's rewrite leaves
bug-005 standing, and the orchestrator orders the two tasks.

### Checked, no finding

- **Name collisions.**
  - On a rename: US-1 (" venus" refused, "Mars" free against its own name).
  - Across organizations: US-1, US-8.
  - On the upgrade: US-7, with R-1 and R-2 (superseded by OQ-1 C: the upgrade drops every team and compares no names).
- **A team linked to several projects (US-6).**
  - One name and one member list.
  - Views and their reorders per project (OQ-3 A); another project's places never move.
  - A default per project.
  - Its address under a project it is not linked to answers «Команда не найдена.».
- **Unlinking.** The named default (US-4), the last linked team (US-4, then US-2's shell and OQ-2), a team that owns nothing
  (US-4), two unlinks that name each other (US-4).
- **Deleting.**
  - A team on four projects, one of them deactivated (US-5). Its counts include the deactivated project, marked as
    doc-012 US-2 marks it.
  - Two deletions that empty a project: both done.
  - The child creation, in either order with the deletion.
- **Races.**
  - With a save or a creation of an item: US-4, US-5.
  - With a reorder, a default naming, a link or an unlink: US-4, US-5, after R-4.
- **Members across projects.** Membership grants nothing (US-1, US-3). Removing someone from the organization empties their
  teams there, and only there (US-1).
- **Team dashboards.** mod-009 is not built (no dashboard code under `src/`). The owed row and the fate row are correct.
- **The route inventory.**
  - mod-016 § 4.1 rows 15-18 and 20 are the team routes.
  - Row 19 went with mod-017.
  - Rows 21-26 are area routes. The spec is right to correct doc-012's Breaks cell, "rows 15-26".
- **Permissions.**
  - ADR-0029 § 4's bundles.
  - `OrganizationRoleBundles` gives «Администратор» every organization code, and `team.write` was hers alone, so no
    «Участник» or «Читатель» loses a right.
  - The default naming moves to `project.write`, also hers alone.
  - The answer order is unchanged: 403 before a nested 404 (US-3, US-6, US-8).
- **Tenant containment.**
  - S-2 is BR-OL-1's contract clause, tested by US-8's first scenario.
  - A foreign id in every body and address answers as an unknown one (US-8's outline).
  - A team of «Другая» can never be linked (US-3, US-8).
- **Size.** L in one MR: doc-012 § Decomposition says splitting a breaking line would not make it additive. The stories are
  `shippable: no` throughout.

### Open questions

OQ-1..OQ-5 stand, each with its options and its recommendation. All five are for the human at gate-1; the orchestrator
puts them in the gate-1 + gate-2 package after the design. R-1 only makes OQ-1 A's "earliest" precise. The reviewer added
no question.

Decided by the human on 2026-10-17: OQ-1 C (the upgrade drops every team; R-1, R-2 and R-9 are superseded), OQ-2 A, OQ-3
to OQ-5 A. The analyst applied them in `spec.md` (BR-OL-10, BR-OL-13, § Existing rules, § Open questions) and rewrote US-7.
The finding statuses above are the only change to this file; nothing else the reviewer read or decided is touched.

### Verdict

`approved-by-ai`: R-1..R-14 are minor and fixed in place, and none is left for the analyst. OQ-1..OQ-5 go to the human with
the gate package; the design continues on the recommended options.
