# Review: feat-033-orphaned-organization-rescue

## Round 1 — requirements

Reviewer: requirements-reviewer (clean context, not the spec author), 2026-11-03.
Input: `spec.md`, `stories/US-1..US-3`, checked against doc-006 (BR-M1-1, -2, -4,
-5, -8, -16, -17, § "Known MVP limitation" H-1), mod-016 (spec, BR-M1-25, -32,
-35, § Entities), mod-014 spec (BR-UP-1, -5, -8, -12, -14, -15, -17), ADR-0027,
ADR-0029 (§ 8, § 9), ADR-0033, tech-024 `status.md`, and ADR-0034 /
tech-025 `status.md` as they stand on the tech/024 branch (read from the
tech-024 worktree `app-deployment-keycloak-9df488`, since they are not on main yet).

Verdict: **needs-human-decision** — 1 blocker, 2 majors, 5 minors. R-1 changes
what the feature detects and when it may act. Only the human can choose,
because every fix either goes beyond the queue line or changes tech-025.

### Rule → story mapping

| Rule | Story / place | Note |
|---|---|---|
| BR-RS-1 right in `InstanceRoles`, `system-admin` only | US-1 "Everyone with the role…", "Losing the role…"; US-2/US-3 "Without the right" | the refusal answer is not named (R-7) |
| BR-RS-2 the list | US-1 "The list shows…", "Filter…", "Volumes" | filter matching and paging unspecified (R-7) |
| BR-RS-3 disclosure boundary | US-1 "discloses nothing else", "content stays hidden"; US-2 "I still see no content" | status of every org is disclosed too (R-6) |
| BR-RS-4 only with no active admin | US-2 "Refused while an active administrator exists", "came back in between" | rests on BR-RS-5, which is dead under ADR-0034 (R-1) |
| BR-RS-5 "known active administrator" | US-1 "unknown administrator", "state follows the projection" | **R-1, R-3** |
| BR-RS-6 appointee: active projected user | US-2 "Only active known users…", "search does not reveal membership" | "active" no longer means anything (R-1, R-2) |
| BR-RS-7 no self-appointment | US-2 "No self-appointment" | ok |
| BR-RS-8 member raised / non-member added | US-2 "Appoint a non-member", "Appoint an existing member" | ok |
| BR-RS-9 next request, deactivated org | US-2 "takes effect on the next request", "A deactivated organization" | ok (mod-016 BR-M1-35 gives the appointee the deactivated list) |
| BR-RS-10 appointment record | US-3 all | "deleted in Keycloak → «неактивен»" cannot be produced (R-3) |
| BR-RS-11 concurrency | US-2 "Two appointments at once", "came back in between" | the second sentence assumes a Keycloak re-enable feed (R-1, R-3) |
| doc-006 H-1, "a person who has left and whose account nobody can use" | none | not covered even before ADR-0034 (R-1) |
| ADR-0029 § 9 "inventory exceptions that carry that reason" | none | missing from the decomposition (R-8) |

Checked and clean: the reference tracker is not named anywhere in the spec or
the stories; nothing writes to Keycloak and no client role is added (ADR-0033
rule 1 holds, `system-admin` already carries every instance code); the
self-appointment ban is stated and tested; the record is separate from the
membership's who/when, written in the same operation, and has a defined
reader; the deactivated organization case is consistent with mod-016
BR-M1-35; both development passes are ≤ M (backend is borderline, R-8).

### R-1
Severity: blocker
Affects: BR-RS-4, BR-RS-5, BR-RS-6, BR-RS-11, US-1, US-2, OQ-4, OQ-5, § Context
Problem: The feature decides whether a rescue is allowed from the
projection's "active" status. ADR-0034 (tech-025, gate-3 approved, merged into
the tech/024 branch, so it reaches main with tech-024, which this task depends
on) removes the source of that status. Its Consequences state: "A user who was
deactivated or deleted in Keycloak stays in the projection as of their last
sign-in: it shows as active and is found by search … the «неактивен» status …
is no longer fed." Rule 3 of the same ADR also fails the sign-in of anyone the
projection does not know, so after tech-025 no new administrator can be
"unknown" either: the creator signed in to create the organization, and every
added member was found in the projection. Together:
- H-1's own case (every admin disabled or deleted in Keycloak) shows «Есть
  активный администратор» forever, and BR-RS-4 refuses the rescue. The feature
  cannot rescue anything it was built to rescue.
- «Нет активного администратора» shows only on push-era data: organizations
  whose admins were marked inactive before tech-025 shipped and have not signed
  in since (ADR-0034 rule 1 re-enables such a row at the next sign-in), plus
  organizations seeded by mod-016 BR-M1-25 whose creator has not signed in
  since. In that second group the creator may be perfectly well and active in
  Keycloak, so the feature fires there by mistake (OQ-5's risk, now the only
  case it fires in).
- doc-006 H-1 also names "a person who has left and whose account nobody can
  use", an account that is still enabled. The spec never covered that case,
  even with the push feed.
OQ-5 treats tech-025 as a possible future change ("if tech-025 changes…"). It
is a known fact today, and US-1/US-2 are written against the push-fed
projection that tech-025 removed.
Required resolution: The human chooses what triggers a rescue. The spec is
then rewritten around that choice, with BR-RS-4, -5, -6, -11, the Context,
OQ-4/OQ-5 and the scenarios of R-3 changed together. Options:
- **A (recommended): rescue on request, in any organization, never taking
  anything away.** The system administrator may appoint an administrator in
  any organization, whether or not it has one. The appointment only adds or
  raises; it never removes or downgrades anyone. A reason is required,
  «Причина», 1–500 characters after trimming, and is stored in the record of
  BR-RS-10. The list drops the «Администратор» column and its filter: the
  projection can no longer answer that question. New refusals: «Пользователь уже
  администратор этой организации» (also used when two concurrent appointments
  pick the same user, replacing BR-RS-11's first sentence). The
  organization's members see the who/when as today. This works with
  ADR-0034 as it is, covers the "person who left" case, and needs no new data.
  Cost: `system-admin` can add an administrator to a healthy organization,
  which is wider than the queue line's wording. The mitigations are the
  self-appointment ban, the durable record with its reason, and the who/when
  every member reads. The ADR of § ADR debt must say so.
- **B: rescue by staleness.** Record each user's last sign-in day (a change
  to tech-025: today "an unchanged profile writes nothing" and an unchanged
  renewal does not touch the database, so a daily write on renewal is new).
  An organization is rescuable when none of its admins has signed in for N
  days (N = 30, configurable). The list shows «Последний вход администратора»
  as a date, with no name. This keeps the queue line's meaning, but it changes
  tech-025, waits N days before a rescue, is a heuristic (a long holiday looks
  the same), and discloses one more fact about membership (R-6).
- **C (not recommended): read the account status from Keycloak's admin REST.**
  It needs a service account with `view-users` in the company realm. ADR-0034
  rejected this (its Option 1), and ADR-0023 keeps the admin surface out of
  shipping code.
- **D (not recommended): keep the spec as written.** It ships a feature that
  fires only on push-era leftovers and refuses H-1's actual case.
Whichever option is chosen, the spec must not claim a detection that the
projection cannot make, and BR-RS-5's reference to mod-016 BR-M1-32 must be
re-read against ADR-0034.
Status: resolved
Resolution: The human chose A. Context rewritten around ADR-0034. BR-RS-4 now
allows an appointment on request in any organization and only adds or raises.
New BR-RS-5 makes «Причина» required (1–500 characters, kept only in the
record). BR-RS-2 drops the administrator state and its filter, and says why.
Detection is moved to Out of scope. OQ-4 and OQ-5 are closed. H-1's "account
still enabled but nobody can use it" case is now covered by the request-based
trigger. Verified in d121cec9 (round 2).

### R-2
Severity: major
Affects: BR-RS-4, BR-RS-6, BR-RS-9, US-2
Problem: Under ADR-0034 the appointee search offers accounts that were
disabled or deleted in Keycloak. They are "active" in the projection and
found by search, so the system administrator can appoint a ghost. Under the
current BR-RS-4 that is irreversible: the ghost now counts as a known active
administrator, so the organization shows «Есть активный администратор», a
second rescue is refused for good, and the organization is orphaned again with
no way out. That is the exact H-1 state, now caused by the feature itself.
Required resolution: A rescue must be repeatable when an appointment did not
stick. Under option A that already holds. Under option B, a ghost appointee
who never signs in becomes stale and the rescue reopens after N days; say so
in BR-RS-9. In every option, the dialog text must say that MUSTASK cannot see
whether the account is still enabled in Keycloak (for example, add to the
dialog: «MUSTASK не видит, отключена ли учётная запись в Keycloak: убедитесь,
что пользователь может войти.»). BR-RS-6 must also drop "an inactive …
user is refused" as the guard against this case: after ADR-0034 that guard
protects nothing.
Status: resolved
Resolution: BR-RS-9 states that the rescue is repeatable, and US-2 "The
rescue is repeatable" tests it. BR-RS-6 drops the "inactive" guard, refuses
only users the projection does not know, and requires the Keycloak warning,
which US-2 quotes verbatim and tests ("The dialog warns about Keycloak
accounts"). The BPMN loops back from "the appointee cannot sign in".

### R-3
Severity: major
Affects: US-1 "The list shows…" (Бета, Гамма), "An unknown administrator does not count", "The state follows the projection"; US-2 "Only active known users…", "An administrator came back in between"; US-3 "Users who left are still named"; BR-RS-11 second sentence
Problem: These criteria cannot be set up against the product after tech-025,
so they cannot be turned into tests. "Disabled in Keycloak", "deleted in
Keycloak", "enabled again and the projection receives it" and "a user the
projection does not know yet" (as an organization's creator, or as a
candidate who exists in Keycloak) all assume the push feed, which ADR-0034
removed. A test would have to plant push-era rows straight in the database,
which tests storage, not behaviour. US-3's «неактивен» on a deleted system
administrator will never show for new data.
Required resolution: Rewrite these scenarios once R-1 is decided, using only
states the product can reach. Under option A: "an organization whose members
report its only administrator has left", appointment with «Причина», the
already-administrator refusal, and two concurrent appointments of the same user
and of different users (both succeed). Under option B: the last sign-in date
and the N-day boundary (N−1 days: not rescuable; N days: rescuable). Replace
BR-RS-11's second sentence with the race that actually exists: under B, an
administrator's sign-in landing during the appointment. Under A it disappears.
Keep «неактивен» / «Неизвестный пользователь» in BR-RS-10's naming rule only
for push-era rows, and say so.
Status: resolved
Resolution: Every Keycloak-feed scenario is gone. US-1 tests status, the
claim-nothing rule, the filter and paging. US-2 tests the reason, an
organization with administrators, already-administrator, never signed in,
repeatable, the same user at once (one refused) and different users at once
(both succeed). US-3 drops "deleted → неактивен". BR-RS-11 is rewritten to
the two races that exist. BR-RS-10 limits «неактивен» / «Неизвестный
пользователь» to pre-tech-025 rows. Every rewritten scenario can be set up
against the product (round 2 check).

### R-4
Severity: minor
Affects: § Open questions (ADR debt), § Decomposition (backend)
Problem: The ADR debt cites "ADR-0029 § 8 ('the instance administrator still
sees no content')" as a rule to amend. That sentence is in the summary table
row for ADR-0027 rule 4. ADR-0029 § 9 (accepted at doc-012 gate-1) already
sanctions this feature: "feat-033's rescue is an instance operation that
appoints an organization admin. It writes a membership, is audited, reads no
content and holds no scoped code. Its routes are inventory exceptions that
carry that reason." So the ADR owed is narrower than the spec says. It must
cover: what the list discloses (R-6), the record, and option A's or B's
trigger. ADR-0027 rule 4 and ADR-0033 § 1 are still amended. ADR-0029 § 9 is
built on, not amended.
Required resolution: Cite ADR-0029 § 9 as the basis, narrow the ADR debt to
the three points above, and name the BR-M1-33 inventory exceptions (ADR-0029
§ 9) in the backend pass.
Status: resolved
Resolution: The ADR debt now builds on ADR-0029 § 9 without amending it. It
decides the trigger, the BR-RS-3 disclosures and the record with its second
reader, and it rewords ADR-0027 rule 4 and ADR-0033 § 1. The backend pass
registers the routes as inventory exceptions (mod-016 BR-M1-33), and BR-RS-1
and BR-RS-3 cite § 9.

### R-5
Severity: minor
Affects: § Open questions OQ-5; deps (status.md, orchestrator's)
Problem: The spec depends on tech-025's projection semantics (R-1), but
status.md lists deps `mod-016,tech-024` only. tech-025 is stacked on tech-024
and reaches main with it, but the dependency is not recorded, and OQ-5 still
reads as conditional.
Required resolution: The orchestrator adds `tech-025` to the deps in
status.md. The spec replaces OQ-5's "if tech-025 changes…" with the fact (ADR-0034,
Consequences) and cites ADR-0034 where BR-RS-5 cites the projection.
Status: resolved
Resolution: `status.md` deps now read `mod-016,tech-024,tech-025`. OQ-5 is
closed as dropped. ADR-0034 is cited in the Context and in BR-RS-2, -6, -10,
the NFR Access line and Out of scope.

### R-6
Severity: minor
Affects: BR-RS-2, BR-RS-3
Problem: BR-RS-3 says the administrator state is "the one fact about an
organization's membership" disclosed. It is silent on two more disclosures:
(a) the status «Деактивирована» of every organization, which mod-016
BR-M1-35 shows only to holders of `organizations.organization.deactivate` in
that organization. doc-006 BR-M1-1 makes the name public, not the status.
(b) The appointee search exposes the whole instance's directory (name and
e-mail of every signed-in user) to the system administrator, outside any
organization. Both are probably acceptable, but they must be stated rules so
the ADR and the tests carry them. Option B would add a third disclosure (the
last administrator sign-in date).
Required resolution: Add to BR-RS-3: "the status of every organization and,
through the appointee search, the name and e-mail of every projected user are
disclosed to the system administrator", plus option B's date if chosen. Carry
these into the ADR debt (R-4).
Status: resolved
Resolution: The human accepted both disclosures. BR-RS-3 now lists exactly
(a) name and status, (b) the search's name and e-mail, (c) the record, and
(d) the already-administrator fact, which is new and is OQ-6. All of them are
carried into ADR debt point (2).

### R-7
Severity: minor
Affects: BR-RS-1, BR-RS-2, US-1 "Filter…", "Volumes", US-2/US-3 "Without the right", US-3 «Последнее назначение»
Problem: Several criteria leave the observable result open:
- "refused for lack of the right" (US-2, US-3) does not name the answer. The
  page says «Доступ ограничен» (US-1); the API needs its code (403
  «Недостаточно прав» / `http.forbidden`, as for other instance routes).
- The name filter's matching is unspecified: case, `ё`/`е`, trimming,
  minimum length. US-1 types «гам» to match «Гамма», so it implies
  case-insensitive substring matching but does not say it.
- 1 000 rows in 2 s: one unpaged list, or ADR-0014's server-side paging
  envelope? The design will need the answer, and "Volumes" cannot be measured
  without it.
- «Последнее назначение» shows a date: in which time zone? The record shows
  «ДД.ММ.ГГГГ ЧЧ:мм»: the viewer's local time?
Required resolution: Name the refusal answer in US-2/US-3. In BR-RS-2, state
"substring of the name, ignoring case, `ё` = `е`, trimmed", and decide whether
the list is paged. State the time zone of both dates, as mod-016 does for
«Добавлен».
Status: resolved
Resolution: BR-RS-1 names 403 «Недостаточно прав» for the API and «Доступ
ограничен» for the page, and US-1/US-2/US-3 test both. BR-RS-2 fixes the
filter (trimmed substring, ignoring case, `ё` = `е`, empty shows all, across
pages), and US-1 has an Examples table plus «Ёлка». The list is paged at 50
rows with the total shown, and US-1 "Paging" tests it; the page size is a
point for the human (gate-1 summary). New BR-RS-12 puts both dates in the
viewer's local time zone, as mod-016 design § 3.3 does.

### R-8
Severity: minor
Affects: § Decomposition for the backlog (backend pass)
Problem: The backend pass holds the instance right, the list with derived
state over up to 500 000 memberships, the search, the appointment with its
rules and concurrency, a new record table and its migration, the record read,
the ADR and (R-4) the inventory exceptions. That is at the top of M. Option A
or B changes the load: B adds a projection change to tech-025's slice.
Required resolution: After R-1 is decided, re-check the backend pass against
≤ M. If option B is chosen, take the last-sign-in write out as its own pass (or its
own tech line amending ADR-0034), since it changes another task's slice.
Status: resolved
Resolution: Re-estimated after A. The derived administrator state over every
membership is gone, the record gains a second read, and the inventory
exceptions are named. Both passes are credibly ≤ M.

### Gate-1 summary, round 1 (superseded by round 2's)

- What the feature does: a holder of `system-admin` sees every organization and
  appoints an administrator in one that nobody can administer any more. The
  appointment is recorded in a durable record that only system administrators
  read. The system administrator gets no access to the content. No Keycloak
  write, no new client role.
- Key decisions already in the spec: any active projected user can be
  appointed, member or not (OQ-1); no self-appointment (OQ-2); a separate
  appointment record next to the membership's who/when (OQ-3).
- **Decision needed (R-1, blocker):** after tech-025 (ADR-0034) MUSTASK no
  longer learns that an account was disabled or deleted, so "no active
  administrator" never shows for H-1's case and the rescue is refused. Choose
  the trigger: **A** rescue on request in any organization, adding only, with a
  required reason (recommended; wider than the queue line), **B** rescue when
  no admin signed in for N days (changes tech-025, a heuristic), C read
  Keycloak (not recommended, against ADR-0034/ADR-0023), D keep as is (not
  recommended, the feature fires on nothing).
- R-2 depends on R-1: an appointment must be repeatable if the appointee
  turns out to be a dead account.
- Minor confirmations: the list also discloses every organization's status
  and the search discloses the instance directory to system administrators
  (R-6); add `tech-025` to deps (R-5).
- After the answer, the spec goes back to the business-analyst for R-1..R-8,
  then to a round-2 delta review.

## Round 2 — requirements (delta)

Reviewer: requirements-reviewer, 2026-11-03. Delta only: the analyst's fix
round d121cec9 (`spec.md`, US-1..US-3), re-read in full because this session
has no shell for `git diff`. Checked against the human's answers: R-1 = A; R-6
both disclosures accepted; OQ-1 and OQ-2 as recommended; OQ-3 changed (the
record is read by system administrators and by the organization's
administrators). Not re-investigated: anything round 1 already settled.

Verdict: **needs-human-decision**. R-1..R-8 are resolved (see each finding's
Resolution). There are no new blockers or majors and 2 new minors (R-9, R-10)
for the analyst, neither of which blocks gate-1. The verdict stays with the
human because the spec carries one open question (OQ-6) and one value the
human has not confirmed (page size 50).

What the fixes were checked for:
- **New BR-RS numbering** (1–12). Every cross-reference resolves: BR-RS-4..-9
  and -11 in the decomposition, BR-RS-5 for «Причина», BR-RS-12 for dates,
  BR-RS-3 (d) ↔ OQ-6. No stale reference to the old BR-RS-5 ("known active
  administrator") is left.
- **«История назначений» on «Участники»** under `organizations.membership.write`.
  That code is scope O and held by `admin` only (ADR-0029 matrix row; mod-016
  api-contract), so "its administrators" and "holders of the code" are the
  same set; no grant reaches it. US-3 covers the admin, member/reader (403),
  a foreign organization (404), and the not-found-before-403 order of ADR-0029
  § 8. Consistent with Out of scope ("not for non-admin members").
- **50-row paging.** BR-RS-2 says 50 rows with the total, the filter across
  pages, and the empty filter shows all. US-1 "Paging" (120 organizations; 3
  matches on different pages collapse to one page) is testable. NFR "a page
  within 2 s" matches.
- **«Пользователь уже администратор этой организации».** It is used
  consistently in BPMN U3, BR-RS-6, BR-RS-11, US-2 "Already an administrator"
  and "The same user twice at once", and US-3 "refused appointment leaves no
  record". Its disclosure is stated as BR-RS-3 (d) and raised as OQ-6, with a
  recommendation and an alternative. Agreed with the recommendation: the fact
  concerns only the person the system administrator named, and the
  alternative (a silent no-op) leaves the operator blind.
- **Testability of the rewritten scenarios.** Every Given can be produced
  against the product after tech-025 ("has signed in", "has never signed
  in", memberships, deactivation, concurrent requests, an injected record-write
  failure). "Members report that Борис has left" and "she cannot sign in" are
  narrative context: they set nothing up and assert nothing. They are
  harmless because the trigger is on request.
- The reference tracker is still not named. No Keycloak write and no new
  client role.

### R-9
Severity: minor
Affects: BPMN (U/C order), BR-RS-1, BR-RS-5, BR-RS-6, BR-RS-7, US-2
Problem: Several refusals can apply to one request and their order is not
stated. Examples: a system administrator who already administers the
organization appoints themselves (self, or already-administrator?); an unknown
user in an organization id that does not exist (the BPMN checks the user
before the organization, which inverts ADR-0029 § 8's habit of 404 before
validation); an empty reason together with an unknown user. The tests will pin
one order, and the design will pick another unless the spec decides. Also,
"1–500 characters" does not say what a character is. ADR-0034 counts code
points for the projection's fields; UTF-16 units would let an emoji-heavy
reason pass the client and fail the server.
Required resolution: Add the order to BR-RS-6 (or a new line): 403 «Недостаточно прав» → 404 «Организация не найдена»
→ «Причина» field errors → «Этого пользователя нельзя назначить
администратором» → «Нельзя назначить администратором себя» →
«Пользователь уже администратор этой организации». Align the BPMN with it. In
BR-RS-5, say "characters (Unicode code points)".
Status: open
Resolution:

### R-10
Severity: minor
Affects: BR-RS-6, BR-RS-8, BR-RS-11
Problem: Two edges are left open.
(a) **Races with mod-016.** Under A, an organization can have working
administrators who change memberships while an appointment runs. BR-RS-11
covers only two appointments. It does not say what happens when an
appointment raises member X while an organization administrator removes X
(is the appointment refused, or does it create a new membership?), or raises
X to admin at the same moment (refused as already administrator, or both
succeed?).
(b) **Push-era inactive rows.** BR-RS-6 accepts "any user the projection
knows". That includes rows the push feed marked disabled, deleted or absent
before tech-025, which mod-014's search hides by default ("include inactive"
= no) and mod-016's AddMember refuses ("known and active"). So such a user is
never offered, yet is accepted when named outside the panel. That is a
second, undocumented difference from mod-016.
Required resolution: (a) Add to BR-RS-11: an appointment and a concurrent
mod-016 change to the same membership are serialized. A member removed first
is appointed as a non-member (new membership); one raised first makes the
appointment refused with «Пользователь уже администратор этой организации».
Or state another outcome, but state one. (b) In BR-RS-6, either refuse a
pre-tech-025 inactive row with «Этого пользователя нельзя назначить
администратором», as mod-016 does, or say explicitly that it is accepted
(the person is let in by their next sign-in, ADR-0034 rule 1). Either way the
search and the API must agree.
Status: open
Resolution:

### Gate-1 summary (round 2, for the orchestrator to send the human)

- **What the feature does:** a holder of `system-admin` opens «Все
  организации» (every organization: name, status, last appointment date; 50
  per page, filter by name) and, on request, appoints an administrator in any
  organization. The appointment adds a new admin membership or raises an
  existing member to admin. It never removes or downgrades anyone, a
  «Причина» is required, and the system administrator gets no access to the
  content. Every appointment writes a permanent record (who, whom, when, why),
  read by system administrators and by that organization's own
  administrators on «Участники». No Keycloak write, no new client role. It
  works with tech-025's sign-in-only projection, because it detects nothing.
- **Decided by the human and now in the spec:** R-1 = A (on request, any
  organization); R-6 (status of every organization and the user directory are
  visible to system administrators); OQ-1 (any user who has signed in, member
  or not); OQ-2 (no self-appointment; another system administrator may
  appoint them); OQ-3 changed (the record has two readers).
- **For the human to decide at gate-1:**
  1. **OQ-6:** refusing «Пользователь уже администратор этой организации»
     tells the system administrator that the person they named already
     administers that organization. Recommended: accept it (it concerns only
     the named person; the record would show as much). Alternative: a silent
     no-op, which leaves the operator unable to tell what happened.
  2. **Page size 50** for «Все организации» (with the total shown). Confirm,
     or name another size.
- **Left for the analyst, not blocking:** R-9 (the order of refusals; reason
  length counted in code points) and R-10 (races with an organization
  administrator's concurrent change; push-era inactive users accepted or
  refused).
- After `gate-1: approved`, stage 3 (design) writes the owed ADR: the
  trigger, the BR-RS-3 disclosures, and the record with its second reader. It
  builds on ADR-0029 § 9 and rewords ADR-0027 rule 4 and ADR-0033 § 1.

## Round 1 — code (security pass included)

Code-reviewer, clean context, 2026-11-04, HEAD 7af90d61, feat-033's commits only
(df5346c0..7af90d61, the 929ca9f4 merge excluded). Ran: three Vitest files (41
green), one throwaway probe (deleted). Verdict: **changes-requested**. 0 blocker,
1 major (CR-1), 6 minor.

Security: clean. Rescue code reached only through `InstanceRoles` system-admin
(InstanceRoles.cs:37), never an organization bundle (OrganizationRoleBundles.cs:23);
realm role, foreign client and no-`user` tokens refused (RescueQaGapTests.cs:31-104);
403 before 404 and body; rows carry name/status/date only; reason is parameterized,
trimmed as the panel does, never logged, rendered as text; record and membership in one
SaveChanges (AppointmentRecordAtomicityTests); mod-016 races serialized on the
organization row (ChangeMemberRoleHandler.cs:84, RemoveMemberHandler.cs:71); nothing in
deploy/keycloak changes.

- **CR-1 major** `AllOrganizationsPage.tsx:150` — `count === 0 && !isPlaceholderData`
  drops the no-match frame for an empty table and a 0-row pager while a refined filter
  loads (probe: empty, table, empty). This is the reported flake at
  `AllOrganizationsPage.test.tsx:192`: under load the debounce settles mid-typing, the
  test finds the intermediate frame and loses it. Fix: decide the empty branch on
  `count === 0` from placeholder data too (or keep the frame while `isPlaceholderData`);
  add a test that refines a no-match filter and keeps the heading.
- **CR-2 minor** (QA D-11) `TablePager.tsx:34` — `disabled` drops focus to body. Fix:
  `aria-disabled` and ignore clicks while busy, or return focus to the arrow after load.
- **CR-3 minor** `GetRescueOrganizationsHandler.cs:28`, `AppointmentReadModel.cs:24` — a
  `name` holding U+0000 reaches PostgreSQL, which refuses it (a 500, not run); contract
  § 1.1 says "never an error". Fix: a filter with U+0000 matches nothing, as
  `WorkItemFilter.cs:116` does; test `?name=%00`.
- **CR-4 minor** `DialogShell.tsx:82` — 900 px is a literal a test pins; ui-kit.md § 5
  item 2 makes it a token in `tokens.ts` with a `theme.test.tsx` pin, and
  `ui-kit.md:223` repeats it against `ui-kit.md:11`. Fix: token, drop the number from the doc.
- **CR-5 minor** `AppointAdministratorHandler.cs:104` — contract § 1.3 row 8 (D-1, PK
  race to `already_member`, nothing written) has no test. Fix: a handler test whose save
  throws that conflict, asserting no record.
- **CR-6 minor** `docs/adr/0035-*.md:98` — "serialized by the membership lock": the lock
  is the organization row's, and a mod-016 add is not serialized (D-1). Fix: say so.
- **CR-7 minor** `docs/features/doc-006-org-access/spec.md:336-347` still says MVP has
  no recovery path for H-1. Fix: one line pointing to feat-033 / ADR-0035.

Claims (prose → anchor):
- plan § 10 `?page=abc` → 400: GetRescueOrganizationsEndpointTests.cs:113. `?page=`
  only at handler level (GetRescueOrganizationsHandlerTests.cs:59); binding unchecked.
- plan § 10 1000 organizations, two statements: AppointmentReadModelTests.cs:61. Holds.
- plan § 10 FK `Restrict`/23001: AppointmentReadModelTests.cs:127. Holds.
- status "D-1 trims as String.prototype.trim": AppointmentReason.cs:59-79,
  AppointmentReasonTrimParityTests. Holds.
- status "pager disabled while refreshing; range follows rows (D-9)":
  AllOrganizationsPage.tsx:104,173. Holds.
- status "page 1 when the filter settles (D-10)": AllOrganizationsPage.tsx:100. Holds.
- status "history on `wide`, reason wraps (D-4, D-5)": AppointmentHistoryDialog.tsx:69,102. Holds.
- qa pass 2 "kit additions default off": DialogShell, Cell, TablePager defaults. Holds.
- contract § 1.1 "never an error": CR-3. § 1.3 row 8: OrganizationsSaveChanges.cs:63, untested (CR-5).
- ADR-0035 § 2 "system-admin only, nothing in Keycloak": InstanceRolesTests, deploy/ untouched. Holds.
- ADR-0035 § 5 serialization: CR-6.
- Direction 2: grepped `system-admin`, `organizations.organization.create`,
  `H-1`/"no recovery path", `TablePager`/`DialogShell` across docs/, the AGENTS.md
  files and deploy/. Hits: ADR-0027/0033 wording (amended by proposed ADR-0035, as the
  ADR rules want), src/backend/AGENTS.md (role names only, true), doc-006 spec (CR-7),
  ui-kit.md (CR-4). Not swept: doc-010/doc-012 analyses citing H-1.
