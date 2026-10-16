# Stage 3 — UX/UI design → Human Gate 2

**Role:** ux-designer · **Model tier:** standard
**Input:** `docs/features/<id>/spec.md` (the index; `gate-1: approved`, or on
`M` reviewed at stage 2 with `gate-1: pending` — see "Human Gate 2") + the
stories it lists under `docs/features/<id>/stories/`,
`docs/conventions/frontend.md`
**Output:** `docs/features/<id>/design.md` + HTML prototype (optional)

## Goal

Design the screens and interaction in terms of MUI components so that the
frontend developer assembles them instead of inventing them.

## Process

1. Screen map: which screens/pages are needed, navigation between them,
   which story is closed by which screen.
2. For each screen, in `design.md`:
   - the layout in words + an ASCII/Mermaid diagram of block placement;
   - the **MUI components used, by name** (DataGrid, Autocomplete,
     Dialog…) — do not invent custom ones where a standard one exists;
   - every state: loading (skeleton), empty, error, success, permissions
     (what each role sees);
   - form validations and error texts (RU);
   - behavior on mobile/narrow screens, where relevant.
3. Optional (mandatory for L features): a clickable HTML prototype of the
   key screen — one self-contained html file in
   `docs/features/<id>/prototype/`, for the human to look at before
   development.
4. Interface texts (labels, buttons, empty states) — go straight into
   design.md, they are part of the design, not "we'll figure it out later."

## Rules

- Design system = MUI defaults + the project theme. No pixel-perfect
  specifications — we fix the structure, the components and the states.
- Reuse: before designing, look at the `design.md` of neighboring features —
  identical tasks are solved with identical patterns.
- **`design.md` has a size budget** — 32 KB
  ([../../features/README.md](../../features/README.md), "Artifact
  budgets"). A design heading past it is two tasks: return a split proposal
  to the orchestrator — which screens make this cut, and what goes to the
  queue. Theme or app-wide changes the screens need are their own task, not a
  chapter of this design.

## Human Gate 2

The orchestrator shows the human `design.md` (+ prototype) and a summary.
The human, in `status.md`:

```
gate-2: approved | rejected: <what to redo>
```

On `M` this is one package with `gate-1`
([../routing.md](../routing.md), "Route shortcuts for `M`"): the human gets
the spec summary with its open questions and the design together, and gives
two verdicts in one answer. A `gate-1: rejected` there sends the spec back to
stage 1, and the design is redone against the corrected spec before the
package is shown again.
