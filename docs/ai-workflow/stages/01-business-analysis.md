# Stage 1 — Business analysis

**Role:** business-analyst · **Model tier:** standard (overrides: an `L`, or a spec rewritten after `gate-1: rejected` — `deep`)
**Input:** task, `docs/backlog.md`
**Output:** `docs/features/<id>/spec.md`, the index, per the
`docs/templates/feature-spec.md` template + one
`docs/features/<id>/stories/US-<N>-<slug>.md` per story, per the
`docs/templates/user-story.md` template

## Goal

Turn the task into a verifiable spec: user stories with acceptance
criteria, a BPMN process, a position in the backlog.

## Process

1. Read the task statement and the current `docs/backlog.md`.
2. Write `spec.md`, the index:
   - context and goal (2–4 sentences, why the user needs this);
   - roles: who uses this functionality and with which permissions;
   - the story table: id, one-line title, `depends-on`, the story's file —
     one row per story file written in step 3; a story spun out into its own
     queue line does not get a row here, see "Per-story delivery" below;
   - BPMN of the main process — a Mermaid diagram in the file itself
     (mandatory for L; for M — only if the process branches non-trivially);
   - business rules and operation completeness (the tables in the template);
   - non-functional requirements (volumes, permissions, audit) — only the ones really needed;
   - out of scope — an explicit list of what we do NOT do;
   - open questions for the human.
3. For each story that is not spun out per "Per-story delivery" below, write
   `docs/features/<id>/stories/US-<N>-<slug>.md`:
   - the header — `story`, `depends-on` (other story ids this one needs read
     to make sense of, or `-`), `shippable`, `status: pending`;
   - the story itself, "As a <role>, I want <action>, so that <value>";
   - acceptance criteria in Gherkin (Given/When/Then), several scenarios where
     that means happy path + failures.
4. Decompose into backlog tasks (each ≤ size M) with dependencies
   and propose a priority — **as a decomposition table in `spec.md`**; do not
   touch `docs/backlog.md` itself: it lives in `main`, and the queue lines are
   entered by the orchestrator after the decomposition is approved — with
   numbers already assigned. In the decomposition table tasks are written with
   the literal `NNN` (`feat-NNN-<slug>`), without digits, and references
   between them use the slug: a number is issued only by a commit to `main`
   when the line is put in the queue — otherwise fifty numbers end up taken
   by a table in an unmerged branch, invisible to other sessions
   ([../../features/numbering.md](../../features/numbering.md)).

## Per-story delivery

A story that is **independently verifiable and leaves no capability that
nothing uses** becomes its own queue line at decomposition, instead of a
story file in this task: a normal row in the decomposition table (step 4),
not a row in the story table (step 2). No partial squashes, no new gate,
no change to `scripts/finish-task.sh` — the existing queue and merge
machinery already handles one task at a time, and this is a decomposition
decision, not a new mechanism.

This is not "always split": most stories are not shippable alone, because
most stories are one side of a capability whose other side is a different
story. A story that adds a client with nothing yet calling it is the
counter-example, not the rule — the stand-only Keycloak client of the
[E2E-without-password example](../../templates/examples/tech-159-e2e-without-password/spec.md), merged
without the test suite that exercises it, would have put a right to
impersonate any user into `main` with nothing there to use it. Mark such a
story `shippable: no` in its header, with the one-line reason, and keep it as
a story file next to the story that does use it.

## Rules

- Every acceptance criterion must be verifiable by an automated test or by hand —
  "the system must work fast" is not a criterion, "the list takes ≤ 2s on 10k rows" is.
- Do not design the implementation: no DB tables and no endpoints in the spec.
  The API contract appears at the backend stage.
- **The line budget is per file, not per folder.** A story file over ~150
  lines is really two stories; `spec.md` over ~250 lines means either content
  that belongs in a story leaked back into the index, or a feature too big for
  one task. Either way, return a proposal to the orchestrator to split it.
  The index gets the larger number on purpose: what lives there — the BPMN,
  the business rules, the NFRs — grows with the *number* of stories, not with
  any one of them, and a budget tight enough to squeeze it would pay for the
  tidiness by deleting requirements. This is the only place these numbers are
  stated — do not restate them elsewhere.

## Definition of Done

- [ ] Every story in the story table has a file under `stories/`, or is
      marked as spun out into its own queue line per "Per-story delivery"
- [ ] All story files have Gherkin criteria and the header
      (`story`, `depends-on`, `shippable`, `status`)
- [ ] The BPMN diagram reflects both the happy path and the failures (for L; for M — if there is one)
- [ ] "Operation completeness": every cell is filled in for each entity — either a
      role or "no: why"; non-obvious operations (edit, cancel, deactivate)
      are decided explicitly, not skipped in silence
- [ ] Out of scope is filled in
- [ ] The decomposition table in `spec.md` is filled in, tasks have their
      dependencies set (by slug; the orchestrator will enter the queue lines in `main` after
      approval)
- [ ] The feature's `status.md` is updated: `stage: 02-requirements-review`
