# feat-041-project-visibility

- type: feature
- size: L
- branch: feat/041-project-visibility
- stage: 5-backend-qa
- epic: core/org-access
- deps: mod-019,mod-021
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-12..-16,
OQ-5 A; story US-6, its core; `design.md`; `docs/adr/0040-*.md`). Their gates
are not repeated here.

Scope (US-6 core): the sight rule of BR-TD-12..-16 in the evaluator's three
forms — a reaching grant, membership of a linked team, or the project's
«Видим всей организации» — with the team and department gates (BR-TD-13,
-14), the flag set in the project's «Настройки» under
`organizations.visibility.write`, its upgrade value off for every existing
project with the per-project log of who lost sight (OQ-5 A), the answer order
(«не найден» before 403; mod-016 api-contract § 0.1 rows 2-4, § 3.4;
BR-M1-26; RetrofitAccessTests restated), and the assignee candidates (those who
see the project). This rewords mod-008's premise that results are readable
across the organization.

Out of scope: applying the rule on lists, selectors, «Структура», «Обзор»,
pickers (feat-061) and on the multi-project views, search, counts and the
sprint clauses owed to mod-007, feat-057, feat-058 (feat-062).

Stages: backend (plan-backend.md, code, tests, api-contract.md) → backend QA
→ frontend («Видим всей организации» in the project's «Настройки», the
assignee picker, «не найден» answers) → frontend QA → code review (one Opus
pass with the security pass) → gate-3 → PR on GitHub, merge commit, auto-merge
asked for (tech-072), branch kept.

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-16 [orchestrator] task started on branch feat/041-project-visibility in worktree .claude/worktrees/workflow-debts-c5f842 (host DESKTOP-2S17CA7, git rev-parse --show-toplevel and git branch --show-current confirmed), cut from origin/main 66727b0e. The worktree's stale core.hooksPath override (doc-012 worktree) unset.
- 2026-11-16 [orchestrator] route confirmed by the human in chat: L from stage 4 (the queue line said M; the L threshold "authorization rules change" fires); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only. Migration additive (a project column, off by default).
- 2026-11-16 [orchestrator] queue line taken by scripts/queue.sh take feat-041, PR #198 with squash auto-merge.
- 2026-11-16 [backend] stage 4 done in f8d82975 (cbdc92f7..f8d82975): plan-backend.md, api-contract.md, the sight rule in the evaluator (set form, new inverse form `UsersSeeingProjectAsync`), the sight gate in `OrganizationPermissionHandler` (404 project/team/area before 403; query `teamId`/`departmentId`/`projectId` only for existing hidden objects), `organizations.visibility.write` + `PUT /projects/{id}/visibility`, `visibleToOrganization` on `GET /projects/{id}`, migration `20261116104040_ProjectVisibleToOrganization` (column default false + `project_sight_upgrade_log` and a notice per project, in the migration, not a startup step), assignee candidates and a newly set assignee limited to those who see the project (409 `assignee_unavailable`). Contract amendments: mod-016 § 0.1 rows 2-4 and § 3.4, noted in mod-016's status.md and in api-contract § 1/§ 3; `contracts/access/role-bundles.json` regenerated. Tests: Api.IntegrationTests green over the full run (10 new scenarios in ProjectSightEndpointTests; about 25 legacy suites restated: unknown/hidden objects answer 404 before 403, fixture projects built visible via `OrganizationsTestData`/`SetVisibleToOrganization(true)`, readers given a Team «Читатель» grant where a team is linked to nothing), Domain 1045, Application 883, Architecture 82, Contracts 237 green; Infrastructure green apart from RichText timing tests under load (unrelated), new migration-on-populated-data and inverse-vs-set parity tests pass, `ScopedAccessStatementCountTests` set pin 4 -> 8 (four sight statements, still fixed). Deviations: (a) a query id naming nothing keeps today's answer while a hidden one answers 404 (the difference is observable); (b) a malformed/unknown path id is 404 for a non-administrator even when the role lacks the code (ADR-0029 § 8); (c) lists, selectors, `GetProjects` and the aggregate views are NOT narrowed (feat-061/062); (d) without `projectId` the candidates stay all active members; (e) the gate loads the set (a few statements) on every request that names an object.
