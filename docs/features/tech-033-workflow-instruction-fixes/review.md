# Review — tech-033-workflow-instruction-fixes

## Round 1

Scope: `git diff origin/main...HEAD` at `d3e897fb` (16 instruction files +
status.md, no runtime code). `bash scripts/verify.sh` — green.
Verdict: **changes-requested** (1 major, 2 minor).

### Findings

**F1 — major. The M package's `gate-1: rejected` loop dead-ends at `/design`.**
`docs/ai-workflow/stages/03-ux-design.md:59-61` says a `gate-1: rejected` in
the package sends the spec back to stage 1 and the design is redone before the
package is shown again. But on that rework `status.md` holds
`gate-1: rejected: <reason>` (`docs/features/README.md:116`; only the human sets
it), and every M-path precondition in the same diff accepts only `approved` or
`pending`: `.claude/commands/design.md:8-12` ("Anything else — stop and
report"), `.claude/commands/review-requirements.md:11-14` and
`docs/ai-workflow/stages/02-requirements-review.md:73-75` ("gate-1 stays
`pending`"), `.codex/agents/ux-designer.toml:7` ("gate-1 still pending").
An orchestrator following them stops after the re-review instead of redoing
the design — a sibling file contradicting the diff, so major.
Fix: name the reopened state. In 02 / `review-requirements.md`: "gate-1 is
left as it is — `pending`, or the package's `rejected` while the spec is
reworked". In `design.md` step 1 and the Codex ux-designer: on `M`, accept
`gate-1: pending` or `gate-1: rejected` with a stage-2 verdict recorded after
the rejection; step 3 then re-asks both gates (a `gate-2` given in the
rejected package is re-asked too, since the design is redone).

**F2 — minor. `/dev-backend` step 4 still reports a Backend QA section on M.**
`.claude/commands/dev-backend.md:17-19`: when step 3 is skipped (M with a
frontend) there is no QA, no defect loop and no `qa.md` Backend section to
point at. Step 3's "report that backend QA is deferred" makes the intent
clear, so nobody acts wrongly. Fix: prefix step 4 with "When step 3 ran —".

**F3 — minor (direction 2). Untouched prose still describes stage 5 as always
separate.** `docs/templates/test-plan.md:3-5` ("Backend" filled by stage 5 QA,
"Frontend" by stage 7) and `docs/ai-workflow/dispatch.md:95-97` (backend QA in
parallel with stage 6). On M both sections come from the single stage-7 pass
and there is no stage 5 to parallelise. No agent does the wrong thing (05 § 7
says who writes both sections). Fix: add "(on `M` with both layers, both by
the single pass — 05-frontend.md § 7)" to each.

### Behavioural claims checked

- `.claude/commands/feature.md:8-12`, `.agents/skills/feature/SKILL.md:10-14` (README/routing read only for a new task or a route question) → `docs/ai-workflow/feature.md:22-29`; weakest anchor: instruction read by the orchestrator these adapters launch.
- `docs/ai-workflow/feature.md:104` (tiers in dispatch.md) → `docs/ai-workflow/dispatch.md:38` table; `routing.md:313-316` now only points there.
- `routing.md:244-247` (gate-1 pending until the package; stage 3 from the reviewed spec; M without UI asked alone) → `02-requirements-review.md:72-78`, enforced by `.claude/commands/review-requirements.md:11-14` + `.claude/commands/design.md:8-12` (instruction anchor). Rejection branch → F1.
- `02-requirements-review.md:72-78` (verdict not asked on M with UI; `needs-human-decision` questions carried) → `review-requirements.md:11-14`, `design.md:15-17`. M without UI → `review-requirements.md:10-11` asks as before.
- `03-ux-design.md:4-6` (input may be gate-1 pending on M) → `design.md:8-12`, `.codex/agents/ux-designer.toml:7`; `.claude/agents/ux-designer.md` defers to the stage file.
- `03-ux-design.md:56-61` (package, two verdicts; rejected → stage 1 and redo) → `design.md:15-17` for the package; the redo → F1.
- `04-backend.md:126-129` (stage 5 not run alone on M with a frontend; is the single pass without one) → `.claude/commands/dev-backend.md:13-16`.
- `05-frontend.md:38-42` (single pass writes both sections, defects to owner) → `.claude/commands/dev-frontend.md:11-20`, `.claude/commands/qa.md:9-11` (`both`), `.claude/agents/qa-engineer.md:11-13`, `.codex/agents/qa-engineer.toml:7`.
- M frontend-only → stage 7 as usual (`dev-frontend.md:12` "with a backend"); M backend-only → `04-backend.md:128-129`; E2E question then at stage 5 (`04-backend.md` § 5 step 4). Consistent.
- `.codex/agents/qa-engineer.toml:7` E2E (question kept; agent may run cold stand after yes; JSON report + stand.json into qa.md) → `05-frontend.md` § 7 step 3 (lines 47-61 of the file), `docs/adr/0023-stand-only-keycloak-client-impersonates-the-e2e-fixture-user.md`, `src/frontend/AGENTS.md:59`; matches `.claude/agents/qa-engineer.md:22-32`.

Direction 2:
- M gate timing — grepped `gate-1`, `approved spec`, `rejected` in `docs/ai-workflow/`, `docs/features/README.md`, `.claude/`, `.codex/`, `.agents/`: `feature.md:84` (generic order, qualified by `:94`), `routing.md:247` (L), `dispatch.md:38`, `01-business-analysis.md:3`, `features/README.md:116` — no stale M text; the README gate values feed F1.
- M single QA — grepped `stage 5|backend QA|single pass|QA pass|package` in ai-workflow README/dispatch/handoff/00/s-route, `docs/templates/`, all agents: `dispatch.md:95`, `test-plan.md:3` → F3; rest none.
- Adapter reading — `AGENTS.md` "read it before your first task" is compatible (first task = new task). None.
- Not swept: existing `docs/features/*/` artifacts and `docs/templates/examples/` (historical records, not instructions).

Note for the orchestrator (not a finding): this task's `status.md` still says `stage: 04-backend`.
