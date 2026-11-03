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
Status: open
Resolution:

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
Status: open
Resolution:

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
Status: open
Resolution:

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
Status: open
Resolution:

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
Status: open
Resolution:

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
Status: open
Resolution:

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
Status: open
Resolution:

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
Status: open
Resolution:

### Gate-1 summary (for the orchestrator to send the human)

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
