# Task numbering — how an id is issued and held

The id format itself is in [README.md](README.md), together with the folder
contents and the `status.md` format. This file is the allocation rules behind
that format: they are read when a task is set up — the orchestrator issuing a
number, `scripts/new-task.sh` filing the task — and by nobody else.

The two folders under [`docs/templates/examples/`](../templates/examples/README.md)
are illustrations, not tasks: nothing below counts them, and their ids take
no number from any counter.

Most of what follows is here because it was learned the expensive way; the
post-mortems are kept inline on purpose, because a rule whose reason has been
stripped off is a rule the next session talks itself out of.

## Numbering rules

The commands are `scripts/queue.sh add|take|withdraw` (`queue.ps1` in
PowerShell); everything below is what they enforce.

- **The number is issued only by a commit to `main` — when the line is put
  into the queue.** The orchestrator or the human adds the line to
  `docs/backlog.md` already carrying the number; this is the only direct
  commit to `main` allowed (one that changes only `docs/backlog.md`, see
  [../conventions/git.md](../conventions/git.md)). A numbered line living in
  an unmerged branch is a violation: its number is not visible to other
  sessions. Introduced after two incidents: one number went to two tasks,
  and one queue line was implemented twice under two ids.
  Since tech-049 that commit reaches `main` through a PR that `queue.sh`
  opens itself (see git.md): the commit is pushed to a
  `sync/queue-<date>-<op>-<short-id>` branch, and until the PR merges that
  pushed branch is what holds the number — source 3 below reads the queue
  files of every remote-tracking branch. This is the one unmerged branch a
  numbered line may live on. `take` reads the queue on `origin/main`, so a
  line whose add PR is still open cannot be taken yet.
- **Claiming work is the branch, not the queue line.** Taking a task into
  work = successfully creating the branch `<prefix>/<NNN>-<slug>` (the
  single point — `git worktree add -b`, done by `scripts/new-task.sh`). The
  id is taken from the queue line **character for character** — taking the
  same work under a different slug is forbidden, it is a lock bypass. Git
  will not create the branch twice: the second session gets a "branch
  already exists" refusal and takes the next line. Working on a task before
  the branch is successfully created is a process violation.
- **After the start the line leaves the queue; after the merge the task is
  protected by its folder.** The orchestrator removes the taken task's line
  with a separate commit to `main`. The local branch may be deleted after
  the merge (the server one stays, `git.md`), but `scripts/new-task.sh`
  refuses if `docs/features/<id>/` exists in even one ref — a completed or
  cancelled task cannot be started again.
- **The number is computed across all branches at once, not from one
  file.** The next number = the maximum number for this prefix + 1, where
  the maximum is taken over the **union** of three sources. They are listed
  in decreasing order of reliability, but there is no priority between them
  — take the maximum across all three, not the value from the first one
  where it was found:
  1. **branch names** — `git branch --all`, normalized to an id (`tech/005`
     → `tech-005`). The most reliable: the number of a claimed but not yet
     merged task is visible only here. Before computing — `git fetch
     --prune`, otherwise remote-tracking branches show the state of the
     last fetch;
  2. `docs/features/<id>/` directories — across all branches, not just the
     current one:
     `git for-each-ref --format='%(refname)' refs/heads refs/remotes | xargs -I{} git ls-tree --name-only {}:docs/features`;
  3. the queue files — `docs/backlog.md` and `docs/backlog/withdrawn.md` —
     also across all branches, including the `Done` section:
     `scripts/backlog.sh --ids` (one pass over both files:
     `git for-each-ref --format='%(objectname):docs/backlog.md%0a%(objectname):docs/backlog/withdrawn.md' refs/heads refs/remotes | sort -u | git cat-file --batch`).
     "All branches" includes the pushed `sync/queue-*` branches of open queue
     PRs, and local `main` ahead of `origin/main`: before tech-049 (and with
     `--local` still) `queue.sh add` and `withdraw` commit to local `main`, and the push comes
     later, so the number of an unpushed queue commit is on no remote branch
     and a count that reads `origin/main` alone issues it a second time
     (`feat-039` and `mod-017` were issued twice that way). `scripts/new-task.sh
     hot` reads the same files ref by ref, local `main` included; both are
     covered by `scripts/tests/queue-numbering-cases.sh`. A read that fails is
     an error, never an empty list: `backlog.sh --ids` exits non-zero and
     `queue.sh` refuses to issue a number, because a number issued from a
     partial list is a number issued twice.

  The walk is deliberately limited to `refs/heads refs/remotes` — otherwise
  `refs/stash` would end up in the set.

  Under this rule the number is issued by the **orchestrator when adding
  the line to the queue**; to see the taken ones — `scripts/backlog.sh
  --ids`. `scripts/new-task.sh` does not issue numbers (the only exception
  is `hot`, see below); it takes the ready-made id from the queue line on
  `main`.
- **Why not "the source of truth is backlog.md".** The rule was written
  exactly that way before, and it broke: parallel sessions in different
  worktrees took the maximum over **their own** branch's backlog, the
  branches stayed unmerged for a long time, and two numbers in a row each
  went to two different tasks. The backlog becomes shared only after a
  merge — before the merge it is one of the sources, not the primary one.
  This does not
  contradict the rule "the number is issued only by a commit to `main`" —
  it follows from it: the `main` queue is the only place that **issues**
  the number, but the occupancy check still runs over all three sources and
  all branches.
- **Order of setting up a task:** a numbered line in the `main` queue
  (added by the orchestrator/human) → route confirmation by the human →
  branch via `scripts/new-task.sh` — **the first action on the task** →
  work in the worktree. Tech debt found along the way is not written by the
  session into its own branch's backlog — it hands the line's wording to
  the orchestrator, who adds it to the `main` queue (already numbered).
  Before it is added to the queue, a proposed task is referenced by its
  slug; the literal `NNN` (`tech-NNN-<slug>`) survives only in `spec.md`
  decomposition tables of unmerged branches and in such requests to the
  orchestrator — the queue itself has no lines with `NNN`.
- **The only exception to "id only from the queue" is `hot`.** A hotfix
  goes outside the queue: `scripts/new-task.sh` issues it the next free
  number from the three sources, and the number is fixed by the branch and
  the `docs/features/<id>/` folder. The list of exceptions is closed;
  declaring a regular task a hotfix to bypass this is caught at code
  review: a `hot` task without a human route confirmation in `## Log` is a
  violation (`hot` is always `S`, gate-3 is mandatory, and per
  [../conventions/git.md](../conventions/git.md) a hotfix that hits the `L`
  thresholds does not merge without a tech task in the queue).
- **If a number does get issued twice** — the window between computing the
  maximum and creating the branch is not zero — the task whose id has not
  yet leaked into merged artifacts gets renamed. Count references across
  all branches before choosing:
  `git for-each-ref --format='%(refname)' | xargs -I{} git grep -c <id> {}`.
  A rename leaves a trail of broken references; fixing them is part of the
  same task, not deferred — otherwise the old id keeps resolving, but now
  to someone else's task.
- **Numbers are not reused or reassigned.** A cancelled **started** task
  does not free its number: its `docs/features/<id>/` folder stays, marked
  in `status.md`, and must make it to `main` (otherwise, with the branch
  deleted, the number disappears from all three sources and looks free
  again); if the task is dropped and later returned to, the line goes back
  into the queue with a commit to `main` under the **same** number. A
  withdrawn **not-yet-started** task (changed mind before the start) is not
  deleted without a trace: its line moves to
  [`docs/backlog/withdrawn.md`](../backlog/withdrawn.md) — one line with the
  number and the reason. The
  counter (source 3) sees it, the number does not go to another task, and
  `deps:` references and mentions in artifacts do not start resolving to
  someone else's task.
- **Short reference — `<prefix>-<NNN>`** (`feat-001`): used to reference in
  `deps:`, in a PR title, in correspondence. The full `<id>` — in paths and
  branch names. A slug is used to reference only tasks from decomposition
  tables of unmerged branches — they don't have a number yet; the queue's
  `deps:` has no slugs and no literal `NNN`, every reference is the short
  id of an existing line or task. `deps:<epic>` is also forbidden: a
  dependency points at a task (usually the module's founder), not at an
  area.
