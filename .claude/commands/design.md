---
description: Stage 3 — UX/UI design + prepare gate-2
argument-hint: <feature id>
---

Feature: $ARGUMENTS

1. Check `docs/features/$ARGUMENTS/status.md`: `gate-1: approved`, or — on
   `size: M` — the M package (`docs/ai-workflow/stages/03-ux-design.md`,
   "Human Gate 2"): a requirements-review verdict recorded with
   `gate-1: pending`, or with `gate-1: rejected: …` when that verdict was
   recorded after the rejection (the rework round). Anything else — stop and
   report.
2. Run the **ux-designer** subagent.
3. Show the human design.md (and the prototype, if any) and ask for a
   verdict on gate-2 and record it in status.md. On `M` with gate-1 not yet
   approved, the request is the package: the spec summary with its open
   questions plus the design, one verdict for each gate.
