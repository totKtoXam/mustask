# Feature orchestration — the single contract

This file is the shared source of the orchestrator's behaviour for Claude Code
and Codex. Vendor adapters only plug this contract into their own
skill/command and subagent mechanism; the rules of the route, the gates and
state hand-off are not duplicated here.

What the orchestrator may do with its own hands, and what it must hand to
a `task-executor` instead, is in
[stages/00-orchestrator.md](stages/00-orchestrator.md). Read it once: an
orchestrator turn is measured at three times the price of the same turn in
a subagent, and that file is the only place the boundary is drawn.

## Starting a task

0. `bash scripts/backlog.sh` (`scripts/backlog.ps1` in PowerShell) is the
   state of the project in one command: what is in flight and at which stage,
   what is queued by epic, how each epic stands, which numbers are taken.
   `--done` lists what is already finished — check it before filing something
   that may already exist. Run these instead of opening `status.md` files one
   by one.
1. For a **new** task, read `docs/ai-workflow/README.md` and
   `docs/ai-workflow/routing.md`: they are what turns a request into a type,
   a size and a route. For a task that **already exists** — the id, type,
   size and stage are recorded in its `status.md` — skip both. Classification
   has already happened, its result is in the file, and re-reading the rules
   that produced it changes nothing except the size of the context. Read the
   routing rules again only when the route itself is in question: an
   escalation, a re-sizing, a change of type.
2. If the task already exists, read its `docs/features/<id>/status.md` and
   only the artifacts the current stage needs. `review.md` and `history.md`
   are not part of that set: open them when you have a reason — a finding to
   resolve, a decision to trace — never by default. Continue from the
   `stage:` in the file, not from the beginning and not from the chat's
   memory.
   `gate-3: approved` — trust the file, do not go to GitLab. `gate-3: pending`
   — check whether the task has already been merged (`git log origin/main
   --grep '(<short id>)'`): the merge happens in GitLab, and the record could
   have gone stale (`stages/07-mr-complete.md`, "The gate-3 verdict and
   the MR link").
3. For a new task classify the type, the size `F/S/M/L`, the route, the gates
   and the artifacts according to `routing.md`. Show the human the `ROUTE`
   block and stop until an explicit confirmation. Before the confirmation it
   is forbidden to create a branch, a worktree, a `status.md` or to change
   files. If the task is not yet in the queue of the `main` branch — the
   line is first put there with `scripts/queue.sh add <prefix> <slug>
   "<description>"`, which issues the number (it is issued by the queue, not
   by a branch).
4. After the confirmation create the task according to
   `docs/features/numbering.md`. Use `scripts/new-task.sh` in a POSIX shell or
   `scripts/new-task.ps1` in PowerShell. The script takes the id from the
   queue line in `main` literally (outside the queue — only `hot`), claims the
   work by creating the branch, creates the worktree and `status.md`; a taken
   branch = the task has already been taken, take the next line.
5. Write the confirmed classification and the grounds for the size into
   `## Log`, remove the line of the taken task with `scripts/queue.sh take
   <short-id>` — it refuses while no branch holds the number — as a separate
   commit into `main` that changes only `docs/backlog.md`, then run all the
   work in the task worktree. `main` stays the hub and is not otherwise
   edited.

## Route

Size `F` is done by the orchestrator itself according to `stages/f-route.md`,
without subagents, without separate QA/review sessions and without additional
artifacts.

For the other sizes apply only the relevant roles in this order:

```text
business-analyst → requirements-reviewer → gate-1 →
ux-designer → gate-2 → backend-developer → qa-engineer →
frontend-developer → qa-engineer → code-reviewer → gate-3 → merge request
```

Skipped stages and shortcuts are determined by `routing.md`:

- `bugfix`, `hotfix` and `tech` start from development;
- with no UI the UX and frontend stages are not performed, `gate-2` has no
  subject;
- for `M` gate-1 and gate-2 are shown as a single package, and QA is performed
  as a single pass after the implementation;
- for `L` the backend plan is performed as a separate deep pass, the
  implementation — as a standard pass; QA also gets the deep level;
- standing approval removes only the explicitly permitted ceremony and never
  replaces substantive human gates.

Every role reads the corresponding instruction from
`docs/ai-workflow/stages/*.md` and the project conventions. The vendor adapter
picks the concrete agent profile and model by the `deep/standard/fast` levels
from `routing.md`.

## Context isolation and file ownership

- Every stage is performed by a fresh session or subagent. QA and review are
  never performed by the author of the artifact under check.
- **A task boundary is where a session may end**, and often should. Review,
  QA or a gate on this session's own work, and any independent next task, go
  to a new session whatever the context size — a compaction returns the
  prefix to a fresh floor but carries the finished task's summary across, and
  `status.md` is only tested by someone starting from it. Work downstream of
  what was just finished carries on here. The table, and the prompt that
  starts the new session, are in [handoff.md](handoff.md).
- Pass a role only the task id, the current stage, the needed paths and the
  permitted write zone. Domain facts the agent re-reads from the feature
  artifacts.
- `status.md`, `docs/backlog.md`, `Mustask.sln` and `Directory.*.props` are
  changed only by the orchestrator; the other agents return a request to
  change these files.
- When working in parallel respect the ownership zones from
  `docs/conventions/git.md`. Do not use `git add -A` or `git add .`.

## Gates and defect loops

At every applicable gate show the human a short summary, the diff/artifact and
the open questions. Continue only after an explicit approve/reject. The value
of `gate-N` is set by the human; the agent may only record the verdict
received in the chat, with a date.

A gate request has three parts, and none of them is optional:

1. **What was done and what is being decided** — a short paragraph, not a
   transcript of the stage.
2. **The artifact, by a path that opens.** Give the absolute path inside the
   worktree the work lives in
   (`.claude/worktrees/<name>/docs/features/<id>/...`). A repository-relative
   path is resolved against the checkout the session itself runs in — a
   different worktree whenever the work is not in this session's own, and the
   file is simply not there. The link then fails silently, and the human is
   asked to approve something they cannot see. Confirmed by a click on a
   real task: the file was already in `main` and the relative link still did
   not open, so "it resolves against the main checkout" is not the
   explanation.
3. **The question as options.** Not a paragraph inviting free-form thought: a
   closed list of the real options — two to four of them — where every option
   carries at least one advantage (➕) and at least one loss or risk (➖). An
   option with no downside is not an alternative and does not belong in the
   list.
   - The option the agent recommends comes **first**, marked ⭐, and opens
     with the reason it is the best one.
   - At least one option is marked ⛔ as not recommended, opens with the
     reason it is the worst one, and comes **last** among the real options.
     It must be an option the human could genuinely pick — often the quick
     one with a catch — never a strawman invented to have something to mark.
     When every option is reasonable, the weakest one gets the ⛔ and the
     reason says honestly why it is weaker.
   - When a slot is left, the final option is "didn't understand, explain in
     more detail". Choosing it earns a fuller explanation — context, terms,
     what each option changes — and the same question again.
   - A plain yes/no confirmation of an action already described is asked in
     prose, not as a form; and when there is only one real option, there is
     no question — act and report.

The same three parts apply to every question put to the human, not only to
gates — the route confirmation of [routing.md](routing.md) and the block 3
questions of its checklist included.

The mechanism for the options is the vendor's: in Claude Code the built-in
question form (`.claude/commands/feature.md`), in Codex a numbered list in the
message. The requirement is shared, the means are not.

QA/review return defects to the developer at most twice. After the second
unsuccessful iteration stop and escalate to the human: list the remaining
defects, the options already tried and the blocking decision.

## Completion

After the checks are green and code review is done, stop at `gate-3`. After
`gate-3: approved` set `stage: complete` in the branch, push it, open the MR
via `glab`, write the MR link into the `gate-3:` line and send a short
notification with the link — the sections "Opening the MR" and "The gate-3
verdict and the MR link" in `stages/07-mr-complete.md`. Then stop: the
Merge button is pressed by the human. The branch on the server is not deleted,
neither before nor after the merge.

With no remote the ending is a local squash — `scripts/finish-task.sh`, which
merges only against a recorded acceptance and a green `verify.sh`. Either
way the task is over, and so is the session: the next task starts in a new
one.
