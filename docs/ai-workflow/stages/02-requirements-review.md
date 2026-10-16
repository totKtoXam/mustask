# Stage 2 — Requirements review → Human Gate 1

**Role:** requirements-reviewer (clean context, not the spec author!) · **Model tier:** deep
**Input:** `docs/features/<id>/spec.md` (the index) and every file under
`docs/features/<id>/stories/` — the reviewer is the one role whose job is the
whole picture, not one story's `depends-on` slice of it
**Output:** findings in `review.md`, verdict in `status.md`

## Goal

Find the holes in the specification before they become code. The reviewer
is adversarial by design: the job is to break the spec, not to praise it.

## Review checklist

1. **Completeness:** every known business rule is either covered by a story
   or explicitly in out of scope. Build a rule → story mapping table.
2. **Verifiability:** every criterion can be turned into a test. Found one
   that cannot — rewrite it more concretely and flag it as a finding.
3. **Edge cases:** empty lists, concurrent access, permissions for every
   BPMN step, an integration failure mid-process, resubmission.
4. **Contradictions** between stories, and with already-approved specs of
   other features.
5. **Size:** the backlog tasks are really ≤ M.

## Where the findings go

Findings go into `docs/features/<id>/review.md`, one entry each:

```markdown
## Round 1 — requirements

### R-1
Severity: blocker | major | minor
Affects: US-3, BR-8
Problem: <what is wrong and why it will cost something later>
Required resolution: <what has to change in the spec>
Status: open | resolved | accepted-risk
Resolution: <what actually changed — BR-8 reworded, US-3 split>
```

`spec.md` and the files under `stories/` are the normative state of the
requirements and nothing else. The reviewer edits them to fix a finding, and
records the finding in `review.md` — never a findings section, a verdict or a
discussion inside the spec or a story. A second round appends `## Round 2` to
the same file.

Round 2 is a delta check, not a repeat investigation: the open findings, the
sections that changed since round 1, and `git diff` on `spec.md` and
`stories/`. Re-reading every normative reference from scratch buys nothing and
costs a full context.

## Verdict

The verdict goes in `status.md`, the reasoning stays in `review.md`:

- `approved-by-ai` — no findings, or all of them fixed (the reviewer makes
  small edits directly, returns large ones to the business-analyst, at most
  2 iterations);
- `needs-human-decision` — there are questions only the human can decide.

## Human Gate 1

After the verdict, the orchestrator writes the human a summary: 5–10
lines — what the feature does, the key decisions, the list of open
questions. The human, in `status.md`:

```
gate-1: approved | rejected: <reason / answers to the questions>
```

Stage 3 continues — only after `gate-1: approved`. **Except on `M` with a
UI** ([../routing.md](../routing.md), "Route shortcuts for `M`"): there the
verdict is not asked here — `gate-1` stays `pending`, stage 3 starts on the
reviewed spec (verdict `approved-by-ai`, or `needs-human-decision` with the
open questions carried into the package), and the human decides `gate-1` and
`gate-2` together after stage 3. An `M` with no UI has no stage 3 to package
with, so its `gate-1` is asked here as usual.
