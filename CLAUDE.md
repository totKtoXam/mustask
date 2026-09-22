@AGENTS.md

## Claude Code

- Feature orchestration: the `/feature <task description>` command — it
  classifies the task and drives it through the pipeline from
  docs/ai-workflow/README.md.
- Stages on their own: /analyze, /review-requirements, /design, /dev-backend,
  /dev-frontend, /qa, /ship.
- The subagents in .claude/agents/ are already bound to stages and model
  tiers — use them through the Task tool, do not run review or QA in the main
  context.
