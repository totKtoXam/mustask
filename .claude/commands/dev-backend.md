---
description: Stages 4–5 — backend development and backend QA
argument-hint: <feature id>
---

Feature: $ARGUMENTS

1. Check the gates in status.md (feature tasks need `gate-2: approved` or a
   note that design is not required).
2. Run the **backend-developer** subagent (stage 4: plan → contract →
   code).
3. Then the **qa-engineer** subagent with "backend" specified (stage 5) —
   as a separate call, clean context. **Skip this step on `size: M` with a
   frontend:** its QA is a single pass over both layers, run by
   `/dev-frontend` after stage 6 (`docs/ai-workflow/stages/04-backend.md`,
   § 5). Report that backend QA is deferred to it.
4. When step 3 ran — blocker defects → send back to backend-developer; at most 2 cycles,
   then escalate to the human. On completion, report the outcome and the
   path to qa.md (section Backend).
