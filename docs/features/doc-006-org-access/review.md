# Requirements review — doc-006-org-access

## Round 1 — requirements

Input reviewed: `spec.md` (289 lines) and `stories/US-1..US-7`, against the
umbrella `doc-001-domain-map` (spec, D-1..D-12, US-1/US-2), its
`research/ado-boards.md` §§ Organization / project, Team settings, Security,
`docs/adr/0018-keycloak-as-identity-provider.md`,
`src/backend/Mustask.Domain/Identity/PermissionCatalog.cs` and the realm import
`deploy/keycloak/realms/mustask-realm.json`.

Verdict: **needs-human-decision** — 3 blockers, 8 majors, 8 minors (5 fixed by
the reviewer in place). The human-only questions are collected at the end.

### Rule → story mapping (checklist 1)

| Rule | Covered by | Note |
|---|---|---|
| BR-3 (area-owned team) | US-6, US-7 | degenerate in MVP — R-6 |
| BR-5 (codes from the membership) | US-4, US-6 | collides with the live realm — R-1 |
| BR-6 (soft delete) | US-2, US-5 | purge path undefined — R-10 |
| BR-13 (assignee candidates) | US-3, US-6 | referenced only, owned by M3 |
| BR-M1-1 (name uniqueness) | US-2, US-5 | leaks existence — R-2; length — R-13 |
| BR-M1-2 (instance right ≠ data access) | US-1, US-4 | |
| BR-M1-3 (process seeded by M2) | US-2, US-5 | no failure path — R-7 |
| BR-M1-4 (last administrator) | US-3 | no concurrency rule — R-8 |
| BR-M1-5 (one membership per org) | US-3 | |
| BR-M1-6 (no re-login) | US-3, US-4 | instance codes differ — R-14 |
| BR-M1-7 (the owning org decides) | US-4 | |
| BR-M1-8 (not found, never forbidden) | US-1, US-4 | broken by US-2 — R-2 |
| BR-M1-9 (one team per project) | US-5, US-6, US-7 | Q-3 — R-5 |
| BR-M1-10 (team ⊆ org membership) | US-3, US-6 | |
| BR-M1-11 (root area) | US-5, US-7 | rename/re-own of the root — R-6 |
| BR-M1-12 (area tree) | US-7 | no concurrency rule — R-8 |
| BR-M1-13 (delete an empty leaf) | US-7 | |
| BR-M1-14 (default area) | US-7 | M3 behaviour — R-19 |
| BR-M1-15 (audit) | **no story** | R-3 |

### R-1
Severity: blocker
Affects: spec § Roles, BR-M1-6, BR-M1-7, US-4
Problem: the spec asserts "No realm role named `admin`/`member`/`viewer`
exists or is checked". In the repository as it stands those three realm roles
do exist — `deploy/keycloak/realms/mustask-realm.json` declares `admin`,
`member`, `viewer` (plus `dictionaries-admin`) and maps them onto the groups
«Администраторы» / «Сотрудники» / «Участники». Worse, ADR-0018 fixes a
contract test that "the `mustask-api` client's roles equal
`PermissionCatalog.AllCodes`" and that "the `admin` role holds every
permission". The spec's Roles table introduces thirteen `organizations.*`
codes without saying whether they enter `PermissionCatalog` and the realm
import at all. Both answers hurt and neither is written down: if they do
enter, the existing contract test grants every org-scoped code to every member
of the group «Администраторы» through their token — exactly the situation
US-4's scenario "No Keycloak realm role named admin, member or viewer is
consulted" declares impossible; if they do not, the contract test and the
catalog have to be split into instance-level and organization-scoped codes,
which is a change to M0's working access model that nobody has been asked to
make. `mod-002-organizations-projects-teams` cannot design the ADR-0018
amendment without this being decided, and a wrong guess is an over-granted
endpoint.
Required resolution: in § Roles, state as a rule (a) whether organization-
scoped codes live in `PermissionCatalog` / the realm import or in the
membership only, (b) what happens to the existing realm roles `admin`,
`member`, `viewer` and to the groups that carry them — removed from the import,
or kept for M0 and never consulted for org-scoped resources, (c) that
`dictionaries.languages.*` and M0's access keep working unchanged. Name this
as the constraint the ADR-0018 amendment owed by
`mod-002-organizations-projects-teams` must satisfy.
Status: open
Resolution: spec § Roles, new block "Constraints the ADR-0018 amendment must
satisfy" (owed by `mod-002-organizations-projects-teams`): (1) organization-
scoped codes are never in the realm import, never on a Keycloak role/group,
never read from the token; catalog placement is the amendment's choice, but
the realm-import contract test compares against instance-level codes only;
(2) realm roles `admin`/`member`/`viewer` leave the import (D-9) — `admin`'s
"every instance-level code" bundle passes to the instance administrator role,
every holder of `dictionaries.languages.read` keeps it, the three groups may be
kept/renamed/removed under that condition; (3) the "admin holds every
permission" contract test is restated for the instance administrator role and
instance-level codes; (4) M0 unchanged. US-4's realm-role scenario replaced by
three: a token-carried role grants nothing org-scoped, the realm-import
contract check, M0 unchanged.

### R-2
Severity: blocker
Affects: BR-M1-1, BR-M1-2, BR-M1-8, US-2, US-5
Problem: BR-M1-8 is stated as a hard confidentiality rule — "a user with no
membership is not told an organization exists ... never 'forbidden'" — and
US-1/US-4 test it. US-2 then breaks it: creating an organization whose name is
taken "is refused with a message naming the conflict", which tells any holder
of the instance-level right that an organization with that name exists,
including one that was deactivated (BR-M1-1 keeps the name reserved) and one
they may not see. BR-M1-2 makes this unavoidable rather than incidental:
holding "create organizations" grants no data access, so there is no list
against which a user could have checked the name, and no way to resolve the
conflict except by guessing another name. Azure DevOps accepts this leak
(organization names are public host names — research § Organization /
project), but MUSTASK has not decided that it does, and the two rules as
written cannot both hold.
Required resolution: decide and write down the confidentiality boundary of the
organization name: either (a) organization names are public within the
instance — say so in BR-M1-1, carve the exception into BR-M1-8 explicitly and
fix the product copy of the refusal, or (b) the refusal must not disclose an
existing organization — then state what the user sees instead and how they
ever succeed. Whichever is chosen, US-2's refusal step gets the exact Russian
string. The same choice does not apply to projects: there the conflict is
inside an organization the acting user already administers, so the project
message may name it (fixed under R-16).
Status: open
Resolution: option (a) per H-2. BR-M1-1 states organization names are public
within the instance and a refusal says nothing beyond "taken"; BR-M1-8 narrowed
to an organization's content (projects, members, teams, areas, work items),
the name's existence explicitly excluded. US-2's refusal is «Название «Пример
организации» уже занято», identical for an active or deactivated holder; the
rename-to-taken case uses the same message.

### R-3
Severity: blocker
Affects: BR-M1-15, § MVP boundary, § Decomposition, NFR "Audit"
Problem: BR-M1-15 requires every membership, role and lifecycle change to be
recorded and readable by the organization's administrators, and the
decomposition proposes a queue line `feat-NNN-org-admin-audit-log` for it —
but no story covers it. There is not one Gherkin criterion about what is
recorded, what an administrator sees, where, or for how long. It is also
unplaced: the § MVP boundary paragraph does not list the audit among the MVP
items and § Release 2 does not list it either, while the decomposition queues
it as if it were MVP. And doc-001 already owes an audit/history ADR to
`mod-work-item-core`; a second, independently designed audit trail for M1
built first, by another task, is how two incompatible audit mechanisms get
into one system.
Required resolution: pick one — (a) audit is in MVP: add a story with
criteria (which events, which attributes, which screen, who reads it) and list
it in § MVP boundary, and say explicitly how it relates to the audit mechanism
`mod-work-item-core` owes; or (b) audit is release 2: move BR-M1-15 and the
NFR bullet into § Release 2, drop the queue line from the decomposition, and
keep in MVP only the plain "created/changed by and when" attributes if they
are wanted.
Status: open
Resolution: option (b) per H-3. BR-M1-15 removed from the rule table and
stated in § MVP boundary → Release 2, built on the audit/history ADR owed by
`mod-003-work-item-core`, not a second mechanism. New BR-M1-16 keeps only
who added a membership and who last changed its role, with dates; US-3 has a
scenario for it (labels «Добавлен», «Роль изменена»). NFR "Audit" rewritten;
`feat-NNN-org-admin-audit-log` removed from the decomposition.

### R-4
Severity: major
Affects: Open question Q-2, US-1, § Release 2
Problem: Q-2 asks the human where the "create organizations" right is granted
— in Keycloak or on a MUSTASK screen. That is already answered by an accepted
ADR: ADR-0018 § Consequences states that "the 'change password', 'reset
password', **'grant permission'** scenarios stop existing in our UI and move
to the Keycloak console; user, role and password-recovery screens ... lose
their subject". A MUSTASK screen for granting a permission code is therefore
not an open option at gate-1 — it would need an ADR amendment of its own.
Putting it to the human as an open question invites reopening an accepted
decision by accident.
Required resolution: delete Q-2 from § Open questions and record the answer as
a rule in § Roles or BR-M1-2, citing ADR-0018 § Consequences: the instance-
level right is granted in the Keycloak console, MUSTASK has no screen for it.
Keep the release-2 line about a possible instance-administration screen and
mark it as requiring an ADR-0018 amendment.
Status: open
Resolution: Q-2 deleted. BR-M1-2 now states the right is granted and revoked
in the Keycloak console through ADR-0018's permission → role → group chain,
MUSTASK has no screen for it (ADR-0018 § Consequences); the Roles table and
Operation completeness row say the same; US-1 lost its Q-2 reference. The
release-2 line reads "a MUSTASK screen for instance-level rights, which needs
an ADR-0018 amendment of its own".

### R-5
Severity: major
Affects: Open question Q-3, BR-M1-9, US-6, US-7
Problem: Q-3 claims doc-001 contradicts itself — "its Operation completeness
row lists `Team: create — org admin`". It does not. The row reads "Team / Area
| org admin (**Team: one default per project, MVP**; Area: tree edits)", and
its Delete cell reads "Team: no, release 2 (a project keeps its default team
in MVP)"; the MVP table for M1 says "exactly one default team per project".
All three say the same thing, and the spec already follows it. Asking the
human to choose again re-opens an MVP cut doc-001 fixed, and the question as
phrased suggests the umbrella is inconsistent when it is not.
Required resolution: remove Q-3 from § Open questions; keep the substance as a
note under BR-M1-9 ("doc-001 MVP table M1 and its Operation completeness row
both fix one team per project; several teams are release 2"), and drop the
"open question Q-3" references from US-6 and US-7. If the human nevertheless
wants several teams in MVP that is a change to doc-001's MVP table, which is a
different task.
Status: open
Resolution: Q-3 deleted. BR-M1-9 carries the note that doc-001's MVP table
(M1) and its Operation completeness row (Team / Area) both fix one team per
project, several teams being release 2; the Q-3 references are gone from US-6
and US-7.

### R-6
Severity: major
Affects: BR-3, BR-M1-9, BR-M1-11, US-5, US-7
Problem: with exactly one team per project (BR-M1-9) and a root area always
owned by that team (BR-M1-11), the whole team-ownership mechanism is
degenerate in MVP, and two criteria become unreachable or unspecified:
- US-7 "An area with no explicit owner inherits the nearest owned ancestor"
  ends with "if no ancestor up to the root has an owner, the answer is 'no
  owner', and work items in that area appear on no team's board" — the root
  always has an owner, so this branch can never occur. It is inherited from
  doc-001 US-2, where BR-M1-11 did not yet exist. A criterion that cannot be
  produced cannot be tested, and M4 will implement a branch that never runs.
- The root area's own mutability is unstated. BR-M1-11 says the root is
  "named after it and renamed with it" and "cannot be deleted or moved" — it
  says nothing about renaming the root directly (US-5's Given "a project whose
  root area **still** carries the project's original name" implies it can be)
  and nothing about re-owning it, although `organizations.area.write` includes
  "re-own". If the root can be re-owned to nothing, the branch above becomes
  reachable and BR-M1-11 is incomplete; if it cannot, the branch is dead.
Required resolution: state in BR-M1-11 whether the root area may be renamed
directly and whether its owning team may be changed or cleared; then either
delete the unreachable step from US-7 or make it reachable and give it a
Given. Say in one line what BR-3 ownership buys in MVP given one team per
project (it is preparation for release 2 — that is a legitimate answer, but it
should be written, not implied).
Status: open
Resolution: BR-M1-11 now says the root is renamed only with the project,
never directly, cannot be deleted or moved, and is always owned by the
project's team (cannot be re-owned or cleared); so every area resolves to an
owner, and the rule says in one sentence that ownership decides nothing in MVP
and is kept so release 2's several teams start from owned areas. US-7: the
unreachable "no owner" branch is deleted, the root scenario adds rename and
re-own to the refused actions, the owner picker offers the project's team or
no explicit owner. US-5's "still carries" Given reworded.

### R-7
Severity: major
Affects: BR-M1-3, US-2, US-5
Problem: organization creation calls M2's seed-a-process capability — the only
cross-module call M1 makes — and nothing says what happens when it fails. If
the seed fails after the organization row exists, the instance holds an
organization with no process, every project in it is unusable (BR-1: a work
item's type comes from the organization's process), and the name is taken
(BR-M1-1) by an object the creator cannot remove. The reviewer checklist calls
for an integration failure mid-process; here it is the founding flow.
Required resolution: add a rule (or extend BR-M1-3): organization creation and
the process seed succeed or fail together — on a seed failure no organization
is created, the name stays free, and the creator sees a refusal. Add the
scenario to US-2. If atomicity is not achievable, name the compensating
behaviour instead.
Status: open
Resolution: BR-M1-3 extended: creation and seed succeed or fail together; on
a seed failure no organization exists, the name stays free, the creator sees a
refusal. US-2 has the scenario "A failed process seed leaves nothing behind"
(«Не удалось создать организацию. Попробуйте ещё раз», no membership, a retry
with the same name succeeds); the main BPMN has the seed-failure branch.

### R-8
Severity: major
Affects: BR-M1-1, BR-M1-4, BR-M1-12, NFR
Problem: three of the spec's invariants are check-then-act and the spec says
nothing about concurrency. doc-001's BR-11 covers only work items and
board/backlog reordering, so M1 inherits no rule at all.
- BR-M1-4: two administrators, each removing the other (or each downgrading
  the other) at the same time — both checks see two admins, both writes
  succeed, the organization is left with none. That is precisely the state
  Q-1 says is unrecoverable.
- BR-M1-1: two simultaneous creations of the same name pass the uniqueness
  check and produce two organizations.
- BR-M1-12: two simultaneous moves can build a cycle that neither move would
  have created alone.
Required resolution: add an NFR or a business rule stating that the
last-administrator invariant, name uniqueness and the acyclicity of the area
tree are enforced under concurrency (a unique constraint / a serialized check,
decided at design), and add at least the last-administrator race as a scenario
in US-3 — it is the one with an unrecoverable outcome.
Status: open
Resolution: new NFR "Concurrency" covers BR-M1-4, BR-M1-1 and BR-M1-12 (of
two changes that pass alone but break the rule together, one is refused;
mechanism decided at design; doc-001's BR-11 does not apply). Scenarios:
US-3 "Two administrators removing each other at once keep one", US-2
simultaneous same-name creation, US-7 simultaneous cycle-forming moves. Per
H-1, Q-1 is no longer a question: § MVP boundary carries "Known MVP
limitation — an organization with no usable administrator (accepted risk)"
with its triggers (every admin account disabled/deleted in Keycloak, or held
by a departed person), its effect and the only mitigation.

### R-9
Severity: major
Affects: US-3, Open question Q-4
Problem: US-3's "An administrator removes a member" asserts as a normative
criterion that work items already assigned to that person "keep that assignee,
**shown as a former member**", while Q-4 asks the human whether a "former
member" marking is wanted in MVP or in release 2. The spec both decides and
asks the same thing; whichever way the human answers Q-4, one of the two
documents is wrong. "Former member" is also a UI concept with no Russian
string and no entry in doc-001's glossary, and BR-13 makes the situation
permanent (a removed member is no longer a candidate but stays an assignee).
Required resolution: reduce the criterion to what is decided — the assignment
is unchanged and the work item stays readable — and let Q-4 decide the
rendering; or, if the rendering is decided here, drop that part of Q-4, add
the term and its Russian string to the spec and keep the criterion.
Status: open
Resolution: Q-4 answered by H-4 and deleted. New BR-M1-17: a member whose
Keycloak account is disabled or deleted keeps memberships, is shown as
«неактивен» in member lists and in «Исполнитель», cannot sign in, and is
removed only by an administrator; the USER projection gains an active status,
NFR "Identity refresh" makes mod-002 fix the refresh period. US-3's removal
scenario is reduced to "assignment unchanged, work items stay readable"; the
rendering of a removed (not inactive) member in «Исполнитель» is left to
`doc-002-work-items`. US-3 gains the inactive and re-enabled scenarios.

### R-10
Severity: major
Affects: BR-6, BR-M1-1, § Operation completeness, the lifecycle BPMN
Problem: BR-M1-1 releases a reserved name only when the object "is purged",
the BPMN lifecycle has a state "admin purges by hand, no retention window",
and § Operation completeness says for Organization "purge is by hand, no UI in
MVP". Nowhere is it said who purges or through what — an organization
administrator has no database access, and BR-M1-2/BR-M1-8 keep the instance
administrator out of the organization entirely. In MVP, therefore, no purge
can happen, and a deactivated organization's or project's name is reserved
forever. The Project row of § Operation completeness does not mention purge at
all, so it is not even clear the same applies there.
Required resolution: say explicitly that MVP has no purge operation and that a
deactivated object's name stays reserved indefinitely (and reword BR-M1-1's
"until it is purged" accordingly), or define the purge: actor, entry point,
what it takes with it. Make the Organization and Project rows of § Operation
completeness say the same thing.
Status: open
Resolution: MVP has no purge. BR-M1-1 reworded: a deactivated organization or
project keeps its name reserved for as long as it exists; the purge state is
removed from the lifecycle BPMN; the Organization and Project rows of
Operation completeness both say "no purge in MVP — it and its name persist";
Out of scope lists purging; US-2 and US-5 deactivated lists show no purge
action.

### R-11
Severity: major
Affects: § Decomposition
Problem: two objections to the proposed backlog.
(a) Sizes. `feat-NNN-projects-and-default-team` creates the Project and Team
slices with their entities, endpoints and permission codes;
`feat-NNN-area-tree` creates the Area slice with the ownership resolution that
M4 will depend on; `feat-NNN-org-admin-audit-log` founds an audit mechanism.
routing.md's L thresholds include "a new slice or module is created" and "the
authentication/authorization rules ... change", and the same file says
"understating the size is forbidden: if you hesitate between M and L — take
L". All three are proposed as `M` with no argument against the threshold list.
(b) The slug. `mod-002-organizations-projects-teams` is narrowed here to
organizations, membership and the code derivation — projects and teams leave
it — but keeps a name that promises projects and teams. The queue line and
every `deps` reference will go on claiming a scope the task no longer has.
Required resolution: for each proposed line, either take `L` or record in the
table the threshold-by-threshold reason it stays `M`; and either keep the
projects/teams scope inside `mod-002-...` or say in the table that the line's
title has to be corrected when the orchestrator edits the queue.
Status: open
Resolution: per H-5 the decomposition is one row — the queued
`mod-002-organizations-projects-teams`, name and scope unchanged, size L,
delivering US-1..US-7; the three `feat-NNN` lines are gone, so (a) and (b)
fall away. US-5, US-6, US-7 headers now say delivered by
`mod-002-organizations-projects-teams`.

### R-12
Severity: minor
Affects: US-4
Problem: the last scenario claims "the only instance-level things they can do
are creating an organization and granting that right" — false: the spec's own
§ Roles keeps a second instance-level role (dictionaries administrator, M0),
and per ADR-0018 the realm `admin` role holds every permission in the catalog,
M0's included. As written the criterion would fail a correct implementation.
Required resolution: scope the claim to M1.
Status: resolved
Resolution: fixed by the reviewer in `stories/US-4-organization-scoped-access.md`
— the claim is now scoped to M1 and M0's instance-level rights are named as
unaffected.
Resolution (analyst, rework round 1): confirmed; the scenario keeps the M1
scope, its Q-1 reference now points at the known MVP limitation.

### R-13
Severity: minor
Affects: BR-M1-1, US-2, US-5, US-6, US-7
Problem: the only length constraint on any name in the whole spec is a Gherkin
step in US-2 ("a name longer than 100 characters"). BR-M1-1 does not carry it,
and project, team and area names have no stated limit at all — so three of the
four name inputs have no validation requirement to test against.
Required resolution: state the constraint once, in BR-M1-1, for every M1 name.
Status: resolved
Resolution: fixed by the reviewer — BR-M1-1 now states that the name of an
organization, project, team or area is 1..100 characters after trimming.
Resolution (analyst, rework round 1): confirmed, kept in BR-M1-1; US-2's
validation refusal now quotes «Название должно содержать от 1 до 100
символов».

### R-14
Severity: minor
Affects: NFR "Access checks", BR-M1-6, US-1
Problem: the NFR says an access check needs "no re-login after a role change",
but US-1 twice requires the user to sign in again after the instance-level
right is granted or revoked — correctly, because that code is carried in the
token and ADR-0018 states plainly that "revocation is NOT instant". The NFR as
written contradicts the story it shares a spec with.
Required resolution: distinguish the two kinds of code in the NFR.
Status: resolved
Resolution: fixed by the reviewer in § Non-functional requirements — the
bullet now separates organization-scoped codes (next request, no re-login)
from the instance-level code (token-carried, effective on token renewal,
ADR-0018).
Resolution (analyst, rework round 1): confirmed, the bullet is unchanged.

### R-15
Severity: minor
Affects: US-4
Problem: "in B they can read everything a member can write, and no action on
**any screen** creates, changes or deletes anything" quantifies over every
screen the system will ever have; it cannot be turned into a test, and the
first half restates a role-table sentence instead of an observable outcome.
Required resolution: enumerate the actions M1 owns.
Status: resolved
Resolution: fixed by the reviewer in US-4 scenario 1 — the criterion now names
the M1 write actions that must be neither offered nor accepted.
Resolution (analyst, rework round 1): confirmed, scenario 1 is unchanged.

### R-16
Severity: minor
Affects: US-3, US-5, US-7
Problem: refusals are specified inconsistently. Some quote the product string
(«Недостаточно прав», «Организация не найдена», «В организации должен остаться
хотя бы один администратор», «Нельзя удалить область ...»), others say only "a
message naming the conflict" / "a validation message" — and a message is
exactly what a tester and a translator need fixed. (The Russian copy that is
present is consistent with doc-001's glossary: «Организация», «Проект»,
«Область», «рабочие элементы» — no term drift found.)
Required resolution: give the conflict refusals their product string.
Status: resolved
Resolution: fixed by the reviewer — US-3 (duplicate membership), US-5 (project
name taken; project name taken by a deactivated project) and US-7 (sibling
area name taken) now quote the Russian string. The organization-name conflict
in US-2 is deliberately left unfixed: its wording depends on the decision
required by R-2.
Resolution (analyst, rework round 1): confirmed; the one gap left open here —
the organization-name conflict in US-2 — is now closed under R-2, and every
refusal added in this rework quotes its Russian string.

### R-17
Severity: minor
Affects: US-3, BR-M1-4
Problem: nothing says whether an administrator may change or remove their own
membership when another administrator exists (self-demotion, leaving the
organization), and nothing says whether a member may leave an organization on
their own. BR-M1-4 only covers the last administrator. The gap will be
answered by whoever writes the handler.
Required resolution: one line in BR-M1-4 or § Operation completeness: an
administrator may downgrade or remove their own membership as long as another
`admin` remains; a member leaves only by an administrator's action (or may
leave on their own — decide).
Status: open
Resolution: BR-M1-4 now says an administrator may downgrade or remove their
own membership while another active administrator remains, and a member or
reader leaves only by an administrator's action (no self-leave in MVP); the
Organization membership row of Operation completeness says the same. US-3:
"An administrator steps down while another administrator remains", the
last-admin scenario covers the administrator acting on themselves, and the
member/viewer scenario covers their own membership.

### R-18
Severity: minor
Affects: US-3, US-6, US-7
Problem: the empty and "nothing found" states are specified for the
organization list and the project list (US-2) and nowhere else: the member
search with no matches, a team whose members have all been removed (reachable
via BR-M1-10 when the creating administrator loses their membership), and a
project whose area tree is the root alone have no criterion. The checklist
calls for empty lists explicitly.
Required resolution: add the three empty/no-result cases, or state that a team
may not be left without members.
Status: open
Resolution: all three cases added. US-3 "A search with no matches says so"
(«Пользователи не найдены»); BR-M1-10 states a team may be left with no
members and US-6 "A team with no members says so" («В команде пока нет
участников»); US-7 "A tree with only the root says so" («Вложенных областей
пока нет»).

### R-19
Severity: minor
Affects: BR-M1-14, US-7
Problem: BR-M1-14 ("a work item created without an explicit area gets the
project's root area") is M3 behaviour written as an M1-local rule, and US-7
tests it with a work item creation. doc-002-work-items is a sibling analysis
task that will write its own creation rules; two owners of one rule is how
they diverge.
Required resolution: reframe it as what M1 provides — the project exposes a
default area, which is the root in MVP (a per-team default is release 2) — and
note that the creation-side rule belongs to doc-002-work-items, which must
echo it.
Status: open
Resolution: BR-M1-14 reframed: the project exposes a default area, the root in
MVP (per-team default is release 2); the creation-side rule belongs to
`doc-002-work-items`, which must echo it. US-7's work-item-creation scenario
replaced by "The project exposes its root area as the default area".

## Notes

### On the length of `spec.md` (289 lines vs the ~250 index guide)

Legitimate size, not leakage. Nothing in `spec.md` is story content: there is
no Gherkin, no per-criterion detail, no wording that belongs under
`stories/`. The overrun is entirely index-shaped material that an entity-heavy
module index has to carry — a 13-row permission table (20 lines), two BPMN
diagrams (32), the ER model with its three invariants (40), fifteen business
rules, an eight-row operation-completeness table. doc-001's umbrella index was
accepted at 378 lines with exactly this shape, and the sections here are each
shorter than their doc-001 counterparts.

Splitting the ER model into a `model.md` is not recommended: the entities, the
invariants and the rules that constrain them are read together, and a second
file buys a jump for the reader in exchange for a line count. The ~250 figure
is a guide against stories leaking upward; that is not what happened here.

### Human-only questions for gate-1

1. **Q-1 (spec) — an organization with no usable administrator.** Genuinely
   human-only: the trade-off is BR-M1-8's isolation against operability, and
   R-8 makes the state reachable by a plain race, not only by a disabled
   account. Option (a) as the spec has it means such an organization is
   unrecoverable without direct database access.
2. **Q-4 (spec) — identity lifecycle.** Genuinely human-only. It must be
   answered rather than deferred, because US-3 already asserts one of the
   answers (R-9).
3. **New, from R-2 — may a name-conflict refusal reveal that an organization
   exists?** Either organization names are public within the instance (the
   Azure DevOps behaviour) or BR-M1-8 wins and the refusal must be
   uninformative. The spec cannot keep both.
4. **New, from R-3 — is the M1 audit log in MVP at all, and what does it
   show?** Its rule exists, its story does not, and its queue line is
   proposed as if it were MVP.
5. **From R-11 — the backlog shape.** The narrowing of
   `mod-002-organizations-projects-teams` and the three new lines land in the
   queue at gate-1; their sizes (`M` against routing's L thresholds) and the
   now-misleading slug need the human's nod.

**Not human-only, contrary to the spec:** Q-2 is already answered by ADR-0018
§ Consequences (R-4) and Q-3 by doc-001's MVP table and Operation completeness
row (R-5). Both should be resolved in the spec instead of being put to the
human.

## Round 2 — requirements (delta)

Input: the round-1 findings and their Resolution lines, `status.md` § Decisions
(D-1..D-12, H-1..H-5) and the analyst's four gate-1 calls, the reworked
`spec.md` (327 lines) and `stories/US-1..US-7`, checked against ADR-0018 and
`deploy/keycloak/realms/mustask-realm.json` for the § Roles constraints. This is
a delta: nothing outside the rework was re-investigated.

Verdict: **needs-human-decision** — no blockers. 2 majors (R-20, R-21) and 1
minor (R-25) are open. The reviewer fixed 3 minors (R-22..R-24) in place. This was the second and last review iteration
(stage file: at most 2), so the open items go to the human at gate-1, not back
to the analyst for a third round. Each has a proposed wording, so a yes/no from
the human is enough to close it.

### Round-1 findings — verification

| Finding | Verdict | Checked where |
|---|---|---|
| R-1 | resolved | § Roles constraints 1–4 match the realm import as it stands: `admin` holds both `dictionaries.languages.*`, `member`/`viewer` hold `.read` only, `dictionaries-admin` is separate. US-4 scenarios 4–6 test them |
| R-2 | resolved, H-2 faithful | BR-M1-1 (names are public, "taken" and nothing more), BR-M1-8 (limited to content), US-2 (same string for an active or a deactivated holder, and for a rename), BPMN C1 |
| R-3 | resolved, H-3 faithful | BR-M1-15 appears only under Release 2, built on `mod-003`'s audit ADR. BR-M1-16 is who/when only, tested in US-3. The feat line is gone |
| R-4 | resolved | BR-M1-2, the Roles table row, Operation completeness, and the Release-2 line |
| R-5 | resolved | the BR-M1-9 note. US-6 and US-7 no longer refer to Q-3 |
| R-6 | resolved | BR-M1-11 (the root has no rename or re-own of its own; ownership is kept for release 2). US-7 no longer has the unreachable branch; US-5's Given is reworded |
| R-7 | resolved | BR-M1-3 (creation and seed are atomic), US-2's seed-failure scenario, BPMN D1 |
| R-8 | resolved, extended by R-21 | NFR Concurrency plus scenarios in US-2, US-3 and US-7. It lists only three invariants — see R-21 |
| R-9 | resolved, H-4 faithful | BR-M1-17, the USER status attribute, NFR Identity refresh, US-3's inactive and re-enabled scenarios. Rendering a removed member is left to `doc-002-work-items` |
| R-10 | resolved | BR-M1-1, the lifecycle BPMN, the Organization and Project rows, Out of scope, US-2 and US-5 |
| R-11 | resolved, H-5 faithful | one row: `mod-002-organizations-projects-teams`, name and scope unchanged, L, US-1..US-7, deps as doc-001 (`mod-001-process-seed`) |
| R-12..R-16 | resolved | confirmed. R-16's leftover gap (the organization-name string) is closed under R-2 |
| R-17 | resolved | BR-M1-4, the membership row, and US-3's step-down, last-admin and member/viewer scenarios |
| R-18 | resolved | US-3, US-6 and US-7 empty states, each with its Russian string. BR-M1-10 allows a team with no members |
| R-19 | resolved | BR-M1-14 now describes what M1 provides. US-7 no longer creates a work item |

The round-1 entries R-1..R-11 and R-17..R-19 still read `Status: open` above.
This table is the round-2 status for them. Round 1 is not rewritten.

**H-1 is recorded as a limitation, not solved.** § MVP boundary names the case
"accepted risk, H-1". It lists the triggers and the effect, gives no recovery
path, and names keeping several administrators as the only mitigation. The
R-8 race rule is kept as correctness, as H-1 requires. See R-23 for the one
clarification added.

### R-20
Severity: major
Affects: § Roles ("Constraints the ADR-0018 amendment must satisfy"), BR-M1-17, NFR "Identity refresh", NFR "Member search", US-3
Problem: the rework makes MUSTASK read Keycloak's user directory on its own
initiative, and the constraints on the ADR-0018 amendment do not mention it.
Two behaviours need it:
(a) Member search (D-10, US-3) has to find users who have never signed in to
MUSTASK. Operation completeness says the projection "appears when a user ... is
picked in member search", so the search runs against Keycloak, not against the
projection.
(b) BR-M1-17 and the Identity refresh NFR need a disabled or deleted account's
status. That can never come from the token, because such a user no longer signs
in.
Both require a server-to-Keycloak channel: an admin-API client with
realm-management rights (a service account, a privileged credential kept as a
secret), or an event listener. ADR-0018 as accepted has no such channel: the
API "only authenticates the caller" and reads only the token. This is a new
trust relationship, and the spec lists the amendment's constraints as if they
were complete. The failure path is also unspecified — what the administrator
sees when Keycloak is unreachable during a search or a refresh. The checklist
asks for integration failures, and so far only the M2 seed has one (R-7).
Cost: `mod-002` either finds this at design and widens the amendment without an
agreed requirement, or grants the service account more than it needs.
Required resolution: add constraint 5 to § Roles: "MUSTASK reads users from
Keycloak — search by name/e-mail and account status — through a channel the
amendment names (admin API with a service account, or events). Its rights are
limited to viewing and querying users; its credential comes only from
environment/user secrets. An access check still never calls Keycloak." Add one
US-3 scenario: when Keycloak is unreachable, the add-member search is refused
with a Russian message (proposed: «Поиск пользователей временно недоступен.
Попробуйте позже»), and existing members' statuses stay as last refreshed.
Status: open — for the human at gate-1 (accept the wording above, or leave it to
`mod-002`'s design as accepted risk)
Resolution: H-6 accepts the wording above. spec.md § Roles gains constraint 5
(a Keycloak user-directory channel named by the amendment, rights limited to
viewing/querying users, credential only from secrets, an access check still
never calls Keycloak); US-3 gains the scenario "Member search is unavailable
when Keycloak cannot be reached", refused with «Поиск пользователей временно
недоступен. Попробуйте позже», existing members' statuses staying as last
refreshed.

### R-21
Severity: major
Affects: NFR "Concurrency", BR-M1-5, BR-M1-10, BR-M1-12, BR-M1-13
Problem: the new NFR lists three invariants (BR-M1-4, BR-M1-1, and the
acyclicity half of BR-M1-12). Everything it leaves out reads as exempt. Other
check-then-act rules in the same table break the same way:
- BR-M1-5: two administrators add the same user at once, and two memberships
  result.
- BR-M1-10: an administrator adds a user to a team while another removes that
  user's organization membership, and a team member ends up outside the
  organization.
- BR-M1-12, sibling-name uniqueness: two siblings with the same name are
  created at once.
- BR-M1-13, across modules: an empty leaf is deleted while M3 creates a work
  item in it, or while a child is created or moved under it. The work item then
  points at a deleted area.
The BR-M1-13 race crosses a module boundary. It is the costliest of these,
because M3 will not look for it in an M1 rule.
Required resolution: reword the NFR's first sentence to: "Every invariant
stated in BR-M1-1, -4, -5, -10, -12 and -13 holds under concurrent requests,
including a work item created in an area (M3) while that area is deleted". The
rest of the NFR stays as is. No new scenarios are needed.
Status: open — for the human at gate-1 (a one-sentence change; the reviewer
could not apply it because this round's write zone allows fixes in place for
minor findings only)
Resolution: H-7 accepts the wording above. NFR "Concurrency" (spec.md) now
opens "every invariant stated in BR-M1-1, -4, -5, -10, -12 and -13 holds under
concurrent requests, including a work item created in an area (M3) while that
area is deleted"; the rest of the NFR is unchanged.

### R-22
Severity: minor
Affects: BR-M1-5, BR-M1-17
Problem: the rework introduced a contradiction. BR-M1-5 said "a membership
always points at an existing Keycloak identity", while the new BR-M1-17 keeps a
deleted account's memberships. After a deletion the two cannot both hold, and a
handler that enforces BR-M1-5 literally would delete the memberships that H-4
keeps.
Required resolution: make BR-M1-5 a rule about creation.
Status: resolved
Resolution: fixed by the reviewer in BR-M1-5. It now reads "a membership is
created only for an existing Keycloak identity, and outlives that identity
being disabled or deleted (BR-M1-17)".

### R-23
Severity: minor
Affects: BR-M1-4, § MVP boundary (known limitation, H-1)
Problem: two problems.
(a) BR-M1-4 opened with an invariant, "an organization keeps at least one
active admin", that a Keycloak disable breaks without any MUSTASK request. That
is exactly H-1's known limitation. The rule also did not say whose view of
"active" counts. With NFR Identity refresh the projection lags Keycloak, so a
test has to know it is the projection.
(b) The known limitation said "no recovery path" without mentioning that an
account that was only disabled, once enabled again in Keycloak, gets its admin
role back. This follows from the analyst's own call ("re-enabled account clears
«неактивен»"). The human should see this when weighing H-1. It does not change
H-1.
Required resolution: state BR-M1-4 as a gate on MUSTASK changes, measured
against the projection, and add one sentence to the limitation.
Status: resolved
Resolution: fixed by the reviewer.
- BR-M1-4 now opens "No change made in MUSTASK may leave an organization
  without an `admin` membership whose account is active (BR-M1-17) — active as
  MUSTASK's user projection shows it at the moment of the change". It adds that
  a Keycloak disable or deletion "is not such a change and is never refused".
- The limitation now adds: a disabled administrator account enabled again in
  the Keycloak console regains its role, a deleted one cannot come back, and
  "neither is a MUSTASK recovery path".
H-1 is unchanged: MUSTASK still offers no recovery path.

### R-24
Severity: minor
Affects: US-3 "Two administrators removing each other at once keep one"
Problem: the scenario required the losing request to be refused with the
last-administrator message. A correct serialized implementation refuses it
differently: once A's removal of B commits, B is no longer a member, so B's
request fails authorization (BR-M1-8) before the last-admin check runs. As
written, the criterion would fail a correct implementation.
Required resolution: allow both refusals and assert only that the losing request
changes nothing.
Status: resolved
Resolution: fixed by the reviewer in US-3. The losing request "is refused and
changes nothing — with «В организации должен остаться хотя бы один
администратор», or, when the refused actor's own membership is the one already
removed, as for a non-member (BR-M1-8)".

### R-25
Severity: minor
Affects: BR-M1-17, US-3 (member search), US-6 (team picker), BR-13
Problem: BR-M1-17 says how an inactive member is shown. It does not say whether
an inactive account is offered as a candidate in three places:
- the add-member search, where a disabled Keycloak user could be added to an
  organization;
- the add-to-team picker;
- the assignee candidates of BR-13.
Whoever writes the handlers will decide this.
Required resolution: one sentence in BR-M1-17. Proposed default: an inactive
account is not offered in the add-member search or the team picker. Whether it
remains an assignee candidate is for `doc-002-work-items` to state, alongside
BR-13.
Status: open — for the human at gate-1 (a product choice beyond H-4's text;
accept the proposed default or state another)
Resolution: H-8 accepts the proposed default. BR-M1-17 (spec.md) gains the
sentence that an inactive account is not offered as a candidate in the
add-member search or the add-to-team picker, leaving the assignee-candidate
question to `doc-002-work-items`. US-3 gains "An inactive account is not
offered in the search"; US-6 gains "An inactive organization member is not
offered in the picker".

### On the length of `spec.md` (327 lines after the rework, 331 after R-23)

Still legitimate size, no split. The growth over round 1 (+38 lines) is index
material:
- the constraints on the ADR-0018 amendment (about 30 lines). These are
  requirements that `mod-002` must meet, and they have no story of their own to
  live in;
- the H-1 known limitation;
- the Concurrency and Identity refresh NFRs;
- BR-M1-16 and BR-M1-17.
None of it is Gherkin or story-level detail. If the constraints block grows
further, for example with R-20's constraint 5, it is the first candidate to move
into a `constraints.md` beside the spec. The ER model should stay where it is.

### Items for the human at gate-1

1. **R-20** — accept constraint 5 (a user-directory channel from MUSTASK to
   Keycloak, least privilege, credential only from secrets) and the "Keycloak
   unreachable" search scenario, or leave both to `mod-002`'s design.
2. **R-21** — accept the widened Concurrency NFR (a one-sentence change).
3. **R-25** — should inactive accounts be offered in member search and the team
   picker? Proposed: no.
4. The analyst's four calls (status.md Log, 2026-09-24):
   - BR-M1-4 counts only active administrators. With R-23, "active" means as
     the projection shows it.
   - Members and readers cannot leave on their own.
   - A re-enabled account loses «неактивен». This is also the only way outside
     MUSTASK to bring back an organization whose admins were disabled (R-23).
   - The root area cannot be renamed or re-owned on its own.
   The reviewer found no contradiction in any of them with D-1..D-12 or
   H-1..H-5.
