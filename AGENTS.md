# MUSTASK — context for AI agents

Every AI tool reads this file (Claude Code, Codex and others). It is short on
purpose — the details are behind the links, read them as you need them.

## Project

MUSTASK ("must task"), our own internal task tracker in the spirit of Jira,
ClickUp and Notion — work items, boards, backlogs. Greenfield development:

- Backend: .NET 10, PostgreSQL, architecture — an Onion + Vertical Slice
  hybrid → `docs/conventions/backend.md`. Local orchestration — .NET Aspire
  (`Mustask.AppHost`), identity — Keycloak (realm `mustask`)
- Frontend: React + MUI, Vertical Slice Design
  → `docs/conventions/frontend.md`. `src/frontend/` is the system's only
  frontend (the MUSTASK panel)
- The reference vertical slice to copy is the dictionaries module
  (languages): `Mustask.Api/Features/Dictionaries`,
  `Mustask.Application/Features/Dictionaries`,
  `src/frontend/src/features/dictionaries`
- Git: branches, commits, MRs, releases → `docs/conventions/git.md`
- Architectural decisions — ADRs in `docs/adr/` (rules: `docs/adr/README.md`)

## How agents work here

All work follows the pipeline described in **`docs/ai-workflow/README.md`** —
read it before your first task. In short:

1. Every task is first classified (type + size F/S/M/L) per
   `docs/ai-workflow/routing.md` — the route depends on that.
2. Each feature lives in `docs/features/<id>/` — spec, design, api-contract,
   QA reports and `status.md` (current stage and gate verdicts) are there.
3. Stages are executed per the instructions in `docs/ai-workflow/stages/*.md`.
   State is handed over through files, not through chat context — when you
   start work, read the feature's `status.md` and the artifacts of the
   preceding stages.
4. **Human gates:** after the requirements review, after the design, and
   before the MR, work stops until the human explicitly approves in
   `status.md`. Do not slip past them.
5. Review and QA always run in a separate session with a clean context, and
   never by the author of the artifact under review.

## Hard rules

- Never commit to main — only task branches `<prefix>/<NNN>-<slug>`
  (`feat/001-order-form`); merging is a squash MR after `gate-3`. The single
  exception is an orchestrator's or human's commit that changes only queue
  files (`docs/backlog.md`, `docs/backlog/*.md`): the queue lives in main, and
  a task gets its number by having its line put there. The queue is edited with
  `scripts/queue.sh add|take|withdraw` — it issues the number, writes the
  line by the format and stages only queue files. What is in flight, what
  is queued and how each epic stands — `bash scripts/backlog.sh`
  (`scripts/backlog.ps1` in PowerShell), and what is already finished —
  `scripts/backlog.sh --done`. Do not reconstruct any of it by reading every
  `status.md`. Branches, commits, MRs, releases →
  `docs/conventions/git.md`; the id format → `docs/features/README.md`, the
  numbering rules → `docs/features/numbering.md`.
- Do not change a feature's `api-contract.md` without a note in its
  `status.md`.
- Tests are written together with the code; an MR with failing tests is not
  opened.
- Secrets — only through environment variables / user secrets, never in code.
- **Bind the actual agent session before task work.** After `new-task` creates
  a worktree, bind the session itself to that worktree before implementation,
  QA, review or subagent delegation. From the bound session, show its
  host/project root plus `git rev-parse --show-toplevel` and
  `git branch --show-current`; all must identify the task worktree and branch.
  A shell `cd`, `git -C` or a command-specific `workdir` does not move an
  existing desktop session. Where the vendor surfaces an IDE/Open target, it
  must point at the task worktree. Use native current-session relocation only
  when this session supports it and its confirmation is available. Otherwise
  keep the worktree, branch and files, prepare an interactive handoff, and
  stop the original session for the human to transfer; do not substitute an
  unattended or background session. See `docs/ai-workflow/handoff.md`.
- If you started a background process — an application, a search, anything —
  either finish with it and stop it yourself, or list it explicitly in your
  answer: what exactly is running, on which URL/port (for applications, take
  the port from the startup output, not from memory: Aspire hands them out
  dynamically, and Vite shifts the port if it is taken) and how to stop it.
  An undeclared process — an abandoned search, an unclosed `dotnet run` —
  outlives your session and takes resources from the next one.
- The human confirms a task's route. Before your first change, show the
  classification (type, size, route, gates) and wait for an answer — always,
  not only when in doubt. The format is in `docs/ai-workflow/routing.md`,
  section "Route confirmation".
- **Standing approval.** The human may grant permission for a series of work
  ("do it and merge without confirmations") — within that series, per-task
  route confirmations and merges need no separate approval. The permission
  covers only the explicitly named class of work, and lasts until it is
  revoked or the session ends; the agent records it in the `## Log` of the
  first task of the series and mentions it in every Complete. Standing
  approval does NOT cover gates that carry a decision (spec, design, final
  code review) — it removes ceremony, not control.
- **A docs-only S task under standing approval** follows the light cycle: no
  QA and no review sessions, verification is `scripts/verify.sh`, and the
  review is the human seeing the diff in the merge report. Conditions: only
  `docs/`, `.claude/`, `scripts/` and hooks change; runtime code is not
  touched. This extends the F logic to S
  (`docs/ai-workflow/stages/s-route.md`, § "Light cycle").
- **The last stage of a task is an MR in GitLab, not a chat message.** After
  `gate-3: approved` the agent pushes the branch and opens a merge request
  through `glab` (squash on, branch deletion off), then sends a short
  notification with the link to the chat — the format and the commands are in
  `docs/ai-workflow/stages/07-mr-complete.md`, § "Opening the MR". **With no
  remote configured** there is nothing to push to and the task ends as a local
  squash — `scripts/finish-task.sh`, which merges only against a recorded
  acceptance and a green `verify.sh`, and refuses otherwise.
- **Only a human presses Merge.** The agent does not merge, does not delete
  the branch — neither locally nor on the server — and does not consider the
  task finished until that is confirmed. A task's branch lives on the server
  after the merge too: the squash collapses it into a single commit, and the
  fine-grained history stays only in the branch.
- Do not search the whole disk or the whole home directory: never start a
  `find` / `grep -r` / recursive walk at `/`, `/c`, `C:\`, `/c/Users` or `~` —
  narrow it to a specific subtree (`find src -name '*.cs'`). In Claude Code a
  wide search is blocked by a PreToolUse hook; how it works, where its limits
  are and which holes are known — `.claude/hooks/README.md`. For other tools
  (Codex and the like) the hook does not apply — there it is just a rule.
- Language: documentation, task artifacts, instructions, code, identifiers
  and commits are English. Product interface copy — everything an end user of
  the system sees — stays Russian: the strings quoted in `spec.md`, its
  stories and `design.md`, the texts in `docs/error-codes.md` and
  `contracts/errors/*.json`, and the Keycloak login theme under
  `deploy/keycloak/`.
- **The reference product is never named.** The tracker that doc-001..007
  studied is not named in anything written from 2026-09-25 on: artifacts,
  code, identifiers, comments, tests, commit messages, UI copy. Say «the
  reference tracker» and cite the analyses by rule id (`doc-002 W-11`,
  `doc-010 D-4`). The analyses merged before that date keep their wording;
  nobody sweeps them (tech-001 D-1).

## Commands

The build, test and stand commands live next to the code they drive:
`src/backend/AGENTS.md` (.NET, Aspire, migrations, the API explorer) and
`src/frontend/AGENTS.md` (Vite, ESLint, Vitest). **Read the one your work is
in.** A tool that discovers nested instruction files gets it without asking —
Codex the nearest `AGENTS.md`, Claude Code the `CLAUDE.md` beside it, which
imports the same file — and for anything else it is one read, not a tax on
every session.

Repository-wide, and the one command that belongs here: `bash
scripts/verify.sh` — proportional verification. It classifies the changed
files and runs only the checks that apply to them; "green" in a task's
Definition of Done means this command.
