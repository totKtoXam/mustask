# Stage 8 — AI code review

**Role:** code-reviewer — a separate session, clean context · **Model:** deep
**Input:** the full diff of the branch against `main`, `spec.md` (the index)
+ the stories the diff touches under `docs/features/<id>/stories/`,
`design.md`, `api-contract.md`, and — for item 6's second direction — the
prose outside the diff that describes behaviour the diff changes
**Output:** findings in `docs/features/<id>/review.md`

What happens after the review — the human gate, the MR, completion — is in
[07-mr-complete.md](07-mr-complete.md). The reviewer has no business there and
does not read it.

## Order of checks (by decreasing importance)

1. **Conformance to the spec** — what is written is implemented; nothing extra.
2. **Correctness** — logic errors, races, transactions, handling of integration
   failures, null/empty collections.
3. **Security** — authorization on every endpoint, IDOR (someone else's data by
   id), injections, secrets in code, input validation.
4. **Architecture** — conformance to the conventions and the accepted ADRs
   (`docs/adr/`): layer boundaries, slice structure, dependencies pointing the
   right way. Read the convention the diff touches, not all of them: backend
   files → `docs/conventions/backend.md`, frontend files →
   `docs/conventions/frontend.md`, branch, commit and MR questions →
   `docs/conventions/git.md`. Backend and git together are ~44 KB, and a
   convention nothing in the diff touches cannot produce a finding. A decision
   that warrants an ADR but was made silently in the code is a finding.
5. **Tests** — not just "they exist", but "they check what they claim to".
6. **Behavioural claims in prose** — every sentence that asserts how the
   system behaves is traced to whatever makes it true, or it is a finding.
   Rules, scope and severity: "Behavioural claims in prose" below.

Every finding: file:line, the essence, severity (blocker / major / minor),
the proposed fix. What happens next depends only on the severity, and the
loop economy below is a rule, not a default the orchestrator may tighten:

- **Blocker/major → back to the developer** (fixes go through stage 4/6).
  The next round is a **delta**: the reviewer re-reads the fix diff and the
  previously open findings, nothing else. QA is re-run only over what the
  fix touched, and only when the fix changed runtime code or tests — a
  prose-only fix (a comment, a document, an ADR sentence) gets the
  reviewer's delta read and no QA session.
- **Minor → a debt, not a round.** A minor is not sent to the developer
  inside the review loop. It stays in `review.md` with its proposed fix, is
  listed under "Deliberately left open" in the gate-3 summary, and the
  orchestrator queues it as a line (`scripts/queue.sh add`) when it is worth
  a task of its own. A round whose open findings are all minors is
  `approve`. A developer may still fold a minor into a major's fix when it
  sits in the same lines — but no round, no QA re-run and no delta read is
  owed to a minor on its own.
- **A finding that waits on a decision stops the loop.** When a blocker or
  major cannot be fixed without a decision the developer may not take — an
  open point of the plan, a spec question, a design delta — the round's
  verdict is `blocked-on-decision` and **no further round starts**: the
  orchestrator (or the human at a gate) records the decision in
  `status.md`, the developer fixes, and one delta round follows. A round
  that reports "still waiting for the decision" has spent a session on
  nothing.
- **The number of rounds is capped by size** —
  [routing.md](../routing.md), "Ceremony caps by size": on `S` and `M` one
  round over the whole diff, run as one session after the last development
  stage, and at most one delta read after it; on `L` at most two delta reads,
  and only `L` may fan the round out into lenses and skeptics. If it does not
  converge within the cap — escalate to the human with the open list, do not
  start another read.

The L security pass (below) runs once, after the last general round, and
its findings follow the same rules: a major gets one fix and one delta
re-check, a minor becomes a debt.

**Findings go into `docs/features/<id>/review.md`**, not into `status.md`.
Each round gets its own section — `## Round 1`, `## Round 2 (delta)`; an `L`
reviewed by side keeps the side in the heading, `## Round 1 — backend` — so
the second round is a delta over the first and not a re-read of everything. `status.md` gets one
line in `## Log` saying the round happened and how many findings are still
open; the findings themselves, their severity and their resolution live in
`review.md`. A reviewer who appends a findings section to `status.md` is
turning the state file into a journal, and the next agent then pays for that
history on every read.

**For L features, additionally** — a separate security pass: one more
code-reviewer session (clean context) that looks ONLY at security against a
checklist: authorization of every endpoint, IDOR (access to someone else's
data by id), personal data (IIN/BIN and others) in logs and API responses,
audit trail of significant actions, injections, secrets in code. A general
reviewer, for whom security is item 3 of 6, consistently sacrifices it.

## Behavioural claims in prose

Item 6 in full. A claim is a sentence asserting how the system behaves: a
guarantee ("adding any client fails the test"), an absence ("no spec writes
realm configuration"), an automatic action ("global setup repairs it"), a
permission or a limit ("the two commands may follow each other with no
pause"). Intent, rationale and anything the text explicitly marks as future
work are not claims. That exclusion is narrow, and it is the one a reviewer
will be tempted to widen: it covers *why* a thing was done and what is openly
deferred, never *what the system does*. A spec, a story or a BPMN node
describing behaviour is making a claim. Being a requirements document is no
defence, and neither is finding no code behind the sentence: a behavioural
sentence nothing implements is not exempt from this pass, it is the plainest
finding the pass can produce. This pass runs on the branch at review time,
when the code the branch's prose describes is supposed to exist — a spec
written ahead of its implementation is not lying, it is early, *when the text
says so*. An unmarked sentence describing behaviour the branch does not ship
is the finding, and marking it deferred is the fix: the marking is what makes
the difference legible to the next reader, so leaving it to the reviewer's
judgement is the same hole in a politer form. The BPMN case below is exactly
that, from the other end: a spec's own diagram outliving the code it
described.

**Every claim is anchored to whatever makes it true, or it is a finding.**
The burden is on the prose: a claim the reviewer cannot anchor inside the
round is a finding by default, and the fix may be to weaken the sentence
rather than to change the code.

An anchor is `file:line` of whatever makes the sentence true in the case it
describes: code, and — for a document about the process itself — the script,
hook, test or settings rule that enforces it. Still within that case, and only
there: where a process document's only enforcement is an instruction a named
role is required to read, the anchor is that instruction *plus* the stage
that puts the role in front of it — the assignment is the mechanism, and
without it you are anchoring prose to prose. Say so when this
is the anchor: it is the weakest one available. Prose asserting behaviour is in
scope wherever it lives, `docs/ai-workflow/`, `.claude/` and the `AGENTS.md`
files included; a rule nothing executes is a claim like any other, and this
section is subject to its own pass. Three things look like anchors and are
not:

- **A symbol with the right name.** F1 of the E2E-without-password task
  ([example review](../../templates/examples/tech-159-e2e-without-password/review.md)): `reconcileStand()` existed
  and was called, and three documents said the suite repairs a stopped
  container — but a reachability check ahead of it threw first, so the repair
  could only run in the one state that did not need it. Anchor to a path the
  claim's own case reaches.
- **Code covering part of what the sentence quantifies.** F2: the criterion
  said adding *any* client fails the test; the test read two hardcoded
  filenames while the stand imports a whole directory. Against any / every /
  no, the anchor covers the whole set, or the sentence is narrower than it
  reads.
- **Another document.** Prose anchors to what enforces it, never to other
  prose. F1's three documents agreed with each other and all three were
  wrong.

**Two directions, not one.** Prose the diff adds or changes → anchor its
claims. Behaviour the diff changes → go find the prose describing the old
behaviour, in files the diff does not touch. The BPMN case is exactly that: a
decision moved a realm setting from the specs to AppHost, and `spec.md`'s
diagram and its operation table — untouched by that diff — went on describing
the old shape. A pass over added lines only would have missed it. The cheap
form: for each behaviour the diff changes, grep the identifier it is named by
(a setting, a container, an endpoint, a command) and read the hits under
`docs/`, `.claude/`, the story files and the `AGENTS.md` files. Bound it the
way item 4 bounds its reading: the identifiers are the ones the diff's own
changed behaviour is named by, and the sweep ends when those hits are read.
If the diff changes more behaviour than the round can sweep, name which
behaviours you swept and which you did not — a named gap is a result, an
unnamed one is the silence this pass is against.

**Severity.** At least **major** when someone acting on the sentence does the
wrong thing — walks into a failure, relies on a guarantee that is not there,
or leaves to code a repair it will not perform. Minor when the sentence
overstates and nothing an agent would do differs. A sentence a sibling file
contradicts **in the same diff** is major regardless: the diff carries its
own disproof.

**The round records the pass — both directions, every round.** In `review.md`,
under the round's section, one line per claim checked: where the claim is, and
the `file:line` it anchored to or the finding it became. Direction 2 gets its
own line whatever the diff looks like — the behaviour that changed, the
identifier grepped for it, what the hits were; "none" is a result and is
written down, and so is any behaviour the round decided not to sweep — that
disclosure goes on the same line, or the bound above is just permission to
stop. A mixed diff owes that line exactly as a code-only one does: the case
this pass exists for arrived on a branch whose diff was 55 files, and the very
change that stranded the prose updated the stand runbook and
`src/frontend/AGENTS.md` among others — "the diff touched docs" is no
evidence the pass ran. A round that lists nothing has not
run this pass, and says so by its own silence. Nothing downstream parses that
list today: it sits in `review.md` next to the findings, where the human
reading the round sees it and the next round takes it as the baseline for its
delta. Its whole job is to make a skipped pass visible to a reader.

**Why this exists.** One branch — the E2E-without-password task, kept as a
filled example — shipped four episodes of prose
promising more than the code did — F1 and F2 (review round 1), a stale BPMN
step and operation-completeness row, and F10 (review round 3), the three
findings in
[its review.md](../../templates/examples/tech-159-e2e-without-password/review.md).
Three were caught by the stage-8 reviewer on the five checks above, without
this section — and F10 only because the human asked for a round nobody had
scheduled. The fourth no reviewer ever saw: the human found the stale diagram
and table by reading the rendered BPMN and fixed them eleven minutes
before the review stage opened. That is the honest size of the gap.
The five checks catch this defect when it sits in the diff in front of them;
the episode that reached a human first was prose in a file the breaking
change never touched — while that same change did update the stand
runbook and `src/frontend/AGENTS.md`, so nothing
about the diff looked neglected. Item 6 exists to make the first three
systematic and to give the fourth somewhere to be caught.
`scripts/verify.sh` checks links, backlog ids, `status.md` size and review
sections creeping into `spec.md`; it does not compare prose against code, and
caught none of the four. A machine worklist of candidate sentences (a net of
absolute markers — always, never, every, any, only…) was tried and dropped:
it caught two of the four cases and flagged hundreds of true lines, so this
pass is the only thing looking.
