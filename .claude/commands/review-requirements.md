---
description: Stage 2 — adversarial review of spec.md + prepare gate-1
argument-hint: <feature id>
---

Feature: $ARGUMENTS

1. Run the **requirements-reviewer** subagent for
   `docs/features/$ARGUMENTS/`.
2. When it finishes — show the human its summary and open questions, ask
   for a verdict on gate-1 and record it in status.md. On `size: M` with a
   UI, do not ask: gate-1 stays `pending` and goes to the human together with
   gate-2 after stage 3 (`docs/ai-workflow/stages/02-requirements-review.md`,
   "Human Gate 1"); report the summary and move on to `/design`.
