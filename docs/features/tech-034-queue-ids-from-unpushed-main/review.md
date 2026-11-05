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

## Round 1 (code review)

Scope: `git diff 5fac020c..HEAD -- scripts docs/features/numbering.md` at
`04410926`, read against `## Plan`. QA's D1-D3 and O1-O2 are known and not
repeated. Verdict: **approve** — no blocker, no major; five minors (debt).
Not re-run in full (verify green per Log and QA); the cases file was run once
against a CR-1 mutant. Timing: `--ids` 0.6 s; the full summary 61 s against
72 s for the `5fac020c` script (Windows spawn cost in the pre-existing loops).

Correct as built: the union is a superset of the old read (`origin/main` and
`main` are both in `refs/heads refs/remotes`); `%0a` is git's format escape;
cat-file gets full shas, so no ref-name ambiguity, and a `missing` reply cannot
match the `^- \[` filter; the `+N unpushed` count is path-limited and guarded
by `queue_src`; no bashism in the new lines (QA ran them under dash).

### Findings

**CR-1 — minor (test). Case 7 does not pin "one pass".**
`scripts/tests/queue-numbering-cases.sh:235-238` puts all 12 extra branches on
the same commit as `main`, so a read that spawns git per *distinct commit*
keeps the process count flat. Mutant: `queue_file_numbers` as `for c in
$(git for-each-ref --format='%(objectname)' refs/heads refs/remotes | sort -u);
do git show "$c:docs/backlog.md"; git show "$c:docs/backlog/withdrawn.md";
done | grep ...` — `--scripts <mutant>`: 7 of 7 PASS. In this repo that is 166
processes (83 distinct commits). Fix: one commit per branch, e.g.
`g branch "scratch/b$n" "$(g commit-tree -p HEAD -m "b$n" 'HEAD^{tree}')"`.

**CR-2 — minor (prose). The source-3 command reads one of the two files.**
`docs/features/numbering.md:57-58` (changed here) names both queue files;
the command on `:59` is still `xargs -I{} git show {}:docs/backlog.md` — run
by hand it misses every withdrawn number. The prescribed tool is
`backlog.sh --ids` (`:72-73`), so nobody is steered wrong. Fix: add
`{}:docs/backlog/withdrawn.md` to the command, or replace it with "`scripts/
backlog.sh --ids` (one pass)".

**CR-3 — minor (comments). Two backlog.sh comments no longer match.**
`scripts/backlog.sh:29` "origin/main ... (fresher than local)" — the block this
diff adds at `:162-165` says local main can carry queue commits origin/main
lacks. `:45-46` "a git process per ref is what this replaced" — the replaced
code (`5fac020c:scripts/backlog.sh:21-45`) read `origin/main` alone with two
`git show`; per-ref is the avoided alternative (the numbering.md command,
`new-task.sh hot`). Fix: "(fresher for others' pushes; own unpushed queue
commits are noted under QUEUED)"; "is what this avoids".

**CR-4 — minor (latent). The queue files are two hardcoded names.**
`scripts/backlog.sh:50` and `scripts/new-task.sh:134-139` read `docs/backlog.md`
and `docs/backlog/withdrawn.md` only; the queue is `docs/backlog.md` plus any
`docs/backlog/*.md` (`.githooks/pre-commit:33-45`, AGENTS.md), and the summary
note here counts the whole `docs/backlog` (`backlog.sh:167`). `docs/backlog.md`
"When to split" moves `Later` to `docs/backlog/later.md` past ~40 entries;
`Later` holds 39. After that split, numbers added to `later.md` are invisible
to `--ids` (older refs' copies hide it for a while). Pre-existing names, but
this diff rewrote the read. Fix: read every queue file in the one pass —
`git grep -h -E '^- \[[ x]\] [a-z]+-[0-9]{3}' <distinct shas> -- docs/backlog.md
'docs/backlog/*.md'` is a single process over many trees — or add "teach
backlog.sh and new-task.sh hot the new file" to the split table; queue it.

**CR-5 — minor (prose, pre-existing). Source 2 is not "across all branches".**
`scripts/queue.sh:29-30` "the same three sources", and numbering.md source 2
(`:54-56`, across all branches), but `taken_ids` reads `ls docs/features` of
the current worktree (`backlog.sh:59`). `queue.sh add` from an older task
worktree misses folders merged since it branched unless a branch name or a
queue line on some ref still carries the number (usually true now that every
ref is read). Fix: narrow the sentence ("this worktree's folders") or read
`docs/features` per distinct commit; queue it, it is outside this plan.

### Behavioural claims checked

- numbering.md:57-58 (both queue files, all branches) → `backlog.sh:50-51`, `new-task.sh:131-139`.
- numbering.md:59 (source-3 command) → CR-2.
- numbering.md:60-64 (add/withdraw commit to local main, push later; origin/main-only count reissues) → `queue.sh:145-147` (commit in the main worktree, no push); cases 1-2 fail on `5fac020c` (QA § 2).
- numbering.md:64-66 (`--ids` one pass, for-each-ref | cat-file --batch) → `backlog.sh:49-55`; its guard → CR-1.
- numbering.md:66 (hot reads ref by ref) → `new-task.sh:131-139`.
- numbering.md:66-67 (both covered by the cases) → backlog.sh: cases 1, 2, 4, 6; hot on unpushed main → QA D2.
- numbering.md:69-70 (heads and remotes only, not stash) → `backlog.sh:51`, `new-task.sh:110`; case 6.
- backlog.sh:13-17 (QUEUED origin/main else main; TAKEN from queue files of every ref feeds --ids, NUMBERS, queue.sh add) → `:33-35`, `:49-62`, `:226`, `queue.sh:96`; source 2 → CR-5.
- backlog.sh:18-19, queue.sh:30-32 (unpushed add/withdraw number already taken) → `backlog.sh:49-55`; cases 1-2.
- backlog.sh:29 (fresher than local), :45-46 (what this replaced) → CR-3.
- backlog.sh:30-32 (QUEUED only; numbers independent) → `:49-55` never reads `queue_cache`.
- backlog.sh:37 (warning: numbers still counted from every other branch) → `:49-55`.
- backlog.sh:46-48 (one read per commit; withdrawn counted; missing → no match) → `:52`, `:50`, `:54`.
- backlog.sh:162-165 (unpushed numbers counted, lines not shown) → `:166-170`, case 4; take/withdraw → QA D3.
- queue.sh:28-30 (same three sources) → CR-5.
- cases :21-22 (verify runs it on the four files) → `verify.sh:617-631`.
- cases :25-27 (`--scripts`, `--shell`) → `:34-44`, `:95`, `:156`, `:169`; caveat: `queue.sh:96` runs backlog.sh with plain `sh`, but each of the three also runs directly under `$shell` (cases 3-7). No finding.
- new-task.sh:123-126 (hot: three sources over heads/remotes) → `:110`, `:128-142`, now incl. withdrawn (`:138-139`).

Direction 2:
- Where taken queue numbers are read (origin/main → every ref): grepped `--ids`, `origin/main`, `withdrawn`, `numbers are taken` in `docs/` (not features/*, templates/examples), `.claude/`, `.codex/`, `.agents/`, `.githooks/`, the AGENTS.md files. Hits: `docs/backlog.md:80-84` ("backlog.sh counts the numbers in it" → `backlog.sh:50`), `:95` table ("all three and all branches" → source 3 true; source 2 → CR-5), `docs/backlog/withdrawn.md:8-11` → `backlog.sh:50`, `docs/ai-workflow/README.md:145` (command only). No stale "origin/main only" text.
- QUEUED gains the unpushed line: grepped `QUEUED`, `unpushed` — only this task's queue line. None.
- Hot counts withdrawn numbers: numbering.md:98-100 ("three sources") is now true; feature.md:50-54 unaffected. None.
- verify.sh runs a new case file: grepped `handoff-cases`, `queue-numbering-cases` — no prose lists per-script cases. None.
- Not swept: other tasks' `docs/features/*/` artifacts and `docs/templates/examples/` (historical).

## Fixes (fix pass after QA and code review round 1)

Cases: `sh scripts/tests/queue-numbering-cases.sh` 12 of 12 PASS (also under
`--shell dash`); `bash scripts/verify.sh` `RESULT: green`. Fail-before: against
the pre-fix scripts the D1 cases (4) and the D3 case fail; against a
`refs/remotes`-only hot path case 3b fails; against a git-per-distinct-commit
read case 7 fails.

- D1 — fixed in b7b68179. `backlog.sh` `queue_file_numbers` returns non-zero
  with a message on stderr when `for-each-ref` or `cat-file --batch` fails or
  answers fewer requests than asked; `--ids` exits 1, the summary prints
  `unavailable` under NUMBERS and exits 1, `queue.sh next|add` refuse (main
  untouched). Cases 9-12 (git shim: exit 128, and exit 0 with no output).
- D2 — fixed in b7b68179. Case 3b: unpushed `queue.sh add hot`, then
  `new-task.sh hot` expects `hot/002`; killed by the `refs/remotes`-only mutant.
- D3 — fixed in b7b68179. The line now reads `(+N unpushed queue commit(s) on
  local main: add/take/withdraw not in origin/main yet, so this list may differ
  from main; their numbers are counted)`; case 4 pins `add/take/withdraw`.
- CR-1 — fixed in b7b68179. Case 7 gives each extra branch its own commit;
  killed by the git-per-distinct-commit mutant (12 vs 60 processes).
- CR-2 — fixed in b7b68179. `numbering.md` source 3 names `backlog.sh --ids` and
  the one-pass command over both queue files.
- CR-3 — fixed in b7b68179. Both `backlog.sh` comments reworded.
- CR-4 — left: the read needs a redesign (`cat-file` cannot glob
  `docs/backlog/*.md`; `git grep` over many trees nears the Windows
  command-line limit) and nothing is lost until `Later` is split out of
  `docs/backlog.md` (39 of ~40 entries); the "When to split" table is not in
  this pass's paths, so the orchestrator should queue it.
- CR-5 — comment fixed in b7b68179 (`queue.sh` header says source 2 is this
  worktree's folders); left: reading `docs/features` per ref is outside the
  plan, the orchestrator should queue it.
- O1, O2 — not findings; untouched (O2: `git update-index --chmod=+x` for the
  three scripts is the orchestrator's call before the PR).
