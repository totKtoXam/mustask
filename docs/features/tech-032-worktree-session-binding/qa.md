# QA — tech-032-worktree-session-binding

## Workflow tooling

### Coverage matrix

| Plan criterion | Check | Test type | Status |
|---|---|---|---|
| Actual agent session is bound and confirmed after `new-task` before work or delegation | `AGENTS.md`, `feature.md`, `new-task.sh`, and generated-prompt source require session binding plus host/project root, toplevel, and branch confirmation before work | static | fail — D-1 conflicts with the detached reading-role route |
| Native relocation is used only when supported and confirmed | Shared policy limits relocation to current-session support with confirmation | static | pass |
| Unsupported relocation stops for an interactive, human-visible transfer | Shared policy and handoff prompt require the original session to stop and the human to start an interactive replacement; unattended replacements are prohibited | static | fail — D-2 strands mandatory queue removal |
| Started checkout, branch, dirty and ignored files are preserved | Started-task path names the existing worktree and expressly forbids `new-task`, `git switch`, removal, or recreation | static | pass |
| No unsupported automatic relocation is claimed | Vendor guidance distinguishes a new CLI command directory from relocating the current desktop session | static | pass |

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Handoff resolves each supported short-id state | It emits a usable human-delivered interactive path and does not remove/recreate a started worktree | pass — four cases green in the proportional verifier |
| 2 | Vendor CLI is absent | Guidance reports the prerequisite and does not attempt installation or issue a known-broken command | pass — inspected `command -v` branch; it only explains installation |
| 3 | Session binding cannot be verified through a local shell directory change | The policy explicitly rejects `cd`, `git -C`, and command-level working-directory evidence | pass |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-10-15 | `scripts/verify.ps1` (implementation evidence recorded in `status.md`) | pass, exit 0: proportional docs/workflow/tooling checks and four `handoff.sh` short-id cases |
| 2026-10-15 | `git diff --check origin/main`; independent diff and prompt-source inspection | pass; no whitespace errors or policy contradiction found |

### Defects

#### D-1: Session-binding proof excludes the required detached QA/review checkout

- Severity: major
- Steps:
  1. Start QA or code review while the implementation session owns the task
     branch.
  2. Follow `docs/conventions/git.md`, "Reading roles do not hold the
     branch", and check the task branch out detached.
  3. Apply the new binding rule, which requires `git branch --show-current`
     to identify the task branch.
- Expected / Actual: The detached reading-role route must remain valid. In a
  detached checkout `git branch --show-current` is empty, so the new rule
  rejects that prescribed QA/review configuration.
- Status: open

#### D-2: Fallback handoff after `new-task` leaves the queue line untaken

- Severity: major
- Steps:
  1. Run `scripts/new-task.sh` for a queued task.
  2. Native relocation is unavailable, so follow its instruction to prepare
     an interactive handoff and stop the original session.
  3. Generate the started-task prompt with `scripts/handoff.sh <short-id>`.
  4. Follow the replacement prompt.
- Expected / Actual: The replacement must complete the required separate
  queue removal on `main`. The started-task prompt forbids
  `scripts/queue.sh take`, while the original session stops before the
  workflow contract's later queue-take step. The queue line can remain
  pending indefinitely.
- Status: open

### Limitations

- No actual vendor desktop-session relocation was attempted. This candidate is
  a policy and prompt change; it correctly does not claim this QA session was
  moved.
- No application stand, browser, or Playwright work was started. The E2E
  decision is pending the human response already requested by the
  orchestrator; no answer is inferred here.

### Verdict

blockers: 0 · majors: 2 · result: fail
