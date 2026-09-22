---
name: feature
description: Drive MUSTASK tasks through the project AI workflow — F/S/M/L classification, feature artifacts, separate roles, human gates, QA, review and a merge request. Use for a new or existing development task, a fix, technical work or analysis in this repository.
---

# Feature

Use the user's current request as the task.

Read `docs/ai-workflow/README.md`, `docs/ai-workflow/routing.md` and the
shared contract `docs/ai-workflow/feature.md`, then execute the contract.

When the contract calls for a separate role, use the project custom agent
of the same name from `.codex/agents/` or a separate bounded subagent in a
fresh context. Pass only the task id, the current stage, the needed paths
and the allowed write zone. Pick the model tier `deep/standard/fast` per
`docs/ai-workflow/dispatch.md`; do not run QA and review in the context of the author of the
artifact under review.

Work that is not a stage — edits, checks, exploration — goes to the
`task-executor` agent rather than into this context.
`docs/ai-workflow/stages/00-orchestrator.md` draws that boundary, and says
when a run is too small for delegating to pay.

At a task boundary, decide whether the next task belongs in this session at
all: `docs/ai-workflow/handoff.md` has the table and
`scripts/handoff.sh <short-id>` writes the prompt for the cases that hand
over. Show the human that prompt and stop — do not start the session
yourself.
