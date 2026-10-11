# tech-023-artifact-size-budgets — review

## Round 1

Verdict: **changes-requested** — 1 major, 2 minor. Diff `main...919e096`, 6 files.
Plan items 1–5 conform; nothing extra. `bash scripts/verify.sh`: green.

### Findings

**R1-1 · major · `docs/templates/examples/README.md:3-5,20-22`** (also `docs/features/README.md:11-13`)
The examples are presented as "what the artifacts look like when they are filled in", read "for the shape of
the work" — but their `review.md` is 105 KB (tech-159) and 55 KB (tech-172) and `qa.md` 61 KB (tech-159),
2–3× the file budgets this diff sets. An agent modelling its artifact on them writes past the budget.
Fix: one sentence in examples/README.md — their `review.md` and `qa.md` predate the artifact budgets
(tech-023) and exceed them; copy the structure, not the length; link `docs/features/README.md`, "Artifact budgets".

**R1-2 · minor · `scripts/verify.sh:244`**
The note tells every over-budget file to "write the next section shorter". For `design.md` the remedy stated in
`03-ux-design.md:38-43` and `docs/features/README.md:183` is a split proposal, and README:188-190 says an
approved in-flight design is not rewritten. Fix: neutral text, e.g. "over the 32 KB artifact budget
(docs/features/README.md, \"Artifact budgets\"):$big".

**R1-3 · minor · `docs/features/README.md:189,194`**
Two numbers do not reproduce. `review.md` average in `main` is 53.4 KiB (54.7 kB), not 54 (design 72.0 and
qa 44.3 KiB match). In-flight designs are 75–212 KiB, not 74–212: feat-029 76 784 B, mod-016 87 718 B,
tech-015 217 375 B (branch heads). Fix: "53 KB", "75–212 KB".

### Claims checked (direction 1)

- README:183-185 budgets table → `03-ux-design.md:38`, `06-code-review.md:72`, `04-backend.md:179` (instruction + role assignment: ux-designer.md, code-reviewer.md, qa-engineer.md — weakest anchor).
- README:187-188 "verify.sh names a changed … of a task in flight past 32 KB, a note" → `scripts/verify.sh:228-245`; re-run as an extracted block under `set -eu` in Git Bash 5.2 (sh and bash): 39 KB in-flight file noted, `stage: complete` folder skipped, missing file skipped, small file last in the loop — exit 0.
- README:191 "per-round/per-run numbers are not the script's" → verify.sh checks file size only (:241). OK.
- README:189 "74–212 KB in flight" → R1-3.
- README:193-196 averages and feat-027 77/154/28 KB → feat-027 reproduces (78 825 / 157 462 / 28 767 B); averages → R1-3.
- 03:40-43 "return a split proposal" → instruction anchor (ux-designer.md → 03); a spec cut after gate-1 is covered by README "substantive change … resets" rule. OK.
- 04:181 "applies to the Frontend section (05 § 7)" → 05-frontend.md:112-113 points 05 QA at 04 "Run economy", where the budget sits; qa-engineer.md names "Run economy". OK (prose chain, instruction anchor).
- 04:182-185 "the JSON report already holds those [passing tests]" → 05-frontend.md:53-55, Playwright JSON reporter. OK.
- 06:72-77 one line a claim, finding in a few lines → consistent with 06 "one line per claim checked". OK.

### Direction 2

Behaviours: artifact size budgets for design/review/qa.md; E2E run recorded as totals + failures + stand record;
verify.sh note. Grepped "every test|each test|row per|all tests|list.*tests", "budget|32 KB|16 KB|proposal|split",
"verify.sh checks", "templates/examples" across `docs/ai-workflow`, `.claude/agents`, `.claude/commands`,
`docs/templates`, `docs/features/README.md`, `src/frontend/AGENTS.md`. Read: 03 Process/Rules, ux-designer.md,
design-spec.md, qa-engineer.md, test-plan.md (matrix per criterion, runs table — no per-test rows), 05 § 7,
04 Run economy, `src/frontend/AGENTS.md` E2E record section, code-reviewer.md, 06:208 verify.sh list (non-exhaustive,
fine). Hit: the examples folder → R1-1. Not swept: other tasks' existing artifacts (archives, out of scope).

## Round 2 (delta)

Verdict: **approve** — 0 open. Read `cb8057a`, `0b7f1da` against R1-1..R1-3.

- R1-1 fixed → `docs/templates/examples/README.md:25-29`, "Their length is not the model", linking "Artifact budgets". The sizes check out (107 356 / 62 017 / 56 328 B). "Two to three times" is loose (1.7–3.3× the 32 KB file budget), but no agent would act differently because of it, so it is not a finding.
- R1-2 fixed → `scripts/verify.sh:244` now names no remedy and points at "Artifact budgets". The escaped quotes print whole (I ran the line with `big` set), and `sh -n` passes.
- R1-3 fixed → `docs/features/README.md:189` 75–212 KB, `:194` 53 KB.
- New contradictions: none. README:11-13 still points at the examples; the paragraph added beside them is the fix.
