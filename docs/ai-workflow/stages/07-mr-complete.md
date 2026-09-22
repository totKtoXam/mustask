# Stages 9–10 — Human Gate 3 → MR & Complete

**Roles:** the human → orchestrator · **Model:** fast
**Input:** a green review ([06-code-review.md](06-code-review.md)), green QA
**Output:** an MR in GitLab, `stage: complete` in the branch's `status.md`

`07-mr-complete` is not a value of the `stage:` field. Between
`06-code-review` and `complete` the task stands at `gate-3`; this file is what
the orchestrator does while it stands there, not a state the task sits in.

## Human Gate 3 — review by the user

The orchestrator prepares for the human:

- a summary of the feature: what was done, deviations from the spec (if any),
  resolved review findings;
- launch instructions for manual checking (docker compose / commands);
- a list: what exactly to look at by hand (from the acceptance criteria).

The verdict lives in `status.md` and carries the MR link — see "The gate-3
verdict and the MR link" below.

## 9–10. MR & Complete

After `gate-3: approved` — or, under standing approval (AGENTS.md,
"Standing approval"), right after green QA and review: gate-3 stays
`pending`, and the human expresses the approve with the Merge button (git.md
§ "Merge Request", item 1; `rejected` blocks in both modes):

1. Rebase onto `main`. If main brought its own EF migrations — the feature's
   migrations are regenerated on top of fresh main (a model snapshot conflict
   is the normal scenario for this step, not a surprise). Do not run the full
   suite by hand here: `pre-push` runs `scripts/verify.sh` on the way out
   (step 1 of "Opening the MR"), and it is the same run. Run something
   targeted only if the rebase actually touched the code — regenerated
   migrations, a resolved conflict — and even then only over what moved.
2. **Complete goes into the squash itself, it does not catch up with it.**
   Before the push, already after `gate-3: approved`, `stage: complete` is set
   in the task branch — and that state arrives in `main` together with
   everything else in a single squash commit. A separate "mark complete after
   merge" commit in `main` is not made: `pre-commit` lets only merges into
   `main`, and such commits either broke through the hook by bypassing it or
   would require an exception for it — both options are worse than the
   `stage: complete` line written a minute before the actual merge.
   `docs/backlog.md` is not changed in the task branch at all — neither on
   completion (the task's line left the queue at the start), nor for the sake
   of debts found: their wording is passed to the orchestrator as a list, and
   it enters them as lines in the queue with a separate commit in `main` —
   already with numbers, because a number is issued only by a commit in `main`
   ([../../features/numbering.md](../../features/numbering.md)). In the request to
   the orchestrator the number is written as the literal `NNN` — the queue
   will issue it.
3. Pushing the branch and **opening the MR — is done by the agent** (how
   exactly — below).
4. **The Merge button is pressed by the human in GitLab.** The agent does not
   merge, does not delete the branch and does not consider the task finished
   until confirmation. The full rules of branching, commits and releases —
   [../../conventions/git.md](../../conventions/git.md).

## Opening the MR

The final stage of the pipeline is a **merge request in GitLab**, not a chat
message, whenever the repository has a remote (`origin`, self-managed
GitLab): a task that reached the end must be visible to everyone, not only in the
correspondence of one session: across several parallel branches you cannot
tell by eye what is already merged and what is not, and one session's local
`main` is not the source of truth.

**If the repository has no remote**, none of this section applies: there is
nowhere to push and no MR to open. The task ends as a local squash into
`main` — `scripts/finish-task.sh [<branch>] [--message-file <path>]
[--dry-run]`, `finish-task.ps1` in PowerShell. The acceptance rule does not
change: it merges only when the task's `status.md` carries `gate-3: approved`
or a standing approval, and only when `verify.sh` is green; otherwise it
refuses and says which of the two is missing. Everything below is for the
remote case.

The tool is `glab` (GitLab CLI). The first time in an environment it has to be
configured: see "GitLab CLI" in
[../../conventions/git.md](../../conventions/git.md) — the same place lists the
rakes `glab auth login` steps on.

```sh
# 1. The branch goes to the server. pre-push runs scripts/verify.sh by itself.
git push -u origin <branch>

# 2. MR from the branch. The title is English — it also becomes the squash commit.
glab mr create     --source-branch <branch>     --target-branch main     --title "feat|fix|tech(<scope>): <essence> (<short id>)"     --description-file <description file>     --squash-before-merge     --yes
```

- **`--squash-before-merge` is mandatory.** One task — one commit in `main`.
  Check after creation: `glab mr view <iid>` (or the API field `squash`) — the
  push option `merge_request.squash` with `git push -o` is silently not
  applied, we have already been burned by this.
- **Do NOT delete the branch after the merge** — neither with the
  `--remove-source-branch` flag, nor with the checkbox in the MR. The task
  branch stays on the server: it is what the fine-grained history, collapsed by
  the squash into one commit, is restored from.
- **The description goes as a file**, not as a string:
  `git push -o merge_request.description` and `--description` do not accept
  line breaks. The file is placed in the session's temporary directory, not in
  the repository.
- A red run is a stop. An MR is not opened with failing tests or linters;
  `pre-push` is exactly what checks this, bypassing it with `--no-verify` is
  not allowed.

### What goes in the MR description

The same blocks that used to go into the chat — now they live in the MR and are
available to everyone, not to one session:

```
Task documentation: docs/features/<id>/  ·  ADR (if any)

## Essence
<what was done, deviations from the spec, key decisions>

## Tests
<command> — <result>
not run: <what and why — Docker, network, absence of an environment>

## Gates
- [x] gate-1 <verdict>
- [x] gate-2 <verdict>
- [x] gate-3 <verdict>

## Deliberately left open
<unclosed manual check items, accepted minor findings, debts>
```

### Chat notification

After creating the MR the agent sends the human a **short** message and stops:

```
MR SENT · <id>

MR:       <url>  ·  <branch> → main
Commits:  N above main — of them mine M, other people's K
Tests:    <command> — <result>
Gates:    gate-1 <verdict> · gate-2 <verdict> · gate-3 <verdict>

Remaining: press Merge in GitLab.
```

Several tasks are ready — send a **combined** notification: one table
(MR · branch · diff stat · tests · whether the changed files overlap with
neighbouring branches from the list). MRs are merged one at a time, after each
one — a check that the next does not conflict with the updated `main`.

Filling rules:

- **Count other people's commits separately.** A branch cut not from `main`
  drags someone else's work along — the squash will haul it into `main` whole.
  If K > 0, first rebase onto `main`, and only then push and MR.
- Name skipped tests explicitly. "Not run because of Docker" is an honest
  result; the same run presented as green is a reason not to trust the whole
  report.
- Do not make anything up: an unclosed question from the review goes into the
  MR description as a line, it is not hushed up.

### The gate-3 verdict and the MR link

The merge happens in GitLab, and `status.md` goes into `main` with the same
squash commit — that is, **at the moment Merge is pressed the file still says
`pending`**, and that is how it stays in the history. The file starts lying:
the task is accepted, but the record says "waiting". Hence a rule of two
halves.

**1. The record always carries the MR link.** The link is put in right after
the MR is created, as a separate commit into the same branch (the MR will
update itself), not after the approve:

```
gate-3: pending · MR !3 https://gitlab.example.com/<group>/<project>/-/merge_requests/3
gate-3: approved · MR !3 https://gitlab.example.com/<group>/<project>/-/merge_requests/3 · merged 2026-08-31
```

`approved` without a link is an unfilled record, not "just an old format".
And it is the link that turns `pending` from a lost verdict into one click.

**2. Read the state from the file, go to GitLab — only on `pending`.**

| In the file | What to do |
|---|---|
| `approved` | Trust the file. Do not go to GitLab — the verdict is already recorded and confirmed by the link |
| `pending` | Check whether the task is merged. Merged — bring the record in order via the F route |
| `rejected: <findings>` | The work is not finished, the MR is not merged |

Checking mergedness is **local, without the API**: the squash commit carries
the short id in the title, so `git log origin/main --grep '(tech-004)'` is
enough. The MR link is already in `status.md` itself, there is no point pulling
`glab`.

The reconciliation across all tasks is done by `scripts/check-gates.sh`: it
finds `pending` on a merged task and `approved` without a link. Tasks from the
pre-GitLab era (merged with a local squash, when the MR did not exist) it skips
and says their count out loud — demanding a link from them is pointless. The
script fixes nothing: it is a report, not a gate.

**Fixing — in a batch via the F route.** Not one record per MR: one branch, a
change to all the records found, one MR.

> **An honest caveat.** The reconciliation task does not close itself: its own
> `status.md` will go into `main` with `pending` for exactly the same reason.
> There is no perfect closure here, and there is no need to pretend there is —
> the discrepancy is caught by the next reconciliation, and all that time the
> MR link shows the real verdict.

### After the merge

The human pressed Merge — the session syncs local `main`
(`git fetch origin && git switch main && git merge --ff-only origin/main`) and
removes the task's worktree (`git worktree remove <path>`). The local task
branch is deleted, the server one stays.

If `git merge --ff-only` does not go through, local `main` has diverged from
`origin/main` — do not "fix" this with another merge commit, but work out where
the local commits came from (usually these are merges made around the MR).
