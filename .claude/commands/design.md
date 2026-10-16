---
description: Stage 3 — UX/UI design + prepare gate-2
argument-hint: <feature id>
---

Feature: $ARGUMENTS

1. Check `docs/features/$ARGUMENTS/status.md`: `gate-1: approved`, or — on
   `size: M` — stage 2 done (a requirements-review verdict recorded) with
   `gate-1: pending`, the M package
   (`docs/ai-workflow/stages/03-ux-design.md`, "Human Gate 2"). Anything
   else — stop and report.
2. Run the **ux-designer** subagent.
3. Show the human design.md (and the prototype, if any) and ask for a
   verdict on gate-2 and record it in status.md. On `M` with gate-1 still
   pending, the request is the package: the spec summary with its open
   questions plus the design, one verdict for each gate.
