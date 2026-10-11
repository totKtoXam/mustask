---
description: Stages 8–10 — code review, gate-3, MR in GitLab and Complete
argument-hint: <feature id>
---

Feature: $ARGUMENTS

0. If `size: F` — code review and QA do not run: go straight to point 3
   per `docs/ai-workflow/stages/f-route.md`.
1. Check in status.md that QA has passed (no blockers).
2. Run the **code-reviewer** subagent (stage 8). For L features —
   additionally a SECOND code-reviewer as a separate call, only for the
   security checklist (see 06-code-review.md).
   Blocker/major findings → send back to the corresponding developer, then
   a delta read within the caps (`docs/ai-workflow/routing.md`, "Ceremony
   caps by size": S/M one, L two); QA re-runs on the affected part only when
   the fix changed runtime code or tests.
3. Prepare the gate-3 package for the human per
   `docs/ai-workflow/stages/07-mr-complete.md`: summary, how to run
   it, what to check by hand. Wait for the verdict, record it in
   status.md.
4. After `gate-3: approved`: rebase on main, a final run of
   tests/linters, `stage: complete` in status.md (goes out in the same
   squash commit), push the branch and open an MR via `glab` per the
   "Opening the MR" section of the stage instructions — squash on, branch
   deletion OFF. In chat — a short notification with the link, then stop:
   the human presses Merge.
5. After the human merges: `git fetch origin && git switch main && git
   merge --ff-only origin/main`, `git worktree remove <path>`. New tasks
   found along the way are entered by the orchestrator as lines in the
   `docs/backlog.md` queue, in a separate commit to main, already
   numbered — the number is issued by putting the line in the queue, not
   by creating a branch (docs/features/numbering.md). Do not delete the
   server branch.
