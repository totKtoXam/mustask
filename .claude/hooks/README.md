# .claude/hooks — hooks and permissions for Claude Code

## block_wide_fs_search.py — blocking a wide search across the disk

The rule in one line is in `AGENTS.md` ("Hard rules"). Here is the full
description: what is intercepted and how, the boundaries, known holes and
false positives. The text was moved from AGENTS.md as-is (2026-08-23): it
is reference material, not normative, and needs to be read by
whoever changes the hook, not by every agent in every session.

What actually
  happens in Git Bash (verified, not hearsay):
  - `/` is NOT `C:\`. `ls -1 /` shows the root of the Git for Windows
    installation (`bin cmd dev etc mingw64 proc tmp usr` plus installer
    files); `C:\` itself is mounted separately, at `/c` — a sibling path,
    not a nested one. So a bare `find /` (without `-L`) physically never
    reaches `node_modules`, the NuGet cache, Docker layers, or the
    project's `bin`/`obj` — they simply aren't there.
  - What eats hours of CPU time is `/proc` — a synthetic filesystem
    through which Git Bash exposes the Windows registry:
    `/proc/registry`, `/proc/registry32`, `/proc/registry64` are real
    (not symbolic) directories of colossal size; `find /proc/registry
    -maxdepth 2` doesn't even finish the top levels in 8 seconds,
    returning 6000+ entries.
  - `/proc/cygdrive` is a symlink to `/` (`readlink /proc/cygdrive` →
    `/`). With the `-L` flag (follow symlinks), `find -L /` because of
    it isn't just slow — it never finishes at all: it enters
    `/proc/cygdrive`, sees `/` in it again, enters again — in a loop.
  - The real danger to `node_modules`/NuGet/AppData/Docker isn't `/`,
    but `/c`, `/c/Users`, `/c/Users/<anyone>` and `~` (the same as
    `/c/Users/<current user>`): these do physically lead to those
    directories, and that's exactly where all five incident processes
    pointed.
  What is actually intercepted: the PreToolUse hook
  `.claude/hooks/block_wide_fs_search.py` (wired up in
  `.claude/settings.json`, matcher `Bash|PowerShell`) parses the real
  command line — compound commands split on `&&`/`||`/`;`/`|`/`&`/line
  breaks, directory changes via `cd`/`pushd`, quoting, any flag order,
  including flags of the wrappers themselves (`sudo -n`, `sudo -u
  root`, `nice -n 10`, `env -i`, `xargs -I{}` — not just the bare
  wrapper name with no arguments), grouping `(...)`/`{ ...; }`, command
  substitution `$(...)` (recursively, with the same check: `x=$(find /
  -name x)`, `echo $(find / -name x)`), `bash -c '...'`/`sh -c '...'`
  (the string's content is parsed by the same code as an ordinary
  command) — and blocks `find`/`rg`/`grep -r`/`ls -R`/`du`/`tree`/
  `Get-ChildItem -Recurse` (and the aliases `gci`/`dir`/`ls`) if the
  target path, once resolved, is `/`, any bare drive (`/c`, `/d`, ...,
  `C:\`, `D:\`, ... — the Git Bash form and the Windows-native form are
  symmetric, any drive letter, not only `C:`), `/c/Users`/`C:\Users`
  (also for any drive letter), one directory directly under
  `/c/Users`/`C:\Users` specifically (NOT under `/c` or `C:\` in
  general — `find /c/Windows`, `find /usr` do not block the subtree,
  this is a deliberately unclosed part, see "What is NOT intercepted"
  below), or `~`/the whole home directory — including when it's named
  through a variable (`$HOME`, `${HOME}`, `$USERPROFILE`,
  `${USERPROFILE}`, `$env:USERPROFILE`, `%USERPROFILE%`): the hook does
  not execute a shell and does not know the real value of the variable
  in the agent's session, it substitutes its own idea of the home
  directory by textual match, not real variable expansion. A deeper
  subtree (`~/.nuget/packages/...`, `/c/Users/<me>/Workspace/...`) is
  not blocked — this is a deliberate boundary, not an oversight:
  otherwise a legitimate targeted search in the cache or in one's own
  checkout would become impossible; the same holds for `find ...
  -maxdepth 0`/`-maxdepth 1` (doesn't descend — cheap and safe
  regardless of the path, so it isn't blocked at all, even for `/`).
  Verified with an offline command table (`.claude/hooks/tests/`, 94
  cases — 59 BLOCK / 35 ALLOW), without running an actual disk walk;
  `scripts/verify.sh` runs this table automatically if `.claude/hooks/*`
  is among the changed files. `permissions.deny` in the same file is a
  coarse duplicate net (literal prefix comparison, so it doesn't cover
  compound commands and doesn't see everything), not the primary
  mechanism.
  What is NOT intercepted mechanically — remains the agent's
  responsibility:
  - a call from a session that physically started outside the
    directory holding this `.claude/settings.json`: the file is read
    only from the session's starting directory, is not searched up the
    tree, and is not re-read on `cd` mid-session — a risk specifically
    for background subagents that the orchestrator may have launched
    elsewhere;
  - subagents (`.claude/agents/*.md`) have no PowerShell tool at all
    (only `Bash`) — the hook's PowerShell branch is not a hole for
    them (there's nothing to use it with), but for a human's live
    interactive session with PowerShell it is protected only by the
    hook itself, there is no other net for it. The branch itself
    (`tool_name == "PowerShell"`) has not been observed live, in a
    real session — checked only with the offline table using synthetic
    JSON on stdin, like the rest of the hook;
  - wrappers of the form `cmd /c "..."` or `powershell -Command
    "..."`, where the real command is a string within a string in a
    DIFFERENT parsing language (not a POSIX shell); the hook does not
    unwrap it. This is not the same as `bash -c '...'`/`sh -c '...'`
    from the Bash tool — that form IS unwrapped (see above);
  - shell variables and loops whose value isn't visible textually in
    the command itself: `for d in /; do find $d -name x; done` — the
    hook sees the token `$d`, not the fact that the loop will assign
    it `/`; this would require tracing variable values, which the hook
    does not do;
  - backtick substitution (`` `find / -name x` ``) — only `$( ... )`
    is parsed;
  - grouping `(...)` is stripped by a heuristic for one character
    glued without a space on each edge of the subcommand text — deeply
    nested or unbalanced parentheses are outside the guarantee;
  - code that walks the disk not through a shell command but on its
    own (Python/`.NET`/a third-party program) — the hook only sees the
    `tool_input` of Bash and PowerShell calls.
  A known false positive in the other direction (not a hole, but an
  extra block): the hook does not understand heredocs (`<<'EOF' ...
  EOF`) — if text of the form `find / ...` appears inside a heredoc
  body as an EXAMPLE command (not actually executed code, just lines
  being written to a file), the hook will still see it and block the
  file write. Parsing heredocs would require multi-line tracking of
  the body's boundary, which is beyond the "simplified parsing" scope
  of this task.
  This rule is enforced for both vendors, out of one policy file.
  Claude Code wires `block_wide_fs_search.py` through
  `.claude/settings.json` (matcher `Bash|PowerShell`); Codex wires the
  same script through `.codex/hooks.json` (matcher `^Bash$`), on
  Windows through the adapter `.codex/hooks/run-pre-tool-use.ps1`,
  which renames the tool to `PowerShell` before handing the payload
  over — and that whole chain is what
  `scripts/validate-ai-workflow.py` cross-checks, so the two copies
  cannot quietly diverge. A third tool, wired to neither, gets no
  automatic protection and only the text rule: don't start a search at
  `/`, `/c`, `C:\`, `/c/Users`, or the whole home directory, narrow it
  to a specific subtree.
  Where to search instead of a wide search:
  - an assembly from NuGet —
    `~/.nuget/packages/<package-name-lowercase>/<version>/` (inside:
    `lib/<tfm>/`, `ref/<tfm>/`, or `runtimes/.../lib/<tfm>/` depending
    on the package, not always `lib/net10.0`; the cache root is shown
    by `dotnet nuget locals global-packages --list`);
  - a project's build output —
    `src/backend/<Project>/bin/Debug/net10.0/`;
  - repository files in Claude Code — with the `Glob`/`Grep` tools;
  - the path is unknown — narrow it to a subtree (`find src -name
    '*.cs'`, `find ~/.nuget/packages/<package> -maxdepth 2 -name
    '*.dll'`): searching within a subtree is not forbidden, only `/`,
    `/c`, `C:\`, `/c/Users`, and the whole home directory are.


## check_workflow_adapters.py — adapter drift at the edit that caused it

**Event:** `PostToolUse`, matcher `Edit|Write|NotebookEdit`.

The AI workflow is a shared contract with two thin vendor adapters, and
`scripts/validate-ai-workflow.py` is what keeps them in step: agent pairs,
model tiers, the hook chain, the links a stage file is required to carry.
`scripts/verify.sh` runs it, so drift never reaches `main` — but it is caught
on the way out, at the push, long after the edit that caused it and often
after the session has moved on.

This hook runs the same validator at the moment a watched file is written,
and only then: `docs/ai-workflow/`, `.claude/agents/`, `.claude/commands/`,
`.claude/settings.json`, `.claude/skills/`, `.codex/`, `.agents/skills/`.
Nothing outside that list can change the validator's verdict, so running it
there would be pure cost. On a failure the hook exits 2 and puts the
validator's own report on stderr, which `PostToolUse` feeds back into the
session.

**Fail open**, exactly as for `block_wide_fs_search.py`: a payload of an
unexpected shape, an interpreter that is missing, a validator that will not
start — none of it stops the work. This hook is an early signal, not a gate.
The gate is `verify.sh`, and it runs for both vendors.

**Codex has no `PostToolUse` wiring here.** Its hook schema is verified for
`PreToolUse` only; guessing at another event risks breaking live sessions for
the sake of a convenience. For Codex the signal stays `verify.sh` — which is
also why the enforcement was left there rather than moved into the hook.

The wiring is itself checked: `validate-ai-workflow.py` fails if
`.claude/settings.json` loses the `PostToolUse` entry or stops pointing at
this script. Since `.claude/settings.json` is on the watched list, that check
covers its own removal.

Offline table: `.claude/hooks/tests/run_adapter_cases.py` — which paths the
hook reacts to (`is_watched` imported directly: through the exit code a
healthy repository cannot be told from a path that did not match) and
fail-open on a broken payload (end-to-end, as a subprocess).
`scripts/verify.sh` runs it whenever `.claude/hooks/*` or
`.claude/settings.json` is among the changed files.

## permissions.allow — what does not stop to ask

`.claude/settings.json` carries an allow list of the commands this project
runs dozens of times a task: read-only git (`status`, `log`, `diff`, `show`,
`branch`, `rev-parse`, `worktree list`, `stash list`), the verification
scripts (`verify.sh`, `check-gates.sh`, `backlog.sh`,
`validate-ai-workflow.py`, both hook tables), and the build and test commands
of the two stacks (`dotnet build`, `dotnet test`, `npm ci`, `npm test`,
`npm run lint|build|test|format:check`). Each entry matches a command by
prefix; anything else goes through the normal permission flow.

Two boundaries hold this list in place:

- **Nothing in it writes to git.** No `commit`, `merge`, `push`, `reset`,
  `checkout`, `stash push`. The rules about `main`, squash merges and who
  presses Merge are enforced by the `.githooks` and by AGENTS.md, and an
  allow list is not the place to soften them.
- **Nothing in it searches the filesystem.** No `find`, `grep`, `rg`, `ls`,
  `du`, `tree`, `Get-ChildItem`. This is deliberate: the allow list must never
  be able to race the wide-search guard above, and the cleanest way to
  guarantee that is to have no overlap at all between the two lists.

`permissions.deny` is unchanged and still takes precedence.
