# Stage 0 — the orchestrator itself

**Role:** orchestrator — the session the human talks to · **Model:** standard
(the tier of `task-executor`, the role this stage delegates to; the
orchestrator's own model is the session's and is not chosen per turn)
**Input:** the human's request, `scripts/backlog.sh`, the task's `status.md`
**Output:** a classified, queued, delegated and merged task — and as few of
its turns as possible taken in this context

Every other stage file says what a role does. This one says what the
orchestrator must **not** do itself, because for a long time nothing did, and
the orchestrator quietly became the most expensive author in the pipeline.

## The measurement this file exists for

`scripts/usage-report.py --by role`, over the whole history of this
repository:

| Role | Turns | Average prefix per turn | Share of cache read |
|------|-------|------------------------|---------------------|
| orchestrator | 4537 | 277,308 | 81% |
| general-purpose | 1688 | 116,268 | 13% |
| the pipeline roles | 1223 | 72K–89K | 6% |

The orchestrator's turn is three times the price of the same turn taken by a
subagent, and for one reason: its context carries every task the session has
already touched. One session here handled 14 branches, another 19 — task
number 15 pays for the fourteen before it until a compaction cuts them off.
63% of the whole bill is spent on turns whose prefix had already passed 300K.

And what those turns were spent on:

```
Bash    1312  52.3%       Agent   91   3.6%
Edit     396  15.8%
Read     209   8.3%
Write    201   8.0%
```

2508 tool calls, 91 of them delegations. The orchestrator was writing the
code, running the checks and editing the docs — at the highest per-turn price
in the system.

## Its own hands

Short list, and it is short on purpose. The orchestrator does what cannot be
handed to a fresh context without handing over the whole conversation with it:

- talking to the human — the `ROUTE` block, the gates, the escalations;
- classification: type, size, route, gates (`routing.md`);
- the queue and the number — `scripts/queue.sh add|take|withdraw`;
- `status.md`: the stage, the gate verdicts, the `## Log`. No other role
  writes this file (`feature.md`, "Context isolation and file ownership");
- choosing who runs next, and writing their brief — within the task's
  ceremony caps ([routing.md](../routing.md), "Ceremony caps by size"): one
  more review read, E2E run or check pass than the table allows runs only
  when the human asks for it, and the ask goes into `## Log`;
- the ending: `scripts/finish-task.sh`, or the MR when there is a remote.

## Delegated

Everything else, and specifically every run that ends in a diff: writing code,
writing docs, running and reading `verify.sh`, exploring the codebase to
answer a question. The pipeline roles already cover the stages; what had no
owner is the plumbing between them, and that is now `task-executor`.

**The threshold is a run, not a call.** A subagent pays ~40K to enter — it
reads `AGENTS.md` and its stage file before it is useful. Delegating one
`git status` is a straight loss. Delegating ten turns of editing saves
roughly `10 × (277K − 90K) − 40K ≈ 1.8M` tokens. So: hand over a *piece of
work with an end state*, never a single command.

This is also why `stages/f-route.md` keeps size `F` in the orchestrator's own
hands. An `F` is a handful of turns; it is below the threshold, and the
delegation would cost more than the task.

## The brief a delegated run gets

The receiving context knows nothing. It also must not be given everything —
that would rebuild the prefix the delegation was meant to escape. Give it
exactly:

1. the task id and the current stage;
2. the paths it may write, by name (`docs/conventions/git.md`, "Parallel
   work") — never "the docs" or "the feature";
3. the artifacts it must read, by path — not "read the feature folder";
4. the end state that means it is done, in one sentence;
5. what to report back.

Domain facts are not pasted into the brief: they are in the feature's
artifacts, and the agent reads them itself. A brief that explains the task
instead of pointing at it has copied the context, not moved it.

## What comes back

A summary, not a transcript: what changed, by path; what `verify.sh` said;
what it could not do and why; anything that needs a decision. The
orchestrator writes the outcome into `status.md` and, if the run touched a
gate, puts it to the human.

If a run comes back needing a decision the orchestrator cannot make from the
artifacts, that is an escalation to the human — not a reason to reopen the
work in this context.

## When the next task gets a new session

The pipeline already says state is handed over through files, not through
chat context (`AGENTS.md`, "How agents work here", point 3). The corollary is
that a task boundary is where a session can end — but not that it always
must, and the first version of this file overstated it.

The overstatement was a cost argument, and the measurement withdrew it: an
auto-compaction returns the context to a fresh session's floor **exactly** —
42,236 against 42,113, 1.0x — and the summarising turn it costs is about
0.45% of everything spent here. Carrying on in a compacted session is not the
expensive choice it was claimed to be.

What a compaction does not do is leave the finished task behind. Its summary
travels into the next task whole, and lossily: what was tentative in the
conversation reads as settled in the summary. And while a session always has
a summary to lean on, `status.md` is never actually tested — the claim that a
fresh session can resume from that file alone is only checked by someone who
starts from it.

So the rule is about contamination, not money, and it is a table rather than
a slogan: [handoff.md](../handoff.md). In short — review, QA or a gate on
this session's own work, and any independent next task, get a new session
whatever the prefix; work downstream of what was just finished carries on;
and above a 250K prefix, carry on with nothing, because a compaction is about
to fire mid-task. `scripts/handoff.sh` writes the prompt for the cases that
hand over, `python scripts/usage-report.py --self` answers the one row that
needs a number, and neither starts a session — the human does.

## Verifying this stays true

After a change to the routing, the tiers or this contract, run

```sh
python scripts/usage-report.py --by role
```

and compare the orchestrator's turn count and average prefix against the
table above. The claim this file makes is measurable; a change that does not
move those two numbers did not do what it said.
