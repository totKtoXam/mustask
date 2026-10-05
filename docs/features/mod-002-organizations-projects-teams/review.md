# Review: mod-002-organizations-projects-teams

## Round 1 — requirements

Reviewed against: `status.md` C-1..C-4 and § Notes (the queue line, also
checked against `docs/backlog.md` together with mod-016's line); doc-006
`spec.md` and US-1..US-7; doc-010 `status.md` D-4..D-24, `spec.md`
§ Normalized queue, § ADR debt, § Permission codes and § Planning rules;
doc-001 § Roles, BR-3, BR-5, BR-6, BR-11, § Deletion lifecycle,
§ Concurrency policy, US-1, US-2; doc-002 W-10, BR-M3-5, § Fields (Область),
US-4, US-13; doc-007 R-5, R-6, BR-M2-1, BR-M2-8, BR-M2-9; ADR-0018;
`Mustask.Api/Authorization/EndpointCategories.cs`;
`deploy/keycloak/realms/mustask-realm.json` (realm roles `admin`, `member`,
`viewer`, `dictionaries-admin`); `src/frontend/src/app/AppLayout.tsx` and
`routes.tsx`.

Parallel tasks: the copies of mod-001, mod-014 and mod-015's `spec.md` and
`status.md` were not at the path the brief named (the `parallel/` folder does
not exist). Their content is taken only from the orchestrator's Log line, so
R-3 and R-15 are coordination items resting on that line.

### Constraint checks

- **C-2 (structure only).** After R-8, no story asserts membership, roles,
  codes, the ADR-0018 amendment, the last-admin or «неактивен» rules, or
  member search. § Out of scope sends all of them to mod-016. BR-M1-19 matches
  the queue line: every operation needs a sign-in, and creation also needs
  the create right.
- **C-3.** A case-insensitive search over the task folder, before and after
  the edits, found no hits. It covered the reference tracker's product,
  vendor and abbreviation names and their predecessors, its field and
  access-level terms, the other trackers AGENTS.md names, and the paths of
  doc-001's research notes and screenshots. The terms are left out here on
  purpose, because C-3 applies to this file too.
- **Language.** All prose is English. Every quoted Russian string is either
  interface copy (messages, labels, empty states, the confirmation) or example
  data the user types and then sees (names). The edits add only example data:
  «Второй проект», «Ядро», `РМК`.
- **Budgets after the edits.** `spec.md` is about 229 lines; the largest story
  is about 104.

### Rule → story mapping

| Rule | Covered by | Verdict |
|---|---|---|
| BR-M1-1 | US-1, US-2 (organization name, race), US-3 (key, project name, reserved name), US-4 (team), US-5 (siblings) | ok; the non-Latin key example was missing (R-13) |
| BR-M1-2 | US-1 (offered or refused, the console grant and revocation, no MUSTASK screen) | ok; the grantable role is now explicit (R-10) |
| BR-M1-3 | US-1 (seed, failure, retry), US-3 (the project uses the process) | only one direction was tested (R-9) |
| BR-M1-4..-8, -10, -16, -17 | § Out of scope → mod-016 | ok (C-2) |
| BR-M1-9 | US-3 (first team), US-4 (later team, root-child clash, race, rename), US-7 (name after deletion) | ok; unverifiable membership steps (R-8) |
| BR-M1-11 | US-3 (root with the project, rename), US-5 (root untouchable), US-6 (owner fixed, inheritance) | ok |
| BR-M1-12 | US-5 (siblings, move destination, cycle, cycle race) | ok |
| BR-M1-13 | US-5 (leaf only), US-6 (default area) | concurrency was missing (R-2) |
| BR-M1-14 | US-3, US-4 (picker, direct request, first team), US-6 (default kept, race, no-team-context answer) | contradicted BR-M1-18 (R-5); one step was defective (R-7) |
| BR-M1-15 | § Out of scope → release 2 | ok |
| BR-M1-18 | US-7 | requests against a deleted team were missing (R-12) |
| BR-M1-19 | US-1 (a third user sees every organization); the actor of US-2..US-7 | clashed with BR-M1-20 (R-6); OQ-1 |
| BR-M1-20 | US-2, US-3 | a change under a deactivated project was unpinned (R-12) |
| BR-M1-21 | US-1, US-3 | whether it is shown was unstated (R-11) |
| BR-M1-22 | none | unmapped (R-1) |
| BR-M1-23 | US-2, US-3 (settings) | ok; no entry point to the list (R-4) |
| Queue line items | organizations and seed (US-1, US-2), key (US-3), teams (US-4, US-7), area tree (US-5, US-6), settings (BR-M1-23), interim guard (BR-M1-19), soft-deactivation ADR (§ Out of scope, § Decomposition) | all mapped |

The doc-006 stories map as follows. US-1 and US-2 go to this task's US-1 and
US-2, without membership. US-3 and US-4 go to mod-016. US-5 goes to US-3.
US-6 goes to US-4 and US-7, without team membership. US-7 goes to US-5 and
US-6. doc-006 US-1's "an instance administrator may always create" rests on
the realm-role inventory, which is mod-016's; until then the existing
`admin` realm role gets the code through ADR-0018's contract test (R-10).

### Size

The task is L by doc-010 PR-2: it founds M1 and owes the soft-deactivation
ADR. It has 7 stories and about 70 scenarios. US-7 alone could be verified
independently, but the queue line and C-2 keep team deletion here, and it
shares the ADR. There is no finding.

---

### R-1
Severity: major
Affects: BR-M1-22, US-5, US-6, BPMN
Problem: BR-M1-22, the isolation boundary, was the one rule with no scenario.
Cross-project references were constrained by pickers only ("the owner picker
offers…"). Nothing tested a direct request that re-owns an area to a team of
another project or organization, moves an area under another project's node,
or creates one there. Under BR-M1-19 every signed-in user may call every
endpoint, so such a request is one crafted id away. Once stored, it is
permanent corruption: mod-016's Area → Project → Organization chain and
mod-006's boards would put one organization's work on another's team. Caught
now, this is a validation rule. Caught after mod-003, it is a data repair.
Required resolution: state the refusal in BR-M1-22 and give it scenarios.
Status: resolved
Resolution: BR-M1-22 gains "A request that names an object of another project
as an area's owner or parent, or as a team's default area, is refused". US-5
gains "A node never leaves its project". US-6 gains "An area's owner is a
team of the area's own project". BPMN node M now checks "Same project". A
default area from another project was already refused by US-4's
explicit-ownership step.

### R-2
Severity: major
Affects: NFR Concurrency, BR-M1-13, BR-M1-18, US-5, US-6
Problem: The concurrency list left out BR-M1-13, which doc-006's approved NFR
lists. Take a leaf being deleted while a child is created or moved under it,
or while it is made a team's default. Each change passes its check alone.
Together they leave a node under a deleted parent, or a team whose default is
a deleted area: a broken tree that no later module can repair. Separately,
the BR-M1-18 item named only "a re-own to a team that is being deleted".
Creating an area with that team as its owner is the same race, but it is not
a re-own.
Required resolution: add BR-M1-13 to the list, widen the BR-M1-18 item, and
give each a scenario.
Status: resolved
Resolution: NFR Concurrency adds "BR-M1-13 (a leaf deleted while a child is
created or moved under it, or while it becomes a team's default)". The
BR-M1-18 item now reads "(an area created with, or re-owned to, a team that
is being deleted)". US-5 gains "A leaf deleted while a child is put under
it". US-6's race scenario now pits either a re-own or a delete against a
default change.

### R-3
Severity: major
Affects: § Out of scope (mod-015), OQ-2, § Decomposition (deps)
Problem: According to the orchestrator's Log, mod-015's D-10 gives this task
three pieces of shell work: the context-switcher wiring, the tree-registry
pattern (mod-015 BR-14) and pages that carry their context in the address
(mod-015 BR-18). It also gives this task a dependency on mod-015. Neither the
queue line (deps: mod-001) nor C-4 carries any of this. mod-015's files were
not available, so its content is unverified here. Taking the dependency would
put an in-flight task onto the MVP critical path (doc-010: mod-001 → mod-002
→ mod-003 → …).
Required resolution: the human answers OQ-2.
Status: resolved (round 2)
Resolution: D-b took the work here with the dependency: US-8, BR-M1-24, the
US-5 tree-registry scenario, deps mod-001, mod-015. Checked in round 2.

### R-4
Severity: major
Affects: US-1..US-7 (every step that opens the organization list), BR-M1-23, C-4, OQ-4
Problem: Every story starts from the organization list, and no requirement
says how a user gets there. The panel's only navigation is the `navItems`
list inside `src/frontend/src/app/AppLayout.tsx`, which C-4 forbids touching.
The context switcher is mod-015's (OQ-2). As written, the design either
breaks C-4 or ships screens that open only from a typed URL, and QA and
gate-3 would each have to rule on that.
Required resolution: the human names the entry point.
Status: resolved (round 2)
Resolution: OQ-4 is added to the spec. The mod-015 bullet in § Out of scope
now cites OQ-2 and OQ-4. Round 2: D-d names the switcher's «Все организации»
and «Нет организаций» (BR-M1-23, BR-M1-24, US-8); R-22 below adds how the
settings pages are reached.

### R-5
Severity: minor
Affects: BR-M1-14, BR-M1-18
Problem: BR-M1-14 said a team's default area "cannot be re-owned … until that
team's default changes" and made no exception. BR-M1-18 re-owns every area
the deleted team owns explicitly, its default among them. Read literally,
every team deletion is refused, because every later team's default is a
non-root area it owns.
Required resolution: exempt the team's own deletion.
Status: resolved
Resolution: BR-M1-14 adds "the one exception is that team's own deletion
(BR-M1-18)".

### R-6
Severity: minor
Affects: BR-M1-19, BR-M1-20
Problem: BR-M1-19 said every signed-in user "lists and opens every
organization and everything under it, deactivated lists included". BR-M1-20
says opening a deactivated organization answers «Организация не найдена». One
test cannot satisfy both.
Required resolution: separate listing from opening.
Status: resolved
Resolution: BR-M1-19 now reads "lists every organization, the deactivated
lists included, opens every active one and everything active under it".

### R-7
Severity: minor
Affects: US-6 "A team's default area stays with its team"
Problem: The last step said that once the default moves to «Платформа /
API», "the same action is allowed", and the delete was one of those actions.
But «Платформа / API» is a child of «Платформа», so «Платформа» is not a leaf
and BR-M1-13 still refuses the delete. A test written to the step fails. For
the same reason, the delete leg of the When had two competing refusal
messages.
Required resolution: use an example where the default area is a leaf and the
replacement default lies outside it.
Status: resolved
Resolution: The Given is now the leaf «Платформа» plus «Отчёты», which the
team also owns explicitly. The default moves to «Отчёты», and after that all
three actions are allowed.

### R-8
Severity: minor
Affects: US-3, US-4, US-5, BR-M1-9, C-2
Problem: Three steps asserted what no test in this task can check. US-3's
"with no members yet" and US-4's "the new one with no members" assert on team
membership. This task does not model team membership (C-2), and the steps
invite a developer to build a membership table here anyway. US-5's last step
("the work-item part of this check comes with mod-003 …; a team's default
area is never deleted (US-6)") is a pointer, not an outcome.
Required resolution: drop them from the Gherkin and keep the pointers in the
rules.
Status: resolved
Resolution: The three steps are removed. BR-M1-9 now reads "named after the
project (team membership, the first member included, is mod-016's)".
BR-M1-13 already carries the mod-003 pointer.

### R-9
Severity: minor
Affects: BR-M1-3, US-1
Problem: "Creation and seed succeed or fail together" was tested in one
direction only: the seed fails, so no organization exists. The other
direction was unstated: the seed ran, then the creation was refused, as it is
for the loser of US-1's name race. A design could commit the seed separately
and leave an orphan process behind every lost race, and QA would have no
criterion to catch it.
Required resolution: state and test both directions.
Status: resolved
Resolution: BR-M1-3 adds "a creation refused after its seed ran (a lost name
race, for instance) leaves no seeded process behind". US-1's race scenario
adds that step.

### R-10
Severity: minor
Affects: § Roles, BR-M1-2, US-1
Problem: US-1's grant scenario needs a realm role or group that a Keycloak
realm administrator can assign. § Roles said only that the contract tests
decide which roles carry the code "besides the one granted for it", and sent
the inventory to mod-016. It did not say that this task adds that role, or
that the role grants nothing else. Today the only realm role that would carry
the new code is `admin`, through ADR-0018's "admin holds every permission"
contract test. Assigning `admin` to grant organization creation grants
everything.
Required resolution: say that this task adds a dedicated, grantable role or
group.
Status: resolved
Resolution: § Roles now says "This task adds to the realm import one realm
role or group that carries the code and grants nothing else, for a Keycloak
realm administrator to assign (US-1)". C-2 names "the create-organizations
realm role" as this task's. The change stays within ADR-0018 as written (a
composite realm role over a client role), so it is not the amendment.

### R-11
Severity: minor
Affects: BR-M1-21, US-1, US-3
Problem: US-1 and US-3 require the creator record, but nothing said whether
any screen shows it. Showing "who" as a name needs mod-014's projection, and
§ Out of scope says this task reads none. The designer would have had to
guess.
Required resolution: say whether it is shown.
Status: resolved
Resolution: BR-M1-21 adds "no screen of this task shows it (a name would need
mod-014's projection)". The criteria can still be checked through the
application layer.

### R-12
Severity: minor
Affects: US-1, US-3, US-7, BPMN
Problem: Four refusal paths were missing or too narrow.
(a) US-3 did not say what a change request under a deactivated project
answers; US-2 does say it for an organization.
(b) No scenario covered a request against a team that is already deleted:
rename it, change its default, make it an area's owner, or delete it again
(the resubmission case).
(c) The anonymous scenario covered only listing and creating organizations,
but the rule applies to every operation.
(d) BPMN node M left out the leaf-only check on delete.
Required resolution: pin each one.
Status: resolved
Resolution:
(a) US-3 now says "a request that changes anything under it answers «Проект
не найден»".
(b) US-7's not-found scenario now covers those requests, answered «Команда не
найдена».
(c) US-1's anonymous scenario now covers any operation of this task.
(d) Node M now lists "only a leaf deleted".

### R-13
Severity: minor
Affects: US-3, BR-M1-1
Problem: The key must be Latin, but none of the examples is non-Latin. In a
Russian interface the likeliest bad key is one typed on a Cyrillic layout.
Several Cyrillic capitals look exactly like Latin ones (Р, М, К, А, С), so a
check that looks only at case, length and the first character would accept
`РМК`.
Required resolution: add a Cyrillic example.
Status: resolved
Resolution: US-3's key scenario adds "`РМК` typed in Cyrillic letters".

### R-14
Severity: minor
Affects: § Decomposition (the proposed queue consequence)
Problem: The proposal had mod-016 make "each project's creator the first
team's member". In the interim any signed-in user can create a project in
any organization (BR-M1-19). That creator may therefore not be a member of
the organization, and BR-M1-10 (team membership is a subset of organization
membership) would forbid making them a team member.
Required resolution: qualify the proposal.
Status: resolved
Resolution: The proposal now reads "each project's creator the first team's
member where BR-M1-10 allows it".

### R-15
Severity: minor
Affects: § Decomposition; doc-001, doc-006 and doc-010 (approved); mod-001 (in flight)
Problem: C-2 moves the ADR-0018 amendment and the organization-scoped codes
to mod-016, but the approved specs still name mod-002 for both:
- doc-001 § Roles, BR-5 and § Decomposition;
- doc-006 § Roles (the constraints preamble and the code-table header),
  BR-M1-6 and § Decomposition;
- doc-010 § ADR debt and § Permission codes.
The orchestrator's Log reports the same drift in mod-001's in-flight BR-M2-10
and BR-M2-11. That is unverified here, because the copy was absent. C-2
settles the substance, but doc-010 PR-7 needs some task to record the
amendment. Until one does, mod-016's analyst and mod-001's implementer read
the wrong owner.
Required resolution: name the task that carries the amendment.
Status: resolved (in the spec, as a proposal the orchestrator applies)
Resolution: The proposed queue consequence gains a third mod-016 item: amend
those specs under PR-7. The mod-001 point goes to that task's own review.

### Questions for the human (gate-1)

- **OQ-1, interim access.** Recommended answer: confirm as written. The queue
  line, which C-2 makes normative, reads this way. The literal alternative
  would lock out of mod-003's work every user who lacks the create right, and
  the split exists to unblock that work. Confirming also accepts a risk: until
  mod-016 merges, any signed-in user can deactivate any organization and
  delete any team for good. There is no remote and no production stand today.
- **OQ-2, mod-015's D-10.** Recommended answer: not here. Keep the deps the
  queue line gives (mod-001). The switcher wiring goes to a follow-up line
  once mod-002 and mod-015 have both merged, or into mod-015 itself if
  mod-002 lands first.
- **OQ-3, link to the process screen.** Recommended answer: yes, as one
  read-only link from the project settings, provided mod-001 is in `main` by
  stage 4. doc-007 R-6 says the screen is reached from a project, and this
  task builds the only project page. mod-001's design decides where the
  screen itself lives.
- **OQ-4, entry point to the organization list.** Recommended answer: give
  the list its own route and link it from «Главная» (`features/home`, outside
  `app/`). Add no AppLayout navigation entry until the switcher lands.

### Verdict

`needs-human-decision`. Findings: 0 blocker, 4 major (R-1 and R-2 resolved;
R-3 and R-4 open, awaiting OQ-2 and OQ-4), 11 minor (all resolved). OQ-1 and
OQ-3 are open with recommended answers.

## Round 2 — delta check (2026-10-01)

Reviewed against: `status.md` D-a..D-l and C-4 as reworded; `git diff
508639a 98153a2` on `spec.md` and `stories/` (US-2, US-3, US-5 changed, US-8
new); the sibling specs at their branch heads, read with `git show`: mod-001
(`mod/001-process-seed` 65aa732: `spec.md`, `status.md`), mod-014 (8338fc2:
`spec.md`, `status.md`), mod-015 (252fa60: `spec.md`, `status.md`, US-3, US-5,
`review.md` round 2); mod-016's queue line in `main` (10f5364); tech-001
(`tech/001-naming-rule-plan-amendment` e482e78) for the doc-006 and doc-010
drift R-15 named. Not re-read: the normative references of round 1.

### Decisions → spec

| Decision | Where it lands | Verdict |
|---|---|---|
| D-a interim access, risk accepted | BR-M1-19, § Roles, NFR Access | ok; the Roles row still contradicted BR-M1-19 (R-16) |
| D-b switcher, tree registry, addresses, dep on mod-015 | Context, BR-M1-24, US-8, US-5, NFR Seam with mod-015, § Out of scope, § Decomposition | ok; R-17, R-19..R-21, R-23, R-24, R-26 |
| C-4 reworded | BR-M1-24 (last sentence) | ok; the seam it needs is R-26 |
| D-c «Процесс» link | BR-M1-23, US-3 | ok |
| D-d «Все организации», «Нет организаций» | BR-M1-23, BR-M1-24, US-8 | ok; how the settings pages are reached is R-22 |
| D-e mod-016's line | § Decomposition, § Out of scope | ok; `main` 10f5364 carries all four items |
| D-f not-found frames named | BR-M1-20, US-2, US-3, US-7 | ok; GATE1-6's "own calls" are not written in any file of this task, so only the XT-1 half is checked |
| D-g organization-context filter | BR-M1-20, US-2 | it also caught the restore request (R-18) |
| D-h demo seed removed | NFR Seam with mod-001 | ok; rests on mod-001's delta (note 2) |
| D-i the create code | § Roles, BR-M1-19 | ok; the code itself was unnamed (R-25) |
| D-j stand reset | NFR Access | ok; matches mod-014 D-10 |
| D-k error codes, seed seam, addresses | NFR Seam with mod-001; BR-M1-24 | seam ok; malformed and foreign ids were unpinned (R-17); the error-code reuse is api-contract matter for stage 4 |
| D-l E2E consent | none needed | ok |

Round 1: R-3 and R-4 are resolved by D-b and D-d; OQ-1..OQ-4 are closed in
`spec.md` with their decisions.

### Rule → story mapping (delta)

| Rule | Covered by |
|---|---|
| BR-M1-20 | US-2 (deactivated, unknown or malformed organization; process screen; restore), US-3 (deactivated project; unknown, malformed or foreign project), US-4 (unknown, malformed or foreign team), US-7 (deleted team), US-8 (a not-found address keeps the selection) |
| BR-M1-23 | US-2, US-3 (settings, «Процесс»), US-8 (organization list, the settings reached through «Настройки») |
| BR-M1-24 | US-8 (lists, default team, address, switch, memory, fallback, own changes, failure), US-5 (tree registry), US-2 and US-3 (landing after a deactivation) |

### Constraint checks

- **C-3.** The same case-insensitive search as round 1, over the task folder
  after the edits, found no hits.
- **Language.** Prose is English. The edits add no interface copy beyond
  strings the spec already quotes; «Отчётность» is example data.
- **Budgets.** `spec.md` 239 lines; the largest story, US-8, 141.

---

### R-16
Severity: minor
Affects: § Roles (row "Any signed-in user"), BR-M1-19
Problem: R-6 fixed BR-M1-19 to "opens every active one", but the Roles row
still read "lists and opens every organization and everything in it", which
BR-M1-20 refutes for a deactivated one.
Required resolution: align the row with BR-M1-19.
Status: resolved
Resolution: the row reads "lists every organization, opens every active one
and everything active in it".

### R-17
Severity: major
Affects: BR-M1-20, BR-M1-22, US-2, US-3, US-4; D-b, D-k; mod-015 BR-18, D-17
Problem: D-b gives this task the context-in-address routes, and D-k fixes
them per mod-015 D-17: route parameters are untyped strings, so a malformed
and an unknown id get the same named not-found answer, for the outermost
level not found (mod-015 BR-18). BR-M1-20 pinned only "an organization
identifier that names no organization". Nothing covered a malformed id, an
unknown project or team id, or a project or team addressed under the wrong
container — the address of organization A carrying a project of B. A lookup
by the inner id alone renders B's project under A's address and puts it into
A's switcher. Today everyone reads everything, so nothing leaks yet; once
mod-016 checks membership against the organization in the address, that same
lookup lets a member of A read B's project. It is BR-M1-22's isolation
boundary at the address, and the cheap time to pin it is before the routes
exist. A typed route constraint would also answer a malformed id with the
route's 404 instead of the named frame.
Required resolution: state the rule and give each level a scenario.
Status: resolved
Resolution: BR-M1-20 now says an identifier that is malformed, names nothing,
or names an object outside the organization or project the address carries
gets the not-found answer of the outermost level not found. US-2's
unknown-organization scenario covers a malformed value. New scenarios: US-3
"A project identifier that names no project of the organization", US-4 "A
team identifier that names no team of the project".

### R-18
Severity: minor
Affects: BR-M1-20, US-2, US-3; D-g
Problem: D-g's filter answers not found for every organization-addressed
request of an organization that is not active, and BR-M1-20 carried that
literally. «Восстановить» is such a request, so read as written a deactivated
organization can never come back. The same holds for a project's restore.
US-2 and US-3's restore scenarios contradicted the rule.
Required resolution: exempt the restore.
Status: resolved
Resolution: BR-M1-20 adds "«Восстановить» is the one request a deactivated
organization or project accepts".

### R-19
Severity: minor
Affects: US-8 header, § Stories
Problem: US-8 depended on US-1, US-3 and US-4, but its scenarios use a
deactivated organization (US-2), a deactivated project (US-3) and a deleted
team (US-7). A plan that builds US-8 before US-2 or US-7 cannot pass it.
Required resolution: complete depends-on.
Status: resolved
Resolution: depends-on is US-1, US-2, US-3, US-4, US-7 in the header and in
the story table; the shippable reason names them.

### R-20
Severity: minor
Affects: BR-M1-24, US-8 "The address sets the switcher"
Problem: BR-M1-24 said "Every page of this task carries its context in its
address", but the organization list has no context, and mod-015 BR-18 leaves
such pages alone on a switch. And "an address that carries a context sets
it" made an address of a deactivated or unknown organization the remembered
selection, which the next reload would then have to fall back from.
Required resolution: scope the address rule and exclude a not-found context.
Status: resolved
Resolution: BR-M1-24 now reads "Every organization-, project- or team-level
page of this task carries its context in its address under mod-015 D-17's
prefixes (the organization list carries none)" and "sets it unless that
context is not found". US-8's scenario adds that a not-found address keeps the
selection.

### R-21
Severity: minor
Affects: BR-M1-24, US-2, US-3, US-8 "The user's own changes…"
Problem: US-8 had the switcher fall back without a reload when the user
deactivates the organization they are in. It did not say what happens to the
page. If a fallback counts as a choice, mod-015 BR-18 sends the user to
«Бета»'s settings. If it does not, they stay on a page that now answers
«Организация не найдена». A test could not tell which is right. The same gap
covered a project deactivated from its own settings.
Required resolution: say whether a fallback moves the page, and where the
deactivating user lands.
Status: resolved (reviewer's call, for the gate-1 summary)
Resolution: BR-M1-24 adds "A fallback changes the selection, never the page;
a user who deactivates the organization or project they are in lands on the
list that holds it among the deactivated ones", so «Восстановить» is one click
away. US-2 and US-3's deactivation scenarios and US-8's own-changes scenario,
which now also deletes the chosen team «Ядро», carry it. The alternative, staying on
the not-found frame, is equally testable; the human may pick it at gate-1.

### R-22
Severity: minor
Affects: BR-M1-23, US-1, US-3, US-8; mod-015 § Navigation model, BR-7, BR-8
Problem: mod-015's navigation model gives this task the settings entries
under «Организация» and «Проект» and hides them until their pages exist
(mod-015 US-3). BR-M1-23 listed what the settings hold, but not that this
task registers those entries, nor where an organization's project list lives.
US-1 and US-3 use that list, and mod-015's model has no entry for it. The
project's name, key and «Деактивировать» have no entry there either. Outside
the organization list and a typed address, the designer would have had to
guess how a user reaches these pages.
Required resolution: name the project list and the registration; leave the
page split to design.
Status: resolved
Resolution: BR-M1-23 adds that an organization's project list holds the
create-project action, the active projects and the deactivated ones. It adds
that this task registers its organization- and project-level pages as
entries under «Организация» and «Проект», through the navigation registry and
under mod-015 BR-8's interim clause, and that which entry holds which page is
stage 3's. US-8 gains "The settings are reached through the shell's
navigation".

### R-23
Severity: minor
Affects: US-8 "A page that addresses one team or area lands on its entry's page"
Problem: The Then read "the settings of «Аналитика» open", which presumes one
project-settings page. mod-015's model lists teams and areas as separate
«Проект» entries, whose landing pages may differ. A design that splits them
fails the step while it follows mod-015 BR-18.
Required resolution: assert the rule, not a page.
Status: resolved
Resolution: the Then now reads "the landing page of that page's navigation
entry opens for «Аналитика», not a team or area of «Аналитика»".

### R-24
Severity: minor
Affects: US-5 "The area tree is shown as the shell's tree registry"
Problem: "the pattern is built once, for any later module's tree to reuse
rather than copy" cannot be checked when only one tree exists.
Required resolution: make it checkable now.
Status: resolved
Resolution: the step now reads "the pattern lives in the shared UI layer and
names nothing of areas, owners or teams".

### R-25
Severity: minor
Affects: § Roles; D-i
Problem: D-i, doc-010 § Permission codes and doc-006 § Roles, as tech-001
amends it, say that this task fixes the string `organizations.organization.create`.
The spec named only "an instance-level code", so the PermissionCatalog entry,
the client role and the realm-import test had no requirement to check against.
Required resolution: name it.
Status: resolved
Resolution: § Roles names `organizations.organization.create` (ADR-0018, D-i).

### R-26
Severity: minor
Affects: BR-M1-24, C-4 as reworded; mod-015 BR-8, BR-11, BR-18
Problem: D-b gives this task the remembered selection, its fallback and the
address sync. C-4 limits it to the shell's registries (navigation, switcher
data). mod-015's build keeps no selection across a reload (its BR-11) and has
no context-in-address page, so nothing there exercises BR-18's switch
navigation. If mod-015's switcher exposes only a list of data, stage 6 can do
neither without editing the shell, and C-4 forbids that.
Required resolution: say through which seam the work goes, and hand the need
to mod-015's design.
Status: resolved here; the mod-015 side is note 3
Resolution: BR-M1-24 adds "The remembered selection, its fallback and the
address sync are supplied through the switcher's context source (mod-015
BR-8), not by editing the shell".

### Notes for the orchestrator (not findings of this spec)

1. `status.md` C-1's fallback ("put the call behind an interface … wire it
   before QA") is superseded by D-k (no stub; mod-001 merges before stage 4).
   The spec follows D-k. C-1 should say so.
2. mod-001 at 65aa732 has not yet taken its round-2 delta. D-h assumes it
   adds the Development-only demo seed, which is still its open question 1
   (a). D-k assumes it registers `organizations.organization.not_found` in
   `contracts/errors/organizations.json` and takes a `Guid`; its spec names
   only the copy (BR-M2-11) and leaves the identifier format to design
   (BR-M2-9). D-c assumes its screen at
   `/organizations/:organizationId/settings/process`; BR-M2-12 says only "its
   own address, which carries the organization identifier". None of these
   contradicts this spec. mod-001's delta should carry them. Its out-of-scope
   line "That a deactivated organization answers 'not found' is M1's rule"
   agrees with D-g.
3. mod-015 at 252fa60 is consistent with this spec on BR-11, BR-12, BR-14,
   BR-18, D-15, D-17..D-20 and D-22, and US-5 designs the failed and
   lost-selection states US-8 cites. Its stage 3 should know two things.
   First, the switcher's context source must own the selection, its memory
   and the address sync, and the switch navigation must live in the shell
   (R-26). Second, its § Navigation model has no entry for an organization's
   project list or for the project's name, key and «Деактивировать», which
   this task adds through the registry (R-22).
4. mod-014 at 8338fc2 is consistent: its D-10 drift check and
   `scripts/stand-reset.sh` are the reset D-j names. Its D-6 and D-11 leave
   M1's code skeleton to this task, which is a stage-4 note, not a spec
   matter.
5. R-15's remainder: doc-001 is on mod-016's line (verified in `main`
   10f5364). doc-006 § Roles, BR-M1-6 and § Decomposition, and doc-010
   § ADR debt and § Permission codes, are amended by tech-001 (e482e78), which
   is not yet in `main`.

### Reviewer's calls for the gate-1 summary

- R-21: the user who deactivates the organization or project they are in
  lands on the list that holds it among the deactivated ones.
- R-17 follows BR-M1-22 and mod-015 BR-18 and makes no new choice. R-22
  leaves the page split under «Организация» and «Проект» to stage 3.

### Verdict

`approved-by-ai`. Round 2 findings: 0 blocker, 1 major (R-17, resolved in
place), 10 minor (R-16, R-18..R-26, all resolved in place). Round 1's R-3 and
R-4 are resolved by D-b and D-d. No open question remains. The gate is the
human's.

## Design check (2026-10-02)

Reviewer: an adversarial design reviewer before gate-2, in the
requirements-reviewer's stance, with a clean context. It wrote none of
`design.md`, the prototype or ADR-0026. Scope: `design.md`,
`prototype/index.html` and
[ADR-0026](../../adr/0026-soft-deactivation-by-status.md) at e35a7da, checked
against `spec.md` and `stories/US-1..US-8` at the same commit. The check also
covered the sibling artifacts as committed on their branches:
mod/015-app-shell-design-system at b6f1d66 (`design.md`, unchanged since
0a9dd46; ADR-0024, now accepted; `plan-frontend.md`, read for how the shell
will carry the seams) and mod/001-process-seed at c9479ba (`design.md`,
`api-contract.md`, `contracts/errors/organizations.json`). The remaining
sources were `docs/conventions/frontend.md`, ADR-0007, ADR-0011, ADR-0019,
doc-001 § Deletion lifecycle, doc-006 § Roles and doc-010 § ADR debt.
Component and icon names were checked against MUI 9.4.0 as installed in the
main checkout.

Special checks:

- **Naming (C-3).** A case-insensitive search of `spec.md`, the stories,
  `design.md`, the prototype and ADR-0026 for the reference tracker's product
  and vendor names and their abbreviations finds nothing.
- **MUI only (frontend.md, ADR-0019).** Every component named is
  `@mui/material` core: the treegrid is a `Table`, and the rest are
  `Autocomplete`, `TextField select`, `Tabs`, `Menu`, `Dialog`, `Snackbar`,
  `Chip` and `Skeleton`. All 14 icon names exist in `@mui/icons-material`
  9.4.0. No tree or grid package is used, and nothing is added.
- **Copy.** 25 strings the stories quote were searched in the prototype, and
  each appears there and in `design.md` verbatim, among them US-7's
  confirmation, every refusal, the empty frames, «Владеет корневой областью»,
  «унаследовано», «Все организации» and «Нет организаций». New copy is
  Russian. The punctuation of a server text: R-32.
- **No invented API.** § 15 lists the operations behind the screens for the
  api-contract to shape. It defines no endpoint. One stage-4 consequence is
  note 3.
- **ADR-0026.** It agrees with the spec and with doc-001 § Deletion
  lifecycle: the status table and its transitions, reservation and freeing by
  index (BR-M1-1), the exact restore (BR-M1-20), the team cascade
  (BR-M1-18), restore only under a reachable container, the status read at
  read time by other modules (US-7, doc-010 PR-9), and the team-row locks for
  US-7's race (NFR Concurrency). The echelons are ADR-0011's. It is
  `proposed`, as the ADR process requires before the gate. No finding.

### Scenario → design coverage

| Story | Scenarios | Where | Check |
|---|---|---|---|
| US-1 | 11 | § 1, § 4.4, § 5 | complete |
| US-2 | 9 | § 3.6, § 5, § 6, § 12 | complete |
| US-3 | 12 | § 4.4, § 7, § 8, § 12 | complete; R-33 |
| US-4 | 9 | § 9, § 10, § 12 | complete; the team page's place in the shell: R-28 |
| US-5 | 13 | § 11 | complete |
| US-6 | 10 | § 11, § 15 | complete |
| US-7 | 7 | § 9.7, § 10, § 12, ADR-0026 | complete; R-28 |
| US-8 | 13 | § 2, § 3 | complete; R-27, R-28, R-31 |

Every page has loading, error and not found. Each list page has the empty
frames of both tabs, and the organization list has one per right. «Команды»
has no empty state, because BR-M1-9 always gives a project its first team.
The tree has its root-only state. Every dialog has busy, a field error and
an error `Alert`, and the default-area dialog has its own loading and failed
read. Restore has busy and failure. Until mod-016 the only refusal is the
create right, so no page needs «Доступ ограничен», and no page has a filter,
so there is no "empty after a filter". The canon is complete.

### Sibling consistency

- **mod-015 (b6f1d66).** The design agrees with mod-015 on:
  - the frame and the trail rule «Настройки / <section>»;
  - `PageHeader`: one primary, text secondaries, tabs as addresses;
  - the canon frames, with not found named by level;
  - `ConfirmDialog` and its destructive flag;
  - the switcher's anatomy, its order, «Все организации», «Нет
    организаций» and the fallback notice's copy;
  - BR-18's landing rule (§ 2.2);
  - the tree registry, with this task's command names, which mod-015 § 10.7
    allows;
  - the entries under «Организация» and «Проект», which mod-015 § 4.1 leaves
    to this task.

  It disagrees on two seams: R-27 and R-28. ADR-0024 is now accepted (R-29).
- **mod-001 (c9479ba).** The «Процесс» link and its address agree with
  mod-001's route and hand-off ("mod-002 owns the «Процесс» link"). The
  not-found code is reused (D-k), and mod-001's contract § 1.3 already
  answers it for a deactivated organization through this task's context
  filter. The registered text ends with a full stop (R-32). The switcher on
  its page: R-31. The seed seam: note 4.

### R-27
Severity: major
Affects: § 3.1, § 3.2, § 3.7, § 15 (the switcher row); mod-015 `design.md`
§ 5.4, § 5.7 and its copy rule; mod-015 `plan-frontend.md` § 4.3, D-15;
`status.md` C-4
Problem: § 3.1 reads the switcher's levels one by one: projects when the
organization changes, and teams when the project changes. § 3.2 then adds
four switcher states for a lower level that loads or fails on its own. They
show the upper levels followed by one or two skeletons, or by «Не удалось
загрузить проекты» or «Не удалось загрузить команды» with «Повторить». The
switcher's UI is mod-015's (`app/shell`). mod-015 § 5.7 hands this task one
context-source interface: a single status (loading, failed or ready), the
tree, `select(level, id)` and `isReal`. Its § 5.4 states are states of the
whole switcher. Its plan renders them from that one status and fixes the
tree as organizations → projects → teams (§ 4.3, D-15). Nothing in mod-015's
design or plan renders a state per level, and C-4 lets this task extend the
shell only through its registries, never by editing the switcher. So stage 6
can neither draw § 3.2's rows without editing `app/shell`, nor express one
level's load through the single status. That status would blank all three
levels, and a failure would read «Не удалось загрузить организации».
Required resolution: make the switcher's data fit the seam, or have the seam
changed. The options:

- (a) Keep the per-level reads, and have the orchestrator amend mod-015
  while its stage 6 is open (its last commit is an interrupted pass). The
  `WorkContextSource` gains a status per level, `ContextSwitcher` renders
  § 3.2's rows, and their copy moves into mod-015's § 5.4, since a later
  design never adds to the shell's copy. Both status files record it, as
  mod-015's plan D-15 did for the organization level.
- (b) Read the switcher as one tree (the active organizations with their
  active projects and each project's active teams) under mod-015's single
  status. § 3.2's rows go, and § 3.1, § 3.7 and § 15 become one read and one
  invalidation. mod-015 does not change, but at NFR Volumes the read carries
  tens × tens × tens names on every sign-in and after every change.

Recommendation: (a). The per-level states are a real need at the stated
volumes. mod-015 moved the organization-level states into the shell for the
same reason, so that this task would not edit the switcher, and the change
is cheapest while mod-015's shell is still being written. (b) only if the
orchestrator accepts the whole-tree read.
Status: open

### R-28
Severity: major
Affects: DC-4; § 1 (the route table, the trail); § 2.1 (the «Команды» row's
"`aria-current="true"` also on"); § 2.2; § 9.7; § 10; § 12; § 17 and the
prototype (its per-page `settings` flag); mod-015 `design.md` § 4.1, § 4.4,
BR-19; mod-015 `plan-frontend.md` § 4.2, § 4.5 (D-7); `status.md` C-4
Problem: the team page lives at `/organizations/:o/projects/:p/teams/:t/settings`.
That address is not beneath any settings entry's landing.
mod-015's shell derives three things from the navigation model:
- whether an address is a settings page, which decides the settings
  navigation and the rail (BR-19);
- which entry is current, `"true"` beneath its landing (§ 4.4);
- the trail: «Настройки / <section>», then the entry as a link for an
  address beneath it (plan § 4.5).

For this route the shell shows no «Меню настроек», marks no entry and
derives no trail. The page the design draws needs all three: the trail
«Настройки / Проект / Команды» with «Команды» as a link, and «Команды»
marked current. That takes a field the model does not have: an entry that
is "also current on" other addresses. The prototype gets the same result
from a per-page flag, which mod-015's shell does not offer. Adding the field
edits `app/navigation`, which C-4 does not allow.
Required resolution: the options:

- (a) Put the team page beneath its entry, at
  `/organizations/:o/projects/:p/settings/teams/:t`. It becomes a
  project-level page that addresses one entity, as mod-015 § 5.5 lists "one
  team" beside a work item. The shell then gives it the settings navigation,
  the «Команды» marker and the trail «Настройки / Проект / Команды», all
  unchanged. This changes:
  - DC-4's "team-level" wording, § 1, § 17 and the prototype's routes;
  - § 2.1: the extra column goes;
  - § 2.2: a team choice leaves the page as it is, because the address
    carries no team level (mod-015 BR-18);
  - § 9.7: a team deleted from its own page is the switcher's only if it
    was chosen there; otherwise the action's own notice shows;
  - § 12: the «Команда не найдена» row.

  US-4, US-7 and US-8 hold either way: the team has its address, it answers
  «Команда не найдена», and a project switch lands on «Команды».
- (b) Keep the route, and have the orchestrator amend mod-015 while its
  stage 6 is open: a navigation entry may declare more addresses on which it
  is current and which count as settings pages. Both status files record it.

Recommendation: (a). It needs no shell change, and the shell's derivation
already gives exactly the trail and the marker the design wants.
Status: open

### R-29
Severity: minor
Affects: the header bullets of `design.md` (Shell, mod-001, ADRs relied on)
Problem: The design cited ADR-0024 as proposed. It is accepted on mod-015's
branch at b6f1d66, by its D-24. The mod-001 bullet did not name mod-001's
`api-contract.md` (c9479ba), which D-k's reuse and § 4.4 rely on.
Required resolution: Cite the current state.
Status: resolved
Resolution: The bullets now say that ADR-0024 is accepted (b6f1d66, D-24),
that mod-015's `design.md` is unchanged at b6f1d66, and that mod-001's
`api-contract.md` is at c9479ba.

### R-30
Severity: minor
Affects: § 2.1
Problem: The design said that the code strings are mod-015 § 4.1's. That
section does not list `organizations.project.read`, which the «Проекты» and
project «Общие» entries carry. It comes from doc-006 § Roles.
Required resolution: Name the source.
Status: resolved
Resolution: § 2.1 now names doc-006 § Roles for that code.

### R-31
Severity: minor
Affects: § 3.4; BR-M1-24; mod-015 BR-18; mod-001 `design.md` (how the
switcher behaves on its page before mod-002)
Problem: § 3.4 synced the switcher from the address only "on a page of this
task". BR-M1-24 says that an address carrying a context sets the selection.
mod-015 BR-18 says that a context page's switcher shows the address's
context. And mod-001's design leaves BR-18's address rule for its «Процесс»
page to this task. So a link to another organization's «Процесс» would have
shown that organization's process under the organization chosen before.
Required resolution: Cover every page under the context prefixes.
Status: resolved
Resolution: § 3.4 now covers every page under mod-015 D-17's prefixes. On a
page another slice owns, the context counts as found when the level's
active list holds the id.

### R-32
Severity: minor
Affects: § 4.4 (the not-found row); mod-001 `api-contract.md` § 1.3, § 3;
`contracts/errors/organizations.json` on mod/001-process-seed
Problem: § 4.4 gave «Организация не найдена» as the server text a dialog's
`Alert` shows. The text that mod-001 registers, and that D-k reuses, is
«Организация не найдена.», with a full stop. The project and team codes
this task adds had no stated text.
Required resolution: Quote the registered text and fix the new ones.
Status: resolved
Resolution: The row quotes the registered text and gives this task's codes
the same form: «Проект не найден.» and «Команда не найдена.». The frame
headings come from mod-015's `NotFound` and stay without a full stop.

### R-33
Severity: minor
Affects: § 8.3; US-3
Problem: The rename dialog's errors listed «… : он деактивирован»: an
abbreviation, with a stray space before the colon. The developer copies
strings verbatim, so this one would be copied wrong. US-3 quotes it in full.
Required resolution: Write the string out.
Status: resolved
Resolution: § 8.3 now has US-3's full string.

### Notes for the orchestrator (not findings of this design)

1. **AppLayout.** mod-015's plan § 13 has mod-002 replace
   `usePlaceholderContextSource()` in `AppLayout` with its own source. D-d
   says "no AppLayout edit", and design § 16 repeats it. Either record that
   this one-line swap is the switcher-data registry C-4 allows, or ask
   mod-015 (stage 6 open) to take the source through a registration point.
2. **Switch navigation.** § 2.2's page moves on a choice (mod-015 BR-18) are
   this task's to build (mod-015 § 5.7), and mod-015's plan builds none. The
   slice's source will need the current entry and that entry's landing in
   the new context. Both live in `app/navigation`, which a slice may not
   import. The frontend plan (6a) should say how `app/` hands that resolver
   to the source when it registers it. No design change.
3. **Stage 4: «Недостаточно прав».** US-1 refuses a create without the right
   with «Недостаточно прав». The platform's 403 answers `http.forbidden`
   «Доступ запрещён.», so the create endpoint needs this task's own error
   code (design § 15: every refusal has its own code).
4. **Stage 4: the seed seam.** mod-001's `api-contract.md` § 2 gives it as
   `IProcessSeed.SeedAsync(Guid, CancellationToken)`, with two caller
   obligations: call it inside `ExecuteInTransactionAsync`, and call it
   before tracking any change of your own. D-k's "no interface stub" still
   holds: this is mod-001's own interface, not a stand-in.
5. If R-28 takes (a), mod-015 § 5.5's "one team" already covers it, and
   nothing in mod-015 changes.

### Edits made by the reviewer

- `design.md`: the header bullets (R-29), § 2.1 (R-30), § 3.4 (R-31), § 4.4
  (R-32) and § 8.3 (R-33).

`spec.md`, the stories, the prototype and ADR-0026 were not changed.

### Verdict

`needs-changes`. The check found 0 blockers and 2 majors, R-27 and R-28,
both open. Each is a seam with mod-015 that the shell, as designed and
planned, cannot carry under C-4, and each comes with a recommended option.
It also found 5 minors, R-29..R-33, all fixed in place. Every one of the 84
story scenarios has its screen or state, and the canon is complete. The copy
is Russian and verbatim. The stack is MUI core only. ADR-0026 agrees with
the spec, and nothing defines an API. Once R-27 and R-28 are resolved, a
delta check of the sections they touch is enough. `gate-2` is the
orchestrator's to record.

## Design check round 2 (2026-10-02)

Reviewer: the same stance, with a clean context. It wrote none of the fix.
This round is a delta check. It covers the two open majors, R-27 and R-28,
and the fix commit bb596bf. That commit changed `design.md`, the prototype
and a `status.md` Log line. `spec.md`, the stories and ADR-0026 did not
change. The check reread the sections the fix touched: the header, DC-4,
DC-11, § 1, § 2.1, § 2.2, § 3 intro, § 3.2, § 3.4, § 3.7, § 9.7, § 10.2,
§ 12, § 15, § 16 and § 17. It checked them against these sources:

- mod-015 at its tip 5dd37c3. Its `docs/` are unchanged since b6f1d66. The
  sections read were `design.md` § 3.4, § 4.1, § 4.4, § 5.4, § 5.5, § 5.7
  and § 7, `plan-frontend.md` § 4.2, § 4.3, § 4.5, § 9 (D-6, D-7, D-15) and
  § 13, and `spec.md` BR-11, BR-18 and BR-19;
- US-4, US-7 and US-8, and BR-M1-24.

Special checks:

- **Naming (C-3).** The fix's text and the prototype name no reference
  tracker product or vendor.
- **Old route.** `…/teams/:t/settings` survives nowhere: not in `design.md`,
  not in the prototype, not in ADR-0026, and not in mod-015's or mod-001's
  docs. The only copy is R-28's own problem statement above.
- **Prototype.** A jsdom check of the delta, run by the reviewer, passed
  46/46. The trail and the marker on a team's page, and both tabs of
  «Проекты», are derived from the model. The switcher's team is not set by
  the page. A team choice keeps the page. Project and organization choices
  land on «Команды». Each «Команда не найдена» case (a deleted team, a team
  of another project, an unknown id) keeps the settings navigation with
  «Команды» `"true"` and no trail. The old route answers «Страница не
  найдена». The organization- and project-not-found frames have no settings
  navigation. Deleting a team on its own page shows its own notice, or the
  fallback notice when the switcher holds that team. The six per-level
  switcher states draw the levels and copy of § 3.2. The designer's smoke
  check (100/100) is not in the repository, so it was not re-run.

### R-27 (round 2)
Status: resolved
Resolution: DC-11 and § 3.2 now rest the per-level rows on an amendment to
mod-015, option (a). The amendment is specified fully enough to make:
- a status for each level, where the organization level's status is
  today's single status;
- a retry for the failed level;
- the switcher draws from the top and stops at the first level that is not
  ready, which reproduces mod-015 § 5.4's rows when the organization level
  is the one loading or failed;
- the four rows' final copy moves into mod-015 § 5.4.

The table matches that rule, and the prototype draws it. (b) is named as
the fallback if the amendment is declined. R-27 (b) above lists what (b)
would rewrite: § 3.1, § 3.7 and § 15 become one read and one invalidation.
§ 16 tells stage 6 to build against the amended interface. That holds,
since stage 6 starts after mod-015 merges (D-b).

Condition carried to gate-2: the design is complete only together with the
orchestrator's record. Either mod-015 is amended, or (b) is chosen
explicitly. The amendment touches mod-015 `design.md` § 5.4 and § 5.7 and
`plan-frontend.md` § 4.3 with its D-15, and both `status.md` files record
it. It is still cheap. mod-015's tip 5dd37c3 closes its pass 2
(`shared/ui`), and no switcher or context-source code exists on its branch
yet. The amendment should land before its pass 3 (the shell) starts.

### R-28 (round 2)
Status: resolved
Resolution: option (a). A team's page is now at
`/organizations/:o/projects/:p/settings/teams/:t`. That is beneath the
«Команды» landing, so mod-015's shell derives all three things from the
model:
- the settings page (its plan § 4.4, `isSettingsPage`);
- the `"true"` marker (plan § 4.2, `entryMarker`);
- the trail «Настройки / Проект / Команды» (plan § 4.5, D-7).

No shell change and no extra entry field is needed. This is the same
derivation that keeps «Языки» current on `/dictionaries/languages/new`
(mod-015 § 4.4).

The edits are consistent with one another:
- DC-4 is reworded;
- § 1 states the derived trail and that no page passes `ancestors`;
- § 2.1 drops the "also on" column;
- § 2.2 has a team choice keep the page and a project choice open
  «Команды», which is mod-015 BR-18's entity rule, since its § 5.5 lists
  "one team" beside a work item;
- § 3.4 says the page's team is its entity, never the switcher's team;
- § 9.7 picks the notice by whether the switcher holds the team;
- § 10.2 has the page pass no `ancestors`;
- § 12 places the navigation around each frame.

The spec holds:
- US-4 and US-7: the team keeps its own address, which answers «Команда не
  найдена»;
- US-8: "a page that addresses one team … lands on its entry's page";
- BR-M1-24 is unaffected. Its "team-level page" now has no instance in this
  task, and § 1 says so.

The fix also moves the deactivated «Проекты» tab, which is beneath its
landing. Its trail becomes «Настройки / Организация / Проекты», ending in a
link to the active tab, and its marker becomes `"true"`. That follows from
the shell's derivation and § 1 states it. No finding: the alternative would
take the tab out from under its entry and lose the settings navigation.

### R-34
Severity: minor
Affects: DC-4; § 3.4; § 2.1 and § 12 (the navigation they derive)
Problem: DC-4 said that the shell derives a team page's settings navigation,
marker and trail "from the address alone". It derives them from the address
and the switcher's selection: mod-015 builds each landing from the current
context (plan § 4.2, `entryMarker(pathname, entry, context)`). § 3.4 sets
the carried levels only once the page's first read answers. Until then, a
page opened from a link into another organization or project has no
settings navigation, no current entry and no trail. If the read fails, the
error state has none of them either. Then the frame shifts: the main
navigation goes from its kept state to the rail beside «Меню настроек».
This applies to every context page of this task, not only the team's. The
design did not say so, so QA would file the shift as a defect and stage 6
might patch it ad hoc.
Required resolution: State the interval, or close it.
Status: resolved
Resolution: DC-4 now says "once the switcher holds the address's
organization and project". § 3.4 gains a paragraph that states the
interval. A page opened inside the current selection shows the navigation
from its first frame. A page opened outside it shows loading or error as a
page beneath no landing, until the read sets the context. Closing the
interval instead would be a design change, and it is left to the
orchestrator (note 2 below).

### Notes for the orchestrator (round 2)

1. **R-27's condition** (above). Record the mod-015 amendment, or choose
   (b), at gate-2. Under (b), `design.md` § 3.1, § 3.2, § 3.7, § 15 and DC-11
   need the designer's rewrite before stage 6.
2. **R-34's alternative.** The shift can be closed if this task's pages use
   the rule § 3.4 already applies to another slice's page: set the carried
   levels as soon as the level's active list holds the id. The page's read
   would still answer not found. This is optional. The design as written is
   consistent.
3. **The AppLayout seam** (round-1 note 1) is now explicitly the
   orchestrator's call in § 16. D-d still says "no AppLayout edit", and
   mod-015's plan § 13 still names the swap in `AppLayout`. Gate-2 should
   record which one wins.

### Edits made by the reviewer (round 2)

- `design.md`: DC-4's wording and a paragraph in § 3.4 (R-34).

`spec.md`, the stories, the prototype and ADR-0026 were not changed.

### Verdict (round 2)

`approved-by-ai`. Both majors are resolved in the design. R-28 is closed
outright. R-27 is closed on the design side, with one condition for gate-2:
the mod-015 amendment, or an explicit choice of (b). One new minor, R-34,
was fixed in place. DC-1..DC-11 are for gate-2. `gate-2` is the
orchestrator's to record.

## Round 1 — whole task

Stage 8 code review, deep tier, run by a session that wrote none of this.
Per D-z this is the only round: backend and frontend together,
authorization included, with no separate L security pass and no delta
round. The diff is `git diff main...0de1fa4`: 378 files, about 46k lines
added. It was read detached at 0de1fa4 in a separate worktree. Nothing
was built or run, because the verification run happens elsewhere (D-z),
and no defect needed one to confirm it. `git status --short` was empty at
the end. No stand was started and no background process was left running.

### What was read

- **Backend product code**:
  - Every endpoint in `OrganizationEndpoints.cs`.
  - The organization-context filter, its metadata and its extension (D-g).
  - The forbidden-code result handler and its metadata (D-p).
  - `HttpContextCurrentUser.SubjectId`.
  - The `Program.cs` diff, where the demonstration seed is removed (D-h).
  - The process read's filter line.
  - The realm import diff and `PermissionCatalog` (D-i).
  - The domain: `Organization`, `Project`, `Team`, `Area`, `AreaTree`, `TeamDefaultArea`, `EntityName`, `ProjectKey` and `AddressedId`.
  - Every write handler. CreateOrganization, CreateProject, AddTeam, CreateArea, SetAreaOwner, SetTeamDefaultArea, MoveArea, DeleteArea, DeleteTeam, RenameArea and RestoreOrganization/RestoreProject were read in full. For RenameProject, RenameTeam, RenameOrganization, DeactivateOrganization and DeactivateProject, the address, lock and re-check steps were read.
  - The read handlers GetTeam, GetTeams and GetAreaTree.
  - All four repositories, `OrganizationsSaveChanges`, `OrganizationsConstraints` and `Reachability`.
  - The four read models and the `IOrganizationStructure` implementation.
  - The configurations' indexes and checks, the migration's keys and indexes, and `UnitOfWork`.
- **Frontend product code**:
  - `organizationsApi.ts` compared with the contract § 1–§ 5, plus `queryKeys.ts`, `queries.ts` and `mutations.ts` (every invalidation compared with design § 3.7).
  - The context source, `resolveSelection`, `addressContext` and `storage` (D-n, D-o, D-y).
  - The diffs of `ContextSwitcher`, `workContext`, `AppLayout`, `switchTarget`, `routes.tsx` and the navigation model.
  - `refusals`, `notFoundLevel`, `validation` and `areaTree`.
  - `OrganizationListPage`, `OrganizationGeneralPage`, `TeamPage` and the first half of `AreasPage`.
  - `CreateProjectDialog`, `ConfirmAction`, `TeamDialogs` and `DeleteTeamDialog`.
  - The diffs of `errorCodes.ts`, `tokenClaims.ts`, `AuthContext.tsx` and `StateFrame.tsx`.
- **Conventions**: the diffs of `docs/conventions/backend.md` («Soft removal») and `frontend.md` (`TreeRegistry`), and frontend.md § Slice rules. ADR-0026 was read only for its status, which is accepted.

### Findings

No blocker and no major. Five minors follow. Under the loop economy each is
a debt, not a round.

#### F1 (minor): the process screen of a just-deactivated organization still opens from the cache

- **Where**: `src/frontend/src/features/organizations/hooks/mutations.ts:52-66`, together with mod-001's `src/frontend/src/features/process/hooks/useProcessDefinition.ts:18` (`staleTime: Infinity`).
- **What happens**: `useDeactivateOrganization` invalidates the organization and the two lists. It leaves `['process', 'definition', organizationId]` alone. That query never goes stale, and the client keeps its unobserved cache for the default `gcTime` of 5 minutes.
- **How to reproduce**: open «Процесс», then «Общие», then deactivate, which lands on the deactivated list. Press Back twice: «Общие» correctly answers «Организация не найдена», but «Процесс» shows the cached definition and sends no request.
- **What it contradicts**: US-2 lines 52-53, "opening its process screen … answers «Организация не найдена»", and the confirmation's own copy at `OrganizationGeneralPage.tsx:97` («её страницы … перестанут открываться»).
- **Why only minor**: the server half is correct, the screen is read-only, the user saw the same data moments before, and the lapse ends on reload or after `gcTime`.
- **Fix**: export `processDefinitionQueryKey` from `features/process/index.ts`. In the hook's `onSuccess`, call `invalidateQueries({ queryKey: processDefinitionQueryKey(organizationId), refetchType: 'none' })`. Invalidation overrides `staleTime: Infinity` on the next mount. Add one page test: deactivate, then open «Процесс» under the same `QueryClient`, and expect «Организация не найдена».

#### F2 (minor): GetTeams can answer 500 when a default moves and the old default is deleted between its two reads

- **Where**: `src/backend/Mustask.Application/Features/Organizations/GetTeams/GetTeamsHandler.cs:57-69`.
- **What happens**: the handler reads the teams and then the tree, as two statements with no transaction. A team's `DefaultAreaId` comes from the first statement, and `tree.PathOf` resolves it against the second.
- **How it fails**: between the two statements, `PUT …/default-area` moves team T's default from D to E and commits, then `DELETE …/areas/D` commits. D is now a leaf that is nobody's default. `PathOf(D)` then throws `KeyNotFoundException`, which surfaces as a 500. The window is narrow, but the handler's remark at :33-35 ("a default area cannot be removed while its team is active, so every area a listed team names is still in the tree") is wrong for exactly this sequence.
- **Fix**: take each team's default from the tree snapshot (`DefaultOfTeamId == row.Id`), as `GetTeamHandler.cs:64` already does. Skip or re-read a team whose default the snapshot does not hold, and correct the remark.

#### F3 (minor): a project rename leaves the first team's default-area path stale in the cache

- **Where**: `src/frontend/src/features/organizations/hooks/mutations.ts:87-100`.
- **What happens**: `useRenameProject` invalidates the project, the active list and the tree. It does not invalidate `keys.teams` or `keys.teamPages`. The first team's `defaultArea.path` is the root's name, which is the project's name (BR-M1-11, DC-9).
- **Impact**: the switcher keeps its teams query mounted, so that query and any cached team page hold the old path until their next fetch. Pages refetch on mount because `staleTime` is 0, so the stale value is only briefly visible.
- **Fix**: add `keys.teams(organizationId, projectId)` and `keys.teamPages(organizationId, projectId)` to the invalidation.

#### F4 (minor): an address with an upper-case id loads the page but never syncs the switcher

- **Where**: `src/frontend/src/features/organizations/context/addressContext.ts:32,34`, against `resolveSelection.ts:67,82`.
- **What happens**: the address ids are compared with the lists by exact string. The server accepts either letter case (contract § 0.2), so `/organizations/<UPPER-CASE-ID>/settings/projects` renders its page. But `addressSync` answers `none`, so the switcher, the settings navigation and the trail stay on the previous context.
- **Fix**: lower-case the ids in `addressContext`. The lists hold the lower-case form (§ 0.2).

#### F5 (minor): the team page's delete confirmation can name an empty first team

- **Where**: `src/frontend/src/features/organizations/pages/TeamPage.tsx:34,45`.
- **What happens**: `firstTeamName` is `''` until the team list is read, and stays `''` if that read fails. «Удалить команду» is offered as soon as the team itself is read. Opened in that gap, the confirmation reads «Её области перейдут команде «».»
- **Fix**: offer the delete command only once `teams.isSuccess`, or let the dialog wait for the list.

### Tests, sampled per risky rule

Each test below would fail if its rule broke:

- **Immutable key**: `RenameProjectEndpointTests.Rename_BodyAlsoCarryingAKey_RenamesOnly_TheKeyStays` checks both the answer and the stored row. `ProjectKey` uses `\z`, and the database check `ck_projects_key_format` backs it.
- **Deactivated organization hides everything beneath it**: `OrganizationContextFilterTests.EveryOrganizationRoute_DeactivatedOrganization_Returns404…_ChangesNothing` is a theory over every addressed route but the restore.
- **The filter is fail-closed**: `OrganizationContextInventoryTests` checks both directions, plus the exact route set and restore as the only route that accepts a deactivated organization. A new addressed route without the filter turns it red.
- **Team-deletion cascade and its race**: `DeleteTeamEndpointTests.Delete_ConcurrentWithReownToIt_*` covers both orders. Commit gates hold the transactions, and `PgLocks` proves the second request is blocked on the first, so the race is real rather than sequential.
- **Area-tree invariants**: `MoveAreaEndpointTests.Move_TwoConcurrentFormingACycle_OneMoved_OtherRefused_BothOrders` and `Move_UnderItselfOrADescendant_*`.
- **Uniqueness under races**: the sibling-name and move-against-create cases in both orders. The project-name race mapping is pinned by Inf `OrganizationsSaveChangesTests`.
- **Cross-organization IDOR**: the `*_ProjectNotOfOrganization_Returns404ProjectNotFound("of another organization")` theories assert the other organization's rows are unchanged.
- **The create right**: `ForbiddenErrorCodeTests` (own code; another permission endpoint still answers `http.forbidden`) and `CreateOrganizationEndpointTests` (grant and revoke). `OrganizationCreateRightRealmTests` checks the realm import.

### Security (item 3)

- **Guard on every endpoint**:
  - All 26 routes carry `AuthenticatedOnly`, except `POST /api/organizations`, which carries `RequiresPermission(organizations.organization.create)` plus its own 403 code.
  - The 401 still comes first, because the filter runs after authorization.
  - D-a's interim openness is the accepted risk, as recorded.
- **IDOR**: every read and lock is scoped by its container:
  - projects by `organization_id`;
  - teams and areas by `project_id`;
  - body references (`ownerTeamId`, `parentId`, `areaId`) resolved inside the addressed project.
  - The composite keys of D-w back this in the database.
- **Injection**: raw SQL is interpolated parameters only, including `ANY({ids})`, and the lock modes are fixed statements.
- **Secrets**: none in code or in E2E support. E2E gets its token through `POST /api/session/token`.
- **What leaks**: error bodies carry only catalog texts and the names of the user's own objects. The seed failure's inner exception goes to the log only (`OrganizationCreateFailedException`, D-t). `localStorage` holds ids under the `sub`, which is a UUID.

### Architecture (item 4)

- **Layering**: Domain, then Application abstractions, then Infrastructure. Api depends on Application only, and the filter resolves `IOrganizationReadModel` from Application.
- **Seam**: the cross-module seam `IProcessSeed` is mod-001's module-root interface (D-k).
- **Frontend boundaries**: `features/organizations` imports neither `app/` nor any other slice's internals. Its one cross-slice import, `processDefinitionPath`, goes through `features/process`'s index. `app/` reaches the slice through its barrel, and no `app/theme` file changed (C-4, D-o).
- **Decisions without an ADR**: none found. The forbidden-code handler is D-p, and soft removal is ADR-0026 plus the backend.md section.

### Behavioural claims (item 6, reduced per the task brief)

- **`api-contract.md`**: these were checked against the code, and none is contradicted:
  - the `Location` headers (§ 1.3, § 2.3, § 3.3, § 4.2);
  - lower-case ids (§ 0.2);
  - list order (§ 0.6, § 3.1);
  - the order of answers in the four area writes and in SetTeamDefaultArea (§ 0.4);
  - the restore no-ops (§ 1.7, § 2.7);
  - the deleted-team seam answer (§ 7);
  - the registrations (§ 8);
  - the process read behind the filter, including an orphan process id (§ 9).
- **`src/backend/AGENTS.md`**: the new paragraph is anchored. The group «Администраторы» maps realm role `admin`, which carries the code (realm json :111-133), and «Создатели организаций» maps `organization-creator`.
- **`src/frontend/AGENTS.md`**: untouched by the diff. A grep for `demonstration`, `placeholder` and the old id finds nothing.
- **Status Log**: "the filter on all 24 organization-addressed routes" matches `OrganizationAddressedRoutes.All` (23 plus the process read). "23 error codes" matches `organizationErrorCodes`.
- **Direction 2**: the demonstration seed's removal was grepped (`DemonstrationProcessSeed`, `DemonstrationSeed`, `d0000000-0000-4000-8000-000000000001`). The only non-test hits are mod-001's merged artifacts. Its api-contract § 4 already states "`mod-002` removes the stand's seed … (mod-002 D-h)", so there is no finding.
- **Not swept**: the prose of design.md, the plan files and the stories, beyond US-2 for F1.

### Not read: named gaps

- **Frontend**:
  - `shared/ui/TreeRegistry.tsx` (the treegrid keyboard model);
  - `AreaDialogs.tsx` and `DefaultAreaDialog.tsx` beyond their picker helpers;
  - the second half of `AreasPage.tsx`;
  - `ProjectListPage`, `ProjectGeneralPage`, `TeamsPage`, `OrganizationsSection`, `useRestore`, `notice.ts`, `NameDialog` and `RestoreButton`;
  - the shared-pattern prop additions (`FormDialog`, `ConfirmDialog`, `PageHeader`), read in diff stat only;
  - `ContextSwitcher` beyond a skim of its diff.
- **Backend**:
  - GetOrganization(s), GetDeactivated*, and GetProject(s) handlers, read only through their read models;
  - Application and Infrastructure DI registration;
  - the generated migration Designer and model snapshot.
- **Tests**: the Application handler tests (in-memory fakes), every frontend test file (sampled by name only), and E2E specs 19-21 with their support.
- **Naming**: no check for the reference tracker's name was run in this round.

### Verdict

**approve.** 0 blockers and 0 majors are open. F1-F5 are minor debts for
the gate-3 summary.
