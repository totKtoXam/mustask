# Review: tech-159-e2e-without-password

## Round 1 — requirements

Reviewed against: the spec's own claims checked file by file against
`src/frontend/e2e/**`, `src/frontend/playwright.config.ts`,
`scripts/keycloak-seed.sh`, `src/backend/Mustask.AppHost/AppHost.cs` (Keycloak
section), `deploy/keycloak/realms/mustask-realm.json`,
`src/backend/tests/Mustask.Contracts.Tests/KeycloakRealmRolesTests.cs`,
`docs/runbooks/stand.md` § E2E, `src/frontend/AGENTS.md` § E2E,
tech-119 `qa.md` § E2E and D-31/D-32/D-33, this task's D-1/D-2.

Factual claims that hold: 13 spec files; specs 6, 9, 11 skip without
`E2E_KEYCLOAK_ADMIN_PASSWORD`; specs 5, 6 skip without `E2E_ALLOW_DOCKER=1`;
spec 3 uses a wrong literal and never reads `E2E_PASSWORD`;
`KeycloakRealmRolesTests` guards the import file, not a live realm; the
`keycloak` container has no volume; realm `mustask` has `eventsEnabled` and
`adminEventsEnabled` (so the attributability NFR's premise is real).

### Rule → story mapping

| Rule | Covered by | Verdict |
|---|---|---|
| BR-1 (no human-supplied secret at run time) | US-1, US-6 | partial — see R-2 (the client's own credential has no delivery channel; "at run time" hides the gap) |
| BR-2 (client never in the production import) | US-3 | criterion exists but is vacuous as written — R-1 |
| BR-3 (real OIDC flow kept) | US-4 | criterion not executable — R-3; and no rule covers the specs that need *no* SSO session — R-4 |
| BR-4 (fixture credential: not typed, not in git) | **no story, no criterion** | unmapped — R-6 |
| BR-5 (fail fast on a missing/unusable client) | US-5 | ok |
| BR-6 (AppHost provisions the client **and** seeds the user) | US-2 covers the user only | the client half is unmapped — R-5 |

---

### R-1
Severity: blocker
Affects: US-3 (both scenarios), BR-2, "Out of scope" § last bullet
Problem: the guarantee the whole size-L classification rests on (D-1) is
stated over an artifact that does not exist and tested in a way that can be
green while the guarantee is false.
1. "The realm-import artifact that a production deployment of realm `mustask`
   would use" is undefined. There is exactly one such artifact today —
   `deploy/keycloak/realms/mustask-realm.json`, bind-mounted whole
   (`AppHost.cs`: `WithBindMount("../../../deploy/keycloak/realms", ...)`,
   `--import-realm`). The spec itself says no deployment pipeline exists
   (tech-060 queued). A test cannot assert over "what production would use"
   until the spec says which files are the production set and which are
   stand-only.
2. The BPMN step B says **"AppHost provisions the stand-only client"** — a
   runtime admin-API call. If the ADR picks that (the only option that works
   for one realm: `--import-realm` skips a realm that already exists, so the
   "second file merged alongside `mustask-realm.json`" option in ADR question 2
   cannot add a client to realm `mustask` at all), then US-3's test over the
   import file is green *by construction* and proves nothing about the real
   risk, which is `AppHost.cs`'s code path and the mounted directory.
3. "It finds no client holding the impersonation right" is not achievable by
   a name check, and a name check is what the realistic mistakes defeat: a
   second client added later, the realm role renamed, the asserted literal
   drifting. ADR question 3 admits this; the acceptance criterion does not.
Required resolution: in BR-2 state (a) which file set is the production
import and what marks a file stand-only, mechanically; (b) that the guard
must cover whichever provisioning mechanism the ADR picks — if the client is
created by code, the guard asserts on that code path (e.g. it exists only in
the AppHost/dev path and a test fails if the call appears outside it), not
only on JSON; (c) that the assertion is property-based and closed: no client
in the production set has a service account with, or any reference to, an
impersonation role; the allowlist of `clientId`s in the production import is
closed, so *adding any client* fails the test until someone edits it; and the
test fails if the role/attribute name it keys on is absent from Keycloak's
own vocabulary (a rename must break the test loudly, not silently pass).
Status: resolved
Resolution: US-3 rewritten. (a) names the production import as exactly
`deploy/keycloak/realms/mustask-realm.json` + `partner-realm.json`, the files
`--import-realm` mounts today, and notes `--import-realm` cannot add a
client to an existing realm via a second file. (b) scenario 3 adds an
architecture-level guard (alongside `Mustask.Architecture.Tests`) that bites on
`Mustask.AppHost`'s own admin-API code path if the ADR picks runtime
provisioning instead of a realm-import entry. (c) scenario 1 makes the
`clientId` set a closed, named allowlist (adding any client fails until the
allowlist is edited); scenario 2 keys the impersonation check on Keycloak's
own `realm-management` roles (`impersonation`, `manage-users`) and fails
loudly if either role is absent from the file's own role list, rather than
silently passing on a rename. BR-2 restated to match.

### R-2
Severity: blocker
Affects: US-1 (scenario 1), BR-1, NFR "Secrets handling", Operation
completeness (row 1, Read column)
Problem: nothing in the spec says how the suite's own process learns the
stand-only client's secret without a human or an agent handling it, and the
NFR as written reintroduces the very step the task removes. The NFR pins "an
`AddParameter(..., secret: true)` in `AppHost.cs`, set once via
`dotnet user-secrets`" — that is a **fourth** mandatory secret on a fresh
clone (`src/backend/AGENTS.md` lists three today) and, worse, the value then
lives only inside AppHost's user-secrets while `npm run e2e` is a separate
process the agent starts by hand. Somebody has to move the value into that
process's environment every run. Doing it by hand is the human step BR-1
claims to have removed; doing it by having the agent run
`dotnet user-secrets list` puts the right to sign in as anybody into the
agent's plaintext context — which the "agents may not handle passwords" rule
exists to prevent, and which is strictly more powerful than the fixture
user's password it replaces. BR-1's "at run time" is the weasel that hides
this. ADR question 3 ("generated once by AppHost like
`keycloak-panel-client-secret`") also contradicts the NFR's "set once via
user-secrets": the spec normatively fixes one answer while pretending the
question is open.
Required resolution: add a rule stating the credential channel as a
requirement: the secret is generated (not human-chosen), reaches both
Keycloak and the suite with no human action and without being printed into
any agent-readable transcript, and a fresh clone gains no new mandatory
human-set secret — or, if it must, say so and send it to the human as an open
question. Remove the prescriptive mechanism from the NFR and leave it to the
ADR, or drop ADR question 3; keep only one of the two.
Status: resolved
Resolution: NFR "Secrets handling" rewritten per D-5 as a rule with
observable consequences: the secret is generated by AppHost (never
human-chosen), reaches Keycloak and the suite through a single gitignored
stand file with no `dotnet user-secrets` step, a fresh clone gains no
fourth mandatory secret, and the value is never printed into
`e2e-results/**`, console output or a trace. BR-1 updated to match. US-1
gained a scenario asserting the grep-for-the-secret check. ADR question 3's
provisioning half is now marked closed by D-5, leaving only the
impersonation-detection half open.

### R-3
Severity: blocker
Affects: US-4 (scenario 1), BR-3
Problem: "none of that — redirect, callback, cookie issuance, renewal — is
mocked or skipped by the shortcut" is an intention, not a criterion: no test
can execute it, and it is exactly the assertion that must survive a future
change. The cheap shortcut a later author will reach for — fabricating
`mustask.session` with `context.addCookies` and skipping the panel's round trip —
leaves all 13 specs green and the suite worthless, while satisfying every
word US-4 currently writes down.
Required resolution: replace with observable criteria, e.g.: for at least one
spec the run records a navigation to
`{keycloakUrl}/realms/mustask/protocol/openid-connect/auth`, a request to
`{apiUrl}/api/session/callback`, and a `Set-Cookie: mustask.session` on *that*
response; `mustask.session` is never present in a context before the callback;
and a structural guard (a test or lint rule over `e2e/**`) fails if anything
under `e2e/` writes an `mustask.session` cookie or stubs
`/api/session/{login,callback,token}`. The injected cookie is allowed only on
Keycloak's own origin.
Status: resolved
Resolution: US-4 scenario 1 replaced with the observable criteria named
here verbatim: a recorded navigation to the authorization endpoint, a
request to `/api/session/callback`, a `Set-Cookie: mustask.session` on that
response, and the cookie's absence before it. A new scenario 2 adds the
structural guard over `e2e/**` (fails on a direct `mustask.session` write or a
stub of `/api/session/{login,callback,token}`), scoped to Keycloak's own
origin for the injected cookie, run in `scripts/verify.sh`.

### R-4
Severity: blocker
Affects: US-1 (scenario 1 "every spec ... still reaches a signed-in panel"),
US-4 (scenario 2), Context § first paragraph
Problem: the spec treats "a signed-in panel" as what every spec wants, and
names spec 3 as the single exception. Checked against the files, **four**
specs depend on the browser *not* having a Keycloak SSO session:
- spec 2 (`02-login-page.spec.ts`) — asserts the login form, theme and
  language; never signs in and never reads `E2E_PASSWORD`;
- spec 3 — the named exception;
- spec 4 (`04-sign-out.spec.ts`) — after sign-out re-opens `/dashboard` and
  asserts `#username` is visible; its whole point is that the Keycloak
  session itself ended;
- spec 10 (`10-persistent-cookie.spec.ts`) — copies `storageState` into a
  second context and asserts the dashboard renders. With an SSO cookie in
  that storage state the dashboard renders via a silent re-login even if
  `mustask.session` were gone: the spec stays green and stops testing D-23.
A per-context or `storageState`-wide injection therefore breaks 2 and 3 and
silently hollows out 4 and 10 — the same "green but worthless" failure US-4
is meant to prevent. The Context paragraph's claim that "every spec's setup
drives Keycloak's own form and types a real password" is simply not true of 2
and 3.
Required resolution: a business rule naming, per spec, which ones start from
a context with no Keycloak session (2, 3, 4 after sign-out, and 10's second
context) and which receive the impersonated cookie; plus a criterion that
spec 10 still fails if `mustask.session` is absent (assert the cookie in the
second context, not only that the page renders). Fix the Context sentence.
Status: **open** (reopened in round 2 — see R-23; the new rule contradicts
itself on spec 10 and mis-describes spec 4)
Resolution: new BR-7 names exactly which contexts start with no Keycloak
session (2, 3, spec 4's post-sign-out context, spec 10's second context)
versus which receive the impersonated cookie; US-4 gained a matching
scenario 4, including spec 10 asserting `mustask.session` itself in its second
context, not just the rendered dashboard. Context § first paragraph
corrected to name specs 2, 3 and 4 as not driving the real form the way the
rest do. — Round 2: the naming is there, but what it names is wrong for two
of the four specs; the blocker is not closed.

### R-5
Severity: major
Affects: US-2, BR-6, "Out of scope" § (`keycloak-seed.sh` accounts, the
`partner` realm), OQ-2
Problem: BR-6 promises "no human step is needed for the suite to become
runnable", and US-2's criterion is "without a human running any seed command
by hand" — while the spec simultaneously declares the `partner` realm and the
human-facing accounts out of scope. `scripts/keycloak-seed.sh` cannot be run
partially: it exits on a missing `MUSTASK_PASSWORD` **or** `PARTNER_PASSWORD`
(lines 53–56) and seeds both realms in one pass. So either AppHost supplies
both (touching `partner`, contradicting the out-of-scope bullet) or AppHost
runs something else and the existing script stays a human step for
`partneradmin`/`operator` — which contradicts neither story but leaves "the
stand comes up needing no human" false for anybody working on the partner
panel. OQ-2 asks the scope question without stating this hard dependency, so
the human is asked to choose without being told one branch is blocked.
Separately: no criterion asserts the *client* exists after a fresh start
(BR-6's other half) — US-2 covers only the user, US-5 only the absence case.
Required resolution: state in OQ-2 that the existing script is all-or-nothing
and what each branch costs; add a criterion to US-2 that after a fresh stand
start the stand-only client exists and a `client_credentials` token can be
obtained for it.
Status: resolved
Resolution: US-2 gained a scenario asserting the stand-only client exists
and a token can be obtained for it after a fresh start (BR-6's client
half), and a scenario stating explicitly that the manual script and the
automatic seed touch disjoint accounts and never call into each other, so
the script's existing `MUSTASK_PASSWORD`/`PARTNER_PASSWORD` all-or-nothing
requirement is unaffected. BR-6 and the "Out of scope" bullet restated the
same fact instead of leaving it to OQ-2.

### R-6
Severity: major
Affects: BR-4, US-2, ADR question 1
Problem: BR-4 is written with a hedge — "the fixture user's credential, **if
the seed still sets one at all**" — which means the spec does not decide
whether a password for the fixture identity exists on the stand. That is not
an implementation detail: if a password exists, "no password anywhere in the
flow" (Context § last sentence) is false — there is a password, nobody types
it — and the seed needs a value from somewhere, which re-opens R-2. BR-4 also
guards the wrong thing: "never lives in the realm-import file" is already
true today and was never at risk (users are not in the import at all,
`keycloak-seed.sh` header).
Required resolution: decide it normatively — the fixture identity is created
with no password credential, and no code path sets one (a criterion a test
can check over the seeding code / via the admin API) — or, if a credential is
unavoidable, say what creates the value and where it lives. Drop the
no-longer-load-bearing half of BR-4.
Status: resolved
Resolution: BR-4 rewritten without the hedge: the fixture user is created
with no password credential and no code path ever sets one; the
"never lives in the realm-import file" clause dropped as never at risk.
US-2 gained a dedicated scenario a test can check over the admin API.

### R-7
Severity: major
Affects: OQ-1, US-1, US-6
Problem: OQ-1 asks *who* the fixture identity is without stating the
constraints any answer must satisfy, so a "dedicated identity" answer silently
breaks specs. Measured from the files: spec 6 needs the identity to hold
`workitems.items.write` (otherwise "Создать задачу" is not rendered —
its own header comment says so), specs 1/9/10 need a rights set that renders
`/work-items` and `/dashboard`, and spec 11 ends sessions *by username*
(`logoutUserSessions(..., env().user)`), so the impersonated identity and
`E2E_USER` must be the same string.
Required resolution: add the constraints to OQ-1 (or to a business rule): the
fixture identity carries at least the rights today's `mustask` account has via
`/Администраторы`, `E2E_USER` resolves to it, and a manual
`keycloak-seed.sh` run must not be able to change it out from under an
unattended run.
Status: resolved
Resolution: new BR-8 states exactly these constraints (rights equivalent
to `/Администраторы` including `workitems.items.write`, `E2E_USER`
resolves to the identity, a manual seed run cannot change it); US-2 gained
a matching scenario.

### R-8
Severity: major
Affects: NFR "No new network exposure"
Problem: the NFR asserts a boundary that does not exist. It claims the client
and its credential are "reachable only from the machine running the
stand/suite, matching the existing local-only trust boundary". Keycloak's
endpoint is deliberately bound to every interface (`AppHost.cs`:
`endpoint.IsProxied = false; endpoint.TargetHost = AnyInterface`), and the
runbook's own § "Access from the local network" says the opposite of
"local-only" — everything listens on `0.0.0.0`, and the stand "shouldn't be
brought up on an untrusted network". So a client able to sign in as anybody
becomes reachable to anyone on the LAN who has the secret. Stating a false
boundary as an NFR is worse than stating none: the ADR reviewer will take it
as given.
Required resolution: restate the requirement over what actually holds — the
secret is never in git, never in a log or `e2e-results/`, never in a browser
context; the client is usable only against the stand's own realm `mustask`; and
the existing warning about untrusted networks applies with more force now.
Add it to the runbook's list of reasons not to raise the stand on an
untrusted network.
Status: resolved
Resolution: NFR renamed "Network exposure" (dropping the false "no new")
and restated over what actually holds — secret never in git/log/
e2e-results/browser context, usable only against realm `mustask`, and the
existing untrusted-network warning applies with more force. US-8 gained a
scenario adding this to the runbook's list of reasons.

### R-9
Severity: major
Affects: NFR "Attributability"
Problem: the requirement ends in "the ADR must confirm the impersonation
mechanism it picks does not bypass that log" — an intention delegated to a
document, with no observable. Nothing fails if the evidence is absent.
Required resolution: name the observable and who checks it: after an
unattended run, realm `mustask`'s event log contains an impersonation/admin event
attributed to the stand-only client's service account, retrievable through
the admin API; checked once at QA and recorded in `qa.md`. (The premise
holds: `mustask-realm.json` has `eventsEnabled` and `adminEventsEnabled` with a
7-day expiry.)
Status: resolved
Resolution: NFR "Attributability" rewritten with exactly this observable —
an impersonation/admin event in realm `mustask`'s event log after an
unattended run, retrievable via the admin API, checked once at QA and
recorded in `qa.md`; a mechanism bypassing the log now fails the NFR, not
just an ADR footnote.

### R-10
Severity: major
Affects: the spec as a whole (no story covers documentation / process),
"Out of scope"
Problem: the task makes false several documents that agents read as
normative, and the spec requires updating none of them: `src/frontend/AGENTS.md`
§ E2E ("**an AI agent never runs this suite** ... A human runs it", plus the
`E2E_*` table), `docs/runbooks/stand.md` § E2E test suite ("**no agent
runs it**", the PowerShell `$env:E2E_PASSWORD` recipe),
`docs/ai-workflow/stages/04-backend.md` and `05-frontend.md` (a "yes" to the
E2E question means "e2e is green **per the human's run**"), and the comments
in `playwright.config.ts`, `e2e/support/auth.ts`, `e2e/support/env.ts`. tech-119
D-32 says explicitly that the process instructions are edited together with
the harness ("Инструкции процесса правятся в этой задаче вместе с появлением
обвязки") — this is that moment. The spec's silence is a decision taken
without the human, and D-2 ("scope is exactly the queue line") is the reason
it needs to be asked rather than assumed either way.
Required resolution: either a story/criterion listing the documents that must
stop saying "a human runs it" (and the process-instruction lines that must
accept an agent's own run), or an open question for gate-1 asking whether
that editing is in scope. Not nothing.
Status: resolved
Resolution: new US-8 lists exactly these documents as acceptance criteria
(`src/frontend/AGENTS.md` § E2E, the runbook's § E2E test suite plus its
network-access list, and the "green per the human's run" wording in stages
04/05); new BR-9 states the rule.

### R-11
Severity: major
Affects: US-1, NFR "No material slowdown", the task's Definition of Done
Problem: (a) nothing in the spec requires the outcome the task exists for to
be *measured once*. US-1's criterion is negative ("does not fail with
'E2E_PASSWORD is not set'") and "every spec ... still reaches a signed-in
panel" — a run where four specs skip satisfies its letter. `status.md` § Plan
has "QA frontend + a real E2E run", but the spec is the normative artifact.
(b) The NFR's baseline is wrong: it calls 5 min 35 s "today's measured human
run" and implies the maximum-coverage run is new, but tech-119 `qa.md` § E2E
records that run as **13 passed, 0 failed** — i.e. already with
`E2E_ALLOW_DOCKER=1` and the admin secret. The headroom argument is built on
a premise that does not hold. (c) "well under 15 minutes" is not measurable;
15 minutes is.
Required resolution: add an acceptance criterion — one unattended run by the
agent, no `E2E_PASSWORD` and no `E2E_KEYCLOAK_ADMIN_PASSWORD` in the
environment, 13 passed / 0 failed / 0 skipped (or the skip list named and
justified), wall time recorded in `qa.md`. Fix the baseline sentence and
replace "well under 15 minutes" with the bound. This does not touch D-2: it
is this task's own acceptance, not a standing pre-review rule.
Status: resolved
Resolution: US-1 gained scenario 3 (one measured unattended run, no
`E2E_PASSWORD`/`E2E_KEYCLOAK_ADMIN_PASSWORD`, 13/0/0 or a named skip list,
wall time in `qa.md`, ≤15 minutes). NFR "No material slowdown" rewritten to
state the correct baseline — tech-119's `qa.md` already recorded 13/0 at
5:35 under a human's run with both secrets set — so this task's job is
automating that coverage, not beating a new one.

### R-12
Severity: major
Affects: US-7 (scenario 1), US-6, BPMN (no terminal state), AGENTS.md
§ Hard rules (declaring or stopping processes you start)
Problem: the spec assumes the existing `afterEach`/`afterAll` hooks are a
sufficient guarantee for an unattended run. They are not, and unattended is
exactly when it matters. Spec 6 sets `accessTokenLifespan` to 90 and restores
900 in `afterEach`; specs 9/11 restore in `afterAll`; specs 5/6 stop the
`keycloak` container and start it back in `afterEach`. None of those hooks
runs if the run is aborted, killed, or dies on a global failure — and with no
human watching, the stand is then left with a 90-second token lifespan or a
stopped Keycloak, and the *next* run behaves differently for reasons nothing
reports. `KeycloakRealmRolesTests` guards the value in the import file, not
the live realm, so nothing catches it either. Separately, the BPMN ends at "a
JSON report on disk": nobody owns the stand the agent started, although
AGENTS.md requires an agent to stop what it starts or declare it.
Required resolution: a rule requiring the run to reconcile the stand's state
before it starts (realm settings at their import values, the `keycloak`
container running) and to report the discrepancy it fixed; and a criterion
stating what happens to the stand after an unattended run — stopped by the
agent, or declared in the agent's answer with the command to stop it.
Status: resolved
Resolution: new US-9: global setup reconciles `accessTokenLifespan` and the
`keycloak` container to their expected state before any spec runs and
reports what it fixed; a second scenario requires the agent to stop the
stand it started or declare it running with the stop command. BPMN gained
the reconciliation step and a terminal teardown/declare branch.

### R-13
Severity: major
Affects: US-7 (both scenarios), OQ-3, OQ-4
Problem: US-7 has no deliverable and its criterion cannot hold. It asserts
behaviour that already exists (specs 5/6 run whenever `E2E_ALLOW_DOCKER=1` is
set, and already restart the container in `afterEach`) — the only thing
blocking an agent is permission, which is OQ-3. Meanwhile its load-bearing
words, "stop and restart **only that stand's own** `keycloak` container",
cannot be satisfied by today's resolver:
`e2e/support/docker.ts:80` runs `docker ps -a --filter name=keycloak
--format {{.Names}}` and takes the **first** match — `-a`, so it also matches
*stopped* containers left by earlier or sibling stands, in no defined order.
That is OQ-4, which the spec offers the human as a possibly-accepted
pre-existing limit. It is not pre-existing in effect: today a human runs the
suite against one stand, while an unattended agent is precisely the case
where two worktrees have stands up at once. US-7 therefore silently depends
on OQ-4 being answered "in scope".
Required resolution: make the dependency explicit — if specs 5/6 are to run
unattended, container resolution must be scoped to the stand under test
(a criterion: the resolver selects exactly one running container belonging to
the stand whose Keycloak the suite is talking to, and fails with one message
on ambiguity), and OQ-4's "accepted limit" branch must be shown to exclude
US-7. Strip US-7 of criteria that only restate existing behaviour and state
what actually changes (the written permission plus the scoping).
Status: resolved
Resolution: US-7 rewritten: a new first scenario requires the container
resolver to select exactly one container scoped to the stand under test
(matched by `E2E_KEYCLOAK_URL`) and fail loudly on ambiguity; the
restart/skip scenarios now depend on that. OQ-3/OQ-4 both resolved by D-6 —
narrowing is a requirement, not a question — reflected in "Open questions".

### R-14
Severity: minor
Affects: US-1 (scenario 2)
Problem: "none of them contains a plaintext human password" is a negative
over a string the run no longer has, so no test can be written for it, and it
guards the wrong secret. After this task the interesting leak is the
stand-only client's secret — the right to sign in as anybody — which *can*
appear in a Playwright trace if the token request is ever made from a browser
context.
Required resolution: reword as: the client-credentials request is made from
the Node side only, never from a browser context; and `e2e-results/**`
(report, console output, retained traces) contains no occurrence of the
client secret — a grep a QA step can actually run.
Status: resolved
Resolution: US-1 scenario 2 reworded exactly this way: the client-
credentials/impersonation request is Node-side only, never from a browser
context, and a grep of `e2e-results/**`/console/traces for the secret must
find nothing.

### R-15
Severity: minor
Affects: US-4 (scenario 2), OQ-1
Problem: realm `mustask` has `bruteForceProtected: true` (`mustask-realm.json:17`),
and spec 3 submits a wrong password **against `env().user`** — the same
identity the suite signs in as. One failure per run is harmless; an
unattended suite run in a loop accumulates them on the fixture identity, and
a temporary lockout is a failure mode no one will read as "brute-force
counter".
Required resolution: state the mitigation — spec 3 targets a username that is
not the fixture identity (the spec asserts only that Keycloak rejects the
attempt), or the run resets the failure counter for the fixture user.
Status: resolved
Resolution: US-4's spec-3 scenario now states the mitigation: the wrong
password is submitted against a username that is not the fixture identity,
so `bruteForceProtected`'s counter never accumulates on the account an
unattended run repeatedly needs.

### R-16
Severity: minor
Affects: Roles § ("Human — ... approves the ADR this task produces"),
`status.md` § Gates
Problem: the spec promises a human approval of the ADR, but the gate list is
`gate-1, gate-3` and nothing in the spec or the plan makes the ADR's
mechanism a gated decision. As written the security mechanism is first seen
by the human at the final code review, after both halves are built.
Required resolution: either name gate-3 as the place the ADR is approved and
drop the implication of a separate approval, or require the ADR to be put to
the human before the frontend stage starts.
Status: resolved
Resolution: Roles § Human bullet reworded — the ADR is reviewed as part of
gate-3 (this route's only gates), dropping the implication of a separate
approval step.

### R-17
Severity: minor
Affects: Decomposition §
Problem: the table's single row restates the task at size L, which is not a
decomposition and leaves the reviewer's size check ("are the backlog tasks
really ≤ M") unanswerable. The reasoning for not splitting is sound and
matches tech-119's precedent; the problem is only that the row claims to be a
decomposition.
Required resolution: say plainly that the task is not decomposed into backlog
lines and that its two halves are stages with their own plan files — which
the prose already says — rather than presenting an L row as the result of a
decomposition.
Status: resolved
Resolution: added a lead-in sentence before the table stating plainly that
the task is not decomposed and the row restates the filed task, not a
split.

---

## Verdict

`needs-human-decision`.

Four blockers (R-1..R-4) and nine majors return to the business analyst;
the five open questions plus the two the spec should have asked (R-10 on
editing the process instructions and the runbook, R-2 on whether a fresh
clone may gain a fourth mandatory human-set secret) need the human at gate-1.

Open questions as posed:

- **Real human decisions:** OQ-1 (which identity — but add the constraints of
  R-7), OQ-2 (how far the AppHost seed reaches — but state the
  all-or-nothing dependency of R-5), OQ-3 (whether an agent may control the
  stand's Docker container at all — a policy about the host, not a design
  detail), OQ-5 (removing a documented interface a human may still use).
- **Using an open question to avoid specifying:** OQ-4. US-7's own criterion
  ("only that stand's own container") requires the narrowing, and the
  "accepted pre-existing limit" branch is only acceptable if specs 5/6 stay
  human-run — see R-13.
- **Decisions taken silently that should have been open questions:** whether
  `src/frontend/AGENTS.md`, `docs/runbooks/stand.md` and the stage
  instructions 04/05 are edited in this task (R-10); whether the fixture
  identity has a password credential at all (R-6 — BR-4 hedges instead of
  deciding); whether a fresh clone gains a fourth mandatory secret (R-2);
  who tears the stand down after an unattended run (R-12).

---

## Round 2 — delta

Scope of this round: the 17 `Resolution:` lines against what `spec.md` now
says, and the material that did not exist in round 1 — US-8, US-9, BR-7/8/9,
the new US-2/US-3/US-4/US-7 scenarios, the two new BPMN branches, the three
rewritten NFRs, the closed "Open questions" section. D-3..D-7 are binding and
are not re-opened anywhere below.

**Resolutions that hold as written:** R-2, R-3, R-5, R-6, R-7, R-8, R-9,
R-10, R-11, R-12, R-13, R-14, R-15, R-16, R-17 — checked against the spec
text, not against the resolution's claim. Two of them are worth naming
because they were the ones most likely to be closed with new words: R-3's
US-4 scenarios 1–2 are now observations a run can record (a navigation to the
authorization endpoint, a request to `/api/session/callback`, `Set-Cookie:
mustask.session` on *that* response, the cookie absent before it) plus a
structural guard over `e2e/**` — executable; and R-13's new US-7 scenario 1
ties container resolution to the stand the suite's own `E2E_KEYCLOAK_URL`
addresses, which is mechanically checkable.

**R-1 holds in substance** — the production import is named as the two files
`--import-realm` mounts, the allowlist is closed, and scenario 3 follows the
guard to `Mustask.AppHost`'s code path if the ADR picks runtime provisioning
(`Mustask.Architecture.Tests` does exist, so the scenario names a real home). One
sub-clause of it is unexecutable — R-18 below, raised separately rather than
re-opening the blocker.

**R-4 is re-opened** — see R-23.

### R-18
Severity: major
Affects: US-3 scenario 2 (second `And`), BR-2
Problem: the rename-proofing clause cannot be implemented as written. It says
the test must fail loudly if Keycloak renames or removes `impersonation` /
`manage-users`, detecting that "the role it looks for is absent from the
file's own `realm-management` client-role list". There is no such list:
`deploy/keycloak/realms/mustask-realm.json` declares exactly two clients
(`mustask-panel`, `mustask-api`) and `partner-realm.json` two (`partner-ui`,
`partner-api`); the `realm-management` client is created by Keycloak itself
and appears in no import file (grep for `realm-management` over
`deploy/keycloak/realms/` returns nothing). A test keyed on that list would
either fail permanently or — if written defensively — pass vacuously, which
is the exact failure mode the clause exists to prevent.
Required resolution: key the rename detection on something that exists. Two
workable shapes: (a) assert positively over the import files that **no**
client has `serviceAccountsEnabled: true` at all (today true of all four) —
a property that needs no role vocabulary and breaks the moment a service
account appears, whatever the role is called; or (b) if the role names must
be checked, check them against the live realm at QA (the impersonation role
list is retrievable through the admin API) and say so, instead of against a
file that does not contain them.
Status: resolved
Resolution: US-3 scenario 2 rewritten to option (a): asserts no client in
the closed allowlist has `serviceAccountsEnabled: true` (verified true
today of all four — `mustask-panel`, `mustask-api`, `partner-ui`, `partner-api`,
checked against both realm-import files), needing no role vocabulary. ADR
question 3 reworded to point at this property first and, if a sharper
role-specific test is wanted, to the *live* realm at QA rather than the
import files (which do not declare `realm-management` at all).

### R-19
Severity: major
Affects: US-7 scenario 2 (last `Then`), US-9 scenario 1
Problem: "leave it running at the end regardless of whether the spec passed,
failed, **or the run was aborted mid-spec**" is not achievable by the run
itself. A hard abort — the worker killed, the process signalled, a crash — is
precisely the case in which no `afterEach` and no teardown executes; that is
why R-12 asked for reconciliation at the *next* run's start, and why US-9
scenario 1 now provides it. As worded, US-7 demands an impossible guarantee,
and the implementer will either satisfy it on paper or leave a criterion that
fails QA for a reason nobody can fix. D-6's intent ("restored even when a run
is aborted") is met by US-9 — the defect is where the criterion is placed, not
what the human decided.
Required resolution: split the guarantee by case in US-7 scenario 2: the
run's own hooks restore the container when a spec passes or fails; a graceful
interruption (Ctrl-C / SIGINT) is handled if the harness can; and an abort
that runs no hook is recovered by US-9 scenario 1's reconciliation at the next
start — cross-reference it instead of claiming the run covers it.
Status: resolved
Resolution: US-7 scenario 2 split exactly by case: `afterEach` restores the
container on pass/fail (unchanged guarantee); a hard abort running no hook
is explicitly *not* claimed as recovered by this run, cross-referenced to
US-9 scenario 1's reconciliation at the next start instead. BPMN node N's
label reworded to match (restores on pass/fail; abort recovered at step E).

### R-20
Severity: major
Affects: BR-1, NFR "Secrets handling", US-1 scenario 2, US-2 scenario 1,
Operation completeness (row 1, Read column)
Problem: "a gitignored stand file" is the only link between the two halves of
this task, and the spec never says which file. The backend stage writes it
(`plan-backend.md`), the frontend stage reads it (`plan-frontend.md`), and
the two stages have separate plans and separate QA — an unnamed path is how
that integration breaks. Worse, "gitignored" is asserted, not required: a
file is ignored only if a pattern covers it, and nothing here requires the
`.gitignore` entry or a check that the file is in fact untracked. The secret
being committed is exactly the failure this whole NFR exists to prevent.
A second, smaller inaccuracy in the same rule: the secret does not reach
*Keycloak* through that file — AppHost hands it to the container directly, the
way `MUSTASK_PANEL_CLIENT_SECRET` already works.
Required resolution: name the path (or require the two plan files to fix one
name and the spec to state it), require a `.gitignore` entry covering it as
an acceptance criterion with a check that the file is untracked after a stand
start, and reword the "reaches both Keycloak and the suite through a
gitignored stand file" half of BR-1 so it describes what actually carries the
value to each side.
Status: resolved
Resolution: BR-1 rewritten to require the two plan files to fix and repeat
verbatim one file path (the spec doesn't invent the literal name itself —
that's `plan-backend.md`'s/`plan-frontend.md`'s job to agree on, per the
reviewer's parenthetical option), and to require a `.gitignore` pattern
covering it plus a post-start untracked check, both now acceptance criteria
(new US-2 scenario "The stand file is named once and stays untracked").
Fixed the Keycloak-delivery inaccuracy: AppHost hands the secret to the
container directly (like `MUSTASK_PANEL_CLIENT_SECRET`), the file is for the
suite's side only. NFR "Secrets handling" and the Operation-completeness
row reworded to match.

### R-21
Severity: major
Affects: US-2 scenario 3, BR-4
Problem: the new criterion asserts the fixture user's group membership and
the absence of a password credential, and stops there. The one non-obvious
precondition for that identity to be usable at all is missing, and it is
documented as a live, already-paid-for lesson in
`scripts/keycloak-seed.sh` (header of `seed_user`): realm `mustask`'s declarative
user profile requires `email`, `firstName` and `lastName`, and a user without
them is created and looks fine but cannot complete a login ("Account is not
fully set up", verified on the stand). A fixture user seeded with username and
group only will very plausibly reproduce that — and the failure will surface
as thirteen red specs at the frontend stage, not as a seeding defect.
Required resolution: extend US-2 scenario 3 (or BR-4) to require the fixture
identity to be fully set up in the realm's own terms — `email`,
`firstName`, `lastName`, `enabled`, `emailVerified`, no pending required
actions — and to require the seed to be the thing that guarantees it.
Status: resolved
Resolution: US-2 scenario 3 extended with exactly these fields (`enabled`,
`emailVerified`, `email`, `firstName`, `lastName`, no pending required
actions), citing `keycloak-seed.sh`'s own "Account is not fully set up"
lesson, and requiring the seed itself to guarantee it.

### R-22
Severity: minor
Affects: BR-9, US-8
Problem: BR-9 lists the documents this task falsifies but not the place the
claim is actually written for the suite's own code. `playwright.config.ts`,
`e2e/support/auth.ts`, `e2e/support/env.ts` and `e2e/support/keycloakAdmin.ts`
each state the human-only constraint in a header comment and cite
`brief-e2e-playwright.md` (and `e2e/README`) as the normative source — neither
of which exists anywhere in the repository (checked). So the comments that
will be left saying "never run by anyone but the human" point at nothing, and
nobody is required to fix them.
Required resolution: add those four files' header comments to BR-9's list (the
frontend stage touches them anyway), and state that the dangling
`brief-e2e-playwright.md` / `e2e/README` references are replaced with a live
one — `src/frontend/AGENTS.md` § E2E — rather than reproduced.
Status: resolved
Resolution: BR-9 and a new US-8 scenario now list the header comments to
fix. Correction to the finding's own file list, checked by grep against
`src/frontend/e2e/**` and `vite.config.ts`: `e2e/support/keycloakAdmin.ts`
does **not** cite `brief-e2e-playwright.md` or the human-only constraint —
its header is only about get-modify-put semantics — so it does not belong
on this list; `e2e/global-setup.ts` and `src/frontend/vite.config.ts` do
cite `brief-e2e-playwright.md` and were left off the finding's list. BR-9
now names the verified set: `playwright.config.ts`, `e2e/support/auth.ts`,
`e2e/support/env.ts`, `e2e/global-setup.ts`, and the `e2e/**` exclusion
comment in `vite.config.ts`.

### R-23
Severity: blocker
Affects: BR-7, US-4 scenario 4 ("Specs that need no Keycloak session start
from a clean context") — the fix for R-4
Problem: the rule I asked for exists, but what it names is wrong for two of
its four specs, and in one case it mandates breaking the spec.
1. **Spec 10 — self-contradictory, and the first half is fatal.** The
   criterion says spec 10's second context starts "with no Keycloak session
   **and no `mustask.session` cookie**", and the very next `And` says spec 10
   "additionally asserts `mustask.session` is present in its second context".
   Both cannot hold. The second is right: `10-persistent-cookie.spec.ts`
   builds the second context *from the first one's `storageState`*, and the
   whole point (D-23) is that `mustask.session` survives. An implementer
   following the first half strips the cookie and the spec proves nothing.
   What spec 10 actually needs is the opposite pairing: `mustask.session`
   carried over, the Keycloak SSO cookie **not** carried over — only then
   does a rendered dashboard prove the session cookie rather than a silent
   SSO re-login.
2. **Spec 4 — "spec 4's post-sign-out context" describes something that does
   not exist.** `04-sign-out.spec.ts` has one context: it signs in, signs
   out, and re-opens `/dashboard` in the same page. Its proof is that the
   *same* browser, with the same cookie jar, no longer has a usable Keycloak
   session — because sign-out ended it server-side. A criterion phrased as a
   context that "starts with no Keycloak session" invites exactly the
   implementation that destroys the spec: a fresh context after sign-out,
   which shows the login form unconditionally and asserts nothing about
   sign-out at all.
Required resolution: restate BR-7 and the scenario per spec:
specs 2 and 3 — a context with neither the Keycloak SSO cookie nor
`mustask.session`; spec 4 — signed in as usual, and after its own sign-out it
keeps the same context (no re-creation, no cookie clearing), so the login
form it then sees is evidence the Keycloak session ended; spec 10 — first
context signed in, second context built from its storage state with
`mustask.session` present and the Keycloak SSO cookie removed, asserting
`mustask.session` is there. Everything else receives the impersonated cookie.
Status: resolved
Resolution: BR-7 and US-4's per-spec scenario rewritten exactly along the
required pairing — specs 2/3: neither cookie; spec 4: same context across
sign-in/sign-out/re-open, no re-creation, no cookie clearing; spec 10:
second context built from the first's storage state with `mustask.session`
carried over and the Keycloak SSO cookie removed, asserting `mustask.session`
is present. Split into three scenarios (one per case) rather than one
scenario naming all four specs, so each pairing is unambiguous on its own.

---

## Round 2 verdict

`needs-fixes`.

One blocker re-opened (R-23, the R-4 fix contradicts itself on spec 10 and
mis-describes spec 4 — in its current form it instructs the implementer to
hollow out the two specs it was written to protect), four new findings from
the new material (R-18, R-19, R-20, R-21 major; R-22 minor). Fifteen of the
seventeen round-1 resolutions hold as written, R-1 holds except for the
sub-clause in R-18, and nothing new for the human appeared — D-3..D-7 answer
everything the spec now asks, and the "Open questions" section is correctly
empty.

Three of the five new findings are the same species: a criterion whose
wording, taken literally by an implementer, produces the opposite of what it
guards (R-23 on specs 4 and 10), cannot be executed at all (R-18's
`realm-management` list, R-19's abort case), or leaves the only interface
between the two stages unnamed (R-20). None needs a decision — they need the
same precision the rest of the spec now has.

On length: 593 lines is criteria density, not scope. The growth since round 1
is US-8/US-9 (documents and stand state, both demanded by this review), the
per-spec context rule, the closed-allowlist guard, and the one measured run —
all of them criteria for the single piece of wiring D-33 already fixed, none
of them a second deliverable. The split the ~400-line guidance exists to
catch would be "two things in one queue line"; here the backend and frontend
halves still cannot ship apart (a client nothing calls, or a suite with no
client), which is the same call tech-119 made. The honest reading is that an
L task whose subject is a security boundary costs more criteria per line of
code than a feature task, and that is the shape this spec has.


## Round 1 — code (backend + frontend)

Stage 8, one pass over the whole branch diff against `main` (55 files,
+6200/−282), by a session that wrote none of it. Read before the diff:
`spec.md` + all nine stories, `status.md` (D-1..D-9 taken as binding),
ADR-0023, `plan-backend.md` §§5/7/9/10–12, `plan-frontend.md`, `qa.md`
(backend rounds 1–2, frontend phases 1–2), and rounds 1–2 above. No stand
was started; nothing in the worktree was modified except this file.

Verdict: **needs-fixes** — 0 blockers, 2 majors, 6 minors. Neither major is
in the mechanism itself; both are in guards and repairs that the artifacts
claim work and that, as written, cannot fire in the case they name.

### What holds (checked, not assumed)

- **The secret's whole life is clean.** Generated by
  `RandomNumberGenerator.GetHexString(40)` (160 bits) in a `Lazy<string>`
  constructed once per process (`AppHost.cs:199`, outside the event
  handler); handed to Keycloak in the client-creation/update *body*, never
  as a container env var or an Aspire parameter, so it is absent from the
  dashboard's environment view; written to exactly one file, which
  `.gitignore` covers by an anchored `/.e2e-stand/` pattern that resolves to
  the same directory from both sides (`AppHost.cs:26` ↔ `standFile.ts:29`).
  Every throw site on both sides was read: the .NET side surfaces only
  `EnsureSuccessStatusCode`'s status-only message, the Node side
  interpolates status/body/path and never `clientSecret`; the one
  `console.warn` (`standFile.ts:238`) logs a Keycloak 401 body, which
  carries no credential. Nothing hands the secret to a browser context.
- **The suite still proves the round trip.** `signIn` injects only
  `KEYCLOAK_*` cookies, on Keycloak's own hostname and realm path
  (`impersonation.ts:161–172`), then waits for the real
  `/api/session/callback` *response* rather than for "origin === panel" —
  the race described in `auth.ts` is a real one and the fix is the right
  one. Spec 1 observes the three facts US-4 scenario 1 names and the absence
  of `mustask.session` beforehand; broken OIDC wiring cannot pass it, and broken
  silent SSO cannot pass `signIn` at all (the callback would never arrive).
  BR-7's per-spec rule is implemented exactly as written: specs 2 and 3
  never call `signIn` and get Playwright's fresh context; spec 4 uses one
  `page` end to end (`04-sign-out.spec.ts:16–24`); spec 10 strips
  `KEYCLOAK_*` from the carried storage state and asserts `mustask.session` is
  present in context 2, not merely that the dashboard renders.
- **Convergence of the repeat-firing path.** Client, roles, fixture user,
  group membership, realm lifespan and the stand file are all
  create-or-update or set-idempotent; the realm write is get-modify-put, not
  a partial PUT. `ProvisionAsync_CalledTwiceWithinOneRun_...` exercises the
  two-firings shape at the provisioner level.
- **`mustask-e2e`'s rights stay minimal** and are guarded by name
  (`ProvisionAsync_NeverGrantsMoreThanImpersonationAndManageUsers`), which
  is the right shape for D-8's condition. `directAccessGrantsEnabled`,
  `standardFlowEnabled` and `implicitFlowEnabled` are all explicitly
  `false` — the client can only ever be used as a service account.
- **The documents are true.** `brief-e2e-playwright.md` / `e2e/README` have
  no surviving reference anywhere outside this feature's own history
  (repo-wide grep); `E2E_PASSWORD`/`E2E_KEYCLOAK_ADMIN_PASSWORD` survive
  only as "this is gone" prose; the 120s-not-900s deviation is stated in
  both `src/frontend/AGENTS.md` and the runbook; the stage files no longer
  gate a "yes" on a human's run. The one drift found is F4 below.

### F1 (major) — the stand reconciliation US-9 promises cannot run in the case it was written for

`src/frontend/e2e/global-setup.ts:25–39`, with
`src/frontend/e2e/support/reconcile.ts:24–33`.

Global setup does, in order: panel reachable → `Mustask.Api /alive` reachable →
**`{keycloakUrl}/realms/mustask` reachable** (line 29) → `loadStandFile()` (line
34, which itself performs a `client_credentials` grant against Keycloak) →
`reconcileStand()` (line 39). The only state `reconcileStand` repairs is a
`keycloak` container that is not `running`. A stopped container means the
realm endpoint refuses the connection, so line 29 throws
`The Keycloak realm 'mustask' is not reachable at ... Is the stand up?` and the
run dies before line 39 is ever reached. The repair is reachable only in the
one state that does not need it — a container that is not `running` yet
still serving HTTP.

That this is the real state after an abort is not a guess: specs 5 and 6
depend on DCP *not* restarting the container behind them (they hold Keycloak
down for tens of seconds and assert against that), so a run killed
mid-outage leaves it stopped with the AppHost still up.

Why it matters: US-9 scenario 1 is the story that makes an unattended run
recoverable without a human, and D-6's second acceptance condition
("restored even when a run is aborted") is met, per US-7's own text, *by
US-9, not by the specs' `afterEach`*. As shipped, an aborted run ends with a
stand that needs a manual `docker start`, and the message an agent gets
points it at the wrong thing ("Is the stand up?" — it is). Three documents
now assert the repair happens (`spec.md`'s BPMN step E,
`docs/runbooks/stand.md` § E2E test suite, `src/frontend/AGENTS.md`
§ E2E), so the dead code is worse than an absent feature: it is a promise
the next agent will rely on. QA phase 1 marked this path "judged sound by
code reading only" and listed a live reproduction as pending; phase 2 did
not perform it, which is how it survived.

Fix: reconcile before the reachability checks, not after — but note that
`reconcileKeycloakContainer` currently calls `loadStandFile()`, which cannot
succeed while Keycloak is down (it verifies the secret with a live token
grant). So the repair needs the container name from the file *without* the
credential verification (split `waitForStandFile`/`parseStandFile` from
`verifyClientSecret`), or the realm `checkReachable` has to catch its own
failure, attempt the container repair, and retry once before giving up.
Either shape, the live reproduction QA deferred should be the acceptance
evidence: stop the container, run `npm run e2e`, watch global setup start it
and say so.

Status: resolved
Resolution: took the first shape named in the fix. `standFile.ts` gained
`peekStandFile()` — a single, non-polling read+parse of the stand file with
no secret verification and no wait for the file to appear (returns
`undefined` if it doesn't exist yet); `reconcile.ts`'s
`reconcileKeycloakContainer` now calls that instead of `loadStandFile()`,
and no-ops (nothing to repair) when the file is absent. `global-setup.ts`
now runs, in order: panel reachable → `Mustask.Api /alive` reachable →
`reconcileStand()` → `{keycloakUrl}/realms/{realm}` reachable →
`loadStandFile()` (the credential-verifying read, unchanged) — the repair
sits exactly where a stopped container can still be found and fixed before
the check that a stopped container would otherwise fail. The stale
`global-setup.ts` comment naming `accessTokenLifespan` as something this
reconciles (F4) is dropped in the same edit, since the reordering touched
the same lines.

Live-verified both directions, on the shared dev stand
(`mustask-e2e-keycloak-5010`, port 5010), reproducible with `docker
stop`/`start` and `npx playwright test e2e/specs/02-login-page.spec.ts`
(chosen for speed — it exercises the same `global-setup.ts` every spec
does, no sign-in needed):

1. **Repair fires and the run proceeds.** `docker stop
   mustask-e2e-keycloak-5010` (confirmed `Exited (143)`), then `npx playwright
   test e2e/specs/02-login-page.spec.ts`. Console: `e2e global setup:
   container 'mustask-e2e-keycloak-5010' was 'exited', started it (an earlier
   run likely aborted mid-outage, e.g. specs 5/6).`, followed by `1 passed`.
   `docker ps` afterward showed the container `Up`. This is the exact case
   F1 named as unreachable before the fix — reproduced live, not read.
2. **No stand still fails loudly, not silently "repaired".** To test this
   without touching the shared stand's own AppHost/panel/API (a backend
   agent was working in the same worktree at the time), pointed
   `E2E_PANEL_URL`/`E2E_API_URL` at two disposable local HTTP listeners (so
   the first two checks pass the way they would against a real panel/API),
   moved the real `.e2e-stand/keycloak-e2e-client.json` aside (simulating no
   stand file — reconcile has nothing to find, matching a stand that was
   never started), and set `E2E_KEYCLOAK_URL` to a port nothing listens on.
   Result: `Error: The Keycloak realm 'mustask' is not reachable at
   http://127.0.0.1:59999/realms/mustask. Is the stand up (dotnet run --project
   src/backend/Mustask.AppHost)? (fetch failed)` — the same message the
   unpatched code gave for the *wrong* case, now correctly reserved for the
   case it actually names. `reconcileStand()` logged nothing (no container
   to act on) and did not mask the failure. Reverted immediately: stand file
   moved back, both temporary listeners killed, `.e2e-stand/` diffed clean
   before restoring it.

`npm run lint`, `npm run build` (`tsc -b`, covers `e2e/**`), `npm test` —
all green after the change.

### F2 (major) — the closed-allowlist guard reads two filenames; `--import-realm` reads a directory

`src/backend/tests/Mustask.Contracts.Tests/KeycloakRealmRolesTests.cs:28–29,
338–366, 370–393` against `src/backend/Mustask.AppHost/AppHost.cs:211`.

Both new facts iterate a hardcoded pair — `MustaskRealm` and `PartnerRealm` —
while the container bind-mounts the whole directory
(`.WithBindMount("../../../deploy/keycloak/realms", "/opt/keycloak/data/import")`)
and `--import-realm` imports every file it finds there. No test anywhere
enumerates `deploy/keycloak/realms/*.json` (checked: `RealmPath` builds a
path from a name, nothing lists the directory).

So the guarantee US-3 scenario 1 states — "adding any client fails the test
until a human edits the allowlist" — holds for adding a client to an
existing file, and not for adding a file. A future
`deploy/keycloak/realms/foo-realm.json` declaring a client with
`serviceAccountsEnabled: true` and the `realm-management` roles would be
imported by every stand and by whatever tech-060 eventually ships, and
`dotnet test Mustask.sln` stays green: the allowlist fact never sees the file,
and the no-service-account fact never opens it. This is the same species as
the hole backend QA found in the namespace scan (a guard keyed to an
enumerated name rather than to the property it guards), one level up — and
it sits on exactly the boundary that made this task size L (D-1).

Fix: derive the file list from the directory (`Directory.EnumerateFiles(...,
"*.json")`), assert the *set of files* equals a named expected set — so a
new realm file is itself a decision a human has to write down, the same
review attention a new client gets — and then run both properties over every
file found. That also makes the pair of facts non-vacuous against a rename
of either file, which today would throw `FileNotFoundException` only because
older tests happen to read the same two names.

Adjacent, same class, lower likelihood (minor, folded in here rather than
filed separately): `StandOnlyKeycloakClientRules.ShippingProjects`
(`src/backend/tests/Mustask.Architecture.Tests/StandOnlyKeycloakClientRules.cs:35–38`)
is also an enumeration. A sixth shipping project added later is scanned by
neither the namespace fact nor the admin-API fact until someone remembers to
add it to three `[InlineData]` lists. It mirrors `OnionDependencyRules`'s
existing style, so it is not a regression this task introduces — but the
admin-API scan is the guard ADR-0023 leans on hardest, and "every project
under `src/backend` except `Mustask.AppHost` and `tests/`" is derivable rather
than typed.

Status: resolved
Resolution: `KeycloakRealmRolesTests.cs` gained
`RealmDirectoryFileNames()`, enumerating `deploy/keycloak/realms/*.json`
directly, plus a new fact
`KeycloakRealmsDirectory_ContainsExactlyTheAllowlistedFiles` asserting that
set equals the named `{MustaskRealm, PartnerRealm}` pair — a third file is now
itself a decision that fails the build. `ProductionRealmImport_ClientAllowlistIsClosed`
and `ProductionRealmImport_NoClientHasServiceAccountEnabled` both now
iterate `RealmDirectoryFileNames()` instead of the hardcoded pair, so both
properties run over every file the directory actually holds. Proven red:
dropped `deploy/keycloak/realms/probe-f2-realm.json` declaring a client
`probe-f2-sneaky-client` with `serviceAccountsEnabled: true` — all three
facts failed, each naming the file and/or the client by name
(`KeycloakRealmsDirectory_ContainsExactlyTheAllowlistedFiles`: "Лишние:
probe-f2-realm.json"; `ProductionRealmImport_ClientAllowlistIsClosed`:
"Лишние: probe-f2-sneaky-client";
`ProductionRealmImport_NoClientHasServiceAccountEnabled`: "у клиента(ов)
включён serviceAccountsEnabled: probe-f2-sneaky-client
(probe-f2-realm.json)") — then the probe was deleted
(`git status --porcelain deploy/` clean afterward). The adjacent
`StandOnlyKeycloakClientRules.ShippingProjects` enumeration (lower
likelihood, same class) is left as noted — not part of this finding's
required fix, and named here only so it isn't lost.

### F3 (minor) — BR-4's only automated guard checks request paths, not the body that would carry a password

`src/backend/tests/Mustask.Contracts.Tests/KeycloakStandProvisioningTests.cs:152–174`
(`ProvisionAsync_NeverCallsResetPassword`), against
`src/backend/Mustask.AppHost/Provisioning/KeycloakStandProvisioner.cs:209–217`.

The test asserts no request *path* contains `reset-password` or
`credentials`. The fake handler does catch a real
`PUT .../users/{id}/reset-password` (it would throw "Unexpected request"),
so that half is covered twice. What is not covered is the shape a regression
would actually take: Keycloak accepts a `credentials` array inside the user
representation itself, so adding a `["credentials"]` entry to `profile` at
line 209 sets a password for the fixture user on both the create and the
update path, sends no new request, touches no new path, and leaves every
test green. The provisioner's own comment ("Deliberately absent: any
`credentials` field") is currently the only thing enforcing BR-4 in the
repository; the live `GET .../credentials → []` check happened once, at QA,
and does not run again.

Fix: assert over the recorded request *bodies* — no body sent to
`/admin/realms/{realm}/users*` contains a `credentials` property — and keep
the path assertions as they are.

Status: resolved
Resolution: the test now also inspects the bodies of every request whose
path matches `/admin/realms/{realm}/users` or `/admin/realms/{realm}/users/{id}`
(a `Regex` excludes sub-resources like `role-mappings`/`groups`, which send
arrays or no body and are not what BR-4 is about), asserting none carries a
`credentials` property, alongside the original path-only assertion. Ran
against the current code: green. Not separately proven red against a live
regression here, since demonstrating it would mean editing production code
to add the very `credentials` field BR-4 forbids; the body-vs-path
distinction is direct enough (a `credentials` key literally never appears
in any recorded body today) that the test's own construction — asserting a
property of the fake handler's *entire* recorded traffic, not one endpoint
— is the evidence.

### F4 (minor) — a pre-D-8 comment survived in the file D-F2 was raised about

`src/frontend/e2e/global-setup.ts:36–38`: "repairs what an aborted or
force-killed prior run left behind (a stopped keycloak container, **a
shortened accessTokenLifespan**)".

`reconcile.ts` deliberately no longer touches `accessTokenLifespan` (D-8),
and its own header explains at length why. Every other mention in the branch
was updated (`playwright.config.ts` by D-F4, US-9's scenario by D-F2, the
runbook and `src/frontend/AGENTS.md` by US-8) — this call site is the one
survivor, and it is the line a reader hits first when asking what the
reconciliation does. Same class of drift as the spec's BPMN the human caught
in `2aa19e3`; a repo-wide grep for `accessTokenLifespan` outside `e2e/specs`
returns this as the only remaining false statement.

Fix: drop the parenthetical's second item.

Status: resolved
Resolution: dropped in the same edit as F1 (the reordering rewrote this
comment block anyway). `global-setup.ts` now attributes the container
repair to itself only, with no mention of `accessTokenLifespan`; a
repo-wide grep for `accessTokenLifespan` outside `e2e/specs/` no longer
returns this file.

### F5 (minor) — ADR-0023 § "Question 3" still describes the two-guard world QA disproved

`docs/adr/0023-stand-only-keycloak-client-impersonates-the-e2e-fixture-user.md`,
§ "Question 3 — how the guard recognizes ...", item 2, versus § "Decision"
item 3.

The reasoning section says the code path is covered by "no shipping project
references `Mustask.AppHost`" plus "no shipping project's source uses the
provisioning namespace/type", and explains why the second exists. Backend QA
round 1 (D-1) proved precisely that pair insufficient — a reimplementation
under an unrelated name passes both — and the fix added a third check
(`ShippingProject_DoesNotCallKeycloaksAdminApi`). That third check appears
only in § Decision. A reader who reads the argument rather than the verdict
gets the pre-QA picture, and the sentence "the second check exists because a
`ProjectReference` scan cannot see a copy-pasted admin-API call" now reads
as a claim that the namespace scan does catch one — the exact overclaim QA
corrected in the test's own docstring.

Related, and the more important half: `qa.md` states the guard boundary with
unusual honesty (a literal-string scan catches the innocent copy-paste and
nothing adversarial; no cleverer literal scan closes that door). None of
that reaches ADR-0023, which says the guard "covers 'never by copy into a
shipping project'". `qa.md` is a feature artifact; the ADR is the durable
record a future task will read instead. The ADR's *decisive* argument —
`Mustask.AppHost` is never deployed, and the Onion rules already forbid a
reference to it — is sound and does not depend on the scans at all; the
scans are a second line, and the ADR should say so in those words.

Fix: bring § Question 3 into line with § Decision (three checks, and why the
first two were not enough), and add one sentence carrying `qa.md`'s boundary
into the ADR.

Status: resolved
Resolution: § Question 3 rewritten to name all three checks (reference,
namespace-literal, admin-API-surface) and to say explicitly why D-1
disproved the first two on their own, matching § Decision's own list.
§ Decision's item 3 gained the sentence F5 asked for: what actually carries
the guarantee is the dependency-graph argument (`Mustask.AppHost` is never
deployed, Onion rules already forbid referencing it), not the scans —
the scans are a second line, a net for the innocent case, not a claim that
survives a determined author (`qa.md`'s own boundary paragraph — string
concatenation, base64, configuration-sourced values — carried into the ADR
in the same words rather than paraphrased weaker or stronger).

### F6 (minor) — "a failed provisioning surfaces on the resource" is an unverified claim

`src/backend/Mustask.AppHost/AppHost.cs:244–298` (the `catch`/`throw` at the end
of the subscriber).

The comment asserts that Aspire surfaces a subscriber exception on the
`keycloak` resource itself, and the rethrow is justified by that. Nothing in
`qa.md` or `plan-backend.md` records this being observed; every live check
on this branch exercised the success path. If the eventing pipeline logs and
swallows instead (the dispatch behaviour for `ResourceReadyEvent` is not
pinned down anywhere in the diff), a failed provisioning leaves a stand that
reports itself up while the stand file is missing — the file having been
deleted at `AppHost.cs:31` — and the suite then spends 120s polling before
failing with "Is the stand up ...?", which is a named cause but the wrong
one.

Impact is bounded either way (the suite never runs against a half-provisioned
realm, which is the property that matters), so this is a minor. Fix: verify
once — break the admin password or point the provisioner at a wrong path,
run the stand, and record what the console and the dashboard actually show —
then either keep the comment or replace it with what was seen. If it is
swallowed, the error log line at least names the stand file, so the cheapest
real improvement is to make that message say "provisioning failed, the suite
will not run" rather than describing the file's staleness.

Status: resolved
Resolution: verified live (2026-09-15). Breaking the admin *password* turned
out to be unable to produce the divergence at all — `AppHost.cs` hands
Keycloak's own bootstrap admin password and the provisioner's admin
password the *same* `AddParameter` resource, so the two can never disagree
by design; a `dotnet user-secrets set` on that parameter changes both
sides together (a fresh container simply boots with the new password and
the provisioner authenticates against it, no failure). Used the fix's
other suggestion instead — pointed the provisioner at a wrong path by
temporarily changing the hardcoded `AdminUsername` literal in the
subscriber to a value Keycloak's bootstrap admin username doesn't match.
Result: `KeycloakStandProvisioner.GetAdminTokenAsync` threw
`HttpRequestException: ... 401 (Unauthorized)`, the `catch` block logged it
in full via `logger.LogError` (stand file name, full exception with call
site), and — checked directly — the AppHost process kept running
afterward (dashboard still responded, the `keycloak` container stayed
`Up`): the rethrow does not crash the whole stand. Whether the dashboard
*additionally* marks the `keycloak` resource itself as faulted was not
checked (no browser access from this verification environment), so that
half of the original comment is dropped rather than repeated unverified;
the comment now states only what was observed, and the log message was
reworded to "so the suite will not run against this stand" per the fix's
cheap-improvement suggestion. Probe reverted (`AdminUsername = "admin"`
restored, `git diff` on that line empty); containers and processes started
during both this and the earlier (inconclusive) password-based attempt
were stopped and removed, `docker ps -a` confirmed empty afterward.

### F7 (minor) — the per-process secret fix is guarded where it cannot regress, not where it can

`src/backend/Mustask.AppHost/AppHost.cs:199` versus
`src/backend/tests/Mustask.Contracts.Tests/KeycloakStandClientSecretTests.cs`.

The live bug (`plan-backend.md` §12) was not in `KeycloakStandClientSecret`
— that class did not exist — it was in *where* the value was minted: inside
the `ResourceReadyEvent` lambda rather than once per process. Both new unit
tests assert properties of the class (stable per instance, distinct across
instances); both would stay green if a future edit moved
`new KeycloakStandClientSecret()` back inside the subscriber, which
reintroduces the exact defect, byte for byte. The two-pass provisioner test
does not catch it either: it passes the same `options` twice by
construction.

Not a correctness finding — today's code is right — but it is this task's
second-largest live finding and it currently has no repository-level net.
Fix (cheap, in the idiom already used by
`StandOnlyProvisioner_ExistsInAppHost_UnderTheExpectedNamespace`): a
source-level fact over `AppHost.cs` asserting exactly one
`new KeycloakStandClientSecret(` and that it occurs before the
`builder.Eventing.Subscribe<ResourceReadyEvent>` call. If that is judged too
clever, the alternative is to say so in `plan-backend.md` §5's "known
limitation" list, which already names the untested seams honestly.

Status: resolved
Resolution: took the suggested shape exactly. New
`src/backend/tests/Mustask.Architecture.Tests/KeycloakStandClientSecretPlacementRules.cs`,
one fact: reads `AppHost.cs`'s source text, asserts exactly one
`new KeycloakStandClientSecret(` occurrence and that it sits before
`builder.Eventing.Subscribe<ResourceReadyEvent>`. Proven red: temporarily
moved the constructor call into the subscriber (`ClientSecret = new
KeycloakStandClientSecret().Value` in place of `mustaskE2eClientSecret.Value`,
leaving the original per-process declaration in place too, reproducing
exactly the "two constructions" shape the finding describes) — the test
failed, naming the count: "Нашёл 2 вызов(ов) `new KeycloakStandClientSecret(`
... ожидался ровно один." Reverted the probe line
(`git diff` on `AppHost.cs` at that line empty afterward) and reran green.

### F8 (minor) — the provisioning subscriber is not serialized against a concurrent re-firing

`src/backend/Mustask.AppHost/AppHost.cs:244`, with
`KeycloakStandProvisioner.CreateOrUpdateClientAsync`
(`KeycloakStandProvisioner.cs:105–140`).

`KeycloakStandClientSecret`'s docstring explicitly reasons about concurrent
firings ("not observed, but not ruled out") and makes the *secret* safe for
them. The provisioning sequence itself is not: two overlapping passes can
both see "client does not exist" at `KeycloakStandProvisioner.cs:121`, both
`POST /clients`, and the loser gets a 409 into `EnsureSuccessStatusCode` — a
failed provisioning on a stand that is otherwise fine. The same race exists
for the fixture user. DCP was observed re-firing repeatedly for ~30s after a
bare `docker stop`/`start`, which is the window where an overlap would
occur; a pass takes about a second, so the odds are low but not zero, and a
stand that fails to provision once in twenty runs is the kind of flake that
costs far more to diagnose than to prevent.

Fix: one `SemaphoreSlim(1,1)` captured by the lambda, or tolerate `409
Conflict` on both create paths by falling through to the find-and-update
branch. The former is two lines and also removes the interleaved-writes
question entirely.

Status: resolved
Resolution: judged reachable in practice (DCP's observed ~30s re-firing
window after a bare `docker stop`/`start` is exactly the shape the frontend
suite's Docker-restarting specs produce) and fixed rather than justified
away, with the first shape named: a `private static readonly
SemaphoreSlim ProvisioningLock = new(1, 1)` on `KeycloakStandProvisioner`,
acquired at the top of `ProvisionAsync` and released in a `finally` —
serializes the whole method, not only the client-creation branch, which
also removes the interleaved get-modify-put realm-update race the finding
names as a secondary concern. New test
`ProvisionAsync_TwoConcurrentFirings_DoNotRaceOnClientCreation`: two
`ProvisionAsync` calls started via `Task.WhenAll` against one shared fake
handler, with an artificial 50ms delay inserted on the "does the client
exist yet" `GET` (`RecordingKeycloakHandler.DelayBeforeClientExistsCheck`)
to widen the race window rather than rely on incidental timing. Proven red:
temporarily disabled the lock (commented out the `WaitAsync`/`Release`
pair) and reran — failed with a genuine concurrency defect, not merely a
count mismatch: `IOException: The process cannot access the file
'...mustask-e2e-stand-test-....json' because it is being used by another
process` (both calls reached `WriteStandFileAsync` for the same path at
once). Restored the lock, reran green
(`dotnet test Mustask.sln`: 0 failed across all projects).

### On ADR-0023 (the human accepts or rejects it at gate-3)

The code matches what the ADR claims, with the single exception noted in F5
(its § Question 3 is one QA round out of date relative to its own §
Decision). Mechanism, placement, roles, the `ResourceReadyEvent` trigger,
the `/admin/realms/{realm}/users/{id}/impersonation` call, the two
realm-import facts and the architecture facts are all present and are what
the ADR describes.

The argument is honest in the places it is easiest not to be. It records
that `mustask-e2e` cannot read its own attributability trail (403 on `/events`
without `view-events`) and that widening it would be a new decision — a fact
that weakens the NFR it is reporting on, disclosed anyway. It records the
plaintext secret on disk under "what gets harder", not as a footnote. It
rejects Option B (RFC 8693) for a concrete, checkable reason — token
exchange returns tokens, not a browser-visible session, so it cannot satisfy
BR-3 — rather than on taste, and it names the deprecation risk it takes on
in exchange.

Where it claims slightly more than it delivers: "covers 'never by copy into
a shipping project'" for a set of literal-string scans, when `qa.md`'s own
boundary paragraph says plainly that no literal scan survives a determined
author. The ADR does not need a weaker decision — its load-bearing argument
is that `Mustask.AppHost` is never deployed and cannot be referenced by anything
that is, which the Onion rules already enforce — it needs one sentence that
ranks the two: dependency graph first, text scans as the net for the
innocent case. F2 is the one place where a scan is weaker than the ADR's own
description in a way that a *non*-adversarial change would walk through.

Recommendation: accept, conditional on F2 and F5 being carried out; the
decision itself is the right one for this stand, and the alternative the ADR
rejects could not have satisfied BR-3.

### Not filed, deliberately

- The 65s wait in spec 6 leaves roughly 15s of margin before the 120s token
  expiry once `stopContainer`'s own bounded 30s wait is counted. Three live
  runs passed and the failure mode (an expired rather than renewing token)
  would be a red spec, not a false green — noted here so a future flake on
  spec 6 starts from this sentence instead of from scratch.
- Two stands started from the *same* worktree on different ports would share
  one `.e2e-stand/keycloak-e2e-client.json`, last writer winning. The issuer
  field turns that into a named failure rather than a silent wrong-stand
  run, which is the designed behaviour; the container name is port-scoped,
  the file is not.
- `Mustask.Contracts.Tests.csproj`'s
  `<Compile Include="..\..\Mustask.AppHost\Provisioning\*.cs">` is a wildcard: a
  future provisioning file that touches an Aspire type breaks this test
  project's build. The csproj comment states the assumption ("zero Aspire
  dependency of their own"), which is the right place for it.
- `Directory.Packages.props`/`Mustask.sln` were correctly left alone; the
  regression gap that creates is named in `plan-backend.md` §5 rather than
  hidden.

### Round verdict

**needs-fixes** · blockers: 0 · majors: 2 (F1, F2) · minors: 6 (F3–F8).

F1 and F2 go back to the developers — F1 is frontend (`global-setup.ts`
ordering plus the `standFile.ts` split it implies), F2 is backend
(`KeycloakRealmRolesTests.cs`). F1's acceptance evidence should be the live
reproduction QA deferred, not a code reading. F3–F8 are worth taking in the
same cycle; none of them changes what the system does today.

Nothing here is a `needs-human-decision`. The one open item genuinely the
human's is the one already scheduled: ADR-0023's status, on which this
review recommends acceptance conditional on F2 and F5.

## Round 2 — code (delta over Round 1)

Scope: `fae6f74` (frontend, F1/F4) and `0844b2a` (backend, F2/F3/F5/F6/F7/F8),
read as a diff against `7d80364`. Each finding's own `Status:`/`Resolution:`
was read *after* the code, not before it. No stand was started; the one
experiment run here (below) was reverted inside the same command and the tree
proved clean against `HEAD` afterwards.

Independent verification, not taken from the fix commits' claims:
`dotnet test tests/Mustask.Contracts.Tests` — 55 passed, 0 failed;
`dotnet test tests/Mustask.Architecture.Tests` — 66 passed, 0 failed.

Verdict: **approved** · blockers: 0 · majors: 0 · minors: 1 new (F9).
All eight round-1 findings are resolved in the code, not only in the prose.

### Dispositions

**F1 — resolved.** `peekStandFile()` (`standFile.ts:145–171`) is the right
seam: one read, one parse, no polling, no token grant, `undefined` on ENOENT.
`reconcileKeycloakContainer` no-ops on `undefined` and keeps its own
try/catch around the Docker calls, so the two failure modes the coordinator
asked about behave correctly: no stand file → silent no-op, the realm check
that follows names the cause; a stand file naming a container that does not
exist → `docker inspect` exits non-zero, `run` rejects, the catch turns it
into `could not reconcile the keycloak container's state (...)` and the run
continues to the real error. The ordering in `global-setup.ts:27–40` has a
property worth naming because it was probably not designed for: the panel and
`Mustask.Api` checks still run *before* the repair, so reconciliation can only
fire when an AppHost is demonstrably alive — an orphaned container from a
dead stand is never silently started into life. The live evidence recorded in
the resolution is the sequence I asked for at gate-3, in both directions.

**F2 — resolved**, and stronger than the minimum. `RealmDirectoryFileNames()`
enumerates the directory; both client-scanning facts iterate it; the new
`KeycloakRealmsDirectory_ContainsExactlyTheAllowlistedFiles` closes the file
set itself. On the three evasions the coordinator asked me to try:

- **A different extension.** Probed directly: dropped
  `deploy/keycloak/realms/ZZ-PROBE.JSON` (uppercase extension) and ran the
  `KeycloakRealm*` facts — red, naming the file ("Лишние: ZZ-PROBE.JSON").
  `Directory.EnumerateFiles(..., "*.json")` is case-insensitive on this
  filesystem, so the guard is *stricter* than the container: Keycloak's
  import runs on Linux and matches the extension case-sensitively, so a file
  it would import is always a file this guard enumerates. Probe deleted in
  the same command; `git status --porcelain` empty afterwards.
- **A subdirectory.** `EnumerateFiles` defaults to `TopDirectoryOnly` and
  Keycloak's directory import is likewise flat — a file one level down is
  imported by neither. No gap, symmetric by accident but symmetric.
- **A case difference in the name** — same answer as the extension case: the
  enumeration is a superset of what gets imported.

**F3 — resolved.** The body scan is correctly scoped:
`^/admin/realms/{realm}/users(/[^/]+)?$` matches the create `POST` and the
update `PUT` and excludes `role-mappings`/`groups` (too many segments) and
the search `GET` (query string defeats the `$`). `Assert.NotEmpty` on the
matched set keeps it from passing vacuously — that matters more than the
assertion itself. The regression shape the finding named
(`profile["credentials"] = ...`) now turns both requests red. The
`.../users/{id}/credentials` sub-resource, excluded by the regex, is still
covered by the fake handler's "Unexpected request" throw. The resolution's
reason for not proving it red (it would mean committing the forbidden field)
is fair; the construction is self-evident enough to accept without it.

**F4 — resolved.** Comment gone, and the repo-wide grep for
`accessTokenLifespan` outside `e2e/specs/` now returns only true statements.

**F5 — resolved, and this is the condition met.** § Question 3 now names all
three checks and says plainly that D-1 disproved the first two on their own.
§ Decision item 3 now leads with the argument that actually carries the
guarantee — `Mustask.AppHost` is never deployed and the Onion rules already
forbid referencing it — and demotes the scans to "a second line ... not a
claim that survives a determined author", carrying `qa.md`'s boundary in the
same words rather than a weaker paraphrase. An ADR that states which of its
own guards is load-bearing is worth more than one that lists them flat.

**F6 — resolved; the narrowed claim is honest and sufficient.** The reason
the password could not be broken in isolation is verifiable from the source
and correct: `AppHost.cs:146` creates one parameter, line 215 gives it to
`KC_BOOTSTRAP_ADMIN_PASSWORD` and line 261 gives it to the provisioner, so
the two cannot disagree. Breaking `AdminUsername` instead reaches the same
code path (`GetAdminTokenAsync` → 401 → the catch). What was observed is now
what the comment claims, and the half that was not observed (whether the
dashboard marks the resource faulted) is dropped rather than repeated — the
right trade, because the log line plus the suite's own named failure are what
a reader actually has. Worth keeping in mind at gate-3 rather than fixing: it
is still unknown whether a stand whose provisioning failed *looks* healthy in
the dashboard. The consequence is bounded — the suite refuses to run and says
why — so this is knowledge missing, not a defect.

**F7 — resolved.** `KeycloakStandClientSecretPlacementRules` pins exactly one
`new KeycloakStandClientSecret(` in `AppHost.cs` and requires it before the
subscription. Vacuity is handled the right way round: a missing or renamed
`AppHost.cs` throws rather than passing, and `SingleOrDefault` throws on two
matches. The check is textual order, not lexical scope — a construction
placed before the subscription but inside some other repeatedly-invoked
lambda would pass — which is an acceptable boundary for a source scan and one
the class's own docstring is honest about.

**F8 — resolved, and the finding turned out to be understated.** The
`SemaphoreSlim(1,1)` is used correctly: `WaitAsync` outside the `try`,
`Release` in the `finally`, so a cancelled wait cannot over-release; no
nesting, no re-entrancy, no sync-over-async, nothing swallowed — the lock
changes only ordering, never error propagation. `ProvisionAsync_TwoConcurrentFirings_...`
forces a real overlap with an injected delay on the existence check rather
than hoping for one, and asserts the counts that distinguish "serialized"
from "raced". Disabling the lock produced an `IOException` from two
concurrent writers to the same stand file, which is a worse failure than the
409 the finding predicted: two passes could reach `WriteStandFileAsync` for
one path at once, i.e. the file the whole suite reads could have been
truncated by a second writer. The fake handler's `Requests` list is now
lock-protected for appends, which the concurrent test needs.

### F9 (minor, new) — a realm file with no `clients` array fails the new guards with a bare `KeyNotFoundException`

`src/backend/tests/Mustask.Contracts.Tests/KeycloakRealmRolesTests.cs`,
`ClientIds` (≈line 626) reached from the two facts that now iterate
`RealmDirectoryFileNames()`.

Found by probe, not by reading: the `ZZ-PROBE.JSON` above contained
`{"realm":"zz-probe","enabled":true}` — legal Keycloak, no `clients` key.
Three facts went red, but only one of them explained itself. The other two
failed with `System.Collections.Generic.KeyNotFoundException : The given key
was not present in the dictionary.` — no file name, no property name, no
mention of `clients`, because `ClientIds` does
`Realm(fileName).GetProperty("clients")` unconditionally.

Why it matters is the workflow the fix itself prescribes. Today the noise is
harmless: the file-set fact fails first and names the file, so whoever added
it has their answer. But the documented next step is to *amend the allowlist*
— and the moment they do, the file-set fact goes green and the two client
facts keep throwing the same anonymous `KeyNotFoundException`, now with
nothing left to explain it. A realm that declares only roles, groups or an
identity provider is an ordinary thing to add, and this is a guard written
precisely to be met by a human who is adding one. It fails closed, which is
why this is minor and not more: the build is red either way, never
accidentally green.

Fix: `TryGetProperty("clients", out var clients)` in `ClientIds`, returning
an empty sequence when absent — or, if a realm file with no clients should
itself be an error, throw one that names the file and the property. Either is
one line; the current behaviour is the only one that names neither.

### Not filed

- `peekStandFile` throws on a malformed stand file from inside a module whose
  header calls itself best-effort. Deliberate, disclosed in its own docstring,
  and the message is good — but it means the "best-effort" claim now holds
  for Docker failures and not for a truncated file. `File.WriteAllTextAsync`
  is not atomic, so that truncation window exists in theory; it is
  sub-millisecond and predates this round (`waitForStandFile` rethrows
  non-ENOENT for the same reason), so it is noted, not filed.
- `reconcile.ts` logs "was 'exited', started it" *after* `startContainer`
  returns, and `startContainer` can wait up to ~120s for Keycloak to serve
  plus the stand-file refresh. US-9 scenario 1 wants the discrepancy
  reported; it is, just after up to two minutes of complete silence. Moving
  the `console.log` above the `await` would cost nothing. Pre-existing shape,
  not introduced by this round's fix.
- The realm-import directory path is now written in two places —
  `AppHost.cs:211`'s bind mount and `RealmDirectoryFileNames()` — with
  nothing tying them together. The regress-to-a-literal has to stop
  somewhere and this is a reasonable place, but if the mount ever moves, the
  guard will happily keep checking the old directory.

### Final word on ADR-0023

**Accept.** Both conditions from round 1 are met: F2 closed the one place
where a scan was weaker than the ADR's description of it in a way a
non-adversarial change could walk through, and F5 rewrote § Question 3 and
§ Decision so the record now says which guard carries the guarantee
(the dependency graph) and which are the net for the innocent case (the
scans), with the boundary stated rather than implied. The mechanism choice,
the placement, the minimal role set and the two-sided guard are all
implemented as described, and the document is honest where honesty costs it
something — the unreadable attributability trail, the plaintext secret, the
deprecation risk it takes on by preferring the admin impersonation endpoint
over token exchange.

### Round 2 verdict

**approved** · blockers: 0 · majors: 0 · minors: 1 (F9, new).

F9 does not block: it degrades a diagnostic message on a path nobody is on
today, and it fails closed. It is worth taking whenever
`KeycloakRealmRolesTests.cs` is next open — and certainly before anyone adds
a third realm-import file, which is the moment it stops being cosmetic.

## Round 3 — code (the cold-stand gate, `5ae6921`)

Scope: one commit, `5ae6921` ("wait for Mustask.Api to see the realm before the
first spec"), added after this branch was approved at round 2 and after the
human lost specs 1 and 2 on a cold stand. New: `e2e/support/apiReadiness.ts`,
`src/test/e2eApiReadinessGate.test.ts`, one call in `e2e/global-setup.ts`,
and prose in `src/frontend/AGENTS.md` + `docs/runbooks/stand.md`.
The rest of the branch is round 2's and was not re-read. `ee3a38a` (the
evidence record in `status.md`) was read, not reviewed.

### What holds (checked, not assumed)

- **The identity claim is true, and it is what makes the gate exact rather
  than a proxy.** `Program.cs:160` registers one `AddSingleton<RealmReadiness>()`;
  `RealmReadinessHealthCheck(RealmReadiness readiness)` returns `Unhealthy`
  exactly when `readiness.Current.IsReady` is false, and
  `SessionEndpoints.LoginAsync(string? returnUrl, RealmReadiness readiness, …)`
  redirects to `…?authError=idp_unavailable` on the same field of the same
  instance. `/health` green therefore *implies* a login will be challenged —
  the direction the gate needs. There is no window to lose: readiness latches
  (`RealmReadinessMonitor.CheckAsyncCore`, the `readiness.Current.IsReady &&
  state.Reason is RealmUnavailable or EmptyKeySet` early return), so once
  green it cannot go back except on `IssuerMismatch`.
- **`/alive` genuinely could not have caught this.**
  `Mustask.ServiceDefaults/Extensions.cs:143` maps it with
  `Predicate = r => r.Tags.Contains("live")`, and only `self` carries that
  tag (`Extensions.cs:108`); `keycloak-realm` is registered untagged
  (`Program.cs:180`). The diagnosis in the commit message is accurate on
  every point I could check: `CheckInterval = 30s`, `auth.ts`'s
  `SIGN_IN_TIMEOUT = 30_000`, and `AnonymousScreen.tsx` rendering «Сервис
  аутентификации недоступен» for `idp_unavailable`.
- **The 120s budget is defensible.** After the setup's own `/realms/mustask`
  check has passed, at most one monitor tick separates the API from the
  truth: `RealmMetadata.RefreshInterval` is 10s, strictly below the 30s
  `CheckInterval`, so `RequestRefresh()` is never throttled into eating a
  tick, and `HttpTimeout` is 5s. 120s is four intervals — slack, not a guess.
- **Placement cannot deadlock or mask.** `waitForApiRealmReadiness` depends
  only on Keycloak serving discovery, which `reconcileStand()` and the realm
  check ahead of it already work toward; `loadStandFile()` behind it depends
  on Keycloak's admin API and the stand file, not on `Mustask.Api`. Neither waits
  on the other. No global-setup timeout exists to overrun
  (`playwright.config.ts` sets `timeout` per test, and no `globalTimeout`).
- **`e2eSessionGuard.test.ts` is not weakened.** It scans `e2e/**/*.ts` by
  file, so `apiReadiness.ts` is now scanned too and passes: it has no
  `name: 'mustask.session'` and no `.route(`. The new Vitest file lives in
  `src/test/`, i.e. outside the scanned tree, but it is not e2e code and
  stubs nothing — it starts a real socket and exercises the real function.
- **Local re-verification** (not taken from the report): `npx vitest run
  src/test/e2eApiReadinessGate.test.ts` → 2 passed, 4.1s (inside the 20s
  `testTimeout`, so the two deliberate 2s sleeps are not a flake); `npx tsc -b
  --force` → 0; `npx eslint` on the three touched TS files → 0. The
  cross-tree import (`src/test/…` → `e2e/support/…`) type-checks and does not
  reach the bundle.
- The test does constrain the behaviour it claims to: `paths.length` is 3
  (proceeding on the first 503 — the defect — fails it), the path set is
  exactly `/health` (a slide back to `/alive` fails it), and the "already
  warm" case pins that the log line stays silent. The `node:http` server is
  closed and `vi.restoreAllMocks()` called in `afterEach`, the MSW
  passthrough is dropped by `setup.ts`'s `server.resetHandlers()`, and the
  handler is keyed on an ephemeral port anyway — nothing leaks between tests.

### F10 (major) — the runbook now promises a thing the setup still refuses

`docs/runbooks/stand.md:490` — "Those two commands may follow each
other immediately, with no pause — but only since tech-159 closed a defect a
human hit here."

The two commands above that sentence are `dotnet run --project
src/backend/Mustask.AppHost` and `npm run e2e`. Followed literally, the second
one fails within a second. `e2e/global-setup.ts:28-40` opens with three
*single-shot* reachability checks — `checkReachable` does one `fetch` and
throws on the first refusal, with no retry and no poll:

- `The panel is not reachable at … Is the stand up?` — and the panel is an
  Aspire resource (`AppHost.cs:395`, `AddViteApp`), so port 3000 is not
  listening until AppHost has got that far;
- `Mustask.Api (/alive) is not reachable …`;
- `The Keycloak realm 'mustask' is not reachable …`.

The new gate removes exactly one window — "the stand answers but `Mustask.Api`
has not seen the realm". It does nothing about the far longer window "the
stand is still starting", which is the one a reader of this paragraph is
being invited to walk into. `src/frontend/AGENTS.md` gets this right in the
same commit ("The suite may start the moment the stand **answers**"), which
is why the runbook's stronger wording reads as a slip rather than a
disagreement.

This is the branch's third instance of prose asserting more than the code
does (F1, F2, and the human's BPMN find), which is why it is major rather
than minor: the defect is one sentence, but the pattern it belongs to is the
reason this round exists.

Fix (prose only — the code needs no change): say what AGENTS.md says. E.g.
"Once the stand is up — the Aspire dashboard shows the panel, `Mustask.Api` and
`keycloak` running — `npm run e2e` needs no extra pause after it: the suite
waits out the window in which the stand answers but `Mustask.Api` has not yet
seen the realm (up to ~30s on a cold stand, reported in the run log). It does
not wait for the stand to come up at all — the reachability checks ahead of
that wait fail fast, naming what did not answer."

### F11 (minor) — the gate waits on an aggregate and reports it as one cause

`src/frontend/e2e/support/apiReadiness.ts:59-71`. `/health` runs *every*
registered check with no tag filter: `self`, `keycloak-realm`, and
`MustaskDbContext` from `AddNpgsqlDbContext`
(`Mustask.Infrastructure/DependencyInjection.cs:41`;
`Mustask.ServiceDefaults/Extensions.cs:127` says so in as many words, and
`DatabaseHealthCheckTests` pins the entry by name). So a 503 here means "the
realm, or the database, or anything added later" — while the thrown message
states one of them as fact:

> `Mustask.Api still does not consider the Keycloak realm ready after 120s …
> Until it does, /api/session/login answers authError=idp_unavailable …`

And the gate cannot tell the difference even in principle: `MapHealthChecks`
is deliberately left without a custom `ResponseWriter`, so the body is the
single word `Unhealthy` (pinned by `HealthEndpointTests` and
`ProductionHealthEndpointTests`: `Assert.Equal("Healthy", body)`). `lastSeen`
will read `HTTP 503: Unhealthy` and add nothing.

Waiting on the superset is the right call — a stand whose database is down
fails every spec anyway, and nothing here can pass falsely. What is wrong is
only the claim attached to it, on a branch whose own standard is "one message
naming the cause". Fix: either soften the message ("`Mustask.Api`'s `/health` is
still not green … `/health` covers the Keycloak realm *and* the database, so
check the Aspire dashboard for which") or, if the realm specifically is worth
naming, have the gate say what it actually observed and what the two
candidate causes are.

Second-order, same line: the poll is every 2s for up to 120s — up to 60
`/health` calls, each of which, per the `Extensions.cs:127` comment, takes a
pooled connection and goes to Postgres with no cache and no rate limit. It is
a stand and this is harmless; a 3–5s interval would cost the gate nothing and
is the more honest neighbour to that comment.

### F12 (minor) — the stub answers in a way `Mustask.Api` never does, and the branch that matters is untested

`src/frontend/src/test/e2eApiReadinessGate.test.ts:29-44`. The comment says
"Answers `/health` like Mustask.Api does: 503 + the monitor's reason until
`healthyFrom`" and the stub sends
`Unhealthy: Keycloak realm is not ready: RealmUnavailable.`. The real
endpoint sends `Unhealthy` and nothing else (F11). Nothing in the assertions
depends on the body, so no test result changes — but it is a false statement
in a comment, in the same file that was written to stop this branch shipping
claims it had not executed, and the next reader is entitled to believe it.

In the same place: the only branch of `waitForApiRealmReadiness` that carries
real diagnostic value — the 120s deadline and the message it throws — has no
coverage at all, and cannot get any today, because
`REALM_READINESS_TIMEOUT_MS`/`REALM_READINESS_POLL_MS` are module constants
with no seam. Fix: make the stub say `Unhealthy` (and fix the comment), and
take an options argument (`{ timeoutMs, pollMs }`, defaulted) so a third case
can assert that a stand that never goes green fails with a message naming the
URL and the last status. That third case is also where F11's wording would
get pinned.

### F13 (minor) — a permanently-4xx `/health` is ground out over 120s under a Keycloak headline

`src/frontend/e2e/support/apiReadiness.ts:59-74`: every non-`ok` response is
treated as "not ready yet". For 503 that is right. For 401/403/404 it is
not — those are terminal, and `Program.cs:230` names them explicitly as the
regression signal to watch for ("401/403/404 от /health — признак регрессии
(сняли AllowAnonymous, увели маршрут)"). If someone drops `.AllowAnonymous()`
from `MapHealthChecks` or moves the route, every E2E run pays 120s to be told
the Keycloak realm is not ready.

It does surface eventually, and `lastSeen` carries `HTTP 404: …`, so this is
diagnosis, not correctness. Fix: break out of the loop on a 4xx with its own
message — "`/health` answered HTTP 404; the E2E readiness gate needs it
anonymous and on this path (`Mustask.ServiceDefaults/Extensions.cs`)". Connection
errors must keep polling as they do now — the API can be restarting.

### Not filed

- **Ordering.** Putting the wait after `loadStandFile()` instead of before it
  would overlap the ≤30s API wait with the ≤90s provisioning poll and make
  the gate free on a cold stand, instead of adding its wait in series. Both
  orders are correct and neither can deadlock; the current one has the virtue
  of failing on the cheaper check first. Noted, not asked for.
- `src/frontend/AGENTS.md:77` is 91 characters, having absorbed a re-wrap the
  edit did not finish; the file otherwise wraps at ~76. `prettier` is at
  `printWidth: 100` and `verify.sh` does not run `format:check`, so nothing
  catches it. Cosmetic.
- `lastSeen`'s initial `'no response yet'` is unreachable at the throw site
  (every path through the loop body assigns it before the deadline check). A
  safe default, not a defect.

### Round 3 verdict

**needs-fixes** · blockers: 0 · majors: 1 (F10) · minors: 3 (F11, F12, F13).

The code is right. The gate waits on the exact state that decides the
failure, not on a proxy for it; the budget, the placement and the latching
behaviour all check out against the backend; the test is real, constrains the
regression it was written for, and leaks nothing. Every finding in this round
is about what the change *says* — one sentence in the runbook that promises
more than the setup delivers (F10), a thrown message and a stub comment that
name a cause they cannot know (F11, F12), and one failure mode reported
120 seconds late under the wrong headline (F13). F10 is the only one that
should hold the gate, and it is a one-sentence edit.

### Round 3 — re-check (`bb77e17`)

All four findings verified fixed in the code and the prose, each against the
source rather than the summary. Re-ran locally: `npx vitest run
src/test/e2eApiReadinessGate.test.ts src/test/e2eSessionGuard.test.ts` → 28
passed (4 + 24) in 1.18s of test time, `npx tsc -b --force` → 0, `npx eslint
e2e src/test` → 0. No stand, no Playwright.

- **F10 — closed.** `docs/runbooks/stand.md:490-493` now opens with
  "Wait for the stand to come up first … The suite does **not** wait for
  that: the reachability checks at the head of its global setup ask once and
  fail fast, naming whichever of the three did not answer." That is exactly
  what `global-setup.ts:28-40` does, and the "no pause of your own" claim
  that follows is now scoped to "Once the stand answers" — the window the
  gate really closes. `src/frontend/AGENTS.md:84-87` carries the same
  parenthetical. Nothing in either paragraph now outruns the code.
- **F11 — closed.** `apiReadiness.ts:102-109` no longer names a cause: it
  reports the endpoint, the last thing seen, that `/api/session/login` *can*
  answer `authError=idp_unavailable` (the hedge is right — the red check may
  be the database), and that `/health` covers the realm and the database with
  one word for both, so the dashboard is where the answer is. The poll is
  3s (`DEFAULT_POLL_MS`), ≤40 pooled connections over the full budget.
- **F12 — closed.** The stub answers the single word `Unhealthy`
  (`e2eApiReadinessGate.test.ts:52`) and its comment now states why
  (no custom `ResponseWriter`, pinned by `HealthEndpointTests`). The gate
  takes `{ timeoutMs, pollMs }`, defaulted, and the third case asserts the
  deadline message names the endpoint and the last status.
- **F13 — closed.** `apiReadiness.ts:80-89`: 4xx sets `terminal` and throws
  before the sleep, with a message naming the anonymity/route requirement and
  `Mustask.ServiceDefaults/Extensions.cs`; connection errors still fall through
  to `lastSeen` and keep polling, as they must. The fourth case pins both the
  message and that it cost one request and under 2s against a 10s budget —
  which is also why the "proven red first" claim needs no re-run to believe:
  remove the branch and that case necessarily takes the 10s and throws F11's
  message instead, failing on both assertions.

The two items left deliberately undone are the right calls, and I would have
made the same ones: the ordering is a wash (overlapping the waits buys ~30s
on a cold stand and costs the cheap-check-first property), and `lastSeen`'s
initial value is a safe default for a line that cannot be reached.

### Nits (not findings, nothing to act on before the gate)

Two of these are the same species as F12 — a comment saying a little more
than the thing it points at — at a magnitude that does not justify another
round.

- `apiReadiness.ts:4` documents the `timeoutMs` *option* with "Four
  `RealmReadinessMonitor.CheckInterval`s (30s each)", which describes
  `DEFAULT_TIMEOUT_MS` on line 9 (undocumented) and is false at every call
  site that passes the option — the tests pass 600ms and 10s. The sentence
  wants to be on line 9.
- `apiReadiness.ts:11-12` cites the pooled-connection cost as
  "(`Mustask.ServiceDefaults/Extensions.cs`, `AddNpgsqlDbContext`)". The comment
  that justifies the claim is indeed in `Extensions.cs:127`, but the call is
  in `Mustask.Infrastructure/DependencyInjection.cs:41`; as punctuated it reads
  as if the registration were in ServiceDefaults.
- `src/frontend/AGENTS.md`: line 77 is re-wrapped as promised, but the same
  edit left `Mustask.Api` alone on line 90 and split "with one message naming /
  the cause" across 79-80. Cosmetic, and mentioned only because "finished the
  re-wrap" is true of the line named and not of the paragraph touched.
- `e2eApiReadinessGate.test.ts:75`: `expect(elapsed).toBeGreaterThanOrEqual(400)`
  against `pollMs: 200` leaves a margin of the three fetches only, where the
  old shape had 2s of slack; Node's timers may fire ~1ms early. The
  assertion that carries the meaning is `paths.length === 3` on the line
  above, so a looser bound (or dropping this line) would lose nothing.
- Process, not code: `status.md:116-145` restates all four findings, their
  severities and their fixes — about thirty lines that are a second copy of
  this section. `06-code-review.md` asks `status.md` for one `## Log` line
  (the round ran, N findings open) precisely so the two cannot drift; the
  file is now at 237 lines against `verify.sh`'s 250 hard stop, with the
  150-line "roll it into history" note long passed.

### Round 3 verdict (after `bb77e17`)

**approved** · blockers: 0 · majors: 0 · minors: 0.

The gate was already correct; this round's work was making it say only what
it can prove. It now does: the message names what was observed and points at
the dashboard for the rest, the terminal case is terminal, the two branches
that carry the diagnostics are tested, and both documents describe a setup
that waits for `Mustask.Api` and never for the stand. The four nits above are
worth a minute whenever the file is next open and should not hold `gate-3`.
