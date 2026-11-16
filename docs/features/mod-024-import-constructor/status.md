# mod-024-import-constructor

- type: module
- size: L
- branch: mod/024-import-constructor
- stage: 04-backend
- epic: core/work-items
- deps: mod-019,doc-017
- gates: gate-2, gate-3

## Gates

gate-2: approved (by the human in chat, 2026-11-13: «сразу переходи к реализации, в процессе разработки пересмотрим, что да как»)
gate-3: pending

## Plan

Route L from stage 3. The requirements are doc-017's, approved at its gate-1
on 2026-11-10: `docs/features/doc-017-import-export-constructor/` (`spec.md`
with BR-IE-*, the converter catalogue, the NFRs incl. BR-IE-21 limits and the
file-safety rules; stories US-1, US-2, US-3, US-4, US-7) and
`docs/adr/0042-import-batch-through-work-item-writes-with-undo.md`
(accepted). Their gate-1 is not repeated here.

Scope (the queue line): import templates per organization (create, edit,
copy, delete, author); .xlsx/.csv source reading; header-based column
mapping; recoding tables (per-type state entries); the converter catalogue;
MUSTASK-identifier matching; the dry run with its per-row report; permission
codes `imports.template.read`, `imports.template.write`,
`imports.import.run`, managers' scope included; limits and file-safety NFRs.

Out of scope: the import write, the batch and its undo (feat-067), export
(feat-069), external links (feat-066), participants (feat-068).

Stages: design (screens of the constructor, clickable HTML prototype, opened
in a browser before the gate) → gate-2 → backend (plan-backend.md,
api-contract.md, code, tests) → backend QA → frontend → frontend QA → code
review (one Opus pass with the security pass) → gate-3 → PR on GitHub with
Auto-fix, merged by the human with a merge commit, branch kept.

Economy mode: one agent per stage, one Opus review. E2E: no (standing answer).
Verification: affected tests locally, the full verify.sh in CI. Examples and
fixtures use made-up data only.

## Log

- 2026-11-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/mod-024-import-constructor
- 2026-11-13 [orchestrator] session bound to the worktree (git rev-parse --show-toplevel and git branch --show-current confirmed), cut from origin/main 1dbcbfee.
- 2026-11-13 [orchestrator] queue line taken by scripts/queue.sh take mod-024, PR #171 with squash auto-merge.
- 2026-11-13 [orchestrator] route confirmed by the human in chat: L from stage 3 (L thresholds: a new module; new permission codes with managers' scope); spec and stories inherited from doc-017 (gate-1 approved 2026-11-10), gate-1 not repeated; gates gate-2 and gate-3. Question 3.2 not applicable: only the module's own new tables.

- 2026-11-13 [ux-designer] stage 3 done: design.md (S1 list, S2 editor with 4 tabs and chain dialog, S3 dry run; all states, RU copy) and prototype/index.html (opened in headless Chrome, scripted walk-through without runtime errors; the human still looks at it in a browser). gate-2 stays pending.
- 2026-11-13 [orchestrator] prototype clicked through in the built-in browser (list, editor's four tabs, chain dialog, recoding, dry run): no runtime errors. Found: the dry-run report shows «Будет обновлён 30» while the template has no identifier column.
- 2026-11-13 [orchestrator] gate-2 asked; the human did not approve or reject: «тут сложно сказать, пока не прогонишь реальный парсер». Decided at the same time: «Импорт» in settings is shown to every member (imports.template.read); the 50-row preview is the first 50 rows of the selected result. gate-2 stays pending.
- 2026-11-13 [orchestrator] gate-2 approved by the human with a caveat: go straight to implementation and revisit the design during development, once the real parser runs (quote in Gates). Stage 4 started.
- 2026-11-13 [backend-dev] stage 4 PARTIAL (stopped on capacity, not finished). Commits: 7d9293c7 plan+contract; 68869a3a source reader .xlsx/.csv (62 tests green) + dev probe src/backend/tests/tools/ImportParserProbe; 61b37a46 field catalogue; 5457bbe2 converter engine (22 Domain tests green). NOT done: template model/validator, permission codes+bundles, persistence+migration, handlers, endpoints, dry run, report xlsx. Contract note: template settings gain ignoredColumns (string[]) for BR-IE-10. Deviation D-1 (planned): dry run will not cover description rich-text reduction, parent hierarchy rules, item concurrency. New NuGet DocumentFormat.OpenXml 3.5.1 in Directory.Packages.props (shared file, orchestrator to be told).
- 2026-11-13 [backend-dev] contract change (stage 4, frontend not started): TemplateSettings gains `ignoredColumns: string[]` (BR-IE-10), api-contract.md § 2.1 updated. Hyperlink implemented per spec (link target, else http(s) text, else empty); the lookup step takes `{entries, policy, defaultValue}`.
- 2026-11-13 [backend-dev] stage 4 DONE (backend complete; not pushed, no gate set). Commits on the branch after 5457bbe2, in order: template model and validator; permission codes and managers' scope (role-bundles.json regenerated); persistence and migration 20261113135300_AddImportTemplates (additive); dry run engine; reference reader and report writer; views, rights and read slices; application handlers (15 slices); api endpoints; endpoint integration tests; rights-before-lookup, route codes, inventories; template handler unit tests; served organization codes in access tests. Tests green: Domain 1029, Application 901, Infrastructure Imports 70, Contracts 234, Architecture 82, Api integration (Authorization + OpenApi + ImportEndpointTests + scoped routes) 450. Full verify.sh left to CI (economy mode). Deviations and decisions for the human: (1) contract shape kept: `notes` is an array of code strings (the panel renders their text from the shared error catalog), `rows[].item` is `{title, identifier}`; (2) route codes: GET templates/{id} declares `imports.template.write` (handler also lets a run-only user in), files routes declare `template.write`, dry-run routes declare `import.run`, all decided in the handler with a membership-only policy, because a manager's right sits on a project; the 403 of these routes is `imports.access_forbidden` (the access-inventory matrix test accepts it for `/imports/` routes); rights are checked before any id lookup, so a viewer gets 403 on an unknown id; (3) PUT/DELETE of a template by someone without `template.write` anywhere is 403 even for an unknown id; (4) upload checks the right before it reads the multipart body, and a body over 10 MB + 1 MB or a file over 10 MB is `imports.file.refused` / `too_large`; (5) D-1 stands: the dry run does not cover description rich-text reduction, parent hierarchy rules or item concurrency (there is no write yet); (6) values/chain-sample read the stored file with the request's source options and run the mapping on the whole file / first 5 rows; (7) `OrganizationAddressedRoutes` now has 115 routes, `DecidedInHandlerRoutes` 12 more, and `WorkItemWriteInventoryTests` 8 NamesNoWorkItem rows for the import writes. Shared-file change to tell the orchestrator about: `Directory.Packages.props` gained DocumentFormat.OpenXml 3.5.1. Left for later stages: frontend (feat-066/067), the import itself (writes), E2E.
- 2026-11-13 [qa-engineer] backend QA done, see qa.md (Backend). 25 tests added (Domain 14, Api integration 11); runs green. Defects: 4 major (QA D-1 assignee without project access accepted by the dry run; D-2 case-only change reads as unchanged; D-3 isoDate refuses a date-time; D-4 extract returns the whole match, not the first group) go back to the backend developer, 1 minor (D-5 expired files purged only on the next upload) carried to gate-3. Three tests are skipped with the defect id until fixed. E2E not run (standing answer: no). No gates set.
- 2026-11-13 [backend-dev] fixed QA-D-1..QA-D-4 (QA-D-5 carried to gate-3 / feat-067). QA-D-1: DryRunContext.CanMemberRead, built by ImportReferenceReader from the mod-019 AccessSet (role + area/project/team grants, 4 statements for all members); an assignee without workitems.workitem.read on the row's project gets imports.row.assignee_not_found (its text already says «или не имеет доступа к проекту»). QA-D-2: title/description/reproSteps/systemInfo compare ordinally; ids and enums still ignore case. QA-D-3: isoDate takes a date or date-time with/without offset; the date is the one written in the source (spec: time and offset dropped), no UTC shift. QA-D-4: extract returns the first group when the pattern has one, else the whole match (fixed in ConverterEngine, an explicit Group still wins). The 4 skipped QA tests run green; added tests for QA-D-1 (Domain + integration wiring), whole-match extract. Domain Imports 84, Application Imports 19, Api Features.Imports 23: green.
- 2026-11-13 [qa-engineer] backend QA re-check 1: QA-D-1..4 verified fixed (Domain 84, Application 19, Api Features.Imports 23, all green); QA-D-5 stays a minor for gate-3. Backend QA verdict: pass, 0 blockers, 0 majors. No gates set.
- 2026-11-13 [frontend-dev] stage 6 DONE (not pushed, no gate set). Commits: 83fa5e1f plan-frontend + file kit part (httpClient FormData/blob, FilePickerButton in the kit with test, showcase and guide block, error-code drift test); d3e02301 slice features/imports (S1 list, S2/S2b editor with four tabs, S2a chain dialog, S2c columns dialog, S3 check page) + route and «Импорт» settings entry + nav tests updated; 666c1019 slice tests. Tests green: features/imports 34, shared/api + shared/ui/actions + features/uikit + kitGuard + colour scan 673 together, src/app nav/layout sets; tsc -b and ESLint clean on touched files. Full verify.sh left to CI; E2E not run (standing answer: no). api-contract.md unchanged, no mismatch met. Shared changes: organizations barrel now exports useMembers, useOrganizationTeams, Member, TeamListItem (recoding targets read through public hooks, the contract has no reference endpoint). Deviations from design.md for the human: kit components replace the prototype's MUI parts; the tab error marker is a count on the tab; the values filter is a select; fields already taken are left out of the field select; no drag-and-drop area; the copy notice has no «Открыть» action; merge/constant are sources, not chain steps (contract); «Проверить файл» on an unsaved template is disabled without a tooltip; the CSV «Определено автоматически» text shows as a hint while the field is focused; the dry run progress is a skeleton, not a bar (kit rule). P-6: no «Будет обновлён» is invented, the panel shows the server's totals and the no_identifier_column note. Not covered by tests yet: ValuesTab and SourceTab upload flow (file reading) in component tests.
- 2026-11-13 [qa-engineer] frontend QA done, see qa.md (Frontend). 56 tests added (SourceTab 18, ValuesTab 14, editor 12, check page 12), 4 of them skipped with the defect id. Runs: 39 files / 929 tests green, 5 skipped (4 open defects), tsc and ESLint clean (2 warnings). Defects: 2 major (QA-FD-1 header-row/first-data-row blur never re-reads the columns; QA-FD-3 «По таблице» policy `keep` is refused by the API) go back to the frontend developer; 6 minors (QA-FD-2 sample lost after save, -4 stale report shows a false notice, -5..-8) carried to gate-3. The developer's deviations are acceptable except the `keep` policy. E2E not run (standing answer: no). No gates set.
- 2026-11-16 [frontend-dev] fixed QA-FD-1 (kit InputField now passes onBlur to the InputBase, kit test added), -2 (heldSample keeps the sample across the remount), -3 («По таблице» sends `empty`, text «Оставить пустым»), -4, -5, -6 (state options carry the type), -7; left QA-FD-8 (greying a stale report, «Уже сопоставлено»). The 4 skipped QA tests un-skipped and green. Runs: features/imports + shared/ui/fields + kitGuard 22 files / 946 passed / 6 skipped (pre-existing skipIf in FieldFrame.shared); tsc -b and eslint clean (0 warnings).

- 2026-11-16 [qa-engineer] frontend QA re-check 1: QA-FD-1..7 verified fixed (kit onBlur does not double-call inputProps.onBlur; `empty` matches the contract enum); new minor QA-FD-9 (heldSample leaks the abandoned `new` sample into the next new template, test skipped); QA-FD-8 stays open (minor, gate-3). 957 passed, 7 skipped in the affected sets, tsc and ESLint clean; result: passed with minors, see qa.md.

## Notes

Summary (from the queue line): Founds the import module: import templates per organization (create, edit, copy, delete), .xlsx/.csv source reading, column mapping, recoding tables, the converter catalogue, the identifier setting, and the dry run with its per-row report; registers the template and import permission codes, managers' scope included (doc-017 US-1..-4, US-7; ADR-0042)
