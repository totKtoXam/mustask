# F route — Fast, a trivial change

**Performer:** the orchestrator in its own session · **Model tier:** `fast`
**Input:** a description of the change from the human
**Output:** the change in the task branch + `status.md` (`## Plan`, `## Log`)

The shortest route: no spec, no design, no implementation plan, no separate
QA session and no separate code review. One human gate remains — the final
one, where the human sees the full diff.

## When F is allowed

A closed list. What is not listed is `S`:

- texts and typos in `docs/`, README, code comments;
- formatting, applying the linter, import order;
- renaming a local variable or a private method **within a single file**;
- a broken link, a typo in a path, a stale file name in the documentation;
- a change to an error message or a log **without changing the conditions**
  under which it is emitted;
- reconciling `gate-3` with the actual state of `main` — setting `approved`
  with the MR link where the file still says `pending` while the task is
  merged (`scripts/check-gates.sh`, the rules are in
  [07-mr-complete.md](07-mr-complete.md), "The gate-3 verdict and the
  MR link"). Fixed in a batch: all the records found in one branch and one MR.

The list is closed deliberately. Five abstract criteria of the form "does not
affect the architecture" the agent will fill in affirmatively, because that is
shorter; a list of concrete cases — it will not.

## When F is forbidden

- **Observable behaviour** changes — any of it: an API response, output in the
  UI, the order or the conditions of execution. That is at least `S`.
- `bugfix` and `hotfix` — never F: a bug has a test that failed before the fix,
  which means there is a change of behaviour too.
- auth, permissions, secrets, the API contract, the data schema, migrations,
  CI, infrastructure, integrations, architecture or business rules are touched.
- A change in several files connected by logic (rather than by the same typo).
- **In doubt — it is `S`.** The doubt here is itself the sign of
  non-triviality.

## Process

1. **Start (orchestrator).** As usual and without shortcuts: a task line with a
   number in the queue `docs/backlog.md` of the `main` branch (no line — first
   put it there with a commit into `main`), then the branch, a separate
   worktree and the folder `docs/features/<id>/` with `status.md` via
   `scripts/new-task.sh` by the rules of
   [../../features/numbering.md](../../features/numbering.md)
   and [../../conventions/git.md](../../conventions/git.md). The triviality of
   the change is no reason to skip the queue or to work in someone else's
   directory: there are no exceptions for `F`/`S`, only `hot` is started out of
   the queue.
2. **Before the first change** in `status.md`: `size: F`, `gates: gate-3`, and
   in `## Plan` — one line
   of the form "what we are changing · which item of the allowed list it falls
   under · what we check it with". The default check is `scripts/verify.sh`: it
   looks itself at which files are touched and runs only what makes sense for
   them. "Check by eye" does not count as a check — it has no exit code.
3. **The change.** The orchestrator makes it itself, in this same session. A
   separate dev subagent is not needed: the work is mechanical.
4. **The check.** Run `scripts/verify.sh` (or the command from the plan, if it
   is narrower), write into `## Log` the command itself and the exit code. A
   red result is a stop, see "Escalation".
5. **The final human gate.** The orchestrator prepares the package for the
   human per [07-mr-complete.md](07-mr-complete.md) — what was changed
   and the result of the check — and stops. The human looks at the full diff in
   its entirety: on F that is the review. The `gate-3` line is written by them
   alone.

Separate `spec.md`, `design.md`, `plan-*.md`, `qa.md` are not created for F.
There is no QA session, and F never changes observable behaviour — the E2E
question (`routing.md`, "Rules for skipping stages") does not apply here.

## Escalation to S

If along the way it turned out that the change goes beyond the allowed list,
the diff grew past what was intended or the check went red — the task
**becomes `S`**: in `status.md` `size: F` is changed to `size: S`, the reason
is written into `## Log`, and further work goes by [s-route.md](s-route.md)
with full QA and code review.

Going back to F after an escalation is forbidden. The cost of a
classification error must be asymmetric: it is easy to go up, impossible to go
down — otherwise F becomes a way to bypass the process.

## Definition of Done

- [ ] `size: F` and the one-line `## Plan` are written **before** the first
      change
- [ ] The change fits into the closed list of what is allowed
- [ ] `scripts/verify.sh` is green; the command and the exit code are written
      into `## Log`
- [ ] Observable behaviour has not changed
- [ ] The branch is pushed, the MR is opened (squash on, branch deletion off),
      the link is sent to the human; the Merge button and `gate-3` stay theirs
