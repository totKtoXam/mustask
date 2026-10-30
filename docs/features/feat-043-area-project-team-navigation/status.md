# feat-043-area-project-team-navigation

- type: feature
- size: L
- branch: feat/043-area-project-team-navigation
- stage: 01-business-analysis
- epic: platform/ui
- deps: feat-040,mod-018,mod-020
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved 2026-10-30 (the human, in chat): all recommendations, OQ-1..5, OQ-7..9, H-1..4 A
gate-2: rejected 2026-10-30 (the human, in chat): navigation structure, see Decisions
gate-3: pending

## Plan

<!-- L: see plan-backend.md / plan-frontend.md -->

## Decisions

- Gate-1, 2026-10-30: OQ-1, 2, 3, 4, 5, 7, 8, 9 and H-1, 2, 3, 4 are answered A; the spec is the recommended text. tech-019 is absorbed; a new ADR is owed at the design.

- Gate-2 rejection, 2026-10-30, the human on the navigation structure (their words, translated): (1) the header is a trail of four selectors, each showing «все» or my organization / my department / my team / my project, each choosable; (2) having to drop into «Проекты» and pick a project to reach Общие, Рабочие элементы, Канбан, Бэклог, Команды, Настройки is inconvenient: all of these belong in the left menu as before, its content changing with what is chosen (organization, area, department, project or team); (3) «Настройки» stay where they were. Reopens the spec's context and switcher rules and D-1, D-3; gate-1 answers stand otherwise.

## Log

- 2026-10-30 [orchestrator] task started in the worktree .claude/worktrees/feat-043-area-project-team-navigation-b716b1 (app-made; branch renamed, based on origin/main cc73b2be; new-task.sh not used because the session is already bound here). Route confirmed by the human: feature, L (threshold: breaks ADR-0024 § 2, a new ADR; retired addresses and moved pages), full route, gates 1/2/3 separate; block 3.3: only the queue line. Economy mode; E2E no; no local VERIFY_FULL. Usage at start: weekly 2%, 5-hour 2%.

- 2026-10-30 [orchestrator] queue: PR #41 reworded the lines after mod-018 (merged by the human), PR #42 takes the line (`queue.sh take`). Stage 1 done (business-analyst): spec.md, US-1..US-9, OQ-1..OQ-9. feat-003 and mod-005 are already merged, so BR-NE-8 makes this line the second to merge and US-6 carries their work; US-6 resolves mod-020 CR-2; tech-011 m4 is recorded obsolete on the queue line after the merge.

- 2026-10-30 [orchestrator] Stage 2 (requirements-reviewer): needs-human-decision, 2 blockers, 14 majors, 9 minors. Fix round (business-analyst) closed them in the spec and stories, except R-16 (feat-038 unmerged at stage 1: no dependency) and part of R-25 (a project moved by someone else while the dialog is open). No delta read. Written for the recommended answers of OQ-1..5, 7..9, H-1..4; the 5 s keyword budget (H-4 A) is the analyst's, from no source. Owed after gate-1 if OQ-5 A: tech-019 withdrawn into this line, tech-020 takes feat-043 as a dep.

- 2026-10-30 [orchestrator] Stage 3 (ux-designer): design.md, prototype/index.html (syntax checked only, not opened in a browser), ADR-0032 proposed. Adds kit parts AppTopBar, NavList, SearchMenu. One additive read family: GET /api/organizations/{o}/workitems and …/workitems/assignees. Owed: tech-020 takes feat-043 as a dep (queue); US-9 marks ADR-0024 §§ 1, 2, 4, ADR-0028 rule 7 and ADR-0031 rule 5 with a pointer to ADR-0032; the plan pins statement counts for «Обзор» and the lists and keeps the retired-address and route inventories as one source a test reads.

## Notes

Summary (from the queue line): Top-level «Области», «Проекты» and «Команды»; the object pages with their sections, a section absent until the line that owns it adds it; the organization's «Обзор» as its home page, the user's organizations beside the content (feat-045 folded in); the «Рабочие элементы» list across projects; breadcrumbs with no false hierarchy; the switcher and the remembered context; empty states. A retired address answers «Страница не найдена», nothing redirects, and the interim pages of the earlier lines (the team and area pages in the organization settings, the project's teams, area links and default team in its settings) move onto the objects' own pages (doc-012 BR-MX-27, -28; US-10)
