# Review: mod-001-process-seed

## Round 1 — requirements

**Verdict: needs-human-decision.** All 11 findings are minor, and every one is
fixed in place. The spec leaves two questions open (§ Open questions), and
only the human can answer them: whether the development stand gets
demonstration data, and which other line's scope gains the way into the
screen. Both are listed at the end of this round.

Inputs checked: spec.md, US-1 and US-2 against doc-007 (§ Roles, § "MVP: the
seeded process", BR-M2-1..9, § Operation completeness, § Out of scope, its
mod-001 row, US-1 and US-2, status.md P-3..P-5 and R-4..R-7); doc-010
(§ Permission codes, § "Critical path and parallelism", E-1, PR-1, PR-2,
PR-4, PR-8, § ADR debt, US-1, and D-4 and D-5 in status.md); doc-006 (§ Roles
constraints 1, 6 and 7, BR-M1-3, BR-M1-8, the lifecycle BPMN); doc-001 (BR-2,
BR-5, BR-6, US-3); the queue lines of mod-002, mod-016, mod-015 and tech-001;
ADR-0018, and the realm-import contract tests it names.

Checklist results:

- **"Open for spec" points (status.md).** Both are closed, each by a rule and
  a scenario. First, the guard and the not-found answer before membership
  exists: BR-M2-10 with the US-2 scenarios "Every signed-in user sees the
  same definition", "A visitor who is not signed in sees nothing" and (added
  here, R-1) "The read's code is registered…", plus BR-M2-11 with "An
  identifier with no process is not found". Second, how the screen is
  reached: BR-M2-12 with "The screen is reached by its address, not through
  a menu".
- **D-25 correction (orchestrator note).** mod-016 is named as the line for
  membership and narrowing everywhere, and no text credits either to mod-002.
  The interim authenticated-only read matches both the mod-002 queue line
  ("every endpoint under the authenticated-only guard until mod-016
  retrofits") and the mod-016 queue line ("the retrofit … onto every
  endpoint that exists when it merges").
- **C-1.** No mention of the reference tracker in spec.md, the stories or
  this file, the edits of this round included.
- **No implementation designed.** There are no tables and no endpoints. The
  spec does use "authenticated-only guard", "realm import", "token" and
  "published API description", but these are existing platform vocabulary
  and doc-006 constraint 1 binds them.
- **Size (checklist item 5).** The line is L, and L is right, because the
  line founds M2 (doc-010 PR-1, PR-2); the decomposition row now says so
  (R-9). The reason for not splitting holds under "Per-story delivery".
  US-1 on its own is exactly that rule's counter-example: a capability with
  nothing calling it until mod-002 merges. So it stays a `shippable: no`
  story next to US-2, which uses its data. US-2 cannot be verified without
  US-1's data. Line budgets: spec.md 182, US-1 92, US-2 96.

### Rule → story mapping

| Rule / source item | Covered by | Note |
|---|---|---|
| BR-M2-1: one process per identifier, its own, seed is the only way, identity handed back | US-1 "A new organization identifier receives…", "Each organization identifier gets a process of its own", "No person can seed or change a process" | the caller recording the process: out of scope (mod-002) |
| BR-M2-2: ordered states, first is initial | US-1 "A new organization identifier…"; US-2 "A seeded process is shown in full" | |
| BR-M2-4: one level per type, rank, kind | US-1 "A new organization identifier…"; US-2 "A seeded process is shown in full" | |
| BR-M2-8: all-or-nothing, synchronous, discarded with the caller's failed operation | US-1 "A failed seed leaves nothing behind…", "A caller's failed operation takes the seed with it" | the caller's own obligation: out of scope (mod-002, doc-006 BR-M1-3) |
| BR-M2-9: stable identity, key, idempotency, retry and race, well-formed key | US-1 "A new organization identifier…" (keys; identity with equal labels, R-4), "A repeated call…", "Two racing calls…", "A malformed identifier is refused…" (R-2) | |
| BR-M2-10: code registered and not in the realm, authenticated-only read, visitor refused | US-2 "Every signed-in user sees the same definition", "A visitor who is not signed in…", "The read's code is registered…" (R-1) | narrowing: out of scope (mod-016) |
| BR-M2-11: «Организация не найдена» for unknown or malformed identifiers | US-2 "An identifier with no process is not found", "A failed read is not taken for a missing organization" (R-5) | non-member: out of scope (mod-016); deactivated organization: out of scope (R-6) |
| BR-M2-12: own address, no entry, current layout | US-2 "A seeded process is shown in full" (layout), "The screen is reached by its address…" | the link: open question 2 |
| BR-M2-13: same content for every reader; no control or request changes a process | US-2 "A seeded process is shown in full", "Categories are shown…", "One state label…", "Nothing on the screen changes the process"; US-1 "No person can seed or change a process" | |
| doc-007 P-5: icon and colour | US-1 "A new organization identifier…" (R-3); US-2 "A seeded process is shown in full" | |
| doc-010 D-4, D-5: states, category labels | US-1 "A new organization identifier…"; US-2 "Categories are shown…", "One state label…" | |
| doc-007 US-1 "Every project … uses the same process" | out of scope (mod-002, doc-006 BR-M1-3) | |
| doc-007 US-2 "from one of its projects"; doc-001 US-3; doc-010 US-1 MS-1 "its process screen" | open question 2 | |
| doc-007 US-2 non-member; doc-001 BR-5; doc-006 BR-M1-8 | out of scope (mod-016's retrofit, D-25) | |
| doc-007 R-7 (transitions, delete/restore) | out of scope (mod-003, mod-005) | |
| doc-007 BR-M2-5..7, release 3, `process.definition.write` | out of scope | |
| doc-007 category table, "Board column" | out of scope (mod-006) (R-8) | |

### R-1
Severity: minor
Affects: BR-M2-10, US-2
Problem: BR-M2-10 makes this task register `process.definition.read` "under that exact name as an organization-scoped code", declared in no realm import, granted by no role or group and carried in no token. No scenario checks any of this, and it is the one part of the queue line ("registers process.definition.read") that nothing else verifies. It also collides with existing code. ADR-0018's realm-import contract test (`KeycloakRealmRolesTests`) requires the `mustask-api` client's roles to equal `PermissionCatalog.AllCodes` in both directions. So the simplest implementation, adding the code to the catalog, fails that test. A developer then either declares the code in the realm, which breaks BR-M2-10 and doc-006 constraint 1, or restates the test, which is work owned by mod-016's amendment (doc-010 § ADR debt, PR-4).
Required resolution: add a scenario that verifies the registration and its absence from the realm, without choosing where the code is registered.
Status: resolved
Resolution: added the US-2 scenario "The read's code is registered, and nothing grants it yet". It is satisfied by either registration shape, and the realm half fails if the code is declared in the realm import. For stages 3–4 see Note 1 below.

### R-2
Severity: minor
Affects: BR-M2-9, BR-M2-11, US-1
Problem: the seed command's contract did not cover an empty or malformed identifier. That is the "empty" edge case on the command, and as written the command could create a process keyed on an empty value. The key was also described two ways. BR-M2-9 said M2 "gives it no other meaning", yet BR-M2-11 answers "a value that is not a well-formed identifier", which requires a format, and the read finds the process by that key (BR-M2-12). doc-007 R-5 keeps the key "only … for idempotency", so this task's second use of it went unstated.
Required resolution: state that the key also addresses the read, that what "well-formed" means is part of the command's contract, and that a malformed call fails and creates nothing. Add a scenario.
Status: resolved
Resolution: BR-M2-9 now says the key is "what the seed is idempotent on and what a read finds the process by (BR-M2-12)". It also says the well-formed format is part of the command's contract, fixed at design, and that a malformed or empty value fails and creates nothing (BR-M2-8). Added the US-1 scenario "A malformed identifier is refused and nothing is created".

### R-3
Severity: minor
Affects: US-1 "A new organization identifier receives the complete seeded process", doc-007 P-5
Problem: "each type carries an icon and a colour set by the seed" would pass if all five types had the same icon and colour, and the reason icon and colour exist is to tell the types apart. No document gives the values.
Required resolution: make the criterion check something (checklist item 2: rewrite a criterion that cannot be tested).
Status: resolved
Resolution: the criterion now reads "no two types sharing an icon and no two sharing a colour". The values are chosen at stage 3 (Note 4).

### R-4
Severity: minor
Affects: BR-M2-9, US-1 "A new organization identifier receives the complete seeded process"
Problem: "the identity does not depend on the display name" had no test. MVP renames nothing, so no scenario could ever tell an identity apart from a name. MVP does have one observable case: equal labels on different types. Every type has «Новая», and «Разрешена» is InProgress on История and Resolved on Баг (doc-010 D-4). A name-keyed seed would merge them.
Required resolution: a criterion that fails when identity follows the label.
Status: resolved
Resolution: added to the first US-1 scenario: "no two types, states or backlog levels share an identity, even where their labels are equal: each type's «Новая» is a state of its own, and so is «Разрешена» on «Пользовательская история» and on «Баг»".

### R-5
Severity: minor
Affects: BR-M2-11, US-2 "An identifier with no process is not found"
Problem: the not-found scenario checked only what the screen shows, not that the answer to the request discloses nothing, which is what BR-M2-11 promises. Nothing covered a read that fails, the "integration failure mid-process" edge case. A screen that turns every failed request into «Организация не найдена» would tell a user that an existing organization does not exist.
Required resolution: check the request's answer, and add a scenario that separates a failed read from a missing process.
Status: resolved
Resolution: the not-found scenario now also requires that no type, state or backlog level is in the answer to the request. Added the US-2 scenario "A failed read is not taken for a missing organization": an error with retry, neither «Организация не найдена» nor any partial process. This is the ADR-0007 screen-state canon, made specific to this rule.

### R-6
Severity: minor
Affects: BR-M2-10, BR-M2-11, § Out of scope
Problem: once mod-002 merges, an organization can be deactivated, and doc-006's lifecycle answers a stale link with "not found". M2 holds no organization status (C-3), so this read keeps showing a deactivated organization's process to any signed-in user until mod-016's check reaches it. The spec did not say this. BR-M2-11's inference "no process means no organization" does not work in reverse either: a process does not mean a visible organization.
Required resolution: state the gap and the rule's owner, so neither design nor QA takes it for a defect of this task.
Status: resolved
Resolution: added an out-of-scope bullet, "Hiding a deactivated organization's process". It states that the process stays readable by identifier until mod-016's check reaches the read, and that the rule is M1's. The exposure is the same kind D-25 already accepts for the interim.

### R-7
Severity: minor
Affects: § Roles
Problem: § Roles opened with "Reading is guarded by `process.definition.read`" and then said the read runs under the authenticated-only guard. In this task nothing checks the code, so the first sentence was false as written.
Required resolution: say that the code exists and is not yet checked.
Status: resolved
Resolution: reworded: "The code for reading is `process.definition.read` … Nothing checks it until `mod-016-organization-access` retrofits the organization-scoped codes: until then the read runs under the authenticated-only guard (BR-M2-10)."

### R-8
Severity: minor
Affects: § Out of scope; doc-007 category table; status.md C-3
Problem: the spec implements doc-007 § "MVP: the seeded process" "as written", and that section's category table has a "Board column" column. Design could read this as data the seed must store, when it is M4's per-(type, state) mapping (doc-010 D-4, D-5). There was also nothing to stop design from putting the organization's name on the screen, which needs a read from M1 and would break C-3 and doc-007 R-5.
Required resolution: exclude both explicitly.
Status: resolved
Resolution: added two out-of-scope bullets. Board columns, filters, queries and metrics belong to their own lines, and the seed stores the category, not a board column. The organization's name is not shown, because M2 holds only the identifier.

### R-9
Severity: minor
Affects: § Decomposition for the backlog
Problem: checklist item 5 expects lines of M or less. The row explained why the line is not split but not why it is allowed to be L.
Required resolution: cite the founder rule.
Status: resolved
Resolution: the row now ends "It is L because it founds M2 (doc-010 PR-1, PR-2)." The Per-story delivery judgement is in the checklist results above.

### R-10
Severity: minor
Affects: § Business rules, BR-M2-10..13
Problem: this task adds BR-M2-10..13 to the M2 rule numbering, which doc-007 started. A later M2 analysis, such as the release-3 process editor, could reuse BR-M2-10 for a different rule. That is the collision doc-010 N-32 set out to remove.
Required resolution: fix the next free number and the citation form.
Status: resolved
Resolution: the § Business rules preamble now says that these rules continue doc-007's numbering, that the next M2 rule is BR-M2-14 whichever spec adds it, and that other specs cite these as "mod-001 BR-M2-10".

### R-11
Severity: minor
Affects: § Open questions (first), BR-M2-1, § Roles
Problem: option (a) of the first open question (a stand seed for a demonstration identifier) did not say what it costs. It makes the stand a second caller of the command, and BR-M2-1 and § Roles name mod-002 as the only caller. It also creates a process that belongs to no organization. A human who approves (a) at gate-1 would be approving a spec change nobody had written.
Required resolution: state the consequences inside the option.
Status: resolved
Resolution: option (a) now says the stand seeds through the command, which is idempotent, so every start is safe. It also says the process belongs to no organization, and that choosing (a) adds the stand as a caller to BR-M2-1 and § Roles plus one scenario. The question itself stays open for the human (Q1). If (a) is chosen, that delta goes to round 2.

### Notes for stages 3–4 (not findings)

1. **Registering the code without pre-empting mod-016.** BR-M2-10 can be met
   without touching the ADR-0018 amendment: register the code outside the
   realm-bound catalog and leave the realm contract tests as they are. The
   other shape is constraint 1's end state: a scope marker on
   `PermissionCatalog`, with the realm test restated to compare
   instance-level codes only. That shape is the amendment's (doc-010 § ADR
   debt: "No ADR is owed by … mod-001"; PR-4), so choosing it goes to the
   human, as the analyst's note in status.md says.
2. **The identifier's format** is part of the command's contract with mod-002
   (R-2). The reference slice keys entities by `Guid`. mod-002 builds
   against this contract if it starts alongside this task.
3. **«Организация не найдена»** must be one answer: mod-016's non-member case
   on this read must be impossible to tell apart from it (BR-M2-11). M1's own
   not-found answers (doc-010 US-1, "Guard") are best served by the same
   error code. mod-001 registers it first, in `docs/error-codes.md` and
   `contracts/errors/`. Its name and namespace are stage 4's to choose.
4. **Icon and colour values** are stored per process as seeded data. If
   mod-015's theme later restyles them, every seeded process needs a data
   migration, unless the seed stores something the theme renders. That is a
   design choice for gate-2.
5. **The seed shares its caller's unit of work.** The US-1 scenario "A
   caller's failed operation takes the seed with it" cannot pass otherwise.

### Human-only questions (gate-1)

- **Q1 (open question 1): can anyone see the screen before organizations
  exist?**
  (a) Recommended: the development stand seeds one process for a fixed,
  documented demonstration identifier at startup, and no other environment
  does. For: gate-3's by-hand check and an E2E run have something to open.
  Against: a process that belongs to no organization exists on dev stands,
  and a small spec delta follows (R-11).
  (b) No stand data. For: no stand-only code path, and no orphan. Against:
  until mod-002 merges, the screen is verified by automated tests only, and
  gate-3 cannot show it by hand.
- **Q2 (open question 2): which line adds the way into the screen from an
  organization or a project?**
  (a) Recommended: mod-002-organizations-projects-teams. For: it is the
  first line with those surfaces, it already follows this task, and MS-1's
  demo needs the link. Against: mod-002's scope grows by a link.
  (b) mod-015-app-shell-design-system. For: it owns navigation (C-2).
  Against: it does not depend on mod-002, so it has no organization to link
  from unless it is re-sequenced after mod-002.
  Either way, the chosen line's queue text is reworded in `main` (doc-010
  PR-8).

## Round 2 — delta check (2026-10-01)

**Verdict: needs-changes.** One major finding is open, R-12. This spec and
mod-015 decide differently who registers the «Процесс» entry and who moves
the screen onto the theme. If mod-015 merges while this task's frontend stage
is running, both jobs fall to nobody. Six minor findings (R-13..R-18) are
fixed in place. No new human-only question: R-12's recommended fix is
mod-015 D-12, a decision the human already took.

Inputs checked (delta only, per the stage's round-2 rule):

- status.md D-4..D-12 and the analyst's round-2 Log line;
- `git diff 65aa732 d5228cd` on spec.md and the stories;
- the sibling specs at their branch heads:
  - mod-002: status D-a..D-l; spec BR-M1-3, BR-M1-19, BR-M1-20, BR-M1-23,
    BR-M1-24, NFR "Seam with mod-001"; US-2;
  - mod-015: status D-4, D-6, D-12, D-15..D-22; spec § Navigation model,
    BR-1, BR-4, BR-8, BR-12, BR-15, BR-18, BR-19, § Decomposition; US-1;
    US-11 "Parallel modules";
  - mod-014: status D-10..D-12; NFR Stand;
- mod-016's queue line in `main`;
- doc-007 § "Logical model", § "MVP: the seeded process" and § "Out of
  scope";
- `contracts/errors/` and ADR-0002;
- the integration-test host, `MustaskApiFactory`.

### Decisions → where the spec carries them

| Decision | Carried by | Check |
|---|---|---|
| D-4 stand seed | § Context, § Roles, BR-M2-1, BPMN "Stand", § Operation completeness, § Out of scope, US-2 stand scenarios | holds after R-15, R-16, R-17 |
| D-5 the way in | BR-M2-12, § Out of scope, § Open questions, US-2 "No visible entry or link…" | R-12 (open), R-18 |
| D-6 token, category code | § Business rules preamble, US-1 token and category-code criteria, BR-M2-13, NFR Layout, Appearance, Language, § Out of scope, § Decomposition, US-2 | R-12 (open), R-13, R-14 |
| D-7 not-found code | BR-M2-11, BPMN R5, NFR Language, US-2 "An identifier with no process…", "The not-found code is registered…" | holds; the catalog shape (`module`, `ru`, `kk`) fits `contracts/errors/` |
| D-8 registry | § Roles, BR-M2-10, § Out of scope, US-2 "The read's code is registered…" | holds; mod-016's queue line in `main` carries the fold |
| D-9 address | BR-M2-12 (address, untrusted id), BPMN R1, US-2 scenarios open the address | holds; the API path is api-contract's; Note 2 |
| D-10 seam | BR-M2-9 (cites D-10), BR-M2-8, US-1 "A malformed identifier…", "A caller's failed operation…" | holds; Note 3 |
| D-11 deactivated organization | § Out of scope, BR-M2-10 | holds after R-17 |
| D-12 E2E, no ADR | § Open questions | nothing owed by the spec |

### Sibling specs

- **mod-002.** The seam is consistent:
  - BR-M1-3 matches BR-M2-8 and BR-M2-9;
  - D-k repeats D-10's signature and reuses D-7's code;
  - D-g and BR-M1-20 match D-11;
  - D-c and BR-M1-23 match BR-M2-12's link;
  - D-h matches BR-M2-1's removal;
  - D-i puts its own create code into `PermissionCatalog`, which does not
    collide with D-8's separate registry;
  - mod-002 US-2's not-found scenarios on the process screen give the same
    answer as BR-M2-11.
- **mod-015.** These are consistent:
  - D-6, D-16 and BR-4 (token set, category codes) match D-6;
  - D-15 and BR-12 (named not-found frame) match BR-M2-11;
  - D-17 and BR-18 match D-9's address and untyped id;
  - D-18 and BR-8 match the hidden entry in BR-M2-12;
  - the «Процесс» row of § Navigation model matches BR-M2-12.

  Two points are not consistent: D-12, BR-15 and US-11 against D-5 and D-6
  (R-12), and BR-4's category colour (R-14).
- **mod-014.** No shared surface. D-10's stand reset leaves an unmigrated
  database, and BR-M2-1's skip path covers that. D-11 ("no
  Features/Organizations") is untouched: this task adds only the
  organizations error catalog, not a code folder.
- **mod-016** (queue line): it folds the registry, retrofits the read and
  reuses the not-found code, which matches D-7, D-8 and BR-M2-10.

### Previously open items

- R-1..R-11 stay resolved.
- R-3's resolution is superseded by D-6. The criterion now names each
  type's token and still fails on a shared one.
- R-6's resolution is superseded by D-11. mod-002's filter hides a
  deactivated organization's process, not mod-016's check.
- Q1 is closed by D-4, and Q2 by D-5.
- Round-1 Notes 1–5 are closed by D-8, D-10, D-7, D-6 and D-10.
- C-1 holds: a case-insensitive search of this folder for the reference
  tracker's product, vendor and abbreviation finds nothing.
- Line budgets: spec.md 218, US-1 97, US-2 144. The size stays L.

### Mapping delta (rows changed since round 1)

| Rule / source item | Covered by |
|---|---|
| BR-M2-1: the Development stand as caller | US-2 "The Development stand shows a demonstration process", "An unmigrated Development stand…", "A failed demonstration seed does not stop the stand" (R-15) |
| BR-M2-11: the error code and its registration | US-2 "An identifier with no process is not found", "The not-found code is registered with its texts" |
| BR-M2-12: address; the link is mod-002's; registry entry | US-2 "A seeded process is shown in full", "No visible entry or link…" (R-18); the link: out of scope (mod-002 D-c) |
| BR-M2-13 and D-6: token, category colour | US-1 "A new organization identifier…"; US-2 "A seeded process is shown in full" (R-14), "Categories are shown by their Russian labels" |
| doc-007 P-5 icon and colour | replaced by the token (D-6); doc-007 amended per R-13 |

### R-12
Severity: major
Affects: BR-M2-12, BR-M2-13, NFR Layout, § Out of scope (the navigation exception), § Open questions (second item), US-2 "A seeded process is shown in full"; status.md D-5, D-6; mod-015 D-12, D-16, BR-15, US-11 "Parallel modules", § Decomposition
Problem: The two specs divide the shell work by different events.

mod-015 divides it by merge order. Whichever of the two tasks merges
second registers mod-001's «Процесс» entry (D-12, BR-15). The follow-up
line that moves the screen onto the theme lookup and the canon frames is
filed only if mod-001 is already merged when mod-015 merges. Otherwise
US-11 leaves that adoption "to mod-001".

This spec divides it by the start of its own frontend stage (D-5, D-6). It
registers the entry and adopts the theme and the frames only if mod-015 is
in `main` before that stage (BR-M2-13, NFR Layout, US-2) or "by" it
(BR-M2-12, § Out of scope, § Open questions). Otherwise it leaves the
adoption to "mod-015's follow-up adoption line".

Take the case where mod-015 merges after this task's stage 6 starts and
before this task merges. mod-015 expects this task to register the entry
and adopt the theme. This task does neither, and the follow-up line is
never filed. The screen ships with no navigation entry, stays off the
theme, and no line owns either. That window covers this task's stages 6–8
and gate-3, while mod-015 has not passed gate-1 and still has its design,
its prototype and its frontend ahead. The case is likely. Inside this spec,
"by" and "before" also disagree when mod-015 lands during stage 6.
Required resolution: The orchestrator records one rule that both specs share
as an amendment of D-5 and D-6, and it is applied to every place listed
under Affects. Both options stay within recorded decisions: (a) is mod-015
D-12, and (b) only extends mod-015 D-16 to the uncovered case.

(a) Recommended: mod-015 D-12's merge-order rule. If mod-015 is in `main`
before this task merges, this task merges second. It then registers the
entry and wires the token, the category colour (R-14) and the canon frames.
If mod-015 lands after stage 6 has started, this task rebases onto it before
stage 8. If this task merges first, mod-015 registers the entry and the
follow-up line adopts the screen.
For: no edit to mod-015, no extra queue line, and mod-015 US-11 already
leaves the adoption to mod-001.
Against: if mod-015 lands late, one finished read-only screen is reworked
before code review.

(b) Keep the stage-6 cut-off, and have the orchestrator amend mod-015
BR-15, § Decomposition and US-11. The follow-up line is then filed whenever
this task merges without the adoption, and this task, if it merges second,
still registers the entry.
For: this task's frontend is final once stage 6 starts.
Against: it reopens another task's spec, and it adds a queue line for a
window this task can cover itself.
Status: resolved
Resolution: (a), delegated 2026-10-01: status.md D-5 and D-6 are amended
in place. BR-M2-12 states the merge-order rule. When this task merges
second, it names three timings: the frontend stage's start, a mod-015 that
lands during the stage, and a mod-015 that lands after code review has begun,
in which case frontend QA and code review run again. When this task merges
first, mod-015 registers the entry on its own rebase, and the follow-up
adoption line moves the screen onto the theme and the canon frames.
BR-M2-13, NFR Layout, § Out of scope, § Open questions and US-2 "A seeded
process is shown in full" cite the rule. mod-015 states the same rule at
cdc5318 (its R-29: BR-15, § Decomposition, US-11). Closed in round 3.

### R-13
Severity: minor
Affects: § Business rules preamble, § Decomposition; doc-007 § "Logical model", § "MVP: the seeded process", § "Out of scope"
Problem: D-6 replaces doc-007's icon and colour with one appearance token,
but the spec carried that change into doc-007 § "Logical model" only. doc-007
also states the built-in types' icon and colour in two other places:
§ "MVP: the seeded process", whose screen lists "fixed icon/colour" and which
this spec implements "as written", and § "Out of scope" ("Editing a type's
icon or colour in MVP"). With only one section amended, doc-007 would
contradict itself and this spec. doc-010 PR-7 also requires a Log line in the
amended task's status.md, and the spec did not name it.
Required resolution: name every doc-007 section the amendment touches, and
the Log line.
Status: resolved
Resolution: The preamble now carries the change "wherever doc-007 states the
built-in types' icon and colour (§ "Logical model", § "MVP: the seeded
process", § "Out of scope"), with a Log line in doc-007's status.md". The
decomposition row says the same. doc-007's release-3 question about custom
types' icon and colour is not about the built-in types, so it stays.

### R-14
Severity: minor
Affects: BR-M2-13, NFR Layout, US-2 "A seeded process is shown in full"; mod-015 BR-4, D-6
Problem: mod-015 BR-4 gives each state category one theme colour, keyed on
the category code, and says "a state shows its category's colour". mod-015 D-6
gave this task the category-code storage for that purpose. Yet this spec's
theme branch drew only the type's token through `app/theme`. This screen is
the first to show states, and it would have been the one place where a state
lacks its category's colour, with no criterion to catch it.
Required resolution: on the theme, each state shows its category colour,
looked up by the code, next to its label. Colour is never the only carrier.
Status: resolved
Resolution: BR-M2-13 and NFR Layout now say this for the theme branch, and
"with no category colour" for the plain-text branch. US-2's first scenario
gained the criterion. The Russian label stays, so colour never carries the
category alone.

### R-15
Severity: minor
Affects: BR-M2-1, BPMN (S6), US-2
Problem: D-4 covers an unmigrated schema, but not a stand seed that fails
for any other reason, such as an unreachable database or a storage error
partway through. BPMN S6 named only mod-002's reaction. A test of a failing
stand seed could not tell whether the start must fail or go on. This is
checklist item 3's integration failure mid-process, on the second caller.
Required resolution: state the stand's reaction.
Status: resolved
Resolution: BR-M2-1 now says: "If the seed fails for any other reason,
nothing of a process remains (BR-M2-8), and the stand logs the failure and
starts without the process; its next start tries again." This extends D-4's
skip-and-start to every failure, because the hook is a convenience and must
not block the stand. S6 says the same, and US-2 gained "A failed
demonstration seed does not stop the stand". If the orchestrator would rather
have the start fail loudly, only these three places change.

### R-16
Severity: minor
Affects: BR-M2-1, US-2 "The Development stand shows a demonstration process"
Problem: "A fixed, documented demonstration organization identifier" and
"that the stand's documentation names" gave QA no place to look, so the
"documented" half of D-4 could not be checked.
Required resolution: name the document.
Status: resolved
Resolution: both now name `src/backend/AGENTS.md`, which holds the stand's
commands (AGENTS.md § Commands).

### R-17
Severity: minor
Affects: § Context and goal, BR-M2-10, § Out of scope (deactivated organization), § Decomposition, US-1 header
Problem: After D-4 and D-11, four statements were stale:

1. The context called the stand the command's "other" caller "until then",
   which reads as until mod-016. Before mod-002 the stand is the only
   caller, and mod-002 removes it (D-4, mod-002 D-h).
2. The decomposition row and US-1's `shippable` line still said nothing
   calls the command until mod-002 merges.
3. BR-M2-10 said that until mod-016 every signed-in user reads the process
   of any identifier that has one. That stops being true once mod-002's
   filter answers not found for a deactivated or unknown organization,
   including the demonstration identifier (D-11, mod-002 D-g, BR-M1-20).
4. The out-of-scope line said a deactivated organization's process stays
   readable until mod-002 merges. Deactivation arrives with that same line,
   so that window never exists.

Required resolution: align the wording with D-4 and D-11.
Status: resolved
Resolution:

1. The context now reads "Until `mod-002` creates real organizations, the
   command's only caller is the Development stand … and `mod-002` removes
   it".
2. The decomposition row and the US-1 header name the stand's seed as the
   only caller before mod-002.
3. BR-M2-10 gained "except where `mod-002`'s organization-context filter
   answers not found once `mod-002` merges (§ Out of scope)".
4. The out-of-scope bullet now says no deactivated organization's process
   is ever readable. It adds that the same filter answers not found for the
   demonstration identifier (mod-002 BR-M1-20).

### R-18
Severity: minor
Affects: BR-M2-12, US-2 "No visible entry or link leads to the screen before mod-002"
Problem: BR-M2-12's exception, under which this task registers the «Процесс»
entry in mod-015's navigation registry, had no criterion. The scenario
checked only that nothing visible leads to the screen. A missing entry passes
that check as well as a hidden one. For the case where mod-015 registers the
same entry, its US-11 checks it "in the navigation model, not on screen".
Required resolution: add a criterion that fails when the registration is
missing. It must not restate the condition, because R-12 may change it.
Status: resolved
Resolution: The scenario gained: "where this task registers the «Процесс»
entry (BR-M2-12), the navigation model holds it under «Настройки» with the
code `process.definition.read`, hidden while the switcher reads placeholder
data, so it is checked in the navigation model, not on screen".

### Notes for stages 3–6 (not findings)

1. **The integration-test host runs in Development.**
   `MustaskApiFactory` calls `UseEnvironment("Development")`, so the stand's
   seed runs in every integration-test host. The backend plan should do
   one of two things: let tests switch the hook off, or have any test that
   counts processes allow for the demonstration one. US-2's criterion for
   environments other than Development runs under `Production`, as the
   existing production tests do.
2. **The page guard before mod-015.** Suppose mod-015 is not in `main` at
   stage 6. Then the panel's only page guard is the dictionaries
   `RequirePermission`, which checks the codes in the token.
   `process.definition.read` is in no token, so guarding the page with it
   refuses everyone, and US-2 "Every signed-in user sees the same
   definition" fails. D-9's "the page declares its code; mod-015's interim
   rule lets every signed-in user in" assumes mod-015 is in `main`. Without
   it, the page relies on sign-in alone.
3. **Where "nothing of a process remains" comes from.** Under D-10 the seed
   never commits. After a failure, nothing remains only through one of two
   mechanisms: the seed rolls back a savepoint of its own, or the caller
   abandons its unit of work (BR-M2-8). The stand's hook is a caller under
   that same obligation (R-15). The plan says which mechanism applies.
4. **The demonstration process after mod-002.** Once mod-002 removes the
   hook (D-h), the demonstration process stays in existing stand databases
   with no organization. mod-002's filter answers not found for it, and
   mod-014's stand reset clears it. This is mod-002's concern. It is noted
   here so that nobody files it against this task.

## Round 3 — delta check (2026-10-01)

**Verdict: approved-by-ai.** R-12, the only open finding, is resolved: this
spec and mod-015 now divide the shell work by the same merge-order rule. Two
new minor findings, R-19 and R-20, are fixed in place. No major finding
remains, and no new question needs the human.

Inputs checked (delta only, per the stage's round-2 rule):

- status.md D-4..D-12, with D-5 and D-6 as amended 2026-10-01, and the
  analyst's round-4 Log line;
- `git diff 5748586 2564c12` on spec.md, status.md and US-2, and the rules
  and scenarios that diff cites, at 2564c12;
- the sibling specs at their branch heads:
  - mod-002 at 35e55c0: status D-a..D-l; spec BR-M1-3, BR-M1-20, BR-M1-23,
    BR-M1-24, NFR "Seam with mod-001"; US-2; US-3 "The project settings
    link to the organization's process";
  - mod-015 at cdc5318: status D-12, D-16..D-18; spec § Navigation model,
    BR-4, BR-8, BR-12, BR-15, BR-19, § Decomposition; US-1 "Appearance
    tokens"; US-3 "An organization-scoped entry waits for a real context";
    US-11 "Parallel modules"; review round 3 (R-29);
  - mod-014 at 93fa86d: NFR Stand; US-4;
- tech-002's branch at 60bd1b4: the external-database mode in
  `src/backend/AGENTS.md`;
- `docs/conventions/git.md` and `scripts/finish-task.sh`: a branch is rebased
  onto `main` before it merges;
- `src/frontend/src/app`: there is no `app/theme` folder yet, and the theme
  is the `createTheme` call in `App.tsx`. The NFR Appearance can still be met
  in the merges-first case, because a screen that uses no literal and only
  the theme's palette and spacing unit satisfies it.

### Decisions → where the spec carries them (delta)

| Decision | Carried by | Check |
|---|---|---|
| D-5 as amended: who registers the «Процесс» entry | BR-M2-12, § Out of scope, § Open questions, US-2 "No visible entry or link…" | holds; the exception's back-reference is fixed (R-20) |
| D-6 as amended: who wires the token, the category colour and the canon frames | BR-M2-12, BR-M2-13, NFR Layout, § Out of scope, § Open questions, US-2 "A seeded process is shown in full" | holds: the three timings for this task merging second and the merges-first case are all named, and no "by" or "before the frontend stage" wording is left; the rebase at the stage's start is now explicit (R-20) |
| D-4: the stand's seed, skipped when the schema is not migrated | BR-M2-1, US-2 stand scenarios | "not migrated" is now determinate (R-19) |
| D-7..D-12 | as round 2's table | the round-4 delta did not touch them; they hold |

### Sibling specs

- **mod-015 (cdc5318).** Its R-29 rewrote BR-15, § Decomposition and US-11
  "Parallel modules" to the same rule. Whichever task merges second
  registers the entry. mod-001 wires the theme lookup and the canon frames
  itself, in its frontend stage or on its rebase. The follow-up adoption line
  is filed only if mod-001 merged first. This matches BR-M2-12, BR-M2-13,
  NFR Layout and D-5/D-6. BR-4 and US-1 "Appearance tokens" (the token set
  and the category codes) match the § Business rules preamble and US-1's
  token criterion. The «Процесс» row of § Navigation model
  (`process.definition.read`, "registered per D-12, D-16") and US-3 "An
  organization-scoped entry waits for a real context" match US-2's registry
  criterion.
- **mod-002 (35e55c0).** Its delta check widened BR-M1-20 to cover a
  malformed identifier and "every organization-addressed page and request
  that exists when this task merges, mod-001's process screen and process
  read included". Its US-2 "An unknown organization answers not found" now
  covers a malformed value as well. Both agree with BR-M2-11 and § Out of
  scope. BR-M1-23 registers M1's own settings entries under «Организация» and
  «Проект». They sit beside the «Процесс» entry, and nothing collides. BR-M1-3
  and NFR "Seam with mod-001" still match BR-M2-8, BR-M2-9 and D-10, and D-h
  still matches BR-M2-1's removal.
- **mod-014 (93fa86d, gate-1 approved).** NFR Stand now pairs Keycloak's
  store with the stand's `mustask` database, an external one included. US-4
  has the stands of two checkouts take turns on one external database
  (tech-002). This task changes no realm import (BR-M2-10), so it never
  triggers mod-014's drift check or its reset. A shared database that another
  branch migrated is a new and common stand state for BR-M2-1: R-19.

### Previously open items

- R-12 is resolved; its Resolution field above is filled in.
- R-1..R-11 and R-13..R-18 stay resolved. The analyst's round-4 Log line says
  R-13..R-18 are present at 2564c12, and that holds.
- C-1 holds. A case-insensitive search of this folder for the reference
  tracker's product, vendor and abbreviations finds nothing.
- Line budgets after this round: spec.md 223, US-1 97, US-2 146. The size
  stays L.

### R-19
Severity: minor
Affects: BR-M2-1, US-2 "An unmigrated Development stand starts without the demonstration process"; tech-002; mod-014 NFR Stand, US-4
Problem: A stand seed that does not happen ends one of two ways, each with
its own log. If the schema is not migrated, the stand logs that it skipped
the seed. If the seed fails for any other reason, the stand logs the
failure. The spec did not say what "not migrated" means, so a partly
migrated database fell between the two. tech-002 makes that state common.
In its external-database mode, every checkout and worktree shares one
`mustask` database, and mod-014 US-4 has the stands of two checkouts take
turns on it. A database that a branch without this task's migration has
migrated is then the usual state of a mod-001 stand. The scenario tested
only the empty case, which cannot tell the two behaviours apart. An
implementation that checks for pending migrations passes it, and so does
one that tries the seed and fails on a missing table.
Required resolution: Define "not migrated" so that a partly migrated
database has one outcome, and test that case.
Status: resolved
Resolution: BR-M2-1 now reads "not migrated, that is, at least one
migration this build ships is not applied (a new or reset database, or an
external one that another branch's stand migrated, tech-002)". The US-2
scenario's Given is "a Development stand whose database lacks at least one
migration this build ships: none applied, or all but this task's". In both
cases the stand still starts. What becomes determinate is the log line, and
the test now checks it.

### R-20
Severity: minor
Affects: BR-M2-12
Problem: The round-4 rewrite of BR-M2-12 caused two problems.

1. It moved sentences, so "the one exception to the sentence above" pointed
   at the wrong sentence: "If `mod-015` is in the `main` this task's branch
   is based on, this task merges second". The intended sentence is "This
   task adds no menu item, link from another screen, shell change or theme
   change".
2. "The frontend stage does this if `mod-015` is in `main` when the stage
   starts" left out the rebase. A branch cut before mod-015 merged cannot
   register an entry in a registry that its base lacks. The two later
   timings each name their rebase.

Required resolution: Point the exception at the right sentence, and state
the rebase at the stage's start.
Status: resolved
Resolution: The "adds no …" sentence now ends "with the one exception
below", and the registration says "which is that exception". The timing
sentence reads "If `mod-015` is in `main` when the frontend stage starts,
the stage rebases onto it first and does this." The meaning is unchanged:
D-5, D-6 and `scripts/finish-task.sh` already require it.

### Notes (not findings)

1. **For the orchestrator: mod-015's D-16 wording.** mod-015's status.md
   D-16 still reads with the stage-6 cut-off ("if mod-015 is in main before
   mod-001's frontend stage …"). mod-015 BR-15, after its R-29, adds the
   third order, and so does BR-M2-12. Neither contradicts D-16's text, so
   nothing blocks this task. Aligning D-16 with mod-015 BR-15 would take one
   line in mod-015's status.md.
2. **For stage 3: when this task merges second.** Inside mod-015's shell,
   the screen follows mod-015 BR-6: its title is the only level-1 heading,
   the browser tab reads «<title> — Mustask», and the trail lists the
   screen's ancestors. It also follows BR-19. The address is a settings
   page, so the settings navigation appears when any settings entry is
   visible, such as «Языки» for a dictionaries reader. «Процесс» itself
   stays hidden until the context is real (mod-015 BR-8, D-18). design.md
   fixes the screen's title and trail. It also fixes what the settings
   navigation marks as current while the screen's own entry is hidden.

## Design check (2026-10-01)

**Verdict: approved-by-ai.** This check stands in for the human at gate-2
under the delegation of 2026-10-01. It was run adversarially, in the stance
of `.claude/agents/requirements-reviewer.md`, against design.md and the
prototype at 6577518. It found nine minor findings, R-21..R-29, and fixed all
of them in place in design.md and `prototype/process-definition.html`. No
major finding is open, and no question needs the human. PC-1..PC-3 are
settled on the recommended options (below). gate-2 is the orchestrator's to
record.

Inputs checked:

- spec.md, US-1 and US-2 at 6577518, and status.md C-1..C-3 and D-4..D-12;
- `docs/conventions/frontend.md`, ADR-0007 and ADR-0019;
- the panel as it stands: `app/AppLayout.tsx`, `app/routes.tsx`, `App.tsx`
  (MUI 9, `ruRU`, `retry: false`, `refetchOnWindowFocus: false`),
  `shared/ui/ErrorBanner.tsx`, `shared/lib/apiErrorText.ts`,
  `shared/api/httpClient.ts` (`ApiError.errorCode`), `features/auth`
  (`RequireAuth`, and the sign-in that returns to the current path), and the
  dictionaries slice's `AccessDenied` and `RegistrySkeleton`;
- doc-007 § "MVP: the seeded process", doc-010 D-5, and the doc-001 glossary
  rows for «Ключ типа», «Статус», «Категория статуса» and «Уровень
  бэклога»;
- the sibling branches at their heads, unchanged since round 3. No sibling
  has a design.md yet, so this check is against their specs:
  - mod-015 at cdc5318: BR-1, BR-4..BR-8, BR-10..BR-12, BR-15, BR-17..BR-19,
    § Navigation model, D-12, D-15..D-18; US-1, US-2, US-3, US-6, US-7;
  - mod-002 at 35e55c0: BR-M1-3, BR-M1-20, BR-M1-23; US-3 "The project
    settings link to the organization's process";
  - mod-014 at 93fa86d: it has no screen (mod-015 D-21), so there is
    nothing to align.

### What holds

| Check | Result |
|---|---|
| Every scenario has its screen or state | All 14 US-2 scenarios and every US-1 scenario map in § "Story coverage". The stand scenarios had the wrong state (R-21). |
| Every state of the canon is designed | Loading, content, read failed with «Повторить», and organization not found are designed in both layouts. Empty, empty after a filter and access denied cannot occur, and each now says why (R-27). The interim navigation state of layout B was missing (R-22). |
| Copy is Russian and exact | T-01..T-27 are final. T-12..T-16 match doc-007 and doc-010 D-5 word for word. T-04, T-05, T-09 and T-10 use the glossary's terms. T-18, T-21 and T-22..T-24 match mod-015 BR-12 and US-7 verbatim, and T-27 follows BR-6's «<title> — Mustask». Category codes and the token's name appear only as the spec allows (NFR Language). |
| MUI only, within frontend.md and ADR-0019 | Every block is MUI core or `@mui/icons-material`: `Grid` with the v9 `size` API, `Paper`, `Stack` as `dl`, `Table*`, an inert `Chip`, `Skeleton`, `Alert` with `AlertTitle`, `Button` and `SvgIcon`. There is no DataGrid, no new dependency (mod-015 BR-17) and no colour literal or fixed dimension (NFR Appearance). The only `shared/ui` change is an optional `title` prop on `ErrorBanner`, which keeps its callers as they are. |
| Consistent with sibling specs | mod-002: the address, the not-found code and the «Процесс» link from the project settings (BR-M1-20, BR-M1-23, US-3) match. mod-015: the shell, trail, tab, canon frames, token and category lookups, interim BR-8 rule and BR-15 sequencing match after R-22..R-24 and R-29. |
| Nothing invents an API | The screen uses the one read of D-9 and the one error code of D-7. It sends no other request, and it branches on `errorCode` only (frontend.md "Named values"). The hand-off to stage 4 states what the screen needs, and it now leaves no ordering gap (R-25) and no second copy of the category words (R-26). |
| C-1 | A case-insensitive search of design.md, the prototype and review.md for the reference tracker's product, vendor and abbreviations finds nothing. |

### Points to confirm (PC-1..PC-3)

- **PC-1 → keep DD-2.** The spec fixes no order for the screen. Backlog-level
  order reads like the hierarchy, and it puts the two «Разрешена» rows that
  US-2 compares side by side. The answer now fixes that order (R-25).
- **PC-2 → keep T-03.** Without it, «Разрешена» showing «В работе» on three
  types and «Решённые» on «Баг» reads as a defect (doc-010 D-4).
- **PC-3 → approve as recommended.** Approve layout A in full. Approve
  layout B as the mapping in § "Two renderings" plus the reconciliation
  table in § "Adopting mod-015". mod-015 owns the shell and settles it at its
  own gate-2 (C-2). The screen's content is the same in both layouts, so
  nothing this gate approves depends on mod-015's frames.

### R-21
Severity: minor
Affects: design.md § "Story coverage"; US-2 "An unmigrated Development stand starts without the demonstration process"; BR-M2-11
Problem: The coverage row said that for both stand scenarios the screen
shows its not-found state. That is true only after a failed seed on a
migrated schema. On an unmigrated schema the process tables are missing, so
the read itself fails. BR-M2-11 forbids answering such a failure as not
found. A tester following the row would file a correct Read failed as a
defect, or an implementation would "fix" it into a forbidden not-found.
Required resolution: Split the row by cause.
Status: resolved
Resolution: The row now says Organization not found after a failed seed on
a migrated schema, and Read failed, never not found, on an unmigrated
schema.

### R-22
Severity: minor
Affects: design.md § "Two renderings" point 2; prototype; mod-015 BR-7, BR-8, BR-19, US-3; round 3 note 2
Problem: Layout B designed only the state after `mod-002`, with «Процесс»
current in «Меню настроек». Under mod-015's interim rule the entry is
hidden until the context is real. That hidden state is the first one layout
B would ship. Round 3 note 2 asked design.md to fix what the settings
navigation marks as current then, and design.md did not.
Required resolution: Design the interim state from mod-015's rules, and
show it in the prototype.
Status: resolved
Resolution: Point 2 now covers both cases. If another settings entry is
visible, «Меню настроек» is shown with nothing marked current and the rail
marks «Настройки» current. If no settings entry is visible, there is no
«Меню настроек» and no «Настройки», and nothing is marked current. Once the
context is real, «Процесс» carries `aria-current="page"`. The prototype gains
a "Context (layout B)" switch, placeholder or real, and the reconciliation
table gains a row to confirm the rule against mod-015's design.md.

### R-23
Severity: minor
Affects: design.md point 10; prototype; mod-015 US-2 (frame table)
Problem: In layout B's not-found state, the prototype kept the trail
«Настройки» and «Процесс» marked current, and design.md was silent on both.
mod-015 US-2 gives a not-found page no trail. An unfound address is not the
entry's own address, so the entry cannot be current.
Required resolution: State both in point 10, and match the prototype.
Status: resolved
Resolution: Point 10 now reads "no page trail (mod-015 US-2); no settings
entry is marked current". The prototype drops the trail and the marker in
that state.

### R-24
Severity: minor
Affects: design.md § "Keyboard and screen reader", "Colour is never the only carrier"; prototype; mod-015 BR-4, US-1 "Appearance tokens"
Problem: The prototype gave «Новые» and «Удалённые» the same colour and told
them apart by shape alone. mod-015 US-1 requires one distinct colour per
category code. Separately, design.md called the hollow ring `app/theme`'s,
while its components table has the category cell pick
`RadioButtonUnchecked` by code. The two statements disagreed on who owns the
shape.
Required resolution: Show distinct category colours, and give the shape one
owner.
Status: resolved
Resolution: The prototype's `Removed` colour is now distinct. It remains
illustrative. The row now says "one distinct colour per category,
«Удалённые» included". It also says the ring is the category cell's, picked
by the code, until the theme carries a shape. That question was already in
the reconciliation table.

### R-25
Severity: minor
Affects: design.md § "Hand-offs" (to stage 4); DD-2; doc-007 backlog-level table
Problem: The hand-off allowed the answer to omit the order and give "the
level's rank, so the client can sort". «Пользовательская история» and «Баг»
share «Истории», rank 2, so rank alone cannot put the story before the bug,
as DD-2 requires. Sorting by key gives the wrong order: `BUG` < `US`.
Required resolution: Have the answer fix the order, or name a tie-breaker.
Status: resolved
Resolution: The hand-off now says the answer fixes DD-2's order and the
client renders the types in the order received, without sorting.

### R-26
Severity: minor
Affects: design.md § "What the screen shows", "Category label"; § "Hand-offs"
Problem: The row asserted a client-side map from code to label, while the
hand-off leaves to stage 4 which side maps the code. If stage 4 put the
label in the answer, the client's map would be the second copy the hand-off
forbids. The row also did not say what layout B draws for an unknown code.
Required resolution: Make the row conditional on the contract, and say what
an unknown code draws.
Status: resolved
Resolution: The row now says that the answer always carries the code,
because layout B looks the colour up by it. If the contract also carries the
label, the screen shows it and holds no map. An unknown code shows T-17 and,
in layout B, no dot.

### R-27
Severity: minor
Affects: design.md § States; ADR-0007 and ADR-0019 canon; frontend.md "Mandatory states"
Problem: The canon names empty, empty after a filter and access denied as
mandatory. § States dismissed only "Empty". The other two were left to DD-5
and the Components note. Nothing said what an unexpected `403` shows, and
the panel's guard would show «Доступ ограничен» if one were wired by habit.
Required resolution: Name each canon state, and route a `403`.
Status: resolved
Resolution: § States now reads "Empty after a filter. Does not exist: the
screen has no filter." It also says that access denied does not exist
(DD-5), and that a `403` which arrives anyway is Read failed, not «Доступ
ограничен».

### R-28
Severity: minor
Affects: design.md § "Screen map" (Access); DD-6; round 2 note 2 for stages 3-6
Problem: "The code is declared by the page" read the same for both layouts.
In layout A, the only page-level mechanism that declares a code is the
dictionaries `RequirePermission`. It checks the token, and this code is in
no token, so wiring it would refuse every user and fail US-2 "Every signed-in
user sees the same definition".
Required resolution: Say how each layout declares the code without a check.
Status: resolved
Resolution: The Access cell now reads: in layout A, a named constant of the
slice that no guard reads; in layout B, the route and the navigation entry
(DD-6).

### R-29
Severity: minor
Affects: design.md § "Adopting mod-015" (reconciliation table); mod-015 BR-11, BR-18; D-4
Problem: In layout B before `mod-002`, the page's address carries an
organization id, which the demonstration id makes common. The switcher still
reads placeholder data. mod-015 BR-18 says the switcher shows the address's
context, and that `mod-002` builds that rule. If mod-015's shell resolved
`:organizationId` against its placeholder dataset, it would answer
«Организация не найдена» for the demonstration id. The screen would then be
unreachable until `mod-002`, which defeats D-4. design.md did not list this
point.
Required resolution: List it for confirmation against mod-015's design.md.
Status: resolved
Resolution: The reconciliation table gains two rows. One confirms that the
switcher keeps its placeholder selection and the shell gives no not-found of
its own before `mod-002`. The other confirms how «Меню настроек» behaves on
a settings page whose entry is hidden, and beside the not-found frame.

### Editorial (no finding)

- The layout A drawing shortens the third column's header. A line under the
  drawing now names its copy, T-10 «Категория статуса».
- The screen-reader row now gives the row's real reading order: the order,
  then the status, which is the row header, with «Начальный», then the
  category.
- § Prototype mentions the new context switch. The prototype's script passes
  `node --check`.

### Notes for stages 4-7 (not findings)

1. **`ErrorBanner`'s `title` prop and mod-015's restyle.** If mod-015
   replaces `ErrorBanner` with its shared error component, the two branches
   touch the same file. Point 9 already says to use mod-015's component when
   it is in `main`. On a rebase, keep the title (T-19) and drop the local
   prop.
2. **The demonstration identifier in E2E.** The E2E path (D-12) opens the
   screen by address, using the id from `src/backend/AGENTS.md`. A layout B
   run before `mod-002` should also assert that «Процесс» is absent from «Меню
   настроек» (R-22).

## Code review, round 1 — backend + frontend (2026-10-04)

Stage 8 (`06-code-review.md`), deep, run by a session that wrote none of
this. One pass over `git diff main...mod/001-process-seed` at 38a3190: 112
files, +12182/−50. The base is c48b565, which is main's tip, so no rebase is
owed. Read before the diff: spec.md, US-1, US-2, status.md (C-1..C-3 and
D-4..D-14 taken as binding), design.md, api-contract.md, plan-backend.md,
plan-frontend.md, qa.md (both sections), the rounds above,
`docs/conventions/backend.md` and `frontend.md`, and the ADR index.

Re-run here, all green:

- backend: Domain 102, Application 60, Contracts 61 and Architecture 68;
  Infrastructure 114 (Testcontainers); `Mustask.Api.IntegrationTests`
  filtered to `Features.Process`, 44;
- frontend: Vitest over `src/features/process`, `src/app` and `src/shared`,
  49 files and 460 tests.

Not re-run: the full `scripts/verify.sh` and E2E spec 20. QA's runs at
a566e51 and 32ec376 stand, and only docs changed after them. No stand was
started.

**Verdict: needs-changes.** 0 blockers, 2 majors, 3 minors. The code itself
is sound. Both majors are approved design sentences that the shipped screen
does not do, and each is disproved by another file in the same diff
(06-code-review.md, "Severity": major regardless).

### What holds (checked, not assumed)

- **The seed is all-or-nothing and idempotent, as the contract says.** It
  checks for an ambient transaction and for pending changes before any
  write (`ProcessSeedStore.cs:39-51`). It works under its own savepoint
  (:54). The root is inserted with a parameterized
  `ON CONFLICT (organization_id) DO NOTHING` (:60-66), then read back in a
  new statement under READ COMMITTED (:69-73, `UnitOfWork.cs`). Any
  exception, cancellation included, rolls back to the savepoint with no
  token and detaches the candidate (:92-96, :111). The tests prove each
  path against a real PostgreSQL: a statement failure before it is sent, a
  database-side late failure that the caller swallows (its own write still
  commits), cancellation, a failure in the caller's operation, two
  orchestrated races and eight unorchestrated calls.
- **The read cannot turn a failure into a not-found.** A malformed id never
  reaches storage (`GetProcessDefinitionHandler.cs:19-22`). An unknown one
  gets the same exception (:25). Nothing catches below it, so a storage
  failure is a 500. The route has no `:guid` constraint
  (`ProcessEndpoints.cs:26`), so a malformed id is the handler's to judge.
  The read is a fixed three queries, pinned by a command-counting test.
- **Security for this scope.** The endpoint is `AuthenticatedOnly`
  (`ProcessEndpoints.cs:27`), the category the inventory test requires. The
  raw SQL takes parameters. The 404 body carries the catalog's text and
  nothing of the input. The log lines carry the organization and process
  ids only, and no personal data. No secret was added. The organization-
  scoped code is in no realm file, and the scan that proves it is shown to
  be non-vacuous (`OrganizationPermissionRealmTests.cs:19-35` and the test
  beside it).
- **Architecture.** Domain holds the blueprint and its invariants.
  Application holds two slices plus the module-root seam `IProcessSeed`,
  which the architecture tests accept. Infrastructure holds the EF mapping,
  the raw SQL and the read model with one `Project(...)` helper. Enums are
  stored and served as strings. No property is named `Code`. The `/api/organizations/...`
  route deviates from backend.md's `/api/<module>/...` by a recorded
  decision (D-9). The frontend slice follows frontend.md: one barrel, a key
  of `['process','definition',id]`, errorCode-only branching, and no colour
  literal (`colourLiterals.test.ts`). Every size I read is
  `theme.spacing` or `theme.layout.*`. No ADR is owed beyond D-12's "none",
  with F5 as the one textual loose end.
- **Tests check what they claim.** Each US-1 and US-2 scenario has a test
  named after it, and I read every assertion of the endpoint, seed and
  stand tests. The derived-host tests restore Serilog's static logger, and
  the race tests wait on `pg_stat_activity` rather than on time. The
  frontend outline covers ten failure answers, and its QA additions check
  the exact request URL for eight address encodings.

### F1 (major) — design point 2 promises «Меню настроек» on the process address before mod-002; the diff's own test pins the opposite, and P-1 is undecided

Where it is:

- `design.md:121`, § "Two renderings" point 2, layout B. While «Процесс» is
  hidden, the address "is still a settings page": a «Языки» reader gets
  «Меню настроек» with nothing current, and the rail marks «Настройки»
  current.
- Its echoes: point 4's trail (:123), T-26 (:356), § "Adopting mod-015"
  item 3 (:397), and the reconciliation row (:416).

What the code does: the merged shell decides "settings page" from an entry's
landing in the *current selection* (`visibility.ts:130`). Before `mod-002`
that selection is the placeholder's. `AppLayout.process.test.tsx:78-90`
therefore asserts the opposite of point 2 for a `LANGUAGES_READ` user: no
«Меню настроек», no trail, nothing current. plan-frontend § 6.3 and § 12
say the same.

The developer raised this as plan-frontend P-1 and went ahead with (a) "if
the orchestrator wants a design delta". No decision is in status.md, and
QA's D-3 is open "for the orchestrator". An approved gate-2 point is
therefore unmet, with no decision replacing it. Anyone who reads design.md
(QA again, `mod-002`, the human at gate-3) expects a navigation state the
build does not have.

Required:

1. The orchestrator records P-1 in status.md Decisions, under the
   2026-10-01 delegation or before the human at gate-3.
2. Under (a): a design delta narrows point 2, point 4, T-26, Adopting item
   3 and row :416 to plan-frontend § 6.3. Before `mod-002`, on any id but
   the placeholder's, the address shows no «Меню настроек», no trail and
   nothing current. The trail «Настройки / Организация» shows once the
   context is real. QA's D-3 is then closed against the decision.
3. Under (b): point 2 is marked deferred to the named shell line, and
   `AppLayout.process.test.tsx:78-90` gains a comment naming that line as
   the interim pin.

No product code changes under (a).

Resolution (developer, 2026-10-04): open, for the orchestrator. The fix
starts with a P-1 decision, which is not the developer's to record. Under
(a) the code does not change and the design delta is the designer's.
Under (b) the developer adds the comment to the test once the shell line
is named.

### F2 (major) — design § States says an unreadable answer is a failed read; qa.md D-4 in the same diff shows that a body missing a part crashes the panel

Where it is: `design.md:271-275` lists "an answer that cannot be read"
among the failed-read triggers.

What the code does: `getProcessDefinition` (`processApi.ts:81-95`, the check
at :87-93) only checks that `workItemTypes` is a non-empty array. A `200`
whose type lacks `states` or `backlogLevel`, or whose state lacks
`category`, renders and throws in `ProcessTypeCard.tsx:58` and :72. The
`TypeError` replaces the whole shell with the app's crash screen «Что-то
пошло не так». qa.md D-4 and F-A14, in this diff, record exactly that
against this sentence. They leave it as "the developer's call", and no call
has been made.

The contract does guarantee the shape (api-contract § 1.2), and the backend
pins it, so the product risk is small. The defect is that the branch ships
a design sentence and a QA report that disagree.

Required, either of:

- (i) In `getProcessDefinition`, also check that each type's `states` is an
  array and `backlogLevel` an object, and that each state's `category` is
  an object, and throw the read-failed error otherwise. Add one test per
  missing part in `processApi.test.ts` or the states outline. QA re-checks
  only that file.
- (ii) Record D-4 as won't-fix in qa.md, with its reason (the contract
  guarantees the shape and `HasExactlyTheContractShapeAndNoNulls` pins it),
  and narrow `design.md:273` to "an answer that is not JSON".

Resolution (developer, 2026-10-04): fixed by (i) in 651f27c, so design.md
stays as approved. `getProcessDefinition` throws
`UnreadableProcessDefinitionError` (the renamed
`EmptyProcessDefinitionError`) for each of the three missing parts, and
also for a type or a state that is not an object. Tests: six rows in
`processApi.test.ts`, and three rows of the failed-read outline in
`ProcessDefinitionPage.states.test.tsx`. Those three show the alert with
«Повторить» inside the shell, not the crash screen. qa.md D-4 and
plan-frontend § 13 record it.

### F3 (minor) — MigrationsGuard's summary still says every hosted seeder checks it

Where it is: `src/backend/Mustask.Infrastructure/MigrationsGuard.cs:7-9`
says "Every hosted seeder (`DictionarySeeder`) checks this first". The file
is not in the diff (direction 2).

This diff adds a second hosted seeder, `DemonstrationProcessSeed`, which
deliberately does not call the guard. It reads `MigrationsState` and skips
(`DemonstrationProcessSeed.cs:70-82`). `MigrationsState.cs:11-12` even says
the guard "stays as it is". The sentence now overstates. Nothing an agent
does differs today, but the next seeder's author would copy a rule that
already has an exception.

Fix: reword it to "The dictionaries seeder checks this first; the
Development demonstration seed (mod-001) reads `MigrationsState` instead and
skips."

Resolution (developer, 2026-10-04): fixed in 4e59cd4, worded as proposed.
The change is to the comment only.

### F4 (minor) — a retry that ends in «Организация не найдена» drops focus to the document (QA D-5, still open)

Where it is: `useRetryFocus.ts:26-36` moves focus to the title on success
and to «Повторить» on failure. On not found, `ProcessDefinitionPage.tsx:31-32`
returns the frame, neither target is mounted, and focus lands on `body`.

The design (`design.md:312-316`) names no target for this outcome, so this
is a gap, not a contradiction. It is an accessibility rough edge on a rare
path.

Fix (the developer's call, with the designer if a target is named): focus
the frame's `h1` «Организация не найдена» when a retry ends in not found.
`StateFrame` may need the same optional-ref seam P-2 gave `PageHeader`. Or
record won't-fix in qa.md D-5 with the reason.

Resolution (developer, 2026-10-04): fixed in bcef95f. Focus goes to the
frame's `h1`, through a new optional `headingRef` on `StateFrame` and
`NotFound`. As with P-2, the prop is additive and sets `tabIndex={-1}` only
when given. The page test fails without the ref. A first load that finds
no organization still moves no focus. design.md § Focus still names no
target for this outcome; adding one sentence there is the designer's
delta, if the orchestrator wants it.

### F5 (minor) — ADR-0021 still names work item types and statuses as future localized dictionaries

Where it is: `docs/adr/0021-localized-dictionaries-pattern.md:37` and :116.
The Problem lists "work item types, … statuses next" as reference data that
must carry a name in every language. The Decision calls its shape "the
pattern every later dictionary (work item types, priorities, statuses and
the like) follows without a new ADR". The file is not in the diff
(direction 2).

This diff founds work item types and states as per-organization M2 process
rows instead. They have Russian-only names and no translation table
(doc-007; spec NFR Language; migration `AddProcessDefinitions`). ADR-0021
is `proposed`, so it does not bind this branch, and D-12 records that no
ADR is owed. But the next reader of ADR-0021, for example a localization
line or mod-003, is told the opposite of what M2 now is.

Fix: one sentence in ADR-0021 § "Current state" saying that work item types
and their states are M2 process data per organization (doc-007,
mod-001-process-seed), not dictionaries, so this pattern does not apply to
them. Or put it to the human with gate-3.

Resolution (developer, 2026-10-04): fixed in b457faa, with one paragraph in
§ "Current state". The paragraph says that work item types and their
states are per-organization process data with Russian names and no
translation table, citing doc-007 and this task's spec. So the pattern
does not apply to them, although the Problem and the Decision still name
them as examples. The rest of the ADR is unchanged, and it stays
`proposed`.

### Behavioural claims pass (item 6)

Direction 1, the prose this diff adds or changes. One line per claim, with
its anchor or the finding it became.

- spec BR-M2-1: one process per identifier, nothing shared between two →
  migration :115 (`ux_processes_organization_id`); `ProcessSeedStore.cs:60-66`;
  a fresh `Guid.CreateVersion7()` per entity in `ProcessDefinition.Seed`;
  the composite key at migration :66-71.
- spec BR-M2-1: the seed command is the only way a process comes to exist →
  the only writers are `ProcessSeedStore.cs:62` and :85-86 (grep). The only
  route is the GET at `ProcessEndpoints.cs:26`. The only resolver of
  `IProcessSeed` is `DemonstrationProcessSeed.cs:86`.
- spec BR-M2-1 and US-2: the stand seeds on every Development start, and no
  other environment seeds → `Program.cs:81-84`. AppHost has no reference to
  the seed (grep).
- spec BR-M2-1 and US-2: this task's migration missing → skip with a log
  line, and the start completes → `DemonstrationProcessSeed.cs:70-82`. It
  skips on any pending migration, which is a superset (Notes 2).
- spec BR-M2-1: a database with no migration at all already stops the start
  → `DictionarySeeder.cs:22` and `MigrationsGuard.cs:18-26`. Ordering:
  `Program.cs:72` comes before :81-84, and the registration is at
  Infrastructure `DependencyInjection.cs:56`. `StartingAsync` runs
  sequentially and aborts on the first failure (no `ServicesStartConcurrently`
  anywhere).
- spec BR-M2-1 and US-2: a failed seed leaves nothing, is logged, the start
  goes on, and the next start retries → `ProcessSeedStore.cs:92-96` and
  :111; `DemonstrationProcessSeed.cs:104-112`.
- spec BR-M2-2: the first state is the initial one →
  `GetProcessDefinitionHandler.cs:50`.
- spec BR-M2-4: ranked levels with a kind → the `SeededProcess` level
  blueprints; the unique index `ux_backlog_levels_process_id_rank`.
- spec BR-M2-8: all-or-nothing, and discarded with the caller's failed
  operation → the savepoint at `ProcessSeedStore.cs:54` and :111; the
  `UnitOfWork` transaction is disposed uncommitted on any exception.
- spec BR-M2-9: the key is unique in the process → migration :144.
  `Guid.Empty` creates nothing → `ProcessDefinition.cs:50-53`, before the
  store. Races create nothing twice → `ProcessSeedStore.cs:64` under READ
  COMMITTED.
- spec BR-M2-10: a registry outside the realm catalog, and a test fails on
  any realm mention → `OrganizationPermissionCatalog.cs:15` and :24;
  `OrganizationPermissionRealmTests.cs:19-35`.
- spec BR-M2-10: authenticated-only, and nothing for a visitor who is not
  signed in → `ProcessEndpoints.cs:27`; `routes.tsx:31` sits inside
  `RequireAuth`.
- spec BR-M2-11: unknown and malformed get one answer, with a ru and a kk
  text → `GetProcessDefinitionHandler.cs:19-25`;
  `contracts/errors/organizations.json`. Any other failure is never
  not-found → nothing catches; `readFailure.ts` branches on errorCode only.
- spec BR-M2-12: the address → `paths.ts:4` (screen) and
  `ProcessEndpoints.cs:26` (read).
- spec BR-M2-12: the entry is registered, and shown only once the context
  is real → `model.ts:39-46`; `visibility.ts:64`.
- spec BR-M2-12: no shell change and no theme change → nothing under
  `app/theme` or `app/AppLayout*` changed. The `shared/ui` props are
  additive pattern seams (frontend.md § MUI, P-2).
- spec BR-M2-13: the content, the token through the theme, the category
  colour → `ProcessTypeCard.tsx:48`, :57-58, :72-81; `TypeMark.tsx:10`;
  `CategoryCell.tsx:14-15`.
- spec BR-M2-13: no control or request that seeds or changes anything →
  one GET at `processApi.ts:81`; the inert chip at `ProcessTypeCard.tsx:78`.
- spec NFR Appearance and Language: no colour literal, no fixed dimension,
  no category code shown → `colourLiterals.test.ts`; dimensions read by
  hand; `CategoryCell` renders the label only.
- US-2 "No visible entry or link…": the entry under «Настройки» with the
  code, hidden on placeholder data → `model.ts:39-46`; `visibility.ts:64`.
- design § "Two renderings" point 2 and point 4, T-26, Adopting item 3, row
  :416 → contradicted by `visibility.ts:130` and
  `AppLayout.process.test.tsx:78-90`: **F1**.
- design § States, Read failed, "an answer that cannot be read" →
  contradicted by `processApi.ts:87-93` and qa.md D-4: **F2**.
- design § States, Loading on first open, on retry and on an id change →
  `ProcessDefinitionPage.tsx` `loading` (`isPending ||
  isError && isFetching`), with one query key per id.
- design: no automatic retry and no refetch on focus → `App.tsx:14-15`.
- design § Focus: success goes to the title, failure to «Повторить» →
  `useRetryFocus.ts:26-36`, `PageHeader` `titleRef`, `ErrorBanner`
  `retryRef`. The not-found outcome is unspecified: **F4**.
- design DD-6: the page never checks the code → there is no `useAuth` and
  no `useAccessCheck` in the slice; an organization scope passes for any
  signed-in user (`AppLayout.tsx:91`).
- api-contract § 1.1: canonical form only, either case, not all zeros →
  `OrganizationKey.cs:25-39`.
- api-contract § 1.2: the order and no nulls →
  `ProcessDefinitionReadModel.cs:35`, :48-49, :58; non-nullable records,
  pinned by `HasExactlyTheContractShapeAndNoNulls`.
- api-contract § 1.3: the status table → 404 from the catalog; 401 and 500
  from the platform handlers; 503 from `RealmReadinessGateMiddleware.cs:49`.
  "Never 400 or 403" → there is no validator, and the guard is
  authenticated-only.
- api-contract § 2: scoped registration; the two `InvalidOperationException`
  guards (`ProcessSeedStore.cs:39-51`); "the savepoint is already rolled
  back when the exception arrives" (:92-96); "flushes before returning"
  (:87); the racing-call semantics (:60-73); "`Organization.ProcessId`
  needs no configuration" (`MustaskDbContext.cs:40`).
- api-contract § 4: the three events are the only log surface →
  `DemonstrationProcessSeed.cs:74`, :94, :106 are its only log calls. "The
  read fails with 500" is pinned by
  `DevelopmentHost_AllButThisTasksMigration_StartsAndLogsSkipped`.
- `src/backend/AGENTS.md`: the id, the panel address and the GET address →
  `DemonstrationProcessSeed.cs:45`, drift-guarded by
  `DemonstrationOrganizationDocumentationTests`.
- `src/backend/AGENTS.md`: only in Development, never AppHost →
  `Program.cs:81-84`; grep of `Mustask.AppHost`.
- `src/backend/AGENTS.md`: "It never stops the start" →
  `DemonstrationProcessSeed.cs:100-112`, which rethrows only the start's
  own cancellation.
- Code comments:
  - the `DemonstrationProcessSeed` remarks on ordering → the same anchors
    as the "no migration at all" line;
  - the `ProcessSeedStore` remark "no call ever sees a half-built process"
    → one transaction, pinned by
    `Get_WhileTheSeedingOperationIsOpen_IsNotFoundUntilItCommits`;
  - `WorkItemType` "the database refuses a level of another process" →
    migration :66-71;
  - `MigrationsState` "the one reader today" → grep finds
    `DemonstrationProcessSeed.cs:70` only;
  - `EmptyProcessDefinitionError` "the banner gives the generic text" →
    the states test "shows the panel's generic text…";
  - the skeleton's reduced motion → `ProcessDefinitionSkeleton.test`, which
    reads the emitted media rule;
  - `index.ts` "app/ imports this slice through this file only" → holds
    for production code (`routes.tsx:6`, `model.ts`). Two `app/` tests
    import `testFixtures` directly, which is test-only and not a finding.
- doc-007 amendment: one token per type, unique, category stored as its
  code, label derived → `WorkItemType.Appearance` and migration :138;
  `StateDefinitionConfiguration` (`HasConversion<string>()`);
  `StateCategoryLabels.Of`.
- status.md Log, dev 6b, "no api-contract mismatch" → the `processApi.ts`
  types match § 1.2 field for field.
- status.md Log, dev 4b, "the other derived-host tests are not changed" →
  the diff touches only `Features/Process` in `Mustask.Api.IntegrationTests`.
- status.md Log and qa.md, "E2E 38/38 on a cold stand" → `stand.json` is
  quoted in qa.md. Not re-run here, which is a named gap.

Direction 2, the behaviour this diff changes and the prose outside it that
describes the old behaviour:

- A second hosted seeder at Development start, one that skips instead of
  failing. Grepped `MigrationsGuard`, "hosted seeder", "at startup" and
  "seed" in `docs/adr`, `docs/conventions`, `docs/runbooks`, the three
  `AGENTS.md` and `.claude/`. Hits:
  - `MigrationsGuard.cs:7-9`: **F3**;
  - `local-migrations.md:4`, :7 and :99-101 (the stand never applies
    migrations): still true, since `MigrationsState` only reads;
  - ADR-0002: still true.
- The new read `/api/organizations/{organizationId}/process` and the screen
  address `…/settings/process`. Grepped both across `docs/`, `.claude/` and
  the `AGENTS.md` files. The only hit outside this feature is
  mod-015 `design.md:451`, which says mod-001 registers the entry:
  consistent.
- The «Организация» settings section now holds an entry. Grepped «Процесс»,
  "merges second" and the section in mod-015's spec and design, in
  frontend.md and in ADR-0024. Hits: mod-015 `design.md:451`, :1054 and
  § 15; frontend.md § MUI. All consistent.
- Work item types and states stored per organization with one token and
  Russian names. Grepped "work item type", "icon and colour" and
  "appearance" in `docs/adr`, `docs/conventions` and the mod-015, doc-001
  and doc-010 docs. Hits:
  - ADR-0021:37 and :116: **F5**;
  - ADR-0024:33-36 and :205: consistent, since the diff carries the
    amendment it names;
  - mod-015 BR-4, design § 2.3 and US-1: consistent;
  - doc-010's cross-review files are history and were not swept further.
- An organization-scoped code kept outside the realm catalog. Grepped
  `process.definition.read` and `PermissionCatalog.AllCodes` in
  `docs/adr` and `deploy/`. ADR-0018's "client roles equal
  `PermissionCatalog.AllCodes`" is still true, since the catalog is
  untouched. Nothing is stale.
- A new error module, `organizations`. Grepped `contracts/README.md` for a
  module list and found none to update. `docs/error-codes.md` is
  regenerated in the diff.
- The `shared/ui` props `titleRef` and `retryRef`. Grepped `docs/` and
  `.claude/`: no hits. frontend.md:64-75 names the patterns without their
  props, which is still true.
- The E2E helper `createEphemeralUserWithoutGroup`. Grepped "ephemeral" and
  "manage-users" in the `AGENTS.md` files and ADR-0023. Hit:
  `src/frontend/AGENTS.md:123`, consistent, since no right was added.
- Not swept: the doc-001, doc-002 and doc-006 analyses' sentences about
  where the process screen is reached from, beyond doc-007. doc-007 defers
  that to this task's design, which D-5 settles. Also not swept: the
  earlier rounds' notes in this file. Round 3 note 2 restates design point
  2; it is history, and F1's fix covers the live text.

### Notes (not findings)

1. **For the L security pass.** Until `mod-016`, the read is an existence
   oracle: any signed-in user can tell, by 200 against 404, whether an
   organization id has a process. That means whether the organization
   exists once `mod-002` creates real ones. BR-M2-10 accepts this
   explicitly (gate-1), the ids are UUID v7 with 74 random bits, and the
   process content is the same for everyone (doc-007 P-4, P-5). I name it
   so the security pass weighs it rather than rediscovers it.
2. **The skip condition is wider than BR-M2-1 states.** The hook skips on
   any pending migration of the build, not only this task's. Today the two
   are the same, because `AddProcessDefinitions` is the latest migration.
   Once a later line lands a migration first, a database lacking only that
   one also skips, with an accurate log line (api-contract § 4 and
   `src/backend/AGENTS.md` already describe the wider condition). `mod-002`
   removes the hook in any case.
3. **DD-4 against frontend.md's and ADR-0024's "the page validates its
   ids".** The server validates for the page (D-9: one definition of
   malformed), and the page answers not found by level. I read this as
   conforming.
4. **The dev 6b `verify.sh` run was red** on the order-dependent
   `SessionLogoutEndpointTests` (bug-002, queued on its own). QA's later
   run on a566e51 was green.

## Code review, round 2 — backend + frontend (2026-10-04)

Stage 8, round 2, deep, run by a session that wrote none of this. A delta
over round 1: the five findings above, and the fix diff
`git diff 427fe23 0e91ef9` (five commits, 14 files: 651f27c, bcef95f,
4e59cd4, b457faa, 0e91ef9). `main` is at abeb23a, two commits past the
branch's base c48b565. Both are queue commits that touch only
`docs/backlog.md`, so nothing in them bears on this diff.

Re-run here at 0e91ef9:

- frontend: the whole Vitest suite, 67 files and 655 tests, green (the count
  the dev Log line gives); `eslint .` clean; `tsc -b` clean; Prettier clean
  on every touched frontend file;
- backend: `dotnet build` of `Mustask.Infrastructure`, 0 warnings and 0
  errors (the only backend change is the `MigrationsGuard` summary, and its
  new `<see cref="MigrationsState"/>` resolves);
- two mutations, each reverted afterwards. With `headingRef` removed from
  `ProcessDefinitionPage.tsx:38`, exactly "moves focus to the frame's
  heading when the retry finds no organization" fails. With the per-type
  loop of `missingPart` (`processApi.ts:96-114`) emptied, all nine new rows
  fail: six in `processApi.test.ts` and three in the states outline. Both
  fixes therefore have tests that check what they claim.

Not re-run: the backend test suites, the full `scripts/verify.sh` and E2E
spec 20. The fix diff changes no backend behaviour, and the frontend
changes have no E2E path that QA's run did not already cover. No stand was
started.

**Verdict: needs-changes.** 0 blockers, 1 major, 0 minors open. F2..F5 are
resolved, and the fix diff raises no new finding. F1 is unchanged, because
its fix starts with a decision that neither the developer nor this
reviewer may record. This is the second of the two rounds the stage
allows, so F1 does not converge inside the loop and goes to the human, or
to the orchestrator if the 2026-10-01 delegation covers P-1.

### Previously open findings

| Finding | Round-2 status | Checked against |
|---|---|---|
| F1 (major) | **open** | status.md `## Decisions` has no P-1 entry. `design.md:121` (point 2), :123 (point 4), T-26, Adopting item 3 and the reconciliation row are unchanged. `AppLayout.process.test.tsx:78-90` still pins the opposite. |
| F2 (major) | resolved by (i), 651f27c | `processApi.ts:88-116` rejects every part the cards dereference: `ProcessDefinitionPage.tsx:70`, `ProcessTypeCard.tsx:58` and :72-81, and `CategoryCell.tsx:42`. The remaining fields are safe when missing: `typeAppearance` and `categoryColour` (`app/theme/workTracking.ts:68-70`) fall back on `undefined`, and a missing string renders empty. The tests are listed above. design.md § States stays as approved. |
| F3 (minor) | resolved, 4e59cd4 | `MigrationsGuard.cs:7-12`. The only caller is `DictionarySeeder.cs:22` (grep), and `DemonstrationProcessSeed.cs:70-83` reads `MigrationsState` and skips. |
| F4 (minor) | resolved, bcef95f | `useRetryFocus.ts:37-43`, `ProcessDefinitionPage.tsx:29-38`, `StateFrame.tsx:80-81`, :162. The ref is attached in the commit that mounts the frame, which runs before the effect that focuses it. The tests are `ProcessDefinitionPage.states.test.tsx:268` and :290, and `StateFrame.test.tsx:164` and :175. |
| F5 (minor) | resolved, b457faa | ADR-0021 § "Current state", the new paragraph. Both links resolve, and the ADR is `proposed`, so docs/adr/README.md rule 3 does not apply. |

### F1 (major, still open) — what is required, unchanged from round 1

1. P-1 is recorded in status.md `## Decisions`, by the orchestrator under
   the 2026-10-01 delegation or by the human.
2. Under (a): a design delta, which is the designer's, narrows design.md
   point 2 (:121), point 4 (:123), T-26, Adopting item 3 and the
   reconciliation row to plan-frontend § 6.3. Before `mod-002`, on any id
   but the placeholder's, the address shows no «Меню настроек», no trail and
   nothing current. QA's D-3 is closed against the decision. No product code
   changes.
3. Under (b): point 2 is marked deferred to a named shell line, and
   `AppLayout.process.test.tsx:78-90` gains a comment naming that line.

Resolution (developer, 2026-10-04, after round 2): still open, for the
orchestrator or the human. This is not a disagreement. At 84a355b,
status.md `## Decisions` still has no P-1 entry. Step 1 is the
orchestrator's or the human's, step 2 is the designer's and then QA's, and
step 3 needs a shell line that does not exist yet. Round 2 also takes F1 out
of the developer loop. So this round changes no code and no test for F1.
What the developer hands over for the call:

- Under (a), nothing is owed in code. `AppLayout.process.test.tsx:78-90`
  pins plan-frontend § 6.3's first row, and :93-111 pins the second (the
  trail «Настройки / Организация» once the context is real). The design
  delta can cite both as they stand. The comment at :78-80 says
  "P-1 (a)". That names the option the build follows (plan-frontend § 12),
  not a recorded decision.
- Under (b), no queued line is that shell line yet. The queued
  `bug-003-shell-address-case-navigation` is about letter case and
  percent-encoding. As queued, it still compares the address with an
  entry's landing in the current selection (`entryMarker`,
  `visibility.ts:107-121`, which `isSettingsPage` at :130 calls). (b) needs
  the settings-page rule to match a settings route by its pattern,
  whatever the selection. That is a new line in mod-015's area, or bug-003
  widened to cover it. Either is a queue edit, which is the orchestrator's.
  Once the line is named, the comment at :78-80 changes to name it. That is
  a comment-only change.

### Behavioural claims pass (item 6), over the fix diff

Direction 1, the prose the fix diff adds or changes:

- `processApi.ts:64-71`, "a `200` the screen cannot draw … is shown as a
  failed read … instead of throwing while it renders" → `processApi.ts:126-130`.
  The failed-read state is pinned by the three new rows of
  `ProcessDefinitionPage.states.test.tsx:156-173`.
- `processApi.ts:64-71`, "its name is not `TypeError`, so the banner gives
  the panel's existing generic text" → `processApi.ts:75` and the last
  branch of `shared/lib/apiErrorText.ts`. The states test "shows the
  panel's generic text for an answer with no types" pins it.
- `processApi.ts:84-87`, "only what rendering dereferences is checked; a
  missing text field shows as an empty cell" → the dereferences and
  fallbacks in F2's row above.
- `useRetryFocus.ts:12-21`, the three focus targets and "nothing happens on
  a first load" → :32-44. Pinned by the states tests at :268 and :290 and by
  the two older focus tests.
- `StateFrame.tsx:35-40`, with `headingRef` "the heading takes the ref and
  `tabIndex={-1}`; absent, it is as before" → :80-81. Pinned by
  `StateFrame.test.tsx:164` and :175.
- `MigrationsGuard.cs:7-12` → F3's row above. "Fails the start" holds, by
  round 1's anchors, `DictionarySeeder.cs:22` and the `Program.cs`
  ordering.
- ADR-0021, "with Russian names and no translation table", per
  organization → the `AddProcessDefinitions` migration, which round 1
  anchored (no translation table; `ux_processes_organization_id`). "The
  Problem and the Decision below still name them" → ADR-0021 § Problem and
  the Decision's closing sentence, both unchanged.
- plan-frontend § 13: it overrides § 3.2, § 4.1 and § 8.3 on the class name
  → the old name is on lines 151, 179 and 403-404, which are exactly those
  sections. "`StateFrame.test.tsx` only adds tests" → its diff adds two
  imports and one `describe` and changes nothing else.
- qa.md D-4 and D-5 "Fix" lines: "six rows", "three rows", "two in
  `StateFrame.test.tsx`" and "fails without the ref" → counted, and the
  last one confirmed by the mutation above.
- review.md, the round-1 Resolution under F4, "a first load that finds no
  organization still moves no focus" → `ProcessDefinitionPage.states.test.tsx:290`.
- status.md, the dev Log line of the round-1 answer: "Vitest 67 files, 655
  tests" → re-run here, same count. "Backend 848 incl. Api.IntegrationTests
  390" → not re-run here. This is a named gap, and the backend change is a
  comment.

Direction 2, the behaviour the fix diff changes and the prose outside it
that describes the old behaviour:

- `getProcessDefinition` now refuses a partial body, and its error class is
  renamed. Grepped `EmptyProcessDefinitionError`, `UnreadableProcessDefinitionError`
  and "cannot be read" in `src/` and `docs/`. Hits:
  - plan-frontend §§ 3.2, 4.1 and 8.3, which § 13 overrides by name;
  - qa.md D-4, updated;
  - design.md § States (:271-274), now true.

  Nothing stale is left.
- Focus after a retry that ends in not found. Grepped "focus" in
  design.md, `docs/conventions/frontend.md`, ADR-0007, ADR-0019, ADR-0024
  and mod-015 design.md § 7 and § 12.5. design.md § Focus (:312-316) names
  no target for this outcome; it is silent, not contradicted. The others
  say nothing about programmatic focus. Nothing is stale.
- `StateFrame` and `NotFound` gain an optional `headingRef`. Grepped
  `StateFrame` and `headingRef` across `docs/`, `.claude/` and the
  `AGENTS.md` files. Hits:
  - ADR-0007:11, frontend.md:70, mod-015 design.md § 7.1 and its plan and
    QA rows: each describes the frame's variants and heading levels, and all
    still hold, because the prop is optional and changes nothing when
    absent.
- `MigrationsGuard` names its one caller. Grepped "hosted seeder" and
  `MigrationsGuard` in `docs/`, `.claude/`, `src/backend` and the
  `AGENTS.md` files. The only hits are this file's round-1 text, which is
  history.
- ADR-0021 now excludes work item types. Round 1 already swept "work item
  type" in `docs/adr` and `docs/conventions`, and the ADR index line in
  docs/adr/README.md (title and status) is unchanged. Nothing more to sweep.

### Notes (not findings)

1. **What the F2 check still lets through.** `missingPart` checks structure,
   not leaf types. A `200` that carries an object where a string belongs,
   for example `name: {}`, would still throw in render: React refuses an
   object child. I read design.md's "an answer that cannot be read" as the
   structural case that the fix and its tests cover. The contract pins the
   types (`HasExactlyTheContractShapeAndNoNulls`), as round 1 said, so this
   is recorded rather than raised.
2. **Frontend QA over the fixes is not recorded yet.** 06-code-review.md
   says that QA is re-run over what was touched. qa.md D-4 and D-5 each end
   "For QA to re-check" and still read `Status: open`. Scheduling that
   re-check is the orchestrator's.
3. **design.md § Focus** names no target for the new outcome. The code now
   fills that gap and contradicts nothing. Adding one sentence is the
   designer's optional delta, as the F4 Resolution says.
4. **ADR-0021's new paragraph** sits under "Current state (2026-09-22)"
   while it describes mod-001. Its own "from mod-001-process-seed on" dates
   it, so nobody would misread it. This is editorial only.

Developer (2026-10-04), on the notes:

- Note 4 is fixed in 222aff0. The paragraph now opens with "Added
  2026-10-04, after the date above". Nothing else in the ADR changed, and it
  stays `proposed`.
- Note 1 is not acted on. The reviewer reads design.md's "an answer that
  cannot be read" as the structural case. The contract and
  `HasExactlyTheContractShapeAndNoNulls` pin the leaf types. A product code
  change now would also land after the two-round cap, with no review round
  left to read it. If the orchestrator wants leaf types checked as well, it
  is a short addition to `missingPart` (`processApi.ts`) with one test row
  per field, for a later round or a follow-up line.
- Note 2 is the orchestrator's to schedule, and note 3 is the designer's
  optional delta.

## Code review, round 3 — backend + frontend (2026-10-04)

Stage 8, round 3, deep, run by a session that wrote none of this. This round
is a delta over round 2. It covers F1, the one finding still open, the
round-2 notes as the developer answered them, and the fix diff
`git diff 84a355b fc7ce6a`. That diff is two commits over three prose files:
222aff0 on ADR-0021, and fc7ce6a on this file and status.md. The round lies
past the stage's two-round cap. The orchestrator scheduled it, and it does
not put F1 back into the developer loop, because F1's fix is not the
developer's.

`main` is at 282a759, three commits past the branch's base c48b565. Two are
queue commits. 282a759 is bug-002's fix, which changes only
`SessionLogoutEndpointTests.cs` and bug-002's own documents. `git merge-tree
--write-tree main mod/001-process-seed` reports no conflict. The rebase
before the merge (`scripts/finish-task.sh`) is still owed, and it is not
this round's.

Re-run here at fc7ce6a:

- `BASE=84a355b bash scripts/verify.sh`: the fix diff classifies as prose
  only. All four prose checks pass (links, backlog ids, the status.md
  budget, spec hygiene), and the RESULT is green.
- C-1: a case-insensitive search of the fix diff and of its two commit
  messages for the reference tracker's product, vendor and abbreviations
  finds nothing.

Not re-run: the backend and frontend suites, the full `scripts/verify.sh` and
E2E spec 20. No code, test, configuration or script has changed since round
2, which re-ran the whole Vitest suite green at 0e91ef9, and this worktree
has no `node_modules`. The status Log's "run 3 RESULT green (backend 848 …)"
therefore stays the developer's claim, which is a named gap. No stand and no
background process was started.

**Verdict: needs-changes.** 0 blockers, 1 major and 0 minors open. F1 is
unchanged and stays with the orchestrator or the human. The fix diff raises
no new finding.

### Previously open findings

| Finding | Round-3 status | Checked against |
|---|---|---|
| F1 (major) | **open** | status.md `## Decisions` at fc7ce6a holds no decision on plan-frontend P-1. D-13 (status.md:39) settles plan-backend's P-1, which is a different point. design.md last changed at b81c780, the design check. Point 2 (:121), point 4 (:123), T-26 (:356), Adopting item 3 (:397) and the reconciliation row (:416) therefore still promise «Меню настроек» and the trail «Настройки» on the process address before `mod-002`. `AppLayout.process.test.tsx:81-90` still pins the opposite, and qa.md D-3 (:224, status at :241) is still open. |

What F1 requires is unchanged from round 1 and round 2:

1. plan-frontend P-1 is recorded in status.md `## Decisions`, by the
   orchestrator under the 2026-10-01 delegation or by the human at gate-3.
   Name it "plan-frontend P-1" there, because D-13 already carries
   plan-backend's P-1.
2. Under (a), the designer narrows design.md point 2 (:121), point 4 (:123),
   T-26 (:356), Adopting item 3 (:397) and the row at :416 to plan-frontend
   § 6.3 (:311-313). Before `mod-002`, on any id but the placeholder's, the
   address shows no «Меню настроек», no trail and nothing current, and the
   trail «Настройки / Организация» appears once the context is real. QA then
   closes D-3 against the decision. No product code or test changes:
   `AppLayout.process.test.tsx:81-90` and :94-111 already pin both rows.
3. Under (b), design.md point 2 is marked deferred to a named shell line,
   which first has to be queued (a new line, or bug-003 widened), and the
   comment at `AppLayout.process.test.tsx:78-80` names that line.

The round-2 notes, as answered:

| Note | Round-3 status | Checked against |
|---|---|---|
| 1, leaf value types | accepted as not acted on | Round 2's wording needs one correction. `Get_SeededOrganization_HasExactlyTheContractShapeAndNoNulls` (`GetProcessDefinitionEndpointTests.cs:146-174`) pins the property names, the nesting and the absence of nulls, but it asserts no leaf kind. The leaf kinds come from the static types of the response records: `Guid`, `string`, `int` and `bool` in `ProcessDefinitionResponse.cs:21`, :29-35, :38, :45 and :49. A non-null `string` serializes only as a JSON string. Taken together, the developer's "the contract and the test pin the leaf types" holds. |
| 2, QA over the fixes | open, the orchestrator's | qa.md D-4 (:264-271) and D-5 (:288-297) end "For QA to re-check". Their Status lines (:260, :286) still read open, and no re-check is recorded. 06-code-review.md requires QA to be re-run over what a blocker or major fix touched, and F2's fix touched `processApi.ts`. This is owed before gate-3. |
| 3, design.md § Focus | unchanged, the designer's optional delta | design.md is unchanged since b81c780. |
| 4, ADR-0021's date | resolved, 222aff0 | `docs/adr/0021-localized-dictionaries-pattern.md:34` opens "Added 2026-10-04, after the date above", under the heading at :11. |

### Behavioural claims pass (item 6), over the fix diff

Direction 1, the prose the fix diff adds or changes:

- ADR-0021:34, "Added 2026-10-04, after the date above" → the heading
  "Current state (2026-09-22)" at :11. The rest of the paragraph is
  b457faa's text, rewrapped, and round 2 anchored it.
- review.md, the developer's round-2 answer under F1:
  - "status.md `## Decisions` still has no P-1 entry" → it holds for
    plan-frontend's P-1, but D-13 (status.md:39) is labelled "plan-backend
    P-1". The sentence is correct in context, and round 2 used the same
    wording. Note 1 below asks that the decision be named in full.
  - "`AppLayout.process.test.tsx:78-90` pins plan-frontend § 6.3's first
    row, and :93-111 pins the second" → the tests at :81-90 and :94-111
    against plan-frontend.md:311 and :312.
  - "The comment at :78-80 says 'P-1 (a)'" → :78.
  - "The queued `bug-003-shell-address-case-navigation` is about letter case
    and percent-encoding" → docs/backlog.md:46, the same line on the branch
    and in `main`.
  - "As queued, it still compares the address with an entry's landing in the
    current selection (`entryMarker`, `visibility.ts:107-121`, which
    `isSettingsPage` at :130 calls)" → the file is
    `app/navigation/visibility.ts`. `entry.landing(selection)` is at :112,
    and `isSettingsPage` (:130-137) calls `entryMarker` at :136. The queue
    line's "match routes the way the router does (matchPath)" names how the
    address is compared, not what it is compared against. So the reading
    that it leaves the selection-based landing alone is fair. Widening it is
    the orchestrator's call, as the answer says.
  - "Once the line is named, the comment … changes" → future work, not a
    claim.
- review.md, the developer's answer on the notes:
  - "Nothing else in the ADR changed, and it stays `proposed`" → the ADR's
    hunk in the fix diff covers :34-36 only, as a rewrap, and the status is
    at :3;
  - "The contract and `HasExactlyTheContractShapeAndNoNulls` pin the leaf
    types" → note 1's row above;
  - "a short addition to `missingPart`" → future work, not a claim.
- status.md, the dev Log line of the round-2 answer:
  - "no code or test changed" → `git diff --stat 84a355b fc7ce6a` lists
    three prose files;
  - "Note 4 fixed, 222aff0" → note 4's row above;
  - "runs 1 and 2 red on bug-002's order-dependent
    `SessionLogoutEndpointTests.Logout_UnexpectedKeycloakError_…` only" →
    not reproduced here. It agrees with 282a759 in `main`, whose message
    names the same test and the same order dependence;
  - "run 3 RESULT green (backend 848 incl. Api.IntegrationTests 390, Vitest
    67 files, 655 tests)" → the Vitest count matches round 2's re-run at
    0e91ef9, and no frontend file has changed since. The backend counts were
    not re-run, which is the named gap above.

Direction 2, the behaviour the fix diff changes: none. The diff changes
three prose files and no code, configuration, script or test. The one fact
it changes is the ADR-0021 paragraph's date. A search for "Current state"
next to ADR-0021 across the repository found only this file's round-1 and
round-2 text and the status Log, all of it history. Outside the fix diff,
`main` changed behaviour in 282a759: bug-002's test now runs its logout on
a child host of its own. A search of the branch's `src/`, `docs/` and
Markdown files for `SessionLogout`, `bug-002` and "order-dependent" ran
outside this feature's status.md and review.md and outside bug-002's own
folder. Leaving aside the Session feature's own identifiers, in code the
branch does not touch, it found three hits:

- qa.md:220, a dated run row, which is history;
- docs/backlog.md:15, the branch's copy of the queue, which the rebase
  replaces with `main`'s;
- `StaticLoggerGuard.cs:17-19`, a "found:" remark that is still true as
  history (note 2).

Nothing stale is left in the live prose.

### Notes (not findings)

1. **Name the decision in full.** When F1's decision is recorded, label it
   "plan-frontend P-1". D-13 already settles "plan-backend P-1", and both
   plans' P-1 recommended (a). A reader who searches Decisions for "P-1"
   would otherwise take F1 as decided.
2. **For tech-006, queued in `main` at abeb23a.** Once this task merges,
   `Features/Process/StaticLoggerGuard.cs` and its three users
   (`DemonstrationProcessSeedTests.cs:114` and :143,
   `GetProcessDefinitionEndpointTests.cs:416`) exist in `main`. tech-006
   sets `preserveStaticLogger: true`. After that the guard protects
   against a state that no longer arises, and its remarks (:10-19)
   describe `Program.cs` as it is today. bug-002's list of exposed tests
   predates this branch and does not name the guard. tech-006's sweep
   should include it. That is for the orchestrator when tech-006 is taken,
   not for this task.
3. **Round count.** This is round 3 against a two-round cap. F1 has not
   converged because it waits on a decision, not on code. This round
   therefore records the same state as round 2 and escalates nothing new.

## Code review, round 4 — frontend (2026-10-04)

Stage 8, round 4, deep, run by a session that wrote none of this. This round
is a delta over round 3. It covers F1, the round-3 notes, and
`git diff 56efcc1...a0c1aa4`: seven commits over six files. b7e14ae records
D-15. 344c91f is the design delta, 732b959 the prototype, c4a7360 and
629ebf6 are QA re-check 1 with its new test file, and 8b900ed and a0c1aa4
close QA's D-6 and D-7. Only one file in the delta is code,
`ProcessDefinitionPage.unreadable.test.tsx`, and it is a test. The shell is
unchanged: `git diff main...HEAD` touches no `AppLayout.tsx`, no
`navigation/visibility.ts` and no `app/shell/` file.

`main` is at 282a759. `git merge-tree --write-tree main mod/001-process-seed`
exits 0 with tree 719e636, so there is no conflict. The rebase before the
merge (`scripts/finish-task.sh`) is still owed, and it is not this round's.

Run here at a0c1aa4. Each mutation was restored with `git checkout`, the two
probes were deleted, and the tree was clean afterwards.

- `npm ci` in `src/frontend`, because the worktree had no `node_modules`:
  390 packages, exit 0.
- `npx vitest run` over `ProcessDefinitionPage.unreadable.test.tsx` and
  `AppLayout.process.test.tsx`: 2 files, 32 tests, exit 0.
- Five mutations, run against the new file:
  - M1: «Повторить» focus removed (`useRetryFocus.ts:42`). 26/26 pass. The
    whole of `src/features/process` and `src/shared/ui` also passes, 25
    files and 185 tests: **F6**.
  - M2: the retry guard removed (`:33`), so focus also moves on a first
    load. 1 test fails, "moves no focus when the first read already finds no
    organization".
  - M3: the title focus removed (`:38`). 1 test fails, the success retry.
  - M4: `missingPart`'s per-type loop emptied (`processApi.ts`). 17 fail
    and 9 pass. This is QA's figure.
  - M5: the not-found focus removed (`:40`). 4 fail and 22 pass. These are
    QA's 4 of the 6 D-5 tests.
- Two temporary probes, deleted and never committed:
  - The retry. With an immediate second 500, the skeleton never renders,
    and «Повторить» is the same DOM node as the one clicked. With the
    second answer held, the test passes, and it fails under M1.
  - The placeholder id on the process address: F8.
- `node --check` on the prototype's script: exit 0.
- `bash scripts/verify.sh`, the whole branch against `main`: exit 0,
  `RESULT: green`. The backend ran 848 tests: Shared 53, Architecture 68,
  Domain 102, Contracts 61, Application 60, Api.IntegrationTests 390 and
  Infrastructure 114. Vitest ran 68 files and 681 tests.
- C-1: a case-insensitive search of the delta and its seven commit messages
  for the reference tracker's product, vendor and abbreviations finds
  nothing.

Not run: E2E spec 20. QA's limit at qa.md:335-341 stands, and it is named
there: spec 20.3 has not run in a browser since `useRetryFocus` changed. No
stand and no background process was started.

**Verdict: needs-changes.** 0 blockers, 1 major and 3 minors are open. F1 is
resolved against D-15. The new major, F6, is a test-strength defect, not a
product defect. Three QA tests in the delta, and one developer test from
round 1, claim to pin focus returning to «Повторить» after a repeated
failure. All four pass with the code that moves the focus removed, and
qa.md says the new tests are not vacuous. F7..F9 are wording and drawing
leftovers of the F1 delta.

### Previously open findings

| Finding | Round-4 status | Checked against |
|---|---|---|
| F1 (major) | **resolved** (D-15, 344c91f, 732b959, 8b900ed) | Decision: status.md:41 names it "plan-frontend P-1", so round 3 note 1 is met. design.md point 2 (:121), point 4 (:123), T-26 (:359), Adopting item 3 (:400-404), the trail row (:417) and the settings-page row (:423) now say what plan-frontend § 6.3 (:311-313) says. Before `mod-002`: no «Меню настроек», no trail and nothing current. The trail «Настройки / Организация» appears once `mod-002` makes addresses set the selection. Beside the not-found frame there is never a menu (:423, last sentence). The code: `visibility.ts:107-137` and :152-163, and `AppLayout.tsx:101-111` and :121-125, with the shell unchanged. The tests: `AppLayout.process.test.tsx:81-90`, :94-111 and :127-137, and `routes.process.test.tsx:116-131`. No product code or test changed, as F1 required. Two wording leftovers are F7 (point 10) and F8 (the placeholder id). |

The round-3 notes:

| Note | Round-4 status | Checked against |
|---|---|---|
| 1, name the decision in full | resolved | status.md:41 opens "D-15 (plan-frontend P-1 → (a)". |
| 2, tech-006 and `StaticLoggerGuard` | unchanged, the orchestrator's | The delta does not touch it. |
| 3, round count | superseded | See note 3 below. |

The round-3 note on QA (round 2 note 2): **discharged for D-4 and D-5
themselves.**

- qa.md § Re-check 1 (:325-417) closes D-4 (:270-276) and D-5 (:301-304).
- Both of QA's mutation figures reproduce here (M4: 17/9; M5: 4 of 6).
- `verify.sh` is green above.
- What it does not discharge is the adjacent claim that focus returns to
  «Повторить». That claim is F6.

### Item 1, F1 against the build: each claim and its anchor

- design.md:121, point 2. Before `mod-002`, no «Меню настроек», no trail and
  nothing current in the rail, a «Языки» reader included. Anchors:
  - the «Процесс» landing in the placeholder selection is `org-1`'s
    (`model.ts:44-45`, `placeholderContext.ts:17`), so `isSettingsPage` is
    false (`visibility.ts:130-137`);
  - so `settingsShown` is false (`AppLayout.tsx:110-111`), which hides the
    menu (:197, :231) and the rail mark (`MainNavigation.tsx:171`);
  - the trail is derived from visible entries only (`visibility.ts:152`);
  - pinned for a `LANGUAGES_READ` user by `AppLayout.process.test.tsx:81-90`.

  The scope is wider than the code: **F8**.
- design.md:121, point 2. Once `mod-002` makes addresses set the selection,
  the menu shows, «Процесс» is `page` and the rail mark is `true` →
  `AppLayout.process.test.tsx:94-107`, through the stub real source at
  :32-38.
- design.md:123, point 4, and T-26 (:359). The trail is «Настройки /
  Организация», the page passes no ancestors, and there is none before
  `mod-002` → `visibility.ts:156`, `ProcessDefinitionPage.tsx:50` (no
  `ancestors`) and `AppLayout.process.test.tsx:108-110`, :88.
- design.md:175-177 and :181, the drawing's lead-in and trail →
  illustrative, and the trail matches point 4. The drawing's settings
  column has no «Организация» section: **F9**.
- design.md:400-404, Adopting item 3 → the same anchors as point 2 and
  point 4.
- design.md:417, the trail row → `visibility.ts:156`;
  `AppLayout.process.test.tsx:109`.
- design.md:423, the settings-page row. The before-`mod-002` half has point
  2's anchors (and F8). "Beside the not-found frame there is no «Меню
  настроек» at any time" → `AppLayout.tsx:101-108` (`currentPage` is
  `null`) and `visibility.ts:113-115`, whatever the source. Pinned for the
  real source by `AppLayout.process.test.tsx:127-137` and for the
  placeholder source by `routes.process.test.tsx:116-131`, whose default
  user can see «Языки».
- design.md:129, point 10, "no settings entry is marked current". QA read it
  as covering the rail (qa.md:386-387). It needs more than one word: **F7**.
- The prototype (`process-definition.html`):
  - the note at :146-151 → `layoutB()` at :299-325;
  - the trail (:307), the menu (:308-313) and the rail mark (:314) are each
    gated on `real && !notFound`;
  - so the placeholder context draws no menu, no trail and no rail mark,
    and «Организация не найдена» has none of the three in either context.
    D-6 holds.
- plan-frontend reads P-1 as decided in each section, so D-7 holds:
  - § 6.3, :315-326;
  - § 7, :351;
  - § 11, :527-534;
  - § 12, :560-561;
  - § 13, :601-604.

  § 7's left-hand cell at :345 ("Trail «Настройки», one segment") records
  what design.md derived at the time. It is history, and the right-hand
  cell carries the answer.

### Item 2, the new QA tests

`ProcessDefinitionPage.unreadable.test.tsx` holds 26 tests. They render
`ErrorBoundary`, the query client, `AppRoutes` and the real shell. The
theme comes through the Vitest alias (`vite.config.ts:47-53`). So a throw in
render reaches the crash screen, as it does in `App.tsx`. The 15 shapes and
3 body kinds (:118-231) check the three missing parts and their wrong-shaped
variants positively. Each finds the alert's title and «Повторить», the
banner and the `h1` «Процесс», and finds no card. The crash-screen text they
assert absent (:100) matches `ErrorBoundary.tsx:61` exactly. M4 shows they
fail on the old check. The not-found focus tests (:271-339) assert the
focused node, `tabindex="-1"` and the single `h1`, and M5 makes four of them
fail. The first-load test (:359-365) fails under M2, and the success retry
(:233-251) fails under M3.

Three assertions pass vacuously: the «Повторить» focus at :263-265, at
:316-317 and at :353-355. That is F6.

### F6 (major) — four "focus back on «Повторить»" tests pass with the code that moves the focus removed, and qa.md certifies them

Where it is:

- `ProcessDefinitionPage.unreadable.test.tsx:253-268`, "brings the alert
  back, with focus on its «Повторить»…";
- :310-324, its second-500 step at :316-317;
- :341-357, "puts focus back on «Повторить» when a retry after a 500 gets an
  unreadable 200";
- the developer's `ProcessDefinitionPage.states.test.tsx:237-255`. Round 1
  listed it under "Tests check what they claim", which was wrong for this
  test.
- The prose that relies on them:
  - qa.md:346 (check 2, "a retry from a broken answer to another (alert
    back, focus on «Повторить»)" ☑);
  - :348 (check 3, "focus on «Повторить» after the second 500", "focus back
    on «Повторить»" ☑);
  - :357 ("the new tests fail against the opposite behaviour, so they are
    not vacuous").

What happens: each test answers the retry at once. In jsdom the retry's
fetching state and its failure settle in the same render, so the skeleton
never replaces the alert (`ProcessDefinitionPage.tsx:41`, :59). The
«Повторить» that `user.click` focused stays mounted and keeps that focus.
The probe showed the same node, still connected, and no skeleton. With
`retryRef.current?.focus()` (`useRetryFocus.ts:42`) removed (M1), all 26
tests of the file pass, and so does every test in `src/features/process`
and `src/shared/ui`. So nothing in the suite pins design.md's "when it fails
again, focus moves to the new alert's «Повторить»" (:316-317). The code is
right by reading (`useRetryFocus.ts:41-42`, and `ErrorBanner`'s `retryRef`).
The behaviour has no test that fails without it, and the QA report says the
opposite. A later change to `useRetryFocus` relies on a guarantee that is
not there, so the severity is major (06-code-review.md, "Severity").

Proposed fix (tests and qa.md only; no product code):

1. In the four tests, hold the retry's answer until the skeleton is on
   screen. Use the `gate()` of `states.test.tsx:78`, as the success test at
   :209-235 already does. While the answer is held, assert the status
   «Загрузка данных» and no `alert`. Then release it and assert that the
   new «Повторить» has focus. The probe's held variant does exactly this:
   it passes as the code stands and fails under M1.
2. Re-run M1. All four must fail.
3. In qa.md, correct :357 to name the mutations that were run, and add M1.
   Re-check rows 2 and 3 over the rewritten tests. That is QA re-run over
   what was touched.

Status: resolved (76e1244, 0d0f983). The four tests now hold the retry's answer
with `gate()`, which moved from `states.test.tsx` to `src/test/gate.ts`. While
it is held each asserts the status «Загрузка данных», no `alert` and that the
clicked button is gone. It then releases the answer and asserts that the new
«Повторить» is another node and has focus. M1 re-run: with
`retryRef.current?.focus()` removed, exactly the four fail on `toHaveFocus()`
(46 of 50 pass in the two files); restored, 50/50, the whole suite 68 files and
681 tests. qa.md: row 2 (:346), row 3 (:348) and the mutation row (:357) now say
the first-written claim held for two mutations only, and a "Correction F6"
section records the vacuous assertion and the fix. QA's re-run of rows 2 and 3
over the rewritten tests (step 3) is still owed, and is QA's.

### F7 (minor) — design.md point 10 says neither that there is no «Меню настроек» beside the frame nor that the rail has no mark

Where it is: design.md:129, layout B, "no page trail (mod-015 US-2); no
settings entry is marked current, since the address names no real context".

What the code does is wider. Beside the frame `currentPage` is `null`
(`AppLayout.tsx:101-108`), so there is no «Меню настроек» and no `aria-current`
in the main navigation, the rail's «Настройки» included
(`AppLayout.tsx:110-111`, `MainNavigation.tsx:171`). It is pinned by
`AppLayout.process.test.tsx:127-137` and `routes.process.test.tsx:127-131`.

Point 10's wording does not cover that:

- «Настройки» is not a settings entry in mod-015's own words ("«Настройки»
  is not an entry", `model.ts:13`; "derived, not an entry", mod-015
  plan-frontend.md:587).
- Point 10 is silent on the menu itself. Inside design.md only the last
  sentence of the row at :423 says so.
- Yet the prototype's comment (`process-definition.html:305-306`) cites
  point 10 for "no «Меню настроек» and no rail mark", and qa.md:386-387
  leaves the rail to a reading.

Nothing an agent does differs, because plan § 6.3 row 3 and the row at :423
say it. So this is minor. The answer to the question put to this round:
more than one word is needed.

Fix: point 10 ends "…no page trail (mod-015 US-2); no «Меню настроек», and
nothing marked current in either navigation, the rail's «Настройки»
included, since no page serves the address (mod-015 BR-13)".

Status: resolved (237c33c). design.md point 10 now ends "no page trail (mod-015
US-2); no «Меню настроек», and nothing marked current in either navigation, the
rail's «Настройки» included, since the address names no real context". The
existing reason clause stays; BR-13 is not cited, since it covers the unknown
address and a context not found, and the clause already says why.

### F8 (minor) — design.md states the before-mod-002 navigation for "this address" with no exception; on the placeholder's own id the shell shows «Меню настроек» while the read is pending

Where it is:

- design.md:121, point 2, "the shell treats this address as outside the
  current selection, so the page shows no «Меню настроек»…";
- :176, the drawing's lead-in;
- :402-404, Adopting item 3;
- :423, the settings-page row.

plan-frontend § 6.3 row 1 (:311) scopes the same row to "any id other than
the placeholder organization's", and so did F1's required text (round 1,
:970-971; round 3, :1550-1552). The delta dropped the scope.

What the code does on `/organizations/org-1/settings/process` (the
placeholder ids are at `placeholderContext.ts:17` and :36):

- the «Процесс» landing in the selection is that address (`model.ts:44-45`),
  so `entryMarker` gives `page` (`visibility.ts:112-118`) and
  `isSettingsPage` is true;
- a «Языки» reader therefore gets «Меню настроек» and «Настройки» current in
  the rail while the read is pending (`AppLayout.tsx:110-111`);
- the read then answers not found (`org-1` is malformed for the API), and
  the frame removes both (:101-108).

The probe confirmed it: the menu was present, with one `aria-current` in the
main navigation, while the read was pending, and both were gone after the
answer. The case is a hand-typed malformed id, and only for the time the
read takes. Nothing an agent does differs, so the severity is minor.

Fix: point 2 and the row at :423 add "on any id but the placeholder
organization's (plan-frontend § 6.3 row 1)". Item 3 and the lead-in already
defer to point 2.

Status: resolved (237c33c). Point 2 and the settings-page row at :423 now say
"on any id but the placeholder organization's (plan-frontend § 6.3 row 1)". The
drawing's lead-in and Adopting item 3 state the rule too, so both carry the
limit as well, and still cite point 2.

### F9 (minor) — the layout B drawing and the prototype draw «Процесс» with no «Организация» section, under a trail that now names it

Where it is:

- design.md:183-191. The settings column reads «Настройки», «Процесс <--»,
  «Справочники», «Языки». The trail above it, :181, now reads «Настройки /
  Организация».
- `process-definition.html:308-312`. The real-context menu has a section
  label for «Справочники» and none for «Процесс».
- design.md:416, the first reconciliation row. It still has "Its section,
  order and icon" to confirm, under the derivation "beside «Организация»,
  «Проект» and «Справочники»".

The build puts «Процесс» inside the «Организация» section
(`model.ts:36-46`), and the shell draws that label
(`AppLayout.process.test.tsx:99-100`). mod-015 design § 15 (:1175-1176)
answered the row ("the third entry of the «Организация» section", no icon).
It also asked that "mod-001's T-26 and its layout B sketch change". The
delta changed the sketch's trail only. plan-frontend § 7 (:344) records the
answer. The drawing is illustrative, so the severity is minor.

Fix:

- draw «Организация» above «Процесс» in the drawing and in the prototype's
  `settingsMenu`;
- mark the row at :416 as settled: «Процесс» is the first entry of
  «Организация» today and the third once «Общие» and «Участники» exist, and
  it has no icon (mod-015 design § 15; `model.ts:18-19`).

Status: resolved (237c33c). The layout B drawing's settings column now reads
«Настройки», «Организация», «Процесс <--», «Справочники», «Языки» (the right
column is unchanged), and the prototype's `settingsMenu` has the section label
«Организация» before «Процесс». The reconciliation row's second column now says
"Settled by `mod-015`'s design § 15": «Процесс» is an entry of «Организация»,
the first today and the third once «Общие» and «Участники» exist, with no icon,
so the `AccountTreeOutlined` suggestion is dropped; the build registers it so
(`app/navigation/model.ts`). The prototype's script passes `node --check`.

### Behavioural claims pass (item 6), over the delta

Direction 1, the prose the delta adds or changes:

- status.md:41, D-15, "the shell stays as merged" → `git diff main...HEAD`
  touches no shell file.
- D-15, "before mod-002 … no «Меню настроек», no trail and nothing marked
  current, because … the demonstration id never matches the placeholder" →
  item 1's point-2 anchors. `d0000000-…0001` is not `org-1` or `org-2`.
- D-15, "the trail «Настройки / Организация» shows once mod-002 …" →
  `AppLayout.process.test.tsx:108-110`. When `mod-002` does it is future
  work of another line (mod-015 design § 5.7).
- D-15, "design.md … narrowed …; AppLayout.process.test.tsx keeps pinning
  both rows" → 344c91f, and the test file is untouched by the delta.
- design.md:121, :123, :175-181, :359, :400-404, :417 and :423 → item 1,
  with F8 and F9.
- design.md:317-319, "When the retry ends in «Организация не найдена» …
  focus moves to the frame's heading …, which takes `tabIndex={-1}`" →
  `useRetryFocus.ts:39-40`, `ProcessDefinitionPage.tsx:37-38`, and the
  `StateFrame` `headingRef` (round 2: `StateFrame.tsx:80-81`). Pinned by
  `unreadable.test.tsx:285-296` (M5) and `states.test.tsx:268-288`.
- design.md:316-317, the unchanged sentence "when it fails again, focus
  moves to the new alert's «Повторить»" that the delta's tests claim to
  pin → code `useRetryFocus.ts:41-42`, and no test that fails without it:
  **F6**.
- design.md:129, point 10, as the prototype now cites it: **F7**.
- prototype :146-151 and the comment at :301-306 → :299-325 (item 1). The
  comment's citation of point 10: F7.
- plan-frontend :315-326, :351, :527-534, :560-561 and :601-604 → status.md:41.
  The commits named in :532-533 exist (344c91f, 732b959). "Those tests stand
  as written" holds, because `AppLayout.process.test.tsx` is not in the
  delta.
- qa.md:245-250, D-3 closed → item 1 (F7 and F8 are wording leftovers that
  do not reopen it).
- qa.md:270-276, D-4 closed, "never the crash screen" →
  `processApi.ts` `missingPart` and `unreadable.test.tsx:118-231`,
  non-vacuous by M4.
- qa.md:301-304, D-5 closed → `useRetryFocus.ts:39-40` and
  `unreadable.test.tsx:272-339`, non-vacuous by M5.
- qa.md:346, check 2:
  - the 18 cases and the 2 retry cases → counted at :119-268;
  - "17 of the 26 tests fail" → reproduced by M4;
  - "alert back, focus on «Повторить»" → **F6**.
- qa.md:347, check 2a, "an object where a text belongs still crashes" → not
  reproduced here. QA deleted its probe, and rounds 2 and 3 accepted it
  (note 1 there). This is a named gap.
- qa.md:348, check 3:
  - "4 of the 6 fail" → reproduced by M5;
  - the «Повторить» halves → **F6**.
- qa.md:357, "so they are not vacuous" → it holds for the two mutations
  QA ran, and not for M1: **F6**.
- qa.md:360 and the status.md Log line at :46 (as at a0c1aa4): "`verify.sh` exit 0, RESULT
  green, Vitest 68 files, 681 tests" → the same result and counts here.
- qa.md:378-387, D-6 resolved → `process-definition.html:314`. "Point 10 …
  is read to cover the rail": F7.
- qa.md:402-408, D-7 resolved, "No other content of the plan changed, and no
  test" → 8b900ed's plan hunks are exactly § 6.3, § 7, § 11, § 12 and § 13,
  and the commit touches no test.
- The status.md Log lines, numbered as at a0c1aa4:
  - :45 and :47-48, "docs only" and "no code or test touched" →
    `git log --stat` of 8b900ed, a0c1aa4, 344c91f and 732b959 lists only
    `docs/` files;
  - :49, "plan-frontend P-1 decided → (a)" → :41.

Direction 2, the behaviour the delta changes. The delta changes no product
code. What it changes is prose: the documented navigation state of the
process address before `mod-002`, T-26's copy («Настройки» becomes
«Настройки / Организация»), the documented focus target after a retry that
ends in not found, the prototype's drawn states, and P-1's status. It adds
26 tests.

Searched for «Меню настроек», «Настройки /», the old "trail «Настройки»",
T-26, "settings page", `railCurrent` and `P-1` across `docs/`, `.claude/`,
the `AGENTS.md` files and `src/` (excluding `node_modules`). The hits outside
this feature's history:

- mod-015 design.md:1175-1176 (§ 15). It asks for T-26 and the sketch to
  change: the trail is done, and the section is F9.
- mod-015 spec.md:132, BR-19, "the current address is a settings page
  (§ Navigation model)". The merged code defines a settings page relative to
  the current selection (`visibility.ts:123-137`), and D-15 adopts that
  reading. mod-015 is merged, and its spec is not this task's to amend: note
  1.
- mod-015 plan-frontend.md:587, "«Настройки» is derived, not an entry". It
  is consistent, and it is the source of F7's ambiguity.
- `docs/conventions/frontend.md:47-48` and :67, and ADR-0024:107, describe
  settings pages and the «Настройки / <section>» trail generically. They are
  still true.
- `src/backend/AGENTS.md:97` names the panel address and says nothing about
  the navigation. It is still true.
- `e2e/specs/20-process-definition.spec.ts`: no hits.
- `AppLayout.process.test.tsx:78`, "P-1 (a)", now matches a recorded
  decision.
- `DemonstrationProcessSeedTests.cs:53` and :178, "plan P-1", mean
  plan-backend's (D-13, as :178 says): note 2.

For focus, the round-2 sweep (frontend.md, ADR-0007, ADR-0019, ADR-0024,
mod-015 design § 7 and § 12.5) found nothing about programmatic focus, and
the delta adds the target only to design.md.

Not swept: mod-002's documents, which are not on this branch. D-15's "once
mod-002 makes addresses set the selection" is that line's future work, and
it is cited through mod-015 design § 5.7.

### Notes (not findings)

1. **mod-015 BR-19 and the merged shell.** BR-19 shows «Меню настроек» on a
   settings address with a visible settings entry. The merged rule keys a
   settings page on an entry's landing in the current selection. These agree
   except where an address carries an id the selection does not hold, which
   before `mod-002` is every real id. D-15 takes the code's reading for this
   task. If `mod-002` slips, the orchestrator may want one line in mod-015's
   status.md saying so. Nothing here blocks this task.
2. **"plan P-1" in the backend tests.** `DemonstrationProcessSeedTests.cs:53`
   does not say which plan, and :178 pairs it with D-13. Now that D-15
   settles a second P-1, writing "plan-backend P-1" at :53 would remove the
   ambiguity. This is editorial, and the file is not in the delta.
3. **Round count.** This is round 4 against a two-round cap. F1 is closed.
   F6 is new and lies in tests, not in product code. The fix is the four
   tests plus a qa.md correction, and QA re-checks only those. Scheduling it
   is the orchestrator's.

## Code review, round 5 — frontend (2026-10-04)

Stage 8, round 5, deep, run by a session that wrote none of this. This round
is a delta over round 4. It covers F6 (major), F7..F9 (minor) and
`git diff bc6f318...bb77bad`: five commits over eight files.

- 76e1244 is the fix: the two test files and the new `src/test/gate.ts`.
- 237c33c is the design delta and the prototype.
- 0d0f983 corrects qa.md.
- 09d4655 adds the Status lines in review.md and the status.md Log line.
- bb77bad is QA's Re-check 2.

The only code in the delta is tests. `git diff --stat c4a7360..bb77bad -- src`
lists only the three files of 76e1244, so no product file changed.

`main` is at 63113b2. `git merge-tree --write-tree main mod/001-process-seed`
exits 0 with tree 7e01624, so there is no conflict. Since the branch base
c48b565, `main` has changed one file under `scripts/`: it adds
`scripts/warm-worktree.sh`. `verify.sh` is unchanged on `main`. The rebase
before the merge is still owed, and it is not this round's.

Run here at bb77bad, detached. Each mutation and probe was restored with
`git checkout --`, and `git status --short` was empty afterwards.

- `main`'s `scripts/warm-worktree.sh`, run from a scratch copy because this
  branch predates it: it copied `node_modules` from the cache, exit 0.
- `npx vitest run` over `ProcessDefinitionPage.states.test.tsx` and
  `ProcessDefinitionPage.unreadable.test.tsx` as committed: 2 files, 50/50,
  exit 0.
- **M1**, `retryRef.current?.focus()` removed (`useRetryFocus.ts:42`): exit 1,
  **4 failed, 46 passed**. The four are the F6 tests and no others. Each fails
  on `toHaveFocus()`, at `states.test.tsx:260` and `unreadable.test.tsx:151`.
- **P1**, a probe: M1 plus `loading` narrowed to `query.isPending`
  (`ProcessDefinitionPage.tsx:41`). The same four fail, on `toHaveFocus()`
  again. See note 1.
- **P2**, a probe: `gate()` returns a gate that is already open, so nothing is
  held. Exit 1, **5 failed, 45 passed**, every one at "Unable to find
  role=\"status\" and name \"Загрузка данных\"". These are the four F6 tests
  (`states.test.tsx:253`, `unreadable.test.tsx:140`) and the developer's held
  success test (`states.test.tsx:220`). An answer that is not held makes these
  tests fail loudly. They do not pass vacuously.
- **M3** (QA's M3), the `isFetching` guard removed (`useRetryFocus.ts:33`):
  exit 1, **5 failed, 45 passed**. These are the four plus the held success
  test. The failure output shows `Received element with focus: <body>`.
- **M5**, the not-found focus removed (`useRetryFocus.ts:40`), with `-t D-5`
  on the unreadable file: exit 1, **4 failed, 2 passed**, 20 skipped. The four
  are the tests that end in not found after a retry.
- `npx prettier --check src/test/gate.ts src/features/process/pages`: exit 0.
- `node --check` on the prototype's only script: exit 0.
- `bash scripts/verify.sh`, the whole branch against `main` (63113b2): **exit
  0, `RESULT: green`**. Every step is `ok`:
  - links, backlog ids, status.md budget, spec hygiene, agent frontmatter,
    AI JSON and workflow compatibility;
  - the backend build;
  - backend tests including integration, 848 in all: Shared 53, Architecture
    68, Application 60, Domain 102, Contracts 61, Api.IntegrationTests 390 and
    Infrastructure 114;
  - frontend lint, and the frontend build (`tsc -b` and `vite build`);
  - Vitest, 68 files and 681 tests.
- C-1: a case-insensitive search of the fix diff and its five commit messages
  for the reference tracker's product, vendor and abbreviations finds nothing.

Not run:

- E2E spec 20. No product code changed. QA's named limit stands (qa.md:474):
  focus after a second failure is asserted in jsdom only.
- QA's M2 (the title focused instead) and M4 (`ref={retryRef}` unwired). By
  reading, M4 leaves `retryRef.current` null, which is M1. M2 moves focus to
  the mounted title, and `toHaveFocus()` on the button rejects that.
- Row 2's `missingPart` mutation (17/9). Round 4 reproduced it on the pre-fix
  file, and the rewrite does not change which tests start from a broken
  answer.

No stand was started and no background process is left running.

**Verdict: approve.** 0 blockers and 0 majors are open. F6 is resolved, and
the four tests now fail without the focus call and pass with it. F7, F8 and F9
are resolved. Two new minors are open, F10 and F11. Both are prose, and they
are debts for gate-3 under the loop economy, not a reason for another round.

### Previously open findings

| Finding | Round-5 status | Checked against |
|---|---|---|
| F6 (major) | **resolved** (76e1244, 0d0f983, bb77bad) | Item 1 below. M1 fails exactly the four tests on `toHaveFocus()`, and P2 shows the hold is load-bearing. qa.md's Correction F6 and Re-check 2 are accurate, except that Re-check 1's row 4 was missed: F11. |
| F7 (minor) | resolved (237c33c) | Point 10 (design.md:129) is anchored in item 2. The reason clause is kept and BR-13 is not cited, which is acceptable: mod-015 spec.md:126 shows that BR-13 covers the unknown address and a context not found. |
| F8 (minor) | resolved (237c33c) | The limit is now at :121, :176-177, :403-406 and :425, citing plan-frontend § 6.3 row 1 (:311). The wording is note 2. The prototype's two unscoped sentences are F10. |
| F9 (minor) | resolved (237c33c) | The drawing (:186-187), the prototype (:310) and the reconciliation row (:418) are anchored in item 2. |

### Item 1, F6 against the tests

- **The retry is held with `gate()`.**
  - In `states.test.tsx:237-246`, the first read answers at once and every
    later read awaits `held.opened`.
  - In `unreadable.test.tsx`, `afterGate` (:71-77) wraps only the retry's
    answer, at :295, :348 and :381-387.
- **Skeleton, no alert while held.** `states.test.tsx:253-254`, and
  `retryHeld` in `unreadable.test.tsx:140-141`.
- **It proves an unmount, not a re-render of the same element.**
  - The proof is the assertion made while the answer is held:
    `expect(clicked).not.toBeInTheDocument()` (`states.test.tsx:255`,
    `unreadable.test.tsx:142`). The node the user clicked is detached before
    the answer arrives.
  - React does not re-attach a host node it has removed, so the «Повторить»
    found after release is a new element.
  - `not.toBe(clicked)` (`states.test.tsx:259`, `unreadable.test.tsx:150`)
    restates this. On its own it would also fail if the node were re-rendered
    in place.
  - P2 shows the guard is load-bearing. Without the hold, the tests stop at the
    status query and never reach the focus assertion.
- **Focus after release.** `states.test.tsx:260` and `unreadable.test.tsx:151`.
  - Both fail under M1 and M3, so only `useRetryFocus.ts:42`, run after the
    fetch settles, puts focus there.
  - jsdom moves focus to `<body>` when the focused button is removed (M3's
    output).
  - Nothing else focuses that button. `ErrorBanner.tsx:40` sets only `ref`,
    with no `autoFocus`. The only `.focus()` calls in the non-test code of
    `features/process` and `shared/ui` are `useRetryFocus.ts:38`, :40 and :42.
- **The fourth test's second step.** "follows every attempt"
  (`unreadable.test.tsx:356-358`) does not hold its second retry, and asserts
  the `h1`. That is right: the not-found frame has no «Повторить» to keep, and
  M5 still fails it.
- **`gate.ts` is sound.**
  - The executor runs synchronously, so `open` is the resolver before
    `gate()` returns (:16-21).
  - There is no timer, listener or handle, and a second `open()` does nothing.
  - The two files make six `gate()` calls, and each one is opened:
    `states.test.tsx` :87/:105, :204/:222 and :237/:256;
    `unreadable.test.tsx` :294/:300, :347/:353 and :378/:392.
  - There is one copy: the local `gate` of `states.test.tsx` is gone.
  - A test that fails before its `open()` leaves its MSW handler awaiting a
    promise nobody resolves. That keeps neither a timer nor a socket alive, and
    `setup.ts:22` resets the handlers before the next test. So the comment's
    "or the request outlives it" is accurate and harmless. An optional
    hardening is note 3.

### Item 2, F7..F9 wording against the build

- **design.md:129, point 10.** "No «Меню настроек», and nothing marked current
  in either navigation, the rail's «Настройки» included".
  - Beside the frame, `currentPage` is `null` (`AppLayout.tsx:101-108`), so
    `entryMarker` gives `null` (`visibility.ts:113-115`).
  - Then `settingsShown` is false (`AppLayout.tsx:110-111`), and so is the
    rail's `settingsCurrent` (`MainNavigation.tsx:171`).
  - Pinned by `AppLayout.process.test.tsx:126-137` for the real source and by
    `routes.process.test.tsx:114-131` for the placeholder source, which checks
    every navigation.
  - "Since the address names no real context" is the reason clause. It is not
    a claim.
- **design.md:121 (point 2), :176-177 (the lead-in), :403-406 (Adopting item 3)
  and :425 (the settings-page row).** "On any id but the placeholder
  organization's … outside the current selection".
  - The landing of «Процесс» is the selection's organization
    (`model.ts:44-45`).
  - A settings page is decided from the model's landings
    (`visibility.ts:130-137`).
  - So the demonstration id gives no menu, no trail and no rail mark, which
    `AppLayout.process.test.tsx:81-90` pins.
  - The exception exists, as round 4's F8 probe showed.
  - The wording is precise enough, with one editorial point: note 2.
- **design.md:186-187, the drawing.** «Организация» is drawn above «Процесс»
  → `model.ts:36-46`. The drawing is illustrative.
- **design.md:418, the reconciliation row.**
  - "Settled by mod-015's design § 15" → mod-015 design.md:1175.
  - "The first today" → `model.ts:36-46`, where «Процесс» is the section's one
    entry, and `model.ts:18-19`.
  - "The third once «Общие» and «Участники» exist" is cited future work
    (mod-015 § 4.1).
  - "Settings entries carry no icon" → `types.ts:9-13`, where a settings entry
    is a `NavigationEntry` with no icon field. Only `MainNavigationEntry` takes
    one (:32-34).
  - "The drawing and the prototype draw it under «Организация»" → :186-187 and
    `process-definition.html:310`.
- **`process-definition.html:310`.** `<div class="sec">Организация</div>`
  before «Процесс». The class exists (:99), and `node --check` passes.

### F10 (minor) — the prototype's note and comment state the before-mod-002 rule without F8's limit

Where it is: `prototype/process-definition.html:146-149`, the note.

> the placeholder context draws what the built screen does before mod-002 …
> the shell treats this address as outside the current selection, so there is
> no «Меню настроек», no trail and no rail mark

The comment in `layoutB()` at :301-302 says the same.

F8 restored "on any id but the placeholder organization's" in the four
design.md places that state this rule. The prototype draws the generic address
`/organizations/:organizationId/settings/process` (:142). Its two sentences
still state the rule for "this address" with no limit.

On the id of the placeholder selection's organization, a «Языки» reader gets
«Меню настроек» and the rail mark while the read is pending:

- `model.ts:44-45`;
- `visibility.ts:112-118` and :130-137;
- `AppLayout.tsx:110-111`.

The trail half holds for every id, because the trail comes from visible entries
only (`visibility.ts:152`). The case is the one F8 named: a hand-typed
malformed id, for the time the read takes. Nothing an agent does differs, so
the severity is minor. This is a direction-2 hit, and F8's list did not name
these lines. Citing point 2 does not scope the sentence, because prose anchors
to code, not to other prose.

Fix: in the note at :147, "the shell treats this address, on any id but the
placeholder organization's (plan-frontend § 6.3 row 1), as outside the current
selection". Make the same change in the comment at :301.

### F11 (minor) — qa.md Re-check 1 row 4 still certifies the retry-failure focus as regression-checked

Where it is: qa.md:349, row 4, "Regressions around the fixes: … the retry
failure path (focus back on «Повторить») … ☑ the targeted run in the table
below".

The targeted run (:359) checked "focus back on «Повторить»" with the tests that
F6 found vacuous. Correction F6 (:363-372) names the places that relied on
them: "Rows 2 and 3 above and the mutation row". Rows 2 and 3, the mutation row
(:357) and the requirement row (:167) are now annotated. Row 4 is not. This is
the one place where the earlier claim stands uncorrected, which answers item 3
of this round's brief. Since 76e1244 the path is pinned (M1 here), so nothing
an agent does differs, and the severity is minor.

Fix:

- At the end of row 4's result, add "(the failure path's «Повторить» focus was
  vacuous in that run; held since 76e1244: Correction F6, Re-check 2)".
- Add row 4 to the list in Correction F6 at :372.

### Item 3, qa.md "Correction F6" and "Re-check 2"

- **Correction F6 (:363-398) is accurate.**
  - The four tests it names are `states.test.tsx:235` and
    `unreadable.test.tsx:290`, :346 and :377.
  - "What was wrong" matches round 4's M1 and its probe.
  - "How it was fixed" matches `states.test.tsx:237-260`,
    `unreadable.test.tsx:136-153` and `gate.ts`.
  - "Exactly four tests fail, each on `toHaveFocus()` … 46 of the 50" is
    reproduced by M1 here.
  - "The success-path and not-found focus tests are unchanged" holds: the
    delta touches neither `states.test.tsx:201-227` and :276-296, nor
    `unreadable.test.tsx:270-288`, :321-344, :362-375 and :397-403.
  - "Still QA's" was true at 0d0f983, and bb77bad discharges it.
- **Re-check 2 (:457-515) is accurate.**
  - The scope line → `git diff --stat c4a7360..bb77bad -- src` lists the three
    files.
  - Row 1 and "six uses" → item 1.
  - Row 2: "jsdom drops focus to body" → M3's output. The ternary at :59-67 is
    what unmounts the button. The skeleton during the retry comes from
    `isPending` (note 1).
  - Row 3 → `ErrorBanner.tsx:40`, the three `.focus()` calls, and
    `20-process-definition.spec.ts:138-156`, which asserts the title only.
  - Row 4: 26/26 within this round's 50/50; 4/2 → M5 here; 17/9 is not
    re-run (named above).
  - The mutations: M1 is reproduced, including its line numbers :260 and
    :151. M3 is reproduced at 5/45. M2 and M4 are checked by reading only.
- **Is the "not vacuous" claim corrected everywhere?** The one explicit
  sentence, at :357, is corrected. Rows 2 and 3 (:346, :348) and :167 are
  annotated. Row 4 (:349) is not: F11.

### Behavioural claims pass (item 6), over the fix diff

Direction 1, the prose the delta adds or changes:

- `gate.ts:3-4`, "a handler that awaits `opened` … keeps a read pending for as
  long as the test wants" → `gate.ts:16-21`; the handlers at
  `states.test.tsx:89-92`, :206-211 and :240-245, and
  `unreadable.test.tsx:72-76`.
- `gate.ts:5-8`, "in jsdom a read that answers at once settles … in one render,
  so the skeleton never shows and the button … stays mounted":
  - the skeleton half → P2;
  - the mounted half → round 4's probe (the same node, still connected).
- `gate.ts:8-9`, "every test that holds one must [open it], or the request
  outlives it" → six `gate()`/`open()` pairs (item 1). A shut gate leaves its
  handler awaiting.
- `states.test.tsx:229-234` and `unreadable.test.tsx:127-133`, "Held, the
  clicked button unmounts and focus can only be on the new one if
  `useRetryFocus` put it there" → :255 and :142, with M1 and M3 here. The
  sentence "passed with the focus call removed" is history, from round 4's M1.
- `unreadable.test.tsx:135` and :146, the helper docs → :136-144 and :147-153.
- `states.test.tsx:235`, the test name "with focus on its new «Повторить»" →
  :255 and :259-260, which fail under M1.
- design.md:121, :129, :176-177, :186-187, :403-406, :418 and :425 → item 2.
- `process-definition.html:310` → `model.ts:36-46`.
- qa.md:167, the retry and focus row → `states.test.tsx:235-263`, which M1
  fails.
- qa.md:346, the row 2 annotation → `unreadable.test.tsx:290-304`, which M1
  fails.
- qa.md:348, the row 3 annotation, "the assertions on the `h1` were not
  affected (4 of the 6)" → M5 here.
- qa.md:349, row 4 → **F11**.
- qa.md:357, the corrected mutation row → round 4's M1 for the pre-fix count,
  and M1 here for 4/46.
- qa.md:361, the developer's run row: "4 failed, 46 passed … 50/50 … 68 files,
  681 tests" → M1 here and `verify.sh` here. Its "QA has not re-run" sentence
  is superseded by bb77bad (note 4).
- qa.md:363-398, Correction F6 → item 3.
- qa.md:457-515, Re-check 2 → item 3.
- review.md, the F6 Status line → M1 here. Its "is still owed" sentence is
  superseded by bb77bad (note 4).
- review.md, the F7 Status line → design.md:129, and mod-015 spec.md:126 for
  BR-13's scope.
- review.md, the F8 Status line → design.md:121, :176-177, :403-406 and :425.
- review.md, the F9 Status line → design.md:186-187,
  `process-definition.html:310`, :418, and `node --check` here.
- status.md:45, the [dev] Log line:
  - the M1 and 50/50 figures → M1 here;
  - "Vitest 68 files, 681 tests" → `verify.sh` here;
  - the F7..F9 summaries → item 2.

Direction 2, the behaviour the delta changes. The delta changes no product
code. It changes four things:

- (a) the strength of four focus tests, and where `gate()` lives;
- (b) the documented scope of the before-`mod-002` navigation rule;
- (c) point 10's statement of the navigation beside the not-found frame;
- (d) the drawn section of «Процесс» and the reconciliation row.

Each was searched across `docs/`, `.claude/`, the three `AGENTS.md` files and
`src/frontend` (excluding `node_modules`). The hits:

- **(a)** Searched for "retry fails again, with focus on its", "focus back on
  «Повторить»", "focus on «Повторить»", "vacuous", `function gate` and
  `states.test.tsx:78`.
  - qa.md :167, :346, :348, :357, :361, :363-398 and Re-check 2 are all
    updated. :349 is **F11**.
  - plan-frontend.md:194-196 and :392, and design.md:316-318, describe the
    behaviour. It is now pinned.
  - e2e 20.3 (:138-156) asserts the title only, as qa.md says.
  - `states.test.tsx:78` survives only in round 4's text, which is history.
- **(b)** Searched for "outside the current selection" and "placeholder
  organization".
  - design.md :121, :404 and :425 carry the limit.
  - T-26 (design.md:360) says "before that none". That holds for every id,
    because the trail comes from visible entries only (`visibility.ts:152`).
  - plan-frontend.md:311 carries the limit, and :315-318 sits under that row.
  - status.md:41, D-15, is scoped to the demonstration id.
  - `process-definition.html:147` and :301 are **F10**.
- **(c)** Searched for «Меню настроек» with not found.
  - plan-frontend.md:313 (§ 6.3 row 3), `AppLayout.process.test.tsx:126` and
    `routes.process.test.tsx:114` all agree.
- **(d)** Searched for `AccountTreeOutlined`, "Процесс <--" and
  `settingsMenu`.
  - The hits are only design.md:418, the drawing and the prototype.
    plan-frontend.md:299-301 says the same section, order and no icon.

Not swept: `mod-002`'s documents, which are not on this branch (as in
round 4).

### Notes (not findings)

1. **What unmounts the alert during a retry.** In query-core 5.102.8,
   `fetchState` (`query.js:345-355`) resets `status` to `pending` and `error`
   to `null` when the query holds no data. So the skeleton during a retry
   comes from `query.isPending`. The `query.isError && query.isFetching` arm of
   `ProcessDefinitionPage.tsx:41` matters only once the query holds data. P1
   shows it: without that arm, the held tests behave the same. Nothing here is
   wrong, and qa.md's citation ":41, :59" still covers it. The note is for
   whoever next reasons about that line.
2. **"The placeholder organization's" means the organization of the placeholder
   selection.** The placeholder dataset holds `org-1` and `org-2`
   (`placeholderContext.ts:15-45`). The switcher can select either, and the
   exception follows the selection (`model.ts:44-45`). "The selected
   placeholder organization's" would be exact. This is editorial, and
   plan-frontend.md:311 uses the same words.
3. **Optional hardening of `gate()`.** A gate could register its own release,
   for example with `onTestFinished(open)` from Vitest, so a test that fails
   before `open()` frees its handler too. It is not needed for correctness
   (item 1).
4. **QA bookkeeping.** bb77bad touched only qa.md. status.md has no [qa] Log
   line for Re-check 2. The [dev] line (status.md:45) and round 4's F6 Status
   line both still say that QA's re-run "is still owed". qa.md § Re-check 2
   discharges it. The [qa] line is the orchestrator's to add with QA's
   hand-off.
5. **Round count.** `main`'s 06-code-review.md (63113b2) allows at most two
   fix rounds after round 1. This is round 5, scheduled by the orchestrator as
   a delta over one major, and round 4's note 3 recorded the overrun. With this
   verdict no further round is owed. F10 and F11 go to gate-3's "Deliberately
   left open" list, or into one prose commit if the developer touches qa.md or
   the prototype again.
