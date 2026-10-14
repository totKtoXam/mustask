# feat-035-story-acceptance-criteria

- type: feature
- size: S
- branch: feat/035-story-acceptance-criteria
- stage: complete
- epic: core/work-items
- deps: feat-022
- gates: gate-3

## Gates

gate-3: approved

## Plan

1. Add domain model `AcceptanceCriterion(Given, When, Then)` in `Mustask.Domain.WorkItems` and update `WorkItem.AcceptanceCriteria` to `IReadOnlyList<AcceptanceCriterion>?` (initialized to `[]` for User Story and `null` for other types).
2. Configure EF Core JSON persistence (`ValueConverter` + `ValueComparer`) on `work_items.acceptance_criteria` in `WorkItemConfiguration.cs` and projection in `WorkItemReadModel.cs`.
3. Update API DTOs (`AcceptanceCriterionRequest`, `AcceptanceCriterionView`) and validation rules in `WorkItemEndpoints.cs`, `WorkItemFieldsEdit.cs`, and `WorkItemFieldRules.cs`.
4. Implement `<AcceptanceCriteriaSection>` in `src/frontend/src/features/workitems/components/AcceptanceCriteriaSection.tsx` with dynamic GIVEN, WHEN, THEN scenario cards, add/remove controls, draft state (`DraftCriterion`, `criteriaDiffer`), and integration into `WorkItemFormPage.tsx` for User Story only.
5. Add unit, integration, and Vitest component tests covering backend serialization/validation and frontend scenario rendering, editing, and saving.

## Decisions

- D-1: `AcceptanceCriteria` is stored as a JSON array of `{ given, when, then }` objects in `work_items.acceptance_criteria` (`text` column), keeping `null` for non-Story types (Epic, Feature, Task, Bug).

## Log

- 2026-10-14 [human] Gate 3 approved, merged to main.
- 2026-10-13 [reviewer-3] Adversarial review round 3: fully verified repository via `.\scripts\verify.ps1` (RESULT: green, 229 frontend test suites / 3,651 passed, backend build & integration tests passed, zero UI kit violations, non-Story backward compatibility confirmed).
- 2026-10-13 [reviewer-2] Adversarial review round 2: stabilized criterion draft IDs to avoid remounting, added disabled guards, and expanded tests with edge cases.
- 2026-10-13 [reviewer] Fixed WorkItemCard.slots.test.tsx query for dynamic AC section; verified backend and frontend tests pass.
- 2026-10-13 [verify] `./scripts/verify.ps1` green (backend build & tests, frontend lint, build, and 152 test files / 1932 tests).
- 2026-10-13 [frontend-developer] Implemented `AcceptanceCriteriaSection.tsx`, updated `draft.ts`, `fields.ts`, `WorkItemFormPage.tsx`, and added `WorkItemFormPage.acceptanceCriteria.test.tsx`.
- 2026-10-13 [backend-developer] Implemented `AcceptanceCriterion`, EF Core JSON converter, DTO contracts, and domain/integration tests.
- 2026-10-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-035-story-acceptance-criteria

## Notes

Summary (from the queue line or an argument): User story acceptance criteria as a dynamic list of GIVEN, WHEN, THEN scenarios
