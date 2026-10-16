# Review — tech-034-queue-ids-from-unpushed-main

## QA (S route, light form)

Checkout `.claude/worktrees/tech-034-queue-ids-from-unpushed-main`, branch
`tech/034-queue-ids-from-unpushed-main`, HEAD `a1b8214b`. Scope: `git diff
5fac020c..HEAD -- scripts docs/features/numbering.md`, read against `## Plan`
in `status.md`. No full test plan (s-route.md § Process 3).

Verdict: **pass** — no blocker, no major. Three minors (debt) and two
observations below. `bash scripts/verify.sh` exit 0, `RESULT: green`.

E2E question: not asked. The human's standing answer is NO (E2E paused since
2026-10-12; `status.md` Log, 2026-10-17 orchestrator line: E2E stays "no"),
and the diff has no runtime or UI code. For the orchestrator to put under
Decisions in `status.md` (QA does not edit it).

### 1. Plan coverage

| Plan point | Case in `scripts/tests/queue-numbering-cases.sh` |
|---|---|
| 1 repro: two `add` on unpushed local main | case 1 (two adds, `tech-011` then `tech-012`); case 2 (add after an unpushed withdraw) |
| 2 numbers over every ref, one pass | case 6 (local branch, remote-only branch, `refs/stash` excluded); case 7 (git processes equal with 1 and 13 branches) |
| 2 QUEUED keeps origin/main, says when local main is ahead | case 4 (one `(+1 unpushed ...)` line, unpushed line not listed, `taken up to 011`); case 5 (quiet when all pushed or the unpushed commit is not a queue commit) |
| 3 hot path pipe | case 3 (`hot-002` withdrawn, expects `hot/003`) |
| 4 cases with the code, wired in verify.sh | the file exists; `verify.sh` printed `ok: queue numbering: taken-number cases` for this diff; trigger list is `backlog.sh`, `queue.sh`, `new-task.sh`, the cases file |
| 5 numbering.md and header comments | read: `backlog.sh`, `queue.sh` headers and `numbering.md` source 3 match the code. Grepped `--ids`, `origin/main`, `backlog.sh`, `queue.sh` in `docs/`, `.claude/`, `.codex/`, `.agents/`, `scripts/`: no other text describes where the taken numbers are read from |

The dev committed the cases (`e5043357`) after the fixes (`26e84e91`,
`49af6133`) and the session was cut before the dev reported a fail-before
run, so that evidence is produced here (section 2).

### 2. Fail before the fix

The three scripts from `5fac020c` (blob ids checked equal to
`5fac020c:scripts/{backlog,queue,new-task}.sh`) were put into a temp
`scripts/` outside the repo beside a copy of the cases file; `sh
<tmp>/scripts/tests/queue-numbering-cases.sh`, exit 1:

| Case | Unfixed | Fixed | Evidence on the unfixed scripts |
|---|---|---|---|
| 1 two adds before a push | FAIL | PASS | second add printed `tech-011-second` (taken: 1) |
| 2 add after unpushed withdraw | FAIL | PASS | `tech-011-after`, the withdrawn number reissued |
| 3 hot skips a withdrawn number | FAIL | PASS | branch `hot/002-fix-it`, the withdrawn `hot-002` reissued |
| 4 summary line | FAIL | PASS | `taken up to 010, next 011`, no unpushed line |
| 5 no line when nothing to say | PASS | PASS | pins the absence of a line; cannot fail before |
| 6 every-ref read, not stash | FAIL | PASS | `--ids` printed only `tech-010` |
| 7 one pass | PASS | PASS | the old script reads one ref, so process count is flat; guards a per-ref fix |

5 of 7 fail before, all 7 pass after. Cases 5 and 7 are property guards, not
repros, so passing before is expected.

Do the cases catch a broken fix? One-line mutants of the fixed scripts, each
run through the whole file (one pass, all 6 in parallel):

| Mutant | Killed by |
|---|---|
| `refs/heads` only (drop `refs/remotes`) | case 6 |
| drop `withdrawn.md` from the read | cases 2, 6 |
| add `refs/stash` | case 6 |
| per-ref `git show` loop instead of `cat-file --batch` (no dedup) | case 7 |
| drop the `-- docs/backlog.md docs/backlog` pathspec of the unpushed count | case 5 |
| print the unpushed line always (`-gt 0` to `-ge 0`) | case 5 |
| `new-task.sh` hot reads `refs/remotes` only | **survives**, see D2 |

### 3. Regressions around the change

- `sh scripts/backlog.sh --ids`: 113 ids with the `5fac020c` script and 113
  with the new one, `comm` shows no id lost and none added (every queue-file
  number in this repo was already visible from branch names or folders).
  Superset holds. 1.1 s now against 1.8 s before.
- `sh scripts/backlog.sh`: byte-identical to the old output except the new
  line `(+2 unpushed queue commit(s) on local main ...)`. `git log
  origin/main..main -- docs/backlog.md docs/backlog` confirms 2 (the Take
  commits `15094ef8`, `f5e708d3`). NUMBERS block unchanged (`tech taken up to
  034, next 035`). `--done`: exit 0, 38 lines.
- `sh scripts/queue.sh next tech` -> `tech-035`, matches the summary.
- Edge probes in a throwaway repo with the fixed scripts: unpushed `hot` add
  then `new-task.sh hot` -> `hot/002` (correct); `queue.sh add` run from a
  task worktree sees the unpushed add on main -> `tech-012`; no remote at all:
  summary and add work, no unpushed line, no warning; origin present but
  `origin/main` absent: QUEUED degrades to `main:docs/backlog.md`; a stale
  branch holding `tech-077` raises `next` to `tech-078` (union is by design).
- POSIX: `dash` exists here (`/usr/bin/dash`, Git for Windows). The whole
  cases file ran under `dash` with `sh` on PATH also resolved to `dash`
  (checked `BASH_VERSION` empty), so `queue.sh` -> `sh backlog.sh` is dash all
  the way: 7 of 7 PASS. Default run (`sh` = bash here): 7 of 7 PASS, 57 s.
- `git diff --check 5fac020c..HEAD` clean; no sandbox or branch leaked into
  the real repo or `%TEMP%` (`git status` clean, no `hot/*`, no new `tmp.*`).

### Defects

None blocker, none major.

**D1 — minor. The one-pass read fails open and silently.**
`scripts/backlog.sh:49-55` (`queue_file_numbers`) sends `for-each-ref` and
`cat-file` stderr to `/dev/null`, has no `pipefail`, and ends in `|| true`. If
`git cat-file --batch` fails, source 3 (all queue-file numbers) vanishes from
`--ids` with exit 0 and no message. The old code said it out loud when the
queue was unreadable; the `Warning: docs/backlog.md could not be read` path
still covers only the QUEUED list. Trigger is exotic (broken or too old git,
corrupt object store), but the result is this task's own defect — a taken
number issued again — and silent.
Repro: repo with `tech-010` queued and pushed; a `git` shim first on PATH that
exits 128 for `cat-file` and `exec`s real git otherwise. `sh scripts/backlog.sh
--ids` prints nothing, rc 0, stderr empty (healthy: `tech-010`); `sh
scripts/queue.sh next tech` prints `tech-001`.
Fix: a canary — count the `<sha> blob <size>` header lines `cat-file` printed
and warn on stderr (or fail `--ids`) when there are none while refs exist.

**D2 — minor (test gap, behaviour is correct). Hot path on an unpushed main is
not pinned.** `numbering.md` source 3 says `new-task.sh hot` reading "ref by
ref" is "covered by `queue-numbering-cases.sh`". Case 3 pushes everything
first ("the missing pipe and nothing else"), so nothing fails if the hot path
stops reading local `main`.
Repro: in `scripts/new-task.sh:110` change `refs/heads refs/remotes` to
`refs/remotes`, run `sh scripts/tests/queue-numbering-cases.sh` -> 7 PASS.
Behaviour today is right (probe: unpushed `queue.sh add hot one`, then
`new-task.sh hot` -> `hot/002`).
Fix: case 3b — one unpushed hot add, then `new-task.sh hot`, expect `hot/002`;
or narrow the sentence in `numbering.md`.

**D3 — minor (message wording). The unpushed-queue line mis-describes `take`
and `withdraw` commits.** The line reads `their numbers are counted, their
lines are not listed`, true for `add`. For `take` the opposite is on screen:
QUEUED is `origin/main`, so a line already taken still shows as queued.
Repro: `sh scripts/backlog.sh` in this checkout prints `feat-039-project-board`
under QUEUED and `feat/039-project-board` under IN PROGRESS, then `(+2
unpushed queue commit(s) ...; their lines are not listed)`; the 2 commits are
`Take tech-034...` and `Take feat-039...`.
Fix: word it for all three: `QUEUED is origin/main; local main differs by N
queue commit(s) (add/take/withdraw, not pushed)`. Cases 4 and 5 match
`unpushed queue commit` and `(+1 unpushed queue commit`, so keep that prefix.

### Observations (not defects of this diff)

- **O1 — cost.** The cases take ~57-62 s on this Windows machine (7 sandboxes,
  git process spawns dominate on Windows); `verify.sh` for this diff took 85 s. Acceptable since
  it runs only when the four files change; not worth trimming now.
- **O2 — executable bit, not reproduced.** The index holds `100644` for
  `scripts/backlog.sh`, `queue.sh` and `verify.sh` (all three changed here);
  `core.fileMode=false` hides it on Windows. `verify.sh` requires `[ -x ]` for
  every changed `*.sh` ("tooling scripts: syntax and permissions", passes here),
  and `.github/workflows/ci.yml` runs `sh scripts/verify.sh` on
  `ubuntu-latest`. If CI runs for the MR, expect `(not executable)` for those
  files. Mode is untouched by the diff and pre-existing; the orchestrator may
  want `git update-index --chmod=+x` before the PR. No Linux runner here to
  confirm.

### Not run

PowerShell wrappers (`backlog.ps1`; unchanged, and `powershell.exe` is refused
by the session sandbox), WSL/Linux (same), `queue.sh add|take|withdraw`
against the real repo (they commit to main — covered by the sandbox cases).
`VERIFY_FULL` not used: the diff touches no runtime code.

Scratch artifacts (mutants, probes, outputs) are plain files under the
session scratchpad `qa034/`; no process of mine is still running.
