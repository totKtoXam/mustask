# S route — bugfix / tech / small change (≤ ~2 h, 1–5 files)

**Roles:** backend-developer or frontend-developer → qa-engineer → code-reviewer
**Models:** dev/QA — standard, review — deep

A shortened route from [routing.md](../routing.md): stages 1–3 are skipped,
of the three human gates one remains — the final one.

## Process

1. **Start (orchestrator).** The task folder is created as usual:
   `docs/features/<id>/status.md` (type: bugfix|hotfix|tech, size: S,
   branch: `<prefix>/<NNN>-<slug>` — `bug/014-amount-rounding`). For `hotfix`
   the branch is cut from fresh `main`, not from the current working one. In
   `status.md`:
   - `stage: 04-backend` or `05-frontend` (where the work starts);
   - `gates: gate-3` — there is no spec and no design, which means the first
     two gates have no subject; in the `## Gates` section only
     `gate-3: pending` remains;
   - the `## Plan` section — 3–5 lines: what we fix/change, where, how we
     check it.
   The spec and the design are not written. For a bugfix the first line of the
   plan is the steps to reproduce the bug.
2. **Development.** By the conventions (`docs/conventions/*`). A test that
   reproduces the bug / pins the new behaviour comes together with the code;
   for a bugfix the test must fail before the fix. `api-contract.md` is needed
   only if the task changes the API contract — then the rule about marking it
   in `status.md` applies as usual.
3. **QA (a separate session, clean context).** In a light form: a full test
   plan is not written. The QA agent checks that the plan from `status.md` is
   covered by tests, runs the tests of the affected module + smoke of the
   neighbouring ones (the whole suite is `VERIFY_FULL=1 bash scripts/verify.sh`,
   instant when the developer's run of this tree left its stamp — never a
   hand-run of the suite, see [../README.md](../README.md), "One full run per
   tree"), looks for regressions around the change. Defects — as a
   list in `review.md`; the re-checks follow
   [04-backend.md](04-backend.md), § 5 item 3 — at most two, a minor is a
   debt — and the cycle ends when no blocker or major is open. The model
   tier is `standard`; the rules of "Saving runs" from
   [04-backend.md](04-backend.md) apply. The scope of checking is set not by
   the size of the task but by what was changed: `scripts/verify.sh`
   classifies the affected files and runs only the checks that make sense for
   them. A task that does not touch the code does not bring up Testcontainers;
   a change to `Directory.Packages.props` does — that is the dependency graph
   of the solution, not "just a config". Proofreading by the orchestrator no
   longer counts as a check: it has neither a command nor an exit code.
   QA also asks the E2E question here — S has one QA session, so it is
   always the last one before review — through the same form as
   [05-frontend.md](05-frontend.md), step 3, and records the answer in
   `status.md` under Decisions.
4. **Code review (stage 8).** As in
   [06-code-review.md](06-code-review.md), unchanged — the size of the
   task is no reason for a weak review.
5. **Human Gate 3 → MR & Complete.** As in stages 9–10: `stage: complete` in
   the branch, push, MR via `glab`, a short notification in the chat. For the
   human in the summary — the plan, what changed, how to check it by hand.

## Definition of Done

- [ ] `## Plan` in status.md is filled in before the code is started
- [ ] For a bugfix: there is a test that failed before the fix
- [ ] `scripts/verify.sh` is green; the command and the exit code are written
      into `## Log`
- [ ] The review is passed, `gate-3: approved`
- [ ] For a hotfix that falls under "`L` thresholds" of routing.md (an
      irreversible migration, an external counterparty, auth/secrets): there is
      a tech task in the backlog queue for the same place going the full route
      — without it the hotfix is not merged

## Light cycle (docs-only under standing approval)

If both conditions hold — (1) the task changes only `docs/`, `.claude/`,
`scripts/` or the hooks, runtime code is not touched; (2) the human granted
standing approval for this class of work (`AGENTS.md`, "Standing approval") —
then separate QA and review sessions are not started:

- the check is `scripts/verify.sh` (the command and the exit code into
  `## Log`);
- the review — the human sees the full diff in the MR itself;
- everything else unchanged: the task folder, the branch, the worktree,
  writing the plan before the changes, squash merge, the branch on the server
  is not deleted.

There is no QA session to ask the E2E question in, and no runtime change for
it to cover — the light cycle does not ask it.

At least one affected runtime file appeared — the cycle is the usual one, S
with no discounts. Legalized on 2026-08-23 following an analysis of the
workflow: a week of practice showed that for changes to the rules the full
cycle duplicates verify.sh and the human's diff review, finding nothing beyond
them.
