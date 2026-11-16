# feat-041-project-visibility

- type: feature
- size: L
- branch: feat/041-project-visibility
- stage: 8-review
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

Interim until feat-061 (review R-4): the header selectors are not narrowed
yet and still offer hidden objects; choosing one makes the aggregate views
(`AggregateWorkItemsPage`) answer 404 and show the generic error with
«Повторить», which never succeeds. Pages that address one object show the
not-found frame.

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
- 2026-11-16 [backend QA] qa.md Backend written: verdict fail, 0 blockers, 1 major (D-1: a hidden query id answers 404 while an id naming nothing answers 200, so existence is observable), 1 minor (D-2). Path ids, answer order, sight paths, visibility authorization, assignee, migration verified. Added ProjectSightQaTests (7 pass, D-1 test skipped until fixed). E2E: not asked, standing NO. Back to backend-developer for D-1.
- 2026-11-16 [backend] qa fix D-1 (major) and D-2: the gate now treats a query `teamId`, `departmentId` or `projectId` that is not a visible object (unknown, foreign, malformed or hidden) as the 404 of its kind, so a hidden id and one that names nothing are indistinguishable (`none` and an empty value pass; the administrator is unchanged); api-contract § 1 row 3 restated (mod-016 note not affected, it names no query ids). D-2 follows: a malformed or unknown `projectId` on assignee candidates is 404 `organizations.project.not_found`. The Skip on `ProjectSightQaTests.AHiddenQueryId_IsIndistinguishableFromOneThatNamesNothing` is removed; `ScopedRoutesQaTests.TheAssigneePicker_..._Is404` restated. Full Api.IntegrationTests: 3658 passed, 3 skipped (not ours), the one failure was a load-bound timing test (`Save_BurstOfLargeDocuments_KeepsTheApiResponsive`) that passes alone.
- 2026-11-16 [backend QA] re-check of 339f7a04 passed: D-1 and D-2 fixed (hidden, unknown and malformed query ids byte-identical 404; none/empty and administrator behave); Api.IntegrationTests Organizations+Boards+AssigneeMembership 2036 green; added a probe test. Verdict pass, 0 blockers/majors. stage: 6-frontend.
- 2026-11-16 [frontend] stage 6 done: plan-frontend.md; «Видим всей организации» (`ProjectVisibilityControl`, section «Видимость») in the project's «Параметры», read from `GET project` `visibleToOrganization`, written by `PUT …/visibility`, shown only for `organizations.visibility.write` in the project's `codes`; turning on asks (`ConfirmDialog`), turning off writes at once. The assignee picker already passed `projectId` and placed 409 `assignee_unavailable` under «Исполнитель» by code: no code change, test added. «Не найден» frames are chosen by error code (`notFoundLevel`), so a hidden object (404, same code as a missing one) already shows them: no screen changed, test added for the project page. Tests: organizations suite 945 (all green after the `paths.test.ts` code-catalog pin gained the new code), assignee test file green, new `ProjectVisibility.test.tsx` 5, ESLint 0 errors (1 warning in untouched AreaTypeMark.tsx), Prettier clean. `tsc -b`: only the environment's missing `@tiptap/*` modules (RichTextEditor, untouched), none in touched files. Deviations: (a) the task text says «Настройки», the panel and design § 6.1 say «Параметры» — the control sits there; (b) the existing not-found headings have no trailing period («Проект не найден»), left as they are; (c) the 403 text in the control is the existing «Недостаточно прав»; no copy invented: every string is design § 6.1's (◆ strings included), the refusal and 409 texts are the server's.
- 2026-11-16 [frontend QA] qa.md Frontend written: verdict pass, 0 blockers, 0 majors, no defects. Plan from design § 6.1, BR-TD-12..-16, US-6 before reading code; § 6.1 copy matches exactly; not-found headings without a period follow the shared NotFound convention (judged not a defect); placement in «Параметры», gating by organizations.visibility.write, confirm-on/no-confirm-off, refusals, 409 assignee verified. npm ci fixed the tsc -b @tiptap errors (clean now). Organizations suite + assignee file 946 pass, ESLint and Prettier clean. Added ProjectVisibility.qa.test.tsx (hidden team page, hidden area, refusal on turn-off; 3 pass). E2E: not asked, standing NO. stage: 8-review.
- 2026-11-16 [code-reviewer] round 1 (with the L security pass) in review.md: changes-requested; open 2 majors (R-1 inventory tests lost the role proof on object routes, R-2 sprint and work item not-found before 403 owed by ADR-0029 § 8 item 4 / mod-019 BR-SA-9), 6 minors (R-3..R-8, debts). Back to backend-developer.
- 2026-11-16 [backend] code review round 1 fixes (R-1, R-2, R-3, R-5, R-6, R-8; R-7 stays a debt, R-4 is the frontend's). R-1: the inventory's role cell runs again with real, visible ids (a world with a project visible to the organization, its linked team, the department, a sprint and an item; the administrator keeps fresh ids), and the not-found cell is a separate theory over the same routes; `ScopedAccessTests` keeps the 404 cell and adds the role cell with a visible project and its team. R-2: the gate resolves `sprintId` (inside the team) and `identifier` (project key and number inside the project, any state) after the project, team and area checks, so a missing or malformed one is `iterations.sprint.not_found` / `workitems.workitem.not_found` before any 403; api-contract § 1 item 3a, note in mod-019's status.md; the identifier's type key is still judged by the handler (a deliberate gap). R-3: `AccessSet.Knows*` deleted. R-5: contained part done: `IOrganizationAccess.SeesAsync` and a sight check on the body ids of `POST /teams/{t}/projects` and `POST /projects/{p}/teams` (hidden answers as unknown, before the second code); NOT done: the item ids of a team backlog reorder (the entries carry no project id, so refusing hidden-project items needs a read change; left for feat-062 with the team views) and body ids of other routes. R-6: information log line (project, value, actor) in `SetProjectVisibilityHandler`. R-8: qa.md says (2). Tests: new `ProjectSightNestedIdsTests` (R-2, R-5); four legacy tests restated for the 404-first order for missing items/hidden teams. Full Api.IntegrationTests before the restatements: 3809 passed, 3 skipped, 6 failed (4 restated and re-run green, 2 load-bound volume/timing tests unrelated: `AggregateViewVolumeTests`, `Save_BurstOfLargeDocuments`); Application 883 and Architecture 82 green.
- 2026-11-16 [code-reviewer] round 2 (delta read of ccb67b2d..4ff58635) in review.md: approved; R-1, R-2, R-3, R-4, R-6, R-8 closed; open minors (debts): R-5 rest (team reorder item ids and other body ids, feat-062), R-7 (per-request statements), R-9 (no test for the hidden body team on POST /projects/{p}/teams). Ready for gate-3.
