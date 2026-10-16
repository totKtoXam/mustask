---
description: Run QA separately (stage 5 or 7) for a feature
argument-hint: <feature id> <backend|frontend|both>
---

Arguments: $ARGUMENTS

Run the **qa-engineer** subagent for the given feature and side (backend,
frontend, or both — the single QA pass of an `M` with both layers,
`docs/ai-workflow/stages/05-frontend.md`, § 7). On completion, show the
verdict and the list of defects from the QA report.
