# tech-172-invoke-sh-utf8-repo-root

- type: tech
- size: S
- branch: tech/172-invoke-sh-utf8-repo-root
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved (2026-09-17, human in chat)

## Plan

Reproduce: PowerShell 7 or 5.1, console at OEM 866, any checkout whose path
has Cyrillic —
`[Console]::OutputEncoding = [Text.Encoding]::GetEncoding(866); & scripts/invoke-sh.ps1 scripts/backlog.sh --ids`
→ `Shell script not found: C:\...\<mojibake>\mustask\...` (reproduced in
both editions on 2026-09-16).

1. `scripts/invoke-sh.ps1`: read git's `--show-toplevel` as UTF-8 without
   depending on the console encoding. History: console encoding switched
   around `& git` (171da0f) → review CR2 (breaks with no console) →
   `--show-cdup` joined to PowerShell's location (cf333b9) → review NF1
   (wrong root through links) → git run as a `System.Diagnostics.Process`
   with `StandardOutputEncoding` UTF-8; the console is never touched and the
   root is git's, as before.
2. Audit the rest of the launcher: the working directory and the arguments
   handed to Git Bash, Cyrillic included.
3. Regression test `scripts/tests/invoke-sh-cases.ps1`: a throwaway repository
   and linked worktree under a Cyrillic directory in %TEMP%, the launcher run
   in pwsh and powershell.exe with the console at 866; checks the exit code,
   bash's working directory and the arguments. Must fail before the fix.
4. `scripts/verify.sh` runs the test whenever a `scripts/*.ps1` file changes.

## Log

- 2026-09-16 [new-task] task started, worktree C:/Users/dev/Проекты/mustask/.claude/worktrees/tech-172-invoke-sh-utf8-repo-root
- 2026-09-16 [orchestrator] route confirmed by the human: tech, S, full S cycle (dev → QA → code review → gate-3 → local squash)
- 2026-09-17 [dev] red first: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/tests/invoke-sh-cases.ps1` on the unchanged launcher → 4 cases, 4 failed, `Shell script not found: ...\invoke-sh-<mojibake>-...\scripts\probe.sh`
- 2026-09-17 [dev] fix 1: `git rev-parse --show-toplevel` decoded as UTF-8, console encoding restored in `finally`; → pwsh cases green, powershell.exe cases still failed on `arguments 'plain|arg-…|with|space'`
- 2026-09-17 [dev] second defect found by the audit (step 2): under legacy native argument passing (5.1, pwsh < 7.3) the unescaped quotes of `-c 'exec "$@"'` split every argument with a space. Dropping the `-c` bootstrap was tried and rejected: a login shell that exits runs Git for Windows' `/etc/bash.bash_logout`, which clears the screen over the output. Kept `exec`, quotes escaped by hand only in legacy mode
- 2026-09-17 [dev] each check proven by a mutant: no legacy escaping → 2 of 4 failed (arguments); no `exec` → 4 of 4 failed (bash_logout marker; the probe ends with `exit 0` because a non-interactive login shell reads bash_logout only on the exit builtin)
- 2026-09-17 [dev] `scripts/tests/invoke-sh-cases.ps1` from pwsh and from powershell.exe → 4 cases, 0 failed each; real wrapper `backlog.ps1 --ids` with the console at 866 → exit 0 in both editions, no screen clear; `scripts/verify.ps1` → RESULT: green, exit 0 (runs the new cases)
- 2026-09-17 [qa] `docs/templates/examples/tech-172-invoke-sh-utf8-repo-root/review.md`: no blockers on 171da0f; `bash scripts/verify.sh` and `pwsh -File scripts/verify.ps1` both green, exit 0; the plan's own repro line reproduced fixed in both editions against the real (Cyrillic) worktree. 2 majors found, both pre-existing (confirmed unchanged against `171da0f^`, outside this diff's param block): Windows PowerShell 5.1 silently drops an empty-string argument, and single-dash arguments (`-v`, `-d`, `-s`, `-e`, ...) are silently reinterpreted as `[CmdletBinding()]` common parameters or abort the launcher with an ambiguous-parameter error — recommend a follow-up tech task. E2E question: not applicable, no frontend/backend runtime surface touched.
- 2026-09-17 [code-review] round 1 (tooling) in review.md: no blocker/major introduced by the diff, 8 minors open (CR1-CR8), QA's 2 pre-existing majors still open; static only, the session hook refused PowerShell runs
- 2026-09-17 [dev] round 1 fixes. Confirmed by run: CR1 (console output code page 65001 → 866 after the cases, read with GetConsoleOutputCP). Fixed: CR1 (cases restore the console encoding; after run 65001 in both editions), CR2 (root via `--show-cdup`, no encoding change), CR3 (child starts in `<root>\scripts`), CR4 (`Push-Location -LiteralPath`), CR6 in part (`commit.gpgsign=false`; stderr redirects under `Continue`), CR7 (verify.sh runs the cases only on MINGW/MSYS/CYGWIN, else NOT CHECKED; redundant pattern dropped), CR8 (probe comment names verify.sh). Not fixed: CR5 (no pwsh < 7.3 or Legacy-mode run on this machine), CR6 `core.hooksPath`/templates. Mutants, each run through the cases from pwsh: launcher at 3298b0f → FAIL all, `exit code 1`, probe did not run; no `Push-Location` → FAIL, `exit code 127`; `-c $bootstrap --` replaced by `--login $Script` → FAIL, bash_logout marker; `exec "$@"` unescaped in legacy mode → 2 of 4 failed (powershell.exe, arguments split). Unmutated: 4 cases, 0 failed from pwsh and from powershell.exe; `backlog.ps1 --ids` from `docs/features` at 866 → exit 0 both editions; `scripts/verify.ps1` → RESULT: green, exit 0
- 2026-09-17 [code-review] round 2 (tooling) in review.md: 1 major open (NF1, show-cdup root wrong through a link below the root, shown by run), 4 new minors plus CR5 and part of CR6 open; round limit reached, escalate to the human; PowerShell runs refused by the session hook
- 2026-09-17 [orchestrator] human decisions on round 2: fix NF1 and run review round 3; the out-of-scope defects (QA's 2 pre-existing majors, NF3) go to ONE follow-up task — queued as tech-176-ps-wrapper-argument-passing (deps tech-172)
- 2026-09-17 [dev] round 2 fixes: NF1 (git run as a Process, stdout read as UTF-8, root = `--show-toplevel`), NF4 (removed with NF1), NF2 (comment and failure message now say a login shell that exits runs bash_logout and a lone -c command is already exec-ed; the earlier "[dev] … no `exec`" log line means the mutant that passed the script file to `bash --login`). Cases gained "through a junction" (a junction outside the repo onto `<main>\scripts`): 6 cases. Mutants, each through the cases from pwsh: launcher at 3298b0f → 6 of 6 failed; launcher at cf333b9 (show-cdup) → 2 of 6 failed, exactly the two junction cases; no `Push-Location` → 6 of 6; script file to `--login` → 6 of 6; no legacy escaping → 3 of 6 (all powershell.exe). Unmutated: 6 cases, 0 failed from pwsh and from powershell.exe, console output code page 65001 before and after. Both editions at 866: `backlog.ps1 --ids` from `docs/features` → exit 0; launcher from %TEMP% → "Run this command inside a Git worktree."; launcher from %TEMP% with GIT_DIR/GIT_WORK_TREE set → exit 0. `scripts/verify.ps1` → RESULT: green, exit 0. Still open: CR5 (Legacy mode on pwsh 7.6 not run), CR6 hooks/templates
- 2026-09-17 [code-review] round 3 (tooling) in review.md: NF1, NF4 resolved, NF2 in substance; 1 major open (R3-1, the cases inherit GIT_DIR and write into the real repo when verify.sh runs from pre-push), minors R3-2, R3-3, CR5, CR6 (rest); not ready for gate-3, the human decides; PowerShell runs refused by the session hook
- 2026-09-17 [orchestrator] R3-1 reproduced safely before asking: GIT_DIR pointed at a throwaway repo, cases from dc86a11 → its `core.autocrlf` true → false, cases failed `fatal: this operation must be run in a work tree`. Human decision: fix R3-1 (+ R3-2, R3-3) and a narrow review of that delta only
- 2026-09-17 [dev] round 3 fixes: R3-1 (cases remove every variable `git rev-parse --local-env-vars` lists, restore in `finally`; `Remove-Item Env:` because pwsh 7 passes `SetEnvironmentVariable($name, $null)` as "" and git then fails `The empty string is not a valid path` — seen on the first attempt), R3-2 (launcher appends git's stderr, read as UTF-8, to its message), R3-3 (comment and failure message state the rule shown by the mutant: the script run as a file of `bash --login` makes its `exit` read bash_logout; the unevidenced "bash execs a lone -c command" is gone). Proof through the real mechanism: `git -C <throwaway linked worktree> -c core.hooksPath=<scratch> hook run pre-push` (hook shows `GIT_DIR=…/.git/worktrees/…`) running the cases → fixed: 6 cases, 0 failed, hook exit 0, repo unchanged (commits 1, autocrlf true, worktrees 2, status 0); dc86a11 cases → hook exit 1, repo polluted (commits 2, local `core.autocrlf=false`, worktrees 3, status 2). Direct runs with GIT_DIR, and GIT_DIR + GIT_WORK_TREE, on a throwaway repo: 6/0 from pwsh and powershell.exe, repo unchanged. Outside a worktree, both editions: "Run this command inside a Git worktree. git: fatal: not a git repository …". `scripts/verify.ps1` → RESULT: green, exit 0. Scratch removed
- 2026-09-17 [code-review] round 3 delta in review.md: R3-1, R3-2, R3-3 resolved; no major; 2 new minors (R3D-1, R3D-2), CR5 and CR6 rest open; ready for gate-3; PowerShell runs refused by the session hook
- 2026-09-17 [dev] after the narrow review (not reviewed again, flagged for gate-3): R3D-1 GIT_* restore moved to the top of `finally`; R3D-2 Notes (dev) line updated. Run: GIT_DIR on a throwaway repo, cases dot-run inside one pwsh and one powershell.exe session → 6 cases, 0 failed each, `GIT_DIR` equal to the original afterwards in the same session, throwaway repo local config untouched; `scripts/verify.ps1` → RESULT: green, exit 0
- 2026-09-17 [orchestrator] gate-3: approved by the human in chat (option "approve, squash"), aware that ef7c2be (R3D-1, R3D-2) was verified by the cases and verify.sh only, not re-reviewed; open: tech-176 (QA majors, NF3), CR5, CR6 remainder

## Decisions

- 2026-09-17 [qa] E2E question (S route step 3): No — this task has no `src/frontend/` or `src/backend/` change, no UI, no API; there is nothing a Playwright suite could exercise.

## Notes (dev)

- Resolved by the round 2 fix (git no longer runs as `& git … 2>$null`): outside a Git worktree Windows PowerShell 5.1 now shows the launcher's own message, followed since R3-2 by git's reason (`… git: fatal: not a git repository …`), instead of git's bare error.
- Known limit of legacy argument passing, unchanged: an argument that itself contains a double quote is still mangled by 5.1.

## Notes

Summary (from the queue line or an argument): Fix scripts/invoke-sh.ps1: repo root from git stdout is mojibake when the path has Cyrillic, every .ps1 wrapper throws
