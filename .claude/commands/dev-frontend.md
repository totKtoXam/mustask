---
description: Stages 6–7 — frontend development and frontend QA
argument-hint: <feature id>
---

Feature: $ARGUMENTS

1. Check that `api-contract.md` exists (frontend can run in parallel with
   backend QA, but not before the contract is fixed).
2. Run the **frontend-developer** subagent (stage 6).
3. Then the **qa-engineer** subagent with "frontend" specified (stage 7) —
   as a separate call, clean context. On `size: M` with a backend, specify
   "backend + frontend" instead: this is the task's single QA pass and it
   writes both sections of qa.md (`docs/ai-workflow/stages/05-frontend.md`,
   § 7).
4. Blocker defects → send back to the developer who owns them
   (frontend-developer, or backend-developer for a Backend-section defect of
   the M pass); at most 2 cycles, then escalate to the human. On completion,
   report the outcome and the path to qa.md (section Frontend, plus Backend
   on M).
