# Review: mod-014-keycloak-user-projection

## Round 1 — requirements

Reviewer: requirements-reviewer (clean context, not the author), 2026-09-28.
Input: `spec.md`, `stories/US-1..US-6`, checked against doc-006 (§ Roles
constraint 5, USER, BR-M1-4/5/17, § NFRs, § Decomposition, US-3), doc-010
(D-9, PR-1, PR-2, § ADR debt row), ADR-0018, ADR-0022, ADR-0023, the realm
import, `AppHost.cs`, `scripts/verify.sh` and the queue lines of
`mod-016-organization-access` and `mod-003-work-item-core` in `main`.

**Verdict: `needs-human-decision`.** 0 blockers, 5 majors, 6 minors. The
spec is thorough and consistent with doc-006 and doc-010 on scope (D-4
settled correctly, no screen, no permission code, ADR-0023's client
allowlist and no-service-account property untouched). But the rule that an
absence from a snapshot is a final deletion produces permanently wrong
statuses in production and on every stand restart (R-1, R-2); the "no admin
access" framing guards a REST surface the provider never needs, while its
real power — in-process model access — is unconstrained (R-3); the
M1-skeleton ordering the spec relies on no longer holds in the queue
(R-4); and the read model has contract holes its consumers would have to
reopen this task for (R-5). R-2 and R-4 need the human; R-1, R-3 and R-5 go
back to the analyst, R-3's framing of Q-3 included.

### Rule → story mapping

| Rule / NFR | Covered by | Gap |
|---|---|---|
| BR-UP-1 single writer, unknown until pushed | US-6 (no MUSTASK user writes), US-1 (unknown id) | no criterion that a user's own sign-in does not fill the projection (R-7) |
| BR-UP-2 what is projected | US-1 (four values only), US-2 (service account, other realm), US-4 | — |
| BR-UP-3 display name | US-1, US-2 | — |
| BR-UP-4 changes carried | US-2, US-3 (lockout) | permanent-lockout wording (R-6) |
| BR-UP-5 active; deleted kept | US-3, US-4 | absence-inferred deletion made final (R-1) |
| BR-UP-6 whole state, ordering | US-3, US-5 | sequential only; no concurrent case (R-8) |
| BR-UP-7 Keycloak never waits | US-5 | — |
| BR-UP-8 reconciliation | US-4 | creation racing a snapshot (R-1); stand restart (R-2) |
| BR-UP-9 one way, ADR-0023 | US-4, US-6 | presumes Q-3's answer; provider's model power unguarded (R-3) |
| BR-UP-10 push credential | US-6 | — |
| BR-UP-11 read model | US-1 | id-filter cap, order, tiebreak (R-5) |
| BR-UP-12 authorizes nothing | US-3, US-6 | token lifetime source (R-9) |
| NFR Freshness | US-2, US-3, US-5 | lost-push scenario has no time bound (R-9) |
| NFR Keycloak unaffected | US-5 | absolute 2 s (R-9) |
| NFR Volumes | US-1, US-4 | — |
| NFR Logging | US-4, US-5, US-6 | — |
| NFR Stand | US-4, US-6 | migrations and the seed are manual steps (R-9); stand restart (R-2); verify.sh bucket (R-10) |
| NFR Keycloak version | none | acceptable as an ADR item (R-10) |
| NFR Audit | n/a (states an absence) | — |
| doc-006 constraint 5 / D-9 | whole spec | — |
| doc-006 BR-M1-4, -5, -17 | consumers, over US-1's lookup/search | "unknown" semantics for consumers (R-7) |

Size: one L line — a founder that owns an ADR (doc-010 PR-2); the refusal to
split is argued and holds. It is a large L (a new JVM deployable, its build
and test path, a new auth scheme in Mustask.Api, a projection, a read
model); the plan at stage 4 should expect it.

### R-1
Severity: major
Affects: BR-UP-5, BR-UP-6, BR-UP-8, US-4, lifecycle diagram
Problem: BR-UP-8 marks a held user deleted when a complete snapshot does not
contain them, and BR-UP-5 makes every deletion final ("nothing makes it
active again"). Two ordinary situations then leave a live Keycloak user
inactive for good, with no way back short of deleting and recreating the
account in Keycloak (new id, lost memberships and assignments):
(a) a user is created after the provider has enumerated the realm for a
reconciliation but before the snapshot completes; the creation push arrives
and is applied, then the complete snapshot, which does not contain them,
deletes them. With an hourly pass of up to 5 minutes over 10 000 users the
window is real, not theoretical. (b) Keycloak's database is restored from a
backup: users deleted after the backup exist again and can sign in, but
MUSTASK shows them «неактивен» forever and never offers them as candidates
(BR-M1-17). BR-UP-6's ordering does not save case (a) because BR-UP-8 does
not say the absence-deletion is dated.
Required resolution: (1) an absence from a complete snapshot deletes only a
held user whose held state is older than the moment the snapshot's
enumeration began — i.e. it is a deletion as of that moment, subject to
BR-UP-6's ordering; (2) decide whether an absence-inferred deletion is
final. Since Keycloak never reuses an id, a later push for such an id can
only mean the absence was wrong; the recommended rule is that an explicit
deletion is final and an absence-inferred one is overridden by any newer
push. If the analyst keeps finality, state case (b) as an accepted risk.
(3) Add a US-4 criterion: a user created while a reconciliation is running
ends active and stays active after that reconciliation completes. Update the
lifecycle diagram accordingly.
Status: open
Resolution: -

### R-2
Severity: major
Affects: BR-UP-5, BR-UP-8, NFR Stand, US-3, US-4; consumers mod-016, mod-003
Problem: on the stand, Keycloak has no persistent store (`AppHost.cs`:
`start-dev` keeps H2 in the container layer, "created seed users vanish too")
while PostgreSQL keeps a data volume. Every stand start therefore gives every
seed and fixture user a new Keycloak id. The start reconciliation — a
complete snapshot — marks every previously held user deleted for good, and
adds the same people again under new ids. After N restarts the projection
holds N inactive copies of «Анна Смирнова» (visible with "include
inactive"), and every stand record a consumer keyed by the old ids — mod-016
memberships, mod-003 assignees — now points at a deleted user, while the
developer's own new id is a member of nothing. The spec never mentions this.
It is not caused by this task (subs already change per stand start), but
this task is what turns it into permanent «неактивен» data, and it is what
consumers' QA will run into first.
Required resolution: the spec states the stand behaviour explicitly and the
human picks one: (a) accept it — the stand's M1 data is disposable across
restarts, stated in the NFR Stand and as a known limitation for mod-016 and
mod-003; (b) give the stand's Keycloak a persistent store so ids survive
restarts (changes the stand's "the import is the source of truth" property
recorded in `AppHost.cs`, and touches ADR-0023's provisioning, which assumes
a fresh realm); (c) another mechanism the ADR designs. Whatever is chosen,
US-4's "stand comes up with its users already projected" states what the
projection holds after a second start.
Status: open
Resolution: -

### R-3
Severity: major
Affects: BR-UP-9, Q-3, US-4 ("The backfill uses no Keycloak admin REST
call"), US-6 ("The feed adds no Keycloak client"), § ADR expected 8
Problem: "no admin REST call, no production service account, ADR-0023's
guards unchanged" holds literally for this design, but it guards the wrong
door. The provider runs inside Keycloak with direct access to its model: it
can create, edit and delete users in any realm, grant roles and read
credential data without ever touching `/admin/realms/...` or holding a
client. It is shipping code that is *more* capable than the admin REST
client ADR-0023 keeps out of production, and ADR-0023's own reasoning — the
real guarantee is that the capable code is never deployed — does not apply,
because this code is deployed by design. Extending the admin-REST scan to
the provider's sources (Q-3, recommended) would pass vacuously: the provider
has no reason to call REST. Meanwhile the spec has no rule that the provider
only reads, only realm `mustask`, and only the fields of BR-UP-2. Also, US-4
and US-6 already assert "the scan covering the provider's sources too",
i.e. they presume Q-3's recommended answer before gate-1.
Required resolution: add a business rule (BR-UP-13 or into BR-UP-9) that the
provider never writes to Keycloak's model, reads realm `mustask` only, and
reads no credential, session or attribute beyond BR-UP-2; add a criterion
that verifies it (a source-level guard over the provider's calls into the
model, stated honestly as a second line the way ADR-0023 § Decision 3
states its scans, plus review) and name it in § ADR expected 8. Reframe Q-3
for the human: extending the scan is cheap and harmless, but it is not the
control — the read-only rule and its guard are. Make US-4/US-6's scan
wording conditional on Q-3, or settle Q-3 at gate-1 and keep it.
Status: open
Resolution: -

### R-4
Severity: major
Affects: § Context ("it lands first and opens M1's code slice"), § ADR
expected 9, doc-010 PR-1
Problem: doc-010 PR-1 made this line land first so that its design "fixes
the module skeleton mod-002 extends". The human's split of 2026-09-25 changed
the queue: `mod-002-organizations-projects-teams` now depends on `mod-001`
only (in flight), not on this task; only `mod-016` and `mod-003` depend on
it. Nothing orders mod-014 before mod-002 any more, so both may create M1's
code slice (`Mustask.Api/Features/<M1>`, `Mustask.Application/Features/<M1>`,
the M1 DbContext configuration and migrations) in parallel, each as its
founder. The spec's § ADR expected 9 already says "which mod-016 extends",
but the Context still claims "lands first and opens M1's code slice", which
the queue does not guarantee.
Required resolution: either (a) the spec drops the "opens M1's code slice"
claim and places the projection as a self-contained sub-slice that neither
needs nor fixes mod-002's skeleton (ADR expected 9 then answers where it
sits and how mod-016 reads it), or (b) the human adds `mod-014` to
`mod-002`'s deps in `main` so PR-1 holds again. Recommended: (a) — it costs
nothing here and keeps the human's split intact. Gate-1 question.
Status: open
Resolution: -

### R-5
Severity: major
Affects: BR-UP-11, US-1; D-2 (3), D-3
Problem: the read model is meant to be fixed so mod-016 and mod-003 never
reopen this task, but three of its parameters are undefined and each will
surface as a consumer's question: (a) "only among these ids" has no cap,
while lookup is capped at 500 — mod-016's add-to-team picker and mod-003's
picker pass an organization's members, and doc-006 sizes those at "~500",
i.e. possibly more; (b) "ordered by display name" / US-1's "alphabetical
order" names no collation for a directory that mixes Cyrillic and Latin
names, and no tiebreak — with a cap of 20, the order decides *which* users
are returned, so two equal display names (the normal case after R-2) make
the answer nondeterministic and the US-1 scenario untestable; (c) lookup's
behaviour for a repeated id or an empty list, and search's maximum text
length, are unstated. Consumers exceeding 500 ids must batch — the spec
says "refused", not "batch".
Required resolution: BR-UP-11 fixes: the id-filter cap (recommended: the
same 500 as lookup, refused above it) and that consumers batch lookups
above 500; the ordering (a named collation, e.g. Russian letters before
Latin, case-insensitive, `ё` = `е`, with the Keycloak id as the final
tiebreak); duplicate ids answered once; an upper bound on the search text.
US-1 gets a criterion for the tiebreak and for the id-filter cap.
Status: open
Resolution: -

### R-6
Severity: minor
Affects: BR-UP-2, BR-UP-4, BR-UP-11, NFRs, § Open questions
Problem: several analyst's calls that no source dictates are presented as
rules, and only one of them (Q-1) reaches the human: service-account users
never projected; no fill from sign-in tokens; the username not searchable
(search is display name and e-mail only — doc-006 says "name or e-mail",
so this is consistent, but a login-only user is findable only when the
display name falls back to it); and the numbers — search minimum 2
characters, cap 20, lookup 500, 10 000 users, 500 ms reads, 10 s freshness,
5 min recovery, 2 s Keycloak budget, 30 s slow-MUSTASK probe. The first
three of the numbers become api-contract and bind both consumers. None is
wrong — service accounts are not people (doc-006 USER), the single-writer
argument for refusing token fill is sound, and the temporary-lockout call
follows from Keycloak itself (a temporary lockout leaves `enabled` true) and
from BR-M1-17 ("disabled or deleted"). Separately, BR-UP-4's "(the realm has
no permanent lockout)" leans on today's realm import: if permanent lockout
is ever switched on, Keycloak disables the account, which BR-UP-5 already
makes inactive — the rule should say that instead of relying on the setting.
Required resolution: list these calls in § Open questions as one
"confirm the analyst's calls" item (or the orchestrator lists them in the
gate-1 summary), so the human approves the contract numbers knowingly;
reword BR-UP-4's lockout clause as above.
Status: open
Resolution: -

### R-7
Severity: minor
Affects: BR-UP-1, BR-UP-11, US-1; consumers of BR-M1-4, BR-M1-5
Problem: with no token fill, a signed-in user can be "unknown" to the
projection (created while the feed was down, before the recovery
reconciliation). The spec states it once (BR-UP-1) but does not tell the
consumers what "unknown" means: it is neither active nor inactive. mod-016
counts active administrators for BR-M1-4 — an organization created by an
unknown user has, by the projection, no active administrator, and every
membership change would be refused until the push arrives. No criterion
checks that a sign-in does not fill the projection.
Required resolution: BR-UP-11 states that "unknown" is a third answer,
distinct from inactive, that it can concern a signed-in user for up to the
freshness bounds, and that its treatment is the consumer's (with a note for
mod-016's BR-M1-4 count). Add a US-1 or US-6 criterion: a user who signs in
to the panel before their push arrives is still "unknown" to a lookup.
Status: open
Resolution: -

### R-8
Severity: minor
Affects: BR-UP-6, US-5
Problem: every ordering criterion is sequential (one push, then the other).
The realistic race is concurrent: a live event push and the same user's
snapshot entry applied at the same moment by two requests. A check-then-write
implementation passes every US-5 scenario and still lets the older state win.
Required resolution: add a US-5 criterion: two pushes for one user, with
moments T1 < T2, applied concurrently, end with the T2 state, and a
deletion applied concurrently with an "enabled" push ends deleted.
Status: open
Resolution: -

### R-9
Severity: minor
Affects: US-2, US-3, US-4, US-5, BR-UP-12, NFR Keycloak unaffected
Problem: criteria that cannot be tested as written, or cite the wrong
source:
- US-4 "An interrupted snapshot deletes nobody": "those 120 are applied"
  presumes a per-user transport; with any batching the cut loses whole
  batches and the scenario fails without a defect.
- US-4 "A deletion whose push was lost": "When ... the reconciliation runs"
  has no time bound; NFR Freshness gives one (5 minutes of MUSTASK being
  reachable).
- US-4 "The stand comes up with its users already projected ... no human
  step": migrations are applied explicitly (ADR-0002) and the seed users
  exist only after `scripts/keycloak-seed.sh` is run — both are human steps
  today. The Given must include them.
- NFR Keycloak unaffected / US-5: "completes within 2 s" is absolute; on a
  loaded developer machine the console can exceed it with no provider at
  all. The NFR already says "as it does without the provider" — make the
  criterion relative to that baseline, with the 2 s as an upper bound.
- US-2 "A user's own profile change" and US-3 "A temporary brute-force
  lockout": this project's agents may not type a Keycloak password (the
  context of ADR-0023). Say how QA drives them — e.g. the account console in
  a session obtained through the stand-only impersonation of ADR-0023, and
  wrong passwords typed at the login form (not the user's password).
- BR-UP-12 cites ADR-0018 § Consequences for "15 minutes", but ADR-0018 says
  the former 15 min were replaced by realm settings; the source is the
  realm import (`accessTokenLifespan: 900`).
Required resolution: reword the four criteria as indicated ("every entry
MUSTASK received before the cut is applied"; "within 5 minutes of MUSTASK
being reachable"; "given the database migrated and the seed script run";
"within 2 s and no slower than the same operation with the provider
disabled"), add the driving notes to US-2/US-3, and fix BR-UP-12's source.
Status: open
Resolution: -

### R-10
Severity: minor
Affects: NFR Stand, NFR Keycloak version, § ADR expected 1
Problem: the tooling rule is feasible — a Keycloak provider must be JVM
code, and it can be compiled and unit-tested inside a multi-stage Docker
build, then loaded into the stand's Keycloak image, with no local Java
toolchain. But the consequences are not stated: `scripts/verify.sh` has no
bucket for the provider's sources (anything under `src/` or unmatched is
classified `dotnet`, which would not run the provider's tests); verifying a
provider change needs Docker; a stand start after a provider change needs
network access to fetch the JVM dependencies; and `Keycloak:Tag` is
overridable in `AppHost.cs`, so the stand can load the provider into a
Keycloak it was not built against. There is no criterion for "the
provider's tests run under verify.sh".
Required resolution: add these to § ADR expected 1 (verify.sh
classification, Docker as a prerequisite of that bucket, the offline-start
consequence, the tag override) and add one criterion: a change to the
provider's sources makes `scripts/verify.sh` run the provider's tests, and a
failing provider test fails it.
Status: open
Resolution: -

### R-11
Severity: minor
Affects: § Open questions Q-2
Problem: Q-2 (keep a deleted user's name and e-mail) is well framed, but
under R-1 (b) and R-2 the kept records multiply: every stand restart and
every absence-inferred deletion keeps another copy of the same person's
name and e-mail. The human should see that when answering Q-2.
Required resolution: Q-2's text mentions that retention covers
absence-inferred deletions too, and refers to R-2's stand choice.
Status: open
Resolution: -

### Questions for the human at gate-1

1. R-2 — stand restarts: accept that the stand's projection and M1 data are
   disposable across restarts, or give the stand's Keycloak a persistent
   store (or another mechanism)?
2. R-4 — M1 skeleton: the projection as a self-contained sub-slice
   (recommended), or add `mod-014` to `mod-002`'s deps in `main`?
3. Q-1 — reconciliation period (recommended 60 min, configurable).
4. Q-2 — a deleted user's name and e-mail (recommended keep), read with
   R-11.
5. Q-3 — the admin-REST scan over the provider's sources, reframed per R-3:
   the control is a read-only rule for the provider with its own guard.
6. R-6 — confirm the analyst's calls: service accounts not projected, no
   token fill, the contract numbers (2 characters, 20, 500) and the NFR
   numbers.

## Round 2 — requirements

Reviewer: requirements-reviewer (clean context, not the author), 2026-09-28.
Delta check: the open findings R-1..R-11, D-5..D-8 in `status.md`, and the
revision of `spec.md` and `stories/` in c9cbf7b over 9ee1829. Spot checks
outside the delta only where D-5 reaches: `AppHost.cs` (the Keycloak
container's args and secrets) and ADR-0023's provisioning.

**Verdict: `approved-by-ai`.** All eleven round-1 findings are resolved as
their required resolutions ask or as D-5..D-8 decide. Round 2 found four
minor findings (R-12..R-15); each was small enough to fix directly and is
fixed below. No question for the human remains. D-6 is reflected (the spec
claims no M1 skeleton, § Out of scope hands it to mod-002 and the PR-1
wording to tech-001), and no artifact of this task names the product studied
in doc-001..007. The read model — BR-UP-11, BR-UP-14..17 — now fixes both
reads' inputs, caps, refusals, empty and duplicate handling, order,
tiebreak, "unknown" and batching; `api-contract.md` for mod-016 and mod-003
can be written from it without a question, the only open form (how the order
is offered for merging, R-15) being named in § ADR expected 9.

### Round-1 findings

- **R-1** — Status: resolved. Resolution: BR-UP-8 dates an absence as of the
  snapshot's enumeration start and leaves newer state untouched; BR-UP-5/-6
  split a final reported deletion from an absence any newer push overrides;
  the backup-restore case is an accepted risk in BR-UP-5 for reported
  deletions only; US-4 "A user created during a reconciliation stays active"
  and "An absence yields to a newer push"; both diagrams updated. R-12 closes
  one literal gap this left.
- **R-2** — Status: resolved (D-5). Resolution: NFR Stand requires a
  persistent Keycloak store, states that realm-import edits need a reset and
  what a one-sided reset does; US-4 "A second stand start keeps the same
  users under the same ids"; § ADR expected 7 covers the store, the reset,
  ADR-0023's provisioning (idempotent create/update already, per ADR-0023
  § Consequences) and the seed script. R-13 adds the secrets D-5 also
  freezes.
- **R-3** — Status: resolved (D-7). Resolution: BR-UP-13 (read-only, realm
  `mustask` only, BR-UP-2 fields only; a guard test of its own as a second
  line, plus review); US-6 "The provider's read-only guard passes" and "The
  read-only guard catches a write" (a mutation test with two planted calls);
  § ADR expected 8; US-4/US-6's scan wording no longer presumes the scan
  covers the provider.
- **R-4** — Status: resolved (D-6). Resolution: § Context places the
  projection as a self-contained user-directory sub-slice that neither founds
  nor needs M1's skeleton; § Out of scope hands the skeleton to mod-002 and
  PR-1's wording to tech-001; § Decomposition adds no dep to mod-002; § ADR
  expected 9 reworded.
- **R-5** — Status: resolved. Resolution: BR-UP-14 (text 2..100 after trim,
  id filter ≤ 500 with duplicates and empty list defined), BR-UP-15 (CLDR
  Russian at primary strength, Keycloak id as ordinal tiebreak), BR-UP-16
  (lookup cap, empty, duplicates), BR-UP-17 (batching); US-1 criteria for
  the order, the tiebreak, the id-filter cap, the empty filter, the text cap
  and lookup duplicates. R-14 and R-15 tighten two edges.
- **R-6** — Status: resolved (D-8). Resolution: the calls and numbers are
  confirmed by D-8 and cited as such (BR-UP-1, -2, -14, NFR preamble);
  BR-UP-4's lockout clause now says a permanent lockout disables the account
  and is carried as a disable, not relying on today's realm setting.
- **R-7** — Status: resolved. Resolution: BR-UP-17 defines "unknown" as a
  third answer, its reach over a signed-in user, and leaves its treatment to
  the consumer with the mod-016 BR-M1-4 note; US-6 "A sign-in does not fill
  the projection"; US-1's lookup scenario says "neither active nor inactive".
- **R-8** — Status: resolved. Resolution: BR-UP-6 covers concurrent
  application; US-5 "Concurrent pushes for one user end with the newest
  state" and "A deletion applied concurrently with an "enabled" push wins",
  each repeated 50 times.
- **R-9** — Status: resolved. Resolution: US-4's interrupted snapshot is
  worded per entry received; the lost-deletion and lost-change scenarios are
  bounded at 5 minutes; the stand scenario's Given includes the migration and
  the seed script; NFR Keycloak unaffected and US-5 are relative to a
  provider-disabled baseline (median of 5, 500 ms margin) with 2 s as a bound;
  US-2 drives the self-service change through ADR-0023's impersonation and
  US-3 the lockout through wrong passwords at the login form; BR-UP-12 cites
  the realm import's `accessTokenLifespan: 900`.
- **R-10** — Status: resolved. Resolution: § ADR expected 1 names the
  verify.sh classification, Docker as its prerequisite, the network a stand
  start needs, the `Keycloak:Tag` override and the upgrade path; NFR Stand and
  US-4 "The provider's tests gate verification".
- **R-11** — Status: resolved (D-5, D-8). Resolution: D-8 keeps a deleted
  user's name and e-mail; BR-UP-5 extends retention to an absence; D-5 removes
  the per-restart copies that made retention multiply on the stand.

### R-12
Severity: minor
Affects: BR-UP-8, BR-UP-5, BPMN S2, NFR Logging, US-3, US-4
Problem: BR-UP-8 as revised marks absent "a held user missing from [a
complete snapshot] whose held state is older than the moment its enumeration
began". Read literally it applies to a reported-deleted user — always
missing, always older — turning a final deletion into an absence that "any
newer push" overrides, which contradicts BR-UP-5/-6 and US-3 "A reported
deletion is final". It also re-marks an already absent user every period,
so "0 marked absent" in US-4's unchanged-realm and second-start scenarios
depended on how the count was read. The reconciliation counts "changed" and
"deleted" were undefined: a snapshot entry always carries a newer moment
than the state held, and a snapshot never reports a deletion.
Required resolution: exclude reported-deleted and already-absent users from
BR-UP-8's absence marking; define the log counts.
Status: resolved
Resolution: fixed by the reviewer — BR-UP-8 now reads "a held user, neither
reported deleted nor already absent, missing from it …" and adds "a reported
deletion is never turned into an absence (BR-UP-5)"; BPMN node S2 matches;
NFR Logging defines changed (a projected value or the status differs, not
merely a newer moment), marked absent (newly, by this snapshot) and deleted
(entries ignored because the id is held as deleted — BR-UP-5's accepted risk
showing); US-3 "A reported deletion is final" gains a clause: after a
complete reconciliation, a later enabled push still leaves her inactive.

### R-13
Severity: minor
Affects: NFR Stand, § ADR expected 7; D-5
Problem: D-5's persistent store freezes more than the realm import.
`AppHost.cs` passes the Keycloak administrator password as
`KC_BOOTSTRAP_ADMIN_PASSWORD` and the `mustask-panel` client secret through a
placeholder in the realm import; Keycloak takes both only when its store is
created. Today every start recreates the store, so changing either user
secret just works; after D-5 a changed administrator password breaks
ADR-0023's provisioning (it signs in as that administrator) and the seed
script, and a changed panel secret breaks the panel's sign-in — until a
reset nobody was told about. Separately, NFR Stand's "resetting one alone
leaves the projection holding every former id as absent" is true of
resetting Keycloak's store alone only; resetting MUSTASK's database alone
just empties the projection, which the backfill refills.
Required resolution: NFR Stand names these values among what needs a reset;
§ ADR expected 7 asks how a developer learns it; the one-sided-reset clause
names which side.
Status: resolved
Resolution: fixed by the reviewer in NFR Stand (the administrator password
and the `mustask-panel` client secret added to what takes effect only after
a reset; "resetting Keycloak's store alone leaves … every former id as
absent, and the stand's M1 data keyed by them") and in § ADR expected 7 (how
a developer learns a changed secret needs a reset).

### R-14
Severity: minor
Affects: BR-UP-15, BR-UP-16, US-1
Problem: (a) BR-UP-15 orders "equal display names" by Keycloak id, but its
comparison is at primary strength, under which «Семён Орлов» and «Семен
Орлов», or «анна» and «Анна», compare equal without being identical; read
as "identical strings", their relative order was undefined and the
"total order" claim did not hold. (b) BR-UP-14 says a repeated id counts
toward the search filter's 500; BR-UP-16 was silent on whether it counts
toward lookup's.
Required resolution: tiebreak on names that compare equal under the order;
state lookup's duplicate counting.
Status: resolved
Resolution: fixed by the reviewer — BR-UP-15: "Display names that compare
equal under this order (identical, or differing only in letter case or
`ё`/`е`) are ordered by Keycloak id"; BR-UP-16: "a repeated id counts toward
the 500 and is answered once".

### R-15
Severity: minor
Affects: BR-UP-17, BR-UP-15, § ADR expected 9; consumers mod-016, mod-003
Problem: above 500 ids BR-UP-17 has a consumer run one restricted search per
batch and merge the answers, while "the read model orders only within one
answer". To return a correct first 20 and a correct "more matched", the
consumer must order the merged answers by BR-UP-15 — a CLDR Russian
collation at primary strength with an ordinal id tiebreak — which each
consumer would otherwise reimplement, or come back to this task for. doc-006
sizes an organization at about 500 members, so mod-016's member search and
mod-003's picker reach this edge.
Required resolution: the read model offers BR-UP-15's order to consumers for
merging, its form decided with `api-contract.md`; the merge rule for the 20
and "more matched" is stated.
Status: resolved
Resolution: fixed by the reviewer — BR-UP-17 now says the read model offers
consumers BR-UP-15's order to merge by, and that the merged answer keeps the
first 20 in that order and says more matched if any batch did or more than 20
came back; § ADR expected 9 asks for the form in which the order is offered.
This is not a third read, so BR-UP-11's "two reads and no others" stands.

### Rule → story mapping, delta

| Rule / NFR | Covered by | Gap |
|---|---|---|
| BR-UP-13 provider read-only | US-6 (guard passes; guard catches two planted calls), US-4 (guards run) | — |
| BR-UP-14 search inputs | US-1 (short text, 100/101, username not searched, id filter: subset, duplicates, unknown id, empty, 500/501) | — |
| BR-UP-15 order | US-1 (Cyrillic/Latin, case, ё; identical names by id) | a case-/ё-only tie is not a scenario — acceptable, same mechanism |
| BR-UP-16 lookup | US-1 (unknown, duplicates, empty, 500/501) | — |
| BR-UP-17 unknown, batching | US-1 (lookup "unknown"), US-6 (sign-in does not fill) | batching is the consumer's behaviour; tested in mod-016/mod-003 |
| NFR Stand (D-5) | US-4 (after a reset; second start), US-6 (fresh clone) | — |

### Editorial changes made by the reviewer in round 2

- `spec.md`: BR-UP-8 (R-12), BPMN node S2 (R-12), NFR Logging (R-12),
  BR-UP-15 and BR-UP-16 (R-14), BR-UP-17 (R-15), NFR Stand (R-13), § ADR
  expected 7 (R-13) and 9 (R-15).
- `stories/US-3-account-status.md`: "A reported deletion is final" gains the
  reconciliation clause (R-12).

### Questions for the human at gate-1

None. The orchestrator's gate-1 summary should mention R-13 as a stand
consequence of D-5 the human did not see when answering: after this task a
changed Keycloak administrator password or panel client secret needs a stand
reset.

## Round 3 — delta check (2026-10-01)

Reviewer: requirements-reviewer (clean context, not the author), 2026-10-01.
Delta check: D-9..D-13 in `status.md` against the analyst's revision 8338fc2
over ab4f091 (`spec.md`, US-4); round 2's findings; consistency with the
sibling branches `mod/001-process-seed` (65aa732), `mod/002-organizations-projects-teams`
(98153a2) and `mod/015-app-shell-design-system` (252fa60), with
`tech/002-apphost-external-database` (99bc54f; the external-database mode
D-10 and D-12 rely on) and with `main` (124181d). Spot checks outside the
delta only where the decisions reach: `AppHost.cs` (the Keycloak container),
the realm import, `docs/conventions/backend.md` and the reference slice's
folders, ADR-0023's split between stand-only and shipping code.

**Verdict: `approved-by-ai`.** D-9..D-12 are reflected in NFR Stand, US-4
and § ADR expected 7 and 9; D-13 is a process consent with nothing to
reflect in the requirements (it answers the Plan's E2E question for stage
4). No round-2 finding is open: R-1..R-15 were resolved, and D-9 accepts
R-13 as fixed (option 1). The delta found 0 blockers, 0 majors and 6 minors
(R-16..R-21). Five are fixed in place. R-21 is fixed in the spec, but its
`deps` line is the orchestrator's. No question for the human remains. No
artifact of this task names the product studied in doc-001..007.

### Decisions → requirements

| Decision | Reflected in | Check |
|---|---|---|
| D-9 gate-1 as the spec stood; R-13 option 1 | NFR Stand (a changed administrator password or panel secret needs a reset), § ADR expected 7 (how a developer learns it) | holds |
| D-10 (a) start drift check | NFR Stand, US-4 "A realm import that differs …", § ADR expected 7 | literal scope not satisfiable, "fails loudly" not observable (R-17) |
| D-10 (b) reset script, external-database safe | NFR Stand (`.sh` + `.ps1`, the repo's pattern: `scripts/*.ps1` wrappers exist), US-4 | missing migration step (R-18); which store a reset wipes (R-16) |
| D-11 `Features/UserDirectory`, own migration, no `Features/Organizations` | § ADR expected 9 | Infrastructure has no `Features/` level (R-19) |
| D-12 branches take turns; no lingering fixed-name container; external database | NFR Stand | store/database pairing unstated (R-16); no criterion (R-16); ADR 7 wording (R-20); tech-002 not in main (R-21) |
| D-13 E2E / live-stand consent | — (process) | nothing to reflect |

### R-16
Severity: minor
Affects: NFR Stand, US-4, § ADR expected 7; D-5, D-10, D-12
Problem: D-10 resets "the stand's Keycloak store and the stand's own
`mustask` database" as a pair, and D-12 has the stands of several branches
take turns, on one external database (the human's PostgreSQL) or each on its
own database container. Aspire names a container database's volume per
AppHost path (`<app>.apphost-<hash>-postgres-data`), so each checkout has
its own database, while one external database is shared by every checkout.
The spec never said which store goes with which database. Two natural
implementations both break D-5's goal that the stand's M1 data stays keyed
by live ids. (a) A store named per checkout, on a shared external database:
each checkout's Keycloak gives the seed users other ids. Each start marks
the other checkout's users absent and adds its own, and M1 data created in
one checkout points at ids the other's store lacks. (b) One store for all
checkouts, each with its own container database: a reset in one checkout
wipes the shared store but only its own database. Every other checkout then
holds former ids — the one-sided reset NFR Stand warns about. No criterion
would catch either.
Required resolution: state that the store pairs one to one with the
`mustask` database, ask the ADR how it is keyed to it, and add a criterion.
Status: resolved
Resolution: fixed by the reviewer. NFR Stand: "The store pairs one to one
with the stand's `mustask` database: stands on one database, an external
one included, share its store, and stands on different databases never
share one", and the reset wipes "that pair". § ADR expected 7 asks how the
store "is keyed to the database it pairs with". US-4 gains "Stands of two
checkouts take turns on one external database". It checks D-12's turn-taking
with no container left behind. It checks the shared store: same ids, 0
added, 0 marked absent. And it checks that a third checkout's reset on its
own database leaves the pair untouched. This is no new choice: D-5's goal
and D-10's pair leave only this reading.

### R-17
Severity: minor
Affects: NFR Stand (drift check), US-4, § ADR expected 7; D-10 (a); mod-002 BR-M1-2, BR-M1-19, D-i
Problem: (a) NFR Stand failed a start whose realm import "differs from the
live realm in client roles, realm roles, composites". Read literally, that
can never pass. The live realm always holds Keycloak's built-in realm roles
(`offline_access`, `uma_authorization`, `default-roles-mustask`), its
built-in clients and their roles, and the `mustask-e2e` client that
ADR-0023 provisions at run time. None of these is in the import. (b) "Fails
loudly" had no observable outcome in US-4: an Aspire start can fail as the
AppHost exiting or as one resource going unhealthy. (c) The import declares
three groups, and mod-002 grants its create right "by the realm role or
group" (BR-M1-2, BR-M1-19, D-i). D-10's list does not compare groups. A
group-only import edit therefore still goes unnoticed. mod-002's own edit is
caught anyway, through its new client role and the `admin` composite.
Required resolution: limit the comparison to what the import declares, in
both directions, with built-ins and runtime provisioning aside. Give "fails
loudly" an observable outcome. Leave groups as an ADR question, keeping
D-10's list as the minimum.
Status: resolved
Resolution: fixed by the reviewer. NFR Stand now compares "the import's
realm roles, its clients' roles, their composites or the listener's
enablement — one declared and missing or different, or one no longer
declared; Keycloak's built-ins and ADR-0023's runtime provisioning aside".
In US-4 the stand "does not come up ready, and an error in the AppHost's
output or dashboard names the missing role and scripts/stand-reset.sh".
§ ADR expected 7 asks "whether it also compares groups (mod-002 BR-M1-2)".

### R-18
Severity: minor
Affects: US-4 "A realm import that differs from the live realm fails the start"
Problem: after `scripts/stand-reset.sh` the scenario went straight to "a new
start and the seed script" and expected every seed user found within 5
minutes. But the reset drops and recreates the database empty (D-10), and
migrations are applied explicitly (ADR-0002; tech-002: "the database is
created by `dotnet ef database update`"). Without that step the projection
has no table, and the criterion fails for no defect. The Given, "the stand
of the previous scenario, run against an external database", was also
unclear. The previous stand used whatever database it was configured with,
and under R-16 moving it to another database gives a different pair.
Required resolution: put the migration in the post-reset steps, and state
the Given on its own.
Status: resolved
Resolution: fixed by the reviewer. The Given now reads "a stand run against
an external database (tech-002) on a server that holds another database too,
started, migrated and seeded once". The steps after the reset read "a new
start, the database migrated and the seed script run, the stand comes up
ready".

### R-19
Severity: minor
Affects: § ADR expected 9; D-11
Problem: D-11 places `Features/UserDirectory` in "Api/Application/Infrastructure
(as the reference Dictionaries slice is laid out)", and the spec copied it.
The reference slice does not have that layout. It is
`Mustask.Api/Features/Dictionaries` and
`Mustask.Application/Features/Dictionaries`, but
`Mustask.Infrastructure/Dictionaries` and `Mustask.Domain/Dictionaries`.
`docs/conventions/backend.md` puts the `Features/` level in Application only.
The decision contradicts its own rationale, and a literal reading starts a
second layout in Infrastructure.
Required resolution: follow the reference layout, which is the decision's
stated intent.
Status: resolved
Resolution: fixed by the reviewer. § ADR expected 9 now reads "laid out as
the reference Dictionaries slice — `Features/UserDirectory` in Api and
Application, `UserDirectory` in Infrastructure (and in Domain for its rules),
which have no `Features/` level". The orchestrator may align D-11's wording
in `status.md`, which is outside the reviewer's write zone.

### R-20
Severity: minor
Affects: § ADR expected 7; D-12; ADR-0023
Problem: item 7 asked "how the store persists with no fixed-name container".
The stand already pins one: ADR-0023 names the Keycloak container
`mustask-e2e-keycloak-<port>` (`WithContainerName`, `AppHost.cs`), and its
provisioning relies on that name. D-12 forbids only a fixed-name container
that outlives the AppHost. Read as written, the item asked the ADR to undo
ADR-0023. The item also never asked what the reset does while a stand is
running: an in-use volume and a database with open connections both block
a wipe.
Required resolution: narrow the wording to D-12's, and add the
running-stand case.
Status: resolved
Resolution: fixed by the reviewer. Item 7 now reads "no fixed-name container
outliving the AppHost (ADR-0023's port-named one lives one run)" and "reset
script, external-database safe, and what it does while a stand runs".

### R-21
Severity: minor
Affects: § Decomposition, NFR Stand, US-4; D-12; `status.md` deps
Problem: D-12 and US-4's external-database scenarios need tech-002's
external-database mode, the `ConnectionStrings:mustask` switch in
`AppHost.cs`. tech-002 is not in `main`: its branch is at QA pass with
gate-3 pending. It is also not a dependency of this task (deps:
doc-006, doc-010). If stage 4 starts before tech-002 merges, those
requirements cannot be built or tested.
Required resolution: state the sequencing in the spec, and add tech-002 to
the task's deps, or record that stage 4 starts after tech-002 merges.
Status: open (orchestrator: the `deps` line); the spec side is fixed
Resolution: fixed by the reviewer in § Decomposition: "NFR Stand's
external-database mode is tech-002's, which stage 4 needs in main (D-12)".

### Consistency with the sibling specs

- **mod-002** (98153a2). D-j sends its realm-import change through "mod-014's
  reset script, if mod-014 is in main", which matches D-10. D-i's new client
  role and its place in `admin`'s composites are what the drift check
  compares. D-i's "role/group" is R-17 (c). mod-002 founds
  `Features/Organizations` and this task creates none (D-11). Its § Out of
  scope ("mod-014: the user projection; reads no user data") and BR-M1-21 (a
  creator's name would need this projection) match D-3.
- **mod-015** (252fa60). D-21 drops mod-014 from D-12, BR-15 and US-11 ("no
  screen"), which matches D-1 here. D-2 (2) of this task (`app/theme` and
  `AppLayout` untouched) stands.
- **mod-001** (65aa732, round 1). It makes no realm-import edit: its code is
  registered and declared in no realm (BR-M2-10). It has no folder overlap
  with `UserDirectory` and no stand change. No conflict.
- **main** (124181d). `mod-016` and `mod-003` both list `mod-014` in their
  deps, as D-3 says.

### Rule → story mapping, delta

| Rule / NFR | Covered by | Gap |
|---|---|---|
| NFR Stand: store/database pairing (R-16) | US-4 "Stands of two checkouts take turns on one external database" | — |
| NFR Stand: drift check (D-10 a) | US-4 "A realm import that differs …" (a declared role missing) | the reverse direction (a role no longer declared) is not a scenario; same mechanism, acceptable |
| NFR Stand: reset script, external-database safe (D-10 b) | US-4 drift scenario (the other database untouched); the two-checkouts scenario (a container-database reset leaves the external pair alone) | the `.ps1` wrapper has no scenario; it is the repo's mechanical wrapper pattern, acceptable |
| NFR Stand: turn-taking, no lingering container, external database (D-12) | US-4 two-checkouts scenario | — |
| NFR Stand: changed secrets need a reset (D-9) | § ADR expected 7 | how a developer learns it is the ADR's, per D-9 |
| D-11 folders | § ADR expected 9 | structural; checked at code review |

### Editorial changes made by the reviewer in round 3

- `spec.md`: NFR Stand (R-16, R-17), § Decomposition (R-21), § ADR expected
  7 (R-16, R-17, R-20) and 9 (R-19). It is now 270 lines. The excess over
  the ~250 budget is the § ADR expected list and NFR Stand, both spec-level
  content, not leaked story content.
- `stories/US-4-backfill-and-reconciliation.md`: the drift scenario (R-17,
  R-18) and the new two-checkouts scenario (R-16). It is now 147 lines.

### For the orchestrator

1. R-21: add `tech-002` to this task's `deps` in `status.md`, or record that
   stage 4 starts after tech-002 merges.
2. R-19: D-11's wording ("Features/UserDirectory in … Infrastructure") may be
   aligned with the reference layout the spec now follows.
3. For the summary, a consequence of D-10 and D-12 that was not visible when
   they were made, in the way R-13 was in round 2. With the drift check
   comparing in both directions, every switch between branches whose realm
   imports differ fails the start and needs a reset. That covers two
   parallel tasks each adding a role, and a branch with an added role
   followed by one without it. On one external database the reset drops and
   recreates the shared `mustask` database, so the stand data of every
   branch on it goes too.

## Round 1 — backend

Code review (stage 8), round 1, full review. Reviewer: code-reviewer, clean
context, author of nothing on this branch, 2026-10-04. Input: `git diff
main...mod/014-keycloak-user-projection` at `52992dd` (187 files), `spec.md`,
US-1..US-6, `api-contract.md`, `plan-backend.md`, ADR-0025, `qa.md`,
`status.md`; conventions read: `docs/conventions/backend.md` (the backend
files), `.githooks/commit-msg` and `git.md` for commit subjects only. Test
suites not re-run here: QA's `scripts/verify.sh` of 2026-10-04 (RESULT green,
`provider` bucket included) postdates the last code commit (`0ab8182`); this
round ran `scripts/verify.sh --list` only.

**Verdict: `needs-changes`.** 0 blocker, 3 major, 8 minor, all open.

What holds. Spec conformance: BR-UP-1..17 and the NFRs are implemented, and
nothing beyond them. Correctness: the ordering rule (strictly greater moment,
deletion final, absence yields to newer) is one domain method both apply
paths share; absences are marked only after MUSTASK itself proves a snapshot
whole; per-user advisory locks plus row locks in one consistent order, a
snapshot-wide lock only on snapshot operations, so no cycle; the provider
captures inside Keycloak's transaction and queues only on commit, never does
I/O on a request thread, and coalesces by user. Security: the body reaches a
handler only through `SignedPushBody<T>` (and a test pins that for every feed
route), HMAC over method, raw target and body hash, constant-time compare,
±300 s window, one undifferentiated 401, refusals logged by reason without
body or secret; secrets only from configuration, the stand's generated per
run; the provider's own allowlist guard is real and not vacuous, and my
first-line read of every provider main class found no write to Keycloak's
model. Architecture: the D-11 layout, explicit `IUserDirectory`, raw SQL
parameterised and confined to Infrastructure, `IClock`, string enums, xmin.
Tests check what they claim in every case I opened (concurrency ×50,
route binding, logging capture, guard fixtures, drift check through the
health-check service). The three majors are ADR-0025 sentences that a
sibling file in this same diff contradicts — the record the human accepts at
gate-3 (D-16) — each a small edit; F1 may instead be fixed in code.

### F1

Severity: major
Where: `docs/adr/0025-keycloak-user-projection.md:360-364`;
`deploy/keycloak/providers/user-projection/src/main/java/mustask/keycloak/userprojection/Reconciler.java:24-27`
Problem: both state that an outage "costs one attempt a minute (plus the
periods), and its end one reconciliation". `qa.md:303-308` (D-4, open, same
diff) shows two full runs at an outage's end whenever the pending retry fires
before the sender's next delivery attempt: the retry's run completes, then
`FeedSender.java:135` (`onRecovery.run()`) reaches `Reconciler.request()`
(`Reconciler.java:92-98`) with no run pending, so nothing coalesces it.
`afterAnOutage_theRecoveryRunsOneReconciliation` drives only the other order.
QA graded the behaviour minor (bounded at two, the second run changes
nothing) and that grading stands; the major is the sentence, which a sibling
file in the same diff disproves (stage 8, "Severity").
Fix: either make it true — e.g. the sender hands over when its failure began
and `Reconciler` drops a recovery request when a run has completed since
then — with a `ReconcilerTest` for the retry-first order; or write "at most
two" in both places. Either way, close D-4 in `qa.md` with the commit.

### F2

Severity: major
Where: `docs/adr/0025-keycloak-user-projection.md:555-570`;
`deploy/keycloak/providers/user-projection/src/test/java/mustask/keycloak/userprojection/ReadOnlyGuard.java:11-21`
Problem: ADR § 8 enumerates the guard's allowlist and then says "A setter,
`credentialManager()`, `getAttributes()`, a session or realm write, a realm
enumeration — anything not listed — fails the test"; the guard's class
comment says the same. The allowlist itself (`ReadOnlyGuard.java:33-34`,
`:75`, `:99-101`) admits `KeycloakSession#getContext` and the setter
`KeycloakContext#setRealm` from `ProjectedRealm`; `plan-backend.md:270-275`
records why, and `status.md:49` says "ADR-0025 § 8's prose list does not name
it yet". The ADR is the statement of the provider's power that gate-3 accepts
(D-7, D-16), and it currently says a session setter fails the guard.
Fix: ADR § 8 names the exception — `getContext` and `setRealm`, from
`ProjectedRealm` only, binding the provider's own background session to
realm `mustask`; session state, nothing written to the model; the fixture
`BindsARealmElsewhere` holds the one-class restriction. The guard's class
comment gets the same qualification.

### F3

Severity: major
Where: `docs/adr/0025-keycloak-user-projection.md:428-431`;
`docs/features/mod-014-keycloak-user-projection/plan-backend.md:172-175`
Problem: both say the per-user advisory locks are taken "in id order" /
"in ordinal id order". `UserDirectoryLocks.cs:18-40` takes them in ascending
lock-key order (`ORDER BY user_key`, the hash of the id) and its comment says
so; only the row locks (`LockHeldRowsAsync`) go in id order. § 6's
no-deadlock argument is that every site takes the keys in one order; a second
lock site written to the ADR's stated order would interleave with the
existing one — the cycle § 6 says cannot form. Contradicted by a sibling
file in the same diff.
Fix: ADR § 6 and plan § 5 say "in ascending order of the lock key
(`hashtextextended` of the id) — the one order `UserDirectoryLocks` uses —
then the held rows `FOR UPDATE` in id order".

### F4

Severity: minor
Where: `src/backend/Mustask.AppHost/AppHost.cs:32-46`;
`src/backend/Mustask.AppHost/Stand/StandReset.cs:54-61`, `:86`
Problem: the AppHost deletes `.e2e-stand/keycloak-e2e-client.json` first
thing and unconditionally — also under `--stand-reset`. With a stand running,
the reset then refuses ("Stop the AppHost first"), but the running stand's
E2E suite has already lost its stand file and fails with "no stand file"
until the AppHost restarts. ADR § 7 step 1 (`:525-526`, "refuses while a
stand runs") reads as a refusal that touches nothing; `StandReset` deletes
the file itself on success (`:115-122`).
Fix: skip the top-of-file deletion when `standResetArguments.Reset` is true.

### F5

Severity: minor
Where: `docs/adr/0025-keycloak-user-projection.md:609-615`;
`src/backend/Mustask.Domain/UserDirectory/ProjectedUser.cs:99-104`, `:157-162`
Problem: "any write — every user is rewritten by each reconciliation —
recomputes a stale one". A deleted user returns from `Apply` before
`Recompute`, and deleted and absent users are gone from Keycloak, so no later
snapshot carries them: after an ICU version change their `sort_key` keeps
the old version for good. `api-contract.md:118-119` ("A search answer is
already in this order") then fails for inactive users in an
`IncludeInactive` search, and `UserSearchResult.Merge` can order them
differently from SQL. Rare trigger (an ICU change on the API host), ordering
only.
Fix: narrow the sentence to present users and record the accepted effect, or
re-key rows whose `sort_key_version` differs (e.g. at a snapshot completion,
in bounded batches).

### F6

Severity: minor
Where: `deploy/keycloak/providers/user-projection/src/main/java/mustask/keycloak/userprojection/ListenerEnablementWatch.java:47-53`;
`…/Reconciler.java:143-147`; `WATCH_DELAY` 20 s
(`UserProjectionListenerFactory.java:34`), `START_DELAY` 30 s
(`Reconciler.java:31`)
Problem: if the first enablement probe (at 20 s) throws, the state stays
unknown, the 30 s start run is `SKIPPED` — logged at DEBUG only — and nothing
retries it; the next successful probe goes unknown → enabled, which requests
nothing (only false → true does). The backfill (BR-UP-8 "at Keycloak start",
US-4 "within 5 minutes") then waits for the period, 60 minutes by default, or
for a delivery failure. A realm still missing at the first probe is handled
(false → true requests a run); only the exception path loses it.
Fix: on unknown → enabled, request a reconciliation when none has completed
since start (or retry a run skipped while unknown), with a test.

### F7

Severity: minor
Where: `src/backend/Mustask.AppHost/AppHost.cs:312-328`;
`src/backend/Mustask.AppHost/Provisioning/UserProjectionIdentifiers.cs:46`, `:49`;
`docs/adr/0025-keycloak-user-projection.md:146-153`
Problem: QA D-1's fix sends the provider to
`http://host.docker.internal:{Ports:Api}` with
`--add-host=host.docker.internal:host-gateway` — the Docker-host-gateway
dependency ADR § 7 cites when rejecting store option B ("a networking
dependency on a machine we do not control"). The decision, its reason
(Aspire's container tunnel loses the api after a `keycloak` restart) and its
verified scope (Docker Desktop on Windows only) live in plan § 9.3, a code
comment and `src/backend/AGENTS.md`, not in the ADR (raised by QA,
`qa.md:310-319`).
Fix: record it in ADR § 3 or § 7 and say why it does not reopen option B
(the api is ours and already listens on `0.0.0.0`; a developer's PostgreSQL
listen addresses and `pg_hba` are not).

### F8

Severity: minor
Where and problem, three overstatements in ADR-0025:
- `:411-413`, a wall clock stepped back across a restart, "the next
  reconciliation heals them": a run before the clock catches up stamps the
  same stepped-back markers — its entries are older than the held ones and
  its begin is refused 409 while its start is not above the newest begun
  (`DirectorySnapshot.cs:58-63`) — so healing waits for the first run after
  the clock passes the held markers.
- `:343-344`, "no Keycloak request thread ever touches the network or a lock
  the sender holds": `PendingChanges`' monitor is taken by `put` (request
  thread, after commit, `PendingChanges.java:41-53`) and by `take`/`awaitSize`
  (sender, `:76`) — briefly, never across I/O.
- `:229`, `verify.sh` "runs `docker build --target test`": it runs
  `--target provider` (`scripts/verify.sh:470-478`); `Dockerfile:36` makes
  `test` an alias, so the result is the same.
Fix: reword the three.

### F9

Severity: minor
Where: `src/frontend/e2e/specs/16-dictionaries-permission-gating.spec.ts:27-32`
(not in the diff; direction 2)
Problem: "created and deleted by this spec alone … nothing left behind
whether the test passes or fails". With the feed, every run leaves a
permanent `Deleted` row in `directory_users` (BR-UP-5 keeps every record;
`qa.md:265-266` saw spec 16's user held `Deleted`).
Fix: amend the comment — nothing is left in Keycloak; the user projection
keeps the deleted user's record by design (BR-UP-5).

### F10

Severity: minor
Where: `src/backend/tests/Mustask.Architecture.Tests/UserDirectoryBoundaryRules.cs:42-65`;
`src/backend/Mustask.Infrastructure/MustaskDbContext.cs:23-27`
Problem: the write-side rule bans `IProjectedUserStore` and
`IDirectorySnapshotStore` outside the sub-slice, but
`MustaskDbContext.DirectoryUsers` (and the snapshot sets) and
`ProjectedUser`'s public `Apply`/`MarkAbsent` stay reachable from any
Infrastructure or Api code — a `mod-016` store in Infrastructure, say.
`api-contract.md` A.8 ("never read the projection's tables, its EF entities or
its store interfaces") and BR-UP-1's "nothing else writes it" have no
mechanical guard on that path.
Fix: extend the rule to `DirectoryUsers`, `DirectorySnapshots`,
`DirectorySnapshotPages` and `ProjectedUser` outside the `UserDirectory`
folders, `MustaskDbContext.cs` and `Migrations/`, with its not-vacuous check.

### F11

Severity: minor
Where: `docs/features/mod-014-keycloak-user-projection/plan-backend.md:90`, `:283-286`
Problem: `:90` lists `KeycloakRealmRolesTests.cs` as changed for the
listener — it is unchanged; the listener check is
`UserProjectionStandTests.cs:38`. `:286` says the not-vacuous test "asserts
three named failures" — it asserts five
(`ReadOnlyGuardIsNotVacuousTest.java:23-30`).
Fix: update both lines.

### Behavioural claims in prose

Direction 1 — prose the diff adds or changes, one line per claim checked:

- `spec.md:50-56` BPMN, Keycloak does not wait; retried; recovery runs a
  reconciliation → `ChangeCapture.java:35`, `:51`; `FeedSender.java:126`,
  `:135`, `:148`.
- `spec.md:57-59` refused push changes nothing, logged without the
  credential, delivery failed → `SignedPushBody.cs:40` (before
  deserialisation), `PushSignatureVerifier.cs:42`; `FeedSender.java:148`.
- `spec.md:60-62` duplicate, older, after a deletion ignored →
  `ProjectedUser.cs:99-126`.
- `spec.md:64-69` triggers; complete vs interrupted snapshot →
  `Reconciler.java:87-88`, `FeedSender.java:135`;
  `CompleteSnapshotHandler.cs:50-55`.
- `spec.md:72-87` lifecycle → `ProjectedUser.cs:70-170`.
- BR-UP-1 only the provider writes, never a sign-in →
  `UserDirectoryBoundaryRules.cs:43` (store interfaces only — F10); no
  claims-based writer anywhere in the diff.
- BR-UP-2 service accounts, other realms → `ProjectedUserReader.java:15`,
  `KeycloakRealmSource.java:21`, `:70`, `UserProjectionListener.java:88`,
  `UserRemovalListener.java:37`.
- BR-UP-3 → `DisplayNameRule.cs`. BR-UP-4 → `UserProjectionListener.java:29-34`,
  `:64`, `UserRemovalListener.java`. BR-UP-5 → `ProjectedUser.cs:99-121`.
- BR-UP-6 incl. concurrency → `ProjectedUser.cs:123-146`,
  `ProjectionBatch.cs:33`, `UserDirectoryLocks.cs:31-40`,
  `PushConcurrencyTests`.
- BR-UP-7 → `ChangeCapture.java:51`, the sender's own thread,
  `PushClient.java:23-25`.
- BR-UP-8 → `Reconciler.java:87-88`, `:125`; `FeedOptions` default 60;
  `CompleteSnapshotHandler.cs:50-55`; `ProjectedUser.cs:157-170`.
- BR-UP-9 → `ReadOnlyGuard.java:25-61`; `UserDirectoryBoundaryRules.cs:25`.
- BR-UP-10 → `PushSignatureVerifier.cs:83`, `:91`;
  `PushChannelOptions.FromConfiguration`; `FeedOptions.java:83`;
  `UserProjectionPushSecretPlacementRules`.
- BR-UP-11, 14–17 → `UserDirectoryReadModel.cs`, `DirectorySearchText.cs`,
  `IUserDirectory.cs` (`Merge`), `DirectoryUserOrder.cs`.
- BR-UP-12 → `UserDirectoryBoundaryRules.cs:68`. BR-UP-13 →
  `ReadOnlyGuard.java`; its ADR prose — F2.
- NFR Freshness → `FeedSender.java:24` (200 ms), `:30` (backoff ≤ 60 s);
  measured in `qa.md` (0.28–0.97 s; RC-1 51 s after `/health`).
- NFR Keycloak unaffected → `ChangeCapture.java:51`; `qa.md` medians.
  NFR Volumes → `UserDirectoryVolumeTests`.
- NFR Logging → `PushSignatureVerifier` refusal line,
  `CompleteSnapshotHandler.cs:68`, `FeedSender` `fail`,
  `Reconciler.java:184-188`; `UserDirectoryLoggingTests`.
- NFR Audit (an absence) → the migration creates only the three projection
  tables.
- NFR Stand: store and pairing → `AppHost.cs:147`, `:167`, `:309`; reset
  scope → `StandReset.cs:139`, `:154` (a refused run — F4); drift fails
  loudly naming the reset → `KeycloakStandDriftCheck.cs`,
  `KeycloakDriftHealthCheck.cs:86`, `AppHost.cs:378`; no fixed-name container
  outlives the AppHost → session lifetime unchanged, `qa.md` NFR row; no
  fourth secret → `AppHost.cs:268`; provider tests gate `verify.sh` →
  `scripts/verify.sh:468-502`.
- NFR Keycloak version → `Dockerfile` `ARG KEYCLOAK_TAG`,
  `KeycloakProviderImageTests`. Operation completeness, rotation →
  `PushSignatureVerifier.cs:91`.
- ADR § 1 build, test and guard in the image; Docker required →
  `Dockerfile:23-39`, `verify.sh:469-470`; "`--target test`" — F8; tag held
  with the AppHost's → `KeycloakProviderImageTests`; "an incompatible SPI
  fails the build and Keycloak does not start" → `mvn verify` precedes the
  final stage (`Dockerfile:29-39`), not exercised live — the weakest anchor
  here, accepted.
- ADR § 2 sources, realm filter, settings → `UserProjectionListener.java:29-34`,
  `:64`, `:88`; `mustask-realm.json:22`.
- ADR § 3 no unverified body; Public; outside the readiness gate →
  `SignedPushBody.cs:33-40`, `UserDirectoryRouteBindingTests.cs:30`,
  `RealmReadinessGateMiddleware.cs:32`; window, both secrets →
  `PushSignatureVerifier.cs:83`, `:91`; replay harmless → `EnsureBegunWith`,
  stored page counts, stored completion counts; start check →
  `PushChannelOptions`; stand generation → `AppHost.cs:268`; TLS →
  `FeedOptions.java:64`.
- ADR § 4 capture on commit, cap, batches, timeouts, backoff, 400 dropped,
  401 at ERROR, recovery, threads, inert → `ChangeCapture.java:35`, `:51`;
  `PendingChanges.java:23`, `:123`; `FeedSender.java:22-30`, `:126-148`;
  `PushClient.java:23-25`; `UserProjectionListenerFactory` `init`/`close`;
  the lock sentence — F8.
- ADR § 5 triggers, single retry → `Reconciler.java:87-88`, `:105`, `:125`;
  "its end one reconciliation" — F1; enumeration →
  `KeycloakRealmSource.java:44`, `:67`; completeness proved by MUSTASK →
  `CompleteSnapshotHandler.cs:50`.
- ADR § 6 marker → `MomentClock.java:34`; after commit →
  `ChangeCapture.java:51`; ties → `ProjectedUser.cs:123`; newer moment stored
  → `:135-138`; deletion final → `:99-104`; "in id order" — F3; clock step —
  F8.
- ADR § 7 listener → `mustask-realm.json:22`; volumes → `AppHost.cs:147`,
  `:167`, `:309`; comparisons → `KeycloakStandDriftCheck.cs` (realm roles
  both ways, built-ins aside, composites, declared clients' roles, listener,
  groups one way, admin password, panel secret, URLs); unhealthy blocks
  provisioning → `qa.md` run 2, RC-10; pass cached →
  `KeycloakDriftHealthCheck.cs:46`, `:56-86`; reset steps →
  `StandReset.cs:54-61`, `:74`, `:139`, `:154` (F4); provisioning converges
  on a surviving realm → QA's E2E 19/19 on persistent stores.
- ADR § 8 → `ReadOnlyGuard.java`; its prose — F2.
- ADR § 9 layout → the diff's folders (no `Features/Organizations`); ICU
  start check → `Program.cs:167`, `DirectoryCollation.EnsureSupported`;
  merged order equals SQL order → `DirectoryCollation` `SortKey`/`Compare`,
  `DirectoryCollationTests` byte-order case; stale keys — F5.
- `api-contract.md` A.3–A.7 → `UserDirectoryReadModel.cs`,
  `DirectorySearchText.cs`, `UserDirectoryBoundaryRules.cs:25`,
  `UserDirectoryVolumeTests`; A.5 "already in this order" — F5; B.1–B.3 →
  `UserDirectoryEndpoints.cs`, `PushBodyReader.MaxBodyBytes`,
  `UserPushValidation.cs`, `UserDirectoryLimits.cs`; B.2 →
  `PushSignatureVectorsTests`; B-2..B-4 tables → the three snapshot
  handlers and `DirectorySnapshot.cs`; B.5 → `ProjectedUser.cs`,
  `ProjectionBatch.cs`; B.6 → the handlers' log lines; Part C →
  `contracts/errors/user_directory.json`, `UserDirectoryErrorCodes.cs`,
  `docs/error-codes.md`; Part D → `UserDirectoryLimits.cs`,
  `PushSignatureVerifier.Window`, `FeedOptions`.
- `plan-backend.md` § 8 allowlist → `ReadOnlyGuard.java:25-61`; § 9.3 feed
  address → `AppHost.cs:324-328`; § 9.4 → `KeycloakDriftHealthCheck.cs`;
  § 13 outcome → `qa.md` runs; § 5 — F3; `:90`, `:286` — F11.
- `src/backend/AGENTS.md` "Keycloak on the stand" → `AppHost.cs:309`,
  `:324-328`, `:378`; `StandReset.cs`; `verify.sh:468-502`.
- `docs/runbooks/local-migrations.md` "After a stand reset" → both plans'
  `NextSteps` in `StandReset.cs`.
- `scripts/keycloak-seed.sh` header and token message → the store volume;
  the drift check's admin-password finding.
- `scripts/stand-reset.sh` header (wipes, refuses, next steps; "every other
  database … untouched") → `StandReset.cs`; `RecreateDatabaseAsync` names
  only the plan's target.
- Code comments: `SignedPushBody.cs` "a test asserts" →
  `UserDirectoryRouteBindingTests.cs:30`; `KeycloakDriftHealthCheck.cs` doc
  → `:46`, `:56-86`; `UserDirectoryLocks.cs:18-23` → its SQL `:33-38`;
  `Dockerfile` header (`test` = `provider`) → `Dockerfile:36`;
  `Reconciler.java:24-27` — F1; `ReadOnlyGuard.java:15-16` — F2.
- `status.md` Log: [dev] 2026-10-02 "§ 8's prose list does not name it yet"
  → still so — F2; [dev] 2026-10-04 "recovery runs one reconciliation" →
  true for the order the test drives — F1; [qa] lines → the `qa.md` sections
  they cite.

Direction 2 — behaviour the diff changes, the identifiers grepped, the hits:

- Keycloak's store persists across starts (it used to die with the
  container): grepped `no volume|without a volume|writable layer|recreat|fresh
  realm|H2|volume|import-realm|keycloak-seed|left behind|leftover` in `docs/`,
  `.claude/`, the `AGENTS.md` files, `src/backend`, `src/frontend/e2e` — hits:
  `scripts/keycloak-seed.sh` (updated in the diff), ADR-0023 `:26`, `:100`
  (still true: the import happens when a store is created),
  `e2e/support/reconcile.ts` (still true), spec 16 `:27-32` → F9. Not swept:
  `docs/templates/examples/` (frozen past-task examples).
- Keycloak is built from `deploy/keycloak/Dockerfile`, not pulled: grepped
  `quay.io|Keycloak:Image|Keycloak:Tag|AddContainer` in `*.md` — only ADR-0025
  and `src/backend/AGENTS.md`, both in the diff. None stale.
- `Mustask.Api` refuses to start without `UserDirectory:Push:Secret`: grepped
  `Keycloak__ClientSecret|Keycloak:ClientSecret` and standalone
  `dotnet run … Mustask.Api` repo-wide — the test factory and the AppHost,
  both updated; `dotnet ef` goes through `MustaskDbContextFactory`,
  unaffected. None.
- The realm import declares `eventsListeners`: grepped `eventsListeners` —
  tests only. None.
- `verify.sh` gains a `provider` bucket that fails without Docker: grepped
  `verify.sh` with bucket/classification wording in `docs/ai-workflow`,
  `.claude`, the `AGENTS.md` files — `AGENTS.md:131`, generic and still true.
  None.
- The `keycloak` resource gains a second health check: grepped
  `keycloak.{0,80}(restart|fresh|container|volume|seed)|stand.{0,40}(restart|reset)`
  in `docs/ai-workflow` — none; the E2E `accessTokenLifespan` comments
  (specs 06, 09, 11, `reconcile.ts`) still true — the provisioner is
  unchanged.
- Not swept: the bodies of the E2E specs beyond the Keycloak admin and
  reconcile helpers.

### Observations, not findings

- `CompleteSnapshotHandler.cs:52-53` takes one advisory lock per absence
  candidate in one transaction. 10 000 passed in QA's test; past PostgreSQL's
  nominal lock table (`max_locks_per_transaction` × connections, 6 400 by
  default) that relies on shared-memory slack — worth knowing on a tuned
  external server. The NFR stops at 10 000, and ADR-0025's "revisit when"
  covers growth.
- QA's own disclosed gaps stand as written in `qa.md`: US-1's "within 500 ms
  on the stand" is measured by the Testcontainers test, US-4's third-checkout
  leg was not run, no cold E2E stand was observed (already carried to gate-3).
- For the orchestrator: this is an L task, so stage 8 owes a separate
  security pass in its own clean session; this round covered security as
  item 3 of 6 only. F1–F3 are prose unless F1 is fixed in code; if it is,
  or if F4/F6 are taken, QA re-runs over the provider's `ReconcilerTest` and
  the AppHost change. No contract change is needed for any finding.

## Round 2 — backend

Code review (stage 8), round 2, a delta over round 1. Reviewer: code-reviewer,
clean context, author of nothing on this branch, 2026-10-04. Input: round 1's
F1–F11 and the fix diff `673aab4..7bdbcd5` (25 files: `7562620`, `f1f8e52`,
`ce70bdb`, `6b14866`, `cad9c2a`, `7bdbcd5`), read against `spec.md`, US-3..US-5,
`api-contract.md`, `plan-backend.md` and ADR-0025. This round ran
`scripts/verify.sh` itself: RESULT failed — `.NET tests tagged KeycloakProvider`
(F13) and `src/frontend/node_modules missing` (this fresh worktree has no
`npm ci`; the frontend bucket's only file is spec 16's comment, so it was not
run). Every other check passed: backend build; Shared 53, Domain 120,
Application 51, Architecture 79, Contracts 114, Infrastructure 89, Api
integration 488 (+9 opt-in skipped); the provider image, 92/92.

**Verdict: `needs-changes`.** 0 blocker, 1 major (F13), 1 minor (F12), both
new. F1–F11 are resolved. F13 is outside the fix diff and older than it. It
showed up because this round ran the provider bucket, and a gate test that
fails two runs in three cannot be waved through. Round 2 is the last round
the stage allows, so F13 goes to the human with this verdict.

What holds in the fixes. F1: the sender takes the reconciler's mark at the
first failure of an outage (`FeedSender.java:163-166`) and hands it back on
recovery (`:140`). `Reconciler.deliveryRecovered` (`Reconciler.java:143-155`)
asks for a run only if no run begun after the mark has completed or is under
way. The bookkeeping is in `runCoalesced` (`:160-179`): the run's number is
published before `rerun` is cleared, and its completion is recorded before
`runUnderWay` is reset, so the two reads at `:145-146` cannot miss a run
between them. A run begun after the mark enumerates after the failure began.
Nothing it misses is lost: the sender requeues every batch it could not send,
and an overflow asks for its own run (`UserProjectionListenerFactory.java:99-103`).
F6: `requestIfSkipped` (`:123-129`) runs on the watch's first successful
sighting (`ListenerEnablementWatch.java:57-65`), on the same single-thread
scheduler as the runs (`UserProjectionListenerFactory.java:81`). F5: the
completion locks its candidates and up to 1 000 stale-key rows in one call
(`CompleteSnapshotHandler.cs:62-66`). That is one advisory-lock statement in
key order (`ProjectedUserStore.cs:16-24`), so § 6's single order holds. Only
candidates can be marked absent (`:67-70`). `RefreshSortKey`
(`ProjectedUser.cs:182-193`) touches derived values only. F4:
`AppHost.cs:40`. F10: `UserDirectoryBoundaryRules.cs:30-43`, `:82-110`. The
tests check what they claim. Without the fix, `ReconcilerTest:299` and `:318`
would each count two completions, and `:370` none by 81 s. `:334` and `:395`
pin the opposite direction. `ReconciliationTests.cs:185` resets the table
before it runs (`UserDirectoryTestData.ResetAsync`). Its ids sort against the
names, so a stale key would reverse the answer.

### Round-1 findings

- **F1** — Status: resolved (`7562620`, `cad9c2a`). The code now matches "its
  end one reconciliation" in both orders, and a recovery during the retry's
  run. Pinned by `ReconcilerTest:256`, `:299`, `:318`, `:334`. `qa.md` D-4 says
  "fixed, awaiting QA's verification", which is accurate.
- **F2** — Status: resolved. ADR § 8 `:605-625` names `getContext`, and names
  `setRealm` as the one setter, for `ProjectedRealm` only
  (`ProjectedRealm.java:32-40`; guard `ReadOnlyGuard.java:40-41`, `:81-82`,
  `:106-108`). It also names the five fixtures
  (`ReadOnlyGuardIsNotVacuousTest.java:24-29`). The guard's class comment
  (`ReadOnlyGuard.java:12-27`) matches.
- **F3** — Status: resolved. ADR § 6 `:448-464` and plan § 5 `:173-180` state
  the key order and then row locks in id order, as `UserDirectoryLocks.cs:31-40`
  and `:54-68` do.
- **F4** — Status: resolved (`f1f8e52`). `AppHost.cs:40` skips the deletion
  under `--stand-reset`. `StandReset.cs:117` deletes the file after the wipe.
  Pinned by `UserProjectionStandWiringRules.cs:58`.
- **F5** — Status: resolved (`6b14866`), by round 1's second option, bounded
  re-keying. ADR § 9 `:664-677` states the remaining window: more than 1 000
  inactive users take one more completion per 1 000.
- **F6** — Status: resolved (`7562620`). Pinned by `ReconcilerTest:370`, `:395`
  and `ListenerEnablementWatchTest` (first sighting once; a disabled first
  check is no sighting).
- **F7** — Status: resolved. ADR § 7 `:501-517`.
- **F8** — Status: resolved. ADR `:229-231`, `:346-353`, `:426-433`.
- **F9** — Status: resolved. Spec 16 `:31-35`.
- **F10** — Status: resolved (`ce70bdb`). It covers the stores, the entities
  and the context's sets, with a not-vacuous check. A residual is under
  Observations.
- **F11** — Status: resolved. Plan `:90-92`, `:290-293`.

### F12

Severity: minor
Where: `docs/features/mod-014-keycloak-user-projection/plan-backend.md:263`
Problem: the `FeedSender` row still reads "first success after a failure →
`Reconciler.request()`". Since `7562620` the sender calls
`DeliveryRecovery.deliveryRecovered(mark)` (`FeedSender.java:140`), and that
asks for a run only when no run begun since the mark covers the outage. The
next row (`:264`) states the condition, so a reader of the table gets it
right. The `FeedSender` row names a call the sender no longer makes.
Fix: "first success after a failure → `deliveryRecovered(mark)` (the mark
taken at the outage's first failure; see `Reconciler`)".

### F13

Severity: major
Where: `src/backend/tests/Mustask.Api.IntegrationTests/Features/UserDirectory/Live/LiveKeycloakFixture.cs:61`;
`…/Live/LiveKeycloakProviderTests.cs:16`, `:84-85`;
`deploy/keycloak/providers/user-projection/src/main/java/mustask/keycloak/userprojection/UserProjectionListenerFactory.java:34`, `:112`;
`…/FeedSender.java:111-113`; `docs/features/mod-014-keycloak-user-projection/spec.md:124-126`
Problem: the provider holds every captured change until its first enablement
check finds the listener enabled. That check runs 20 s after `postInit`
(`WATCH_DELAY`; `FeedSender.drainOnce` returns `HELD` until then). The live
fixture counts Keycloak as ready once `/realms/mustask` answers, which can be
well inside those 20 s. The first live test to run then waits for the check
against `Fresh` = 15 s.
- Observed this round. `verify.sh`:
  `ADeletion_IsPushedCarryingTheLastState` failed with "No matching push …
  within 15 s; received 0 request(s)".
- Re-runs of the category: one passed 9/9, with that test taking 13 s against
  ≤ 5 s for the rest. The next failed 8/9 with the same message.
- One diagnostic run used a temporary edit, reverted and never committed, that
  dumped the container log. Keycloak logged "started … Listening" at
  08:57:12.8. The test created its user at 08:57:13.8. The provider logged
  "the listener is enabled in realm mustask" at 08:57:28.7. The push waited
  15 s for that check, and the test passed at 15 s.

The live tests gate `verify.sh`'s `provider` bucket (`scripts/verify.sh:488-499`).
So any change under `deploy/keycloak/` now fails verification more often than
not on this machine, and an MR is not opened with failing tests (AGENTS.md).
The same window also falsifies NFR Freshness: "with both sides up, a change in
Keycloak shows in the read model within 10 s" does not hold for a change made
in the first ~15 s after Keycloak is ready. Nothing states that exception: ADR
§ 5 `:364-368` says pushes wait for the first poll, not what that costs
freshness. It is older than the fixes, not caused by them. `WATCH_DELAY`, the
sender's gate and the fixture are unchanged since `cda3cfc` and `259db73`. The
fix diff's only change on this path, the first-sighting callback, runs after
the state is set. Dev's and QA's runs passing points to machine timing, not
to a cure.
Fix, either:
(a) **Shorten the window in the provider.** Make the first enablement check
run within a few seconds of `postInit`, and repeat it every few seconds while
the state is unknown or the realm is still missing. Count a missing realm as
unknown, not as "not enabled", or its later appearance adds a re-enablement
run beside the start run. Then go to the minute. NFR Freshness then holds and
the test passes as written. Add a `ListenerEnablementWatch` or factory test
for the cadence.
(b) **Keep the window and make it true and waited for.** Make the fixture wait
after readiness until the provider is known enabled, e.g. poll `LogsAsync()`
for "the listener is enabled in realm mustask", or await the start
reconciliation's begin at the receiver. ADR § 5 then states the start window
and what it costs freshness. The NFR Freshness deviation goes to the
orchestrator for gate-3 (the spec is gate-1's), not into `spec.md` silently.
(a) is recommended: it removes the deviation instead of documenting it.

### Behavioural claims in prose

Direction 1 covers prose the fix diff adds or changes, one line per claim.
Round 1's list is the baseline, and lines it anchored that the fix did not
touch are not repeated.

- `Reconciler.java:27-33` (recovery covered by a run begun since the
  failure; one reconciliation whichever comes first; skipped runs made up) →
  `:143-155`, `:160-179`, `:123-129`; `ReconcilerTest:256`, `:299`, `:318`,
  `:370`.
- `DeliveryRecovery.java:3-9`, `FeedSender.java:17-20` → `FeedSender.java:163-166`,
  `:140`; `Reconciler.java:133-155`.
- `ListenerEnablementWatch.java:13-17` → `:57-65`; `Reconciler.java:176`,
  `:123-129`.
- `ReadOnlyGuard.java:12-27` → `:40-41`, `:81-82`, `:106-108`;
  `ReadOnlyGuardIsNotVacuousTest.java:29`.
- ADR § 1 `:229-231` (`--target provider`, also named `test`) →
  `scripts/verify.sh:473`; `deploy/keycloak/Dockerfile:23`, `:36`.
- ADR § 4 `:342-345` (recovery requests unless covered) → `Reconciler.java:143-155`.
- ADR § 4 `:346-353` covers the queue's monitor. The anchors are
  `PendingChanges` `put` and `requeue` (synchronised merge, `onOverflow` run
  outside it), `take` (synchronised, in memory) and `awaitSize` (`wait`
  releases the monitor), plus the send outside any monitor (`FeedSender.java:126-133`).
- ADR § 5 `:361-379`. Poll cadence and first poll at 20 s →
  `UserProjectionListenerFactory.java:34-36`, `:112`. Held while unknown →
  `FeedSender.java:111-113`, `Reconciler.java:215-218`. Made up "by a minute,
  not by a period" → `ReconcilerTest:370` (at 80 s). The recovery rule →
  `Reconciler.java:143-155`.
- ADR § 6 `:426-433` (a stepped-back clock heals at the first run past the
  held markers, within one period) → `DirectorySnapshot.cs:56-62` (a begin
  at or below the newest begun is refused) and `ProjectedUser.cs:124-127`.
  A refused begin fails the run, which is retried each minute
  (`Reconciler.java:262-274`, `:174`), so "at most one period" is a true
  upper bound.
- ADR § 6 `:448-464` (one call, key order, then rows in id order, the
  completion's two sets in one call, snapshot lock first) →
  `UserDirectoryLocks.cs:31-40`, `:54-68`, `ProjectedUserStore.cs:16-24`,
  `CompleteSnapshotHandler.cs:66`, `DirectorySnapshotStore.cs:11`.
- ADR § 6 `:456-457` and plan § 5 `:178-179`: "a lock site added later takes
  its keys through the same method, in one call per transaction". This is a
  rule for future code, and nothing executes it. Its anchor is ADR-0025 plus
  stage 8 item 4 (`06-code-review.md:21-28`), which puts every later diff's
  reviewer in front of accepted ADRs. That is the weakest anchor there is,
  and is named as such. Today's three sites make one call each
  (`ApplyUserChangesHandler.cs:30`, `ApplySnapshotPageHandler.cs:50`,
  `CompleteSnapshotHandler.cs:66`).
- ADR § 7 `:501-517`. Feed address and host mapping → `AppHost.cs:334`,
  `UserProjectionIdentifiers.cs:46-49`, `UserProjectionStandTests.cs:66`.
  "Binds non-proxied on 0.0.0.0" → `AppHost.cs:88-94`, `:191`. A blocked
  route "shows as delivery failures, never a silent gap" →
  `FeedSender.java:177-179`. "Verified on Docker Desktop for Windows" →
  `qa.md` re-check 1, the live stand.
- ADR § 8 `:599-625` → `ReadOnlyGuard.java:32-68`, `:106-108`;
  `ProjectedRealm.java:32-40`; `ReadOnlyGuardIsNotVacuousTest.java:24-29`.
- ADR § 9 `:664-677`, and `CompleteSnapshotHandler.cs:17-20`, `:28-33`,
  `:64-65`, `DirectoryCollation.cs:33-34`, `ProjectedUser.cs:8-9`,
  `:173-181`, `IProjectedUserStore.cs:41-45` → `CompleteSnapshotHandler.cs:33`,
  `:62-76`; `ProjectedUserStore.FindStaleSortKeysAsync`;
  `ProjectedUser.cs:182-193`; `ReconciliationTests.cs:185` (lifecycle,
  moment and change time unchanged; `IncludeInactive` order).
- `AppHost.cs:32-36` → `:40`; `StandReset.cs:117`.
- `UserDirectoryBoundaryRules.cs:10-16`, `:25-29`, `:33` → `:61-72`,
  `:82-110`. "`IUserDirectory` and `DirectoryUser` match none" holds by
  ordinal comparison: `DirectoryUser` is not `DirectoryUsers`.
- Spec 16 `:31-35` (one `Deleted` row per run, name and e-mail kept) →
  `ProjectedUser.cs:100-104`; `ProjectionBatch.cs:44`; `qa.md:265-266`.
- Plan `:90-92` → `UserProjectionStandTests.cs:36`, `:45`, `:66`.
  `KeycloakRealmRolesTests.cs` is absent from `git diff main...`.
- Plan § 6 `:192` → `CompleteSnapshotHandler.cs:44-78`.
- Plan `:263` → F12. Plan `:264-265` → as ADR § 5. Plan `:277-280`, `:290-293` → as ADR § 8.
- `qa.md` D-4 status → the three named tests exist and pass, 92/92 this round.
  "Failed against the code before the fix" was not re-run. By reading, without
  the guard in `deliveryRecovered` both give two completions.
- `status.md` [dev] 2026-10-04: the fixes → each line above. "Provider tests
  92/92" → this round's run. "verify.sh: RESULT green" was not reproduced:
  this round's run failed on F13's test. The dev run may have drawn a
  favourable timing.

Direction 2 covers behaviour the fix diff changes, the identifiers grepped,
and what the hits were:

- **Delivery recovery no longer always asks for a run.** Grepped
  `recover|a reconciliation follows|delivery to MUSTASK recovered|onRecovery`
  in `spec.md`, the stories, `api-contract.md`, the plan, ADR-0025, the
  `AGENTS.md` files, `docs/runbooks/`, the provider's sources and the C#
  tests. Hits: `spec.md:55`, `:64`, `:103` (BR-UP-8's trigger list). These
  hold in substance: every outage's end is still followed by one
  reconciliation begun after delivery started failing (the retry's or the
  recovery's), and the queue keeps every change meanwhile. US-5 `:35-40` is
  outcome-based and holds. Plan `:263` → F12. Plan `:494`, `:513` (test
  matrix) hold. The live tests assert no recovery log line. The log lines
  quoted in `qa.md` are dated evidence, not claims.
- **A start run skipped while the state was unknown is made up.** Grepped
  `start reconciliation|30 s|first check|unknown` in the same set. ADR § 5
  and plan `:264-265` are updated. BR-UP-8's "at Keycloak start" now also
  holds when the first probe fails. Nothing is stale.
- **`--stand-reset` keeps the E2E stand file.** Grepped
  `keycloak-e2e-client.json|stand file|stand-reset` in `docs/`, the
  `AGENTS.md` files, `scripts/` and `e2e/support`. Hits:
  `src/backend/AGENTS.md:39`, `src/frontend/AGENTS.md:43` ("generates it at
  every stand start", still true), the `scripts/stand-reset.sh` header (does
  not mention the file), runbook `:119`. Nothing is stale.
- **The completion re-keys stale sort keys.** Grepped
  `sort_key_version|SortKeyVersion|recompute|already in this order` in
  `docs/` and the `AGENTS.md` files. `api-contract.md:118-119` ("A search
  answer is already in this order") holds outside the window after an ICU
  change on the API host. Round 1 accepted that window for present users
  (until the first reconciliation). For inactive users it now closes within
  one completion per 1 000 (ADR § 9 `:675-677`), so no finding. Plan
  `:119-120` holds.
- **The boundary rule is widened.** Grepped `A.8|IProjectedUserStore|DirectoryUsers`
  in `api-contract.md` and the plan. A.8 now has a guard for stores,
  entities and sets (see Observations for raw SQL).
- **Found through verification, not this sweep:** NFR Freshness
  (`spec.md:124-126`) against the provider's start hold → F13.
- **Not swept:** `docs/templates/examples/` (frozen), the E2E spec bodies.

### Observations, not findings

- F10's rule matches text. A consumer's raw SQL on `directory_users` or
  `directory_snapshots` is not a token, so it is not caught. A consumer
  property named `DirectoryUsers` would be flagged, a visible false positive.
  Adding the two table names to `ProjectionInternals` is cheap if wanted.
  Round 1 asked for the sets, and the rule claims no more.
- Round 1's note on `max_locks_per_transaction` now applies to the candidates
  plus up to 1 000 stale-key rows, after an ICU change.
- A `changes`-route-only failure, during which a period run completes, ends
  with no further run. Nothing is lost, because the queue still holds every
  change. ADR § 4–5 state the rule this follows.
- For the orchestrator:
  - QA's re-check over what the round-1 fixes touched is still owed: the
    `Reconciler`, `FeedSender` and `ListenerEnablementWatch` changes, the
    AppHost change and the completion's re-key.
  - The fixes changed two provider log lines, so a re-check grepping for "a
    reconciliation follows" will not find it after a recovery. The new lines
    are at `FeedSender.java:139` and `Reconciler.java:149`, `:153`.
  - The L security pass, in its own clean session, is still owed.

## Round 3 — backend

Code review (stage 8), round 3, a delta over round 2. Reviewer: code-reviewer,
clean context, author of nothing on this branch, 2026-10-04. Round 2 was the
last round the stage schedules; the orchestrator scheduled this one. Input:
round 2's open findings F12 and F13 and the fix diff `9f4a0cf..971c1cc` (13
files: `0366b6e`, `f4ce0d6`, `5c4ca3d`, `fd50be8`, `971c1cc`), read against
`spec.md` (NFR Freshness, NFR Logging), US-4, US-5, `plan-backend.md` and
ADR-0025 § 5.

Verification this round:
- `docker build --target provider`: 101/101 (`ListenerEnablementWatchTest`
  13, `ReconcilerTest` 21, `FeedSenderTest` 13).
- The `KeycloakProvider` category of `Mustask.Api.IntegrationTests`, three
  runs on their own and a fourth inside `verify.sh`: 10/10 each time. Round 2
  saw two failures in three runs. `ADeletion_IsPushedCarryingTheLastState`,
  F13's test, now takes 0.75 s. `Mustask.Contracts.Tests` `KeycloakProvider`:
  17/17.
- The full `scripts/verify.sh`: RESULT failed, on two environment causes, not
  on the code. (1) The `provider` bucket's `docker build` could not resolve
  `docker/dockerfile:1` ("TLS handshake timeout" at `auth.docker.io`). The
  same build had passed 101/101 here minutes before, and passed again on the
  retry right after. (2) `src/frontend/node_modules` is missing in this fresh
  worktree; the frontend bucket's only file is spec 16's comment. Everything
  else is green: docs checks, backend build, Shared 53, Domain 120,
  Application 51, Architecture 79, Contracts 114, Infrastructure 89, Api
  integration 488 (+10 opt-in skipped), and the `KeycloakProvider` tests.
- One throwaway container of the built image, with a fresh realm import and
  no published port, stopped and removed afterwards (the times are
  hh:mm:ss.s of one minute). The first check ran at 18.6 and found no realm
  ("realm mustask does not exist yet; changes are held …", INFO, once).
  Keycloak logged "Realm 'mustask' imported" at 19.7 and "Listening" at 20.2.
  The provider logged "the listener is enabled in realm mustask" at 20.7,
  0.5 s after readiness.

**Verdict: `needs-changes`.** 0 blocker, 1 major (F14), 2 minor (F15, F16),
all new. F12 and F13 are resolved. F14 is one Javadoc sentence that the F13
fix made false. The watch's class comment and ADR § 5, in the same diff, say
otherwise. No code, contract or test change is asked for any of the three.

What holds in the fix. F13 is fixed by round 2's option (a).
- `ListenerEnablementWatch.scheduleOn` (`:98-101`) runs the first check 2 s
  after `postInit`. `nextDelay` (`:122-138`) re-checks every 2 s while the
  state is unknown, for up to 5 minutes of nominal delays, then each minute,
  and each minute once the state is known.
- The chain re-schedules in `finally` (`:113-119`). A shut-down scheduler
  ends it quietly (`:106-110`); `close()` uses `shutdownNow`
  (`UserProjectionListenerFactory.java:122-124`), so no queued check
  outlives the provider.
- A missing realm while the state is unknown stays unknown (`:154-157`), so
  the import's arrival is the first sighting (`requestIfSkipped`), not a
  re-enablement (`:173-175`). Once the state is known, a realm gone counts as
  not enabled (`:159-160`, `:176-178`).
- `isEnabledOrUnknown` is unchanged, so the removal listener keeps capturing
  while the state is unknown (`UserRemovalListener.java:37`).
- The fields that the comment at `:74` reserves to the checks are written
  only on the single scheduler thread (`UserProjectionListenerFactory.java:75`).
  `scheduleOn`'s one write happens before the first task is submitted.
- The live fixture now absorbs the start window before any test runs
  (`LiveKeycloakFixture.cs:81`, `:84-105`). The order-dependence that made
  F13 a flake is gone, whatever the window. `LiveKeycloakProviderTests.cs:51-57`
  holds the window itself to the NFR's 10 s. Round 2's diagnostic run measured
  about 15 s under the old 20 s first check, so the test tells the two apart.

The tests check what they claim:
- `ListenerEnablementWatchTest:154`: no probe at 1.999 s, one at 2 s, the
  next at 62 s.
- `:175`: five probes in 10 s, seen at the first check after the realm
  arrives, no re-enablement.
- `:200`: 150 + 5 probes, one INFO line and one WARN.
- `:117` against `:135`: a realm missing before the state is known, and one
  gone after.
- `:225`: a shut-down scheduler ends the chain.
- `ReconcilerTest:410`: realm at 8 s, so the start run is the only run.
  Counting a missing realm as "not enabled" would add a run at 8 s.
- `ReconcilerTest:431`: realm at 42 s, made up once. `:370`: made up at 32 s.
- `FeedSenderTest:238`: held at 4 s, sent at 6 s.

### Round-2 findings

- **F12** — Status: resolved (`5c4ca3d`). Plan `:263` names
  `deliveryRecovered(mark)` and the mark taken at the outage's first failure,
  as `FeedSender.java:140` and `:165` do.
- **F13** — Status: resolved (`0366b6e`, `f4ce0d6`, `fd50be8`, `5c4ca3d`).
  Both halves are fixed:
  - The `provider` bucket's live tests are green in four runs of four here.
  - NFR Freshness holds from readiness on. Three anchors: the fixture's
    measurement, the live test that bounds it, and the container run above.
  ADR § 5 `:367-377` states the cadence and the missing-realm rule. No
  freshness deviation is left for gate-3.

### F14

Severity: major
Where: `deploy/keycloak/providers/user-projection/src/main/java/mustask/keycloak/userprojection/Reconciler.java:119-121`
Problem: the Javadoc of `requestIfSkipped` says: "A first enablement check
that failed leaves the state unknown past the start run, which is then
skipped; without this, the backfill would wait for the period."
- That was true with the 20 s first check and a minute between checks.
  Since `0366b6e` it is false. A failed first check is retried 2 s later
  (`ListenerEnablementWatch.java:122-129`). The state is still unknown when
  the start run is due (30 s) only if checks keep failing, or the realm stays
  missing, until then.
- The watch's class comment says exactly that
  (`ListenerEnablementWatch.java:30-33`, "a check failing until the start run
  is due"), and so does ADR § 5 `:378-381` ("checks failing past the start
  run"). Both are in the same diff, so this is major (stage 8, "Severity").
- A reader who comes to this method to learn when a run is skipped would
  expect one failed check to skip the start run. They would then look for
  the make-up log line (`Reconciler.java:125`), which never comes.
- The sentence also does not name the missing realm, which is now the
  ordinary way to reach this method (`ReconcilerTest:431`).
Fix: "… called when the listener is first seen enabled. Checks that fail, or
find realm `mustask` not imported yet, until the start run is due leave the
state unknown then, and that run is skipped; without this, the backfill
would wait for the period."
Status: resolved (`80d481f`). The Javadoc of `Reconciler.requestIfSkipped` now
reads as the Fix asks, naming the checks that fail and the realm not imported
yet; a comment only, no code change.

### F15

Severity: minor
Where: `docs/adr/0025-keycloak-user-projection.md:364-367`
Problem: the sentence reads: "While the listener is not enabled in the
realm — or not yet known to be, … — nothing is pushed and no reconciliation
runs (US-5 …); changes are still captured and held."
- The clause added at its end reads as covering both states. Only the
  unknown state captures.
- A realm that does not list the listener gets no events from Keycloak. The
  removal listener drops a removal unless the state is enabled or unknown
  (`UserRemovalListener.java:37`; `ListenerEnablementWatch.java:218-223`).
  Only what was queued before a disable is held.
- For consumers the outcome is the same: the re-enablement's reconciliation
  catches up. A deletion made meanwhile arrives as an absence, inactive
  either way. So this is minor.
Fix: "… changes are still captured and held while the state is unknown;
while the listener is known not to be enabled none are captured (what was
already queued is held), and the re-enablement's reconciliation catches up."
Status: resolved (`80d481f`). ADR-0025 § 5 limits "captured and held" to the
unknown state and says nothing is captured while the listener is known not to
be enabled; checked against `UserRemovalListener.java:37` and
`ListenerEnablementWatch.java:218-223`.

### F16

Severity: minor
Where: `docs/features/mod-014-keycloak-user-projection/plan-backend.md:265`
Problem: the watch row says "the first check finding it enabled →
`Reconciler.requestIfSkipped()`".
- The code calls it only from the first check that reads the realm, and
  only if that check finds the listener enabled
  (`ListenerEnablementWatch.java:162-170`).
- If that first reading finds the listener not enabled, the later first
  check that finds it enabled is a re-enablement. That calls `request()`
  (`:173-175`), not `requestIfSkipped()`.
- The constructor's Javadoc (`:89`) and ADR § 5 are precise. No agent would
  act differently: `request()` runs a reconciliation in every case where
  `requestIfSkipped()` would.
Fix: "the first check that reads the realm, if it finds it enabled →
`Reconciler.requestIfSkipped()`".
Status: resolved (`80d481f`). Plan `:265` reads as the Fix asks, matching
`ListenerEnablementWatch.java:162-175`.

### Behavioural claims in prose

Direction 1 covers prose the fix diff adds or changes, one line per claim.
The baseline is rounds 1 and 2.

- `ListenerEnablementWatch.java:11-34`, the class comment:
  - The cadence → `:98-101`, `:122-138`.
  - A missing realm while the state is unknown → `:154-157`.
  - A realm gone once the state is known → `:159-160`, `:176-178`.
  - The make-up run → `:162-170`, then `Reconciler.java:123-129`.
  - "Held for seconds" → `LiveKeycloakProviderTests.cs:51-57` and the
    container run.
- `ListenerEnablementWatch.java:74` ("touched only by the checks, one at a
  time on the scheduler's thread") → the single-thread scheduler
  (`UserProjectionListenerFactory.java:75`) and the chain at `:98-119`. In
  main code, `scheduleOn` is the only entry point
  (`UserProjectionListenerFactory.java:106`); `check()` is public for the
  tests.
- `ListenerEnablementWatch.java`, the rest:
  - `:86-90`, the constructor parameters → `:162-175`.
  - `:97`, "they stop with it" → `:106-110`.
  - `:140` → `:144-157`.
  - `:200`, "reported once at INFO" → `:204-210` and the test at `:200`.
  - `:218` → `:219-223`.
- `UserProjectionListenerFactory.java:132` → `:133-139`, and
  `ListenerEnablementWatchTest` `sightingIn_readsTheRealmsEventListeners`.
- ADR § 5:
  - `:361-364`, the triggers → as round 2.
  - `:364-367`: nothing pushed → `FeedSender.java:111-112`; no
    reconciliation → `Reconciler.java:215-218`; "changes are still captured
    and held" → F15.
  - `:367-370`, the cadence → `ListenerEnablementWatch.java:37-47`,
    `:122-138`; tests `:154`, `:175`, `:200`.
  - `:370-373`: "Keycloak imports the realm before it answers … within about
    2 s of readiness … NFR Freshness's 10 s holds from readiness on; the live
    fixture measures it". Anchors: the container run (imported 0.5 s before
    "Listening", seen 0.5 s after), `LiveKeycloakFixture.cs:73` (ready means
    `/realms/mustask` answers) and `:84-105`, and
    `LiveKeycloakProviderTests.cs:51-57`, 4/4 here. Keycloak's start order is
    an empirical anchor. If a Keycloak upgrade changed it, the live test is
    what would show it.
  - `:373-377`, the missing realm and the realm gone →
    `ListenerEnablementWatch.java:154-157`, `:173-178`;
    `ListenerEnablementWatchTest:117`, `:135`; `ReconcilerTest:410`.
  - `:377-381`, made up "by seconds (or, after the quick checks, a minute)"
    → `Reconciler.java:123-129`; `ReconcilerTest:370` (32 s), `:431` (42 s);
    the minute → `ListenerEnablementWatch.java:137`.
- The plan:
  - `:263` → as F12.
  - `:265`, the cadence → as ADR § 5.
  - `:265`, "a missing realm is unknown until the state is known, not
    enabled after" → `:154-160`.
  - `:265`, "the first check finding it enabled" → F16.
- Test comments:
  - `LiveKeycloakFixture.cs:42-49`: "measured here, before any test runs" →
    `:81`; "Null when it did not arrive" → `:101-104`.
  - `LiveKeycloakProviderTests.cs:46-49` → `:51-57`.
  - `FeedSenderTest:235-236`; `ReconcilerTest:349-353`, `:366-368`,
    `:392-393`, `:406-408`, `:427-429`; and the `ListenerEnablementWatchTest` comments →
    the assertions beneath each.
  - `VirtualScheduler.java:77` ("what a real ScheduledThreadPoolExecutor does
    once shut down") → the JDK's default `AbortPolicy`, which throws
    `RejectedExecutionException` after shutdown.
- Commit bodies:
  - `0366b6e`, "quick failing checks log one INFO line, not one per check" →
    `ListenerEnablementWatch.java:201-210`.
  - `f4ce0d6` and `fd50be8` → as above.
- `status.md` [dev] 2026-10-04, the round-2 fixes:
  - The cadence and the missing realm → as ADR § 5.
  - "Realm imported 0.5 s before Listening, listener seen enabled 1.7 s after
    it" → reproduced in kind (0.5 s and 0.5 s).
  - "New provider tests … failed with each old behaviour put back" → not
    re-run, because that takes code edits. By reading: under the 20 s delay,
    `:154` sees no probe at 2 s; `:117` and `ReconcilerTest:410` count a
    re-enablement; `FeedSenderTest:238` is still held at 6 s.
  - "Changed watch log lines" → `:155` via `:206`, and `:132`. Two other
    changed lines are not listed; see Observations.
  - "verify.sh: RESULT green (provider 101/101, live 10/10, Api integration
    488)" → each number reproduced. This round's own `verify.sh` failed on
    the two environment causes above.

Direction 2 covers behaviour the fix diff changes, the identifiers grepped,
and what the hits were. The search covered the repository outside
`docs/templates/examples/`.

- **The cadence: the first check at 2 s, not 20 s, then quick re-checks.**
  - Grepped `WATCH_DELAY|WATCH_PERIOD|isEnabledIn|first poll|polled each
    minute|20 s after|first 20 s|at 20 s`. The only hits are this file's
    rounds 1–2, which are history.
  - Grepped `fail\w* first|first \w* ?check \w* ?fail|first probe|first
    enablement`. Hits: `Reconciler.java:120` → F14;
    `LiveKeycloakProviderTests.cs:47`, past tense and accurate.
  - Grepped `each minute|every minute|by a minute|first check|first
    sighting|first successful` in `docs/`, the `AGENTS.md` files and the
    provider's sources. Hits:
    - ADR § 5 and plan `:264-265`, both in the fix diff.
    - `Reconciler.java:25` and `:31`: the retry's minute, unchanged and true.
    - US-5 `:87-92`: outcome-based, holds.
    - `qa.md`: dated evidence.
    - `src/frontend/AGENTS.md:94` and `apiReadiness.ts`: the E2E readiness
      gate, a different "first check".
- **A missing realm while the state is unknown is no longer "not enabled".**
  Grepped `not enabled|enabled again|re-enabl|missing realm|does not exist
  yet` in the same set. Hits:
  - `spec.md:50`, `:79`, `:120`, US-3 and US-5: an operator re-enabling the
    listener; they hold.
  - `qa.md` RC-9 and A-35: dated evidence of the operator path, which this
    fix does not change.
  - `KeycloakStandDriftCheck.cs:89`: the stand's drift check, a different
    mechanism.
  None is stale.
- **The watch logs a failing probe once per episode, not each minute.**
  Grepped `could not read realm|last known state`; the only hits are in the
  watch itself. NFR Logging (`spec.md:140-146`) names refused pushes, failed
  deliveries and completed reconciliations, not the probe. None stale.
- **The live fixture creates a user, `live.first`, before any test.**
  Grepped `LiveKeycloak|live test|9/9|10/10` in `*.md`. Hits: ADR `:220`,
  `:232`, `:240`, `:714`, and plan § 10 and § 13, none of which gives a test
  count or a list of users. None stale. The live tests pass with the extra
  user.
- **Not swept:** `docs/templates/examples/` (frozen), and the E2E spec bodies
  (the fix diff touches no stand or E2E code).

### Observations, not findings

- The [dev] Log line lists two changed watch log lines and leaves out two
  others: the WARN "realm mustask no longer exists …"
  (`ListenerEnablementWatch.java:177`), and the WARN for an unreadable realm
  once the state is known. That WARN is now logged once per failing episode
  (`:191`), with DEBUG after it (`:194`). The list is meant for QA's grep and
  claims no completeness. For QA's re-check, the watch's lines are `:132`,
  `:164`, `:174`, `:177`, `:180`, `:191` and `:206`.
- `QUICK_CHECKS_FOR` is measured in nominal delays (`dueAt`), not wall time,
  so a slow probe stretches the quick phase a little. Harmless.
- For the orchestrator:
  - QA's re-check is still owed, over the round-1 fixes (round 2's list) and
    now also over the watch on the stand. On a fresh-store start, look for
    the INFO "does not exist yet" line, then "enabled" within about 2 s of
    readiness. On a persistent-store start, look for "enabled" at the first
    check.
  - The L security pass, in its own clean session, is still owed.
  - F14–F16 are prose. A fix round changes no behaviour that QA would
    re-run.

## Security pass — backend

The separate L security pass (stage 8, "For L features, additionally").
Reviewer: code-reviewer, clean context, author of nothing on this branch,
2026-10-04. Scope: security only, against the orchestrator's eight-item
checklist. Correctness, architecture and the prose pass belong to rounds 1–3
and are not repeated here. Input: `git diff main...d129de4` (189 files),
`api-contract.md` Part B, US-6, `spec.md` BR-UP-1, BR-UP-5..13 and NFR
Logging / Audit / Stand, ADR-0025 §§ 3–8, and D-9, D-10, D-12 in `status.md`.
The six commits that landed during the pass (`d129de4..8c0ab59`: QA re-check
2 tests, the round-3 prose fixes, one Javadoc) change no code this pass looks
at. The anchors below are taken at `8c0ab59`.

**Verdict: `needs-changes`.** 0 blocker, 1 major (S1), 4 minor (S2–S5), all
open. S1 lets any user of realm `mustask` stop reconciliation for everyone.
Nothing found exposes data or bypasses authorization: the HMAC channel, the
refusals, the read-only provider, the stand reset and the logging hold.

### S1

Severity: major
Where:
- the source: `deploy/keycloak/providers/user-projection/src/main/java/mustask/keycloak/userprojection/ProjectedUserReader.java:19-27`;
- the effect: `FeedSender.java:146-150` and `Reconciler.java:246`, `:263-275` (same folder);
- the refusal: `src/backend/Mustask.Api/Features/UserDirectory/SignedPushBody.cs:44-51`.

Problem: any user of realm `mustask` can stop reconciliation for the whole
realm, indefinitely. All it takes is an unpaired UTF-16 surrogate in their
own first or last name.

How it happens:
- **Keycloak accepts the value.** Account REST answered a self-service update
  204, and admin REST answered a create 201 (live fixture). The person-name
  validator refuses control characters: U+0000 got
  `error-person-name-invalid-character`. It does not refuse an unpaired
  surrogate. Users have `manage-account` by default. The stand's realm import
  sets no default roles and no account clients, so the same should hold
  there — but this was run in the live fixture's own realm, not on the stand.
- **The provider pushes it.** It captures the `UPDATE_PROFILE` event and
  sends the name as Jackson writes it: `"firstName":"Сам\uD800"` (observed).
- **MUSTASK refuses it.** System.Text.Json rejects an unpaired surrogate
  escape, so the push is answered 400 `user_directory.push_malformed`. The new
  probe `PushChannelSecurityProbeTests`, case "lone surrogate", sends exactly
  that body.
- **The whole batch is dropped.** On a 400 the provider drops the batch
  (`FeedSender.java:146-150`, "a contract bug"). A batch holds up to 500
  entries, filled for 200 ms. Any other user's change taken into it is lost,
  a disable or a deletion of someone else included.
- **Reconciliation never completes again.** Reconciliation is what heals lost
  changes (BR-UP-7, BR-UP-8, NFR Freshness: "a change whose push was lost
  shows by the next reconciliation"). But the page carrying this user is
  refused the same way (`Reconciler.java:246` → `:263-275`: FAILED, retried
  in 60 s). The user is in every enumeration, so every run fails at that
  page. Until an administrator edits the name, nothing lost is healed and
  nobody is marked absent.
- **The operator gets no lead.** The failure line names the page and the
  error code, not a user id.

Seen end to end in the live fixture, with the receiver answering a body that
carries `\uD800` the way MUSTASK does:
1. The self-service update answered 204.
2. The changes push was answered 400, and the provider logged it as
   "dropped".
3. After a delivery recovery, the provider logged "reconciliation … stopped
   at its page 0 (HTTP 400 …); nobody is marked absent, it is retried in
   60 s".
4. No completion followed.

Why major and not blocker: nothing confidential leaks, and nothing is
authorized by the status (BR-UP-12). Why at least major: an ordinary user's
input turns off a guarantee the spec gives every consumer. ADR-0025 § 4
(`:340`) and the FeedSender Javadoc (`:15`) treat a 400 as "a contract bug".
This one is a user's input.

Fix:
- **Provider:** make every projected string well-formed before it is queued.
  Do it in `ProjectedUserReader.read`, the one place the event, removal and
  reconciliation paths share: replace unpaired surrogates with U+FFFD and drop
  U+0000 (see S2). Add a provider unit test with `"\uD800"` in each of the
  four fields. Add a live test where a user updates their own name over
  account REST and the push is accepted.
- **Provider:** when MUSTASK refuses a reconciliation page with 400, log the
  page's user ids — ids only, as the B-1 drop line already does.
- **ADR-0025 § 4 and the FeedSender Javadoc:** a 400 can also mean input that
  Keycloak accepted and MUSTASK cannot parse. Say that the provider now
  prevents it.

Status: resolved (`d65834f`). `ProjectedUserReader.read`, the one place the
event, removal and reconciliation paths share, now makes the id and the four
projected strings well-formed: an unpaired surrogate becomes U+FFFD, U+0000 is
dropped (S2's source), a pair of surrogates and a null are kept.
- **Tests:** `ProjectedRealmAndReaderTest` (nine malformed shapes in each of the
  four fields, the id, well-formed text kept, and the push body of such a user
  valid UTF-8 with no escaped surrogate), `UserProjectionListenerTest` (an own
  profile update), `UserRemovalListenerTest`, `KeycloakRealmSourceTest` (a page
  of the snapshot). All failed with the old reader (the provider run before the
  fix: 16 failures in five classes, with the two `ReconcilerTest` cases below).
- **Live (opt-in):** `LiveKeycloakProviderTests.AUsersOwnNameWithAnUnpairedSurrogate_…`
  — a user updates their own first name over account REST (204), the receiver now
  answers 400 to a body System.Text.Json cannot read, as MUSTASK does
  (`SignatureCheckingReceiver`), and the push is accepted with `Сам�`; after
  an outage and its recovery the reconciliation completes with that user on its
  page and no 400 was answered. It failed against the old provider (the push
  never arrived: a 15 s timeout).
- **The 400 lead:** `Reconciler` logs a page refused with 400 at ERROR with the
  ids of the whole page (up to 500 — the one to look for is among them), ids
  only; other failures and a refused begin or completion name no id
  (`ReconcilerTest`, three tests).
- **Prose:** ADR-0025 § 4 and the `FeedSender` Javadoc and drop line say a 400
  can also be input Keycloak accepted and MUSTASK cannot parse, which the
  provider now prevents; plan § 5's `ProjectedUserReader` row says so too.
- **Taken in the same commit — QA re-check 2, O-1 and O-4** (both in
  `Reconciler.java`): `deliveryRecovered` now asks for its run (`request()`)
  before it logs that one follows, so the window in which a retry and the
  recovery both run is the nanoseconds between the counter reads and the
  request (`ReconcilerTest.aRecovery_asksForItsRunBeforeItLogsThatOneFollows`,
  which failed before); the Javadoc reads "should it fail, it schedules the
  retry itself", and ADR-0025 § 5 had the same words ("its retry is already
  pending") and reads the same now.

### S2

Severity: minor
Where:
- no rule refuses it: `src/backend/Mustask.Domain/UserDirectory/UserPushValidation.cs:161-178`;
- it fails here: `src/backend/Mustask.Infrastructure/UserDirectory/UserDirectoryLocks.cs:31-37` for an id, at save for any other field.

Problem: a signed push with U+0000 in `id`, `username` or `firstName` is
answered 500 `http.internal_error`. All three were probed. PostgreSQL text
cannot hold 0x00.
- The contract promises 400 for an invalid value.
- The provider retries a 5xx without end (`FeedSender.java:154`, requeue), so
  one such entry stalls the whole change feed.

Why minor: few can send it. Admin REST refused U+0000 in all four projected
fields (probed: `error-person-name-invalid-character`,
`error-username-invalid-character`, `error-invalid-email`), and a
PostgreSQL-backed Keycloak cannot store it. That leaves the holder of the
secret, or an unvalidated import into the stand's H2 store.

Fix:
- In `UserPushValidation`, refuse U+0000 in the five text fields with
  `value_invalid` (400). Add a test per field.
- Name the case in the `value_invalid` row of `api-contract.md` Part C.
- S1's provider-side cleaning removes the source.

Status: resolved (`f928b23`). `UserPushValidation` refuses U+0000 in the five
text fields as `value_invalid` (400), on both apply paths, with the field's JSON
path as the key; a text over 255 code points is still `value_too_long` first.
`PushNulCharacterTests`: each of the five fields on B-1 and on a B-3 page (ten
cases), and one entry among good ones naming only itself; all failed with a 500
before. The `value_invalid` row of Part C names the case and B.3 states it
(a documented row, no new code); the Log line in `status.md` records the change.

### S3

Severity: minor
Where: `src/backend/Mustask.Api/Features/UserDirectory/SignedPushBody.cs:37-40`
Problem: the body is read and buffered before any header is looked at. That
is up to 4 MiB, plus a second copy in `ToArray`.
- An unauthenticated caller can make the API hold about 8 MiB per request.
- An unsigned body over the limit is answered 400
  `user_directory.push_malformed`, not 401. Probed; the probe was not kept.
- Every refusal writes two WARN lines, the verifier's and
  `BusinessExceptionHandler`'s, and there is no rate limit.

Why minor: the cost is bounded, and it is below Kestrel's 30 MB default that
every other route has.

Fix: in `BindAsync`, run the checks that need no body first: both headers
present and well-formed, and the timestamp within the window. Then read the
body, then check the HMAC. The contract's "read (bounded by 4 MiB) and
verified before it is parsed" still holds.

Status: resolved (`513ac88`). `PushSignatureVerifier` splits the check:
`CheckEnvelope` / `EnsureEnvelopeValid` (headers present and well-formed,
timestamp inside the window) and the full `Check` / `EnsureValid`;
`SignedPushBody.BindAsync` runs the envelope first, then reads the bounded body,
then verifies the HMAC. `PushEnvelopeBeforeBodyTests`: an unsigned, a
stale-timestamp and a malformed-header body over 4 MiB are each 401, not 400; a
body that carries well-formed headers is still 400; and a counter on the server's
side of the request stream reads 0 bytes for an unsigned 3 MiB body (the control,
a signed one, is read in full). With the early check taken out four of the five
fail (the counter reads 3 145 755 bytes). The contract's B.2 and ADR-0025 § 3 say
the order.

### S4

Severity: minor
Where: `src/backend/Mustask.Api/Features/UserDirectory/PushSignatureVerifier.cs:90-94`; `PushChannelOptions.cs:43-52`
Problem: a request accepted under `PreviousSecret` is not logged and looks
like any other, and `PreviousSecret` never expires.
- ADR-0025 § 3's rotation ends with "remove `PreviousSecret`". The operator
  has no signal that the provider has moved to the new secret.
- If `PreviousSecret` is left configured — after a rotation made because the
  old secret leaked — the old secret stays valid, silently.

Fix:
- Log at WARN when a request is accepted under the previous secret, once per
  process or rate-limited, naming the key and never the value.
- Log at Information at startup when `PreviousSecret` is configured.
- Cover both with a test through `ConsoleCapture`.

Status: resolved (`513ac88`). `PushSignatureVerifier.EnsureValid` logs a Warning
when a request is accepted under `PreviousSecret`, at most once an hour (a
one-hour hold-back kept per process, thread-safe), naming `UserDirectory:Push:PreviousSecret`
and never a value; `Check` alone stays free of side effects. A new
`PushChannelStartupNotice` hosted service logs at Information at startup when the
key is configured. `PreviousSecretAuditTests`, through `ConsoleCapture`: the
startup line (one, by key only), no line without the key, one Warning for three
accepted requests, none for requests under the current secret; and, on a settable
clock, the Warning again after the hour and not for the current secret. The
secrets are asserted absent from the output. ADR-0025 § 3 and the contract's B.6
say so.

### S5

Severity: minor
Where: `src/backend/Mustask.Application/Features/UserDirectory/ApplyUserChanges/ApplyUserChangesHandler.cs:38-45`
Problem: a reported deletion is final (BR-UP-5), yet no log line says which
ids were deleted.
- The Information line counts deletions inside `changed`.
- The Debug line lists every id of the request, not which ones were
  deletions.
- NFR Audit leaves the audit to Keycloak's event log. That log records
  deletions made in Keycloak, not a deletion pushed by whoever holds the
  secret. Such a deletion leaves the row (`lifecycle`, `changed_at`) and a
  count, and nothing that names it.

Fix: count deletions separately in the B-1 line, and log the ids of entries
applied as deletions at Information (ids only, as NFR Logging allows). If the
human prefers NFR Audit as written, waive this at gate-3.

Status: resolved (`e83c995`). `ProjectionBatchResult` gains `Deleted`, the ids
whose deletion the batch applied (a state entry for a new id added as deleted, or
a held user newly deleted; a deletion that finds the user deleted already is a
duplicate and not listed). `ApplyUserChangesHandler`'s Information line ends
"…, deletions applied N", and when N > 0 a second Information line names the ids
("User directory: deletions applied for user ids …"), ids only. Tests:
`ProjectionBatchTests` (applied, already deleted, plain state, an id twice) and
`DeletionAuditLogTests` through `ConsoleCapture` (the count, the ids, none of the
surname, e-mail or username the deletion carries; a repeated deletion counts 0
and names nothing; a push with no deletion logs no ids line). The contract's B.6
says so. Nothing is waived; the human may still prefer NFR Audit as written at
gate-3.

### Checklist

One entry per item, with what was checked. "Clean" means no finding.

1. **Authorization of every endpoint — clean.**
   - The four feed routes are the only additions to Mustask.Api's surface
     (`UserDirectoryEndpoints.cs:29-62`). The read model has no route
     (`IUserDirectory` is in-process). The drift check is an AppHost health
     check, not an API endpoint.
   - Each route is `.Public(...)` and takes its body through
     `SignedPushBody<T>`. `EndpointCategoryInventoryTests.cs:50-53`
     allowlists exactly these four, and `UserDirectoryRouteBindingTests`
     proves each binds exactly one `SignedPushBody<T>`.
   - Refusals are tested (`PushChannelAuthenticationTests`) for: no
     credential, a wrong secret, the Bearer token of an ordinary user and of
     a user with every code, a Bearer beside a wrong signature, a stale
     timestamp, a malformed header, another operation's signature, an
     altered body. `UserDirectoryWriteSurfaceTests:35-52` sends a user's
     Bearer token to all four routes.
2. **HMAC push channel — clean except S4.**
   - **Canonical string:** `PushSignatureVerifier.cs:100-107` matches
     `PushSigner.java:33-35`, with shared test vectors on both sides. No
     field can carry an LF.
   - **Target:** it is the raw path, so a captured signature does not carry
     over to another spelling of the same route (new probe).
   - **Comparison:** `FixedTimeEquals` over 32 bytes (`:113-114`), lowercase
     hex only, exactly 64 digits. A header sent twice is refused (new probe).
   - **Window:** ±300 s (`:81-86`, `PushTimestampWindowTests`).
   - **Replay inside the window is harmless:** B-1
     (`ApplyUserChangesEndpointTests:196`), B-2 (`BeginSnapshotEndpointTests`),
     B-3 and B-4 idempotent (`CompleteSnapshotEndpointTests`
     `Complete_Again`). The Host header is not signed, so a captured request
     replays against another instance that shares the secret: keep one
     secret per environment.
   - **The secret:** 32 bytes from `RandomNumberGenerator`, base64url
     (`UserProjectionPushSecret.cs:19-24`). Read only from environment on
     both sides (`PushChannelOptions.cs:32`, `FeedOptions.java:37`). The
     AppHost declares it `secret: true` (`AppHost.cs:277-278`), so the
     dashboard masks it. The only build args are the image and the tag. No
     code writes it to a file.
   - It is visible in `docker inspect` of the Keycloak container and in the
     api's process environment while they run. That is inherent to
     BR-UP-10's environment-only rule; the value is per process, and the
     container is removed with the AppHost.
3. **IDOR and cross-realm — clean.**
   - One machine principal: there is no per-user authorization to bypass,
     and ids are opaque.
   - The provider reads realm `mustask` only. `ProjectedRealm` is the only
     lookup, by name. The event listener checks the realm id
     (`UserProjectionListener.java:87-88`), the removal listener the realm
     name (`UserRemovalListener.java:37`). The `ReadOnlyGuard` allowlist
     passed in the provider build.
   - The drift check only GETs under `/admin/realms/mustask`, plus a token
     from realm master, and writes nothing.
   - The stand reset (D-10, D-12):
     - container mode removes only this checkout's two generated volumes;
     - external mode drops only the database the connection string names
       (`Target.Database`), never `postgres`, `template0` or `template1`;
     - it refuses while anyone is connected (55006) or while a container
       holds a volume (`StandReset.cs`);
     - the Keycloak volume is keyed by host, port and database.
4. **Personal data in logs and responses — clean.**
   - **MUSTASK logs:** a refusal's reason, method and route; counts; ids at
     Debug only; snapshot ids. `UserDirectoryLoggingTests` asserts no
     surname, e-mail, username or secret.
   - **Exception paths:** the business handler logs the code, the template
     and the params, not values. JsonException messages carry a path and a
     position. A 500 logs Npgsql's error without parameter values: no
     `EnableSensitiveDataLogging`, no `Include Error Detail`.
   - **Provider:** `FeedOptions.toString` omits the secret. The refusal,
     drop and failure lines carry ids, status and error codes only. The new
     live probe confirms that Keycloak's log, over accepted, 401 and 400
     pushes, carries the ids and neither the secret nor a name, username or
     e-mail.
   - **Drift check:** its findings carry role, client and group names. The
     admin password and the panel secret are compared, never printed.
   - **AppHost console:** the volume name and `host:port/database`, no
     credential (`StandStoreKey.cs`, `ExternalStandResetPlan.ToString`).
   - **API responses:** field keys are JSON paths, never values.
5. **Input validation — not clean: S1 (major), S2, S3.**
   - **Limits:** 255 code points per text field, 500 entries, a moment above
     0 and at most 1 h ahead, counts ≥ 0, a negative page number refused, 500
     ids in the read model. The developer's and QA's tests cover these.
   - **Bodies:** over 4 MiB → 400 (unsigned too, S3). Nesting past depth
     64, invalid UTF-8, an unpaired surrogate escape → 400 (new probes).
     U+0000 → 500 (S2).
   - **Snapshot completeness:** completion requires pages `0..n-1` exactly
     and `entryCount` stamped users, so withheld or lost pages mark nobody
     (tested).
   - **A holder of the secret** can mark every present user absent with an
     empty "complete" snapshot, or delete everyone through B-1. That is the
     design for one trusted principal: an absence reverts on the next push, a
     deletion does not.
6. **Injections — clean.**
   - SQL is parameterised (`UserDirectoryLocks.cs`,
     `ExecuteSqlInterpolatedAsync`), and LIKE patterns escape `%`, `_` and
     `\` (`DirectorySearchText.cs:101-116`).
   - The reset's DROP and CREATE quote the identifier, doubling inner quotes,
     and refuse system databases (`StandResetRules.cs:42-55`).
   - Docker is started with `ArgumentList` (`StandReset.cs:178-187`), and
     `stand-reset.sh` passes `"$@"` to `dotnet run`.
   - The Dockerfile takes no input beyond the configured image and tag.
7. **Secrets in code — clean.** A grep of the diff's added lines finds only
   test literals that say they are not real (`test-push-secret-not-real-…`,
   `example-only-not-a-real-secret-…`) and random values in the live
   fixture. The realm-import change is `eventsListeners` only.
8. **Audit trail — not clean: S4, S5.**
   - A snapshot completion is logged with its id and `markedAbsent`, on both
     sides.
   - Each drift finding is logged once, at error.
   - The reset prints what it will wipe and asks before.
   - A secret rotation on the stand is a reset (D-9).

### What ran

All exit 0 unless noted.

- `dotnet build tests/Mustask.Api.IntegrationTests`: 0 warnings, 0 errors.
- `Mustask.Api.IntegrationTests`:
  - the `Features.UserDirectory` and `EndpointCategoryInventoryTests`
    filter: 152 passed, 10 skipped (opt-in live);
  - the whole suite with the new probes: 493 passed, 11 skipped.
- Opt-in live (`MUSTASK_KEYCLOAK_PROVIDER_TESTS=1`), the whole `Live`
  folder: 11/11, the developer's 10 plus the new log probe.
- `docker build --target provider deploy/keycloak`: the `mvn verify` layer
  was cached from an identical green build.
- `Mustask.Contracts.Tests`:
  - the stand-reset, store-key, signature-vector, drift-check and stand
    filters: 60/60;
  - `KeycloakRealm*`: 14/14.
- `Mustask.Architecture.Tests`:
  - the user-projection and user-directory rules: 11/11;
  - `StandOnlyKeycloakClientRules` and `AppHostExternalDatabaseRules`:
    18/18.
- Observation runs, not kept. Each ended in exit 1 by design: the test
  failed on purpose so that its output carried the observation.
  - U+0000 over admin REST: refused in all four fields.
  - An unpaired surrogate over admin REST: 201, stored, pushed as `\uD800`.
  - The same over account REST as the user: 204, pushed. A first try
    without `Accept: application/json` got 405.
  - The poisoned-name chain of S1.
- Two probe observations, not kept (both passed): U+0000 answered 500 (S2),
  and an unsigned body over 4 MiB answered 400 (S3).
- Not run: the full `scripts/verify.sh`. Round 3 ran it, and it needs the
  frontend `node_modules` this worktree lacks. No live stand was started.
  The Testcontainers containers were removed by Ryuk.

### Probing tests

Written, run green, and **not committed**: QA's re-check held the branch.
They are worth keeping, and the developer can add them with the S1 fix. A
copy is in the reviewer's session scratchpad (`…/scratchpad/probes/`, see
the report).
- `src/backend/tests/Mustask.Api.IntegrationTests/Features/UserDirectory/PushChannelSecurityProbeTests.cs`,
  5 tests:
  - bodies the JSON reader rejects are a 400, never a 500, and change
    nothing: the unpaired surrogate escape as the provider writes it, invalid
    UTF-8, and nesting past the reader's depth;
  - a signature header sent twice is refused;
  - a captured signature replayed on another spelling of the route (a
    trailing slash) is refused.
- `…/UserDirectory/Live/LiveProviderLogProbeTests.cs`, 1 opt-in test:
  Keycloak's log over accepted, 401 and 400 pushes carries the ids and never
  the secret or a person.
- `LiveKeycloakFixture.cs` gains the internal `PushSecret` accessor it needs.

### Observations, not findings

- **A future `enumerationStart`.** A begin up to 1 h ahead (from the secret,
  or a provider clock that is ahead) makes every later begin a 409 until
  MUSTASK's clock passes it. That is robustness, not security; it is here
  for the general review.
- **Floating base-image tags.** The Dockerfile's base images are floating
  tags (`26.4`, `maven:3.9-eclipse-temurin-21`). The supply-chain posture is
  unchanged from before.
- **Unbounded response read.** `PushClient` reads MUSTASK's response without
  a bound. MUSTASK is trusted.
- **For the orchestrator:**
  - S1 goes back to the developer (the provider). QA re-checks the provider
    change, and S2–S4 if they are taken.
  - The next security re-check is a delta over S1–S5.

## Security pass — backend, delta 1

Reviewer: code-reviewer, clean context, author of nothing on this branch,
2026-10-04. Scope: the fix commits `f50d122..319f169` (`d65834f`, `f928b23`,
`513ac88`, `e83c995`, `319f169`, and the probe tests of `f50d122`), checked
against S1–S5, plus a prose pass over what they change. The anchors are taken
at `319f169`.

**Verdict: `needs-changes`.** S1–S5 are closed in code and tests. 0 blocker,
1 new major (S6), 2 new minor (S7, S8), all prose; no code change is
required. S6 is a security guarantee that ADR-0025 § 3 now gives and the code
does not hold: an unauthenticated caller can still make the API buffer up to
4 MiB.

### S1–S5

- **S1 — closed.**
  - **The cleaning.** `ProjectedUserReader.read`
    (`ProjectedUserReader.java:26-34`) cleans the id and all four strings
    through `wellFormed` (`:43-68`).
    - Every entry is built through `read`: the event and removal paths via
      `ChangeCapture.java:33`, reconciliation via `KeycloakRealmSource.java:74`.
      Nothing else constructs a `UserState`.
    - No attribute is projected: `UserState` holds five strings and
      `enabled`.
    - Everything else on the wire is made by the provider: the snapshot id (a
      UUID), moments and counts.
    - Valid pairs and null are kept. Lone high or low surrogates become
      U+FFFD — a reversed pair and a trailing high surrogate included — and
      U+0000 is dropped.
  - **The 400 lead.** A page refused with 400 logs the page's ids at ERROR,
    ids only (`Reconciler.java:236`, `:253`, `:280-283`, `:296-298`). A 401, a
    5xx, a timeout, a begin or a completion names no id.
  - **Prose.** ADR-0025 § 4 (`:349-360`), `FeedSender.java:15-19` and `:151`,
    plan § 5 (`plan-backend.md:260`).
  - **Collision.** Two values that differ only by the bad code unit project
    to one string. That is harmless today:
    - `directory_users` has no unique index besides the primary key
      (`ProjectedUserConfiguration.cs:27`, `:52`);
    - nothing looks a user up by username or e-mail;
    - ids are Keycloak's UUIDs.
    - Probe P4 and the live probe confirm it.
  - **Live.**
    - Account REST answered 204 to a lone surrogate in the first name, the
      last name **and the e-mail**. E-mail was therefore a second vector, and
      the fix covers it (the prose: S8).
    - With `editUsernameAllowed` on, the username was refused: 400
      `error-username-invalid-character`.
- **S2 — closed.**
  - **The rule.** `UserPushValidation.cs:183-187` refuses U+0000 in all five
    text fields (`:64-68`), on both paths (`ApplyUserChangesHandler.cs:28`,
    `ApplySnapshotPageHandler.cs:33`). B-2 and B-4 carry no text, and the
    route ids are `:guid`.
  - **Tests.** `PushNulCharacterTests` covers each of the five fields on B-1
    and on B-3, plus one bad entry among good ones.
  - **Over-length.** A text with U+0000 that is also over 255 code points is
    `value_too_long` instead (probe P5; S8).
- **S3 — closed as prescribed.**
  - **Order.** `SignedPushBody.cs:42` checks the envelope, `:44` reads the
    bounded body, `:46` checks the HMAC.
  - **No key oracle before the body.** The envelope check consults no secret
    (`PushSignatureVerifier.cs:127-150`).
    - A forged envelope, a wrong key and no headers draw identical 401
      problem documents (P3).
    - Over 4 MiB, a forged envelope, a wrong key and the right key all draw
      400 `push_malformed` (P2).
  - **Fails closed.** A valid envelope with a wrong HMAC is refused and
    changes nothing (P3).
  - **Residual.** The envelope needs no secret, so a request with forged
    headers still has its body read in full (P1). That cost is acceptable at
    S3's minor severity, but the prose overstates it (S6).
- **S4 — closed.**
  - **Rate limit.** It is per process, not per request:
    - the verifier is a singleton (`Program.cs:169`);
    - the hold-back is an instance field, updated by compare-and-swap
      (`PushSignatureVerifier.cs:45`, `:166-175`);
    - so the Warning comes at most once an hour per process.
  - **No value.** Both log calls take only `PushChannelOptions.PreviousSecretKey`
    (`:173`, `PushChannelStartupNotice.cs:17-19`). Neither message has a
    placeholder for a value.
  - **Tests.** `PreviousSecretAuditTests` assert both secrets absent.
- **S5 — closed.**
  - **The list.** `ProjectionBatch.cs:26` and `:57-60` list an id only when a
    deletion changed something. A user already deleted answers `Unchanged`
    (`ProjectedUser.cs:100-105`), so each id appears once.
  - **The lines.** `ApplyUserChangesHandler.cs:41-48` writes the count line
    and the ids line, ids only.
  - **Tests.** `DeletionAuditLogTests` assert no surname, e-mail or username.
  - The ids line prints the `id` field as pushed (Observations).

Contract rows against the code:
- **Part C `value_invalid`** (`api-contract.md:406`): matches
  `UserPushValidation.cs:183-187`.
- **B.2** (`:215-221`): matches `SignedPushBody.cs:42-46` and `:77-80`, and
  P1/P2.
- **B.3** (`:266-271`): matches, except the over-length case (S8).
- **B.6** (`:379-391`): matches `ApplyUserChangesHandler.cs:41-48`,
  `PushChannelStartupNotice.cs:17-19` and `PushSignatureVerifier.cs:166-175`.

### S6

Severity: major
Where:
- `docs/adr/0025-keycloak-user-projection.md:283-285`;
- `src/backend/Mustask.Api/Features/UserDirectory/PushSignatureVerifier.cs:65-68`.

Problem: ADR § 3 now says "A request that fails the first checks is refused
401 before a byte of its body is read, so an unauthenticated caller cannot
make the API buffer 4 MiB".
- **The first checks need no secret.** Any current Unix timestamp passes
  them, and so does `v1=` followed by 64 lowercase hex digits.
- **Such a request is still read.** Its body is read and buffered up to
  4 MiB, and copied once more by `ToArray` (`SignedPushBody.cs:102`), before
  the HMAC refuses it.
- **Probe P1:** no secret and a 3 MiB body drew a 401, and the server read
  3 145 755 of 3 145 755 bytes.
- **The same overclaim** is in the `EnsureEnvelopeValid` Javadoc: "a request
  that cannot be signed right costs nothing to refuse".
- **B.2 is accurate.** The contract, in the same diff, says a body that
  arrives with well-formed headers is read before its signature is checked.

Why major: 06-code-review.md, "relies on a guarantee that is not there". An
operator reading the ADR would expose the feed routes without an ingress body
limit or a rate limit. The cost itself stays at S3's minor.

Fix:
- Weaken both sentences. For example: "A request with a header missing or
  malformed, or a timestamp outside the window, is refused 401 before its
  body is read. Forming those headers needs no secret, so any caller can
  still have up to 4 MiB read before the signature refuses it: the limit
  bounds the cost, it does not authenticate."
- Optional, not required: return the `MemoryStream`'s own buffer instead of
  `ToArray()`, which drops the second copy.
- Anything stronger — a rate limit, an ingress limit — is the human's call.

Status: resolved (`300d45a`, `f198ee0`). ADR-0025 § 3 now reads as the Fix
words it: a request with a header missing or malformed, or a timestamp outside
the window, is refused 401 before its body is read; forming those headers needs
no secret, so any caller can still have up to 4 MiB read before the signature
refuses it — the limit bounds the cost, it does not authenticate. It adds that
the routes have no rate limit of their own and that one, or an ingress body
limit, is a deployment decision not made here (left to the human, as the
finding says). The XML doc comment of `EnsureEnvelopeValid` says the same, and
so does the comment in `SignedPushBody.BindAsync` at the call; plan § 7 carries
the sentence too. The optional copy is dropped: `ReadBoundedAsync` returns a
slice over the `MemoryStream`'s own array (`GetBuffer()`), not a `ToArray()`
copy; the array is taken before the stream is disposed, and disposing a
`MemoryStream` does not release it. Api UserDirectory tests: 310 passed, 15
opt-in live skipped (the envelope, signature and body-size tests included).

### S7

Severity: minor
Where: `docs/features/mod-014-keycloak-user-projection/plan-backend.md:198-200`, `:227-232`.

Problem: the plan still describes two behaviours this delta changed (found
by the direction-2 sweep):
- **Logging.** It says "users named only by id and only at Debug". S5 now
  names deletion ids at Information (`ApplyUserChangesHandler.cs:45-48`).
- **The binder.** It says `SignedPushBody<T>` "reads the raw body … verifies
  … then deserialises". S3 now checks the envelope first
  (`SignedPushBody.cs:42`).

Why minor: the contract and the ADR describe the new behaviour.

Fix: amend the two sentences.

Status: resolved (`300d45a`). Plan § 5's logging sentence names the deletion
ids at Information in a second line, any other user's id only at Debug; plan
§ 7's `SignedPushBody<T>` bullet gives the order the code has: envelope first,
then the bounded body, then the signature, then the deserialisation.

### S8

Severity: minor
Where:
- `docs/features/mod-014-keycloak-user-projection/api-contract.md:266-268`;
- `docs/adr/0025-keycloak-user-projection.md:351`;
- `deploy/keycloak/providers/user-projection/src/main/java/mustask/keycloak/userprojection/FeedSender.java:17`.

Problem: two narrow inaccuracies. Nothing anyone does would differ.
- **B.3** says U+0000 "is refused `value_invalid`". A text that is also over
  255 code points is `value_too_long` instead
  (`UserPushValidation.cs:179-182`; probe P5). The S2 status line says so;
  the contract does not.
- **ADR § 4 and the FeedSender Javadoc** name "an unpaired surrogate in a
  name". Account REST also takes one in the e-mail (live probe: 204). The fix
  itself covers every field.

Fix:
- B.3: "…`value_invalid` (`value_too_long` first when it is also over the
  limit)".
- ADR and Javadoc: "in a name or the e-mail".

Status: resolved (`300d45a`, `bc32287`). B.3 says `value_too_long` instead when
the text is also over the limit (the length is checked first, a field carries
one code), and Part C's `value_invalid` row says so; ADR-0025 § 4 and the
`FeedSender` Javadoc say "in a name or the e-mail". `PushNoncharacterTests`
asserts the order for a noncharacter (`ATextOver255CodePointsWithANoncharacter_IsTooLong_NotInvalid`);
`PushNulCharacterQaTests` already asserts it for U+0000.

### Prose pass

Direction 1, the claims the delta adds:
- **SignedPushBody class summary** (`SignedPushBody.cs:12-21`) → `:42-46`.
- **ADR § 3, envelope order** (`:280-283`) → `SignedPushBody.cs:42-46`. Its
  "cannot buffer" clause is S6.
- **ADR § 3, rotation audit** (`:320-325`) → `PushChannelStartupNotice.cs:17-19`,
  `PushSignatureVerifier.cs:59-62`, `:166-175`.
- **ADR § 4, cleaning and the 400 lead** (`:349-360`) →
  `ProjectedUserReader.java:26-68`, `ChangeCapture.java:33`,
  `KeycloakRealmSource.java:74`, `FeedSender.java:149-153`,
  `Reconciler.java:280-288`. "In a name" is S8.
- **Contract** B.2, B.3, B.6 and Part C → as listed above; S8.
- **Plan § 5, the `ProjectedUserReader` row** (`plan-backend.md:260`) →
  `ProjectedUserReader.java:26-68`.
- **Reconciler Javadoc** (`Reconciler.java:27-29`) → `:280-283`.

Direction 2, the old behaviour in files the diff does not touch:
- **Body read before the headers** — grepped `SignedPushBody`, `raw body`,
  `4 MiB`, `push_malformed`, `before it is parsed` over `docs/`, `spec.md`
  and the stories: `plan-backend.md:227-232` is stale (S7); nothing else.
- **"A 400 is a contract bug"** — grepped `contract bug`: only the updated
  lines.
- **B-1 ids at Debug only** — grepped `Debug`, `Logging`, `ids only`:
  - `plan-backend.md:198-200` is stale (S7);
  - `spec.md:140-146` NFR Logging ("names a user by Keycloak id only")
    holds;
  - US-4:118, US-5:26 and US-6:24 hold;
  - NFR Audit (`spec.md:147-149`) is not contradicted: a log line is not a
    history.
- **PreviousSecret silent** — grepped `PreviousSecret`: `plan-backend.md:22`
  and `:237`, `api-contract.md:186` and `:211`, `qa.md:129`. None stale.
- **U+0000 and surrogates** — grepped both: only the delta's own lines.
- **Not swept:** O-1 and O-4 (`Reconciler.deliveryRecovered`, ADR § 5). They
  are QA observations, not security, and are left to QA's re-check.

### What ran

All exit 0.
- **Provider unit tests:** `docker build --progress=plain --no-cache-filter provider --target provider deploy/keycloak`.
  - The `mvn verify` step re-ran, not cached, against Keycloak 26.4.7.
  - 133 tests, 0 failures, 0 errors, BUILD SUCCESS.
- **Build:** `dotnet build src/backend/tests/Mustask.Api.IntegrationTests`:
  0 warnings, 0 errors.
- **Api integration,** `dotnet test … --filter FullyQualifiedName~Features.UserDirectory`:
  - 196 total, 184 passed, 12 skipped (opt-in live);
  - without my 6 probes: 190 total, 178 passed, 12 skipped;
  - it includes `PushChannelSecurityProbeTests` 5, `PushNulCharacterTests`
    11, `PushEnvelopeBeforeBodyTests` 5, `PreviousSecretAuditTests` 6 and
    `DeletionAuditLogTests` 3.
- **Live,** `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1 dotnet test … --filter Category=KeycloakProvider`:
  13/13. That is the 11 of `LiveKeycloakProviderTests` (the new S1 test
  included), `LiveProviderLogProbeTests` and my live probe.
- **Not run:** `scripts/verify.sh`, the whole Api suite, the other test
  projects. No live stand was started. The Testcontainers containers were
  removed.

### Probing tests

Written, run, and deleted; none committed.
- **`ZzReviewerDeltaProbeTests`** (6, all passed):
  - **P1:** a forged envelope, no secret, a 3 MiB body → 401 after the server
    read 3 145 755 of 3 145 755 bytes (S6).
  - **P2:** a body over 4 MiB with a forged envelope, a wrong key and the
    right key → 400 `push_malformed` each. The 400 says nothing about the
    key.
  - **P3:** a small valid body with a forged envelope, a wrong key and no
    headers → 401 each. The problem documents are identical but for
    `traceId`, and nothing was stored.
  - **P4:** two users whose username, names and e-mail are identical after
    cleaning (`…�`), on one B-3 page and then in one B-1. The snapshot
    completed with received 2 and added 2; the B-1 answered changed 2; two
    rows.
  - **P5:** U+0000 in a 301-code-point first name → `value_too_long` (S8).
  - **P6:** a deletion whose `id` carries CR LF and a fake log prefix, with a
    name, e-mail and username.
    - The ids line splits, and `[00:00:00 INF] forged line.` appears as its
      own line in the Development console.
    - None of the name, e-mail or username appears.
- **`ZzLiveReviewerProbeTests`** (1, opt-in, passed):
  - Two users each put a lone surrogate (`\uD800`, `\uDBFF`) into their own
    profile over account REST:
    - first name, last name and e-mail → 204, 204, 204;
    - username → 400 `error-username-invalid-character`.
  - The changes pushes and the reconciliation page after an outage carry the
    cleaned values.
  - No received body has an escaped surrogate, no request was answered 400,
    and the reconciliation completed.

### Observations, not findings

- **The ids line prints the `id` field as pushed.**
  - Through the provider it is Keycloak's UUID. Only the holder of the
    secret can put anything else there, CR LF included (P6).
  - Production's console is JSON, which escapes it.
  - The secret holder is the one trusted principal and can already delete
    everyone. No change is asked.
- **Collisions after cleaning.** Keycloak e-mails that differ only by a bad
  code unit project to one string (live probe: both were accepted). That is
  harmless today. A future module that keys anything on the projected e-mail
  or username should know it.
- **S3's third bullet is unchanged.** Two WARN lines per refusal and no rate
  limit: it was not in the prescribed fix.

## Security pass — backend, delta 2 (and D-5)

Reviewer: code-reviewer, clean context, author of nothing on this branch,
2026-10-04. Scope: the fix commits over `319f169` — `bc32287` (provider,
noncharacters), `3570878` (MUSTASK validation, total normaliser, A.3),
`300d45a` (docs), `f198ee0` (S6), `b50cf7c`/`b74c376`/`cd28f35`
(resolution notes, Log) — checked against S6–S8 (delta 1) and QA's D-5
(qa.md, re-check 3), plus a prose pass over what they change. The QA commits
in `319f169...HEAD` (`de0e4b7`..`f9c1a1f`) are read as context only. Anchors
are taken at `cd28f35`. This is the single delta the loop allows; no further
round is started from here.

**Verdict: `needs-changes`.** The code is right: S6's buffer change, D-5's
rule, the total normaliser and the provider's replacement all hold, and every
test, probe and live run is green. S7 and S8 are closed. 0 blocker,
2 major (S9, S10), 1 minor (S11). All three are prose. S9 and S10 are major
only by 06-code-review.md's rule that a sentence the same diff disproves is
major regardless. Each one is a one-sentence edit. No code change is required.
(Delta 1 graded S8, a same-diff case of the same kind, minor. The rule's text
is followed here; the human may weigh that precedent at gate-3.)

### S6–S8 and D-5

- **S6 — closed.**
  - **Prose.** ADR-0025 § 3 (`:283-289`) no longer promises that an
    unauthenticated caller cannot have 4 MiB read. Anchors:
    - the envelope runs before the read: `SignedPushBody.cs:44` before `:46`;
    - the envelope check consults no secret: `PushSignatureVerifier.cs:131-154`;
    - the body is read in full and only then refused:
      `SignedPushBody.cs:46-48`, `PushBodyReader` `:69`, `:79-99`;
      `PushEnvelopeQaTests.AWellFormedEnvelope_WithAWrongSignature_IsReadAndThenRefused_401`
      counts the whole body;
    - "no rate limit of their own": there is no `RateLimiter` or
      `AddRateLimiter` anywhere in `src/backend` outside the tests.
  - **The same sentence** is in the `EnsureEnvelopeValid` XML doc
    (`PushSignatureVerifier.cs:65-73`) and the call-site comment
    (`SignedPushBody.cs:40-43`).
  - **The code change** (`SignedPushBody.cs:84`, `:104-105`) is correct:
    - The `GetBuffer()` slice is taken when the return expression is
      evaluated, which is before the `using` disposes the stream.
    - `MemoryStream.Dispose` clears only its flags and never nulls `_buffer`:
      the runtime keeps it on purpose so `GetBuffer`/`ToArray` still work.
    - The parameterless constructor makes the buffer exposable, so
      `GetBuffer` does not throw.
    - The array is a plain allocation, not pooled, so it cannot be reused
      under the slice.
    - The slice is `[0, Length)`. Every signed test hashes exactly that
      range; `TheBodyBound_IsExactly4MiB_ForASignedRequest` signs 4 194 304
      bytes and gets 200.
    - An empty body gives an empty slice and still the 400.
  - **Nothing keeps the buffer after the request.**
    - `PushRequestParts` is passed to the verifier and never stored: `Check`
      hashes the span, `Refuse` logs the method and the route, and
      `NoteAcceptedUnderPreviousSecret` keeps ticks only.
    - `JsonSerializer.Deserialize` copies out what it reads.
    - `JsonException`/`ValidationException` carry no reference to the
      buffer.
    - `SignedPushBody<T>` holds only `Value`.
- **S7 — closed.**
  - Plan § 5 (`plan-backend.md:198-202`) → `ApplyUserChangesHandler.cs:41-53`.
    The ids of deletions are at Information (`:45-48`), the other ids at
    Debug (`:50-53`).
  - Plan § 7 (`:229-240`) → `SignedPushBody.cs:38-61`. The order is: the
    verifier resolved (`:38`), the envelope (`:44`), the bounded read
    (`:46`), the signature (`:48`), then deserialisation (`:52-59`).
- **S8 — closed.**
  - B.3 (`api-contract.md:272-274`) and Part C (`:414`) →
    `UserPushValidation.cs:179-190`. The length is checked first, and each
    branch writes a single `FieldError`, so a field carries one code.
  - ADR § 4 (`:354-355`) and `FeedSender.java:17` say "in a name or the e-mail".
  - Tests: `PushNoncharacterTests.ATextOver255CodePointsWithANoncharacter_IsTooLong_NotInvalid`
    and, for U+0000, `PushNulCharacterQaTests` (RC3-15).
- **D-5 — closed in code.**
  - **The set.** Both sides use the same mask:
    - .NET: `DirectorySearchText.cs:124-125`;
    - Java: `ProjectedUserReader.java:85-87`;
    - the mask: `(cp >= 0xFDD0 && cp <= 0xFDEF) || (cp & 0xFFFE) == 0xFFFE`.
    - That is 32 + 17 × 2 = 66 values, which is the standard's set. Each side
      has a test that writes the set out from the definition:
      `DirectorySearchTextTests.IsNoncharacter_IsExactlyTheStandardsSixtySixValues`
      and `ProjectedRealmAndReaderTest.everyCodePoint_isCleanedAsDocumented`
      (it counts 66).
  - **The order.** `UserPushValidation.cs:179-182` checks the length first,
    then `:183-190` (`HoldsAnUnacceptableCharacter`, `:193-204`, U+0000 or a
    noncharacter, by runes) gives `value_invalid`. This applies to all five
    text fields (`:64-68`) on B-1 (`ApplyUserChangesHandler.cs:28`) and B-3
    (`ApplySnapshotPageHandler.cs:33`), a deletion entry included.
  - **`Normalize` is total.** Every step but one is total:
    - `EnumerateRunes` turns an ill-formed surrogate into U+FFFD;
    - a noncharacter becomes U+FFFD (`:88-91`);
    - controls are dropped;
    - `ToLowerInvariant` and `Replace` do not throw.

    The one step that can throw is the composition (`:101-108`). On the ICU
    path, .NET's NFC throws `ArgumentException` only on U+FFFE or an
    ill-formed surrogate, and both are gone before composition. The fallback
    catches exactly `ArgumentException`, which also covers the NLS path's
    "invalid Unicode". Any other exception is not swallowed
    (`Normalize_SwallowsNothingButTheCompositionStepsArgumentException`).
    No input reaches the fallback on this runtime; it is defence in depth
    (`Normalize_WhenTheCompositionStepThrows_GoesOnWithoutIt`). Null throws
    `ArgumentNullException`, as before; no caller passes null. The API
    refuses an invariant-globalization runtime at start
    (`DirectoryCollation.EnsureSupported`), so a
    `PlatformNotSupportedException` from NFC cannot arise either.
  - **The search path matches A.3.**
    - `Parse` (`DirectorySearchText.cs:43-63`) counts code points before
      normalising, so a noncharacter is one character (100 accepted, 101
      refused).
    - It then normalises, so the noncharacter becomes U+FFFD and matches a
      stored U+FFFD.
    - My probe below confirms this, as do `Parse_ATextWithU_FFFE_IsAPatternWithTheReplacementCharacter`,
      `Parse_TwoNoncharacters_AreTwoCharacters_NotTooShort`,
      `PushNoncharacterTests.ASearchText_WithU_FFFE_IsAnswered_AndFindsAUserWhoseNameTheProviderCleaned`
      and `ProjectedUserTotalityQaTests.EveryScalarValue_AsASearchText_IsParsedWithoutThrowing`.
  - **The provider.** `ProjectedUserReader.read` (`:28-36`) runs `wellFormed`
    (`:48-78`) on the id and all four strings.
    - A noncharacter pair becomes one U+FFFD (`:61-62`), a BMP noncharacter
      U+FFFD (`:70-71`).
    - Lone surrogates and U+0000 are handled as before, and a valid pair is
      kept.
    - Paths: event and removal through `ChangeCapture.java:33`
      (`UserRemovalListener.java:41` calls `capture(…, true)`); reconciliation
      through `KeycloakRealmSource.java:74`. Nothing else builds a
      `UserState`.
  - **Tests, by item:**
    - **B-1/B-3 refusals:** `PushNoncharacterTests`, 30 cases.
      - `Changes_WithU_FFFEInATextField_…` ×5 (B-1);
      - `SnapshotPage_WithANoncharacterInATextField_…` ×5 (B-3);
      - `EveryKindOfNoncharacter_IsRefusedInAName` ×8 (the edges);
      - `TheNeighboursOfANoncharacter_AreStoredAsSent` ×8;
      - `ADeletion_CarryingANoncharacter_…`;
      - `…IsTooLong_NotInvalid`;
      - `…NameThatEntryOnly_AndDoNotEchoIt`;
      - the search case.
      - QA's theory `PushNulCharacterQaTests.TheNoncharacterU_FFFE_IsStoredOrRefused_NeverAServerError`
        (×3) is un-skipped.
    - **Totality:**
      - `DirectorySearchTextTests.Normalize_IsTotal_OverEveryScalarValue`,
        `…ReadsANoncharacterAsTheReplacementCharacter` ×6,
        `…TakesALoneSurrogate_…`;
      - `ProjectedUserTotalityQaTests` ×2 (every scalar value, un-skipped);
      - `ProjectedUserTests.Create_AndApply_WithANoncharacterInTheNameOrTheEmail_DeriveWithTheReplacementCharacter`.
    - **Provider:**
      - `ProjectedRealmAndReaderTest.everyProjectedString_isMadeWellFormed_inEachOfTheFourFields`
        (8 new noncharacter rows), `theId_isMadeWellFormedToo`,
        `aValidCharacterNextToTheNoncharacters_isKept_inEachOfTheFourFields`
        ×9, `everyCodePoint_isCleanedAsDocumented`,
        `theBodyOfAUserWithNoncharacters_carriesOnlyTheReplacement`;
      - `ProjectedTextQaTest`'s oracle and reader tests, over an alphabet
        that now holds six noncharacters and three of their neighbours;
      - `theEventTheRemovalAndThePage_carryTheSameCleanText_inJsonMustaskCanRead`
        drives all three paths over that alphabet. It asserts event = page
        and a well-formed wire, not the absence of noncharacters on those
        paths (Observations).
    - **Live:**
      - `LiveProviderQaTests.AUser_CanPutTheNoncharacterU_FFFE_InTheirOwnName_AndKeycloakAcceptsIt`
        (account REST 204, pushed as `Сам�И`);
      - `ANoncharacterTheValidationAccepts_InAName_IsPushedInAFormMustaskCanDerive`
        (admin REST, un-skipped).
  - **Two prose sentences of the fix are wrong** (S9, S10).

### S9

Severity: major
Where: `src/backend/Mustask.Domain/UserDirectory/UserPushValidation.cs:185-188`

Problem: the comment that justifies the new rule says: "A noncharacter: it
has no place in a name, and .NET's normaliser throws on U+FFFE, so left to the
derivation it is a 500 too (D-5)."
- **The same commit makes that false.** `DirectorySearchText.cs:66-76` says
  `Normalize` "is total — no text makes it throw, so no text is ever a 500 on
  the push channel or in a search". `:88-91` and `:101-108` make it so.
- **The developer's own run disproves it** (`status.md:46`): with the
  validation rule alone taken out, "the fix-agnostic theory passes … (stored
  is allowed once the normaliser is total)".
- **Behaviour is unaffected.** The rule is still needed, because B.3 refuses
  the character. A reader of the comment, though, takes the validation for
  the only thing standing between U+FFFE and a 500. That is the old state.

Why major: 06-code-review.md, "Severity" — the diff carries its own
disproof.

Fix: reword the noncharacter half, e.g. "A noncharacter: no name or e-mail
needs one, and B.3 keeps the projection free of them, so a text copied from
Keycloak meets the provider's U+FFFD. Before D-5 it was also a 500: .NET's
normaliser throws on U+FFFE, which `DirectorySearchText.Normalize` now
replaces first." Keep the U+0000 half; it is still true (S2).

### S10

Severity: major
Where:
- `docs/features/mod-014-keycloak-user-projection/plan-backend.md:266`;
- also read: `docs/adr/0025-keycloak-user-projection.md:365-367`.

Problem: the `ProjectedUserReader` row now ends "so it never sends what
api-contract.md B.3 refuses". The reader covers B.3's character rule only.
- **QA's O-2 shows the gap** (`qa.md:637-639`, on this branch): an id or a
  username that is only U+0000 is cleaned to `""` and pushed, and B.3 refuses
  it with `value_required` (`api-contract.md:257`, `:259`;
  `UserPushValidation.cs:163-166`). The 400 drops the whole batch.
- **Reach.** O-2 is reachable only through a username Keycloak's own
  validator refuses, i.e. an unvalidated import. The behaviour stays the
  minor QA recorded; the "never" is what is wrong.
- **The ADR's wording.** "holds for the whole of B.3's text rule" is true if
  "text rule" means the paragraph on characters (`api-contract.md:266-279`).
  Read as B.3's rules for text fields, it is the same overclaim.

Why major: the same rule as S9 — `qa.md` O-2 on the branch disproves an
absolute the fix diff added.

Fix: one of:
- Narrow the sentences: "so it never sends a character B.3 refuses (an id or
  username that is only U+0000 is sent empty and refused `value_required` —
  QA O-2)", and in the ADR "B.3's character rule".
- Make the claim true, as O-2 suggests: the provider skips a user whose id
  or username cleans to empty, with a log line naming the id, plus a test.

### S11

Severity: minor
Where:
- `docs/features/mod-014-keycloak-user-projection/plan-backend.md:130-131`;
- `deploy/keycloak/providers/user-projection/src/main/java/mustask/keycloak/userprojection/FeedSender.java:16-17`
  and its drop line `:152`.

Problem: two descriptions of the changed behaviour are behind the code.
- **The plan's `Normalize`** (direction 2, untouched by the diff) lists "NFC,
  `ToLowerInvariant`, `ё` → `е`, control characters removed". It omits the
  noncharacter → U+FFFD step and the uncomposed fallback (`DirectorySearchText.cs:88-91`,
  `:101-108`). The contract's A.3 row has the full list.
- **The `FeedSender` Javadoc** files a noncharacter under "input … MUSTASK
  cannot parse". MUSTASK parses it and refuses it at validation
  (`value_invalid`); the ADR (`:353-354`) says "cannot parse or refuses". The
  drop line `:152` says "refused … as malformed … input it cannot parse" for
  every 400, though it does print the error codes.

Nothing anyone does would differ.

Fix: add the two steps to the plan bullet; in the Javadoc write "cannot parse
or refuses". The log line is optional.

### Prose pass

Direction 1, the claims the fix diff adds or changes:
- **ADR § 3, envelope then body** (`:283-285`) → `SignedPushBody.cs:44-46`,
  `PushSignatureVerifier.cs:131-154`; `PushEnvelopeQaTests.ABadEnvelope_WithABodyOverTheLimit_…`
  (0 bytes read).
- **ADR § 3, "any caller can still have up to 4 MiB read … does not
  authenticate"** (`:285-287`) → `PushSignatureVerifier.cs:131-154` (no
  secret), `SignedPushBody.cs:46-48`, `:79-99`.
- **ADR § 3, "no rate limit of their own"** (`:287-289`) → no rate limiter
  in `src/backend` outside the tests.
- **ADR § 4, a 400 as input Keycloak accepted** (`:352-357`) →
  `UserPushValidation.cs:183-190`. For "account REST takes it from the
  user": `LiveProviderQaTests.AUser_CanPutTheNoncharacterU_FFFE_…` covers the
  first name only. The e-mail rests on admin REST (201, qa.md D-5) and on the
  lone-surrogate account-REST probe of delta 1 — accepted as the reach
  argument it is.
- **ADR § 4, the provider's cleaning** (`:358-363`) →
  `ProjectedUserReader.java:28-36`, `:48-78`, `:85-87`;
  `ChangeCapture.java:33`; `KeycloakRealmSource.java:74`.
- **ADR § 4, "MUSTASK refuses what the provider replaces … its own
  derivation is total, so no text that reaches it is a 500"** (`:363-366`) →
  `UserPushValidation.cs:179-204`, `DirectorySearchText.cs:79-113`,
  `ProjectedUser.cs:222`, `:234`; `ProjectedUserTotalityQaTests` (every
  scalar value).
- **ADR § 4, "holds for the whole of B.3's text rule"** (`:366-367`) → read
  as the character paragraph it holds; otherwise S10.
- **ADR § 4, the search text reads a noncharacter as U+FFFD**
  (`:367-369`) → `DirectorySearchText.cs:55`, `:88-91`.
- **Contract A.3 Matching** (`api-contract.md:94`) →
  `DirectorySearchText.cs:43-63`, `:79-113`;
  `PushNoncharacterTests.ASearchText_WithU_FFFE_…`; my probe (100/101).
  "The projection holds none" → `UserPushValidation.cs:183-190`.
- **Contract B.3 character paragraph** (`:266-279`) →
  `UserPushValidation.cs:64-68`, `:179-204`; `DirectorySearchText.cs:124-125`;
  `ProjectedUserReader.java:48-78`.
  - "Sixty-six" → the two definitional tests.
  - "`value_too_long` first, one code" → `:179-190`.
  - "never sends any of them" → `everyCodePoint_isCleanedAsDocumented`.
  - "(the rule …)" as a general rule → S10.
- **Contract Part C `value_invalid`** (`:414`) → `UserPushValidation.cs:183-190`.
- **Plan § 5 logging** (`:198-202`) → `ApplyUserChangesHandler.cs:41-53`.
- **Plan § 7 binder** (`:229-240`) → `SignedPushBody.cs:38-61`; "bounded,
  not authenticated" → as in ADR § 3.
- **Plan § 8 `ProjectedUserReader` row** (`:266`) → the cleaning is
  anchored as in ADR § 4; "never sends what B.3 refuses" is S10.
- **Code comments:**
  - anchored:
    - `SignedPushBody.cs:40-43` → `:44-48`;
    - `:104` → the `MemoryStream.Dispose` semantics above;
    - `PushSignatureVerifier.cs:65-73` → `:131-154`;
    - `DirectorySearchText.cs:5-12`, `:66-76`, `:115-123` → the code beside
      them. "U+FFFE is the one value .NET's NFC throws on" rests on QA's
      exhaustive run (qa.md:560) and is hedged at `:73-74`.
    - `ProjectedUserReader.java:9-13`, `:38-47`, `:80-84` → `:28-36`,
      `:48-87`.
    - `Mustask.Domain.csproj` "nothing else is shared" → the only `internal`
      member of `Mustask.Domain` is `DirectorySearchText.cs:79`.
  - findings:
    - `UserPushValidation.cs:185-188` → S9;
    - `FeedSender.java:16-17` → S11.
- **qa.md D-5 `Status: resolved`** (`:607-627`) → every clause anchored
  above. "3 cases: 400 `value_invalid` now" →
  `PushNoncharacterTests.Changes_WithU_FFFEInATextField_…`; the theory
  itself accepts 200 or 400.
- **review.md S6/S7/S8 resolution notes** → as in "S6–S8 and D-5" above.
- **`[dev]` Log line** (`status.md:46`), its counts:
  - Rerun here and equal:
    - Domain 138;
    - provider 158/158, uncached;
    - live `KeycloakProvider` 15 in the Api tests;
    - Api `UserDirectory` 310 + 15 skipped (the S6 note).
  - Consistent by arithmetic: Api 656 + 15 = QA's 624 + 16, plus the 30
    `PushNoncharacterTests`, plus the 3 un-skipped theory cases, minus the
    U+FFFF case, with one skip fewer.
  - The mutations, recomputed from the tests:
    - "20 of 73": 73 = `PushNoncharacterTests` 30 + `PushNulCharacterQaTests`
      43, counted from the TRX. Without the rule, 5 + 5 + 8 + 1 + 1 of the 30
      fail.
    - "13 of 158": 8 noncharacter rows, the id, the every-code-point test and
      the body test make 11 in `ProjectedRealmAndReaderTest`, plus QA's
      oracle and reader tests.
  - Not rerun here: Contracts 114/17, Architecture 81, Application 51,
    Infrastructure 89, Shared 53.

Direction 2, behaviour the diff changes, prose outside it:
- **Noncharacters refused / read as U+FFFD.** Grepped `U+FFFE`, `U+FFFF`,
  `noncharacter`, `value_invalid`, `U+0000`, `control characters`, `NFC`,
  `normalis`/`normaliz`, `DirectorySearchText`, `search_name`,
  `IsNoncharacter` over `docs/`, `.claude/`, `contracts/`,
  `src/backend/AGENTS.md`, the spec and the stories.
  - `plan-backend.md:130-131` is stale (S11).
  - `spec.md:109` BR-UP-14 ("ignoring letter case, `ё`/`Ё` equal to
    `е`/`Е`") holds.
  - `contracts/errors/user_directory.json:26-32` and
    `docs/error-codes.md:56` carry no case list: no change needed.
  - qa.md RC3-14 and RC3-39 still list U+FFFF as stored. They are QA's
    record, and the D-5 status line says so.
  - The mod-016 and mod-003 docs: no hits.
- **The `ToArray` copy dropped.** Grepped `ToArray`, `GetBuffer`,
  `second copy`, `own buffer`, `8 MiB`: only review.md S3's own history and
  unrelated hits (ADR `:167`, US-6 `:98`). None stale.
- **A 400 that the provider now prevents.** Grepped `cannot parse`,
  `malformed`, `contract bug`: `FeedSender.java:152` (S11). Nothing else.
- **Not swept:** the provider's other log lines and the frontend (the
  frontend stage has not started).

### What ran

- **Build:** `dotnet build Mustask.sln`: exit 0, 0 errors. The 2 MSB3073
  warnings are `git config core.hooksPath` exiting 255 inside this agent
  worktree, from `Directory.Build.props`; they are unrelated to the diff.
- **Domain:** `dotnet test src/backend/tests/Mustask.Domain.Tests`: 138
  passed, 0 failed, 0 skipped.
- **Api integration,** `dotnet test … Mustask.Api.IntegrationTests --filter FullyQualifiedName~Features.UserDirectory`:
  325 total, 310 passed, 15 skipped (opt-in live), 0 failed.
- **Provider, uncached:** `docker build --progress=plain --no-cache-filter provider --target provider deploy/keycloak`.
  - First attempt: `Tests run: 158, Failures: 0`, then Maven failed to
    resolve the jar plugin's dependencies from the network ("Failed to read
    artifact descriptor"): exit 1, environmental.
  - Plain `docker build --target provider deploy/keycloak`: exit 0, but the
    layer was cached.
  - Uncached again: exit 0, Keycloak 26.4.7, `Tests run: 158, Failures: 0,
    Errors: 0, Skipped: 0`, BUILD SUCCESS (`ProjectedRealmAndReaderTest` 36,
    `ProjectedTextQaTest` 6).
- **Live, once:** `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1 dotnet test … --filter Category=KeycloakProvider`:
  15 passed, 0 failed, 0 skipped, exit 0.
- **Not run:** `scripts/verify.sh`, the rest of the Api suite, Contracts,
  Architecture, Application, Infrastructure, Shared. No live stand was
  started. The Testcontainers containers of these runs were removed by Ryuk.

### Probing tests

Written, run, and deleted. None was committed; `git status --short` is empty.
- **`Mustask.Domain.Tests/UserDirectory/ZzReviewerDelta2ProbeTests`,** 22
  passed:
  - **Surrogate-adjacent noncharacters through `Normalize`**, built at run
    time. All read as expected:
    - `\uD800￾`, `￾\uDC00`, `\uDC00\uD800￿`: one U+FFFD for
      each unpaired half and each noncharacter;
    - U+1FFFE and U+10FFFF as pairs: one U+FFFD;
    - `\uD800`+U+10FFFF and U+10FFFF+`\uDC00`: two U+FFFD;
    - `е`+U+FFFE+U+0308: `е`+U+FFFD+U+0308, the replacement blocking
      composition;
    - `Е`+U+0308+U+FFFE: `е`+U+FFFD.
    - Byproduct: a first try with `[InlineData]` failed 4 cases because a
      lone surrogate in an attribute argument does not survive the
      compiler's UTF-8 metadata (it arrives as three U+FFFD). That is a
      harness artefact; no committed test uses that form.
  - **`Parse` boundaries:** 99 letters + U+10FFFF (100 code points, 101
    UTF-16 units) is a pattern ending `U+FFFD%`; one more letter is
    `TooLong`.
  - **`UserPushValidation`**, in each of the five fields, with U+FFFE,
    U+FDEF, U+10FFFF and U+1FFFE as the 255th code point:
    - `value_invalid` on B-1 and B-3, the only error;
    - with one more letter, `value_too_long`;
    - the same 255 with U+10FFFD instead pass.
- **`Mustask.Api.IntegrationTests/Features/UserDirectory/ZzReviewerDelta2ProbeTests`,**
  12 passed:
  - U+10FFFF as the 255th code point, raw UTF-8 and as the escape pair
    `􏿿`, in each of the five fields on B-1: 400 `value_invalid`
    at the field's path, 0 rows, never a 500.
  - On a B-3 page: 255 with U+FFFE gives `value_invalid`, 256 gives
    `value_too_long`, 0 rows.
  - Through the read model: a 100-code-point search text ending in U+10FFFF
    is answered; 101 is refused.

### Observations, not findings

- **Retained size.** The slice keeps the stream's whole array: about 5 MiB
  for a 4 MiB body read in 80 KiB chunks, under 8 MiB at worst through
  doubling. Before, the stream's array plus a 4 MiB copy were both
  allocated, so the peak per request is lower now.
- **One chunk past the bound.** `ReadBoundedAsync` reads one chunk (at most
  80 KiB) past 4 MiB before it refuses. "Up to 4 MiB read" is that, give or
  take a chunk.
- **The uncomposed fallback.** If it ever fired, it would apply per text, so
  a stored name and a search text could be composed differently. No input
  reaches it on this runtime.
- **A stronger per-path test.** `ProjectedTextQaTest.assertWireIsWellFormed`
  could also assert that no noncharacter appears on the event, removal and
  page paths. Today that rests on those paths all calling `read`, which
  S1's per-path tests pin.
- **For the orchestrator:**
  - S9 and S10 go back to the developer as text edits. S10 may be fixed in
    code instead (O-2).
  - No code QA re-check is needed for the prose fixes. If S10 is fixed in
    code, the provider's unit tests cover it.

Status of S9, S10, S11: resolved (`b5b924c`) by the orchestrator at gate-3 — three sentences (the `UserPushValidation` comment, plan § 5's reader row and ADR § 4 narrowed to B.3's character rule citing QA O-2, the plan's `Normalize` bullet and the `FeedSender` wording). The delta read of these sentences is the orchestrator's, recorded in status.md (D-17); no further review session.
