# tech-001-naming-rule-plan-amendment

- type: tech
- size: S
- branch: tech/001-naming-rule-plan-amendment
- stage: complete
- epic: workflow
- deps: doc-010
- gates: gate-3

## Gates

gate-3: approved · delegated by human 2026-10-01 · merged 2026-10-01 (local squash, no remote)

## Plan

Docs only, light cycle (D-2). Five files, no runtime code:

1. `AGENTS.md`: a hard rule after "Language" — the tracker doc-001..007
   studied is never named in anything written from 2026-09-25 on; say «the
   reference tracker», cite the analyses by rule id; the merged analyses keep
   their wording (D-1). The same file's § Project sentence drops the name.
2. doc-010 `status.md`: D-25 (mod-002 split → mod-016, parallel start, the
   consumer deps), D-26 (the naming rule, no sweep), D-27 (mod-015 app shell
   line, feat-014 title); `spec.md`: § Releases (MS-1 lines), § Normalized
   queue (rows and totals), § Critical path (graph, paths, waves, closing
   paragraph), § ADR debt and § Permission codes (ownership → mod-016), PR-1;
   `stories/US-1` delivering lines.
3. doc-006 `spec.md`: § Decomposition rows for mod-002 (structure) and
   mod-016 (access), the priority paragraph, ownership statements of the
   ADR-0018 amendment and the codes; a `[tech-001] amended` Log line in its
   `status.md`.
4. Check: `scripts/verify.sh` green; a grep of the edited files shows the
   reference product only on lines this task did not touch.

## Decisions

- D-1 (human 2026-09-25, form) Existing mentions of the reference product
  (162 in 35 files) are not swept; only the rule is added. One exception by
  the orchestrator: the § Project sentence of `AGENTS.md`, the file that
  carries the rule, drops the name — reversible at gate-3.
- D-2 (human 2026-09-28, form) Route: S, light cycle, in the orchestrator's
  session. Standing approval granted for the class "docs-only S tasks on the
  workflow rules and the product plan" (AGENTS.md § Standing approval; this
  is the first task of the series). `AGENTS.md` sits at the repository root,
  outside the paths the light cycle names (`docs/`, `.claude/`, `scripts/`,
  hooks); the human accepted it as the rules file the cycle exists for.
- D-3 The queue changes this task records were made in `main` by queue-only
  commits before it started (d5e1473, 87dcc5d, d00b1ac, 373b24c, 3eb20e1);
  the task amends the plan and doc-006 to match, it does not touch the queue.

## Log

- 2026-10-01 [orchestrator] gate-3: approved under the human's delegation of 2026-10-01 (the human told the orchestrator in chat to decide the gates and merge the series itself: «делай всё сам, продолжай»); the § Project naming deviation kept; local squash via scripts/finish-task.sh
- 2026-09-28 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-001-naming-rule-plan-amendment
- 2026-09-28 [orchestrator] route confirmed by the human in chat (form): tech, S, light cycle → gate-3 → local squash; gate-1/gate-2 inapplicable (no spec, no screen); dep doc-010 merged (2956f72); queue line taken (0a1b39f)
- 2026-09-28 [orchestrator] standing approval recorded (D-2): docs-only S tasks on the workflow rules and the product plan
- 2026-09-28 [task-executor] edits applied per § Plan: AGENTS.md, doc-010 status.md (93 lines) + spec.md + stories/US-1, doc-006 spec.md + status.md; 6 files, +76/−52; `sh scripts/verify.sh` → RESULT: green, exit 0; grep of the edited files: every hit of the reference product is on an untouched line
- 2026-09-28 [orchestrator] diff reviewed; `sh scripts/verify.sh` re-run after status.md: RESULT: green, exit 0; gate-3 requested from the human
- 2026-10-01 [orchestrator via task-executor] pre-gate-3 amendments: PR-1 per mod-014 D-6; § ADR debt names mod-015's shell ADR; mod-NNN → mod-014; D-27/MS-1 per D-10 (mod-015 gates mod-002's frontend); § Project naming deviation accepted

## Notes

Summary (from the queue line or an argument): Docs only. AGENTS.md hard rule: the product studied in doc-001..007 is never named in any artifact, code, identifier, comment, commit or UI copy (say «the reference tracker»); doc-010 amended for the mod-002 split (a decision in its status.md; mod-016 owns the ADR-0018 amendment and the membership codes; the waves, the critical path and the ADR-debt table updated; mod-003 lists mod-014) and doc-006's decomposition rows for mod-002 and mod-016; no sweep of the existing mentions (human decision 2026-09-25)
