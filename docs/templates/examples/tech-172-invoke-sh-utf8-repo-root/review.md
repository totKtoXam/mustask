# tech-172-invoke-sh-utf8-repo-root — review

## QA

S route, full cycle (not the docs-only light cycle — `scripts/*.ps1` and
`scripts/verify.sh` are runtime tooling under that rule's own definition).

Clean-context QA, commit `171da0f`. Plan checked first (`status.md` `## Plan`),
implementation read only after. Scope: `scripts/invoke-sh.ps1`,
`scripts/tests/invoke-sh-cases.ps1`, `scripts/verify.sh`'s new hook — the four
files the commit touches, plus a smoke of the wrappers that call the launcher.

## Plan-vs-tests coverage

The plan's three items are covered by `invoke-sh-cases.ps1`:

1. Cyrillic root, decoded as UTF-8 — covered (`arg-<cyrillic>` in the
   argument list, and the root itself is a Cyrillic directory under `%TEMP%`).
2. Main checkout vs. linked worktree — covered (`$roots` loop).
3. pwsh and powershell.exe, console forced to 866 — covered (`$shells` loop).
4. `exec` bootstrap / no-`bash_logout`-clear — covered (`$logoutMarker`).
5. Legacy-mode quote escaping for a spaced argument — covered (`'with space'`
   in `$arguments`).

Not covered by the new suite, and not mentioned in the plan's audit step
("the arguments handed to Git Bash") or in `status.md`'s "Notes (dev)" list of
known limits: empty-string arguments, and single-dash arguments that collide
with `[CmdletBinding()]`'s common parameters or with the launcher's own
`-Script`/`-ScriptArguments` names. Both are pre-existing (present before
171da0f, see below), but they sit exactly in the surface this task's plan
says it audited, and they were found there. See Defects.

## What was verified

- `bash scripts/verify.sh` (`RESULT: green`, exit 0) — runs the new
  `invoke-sh.ps1` cases as part of it (4 cases, 0 failed, both editions).
- `scripts/verify.ps1` run for real, through `pwsh -File scripts/verify.ps1`
  (i.e. through the launcher itself, not a shortcut around it) — same green
  result, exit 0.
- The exact repro line from `status.md`'s Plan —
  `[Console]::OutputEncoding = [Text.Encoding]::GetEncoding(866); &
  scripts/invoke-sh.ps1 scripts/backlog.sh --ids` — run against the real
  worktree (whose own path already contains Cyrillic:
  `...\Проекты\mustask\...`), in both pwsh and powershell.exe: prints
  the backlog ids, exit 0 in both. Pre-fix (`171da0f^`), the same command
  throws `Shell script not found: ...` (reproduced separately, matches the
  dev's red-first log entry).
- Own probes, plain (non-Cyrillic) sandbox repos under `%TEMP%`, cleaned up
  after each run, both editions unless noted:
  - Exit-code propagation: a probe that `exit 7`s propagates as exit code 7
    through the launcher, both editions. No regression.
  - `--ids` (double-dash) and `-v`/plain single-dash-non-colliding arguments:
    reach the wrapped script correctly in both editions (`-v` reaching the
    script cleanly was disproven once collision was checked — see Defect 2).
  - Console at 65001 (UTF-8): unaffected, works exactly as at 866. No
    regression on the already-working path.
  - Outside a Git repository: powershell.exe 5.1 still surfaces git's raw
    `fatal: not a git repository...` instead of the launcher's own message —
    unchanged before/after 171da0f, matches the "Notes (dev)" entry, not a
    regression. pwsh actually improved here: pre-fix it threw a garbled
    "cannot call a method on a null-valued expression" (mojibake under 866,
    an unrelated latent bug in the old single-line assignment-then-`.Trim()`
    chain); post-fix it throws the clean, intended `Run this command inside a
    Git worktree.` This is a welcome side effect of the refactor, not
    something 171da0f advertises — worth a one-line mention in the Log for
    whoever reads it next, but not a defect.
- `git status --porcelain` — clean at the end; no scratch files left in the
  worktree, no leftover directories under `%TEMP%` (`invoke-sh-qa-*`
  sandboxes removed after each probe, including one leftover from an
  aborted first attempt that `set -e` cut off before its own cleanup ran —
  removed by hand, not by the probe).

## Defects

### 1. Major (pre-existing, not introduced by 171da0f) — an empty-string argument is silently dropped under Windows PowerShell 5.1

Repro (plain sandbox repo, `scripts/probe-args.sh` prints each `"$@"` between
brackets):

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\invoke-sh.ps1 scripts/probe-args.sh before '' after
```

Observed (powershell.exe 5.1.26100.9444):
```
[before]
[after]
```
Expected (and what pwsh 7.6.6 actually prints for the identical command):
```
[before]
[]
[after]
```

Exit code is 0 in both — the failure is silent: `after` is still there, so a
caller has no signal that an argument went missing, only that the *count*
shifted by one. Reproduced against both the pre-fix (`171da0f^`) and the
post-fix launcher — identical in both, so this is not a regression from this
commit. It sits in the exact area item 2 of the plan says it audited
("the arguments handed to Git Bash") and is not covered by
`invoke-sh-cases.ps1`, which only exercises `plain`, `arg-<cyrillic>` and
`'with space'`. Worth a line in "Notes (dev)" next to the already-recorded
quote-in-argument limit, since it is the same family of legacy-native-
argument-passing gap and a future caller could hit it silently.

### 2. Major (pre-existing, not introduced by 171da0f) — single-dash arguments can be silently reinterpreted as `[CmdletBinding()]` common parameters, or abort the launcher outright

Same probe. Three sub-cases, all reproduced in pwsh 7.6.6 (powershell.exe
5.1 behaves the same for the swallow case, not re-checked for the abort
case since the parameter-binding mechanics are edition-independent):

```
& scripts/invoke-sh.ps1 scripts/probe-args.sh -v plain
```
prints only `[plain]` — `-v` is silently bound as `-Verbose` (unique
case-insensitive prefix among the common parameters) instead of being
forwarded. Exit 0, no error, no indication an argument was consumed.

```
& scripts/invoke-sh.ps1 scripts/probe-args.sh -d foo
```
prints only `[foo]` — `-d` silently bound to `-Debug`, same failure mode.

```
& scripts/invoke-sh.ps1 scripts/probe-args.sh -s foo
```
```
invoke-sh.ps1: Не удается выполнить привязку параметра, так как имя
параметра "s" неоднозначно. Возможные совпадения: -Script -ScriptArguments.
```
exit code 1 — the launcher aborts before the wrapped script ever runs,
because `-s` is an ambiguous prefix of both the launcher's own `-Script` and
`-ScriptArguments` parameters. `-e` aborts the same way (`-ErrorAction`
vs. `-ErrorVariable`); by the same mechanism `-w`, `-i`, `-o`, `-c` are also
live candidates (ambiguous or unique matches among `WarningAction`,
`WarningVariable`, `InformationAction`, `InformationVariable`, `OutBuffer`,
`OutVariable`, `Confirm`), not individually re-checked here.

Confirmed present, byte-for-byte the same mechanism, in `171da0f^` — the
`[CmdletBinding()]` and `param()` block are untouched by this commit, so
this is not a regression from the fix under review. It is, again, in the
exact surface the plan's audit step names, is not covered by the new test
suite, and is more consequential than Defect 1: `-s`/`-e` (and likely
`-w`/`-i`/`-o`/`-c`) don't just lose data, they crash the wrapper outright
before the underlying `scripts/*.sh` even starts, for any caller who passes
a short flag most Unix tools use routinely. `--ids` (double-dash) is
unaffected — the collision is specific to single-dash short forms.

## Verdict

No blockers on 171da0f itself: the Cyrillic-root decode and the legacy quote
escaping do what the plan says, `scripts/verify.sh` and `scripts/verify.ps1`
are green, and the new regression suite is reliable (cleans up after itself,
covers both editions and both checkout shapes, and the dev's own log records
mutant-tested proof for both fixed defects). Two majors found, both
pre-existing and outside this commit's diff (confirmed against `171da0f^`),
in the argument-passing surface the plan's own audit step claims to cover
and the new test suite does not exercise. Recommend a follow-up tech task
for the dash-argument collisions and the empty-string drop rather than
reopening this branch, since fixing either changes `invoke-sh.ps1`'s
parameter design (e.g. switching to `$args`/raw command-line handling)
well beyond this commit's stated scope — the orchestrator's call, not
QA's, whether that follow-up gates this MR.

## Decisions

E2E question (S route, step 3): not applicable — this task touches only
`scripts/*.ps1` and `scripts/verify.sh`, no file under `src/frontend/` or
`src/backend/`, no UI, no API. There is no Playwright suite whose scope
could cover a PowerShell launcher, so the answer is No, recorded here for
the gate-3 packet.

## Code review — Round 1 — tooling

Clean-context review of `3298b0f..HEAD` (code `171da0f`, QA `2704317`) against
the human's requirement: a Cyrillic root works in the main checkout and in
linked worktrees, the rest of the launcher is audited for the same class of
problem, and a regression test in the `.codex/hooks/tests/run-cases.ps1` form
is wired into `verify.sh`.

**Limit of this round.** Nothing was run in PowerShell. The session's
worktree-isolation hook refused every `pwsh`/`powershell.exe` invocation from
the worktree (including `pwsh -File <scratch>.ps1`), and the refusal was not
routed around through another shell. The findings below are static: they come
from the code, the .NET/PowerShell behaviour they cite, and git/grep evidence.
The run evidence for the fix itself is the dev's red-first and mutant log
(`status.md` Log) and QA's independent runs, not this round. Where a finding
would benefit from a run, the command is given.

### Correctness (no finding)

- UTF-8 decode (`scripts/invoke-sh.ps1:17-24`): both editions decode a native
  command's stdout with `[Console]::OutputEncoding`. The encoding is set before
  the call and restored in `finally` on every path, including a throw from
  `& git` under 5.1's `2>$null` + `Stop`. `$LASTEXITCODE` (`:25`) is read after
  `finally`, which does not touch it. A `$null` output is caught by
  `IsNullOrWhiteSpace` before `.Trim()` (`:28`), so StrictMode no longer trips
  on a null root (the improvement QA observed).
- Argument-passing detection (`:64-68`): 5.1, and pwsh 7.0-7.2 without the
  experimental feature, have no variable, so they get the escaped bootstrap.
  7.2 experimental (`Standard`), the 7.3+ default `Windows` (bash.exe is not
  on the `Windows` mode's legacy list) and an explicit `Standard` get the plain
  one; an explicit `Legacy` gets the escaped one. `Get-Variable` walks the
  scope chain, so a profile's `$PSNativeCommandArgumentPassing = 'Legacy'` is
  honoured. The logic is right for every mode named in the brief; CR5 lists
  what no run exercises.
- Exit code (`:69-76`): unchanged in shape; QA confirmed that 7 propagates.

### Findings

**CR1 — minor — the test leaves the caller's console at code page 866.**
`scripts/tests/invoke-sh-cases.ps1:55-61,100`: the child is started with `&`,
so it shares the caller's console, and `[Console]::OutputEncoding = 866` calls
`SetConsoleOutputCP` on that shared console. The child never restores it.
After `scripts/verify.ps1` (or `verify.sh` from a console host) the developer's
terminal is left at 866, while the parent pwsh's cached .NET encoding still
reports the old value. That is the state class this task fixes, now produced
by its own test whenever the console was at 65001. The precedent does not do
this: `.codex/hooks/tests/run-cases.ps1:26-31` starts its child through
`ProcessStartInfo` with `CreateNoWindow = $true`, which gives it its own hidden
console. Fix: start the child the same way, or wrap the child command in
`try { ... } finally { [Console]::OutputEncoding = $saved }`.
Confirm: in pwsh at `chcp 65001`, run the suite, then run `chcp`.

**CR2 — minor — new failure mode when the process has no console.**
`scripts/invoke-sh.ps1:19,23`: the `Console.OutputEncoding` setter calls
`SetConsoleOutputCP`, which throws `The handle is invalid` when there is no
console. Windows PowerShell ISE is the well-known case; detached processes are
another. Before `171da0f` the launcher never called the setter, so a checkout
on an ASCII path worked in such a host. Now every wrapper exits 1 there. Fix:
guard the set and the restore with try/catch and fall back to the plain call,
or avoid decoding a non-ASCII path at all: `git rev-parse --show-cdup` prints
only `../` segments, which can be resolved against
`(Get-Location).ProviderPath`.

**CR3 — minor — the working-directory check cannot fail.**
`scripts/tests/invoke-sh-cases.ps1:57` moves the child into the root before it
calls the launcher. Bash therefore inherits the root whether the launcher's
`Push-Location` (`invoke-sh.ps1:52`) works or is missing, and the check at
`:119-126` passes for that mutant. The reported defect is still caught, through
`Shell script not found` via `Test-Path`. `status.md` Plan item 3 ("checks ...
bash's working directory") reads as a guarantee the suite does not give. Fix:
run the child from `<root>\scripts`, a subdirectory. That also exercises the
root lookup the real wrappers rely on.

**CR4 — minor, pre-existing, in the audited surface — wildcard path.**
`scripts/invoke-sh.ps1:52`: `Push-Location $repoRoot` binds `-Path`, which
treats `[` and `]` as wildcards, so a checkout under e.g. `...\proj[2]` cannot
enter its root. Plan step 2 audited "the working directory ... handed to Git
Bash", and `Test-Path` two statements earlier already uses `-LiteralPath`.
Fix: `Push-Location -LiteralPath $repoRoot`.

**CR5 — minor — the pwsh legacy branch is not exercised.** The suite runs
whichever `pwsh` is installed (7.6 here: `Windows` mode, unescaped bootstrap)
and `powershell.exe` (escaped bootstrap). No run covers the escaped bootstrap
under pwsh's own legacy passer (below 7.3, or with
`$PSNativeCommandArgumentPassing = 'Legacy'`), nor an explicit `Standard`. The
comment at `invoke-sh.ps1:58-63` names "PowerShell before 7.3" as a case. Fix:
for the pwsh shell, add child variants that set the variable to `Legacy` and to
`Standard` before calling the launcher.

**CR6 — minor — the suite depends on the machine's git config, and under some
parents it aborts instead of reporting.** `invoke-sh-cases.ps1:40-45,76-80`:
the sandbox `git init`/`commit`/`worktree add` calls run with the user's global
and system config. `commit.gpgsign=true`, a global `core.hooksPath` or hooks
from `init.templateDir` would turn the suite red (or run the user's hooks) for
reasons that have nothing to do with the launcher. None of these is set on this
machine (`git config --global --get-regexp` found nothing). Separately,
`2>&1` under `$ErrorActionPreference = 'Stop'` (`:41,101`) turns the first
stderr line into a terminating error when the parent is 5.1 or pwsh 7.0/7.1.
That cannot produce a false pass (the exit code is non-zero), but the per-case
FAIL diagnostics are lost. Fix: point `GIT_CONFIG_GLOBAL` at an empty sandbox
file and set `GIT_CONFIG_NOSYSTEM`, or pass
`-c commit.gpgsign=false -c core.hooksPath=`; lower `ErrorActionPreference`
locally around the two captures.

**CR7 — minor — `verify.sh` reports a pass for a suite that did not run.**
`scripts/verify.sh:365-375`: on a non-Windows host that has pwsh, the suite
prints `NOT CHECKED` and exits 0 (`invoke-sh-cases.ps1:22-25`), and `verify.sh`
records `ok: PowerShell wrappers: invoke-sh.ps1 cases`. CI is exactly such a
host: `.github/workflows/ci.yml:22` runs on `ubuntu-latest` (pwsh is
preinstalled) and `:54` runs `sh scripts/verify.sh`. With no PowerShell at all
the behaviour is correct (`note "NOT CHECKED"`, no fail). Fix: gate the block
on a Windows `uname -s` (`MINGW*|MSYS*|CYGWIN*`) and `note` otherwise, or give
the suite a distinct exit code for "not checked". Cosmetic: the
`scripts/tests/*.ps1` pattern at `:361` is redundant, because `*` in a `case`
pattern already matches `/`.

**CR8 — minor — two sentences are not anchored.**
- `invoke-sh-cases.ps1:69-70`, "It ends with an explicit exit like the real
  scripts": of the six wrapped scripts only `scripts/verify.sh` ends with
  `exit`. `backlog.sh`, `finish-task.sh`, `handoff.sh`, `new-task.sh` and
  `queue.sh` end on `printf` or `fi`. Fix: "like `verify.sh`".
- `status.md` Log 2026-09-17, "no `exec` → 4 of 4 failed (bash_logout marker
  ...)": the log does not record the exact mutant. Per the bash manual, a
  non-interactive login shell runs `bash_logout` only when *it* executes
  `exit`. With `-c '"$@"'`, the probe's `exit 0` runs in the script's own
  process, and bash may exec a single `-c` command directly, so that mutant is
  not obviously caught. Dropping `-c` altogether is caught, because then the
  login shell reads the script and executes its `exit`. This could not be
  re-run this round (see the limit above). Fix: put the exact mutant line in
  the Log; if it was the no-`-c` form, say that the marker guards against that
  form.

### Behavioural claims checked (item 6)

Direction 1 — prose the diff adds or changes:
- `invoke-sh.ps1:13-16`, UTF-8 decode and restore → `invoke-sh.ps1:17-24`; red-first run in the Log (not re-run, see the limit).
- `invoke-sh.ps1:54-56`, exec stops the login shell from running `bash_logout`, which clears the screen → `invoke-sh.ps1:69`; Git for Windows `/etc/bash.bash_logout:13-15` (`clear` when `SHLVL=1`); marker at `invoke-sh-cases.ps1:84-89,110-113`. The mutant claim became CR8.
- `invoke-sh.ps1:58-63`, legacy vs standard escaping → `:64-68`. The suite exercises the 5.1 case; pwsh legacy and `Standard` are not exercised → CR5.
- `invoke-sh-cases.ps1:12-17`, independent of checkout location, 866 forced, file is ASCII → `:27,47-67`, `:56`. `LC_ALL=C grep` for non-printable bytes found none in either `.ps1` file.
- `invoke-sh-cases.ps1:69-71`, "like the real scripts" → CR8.
- `invoke-sh-cases.ps1:107-108`, Git for Windows' logout file clears the screen → `/etc/bash.bash_logout:13-15`.
- `verify.sh:358`, every `scripts/*.ps1` wrapper goes through `invoke-sh.ps1` → `scripts/{backlog,finish-task,handoff,new-task,queue,verify}.ps1:9`. The only other `scripts/*.ps1` is the launcher itself.
- `status.md` Plan 1, "root stays git's, main checkout and linked worktrees as before" → `invoke-sh.ps1:20`; cases at `:90-93`.
- `status.md` Plan 3, "checks ... bash's working directory" → CR3.
- `status.md` Plan 4, "verify.sh runs the test whenever a scripts/*.ps1 file changes" → `verify.sh:359-375`. On non-Windows it reports a pass without running → CR7.
- `status.md` Notes (dev), 5.1 outside a worktree shows git's `fatal` → `invoke-sh.ps1:20` (`2>$null` under `Stop` in 5.1); QA reproduced it.
- `status.md` Notes (dev), a double quote inside an argument is mangled in 5.1 → the legacy passer, `invoke-sh.ps1:69`; not re-run.

Direction 2 — prose outside the diff that describes the changed behaviour.
Behaviours: the root lookup under an OEM console, spaced arguments under
legacy passing, and the new `verify.sh` check. Identifiers grepped over
`docs/`, `.claude/`, `.codex/`, `.agents/`, `AGENTS.md`, `src/*/AGENTS.md` and
`scripts/`: `invoke-sh`, `.ps1 wrapper(s)`, `backlog.ps1`, `queue.ps1`,
`verify.ps1`, `mojibake`, `OutputEncoding`, `chcp`, `PowerShell 5.1`,
`run-cases.ps1`, and the pass label `tooling scripts: syntax`. Hits:
`docs/ai-workflow/README.md:120-129` says the wrappers run the POSIX scripts
through Git Bash; its `new-task.ps1 ... ["<summary>"]` line was broken under
5.1 for a spaced summary before and works now, with no stale warning to remove.
`AGENTS.md:52`, `docs/ai-workflow/feature.md:16`,
`docs/features/numbering.md:14` and `docs/conventions/git.md:143` only mention
that the wrappers exist; no behavioural claim is affected. The tech-096,
tech-132 and tech-165 `status.md` hits are historical logs, not live claims.
No prose describes the old failure, a workaround, or `verify.sh`'s list of
checks. Stale: none.

### QA's open majors

QA Defect 1 (5.1 drops `''`) and Defect 2 (single-dash arguments bound as
common parameters, or rejected as ambiguous for `-s` and `-e`) are unchanged
on `HEAD`. The param block at `invoke-sh.ps1:1-8`, and the identical
`[CmdletBinding()]` blocks in the wrappers (e.g. `scripts/backlog.ps1:1-5`),
are outside the diff. Agreed: both are pre-existing and need a parameter
redesign. Deferring them to a follow-up task is the human's call at gate-3,
and if they are deferred the follow-up should be queued.

### Verdict

This diff introduces no blocker or major. The reported defect is fixed by a
sound change, as is the second defect (spaced arguments under 5.1), and the
regression suite catches the reported failure. 8 minor findings (CR1-CR8);
none blocks gate-3. CR1, CR3 and CR7 are the cheapest and the most worth
fixing before the merge. Ready for gate-3; QA's two pre-existing majors stay
open for the human to defer or take.

## Code review — Round 2 — tooling

Clean-context review of `cc47054..cf333b9`, the developer's response to round 1,
plus the final state of `scripts/invoke-sh.ps1`,
`scripts/tests/invoke-sh-cases.ps1` and `scripts/verify.sh:358-390`.

**Limit of this round.** PowerShell still could not be run: the
worktree-isolation hook refused `pwsh -NoProfile -File
scripts/tests/invoke-sh-cases.ps1`, and also `cmd` and `git -C` into a scratch
repository under `%TEMP%`. None of these refusals was routed around through
another shell. What *did* run: plain `git -C <path> rev-parse` against this
worktree, reached once directly and once through a scratch directory symlink
under `%TEMP%`, and a Git Bash experiment with the launcher's bootstrap forms
(scratch removed afterwards). The CR1 console-code-page result and the mutant
results in `status.md` are the developer's runs and were not repeated.

### Round 1 findings

- **CR1 — resolved (code; the run is the developer's).**
  `invoke-sh-cases.ps1:69-75` reads the encoding (and tolerates having no
  console), and `:168-171` puts it back in `finally`.
- **CR2 — resolved.** The launcher no longer touches `[Console]` at all
  (`invoke-sh.ps1:18-31`). The replacement introduces NF1.
- **CR3 — resolved.** The child starts in `<root>\scripts`
  (`invoke-sh-cases.ps1:64-65`). Now a missing `Push-Location` leaves bash in
  `scripts\`, so `scripts/probe.sh` is not found, and the working-directory
  check at `:137-144` can fail. That matches the logged mutant (`exit code 127`).
- **CR4 — resolved.** `Push-Location -LiteralPath` (`invoke-sh.ps1:55`).
- **CR5 — open, honestly recorded** (status.md Log: no pwsh < 7.3 or Legacy
  run). Still minor. Setting `$PSNativeCommandArgumentPassing = 'Legacy'` in a
  pwsh 7.6 child needs no old pwsh install, so the stated reason covers only
  "pwsh < 7.3", not the `Legacy` variant.
- **CR6 — partly resolved, rest honestly recorded.** `commit.gpgsign=false`
  (`:93`) is fixed, and so is stderr capture under `Continue` (`:46`,
  `:116-119`; `$LASTEXITCODE` is global, so `:120` still reads the child's
  code). Still open: a global `core.hooksPath` or `init.templateDir` hooks run
  on `commit`/`worktree add` (`:93-94`), and so does the system config.
- **CR7 — resolved.** `verify.sh:365-373` sends non-MINGW/MSYS/CYGWIN hosts
  to `NOT CHECKED`, and the redundant pattern is gone (`:361`).
- **CR8 — part 1 resolved, part 2 partly.** `invoke-sh-cases.ps1:83` now says
  "as scripts/verify.sh does" (`verify.sh` ends with `exit 0`). The new Log
  line names the exact mutant (`--login $Script`, which drops `-c`). The run
  below shows that this is the only form the marker catches, and that `exec`
  is not what prevents `bash_logout`, so the older prose still claims it is →
  NF2.

### New findings

**NF1 — major — the new root is wrong when the current location is reached
through a directory symlink, junction or `subst` that does not sit at or above
the repository root.** `scripts/invoke-sh.ps1:26-27` joins git's `--show-cdup`
to `(Get-Location -PSProvider FileSystem).ProviderPath`. The two measure
different paths. PowerShell keeps the logical (unresolved) location, and that
is also the working directory it gives `git.exe`. Git for Windows then resolves
its working directory to the final path (`GetFinalPathNameByHandleW` in
`mingw_getcwd`) and counts `../` from there. Run evidence (git 2.47.1.windows.1),
from a scratch symlink `%TEMP%\cr2-tech172-link` → `<this worktree>\docs\features`:

```
git -C C:/Users/dev/AppData/Local/Temp/cr2-tech172-link rev-parse --is-inside-work-tree --show-cdup --show-toplevel
true
../../
C:/Users/dev/Проекты/mustask/.claude/worktrees/tech-172-invoke-sh-utf8-repo-root
```

From that PowerShell location the launcher computes
`...\Temp\cr2-tech172-link\..\..` = `C:\Users\dev\AppData\Local`. The old
`--show-toplevel` gave the correct root. In the ordinary case the failure is
loud (`Shell script not found`). If the wrong directory has its own
`scripts/<name>.sh`, though, the launcher runs *that* script. For example, a
link inside the main checkout that points into a linked worktree can land on
the main checkout's root. This is a regression against main on
ASCII paths and 65001 consoles, in exactly the code this round rewrote. It also
makes `status.md` Plan 1 ("Main checkout and linked worktrees resolve as
before") and the premise of `invoke-sh.ps1:13-17` untrue for these locations.
The suite has no case for it. A `subst` drive mapped *below* the root fails the
same way: `GetFullPath` clamps at `X:\`. A link at or above the root (the usual
"relocated workspace" junction) is unaffected, because the `../` count is the
same.
Fix, preferred: keep `--show-toplevel`, but start `git.exe` through
`System.Diagnostics.Process` with `RedirectStandardOutput`,
`StandardOutputEncoding = UTF8` and `WorkingDirectory` = the FileSystem
location. This reads git's bytes as UTF-8 and does not touch the console, so
CR2 stays fixed. Add a suite case that runs the child from a directory
symlink/junction into `<root>\scripts`, created without admin through
`New-Item -ItemType Junction`. Alternative: resolve the base with
`GetFinalPathNameByHandle` before joining. That is more code for the same
result.

**NF2 — minor — the prose credits `exec` for what `-c` does.**
`invoke-sh.ps1:57-59` ("exec replaces the login shell ... so ...
bash_logout does not run"), `invoke-sh-cases.ps1:129` ("the script was not
exec-ed"), and `status.md` Log 2026-09-17, "no `exec` → 4 of 4 failed
(bash_logout marker ...)". Run (Git for Windows `bin/bash.exe`, `SHLVL=0`, a
`~/.bash_logout` that writes a marker, a probe ending in `exit 0`):

```
exec form    (--login -c 'exec "$@"' -- ./probe.sh): exit=0 marker=no
no-exec form (--login -c '"$@"'      -- ./probe.sh): exit=0 marker=no
no -c form   (--login ./probe.sh):                    exit=0 marker=yes, output ends in ESC[H ESC[2J ESC[3J
```

With `-c`, the login shell never runs the `exit` builtin itself: the probe's
`exit` runs in the script's own process. So the "no `exec`" mutant as worded
is not caught, and `exec` is harmless but not the guard. The new round-1 Log
line (`--login $Script` → marker) is accurate. Nothing anyone does differs
(keeping `exec` is fine), so this is minor. Fix: say that running the script
under `-c`, rather than as the login shell's own script, is what keeps
`bash_logout` from running; reword the FAIL message; mark the older Log line as
superseded by the round-1 mutant.

**NF3 — minor, outside this diff, same defect class — `.codex/hooks.json:11`.**
Its `commandWindows` builds a path from `(git rev-parse --show-toplevel)` inside
`powershell.exe -Command`. That is the pattern this task removed from the
launcher, and this repository's own path contains Cyrillic. Not run (Codex is
not available in this session), so whether the hook host's code page garbles
it is unverified. Fix: a follow-up task, not this branch. The same
`System.Diagnostics.Process`/UTF-8 read applies.

**NF4 — minor — behaviour change under `GIT_DIR`/`GIT_WORK_TREE`.** With both
set and a location outside the work tree, `--is-inside-work-tree` prints
`false`, so the launcher now throws (`invoke-sh.ps1:19-21`). Before, it ran
from `--show-toplevel`. Static reading of `rev-parse`, not run: the hook
refuses `git` outside this worktree. No wrapper caller sets these
interactively. The NF1 fix removes this too; otherwise, record it in Notes
(dev).

Checked statically, no finding: a location at a drive root
(`GetPathRoot` guard, `:29-31`), UNC (`\\server\share\` trimmed to
`\\server\share`, which `Set-Location` accepts), and a non-FileSystem provider
or PSDrive (PowerShell starts native commands in the current FileSystem
location, the same `ProviderPath` used at `:27`). Also checked: inside `.git`
(`is-inside-work-tree` = `false` → throw, as `--show-toplevel` failed before;
not run, the hook refused `git -C` into the shared `.git`), submodules (cdup is
relative to the submodule's work tree, as `--show-toplevel` was), empty cdup
(`true` plus a blank line or no second line; both handled at `:22-25`), and a
subdirectory (`git -C <worktree>/docs/features` → `true`, `../../`).

### Behavioural claims checked (item 6)

Direction 1 — prose the round-2 diff adds or changes:
- `invoke-sh.ps1:13-17`, root built from PowerShell's location, console left alone → `:18-31`; no `[Console]` in the file. Premise fails through links → NF1.
- `invoke-sh.ps1:28`, drive root kept as `C:\` → `:29-31`.
- `invoke-sh-cases.ps1:15-16`, child runs from a subdirectory so the launcher must find and enter the root → `:64-65`, checked at `:137-144`.
- `invoke-sh-cases.ps1:20-21`, console encoding put back at the end → `:69-75`, `:168-171` (the developer's run, not repeated).
- `invoke-sh-cases.ps1:45`, 5.1 turns redirected native stderr into errors under `Stop` → `:46`, `:117`.
- `invoke-sh-cases.ps1:83-84`, "as scripts/verify.sh does" → `verify.sh` last line `exit 0`. "reads bash_logout only on the exit builtin" → the NF2 run (the no `-c` form writes the marker; the `-c` forms do not).
- `verify.sh:363-364`, off Windows the cases would print NOT CHECKED and pass → `invoke-sh-cases.ps1:26-29`; gated at `verify.sh:365-373`.
- `status.md` Plan 1, "Main checkout and linked worktrees resolve as before" → NF1.
- `status.md` Log, round-1 fixes: CR1-CR4, CR7 and CR8 part 1 → the anchors above. The mutant outcomes are consistent with the code (e.g. no `Push-Location` → 127), not re-run. "no `exec` → 4 of 4 failed" (older line) → NF2.

Direction 2 — prose outside the diff that describes the changed behaviour.
Behaviours: how the launcher finds the root (`--show-toplevel` →
`--show-cdup`), and `verify.sh` running the cases only on Windows. Grepped
`show-toplevel`, `show-cdup`, `invoke-sh` and `NOT CHECKED: not Windows` over
the repository. Hits outside this feature's
files: `.githooks/pre-push:13`,
`scripts/{backlog,queue,handoff,finish-task,check-gates}.sh` (bash, byte-clean,
and they do not describe the launcher) and the six `scripts/*.ps1` wrappers
(call sites, no claims). `.codex/hooks.json:11` repeats the removed pattern →
NF3. The tech-101 `status.md:535` hit is a historical log. No prose describes
the launcher's root lookup or `verify.sh`'s PowerShell gating. Stale: none
besides this feature's own Plan (NF1).

### Verdict

Not ready for gate-3 as it stands. Round 1's cheap fixes landed (CR1, CR2, CR3,
CR4, CR7, CR8 part 1), and CR5/CR6 are honestly recorded as open. But the CR2
replacement introduces a **major** regression (NF1): a wrong root from any
location reached through a link below the root, shown by run. Minors open: NF2,
NF3 (follow-up), NF4, CR5, CR6 (rest). QA's two pre-existing majors are
unchanged and still the human's call. This is round 2 of at most 2: NF1 goes
back to the developer, and since the review has not converged, the human
decides whether a fix and a third round go ahead, or whether NF1 is accepted
(e.g. recorded as a known limit) at gate-3.

## Code review — Round 3 — tooling

Clean-context review, authorised by the human after round 2 hit the limit.
Scope: `39a2944..dc86a11` (the response to NF1-NF4), the final
`scripts/invoke-sh.ps1` and `scripts/tests/invoke-sh-cases.ps1`, and a final
pass over `3298b0f..HEAD -- scripts`.

**Limit of this round.** PowerShell could not be run. The worktree-isolation
hook refused `pwsh -NoProfile -ExecutionPolicy Bypass -File
scripts/tests/invoke-sh-cases.ps1`, both with and without an explicit `cd`. It
also refused `git -C` onto a UNC path. None of these refusals was routed
around. Three things did run. Two ran in this worktree and only print the
environment git gives a child process: `git -c alias…` and `git -c
core.hooksPath=<scratch> hook run pre-push`. The third was a Git Bash
process-tree probe for the `-c` bootstrap (scratch removed afterwards). The
launcher's own behaviour below is read from the code. The six-case and mutant
results are the developer's runs.

### Round 2 findings

- **NF1 — resolved.** `invoke-sh.ps1:25-43` runs `git rev-parse
  --show-toplevel` as a `Process`, with `WorkingDirectory` set to the
  FileSystem location and `StandardOutputEncoding` set to UTF-8 without BOM.
  The root is git's again, so a link below the root resolves as it did at
  `3298b0f`. The new "through a junction" case
  (`invoke-sh-cases.ps1:107-116`) starts the child in a junction outside the
  repository that points at `<main>\scripts`. With the `cf333b9` launcher that
  case cannot find `scripts/probe.sh`, because `<sandbox>\link\..` has none.
  This matches the logged mutant (2 of 6 failed, exactly the two junction
  cases).
- **NF2 — resolved in substance, one sentence left over → R3-3.** The new
  comment and the FAIL message now say that passing the script file to
  `bash --login` is what runs `bash_logout`, and that a `-c` command does not.
- **NF3 — moved to tech-176 by the human** (queued on main in `efd2b79`).
  Not re-raised.
- **NF4 — resolved.** `--is-inside-work-tree` is gone, and `--show-toplevel`
  honours `GIT_DIR`/`GIT_WORK_TREE` as it did at `3298b0f`.
- **CR5 — still open, minor.** There is no `Legacy` or `Standard` child
  variant under pwsh 7.6. The status.md Log says so.
- **CR6 — the rest is still open, minor.** A global or system
  `core.hooksPath` and `init.templateDir` hooks still reach the sandbox. R3-1
  is the same family of problem (the sandbox inherits the caller's git state),
  but it is not minor.
- **QA's two pre-existing majors — moved to tech-176 by the human.** Not
  re-raised.

### Launcher, checked with no finding

- **git resolution** (`:20-24`): `Get-Command -CommandType Application`
  searches PATH in order and, within a directory, picks `.exe` before `.cmd`,
  as `& git` did. A `git.cmd` shim works: with `UseShellExecute = $false`,
  `Process.Start` passes the `.cmd` to `CreateProcess`, which runs it through
  `cmd.exe`. The only lookups no longer honoured are aliases, functions and
  `git.ps1` shims, and none of those is a supported setup here. Not run.
- **Working directory** (`:28`): `Get-Location -PSProvider FileSystem` returns
  the session's FileSystem location even when the current location is `HKCU:`
  or a custom PSDrive, and `ProviderPath` is the real path. PowerShell's
  native-command processor puts the same value into `WorkingDirectory` for
  `& git`, so nothing changed against `3298b0f`.
- **Environment**: `ProcessStartInfo` copies the process environment. The
  `GIT_*` variables and any `$env:` edits made in the session reach git as
  they did before.
- **Deadlock** (`:35-39`): stderr is drained with `ReadToEndAsync`, stdout is
  read synchronously, and then `WaitForExit` runs. A PowerShell pipeline
  thread has no `SynchronizationContext`, so `.Result` cannot deadlock, and a
  full stderr pipe cannot block the stdout read.
- **Exit code and whitespace** (`:37-42`): `ExitCode` is read after
  `WaitForExit`. `Trim()` removes the trailing newline, and a Win32 path
  cannot end in a space anyway. An empty result or a non-zero exit throws.
- **Separators and UNC** (`:43`): git prints `C:/…` or `//server/share/…`, and
  `GetFullPath` turns these into `C:\…` and `\\server\share\…`. `Join-Path`,
  `Test-Path -LiteralPath` and `Push-Location -LiteralPath` all accept both
  forms. `CreateProcess` accepts a UNC `WorkingDirectory`. Only a `.cmd` shim
  would fall back to another directory, because `cmd.exe` refuses a UNC
  working directory, and `& git.cmd` did the same at `3298b0f`. Not run: the
  hook refused `git -C` onto a UNC path.
- **No console**: nothing in the launcher touches `[Console]` any more (grep
  finds no use), so CR2 stays fixed.

### Tests, checked with no finding

- **Junction cleanup** (`invoke-sh-cases.ps1:182-188`):
  `[IO.Directory]::Delete($junction)` without `recursive` calls
  `RemoveDirectoryW` on the reparse point. That removes the link and never
  lists the target's contents. If the call throws, the
  `Remove-Item -Recurse` after it does not run: the sandbox is left in
  `%TEMP%`, and nothing is deleted through the link. `Test-Path` skips a
  dangling junction, which has nothing behind it to delete. The target,
  `<sandbox>\repo …\scripts`, is inside the sandbox anyway, so even a 5.1
  `Remove-Item` that followed a junction could not reach anything outside
  `%TEMP%`.
- **False greens**: `probe.out` is removed before every case (`:121-122`), so
  the junction case cannot pass on the file left by the main checkout case.
  `$LASTEXITCODE` (`:129`) holds the child's exit code. The junction case
  catches a root built from PowerShell's location (the `cf333b9` mutant).

### New findings

**R3-1 — major — run from the pre-push hook of a linked worktree, the cases
write into the real repository.** `scripts/tests/invoke-sh-cases.ps1:92-96`
runs `git -C $mainRoot init / config / add / commit` and `worktree add`
with the caller's `GIT_DIR` still set. The chain that sets it:

- `.githooks/pre-push:23` runs `scripts/verify.sh`, and `core.hooksPath` is
  `.githooks` in this repository.
- `verify.sh:359-386` runs these cases whenever a `scripts/*.ps1` file is in
  the branch diff. That is true of this branch and of tech-176.
- Git exports `GIT_DIR` to hooks. Run in this worktree (the hook only prints
  its environment):

```
git -c core.hooksPath=<scratch> hook run pre-push
GIT_DIR=C:/Users/dev/Проекты/mustask/.git/worktrees/tech-172-invoke-sh-utf8-repo-root
```

When `GIT_DIR` is exported, `-C <dir>` does not select another repository. It
makes `<dir>` the work tree of the *same* repository:

```
git -c "alias.probe=!git -C docs/features rev-parse --show-toplevel --absolute-git-dir" probe
C:/…/tech-172-invoke-sh-utf8-repo-root/docs/features
C:/…/mustask/.git/worktrees/tech-172-invoke-sh-utf8-repo-root
```

So a `git push` from a task worktree has these effects:

- `config core.autocrlf false` writes to the real repository's config.
- `add scripts` stages the sandbox's `scripts/probe.sh` into the real
  worktree's index.
- `commit -m probe` creates a commit on the task branch.
- `worktree add` registers a worktree and a branch in the real repository.
  Deleting the sandbox leaves both stale.
- `init` may also reinitialise the real git directory.

After that, the launcher's `--show-toplevel` sees the same `GIT_DIR` and
prints the start directory, so every case fails. The push stops, and the
developer is left with a red check and a stray `probe` commit. These effects
are inferred from the two runs above, not reproduced: running them would
damage this repository.

Reachability today: there is no remote, so nothing pushes. Under AGENTS.md
("The last stage of a task is an MR"), pushing starts as soon as a remote
exists. `finish-task.sh:184` runs `verify.sh` from a plain shell and is not
affected.

Fix: at the top of the cases file, save and remove the variables that select
a repository: `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_COMMON_DIR`,
`GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES`, `GIT_NAMESPACE`
and `GIT_PREFIX`. Restore them in the existing `finally`. To prove the fix,
run the cases from a hook through `git -c core.hooksPath=<scratch> hook run
pre-push`, then check that `git status` and `git log -1` are unchanged. A
comment in `verify.sh` is not enough: the cases file is what creates the
repositories, so the fix belongs there.

**R3-2 — minor — git's own error is thrown away.** At `invoke-sh.ps1:36-41`,
git's stderr is discarded (`$null = $gitErrors.Result`), and any non-zero exit
becomes "Run this command inside a Git worktree." Take `fatal: detected
dubious ownership in repository` (`safe.directory`): it is the usual failure
on a share or in a directory owned by another account, which is exactly the
UNC case. The launcher then tells a user who *is* inside a worktree that they
are not. At `3298b0f`, Windows PowerShell 5.1 showed git's `fatal:` line, and
pwsh already discarded it. status.md "Notes (dev)" presents the change as an
improvement. Fix: when stderr is not empty, append it trimmed, e.g.
`throw "Run this command inside a Git worktree. $($gitErrors.Result.Trim())"`.

**R3-3 — minor — NF2 leftover: the opening sentence still claims more than
the mechanism.** `invoke-sh.ps1:69-70` says "The script must replace the login
shell rather than run under it: a login shell that exits runs …
bash_logout", and `invoke-sh-cases.ps1:135` says the same.
`invoke-sh-cases.ps1:85-86`, in the same diff, gives the actual rule: a
non-interactive login shell reads `bash_logout` only when it runs the `exit`
builtin. Round 2's run showed that `-c '"$@"'` without `exec` does not write
the marker.

The parenthetical "bash already execs a lone -c command" is not anchored.
This round's Git Bash process listing (a probe running `ps -l` under both
`--login -c '"$@"'` and `--login -c 'exec "$@"'`) looks the same for both
forms: the login `bash` is still listed next to the probe. MSYS therefore
neither confirms nor refutes the parenthetical.

This is minor rather than the "a sibling file contradicts it" major. Both
files agree on the outcome of every form the code and the tests use, and
keeping `exec` is harmless. Fix: say "a login shell reads bash_logout only
when it executes `exit` itself. Passing the script file to `bash --login`
makes the script's `exit` the login shell's; under `-c` it is not", and drop
the parenthetical.

### Behavioural claims checked (item 6)

Direction 1 — prose the round-3 diff adds or changes:
- `invoke-sh.ps1:13-17`, stdout read as UTF-8, console never touched, works with no console → `:25-39`; the file has no `[Console]` use.
- `invoke-sh.ps1:17-19`, a root taken from PowerShell's location breaks under links because git resolves them → the round 2 NF1 run; junction case at `invoke-sh-cases.ps1:107-116`; `cf333b9` mutant in the Log (not re-run).
- `invoke-sh.ps1:35`, draining stderr asynchronously prevents a blocked stdout read → `:36-39`.
- `invoke-sh.ps1:69-73`, the bash_logout mechanism → R3-3.
- `invoke-sh-cases.ps1:15-17`, the child also starts through a junction → `:110-116`, `:124`, `:65`.
- `invoke-sh-cases.ps1:107-109`, git resolves the link while PowerShell keeps the path as typed → the round 2 NF1 run.
- `invoke-sh-cases.ps1:182`, the junction is removed first so the recursive delete cannot follow it → `:183-188` (see "Tests" above).
- `invoke-sh-cases.ps1:138`, FAIL message "did not replace it" → R3-3.
- `status.md` Plan 1, history ending in a `Process` with `StandardOutputEncoding` UTF-8, root taken from git → `invoke-sh.ps1:25-43`.
- `status.md` Log, round-2 fixes: NF1 and NF4 → anchors above. NF2 → R3-3. "launcher from %TEMP% → Run this command inside a Git worktree." → `:40-41`. "with GIT_DIR/GIT_WORK_TREE set → exit 0" → `--show-toplevel` honours both. The mutant counts (6/6, 2/6, 6/6, 6/6, 3/6) are consistent with the code and the cases; not re-run. "Still open: CR5, CR6 hooks/templates" → matches.
- `status.md` Notes (dev), 5.1 now shows the launcher's message outside a worktree → `:40-41`; the cost of that change → R3-2.
- `verify.sh:358-389`, the cases run on Windows when a `scripts/*.ps1` file changes → unchanged since round 2; the environment they may run in → R3-1.

Direction 2 — prose outside the diff that describes the changed behaviour.
Behaviours: how the launcher runs git (`& git` → `Process`) and the new
junction case. Searched the repository for `show-cdup`,
`is-inside-work-tree`, `invoke-sh` and `StandardOutputEncoding`. Hits outside this feature's files: the six
`scripts/*.ps1` wrappers (call sites only), `scripts/verify.sh:358-388`
(already in scope) and the queue line `docs/backlog.md:52` (a summary, no
claim). For R3-1, `.githooks/pre-push:2-23` calls `verify.sh` the single entry
point but says nothing about what runs under it. Stale: none.

### Verdict

Not ready for gate-3. NF1 and NF4 are resolved, NF2 is resolved in substance,
and the launcher's switch to `Process` is sound in both editions for every
item in the brief. One major keeps this from being ready: **R3-1**. The
regression suite this task adds writes into the real repository when
`verify.sh` runs from the pre-push hook of a worktree. The defect is latent
while there is no remote, and the fix is a few lines in the cases file. Minors
still open: R3-2, R3-3, CR5 and the rest of CR6. NF3 and QA's two majors now
belong to tech-176. This round already went past the two-round limit, so the
human decides: fix R3-1 and re-check only that fix, or accept R3-1 at gate-3
as a known limit.

## Code review — Round 3 delta

Narrow review ordered by the human. Scope: `git diff be33947..c572317 --
scripts`, which is `scripts/tests/invoke-sh-cases.ps1` and
`scripts/invoke-sh.ps1`, plus the round-3 `[dev]` Log line in status.md.
Findings the human already routed are not reopened: QA's two majors, NF3
(both tech-176), CR5 and the rest of CR6.

**Limit of this round.** PowerShell could not be run. The worktree-isolation
hook refused `pwsh -NoProfile -ExecutionPolicy Bypass -File
scripts/tests/invoke-sh-cases.ps1` in its plain form from this worktree. It
also refused a scratch driver that set `GIT_DIR` at a throwaway repository
under `%TEMP%`, and a bash `GIT_DIR=<scratch> git rev-parse
--local-env-vars`. None of these refusals was routed around. Two things did
run: `git rev-parse --local-env-vars` in this worktree (git 2.47.1.windows.1),
and creating a throwaway repository with a linked worktree in the session
scratchpad. That repository was never touched by a case, and it has been
removed. Everything below about PowerShell behaviour is read from the code.
The pre-push and direct-run results are the developer's (status.md Log,
round 3 fixes), and this round did not repeat them.

### Round 3 findings

- **R3-1 — resolved.** `invoke-sh-cases.ps1:83-92` removes every variable
  that `git rev-parse --local-env-vars` prints, and it does so before the
  first sandbox git call (`:108`) and before the first child shell (`:143`).
  On this git that list is `GIT_ALTERNATE_OBJECT_DIRECTORIES`, `GIT_CONFIG`,
  `GIT_CONFIG_PARAMETERS`, `GIT_CONFIG_COUNT`, `GIT_OBJECT_DIRECTORY`,
  `GIT_DIR`, `GIT_WORK_TREE`, `GIT_IMPLICIT_WORK_TREE`, `GIT_GRAFT_FILE`,
  `GIT_INDEX_FILE`, `GIT_NO_REPLACE_OBJECTS`, `GIT_REPLACE_REF_BASE`,
  `GIT_PREFIX`, `GIT_SHALLOW_FILE` and `GIT_COMMON_DIR`. It covers every
  variable that selects a repository, a work tree, an index or an object
  store. It is git's own list for exactly this job (git clears it before
  running a command in another repository), so it follows the installed git
  and not a hand-kept copy. The three sources in the brief:
  - **Hook in a linked worktree:** git sets
    `GIT_DIR=<common>/.git/worktrees/<name>` (round 3 run). It is cleared.
  - **Hook in a main checkout:** git sets `GIT_DIR`, possibly relative. It is
    cleared whatever its value, and the listing itself needs no repository
    (below).
  - **Set by the user** in the session (`GIT_DIR`, `GIT_WORK_TREE`,
    `GIT_INDEX_FILE`, `-c` via `GIT_CONFIG_PARAMETERS`): cleared.

  Variables outside the list do not move writes out of the sandbox.
  `GIT_NAMESPACE`, which round 3's suggested list named, only prefixes refs
  for namespace-aware commands inside the same repository.
  `GIT_CEILING_DIRECTORIES` and `GIT_DISCOVERY_ACROSS_FILESYSTEM` only narrow
  discovery. Templates, hooks and `GIT_CONFIG_GLOBAL`/`GIT_CONFIG_SYSTEM` are
  CR6's remainder, already routed. `rev-parse --local-env-vars` is handled
  before rev-parse sets up a repository, so a stale or relative `GIT_DIR`
  cannot make the listing fail. This round could not run that with `GIT_DIR`
  set (hook refusal). The developer's hook run, where `GIT_DIR` was set and
  the result was 6/0 with the repository unchanged, is the evidence that it
  works. Mechanics per edition: `Remove-Item Env:\<name>` deletes the
  variable from the process environment in 5.1 and in 7, and both editions
  build a native child's environment from the process environment. The saved
  values are .NET strings, so a Cyrillic `GIT_DIR` survives the round trip.
  `$savedGitEnv` is assigned before `try`, so under `Set-StrictMode` the
  `finally` always sees it.
- **Restore in `finally` — correct in both editions for the normal and the
  failing-case path, with one gap → R3D-1.** `:206-208` sets each saved
  value back with `[Environment]::SetEnvironmentVariable`. Every value it
  restores was present and non-null when saved (`:86`). A git hook never
  sets an empty `GIT_DIR`, so the ""-versus-delete difference between .NET
  Framework and .NET Core (the reason for `:88-89`) does not come up on
  restore. `exit` (`:213`, `:215`) comes after `finally`, and Ctrl+C runs
  `finally` in both editions. The restore only matters when someone runs the
  file in-session (`& scripts/tests/invoke-sh-cases.ps1`). Its only automated
  caller, `verify.sh:381-382`, starts a separate `-File` process.
- **R3-2 — resolved.** `invoke-sh.ps1:34` decodes stderr as UTF-8 too, so a
  Cyrillic path inside git's `fatal:` line is not mojibake. `:40-46` appends
  the trimmed stderr when there is any, and it keeps the bare message
  otherwise. Stderr is still drained asynchronously (`:37`) and read only
  after `WaitForExit` (`:39-40`), so the deadlock analysis from round 3 still
  holds. A warning on stderr with exit 0 is ignored, as before.
- **R3-3 — resolved.** The parenthetical "bash already execs a lone -c
  command" is gone. Neither the scripts nor any doc outside this feature
  contains it, and the only remaining hit is the historical round-2 Log line.
  `invoke-sh.ps1:74-77`, `invoke-sh-cases.ps1:151-155` and the FAIL message
  now state one rule: the script handed to `bash --login` as a file makes its
  `exit` the login shell's, and that reads `bash_logout`. The probe comment
  `:101-102` and bash's documented rule (a non-interactive login shell reads
  it only on the `exit` builtin) agree with it.

### New findings

**R3D-1 — minor — the environment restore runs last in `finally`, after
the two deletes that can throw.** `invoke-sh-cases.ps1:200-208`. Under
`$ErrorActionPreference = 'Stop'` (`:25`), a failing
`[IO.Directory]::Delete($junction)` or a `Remove-Item -Recurse` that meets a
locked file raises a terminating error, and that ends the `finally` block.
The `foreach` that restores the saved variables never runs, so an in-session
caller loses its `GIT_DIR`/`GIT_WORK_TREE` without being told. `verify.sh` is
not affected (separate process), and the effect is a lost variable, not a
write into a repository. Fix: move the restore loop to the top of `finally`,
or wrap the deletes in their own `try`.

**R3D-2 — minor — status.md "Notes (dev)" now contradicts the launcher.**
status.md, `## Notes (dev)`, first bullet: "outside a Git worktree Windows
PowerShell 5.1 now shows the launcher's own message instead of git's
`fatal: not a git repository`". Since R3-2, `invoke-sh.ps1:43-44` shows both
messages, and the round-3 `[dev]` Log line says so ("Run this command inside
a Git worktree. git: fatal: not a git repository …"). A reader who acts on the
note does nothing different, so this is minor. Fix: change the bullet to say
that the launcher's message now carries git's reason, or delete it.

No regression found in the delta. The comment rewording changes no code
path. The stderr change affects only the failure branch, and the environment
clearing happens before the cases run, so every case and child still sees
the same state as at `dc86a11` when started from a clean shell.

### Behavioural claims checked (item 6)

Direction 1 — prose the delta adds or changes:
- `invoke-sh-cases.ps1:78-80`, when run as a hook through pre-push → verify.sh, the process inherits `GIT_DIR`, which beats `git -C`, and init/config/add/commit would land in the real repository → `.githooks/pre-push:22`, `verify.sh:381-382`; the round 3 runs; the orchestrator's throwaway reproduction (Log).
- `invoke-sh-cases.ps1:80-82`, every variable git lists is cleared for the sandbox calls and the children → `:84-91` before `:108` and `:143`; "restore them at the end" → `:206-208`, with R3D-1 as the gap.
- `invoke-sh-cases.ps1:88-89`, pwsh 7 turns `$null` into "" and git rejects an empty `GIT_DIR` → the developer's first-attempt observation (Log); not re-run.
- `invoke-sh-cases.ps1:151-155` and the FAIL message → R3-3 above.
- `invoke-sh.ps1:42`, "dubious ownership" is not "outside a worktree" → `:43-44` carries git's text.
- `invoke-sh.ps1:74-77`, the bash_logout mechanism → R3-3 above.
- status.md Log, `[dev] round 3 fixes`: the mechanism → `:83-92`, `:206-208`; R3-2 → `invoke-sh.ps1:34`, `:40-46`; R3-3 → no occurrence of "lone -c" remains in the scripts; the "Outside a worktree" message format → `invoke-sh.ps1:44`. The run results (hook 6/0 with the repository unchanged, `dc86a11` polluting the repository, direct `GIT_DIR` runs in both editions, `verify.ps1` green) are consistent with the code, but this round did not re-run them.

Direction 2 — prose outside the delta that describes the changed behaviours.
Two behaviours changed: the launcher's failure message, and the cases'
handling of the environment. Searched `*.md`, `*.ps1`, `*.sh` and `pre-push`
for `inside a Git worktree`, `lone -c`, `replace the login shell`, `did not
replace`, `local-env-vars` and `GIT_DIR`.
Hits: this feature's status.md (the Notes bullet became R3D-2; the Log lines
are history); `docs/features/tech-097-…/status.md:30`, which is unrelated
("$GIT_DIR too big"). `.githooks/pre-push` makes no claim about the
environment. Stale: R3D-2 only.

### Verdict

Ready for gate-3. R3-1 is resolved: the cases can no longer write outside
their sandbox through inherited repository-selecting git variables, whether
those come from a hook in a main checkout or a linked worktree, or from the
user. R3-2 and R3-3 are resolved as well. The delta introduces no blocker or
major. Open minors: R3D-1, R3D-2, CR5 and the rest of CR6. This round ran no
PowerShell: the conclusions about both editions are read from the code and
rest on the developer's logged runs.
