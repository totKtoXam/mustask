# New session, or carry on in this one

The shared contract for every vendor. `scripts/handoff.sh` implements the
mechanical half; the `feature` adapters
(`.claude/commands/feature.md`, `.agents/skills/feature/SKILL.md`) plug it
into their own command mechanism and add nothing of their own. There is
deliberately no separate `handoff` command: `/feature <id>` faces the same
decision this file answers, and two entry points would force the human to
make it before knowing the answer.

## What compaction already does, and what it does not

The obvious objection to any of this is that auto-compaction handles it. It
mostly does, and the measurement says so:

| | median |
|---|---|
| fresh session, turn 1 | 42,113 |
| first turn after a compaction | 42,236 |
| ceiling before a compaction | 279,546 (max 703,829) |

A compaction returns the context to **exactly** a fresh session's floor —
1.0x, not approximately. The summarising turn reads ~280K, but once per
cycle: about 0.45% of everything this repository has spent. So on cost, a
handoff buys almost nothing over compaction. Its one remaining money argument
is timing — a compaction fires when the window fills, at a median of 279K and
once at 703K, which is mid-task as often as not; a handoff fires at a seam
you chose. Worth single digits of percent, and only when the seams line up.

What compaction does **not** do:

- **Contamination.** The summary of the finished task travels into the next
  one whole, and in lossy form: what was tentative in the conversation is
  stated as settled in the summary. A fresh session carries only files.
- **Testing `status.md`.** [stages/00-orchestrator.md](stages/00-orchestrator.md)
  claims a fresh session can resume the work from that file alone. While a
  session lives on compactions it always has a summary to lean on, so the
  gaps in the file never surface. The claim is only ever checked by someone
  actually starting from the file.

So: compaction handles the money, a handoff handles the correctness. The rule
below is driven by contamination, not by cost.

## The rule

The question only arises when picking up **new work**. Mid-task you never
hand off — you finish, or you let the compaction happen.

| Situation | Session | Why |
|---|---|---|
| Review, QA or a gate on what this session produced | **new** | clean context, `AGENTS.md` point 5 — the reviewer must not inherit the author's blind spots |
| The next task is independent | **new**, whatever the prefix | contamination, and `status.md` has to be the channel |
| The next task is downstream — the next stage of the same feature, or its `deps` names the one just finished | carry on | here the context is an asset; let compaction do its work |
| The same task, after an interruption | carry on | not new work |
| Carrying on by the rows above, but the prefix is already ≥ 250K | **new** | a compaction is about to fire, and it will fire mid-task |

Only the last row needs a number, and it is measurable:

```sh
python scripts/usage-report.py --self
```

The session reads its own transcript — the file Claude Code is writing as it
goes — and prints the prefix of its last turn. Where that is unavailable
(another vendor, a headless run) the fallback is cruder and stated as such:
if a task has already been finished in this session, start a new one.

## The handoff itself

```
1. The session finishes a task.
2. The human asks what else there is.
3. The session answers from `scripts/backlog.sh`.
4. The human names one: "start tech-004".
5. The session applies the rule above. Carrying on — it just carries on.
   Handing off — `scripts/handoff.sh tech-004`, then it shows the human the
   prompt and stops.
6. The human reads the prompt and, if it is wrong, says so.
7. The human delivers it: a background task chip they click, or — no chip
   available, or a terminal preferred — the command the script printed.
   Either way it is a human who starts the session and stays to answer it.
8. Only after it has started does the old session offer to archive itself.
```

Step 5 is the point of the whole thing. The prompt is composed by the only
party holding both the artifacts and the conversation, and read by the only
party who can say it is wrong — before a new session spends anything on it.

## What goes into the prompt

Only what the new session cannot read for itself:

1. the id, and whether the task is started or still queued;
2. the working directory;
3. what to read first — `status.md`, then the stage file;
4. the facts that live in the queue line rather than in a file: size, epic,
   dependencies;
5. one instruction to name itself (below).

Everything else it reads from the artifacts. A prompt that explains the task
instead of pointing at it has copied the context, not moved it — the same
rule a delegation brief follows in
[stages/00-orchestrator.md](stages/00-orchestrator.md).

What must **not** go in: a summary of the finished conversation, findings
already written into `review.md` or `qa.md`, an explanation of the codebase,
or a decision belonging to a gate.

`--note` exists for the fact that is true, needed, and in no file. Using it is
a signal, not a feature: what had to be said there belongs in `status.md` (or,
for a task not yet started, in the `note:` field of its queue line), and the
script says so after every run that uses it. A handoff that needs no note is
the evidence that `status.md` does what this rule claims of it.

## Naming

The new session names itself, first thing, with the full id —
`tech-004-gitlab-ci`. Not the launcher: a title set by the session works the
same whether it was started from a terminal, from a task chip or by another
vendor, and the id already carries both the number and the name, matching the
branch, the folder and the queue line. The queue line's description does not
go in the title — it is long and gets truncated.

The CLI form also passes `-n '<id>'`, which is belt and braces for a session
that never gets as far as titling itself.

## Vendors

The command directory can choose where a **new** CLI session starts. It does
not relocate an existing desktop session. After `new-task`, the session doing
the work must be bound to the existing task worktree and prove its
host/project root, Git toplevel and branch there. A shell `cd`, `git -C` or a
tool-local workdir is insufficient; an IDE/Open target, when surfaced, must
also identify that worktree.

| Situation | Claude Code | Codex |
|---|---|---|
| Task not started yet | a background task chip — the app spawns the session with its own worktree, which is right, because the new session creates the branch itself | `codex "$(cat <prompt>)"` from the main worktree |
| Task already started | use a confirmed native relocation of the current session when available; otherwise the human starts an ordinary interactive session in the existing task worktree | the same; `codex "$(cat <prompt>)"` starts the replacement from the existing task worktree |

For a started task, reuse its task worktree, branch, dirty and ignored files.
Do not remove or recreate the worktree just to bind a session, and do not run
`new-task.sh` again. A vendor operation that can move another thread, register
a managed checkout, or set a command directory is not proof that the current
session relocated. Use native relocation only if it is supported for the
current session and reports confirmation.

When native relocation is unavailable, the original session writes or checks
the handoff prompt, stops work, and the human starts the interactive
replacement from the existing task worktree. The replacement confirms its
host/project root, Git toplevel and branch before it resumes. This retains the
human at every gate; it does not authorize an unattended or background
replacement.

If the vendor's CLI is not installed, the script says so and prints what to
install instead of a command it knows will fail. It does not install
anything: that is a change to the machine, and the human makes it.

**Not in this contract:** `claude -p`, `claude --bg`, `codex exec`. They start
from a tool call and need no human, which is the whole problem — nobody can
talk to them, so they cannot pass a gate, and every route except `F` has
gates. Where a run genuinely needs no human, the answer is the
`task-executor` subagent from
[stages/00-orchestrator.md](stages/00-orchestrator.md), which is cheaper than
a whole process that re-reads `AGENTS.md` from scratch.

A human-delivered interactive session is distinct from the unattended forms
above. A task chip can be such delivery for a queued task; for a started task,
the human starts or relocates an ordinary session at the existing task
worktree. That session asks its own questions and waits for its own gate-3.
This is why the handing-off session cannot start the next one itself: an
`AskUserQuestion` form only asks, and a `claude` process started from a tool
call has no terminal and never appears in the sidebar.

## Archiving the old session

Offer it **after** the new session has started, never before and never
automatically. The old session is still the only place holding the
conversation, and the new one may need something from it in its first minutes.
Archiving is reversible; what it stops is not.

In Claude Code the session can archive itself (`archive_session`, with
`self`), which ends the conversation, and the app asks the human first. In
Codex it is `codex archive <id>`. Either way it is a question put to the
human, not a step the session takes on its own.

## What this does not do

It does not decide what to work on next — that is `scripts/backlog.sh` and
the human. It does not create the branch or take the queue line either: the
new session does that itself after the route is confirmed, because the route
is a human decision and confirming it is the first thing that session does.
