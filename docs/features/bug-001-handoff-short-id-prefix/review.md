# bug-001-handoff-short-id-prefix — review

## QA

No defects. Zero blockers.

Scope checked, beyond the plan's own cases:

- The other two id lookups in `handoff.sh` (the queue line in main, the
  `docs/features` folder in main) already anchor on the full `$short`
  (prefix included) — unaffected by this bug's fallback, unchanged by the
  fix.
- `scripts/backlog.sh` line 50 turns every `<prefix>/<NNN>` branch into
  `<prefix>-<NNN>` for its own listing; it never strips a prefix to look one
  id up, so it does not carry the same defect.
- `scripts/queue.sh`, `scripts/new-task.sh`, `scripts/finish-task.sh`: every
  short-id-to-branch pattern in these three takes the prefix as its own
  argument or matches on the full id (`$1-`), never reconstructs a branch
  name by stripping a prefix off a short id and guessing at a class of
  prefixes (`[a-z]+/...`) the way the old `handoff.sh` fallback did — no
  sibling of this bug found.
- A queued line and a started branch cannot coexist for the same id:
  `queue.sh take` deletes the queue line once the branch (or folder) exists,
  so "a branch that is also queued" is not a reachable state to add a case
  for.
- "A task merged while its branch is kept" is already the shape of case 3
  (`doc-001`, folder + `stage: complete` in main, branch `doc/001-domain-map`
  kept) — not a gap.
- `hot` is not special-cased anywhere in `handoff.sh` (only appears in the
  short-id format regex); the fix's pattern is prefix-agnostic, and the
  already-present tech-002 case (in-flight, no queue line, no folder)
  exercises the same code path a `hot-NNN` id would. No separate case
  needed.
- A third prefix sharing the same number (e.g. `doc/001`, `mod/001`,
  `tech/001` all present) would not exercise any code path the current
  two-prefix collision (`doc/001` vs `mod/001`, tested by case 1) does not
  already cover: the fixed pattern anchors on `^${short%%-*}/${short#*-}-`,
  which fully specifies both prefix and number, so it cannot be fooled by
  how many other prefixes also hold that number.

Confirmed the cases fail without the fix: copied `main`'s `scripts/handoff.sh`
and this branch's `scripts/tests/handoff-cases.sh` into a scratch
`scripts/`+`scripts/tests/` layout outside the repo and ran the case file
against it — cases 1 and 2 FAIL, cases 3 and 4 PASS, exit 1, matching the
dev log's pre-fix record.

## Commands run

- `sh scripts/tests/handoff-cases.sh` against `main`'s `handoff.sh` (copied to
  a scratch dir, this branch's case file) → 2/4 FAIL, exit 1 (pre-fix,
  confirms the cases catch the bug)
- `sh scripts/tests/handoff-cases.sh` (in-repo, fixed `handoff.sh`) → 4/4
  PASS, exit 0
- `bash scripts/verify.sh` → green, exit 0 (includes "handoff.sh: short-id
  resolution cases")

## Verdict

Pass. No defects.

## Code review

### Round 1 — scripts

Verdict: **approve**. 0 blocker, 0 major, 1 minor, 4 nit. None of them blocks
gate-3; the minor is worth a one-line follow-up, the human decides whether it
goes into this branch or a later one.

Scope: `git diff main...HEAD` — `scripts/handoff.sh` (+2/-1),
`scripts/tests/handoff-cases.sh` (new), `scripts/verify.sh` (+13), this
folder's `status.md` and `review.md`. Conventions: none of `backend.md` /
`frontend.md` applies; branch and commits follow `git.md` (task branch,
`bug-001: ` subjects).

The fix itself is correct. `short` is validated at `scripts/handoff.sh:69`
against `^(feat|bug|hot|tech|mod|doc)-[0-9]{3}$`, so `${short%%-*}` is always a
known prefix and `${short#*-}` three digits, with no regex metacharacters
reaching `grep -E`; `^mod/001-` cannot match another prefix's branch, and the
match no longer depends on `git branch` ordering. Reproduced in the real
repository: `main`'s `handoff.sh mod-001` now resolves to
`bug-001-handoff-short-id-prefix` (the defect, with a new sibling sorting
first); the branch's resolves to `mod-001-process-seed`, stage
`01-business-analysis`.

#### Findings

**R1-1 (minor) — `scripts/tests/handoff-cases.sh:24-25`: the isolation
comment claims more than the code does.** "The user's own git configuration
(hooks, signing, templates) stays out" is enforced only by `HOME=` and
`GIT_CONFIG_NOSYSTEM=1`. Git also reads `$GIT_CONFIG_GLOBAL` (which overrides
`HOME`) and `$XDG_CONFIG_HOME/git/config`, and neither is neutralised.
Failure scenarios, both reproduced on git 2.47.1.windows.1:

- A `core.hooksPath` with a rejecting `pre-commit` in either location →
  the harness dies during setup at the first `g commit` (`set -e`) with no
  PASS/FAIL line, exit 1; `verify.sh` reports
  "FAIL: handoff.sh: short-id resolution cases" for a change that is fine.
- `branch.sort = -refname` in `$GIT_CONFIG_GLOBAL` → against `main`'s
  (unfixed) `handoff.sh`, case 1 PASSES: `git branch` lists `mod/001-…`
  before `doc/001-…`, so the old `^[a-z]+/001-` picks the right branch by
  luck. Case 1 is the case named after the defect, and it can pass
  vacuously; the suite still goes red only because case 2 does not depend
  on order.

Fix: add `GIT_CONFIG_GLOBAL="$sandbox/home/.gitconfig"` (or `/dev/null`) to
the `export`, and `unset XDG_CONFIG_HOME GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT`
next to the existing `unset`; or weaken the comment to what `HOME` covers.

**R1-2 (nit) — `scripts/tests/handoff-cases.sh:22`: `trap … INT TERM`
cleans up but does not exit.** In POSIX sh a trapped INT runs the action and
the script carries on: Ctrl-C mid-run deletes the sandbox, then the next
`g …` dies under `set -e`, or, inside the cases section, `run` swallows the
failure and the script prints spurious `FAIL` lines. Fix:
`trap 'rm -rf "$sandbox"' EXIT` plus `trap 'exit 130' INT TERM`, so the
EXIT trap does the cleanup once.

**R1-3 (nit) — `scripts/tests/handoff-cases.sh:92-96`: case 3 cannot see
the change.** `doc-001` resolves through the `docs/features` lookup
(`handoff.sh:105`) and never reaches the branch fallback the diff touches.
It passes before and after the fix (confirmed), so it guards the unchanged
merged-detection path, not this fix. Harmless. Its comment says so
("through the folder in main"), so this is not a false claim. Recorded so
nobody counts it as coverage of the fix.

**R1-4 (nit) — `scripts/tests/handoff-cases.sh:14`: "(verify.sh runs it)"**
— only when `scripts/handoff.sh` or the case file is in the change set
(`scripts/verify.sh:387-398`). "verify.sh runs it when handoff.sh or this file
changes" is what the wiring does.

**R1-5 (nit) — QA prose in this file, two overstatements that do not
change its conclusions.**
- "`hot` … only appears in the short-id format regex": it also appears in
  the type mapping at `scripts/handoff.sh:151`. The conclusion (no special
  lookup path for `hot`) stands.
- "A queued line and a started branch cannot coexist … not a reachable
  state": they do coexist between `new-task.sh` and `queue.sh take`.
  `scripts/queue.sh:256-266` refuses to remove the line until the branch
  exists. In that state the queue lookup (`handoff.sh:96-102`,
  prefix-anchored) resolves the id before the fallback, so no case is
  missing. The sentence is wrong, the conclusion is right.

#### Tests — do they check what they claim

- Cases 1 and 2 fail against `main`'s `handoff.sh` and pass against the fix
  (reproduced with a scratch `scripts/` + `scripts/tests/` layout outside the
  repo). Case 2 fails the old code regardless of ref order. Case 1 does
  too in a clean environment; see R1-1 for when it does not.
- Case 4 is a positive control: the fallback still finds a branch with
  an unshared number, so a fix that over-tightened the fallback into
  matching nothing goes red. Case 3: R1-3.
- Neither PASS path can be satisfied by stale output: cases 1 and 4
  `rm -f "$out"` first and require exit 0 plus the `Continue task <id>.` line
  in a freshly written prompt; cases 2 and 3 require a non-zero exit plus the
  specific message.
- Portability: the base branch comes from `symbolic-ref`, so
  `init.defaultBranch` does not matter; identity and signing are passed per
  command; `GIT_DIR`, `GIT_WORK_TREE` and `GIT_INDEX_FILE` are unset, which
  matters under the `pre-push` hook. It ran green here under Git Bash, where
  `sh` is bash in POSIX mode, with Windows `C:/…` paths coming back from
  `git worktree list`. Not run under dash, the `sh` of `ubuntu-latest` in
  `.github/workflows/ci.yml:54`. A read of both files found no bashisms
  (no `local`, `[[`, arrays or `source`), but that is a gap I am naming,
  not a check I ran.

#### verify.sh wiring

`scripts/verify.sh:387-398` sits inside `if [ "$need_shell" = 1 ]`. Both
trigger paths classify as `shell` (`scripts/*`), so the block is reachable
exactly when it should be. It copies the `touches_wrappers` idiom next to
it, and uses `pass`/`fail` like its neighbours. There is no OS gate, and
there should be none: unlike the `.ps1` cases, this harness needs nothing
Windows-only. Confirmed in the run below: the classification lists both
files as `shell`, and the cases ran.

#### Behavioural claims (item 6)

Direction 1: claims the diff adds or changes.
- `handoff.sh:107-108` "numbers are per prefix … mod-001 and doc-001 are
  different tasks" → anchored at `scripts/queue.sh:100` (next number =
  max over `^$prefix-[0-9]{3}$` only).
- `handoff-cases.sh:3-8` defect description (`^[a-z]+/001-`,
  doc/001 matched first, reported as already merged) → `main`'s
  `handoff.sh` fallback line plus `handoff.sh:119-122`. Reproduced with
  main's script in the sandbox: case 1 output.
- `handoff-cases.sh:10-12` "the shape the real one had … do not depend on
  this checkout's branches" → real repo holds `doc/001-domain-map` (folder in
  main, `stage: complete`) and `mod/001-process-seed`. The script runs with
  `cd "$repo"` against a sandbox repo. Anchored.
- `handoff-cases.sh:14` "(verify.sh runs it)" → R1-4.
- `handoff-cases.sh:24` isolation claim → R1-1.
- `verify.sh:387` comment → the block itself. Anchored.
- status.md Plan 2 / Log 40, "queue and docs/features lookups … and
  backlog.sh keep the prefix" → `handoff.sh:97` (`$short-`), `handoff.sh:105`
  (`^$short-`), `backlog.sh:50` (`[a-z]+/[0-9]{3}` → `prefix-NNN`, no
  stripping). Anchored.
- status.md Log 39/43, "cases 1 and 2 fail before the fix, 3–4 pass" →
  reproduced. Plan 3's "The cases fail before the fix" reads as all of them.
  Log 39 states which, so nothing an agent does differs; no finding.
- status.md Log 41, "`handoff.sh mod-001 --command-only` in the real
  repository → 'Continue task mod-001-process-seed', exit 0" → reproduced.
  Stdout is the `cd … && claude -n 'mod-001-process-seed' …` command, and
  the "Continue task" line is in the prompt file it writes. True in
  substance.
- QA section claims → anchored, except the two in R1-5.
- Commit messages: 87c5f1f subject matches the diff. eaa3512's body
  matches the QA section, with the same caveats as R1-5.

Direction 2: prose describing the changed behaviour, outside the diff.
Behaviour: how `handoff.sh` resolves a short id to a branch. Grepped
`handoff.sh` under `docs/`, `.claude/` and the `AGENTS.md` files. Hits:
`docs/ai-workflow/handoff.md:3,76`, `docs/ai-workflow/README.md:57`,
`docs/ai-workflow/stages/00-orchestrator.md:127`,
`.claude/commands/feature.md:37`, `docs/backlog.md:15` (this task's own
queue line, on the branch only), and
`docs/templates/examples/tech-172-invoke-sh-utf8-repo-root/review.md:293`.
None describes the lookup order or the branch fallback's matching, so no
stale prose. Also grepped `scripts/`, `.githooks/` and `.claude/hooks/` for
the same prefix-stripping shapes (`#*-`, `%%-*`, `[a-z]+/`). The only other
hit is `backlog.sh:50`, which lists ids and does not look one up.
`handoff.ps1` delegates to `handoff.sh` through `invoke-sh.ps1`. Nothing
else in `scripts/` needed the fix.

#### Commands run

- `sh scripts/tests/handoff-cases.sh` → 4/4 PASS, exit 0
- `bash scripts/verify.sh` → RESULT: green, exit 0 (includes
  "handoff.sh: short-id resolution cases")
- `main`'s `handoff.sh` + this branch's case file in a scratch layout,
  `sh …/handoff-cases.sh` → cases 1 and 2 FAIL, exit 1
- the same with `GIT_CONFIG_GLOBAL` → `branch.sort=-refname` → case 1 PASS,
  case 2 FAIL (R1-1)
- in-repo `sh scripts/tests/handoff-cases.sh` with `XDG_CONFIG_HOME`, then
  with `GIT_CONFIG_GLOBAL`, each pointing at a rejecting `core.hooksPath` →
  exit 1 at setup, no case lines (R1-1)
- `bash scripts/handoff.sh mod-001 --command-only --out <scratch>` → exit 0,
  prompt "Continue task mod-001-process-seed"; `main`'s version → resolves to
  `bug-001-handoff-short-id-prefix`, exit 0 (the defect)

### Round 2 — scripts

Verdict: **approve**. The delta is `git diff da018c7..a10ed23`, which changes
`scripts/tests/handoff-cases.sh` and adds one `[dev]` line to `status.md`.
R1-1, R1-2 and R1-4 are closed. R1-3 and R1-5 stay as recorded: they were
informational and need no change. No new findings. Open: 0 blocker,
0 major, 0 minor, 2 nit (R1-3, R1-5), both deliberately left as they are.

#### Round 1 findings, re-checked

- **R1-1 (closed): `handoff-cases.sh:26-33`.** `GIT_CONFIG_GLOBAL` now
  points to an empty file in the sandbox, and `XDG_CONFIG_HOME`,
  `GIT_CONFIG_PARAMETERS` and `GIT_CONFIG_COUNT` are unset.
  `GIT_CONFIG_KEY_n`/`VALUE_n` left behind are inert without the count. I
  reproduced both round 1 scenarios against a10ed23 with a config holding a
  rejecting `core.hooksPath` and `branch.sort=-refname`:
  - Fixed script, config through `XDG_CONFIG_HOME` → 4/4 PASS, exit 0.
    Through `GIT_CONFIG_GLOBAL` → 4/4 PASS, exit 0. Through
    `GIT_CONFIG_PARAMETERS` → exit 0. Through `GIT_CONFIG_COUNT`/`KEY_0`/
    `VALUE_0` → exit 0. The rejecting hook no longer reaches the setup.
  - `main`'s script with this case file, `branch.sort=-refname` through
    `GIT_CONFIG_GLOBAL` → cases 1 and 2 FAIL, exit 1. The same with
    hooksPath and sort through `XDG_CONFIG_HOME` → cases 1 and 2 FAIL,
    exit 1. Case 1 no longer passes vacuously; in round 1 it did.
  - The new comment at `:26-28` claims that a rejecting hooksPath would fail
    the setup and that a mod-before-doc `branch.sort` would let case 1 pass
    against the unfixed script. Both are anchored by the round 1
    reproductions.
- **R1-2 (closed): `handoff-cases.sh:23-24`.** Cleanup now runs on EXIT only,
  and INT and TERM exit with 130. I sent INT at 0.2 s to 4.3 s, which covers
  both setup and the cases section, under `sh` (Git Bash) and under `dash`.
  I also sent TERM at 3.5 s under `dash`. Every run exited 130, left no
  sandbox under `/tmp`, and printed no FAIL line.
- **R1-4 (closed): `handoff-cases.sh:14-15`.** The comment now says "verify.sh
  runs it when handoff.sh or this file changes", which matches
  `scripts/verify.sh:387-398`.
- **R1-3, R1-5: unchanged by design.**

#### Round 1 gaps, closed

- The dash gap named in round 1 is closed: `dash scripts/tests/handoff-cases.sh`
  gives 4/4 PASS, exit 0. With `main`'s `handoff.sh` under `dash`, 2 cases
  FAIL.

#### Observation (not a finding)

With a long `TMPDIR` on Windows the setup fails with `Filename too long` at
`git add`. My session scratchpad path, about 150 characters, plus
`tmp.*/repo/.git/objects/…` goes past MAX_PATH. That is not a regression of
round 2: since round 1, the user's global config, including any
`core.longpaths`, has not applied inside the sandbox. The default `/tmp`
path is short and works. It is recorded so nobody mistakes it for a
defect in the fix.

#### Behavioural claims (item 6)

- Direction 1: the claims the delta adds.
  - The `handoff-cases.sh:15` usage comment is anchored at
    `verify.sh:387-398`.
  - The `handoff-cases.sh:26-28` isolation comment is anchored by the
    reproductions above.
  - `status.md` Log line 45 claims that under `branch.sort=-refname` and a
    rejecting hooksPath, main's script fails cases 1–2 and the fixed one
    passes 4/4, and that `verify.sh` exits 0. All three reproduced.
- Direction 2: the delta changes only the harness's environment and signal
  handling, not `handoff.sh`'s behaviour. I grepped `handoff-cases` under
  `docs/`, `.claude/` and the `AGENTS.md` files. The only hits are this
  task's own `status.md` and `review.md`, so no other prose describes the
  harness.

#### Commands run

- `sh scripts/tests/handoff-cases.sh` → 4/4 PASS, exit 0
- `dash scripts/tests/handoff-cases.sh` → 4/4 PASS, exit 0
- fixed, a config with hooksPath and `branch.sort=-refname` via
  `XDG_CONFIG_HOME`, then via `GIT_CONFIG_GLOBAL` → 4/4 PASS, exit 0 each
- fixed, hooksPath via `GIT_CONFIG_PARAMETERS`, then via
  `GIT_CONFIG_COUNT` → exit 0 each
- `main`'s `handoff.sh` + this case file, `branch.sort=-refname` via
  `GIT_CONFIG_GLOBAL` → cases 1–2 FAIL, exit 1. The same with hooksPath and
  sort via `XDG_CONFIG_HOME` → exit 1. Under `dash` → 2 FAIL
- `timeout --preserve-status -s INT <0.2…4.3s>` under `sh` and `dash`,
  and `-s TERM 3.5s` under `dash` → exit 130, 0 leftover sandboxes, 0 FAIL
  lines
- `bash scripts/verify.sh` → RESULT: green, exit 0
