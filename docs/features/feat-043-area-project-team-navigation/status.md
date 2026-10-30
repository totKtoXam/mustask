# feat-043-area-project-team-navigation

- type: feature
- size: L
- branch: feat/043-area-project-team-navigation
- stage: 01-business-analysis
- epic: platform/ui
- deps: feat-040,mod-018,mod-020
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-10-30 [orchestrator] task started in the worktree .claude/worktrees/feat-043-area-project-team-navigation-b716b1 (app-made; branch renamed, based on origin/main cc73b2be; new-task.sh not used because the session is already bound here). Route confirmed by the human: feature, L (threshold: breaks ADR-0024 § 2, a new ADR; retired addresses and moved pages), full route, gates 1/2/3 separate; block 3.3: only the queue line. Economy mode; E2E no; no local VERIFY_FULL. Usage at start: weekly 2%, 5-hour 2%.

## Notes

Summary (from the queue line): Top-level «Области», «Проекты» and «Команды»; the object pages with their sections, a section absent until the line that owns it adds it; the organization's «Обзор» as its home page, the user's organizations beside the content (feat-045 folded in); the «Рабочие элементы» list across projects; breadcrumbs with no false hierarchy; the switcher and the remembered context; empty states. A retired address answers «Страница не найдена», nothing redirects, and the interim pages of the earlier lines (the team and area pages in the organization settings, the project's teams, area links and default team in its settings) move onto the objects' own pages (doc-012 BR-MX-27, -28; US-10)
