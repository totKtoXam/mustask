# Git conventions: branches, commits, PRs, releases

Model — **trunk-based**: one long-lived `main` branch and short-lived task
branches. No `develop` and no `release/*`: a task branch lives days, not
weeks, and merges into `main` whole after a human approve.

## Branches

| | |
|---|---|
| Long-lived | `main` — always green, always ready to ship |
| Task branch | `<prefix>/<NNN>-<slug>` — `feat/001-order-form`, `bug/014-amount-rounding` |

Branch name = task id with the first hyphen replaced by a slash. The id
format and prefixes — [../features/README.md](../features/README.md), the
numbering rules — [../features/numbering.md](../features/numbering.md).

- The branch is created **from a fresh `main`** (after `fetch`, from
  `origin/main`) and is created **first** — that is the claim on the work:
  the id is taken character-for-character from the queue line in
  `docs/backlog.md` on `main`, the single point of creation is
  `git worktree add -b` inside `scripts/new-task.sh`. Working on the task
  before the branch is successfully created is a process violation
  ([../features/numbering.md](../features/numbering.md)).
- One task, one branch. Don't pile incidental changes into it: debt found
  along the way goes as a line into the queue
  [../backlog.md](../backlog.md) **on `main`**, not into the current diff
  and not into the branch's own copy of the backlog. The orchestrator adds
  the line (with the number right away) — the session hands it the
  wording, in the request the number is written literally as `NNN`
  (`tech-NNN-<slug>`), the queue assigns it
  ([../features/numbering.md](../features/numbering.md)).
- Direct commits to `main` are forbidden — only merging an MR. **The
  single exception:** a commit by the orchestrator or the human that
  changes **only** queue files — `docs/backlog.md` and any `.md` under
  `docs/backlog/` — the queue lives on `main`, and lines are added and
  removed there (`.githooks/pre-commit` lets through exactly this case, and
  one foreign path in the same commit cancels the exception). Make the commit
  with `scripts/queue.sh` rather than by hand: it stages only queue files, so
  the exception cannot be lost by accident. With a remote, main takes even
  this commit only through a PR, and `queue.sh` makes that PR itself
  (tech-049): it builds the commit in a throwaway worktree detached at
  `origin/main`, pushes it to a new `sync/queue-<date>-<op>-<short-id>`
  branch, opens a squash PR with `gh`, asks for auto-merge and prints the PR
  URL — or, without `gh` or when auto-merge is refused, what is left for a
  human. The local `main` is never touched, so it is never left ahead of
  `origin`, and the command runs from any worktree. `--local` (chosen by
  itself when there is no `origin`) is the old path: the commit lands on the
  local `main` of the worktree that has it checked out.
- The queue is not part of a task branch's diff: the orchestrator
  removes the taken task's line with a separate commit on `main` at the
  start, so a merge conflict over the queue is impossible.
- **After merge, the branch on the server is NOT deleted.** Squash
  collapses the task into a single commit on `main`, and the fine-grained
  history is the only place where you can see how the task went: the
  rounds of review changes, what got fixed after QA, which commit broke
  what. Deleting the branch loses that forever. The local copy can be
  removed after syncing (`git branch -d`), the server one cannot; in the
  MR "Delete source branch" must be unchecked. The task history in
  `docs/features/<id>/` supplements the branch, it does not replace it.
- Independent tasks are worked in separate worktrees, so that parallel
  agent sessions don't collide in the same working directory.

## Commits within a branch

- Language — English (rule from `AGENTS.md`); this also applies to
  `docs/` artifacts — only product interface copy quoted there stays
  Russian.
- Subject line — imperative, ≤ 72 characters, no trailing period:
  `Add order balance handler`.
- Body (when needed) — **why**, not what: the diff shows what.
- Granularity is free: the branch collapses on merge, so fine-grained
  "wip" commits are allowed. But every commit must build — otherwise
  `git bisect` is useless.
- Never commit secrets, `.env`, database dumps.

## Merge Request

Remote — self-managed GitLab (`origin`) once one is configured; until then
the repository has no remote and a task ends as a local squash (see
"Merge into main" below and `scripts/finish-task.sh`). With a remote, the final
stage of the pipeline is **an MR in GitLab**, not a chat message: a task
that has reached the end must be visible to everyone, not just to one agent
session.

1. By default it is opened **after `gate-3: approved`** — a verdict in
   `status.md` or an explicit human approve in chat. Under **standing
   approval** (AGENTS.md, "Standing approval") the MR is opened right
   after green QA and review, with the record
   `gate-3: pending · MR !N <url>` — the human expresses the approve
   through the Merge button itself; the record format and fixing it after
   merge —
   [07-mr-complete.md, § "The gate-3 verdict and the MR link"](../ai-workflow/stages/07-mr-complete.md).
   `gate-3: rejected` blocks the MR in both modes.
2. Before opening: rebase onto `main`; the push runs the quick `pre-push`
   check, and the full run of tests and linters is CI's, on the MR itself
   (`VERIFY_FULL=1 git push` asks for it before the push).
   If `main` brought its own EF migrations — the task's migrations are
   regenerated on top of the fresh `main`.
3. Title: `feat|fix|tech|chore(<scope>): <gist> (<short id>)` —
   `feat(orders): calculate remaining order balance (feat-001)` — the
   title, like the commits, is English (rule from AGENTS.md; the
   `commit-msg` hook rejects Cyrillic in a squash commit's title).
4. Description: a link to `docs/features/<id>/`, a summary of the
   changes, a gate checklist, manual-verification instructions, and
   knowingly open items. Passed **as a file** (`--description-file`):
   line breaks in `--description` and in
   `git push -o merge_request.description` don't survive.
5. Squash — **enable it in the MR itself** (`--squash-before-merge`) and
   verify it took. Branch deletion — **disable**.
6. **Red CI — stop.** An MR is not opened and not merged with failing
   tests or linters.
7. **The human presses the Merge button**, not the agent.

Step-by-step instructions with commands —
[stage 9–10](../ai-workflow/stages/07-mr-complete.md).

### GitLab CLI

Tool — `glab`. Install (Windows): `winget install GLab.GLab`.

```sh
glab auth login --hostname <GitLab host>
```

Three gotchas, each one cost a session:

- **`glab auth login` puts the token in the keyring, but may not write
  the host section into `config.yml`.** Symptom: "None of the git remotes
  configured for this repository point to a known GitLab host", even
  though the login succeeded. Fixed by adding the section — there are no
  secrets in these commands:

  ```sh
  glab config set --host <host> api_host <host>
  glab config set --host <host> api_protocol https
  glab config set --host <host> git_protocol https
  ```

- **For `glab`, the `-h` flag is `--help`, not host.**
  `glab config get -h <host> …` silently prints help instead of the
  value. Host — only `--host`.
- **The token lives in the OS keyring and is not directly accessible to
  the agent** — and should not be: don't extract it, don't move it into
  `config.yml` in plain text, don't set up `GITLAB_TOKEN` in the
  environment for convenience. Once the host section is correct, `glab`
  fetches the token itself, and that is enough.

## Merge into main

**Method — squash merge.** One task = one commit on `main`; `main`'s
history reads as a list of completed tasks, not a stream of "wip" noise.
Rebase-merge and merge-commit are not used.

With a remote, the squash happens in GitLab and the button is the human's.
With no remote configured it happens locally, through
`scripts/finish-task.sh` (`finish-task.ps1` in PowerShell): the same rule,
enforced by a script instead of a button — it merges only when the task's own
`status.md` records `gate-3: approved` or a standing approval, and only with
a green `verify.sh`.

The squash commit's title = the PR's title (with the short id) — it is
what reconstructs the link commit → task → `docs/features/<id>/`:

```
git log --oneline --grep 'feat-001'
```

The merge is performed **in GitLab, by the human**. `stage: complete` in
`status.md` is set on the branch BEFORE the merge, so it travels along in
the same squash commit; newly found debt is a request to the
orchestrator, who adds it as lines to the `main` queue with a separate
commit, with numbers right away (§ Branches above).

After the merge, the session pulls `main` up to the server:

```
git fetch origin && git switch main && git merge --ff-only origin/main
```

`--ff-only` here is not a precaution, it's a check: if it fails, the
local `main` has diverged from `origin/main` — meaning something was
merged bypassing the MR, and that has to be investigated, not papered
over with a merge commit.

## Local checks (git hooks)

Hooks live in `.githooks/` — plain `sh` scripts, **with no npm
dependencies** (husky and the like are not used: an extra dependency in
the supply chain, and it doesn't work for anyone who doesn't run
`npm ci`).

Enabled automatically on the first build — the `EnableGitHooks` target in
`Directory.Build.props` runs:

```
git config core.hooksPath .githooks
```

Anyone who doesn't build the backend sets the same command by hand, once.

The value is **relative**, and git takes a relative `core.hooksPath` from the
root of the working tree the hook runs in. The setting is stored in the shared
`.git/config`, so every worktree of the clone gets it and **each worktree runs
its own checkout's copy** of the hooks: a hook edited on a task branch is the
one that runs for that worktree's commits and pushes. A machine can hold an
**absolute** path instead (set by hand or by a script): then every worktree
runs the copy in that one checkout, usually the hub, on whatever branch it is,
and a hook edited on a task branch does not run in its worktree until it
reaches that checkout. What is live here: `git config --show-origin
core.hooksPath`; the next build rewrites it to the relative form, and the same
command by hand restores it. The hooks that call `scripts/verify.sh` run the
one of the tree being pushed from (`git rev-parse --show-toplevel`) either
way, so an edit to `verify.sh` is live at once.

| Hook | What it checks |
|-----|---------------|
| `pre-commit` | the commit is not on `main`; branch name matches the format; secrets and dumps in the index; conflict markers |
| `commit-msg` | subject ≤ 72 characters, English, no trailing period |
| `pre-push` | `scripts/verify.sh`, the quick run — checks per the class of changed files (build, lint, the frontend tests that reach what the branch changed); the same input is named in the stages' Definition of Done, so the hook and the DoD don't diverge. The changed files are counted against `origin/main` (against `main` when there is no such ref; an exported `BASE` wins) — run `git fetch` first, a stale `origin/main` widens the diff. A test run that aborts (a crashed test host, a Vitest run with no summary) fails the push even when its exit code was lost. `VERIFY_FULL=1 git push` asks for the whole frontend suite |

> Hooks are **feedback, not a barrier**: `git commit --no-verify` removes
> them entirely. Anything critical must be duplicated in CI — and it is: the
> `CI` workflow (`.github/workflows/ci.yml`) runs the same `scripts/verify.sh`
> with `VERIFY_FULL=1` on every pull request, so the whole frontend suite is
> CI's check, required on `main`, and the push pays only for the quick run.
> The check's name, `verify.sh (change-proportionate)`, is the context
> `main`'s protection requires: rename the job and the protection together.
> A full run before the push is one variable away (`VERIFY_FULL=1 git push`;
> in PowerShell `$env:VERIFY_FULL = 1; git push`) and is instant when a green
> full run of this tree already left its stamp (`scripts/verify.sh`, header).
> Without a CI run the only real architecture check is
> `Mustask.Architecture.Tests`, because it fails in a plain `dotnet test`.

## Architecture tests

`src/backend/tests/Mustask.Architecture.Tests` checks the boundaries of
Onion × Vertical Slice with code, not with a reviewer's eyes: the
direction of dependencies between layers, keeping the domain free of
infrastructure packages, the isolation of slices from each other. The
rules read `.csproj` files and source code, so they catch a violation
from the moment a reference is declared, not from its first use.

A separate `RulesAreNotVacuous` class guards against the main disease of
such suites — a green status because the check stopped reading anything.

When the architecture changes — first the ADR and
`docs/conventions/backend.md` are updated, then the rule in the tests,
and only then the code. The reverse order means the convention is being
rewritten after the fact to match what was already done.

## Parallel work

Several agent sessions work on the project at the same time. Git does not
isolate them within one working directory: there are no locks, the last
one to write wins. What's shared is not just files but also `obj/`,
`bin/`, `node_modules/` — two simultaneous `dotnet build` runs in the
same directory corrupt each other's intermediate files, and the error
looks like anything except a race.

### Default — one worktree per stream

One session = one worktree = one branch:

```
scripts/new-task.sh feat order-form
```

The script takes the id from the queue line in `docs/backlog.md` on
`main` (the number, size and epic are already there), creates the
branch — that is the claim on the work — and creates the worktree
`.claude/worktrees/<id>/` (inside the repository, the directory is in
`.gitignore`) with the feature's folder and `status.md`. The location
was chosen deliberately: Claude's desktop app groups worktree sessions
under `.claude/worktrees/` beneath the repository's own project, and for
Codex the path is neutral — its sessions are keyed by cwd. If the branch
already exists, another session has taken the task — the script refuses;
take the next line. It doesn't commit anything and doesn't touch the
backlog — the orchestrator removes the taken task's line with a commit
on `main`. The same thing by hand:

```
git worktree add .claude/worktrees/feat-001-order-form -b feat/001-order-form main
git worktree remove .claude/worktrees/feat-001-order-form
```

Git won't let two worktrees sit on the same branch — that's not a
limitation, it's the wanted behavior. Hooks still work: `core.hooksPath`
is stored in the shared config.

**Reading roles do not hold the branch.** The branch is the claim of the
session that changes code. A session whose output is a document — a code
review, a security pass, a QA re-check that adds no test, a requirements
review — checks the branch out detached (`git switch --detach <branch>`),
works there, and hands its section (`review.md`, `qa.md` text and the
`status.md` Log line) to the orchestrator, who commits it. Two readers and
one writer can then work on the same task at the same time instead of
waiting for each other's `git switch`. A session that does change code or
tests takes the branch as before — one at a time. The orchestrator's own
worktree stays on its own branch; it switches to a task branch only to
commit what readers handed in or to record gates and decisions, and
switches back.

What you need to know about worktrees:

- **Untracked files don't move over.** `frontend/.env`, user secrets,
  and local configs are absent in the new directory — they're created
  again. This is the most common cause of "everything broke after
  switching".
- `obj/`, `bin/`, `node_modules/` — each directory has its own: the
  first build will be a full one. For `node_modules` run
  `bash scripts/warm-worktree.sh` first: it copies the install for the
  current lock file from a machine-wide cache and runs `npm ci` only
  when the lock file has not been seen on this machine.
- The repository's main directory stays on `main` and serves as the
  hub: worktrees are created from it and merges go into it. Agents don't
  work in it — otherwise a neighboring session would switch the branch
  out from under them.
- **The lock "branch = claim" holds within a single clone** (a shared
  `.git` + its worktrees) and its remote-tracking branches after
  `fetch`. As long as all sessions work in worktrees of one clone, that
  is enough. When working from two or more machines, creating a branch
  locally stops being a claim — two machines will create a same-named
  branch and only find out at push time; then the claim must become the
  **push** of the branch. This is a known limitation, deliberately left
  unclosed — recorded here so the rule doesn't silently weaken.

### If streams end up in the same feature

The standard case from
[../ai-workflow/routing.md](../ai-workflow/routing.md): backend QA runs
in parallel with the frontend starting. Then the streams split by
**ownership zones** — non-overlapping paths:

| Zone | Who writes |
|------|-----------|
| `src/backend/Mustask.*`, `tests/` | backend stream |
| `src/frontend/` | frontend stream |
| `Mustask.sln`, `Directory.*.props`, `docs/features/<id>/status.md`, `docs/backlog.md` | **orchestrator only** |

The four shared files in the last row are the source of almost every
conflict. An agent that needs to change them asks the orchestrator,
rather than editing them itself.

For `L` tasks, sub-branches are more reliable than zones:
`feat/001-…-backend` and `feat/001-…-frontend` in their own worktrees,
both merging into the feature branch. The cost is two merges; the gain
is full isolation. The frontend then works against `api-contract.md`,
not against the backend's code.

### Write rules

- **Never `git add -A` and `git add .`** — explicit paths only. This one
  rule removes half of the incidents: `-A` picks up files created by a
  neighboring session a second ago, and they end up in someone else's
  commit.
- `git status` — right before `git add`, not "I checked five minutes
  ago." Five minutes ago is a past state of the repository.
- **Read-modify-write instead of a full overwrite.** The file is
  re-read right before writing; content read at the start of the task is
  already stale. This applies to a "small change" too — overwriting the
  whole file from memory silently wipes out someone else's lines,
  without a conflict.
- The `Log` section in `status.md` — append only, at the end.
  Append-only conflicts incomparably less often than editing the middle.
- Don't build or install dependencies at the same time in the same
  directory.

## Protecting main

The rule "don't commit to `main`" is held by configuration on top of agent
discipline. What is live on GitHub, as read on 2026-10-18 with `gh api
repos/totKtoXam/mustask/branches/main/protection` (read-only: the settings
are changed by the human, never by an agent):

- **A required status check:** `verify.sh (change-proportionate)` — the job of
  `.github/workflows/ci.yml`, reported by GitHub Actions. The branch does not
  have to be up to date with `main` first (`strict` is off).
- **No required reviews, no required approve.** The API shows no
  pull-request rule at all: the merge is the human's Merge button (or the
  auto-merge a standing approval allows), and the check is what must be green
  first.
- **`enforce_admins` is off:** the rules do not bind a repository admin.
- Force-push and deletion of `main` are forbidden.
- Not required: signed commits, linear history, resolved conversations. No
  push restrictions and no rulesets.

The repository's merge settings: squash, merge-commit and rebase-merge are
all allowed, auto-merge is allowed, and "delete head branches on merge" is
**off**. "Squash only" and "the branch is not deleted" are therefore this
document's rules, held by the MR step and by agents — not by the server.

What follows for the work:

- The required check is the full gate: `ci.yml` runs `scripts/verify.sh` with
  `VERIFY_FULL=1` on every pull request, against `origin/main`. It also runs
  on every push to `main` — after a merge — and there compares the merged
  commit with the commit `main` pointed at before the push, so the run shows
  what the merge brought rather than "No changes".
- The job's name is the context the protection requires. Rename the job and the
  rule together, or every PR waits for a check that never reports.
- When the human changes the protection, this section is updated in the same
  sitting: a description that lags the settings is how it once said "not
  configured" while the check was already required.

## Releases and versions

- Versioning — `MAJOR.MINOR.PATCH` (SemVer), tag `vX.Y.Z` on a commit on
  `main`.
- The tag is set manually, by the human's decision; deploying to prod —
  only from a tag, not from an arbitrary commit on `main`.
- For a system whose releases are audited, the tag is the point the
  acceptance certificate refers to, so tags must not be rewritten.

> **Status: the policy is fixed, the rollout process is not described**
> — it will appear together with the environments (stage/prod) and the
> CD pipeline.

## Hotfix

A `hotfix` differs from a `bugfix` only in urgency and the branching
point: a branch from a fresh `main`, the task goes outside the backlog
queue — the only exception to the rule "id only from a queue line," and
the list of exceptions is closed: `scripts/new-task.sh` issues the `hot`
task the next free number itself
([../features/numbering.md](../features/numbering.md)). Route — S
([../ai-workflow/stages/s-route.md](../ai-workflow/stages/s-route.md)),
but a test that reproduces the defect, code review and `gate-3` remain
mandatory. "Urgent" cancels none of them.

If the hotfix falls under "`L` thresholds" from
[../ai-workflow/routing.md](../ai-workflow/routing.md) (an irreversible
migration, exchange with an external counterparty, auth/secrets) — it
still goes the S route, but **before its merge** a tech task on the full
route is filed into the backlog queue to rework the same spot. A hotfix
without such a line in the queue is not merged.
