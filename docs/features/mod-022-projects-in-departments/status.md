# mod-022-projects-in-departments

- type: module
- size: L
- branch: mod/022-projects-in-departments
- stage: 4-backend
- epic: core/org-access
- deps: mod-021
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-3, -4,
-6, -19 steps 2-3, -20 for projects; stories US-5 and US-2; `design.md`;
`docs/adr/0040-*.md`). Their gates are not repeated here.

Stages: backend (plan in `plan-backend.md`, code, tests, `api-contract.md`)
→ backend QA → frontend («Расположение» and the move dialog without
«Верхний уровень», the move refused while linked, team–project link offers
inside one department) → frontend QA → code review (one Opus pass with the
security pass) → gate-3 → PR on GitHub with auto-merge (merge commit),
branch kept.

The upgrade (BR-TD-19 steps 2-3) is irreversible over populated tables: it
is idempotent, logs per organization what it placed and cut (counts and
names), and is tested on a populated fixture as mod-021's was. The release
that carries it is tagged by the human.

Out of scope: department managers running teams (mod-023), scoped team
links at project creation and the shared-board card flag (feat-054), the
cascading selectors (feat-063).

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-18 [orchestrator] task started on branch mod/022-projects-in-departments in worktree .claude/worktrees/kanban-test-case-2c0b58 (host DESKTOP-2S17CA7, git rev-parse --show-toplevel and git branch --show-current confirmed), cut from origin/main 731260bc.
- 2026-11-18 [orchestrator] queue line taken by scripts/queue.sh take mod-022, PR #220 with squash auto-merge.
- 2026-11-18 [orchestrator] route confirmed by the orchestrator session (coordination delegated by the human, 2026-11-18): L from stage 4 (the queue line said M; L thresholds fired: an irreversible migration cutting project–team links over populated tables, and a breaking API change — a project with no department is refused, BR-TD-6); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only. 3.1: no regulatory basis. 3.3: nothing beyond the line. The upgrade must be idempotent, log its cuts per organization, and be tested on a populated fixture.
